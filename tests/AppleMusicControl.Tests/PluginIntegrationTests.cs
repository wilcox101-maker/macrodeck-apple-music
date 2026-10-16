using AppleMusicControl.Playback;
using AppleMusicControl.Volume;
using MacroDeck.Plugin.Protocol.Capabilities.Variables;
using MacroDeck.Plugin.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace AppleMusicControl.Tests;

/// <summary>Runs the plugin's own capability handlers through the SDK harness with fake OS access.</summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private FakeMediaSessionSource _source = null!;
	private FakeSystemVolume _volume = null!;

	[SetUp]
	public void SetUp()
	{
		_source = new FakeMediaSessionSource { Session = Sessions.Playing() };
		_volume = new FakeSystemVolume();
	}

	private PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder =>
		{
			builder.UseLocalization(Strings.LocalizationCatalog).RegisterIntegration<AppleMusicIntegration>();
			builder.Services.AddSingleton<IMediaSessionSource>(_source);
			builder.Services.AddSingleton<ISystemVolume>(_volume);
			builder.Services.AddAppleMusic();
		});

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[Test]
	public async Task Next_action_reaches_apple_music()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("next");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(string.Join(", ", _source.Commands), Is.EqualTo("Next"));
	}

	[Test]
	public async Task Next_action_fails_when_apple_music_is_not_running()
	{
		_source.Session = null;
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("next");

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task Set_volume_action_sets_the_system_volume()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync("set-volume", new Dictionary<string, object?> { ["volume"] = 70 });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(_volume.Percent, Is.EqualTo(70));
	}

	[Test]
	public async Task Track_variable_is_served()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Variables.GetAsync("track");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(outcome.Data?.ToString(), Does.Contain("Song"));
	}

	[Test]
	public async Task Writing_the_volume_variable_sets_the_system_volume()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Variables.SetAsync("volume", new VariableValueDto { Kind = "number", Number = 25 });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(_volume.Percent, Is.EqualTo(25));
	}

	[Test]
	public async Task Music_player_state_reports_the_track()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.MusicPlayer.GetStateAsync(new { instanceId = AppleMusicIntegration.InstanceId });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(outcome.Data?.ToString(), Does.Contain("Song"));
	}
}

[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.wilcox101-maker.apple-music"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
