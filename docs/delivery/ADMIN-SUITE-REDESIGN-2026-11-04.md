# Admin suite redesign — 3–4 November 2026

CONFIRMED (owner, 3–4 November 2026, in session): approved click-through mockups for Beta enrolments, Media review, Date of birth reviews and the remaining admin routes; asked for them to be applied to the admin and API and brought to `main`. Owner direction: no instructional helper text in the admin portal (SOPs are written separately), no "beta" wording in reason labels, bird logo with lower-case `swoovr` wordmark, the Media rename (video later), and the keyboard layer below.

## What changed

- **Shell:** bird mark and `swoovr` wordmark; header has **Jump to…** (⌘K / Ctrl+K palette over every sidebar page plus "Search accounts for …"), a **Theme** light/dark toggle (per-browser, Standard scheme only) and an initials avatar. Global keys: `/` page search, `?` shortcut list, `g` then a letter to go to a page, ⌘/Ctrl+Enter submits the current form, Esc closes. All progressive enhancement; pages work without JavaScript. Theme is applied by an external `theme-init.js` (CSP `script-src 'self'`).
- **Shared presentation:** one set of `ui-*` styles (page head, tabs with counts, stat tiles, filter cards with uniform 40px controls, tables, status pills, segmented filters, copy buttons, empty states, timelines, grouped forms). Colours remain token-only and shadow-free (`check_design.py`).
- **Pages:** Queue, Date of birth, Accounts (+ account detail), Support lookup, Contact history, Identifier review, Legal holds, Law enforcement, Disclosure register (search/agency filter, CSV export of the visible table), Elevated access (countdown, capability labels, latest 25 log rows with "Show all"), Security, Safety resources (+ country editor), Beta scorecard, Annual report data, Beta enrolments and Media review (Queue / Appeals / History tabs, review dialog with V, A/R/E, ↑/↓, Enter, ←/→ keys; first reason auto-selected).
- **Account detail (#156):** readable labels instead of wire values (raw values kept in `data-*` attributes), header with status pills, sticky section bar, grouped cards. No authority, preview or evidence scope was widened.

## API and data

- **D307 account search** (`GET /api/admin/accounts/search?q=`): mobile (optional `+`, spaces ignored, international digits without `+`), lower-cased email, or registered first/last name with `%` as the only wildcard (`_` and `\` literal, ILIKE with `\` escape). Empty and `%`-only searches are refused. DEFAULT: existing Support lookup permission; audited as `kind:<k> results:<n>` without the value; first 50 results. Parser in `Swoovr.Domain/Accounts/AccountSearch.cs`, 18 checks in `AccountSearchChecks`.
- **Beta enrolments:** confirmed registered first/last names (D300) in search, sort and rows.
- **Date of birth reviews:** new nullable `dob_reviews.resolution_note` (migration `DobReviewResolutionNotes`, additive) storing the resolving reason; the list returns registered names, Didit session id, resolution note and resolver name; `open=false` now returns the newest 200. No date of birth is returned or shown (D166/D299). The Didit console link is optional configuration `Didit:SessionUrl` (template with `{0}`); without it the session id is shown for copying. No Didit URL format is assumed.
- **Media:** reason label for `REJECT_EXPLICIT_BETA` is now "Explicit content"; the stored code is unchanged (renaming needs a data migration — OPEN, not requested).

## Evidence (4 November 2026, local managed stack)

- `./scripts/check.sh` with the managed PostgreSQL connection: PASS, Release build 0 warnings/0 errors, 704 PostgreSQL persistence cases, account search 18 checks, all HTTP smoke stages, design/naming checks.
- `tests/admin/run-isolated.cjs` (real Keycloak, disposable DB, Edge): default shell 77 checks, `beta`, `media`, `accounts`, `presentation` — all PASS.
- `tests/auth/run-owned.cjs` suites `accounts` (168), `review-workflows`, `didit` (144, synthetic Didit stand-in) — PASS.
- Signed-in screenshots of every redesigned route at 1440px and 390px: no console errors, no page-level horizontal overflow.

Test changes: shell suite expects 19 sidebar links (Registration settings existed before this work) and resizes to desktop before opening the desktop-only `/access`; reflow failures now name the overflowing elements. Beta invitation wording expectation follows "Invitation email queued, not yet sent". Media suite heading is "Media review".

## Not done / OPEN

- Q123/#173 guest `/safety` split and 72-hour trusted-contact links, Q121/#170 Android package split and Q99/#141 landing page are recorded decisions awaiting implementation.
- #156 role-by-role acceptance (Support/Admin/Legal/Root) and #161 hosted acceptance were not re-run beyond the suites above.
- Live Didit, real screening, hosted deployment and physical-device checks remain outside this local evidence.
