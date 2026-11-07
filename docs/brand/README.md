# Swoovr brand mark

Owner decision, 7 November 2026 (issue [#202](https://github.com/repasscloud/wanderconnect/issues/202), D340): **inside the apps** (member web and Admin), the top-left brand is the word `swoovr` typed in **Nunito Bold**, not the logo. The **Keycloak sign-in and account pages** keep the official logo. Nunito was chosen as the closest open font to the logo's lettering; it is not the logo's original typeface, which was never identified.

## Nunito wordmark (`fonts/`)

| Field | Value |
| --- | --- |
| Font | Nunito, weight 700 (Bold), by The Nunito Project Authors |
| Licence | SIL Open Font License 1.1, in `fonts/OFL.txt` (no Reserved Font Name) |
| File | `fonts/nunito-swoovr-700.woff2`: a subset holding only the letters `s w o v r` (1,172 bytes) |
| Source | Google Fonts `css2?family=Nunito:wght@700&text=swoovr` (font version v32), fetched 7 November 2026 |
| SHA-256 | `b38dd88116d321711890280f52543a1f9a3a32ca876ff3203684bd8559db3a68` |

The site's content security policy allows fonts only from the app's own origin, so the font is self-hosted. `python3 scripts/check_design.py --write` copies the font and its licence to `src/Swoovr.Web/wwwroot/fonts/` and `src/Swoovr.Admin/wwwroot/fonts/`. The CSS loads it as `'Swoovr Nunito'`, used only by `.wordmark`: 700 weight, −0.06em letter spacing. Sizes are DEFAULT: member header 1.75rem (1.5rem on phones), Admin sidebar 2rem. Other letters fall back to the Swoovr font stack, so changing the wordmark text needs a new subset (re-run the URL above with the new `text=`).

`--check` (run by `./scripts/check.sh`) fails if a copy drifts, if a header stops using the wordmark, if Nunito is used outside `.wordmark`, or if an in-app page shows the logo again.

## Logo (`swoovr-logo.svg`, sign-in pages only)

`swoovr-logo.svg` is the official horizontal lockup (bird + lowercase `swoovr` wordmark), copied byte for byte. Treat it as read-only: change it in the source repository and copy it across again.

| Field | Value |
| --- | --- |
| Source repository | `repasscloud/swoovrcom-web` |
| Source path | `public/brand/logo.svg` |
| Source commit | `2bcb54ad9e3af4987271ebd3e6cd8de8c19f13e4` (23 October 2026) |
| Copied | 7 November 2026 |
| SHA-256 | `2568a5cd8f942692dc950dd942bc37d55f065f146dbe9d26a4ced4ed1b7f30c4` |
| viewBox | `0 0 1160 360` (two outlined paths: bird and wordmark) |

`check_design.py --write` generates `containers/keycloak/themes/swoovr/login/resources/img/logo.svg`. Its only changes are `fill="currentColor"` and the paths grouped as `#swoovr-logo`. The sign-in header shows it with `<svg viewBox="0 0 1160 360"><use href="…/logo.svg#swoovr-logo"/></svg>`, 145px wide on phones and 174px on desktop.

Logo rules (owner, 4 November 2026): keep the exact shapes, spacing and aspect ratio; scale with `height: auto`; don't use `SwoovrWordmark-Regular.ttf` (it holds the whole logo as one glyph).

The mobile app (MAUI) still shows its own text label. It is out of scope while the web app comes first (owner, 7 November 2026).
