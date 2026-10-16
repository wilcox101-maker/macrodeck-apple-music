using MacroDeck.Sdk.MusicPlayer;

namespace AppleMusicControl.Playback;

/// <summary>Used on non-Windows builds, which exist only for development and tests.</summary>
internal sealed class UnsupportedMediaSessionSource : IMediaSessionSource
{
	public Task<MediaSessionSnapshot?> GetAppleMusicSessionAsync(CancellationToken cancellationToken)
		=> throw new MediaControlUnavailableException("Media session control is only supported on Windows.");

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken)
		=> Task.FromResult<MusicPlayerArtwork?>(null);

	public Task<bool> SendAsync(SessionCommand command, CancellationToken cancellationToken) => Task.FromResult(false);

	public Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken) => Task.FromResult(false);

	public Task<bool> SetShuffleAsync(bool enabled, CancellationToken cancellationToken) => Task.FromResult(false);

	public Task<bool> SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken) => Task.FromResult(false);
}
