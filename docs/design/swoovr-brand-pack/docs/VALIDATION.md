# Validation of this release

- All delivered PNGs were fully decoded.
- ICO and ICNS containers and all of their representations were decoded.
- All SVGs parsed as XML, contained no embedded raster images and had no duplicate IDs.
- The TTF loaded successfully and maps U+E000 to the whole wordmark.
- Python files compiled and the Bash wrapper passed syntax validation.
- Custom CLI palette builds were exercised, including 7 bird stops, 3 wordmark stops and 2 background stops at 45 degrees, plus a complete solid-colour core build.
- Studio JavaScript syntax, preset SVG generation and invalid-colour handling passed checks. Automated browser testing of the export buttons was unavailable in this environment.

Use `python scripts/verify_assets.py` to repeat the file integrity checks. A full rebuild also requires Inkscape. The package includes already-built exports so no build is necessary to use the artwork.
