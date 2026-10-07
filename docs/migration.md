# Moving from InfiniteDrive

Undertow is InfiniteDrive's successor. It is a new channel with new item identities, not an in-place database upgrade. InfiniteDrive's repository and code remain available.

Before moving, configure the desired libraries in the Jellyfin source. If using AIOMetadata/AIOStreams, configure its catalogs and MDBLists as described in the source guide. InfiniteDrive's direct list subscriptions are not copied automatically. Neither are its blocks, repair ledger, user saves or watch progress.

1. Stop InfiniteDrive's automatic worker and let any active pass finish. Stop playback before changing the server.
2. Back up Emby's configuration, databases, InfiniteDrive state and managed stream files. Check that the backup can be read.
3. If you want a clean retirement, use InfiniteDrive's own Wipe Library Data / reset action while it is still installed. Review its scope first. It deletes managed stream files and selected plugin tables; it is not a promise that every historical table or metadata file is removed.
4. Stop Emby. Remove `InfiniteDrive.dll`, install `Undertow.dll` and restart.
5. Configure Undertow and refresh its catalog. Test playback before enabling its schedule.
6. Retire old streamed library entries separately through Emby's library controls. Keep owned movie, TV and sports libraries unchanged.

Deleting InfiniteDrive's managed content is optional for other users and destructive. Our household chose a backed-up clean retirement. Undertow itself has no delete-all action.

Rollback requires more than replacing a DLL after a wipe. Preserve the fresh pre-wipe backup, quiesce the server and reconcile newer watch/configuration changes before restoring state. Never restore an old database over a running server.

## Upgrading an existing Undertow installation

Stop Emby, replace the older `Jellyembifier.dll` with `Undertow.dll`, then restart. Do not leave both DLLs installed. Preserve the existing plugin configuration and `jellyembifier` data directory; the GUID, configuration filename and channel identity remain stable. A filename change does not require a library rebuild.

The V0.42.4 Infuse adapter is retired. Remove its extra port publication and saved adapter connection after upgrading. Native Emby clients use the ordinary Emby address and account. Connect Infuse directly to the source instead; see [Infuse](infuse.md).
