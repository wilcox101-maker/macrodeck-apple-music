using MacroDeck.Sdk.MusicPlayer;

namespace AppleMusicControl.Playback;

internal enum SessionPlaybackStatus
{
	Closed,
	Opened,
	Changing,
	Stopped,
	Playing,
	Paused
}

[Flags]
internal enum SessionControls
{
	None = 0,
	Play = 1,
	Pause = 2,
	TogglePlayPause = 4,
	Next = 8,
	Previous = 16,
	Seek = 32,
	Shuffle = 64,
	Repeat = 128
}

internal enum SessionCommand
{
	Play,
	Pause,
	TogglePlayPause,
	Next,
	Previous
}

/// <summary>What the operating system reports about the Apple Music media session at one moment.</summary>
internal sealed record MediaSessionSnapshot
{
	public required string SourceAppId { get; init; }

	public string? Title { get; init; }

	public string? Artist { get; init; }

	public string? Album { get; init; }

	public SessionPlaybackStatus Status { get; init; }

	public SessionControls Controls { get; init; }

	public bool? ShuffleActive { get; init; }

	public RepeatMode? RepeatMode { get; init; }

	public TimeSpan? Position { get; init; }

	public TimeSpan? Duration { get; init; }

	public DateTimeOffset? TimelineUpdatedAt { get; init; }

	public string? ArtworkId { get; init; }
}
