# Undertow

![Undertow jellyfish with a trident](assets/readme-logo.png)

Undertow brings Jellyfin libraries into Emby. Install the plugin in Emby, connect a source, and browse its movies and series in Emby's native apps.

It is the successor to [InfiniteDrive](https://github.com/OneHotTake/InfiniteDrive). We run it in production. We also break it in production. See [How we run it](docs/how-we-run-it.md) for our setup.

**Target: Emby 4.10.0.40. One source per Emby instance.**

## Why we replaced InfiniteDrive

InfiniteDrive built a library from addon manifests. It wrote metadata and stream files, expanded episodes, and repaired expired links. We spent too much time maintaining the plumbing.

Jellyfin already supplies libraries, movies, series, seasons, episodes and playback sources. Undertow imports that structure into Emby and requests versions when needed. The source manages its catalog. Emby handles metadata, users and playback.

InfiniteDrive's code stays available. Undertow does not migrate its database, lists, blocks or watch history. Read the [migration guide](docs/migration.md) before removing the old library.

## What it does

- Imports movies, series, seasons and episodes into an Emby channel.
- Separates anime movies and series using anime IDs or catalog origin.
- Refreshes metadata manually or on a schedule.
- Keeps existing titles when an upstream catalog changes. Sync is additive.
- Requests playback versions on demand and probes only the selected file.
- Uses Emby's metadata providers, audio preferences and playback pipeline.
- Shows recent unavailable-source diagnostics in Maintenance.
- Includes default folder artwork in the DLL; preserves existing images.

Catalog refresh fetches metadata. It does not resolve streams or write STRM and NFO files. Candidate searches can query upstream addons and indexers. The selected file's probe may resolve its final URL before playback.

## Install

1. Download `Undertow.dll` from the [latest release](https://github.com/OneHotTake/undertow/releases/latest).
2. Stop Emby and copy it into the plugins directory. Remove the older `Jellyembifier.dll` if present. Keep the existing configuration and data; install one copy.
3. Restart Emby. Open **Dashboard → Plugins → Undertow → Settings**.
4. Enter the source's server address, username and password. Some compatible servers use a profile ID as the username.
5. Choose catalogs, then select **Maintenance → Refresh now**.
6. Test a movie and an episode. Set the refresh interval when ready.

See [Setup](docs/setup.md) and [Settings](docs/settings.md) for details.

## What we tested

We tested stock Jellyfin 12.2.0 and other compatible servers. The stock Jellyfin test imported one movie and a series with five seasons and 44 episodes. The movie and pilot played through Emby. V0.42.3 added the authorization header required by that server.

V0.45.7 passed 39 contract checks. Earlier V0.42.5 production checks returned twelve versions for a movie and an episode, selected English audio, and preserved the existing library IDs.

Other Jellyfin-compatible servers should work if they implement the [required API](docs/compatibility.md). Test yours. Short decoder checks do not prove a whole film, HDR, subtitles, seeking or every Apple device. See the [verification report](docs/verification.md) for dated results and gaps.

## Infuse

Connect Infuse directly to the Jellyfin source. Keep its Emby connection for owned media.

Infuse reads versions from item details. Emby returns stored sources there; Undertow discovers versions through Emby's playback callbacks. We tested workarounds and retired them. Preparing versions for the entire catalog would defeat lightweight sync.

Use Emby's native clients for Undertow. See the [Infuse guide](docs/infuse.md) for the evidence and direct connection setup.

## Limits

- One upstream source per Emby instance.
- Native Emby search covers imported titles only.
- Sync does not purge titles or forward watch progress upstream.
- Sources requiring upstream session opening and closing are unsupported.
- A native static-proxy path fails. Remux delivery works in the tested cases.
- ASS subtitle rendering remains unresolved.

## Build and report bugs

Run `bash scripts/build.sh` on a Docker host. It extracts references from the pinned Emby 4.10.0.40 image, builds with .NET 8 and runs the contract checks. It does not restart Emby or include Emby assemblies in the release.

Use sources you are authorized to access. File bugs in [Issues](https://github.com/OneHotTake/undertow/issues). Include versions and redacted logs. Keep credentials and signed stream URLs out of them.

## License

MIT. See [LICENSE](LICENSE).

Vibe coded with Codex, Claude, and whoever still had a free trial. I put it in production, so apparently I'm the adult supervision. Guaranteed to have bugs. Probably some I haven't met yet. Good luck. Bring logs.
