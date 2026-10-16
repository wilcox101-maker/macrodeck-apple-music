using AppleMusicControl.Playback;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.MusicPlayer;
using MacroDeck.Sdk.MusicPlayer.Actions;
using MacroDeck.Sdk.Variables;
using Serilog;

namespace AppleMusicControl;

/// <summary>
/// Exposes Apple Music as one music player (the deck's Music Player widget and the standard transport
/// actions) plus now-playing variables, all read from the same <see cref="AppleMusicPlayer"/>.
/// </summary>
internal sealed class AppleMusicIntegration : IPluginIntegration, IMusicPlayerProvider, IVariableProvider
{
	internal const string InstanceId = "default";

	private readonly ILogger _logger;
	private readonly AppleMusicPlayer _player;

	public AppleMusicIntegration(AppleMusicPlayer player, ILogger logger)
	{
		_player = player;
		_logger = logger.ForContext<AppleMusicIntegration>();
		Actions = MusicPlayerActions.Common(ResolvePlayer, GetInstances);
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public string ProviderName => "Apple Music";

	public Task InitializeAsync(IIntegrationContext context)
	{
		_logger.Information("Apple Music Control initialized");
		return Task.CompletedTask;
	}

	public Task ShutdownAsync() => Task.CompletedTask;

	public IReadOnlyList<MusicPlayerInstance> GetInstances() => [new MusicPlayerInstance(InstanceId, "Apple Music")];

	public IMusicPlayer? GetPlayer(string instanceId) => instanceId == InstanceId ? _player : null;

	// An empty id is what the standard actions send for "first available player".
	private IMusicPlayer? ResolvePlayer(string? instanceId) => GetPlayer(string.IsNullOrEmpty(instanceId) ? InstanceId : instanceId);

	// Ids are persisted in user profiles; never rename them.
	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("apple_music_track", VariableType.Text) with
		{
			Id = "track", DisplayName = Strings.Variables.Track.Name(), Description = Strings.Variables.Track.Description()
		},
		VariableDefinition.Eager("apple_music_artist", VariableType.Text) with
		{
			Id = "artist", DisplayName = Strings.Variables.Artist.Name(), Description = Strings.Variables.Artist.Description()
		},
		VariableDefinition.Eager("apple_music_album", VariableType.Text) with
		{
			Id = "album", DisplayName = Strings.Variables.Album.Name(), Description = Strings.Variables.Album.Description()
		},
		VariableDefinition.Eager("apple_music_is_playing", VariableType.Boolean) with
		{
			Id = "is-playing", DisplayName = Strings.Variables.IsPlaying.Name(), Description = Strings.Variables.IsPlaying.Description()
		},
		VariableDefinition.Eager("apple_music_state", VariableType.Text) with
		{
			Id = "state", DisplayName = Strings.Variables.State.Name(), Description = Strings.Variables.State.Description()
		},
		VariableDefinition.Eager("apple_music_position", VariableType.Numeric, refreshInterval: TimeSpan.FromSeconds(1)) with
		{
			Id = "position",
			DisplayName = Strings.Variables.Position.Name(),
			Description = Strings.Variables.Position.Description(),
			Unit = "s",
			SemanticKind = VariableSemanticKinds.Duration,
			Write = MusicPlayerVariableWrites.Position
		},
		VariableDefinition.Eager("apple_music_duration", VariableType.Numeric) with
		{
			Id = "duration",
			DisplayName = Strings.Variables.Duration.Name(),
			Description = Strings.Variables.Duration.Description(),
			Unit = "s",
			SemanticKind = VariableSemanticKinds.Duration
		},
		VariableDefinition.Eager("apple_music_shuffle", VariableType.Boolean) with
		{
			Id = "shuffle", DisplayName = Strings.Variables.Shuffle.Name(), Description = Strings.Variables.Shuffle.Description()
		},
		VariableDefinition.Eager("apple_music_repeat", VariableType.Text) with
		{
			Id = "repeat", DisplayName = Strings.Variables.Repeat.Name(), Description = Strings.Variables.Repeat.Description()
		},
		VariableDefinition.Eager("apple_music_connected", VariableType.Boolean) with
		{
			Id = "connected", DisplayName = Strings.Variables.Connected.Name(), Description = Strings.Variables.Connected.Description()
		},
		VariableDefinition.Eager("apple_music_volume", VariableType.Numeric) with
		{
			Id = "volume",
			DisplayName = Strings.Variables.Volume.Name(),
			Description = Strings.Variables.Volume.Description(),
			Unit = "%",
			SemanticKind = VariableSemanticKinds.Percentage,
			Write = MusicPlayerVariableWrites.Volume
		}
	];

	public async ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		var state = await _player.GetStateAsync(cancellationToken).ConfigureAwait(false);
		return NowPlayingVariables.Read(localId, state);
	}

	public ValueTask<VariableWriteResult> SetValueAsync(string localId, object? value, CancellationToken cancellationToken = default)
		=> localId switch
		{
			"volume" => MusicPlayerVariableWrites.SetVolumeAsync(_player, value, cancellationToken),
			"position" => MusicPlayerVariableWrites.SeekAsync(_player, value, cancellationToken),
			_ => ValueTask.FromResult(VariableWriteResult.NotWritable())
		};
}
