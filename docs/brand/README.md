# Swoovr logo

`swoovr-logo.svg` is the official horizontal lockup (bird + lowercase `swoovr` wordmark), copied byte for byte. Treat it as read-only: change it in the source repository and copy it across again.

| Field | Value |
| --- | --- |
| Source repository | `repasscloud/swoovrcom-web` |
| Source path | `public/brand/logo.svg` |
| Source commit | `2bcb54ad9e3af4987271ebd3e6cd8de8c19f13e4` (23 October 2026) |
| Copied | 7 November 2026, issue [#202](https://github.com/repasscloud/wanderconnect/issues/202) |
| SHA-256 | `2568a5cd8f942692dc950dd942bc37d55f065f146dbe9d26a4ced4ed1b7f30c4` |
| viewBox | `0 0 1160 360` (two outlined paths: bird and wordmark) |

## How the apps use it (D340)

`python3 scripts/check_design.py --write` generates one copy per web host from this file, changing only the hard-coded `fill="#0A0A0A"` to `fill="currentColor"` and grouping the paths as `#swoovr-logo` so pages can reference it with `<svg viewBox="0 0 1160 360"><use href="…/logo.svg#swoovr-logo"/></svg>`. The logo then takes the page's text colour, so it reads in light, dark and every personal scheme. Staff pages stay on Standard. `--check` (run by `./scripts/check.sh`) fails if a copy drifts or a surface types the brand as text again.

| Surface | Copy |
| --- | --- |
| Member web | `src/Swoovr.Web/wwwroot/brand/logo.svg` |
| Admin | `src/Swoovr.Admin/wwwroot/brand/logo.svg` |
| Keycloak sign-in and account pages | `containers/keycloak/themes/swoovr/login/resources/img/logo.svg` |

Brand rules (owner, 4 November 2026): keep the exact shapes, spacing and aspect ratio; scale with `height: auto`; never re-type "swoovr" in a system font; don't use `SwoovrWordmark-Regular.ttf` (it holds the whole logo as one glyph). The website shows the logo 174px wide on desktop and 145px wide on phones.

The mobile app (MAUI) still shows a text label. It is out of scope while the web app comes first (owner, 7 November 2026) and is tracked as a follow-up of #202.
