# Colours, flags and rotation

Open `PREVIEW.html` for the offline studio. Choose a palette, bird treatment, colour balance and paint angle, then download SVG or PNG. The CLI and studio use the same palette data and matching paint calculations, checked by regression tests. All generated artwork uses the master bird in `source/geometry.json`.

## Quick commands

Run these inside the brand-pack folder, with Pillow installed and Inkscape on PATH:

```bash
# Your diagonal trans treatment, with balanced blue/pink/white coverage:
./build.sh --theme pride-trans-5 --angle 138

# Original eight-colour rainbow, with horizontal colour bands:
./build.sh --theme pride-8 --angle 90

# Lavender-first nine-colour version, diagonally:
./build.sh --theme pride-9 --angle 138

# Crisp bisexual bands, retaining the 2:1:2 colour-share ratio:
./build.sh --theme pride-bi --style bands --angle 45

# Pink Union Jack pattern, rotated inside the bird:
./build.sh --theme pink-union-jack --angle 25

# Separate output keeps the standard exports intact:
./build.sh --theme pride-pan --angle 138 --output campaigns/pan-diagonal

# Regenerate the complete pack, offline studio and overview:
./build.sh
python scripts/make_preview.py
python scripts/make_contact_sheet.py
python scripts/verify_assets.py assets
```

Builds overwrite matching files in the output folder. An `--output` campaign folder is the easiest way to keep several rotations. The studio uses standard primary templates from `assets/`; the overview discovers both `assets/` and `custom-assets/` by default. `--svg-only` skips raster/container exports; `--core-only` restricts layouts to logos and platform artwork. A full build exports every preset at its own default angle.

## Included flag designs

Colour order below is top to bottom at 90°. Hex values are this pack's editable screen palette, not a claim of universal textile, Pantone or print specifications.

| Preset | Design and colour order | Context |
| --- | --- | --- |
| `pride-trans-5` | Light blue, pink, white, pink, light blue | Monica Helms' transgender Pride flag, 1999. Five equal colour shares. |
| `pride-8` | Hot pink, red, orange, yellow, green, turquoise, indigo, violet | Gilbert Baker's original eight-stripe rainbow, 1978. |
| `pride-7` | Red, orange, yellow, green, turquoise, indigo, violet | The 1978 seven-stripe version after hot pink was removed because of fabric availability. Corrected from this pack's former cyan/royal-blue spectrum palette. |
| `pride-6` | Red, orange, yellow, green, royal blue, violet | The familiar 1979 version, omitting turquoise and replacing indigo with royal blue. |
| `pride-9` | Lavender followed by the original eight colours | Baker's 2017 nine-stripe version; lavender represents diversity. |
| `pride-bi` | Pink, purple, blue | Bisexual Pride; Michael Page-led design, 1998. Colour shares are 40%, 20%, 40%. |
| `pride-pan` | Pink, yellow, blue | Pansexual Pride; the three-colour design associated with 2010. Equal shares. |
| `pride-nonbinary` | Yellow, white, purple, black | Non-binary Pride; Kye Rowan, 2014. Equal shares. A slate tile background keeps both white and black visible. |
| `pink-union-jack` | Pink crosses, white borders/diagonals, plum field | Custom Union Jack-inspired campaign artwork, clipped inside the bird. Its crosses remain a pattern when rotated. It is not a historical Pride flag or an official UK flag specification. |

The remaining presets are `primary`, `dark`, `purple`, `black`, `white`, `holiday-3`, `new-year-2`, `aurora-4` and `violet-gradient`.

