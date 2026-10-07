# Undertow artwork

Generated with the **built-in imagegen tool** on October 6, 2026. The user selected Undertow and an Emby-green palette. The square plugin logo is `../assets/plugin/channel.png` (1254×1254). The home/library card is `../assets/plugin/channel-card.png`; all five folder cards are in `assets/plugin/folders/`. All six cards are1672×941, approximately16:9. Requested dimensions were guidance; these are the actual generated dimensions. Images were copied without resizing or cropping.

Seven PNG resources ship inside Undertow.dll. Native Emby SaveImage installs landscape cards; the square image supplies the plugin thumbnail. Folder cards have no embedded text so configured/translatable native labels remain authoritative. The earlier blue/purple design was replaced during development.

The forum examples were visual references, not copied artwork: [Simple Library Icons](https://emby.media/community/topic/97417-simple-library-icons-pack-v11/), [Emby green template](https://emby.media/community/topic/149253-emby-inspired-green-icons-template/) and the other user-provided community links.

## Exact shared prompt

Use case: logo-brand. Production artwork for Undertow, an AIO media catalog bridge into Emby. Original premium visual system compatible with Emby's green interface. Pure charcoal-black background #111614, striking fresh Emby-like leaf/neon green #52B54B with restrained pale mint highlights. Sculptural clean icon with flowing water-current ribbons, crisp silhouette, subtle rim light, restrained glow. No blue, no purple, no jellyfish, no existing brand logos, no mockup, no watermark.

## Logo suffix

Square 1024x1024 app/plugin logo. Subject: an elegant abstract curling undertow wave subtly forming a right-pointing play aperture, one bold compact emblem. No text. Generous safe margins.

## Home card suffix

Landscape 16:9 library card, 1536x864. Subject: an elegant curling undertow wave subtly forming a right-pointing play aperture, left of center, with the exact word UNDERTOW to its right in refined bold white typography. Calm flowing green current lines across the lower edge. Only text is UNDERTOW. Strong legibility at thumbnail scale, generous safe margins.

## Folder suffix

The supplied Undertow card is a STYLE REFERENCE only. Create one matching landscape 16:9 Emby library card, 1536x864. Subject: {subject}. Place one bold recognizable central emblem, with calm flowing green current lines across the bottom edge. Match the charcoal background and luminous green sculptural material. Generous safe margins and excellent thumbnail legibility. No text, no letters, no wordmark, no square framing. Only this single finished card.

Each folder call supplied the generated home card as a style reference and concatenated the shared prompt with the suffix, substituting the subject below.

| Asset | Subject |
|---|---|
| movies | a sculptural cinematic film reel with a sweeping ribbon of film |
| series | three staggered widescreen television frames with a subtle play aperture |
| anime | an original stylized rising comet and bold starburst crest, elegant Japanese animation energy, no characters |
| anime-movies | a cinematic film reel joined to a sweeping comet crest |
| anime-series | three staggered widescreen television frames joined to a sweeping comet crest |

## README mascot

`../assets/readme-logo.png` is README artwork generated with the built-in imagegen tool. It is separate from the embedded plugin and folder images.

Prompt: “A polished original UNDERTOW logo: an acid-green jellyfish holding a trident and film-director clapperboard, director sunglasses and a sassy expression; tentacles form an undertow spiral. Dark landscape background, ivory wordmark, crisp silhouette, emerald and lime glow. No purple, blue, other brands or watermark.”

## User artwork

Undertow installs a default card only when the folder has no primary image. Sync preserves existing images. It does not refresh installed artwork when embedded assets change.
