#if WINDOWS
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AppleMusicControl.Playback;
using MacroDeck.Sdk.MusicPlayer;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace AppleMusicControl.Platform;

/// <summary>Apple Music through the Windows Global System Media Transport Controls.</summary>
internal sealed class GsmtcMediaSessionSource : IMediaSessionSource, IDisposable
{
	// Covers are small; a larger stream is not a cover and is not worth holding in memory.
	private const int MaxArtworkBytes = 2 * 1024 * 1024;

	private readonly SemaphoreSlim _managerGate = new(1, 1);
	private readonly SemaphoreSlim _artworkGate = new(1, 1);

	private GlobalSystemMediaTransportControlsSessionManager? _manager;
	private volatile TrackCache? _track;

	public async Task<MediaSessionSnapshot?> GetAppleMusicSessionAsync(CancellationToken cancellationToken)
	{
		var session = await FindSessionAsync(cancellationToken).ConfigureAwait(false);
		if (session is null)
		{
			return null;
		}

		try
		{
			var playback = session.GetPlaybackInfo();
			var timeline = session.GetTimelineProperties();
			var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
			var artworkId = await GetArtworkIdAsync(properties, cancellationToken).ConfigureAwait(false);

			return new MediaSessionSnapshot
			{
				SourceAppId = session.SourceAppUserModelId,
				Title = properties?.Title,
				Artist = properties?.Artist,
				Album = properties?.AlbumTitle,
				Status = MapStatus(playback.PlaybackStatus),
				Controls = MapControls(playback.Controls),
				ShuffleActive = playback.IsShuffleActive,
				RepeatMode = playback.AutoRepeatMode is { } repeat ? MapRepeat(repeat) : null,
				Position = timeline.Position,
				Duration = timeline.EndTime - timeline.StartTime,
				TimelineUpdatedAt = timeline.LastUpdatedTime,
				ArtworkId = artworkId
			};
		}
		catch (COMException ex)
		{
			// The session can close between listing and reading it; treat that as not running.
			if (await FindSessionAsync(cancellationToken).ConfigureAwait(false) is null)
			{
				return null;
			}

			throw new MediaControlUnavailableException("Reading the Apple Music session failed.", ex);
		}
	}

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken)
		=> Task.FromResult(_track is { } track && track.ArtworkId == artworkId ? track.Artwork : null);

	public async Task<bool> SendAsync(SessionCommand command, CancellationToken cancellationToken)
	{
		var session = await FindSessionAsync(cancellationToken).ConfigureAwait(false);
		if (session is null)
		{
			return false;
		}

		var operation = command switch
		{
			SessionCommand.Play => session.TryPlayAsync(),
			SessionCommand.Pause => session.TryPauseAsync(),
			SessionCommand.TogglePlayPause => session.TryTogglePlayPauseAsync(),
			SessionCommand.Next => session.TrySkipNextAsync(),
			SessionCommand.Previous => session.TrySkipPreviousAsync(),
			_ => throw new ArgumentOutOfRangeException(nameof(command))
		};
		return await operation.AsTask(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken)
	{
		var session = await FindSessionAsync(cancellationToken).ConfigureAwait(false);
		return session is not null
			&& await session.TryChangePlaybackPositionAsync(position.Ticks).AsTask(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> SetShuffleAsync(bool enabled, CancellationToken cancellationToken)
	{
		var session = await FindSessionAsync(cancellationToken).ConfigureAwait(false);
		return session is not null
			&& await session.TryChangeShuffleActiveAsync(enabled).AsTask(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken)
	{
		var session = await FindSessionAsync(cancellationToken).ConfigureAwait(false);
		var target = mode switch
		{
			RepeatMode.Track => MediaPlaybackAutoRepeatMode.Track,
			RepeatMode.Context => MediaPlaybackAutoRepeatMode.List,
			_ => MediaPlaybackAutoRepeatMode.None
		};
		return session is not null
			&& await session.TryChangeAutoRepeatModeAsync(target).AsTask(cancellationToken).ConfigureAwait(false);
	}

	public void Dispose()
	{
		_managerGate.Dispose();
		_artworkGate.Dispose();
	}

	private async Task<GlobalSystemMediaTransportControlsSession?> FindSessionAsync(CancellationToken cancellationToken)
	{
		var manager = await GetManagerAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return AppleMusicSessionSelector.Select(
				manager.GetSessions(),
				s => s.SourceAppUserModelId,
				s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
		}
		catch (COMException ex)
		{
			throw new MediaControlUnavailableException("Listing media sessions failed.", ex);
		}
	}

	private async Task<GlobalSystemMediaTransportControlsSessionManager> GetManagerAsync(CancellationToken cancellationToken)
	{
		if (_manager is { } existing)
		{
			return existing;
		}

		await _managerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_manager is null)
			{
				if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
				{
					throw new MediaControlUnavailableException("Windows 10 version 1809 or later is required.");
				}

				_manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()
					.AsTask(cancellationToken).ConfigureAwait(false);
			}

			return _manager;
		}
		catch (Exception ex) when (ex is COMException or TypeLoadException or PlatformNotSupportedException)
		{
			throw new MediaControlUnavailableException("The Windows media session manager is unavailable.", ex);
		}
		finally
		{
			_managerGate.Release();
		}
	}

	/// <summary>Reads the cover once per track; the id is a content hash so equal covers share it.</summary>
	private async Task<string?> GetArtworkIdAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? properties,
		CancellationToken cancellationToken)
	{
		if (properties is null)
		{
			return null;
		}

		var key = $"{properties.Title}\u001f{properties.Artist}\u001f{properties.AlbumTitle}";
		if (_track is { } cached && cached.Key == key)
		{
			return cached.ArtworkId;
		}

		await _artworkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_track is { } again && again.Key == key)
			{
				return again.ArtworkId;
			}

			var artwork = await ReadThumbnailAsync(properties.Thumbnail, cancellationToken).ConfigureAwait(false);
			if (artwork is null)
			{
				// Apps often publish the cover after the title, so try again on the next read.
				return null;
			}

			var id = Convert.ToHexStringLower(SHA256.HashData(artwork.Data))[..16];
			_track = new TrackCache(key, id, artwork);
			return id;
		}
		finally
		{
			_artworkGate.Release();
		}
	}

	private static async Task<MusicPlayerArtwork?> ReadThumbnailAsync(IRandomAccessStreamReference? thumbnail,
		CancellationToken cancellationToken)
	{
		if (thumbnail is null)
		{
			return null;
		}

		try
		{
			using var stream = await thumbnail.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
			if (stream.Size is 0 or > MaxArtworkBytes)
			{
				return null;
			}

			using var input = stream.AsStreamForRead();
			using var buffer = new MemoryStream((int)stream.Size);
			await input.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
			var data = buffer.ToArray();
			return ArtworkFormat.Detect(data) is { } mime ? new MusicPlayerArtwork(data, mime) : null;
		}
		catch (COMException)
		{
			return null;
		}
	}

	private static SessionPlaybackStatus MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
	{
		GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => SessionPlaybackStatus.Opened,
		GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => SessionPlaybackStatus.Changing,
		GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => SessionPlaybackStatus.Stopped,
		GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => SessionPlaybackStatus.Playing,
		GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => SessionPlaybackStatus.Paused,
		_ => SessionPlaybackStatus.Closed
	};

	private static SessionControls MapControls(GlobalSystemMediaTransportControlsSessionPlaybackControls c)
	{
		var controls = SessionControls.None;
		if (c.IsPlayEnabled) controls |= SessionControls.Play;
		if (c.IsPauseEnabled) controls |= SessionControls.Pause;
		if (c.IsPlayPauseToggleEnabled) controls |= SessionControls.TogglePlayPause;
		if (c.IsNextEnabled) controls |= SessionControls.Next;
		if (c.IsPreviousEnabled) controls |= SessionControls.Previous;
		if (c.IsPlaybackPositionEnabled) controls |= SessionControls.Seek;
		if (c.IsShuffleEnabled) controls |= SessionControls.Shuffle;
		if (c.IsRepeatEnabled) controls |= SessionControls.Repeat;
		return controls;
	}

	private static RepeatMode MapRepeat(MediaPlaybackAutoRepeatMode mode) => mode switch
	{
		MediaPlaybackAutoRepeatMode.Track => RepeatMode.Track,
		MediaPlaybackAutoRepeatMode.List => RepeatMode.Context,
		_ => RepeatMode.Off
	};

	private sealed record TrackCache(string Key, string ArtworkId, MusicPlayerArtwork Artwork);
}
#endif