The rainbow colours traditionally signify sex, life, healing, sunlight, nature, magic/art, serenity and spirit. The [Gilbert Baker Foundation](https://gilbertbaker.com/rainbow-flag-color-meanings/) explains those original meanings and publishes its own colour conversions. This pack retains a coordinated screen palette rather than claiming those conversions. The [Smithsonian's original transgender flag record](https://americanhistory.si.edu/collections/object/nmah_1694617), [UCSB Pride flags glossary](https://rcsgd.sa.ucsb.edu/resources/lgbtqia-informational-resources/pride-flags-glossary) and [Library.LGBT rainbow history](https://flag.library.lgbt/flags/rainbow-original-design/) provide design/history references.

## Rotation

| Angle | Stripe/gradient direction | Visible arrangement |
| --- | --- | --- |
| `0` | Left to right | Vertical bands |
| `45` | Upper left to lower right | Diagonal bands |
| `90` | Top to bottom | Horizontal bands, normal flag order |
| `138` | Upper right to lower left | The diagonal from the trans example |
| `180` | Right to left | Reversed vertical bands |
| `270` | Bottom to top | Reversed horizontal bands |
| `360` | Same as 0 | Full turn |

The angle rotates the paint, while the bird and wordmark stay upright. For stripe/gradient fills this is the direction of colour progression; the stripe boundaries run across it. SVG gradients use normalized object-bounding-box coordinates, so the apparent physical angle depends on the mark's proportions. Union Jack angles rotate the pattern itself: 0° is upright. The shared angle also applies to any multi-colour wordmark/background. Solid colours do not change with rotation.

DEFAULT: flag presets use 90°, `soft` treatment and `area` balance; the pink Union Jack uses 0° and its pattern treatment. Other presets keep 0° and their continuous blends. `--angle` overrides the preset. You can change defaults in `source/themes.json`.

## Colour balance and treatments

`--balance area` distributes the flag's colour shares across the **filled bird**, ignoring empty bounding-box corners and the tail openings. Boundaries are recalculated for each angle. This avoids blue end colours consuming most of a diagonal trans bird. Sampling is approximate; the short soft transitions blend neighbouring colours. Each trans stripe targets 20% of filled area, giving blue and pink 40% each and white 20% overall. Bisexual retains its 2:1:2 shares; the other stripe flags use equal shares.

`--balance linear` uses literal geometric stripe widths across the paint axis. On the irregular bird, some colours can occupy much less visible area. Choose this when geometric flag construction matters more than balanced logo coverage. These choices affect stripe treatments only; they do not change the Union Jack pattern or ordinary continuous blends.

| Treatment | Result |
| --- | --- |
| `--style soft` | Named colour bands with short, smooth transitions; default for stripe flags. |
| `--style bands` | Crisp flag bands with hard boundaries. Pair with `--balance linear` for literal stripe-width ratios. |
| `--style blend` | Continuous gradient through evenly spaced colour stops; useful for abstract campaign blends. |
| `--style union-jack` | Custom pink Union Jack pattern. Requires exactly three bird colours in pink, white, field order. |

Examples:

```bash
./build.sh --theme pride-trans-5 --angle 138 --style soft --balance area
./build.sh --theme pride-8 --angle 90 --style bands --balance linear
./build.sh --theme pride-pan --angle 45 --style blend
```

The complete bird has one paint field, including its separate tail contour. Gradients and patterns do not restart at wing/body joins. The rounded inner back-to-tail junction is geometry, not a covering stroke.

## Custom palettes

`bird`, `word` and `background` each accept 1–9 six-digit hex colours. One colour is solid; multiple colours support gradients. `--style` applies to the bird; use `--word-style` or `--background-style` for other layers. Quote hex values so the shell does not interpret `#` as a comment.

```bash
./build.sh --theme custom \
  --bird '#FF218C,#FFD800,#21B1FF' --style soft --balance area \
  --word '#FFFFFF' --background '#23162F' --angle 138 \
  --output campaigns/custom-pan
```

A command-line bird palette override resets the preset's stripe weights to equal shares. For reusable unequal shares, add a named theme to `source/themes.json`, with positive `weights` matching the number of bird colours:

```json
"custom-bi": {
  "label": "Custom bisexual campaign",
  "bird": ["#D60270", "#9B4F96", "#0038A8"],
  "word": ["#FFFFFF"],
  "background": ["#23162F"],
  "weights": [2, 1, 2],
  "style": "soft",
  "balance": "area",
  "angle": 138
}
```

Standalone birds/wordmarks/lockups are transparent. Tiles and marketing canvases use the background field. Review the selected background at small sizes, especially black or white bands. Raster exports use screen RGB values; ask your print supplier to manage print profiles from the SVG master.

## Solid bird on a flag background, swaps and saved custom sets

The bird, wordmark and background have independent colours and paint treatments. `bird` means the foreground bird; `word` means the logo lettering. Use any valid hex, including Carbon `#0D0F12`, Surface `#15181D` or an off-white such as `#F4F1EA`.

A named one-off build can start from an existing flag, swap its complete paint onto the background, then set a solid bird and wordmark:

```bash
./build.sh --theme pride-trans-5 --name trans-carbon \
  --swap bird-background \
  --bird '#0D0F12' --word '#0D0F12' --background-angle 138

./build.sh --theme pink-union-jack --name pink-jack-offwhite \
  --swap bird-background \
  --bird '#F4F1EA' --word '#F4F1EA' --background-angle 0

# Completely manual foreground/background colours:
./build.sh --theme custom --name carbon-coral \
  --bird '#0D0F12' --word '#0D0F12' --background '#FF8EAB'
```

`--swap` accepts `bird-background`, `bird-word` and `word-background`. It moves the colours, style, weights, balance and angle together, before applying command-line overrides. A moved background uses geometric stripe widths by default, because a rectangular canvas has a different visible shape from the bird. The studio's **Swap bird and background** button performs the same exchange. Set solid foreground colours after swapping.

For reuse, copy `source/custom-examples.json` to your own JSON config. A config maps output names to settings. It can contain one or many variants:

```json
{
  "trans-carbon": {
    "base": "pride-trans-5",
    "swap": "bird-background",
    "bird": ["#0D0F12"],
    "word": ["#0D0F12"],
    "background_angle": 138
  },
  "rainbow-surface": {
    "base": "pride-8",
    "swap": "bird-background",
    "bird": ["#15181D"],
    "word": ["#15181D"],
    "background_style": "bands",
    "background_angle": 90
  }
}
```

Build a whole saved set with:

```bash
./build.sh --config source/custom-examples.json
# Or your own config, in any location:
./build.sh --config /path/to/my-campaigns.json
```

The included example config builds four variants: trans/Carbon, eight-stripe rainbow/Surface, pink Union Jack/off-white, and solid coral/Carbon. These are custom examples, separate from the 18 built-ins.

Every layer supports `<layer>_style`, `<layer>_angle`, `<layer>_weights` and `<layer>_balance` in JSON, where `<layer>` is `bird`, `word` or `background`. Equivalent CLI controls include `--background-style bands`, `--background-angle 138` and `--background-balance linear`. Weights are supplied in JSON. Legacy `style`, `balance` and `weights` refer to the bird. A CLI `--angle` overrides angles for all layers; otherwise per-layer settings override the config's shared `angle`. The studio has optional layer angle overrides alongside its shared slider.

Standalone bird, wordmark and horizontal/stacked lockups stay transparent. A flag background appears in app tiles, avatars, social artwork, banners and backgrounds. Use `app-rounded-1024.png` or `social-square.svg` to inspect the combined result. Foreground-only adaptive artwork and the separate adaptive background remain available for platform composition.

## Output ownership and overview regeneration

- `./build.sh` builds only the 18 built-ins into `assets/`. It does not load custom configs.
- `--config`, `--name` and `--theme custom` default to `custom-assets/`. Each variant gets its own named subfolder. `--output` overrides the output root.
- A full rebuild refreshes files recorded in that variant's `build.json` before exporting. Other named variants, config sources and unrelated files are preserved. SVG-only runs overwrite matching SVGs without clearing existing rasters; regenerate the rasters before relying on them after a geometry/paint edit.
- Keep reusable JSON configs outside output folders. `build.json` records the resolved paints and the build's shared angle override for inspection. Configs are the editable source; output folders are generated.
- Building artwork does **not** automatically regenerate `OVERVIEW.png`. This lets you finish several custom builds before collecting them.

Run the separate script whenever you want a fresh overview:

```bash
# Discover all rendered built-ins plus custom variants:
./overview.sh

# Built-ins only:
./overview.sh --input assets

# Custom set only, as a separate sheet:
./overview.sh --input custom-assets --output CUSTOM-OVERVIEW.png

# Combine any arbitrary output folders (repeat --input):
./overview.sh --input assets --input custom-assets \
  --input /path/to/other-exports --output OVERVIEW.png
```

The script searches recursively for `app-rounded-1024.png`; it does not require entries in `source/themes.json`. A folder label identifies each card. SVG-only exports have no raster thumbnail and are omitted until rendered. Any complete manually generated pack using the usual filenames can be included with `--input`.

Custom output rasters are ignored by Git, just like built-in exports. Keep your JSON configs if you want to recreate a custom set after clearing generated artwork.

## Reddit banners

Full/general exports include `reddit-banner.svg` / `reddit-banner-1072.png` (1072 × 128) and `reddit-mobile-banner.svg` / `reddit-mobile-banner-1080.png` (1080 × 128) for every built-in or configured custom variant. They reuse the centred X-header bird/wordmark composition, scaled down to fit the shallow canvas with 12 px vertical clearance. Colours, flag backgrounds and layer rotation settings carry through automatically. Core-only builds omit social banners; SVG-only builds omit PNGs. Both layouts are selectable in PREVIEW.html.
