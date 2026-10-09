# Verification

## Experimental native libraries — October 9, 2026

The local `experiment/native-library-infuse` branch passed 86 contract checks.
An isolated Emby 4.10.0.40 lab published six standard, fileless library records,
preserved their IDs and progress across validation scans/restart, and resolved
and opened one selected source through the existing upstream path. Publication
and Infuse browsing made zero source requests. Actual macOS Infuse could see
the libraries but failed before Play; its item-detail reads still received
pathless placeholders rather than dynamic candidates. This is a failed Infuse
acceptance gate, not released support. See the [experiment and API trace](native-library-experiment.md).

## V0.47.0: recent-movie availability — October 8, 2026

Built against pinned Emby 4.10.0.40 references; **82 contract checks passed**, with zero build warnings/errors. Checks cover release-date scope, upcoming dates, positive/empty/error publication decisions, keeping existing pending entries, withholding new unchecked entries, durable daily/minute/rolling limits, concurrent lookup coalescing, expiry and malformed/error responses.

Production Emby 4.10.1.0 loaded the local V0.47.0 build with the optional gate enabled. The exact initial queue was 18 already-premiered recent movies; twelve future-premiere entries were withheld during native refresh without source enumeration. The first standard lookup returned no usable candidates for Heart of the Beast and its pathless Undertow entry was removed. No positive live source result had been observed at the 20:09 CDT checkpoint; the positive publication branch is covered by contracts. Existing untested titles were retained pending evidence.

All 14,421 file-backed native IDs/GUIDs/paths and all 73 user-data rows remained exact, SQLite quick_check passed, and the saved source connection was unchanged. The full saved catalog remained intact. Zero native source opens/probes occurred. Full stack reachability/security and detailed Mycelium health passed at 20:12 CDT. Maintenance rendered the enabled switch, bounded-search explanation, empty result and private blank credential field. Physical clients and sustained playback were not retested.

At 20:18 CDT, the native interval task had run automatically after restart, preserving its consumed allowance. Two empty results and twelve future premieres accounted for all fourteen removed native entries, with zero removals outside the gate. File-backed identities, user data and source settings still matched. Full stack/security/detailed Mycelium health passed again at 20:18 CDT. The final deployed DLL SHA256 is `d54b2a76dd8722ba5f3a18ad9f3c378cbb97caea21518d7880940c260d135b9a`.

Emby's native interval implementation can delay a task's first automatic run by one hour when no execution history exists; the guide documents the standard manual task start for an immediate first result. Persisted budgets still apply.

The owner-requested production full rebuild completed at 20:23:59 CDT in 19.24 seconds. All 406 series were fetched again through the standard metadata path. The snapshot retained 493 movies, 406 series, 2,242 seasons and 36,885 unique episodes. All 14,421 file-backed identities/paths and 73 user-data rows remained exact; SQLite quick_check passed. Connection/settings and all five previously consumed availability attempts were preserved. No additional source lookup/open/probe occurred during the rebuild. Full stack/security/detailed Mycelium health passed at 20:23:48 CDT. Upstream metadata caches may still apply.

At 20:40 CDT, all 18 eligible recent movies had evidence: one positive (Runner), seventeen empty, zero pending and zero errors. Native catalog checks confirmed the positive title present, all seventeen empty titles absent and all twelve future-premiere titles absent. Seventeen background attempts were consumed; one user playback lookup supplied the remaining result outside the background allowance. SQLite quick_check and full stack health passed; no video opened or probed.

The V0.47.0 release build passed all 82 checks again with zero warnings/errors and reproduced the exact deployed DLL SHA256 above. Download only `Undertow.dll`; preserve existing configuration and data when upgrading. The TMDB credential is needed only for the optional availability gate and supplies release dates, not streams.

## V0.46.1: whole-title duplicate suppression — October 7, 2026

Built against pinned Emby 4.10.0.40 references; **57 contract checks passed**. Production Emby 4.10.1.0 loaded V0.46.1 with the switch enabled. Its native refresh matched 132 movies and 23 series, omitting each matching series in full (1,948 cached episodes). Episode coverage is deliberately not checked. Native refresh removed 132 movie duplicates, then 2,125 additional series/season/episode records. The complete 837-title catalog snapshot remains retained.

The native settings switch saved/reloaded. Disabling and refreshing republished retained titles; re-enabling and refreshing removed 2,224 channel records in the repeat cycle. All 14,720 baseline path-bearing IDs/GUIDs/paths and 70 native user-data rows remained exact; database quick_check passed. No stream opened or probed. Local-file cleanup is a separate task. V0.46.1 was subsequently published on October 7; physical-client playback was not repeated.

## V0.45.7: hourly structure settings

The season/episode cache interval now uses hours (1–720), with a six-hour default for new installs. Existing day settings retain their duration when saved and reloaded as hours. All 39 contract checks passed, including legacy XML migration.

Production catalog and structure intervals are explicitly set to two hours. Production refresh of 824 titles and 344 series completed in about 9.05 seconds, with zero native stream probes or opens. Full stack health passed. This is a warm-cache metadata test, not a sustained playback test.

## V0.45.6: source cleanup

