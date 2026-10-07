# Compatibility

## Tested on October 6, 2026 — Undertow V0.42.3

- Emby 4.10.0.40, with references taken from the exact pinned server image.
- Stock Jellyfin 12.2.0: one movie, one series, five seasons and 44 episodes imported from read-only local media. Metadata-only refresh preserved all 51 content IDs. The movie and pilot each decoded two seconds with English audio through Emby.
- Self-hosted AIOMetadata 3.2.0 backed by AIOStreams 2.34.1: full exposed catalog import, metadata refresh, version selection, selected-file probing and short web playback.
- Remux 0.34.0 / catalog 1.0.11: bounded Matrix and Breaking Bad pilot import, selected-file probing and short native remux decoding. The owner also reports client playback and correct language selection after updating Emby account preferences.

These three implementations have been tested. Other Jellyfin-compatible servers should work if they expose the following subset, but need their own test. V0.42.3 sends standard `Authorization: MediaBrowser` headers alongside legacy compatibility headers. Stock Jellyfin 12.2.0 rejected the legacy-only login used in V0.42.2.

## Required API behavior

- Username/password login at `Users/AuthenticateByName`, returning a user ID and access token.
- User views and paginated item listings at `Users/{user}/Views` and `Users/{user}/Items`.
- Series seasons and episodes at `Shows/{series}/Seasons` and `Shows/{series}/Episodes`.
- Playback sources at `Items/{item}/PlaybackInfo` with stable source IDs and usable HTTP paths, or a working authenticated `Videos/{item}/stream` endpoint.
- Stable movie, series, season and episode identities, types and numbering.

The adapter preserves direct HTTP headers. Filesystem or virtual paths are translated into upstream stream endpoints; they are not opened as local Emby files. Sources requiring upstream live-session opening/closing are skipped. Private saved-token authentication was used in the Remux lab; expired tokens require renewal. The public settings page supports password login, not token editing.

## Known gaps

The native `Static=true` proxy path hits an unsupported copy operation in the selected-stream implementation. Tested `Static=false` remux delivery works. Do not claim every client's direct-play path works.

ASS subtitle rendering, broad Apple TV/iOS acceptance, sustained playback, HDR/Dolby Vision, expiry recovery and upstream restart identity stability remain unverified or unresolved. One upstream anime series returned mismatched adaptation streams; catalog classification cannot repair incorrect upstream identity.

There is no multi-source merge, upstream watch-state sync, arbitrary-title search or automatic purge. Emby retains its own watch state for Undertow items.

## Infuse

Undertow targets Emby's native clients. Infuse can connect directly to the upstream Jellyfin server, AIOMetadata's Jellyfin-compatible interface or Remux. Undertow's optional Infuse adapter is retired.

An actual pre-play capture on Mac Infuse 8.5.5 showed both connections requesting item details with `MediaSources` and `AlternateMediaSources`. AIOMetadata returned twelve real sources; the native Emby lab returned its two stored fixture sources. Neither capture contained a PlaybackInfo request. The difference was how the servers filled the requested fields, not a demonstrated difference in client timing.

Emby's generic item-details path reads persisted sources. Undertow's on-demand versions use channel playback callbacks. A native-route experiment played a stock movie and pilot and displayed two choices, but both choices represented the same file. It did not establish twelve real versions before first playback without additional discovery machinery. No Firecore defect was established.

See [Infuse](infuse.md) for direct setup. Ordinary Emby playback remains the supported bridge path; direct Infuse connections have their own server and client behavior.
