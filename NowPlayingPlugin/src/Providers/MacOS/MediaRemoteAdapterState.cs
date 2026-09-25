namespace Loupedeck.NowPlayingPlugin.Providers.MacOS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;

    using Loupedeck.NowPlayingPlugin.Models;

    // Maintains the merged now-playing state reported by the MediaRemoteAdapter
    // `stream` command and turns it into NowPlayingSnapshot instances.
    //
    // The adapter's `stream` command emits either full snapshots (diff: false) or
    // partial updates (diff: true) that must be merged into the last full state;
    // a key with a JSON null value in a diff update means that key was removed.
    // See the upstream README's "stream" section for the full contract.
    internal sealed class MediaRemoteAdapterState
    {
        // Keys that affect what NowPlayingCommand renders. Updates that only touch
        // other keys (e.g. elapsedTime, timestamp) are ignored so we don't decode
        // artwork or recompute a change key on every playback-position tick.
        private static readonly HashSet<String> RelevantKeys = new HashSet<String>(StringComparer.Ordinal)
        {
            "bundleIdentifier",
            "title",
            "artist",
            "album",
            "playing",
            "artworkData",
            "artworkMimeType",
        };

        private JsonObject _state = new JsonObject();
        private Boolean _hasReceivedAnyPayload;

        // Applies an adapter `stream` message payload. Returns true and sets
        // `snapshot` when the update is relevant to rendering; returns false when
        // the update should be ignored (no relevant key changed).
        public Boolean TryApply(JsonObject payload, Boolean isDiff, out NowPlayingSnapshot snapshot)
        {
            payload ??= new JsonObject();

            var touchedKeys = payload.Select(kv => kv.Key).ToArray();
            var isFirstPayload = !this._hasReceivedAnyPayload;
            this._hasReceivedAnyPayload = true;

            if (isDiff)
            {
                foreach (var key in touchedKeys)
                {
                    var value = payload[key];
                    if (value is null)
                    {
                        this._state.Remove(key);
                    }
                    else
                    {
                        // Detach the node from the source payload before reparenting it.
                        payload[key] = null;
                        this._state[key] = value;
                    }
                }
            }
            else
            {
                var replacement = new JsonObject();
                foreach (var kv in payload)
                {
                    if (kv.Value != null)
                    {
                        payload[kv.Key] = null;
                        replacement[kv.Key] = kv.Value;
                    }
                }

                this._state = replacement;
            }

            var relevantKeyTouched = touchedKeys.Any(RelevantKeys.Contains);
            if (!isFirstPayload && isDiff && !relevantKeyTouched)
            {
                snapshot = null;
                return false;
            }

            snapshot = this.BuildSnapshot();
            return true;
        }

        private NowPlayingSnapshot BuildSnapshot()
        {
            var bundleIdentifier = GetString(this._state, "bundleIdentifier");
            var title = GetString(this._state, "title");

            if (String.IsNullOrEmpty(bundleIdentifier) && String.IsNullOrEmpty(title))
            {
                return NowPlayingSnapshot.Idle();
            }

            var artist = GetString(this._state, "artist");
            var album = GetString(this._state, "album");
            var isPlaying = GetBool(this._state, "playing");
            var artworkData = GetBase64Bytes(this._state, "artworkData");

            var changeKey = String.Join(
                "|",
                bundleIdentifier,
                title,
                artist,
                album,
                isPlaying,
                artworkData != null ? ComputeFingerprint(artworkData) : "noart");

            var media = new NowPlayingMedia(title, artist, album, isPlaying, artworkData, changeKey);
            return NowPlayingSnapshot.Active(media);
        }

        private static String GetString(JsonObject state, String key) =>
            state.TryGetPropertyValue(key, out var node) && node != null && node.GetValueKind() == System.Text.Json.JsonValueKind.String
                ? node.GetValue<String>()
                : null;

        private static Boolean GetBool(JsonObject state, String key) =>
            state.TryGetPropertyValue(key, out var node) && node != null
            && (node.GetValueKind() == System.Text.Json.JsonValueKind.True || node.GetValueKind() == System.Text.Json.JsonValueKind.False)
            && node.GetValue<Boolean>();

        // Defensive upper bound on decoded artwork size; artwork this large is not
        // expected from any known media player and is more likely a malformed or
        // hostile payload than legitimate album art.
        private const Int32 MaxArtworkBytes = 16 * 1024 * 1024;

        private static Byte[] GetBase64Bytes(JsonObject state, String key)
        {
            var base64 = GetString(state, key);
            if (String.IsNullOrEmpty(base64))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return bytes.Length <= MaxArtworkBytes ? bytes : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // A cheap, stable fingerprint used only to detect artwork changes for the
        // change key; not a cryptographic hash.
        private static String ComputeFingerprint(Byte[] data)
        {
            unchecked
            {
                const UInt64 offsetBasis = 14695981039346656037;
                const UInt64 prime = 1099511628211;
                var hash = offsetBasis;
                foreach (var b in data)
                {
                    hash ^= b;
                    hash *= prime;
                }

                return data.Length + ":" + hash.ToString("x16");
            }
        }
    }
}
