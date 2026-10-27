# Main UI gallery

Open `index.html` directly, or run `./start.sh` from this folder. It serves on http://127.0.0.1:8768/index.html until Ctrl+C. Pass a different port with `./start.sh 8769`.

33 source-based HTML reconstructions, plus the index. See `manifest.json` for the exact base commit, source component per screen and limitations. Rebuild with `python3 docs/design/main-ui-gallery/build.py` from the repository root.

CONFIRMED: Standard + System default, eight personal schemes, existing member/staff separation and the documented monochrome design. Gallery relocated here at the owner’s explicit request; canonical design assets remain unchanged.

DEFAULT: illustrative populated states with fictional adults, sample dates, descriptive form values and static controls. No requests, account persistence, messages, verification or moderation actions execute. Appearance controls change this preview only. Selected controls are simplified (for example choice fields rendered as text fields); these are review aids, not pixel-exact captures. Hosted Keycloak screens approximate the form layout using member CSS. Account-tools is a representative excerpt.

OPEN: authenticated runtime visual parity, native MAUI device appearance and unimplemented product screens. Commerce, venues, vouchers, photo upload and proposed future screens are not presented as implemented. Some main copy is stale; the welcome notice is preserved as source evidence, not a current capability claim.

Validation: repository `./scripts/check.sh` passed; database persistence checks skipped because `SWOOVR_TEST_DATABASE` was unset. HTML link/source/CSS integrity checked locally. Explore reviewed in the in-app browser; this is not a full responsive or accessibility audit of every screen.
