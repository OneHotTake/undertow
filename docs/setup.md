# Setup

## Prepare the source

Use a Jellyfin server with movie and TV libraries, or a server implementing the required Jellyfin API. Undertow installs in Emby; the source server needs no Undertow plugin.

Enter the server’s base address and a username/password account that can browse and play the chosen libraries. Retain any configured URL base path. Confirm the Emby host can reach the address. A browser on another machine is not a network test for Emby.

For compatible implementations, follow the separate [AIOStreams/AIOMetadata](aiostreams.md) or [Remux](remux.md) guide. Our household uses the built-in Jellyfin-compatible interface in the AIO services. Undertow does not configure their lists or provider credentials.

## Install the plugin

1. Back up Emby's configuration and databases while Emby is stopped.
2. Copy the released `Undertow.dll` into Emby's plugins directory. When upgrading, remove the older `Jellyembifier.dll`; keep its configuration and data. Install one copy of this plugin.
3. Start Emby and open its dashboard. Select Plugins → Undertow → Settings.
4. Enter Server URL, Username / profile ID and Password. Save.
5. Select catalogs. Mark anime catalogs if their entries lack anime-specific IDs.
6. Open Maintenance. Select Refresh now, then Refresh status to check progress.
7. Browse Undertow in Emby and test one movie and one episode. Check audio, subtitles, seek and resume on your actual player.
8. Enable scheduled refresh and save your chosen interval.

Emby's data paths depend on your installation. In the standard Emby container, plugins are under `/config/plugins`; use your mapped host directory. No lab environment switch is required.

## Playback preferences

In the Emby user's playback settings, choose the preferred audio language and turn off playback of the file's default audio track when that conflicts with your preference. Remembered audio selections may override a new preference. Undertow exposes verified tracks after probing the selected file. It cannot create a dub that the file lacks.

For broker-based sources, configure release size, language, codec and availability filters upstream. Undertow's preferred 4K size ranks candidates; it does not exclude oversized files.

## Secrets and recovery

Passwords remain masked in settings. Server URL and username are visible to administrators. Connection data is stored privately under Emby's data directory at `jellyembifier/connection.json`; Unix permissions are owner-only. Keep it and Emby's plugin configuration out of public backups and issue reports.

Once a catalog is published, the source URL and username are locked to protect item identity. Password changes are allowed. Use a separate Emby instance to try a different source; multiple-source support is not implemented.

To uninstall, stop Emby, remove `Undertow.dll` and restart. Preserve plugin data if you want a rollback. Uninstalling does not authorize deleting other libraries or media.

## Infuse

Connect Infuse directly to your Jellyfin source; keep Emby's connection for owned media if desired. Undertow does not add an Infuse listener, connection base path or alternate server identity. See the [Infuse guide](infuse.md).
