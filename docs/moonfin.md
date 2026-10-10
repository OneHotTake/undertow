# Moonfin

V0.47.1 repairs clients that start playback with the pathless static source ID
from Emby's item details. Undertow offers an **Automatic** choice under that ID
through its existing dynamic provider. It uses the first candidate from Undertow's normal ordering as a finite HTTP
file, without a live-stream session ID. Moonfin was treating that ID as live TV
and retrying at end-of-file instead of completing movie startup.
All real version IDs remain available to native Emby clients. An unavailable
explicit version still fails instead of silently choosing another file.

This does not enumerate streams while importing or browsing the catalog, save
stream URLs, change item IDs, or require a Moonfin build or a separate listener.
The alias is confined to Undertow's pathless movie/episode channel records with
a header-free HTTP(S) candidate. Header-protected sources fail closed on this
automatic route; explicit native versions still preserve their HTTP headers.
It is appended after the real choices, so the configured version limit remains
the limit on real candidates, with one additional automatic choice.

## Why normal Emby versions work

Moonfin's shared detail screen defaults to the first `MediaSources` ID. Its
Version action appears when that list has more than one entry and displays
those already received entries. A selection reloads the item detail with the
selected ID; it does not first inspect `MediaSourceCount` and fetch a new list.
Both its Jellyfin and Emby playback resolvers send the selected ID to
`PlaybackInfo`. They prefer the matching returned source, then direct play,
direct stream, transcoding and finally the first returned source.

Normal Emby file versions are saved library records grouped under the same
presentation identity. Emby's detail DTO includes their static descriptors.
Undertow's pathless records instead have one static Placeholder, while real
versions arrive later through `PlaybackInfo` and `IMediaSourceProvider`.
Before V0.47.1, pinning that Placeholder matched no dynamic real source.
The automatic alias bridges that request without modifying native Emby routes.

## Limits

**Moonfin's Undertow version menu is not fixed by this change.** Its detail
response still has only the Placeholder. Use the direct upstream Jellyfin
connection for full version selection in Moonfin. Native Emby clients retain
real version selection, plus the automatic choice.

Emby's public `ChannelItemInfo.MediaSources` can publish stored descriptors,
but it does not automatically perform dynamic lookup on an item-detail GET.
Resolving all titles at import would defeat Undertow's lightweight catalog.
A future lazy detail-descriptor implementation needs its own design and tests.
No duplicate native route, private server patch or request interception is used.

The automatic file does not use Undertow's managed selected-source probe;
The player decodes the file's audio, but installed Mac Moonfin's Audio Track
dialog was empty in the bounded test. Default file audio, manual language/track
selection and subtitles need actual-device acceptance; do not claim those menus
are repaired. Emby account audio preferences and real track menus remain available
through probing for explicit native versions.

Native Static=true proxy delivery for managed sources remains unsupported. Direct HTTP and native
Static=false remux need separate client/network qualification. Sources that
require upstream live-session opening/closing remain unsupported. Short decode
and seek checks do not certify sustained playback, subtitle rendering, audio
listening, remote access, Apple TV or iOS.

See [Verification](verification.md) for dated build, deployment and client tests.
