# Jellyfin to Emby

Undertow installs in **Emby** and connects to a **Jellyfin server**. Jellyfin owns the source libraries and files. Emby owns the imported channel, local watch state and playback delivery. No Undertow plugin is required on Jellyfin.

## Set up the source

1. Start Jellyfin and create movie and TV libraries containing real media files. In a container, mount the media folders; read-only mounts are sufficient for this bridge.
2. Create an account that can see and play the chosen libraries. Avoid granting administration rights to the bridge account unless you need them separately.
3. Copy the Jellyfin server’s base address. Include its URL base path if configured. Ensure Emby can reach that address.
4. Install Undertow **V0.42.3 or later** in Emby. Save the address, account username and password in its native settings.
5. Reload catalogs, select libraries, then run Maintenance → Refresh now.
6. Browse Movies and Series. Test a movie and an episode on the Emby client you intend to use before enabling scheduled refresh.

[Jellyfin’s container guide](https://jellyfin.org/docs/general/installation/container/) explains mounts and persistent server state. [Undertow setup](setup.md) covers installation and recovery. For compatible implementations, use the separate [AIOStreams/AIOMetadata](aiostreams.md) or [Remux](remux.md) guide.

## How playback works

Library refresh requests metadata, not stream URLs. On a playback-source request, Undertow asks Jellyfin for PlaybackInfo. A local filesystem path is translated into Jellyfin’s authenticated stream endpoint for the exact media-source ID. Emby does not need access to Jellyfin’s filesystem.

When Emby opens the selected source, it probes that source and receives the file’s real audio/subtitle tracks. Emby then supplies media to its client using its own playback pipeline. There is no all-version probe or second acquisition service. Watch progress remains in Emby; it is not forwarded to Jellyfin.

## Verified October 6, 2026

The official Jellyfin image reported **12.2.0**. Undertow V0.42.3 on Emby 4.10.0.40 imported Straight Outta Compton and Ted Lasso: one movie, one series, five seasons and 44 episodes. All 51 native content records lacked local filesystem paths. Metadata-only refresh preserved their IDs and caused zero playback requests or probes.

The movie and season-one pilot each returned one media source, opened and probed only that source, selected English audio index 1, and decoded 48 frames over two seconds through native Emby remux delivery. Both sources closed without probe failures. The small initial import took about 0.52 seconds with internet metadata providers disabled upstream; it is not a full-library or cold-metadata benchmark.

The test found a real compatibility defect: Jellyfin required the standard authorization header. V0.42.3 supplies it alongside legacy headers and preserves token-free direct CDN requests. Earlier releases were not validated against stock Jellyfin.

This proves a bounded bridge path. It does not prove every Jellyfin release, physical Apple TV/iOS behavior, direct-play delivery, long seeking, HDR or subtitle rendering. See [compatibility](compatibility.md) and [verification](verification.md).
