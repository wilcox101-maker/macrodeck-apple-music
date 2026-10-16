using MacroDeck.Sdk.MusicPlayer;

namespace AppleMusicControl.Playback;

internal static class PlaybackStateMapper
{
	public const string NotRunningMessage = "Apple Music not running";
	public const string ControlsUnavailableMessage = "Media controls unavailable";

	public static MusicPlayerState Map(MediaSessionSnapshot? session, int? volumePercent, DateTimeOffset now)
	{
		if (session is null)
		{
			return MusicPlayerState.Unavailable(NotRunningMessage) with { VolumePercent = volumePercent };
		}

		return new MusicPlayerState
		{
			IsConnected = true,
			PlaybackState = MapStatus(session.Status),
			TrackName = Blank(session.Title),
			Artists = Blank(session.Artist) is { } artist ? [artist] : [],
			AlbumName = Blank(session.Album),
			ArtworkId = session.ArtworkId,
			Position = CurrentPosition(session, now),
			Duration = session.Duration is { } d && d > TimeSpan.Zero ? d : null,
			VolumePercent = volumePercent,
			ShuffleEnabled = session.ShuffleActive ?? false,
			RepeatMode = session.RepeatMode ?? RepeatMode.Off
		};
	}

	public static PlaybackState MapStatus(SessionPlaybackStatus status) => status switch
	{
		SessionPlaybackStatus.Playing => PlaybackState.Playing,
		SessionPlaybackStatus.Paused => PlaybackState.Paused,
		_ => PlaybackState.Stopped
	};

	/// <summary>The OS reports the position only when it changes, so a playing track is extrapolated from
	/// the time of that report.</summary>
	public static TimeSpan? CurrentPosition(MediaSessionSnapshot session, DateTimeOffset now)
	{
		if (session.Position is not { } position)
		{
			return null;
		}

		if (session.Status == SessionPlaybackStatus.Playing && session.TimelineUpdatedAt is { } updatedAt && now > updatedAt)
		{
			position += now - updatedAt;
		}

		if (session.Duration is { } duration && duration > TimeSpan.Zero && position > duration)
		{
			position = duration;
		}

		return position < TimeSpan.Zero ? TimeSpan.Zero : position;
	}

	private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
