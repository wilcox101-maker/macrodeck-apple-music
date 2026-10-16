using AppleMusicControl.Volume;
using MacroDeck.Sdk.Logging;
using MacroDeck.Sdk.MusicPlayer;
using Serilog;

namespace AppleMusicControl.Playback;

/// <summary>The one Apple Music player. State reads are cached briefly because the widget poll and every
/// variable read arrive independently and each would otherwise query the OS.</summary>
internal sealed class AppleMusicPlayer : IMusicPlayer
{
	internal static readonly TimeSpan CacheLifetime = TimeSpan.FromMilliseconds(250);

	private readonly IMediaSessionSource _source;
	private readonly ISystemVolume _volume;
	private readonly TimeProvider _time;
	private readonly ILogger _logger;
	private readonly FailureEpisodeTracker _failures;

	private volatile CachedState? _cached;

	public AppleMusicPlayer(IMediaSessionSource source, ISystemVolume volume, TimeProvider time, ILogger logger)
	{
		_source = source;
		_volume = volume;
		_time = time;
		_logger = logger.ForContext<AppleMusicPlayer>();
		_failures = new FailureEpisodeTracker(time: time);
	}

	public async Task<MusicPlayerState> GetStateAsync(CancellationToken cancellationToken = default)
	{
		var now = _time.GetUtcNow();
		if (_cached is { } cached && now - cached.At < CacheLifetime)
		{
			return cached.State;
		}

		var state = await ReadStateAsync(now, cancellationToken).ConfigureAwait(false);
		_cached = new CachedState(state, now);
		return state;
	}

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken = default)
		=> _source.GetArtworkAsync(artworkId, cancellationToken);

	public Task PlayAsync(CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Play, "play", s => s.SendAsync(SessionCommand.Play, cancellationToken), cancellationToken);

	public Task PauseAsync(CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Pause, "pause", s => s.SendAsync(SessionCommand.Pause, cancellationToken), cancellationToken);

	public async Task TogglePlayPauseAsync(CancellationToken cancellationToken = default)
	{
		var session = await RequireSessionAsync(cancellationToken).ConfigureAwait(false);

		// Some apps only advertise separate play and pause buttons, so fall back to the one that applies.
		var (control, command) = session.Controls.HasFlag(SessionControls.TogglePlayPause)
			? (SessionControls.TogglePlayPause, SessionCommand.TogglePlayPause)
			: session.Status == SessionPlaybackStatus.Playing
				? (SessionControls.Pause, SessionCommand.Pause)
				: (SessionControls.Play, SessionCommand.Play);

		await RunAsync(session, control, "toggle play/pause", s => s.SendAsync(command, cancellationToken)).ConfigureAwait(false);
	}

	public Task NextAsync(CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Next, "next track", s => s.SendAsync(SessionCommand.Next, cancellationToken), cancellationToken);

	public Task PreviousAsync(CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Previous, "previous track", s => s.SendAsync(SessionCommand.Previous, cancellationToken), cancellationToken);

	public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Seek, "seek", s => s.SeekAsync(position, cancellationToken), cancellationToken);

	public Task SetShuffleAsync(bool enabled, CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Shuffle, "shuffle", s => s.SetShuffleAsync(enabled, cancellationToken), cancellationToken);

	public Task SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken = default)
		=> CommandAsync(SessionControls.Repeat, "repeat", s => s.SetRepeatModeAsync(mode, cancellationToken), cancellationToken);

	/// <summary>Sets the Windows master volume; it does not depend on Apple Music running.</summary>
	public Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_volume.SetPercent(Math.Clamp(volumePercent, 0, 100));
		Invalidate();
		return Task.CompletedTask;
	}

	private async Task<MusicPlayerState> ReadStateAsync(DateTimeOffset now, CancellationToken cancellationToken)
	{
		var volume = _volume.TryGetPercent();
		try
		{
			var session = await _source.GetAppleMusicSessionAsync(cancellationToken).ConfigureAwait(false);
			if (_failures.RecordSuccess() is { } ended)
			{
				_logger.Information("Media controls available again after {Duration} and {Failures} failures", ended.Duration, ended.Failures);
			}

			return PlaybackStateMapper.Map(session, volume, now);
		}
		catch (MediaControlUnavailableException ex)
		{
			var signal = _failures.RecordFailure(ex.Message);
			switch (signal.Kind)
			{
				case FailureEpisodeSignalKind.Onset:
					_logger.Warning(ex, "Media controls unavailable");
					break;
				case FailureEpisodeSignalKind.SummaryDue:
					_logger.Warning("Media controls still unavailable after {Duration} ({Failures} attempts): {Error}",
						signal.Duration, signal.ConsecutiveFailures, signal.LastError);
					break;
				default:
					_logger.Debug("Media controls unavailable: {Error}", signal.LastError);
					break;
			}

			return MusicPlayerState.Unavailable(PlaybackStateMapper.ControlsUnavailableMessage) with { VolumePercent = volume };
		}
	}

	private async Task CommandAsync(SessionControls control, string name, Func<IMediaSessionSource, Task<bool>> command,
		CancellationToken cancellationToken)
	{
		var session = await RequireSessionAsync(cancellationToken).ConfigureAwait(false);
		await RunAsync(session, control, name, command).ConfigureAwait(false);
	}

	private async Task<MediaSessionSnapshot> RequireSessionAsync(CancellationToken cancellationToken)
		=> await _source.GetAppleMusicSessionAsync(cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Apple Music is not running or has nothing loaded.");

	private async Task RunAsync(MediaSessionSnapshot session, SessionControls control, string name,
		Func<IMediaSessionSource, Task<bool>> command)
	{
		if (!session.Controls.HasFlag(control))
		{
			throw new NotSupportedException($"Apple Music does not currently allow {name}.");
		}

		// A false result means the app refused; reporting success would make the button lie.
		if (!await command(_source).ConfigureAwait(false))
		{
			throw new InvalidOperationException($"Apple Music rejected {name}.");
		}

		Invalidate();
	}

	private void Invalidate() => _cached = null;

	private sealed record CachedState(MusicPlayerState State, DateTimeOffset At);
}
