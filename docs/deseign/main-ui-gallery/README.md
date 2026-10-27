# Main UI gallery

Open `index.html` directly, or serve this folder with `python3 -m http.server 8768 --bind 127.0.0.1`.

33 source-based HTML reconstructions, plus the index. See `manifest.json` for the exact base commit, source component per screen and limitations. Rebuild with `python3 docs/deseign/main-ui-gallery/build.py` from the repository root.

CONFIRMED: Standard + System default, eight personal schemes, existing member/staff separation and the documented monochrome design. Original `docs/design/` stays untouched; `docs/deseign/` is the explicitly requested output path.

DEFAULT: illustrative populated states with fictional adults, sample dates, descriptive form values and static controls. No requests, account persistence, messages, verification or moderation actions execute. Appearance controls change this preview only. Selected controls are simplified (for example choice fields rendered as text fields); these are review aids, not pixel-exact captures. Hosted Keycloak screens approximate the form layout using member CSS. Account-tools is a representative excerpt.

OPEN: authenticated runtime visual parity, native MAUI device appearance and unimplemented product screens. Commerce, venues, vouchers, photo upload and proposed future screens are not presented as implemented. Some main copy is stale; the welcome notice is preserved as source evidence, not a current capability claim.

Validation: repository `./scripts/check.sh` passed; database persistence checks skipped because `SWOOVR_TEST_DATABASE` was unset. HTML link/source/CSS integrity checked locally. Explore reviewed in the in-app browser; this is not a full responsive or accessibility audit of every screen.
