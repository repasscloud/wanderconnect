# Current Swoovr delivery status

As of 22 October 2026 (Australia/Adelaide). This page is the current summary; dated slice documents and the progress ledger explain how we reached it. Swoovr is an integrated local prototype, not a member-ready pilot or production deployment.

## What exists and where

| Area | Implemented | Location / remaining boundary |
| --- | --- | --- |
| Foundation — phase 0 complete | Seven .NET application projects, portable checks, local server containers | `src/Swoovr.*`, `Swoovr.Local.sln`; full solution includes unvalidated mobile targets |
| Identity — phase 1 partial | Owner-approved [Keycloak design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md); local members/venues/staff realms with Standard theme, 18+ registration, email verification, 15-minute lockout, passkeys; realm-aware session evidence; API token validation; members web sign-in | `containers/keycloak`, `src/Swoovr.Domain/Access`, `src/Swoovr.Api/Auth`, `src/Swoovr.Web/Auth`; [slice record](PHASE-1-KEYCLOAK-MEMBERS.md). Local only; no Apple/Google/Entra/MailerSend, no persistence, venue/staff areas closed |
| Appearance — phase 2 partial | Eight schemes (Standard default) × System/Light/Dark, browser-local guest saving; portable account conflict/retry rules | `/settings/appearance`, `src/Swoovr.Client/AppearanceSyncState.cs`; no account sync transport or mobile acceptance |
| Discovery — phase 2 partial | Policy gates and fictional adult grid/profile API → Client → Web preview | `/prototype/people`; development opt-in only; no live profile storage, identity or location evidence |
| Connections — phase 3 partial | Request/accept/decline/canned reply/block/close scenarios with revision checks and SSR forms | `/prototype/conversation`; fictional actors, process-local state, no real messages/realtime |
| Meetings and feedback — phase 3 partial | Portable mutual-consent plan revisions and author-private feedback rules | `src/Swoovr.Domain/Connections`; no live UI, persistence or moderation integration |
| Venues / commerce — phase 4 planned | Requirements only | No booking, payment, voucher or provider integration |
| Pilot operations — phase 5 planned | Requirements and open decisions | No completed security/device/store/operations/pilot acceptance |
| Naming — owner confirmed 22 October | Swoovr display/engineering and swoovr machine identifiers | [D30](DECISIONS.md#naming-confirmation--22-october-2026), [migration](SWOOVR-RENAME-2026-10-22.md) |

## When and why

The 16–18 October slices established and tested the foundation, independent policies and fictional journeys while live integration decisions remained unresolved. PRs #5–#17 were integrated into main; see [integration evidence](MAIN-INTEGRATION-2026-10-18.md). The 22 October rename follows the owner's explicit instruction to use one name throughout the codebase and organise the documentation. It does not complete an unfinished product phase.

## How to run and assess it

Follow [local development](LOCAL-DEVELOPMENT.md) for tools and commands. Run `./scripts/check.sh` for portable builds, policy/state/Node checks, design/link checks and local HTTP gates. Use the [discovery](PHASE-2-DISCOVERY-PREVIEW.md#local-demo) and [connection](PHASE-3-CONNECTION-WEB.md) opt-in instructions for simulations. [Rename validation](SWOOVR-RENAME-2026-10-22.md#validation) records current evidence; [validation history](VALIDATION.md) preserves earlier observations. A passing portable check is not authentication, device or production acceptance.

## What comes next and why

1. Continue the [Keycloak design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md#delivery-order-default): persistence for accounts/roles/invitations, Apple/Google brokering once registrations exist, email sign-in link and MailerSend extension, venue and staff portals (Entra, root admin OTP), then mobile. Q03 (authenticated web rendering) and Q09 (mobile tooling) remain OPEN; Q02 is answered (D34).
2. Add persistence for real accounts/profiles and connect authenticated client journeys. PostgreSQL remains a DEFAULT, not a configured service. Keep privacy/media/authorization rules server-side.
3. Connect account appearance, live discovery/location/identity, messaging, meeting plans and private feedback. Resolve Q04 verification and Q05 pre-arrival reply policy before enabling dependent behavior.
4. Implement venue/commerce sandbox after the core loop; Q06 governs real financial responsibilities. Complete operations/device/security/accessibility/store gates before a pilot (Q07/Q08).

Owner attention should focus on the earliest material blocker, one question at a time under the [decision process](DECISIONS.md). Continue independent work without inventing provider capabilities, credentials, pricing or authorization. No deployment is authorized by this rename.
