# Apple Music Control

A Macro Deck 3 plugin that shows and controls the **Apple Music app for Windows** from your deck:
play/pause, next and previous, seek, shuffle, repeat, the Windows system volume, now-playing variables
and the built-in **Music Player widget** with album artwork. It works with any Macro Deck client,
including the iPhone app.

This is an independent community plugin. It is not made, endorsed or supported by Apple. "Apple Music"
is used only to name the app the plugin works with.

## Requirements

- Windows 10 version 1809 or later, x64 (the only supported platform)
- The Apple Music app for Windows (Microsoft Store), signed in and playing or paused
- Macro Deck 3

## How it works

Apple Music publishes its playback to Windows' media controls (the same session the volume flyout and
keyboard media keys use). The plugin reads that session through the Windows
`GlobalSystemMediaTransportControlsSessionManager` API and sends play, pause, skip, seek, shuffle and
repeat requests to it. Other apps' sessions (browsers, Spotify) are ignored. Only a session whose app id
contains `AppleMusic` is used.

Volume is the **Windows master volume** of the default playback device, set through the Windows Core
Audio API. Apple Music's own in-app volume slider has no Windows API, so it is not changed.

## Using it

### Music Player widget

Add a **Music Player** widget to a page and pick **Apple Music** as the player. It shows title, artist,
album art, progress and the playback state, and works well on a phone-sized grid. Tapping its controls
uses the actions below.

### Actions

All actions come from the standard Macro Deck music player set. Choose **Apple Music** (or leave the
player empty) in the action's `instance` parameter.

| Action | What it does |
| --- | --- |
| Play, Pause, Toggle Play/Pause | Transport control. Toggle shows the current state on the button. |
| Next Track, Previous Track | Skip. |
| Seek | Jump to a position in seconds. |
| Toggle Shuffle | Toggle, or force on or off. |
| Set Repeat Mode | Off, repeat track, or repeat album/playlist. |
| Set Volume, Volume Up, Volume Down | Windows master volume. |
| Refresh State | Reads the current state immediately. |

An action reports a failure (instead of pretending to succeed) when Apple Music is not running, when it
rejects the request, or when it does not offer that control right now (for example shuffle on a radio
station). Volume actions work even when Apple Music is closed.

### Variables

| Variable | Type | Value |
| --- | --- | --- |
| `apple_music_track` | Text | Song title |
| `apple_music_artist` | Text | Artist |
| `apple_music_album` | Text | Album |
| `apple_music_is_playing` | Boolean | `true` while playing |
| `apple_music_state` | Text | `playing`, `paused`, `stopped`, `not-running` or `unavailable` |
| `apple_music_position` | Number (s) | Position; writable, so a slider bound to it seeks on release |
| `apple_music_duration` | Number (s) | Track length |
| `apple_music_shuffle` | Boolean | Shuffle on |
| `apple_music_repeat` | Text | `off`, `track` or `context` |
| `apple_music_connected` | Boolean | Apple Music has a media session |
| `apple_music_volume` | Number (%) | Windows master volume; writable, so a slider bound to it sets the volume |

Example button label: `{{ vars.apple_music_track }}` with a second line `{{ vars.apple_music_artist }}`.
Track variables read as "not available" while Apple Music is closed.

## Privacy and network

The plugin makes **no network connections**. It reads the now-playing metadata (title, artist, album,
cover) and playback state of the local Apple Music media session and the system volume, and passes them
only to your Macro Deck. Nothing is stored on disk. It needs no account and no credentials.

## Limitations

- Windows only. Apple Music on macOS is not supported.
- What Apple Music exposes through Windows media controls decides what works. Shuffle, repeat and seek are
  attempted only when Apple Music reports them as available; otherwise the action fails.
- If the old iTunes app is running at the same time, it can take over the media controls. Close iTunes.
- The position is extrapolated between Windows' updates and can be off by a second.

## Development

Requires the .NET 10 SDK. The plugin multi-targets `net10.0-windows10.0.19041.0` (the shipped build,
with the Windows adapters) and `net10.0` (platform-neutral logic only, so tests run on any OS). Windows
code lives in `src/AppleMusicControl/Platform/` behind `#if WINDOWS`.

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release
```

The CLI version must equal the SDK version pinned in `Directory.Packages.props`:

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --version 3.0.0-beta.17
macrodeck-plugin build --source src/AppleMusicControl --output ./artifacts
macrodeck-plugin validate --level publication --artifact ./artifacts/com.wilcox101-maker.apple-music-1.0.0.macroDeckPlugin
macrodeck-plugin test --executable "$PWD/src/AppleMusicControl/bin/Release/net10.0-windows10.0.19041.0/AppleMusicControl.dll"
```

`macrodeck-plugin test --project` cannot choose a framework of a multi-targeted project, so pass the
Windows build with `--executable` (an absolute path).

### Try it against your Macro Deck (Windows)

With Macro Deck running on the same PC:

```powershell
macrodeck-plugin run --artifact .\artifacts\com.wilcox101-maker.apple-music-1.0.0.macroDeckPlugin
```

Approve the pairing prompt in Macro Deck. Alternatively install the `.macroDeckPlugin` file from a local
file in Macro Deck and confirm the unsigned-package prompt, or debug with the **Macro Deck - Real Host**
launch profile (see Macro Deck's debugging guide; keep the enrollment token in .NET User Secrets only).

### Third-party notices

`THIRD-PARTY-NOTICES` is generated from the published output:

```bash
python3 inventory-dependencies.py src/AppleMusicControl/bin/publish/win-x64 --notices THIRD-PARTY-NOTICES --header-name "Apple Music Control"
```

(from the Macro Deck plugin creator tooling), then the hand-written Windows SDK section is re-added,
because the Windows SDK projection comes from a targeting pack, not from `deps.json`.

## License

MIT, see [LICENSE](LICENSE). Bundled components are listed in [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES):
the Macro Deck SDK and Serilog (Apache-2.0) and the Microsoft Windows SDK projection (Microsoft Windows SDK
license).
