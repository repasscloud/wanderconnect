# Validation of this release

- All delivered PNGs were fully decoded.
- ICO and ICNS containers and all of their representations were decoded.
- All SVGs parsed as XML, contained no embedded raster images and had no duplicate IDs.
- The TTF loaded successfully and maps U+E000 to the whole wordmark.
- Python files compiled and the Bash wrapper passed syntax validation.
- Custom CLI palette builds were exercised, including 7 bird stops, 3 wordmark stops and 2 background stops at 45 degrees, plus a complete solid-colour core build.
- Studio JavaScript syntax, preset SVG generation and invalid-colour handling passed checks. Automated browser testing of the export buttons was unavailable in this environment.

Use `python scripts/verify_assets.py` to repeat the file integrity checks. A full rebuild also requires Inkscape. The package includes already-built exports so no build is necessary to use the artwork.

## Wing/body seam correction — 16 October 2026

- The upper wing and body were joined using a cubic Bézier bridge and Skia PathOps boolean union, then saved as the master path in `source/geometry.json`. The separate tail remains a second contour; there are no overlapping wing/body subpaths under `evenodd` and no stroke covering the seam.
- Boolean difference against the preceding geometry found no removed filled region. Added fill is confined to x=233–298.901, y=168.836–233; overall bounds remain 0,0–384,324. The larger tail openings are preserved.
- All 12 presets were rebuilt with the existing local campaign colours, explicit stop positions and angles. The single bird path retains one gradient across its complete bounding box. PREVIEW.html and OVERVIEW.png were regenerated.
- Visual inspection covered native 16, 24, 32, 48 and 64 px application icons, enlarged pixel views, and the seam in the 4096 px bird export.
- `./scripts/check.sh` passed: design/link/workflow checks, 31 browser-harness tests, .NET build, 50 account policy cases, 71 discovery policy cases, client contract checks and local HTTP smoke checks.
- Post-build integrity checks passed with no errors, including 216 SVGs, 12 ICO and 12 ICNS containers. All 961 asset checksums matched; every generated SVG matched the master paths and the existing builder gradient stops/endpoints. Regenerated studio JavaScript passed `node --check`.

## Flag treatments and rounded inner back — 16 October 2026

- Replaced the sharp inner wing-to-tail corner with a cubic fillet. Regression checks confirm tangent continuity at both joins. The bird remains two disjoint contours; `evenodd` and nonzero fills produce equivalent silhouettes. No stroke covers the wing/body seam.
- Added/corrected transgender, 1978 eight- and seven-stripe, 1979 six-stripe, 2017 nine-stripe, bisexual, pansexual and non-binary palettes, plus the owner-selected custom pink Union Jack pattern. Total: 18 presets.
- Stripe presets now default to soft bands at 90 degrees, balanced by sampled visible bird area. Trans targets 20% per stripe, approximately 40% blue / 40% pink / 20% white overall, including at 138 degrees. Bisexual uses 2:1:2 weights. Crisp bands, continuous blends and geometric-width balance remain selectable. Up to nine colours are supported.
- CLI/studio paint parity was checked across seven angles and three stripe treatments. All six Python regression tests and 162 generated-studio preset/angle/layout checks passed; invalid colours were rejected.
- Visual inspection covered all nine flag treatments at 0, 90 and 138 degrees; the inner back at 4096 px; native/enlarged 16–64 px icons; and the regenerated 18-palette overview.
- All 324 SVGs exactly matched freshly generated current-master artwork, including maskable icons. All 1,062 PNGs, 18 ICO containers, 18 ICNS containers and 18 manifests passed integrity checks. All 1,441 entries in the asset checksum manifest matched. An interrupted-build temporary render was removed before final validation.
- `./scripts/check.sh` passed design/link/workflow checks, 31 harness tests, the .NET Release build, policy/client checks and local HTTP smoke checks.
- PREVIEW.html and OVERVIEW.png were regenerated. The automated browser rejected opening the local HTML URL, so actual browser download-button/export interaction remains untested. Studio code was executed against a DOM test stub, and SVG/PNG exports were inspected through direct generation/rendering.

Repeat the focused checks with:

```bash
python -m unittest discover -s tests -v
node tests/studio-check.cjs
python scripts/verify_assets.py assets
```

## Custom layer paints, reusable configs and discovered overviews — 16 October 2026

- Added reusable named JSON configs, complete paint swaps, independent bird/wordmark/background styles, angles and balances, and one-off `--name` builds. The offline studio exposes foreground/background swaps and per-layer controls.
- Built four complete custom packs from `source/custom-examples.json`: Carbon on trans, Surface on eight-stripe rainbow, off-white on pink Union Jack, and Carbon on solid coral. Visually inspected the resulting tiles together.
- Custom outputs default to `custom-assets/`; default builds select only built-ins in `assets/`. Tests verify that unrelated named folders/files survive, config key order does not change explicit weights, swaps retain flag weights/styles, and built-in bird SVGs remain unchanged. Generated `build.json` records resolved paints and owned output files for subsequent full refreshes.
- `overview.sh` discovers rendered thumbnails recursively. The regenerated OVERVIEW.png includes 22 designs: 18 built-ins plus four custom variants. Arbitrary roots and custom-only sheets were exercised with `--input`/`--output`.
- All 14 Python regression tests and 162 studio combinations passed, including studio swap/solid-bird/background-flag checks. Custom exports passed integrity checks: 72 SVGs, 236 PNGs, four ICO and four ICNS containers and four manifests. Repository checks passed. The earlier live browser interaction gap remains.

## Reddit desktop/mobile exports — 16 October 2026

- Added desktop 1072 × 128 and mobile 1080 × 128 layouts to general exports, including reusable custom configs. Both use the existing X-header composition scaled to fit with 12 px vertical clearance. SVG-only and core-only behaviour is retained.
- Generated 44 Reddit PNGs and 44 SVGs across all 18 built-ins and four custom examples without rerendering unchanged assets. Every PNG had the requested dimensions. Visually inspected native-size primary, trans, Carbon-on-trans and off-white-on-pink-Union-Jack banners.
- Added fit/clearance and general-export regression checks. All 15 Python tests and 270 studio preset/angle/layout combinations passed; repository checks passed. PREVIEW.html and output checksum manifests were regenerated. Asset integrity checks passed for built-in and custom exports.
