using AppleMusicControl.Playback;
using AppleMusicControl.Volume;
using MacroDeck.Sdk.MusicPlayer;

namespace AppleMusicControl.Tests;

internal sealed class FakeMediaSessionSource : IMediaSessionSource
{
	public MediaSessionSnapshot? Session { get; set; }

	public bool Unavailable { get; set; }

	public bool CommandResult { get; set; } = true;

	public int Reads { get; private set; }

	public List<string> Commands { get; } = [];

	public MusicPlayerArtwork? Artwork { get; set; }

	public Task<MediaSessionSnapshot?> GetAppleMusicSessionAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Reads++;
		return Unavailable ? throw new MediaControlUnavailableException("test outage") : Task.FromResult(Session);
	}

	public Task<MusicPlayerArtwork?> GetArtworkAsync(string artworkId, CancellationToken cancellationToken)
		=> Task.FromResult(Session?.ArtworkId == artworkId ? Artwork : null);

	public Task<bool> SendAsync(SessionCommand command, CancellationToken cancellationToken) => Record(command.ToString());

	public Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken) => Record($"Seek {position.TotalSeconds}");

	public Task<bool> SetShuffleAsync(bool enabled, CancellationToken cancellationToken) => Record($"Shuffle {enabled}");

	public Task<bool> SetRepeatModeAsync(RepeatMode mode, CancellationToken cancellationToken) => Record($"Repeat {mode}");

	private Task<bool> Record(string command)
	{
		Commands.Add(command);
		return Task.FromResult(CommandResult);
	}
}

internal sealed class FakeSystemVolume : ISystemVolume
{
	public int? Percent { get; set; } = 40;

	public int? TryGetPercent() => Percent;

	public void SetPercent(int percent) => Percent = percent;
}

internal static class Sessions
{
	public static readonly DateTimeOffset Reported = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

	public static MediaSessionSnapshot Playing(SessionControls controls = AllControls) => new()
	{
		SourceAppId = "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
		Title = "Song",
		Artist = "Band",
		Album = "Record",
		Status = SessionPlaybackStatus.Playing,
		Controls = controls,
		ShuffleActive = true,
		RepeatMode = RepeatMode.Context,
		Position = TimeSpan.FromSeconds(30),
		Duration = TimeSpan.FromSeconds(200),
		TimelineUpdatedAt = Reported,
		ArtworkId = "abc"
	};

	public const SessionControls AllControls = SessionControls.Play | SessionControls.Pause | SessionControls.TogglePlayPause
		| SessionControls.Next | SessionControls.Previous | SessionControls.Seek | SessionControls.Shuffle | SessionControls.Repeat;
}
