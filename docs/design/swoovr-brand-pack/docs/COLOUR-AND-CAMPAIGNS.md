# Colours and campaigns

Each theme has three independent fields: `bird`, `word`, and `background`. Each accepts **1 to 7 six-digit hex colours**. One colour gives a solid fill; 2–7 colours give an evenly spaced linear gradient. The same setting applies to every layout in that build. All generated SVGs retain editable gradient stops.

Always quote colours in shell commands: `#` otherwise starts a shell comment in many situations. Use comma-separated values with no spaces. Short hex, CSS names, alpha hex and arbitrary CSS are deliberately rejected.

## Solid bird, wordmark and background

```bash
./build.sh --theme custom \
  --bird '#7228E8' --word '#23162F' --background '#FFFFFF' \
  --output campaigns/standard
```

The standalone bird, wordmark and logo lockups are transparent by design. Background settings affect app tiles, avatars and marketing canvases. For a coloured logo panel, use a social or header layout or place a transparent lockup on your desired background in your design software.

## Pride bird, white lettering, dark background

```bash
./build.sh --theme custom \
  --bird '#E40303,#FF8C00,#FFED00,#008026,#24408E,#732982' \
  --word '#FFFFFF' --background '#23162F' --angle 0 \
  --output campaigns/pride
```

## Gradient lettering as well

```bash
./build.sh --theme custom \
  --bird '#E40303,#FF8C00,#FFED00,#008026,#24408E,#732982' \
  --word '#E40303,#FF8C00,#FFED00,#008026,#24408E,#732982' \
  --background '#17121F' --angle 0 --output campaigns/pride-all
```

## Gradient background with white bird and words

```bash
./build.sh --theme custom --bird '#FFFFFF' --word '#FFFFFF' \
  --background '#5420C5,#A358F5,#EF5DA8' --angle 45 \
  --output campaigns/launch
```

## 2 / 3 / 4 / 5 / 6 / 7 colour examples

| Stops | Example palette |
| --- | --- |
| 2 | `#5420C5,#A358F5` |
| 3 | `#C41E3A,#FFF5E1,#168B57` |
| 4 | `#11D5C1,#35A7FF,#8A4FFF,#EF5DA8` |
| 5 | `#5BCEFA,#F5A9B8,#FFFFFF,#F5A9B8,#5BCEFA` |
| 6 | `#E40303,#FF8C00,#FFED00,#008026,#24408E,#732982` |
| 7 | `#E40303,#FF8C00,#FFED00,#008026,#00A8E8,#24408E,#732982` |

`--angle 0` is left to right, `90` is top to bottom, `180` reverses horizontal direction, and `45` is diagonal. A shared angle controls all three paints. SVG gradients use each complete bird or complete wordmark's bounding box, so they flow continuously across all bird pieces or all letters. The bird and wordmark each start their own gradient.

These are blended campaign treatments, not literal flag reproductions. `pride-6` uses six rainbow colours; `pride-7` is a seven-colour spectrum variation. Neither is a Progress Pride flag. `pride-trans-5` uses trans flag colour order, blended through the artwork.

## Christmas and New Year

```bash
./build.sh --theme holiday-3 --angle 90 --output campaigns/christmas
./build.sh --theme new-year-2 --angle 45 --output campaigns/new-year
```

Add your own named theme to `source/themes.json` to keep it reusable:

```json
"summer": {
  "bird": ["#FF705B", "#FFC857"],
  "word": ["#FFFFFF"],
  "background": ["#123B47"]
}
```

Then run `./build.sh --theme summer`. Preserve valid JSON syntax, including commas between entries.

## SVG first, PNG later

```bash
./build.sh --theme pride-6 --svg-only --output campaigns/vector-draft
inkscape campaigns/vector-draft/pride-6/bird.svg \
  --export-filename=bird-pride-8192.png --export-width=8192
```

Vector masters scale independently of raster resolution. The normal logo PNGs top out at 4096 px wide; export larger versions only when needed. Raster files use RGB/sRGB-style colour values, not press-specific CMYK profiles. Give a print supplier the SVG and ask them to handle their own output profile.

For a manual SVG change, edit `stop-color` and the gradient endpoints or open the SVG in Inkscape/Figma/Illustrator. Rebuilding from JSON is the safest way to keep all exports consistent.
