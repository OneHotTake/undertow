# Infuse

An October 9 [native-library experiment](native-library-experiment.md) repeated
the test using ordinary Movie/TV libraries instead of channel parents. Infuse
still received pathless placeholders in item details and errored before Play.
The captured requests returned HTTP 200 and triggered no dynamic-source lookup.
Native library publication alone does not remove the source-descriptor gap.

Connect Infuse directly to the Jellyfin source. Undertow remains an Emby plugin for Emby's native clients; its Infuse adapter is retired.

## Direct connection

1. Add a Jellyfin connection in Infuse to the source's address, port and any required URL base path.
2. Sign in with that source's account. An Emby account is not an upstream Jellyfin account.
3. Browse the libraries and test a movie and an episode. Check versions, audio, subtitles and seeking on the actual device.

Use the [AIOMetadata/AIOStreams guide](aiostreams.md), [Remux guide](remux.md) or [stock Jellyfin guide](jellyfin.md) for source configuration. AIOMetadata supplies a Jellyfin-compatible facade; it need not run a stock Jellyfin daemon. Keep Infuse's ordinary Emby connection if you use it for owned media. No Undertow listener or added connection path is needed.

Infuse owns audio-track selection on a direct connection. Set its preferred audio language explicitly and use the playback Audio menu when needed. A missing or mistagged dub cannot be fixed by an Emby language setting.

## Why the adapter was retired

We captured Mac Infuse 8.5.5 opening a movie through both Discover (AIOMetadata) and the native Emby lab. Both requested item details with `MediaSources` and `AlternateMediaSources`. Discover's response contained twelve real sources. Emby's response contained its two stored test sources. Neither pre-play window contained a PlaybackInfo request.

AIOMetadata's single-item handler can discover and return real versions in that response. It skips this work for catalog listings. Emby's generic item-details handler reads stored media sources; Undertow's real candidates arrive through channel playback callbacks. Discovery may query upstream addons and Usenet indexers. Avoiding video downloads or probes does not make those searches free.

A private native-route experiment played a stock movie and pilot through the ordinary Emby URL and displayed a version menu. Its two choices deliberately pointed to the same movie file. It proved native presentation and routing, not the required twelve real choices before first playback.

A separate-port adapter also worked in bounded movie tests, but required another connection and presentation identity. Further attempts added private server hooks or moved discovery into extra machinery. We chose to keep the plugin small and use Infuse's direct Jellyfin connection. We did not establish a Firecore bug or a universal incompatibility with Emby channels.

Retired experiments are excluded from the public launch snapshot. Their results are not current support or installation instructions. Cross-client watch-state synchronization, episode version selection and broad Apple-device acceptance were not established by those tests.
