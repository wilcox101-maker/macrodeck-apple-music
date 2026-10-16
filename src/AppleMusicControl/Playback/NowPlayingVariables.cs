using MacroDeck.Sdk.MusicPlayer;
using MacroDeck.Sdk.Variables;

namespace AppleMusicControl.Playback;

internal static class NowPlayingVariables
{
	public static VariableReading Read(string localId, MusicPlayerState state)
	{
		if (localId == "volume")
		{
			return state.VolumePercent is { } volume ? VariableReading.Of(volume, 0, 100, 1) : VariableReading.Unavailable;
		}

		if (localId == "connected")
		{
			return VariableReading.Of(state.IsConnected);
		}

		if (localId == "state")
		{
			return VariableReading.Of(StateToken(state));
		}

		if (!state.IsConnected)
		{
			return VariableReading.Unavailable;
		}

		return localId switch
		{
			"track" => Text(state.TrackName),
			"artist" => Text(state.Artists.Count > 0 ? string.Join(", ", state.Artists) : null),
			"album" => Text(state.AlbumName),
			"is-playing" => VariableReading.Of(state.PlaybackState == PlaybackState.Playing),
			"position" => state.Position is { } p ? VariableReading.Of(Math.Floor(p.TotalSeconds)) : VariableReading.Unavailable,
			"duration" => state.Duration is { } d ? VariableReading.Of(Math.Floor(d.TotalSeconds)) : VariableReading.Unavailable,
			"shuffle" => VariableReading.Of(state.ShuffleEnabled),
			"repeat" => VariableReading.Of(RepeatToken(state.RepeatMode)),
			_ => VariableReading.Unavailable
		};
	}

	public static string StateToken(MusicPlayerState state)
	{
		if (!state.IsConnected)
		{
			return state.StatusMessage == PlaybackStateMapper.NotRunningMessage ? "not-running" : "unavailable";
		}

		return state.PlaybackState switch
		{
			PlaybackState.Playing => "playing",
			PlaybackState.Paused => "paused",
			_ => "stopped"
		};
	}

	public static string RepeatToken(RepeatMode mode) => mode switch
	{
		RepeatMode.Track => "track",
		RepeatMode.Context => "context",
		_ => "off"
	};

	private static VariableReading Text(string? value) => value is null ? VariableReading.Unavailable : VariableReading.Of(value);
}
