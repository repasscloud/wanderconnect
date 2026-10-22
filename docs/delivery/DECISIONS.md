# How questions and decisions work

Keep owner attention on decisions that change the product. Before asking, read the brief and decision register, inspect existing evidence, and check whether a confirmed requirement already answers it.

1. Record each uncertainty in QUESTIONS.md with ID, status, earliest blocking phase, consequence and proposed default.
2. Ask one concrete question at a time, highest dependency first. Explain the affected user moment, give a recommendation and at most two alternatives when useful. Bundle closely related facts such as provider URL/version in one request.
3. Ask only when the answer materially changes scope, policy, cost, privacy or integration feasibility. Choose reversible implementation details autonomously and mark DEFAULT. Do independent work while awaiting required answers.
4. Put the answer, date, source (owner or verified provider reference), affected documents and implementation consequence in the decision register. Update the product docs in the same PR. Do not leave conflicting copies.
5. Before each phase, present a concrete reviewable slice, evidence, remaining questions and the next phase's entry conditions. Unanswered questions stay OPEN; silence is not approval.

Q10 was answered by the owner on 16 October 2026 (D14). Configured Keycloak session evidence remains a phase 1 integration gate.

## Decision register — 16 October 2026

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D01 | CONFIRMED, owner | Six personal schemes, both modes | Theme spec replaces original muted palettes |
| D02 | SUPERSEDED by D11 | Earlier IdentityCloak naming | Owner corrected provider to Keycloak |
| D03 | CONFIRMED, owner | Partner/admin/support dedicated email/password or magic link; optional MFA | Backend-enforced login matrix; separate support role |
| D04 | CONFIRMED, owner | Other users Apple/Google/email; extra options uncertain | Member links DEFAULT proposal; no extra social providers |
| D05 | CONFIRMED direction, owner | MAUI, Blazor, API, Contracts, Client, Domain, Infrastructure | Seven projects plus tests |
| D06 | CONFIRMED, owner | Local testing, Actions commented out, PR to main | No hosted workflow execution |
| D07 | PARTLY SUPERSEDED by D30 | Original brand-neutral namespace default replaced by Swoovr; .NET 10, SSR shell and separate local solution retained | D30 confirms product/code naming; mobile tooling remains isolated |
| D08 | PARTLY SUPERSEDED by D12 | Invitation provisioning remains DEFAULT; separate staff/member account assumption withdrawn | Q10 defines dedicated-email access; no email-based classification |
| D09 | DEFAULT, implementation | Electric violet + System; member email links; 10-minute link expiry | Provider and phase review can amend defaults |
| D10 | DEFAULT, implementation, 16 October 2026 | Coarse account-area policy uses one application-owned role per evaluated session; no implicit role hierarchy; venue access requires an explicit nonempty venue ID and current membership | Phase 1 Domain slice only; separate-account portion of D08 superseded by D12. Additional roles/permissions require explicit design, not email matching or admin inheritance. See [slice](PHASE-1-ACCESS-POLICY.md) and [authentication](../architecture/AUTHENTICATION.md). |
| D11 | CONFIRMED, owner reply, 16 October 2026 | Keycloak is the central authentication service; official docs and source supplied | Supersedes D02 naming; inspect/reuse configuration before proposing a pin and realm |
| D12 | CONFIRMED, owner reply, 16 October 2026 | Accounts may have direct and linked Apple/Google credentials; registration, email ownership and session authentication are distinct evidence | Supersedes exclusive staff/member account assumption in D08/D10. Dedicated-email definition subsequently resolved by D14; provider evidence remains unvalidated |
| D13 | DEFAULT proposal, implementation, 16 October 2026 | Keycloak 26.8.1 and isolated swoovr-dev realm | No existing pin/realm/sandbox found; not deployed, configured or owner-approved. See [proposal](../architecture/KEYCLOAK-DEVELOPMENT.md) |
| D14 | CONFIRMED, owner Q10 reply, 16 October 2026 | Allow linked identities; privileged access requires verified email and a fresh direct email/password or approved email-link session | No separate social-free account requirement. Social sessions remain denied even on accounts with direct credentials. Configure and validate positive session evidence before enabling access; email-link capability remains unverified |

