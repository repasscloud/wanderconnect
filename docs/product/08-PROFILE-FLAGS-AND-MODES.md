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

CONFIRMED, owner 23 October 2026 (D92; full rules in [Q12 — Connection modes, discovery and Travel state](../delivery/QUESTIONS.md#q12--connection-modes-discovery-and-travel-state)). Connection modes say **why** someone wants to connect; Travel says **where** they want to be discovered. The two are independent.

### Connection modes

Swoovr has four connection modes: **Dating, Friends, Activities and Business**. No other connection modes exist.

- **Several at once.** A member may turn on any combination of modes. Turning one on or off never changes the others.
- **Independent per mode.** Each mode keeps its own discovery preferences, filters, compatibility criteria and profile visibility, and has its own intention chips. Discovery respects the selected mode and the preferences of both members.
- **Intention chips are configured per mode.** CONFIRMED, owner 24 October 2026 ([Q16](../delivery/QUESTIONS.md#q16--mode-specific-intention-chips-and-selection-limits), D106): ten chips per mode, forty in all, up to five per mode. Chips never exclude anyone from discovery on their own. A chip retired after a member picked it stays stored and hidden and never blocks a save (D113, DEFAULT).
- **One identity.** Every mode runs on the same account and core profile. No separate accounts or duplicated profiles per mode, and switching mode never bypasses a block.
- **Data-driven.** Modes have stable internal identifiers, separate from their display labels. Labels, order, availability and presentation are configuration, not code.
- **Segmented control.** The primary segmented control shows the four modes: Dating, Friends, Activities, Business. Travel never appears in it.

### Travel

Travel is an optional, independent **discovery state**, not a fifth mode. It changes where a member can be discovered, across whichever modes they have on, without changing their modes, profile, compatibility criteria or preferences. It lets a member be discovered in a destination before arriving.

- **14-day limit.** Travel for a single destination can be switched on or scheduled up to 14 days before the planned arrival. It never gives open-ended visibility somewhere else.
- **Multi-city itineraries.** A member may plan several destinations. Later destinations in the same itinerary may fall beyond the 14-day limit. Each destination records its location, arrival date, departure date and discovery period, and can be managed on its own. Contradictory or unlimited destinations are refused.
- **Cancel any time.** A member can turn Travel off, cancel an itinerary or remove one destination at any time.
- **Arrival and expiry.** On arrival, discovery moves to the member's actual location, within their location permissions and privacy settings. When a destination's period ends, remote discovery there stops automatically unless another valid destination applies.
- **Shown as travelling.** A profile found through Travel says the member is travelling or planning to visit, never that they are already there. Exact dates and precise locations are shown only as far as the member's privacy settings allow.
- **No bypass.** Travel never overrides blocks, account restrictions, moderation, age or verification requirements, mode eligibility or any other discovery safeguard. Pre-arrival messaging follows D60: locals may start conversations; travellers may reply but not start one before arrival.
- **Own interface.** Travel has its own screen for choosing destinations, managing the itinerary, scheduling and cancelling, separate from the segmented control.
- **Server-side.** Itinerary checks, the 14-day limit and its multi-city exception, activation and expiry are enforced by the application, not the client.

### Earlier names

**Connect**, **Friends & company** and **Travel & exploring** were the [product brief](01-PRODUCT-BRIEF.md)'s original context labels. They are legacy names, not extra connection modes. "Dating, Friends, Activities" on the reference screens was a placeholder set, now replaced by the four modes above.

Built on 23 October 2026 (slice I, D99–D105): mode records with stable ids and configurable labels, members' per-mode settings (on, visible, intentions), the shared core profile (display name, area, about), Explore for one mode and another member's profile in shared modes, on the web in Interactive WebAssembly. Built on 24 October 2026 (slice J, D106, D113): the forty chips seeded with fixed ids, the "n of 5" picker on the web profile page, chips on Explore tiles and person pages. Not built: per-mode filters and compatibility criteria, Travel itineraries (Q17), and the modes screens in the app.

## Current state

- Web: `src/Swoovr.Web.Client/UI/Badge.razor` (moved from `Swoovr.Web/Components/UI` on 23 October so WebAssembly pages can use it) draws photo verified, ID verified, founding, staff and bot badges with fixed `--badge-*` colours. The fictional people preview shows a "simulated" ID badge only. Admin verified has no badge by design (D58).
- MAUI: badge colours in `Colors.xaml`; no badge drawing yet.
- Modes (slices I, J): `connection_modes`, `mode_intentions` (forty chips, D106), `member_modes`, `member_mode_intentions`; routes `/api/v1/modes`, `/api/v1/me/profile`, `/api/v1/me/modes/{id}`, `/api/v1/explore`, `/api/v1/profiles/{publicId}`; web pages `/me/profile`, `/explore`, `/people/{id}`. Others' badges are not returned yet (Q11).
- API: `member_profiles` stores the flags, verification statuses and optional DOB/age (D69, D70); `BadgePolicy` returns the badges to show (D74) and `/api/v1/me` returns the member's own. Founding status is granted by an admin route and restorable with the recovery token (D79–D81). No other-profile endpoint, other flag-setting admin routes or verification integrations yet.

## Open questions

- **Q11.** Remaining flag rules: whether admin verified changes age status (for example D40's staff override to adult); whether someone can show several badges at once; whether bot and employee accounts appear in discovery. Answered: founding member (D63), employee badge (D57) and admin verified (D58).
- **Q12.** ANSWERED, owner 23 October 2026 (D92): four connection modes and Travel as a separate discovery state; see [Q12 — Connection modes, discovery and Travel state](../delivery/QUESTIONS.md#q12--connection-modes-discovery-and-travel-state). The intention set for each mode is still to be defined.
