# AIOStreams and AIOMetadata

Our household runs Undertow against the built-in Jellyfin-compatible interface in self-hosted AIOMetadata, backed by AIOStreams. These services supply metadata, catalogs and playback sources. Undertow consumes their server API; it does not consume Stremio manifests.

## Connect

1. Enable the Jellyfin-compatible interface in your self-hosted source and configure the catalogs you want to expose.
2. In AIOMetadata, configure MDBLists and other catalog subscriptions. In AIOStreams, configure providers, release filters and result limits.
3. Copy the API address and account details from the source’s **Jellyfin client setup**. Preserve the complete profile path.
4. In Emby → Plugins → Undertow → Settings, save Server URL, Username / profile ID and Password. Use the username from client setup: it may differ from the web editor’s configuration ID.
5. Reload catalogs, choose which to include, mark anime catalogs where needed, then select Maintenance → Refresh now.

An API address with a profile path belongs in Server URL. A manifest document is not the server endpoint. Never post the profile address, credentials or resolved media URLs in an issue.

## Playback and filters

Library sync fetches metadata without resolving media. A detail page can request playback candidates. AIOStreams may resolve the final broker/CDN destination when Emby opens the selected source for its probe and playback. Only the selected file is probed.

Our household profile uses English availability, cached releases, twelve results with a mix of resolutions, and compact 4K choices. Those are personal source settings, not requirements or guarantees of Undertow. A preferred 4K size of 20 GB ranks eligible sources; it cannot manufacture a smaller release or an English dub.

Emby’s preferred audio language applies after the selected file is probed. Disable “play default audio track” when it conflicts with your preference. Remembered selections can override it. Recent unavailable titles in Maintenance may show safe filter counts supplied by the source.

Our profile is shared with Infuse. Editing its catalogs or release policy affects both clients. Use a separate profile if you want independent behavior.

## Tested scope

AIOMetadata 3.2.0 with AIOStreams 2.34.1 was tested on October 6, 2026: library import, additive refresh, stable native IDs, selected-file probing, language selection and short playback. See [verification](verification.md) for timings and open findings. App Store approval has not been tested or promised; a server-side bridge does not establish a client’s distribution eligibility.
