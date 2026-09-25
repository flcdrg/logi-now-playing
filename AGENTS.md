# Copilot instructions for logi-now-playing

A C# [Logi Actions SDK](https://logitech.github.io/actions-sdk-docs/csharp/plugin-development/introduction/)
plugin (`NowPlayingPlugin`) that shows system-wide currently-playing media
(artwork and/or title) on a Logi/Loupedeck device button. Currently macOS-only.

## Requirements to build/test at all

- **Logi Options+ must be installed and running** (macOS: `/Applications/Utilities/LogiPluginService.app`).
  The build resolves `PluginApi.dll` (and, for tests, `SkiaSharp.dll` /
  `libSkiaSharp.dylib`) from that local install via `HintPath` in the
  `.csproj` files by default. None of these SDK binaries are committed to
  source control.
  - **Alternative that needs no desktop app**: `PluginApi.dll`/`SkiaSharp`
    are also bundled inside the `LogiPluginTool` .NET global tool.
    `scripts/stage-logi-sdk-assemblies.sh` stages them into a local
    directory shaped like the real install, for use with
    `-p:PluginApiDir=...` — this is what CI uses (see below).
- .NET 10 SDK (the SDK's own docs/sample target `net8.0`, but this project
  intentionally targets `net10.0` — confirmed compatible with the installed
  Logi Plugin Service host).

## Build / test / package commands

```bash
# Build the plugin (also copies package/ assets, writes a .link file into
# Logi Plugin Service's Plugins folder, and asks the running service to
# hot-reload the plugin — see CopyPackage/PostBuild targets in the .csproj)
cd NowPlayingPlugin/src && dotnet build

# Run all tests
cd NowPlayingPlugin/tests/NowPlayingPlugin.Tests && dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~MediaRemoteAdapterStateTests.Merge_LateArtwork_ChangesChangeKey"

# Build the whole solution (both projects) — safe now that
# Directory.Build.props no longer overrides BaseIntermediateOutputPath with
# $(SolutionDir); don't reintroduce a shared obj/ path across projects, it
# causes AssemblyInfo/TargetFrameworkAttribute collisions.
cd NowPlayingPlugin && dotnet build NowPlayingPlugin.sln

# Package/verify a distributable .lplug4 (requires `dotnet tool install --global LogiPluginTool`)
logiplugintool pack ./NowPlayingPlugin/bin/Debug ./NowPlaying.lplug4
logiplugintool verify ./NowPlaying.lplug4
```

There is no lint/format command configured beyond the `.editorconfig` (see
Conventions below). CI (`.github/workflows/build.yml`) builds, tests, and
packs/verifies a `.lplug4` on every push/PR to `main`, uploading it as a
workflow artifact; pushing a `v*` tag also attaches it to a GitHub release.
Local dev builds still rely on manual build/test plus live verification
against the running Logi Plugin Service
(see README's "Manual verification" section).

## Architecture

```
NowPlayingCommand (PluginDynamicCommand)   — the single Logi action, one instance,
        │ subscribes to                     three display-mode parameters (artwork+title,
        ▼                                    artwork only, title only). Display-only:
NowPlayingCoordinator                        RunCommand is intentionally inert.
        │ owns (one shared instance per
        │  plugin session — constructed
        │  in NowPlayingPlugin's ctor, not
        │  Load(), so it's never null
        │  regardless of dynamic-action
        │  load order)
        ▼
INowPlayingProvider (interface)            — platform-neutral contract:
        │ implemented by                     StartAsync/StopAsync + SnapshotChanged event.
        ▼                                    This is the extension point for future
MacOsNowPlayingProvider                      Windows (GSMTC) / Linux (MPRIS) providers.
        │ launches/monitors
        ▼
vendored mediaremote-adapter.pl             — external process, NEVER linked into
  (NowPlayingPlugin/src/package/mac/...)      managed code. Runs Apple's private
                                              MediaRemote framework via an OS-entitled
                                              perl helper (BSD-3-Clause, pinned commit,
                                              see third_party/mediaremote-adapter/).

NowPlayingImageRenderer                     — cover-crops artwork onto a button-sized
                                               BitmapImage using only PluginApi's own
                                               BitmapBuilder/BitmapImage APIs.
```

Key architectural rules (deviating breaks the design, not just style):

- **One shared process, not one per button.** `NowPlayingCoordinator` fans a
  single `MacOsNowPlayingProvider`'s updates out to every configured button
  parameter. Never spawn a new adapter/helper process per command or per
  button. The provider continuously drains both stdout and stderr from that
  long-running process; deferring stderr reads until process exit can fill the
  pipe and deadlock all future metadata updates. A one-shot `get`
  reconciliation every 10 seconds also repairs state if macOS drops a stream
  notification.
- **`Idle` vs `Unavailable` are distinct states**, not both collapsed to
  "nothing to show". `Idle` = provider healthy, nothing playing anywhere.
  `Unavailable` = the provider itself can't function (e.g. unsupported OS
  version). The action must show different fallback content for each.