Built against pinned Emby 4.10.0.40 references. All 37 contract checks passed. One fixture-only check was removed with the retired fixture configuration; the native metadata, catalog, playback and embedded-artwork checks remain.

Source now lives in `src/Undertow/`. The project is `Undertow.csproj`; the release remains `Undertow.dll`. Assets, docs, tests and build scripts have separate directories.

The scan removed the always-on lab gate, explicit movie/episode fixtures, pre-sync fallback browsing, first-sync demo imports, hidden environment configuration and unused test routes. One admin-only status route remains because the production portal reads it. Native settings own refresh and maintenance.

Folder artwork is installed only when a primary image is missing. Existing artwork is preserved. Stable plugin, channel and configuration identities are unchanged.

V0.45.6 was deployed on October 7, 2026. Production movie and pilot checks each returned twelve candidates, selected English audio index 1 and decoded 48 frames over two seconds through native Emby remux. Two selected-file probes completed without native probe failures; close requests completed. All 227,396 pre-deployment library IDs were retained, including 13,944 owned-media IDs/paths. SQLite quick-check and stack health passed. The saved connection was unchanged.

The deployed DLL matches the release. These bounded checks do not establish sustained playback or physical Apple-device acceptance. Artwork was being edited separately; deployment did not replace images or use a full sync to compare concurrent changes.

## V0.42.5: earlier production acceptance

The Shawshank Redemption and Breaking Bad pilot each returned twelve candidates. Both decoded 48 frames over two seconds through native Emby remux with English audio selected. Two selected-file probes completed without native probe failures. All 13,944 owned-media IDs/paths matched the pre-deployment backup; SQLite quick-check passed. These bounded checks do not establish long playback or physical Apple-device acceptance.

## Earlier live evidence

On October 6 the AIO lab imported 821 top-level titles and 38,103 visible native content records. Initial import took about 5m9s; a warm refresh took about 19s. These timings include warm upstream caches and apply to that profile. Metadata refresh preserved native IDs and resolved/probed zero streams.

Candidate enumeration returned twelve choices without native probing. Opening a selected file invoked one native probe and exposed its real language tracks. English selection worked in tested web playback after account preferences were set; duplicate-language tracks explicitly titled Commentary are suppressed when another soundtrack in that language exists.

Remux imported The Matrix and Breaking Bad S1E1 and decoded two seconds through native remux delivery. The owner subsequently confirmed playback and preferred-language selection. This is bounded acceptance, not a universal playback guarantee.

See [compatibility](compatibility.md) for the static-proxy and subtitle gaps. Physical Apple TV/iOS coverage, sustained playback and long seeks remain incomplete.

## Production dogfooding: October 6, 2026

At this checkpoint, the household production Emby server ran V0.42.1, after the owner wiped InfiniteDrive and removed its virtual library registrations. Owned-media IDs and paths matched the pre-cutover baseline for 13,942 records. The native library database passed its integrity check.

The first production sync saved 475 movies, 344 series, 2,109 seasons and 35,224 distinct episodes. Listing took 0.59 seconds, season/episode metadata 5.99 seconds, and native Emby import 408.67 seconds: about 6 minutes 55 seconds overall. The upstream cache was already warm; this is not a cold-cache benchmark. The native database contains 85 additional episode records sharing upstream IDs, an open dogfooding finding rather than 85 extra episodes.

Movie and episode checks each returned 12 candidates and decoded 48 video frames with English audio through native Emby remux delivery. Only two selected files were probed; both sources closed, with zero probe failures. The checks passed again after installing the branding patch. Automatic sync is enabled every six hours, with a seven-day series cache.

V0.42.1 applies visible channel branding during browsing, before a long import completes. Stable internal IDs and configuration paths remain unchanged. The operations portal reports saved catalog counts and playback counters; the retired InfiniteDrive controls no longer forward actions. Physical Apple-device and sustained playback coverage remains incomplete.

A repeat production sync took 13.18 seconds, with no additional playback requests, source opens or native probes. The non-authenticated stack health snapshot passed after the final restart.

## V0.42.2: recent unavailable titles

37 contract checks pass, including safe placeholder parsing, bounded ten-entry retention and clearing a title after a successful lookup. Native Maintenance displays Digger and Hope’s upstream quality/resolution rejection counts. The same checks pass in the AIO lab and production without source opens or probes. Only existing source lookups populate the in-memory list; there is no fallback video or background search.

Production movie and episode playback still returns twelve versions and decodes 48 frames with English audio through native Emby remux delivery. Both selected sources close with no probe failures. Owned-media IDs and paths match the pre-cutover baseline for 13,942 records; database integrity passes.

## Production V0.42.3 — October 6, 2026, 20:39 CDT

The household server now loads the exact V0.42.3 release DLL. Connection and settings match the fresh backup byte for byte. Shawshank and Breaking Bad’s pilot each returned twelve versions, selected English audio index 1 and decoded 48 frames over two seconds through Emby remux delivery. Two selected sources opened, probed and closed with zero probe failures.

All 13,942 owned-media baseline identities and paths remain. One additional indexed episode raises the current count to 13,943; database integrity passed. Stack health passed and the operations collector reports Undertow available. The existing AIO source and refresh schedule were retained. Physical Apple-device coverage remains incomplete.
