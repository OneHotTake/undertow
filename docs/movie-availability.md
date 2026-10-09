# Recent movie availability

V0.47.0 adds an optional publication gate for recent movies whose home release is not confirmed. It avoids putting likely dead ends into Emby's native catalog while keeping background source discovery bounded. It is **off by default**, including when upgrading an existing installation.

## Enable it

Open **Emby Dashboard → Plugins → Undertow → Settings → Maintenance**. Enter a **TMDB API key or read access token**, enable **Check recent movies without a confirmed home release**, save and select **Refresh now**. Leaving the credential blank retains it; it is never returned in the page model. On Unix it is saved in a private file with owner-only permissions, separate from the source login.

No AIOStreams setting or shared source profile is changed. This works through Undertow's existing Jellyfin-compatible source API. Sources without valid TMDB movie IDs cannot receive a release-date classification.

## Which movies get searched?

| Movie state | Behavior |
| --- | --- |
| Premiere date is in the future | Withhold until that date; do not search streams in advance. Date transitions apply during catalog refresh. |
| Premiere was less than 365 days ago; no past home release recorded | Require usable candidates from a normal source lookup before publishing. |
| Past digital, physical or TV release recorded | Use ordinary publication; stop special background source checks. This is not proof streams work. |
| Premiere was at least 365 days ago | Use ordinary publication even if release metadata is incomplete. |
| No usable premiere date | Use ordinary publication; do not guess age from the title. |
| Release metadata request fails or no valid TMDB ID exists | Retain an existing entry. Withhold an unverified new recent entry pending evidence. A normal positive source result can establish availability. |
| Locally owned duplicate skipped by the duplicate switch | Keep skipping it; never spend background source checks on it. |
| Series, seasons and episodes | Unchanged; no background availability searches. |

TMDB's release types 4 (digital), 5 (physical) and 6 (TV), in any country, count as home-release evidence. This is separate from source language/quality filters. A foreign release does not establish an English soundtrack. [TMDB release-date API](https://developer.themoviedb.org/reference/movie-release-dates)

Catalog refresh requests release metadata only for recent, already-premiered, nonduplicate movies. The cache lasts 24 hours for unresolved releases; a known past home release needs no repeated lookup. A refresh fetches at most 128 release records and stops after three consecutive failures; the minute task finishes pending metadata. These requests go to TMDB, not indexers.

## Standard source lookup and publication

The native **Check Undertow recent-movie availability** task runs at one-minute intervals. On a first installation, Emby can delay the first automatic interval run by one hour; run this task once in **Scheduled Tasks** to obtain an immediate result. This does not bypass the persisted budgets. It selects a due movie and calls the same `PlaybackInfo` source enumeration and translation used by native Emby playback. It respects the saved source profile, upstream filters, supported delivery types and normal version cap. It does not bypass source filtering, open the video or call Emby's media probe.

- At least one usable candidate: publish or retain the movie in Emby with Undertow's normal version-selection path.
- Successful response with no usable candidates, including placeholder-only responses: withhold a new movie or remove its existing Undertow entry on native channel refresh.
- Timeout, authentication/HTTP failure or malformed response: retain the last positive/empty decision. For a new unchecked title, remain pending. Errors are shown separately from completed empty results.

An already-published movie awaiting its first check stays visible until a conclusive result arrives. New eligible movies stay withheld until a positive result. A future premiere date is an independent reason to withhold an upcoming movie without searching streams.

Only the native channel's pathless Undertow entries are eligible for removal. All catalog metadata remains saved. Native item identities use the same source IDs when entries return. Before removal, a private receipt journals affected native IDs, catalog metadata and per-user native data. It is not a complete server backup. Local files, other libraries, source ownership and requests are not cleanup targets. Emby may remove a hidden entry’s native metadata/artwork directory and fetch it again when the entry returns; artwork on other entries is unchanged.

This does not eagerly prepare permanent URLs for all Emby item details or add Infuse support. Normal native Emby playback provides current versions and probes only the selected file. Durable availability records contain IDs, dates and outcomes—not signed URLs, headers or raw provider messages. A bounded one-minute memory cache coalesces immediate repeat requests for recent movies and retains the original observation time; ordinary titles keep their existing on-demand behavior.

## Search budgets

One movie is due at most once per 24 hours. Background source discovery has both a one-minute minimum interval and a ceiling of **48 attempts in a rolling 24 hours**. Reservations are persisted before HTTP, so failures, cancellation, restarts and repeated manual task runs cannot reset the allowance. Each background attempt has a 45-second bound. Browsing performs no stream searches. Catalog refresh/rebuild does not bypass the source cooldown or trigger source checks for the whole catalog.

Normal playback discoveries update the same result and satisfy the daily check. They remain user-initiated requests, outside the background allowance. One title lookup can query several upstream addons/indexers; the playback version cap limits returned choices, not upstream work. When the queue exceeds the budget, remaining titles wait, ordered by their oldest attempt.

## Status, disabling and limitations

Maintenance shows the candidate-evidence queue, positive/empty/pending counts, upcoming movies, lookup errors, consumed allowance and last completed publication time. Emby's native task history records execution/failure. A failed import stays pending and is retried without resetting source cooldowns. A two-hour catalog interval is not a two-hour stream search interval; the availability task has its own schedule. Cancelling a current task does not disable its future runs.

Disable the switch, save and refresh to republish retained movies under ordinary additive behavior. The local-duplicate switch still applies. Do not restore an older whole database merely to undo this feature; it would rewind newer watch progress. Keep a consistent configuration/database backup before deployment and use the private per-removal receipts for selective recovery if needed.

“Candidates found” means the upstream returned at least one source Undertow can offer. It does not certify the correct film, complete media, track languages, sustained playback or every device's codec support. Availability can change before the next daily check; actual playback remains authoritative.
