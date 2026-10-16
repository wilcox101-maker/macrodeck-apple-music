using MacroDeck.Sdk.MusicPlayer;

namespace AppleMusicControl.Playback;

/// <summary>Reads and drives the Apple Music media session through the operating system.</summary>
internal interface IMediaSessionSource
{
	/// <summary>Returns null when Apple Music has no media session (not running or nothing loaded).</summary>
	/// <exception cref="MediaControlUnavailableException">The operating system media controls cannot be used.</exception>
	Task<MediaSessionSnapshot?> GetAppleMusicSessionAsync(CancellationToken cancellationToken);

	Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken);

	Task<bool> SendAsync(SessionCommand command, CancellationToken cancellationToken);

	Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken);

	Task<bool> SetShuffleAsync(bool enabled, CancellationToken cancellationToken);

	Task<bool> SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken);
}

internal sealed class MediaControlUnavailableException : Exception
{
	public MediaControlUnavailableException()
	{
	}

	public MediaControlUnavailableException(string message) : base(message)
	{
	}

	public MediaControlUnavailableException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
