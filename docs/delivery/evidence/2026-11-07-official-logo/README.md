# Top-left brand (issue 202, D340), 7 November 2026

`before/` is `main` at `86b8a07`. `after/` is branch `feature/official-svg-logo` after the owner's Nunito decision: the text "swoovr" in Nunito Bold inside the apps, and the official logo on the Keycloak sign-in pages.

Both were rendered by headless Chrome from a static harness served over local HTTP. The harness uses each host's real stylesheets (`themes.css`, `app.css`, `admin.css`, the Keycloak `swoovr.css`), the generated font and logo copies, and the header markup copied straight out of `MainLayout.razor`, `AdminNavigation.razor`, `Admin/Program.cs` (signed-out page) and `template.ftl`. Page bodies are placeholders. No account, session or member data is involved.

| File | Surface | Width | Theme |
| --- | --- | --- | --- |
| `member-standard-{light,dark}-1440.png` | Member web header | 1440px | Standard |
| `member-pride-{light,dark}-1440.png` | Member web header | 1440px | Pride (personal scheme) |
| `member-768.png` | Member web header | 768px | Standard light |
| `phone-member-standard-{light,dark}.png` | Member web header | 320px and 375px | Standard |
| `admin-{light,dark}.png` | Admin sidebar | 900px | Standard |
| `admin-signed-out.png` | Admin signed-out page | 900px | Standard light |
| `keycloak-1440.png`, `phone-keycloak.png` | Keycloak sign-in header (logo) | desktop; 320px and 375px | Standard light (the theme has no dark mode) |
| `nunito-loaded.png` (after only) | Wordmark at 120px above the same word in Helvetica | n/a | the page reports `Swoovr Nunito 700 loaded` |

Result: the in-app headers show "swoovr" in Nunito Bold in the theme text colour, and the browser confirms the self-hosted face loaded, not a fallback. The sign-in pages show the official logo. Nothing scrolls sideways at 320px.

Not covered: no check inside the running Blazor apps or a live Keycloak 26.8.1 container, and no screen-reader pass. The mobile app (MAUI) is out of scope.

An earlier harness run of the logo version caught a real defect: the generated SVG's comment contained a double hyphen, so browsers rejected the whole file. `check_design.py` now parses the generated logo as XML.
