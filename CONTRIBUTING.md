# Contributing

Build against the pinned Emby ABI and run the contract checks. Use an isolated Emby server for native tests. Include a movie, an episode, metadata-only refresh and selected-source playback in compatibility reports. Redact credentials, private profile paths and signed URLs. Keep prose plain: say what changed, what passed and what remains uncertain.

## Repository layout

- `src/Undertow/`: plugin source, native settings UI and project file.
- `assets/`: README artwork and embedded plugin images.
- `docs/`: setup, compatibility, verification and artwork notes.
- `tests/`: native contract checks.
- `scripts/`: build against the pinned Emby image.

Run `bash scripts/build.sh` from the repository. Build output goes to ignored `artifacts/`; extracted Emby references go to ignored `libs/`. Neither belongs in Git.

The internal namespace, configuration filename and channel identity retain their original names to preserve existing installations. The project and shipped assembly are named Undertow. Retired Infuse prototypes are excluded from the public launch snapshot.
