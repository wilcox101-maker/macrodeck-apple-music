using AppleMusicControl.Playback;
using MacroDeck.Plugin.Testing;
using MacroDeck.Sdk.MusicPlayer;
using NUnit.Framework;
using Serilog;

namespace AppleMusicControl.Tests;

[TestFixture]
public sealed class AppleMusicPlayerTests
{
	private FakeMediaSessionSource _source = null!;
	private FakeSystemVolume _volume = null!;
	private ManualTimeProvider _clock = null!;
	private AppleMusicPlayer _player = null!;

	[SetUp]
	public void SetUp()
	{
		_source = new FakeMediaSessionSource { Session = Sessions.Playing() };
		_volume = new FakeSystemVolume();
		_clock = new ManualTimeProvider(Sessions.Reported);
		_player = new AppleMusicPlayer(_source, _volume, _clock, new LoggerConfiguration().CreateLogger());
	}

	[Test]
	public async Task State_includes_system_volume()
	{
		var state = await _player.GetStateAsync();

		Assert.That(state.VolumePercent, Is.EqualTo(40));
		Assert.That(state.TrackName, Is.EqualTo("Song"));
	}

	[Test]
	public async Task Reads_within_the_cache_lifetime_query_the_os_once()
	{
		await _player.GetStateAsync();
		await _player.GetStateAsync();
		_clock.Advance(AppleMusicPlayer.CacheLifetime);
		await _player.GetStateAsync();

		Assert.That(_source.Reads, Is.EqualTo(2));
	}

	[Test]
	public async Task Os_outage_reads_as_unavailable_instead_of_throwing()
	{
		_source.Unavailable = true;

		var state = await _player.GetStateAsync();

		Assert.That(state.IsUnavailable, Is.True);
		Assert.That(state.StatusMessage, Is.EqualTo(PlaybackStateMapper.ControlsUnavailableMessage));
		Assert.That(state.VolumePercent, Is.EqualTo(40));
	}

	[Test]
	public async Task Unexpected_os_error_reads_as_unavailable()
	{
		_source.Failure = new InvalidCastException("broken session");

		var state = await _player.GetStateAsync();

		Assert.That(state.IsUnavailable, Is.True);
	}

	[Test]
	public async Task Next_sends_the_command()
	{
		await _player.NextAsync();

		Assert.That(string.Join(", ", _source.Commands), Is.EqualTo("Next"));
	}

	[Test]
	public void Command_fails_when_apple_music_is_not_running()
	{
		_source.Session = null;

		Assert.ThrowsAsync<InvalidOperationException>(() => _player.PlayAsync());
		Assert.That(_source.Commands, Is.Empty);
	}

	[Test]
	public void Command_fails_when_apple_music_rejects_it()
	{
		_source.CommandResult = false;

		Assert.ThrowsAsync<InvalidOperationException>(() => _player.PreviousAsync());
	}

	[Test]
	public void Shuffle_fails_when_apple_music_does_not_offer_it()
	{
		_source.Session = Sessions.Playing(Sessions.AllControls & ~SessionControls.Shuffle);

		Assert.ThrowsAsync<NotSupportedException>(() => _player.SetShuffleAsync(false));
		Assert.That(_source.Commands, Is.Empty);
	}

	[Test]
	public async Task Repeat_and_seek_pass_their_values()
	{
		await _player.SetRepeatModeAsync(RepeatMode.Track);
		await _player.SeekAsync(TimeSpan.FromSeconds(90));

		Assert.That(string.Join(", ", _source.Commands), Is.EqualTo("Repeat Track, Seek 90"));
	}

	[Test]
	public async Task Toggle_falls_back_to_pause_while_playing_without_a_toggle_control()
	{
		_source.Session = Sessions.Playing(SessionControls.Play | SessionControls.Pause);

		await _player.TogglePlayPauseAsync();

		Assert.That(string.Join(", ", _source.Commands), Is.EqualTo("Pause"));
	}

	[Test]
	public async Task Toggle_falls_back_to_play_while_paused_without_a_toggle_control()
	{
		_source.Session = Sessions.Playing(SessionControls.Play | SessionControls.Pause) with { Status = SessionPlaybackStatus.Paused };

		await _player.TogglePlayPauseAsync();

		Assert.That(string.Join(", ", _source.Commands), Is.EqualTo("Play"));
	}

	[Test]
	public async Task Volume_is_clamped_and_works_without_apple_music()
	{
		_source.Session = null;

		await _player.SetVolumeAsync(150);

		Assert.That(_volume.Percent, Is.EqualTo(100));
	}

	[Test]
	public async Task A_command_refreshes_the_cached_state()
	{
		await _player.GetStateAsync();
		await _player.SetVolumeAsync(10);

		var state = await _player.GetStateAsync();

		Assert.That(state.VolumePercent, Is.EqualTo(10));
	}

	[Test]
	public void Cancellation_is_honored()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		Assert.CatchAsync<OperationCanceledException>(() => _player.GetStateAsync(cts.Token));
	}
}

[TestFixture]
public sealed class NowPlayingVariablesTests
{
	private static readonly MusicPlayerState Playing = PlaybackStateMapper.Map(Sessions.Playing(), 40, Sessions.Reported);

	[TestCase("track", "Song")]
	[TestCase("artist", "Band")]
	[TestCase("album", "Record")]
	[TestCase("state", "playing")]
	[TestCase("repeat", "context")]
	public void Text_variables_read_the_state(string id, string expected)
	{
		Assert.That(NowPlayingVariables.Read(id, Playing).Value, Is.EqualTo(expected));
	}

	[Test]
	public void Numeric_and_boolean_variables_read_the_state()
	{
		Assert.That(NowPlayingVariables.Read("position", Playing).Value, Is.EqualTo(30d));
		Assert.That(NowPlayingVariables.Read("duration", Playing).Value, Is.EqualTo(200d));
		Assert.That(NowPlayingVariables.Read("is-playing", Playing).Value, Is.EqualTo(true));
		Assert.That(NowPlayingVariables.Read("shuffle", Playing).Value, Is.EqualTo(true));
		Assert.That(NowPlayingVariables.Read("volume", Playing).Value, Is.EqualTo(40));
	}

	[Test]
	public void Track_variables_are_unavailable_when_apple_music_is_not_running()
	{
		var state = PlaybackStateMapper.Map(null, 40, Sessions.Reported);

		Assert.That(NowPlayingVariables.Read("track", state), Is.EqualTo(MacroDeck.Sdk.Variables.VariableReading.Unavailable));
		Assert.That(NowPlayingVariables.Read("state", state).Value, Is.EqualTo("not-running"));
		Assert.That(NowPlayingVariables.Read("connected", state).Value, Is.EqualTo(false));
		Assert.That(NowPlayingVariables.Read("volume", state).Value, Is.EqualTo(40));
	}

	[Test]
	public void Writable_variables_report_a_default_without_session_or_endpoint()
	{
		var state = PlaybackStateMapper.Map(null, null, Sessions.Reported);

		Assert.That(NowPlayingVariables.Read("volume", state).Value, Is.EqualTo(0));
		Assert.That(NowPlayingVariables.Read("position", state).Value, Is.EqualTo(0d));
	}
}
