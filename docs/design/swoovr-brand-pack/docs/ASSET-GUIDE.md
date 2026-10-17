# Choosing and integrating assets

Every `assets/<theme>/` folder follows the same naming scheme. Size suffixes on logo PNGs refer to **width**, not height. SVGs have logical viewBoxes and can be scaled without pixelation.

| File | Use |
| --- | --- |
| `bird.svg` | Bird alone, transparent, tightly fitted viewBox |
| `wordmark.svg` | Outlined text alone, transparent, tightly fitted viewBox |
| `horizontal.svg` | Main website, email and marketing lockup |
| `stacked.svg` | Centred or narrow compositions |
| `app-square.svg`, `app-square-1024.png` | Opaque unrounded platform master |
| `app-rounded.svg` | Premasked presentation tile, not the Apple submission master |
| `swoovr.ico` | Windows icon: 16, 24, 32, 48, 64, 128, 256 px frames |
| `swoovr.icns` | macOS icon container including Retina representations |
| `app-*.png` | Individual application icon sizes, opaque |
| `adaptive-foreground.svg` | Bird inside a generous central safe region on transparent 1080 canvas |
| `adaptive-background.svg` | Full opaque 1080 canvas |
| `pwa-maskable-512.png` | PWA icon with additional safe padding |
| `manifest.webmanifest` | Merge icons into your application's existing manifest |
| `avatar.svg` | Circular avatar preview; transparent corners |
| `social-square.svg` | 1080 × 1080 post |
| `social-portrait.svg` | 1080 × 1350 portrait post |
| `story.svg` | 1080 × 1920 story / vertical campaign canvas |
| `open-graph.svg` | 1200 × 630 link-preview canvas |
| `x-header.svg` | 1500 × 500 header canvas |
| `linkedin-cover.svg` | 1584 × 396 cover canvas |
| `youtube-banner.svg` | 2560 × 1440 banner; logo in central crop region |
| `wallpaper.svg` | 3840 × 2160 background |

These social exports are useful canvas presets, not a live specification feed. Review crops, avatar overlays, mobile previews and any current platform upload requirements before publishing. The pack supplies branded backgrounds without invented campaign copy.

Use `purple` for white birds on violet tiles matching the left concept icon; use `dark` for lavender birds on dark tiles matching the right. `primary` gives the violet bird and dark lettering on white marketing backgrounds. `black` and `white` are true monochrome variants.

## Layout rules

Keep the native proportions. Do not stretch either mark. Leave at least a quarter of the bird's height as external clearance in normal layouts; the tightly cropped standalone masters do not include this margin. Review the thin tail at favicon sizes. The supplied tiny icons use the same master; a separately art-directed simplified micro-icon may be desirable after real device testing.

Use the white wordmark on dark canvases and dark wordmark on light canvases. Campaign gradients should not make the mark disappear against its background. The scripts allow arbitrary valid palettes and do not automatically guarantee contrast.

## Platforms

- **Windows:** point your application/build configuration at `swoovr.ico`.
- **macOS:** use `swoovr.icns`. For an app store release, check the final iconset against that submission workflow.
- **Apple mobile:** start with opaque `app-square-1024.png`; let the platform apply the corner mask. This is artwork, not an Xcode asset catalog.
- **Android:** use the separate adaptive foreground/background artwork as input to your platform asset tooling. These SVGs are not Android VectorDrawable XML. Export/convert into the formats required by your Android build. Full Android density resource folders are not generated.
- **PWA:** copy the listed PNGs beside the manifest or update its relative paths; merge the `icons` list with the existing site manifest. Supply your own start URL, display mode and other application metadata.

## Web

```html
<img src="/brand/primary/horizontal.svg" alt="Swoovr" width="290" height="90">
<link rel="icon" href="/brand/purple/swoovr.ico">
<link rel="apple-touch-icon" href="/brand/purple/apple-touch-icon.png">
```

The wordmark SVG contains paths rather than live text. Keep accessible names in the surrounding application markup.

## Wordmark-only font

The bundled TTF maps **U+E000** to the whole wordmark. Install it like any TTF or use it on the web:

```css
@font-face {
  font-family: 'Swoovr Wordmark';
  src: url('/fonts/SwoovrWordmark-Regular.ttf') format('truetype');
}
.swoovr-wordmark { font-family: 'Swoovr Wordmark'; font-weight: 400; }
```

```html
<span class="swoovr-wordmark" role="img" aria-label="Swoovr">&#xE000;</span>
```

This font is a logo convenience, not a general text font and not an identification of the original concept's typeface. For headings, body copy or an expanded branded alphabet, select a separately licensed typeface or commission lettering. SVG remains the preferred production logo format.

## Rebuild verification

The builder validates every PNG immediately after rendering and writes `assets/checksums.json` with SHA-256 hashes. ICNS and ICO files are created using Pillow. Rebuilds may vary at a byte level with renderer versions; use the supplied prebuilt assets when exact byte identity matters.