A default does not imply the corresponding feature has been implemented. New decisions append rows and supersede old ones explicitly.

D10 is an implementation default, not an owner answer to Q01–03/Q09. D03–04 and D11–12 remain authoritative. Member email links remain a proposal under D09 and are denied by the current policy until the decision and provider contract are resolved.

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D15 | CONFIRMED, owner 16 October 2026 | All server components containerised for public/dev infrastructure; mobile apps excepted | Prepare local Docker builds/testing; no deployment authorization |
| D16 | DEFAULT, implementation 16 October 2026; advances D13 | Separate digest-pinned API/web images and local-only Keycloak 26.8.1, empty swoovr-dev realm, generated private bootstrap secret, development embedded database | Reversible local infrastructure only; no app clients or login adapter. See [sandbox slice](PHASE-1-KEYCLOAK-SANDBOX.md). Production database/HTTPS/secrets remain a later deployment slice |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D17 | DEFAULT, implementation 16 October 2026 | Opt-in local Node browser investigation client, fixed loopback callback, copied password-execution AMR with experimental 60-second lifetime, private fictional-adult fixture with simulated administrator-asserted email status | Advances Q01 with browser/code/PKCE evidence only. No app authentication, privileged grant, real email ownership or product freshness policy. See [browser slice](PHASE-1-BROWSER-INVESTIGATION.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D18 | DEFAULT, implementation 16 October 2026 | Investigation transactions supersede older begins; serialize local session provider operations; logout invalidates pending callbacks; silent SSO preserves known identity/session/time; reauth preserves subject and validates auth_time against request start with five-second clock tolerance | Investigation-only consistency checks, no product privileged freshness policy or access grant. See [browser investigation](PHASE-1-BROWSER-INVESTIGATION.md#session-consistency-follow-up--16-october-2026) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D19 | DEFAULT, implementation 16 October 2026 | Pure Domain discovery gate requires active verified adults, reciprocal explicit intention/context/area rules, both-direction block enforcement and shared resource checks; contact adds permission, request limit, explicit incognito disclosure and actor/area-bound fresh travel presence | No endpoint/provider enabled. Hidden intentions remain checked; no-photo status never denies. Accepted-chat continuity and Q05 reply exception are outside this new-contact gate. See [scope](PHASE-2-DISCOVERY-POLICY.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D20 | DEFAULT, implementation 16 October 2026 | Static SSR appearance settings with a synchronous external guest preference bootstrap; versioned browser-local scheme/requested mode only; retain explicit guest choice on this browser; storage denial keeps in-memory choice with unsaved notice | No account sync, authentication or web render-mode decision. Generated web CSS/data come from themes.json. See [scope](PHASE-2-WEB-APPEARANCE.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D21 | DEFAULT, implementation 16 October 2026 | Portable appearance state uses per-activation response tickets, default display while loading, versioned server compare-and-swap/idempotency DTOs, unchanged retry identity offline and explicit conflict resolution; generic guest preference deliberately survives sign-out | No HTTP/account integration. Revision long.MaxValue is an explicit terminal read-only state: accept its acknowledgement and prevent further saves. Future authenticated transport must bind outgoing work to its activation. See [scope](PHASE-2-APPEARANCE-SYNC.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D22 | DEFAULT, implementation 16 October 2026 | Opt-in Development-only fictional discovery API/typed client/static SSR preview, fixed adult viewer, explicit verification/location simulations and generic resource denials; optional intention/activity filters demonstrate allowed/empty/denied states | No real session, privileged verdict, provider/storage connection or messages. Production suppresses routes regardless of flag. See [scope and local demo](PHASE-2-DISCOVERY-PREVIEW.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D23 | DEFAULT, implementation 16 October 2026 | Opt-in unique temporary local RSA signing provider; preserve existing keys; verify JWKS overlap/removal and restore original active key; renew short-lived management authorization for cleanup | Provider/browser investigation only, no production validator/cache policy or privileges. See [scope](PHASE-1-SIGNING-ROTATION.md) |

| D24 | DEFAULT, implementation17 October2026 | Optional owned-fixture TOTP browser investigation with unique temporary authenticator, preexisting-credential refusal and exact cleanup; RFC6238 generator used only in test memory | No global MFA mandate, recovery or application access. See [evidence](PHASE-1-OPTIONAL-MFA.md) |

| D25 | DEFAULT, implementation17 October2026 | Immutable retained-pair Domain connection lifecycle; one text request, recipient accept/decline, terminal decline/close, accepted-chat continuity under fresh safety gates, independent historical reporting eligibility | Future store must enforce unique pair and CAS; no messaging/realtime enabled. Q05 pre-arrival acceptance stays denied. See [slice](PHASE-3-CONNECTION-RULES.md) |

| D26 | DEFAULT, implementation17 October2026 | Private immutable meeting proposals require both explicit confirmations; material edits retain the last agreement until both accept replacement; completion requires scheduled instant and both lifecycle acknowledgements | Not attendance/chemistry/payment/booking evidence. Current connection safety gates collaboration; cancellation remains after closure/block. See [slice](PHASE-3-MEETING-PLANS.md) |

| D27 | DEFAULT, implementation17 October2026 | Independent owner-private feedback for retained genuine connection/agreed meeting; scalar self-report answers and immutable amendment audit, exact actor/pair/interaction binding | No counterpart access, public score, automated sanctions, real reports/storage or attendance proof. Future unique(interaction,author)/CAS/retention gates. See [slice](PHASE-3-PRIVATE-FEEDBACK.md) |

| D28 | DEFAULT, implementation17 October2026 | Development-only disposable fictional connection API/client, canned messages, explicit actor simulation,32scenarios/64messages/20min expiry/16KiB body; locked scenario revision updates and current Domain projections | No real auth/messages/durable pair/realtime. Reset creates a distinct disposable simulation; production repeat policy unchanged. See [contract](PHASE-3-CONNECTION-SCENARIOS.md) |

| D29 | DEFAULT, implementation17 October2026 | Existing staticSSR named forms/antiforgery drive the fictional connection preview; explicit simulated role switch, canned messages and controlled stale/offline states; shared personal theme tokens | No authenticated interaction/render decision(Q03), real requests/chat or realtime. See [browser evidence](PHASE-3-CONNECTION-WEB.md) |

## Naming confirmation — 22 October 2026

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D30 | CONFIRMED, owner 22 October 2026 | Product and codebase use Swoovr; lowercase identifiers use swoovr | Supersedes D07 namespace/name separation and the naming shortlist. Rename solutions, seven projects, test project, namespaces, UI, scripts, local realm/Compose IDs and documentation. Preserve real external URLs and historical evidence; no production deployment or store clearance implied. See [conventions](../architecture/NAMING.md) and [migration](SWOOVR-RENAME-2026-10-22.md). |

## Keycloak application integration — 22 October 2026

Owner answers given while designing the [Keycloak integration](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md). The design document was approved by the owner on 22 October 2026; its DEFAULT and VERIFY items are not decisions.

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D31 | CONFIRMED, owner 22 October 2026 | Three Keycloak realms: members, venues, staff. Venue organisations may later sign in with email or their own SSO | Supersedes the shared-account part of D12; a person who is both member and venue partner has separate accounts. Social providers exist only in the members realm |
| D32 | CONFIRMED, owner 22 October 2026 | Repass Cloud employees sign in with Microsoft 365 / Entra ID SSO; web portal only (mobile browser allowed, no app sign-in) | Adds corporate SSO as a privileged method; replaces D14's email-session mechanics for staff. Consumer Apple/Google stay excluded |
| D33 | CONFIRMED, owner 22 October 2026 | Passkeys are in scope for members; custom QR-code sign-in is dropped | Supersedes "passkeys OPEN". Passkey cross-device sign-in covers the QR use case |
| D34 | CONFIRMED, owner 22 October 2026 | Member and venue email sign-in links, implemented inside Keycloak (custom authenticator) | Answers Q02 for members. If impractical, remove and document why |
| D35 | CONFIRMED, owner 22 October 2026 | The Swoovr API must never mint sign-in tokens or obtain sessions on a user's behalf | No impersonation role, no user-token exchange for the API, no API-created login links |
| D36 | CONFIRMED, owner 22 October 2026 | One root admin: username/password only with MFA enforced | Exception to optional MFA (D03) for this account only |
| D37 | CONFIRMED, owner 22 October 2026 | Lock an account for 15 minutes after 3 failed password attempts; a completed password reset clears the lock immediately | Revised by the owner the same day from lock-until-reset, to limit deliberate lockout. Keycloak temporary brute-force lockout |
| D38 | CONFIRMED, owner 22 October 2026 | Never link accounts by matching email; reauthenticate before linking/unlinking; handle Apple private relay addresses | Restates product policy as owner-confirmed |
| D39 | CONFIRMED, owner 22 October 2026 | Production `https://swoovr.app`, UAT `https://uat.swoovr.app` (UAT moves to separate Azure billing later); email via the MailerSend HTTP API (not SMTP); daily backups of Keycloak and the database | Passkey relying-party IDs follow these domains. No deployment or account configured |
| D40 | CONFIRMED, owner 22 October 2026 | Registration requires an "I'm 18 or over" checkbox; DOB entered at profile setup and stored; under-18 DOB locks the account; AI profile review can require Stripe age verification; support/admin can override to adult; an 18+ DOB is not "age verified" | Keycloak holds only the attestation; age status is application-owned. Stripe Identity answers Q04's provider choice only |
| D41 | CONFIRMED, owner 22 October 2026 | Keycloak pages use Standard only, light/dark from the device, no scheme picker or remembered scheme; claims footer on every page; provider brand rules for Apple/Google; emails in Standard light | Uses the supplied `keycloak.v2` theme after version checks |

## Keycloak members slice — 22 October 2026

Implementation choices made while building the first slice of the [integration design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md). All are reversible; see the [slice record](PHASE-1-KEYCLOAK-MEMBERS.md) for evidence.

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D42 | DEFAULT, implementation 22 October 2026 | Member-area access requires the members realm, an active session and verified email, but no particular sign-in method | Realm separation (D31) makes method evidence a privileged-only concern. Needed because Keycloak's email-first registration session carries no AMR. Venue and staff areas still require positive method evidence |
| D43 | DEFAULT, implementation 22 October 2026 | The 18+ checkbox is Keycloak's built-in registration terms step (relabelled) plus the `TERMS_AND_CONDITIONS` default required action | Server-enforced before any account exists; records the attestation time, emitted as `swoovr_adult_attested_at`; also asks brokered accounts. The user-profile checkbox approach failed on 26.8.1 |
| D44 | DEFAULT, observed Keycloak 26.8.1 behaviour | Keep email verification on, which makes registration email-first (password after the link) | No password can be set on an unverified address |
| D45 | DEFAULT, implementation 22 October 2026 | Passkeys enabled only in the members realm | 26.8.1 reports a passkey used on the sign-in form as `pwd`; that would blur privileged evidence in venues/staff. Splitting the flow is follow-up work |
| D46 | DEFAULT, implementation 22 October 2026 | AMR references use a 12-hour `maxAge`, matching the SSO session | Keycloak's default 0 drops the reference after the authentication second. Privileged freshness stays on `auth_time` (12 hours) and future step-up |

## UI style step 3: shared components — 22 October 2026

Choices made while building the [UI style](../design/reference/UI-STYLE.md) components and restyling the web screens. All are reversible; evidence is in [validation](VALIDATION.md#ui-style-step-3-shared-components-and-restyled-web-screens--22-october-2026).

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D47 | DEFAULT, implementation 22 October 2026 | Badge colours are generated from `docs/design/brand-colours.json` into `themes.css` (`--badge-*`) and MAUI `Colors.xaml` (`Badge*`) | themes.json has no badge values; UI-STYLE fixes them in every scheme. No other colour outside themes.json |
| D48 | DEFAULT, implementation 22 October 2026 | The prototype's in-between greys (placeholder, disabled, checkbox border, focus ring, quote mark, tile intentions, sheet backdrop) are `color-mix()` of themes.json tokens | Standard light gives the exact prototype values; dark and personal schemes get matching shades. `check_design.py` rejects literal hex/rgb/hsl, shadows, gradients and Bootstrap classes in web styles |
| D49 | DEFAULT, implementation 22 October 2026 | The photo-free tile and profile quote block use the scheme's primary/onPrimary ("ink" in Standard light) | In dark mode the block is light, like primary buttons; in personal schemes it takes the scheme colour |
| D50 | DEFAULT, implementation 22 October 2026 | One navigation: fixed 80px bottom tab bar below 760px, top navigation above. Explore → people preview, Chats → connection preview (both only when the prototype is on), Me → account and settings. Connections has no screen and is shown as plain muted text, not a link | Appearance moved from the header into Me (linked from the account page). Revisit when real Connections/Chats screens exist |
| D51 | DEFAULT, implementation 22 October 2026 | Fictional people show only the ID badge, labelled "simulated in this preview" in its accessible name and on the profile card; no photo badge | The prototype contract has no photos or per-person verification; the policy simulates identity for every fixture |
| D52 | DEFAULT, implementation 22 October 2026 | Filters are a native `popover` bottom sheet on phones (no script) and an inline side panel at 760px and wider. The rule chips (Long-term on; Casual and FWB struck through) are derived from the fixed viewer rules | Existing form fields, names and behaviour are unchanged |
| D53 | SUPERSEDED by D56, owner 23 October 2026 (was DEFAULT, implementation 22 October 2026) | Where screenshot 13 (chat) and the UI-STYLE table differ, the table wins: their bubble is surface + hairline (not soft grey) and send is a round ink button (not an outline icon) | Matches UI-STYLE's rule that it overrides frames |
| D54 | DEFAULT, implementation 22 October 2026 | `/prototype/components` (Development + `Prototype:Enabled` only) shows every shared component, including the Plan a date pieces that have no screen yet | Used by `tests/ui/components.browser.cjs`; closed in default and Production configuration |

## Profile flags owner direction — 23 October 2026

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D55 | CONFIRMED, owner 23 October 2026 | Keycloak answers "who" (authentication, minimal data, the first 18+ catch at registration); the application database and API answer "what". Profile flags (founding member, photo verified, ID verified, admin verified, bot account, employee account) live on the profile in the application, never in Keycloak, and the API decides which badges a viewer sees | Clients draw only the badges the API returns. See [profile flags and modes](../product/08-PROFILE-FLAGS-AND-MODES.md); criteria and the admin-verified badge are Q11 |
| D56 | CONFIRMED, owner 23 October 2026 | The chat follows reference screen 13 (file `13-plan-a-date.png`): their bubble is a soft grey pill with no border; the member's own reply is the scheme's opposite colour (primary, black-ish in Standard light); the composer has an outlined round "+" button, a soft pill input and an outline send arrow | Supersedes D53 and the UI-STYLE.md Chat row. `docs/design/` is a read-only copy, so that row needs updating in swoovr-marketing. The conversation preview shows "+" disabled because the demo has no attachments |
| D57 | CONFIRMED, owner 23 October 2026 | Employee accounts show the ruby "Swoovr staff" bird badge | Display only; staff access still comes from the staff realm and application roles |
| D58 | CONFIRMED, owner 23 October 2026 | "Admin verified" is a backend flag with **no badge**. Likely uses: employee accounts, or people Swoovr has met in person and whose age was checked in person. It never shows or implies the ID-verified badge, because no identity provider checked and stored their ID | The API may use the flag in its decisions but never returns it as a badge. See [profile flags and modes](../product/08-PROFILE-FLAGS-AND-MODES.md) |
