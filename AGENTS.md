# Working on Undertow

Read README.md and the relevant docs before changing behavior. Build with scripts/build.sh against the pinned Emby ABI. Keep reference assemblies, binaries, databases, credentials and real playback responses out of Git. Preserve the plugin GUID, channel identity, configuration filename and additive catalog IDs. Probe only the selected file; candidate enumeration must not probe every release. Native settings use Emby's settings API. Back up state before migrations. Report tested behavior and unresolved limits separately.
