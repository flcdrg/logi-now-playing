#!/usr/bin/env bash
#
# Stages the Logi Actions SDK assemblies (PluginApi.dll and its SkiaSharp
# native dependency) into a local directory shaped like a real
# LogiPluginService.app/Contents/MonoBundle install, WITHOUT requiring
# Logi Options+ to be installed.
#
# This works because `LogiPluginTool` (the official packaging CLI, installed
# as a .NET global tool from nuget.org) bundles the same PluginApi.dll /
# SkiaSharp.dll / libSkiaSharp.dylib used by the desktop app. This script is
# what makes CI builds/tests possible on a plain GitHub-hosted macOS runner,
# and can also be used locally as an alternative to installing Logi Options+
# just to build/test this repository.
#
# Usage:
#   dotnet tool install --global LogiPluginTool   # if not already installed
#   scripts/stage-logi-sdk-assemblies.sh [output-dir]
#
# Then build/test with:
#   dotnet build NowPlayingPlugin/src -p:PluginApiDir="<output-dir>/"
#   dotnet test  NowPlayingPlugin/tests/NowPlayingPlugin.Tests -p:PluginApiDir="<output-dir>/"
#
set -euo pipefail

OUT_DIR="${1:-.logi-sdk}"
OUT_DIR="$(mkdir -p "${OUT_DIR}" && cd "${OUT_DIR}" && pwd)"

TOOL_STORE="${HOME}/.dotnet/tools/.store/logiplugintool"

if [[ ! -d "${TOOL_STORE}" ]]; then
    echo "error: LogiPluginTool is not installed. Run: dotnet tool install --global LogiPluginTool" >&2
    exit 1
fi

# The tool's own assemblies live under a version- and TFM-specific path, e.g.
# .../logiplugintool/<version>/logiplugintool/<version>/tools/<tfm>/any/ —
# locate it by content rather than hardcoding the version/TFM.
TOOL_ANY_DIR="$(dirname "$(find "${TOOL_STORE}" -type f -name 'PluginApi.dll' -print -quit)")"

if [[ -z "${TOOL_ANY_DIR}" ]]; then
    echo "error: could not find PluginApi.dll inside the installed LogiPluginTool" >&2
    exit 1
fi

echo "Found LogiPluginTool SDK assemblies at: ${TOOL_ANY_DIR}"

# PluginApi.dll (+ symbols, for nicer stack traces in test failures).
cp "${TOOL_ANY_DIR}/PluginApi.dll" "${OUT_DIR}/"
[[ -f "${TOOL_ANY_DIR}/PluginApi.pdb" ]] && cp "${TOOL_ANY_DIR}/PluginApi.pdb" "${OUT_DIR}/"

# libSkiaSharp.dylib must sit flat alongside PluginApi.dll — this matches the
# real MonoBundle layout, and is where NowPlayingPlugin.Tests.csproj's
# CopySkiaSharpNative target looks for it (relative to $(PluginApiDir)).
NATIVE_SKIA="$(find "${TOOL_ANY_DIR}" -path '*runtimes/osx/native/libSkiaSharp.dylib' -print -quit)"
if [[ -z "${NATIVE_SKIA}" ]]; then
    echo "error: could not find runtimes/osx/native/libSkiaSharp.dylib inside LogiPluginTool" >&2
    exit 1
fi
cp "${NATIVE_SKIA}" "${OUT_DIR}/libSkiaSharp.dylib"

# SkiaSharp.dll (managed) must sit under .xamarin/osx-arm64/ and
# .xamarin/osx-x64/ — this matches the real MonoBundle layout, and is where
# NowPlayingPlugin.Tests.csproj's SkiaSharpNativeDir property (computed from
# $(PluginApiDir) + the running architecture) expects to find it. Copying to
# both architecture folders makes the staged directory work regardless of
# which architecture CI (or a contributor's machine) happens to run on.
mkdir -p "${OUT_DIR}/.xamarin/osx-arm64" "${OUT_DIR}/.xamarin/osx-x64"
cp "${TOOL_ANY_DIR}/SkiaSharp.dll" "${OUT_DIR}/.xamarin/osx-arm64/"
cp "${TOOL_ANY_DIR}/SkiaSharp.dll" "${OUT_DIR}/.xamarin/osx-x64/"

echo "Staged Logi SDK assemblies at: ${OUT_DIR}"
echo "PLUGIN_API_DIR=${OUT_DIR}/"
