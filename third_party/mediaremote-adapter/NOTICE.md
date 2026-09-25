# MediaRemoteAdapter

Vendored from: https://github.com/ungive/mediaremote-adapter.git
Pinned commit: 73f14ab1568371e6e3c44063f21c34c5e2712c4d
License: BSD-3-Clause (see LICENSE in this directory)

This helper invokes Apple's private MediaRemote framework via an
OS-entitled system Perl interpreter to read now-playing metadata from
other applications. It is rebuilt from source by
scripts/build-mediaremote-adapter.sh and its binary output is vendored
into NowPlayingPlugin/src/package/mac/MediaRemoteAdapter for packaging
with the plugin. Do not link the plugin assembly against this framework;
it is only ever invoked as an external helper process.
