# Settings

Undertow uses Emby's native settings API. There are two pages. Connection and maintenance fields save with the page's Save action; catalog switches save immediately.

## Connection & Catalogs

| Setting | Meaning |
| --- | --- |
| Server URL | HTTP(S) Jellyfin server address, including any URL base path. Compatible sources may require a profile path. URL credentials, query parameters and fragments are rejected. |
| Username / profile ID | Jellyfin account username. Compatible sources may supply a profile ID instead; follow their setup guide. Visible to administrators. |
| Password | Required for a new password-based connection. Leave blank to retain the saved password. |
| Movies, Series, Anime, Anime Movies, Anime Series | Five folder labels. Each accepts 1–100 characters. Renaming preserves folder identity. |
| Include | Include a catalog in future syncs. At least one must remain selected. Existing titles remain when a catalog is disabled. |
| Mark Anime | Treat that catalog's entries as anime even without anime-specific IDs. Existing anime classification is retained by additive sync. |
| Test saved connection / reload catalogs | Read available catalogs using the saved connection. Save edited credentials before testing. |

Anime-specific IDs take precedence over TMDB/TVDB IDs. A marked anime catalog also establishes origin. Animation genre or a missing TMDB ID does not prove anime.

## Maintenance

| Setting or action | Meaning |
| --- | --- |
| Enable scheduled refresh | Controls automatic refresh. Manual refresh remains available when the schedule is off. |
| Refresh interval | 1–168 hours; default 6. Emby's task checks hourly, then refreshes when due. |
| Refresh existing seasons and episodes | 1–720 hours; default 6. New series fetch on the next sync. Existing structure uses this cache period. |
| Recent unavailable titles | The last ten failed source lookups, with safe upstream filter counts where available. Memory only; cleared at restart or when that title returns playable versions. Uses existing playback requests. |
| Refresh status | Reload counts, last successful sync, duration, due time and current stage. Counts describe the saved snapshot, including retained titles. |
| Refresh now | Fetch selected catalog membership, recheck due series and update Emby. |
| Cancel current refresh | Request cancellation. A committed snapshot may already exist; native import can finish only partly and needs a retry. |
| Rebuild catalog | Refetch all selected catalogs, seasons and episodes, bypassing Undertow's structure cache. Keeps titles and watch progress. Upstream caches may still apply. |
| Maximum playback versions | 1–50; default 12. Caps returned choices without changing upstream filters. |
| Preferred 4K size | 1–200 decimal GB; default 20. Ranks 4K candidates nearest this size first. Emby may reorder them for the player. |

Older saved day intervals migrate to their equivalent hours. New installations default to six hours for both catalog and series structure. Save interval and playback settings before expecting them to apply. Rebuild is a metadata recheck, not a delete-all button. A title disappearing upstream is not permission to delete it locally.

There are no Infuse bridge or listener-port settings. Connect Infuse directly to the Jellyfin source. Old adapter configuration fields are retired; see [migration](migration.md).

Folder artwork is a default. Undertow installs it only when a folder has no primary image. Sync preserves existing artwork, including images you upload.
