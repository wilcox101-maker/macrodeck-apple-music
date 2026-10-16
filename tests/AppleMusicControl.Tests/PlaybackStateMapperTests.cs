using AppleMusicControl.Playback;
using MacroDeck.Sdk.MusicPlayer;
using NUnit.Framework;

namespace AppleMusicControl.Tests;

[TestFixture]
public sealed class PlaybackStateMapperTests
{
	[Test]
	public void No_session_is_unavailable_with_not_running_message_but_keeps_volume()
	{
		var state = PlaybackStateMapper.Map(null, 55, Sessions.Reported);

		Assert.That(state.IsConnected, Is.False);
		Assert.That(state.IsUnavailable, Is.True);
		Assert.That(state.StatusMessage, Is.EqualTo(PlaybackStateMapper.NotRunningMessage));
		Assert.That(state.VolumePercent, Is.EqualTo(55));
	}

	[Test]
	public void Playing_session_maps_metadata_shuffle_and_repeat()
	{
		var state = PlaybackStateMapper.Map(Sessions.Playing(), 40, Sessions.Reported);

		Assert.That(state.IsConnected, Is.True);
		Assert.That(state.PlaybackState, Is.EqualTo(PlaybackState.Playing));
		Assert.That(state.TrackName, Is.EqualTo("Song"));
		Assert.That(string.Join(", ", state.Artists), Is.EqualTo("Band"));
		Assert.That(state.AlbumName, Is.EqualTo("Record"));
		Assert.That(state.ArtworkId, Is.EqualTo("abc"));
		Assert.That(state.ShuffleEnabled, Is.True);
		Assert.That(state.RepeatMode, Is.EqualTo(RepeatMode.Context));
		Assert.That(state.Duration, Is.EqualTo(TimeSpan.FromSeconds(200)));
	}

	[Test]
	public void Blank_artist_and_album_become_absent()
	{
		var session = Sessions.Playing() with { Artist = " ", Album = "" };

		var state = PlaybackStateMapper.Map(session, null, Sessions.Reported);

		Assert.That(state.Artists, Is.Empty);
		Assert.That(state.AlbumName, Is.Null);
	}

	[Test]
	public void Non_playing_statuses_map_to_paused_or_stopped()
	{
		Assert.That(PlaybackStateMapper.MapStatus(SessionPlaybackStatus.Paused), Is.EqualTo(PlaybackState.Paused));
		Assert.That(PlaybackStateMapper.MapStatus(SessionPlaybackStatus.Stopped), Is.EqualTo(PlaybackState.Stopped));
		Assert.That(PlaybackStateMapper.MapStatus(SessionPlaybackStatus.Changing), Is.EqualTo(PlaybackState.Stopped));
		Assert.That(PlaybackStateMapper.MapStatus(SessionPlaybackStatus.Closed), Is.EqualTo(PlaybackState.Stopped));
	}

	[Test]
	public void Playing_position_advances_with_time_since_the_report()
	{
		var position = PlaybackStateMapper.CurrentPosition(Sessions.Playing(), Sessions.Reported.AddSeconds(12));

		Assert.That(position, Is.EqualTo(TimeSpan.FromSeconds(42)));
	}

	[Test]
	public void Paused_position_does_not_advance()
	{
		var session = Sessions.Playing() with { Status = SessionPlaybackStatus.Paused };

		Assert.That(PlaybackStateMapper.CurrentPosition(session, Sessions.Reported.AddMinutes(5)), Is.EqualTo(TimeSpan.FromSeconds(30)));
	}

	[Test]
	public void Extrapolated_position_never_passes_the_duration()
	{
		var position = PlaybackStateMapper.CurrentPosition(Sessions.Playing(), Sessions.Reported.AddHours(1));

		Assert.That(position, Is.EqualTo(TimeSpan.FromSeconds(200)));
	}

	[Test]
	public void Zero_duration_is_reported_as_unknown()
	{
		var state = PlaybackStateMapper.Map(Sessions.Playing() with { Duration = TimeSpan.Zero }, null, Sessions.Reported);

		Assert.That(state.Duration, Is.Null);
	}
}

[TestFixture]
public sealed class AppleMusicSessionSelectorTests
{
	private sealed record Session(string Id, bool Playing);

	private static Session? Select(params Session[] sessions)
		=> AppleMusicSessionSelector.Select(sessions, s => s.Id, s => s.Playing);

	[Test]
	public void Picks_apple_music_among_other_apps()
	{
		var selected = Select(new Session("Spotify.exe", true), new Session("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", false),
			new Session("MSEdge", true));

		Assert.That(selected?.Id, Does.Contain("AppleMusic"));
	}

	[Test]
	public void Returns_none_when_apple_music_has_no_session()
	{
		Assert.That(Select(new Session("Spotify.exe", true), new Session("MSEdge", false)), Is.Null);
	}

	[Test]
	public void Prefers_the_playing_apple_music_session()
	{
		var selected = Select(new Session("AppleInc.AppleMusicWin_a!App", false), new Session("AppleInc.AppleMusicWin_b!App", true));

		Assert.That(selected?.Id, Is.EqualTo("AppleInc.AppleMusicWin_b!App"));
	}

	[Test]
	public void Itunes_is_not_apple_music()
	{
		Assert.That(AppleMusicSessionSelector.IsAppleMusic("iTunes.exe"), Is.False);
	}
}

[TestFixture]
public sealed class ArtworkFormatTests
{
	[Test]
	public void Detects_png_and_jpeg()
	{
		Assert.That(ArtworkFormat.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D]), Is.EqualTo("image/png"));
		Assert.That(ArtworkFormat.Detect([0xFF, 0xD8, 0xFF, 0xE0]), Is.EqualTo("image/jpeg"));
	}

	[Test]
	public void Unknown_or_empty_data_is_not_artwork()
	{
		Assert.That(ArtworkFormat.Detect([]), Is.Null);
		Assert.That(ArtworkFormat.Detect("<svg"u8), Is.Null);
	}
}
