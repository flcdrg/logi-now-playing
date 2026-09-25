# logi-now-playing

A [Logi Actions SDK](https://logitech.github.io/actions-sdk-docs/csharp/plugin-development/introduction/)
plugin that shows the currently playing system-wide media (artwork and/or
title) on a button of an MX Creative Console device — an MX Creative Keypad,
MX Creative Dialpad, or Actions Ring — or another Logi Options+/Loupedeck
compatible device.

The plugin exposes a single parameterized, **display-only** action with three
modes:

- **Artwork and title** — cover-cropped artwork filling the button, with the
  track title shown as the button's caption.
- **Artwork only** — cover-cropped artwork, no caption.
- **Title only** — no artwork, just the track title.

Button presses do nothing in this release; the action is purely informational.

## How it works

Apple's public Now Playing APIs (`MPNowPlayingInfoCenter`, `MPRemoteCommandCenter`,
[developer.apple.com/documentation/nowplaying](https://developer.apple.com/documentation/nowplaying))
only expose an application's *own* playback state to that application. There
is no public, supported API for reading which app is currently playing
media system-wide.

To work around this, the macOS implementation bundles a small, vendored,
BSD-3-Clause helper — [`mediaremote-adapter`](https://github.com/ungive/mediaremote-adapter)
— which invokes Apple's *private* `MediaRemote` framework via an
OS-entitled `/usr/bin/perl` process. This is the same private-API approach
used by tools such as [`nowplaying-cli`](https://github.com/kirtan-shah/nowplaying-cli)
(used only as behavioral reference here, not as source — that project is
GPL-3.0 licensed and this repository is Apache-2.0).

**Because this relies on a private, unsupported Apple framework:**

- It can break on future macOS releases without warning.
- It is not appropriate for Mac App Store distribution.
- The plugin runs the adapter as a separate, sandboxed-by-default OS process
  and never links against the private framework directly from managed code.

The plugin architecture keeps this risk isolated behind a platform-neutral
`INowPlayingProvider` interface (see [Architecture](#architecture--future-platforms)),
so a future Windows implementation (via the public
[Global System Media Transport Controls](https://learn.microsoft.com/en-us/uwp/api/windows.media.control) API)
or Linux implementation (via [MPRIS](https://specifications.freedesktop.org/mpris-spec/latest/)
over D-Bus) can be added without redesigning the action or rendering code.

## Requirements

- macOS (Apple Silicon or Intel).
- [Logi Options+](https://www.logitech.com/en-us/software/logi-options-plus.html)
  installed and running (this installs and runs the Logi Plugin Service and
  its bundled `PluginApi.dll` / `libSkiaSharp.dylib`, which this plugin
  references at build time — see [Building](#building)).
- .NET 10 SDK, to build the plugin from source.

The plugin has been developed and tested against Logi Plugin Service's
embedded CoreCLR host, which loads a `net10.0` plugin assembly successfully
even though the SDK's own documentation and sample project target `net8.0`.

## Building

```bash
cd NowPlayingPlugin/src
dotnet build
```

This automatically:

1. Compiles the plugin against the locally installed `PluginApi.dll`
   (resolved from `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/`
   on macOS — see `PluginApiDir` in `NowPlayingPlugin.csproj`).
2. Copies the `package/` folder (metadata, icons, and the vendored macOS
   `MediaRemoteAdapter` helper) alongside the compiled `bin/` output.
3. Writes a `.link` file into Logi Plugin Service's `Plugins` folder pointing
   at the build output, and asks the running service to reload the plugin —
   so a rebuild is immediately reflected without a manual install step.

To inspect the resulting on-disk layout or install a packaged build instead,
use [`LogiPluginTool`](https://logitech.github.io/actions-sdk-docs/csharp/plugin-development/introduction/):

```bash
dotnet tool install --global LogiPluginTool
logiplugintool pack ./NowPlayingPlugin/bin/Debug ./NowPlaying.lplug4
logiplugintool verify ./NowPlaying.lplug4
logiplugintool install ./NowPlaying.lplug4
```

### Running tests

```bash
cd NowPlayingPlugin/tests/NowPlayingPlugin.Tests
dotnet test
```

The test project resolves `PluginApi.dll` and its `SkiaSharp`/`libSkiaSharp.dylib`
native dependency from the same locally installed Logi Plugin Service, so
Logi Options+ must be installed to build and run tests, but nothing from the
SDK installation is copied into the repository or committed to source
control. A small number of image-rendering tests are marked `Skip`: they
exercise a `BitmapBuilder.DrawImage` code path that depends on native Skia
state Logi Plugin Service initializes at its own startup, which isn't
reproducible in a standalone `dotnet test` process — those code paths are
instead verified manually against the running plugin (see
[Manual verification](#manual-verification)).

## Setting up the action on a device

1. Build and install the plugin (see above).
2. In Logi Options+, add the **Now Playing** action to a button on your
   device.
3. Choose a **Display mode** parameter for that button: *Artwork and title*,
   *Artwork only*, or *Title only*.
4. Repeat for additional buttons if you want more than one mode visible at
   once (e.g. one large button with artwork+title, and a smaller button with
   just the title).

## Diagnostics / troubleshooting

Logi Plugin Service writes a dedicated log file per plugin:

```
~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/NowPlaying.log
```

Useful things to check there:

- `Plugin 'NowPlaying' version '1.0' loaded from '...' in Nms` — confirms the
  plugin assembly loaded successfully.
- `MediaRemoteAdapter stream ended; restarting in Ns (attempt N)` — the
  native helper process exited unexpectedly and the plugin is retrying with
  exponential backoff (1s, 2s, 5s, 10s, 30s). A single restart after system
  sleep/wake or a media app quitting is normal; continuous restarts suggest
  the current macOS version is incompatible with the vendored adapter build.
- Any `ERROR` lines, e.g. path resolution failures if the plugin's package
  layout is unexpectedly different from what was built (this would indicate
  a packaging bug, not a user configuration issue).

If the button shows nothing, an idle placeholder is expected when there
truly is no active media session anywhere on the system — this is
distinguished in the log/behavior from a genuinely unavailable/broken helper.

### Manual verification

Because artwork rendering depends on native Skia state that only Logi Plugin
Service initializes, the most reliable way to check the full pipeline is to
watch the per-plugin log after a build (see above) and confirm:

1. `Dynamic action added: '...NowPlayingCommand'` and `N dynamic actions
   loaded`.
2. `Plugin 'NowPlaying' version '1.0' loaded from '...' in Nms` with no
   preceding `ERROR` lines.
3. Exactly one `mediaremote-adapter.pl ... stream` process running
   (`ps aux | grep mediaremote-adapter`) regardless of how many buttons use
   the action — the plugin runs a single shared helper process and fans
   updates out to every configured button.

## Architecture / future platforms

```
NowPlayingCommand (PluginDynamicCommand)
        │ subscribes to
        ▼
NowPlayingCoordinator            — one shared instance per plugin session;
        │ owns                     deduplicates snapshots by ChangeKey and
        ▼                          fans SnapshotChanged out to every button.
INowPlayingProvider (interface)  — platform-neutral contract: StartAsync,
        │ implemented by           StopAsync, and a SnapshotChanged event.
        ▼
MacOsNowPlayingProvider         — launches/monitors the vendored
                                   mediaremote-adapter helper process,
                                   parses its newline-delimited JSON `stream`
                                   output, and restarts it with backoff if it
                                   exits unexpectedly.

NowPlayingImageRenderer          — renders artwork bytes onto a button-sized
                                    BitmapImage, center-cropped ("cover" fit),
                                    using only the SDK's own BitmapBuilder/
                                    BitmapImage APIs (no extra imaging
                                    dependency).
```

To add a new platform, implement `INowPlayingProvider` (see
`Providers/INowPlayingProvider.cs`):

- Publish an `Initializing` snapshot synchronously (or very soon after)
  `StartAsync` is called.
- Publish further snapshots only when something rendering-relevant changes
  (title/artist/album/playing/artwork), not on every playback-position tick.
- Distinguish `Idle` (provider is healthy, nothing is playing) from
  `Unavailable` (the provider itself cannot function, e.g. an unsupported OS
  version) — the action shows different fallback text for each.
- Release all resources in `StopAsync`/`Dispose` — in particular, do not
  leave a helper process or background thread running after `StopAsync`
  returns.

Candidate future providers (not implemented in this release):

- **Windows** — [`GlobalSystemMediaTransportControlsSessionManager`](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager),
  a public, documented API with no private-framework risk.
- **Linux** — [MPRIS](https://specifications.freedesktop.org/mpris-spec/latest/)
  `org.mpris.MediaPlayer2.Player` over D-Bus, also public and standardized.

## Licensing and attribution

- This repository is licensed under [Apache-2.0](LICENSE).
- The vendored `MediaRemoteAdapter.framework` and `mediaremote-adapter.pl`
  (under `NowPlayingPlugin/src/package/mac/MediaRemoteAdapter/`) are built
  from a pinned commit of [`ungive/mediaremote-adapter`](https://github.com/ungive/mediaremote-adapter),
  BSD-3-Clause licensed. See `third_party/mediaremote-adapter/LICENSE` and
  `NOTICE.md` for the exact pinned commit and attribution details, and
  `scripts/build-mediaremote-adapter.sh` for the reproducible build/vendoring
  process.
- [`nowplaying-cli`](https://github.com/kirtan-shah/nowplaying-cli) (GPL-3.0)
  was used only as behavioral/protocol reference during design; no source
  from that project is included here.
