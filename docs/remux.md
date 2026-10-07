# Remux

Remux exposes a Jellyfin-compatible server API. Undertow can use that interface to bring its libraries into Emby. Remux remains responsible for its addons, catalog and media availability.

## Connect

1. Start Remux and configure its catalog and playback sources.
2. Create or choose a Remux account with access to the libraries and media you want.
3. Save the Remux server’s base address, username and password in Undertow’s native Emby settings. Preserve any configured base path.
4. Reload catalogs, select libraries and run Maintenance → Refresh now.
5. Test one movie and one episode, including audio selection, before enabling the refresh schedule.

Use a fresh Emby instance to test a different source. Undertow supports one source per instance; it locks source URL and username after publishing a catalog to protect item identity.

## Delivery and language

Upstream filesystem or virtual paths belong to Remux. Undertow translates them into authenticated Remux stream endpoints for the exact media-source ID. Absolute HTTP sources keep their supplied headers. Sources requiring upstream session opening/closing are unsupported.

In Emby, select your preferred audio language and disable playback of the file’s default track when it conflicts. Remembered audio selections may override the preference. Undertow probes only the selected file and exposes its actual tracks. It cannot play a language that is absent.

The lab used private saved-token authentication during early testing. Public settings support username/password login; no token editor is exposed. An expired saved token needs renewal in the private connection store.

## Tested scope

Remux 0.34.0 / catalog 1.0.11 was tested on October 6, 2026 against Emby 4.10.0.40 and Undertow V0.42.2. The Matrix and Breaking Bad’s pilot imported, returned versions, exposed real audio tracks and decoded two seconds through Emby’s native remux delivery. The owner also confirmed playback and language selection after changing Emby account preferences. See [compatibility](compatibility.md) for remaining delivery and client gaps.
