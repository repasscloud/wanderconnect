# Intake evidence publication review — 2 November 2026

All 16 supplied images were visually inspected locally. Baseline commit `c559324` already tracks all originals; the six withheld images are removed from the current tracked tree in this processing commit, while local files are retained. Earlier Git history is not rewritten, so those prior copies still exist. This task does not claim historical erasure. Originals were not altered. Published images contain public UI or explicitly fictional `example.test` / Local Admin fixture details, with no visible credentials, tokens, real personal information or private messages identified. This is a visual review, not proof of runtime safety. Authentication-flow URLs are treated as sensitive even when they may have expired.

| File | Publication | Reason |
| --- | --- | --- |
| `02-cannot-search-for-account-by-email-address.png` | [Published original](02-cannot-search-for-account-by-email-address.png) | Visual review cleared; UI/fictional fixture only |
| `03-session-ended-01.png` | [Published original](03-session-ended-01.png) | Visual review cleared; UI/fictional fixture only |
| `03-session-ended-02.png` | [Published original](03-session-ended-02.png) | Visual review cleared; UI/fictional fixture only |
| `04-photos-asking-for-reason-to-view-to-verify-01.png` | [Published original](04-photos-asking-for-reason-to-view-to-verify-01.png) | Visual review cleared; UI/fictional fixture only |
| `04-photos-asking-for-reason-to-view-to-verify-02.png` | [Published original](04-photos-asking-for-reason-to-view-to-verify-02.png) | Visual review cleared; UI/fictional fixture only |
| `05-beta-enrollments-screen-is-messy.png` | [Published original](05-beta-enrollments-screen-is-messy.png) | Visual review cleared; UI/fictional fixture only |
| `07-loading-screen-shoult-not-have-anything-except-login-register-options.png` | [Published original](07-loading-screen-shoult-not-have-anything-except-login-register-options.png) | Visual review cleared; UI/fictional fixture only |
| `08-account-route-when-logged-out.png` | [Published original](08-account-route-when-logged-out.png) | Visual review cleared; UI/fictional fixture only |
| `11-fictional-alex-account-cannot-register.png` | Removed from current tracked tree; original retained locally | authentication transaction identifiers in browser URL |
| `12-forgot-password-screen-is-ugly.png` | Removed from current tracked tree; original retained locally | authentication transaction identifiers in browser URL |
| `13-forgot-password-screen-notification-incorrect.png` | Removed from current tracked tree; original retained locally | authentication transaction identifiers and populated masked credential field |
| `15-profile-screen.jpeg` | [Published original](15-profile-screen.jpeg) | Visual review cleared; UI/fictional fixture only |
| `15-redesign-profile-and-modes-page-for-responsive-layout.jpeg` | Removed from current tracked tree; original retained locally | populated personal profile/intention data; fictional provenance unverified |
| `16.png` | Removed from current tracked tree; original retained locally | identifiable person photograph; provenance/permission unverified |
| `17.png` | Removed from current tracked tree; original retained locally | sensitive identity/orientation/preferences; fictional provenance unverified |
| `Screenshot 2026-11-01 at 23.13.40.png` | [Published original](https://github.com/repasscloud/wanderconnect/blob/main/docs/delivery/evidence/2026-11-01-issue-intake/Screenshot%202026-11-01%20at%2023.13.40.png) | Visual review cleared; UI/fictional fixture only |

Items 14, 16 and 17 had no Markdown screenshot attachment in the report: the account capture, `16.png` and `17.png` were matched by visible page content. Item 15 has a safe blank-profile capture; its populated alternative is withheld. Item 10 uses the existing design reference and the locally reviewed item 11 capture. Item 9 references the locally reviewed item 13 capture. No new screenshot is fabricated. Request fresh fictional-fixture captures for withheld evidence before any later publication; never upload the sensitive originals to an issue.

## Validation and publication boundary

- `./scripts/check.sh` rerun after correcting pre-existing intake design-reference links and a percent-encoded local link unsupported by the repository link checker. Naming, 33 backup tests, design/Markdown/disabled-workflow checks, Keycloak realm checks, Node checks, restore and Release build passed (0 warnings, 0 errors); executable checks passed through non-persistence checks, then exited 134 at PostgreSQL setup with `28P01: password authentication failed for user "swoovr"`. Full suite is **not passing**; database persistence acceptance is unverified. No credentials changed, database reset or application fix attempted.
- The later commands skipped by fail-fast `check.sh` were run independently: `python3 scripts/smoke.py`, `python3 scripts/prototype_smoke.py`, `python3 scripts/connection_smoke.py` all exited 0.
- `git diff --check` and staged diff whitespace checks passed. Issue image/source/document destinations were checked against repository paths; the GitHub issue/commit publication is verified after pushing.
- Investigation is source/screenshot review, not runtime reproduction of the 15 reports. No native/device, actual session renewal, real email delivery or comparative enumeration acceptance is claimed.
- Owner explicitly authorised committing/pushing these tracking records and safe evidence directly to `main`, overriding the usual feature-branch/PR delivery rule for this task. No workflows were enabled; no fixes or deployment.
