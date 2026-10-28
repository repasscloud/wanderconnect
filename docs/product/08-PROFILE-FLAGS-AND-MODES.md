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
| ID verified | Government ID and age checked | The application, from Didit's outcome (D126; Stripe Identity in D40 is superseded) | Teal `#00AAA0` card with tick | Built in test mode (slice P, D155); revocable by an admin (D128) |
| Admin verified | Backend only. A Swoovr admin confirms something in person, for example an employee account or someone met in person whose age was checked (CONFIRMED, D58) | A Swoovr admin, recorded and audited in the application | **No badge.** Never shows or implies ID verified, because no identity provider checked and stored ID | CONFIRMED (Q11, 24 October 2026: internal trust review, valid until revoked, never public) |
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

### Travel as built (slice M, 24 October 2026)

Q17 and the follow-up answers (D107, D109–D111) are built as described in D120–D122: GeoNames city search, a private home city, unlimited trips with a lifecycle (planned, visible, expired, cancelled), itineraries of back-to-back stops that open together, overlap refusal for visible trips, and the destination feed in which locals of an open market (only Adelaide) see visiting travellers and may send them a request. Travellers get no feed of locals.

**Home discovery is local (Q24, D125, D146; slice O, built 25 October 2026).** Explore, another member's profile and a connection request reach only people whose home city is in the viewer's own open market (Beta or Active), or official bots an admin assigned to that market (`*` for every open market). The server works out each member's home market from their home city and the market's areas and never returns it. A member with no home city gets 403 `home-city-required`; outside every open market, in a planned or suspended market, or in a mode the market doesn't offer, 403 `market-not-open` with an option to register interest (`PUT /api/me/market-interest`). A profile also opens for a pair with a pending or accepted request in any mode, and for a local looking at a traveller visiting their market; a request also goes from a local to such a traveller (the visit is attached) and between two people already connected in another mode. Everything else about another member is the same 404 as anything not available. Moving the home city into a different market waits 30 days after the last such move (403 `home-market-locked`). Admins configure markets (`GET/PUT /api/admin/markets/{id}`: name, status, areas, modes) and bot markets (`PUT /api/admin/accounts/{id}/bot-markets`) without a release. Before 25 October 2026 Explore was not market-scoped (D101, D122), contradicting Q07 rule 2, Q17 and D60; slice O restores the design.

**Traveller destination search: prerequisite only (Q17 rules 33–36, not built; built in slice T per Q41, D142).** A future `GET` search for locals in a destination must take the id of one of the caller's own trips, not a free city. It is allowed only while that trip is not cancelled or expired, has both dates and references the destination's GeoNames id, and its city is in an active market. It must apply the same account, mode, block and moderation rules as Explore, and it must never add its results to the normal Explore feed. Without such a trip the answer is the same 404 as for anything not available.

Not built: arrival changing discovery to a device location (Q23), traveller search (above), Travel in the MAUI app (see validation).

### Earlier names

**Connect**, **Friends & company** and **Travel & exploring** were the [product brief](01-PRODUCT-BRIEF.md)'s original context labels. They are legacy names, not extra connection modes. "Dating, Friends, Activities" on the reference screens was a placeholder set, now replaced by the four modes above.

Built on 23 October 2026 (slice I, D99–D105): mode records with stable ids and configurable labels, members' per-mode settings (on, visible, intentions), the shared core profile (display name, area, about), Explore for one mode and another member's profile in shared modes, on the web in Interactive WebAssembly. Built on 24 October 2026 (slice J, D106, D113): the forty chips seeded with fixed ids, the "n of 5" picker on the web profile page, chips on Explore tiles and person pages. Built on 24 October 2026 (slices K–M): badges for others, staff and bots in discovery, connection requests and blocks, and Travel (see above). Not built: per-mode filters and compatibility criteria, traveller search (Q17 rule 36), and in the app the person pages, Connections lists, blocking, badge artwork and itinerary choice (slice N, D123, built the Profile, Explore and Travel tabs).

## Current state

- Web: `src/Swoovr.Web.Client/UI/Badge.razor` (moved from `Swoovr.Web/Components/UI` on 23 October so WebAssembly pages can use it) draws photo verified, ID verified, founding, staff and bot badges with fixed `--badge-*` colours. The fictional people preview shows a "simulated" ID badge only. Admin verified has no badge by design (D58).
- Slice K (24 October 2026, D108, D114–D116): other people's badges on Explore tiles and person pages (`BadgeRow`), several at once in the order held in `badge_definitions`; staff in discovery; bots only in admin-authorised modes with only the bot badge; admin routes for bot classification, bot modes and staff affiliation. Photo/ID verification and staff-report moderation are not built (Q04, Q20).
- MAUI (slice S, 26 October 2026, D163): badges drawn from the same shapes as the web (`Swoovr.Client/BadgeArtwork`) in the `Badge*` colours from `Colors.xaml`, on Explore, person pages, Connections and the account screen.
- Modes (slices I, J): `connection_modes`, `mode_intentions` (forty chips, D106), `member_modes`, `member_mode_intentions`; routes `/api/modes`, `/api/me/profile`, `/api/me/modes/{id}`, `/api/explore`, `/api/profiles/{publicId}`; web pages `/me/profile`, `/explore`, `/people/{id}`. Others' badges are returned since slice K.
- API: `member_profiles` stores the flags, verification statuses and optional DOB/age (D69, D70); `BadgePolicy` returns the badges to show (D74) and `/api/me` returns the member's own. Founding status is granted by an admin route and restorable with the recovery token (D79–D81). Other members' profiles open through `/api/profiles/{publicId}` (slice I); bot and staff-affiliation admin routes exist (D115). No photo/ID verification integration, Admin verified review route or other flag-setting admin routes yet.

## Open questions

- **Q11.** ANSWERED, owner 24 October 2026 (D108); see [Q11](../delivery/QUESTIONS.md#q11--profile-flags-verification-badges-founding-membership-staff-affiliation-and-official-bots).
- **Q12.** ANSWERED, owner 23 October 2026 (D92): four connection modes and Travel as a separate discovery state; see [Q12 — Connection modes, discovery and Travel state](../delivery/QUESTIONS.md#q12--connection-modes-discovery-and-travel-state). The intention chips were set on 24 October 2026 (Q16, D106).

### Filters, identity and matching as built (slice R, 25 October 2026)

D137–D140 are built as described in D157: a required, never-shown date of birth with the age shown unless hidden; gender, pronouns and orientation (each optional and separate, orientation never used for matching); Dating's reciprocal "show me"; per-mode filters (age range, required chips any or all, excluded chips that work both ways, ID Verified only, visitor visibility) and two Plus filters (distance between home cities, most in common first) answered by the entitlement service (D151). The page is **Me → Who I'd like to meet** (`/me/matching`). Owner answers on 26 October 2026: "Prefer not to say" is never a "show me" option and never hides someone by itself; "Another gender" is an inclusive "show me" category (D161); every member needs a date of birth and may hide their age while still matching age ranges (D162). Not built: height, body type, lifestyle, education, profession, saved searches and the weighted compatibility algorithm (later, D140).
