# Native library experiment — October 9, 2026

Branch: `experiment/native-library-infuse`, based on V0.47.0 (`97ed53d`).
This is a bounded lab prototype, not a supported migration or release.

**Result:** ordinary Movie/TV libraries can retain fileless Undertow records
without searching for sources during publication or browsing. Moving those
records out of a channel does **not**, by itself, make them usable in Infuse.
The actual macOS client encountered errors before Play or version selection.

## Decision for the next revisit

Set this approach aside as an Infuse fix. Do not repeat the library-only
experiment merely because ordinary libraries look more compatible than channels.
The blocking evidence is the static item-detail source boundary, not catalog
publication, provider latency or failed HTTP requests.

Revisit if Emby or Infuse changes that boundary, or if there is a concrete way
to supply a usable source on demand for the selected title without resolving
every listed movie/episode. AIOStreams should continue proxying playback; this
test gives no reason to add a separate file player. Any future source-descriptor
solution still needs actual Infuse movie/episode and version-selection tests.

The tested implementation is commit `9703eed`; this report and the redacted
API trace are retained on the
[experimental GitHub branch](https://github.com/OneHotTake/undertow/tree/experiment/native-library-infuse).

## What changed

`NativeLibraryLab` creates standard Movie, Series, Season and Episode records
through Emby's library API. They have stable identities, null paths,
`IsVirtualItem=false`, native parent relationships and an ownership marker.
Two empty directory anchors register ordinary `movies`/`tvshows` libraries.
No STRM/NFO files, saved release URLs, source prefetch, relay, file player,
private route replacement or server patch was added.

The existing selected-source provider recognizes marked Movie/Episode records
and resolves candidates through the saved upstream only when Emby's dynamic
playback callback runs. Selected-source opening uses the existing AIOStreams
delivery path and probes only the chosen file.

Publication is explicit, admin-only and gated by a private marker file. It
rejects snapshots other than one movie, one series and at most three episodes.
Automatic catalog import/migration, artwork enrichment and reconciliation of
a full native library are outside this prototype.

## Isolated environment

Verified October 9, 2026, approximately 07:26–08:07 CDT:

- Emby 4.10.0.40, pinned official image; temporary raw Docker container
  `undertow-native-library-lab-20261009`, LAN binding
  `192.168.1.100:8076 -> 8096`, restart policy `no`.
- Private cloned config `/mnt/fast/configs/undertow-native-library-lab-20261009`;
  the stopped original stock-Jellyfin compatibility lab remained untouched.
- One movie (The Shawshank Redemption), one series (Breaking Bad), one season
  and three episodes. IDs: `641`, `643`, `646`, `647`, `648`, `649`.
  Native library collection-folder IDs: Movies `637`, TV `639`.
- No production media mount. The clone retained its old channel separately;
  all checks here targeted the new ordinary library folders/records.
- macOS Infuse 8.5.5, build 8.5.5752. Temporary share `Native Library Test`
  used a disposable non-admin lab account. The temporary share was removed
  afterward; original Discover and Emby Vault shares remained visible.
- Source connection was copied privately for playback checks. Credentials
  remain in the existing credential service/private lab configuration, never
  in this report or Git. No provider profile was edited.

The branch DLL built against the pinned ABI with **86 passing contract checks**,
zero warnings/errors. SHA256:
`7ed2a28d845bfe5f16a5db37b21eef63421fdc3556d85473d3e3f7b3d3d76bb2`.

## Results

| Check | Observed result |
| --- | --- |
| Publish six content records and list native library views | Zero source lookups, opens or probes |
| Movie/episode item details with `MediaSources,AlternateMediaSources` | Only one pathless `Placeholder` source; no real versions or tracks; zero dynamic callbacks |
| Explicit native `PlaybackInfo`, movie and pilot | Twelve real candidates each, approximately 1.46 s and 1.14 s; two upstream requests, zero opens/probes |
| Open one selected movie source | Approximately 2.24 s; one video track, one audio track, nineteen total tracks; one open/probe, zero probe failures; source closed |
| Actual Infuse folder browsing | Both library folders visible; TV lists Breaking Bad; Movies browsing errors |
| Actual Infuse series details | Series/season/episode requests return 200; client reports “Unexpected server response” before Play/version selection |
| Scoped native validation scans | All six content IDs retained; no additional source requests/opens/probes |
| Lab restart, without republishing | All six IDs retained, paths still null, empty anchors; disposable user's 90-second episode progress retained; startup source counters zero |
| Production protection | Container ID, image and StartedAt exactly matched pre-test receipt |
| Final health, 08:07:26 CDT | Full `verify-stack.sh` snapshot and detailed Mycelium health passed |

There were three outbound playback-source requests in total: the two explicit
PlaybackInfo checks and selected-source revalidation. Infuse browsing/detail
reads added none. The two native provider counters count callback attempts;
`PlaybackRequests` counts outbound upstream requests. Counts reset on restart.

No sustained decode, seeking, HDR, subtitles or physical Apple-device playback
was established. The six-record fixture is not a large-library benchmark.

## What Infuse actually requested

Server debug logging captured these requests during repeated local UI actions.
It was disabled again before restart. All paths below are relative to the
lab's Emby API; `{user}` is the disposable account, not a credential.
The full sanitized trace is [saved here](native-library-infuse-trace.json).

| UTC time | Method and path | Relevant parameters | Actual response |
| --- | --- | --- | --- |
| 12:50:54.066 | `GET /Users/{user}/Items/643` | Fields include `MediaSources,AlternateMediaSources,Path` | 200, 6 ms |
| 12:50:54.134 | `GET /Shows/643/Seasons` | `UserId`, `Fields=Genres,ParentId` | 200, 3 ms |
| 12:50:54.151 | `GET /Shows/643/Episodes` | `UserId`, Fields include `MediaSources,AlternateMediaSources` | 200, 7 ms |
| 12:53:54.933 | `GET /Users/{user}/Views` | — | 200, 1 ms |
| 12:54:00.097 | `GET /Users/{user}/Items/637` | Fields include `MediaSources,AlternateMediaSources,Path` | 200, 2 ms |
| 12:54:00.172 | `GET /DisplayPreferences/usersettings` | `userId` (client preference parameters omitted here) | 200, 0 ms |
| 12:54:00.204 | `GET /Users/{user}/Items` | `ParentId=637`, `IncludeItemTypes=Movie`, `Recursive=true`, `SortBy=SortName`, `SortOrder=Ascending`, `StartIndex=0`, `Limit=50`; Fields include `MediaSources,AlternateMediaSources,Path` | 200, 2 ms |

The observed pre-play window contains **no `PlaybackInfo` or `LiveStreams/Open`
request from Infuse**. Those earlier requests were explicitly made by the test
script and must not be attributed to the client. Authentication requests also
appear in the full trace; authentication bodies/headers were not exported.

Replaying the captured read shapes returned one season and three episodes.
Each episode, and the movie listing, advertised only:

```json
{
  "Id": "mediasource_647",
  "Type": "Placeholder",
  "Path": null,
  "Container": null,
  "MediaStreams": []
}
```

This is a compact summary of the returned source fields, not the complete DTO.
The series and season themselves have no playable media sources.

**Inference:** Infuse rejects the unusable source representation. The API
responded successfully and quickly, but the client error does not identify the
exact rejected field. No Firecore bug or universal Emby incompatibility is
established.

The inspected pinned server implementation corroborates the boundary: item DTOs
call `GetStaticMediaSources`; native PlaybackInfo adds dynamic sources from
`IMediaSourceProvider`. Ordinary Movie/Episode records still follow that split.
Moving them out of an `IChannel` does not change the item-detail callback.

## Implication and rollback

Native fileless publication remains a promising catalog architecture: scans,
identity and user progress work without per-episode provider lookups. Infuse
still needs a usable source descriptor at its detail/listing boundary. AIOStreams
can proxy an already selected source, but that alone does not populate Emby's
static item-detail sources. A future design must bridge that boundary lazily;
catalog-wide resolution would reintroduce the original InfiniteDrive problem.

The lab container is **stopped**, restart policy `no`, and its private config,
build artifacts and redacted receipts are preserved. No production rollout,
release or migration occurred. To repeat, start only this
named disposable container and reconnect a lab account. To abandon, leave it
stopped and discard the experimental branch; no production rollback is needed.

Historical distinction: the September InfiniteTrends fileless-library test had
already established catalog feasibility; April InfiniteDrive's earlier virtual
proposal was deferred, not conclusively disproven. Its later pre-resolved STRM
design incurred provider search/repair costs. [rd-zurg-for-emby](https://github.com/debridmediamanager/rd-zurg-for-emby)
publishes already-known account files; that inventory model does not eliminate
source discovery for a metadata-only catalog's thousands of episodes.
