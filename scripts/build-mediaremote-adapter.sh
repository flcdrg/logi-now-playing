#!/usr/bin/env bash
#
# Builds the vendored MediaRemoteAdapter native helper from a pinned upstream
# commit of https://github.com/ungive/mediaremote-adapter (BSD-3-Clause) and
# copies the build output into the plugin's macOS package assets.
#
# Requirements: git, cmake, Xcode command line tools (clang).
#
# Usage:
#   scripts/build-mediaremote-adapter.sh
#
set -euo pipefail

# Pinned upstream commit. Update deliberately and re-run this script after
# reviewing the upstream diff for security and behavior changes.
ADAPTER_REPO="https://github.com/ungive/mediaremote-adapter.git"
ADAPTER_COMMIT="73f14ab1568371e6e3c44063f21c34c5e2712c4d"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="$(mktemp -d)"
DEST_DIR="${REPO_ROOT}/NowPlayingPlugin/src/package/mac/MediaRemoteAdapter"
NOTICE_DIR="${REPO_ROOT}/third_party/mediaremote-adapter"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

echo "Cloning ${ADAPTER_REPO} at ${ADAPTER_COMMIT}..."
git clone --quiet "${ADAPTER_REPO}" "${WORK_DIR}/mediaremote-adapter"
git -C "${WORK_DIR}/mediaremote-adapter" checkout --quiet "${ADAPTER_COMMIT}"

echo "Configuring and building with CMake..."
cmake -S "${WORK_DIR}/mediaremote-adapter" -B "${WORK_DIR}/mediaremote-adapter/build" >/dev/null
cmake --build "${WORK_DIR}/mediaremote-adapter/build" >/dev/null

echo "Verifying the built adapter self-test..."
FRAMEWORK_PATH="${WORK_DIR}/mediaremote-adapter/build/MediaRemoteAdapter.framework"
TEST_CLIENT_PATH="${WORK_DIR}/mediaremote-adapter/build/MediaRemoteAdapterTestClient"
/usr/bin/perl "${WORK_DIR}/mediaremote-adapter/bin/mediaremote-adapter.pl" "${FRAMEWORK_PATH}" "${TEST_CLIENT_PATH}" test

echo "Copying artifacts into ${DEST_DIR}..."
rm -rf "${DEST_DIR}"
mkdir -p "${DEST_DIR}"
cp -R "${FRAMEWORK_PATH}" "${DEST_DIR}/MediaRemoteAdapter.framework"
cp "${WORK_DIR}/mediaremote-adapter/bin/mediaremote-adapter.pl" "${DEST_DIR}/mediaremote-adapter.pl"

echo "Recording provenance in ${NOTICE_DIR}..."
mkdir -p "${NOTICE_DIR}"
cp "${WORK_DIR}/mediaremote-adapter/LICENSE" "${NOTICE_DIR}/LICENSE"
cat > "${NOTICE_DIR}/NOTICE.md" <<EOF
# MediaRemoteAdapter

Vendored from: ${ADAPTER_REPO}
Pinned commit: ${ADAPTER_COMMIT}
License: BSD-3-Clause (see LICENSE in this directory)

This helper invokes Apple's private MediaRemote framework via an
OS-entitled system Perl interpreter to read now-playing metadata from
other applications. It is rebuilt from source by
scripts/build-mediaremote-adapter.sh and its binary output is vendored
into NowPlayingPlugin/src/package/mac/MediaRemoteAdapter for packaging
with the plugin. Do not link the plugin assembly against this framework;
it is only ever invoked as an external helper process.
EOF

echo "Done. Vendored MediaRemoteAdapter into: ${DEST_DIR}"