- Providers must publish snapshots only when something rendering-relevant
  changes (title/artist/album/playing/artwork) — not on every
  playback-position tick. `NowPlayingSnapshot.ChangeKey` is what
  `NowPlayingCoordinator` dedupes on.
- Artwork can arrive **after** the initial metadata snapshot; an
  artwork-only update must still be treated as a meaningful change.

## Non-obvious SDK/host quirks (do not "clean up" without re-verifying live)

- **`Plugin.AssemblyFilePath` is not populated until after the `Plugin`
  subclass's constructor returns.** Reading it in the constructor throws /
  returns null. Any consumer must receive it as a lazily-evaluated
  `Func<String>` (e.g. `() => this.AssemblyFilePath`) and only read it later
  (e.g. inside `StartAsync`). `Assembly.Location` is *also* unusable here —
  it's empty under Logi Plugin Service's CoreCLR host.
- **Never delete the scaffolded `NowPlayingApplication : ClientApplication`
  class**, even though `Plugin.HasNoApplication => true` makes it look like
  dead code. The closed-source SDK's plugin loader requires it to exist
  regardless; removing it causes total plugin load failure
  (`Cannot load plugin from '...'`) that's reproducible even after a full
  Logi Plugin Service restart, yet the assembly still loads fine via plain
  reflection outside the host. This has been verified empirically (deleted →
  broke, restored → fixed) — don't re-attempt the "cleanup" without
  re-verifying against the live host.
- To distinguish "genuinely broken plugin" from "stale disabled-plugin state
  in the host" while debugging, fully kill/restart the main
  `LogiPluginService.app/Contents/MacOS/LogiPluginService` process (not just
  `LogiPluginServiceExt`) — this clears in-memory disabled-plugin state.
  `ps aux | grep mediaremote-adapter` is the most reliable non-hardware way
  to confirm the provider's process lifecycle (single shared process,
  restart-with-backoff behavior) since there's no physical device in dev.

## Test project conventions

`NowPlayingPlugin.Tests` deliberately does **not** use a `ProjectReference`
to the main plugin project. Instead it links specific pure-logic source
files directly via `<Compile Include="..\..\src\...\*.cs" LinkBase="..." />`
in the `.csproj`. This is because a `ProjectReference` (or even building the
main project) triggers its `CopyPackage`/`PostBuild` MSBuild targets, which
copy files into the *live installed* plugin folder and send a
`loupedeck:plugin/NowPlaying/reload` command to the running service —
undesirable on every test build.

- Only pure-logic files are linked into tests: `Models/**`,
  `Providers/INowPlayingProvider.cs`, `Providers/NowPlayingCoordinator.cs`,
  `Providers/MacOS/MediaRemoteAdapterState.cs`,
  `Providers/MacOS/MediaRemoteAdapterPaths.cs`, `Rendering/NowPlayingImageRenderer.cs`.
- Process-management/lifecycle classes (`MacOsNowPlayingProvider`,
  `UnixSignal`, `NowPlayingPlugin`, `NowPlayingCommand`) are intentionally
  **not** unit tested — they're covered by manual/live verification only.
  When adding new logic, prefer putting it in a linkable, provider-agnostic
  file so it can actually be unit tested.
- `internal` types (e.g. `MediaRemoteAdapterState`) are visible to tests
  without `InternalsVisibleTo` because the source is compiled directly into
  the test assembly, not referenced externally — don't add
  `InternalsVisibleTo` as a "fix" for this.
- A few `NowPlayingImageRendererTests` are marked `Skip`: `BitmapBuilder.DrawImage`
  needs native Skia state that only Logi Plugin Service's own startup
  initializes, so those specific paths can't run in standalone `dotnet test`.
  Keep them as documented skips rather than deleting them.

## Code style

Enforced via `NowPlayingPlugin/src/.editorconfig` (warnings, not just
suggestions, for these):

- Always qualify member access with `this.` (fields, properties, methods,
  events) — `dotnet_style_qualification_for_* = true:warning`.
- Use BCL type names, not C# keyword aliases: `Boolean`/`String`/`Int32`
  etc., not `bool`/`string`/`int` (`dotnet_style_predefined_type_for_*
  = false:warning`). This is a Loupedeck-scaffold convention followed
  throughout the codebase (e.g. `public override Boolean HasNoApplication`).
- `RootNamespace` is `Loupedeck.NowPlayingPlugin` (plugin) /
  `Loupedeck.NowPlayingPlugin.Tests` (tests) — namespaces follow folder
  structure under `src/`/`tests/...`.

## Licensing boundaries

- This repo is Apache-2.0. The vendored `mediaremote-adapter` helper
  (`NowPlayingPlugin/src/package/mac/MediaRemoteAdapter/`) is a separately
  BSD-3-Clause-licensed, pinned-commit build — see
  `third_party/mediaremote-adapter/LICENSE` / `NOTICE.md` and
  `scripts/build-mediaremote-adapter.sh` for how it's rebuilt/re-vendored.
- `nowplaying-cli` (GPL-3.0) was used only as behavioral/protocol reference
  during design — do not copy source from it into this repo.
