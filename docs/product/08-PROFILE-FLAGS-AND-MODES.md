# Profile flags, badges and modes (planned)

Status: **PLANNED**, owner direction 23 October 2026. Nothing here is implemented beyond the badge visuals listed under [Current state](#current-state). The profile itself is still being defined; this document records what is decided so far and what is open, so profile work starts from it. Update it on `main` as the profile and modes are settled.

## Who versus what

CONFIRMED, owner 23 October 2026 (D55):

- **Keycloak answers "who".** It authenticates people, keeps authentication data to a minimum and manages identity: credentials, linked Apple/Google identities, sessions, MFA, passkeys and email verification. It is also the first place we catch the 18+ rule: registration requires the "I'm 18 or over" confirmation (D40, D43).
- **The Swoovr database and API answer "what".** What someone can do, how their profile appears and which flags they carry are application records, keyed by the account's `(issuer, subject)` (see [Keycloak integration design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md)).
- **Profile flags live on the profile in the application**, never in Keycloak: not as realm roles, groups, user attributes or token claims.
- **The API decides which badges a viewer sees.** Opening someone's profile returns the badges to show; web and mobile only draw what the API returns and never work a badge out from a token, email domain, linked identity or client input.

## Flags

| Flag | Meaning (working) | Set by | Badge (UI-STYLE.md, brand-colours.json) | Status |
| --- | --- | --- | --- | --- |
| Founding member | One of the first 1,000 founding members, activated after onboarding and Photo and/or ID verification; permanent even without a paid subscription (Q08, D63) | The application, from the founding programme | Gold bird `#D4AF37` ("Swoovr Founder") | CONFIRMED (D63) |
| Photo verified | Profile photos matched to a verification photo; does not prove age | The application, after a photo check; process OPEN | Blue `#087CFA` portrait with tick | Badge drawn; check OPEN |
| ID verified | Government ID and age checked | The application, from the identity provider's outcome (Stripe Identity, D40) | Teal `#00AAA0` card with tick | Badge drawn; integration OPEN (Q04) |
| Admin verified | Backend only. A Swoovr admin confirms something in person, for example an employee account or someone met in person whose age was checked (CONFIRMED, D58) | A Swoovr admin, recorded and audited in the application | **No badge.** Never shows or implies ID verified, because no identity provider checked and stored ID | CONFIRMED; how it affects age status OPEN (Q11) |
| Bot account | An automated Swoovr account (for example a guide) | The application; never self-declared | Silver bird `#C0C0C0` ("Swoovr guide (bot)") | Badge drawn |
| Employee account | A RePass Cloud / Swoovr employee | The application, from the staff record (staff realm invitation, D31) | Ruby bird `#E0115F` ("Swoovr staff") | CONFIRMED (D57) |

Rules (DEFAULT unless marked):

- Each badge describes **one** check or fact, never a general endorsement or a safety claim ("Each badge describes one check, not a general endorsement").
- A badge never grants a role or permission (CONFIRMED in [identity, trust and safety](04-IDENTITY-TRUST-AND-SAFETY.md)). An employee badge is display only; staff access still comes from the staff realm plus the application's role records.
- A verification badge shows only while its status is *verified*. Pending, needs action, rejected and expired/recheck show nothing publicly (statuses from [identity, trust and safety](04-IDENTITY-TRUST-AND-SAFETY.md)).
- Where the API provides it, the profile shows when each check happened ("Checked 17 Oct", reference screen 10).
- Badges sit right after the name, 16 px on tiles and lists and larger on the profile page, with the same colours in every scheme and mode (UI-STYLE.md).
- Fictional and prototype people may show badges only when labelled as simulated.
- Not every flag has a badge: admin verified is backend only (D58).

## Modes

OPEN (Q12). The final set of modes is not decided yet. Names in use so far:

| Source | Names |
| --- | --- |
| [Product brief](01-PRODUCT-BRIEF.md) (contexts) | Connect, Friends & company, Travel & exploring |
| Reference screens (segmented control) | Dating, Friends, Activities |
| Owner, 23 October 2026 (examples) | Dating, friends, business |

Until the set is final, the segmented control and intention chips take their labels from data, and screens use the reference names only as placeholders.

## Current state

- Web: `Components/UI/Badge.razor` draws photo verified, ID verified, founding, staff and bot badges with fixed `--badge-*` colours. The fictional people preview shows a "simulated" ID badge only. Admin verified has no badge by design (D58).
- MAUI: badge colours in `Colors.xaml`; no badge drawing yet.
- API: `member_profiles` stores the flags, verification statuses and optional DOB/age (D69, D70); `BadgePolicy` returns the badges to show (D74) and `/api/v1/me` returns the member's own. No other-profile endpoint, flag-setting admin routes or verification integrations yet.

## Open questions

- **Q11.** Remaining flag rules: whether admin verified changes age status (for example D40's staff override to adult); whether someone can show several badges at once; whether bot and employee accounts appear in discovery. Answered: founding member (D63), employee badge (D57) and admin verified (D58).
- **Q12.** The final set of modes and how they relate to intentions and discovery contexts.
