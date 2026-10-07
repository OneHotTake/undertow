# How we run it

We run Undertow in Emby against the Jellyfin-compatible interface in self-hosted AIOMetadata, backed by AIOStreams.

AIOMetadata supplies catalogs and metadata. AIOStreams supplies playback candidates and release filters. Undertow imports the library into Emby and requests versions on demand. Emby probes the selected file and handles playback.

## Configure the source

Follow the [AIOMetadata and AIOStreams guide](aiostreams.md). Enable the Jellyfin-compatible interface, choose catalogs, and configure playback filters. Copy the Jellyfin client address and account details into Undertow's settings.

Our profile favors English audio, cached releases, twelve candidates, and 4K files near 20 GB. These are our preferences. They are not plugin requirements. (A size setting won't conjure a smaller file or a missing dub.)

We also tested [stock Jellyfin](jellyfin.md) and [Remux](remux.md). See [Compatibility](compatibility.md) for the required API and [Verification](verification.md) for the results.

## Connect the clients

Use Undertow with Emby's native clients. In Infuse, connect directly to the upstream Jellyfin-compatible source. Keep the Emby connection for owned media. The [Infuse guide](infuse.md) explains the version-picker limitation.

Our source profile is shared with Infuse. Changing its catalogs or release filters affects both clients. Use separate profiles if you want separate behavior.

## Keep private settings private

Store credentials and profile URLs in settings. Redact them from logs and issues. Our deployment tests playback; it does not establish Emby catalog acceptance or App Store approval.
