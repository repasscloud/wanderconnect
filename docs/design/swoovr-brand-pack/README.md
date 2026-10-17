# Swoovr brand & campaign pack · v1

Start with **PREVIEW.html** in a browser for the offline colour studio. **OVERVIEW.png** collects rendered built-ins and custom variants; 18 palettes are built in. Everything is built from actual vector paths: SVGs contain no embedded bitmap images and do not require installed fonts.

## What's included

- Separate bird, outlined wordmark, horizontal and stacked logos.
- Primary violet, lavender on dark, white on violet, true black and white, violet gradient, six-, seven-, eight- and nine-stripe rainbows, transgender, bisexual, pansexual and non-binary Pride, pink Union Jack, Christmas, New Year and aurora palettes.
- SVG masters; PNG logos at widths 512 / 1024 / 2048 / 4096 px.
- Square and rounded app artwork, circular avatars, PNG application sizes from 16 to 1024 px, multi-resolution Windows ICO and macOS ICNS.
- Opaque unrounded app masters, PWA icons and manifest, a maskable PWA version, Android-style adaptive foreground and background artwork.
- Social squares, portrait posts, stories, link previews, Reddit desktop/mobile banners, X headers, LinkedIn covers, YouTube banners and 4K backgrounds.
- Editable geometry and palette JSON; Python builder; Bash wrapper; wordmark-only TTF font.
- Original concept for comparison and documentation for campaign colours and implementation.

## Quick start

The prebuilt files can be used immediately. Rebuilding requires Python 3.10+, Pillow and Inkscape's command line executable on PATH. FontTools is only needed to rebuild the wordmark font. The build works offline after dependencies are installed.

```bash
python3 -m venv .venv
source .venv/bin/activate  # Windows PowerShell: .venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
./build.sh --theme primary
# Equivalent, including Windows:
python scripts/build.py --theme primary
```

Install Inkscape separately using its installer or your package manager. On macOS, if needed:

```bash
export PATH="/Applications/Inkscape.app/Contents/MacOS:$PATH"
```

SVG-only builds need Pillow imported by the builder but do not need Inkscape. Builds overwrite matching files under the selected output folder; use a new output folder for each campaign to retain older exports.

```bash
./build.sh                           # all 18 presets, full pack
./build.sh --theme dark               # one preset
./build.sh --theme pride-6 --svg-only  # only SVG masters
./build.sh --theme custom --core-only # logos and core app artwork
python scripts/build_wordmark_font.py
```

Flag presets default to balanced soft bands at 90°. Rotate with `--angle 138`, choose `--style bands` for crisp stripes, or use `--style blend` for a continuous gradient. The pink Union Jack is a rotatable pattern. Bird, wordmark and background colours accept up to nine colours.

See **docs/COLOUR-AND-CAMPAIGNS.md** for flag histories, colour balance, rotation and commands. See **docs/ASSET-GUIDE.md** for choosing files.

## Custom colours and foreground/background swaps

Use `./build.sh --config source/custom-examples.json` for Carbon/Surface birds on flag backgrounds and an off-white bird on the pink Union Jack. Custom variants go into `custom-assets/`; a normal `./build.sh` refreshes built-ins in `assets/` only. Use the studio swap button or CLI `--swap bird-background`, with separate layer styles and angles.

After any manual/custom builds, run `./overview.sh` to collect every rendered variant into OVERVIEW.png. Repeat `--input` to include arbitrary output folders. Full commands and config fields are in **docs/COLOUR-AND-CAMPAIGNS.md**.

## Fidelity and typography

The supplied PNG is a concept board, not an original vector source. The bird has been reconstructed as editable cubic curves. The lettering has been vectorised from the large wordmark silhouette and smoothed. This preserves its appearance closely but does not recover the original designer's curves or identify an original font; small geometric differences remain.

`fonts/SwoovrWordmark-Regular.ttf` is an exact rendering of the supplied vector wordmark, exposed as **one private-use glyph U+E000**. It is not a normal typeface: typing `swoovr` does not produce the logo, and it cannot typeset other words. For branding, use the outlined SVG. An exact matching alphabet font cannot be established from this image alone.

The reference's paper texture, lighting and small inconsistencies between birds are intentionally absent from the clean vector production artwork. All generated variants use one consistent bird.

## Source of truth

`source/geometry.json` holds the bird and wordmark paths. `source/themes.json` holds palette stops. Edit those and rebuild; direct edits to generated SVGs will be overwritten. No external font or paid asset is needed to display the logos. This package does not establish trademark clearance or rights in the supplied concept.
