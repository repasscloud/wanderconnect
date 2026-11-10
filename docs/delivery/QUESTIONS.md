# Ordered question backlog

**Current UAT pointer (10 November 2026):** [D355](D355-UAT-HOSTNAMES-2026-11-10.md) supersedes historical nested hostnames and paid edge certificate assumptions. UAT hosting is OPEN, including Vultr; production remains Azure. Earlier records below preserve their original wording; Azure infrastructure is an optional implementation.


No answer is needed to review the phase 0 foundation. Ask these when approaching their blocking phase; do not ask all at once.

From 27 October 2026 (owner) every new question, and every piece of review feedback or follow-up to be factored in later, is also a GitHub issue; its row here carries the Q number and the issue link, and the issue links back here. Answers are recorded in [DECISIONS.md](DECISIONS.md) and on the issue.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q01 | PARTLY ANSWERED, owner 16 October 2026 | Phase 1 live auth | Keycloak docs/source confirmed; no existing pin/realm/sandbox found. Proposed pin/realm in D13; configured session evidence still needs sandbox validation | Use official sources; keep privileged integration disabled pending validated session evidence (Q10 answered in D14) |
| Q02 | ANSWERED, owner 22 October 2026; D34 | Phase 1 auth UX | Enable member magic links in addition to Apple/Google/email-password? What cross-device email-link behavior is expected? | Yes, if backend supports the required safe exchange |
| Q03 | ANSWERED, owner 23 October 2026; D59 | Phase 1 web interaction | Should authenticated web use Interactive Server or justify adding WASM client? | Server initially; measure scale needs before adding project |
| Q04 | PARTLY ANSWERED, owner 22 October 2026; D40 (Stripe Identity) | Phase 2 live verification | Which document/liveness provider, gate timing, retention and manual appeal route? | Gate browsing/contact; separate from Keycloak auth |
| Q05 | ANSWERED, owner 23 October 2026; D60 | Phase 3 travel | Can travellers reply before arrival when a local initiates? | Allow only replies to local initiation, subject to privacy rules |
| Q06 | ANSWERED, owner 23 October 2026; D61 | Phase 4 real commerce | Merchant of record, settlement, voucher benefit, refund and cancellation owner? | Sandbox only until contracts are decided |
| Q07 | ANSWERED, owner 23 October 2026; D62 | Phase 5 pilot | Initial city/markets and staffed support/moderation coverage? | Compact pilot; owner chooses market and operations |
| Q08 | ANSWERED, owner 23 October 2026; D63 (trademark/store checks outstanding) | Phase 5 publishing | Swoovr is confirmed; trademark/store availability, first paid entitlements and prices remain unresolved | Use Swoovr conventions; no invented clearance or pricing |
| Q09 | ANSWERED, owner 23 October 2026; D64 | Phase 1 mobile acceptance | Which Apple/Xcode and Android toolchain versions/devices are available? | Pin supported workload set after checking official compatibility |

Extra social sign-in providers/passkeys are deferred. Do not reopen approved six-scheme personalisation or optional MFA as unanswered questions. See [decision process](DECISIONS.md).

## Phase 1 follow-up — 16 October 2026

Q01 was asked again in the implementation chat as one focused request for Keycloak documentation/source, version, sandbox configuration and trusted dedicated-email versus social authentication evidence. No answer or provider contract was available when this slice was prepared; Q01 remains OPEN. No protocol, endpoint or provider claim was inferred. Independent Domain policy checks proceed; real login remains blocked. Q02/Q03/Q09 remain OPEN and are not silently approved by this slice.

## Owner answer — 16 October 2026

Owner corrected the provider name to Keycloak and supplied https://www.keycloak.org/documentation and https://github.com/keycloak/keycloak. This supersedes the preceding unanswered-provider snapshot. Inspection found no existing version pin, realm export, sandbox deployment or credentials configuration in the repository. D13 proposes a version/realm without claiming an existing environment. Direct email registration, verified email ownership, linked identities and the current session method are separate; email never determines exclusive account type. Government ID/liveness remains Q04.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q10 | ANSWERED, owner 16 October 2026; D14 | Policy resolved; configured evidence still blocks access | Allow linked Apple/Google identities; require verified email and a fresh direct email/password or approved email-link session for privileged access | No separate social-free account required; no configured Keycloak evidence or adapter yet |

Q10 owner selected “Allow linked identities; require direct email session” in response to the verified-email/fresh-session proposal. D14 records this confirmation. Exact freshness window and the configured proof remain implementation/validation work, not invented provider claims.

## Local sandbox follow-up — 16 October 2026

Q01 now has a repository-owned reproducible local sandbox (D16), superseding earlier no-environment snapshots. Discovery and realm startup are a separate validation layer from configured session evidence. Q01 remains partly answered until direct/brokered/linking/SSO/refresh/reauthentication and token-validation tests establish the adapter contract. No owner answer is needed for disposable container defaults. Q02/Q03/Q09 remain OPEN; neither a magic-link feature nor a web render-mode change is introduced.

## Browser investigation follow-up — 16 October 2026

Q01 advances with an opt-in [browser harness](PHASE-1-BROWSER-INVESTIGATION.md): local ID-token validation and configured method/session observations. Fixture email status is administrator-asserted simulation; no ownership delivery is demonstrated. Real broker/linked-account transitions, optional MFA/recovery, rotation and application authorization remain OPEN validation gates. D17's 60-second AMR lifetime is an experiment, not an owner-approved freshness policy. Q02/Q03/Q09 remain unchanged; none blocks this isolated investigation.

## Owner naming answer — 22 October 2026

D30 confirms Swoovr for the product and engineering identifiers. It supersedes the previous namespace exception and naming shortlist. Q08 remains partly answered because publishing clearance and paid terms were not decided. See [naming](../architecture/NAMING.md) and [current status](STATUS.md).

## Keycloak integration answers — 22 October 2026

Q02 is ANSWERED for members and venue partners by D34: email sign-in links are wanted, built inside Keycloak; the API never mints them (D35). Cross-device behaviour is a DEFAULT in the [integration design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md#email-sign-in-link). Q04 is PARTLY ANSWERED by D40: Stripe Identity is the document provider and the AI-flag/DOB/override rules are confirmed; liveness, retention and appeal route remain OPEN. Q03 and Q09 remain OPEN and do not block the first integration slices. The table above keeps its original snapshot.

## Profile flags and modes — 23 October 2026

D55 confirms that flags are application-owned. The owner is still defining the profile, so these stay OPEN and block only the profile slice. See [profile flags and modes](../product/08-PROFILE-FLAGS-AND-MODES.md).

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q11 | ANSWERED, owner 24 October 2026; D108 | Profile persistence and badges | Final flag set and rules: what admin verified means and its badge; founding-member criteria; employee badge = ruby "Swoovr staff" bird; several badges at once; bot/employee accounts in discovery | Use the five drawn badges; add admin verified when it is defined |
| Q12 | ANSWERED, owner 23 October 2026; D92 | Profile, discovery and the segmented control | Final set of modes (brief: Connect / Friends & company / Travel & exploring; reference screens: Dating / Friends / Activities; owner examples: dating, friends, business) | Answered: Dating, Friends, Activities and Business; Travel is a separate discovery state. See [Q12 — Connection modes, discovery and Travel state](#q12--connection-modes-discovery-and-travel-state) |

## Owner answers — 23 October 2026

The owner answered Q03 and Q05–Q09 below (decisions D59–D64) and part of Q11 (D57, D58; the founding criteria come from the Q08 answer). The chat style is settled by D56. The tables above keep their original questions; their status column was brought up to date on 23 October 2026.

| ID | Status | Answer in brief |
| --- | --- | --- |
| Q03 | ANSWERED, owner 23 October 2026; D59 | `swoovr.app` is a Blazor Web App: Static SSR for public and sign-in pages, Interactive WebAssembly for everything signed in, no Interactive Server or Auto. `swoovr.com` stays a separate Astro site |
| Q05 | ANSWERED, owner 23 October 2026; D60 | Travellers may reply before arrival to conversations a local started; they cannot start one until they arrive |
| Q06 | ANSWERED, owner 23 October 2026; D61 | Several payment/booking models per vendor (Swoovr-managed, vendor-managed, hold only, free); provider-agnostic payments, Stripe first; real money stays off until validated |
| Q07 | ANSWERED, owner 23 October 2026; D62 | Private, invitation-only beta in Adelaide (about 100 testers); business-hours support in beta, 24/7 at public launch; India-based team with escalation to RePass Cloud |
| Q08 | ANSWERED, owner 23 October 2026; D63 | Published by RePass Cloud Pty Ltd; Free/Plus/Premium tiers with AUD prices; Free only during beta; 1,000 founding memberships. Trademark, store and launch checks remain outstanding |
| Q09 | ANSWERED, owner 23 October 2026; D64 | .NET 10 + MAUI for iOS and Android, built and tested locally on macOS; CI/CD only for authorised store releases. Workload installation and validation remain outstanding |
| Q11 | ANSWERED, owner 24 October 2026; D108 (partly 23 October: D57, D58) | Employee = ruby staff bird (D57); admin verified = backend flag, no badge (D58); founding = gold Swoovr Founder badge, permanent, after onboarding and Photo and/or ID verification (Q08). 24 October: several badges show at once in a configurable order; staff take part in all four modes like anyone else and cannot hide the badge; bots appear only in modes an admin authorised, never initiate and never carry Photo/ID badges. See [Q11](#q11--profile-flags-verification-badges-founding-membership-staff-affiliation-and-official-bots) |
| Q12 | ANSWERED, owner 23 October 2026; D92 | Four connection modes (Dating, Friends, Activities, Business), several at once; Travel is an independent discovery state with a 14-day advance limit and multi-city itineraries. See [Q12 — Connection modes, discovery and Travel state](#q12--connection-modes-discovery-and-travel-state) |

### Points confirmed

Raised while filing these answers and answered by the owner on 23 October 2026.

1. **Q03 adds a project.** CONFIRMED. The first Interactive WebAssembly slice adds a separate `Swoovr.Web.Client` project, updates the [architecture](../architecture/README.md) to the new project count, updates `scripts/check_naming.py` to recognise and validate it, and keeps `swoovr.com` as a separate Astro application. These changes are authorised under Q03/D59 and need no further architectural approval.
2. **Founder recovery.** The earlier approvals in Q08 were not recorded in this repository. The founding rules below are now the record, and the owner described the recovery-token mechanism; see [founder recovery token](#founder-recovery-token).
3. **Company name.** Use "RePass Cloud Pty Ltd" everywhere (D66).
4. **Q07 rule 9.** Confirmed: the 24/7 staffing model applies from public launch; beta support is business hours.
5. **Q09 rule 2.** Confirmed by `sw_vers` on the development Mac: macOS 27.0.1, build 26A434.

### Q03 — Web rendering architecture

Decision: Hybrid Blazor Web App — Static SSR + Interactive WebAssembly.

Swoovr operates two distinct public-facing domains:

- **swoovr.com:** Corporate and product website, built using Astro. Remains independent.
- **swoovr.app:** Main Swoovr application, built entirely with Blazor.

swoovr.app rendering strategy:

- **Static SSR:** Unauthenticated landing page, login/registration entry points, authentication callbacks and server-handled account flows.
- **Interactive WebAssembly:** All authenticated application functionality, including discovery, profiles, messaging, activities, business connections, compatibility, account settings, etc.
- **No Interactive Server:** Avoid persistent Blazor server-side UI circuits for member interactions.
- **No Interactive Auto:** Do not use the Interactive Server fallback during WASM loading.

The public login page may use advanced CSS and JavaScript animations without requiring Blazor client-side interactivity.

Backend and security:

- ASP.NET Core Web API shared with .NET MAUI mobile applications.
- Keycloak for identity and authentication.
- Backend-for-Frontend for browser authentication and secure server-managed tokens.
- Server-enforced authorization, account permissions, profile visibility and privacy restrictions.
- SignalR for real-time functionality.

Rationale: Static SSR provides fast initial rendering for unauthenticated visitors. WebAssembly provides responsive client-side interactions without persistent server UI circuits. Both rendering models coexist in the same Blazor Web App.

Status: ANSWERED, owner 23 October 2026 (recommended architecture; the project layout below is a suggestion, not a requirement).

Suggested layout (owner: "do not need to follow my design"):

```none
Swoovr.Web/
├── Components/
│   ├── Pages/
│   │   └── Landing.razor       # Static SSR
│   ├── App.razor
│   └── Routes.razor
├── Program.cs
│
Swoovr.Web.Client/
├── Pages/
│   ├── Discover.razor         # WASM
│   ├── Messages.razor         # WASM
│   ├── Profile.razor          # WASM
│   └── Settings.razor         # WASM
```

### Q05 — Pre-arrival travel messaging

Status: ANSWERED, owner 23 October 2026

Decision: Travellers may respond to conversations initiated by local members before arriving at their destination.

Rules:

1. **Local members** may initiate conversations with travellers planning to visit their area.
2. **Travelling members** may respond to these conversations before arriving.
3. **Travelling members** cannot initiate new conversations with local members until they have arrived at their destination.
4. These restrictions apply specifically to pre-arrival Travel Mode and do not override messaging permissions in other connection modes.
5. All interactions remain subject to existing privacy, blocking, verification and messaging restrictions.

Rationale: Allows travellers to establish connections before arrival while limiting unsolicited messaging to local members.

Implementation: Enforce these restrictions server-side through the API, using the traveller's arrival status and applicable connection mode.

### Q06 — Commerce, payments and vendor settlement

Status: ANSWERED, owner 23 October 2026

Decision: Swoovr supports multiple payment and booking models depending on the commercial agreement with each vendor or service provider. The payment architecture must support multiple payment processors, with Stripe as the initial provider and support for PayPal, Square and other processors in future releases.

Rules:

1. **Swoovr-managed payments:** Swoovr / RePass Cloud Pty Ltd processes the customer's payment through a supported payment processor and remits the agreed amount to the vendor, deducting applicable platform fees, commissions and payment processing costs according to the vendor agreement.
2. **Vendor-managed payments:** The vendor connects a supported payment processor account to Swoovr. Payments are processed for the vendor through the agreed provider integration, with any applicable Swoovr platform fees handled according to the commercial arrangement and provider capabilities.
3. **Reservation or hold only:** Swoovr submits a booking or reservation request to the vendor to hold an item, ticket, service or availability. No payment is collected through Swoovr. The customer pays the vendor directly, typically upon arrival or when the service is delivered.
4. **Free bookings or activities:** Swoovr facilitates bookings, registrations or reservations without requiring payment. This applies to vendor-provided free offerings and activities independently organised through the platform.
5. **Vendor configuration:** Each vendor integration specifies its supported payment model, payment processor, booking process, commission arrangements, cancellation conditions and refund responsibilities.
6. **Refunds and cancellations:** Responsibility for refunds, cancellations and disputes is determined by the payment model, payment processor capabilities and vendor agreement. These conditions must be communicated to customers before confirming a transaction.
7. **Vouchers and benefits:** Vendor-specific discounts, promotional offers, vouchers and member benefits are governed by the applicable commercial agreement and must be validated before redemption.
8. **Payment security:** Payment processing must use secure, provider-supported integrations. Vendor credentials must never be stored or exposed insecurely. OAuth, connected accounts, delegated authorisation or equivalent provider-supported mechanisms should be used where available.
9. **Payment processor independence:** Payment processing must be implemented through a provider-agnostic abstraction rather than tightly coupled to Stripe. Stripe will be the initial supported processor, with the architecture designed to accommodate PayPal, Square and other providers without substantial changes to core commerce functionality.
10. **Provider capabilities:** Each payment processor integration must declare its supported capabilities, including payment collection, refunds, partial refunds, connected accounts, marketplace settlements, commissions, recurring payments and webhooks where applicable. Unsupported capabilities must not be assumed or exposed to vendors.
11. **Provider-specific data:** Transaction records must retain the payment provider, external transaction references, settlement information and relevant payment state independently of the underlying provider's implementation.
12. **Production activation:** Real-money transactions and vendor settlement functionality remain disabled until the relevant payment integration, commercial agreements and operational requirements have been validated.

Rationale: Allows Swoovr to support different vendor business models, including direct sales, commission-based transactions, reservations and free activities, without requiring every partner to adopt the same payment or settlement arrangement. A provider-independent architecture prevents vendor commerce from becoming permanently dependent on Stripe and allows additional payment processors to be introduced as the platform expands.

Implementation: Develop a modular payment orchestration layer with provider interfaces and dedicated payment processor adapters. Implement Stripe as the initial adapter, with future adapters for PayPal, Square and other supported processors. Separate payment processing, booking management, vendor settlement, commissions, refunds and vouchers into appropriate application services. Configure supported processors and payment models per vendor and offering. Use vendor booking APIs where available and internal reservation workflows where appropriate. Enforce all financial, cancellation, refund, commission and voucher rules server-side according to provider capabilities and applicable vendor agreements.

### Q07 — Pilot market, support and moderation

Status: ANSWERED, owner 23 October 2026

Decision: Swoovr will launch initially as a private beta focused on Adelaide, South Australia, with approximately 100 beta testers. Support and moderation will operate during business hours throughout the beta, transitioning to 24/7 coverage for the public launch. An India-based support and moderation team will handle day-to-day operations, with escalation to RePass Cloud Pty Ltd.

Rules:

1. **Initial launch market:** Adelaide, South Australia, is the primary launch market, with approximately 100 beta testers initially targeted.
2. **Geographic availability:** Members outside Adelaide may register and participate in the beta where authorised. Discovery functionality will be geographically restricted until their respective markets are activated.
3. **Private beta access:** The beta will operate as a controlled, invitation-only environment. Users must be explicitly enrolled before accessing the application.
4. **iOS beta distribution:** iOS testers must be enrolled through the approved private distribution mechanism before they can install and access the beta application.
5. **Web beta access:** The web application will only permit access to users whose registered email addresses have been pre-authorised in the Swoovr system. Eligible users will be automatically activated when their verified email address matches an authorised beta enrolment.
6. **Beta testing scope:** Beta testers are encouraged to explore and test all available application functionality, including registration, verification, discovery, messaging, reporting, blocking, privacy controls and other enabled features.
7. **Beta support coverage:** Support and moderation will operate during defined business hours throughout the beta period. Escalation procedures must remain available to beta testers, including for issues discovered while testing functionality.
8. **Public launch coverage:** Support and moderation will transition to 24/7 operations when the application becomes publicly available.
9. **Support staffing:** Support and moderation will operate 24/7 using three eight-hour shifts per day, with two staff members assigned to each shift. Staffing will be drawn from a pool of more than 12 contractors, ensuring continuous coverage, shift rotation and operational flexibility. Contractor scheduling, staffing allocation and workforce management will be handled by RePass Cloud Pty Ltd.
10. **Support team location:** Day-to-day support and moderation will be handled by an India-based team, with RePass Cloud Pty Ltd responsible for oversight, escalation and final decisions on serious incidents.
11. **Support channels:** Members may request assistance through in-app support and email. An emergency escalation mechanism will be available for urgent safety, security or operational incidents.
12. **Response SLA:** The initial target is a response within four hours. This is a first-response target rather than a guaranteed resolution time. The target may be increased or reduced as operational capacity, platform usage and service requirements evolve.
13. **Automated moderation:** Automated screening will assist with identifying potentially harmful content, suspicious accounts, policy violations and other safety concerns.
14. **Human moderation:** Flagged content, member reports and other incidents requiring judgement will be reviewed by authorised human moderators.
15. **Appeals:** Members must have access to an appeal process for moderation actions, account restrictions and suspensions. Appeals must be reviewed independently of fully automated decisions where appropriate.
16. **Escalation:** Serious incidents, urgent safety concerns, security breaches, platform-wide issues and matters exceeding moderator authority must be escalated to RePass Cloud Pty Ltd.
17. **Beta feedback:** Beta testers must be able to report defects, usability concerns, unexpected behaviour and feature suggestions through established support and feedback channels.
18. **Operational readiness:** Public launch requires satisfactory completion of security reviews, privacy and safety assessments, moderation procedures, support training, escalation testing and operational readiness checks.
19. **Launch approval:** The public launch date will be determined by readiness rather than a fixed deadline. RePass Cloud Pty Ltd retains final approval authority for transitioning from private beta to public availability.

Rationale: A controlled Adelaide-focused beta allows Swoovr to validate functionality, moderation, privacy controls and operational processes with a manageable initial membership. Restricted enrolment ensures that early access is intentional and traceable, while encouraging testers to exercise the full application. Business-hours support during beta reduces initial operational requirements, with dedicated escalation available for serious issues. The transition to continuous moderation and support at public launch provides a scalable operational foundation.

Implementation: Develop a centralised beta enrolment and access-control system supporting authorised email addresses, activation status, distribution eligibility and geographic market restrictions. Integrate beta eligibility with Keycloak authentication and enforce access server-side. Configure market availability and discovery restrictions independently from account registration. Implement in-app support, email ticketing, moderation queues, automated screening, human review, appeals and incident escalation workflows. Establish an India-based support team with rotational scheduling, four-hour initial response targets and escalation procedures to RePass Cloud Pty Ltd. Introduce operational monitoring, support metrics and staffing reviews to support the transition from business-hours beta operations to 24/7 public launch coverage.

### Q08 — Branding, publishing, subscription tiers and pricing

Status: ANSWERED, owner 23 October 2026. Publishing clearance and operational launch checks remain outstanding.

Decision: Swoovr is the confirmed product and engineering name. The application will be published on iOS and Android under RePass Cloud Pty Ltd. Swoovr will offer Free, Plus and Premium membership tiers, with Free access during private beta and paid subscriptions introduced at public launch. Australian and international trademark protection will be pursued.

Rules:

1. **Product identity:** Swoovr is the approved product name across the application, documentation, branding, marketing and engineering identifiers. The naming conventions established in D30 remain authoritative.
2. **Business ownership:** Swoovr is owned and operated by RePass Cloud Pty Ltd, Australia.
3. **Domain ownership:** RePass Cloud Pty Ltd already controls `swoovr.com` and `swoovr.app`. The `.com` domain serves the corporate website, while `.app` hosts the member application.
4. **Trademark protection:** Australian and international trademark protection will be pursued. Registration and clearance activities are planned but have not yet been completed. Appropriate jurisdictions and registration classes will be determined during the trademark application process.
5. **Store publishing:** The iOS and Android applications will be published under RePass Cloud Pty Ltd using its authorised Apple Developer and Google Play developer accounts.
6. **Application identifiers:** The approved Swoovr naming conventions apply to namespaces, assemblies, application identifiers, browser assets, configuration and infrastructure. Final mobile application identifiers must be validated against signing and store requirements before publication.
7. **Beta subscriptions:** Only Swoovr Free will be generally available during private beta. Paid subscription purchases will not be enabled during this period.
8. **Public subscriptions:** Swoovr Free, Swoovr Plus and Swoovr Premium will be introduced as the public membership structure, with monthly and annual subscription options for paid memberships.
9. **Subscription currency:** Initial subscription prices are defined in Australian dollars (AUD). International pricing, currency conversion, regional pricing and applicable tax treatment will be determined separately.
10. **Subscription management:** Members may upgrade, downgrade or cancel their subscriptions according to the applicable billing and platform policies. Subscription entitlements must be determined server-side.
11. **Feature entitlements:** The membership feature matrix below establishes the approved feature availability by tier. Detailed quotas, restrictions, behavioural definitions and implementation rules will be maintained in a separate subscription specification.
12. **Safety and verification:** Essential safety features, reporting, blocking and Photo/ID verification must remain available to all members regardless of subscription tier. Paid subscriptions must not be required to access essential safety functionality.
13. **Founding memberships:** The previously approved founding membership programme remains unchanged, with 1,000 available founding positions.
14. **Founder eligibility:** Eligible early adopters must complete the required account setup and Photo and/or ID verification before their founding membership benefits are activated.
15. **Founder recognition:** Activated founding members receive a permanent gold Swoovr Founder badge. Founder status does not require continuous paid subscription.
16. **Founder subscription benefit:** Activated founders receive three months of Swoovr Plus at no charge, followed by eligibility for a 50% discount on Plus subscriptions for the subsequent 12 months.
17. **Founder billing:** Founder benefits do not require an upfront payment method. Members must explicitly opt in and provide a payment method to continue with a paid Plus subscription after their complimentary period.
18. **Founder discount:** The 50% discount applies to either monthly or annual Swoovr Plus subscriptions during the approved 12-month discount eligibility period, subject to the detailed billing rules in the subscription specification.
19. **Payment processing:** Subscription billing must comply with the applicable Apple App Store, Google Play and web payment requirements. The backend must maintain authoritative membership and entitlement state across supported platforms.
20. **Publishing approval:** Public publication must not proceed until relevant brand clearance, store registration, signing, privacy, security, payment and operational requirements have been satisfactorily validated.

#### Approved subscription pricing

| Membership | Monthly (AUD) | Annual (AUD) |
| --- | ---: | ---: |
| Swoovr Free | $0.00 | $0.00 |
| Swoovr Plus | $7.99 | $79.99 |
| Swoovr Premium | $14.99 | $149.99 |

Pricing rules:

1. **Swoovr Free:** No subscription charge.
2. **Swoovr Plus:** $7.99 per month or $79.99 per year.
3. **Swoovr Premium:** $14.99 per month or $149.99 per year.
4. **Annual billing:** Annual subscriptions are charged upfront for the applicable annual billing period.
5. **Regional pricing:** The prices above are the initial Australian prices. International pricing and store-specific price schedules must be explicitly configured before international paid subscriptions are offered.
6. **Price changes:** Subscription prices must be configurable without application code changes, subject to the payment provider's subscription and price-change requirements.
7. **Payment terms:** Renewal terms, cancellation conditions, billing intervals and applicable charges must be communicated before subscription confirmation.

#### Approved membership feature matrix

| Feature | Free | Plus | Premium |
| --- | :---: | :---: | :---: |
| Create profiles across all modes | ✓ | ✓ | ✓ |
| Photo and ID verification | ✓ | ✓ | ✓ |
| Basic compatibility matching | ✓ | ✓ | ✓ |
| Essential safety and reporting | ✓ | ✓ | ✓ |
| Browse and discover people | ✓ | ✓ | ✓ |
| Messaging and connections | ✓ | ✓ | ✓ |
| Standard search filters | ✓ | ✓ | ✓ |
| Expanded search filters | — | ✓ | ✓ |
| Detailed compatibility breakdown | — | ✓ | ✓ |
| Basic travel discovery | ✓ | ✓ | ✓ |
| Extended travel discovery | — | ✓ | ✓ |
| Incognito browsing | — | ✓ | ✓ |
| One private photo album | ✓ | ✓ | ✓ |
| Additional private photo albums | — | ✓ | ✓ |
| Advanced visibility controls | — | ✓ | ✓ |
| Detailed compatibility insights | — | — | ✓ |
| Additional profile customisation | — | — | ✓ |

Feature rules:

1. **Tier inheritance:** Higher membership tiers include the functionality of lower tiers unless a feature is explicitly defined otherwise.
2. **Core functionality:** Free members retain access to the core Swoovr experience, including profiles, discovery, basic compatibility, messaging, verification and essential safety features.
3. **Plus functionality:** Plus members receive expanded discovery, compatibility, privacy, visibility and album functionality.
4. **Premium functionality:** Premium members receive all Plus features together with additional compatibility insights and profile customisation.
5. **Private albums:** Free members may create one private photo album. Plus and Premium members may create additional albums, with exact allowances defined separately. *(Superseded by D338, owner 6 November 2026: four albums for every plan, ten photos each.)*
6. **Compatibility:** Basic compatibility matching is available to all members. Plus provides detailed compatibility breakdowns, while Premium provides further compatibility insights. The specific calculations and presentation differences must be defined in the compatibility specification.
7. **Travel discovery:** Basic travel discovery is available to all members, while extended travel discovery requires Plus or Premium. Applicable privacy and location restrictions remain mandatory.
8. **Incognito browsing:** Available to Plus and Premium members, subject to member privacy preferences and existing restrictions on interactions with incognito users.
9. **Visibility controls:** Standard privacy and safety controls remain available to all members. Advanced visibility options are available to Plus and Premium.
10. **Feature configuration:** Feature availability, limits and entitlements must be centrally managed and enforced through the API rather than hardcoded into individual client applications.

#### Founding membership programme

Status: ANSWERED, owner 23 October 2026 (existing programme retained).

Rules:

1. **Capacity:** 1,000 founding memberships.
2. **Qualification:** Eligible early adopters who complete the required onboarding and verification conditions.
3. **Recognition:** Permanent gold Swoovr Founder badge.
4. **Complimentary subscription:** Three months of Swoovr Plus at no charge after founder activation.
5. **Discount:** 50% off Swoovr Plus for the following 12 months, available for monthly or annual subscription options.
6. **Payment method:** No payment method required to activate the complimentary period.
7. **Paid conversion:** Members must explicitly opt in to a paid subscription; complimentary memberships must not automatically convert into paid subscriptions.
8. **Founder permanence:** Founder recognition remains permanent even if the member cancels or does not maintain a paid subscription.
9. **Account recovery:** Founder status may be recovered following account deletion and re-registration using the previously approved secure recovery-token mechanism, subject to account verification and recovery policies.

#### Founder recovery token

Status: CONFIRMED design, owner 23 October 2026 (D65), with the owner's clarification and review answers of the same day. Implemented locally on 23 October 2026 (D79–D81, admin-granted until verification exists); follow-ups in [issue #26](https://github.com/repasscloud/wanderconnect/issues/26). Privacy follow-up: [issue #22](https://github.com/repasscloud/wanderconnect/issues/22).

Founding status survives account deletion through a token that Swoovr stores and the member keeps, so Swoovr need not keep the deleted profile.

1. **Founding membership is activated.** The member completes registration and either identity or photo verification, and qualifies for one of the 1,000 founding positions.
2. **A token is issued automatically.** Background logic calculates an HMAC-SHA256 token from the member's normalised, verified email address and a server-side secret when founder status is reached, and stores it in the founder-token table. The table stores the token and its status, not the email address.
3. **The member receives the token by email** when they reach founder status, and again when they ask to delete their account (part of offboarding, sent before the email address is deleted). They are told to keep it, because it is needed to reclaim founding status.
4. **The member deletes their account.** Swoovr deletes their personal information, including the email address, subject to any independently applicable legal retention requirements. The founder-token row stays.
5. **The member returns,** creates a new account, verifies their email address, and enters the token on the in-app **coupon code** screen.
6. **Swoovr checks the token.** It must exist in the founder-token table and be valid (not deleted or invalidated), **and** the HMAC of the account's current verified email address must equal it, compared in constant time. The email cannot be read back out of the token; the check recalculates the HMAC from the current email. A token sold or passed to someone else therefore fails, because their email produces a different value.
7. **Founding status is restored** on the new account.

Example (owner's sketch, not production code):

```csharp
using System.Security.Cryptography;
using System.Text;

public static class FoundingTokenService
{
    public static string Generate(
        string email,
        byte[] secret)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var payload = Encoding.UTF8.GetBytes(
            $"swoovr:founder:v1:{normalizedEmail}");

        var hash = HMACSHA256.HashData(secret, payload);

        return Convert.ToHexString(hash);
    }

    public static bool Validate(
        string email,
        string suppliedToken,
        byte[] secret)
    {
        var expected = Generate(email, secret);

        if (!Convert.TryFromHexString(
            suppliedToken, new byte[32], out var written)
            || written != 32)
        {
            return false;
        }

        var actual = Convert.FromHexString(suppliedToken);
        var expectedBytes = Convert.FromHexString(expected);

        return CryptographicOperations.FixedTimeEquals(
            actual, expectedBytes);
    }
}
```

Properties and rules:

- **The token record exists.** Admins can delete a founder token or set it to invalid in its table; a deleted or invalid token never restores status.
- **A token can be used more than once,** across later deletions and returns, as long as it stays valid and the email matches. It is re-sent at each deletion request. Because an email address belongs to one account at a time, only one live account can hold a given founder status.
- A match shows the token was issued for that verified email address. It proves founding membership only because tokens are issued solely by the founder logic.
- If the HMAC secret is compromised, founding tokens could be forged for any email address; admins can invalidate affected tokens.
- Restoring founding status does not restart the three-month complimentary Plus period or the 12-month discount; those apply once, when founding status first starts.
- **Restoring uses the existing token row, so it never takes a new one of the 1,000 places.**
- Founding membership is a permanent recognition entitlement, separate from the subscription.

Owner answers to the review notes, 23 October 2026:

1. **Email changes.** When a founder changes their email address, Swoovr issues a new token for the new verified address and invalidates the old one. Only accounts with founder status get a replacement.
2. **Privacy of the retained row.** Follow-up tracked in [issue #22](https://github.com/repasscloud/wanderconnect/issues/22): mention the retained founder token in the privacy notice and deletion confirmation, and offer to delete it.
3. **Secret versions.** Each token records the secret version that made it, so the secret can be rotated without breaking stored tokens; old secrets stay available, server-side only, for checking.
4. **Coupon screen.** Founder tokens carry a fixed prefix that identifies them, so the server routes them to the founder check, separate from ordinary coupons. DEFAULT format: `FOUNDER-V1-` followed by the 64-character hex HMAC, where `V1` is the secret version (item 3). Rate-limit and audit attempts per account.

Still to do in the implementation (not owner decisions): keep secrets in a managed key store, never in configuration files; normalise the email (trim + lower-case invariant) identically when issuing and checking; and in the sketch, `Validate` decodes the supplied hex twice, where one `Convert.TryFromHexString` into a 32-byte buffer is enough.

#### Outstanding publishing and implementation checks

These items are not approvals or claims of completed clearance.

1. **Trademark:** Conduct appropriate trademark searches and pursue Australian and selected international registrations.
2. **Brand availability:** Confirm applicable trademark and trade-name conflicts before public commercial release.
3. **Apple App Store:** Confirm developer account, application identifier availability, signing, certificates and store submission requirements.
4. **Google Play:** Confirm developer account, package identifier availability, signing and store submission requirements.
5. **Store compliance:** Validate subscription purchase flows, payment policies, account deletion, privacy disclosures and relevant store review guidelines.
6. **Tax and international pricing:** Confirm GST treatment, international pricing, currencies and any platform-specific price differences.
7. **Subscription specification:** Finalise per-tier quotas, behavioural restrictions, feature definitions, renewal handling, upgrades, downgrades and entitlement lifecycle.
8. **Founder billing:** Finalise the precise discount application and expiry rules, particularly for annual subscriptions spanning the end of the 12-month discount period.
9. **Production readiness:** Complete the security, privacy, operational and publishing checks required before enabling public accounts and paid transactions.

Rationale: Establishes one consistent Swoovr product identity while retaining the approved engineering conventions. The subscription structure provides a functional free experience, with Plus and Premium offering additional discovery, compatibility, privacy and customisation capabilities. Delaying paid subscription availability until public launch allows beta testing to focus on core functionality, stability, security and user experience. The founding membership programme rewards early adoption without requiring automatic paid conversion or continuous subscription.

Implementation: Apply the confirmed Swoovr naming conventions throughout the product and engineering repositories. Maintain separate Astro corporate and Blazor application deployments for `swoovr.com` and `swoovr.app`. Build a configurable subscription and entitlement system supporting Free, Plus and Premium, with per-feature permissions and quotas enforced server-side. Integrate subscription purchases and lifecycle events with supported billing providers and applicable mobile platform requirements. Implement founding membership allocation, verification-based activation, complimentary Plus entitlements, discount eligibility and permanent badge recognition independently of billing state. Complete trademark, store, security, privacy and payment compliance checks before authorising public launch.

### Q09 — Mobile toolchain, testing and release pipeline

Status: ANSWERED, owner 23 October 2026. Toolchain installation and compatibility validation remain outstanding.

Decision: Swoovr will use .NET 10 and .NET MAUI to develop native iOS and Android applications. Development, building, debugging and testing will be performed locally on macOS. CI/CD will be used for release publishing to the Apple App Store and Google Play, rather than routine development builds.

Rules:

1. **Development environment:** Primary development will take place on an Apple Silicon MacBook Pro (M2 Pro) running macOS.
2. **Operating system:** Use the latest stable macOS release supported by the required development toolchain. The currently installed version is macOS 27.0.1 (build 26A434).
3. **Xcode:** Use the latest stable Xcode version compatible with the selected .NET MAUI workloads. Xcode 27.1 (build 27A9275) is currently installed.
4. **.NET SDK:** Target .NET 10.x, using supported stable SDK and runtime releases. The currently installed SDK is 10.0.400, with workload set 10.0.400.1.
5. **SDK version management:** Use the repository's `global.json` to control SDK selection and a versioned workload set to maintain reproducible builds. Updates must be validated before changing the approved toolchain versions.
6. **.NET MAUI workloads:** Install the required iOS and Android workloads. Only install additional workloads when required by a supported target platform.
7. **iOS target:** Develop and test against current supported iOS versions, prioritising the latest stable iOS release while maintaining compatibility with the application's configured minimum supported iOS version.
8. **Android target:** Install the Android SDK, required build tools, compatible Java Development Kit and Android emulator tooling as necessary for .NET MAUI development.
9. **Android compatibility:** Select Android target SDK and minimum supported API levels according to current .NET MAUI compatibility requirements and Google Play publishing policies.
10. **Physical device testing:** Use available physical Android devices and compatible iOS devices where available. Physical-device testing will supplement, rather than replace, emulator and simulator testing.
11. **Simulators and emulators:** Use the Xcode iOS Simulator and Android emulators for routine development, UI validation and regression testing.
12. **Local development:** Application development, compilation, debugging, testing and development builds will be performed locally.
13. **CI/CD scope:** Automated CI/CD workflows will primarily support approved release builds, signing, packaging and publishing to the relevant application stores.
14. **Release publishing:** Publishing workflows must be triggered explicitly as part of an authorised release process. Routine commits and pull requests must not automatically publish applications.
15. **Application signing:** iOS and Android release builds must use approved signing credentials, certificates, provisioning profiles and platform-specific application identifiers.
16. **Release validation:** Applications must pass required functionality, compatibility, security, privacy and device acceptance testing before release publishing is authorised.
17. **Toolchain updates:** macOS, Xcode, .NET, MAUI and Android tooling may be updated as stable releases become available, provided compatibility is verified before adoption.
18. **Platform scope:** The initial mobile release targets iOS and Android simultaneously. Windows and macOS desktop application releases are not included in the initial mobile delivery scope.

Rationale: A local-first development environment provides direct control over development tooling, debugging, physical devices and application builds. Standardising on .NET 10 and compatible MAUI workloads supports shared application architecture and cross-platform development. Restricting CI/CD publishing to approved releases avoids unnecessary build infrastructure and ensures application-store submissions remain deliberate and controlled.

Implementation: Configure the existing .NET 10 solution for iOS and Android MAUI targets. Install the required MAUI workloads, Android SDK, Java SDK and emulator dependencies. Validate Xcode compatibility and pin the supported SDK and workload versions. Configure local development, simulator execution and physical-device debugging. Establish repeatable build and test commands and release-only CI/CD workflows for signing, packaging and publishing to Apple App Store Connect and Google Play Console. Store release signing credentials securely and require explicit authorisation before production submission.

## Overnight build questions — 23 October 2026

Raised during the unattended build (PR #24). Each has a recommended default; none blocks the merged slices.

| ID | Status | Question | Earliest blocker | Recommended default |
| --- | --- | --- | --- | --- |
| Q13 | ANSWERED, owner 23 October 2026 → D89 | Which artwork should the app icon use? The iOS/Android icon is still the MAUI template (purple ".NET" square) | Any TestFlight or store build (D62 iOS beta) | Owner: the Swoovr bird in white on a black background. The owner also asked whether members could choose a different icon in Settings (answered in D89) |
| Q14 | ANSWERED, owner 23 October 2026 → D90 | Where do founding-member benefits (3 months of Plus, then 50% off) live before billing exists? | Founder activation after verification (Q04) | Owner: an entitlement record that sets flags or "instructions" on accounts, designed so later programmes and sales can apply to everyone or to selected members; the builder chooses the long-term design |
| Q15 | ANSWERED, owner 23 October 2026 → D91 | Should the swoovr.com/beta form enrol people automatically or feed a review queue? | Beta intake (issue #25) | Owner: a review queue in a Supabase database, owned by the separate swoovr.com project. Swoovr staff review it, then invite and onboard people through the Swoovr UI/API; joining the queue never guarantees access |

### Q12 — Connection modes, discovery and Travel state

Status: ANSWERED, owner 23 October 2026

Decision: Swoovr supports four independent connection modes: Dating, Friends, Activities and Business. Users may enable multiple connection modes simultaneously, with each mode controlling its own discovery preferences, filters, compatibility criteria and relevant profile information. Travel is not a connection mode; it is an independent, optional discovery state that allows users to become discoverable in other locations before arriving, across any combination of their enabled connection modes.

Rules:

1. **Connection modes:** The four connection modes are Dating, Friends, Activities and Business. These represent the types of connections users wish to establish and are independent of their geographical discovery state.
2. **Multiple active modes:** Users may enable any combination of connection modes simultaneously. Enabling or disabling one mode must not automatically affect the others.
3. **Mode-specific discovery:** Each connection mode maintains its own discovery preferences, filters, compatibility criteria and profile visibility rules. Discovery results must respect the selected mode and the preferences of both users.
4. **Mode selection:** The primary connection-mode segmented control displays Dating, Friends, Activities and Business. Travel must not appear as a fifth connection mode.
5. **Travel independence:** Travel is a separate discovery state that changes where a user may be discovered without changing their enabled connection modes, underlying profile, compatibility criteria or connection preferences.
6. **Remote discovery:** Users may configure a Travel destination and become discoverable to people in that location before physically arriving, subject to their enabled connection modes and discovery preferences.
7. **Advance scheduling:** Users may activate or schedule Travel for a single destination up to 14 days before their planned arrival. Travel must not provide unrestricted indefinite visibility in remote locations.
8. **Multi-city travel:** Users may configure multiple destinations as part of a multi-city itinerary. Subsequent destinations may extend beyond the standard 14-day advance scheduling restriction when they belong to the same itinerary.
9. **Travel itinerary:** Each Travel destination records its intended location, arrival date, departure date and applicable discovery period. Multi-city itineraries must support sequential destinations and independently managed itinerary entries.
10. **Multiple destinations:** A user may have multiple planned Travel destinations. Remote discovery must respect the configured itinerary and prevent unlimited or contradictory destination activation outside the supported Travel rules.
11. **Travel cancellation:** Users may disable Travel, cancel an itinerary or remove individual destinations at any time, including when their travel plans change or they decide not to travel.
12. **Arrival behaviour:** When a user arrives at a destination, discovery should transition to their actual location according to their location permissions and privacy settings. Travel must not require the user to remain artificially associated with a remote location after arrival.
13. **Departure behaviour:** When a configured Travel period expires, remote discoverability for that destination must end automatically unless another valid itinerary destination or discovery state applies.
14. **Travel visibility:** Profiles discovered through Travel must clearly indicate that the user is travelling or planning to visit, rather than implying that they are already physically present. Exact travel dates and precise locations must not be disclosed beyond the user's configured privacy permissions.
15. **Discovery eligibility:** Remote visibility through Travel does not override blocks, account restrictions, moderation controls, age requirements, verification requirements, connection-mode eligibility or other discovery safeguards.
16. **Single user identity:** All connection modes and Travel destinations operate against the same underlying user account and identity. Separate user accounts or duplicated profiles must not be created for individual modes or destinations.
17. **Data-driven configuration:** Connection modes must use stable internal identifiers independent of their display labels. Display names, ordering, availability and presentation must remain configurable without changing the underlying architecture.
18. **Travel interface:** Travel must be accessible independently of the connection-mode segmented control, through a dedicated Travel configuration interface supporting destination selection, itinerary management, scheduling and cancellation.

Rationale: Connection modes define why people want to connect, while Travel defines where they want to be discovered. Separating these concerns allows someone travelling to Sydney, Melbourne or overseas to continue seeking Dating, Friends, Activities and Business connections without changing their existing preferences. The 14-day advance limit prevents indefinite remote discovery, while multi-city itinerary support accommodates legitimate extended travel involving multiple destinations. This approach keeps connection intent, geographical discovery and travel planning independently configurable and extensible.

Implementation: Model connection modes using stable identifiers and per-user mode configuration, including mode-specific discovery preferences, filters, compatibility criteria and profile visibility. Implement Travel as a separate discovery state with itinerary and destination records containing location, arrival date, departure date, discovery activation and cancellation state. Enforce the standard 14-day advance limit server-side, with an explicit multi-city itinerary exception for subsequent destinations. Extend discovery queries to include eligible remote Travel destinations alongside actual-location discovery, applying the user's enabled connection modes and all existing privacy, eligibility and moderation constraints. Implement scheduled activation and expiration of Travel discovery periods, with reconciliation against actual location where permission is available. Keep the four-mode segmented control independent of Travel and provide a dedicated Travel management interface. Ensure itinerary validation, destination activation and discovery eligibility are enforced by application services rather than client-side UI rules.

## Mobile sign-in, icon and modes — 23 October 2026

The owner accepted the builder's defaults for slices F–I (D93–D104). Both were answered on 24 October 2026 (full answers below); the table keeps the original questions.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q16 | ANSWERED, owner 24 October 2026; D106 | Intention chips content (not the code) | Which intention chips does each mode offer (Dating, Friends, Activities, Business), and how many may a member pick? D92 gives only illustrations ("Something serious", "Casual") | Owner supplies a list per mode; it is loaded as `mode_intentions` rows. Keep the DEFAULT limit of five per mode (D105) |
| Q17 | ANSWERED, owner 24 October 2026; D107, D109–D111 | Travel slice | How is a Travel destination chosen and shown: a fixed list of cities, or free search with a geocoding provider (which one)? How precisely may others see it (city only, suburb)? Do destinations need the beta's activated markets (D62 rule 2)? | A fixed list of cities to start (Adelaide, Melbourne, Sydney and so on), shown to others as the city name only, no geocoding provider until needed |

### Q16 — Mode-specific intention chips and selection limits

Status: ANSWERED, owner 24 October 2026.

Decision: Swoovr will provide ten predefined intention chips for each of its four connection modes: Dating, Friends, Activities and Business. Members may select up to five intentions per mode. Intentions are independent across modes and communicate what a member is seeking within each connection context. Intention definitions must be data-driven, stored in the `mode_intentions` table and associated with stable mode identifiers.

Rules:

1. Connection modes: Intention chips are available for Dating, Friends, Activities and Business, with ten predefined intentions per mode.
2. Selection limit: Members may select a maximum of five intention chips per mode, consistent with D105. The limit applies independently to each mode.
3. Multiple intentions: Members may select multiple intentions within the same mode, including intentions that appear contradictory but reflect genuine flexibility. For example, someone may be interested in both long-term relationships and casual dating.
4. Mode independence: Selecting intentions in one mode must not automatically add, remove or modify intentions in another mode.
5. Intention persistence: Selected intentions must remain saved when a member temporarily disables a connection mode. Disabling a mode removes its eligibility for discovery but does not delete its configuration.
6. Dating intentions: Dating intentions describe romantic, relationship and intimacy goals. They must remain separate from gender, sexual orientation, relationship status, physical attributes and compatibility preferences.
7. Friends intentions: Friends intentions describe platonic friendships and social connections, including networking, group activities, emotional support and travel companionship.
8. Activities intentions: Activities intentions describe how members wish to participate in experiences rather than functioning as a catalogue of activity categories. Specific interests such as cinema, hiking, dining and concerts must be maintained separately.
9. Business intentions: Business intentions support professional relationships involving networking, partnerships, customers, mentorship, career opportunities and collaboration. Members must not be required to own or represent a business to participate.
10. Discovery eligibility: Intention selections may inform compatibility, ranking and discovery preferences but must not automatically exclude members unless an explicit discovery rule or user-selected filter requires exclusion.
11. Intention compatibility: Different intentions may have different degrees of compatibility. The matching system should support evaluating intention alignment without assuming all different intentions are incompatible.
12. Data-driven configuration: Intention definitions must be stored as data rather than hardcoded into UI components or application logic.
13. Stable identifiers: Each intention must have a stable internal identifier independent of its displayed label, allowing future wording changes without breaking existing selections.
14. Presentation: Intention chips must support configurable labels, display ordering and active status.
15. Validation: The API must enforce valid intention identifiers, mode associations and the maximum of five selections per mode. Client-side validation alone is insufficient.
16. Future changes: Administrators may introduce, deactivate or rename intentions without requiring database schema changes. Existing selections referencing retired intentions must be handled without corrupting user profiles.
17. Cross-platform consistency: The same intention definitions and selection rules must apply to the Blazor web application and .NET MAUI iOS and Android applications.
18. Travel independence: Travel remains an independent discovery state under Q12. Travel destinations must not change a member's selected intentions within Dating, Friends, Activities or Business.

#### Approved intention definitions

Dating — 10 intentions

| Order | Intention                           |
| ----- | ----------------------------------- |
| 1     | Long-term relationship              |
| 2     | Short-term dating                   |
| 3     | Casual dating                       |
| 4     | Something casual / no strings       |
| 5     | Friends with benefits               |
| 6     | Open to exploring                   |
| 7     | Marriage / life partner             |
| 8     | Intimacy / physical connection      |
| 9     | Not sure yet                        |
| 10    | New connections / see where it goes |

Friends — 10 intentions

| Order | Intention                              |
| ----- | -------------------------------------- |
| 1     | New friendships                        |
| 2     | Close / meaningful friendships         |
| 3     | Casual friendships                     |
| 4     | Social groups / meetups                |
| 5     | Activity partners                      |
| 6     | Networking / meeting people            |
| 7     | Emotional support / someone to talk to |
| 8     | Travel companions                      |
| 9     | Local connections                      |
| 10    | Expanding my social circle             |

Activities — 10 intentions

| Order | Intention                       |
| ----- | ------------------------------- |
| 1     | Finding activity partners       |
| 2     | Group outings                   |
| 3     | Exploring local events          |
| 4     | Trying new experiences          |
| 5     | Fitness and outdoor activities  |
| 6     | Food and dining experiences     |
| 7     | Arts, culture and entertainment |
| 8     | Weekend adventures              |
| 9     | Travel and sightseeing          |
| 10    | Spontaneous plans               |

Business — 10 intentions

| Order | Intention                   |
| ----- | --------------------------- |
| 1     | Professional networking     |
| 2     | Business partnerships       |
| 3     | Finding clients / customers |
| 4     | Finding service providers   |
| 5     | Mentorship / guidance       |
| 6     | Career opportunities        |
| 7     | Collaboration / projects    |
| 8     | Entrepreneurship / startups |
| 9     | Industry events / meetups   |
| 10    | Knowledge sharing           |

Rationale: Intention chips allow members to communicate their goals without forcing them into a single predefined relationship or connection type. Providing ten choices per mode offers meaningful variety while the five-selection limit encourages members to identify their priorities. Maintaining separate intention definitions for Dating, Friends, Activities and Business supports Swoovr's contextual compatibility model and prevents unrelated intentions from affecting each other. Data-driven configuration allows the platform to evolve its terminology, discovery and compatibility rules without requiring changes to application architecture.

Implementation: Populate the `mode_intentions` table with the forty approved definitions, assigning each a stable identifier, connection-mode identifier, display order and active status. Store member selections in an appropriate relational association table with uniqueness constraints. Enforce the five-selection limit per mode through application services and API validation, with additional database protections where appropriate. Provide endpoints for retrieving available intentions and updating member selections. Implement reusable chip-selection components in Blazor and .NET MAUI, displaying available options and the current selection count. Preserve selections when modes are disabled. Expose selected intentions to the discovery and contextual compatibility services without treating intention differences as automatic exclusion criteria. Keep activity categories, profile attributes and Travel configuration separate from intention definitions.

### Q17 — Travel destinations, visibility, discovery and connection permissions

Status: ANSWERED, owner 24 October 2026.

Decision: Swoovr will support searchable worldwide Travel destinations using its locally maintained GeoNames database, without requiring an external geocoding provider. Members may save unlimited trips, each containing a destination city, mandatory arrival and departure dates, and an independent discovery-visibility setting. Eligible destination-local members may discover travellers and initiate connection requests, while travellers remain independently discoverable in their home market. Travel discovery is intentionally asymmetric: travellers do not automatically receive destination-local profiles through normal discovery. Travellers may explicitly search for destination-local people only after establishing a qualifying Travel intention for that destination; the destination-search feature itself remains outside the Q17 implementation scope.

Rules:

1. Destination source: Travel destinations must be selected from Swoovr's locally maintained GeoNames database, supporting worldwide cities rather than a restricted or hardcoded list.
2. Destination search: Members must be able to search for cities by name using an autocomplete interface. Results should include the city, state or region where applicable, and country to distinguish locations with identical names.
3. Geographical identifiers: Each destination must reference its GeoNames identifier. Destination names must not be used as unique database keys.
4. External services: No external geocoding provider is required for the initial implementation. Destination searching and resolution must use Swoovr's internal geographical dataset.
5. Worldwide selection: Members may select any supported worldwide GeoNames city, including cities where Swoovr has not officially launched.
6. Activated markets: Selecting a destination does not override existing activated-market restrictions under D62, rule 2. Travel discovery must respect the market's eligibility and activation requirements.
7. Travel intention: A Travel intention is a destination-specific travel record containing the intended city, arrival date and departure date. It is independent of the four connection-mode intention-chip sets defined in Q16.
8. Mandatory dates: Every Travel destination must include both an arrival date and a departure date. Open-ended trips without a departure date are not permitted.
9. Date validation: Arrival must not occur after departure. Dates must be validated by application services and interpreted using the destination's local timezone.
10. Advance discovery: A traveller may become discoverable in a destination up to 14 days before their planned arrival. Travel visibility must not begin earlier than the applicable eligibility window.
11. Multi-city itineraries: Members may configure multiple destinations, including sequential cities within the same itinerary. Previously approved Q12 rules concerning subsequent itinerary destinations and their scheduling exceptions remain applicable.
12. Unlimited saved trips: Free, Plus and Premium members may save unlimited Travel destinations and trips. No membership-tier restrictions apply to the number of saved trips.
13. Repeat destinations: Members may save multiple trips to the same city when they represent different travel periods.
14. Independent trip configuration: Each Travel destination must maintain its own arrival date, departure date, visibility setting and lifecycle state.
15. Independent visibility toggle: Every destination must provide an independent visibility control. Disabling visibility must preserve the destination and its dates rather than deleting the trip.
16. Visibility reactivation: Members may re-enable visibility for an existing trip, provided its dates, market eligibility, account permissions and other applicable discovery conditions remain valid.
17. Automatic expiration: Destination discovery eligibility must end automatically after the departure date, based on the destination's local timezone.
18. Travel history: Expired trips may remain available in the member's private Travel history but must not continue to generate remote discovery eligibility.
19. Trip cancellation: Members may edit, disable, cancel or remove individual Travel destinations without affecting their other configured trips.
20. Simultaneous destinations: Multiple eligible Travel destinations may be visible simultaneously, including destinations within a multi-city itinerary, subject to their individual scheduling and activation rules.
21. Home discovery: Travelling members must retain normal discovery visibility in their home city while Travel is active. Enabling Travel must not automatically disable or replace home-market discovery.
22. Destination discovery: Eligible local members in a destination city may discover travellers who have an active and visible Travel intention for that destination.
23. Asymmetric discovery: Travel discovery is intentionally one-directional. A traveller becoming visible in a destination must not automatically gain access to destination-local profiles through normal discovery.
24. Traveller identification: Members appearing through Travel discovery must be explicitly identified as travelling or planning to visit. The application must not present them as local residents of the destination.
25. Public destination visibility: Other members may see the destination city and country only. Destination suburbs, precise coordinates and exact geographical distances must not be disclosed.
26. Public travel dates: Eligible destination-local members may see the traveller's exact arrival and departure dates when the destination's visibility setting is enabled and the trip is eligible for discovery.
27. Date presentation: Travel dates must be formatted according to the viewer's locale, while preserving the destination-local calendar dates entered by the traveller.
28. Hidden destinations: Destinations with visibility disabled must not appear in destination discovery or disclose their intended location or travel dates through public-facing profile data.
29. Connection initiation: Eligible destination-local members may initiate connection requests with travellers discovered in their city, subject to existing connection, safety and moderation rules.
30. Connection acceptance: The travelling member may accept or decline an incoming connection request from a destination-local member.
31. No automatic connections: Destination visibility and incoming connection requests must not automatically establish a connection. The traveller must explicitly accept the request.
32. Messaging permissions: Once a connection has been accepted, messaging and communication must follow the existing Swoovr connection permissions and membership rules.
33. Destination-search prerequisite: A traveller may only search for local people in a destination city when they have first established a qualifying Travel intention for that destination.
34. Travel intention validation: Destination search eligibility must reference a valid Travel intention containing the destination GeoNames ID and mandatory arrival and departure dates. A member must not gain destination-search eligibility merely by entering an arbitrary city in a search field.
35. Separate search permissions: Creating a Travel intention establishes potential eligibility for destination search but must not automatically insert destination-local profiles into the traveller's normal discovery feed.
36. Destination-search scope: The functionality allowing travellers to actively search for destination-local members is deferred. Q17 must document and preserve its Travel-intention prerequisite but must not implement the destination-search interface or broader search behaviour.
37. Connection-mode independence: Travel operates across the member's enabled Dating, Friends, Activities and Business modes. Travel must not introduce a fifth connection mode or modify existing mode-specific intentions.
38. Mode-specific eligibility: Destination discovery and connection requests must respect applicable connection-mode preferences, eligibility conditions, compatibility requirements and visibility restrictions.
39. Safety and moderation: Travel must not bypass blocks, account suspensions, moderation controls, age restrictions, identity-verification requirements or other existing discovery safeguards.
40. Server-side enforcement: Travel eligibility, discovery direction, destination-search prerequisites, visibility settings, date windows and activated-market restrictions must be enforced by application services and APIs, not solely by client-side interface controls.
41. Cross-platform behaviour: The same Travel configuration and discovery rules must apply across Blazor web, .NET MAUI iOS and .NET MAUI Android applications.
42. Data protection: Destination coordinates may be retained internally where necessary for geographical resolution and market matching but must not be exposed through public APIs or discovery responses.
43. Performance and abuse prevention: Unlimited trip storage must be supported using appropriate database indexes, paginated retrieval, request validation and API rate limiting.
44. Single identity: Travel destinations and associated connections must reference the member's existing Swoovr account. Separate accounts or duplicated member profiles must not be created for different destinations.

#### Approved Travel discovery behaviour

| Scenario                                     | Behaviour                                                                                               |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Member is at home                            | Normal home-market discovery applies                                                                    |
| Member creates a future trip                 | Trip is saved with destination and dates                                                                |
| Trip is more than 14 days away               | Destination discovery is not yet eligible, except applicable Q12 itinerary rules                        |
| Trip enters the permitted visibility window  | Destination locals can discover the traveller if visibility is enabled and other eligibility rules pass |
| Member remains in their home city            | Home discovery continues independently                                                                  |
| Destination local views traveller            | Traveller is identified as visiting, with destination city, country and exact travel dates              |
| Destination local requests connection        | Traveller receives an incoming request                                                                  |
| Traveller accepts request                    | Normal connection and messaging rules apply                                                             |
| Traveller declines request                   | No connection is established                                                                            |
| Traveller opens normal discovery             | Home-market discovery applies; destination locals are not automatically included                        |
| Traveller wants to search destination locals | Requires a qualifying Travel intention; separate search functionality is deferred                       |
| Traveller disables trip visibility           | Destination discovery stops; trip details are preserved                                                 |
| Traveller departs or trip expires            | Destination discovery eligibility ends automatically                                                    |
| Member has multiple trips                    | Each trip is evaluated independently                                                                    |
| Destination market is not activated          | Trip may be saved, but discovery remains subject to D62                                                 |

Rationale: Travel is designed to allow local residents to discover people planning to visit their city and initiate connections before or during their stay. This differs from conventional location-spoofing systems, where travellers can immediately browse residents of any selected destination. Asymmetric discovery gives destination locals control over whether to approach a visitor, while allowing travellers to accept relevant connections without being presented with an unsolicited remote discovery feed. Requiring an established Travel intention before destination-local search prevents arbitrary worldwide searching without a declared travel context. GeoNames provides a self-managed worldwide city database without dependence on external geocoding services. City-level public visibility, mandatory travel dates, independent visibility controls and existing activated-market restrictions provide predictable discovery behaviour while preserving location privacy. Unlimited saved trips support frequent travellers and multi-city itineraries without introducing artificial subscription limits.

Implementation: Implement Travel using dedicated destination and itinerary persistence associated with the existing member account, referencing the internal GeoNames city dataset through stable GeoNames identifiers. Each trip must store its destination, arrival date, departure date, visibility preference and lifecycle state. Support unlimited saved trips, repeat visits and independently managed itinerary entries. Implement searchable city autocomplete backed by the local GeoNames database and return distinguishable city, region and country labels.

Validate mandatory dates, itinerary associations and Travel discovery windows server-side. Apply the standard 14-day advance-discovery rule and preserve the approved Q12 multi-city itinerary exception. Calculate eligibility using destination-local dates and timezone rules. Support independent trip visibility toggles, immediate suppression of hidden trips and automatic removal of expired trips from remote discovery eligibility.

Extend discovery services to evaluate home-market and destination-market visibility independently. A travelling member must remain eligible for normal home discovery while simultaneously appearing in destination discovery when their Travel configuration permits it. Destination discovery must be asymmetric: eligible destination locals may discover and request connections with travellers, but travellers must not automatically receive destination-local profiles through ordinary discovery. Reuse existing connection request, acceptance, rejection, blocking and messaging infrastructure rather than implementing a separate Travel connection system.

Expose only the destination city, country, relevant travel dates and traveller status in authorised destination-discovery responses. Do not return precise destination coordinates, suburbs or distance information through public profile or discovery DTOs. Apply existing connection-mode eligibility, moderation restrictions, account verification, market activation and privacy controls before returning results.

Document the destination-search prerequisite as a separate future capability: a member must possess a qualifying Travel intention for the requested GeoNames destination before destination-local search is permitted. Preserve this requirement in the application specification and future API design, but do not implement traveller-initiated destination search as part of Q17.

Maintain cross-platform consistency between the Blazor web application and .NET MAUI clients. Add automated tests covering date validation, advance visibility windows, multi-city itineraries, unlimited trip storage, home and destination discovery independence, asymmetric discovery permissions, connection initiation and acceptance, destination visibility toggles, expiration, activated-market restrictions and prevention of unauthorised destination-local discovery.

### Q11 — Profile flags, verification badges, founding membership, staff affiliation and official bots

Status: ANSWERED, owner 24 October 2026. Supersedes earlier proposals concerning separate public staff accounts, staff administrative privileges and public Admin Verified badges.

Decision: Swoovr will maintain independent profile flags, verification credentials, membership recognition badges, employment-affiliation badges and official bot classifications. Public badges must accurately communicate what has been verified or recognised without implying additional assurances or privileges. Photo Verified and ID Verified are separate credentials. Admin Verified represents an internal enhanced manual trust review with no public badge and remains valid until revoked. Founding members receive permanent gold recognition after satisfying the approved founding-programme requirements. The ruby Swoovr Staff bird identifies an ordinary member whose current employment with Swoovr has been confirmed; it does not represent administrative authority, system access or a privileged account type. Staff-affiliated members are held to higher standards of conduct, and every report against them requires human review. Official bots are separately classified, internally authorised, publicly identified and restricted to member-initiated interactions. Members may display multiple applicable badges simultaneously.

Rules:

1. **Independent profile flags:** Profile verification, account classification, membership recognition, employment affiliation, administrative trust review and system permissions must be represented as distinct concepts. One status must not automatically imply another.

2. **Account registration:** A registered account does not automatically receive any public verification badge. Registration establishes an account but does not prove identity, adulthood beyond applicable checks, ownership of profile photographs or trustworthiness.

3. **Email verification:** Email Verified is an internal account status confirming that the member has demonstrated control of their registered email address. It must not produce a public profile badge or be represented as identity verification.

4. **External authentication:** Signing in through Apple, Google or another approved identity provider does not automatically grant Photo Verified or ID Verified status. Authentication and identity assurance remain separate.

5. **Photo Verified:** The Photo Verified badge confirms that Swoovr has successfully compared the member's relevant profile photographs against a live verification selfie or equivalent approved photo-verification evidence.

6. **Photo verification process:** Photo verification may use automated image matching, liveness checks and other approved verification methods. Failed, uncertain or inconclusive automated assessments must be eligible for human review rather than being treated as successful verification.

7. **Photo verification lifecycle:** Material changes to the photographs covered by an existing Photo Verified credential must trigger re-verification where the previous verification no longer establishes that the displayed photographs belong to the member. The credential must support expiry, suspension, revocation and replacement.

8. **ID Verified:** The ID Verified badge confirms successful completion of Swoovr's approved identity-document verification process, including applicable identity and liveness checks. It must remain independent of Photo Verified.

9. **ID verification limitations:** ID Verified must not imply that publicly displayed profile photographs match the verified person, that the member has passed a criminal background check or that their future behaviour is guaranteed to be safe.

10. **Public anonymity:** A member may hold an ID Verified credential without displaying a recognisable public face photograph, subject to applicable profile and discovery rules. Identity assurance must not require unnecessary disclosure of the member's verified legal identity to other members.

11. **Independent verification credentials:** Photo Verified and ID Verified must be awarded, maintained, expired and revoked independently. Completing one verification process must not automatically grant the other badge.

12. **Verification timestamps:** Profiles must support displaying relevant verification recency information, including "Last identity checked" and "Last photo checked", where the corresponding credential is valid and public display is permitted.

13. **Verification revocation:** Public verification badges must cease appearing when their underlying credentials expire, are revoked or otherwise become invalid. A previously successful verification must not remain publicly represented as current after invalidation.

14. **Admin Verified definition:** Admin Verified represents an enhanced manual trust review performed by an authorised Swoovr administrator. It is an internal assessment of an account or relevant verification circumstances, not a public identity credential.

15. **Admin Verified visibility:** Admin Verified must never produce a public badge, icon, profile label or discovery indicator, consistent with D58. It must not be exposed through public profile or discovery APIs.

16. **Admin Verified independence:** Admin Verified does not automatically grant Photo Verified, ID Verified, Founding Member status, Staff affiliation, additional messaging permissions, discovery priority or subscription benefits.

17. **Admin Verified validity:** An approved Admin Verified status remains valid until explicitly revoked by an authorised administrator. It does not have a mandatory periodic expiry date.

18. **Admin Verified re-review:** New reports, suspicious activity, changes to verification evidence or other relevant circumstances may trigger another manual trust review. Re-review must not automatically invalidate other independent credentials.

19. **Admin Verified auditing:** Administrative trust reviews must record the reviewing administrator, review date, outcome, reason and relevant revocation or reassessment events. Access must be restricted to authorised administrative personnel.

20. **Founding-member programme:** Swoovr will offer 1,000 founding-member positions to qualifying early adopters under the previously approved founding-membership programme.

21. **Founding-member eligibility:** Members must complete the required profile setup and successfully complete ID verification and/or Photo verification before their founding membership and associated introductory benefits are activated. Registration or joining an invitation queue alone is insufficient.

22. **Founding-member badge:** Activated founding members receive the permanent gold Swoovr Founding Member badge. The badge represents membership recognition, not identity verification, employment, moderation authority or an enhanced safety guarantee.

23. **Founding-member benefits:** Eligible founding members receive three months of Swoovr Plus without requiring a payment method, followed by the opportunity to subscribe to Plus at a 50% discount for the subsequent twelve months, applicable to monthly or annual subscriptions. Paid continuation requires explicit member opt-in and must not begin automatically.

24. **Permanent founding recognition:** Once legitimately activated, Founding Member status remains permanent regardless of whether the member maintains a paid subscription. Subscription entitlements and permanent founder recognition must be stored and evaluated separately.

25. **Founding-member recovery:** The previously approved founder-recovery mechanism must support recovery of legitimate founding recognition following account deletion and re-registration. Recovery uses a securely generated HMAC-SHA256 token derived from the normalised verified email address and a server-held secret, with appropriate validation and anti-abuse controls. The recovery mechanism must not be treated as a replacement for account authentication or required verification.

26. **Founding-member entitlement separation:** Founder recognition must remain independent of subscription entitlements. The entitlement system must support future promotional programmes, grants, discounts and account-specific benefits without embedding all benefit logic directly into the badge system.

27. **Swoovr Staff badge:** The public Swoovr Staff badge is represented by the approved ruby-coloured Swoovr bird artwork. It identifies an ordinary member whose current employment affiliation with Swoovr has been confirmed.

28. **Staff affiliation meaning:** The Staff badge communicates confirmed employment affiliation only. It does not identify an administrator, moderator, support operator or someone with privileged access to Swoovr's internal systems.

29. **Normal employee accounts:** Employees using Swoovr for personal participation have ordinary member accounts. Their profiles, connections, conversations, intentions, privacy settings and membership features behave according to standard member rules.

30. **No mandatory separate public accounts:** Employees are not required to create separate official public-facing and personal member accounts merely because they work for Swoovr. The Staff badge is an affiliation credential attached to their normal member account.

31. **Administrative identity separation:** Employees who require administrative or operational system access must receive those permissions through Swoovr's separate authentication, authorisation and internal access-control mechanisms. Public Staff badges must never be used to determine administrative permissions.

32. **Staff discovery participation:** Staff-affiliated members may participate in all four connection modes: Dating, Friends, Activities and Business. They may enable any combination of modes and maintain ordinary mode-specific intentions and discovery preferences.

33. **Staff connection permissions:** Staff-affiliated members may initiate connection requests, accept or decline requests, and send and receive messages under the same social interaction rules applicable to other members.

34. **Mandatory staff identification:** When an account holds an active Staff affiliation credential, its ruby badge must be displayed on applicable discovery cards, public profiles and conversation identity surfaces. The member must not be able to conceal the badge while retaining active Staff affiliation.

35. **Independent staff verification:** Employment affiliation does not automatically grant Photo Verified or ID Verified. Staff-affiliated members must complete the relevant human verification processes to receive those badges.

36. **Staff and Admin Verified independence:** A member displaying the Staff badge is not automatically Admin Verified. A member who is Admin Verified is not automatically a Swoovr employee. Neither status grants the other.

37. **No elevated social privileges:** The Staff badge must not grant additional connection limits, messaging permissions, discovery reach, subscription privileges, access to private information or exemptions from standard platform rules.

38. **Higher staff conduct standards:** Staff-affiliated members are held to higher behavioural and conduct standards because their public affiliation represents Swoovr. Employment affiliation must increase accountability rather than reduce it.

39. **Mandatory staff report review:** Every report submitted against a staff-affiliated member must receive human review, regardless of the report category, initial automated risk score or apparent severity.

40. **Automated moderation of staff reports:** Automated systems may classify reports, preserve evidence, detect urgent threats and apply immediate protective restrictions where appropriate. Automated systems must not replace the mandatory human review or silently dismiss a report against a staff-affiliated member.

41. **Staff report escalation:** Reports against staff-affiliated members must be routed to appropriately authorised moderators or designated senior reviewers. The review process must provide conflict-of-interest protections, including preventing an employee from reviewing reports concerning their own account.

42. **Staff misconduct consequences:** Substantiated misconduct may result in warnings, restrictions, suspension, permanent account termination, removal of Staff affiliation or other proportionate enforcement actions. Serious matters may also be escalated to authorised Swoovr management for separate employment-related consideration.

43. **Staff report auditability:** Staff-related moderation reviews must record the report, reviewer, relevant evidence, assessment, outcome, enforcement action and escalation history. Reporting members must not receive confidential internal employment information.

44. **Staff affiliation lifecycle:** Staff affiliation must support issuance, verification, revocation and historical audit records. When a member ceases to be an employee, their Staff badge must be revoked without automatically deleting or disabling their ordinary member account.

45. **Official bot classification:** Official Swoovr bots must have a distinct automated-account classification. A bot is not a human member and must not be represented as one.

46. **Official bot authorisation:** Bot accounts must be created, approved and authorised through Swoovr's internal administrative systems. Unauthorised accounts must not be able to claim official bot status or display the official Bot badge.

47. **Official bot badge:** Every official bot must display the approved bot identification badge on discovery cards, profiles and relevant conversations. Bot identification must be mandatory and must not be removable by the bot or concealed through ordinary profile settings.

48. **Bot discovery participation:** Official bots may participate in normal discovery across Dating, Friends, Activities and Business, but their permitted modes must be configured independently for each bot account.

49. **Per-bot mode configuration:** Administrators may assign one, several or all four connection modes to an official bot. A bot must only appear in discovery for modes it has been explicitly authorised and enabled to participate in.

50. **Member-initiated bot interactions:** Official bots must not initiate connection requests or conversations with members. The member must initiate the interaction.

51. **Bot response permissions:** Once a member initiates contact, the bot may respond and continue the conversation within its authorised capabilities and applicable platform rules. Member initiation does not grant unlimited or unrestricted access to the member.

52. **No unsolicited bot messaging:** Official bots must not independently start social conversations, send unsolicited connection requests or bypass communication preferences using their official account classification.

53. **Bot verification exemption:** Official bots are exempt from human identity-document, selfie, liveness and photo-matching verification requirements.

54. **No human badges for bots:** Official bots must never receive Photo Verified or ID Verified badges. Their internal authorisation and official Bot badge provide an account-type assurance rather than a human identity assurance.

55. **Bot credentials and security:** Bot authentication credentials, operational permissions and account classification must be managed separately from human verification credentials and normal member sign-in processes.

56. **Bot blocking and reporting:** Members must be able to block and report official bots using applicable platform controls. Official status does not exempt bots from safety, content or moderation requirements.

57. **Multiple simultaneous badges:** A human member may display multiple applicable public badges simultaneously, including Photo Verified, ID Verified, Founding Member and Swoovr Staff. Each badge must retain its independent meaning.

58. **Badge combinations:** Badge combinations must be derived from independently valid credentials and account states. Possession of one badge must not cause unrelated badges to be issued automatically.

59. **Badge applicability:** Official bots may display their mandatory Bot badge and other explicitly applicable non-human account indicators, but must not receive human verification badges or human founding-membership entitlements.

60. **Badge presentation:** Public badges must use approved Swoovr artwork and consistent visual semantics. The ruby Staff badge, gold Founding Member badge, Photo Verified badge, ID Verified badge and Bot badge must remain visually distinguishable.

61. **Badge ordering:** Badge display order should be deterministic and configurable so that profiles with multiple badges render consistently across discovery cards, profiles and conversations.

62. **Badge explanations:** Public badges must provide accessible explanations of their meaning. Descriptions must not imply guarantees beyond the underlying verification or recognition process.

63. **Badge authenticity:** Public badge eligibility must be determined by trusted server-side account and credential records. Members must not be able to create, upload, impersonate or self-assign official badges through editable profile fields.

64. **Public and internal data separation:** Public profile responses must expose only the public badges and related approved metadata necessary for presentation. Internal Admin Verified status, employee access permissions, moderation investigations and confidential verification evidence must not be disclosed.

65. **Verification evidence protection:** Identity documents, verification selfies, biometric information and associated sensitive verification records must be protected using appropriate access controls, retention policies and secure storage practices. Public badge rendering must not require exposing underlying evidence.

66. **Badge status changes:** Revocation or invalidation of a credential must be reflected in subsequent profile, discovery and conversation responses without requiring the member to manually update their profile.

67. **Account lifecycle:** Badge eligibility and associated entitlements must account for account deactivation, deletion, suspension, reinstatement and legitimate recovery. Historical audit records must remain separate from currently active public badge states.

68. **Cross-platform consistency:** The same badge definitions, credential semantics, visibility restrictions and account classifications must apply to Swoovr's Blazor web application and .NET MAUI iOS and Android clients.

69. **Server-side enforcement:** Badge issuance, revocation, staff affiliation, bot authorisation, bot interaction restrictions, verification eligibility and enhanced staff moderation must be enforced by trusted application services and APIs rather than client-side presentation logic.

70. **Extensibility:** The profile flag and badge architecture must support additional credentials, membership recognition programmes, affiliations and account classifications without requiring unrelated verification concepts to be merged or existing badges to change meaning.

#### Approved public badges and internal states

| Badge / state | Public badge | Meaning | Assignment / validation |
| --- | --- | --- | --- |
| Account Registered | No | Member has created an account | Authentication and registration |
| Email Verified | No | Member controls their registered email address | Email verification |
| Photo Verified | Yes | Profile photographs have passed the approved photo-matching process | Automated verification and/or authorised human review |
| ID Verified | Yes | Member has passed the approved identity-document verification process | Identity verification provider and/or authorised review |
| Admin Verified | No | Enhanced manual trust review completed | Authorised administrator; valid until revoked |
| Founding Member | Gold badge | Permanent recognition as a qualifying Swoovr founder | Founding programme eligibility and activation |
| Swoovr Staff | Ruby bird | Confirmed current employment affiliation with Swoovr | Authorised employment-affiliation assignment |
| Official Bot | Bot badge | Internally authorised automated Swoovr account | Authorised bot provisioning and classification |

#### Badge combination examples

| Account | Permitted public badges |
| --- | --- |
| Ordinary unverified member | None |
| Photo-verified member | Photo Verified |
| ID-verified member | ID Verified |
| Member with both verifications | Photo Verified, ID Verified |
| Verified founding member | Founding Member and applicable verification badges |
| Staff-affiliated member | Swoovr Staff |
| Verified staff-affiliated member | Swoovr Staff, Photo Verified and/or ID Verified |
| Staff-affiliated founding member | Swoovr Staff, Founding Member and applicable verification badges |
| Admin-reviewed ordinary member | Only independently earned public badges; Admin Verified is hidden |
| Official Swoovr bot | Official Bot; no human verification badges |

#### Staff affiliation and administrative access separation

| Capability / attribute | Staff-affiliated member | Authorised internal administrator |
| --- | --- | --- |
| Ordinary Swoovr member account | Yes | Independent of administrative access |
| Ruby Swoovr Staff badge | Yes, while affiliation is valid | Not automatically |
| Dating, Friends, Activities, Business | All four modes | Based on their ordinary member account |
| Normal messaging and connections | Yes | Based on their ordinary member account |
| Internal administrative system access | No, not from badge | Only through explicitly assigned permissions |
| Access to member moderation tools | No, not from badge | Only if specifically authorised |
| Access to identity verification evidence | No, not from badge | Only if specifically authorised |
| Subject to normal reporting | Yes | Yes, for their ordinary member account |
| Mandatory human review when reported | Yes, while staff-affiliated | Not automatically unless separately staff-affiliated |
| Higher staff conduct standards | Yes | As applicable to their employment and operational responsibilities |
| Automatic Photo Verified / ID Verified | No | No |

Rationale: Swoovr's badge system must communicate specific, independently validated facts rather than creating a misleading overall trust score. Photo verification, identity verification, administrative trust review, permanent membership recognition, employment affiliation and bot authorisation establish different things and must remain distinguishable. Allowing multiple simultaneous badges gives members useful context without collapsing these meanings into a single verification status.

The Founding Member badge recognises qualifying early adopters and preserves their contribution to Swoovr independently of paid subscription status. The Staff badge identifies a normal member whose employment affiliation has been confirmed, without incorrectly suggesting administrative privileges or privileged system access. Because staff-affiliated members publicly represent the organisation, reports against them require mandatory human review and higher accountability. Official bot identification ensures automated accounts can participate transparently in discovery without impersonating human members or initiating unsolicited social interactions. Keeping the system data-driven and enforcing eligibility server-side supports consistent, secure operation across all application platforms.

Implementation: Extend the existing account, profile and credential persistence model to represent independent verification states, public badge assignments, founding-member recognition, employment affiliation, administrative trust review and official bot classification. Reuse existing authentication and authorisation infrastructure for privileged system access rather than deriving permissions from public profile badges.

Persist Photo Verified and ID Verified as independently managed credentials with issuance, verification, expiry and revocation metadata. Implement validation of the underlying verification evidence and ensure that photograph changes can trigger re-verification without affecting unrelated identity credentials. Expose appropriate public verification dates without disclosing sensitive evidence or identity information.

Implement Admin Verified as an internal, auditable enhanced manual trust-review record. Provide authorised administrative operations for review completion, reassessment and revocation. The status must remain valid until revoked and must not be included in public badge or discovery responses.

Implement Founding Member recognition as a durable programme entitlement or status associated with the member account, subject to the approved 1,000-position limit and required verification activation. Keep permanent founding recognition distinct from the three-month Plus grant and subsequent twelve-month discounted subscription opportunity. Integrate the approved founder-recovery mechanism without using recovery tokens as authentication credentials.

Model Swoovr Staff as a revocable employment-affiliation credential attached to an otherwise ordinary member account. Assign the ruby badge only after authorised confirmation of employment. Do not create a privileged public Staff account type, automatically grant administrative permissions or require separate public-facing accounts. Administrative identities and role assignments must be managed independently. Ensure that revoking employment affiliation removes the public badge without automatically deleting the member's normal account or unrelated credentials.

Extend the moderation workflow so that every report against a staff-affiliated member is flagged for mandatory human review. Automated triage may continue to operate, including urgent protective interventions, but must not close the report without the required human-review outcome. Implement appropriate reviewer assignment, conflict-of-interest protections, senior escalation and comprehensive auditing. Support separate internal management escalation where serious employee misconduct is substantiated.

Implement official bots using an explicit automated-account classification and authorised provisioning workflow. Persist independently configurable connection-mode permissions for each bot. Include eligible official bots in normal discovery results, with mandatory bot identification. Enforce member-initiated contact by rejecting bot-originated initial connection requests and unsolicited conversation creation at the API and application-service layers. Permit appropriate bot responses after member initiation. Prevent bots from receiving human Photo Verified or ID Verified credentials.

Extend the existing badge-rendering components, including the Blazor WebAssembly badge component and corresponding .NET MAUI UI, to render all approved badge types and valid combinations. Use centrally defined badge metadata for identifiers, artwork, labels, ordering, accessibility descriptions and visibility rules. Ensure badges appear consistently across discovery cards, member profiles and messaging surfaces.

Public-facing APIs must derive badges from trusted current credential and account records, exclude confidential administrative states and avoid exposing verification evidence, privileged role assignments or internal moderation information. Implement automated tests covering badge eligibility, combinations, verification independence, founding-member activation and recovery, staff affiliation assignment and revocation, mandatory staff-report review, bot mode configuration, bot identification, bot-initiated interaction restrictions, public/internal data separation and cross-platform presentation consistency.

## Owner follow-up answers — 24 October 2026

Given by the owner in the overnight brief of 24 October 2026, after the Q16, Q17 and Q11 answers above. CONFIRMED; recorded as D109–D112.

1. **Itineraries (Q17 rule 11, D109).** Trips may be grouped into an itinerary of back-to-back stops: each stop arrives on or after the previous one departs, with a gap of 14 days or less. Every stop in an itinerary becomes visible once its first stop is within 14 days of arrival. A trip outside an itinerary opens 14 days before its own arrival. Turning visibility on for a trip whose dates overlap another visible trip is refused (sharing the changeover day is fine).
2. **Locals and markets (Q17 rules 6 and 22, D110).** The core profile gains a home city, picked from the same GeoNames search; the free-text Area stays. A `markets` table lists each market (stable id, name, active flag, centre GeoNames id, internal radius). Adelaide is the only active market. A "local" is a member whose home city is in the same active market as the trip's city. Coordinates and distances never leave the server.
3. **GeoNames (Q17 rules 1–4, D111).** Use `cities1000` plus `admin1CodesASCII` and `countryInfo`. A script downloads them from download.geonames.org into `.local` (never committed) and imports them into a `geo_cities` table (GeoNames id as the key, name, ASCII name, alternate names for search, admin1, country, population, IANA timezone, coordinates). Tests use a small committed sample file. Autocomplete ranks by population and shows "City, Region, Country". Every screen with city search shows "Place data © GeoNames (CC BY 4.0)".
4. **Scope (D112).** Build connection requests and blocks before Travel. Real messaging stays out (the conversation stays a prototype).

## Chips, badges, requests and Travel build questions — 24 October 2026

Raised during the unattended build of slices J–M. The owner accepted every recommended default on 24 October 2026 (D124).

| ID | Status | Question | Earliest blocker | Recommended default (built) |
| --- | --- | --- | --- | --- |
| Q18 | ANSWERED, owner 24 October 2026 (defaults accepted); D121, D124 | What radius defines the Adelaide market (D110 gives each market a centre GeoNames id and an internal radius but no value)? | Who counts as a local for Travel (slice M) | 50 km around Adelaide (GeoNames 2078025): metropolitan Adelaide including Gawler and Mount Barker; Victor Harbor and the Barossa fall outside. A row value, changed without code |
| Q19 | ANSWERED, owner 24 October 2026 (defaults accepted); D116, D124 | How do official bots sign in? Q11 rule 55 keeps bot credentials apart from member sign-in, but today a bot is a members-realm account an admin classifies (D115) | Running a real bot (guide) account | Keep admin classification of members-realm accounts for the prototype; before any real bot runs, give bots a Keycloak service client (client-credentials) in their own realm or client, with no password sign-in, and map it to the bot's account record |
| Q20 | ANSWERED, owner 24 October 2026 (defaults accepted); D108, D124 | Photo verification, ID verification (Stripe Identity, Q04) and staff-report moderation with mandatory human review (Q11 rules 5–13, 39–43) are not built. Which comes first? | Founder activation, public Photo/ID badges, moderation | Q04 first (Stripe Identity sandbox), then reports and moderation with the staff rule; until then Photo/ID badges come only from records an admin or test sets, and nothing is labelled as live verification |
| Q21 | ANSWERED, owner 24 October 2026 (defaults accepted); D117, D118, D124 | What request limits suit the beta: how many requests per day, and how long before someone may ask again after a decline? Should a block end an existing connection for good rather than hide it? | Abuse handling at beta scale | 20 a day per sender, a 30-day wait after a decline in that mode, and a block hides (not deletes) a connection so unblocking restores it (D117, D118) |
| Q22 | ANSWERED, owner 24 October 2026 (defaults accepted); D120, D122, D124 | Travel limits and details not given by Q17: how far ahead a trip may start, how long it may be, how many trip changes an hour, whether other members ever see a home city, and what the feed shows of a traveller beyond name, badges, chips and the visit | Travel at beta scale | Two years ahead, at most 365 nights, 30 changes an hour, home city never shown to others, and no area or about text in the feed (D120, D122) |
| Q23 | ANSWERED, owner 24 October 2026 (defaults accepted); D124 | Q12 says discovery moves to the member's actual location on arrival, but Swoovr has no device location yet (D100/D110 use only a home city). Should arrival change anything before device location exists? | Arrival behaviour | No change on arrival: the trip stays a "Visiting" entry until it expires, and home discovery is unchanged. Device location is a separate decision with its own privacy review |

## Review and next-slices questions — 24 October 2026

Raised by the builder's [review of 24 October 2026](REVIEW-2026-10-24.md) for slices O–U (order and reasons there). All answered by the owner on 24–25 October 2026 under [Answers](#answers) below and recorded as D125–D144. Earliest blocker first. Answer in the **Owner answer** column; "default" accepts the recommendation. Provider values (keys, secrets) never go in this file or in chat: the questions say where they should be put.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q24 | ANSWERED | O (market-scoped home discovery) | Should Explore, opening a profile from Explore and sending a request without a trip work only between members whose home cities are in the same active market? What does a member with no home city, or one living outside every active market, see? Where do official bots appear? | Today Explore ignores markets, so a member from Sydney sees Adelaide members and can request them before arriving. That bypasses "travellers get no feed of locals" and "no traveller initiation before arrival" (Q17, D60, D122) and Q07 rule 2 | Yes. Both need a home city in the same active market (Adelaide). No home city: "Add your home city to see people near you". Outside every active market: "Swoovr isn't open where you live yet"; they can still edit their profile, plan trips, answer requests and see existing connections. Existing connections and pending requests across markets are kept. Staff follow the rule like anyone else; an official bot appears in every active market for its authorised modes | See [Q24](#q24--market-scoped-discovery-local-connections-and-travel-access) |
| Q25 | ANSWERED | P (ID verification) | Which Stripe account will the Identity sandbox use (RePass Cloud Pty Ltd's, test mode only), and has its Stripe Identity application been submitted and approved? Stripe decides Identity eligibility by business location and use case, so an adult-connection app may need review. Will you put a restricted test-mode key and the webhook signing secret in `.local/docker.env` as `SWOOVR_STRIPE_SECRET_KEY` and `SWOOVR_STRIPE_WEBHOOK_SECRET`, and run `stripe login` so I can forward events with `stripe listen`? | Without your keys and an approved application no real test-mode verification can run; I won't invent either | A restricted `rk_test_` key limited to Identity verification sessions (read and write); events forwarded locally by the Stripe CLI to `http://localhost:5080/api/v1/webhooks/stripe`, with the `whsec_` secret `stripe listen` prints copied into the env file by you. Until then I build against a signed-event simulator used only by tests, shown as "Simulated — not Stripe", and report the real test-mode run as not done | See [Q25](#q25--identity-verification-provider-and-integration) |
| Q26 | ANSWERED | P | In the beta, is ID verification optional (it gives the ID badge, a Verified age status and founder activation) or required before discovery and contact (the DEFAULT gate in `04-IDENTITY-TRUST-AND-SAFETY.md` §2)? Does an age review (D40 `NeedsVerification`) require it? | Decides who can browse and contact; each check is charged to your Stripe account | Optional in the beta. Required only when staff ask for an age check: that account stays out of discovery and requests until verified or overridden (as `NeedsVerification` already does). Revisit the gate before public launch | See [Q26](#q26--optional-id-verification-and-mandatory-age-assurance) |
| Q27 | ANSWERED | P | Which checks and what Swoovr keeps: document plus a matching live selfie? Which documents (passport, driver licence, national ID card)? Do we keep the verified date of birth (replacing what the member typed) or only "adult: yes" and the check date? Should Swoovr ask Stripe to redact each session, and after how long? Will you supply the consent and privacy wording shown before the check, or use my draft marked "draft — not legal advice"? | Minimum data (`04-IDENTITY…` §2, Q11 rule 65); dates of birth are sensitive; Stripe keeps documents and selfies unless redacted | Document plus live selfie, all three document types, live capture only. Keep the Stripe session id, status, error code, check time and the verified date of birth (it replaces a typed date or age; never name, document number, address or images). Redact each session 30 days after a final outcome, so support can look into a dispute in the Stripe Dashboard first. Use my draft wording until yours replaces it | See [Q27](#q27--identity-documents-liveness-and-verification-flags) |
| Q28 | ANSWERED | P | How many attempts may a member start (each costs a fee)? What happens after a failure or "consent declined"? Does ID verified expire? Who handles verification appeals in the beta? | Cost control, fairness, and the manual fallback that `04-IDENTITY…` §2 says is necessary | 3 started checks per member per 30 days, then "try again later". A failure shows "Needs action" with Stripe's reason; consent declined or repeated failure points to support. The manual route is support or an admin recording Admin verified after an in-person or video check, which never gives the ID badge (D58). No expiry in the beta; staff can revoke on evidence. An under-18 result locks the account (D40) | See [Q28](#q28--verification-attempts-failures-and-administration) |
| Q29 | ANSWERED | P | Does the first successful ID verification activate founding membership automatically when a place is free? What is the "required profile setup" (Q11 rule 21)? Should the admin grant route (D79) still work for an account with no verification? | Q11 rule 21 and D63 make verification a condition; D79 was a stop-gap | Automatic on ID verified once the profile has a display name, a home city and at least one mode on, and a place is free (Photo verified will also count when it exists). The admin route stays for exceptions but refuses an account with neither verification. The founder offer is recorded as an entitlement (D90) with its start date; no billing | See [Q29](#q29--founding-membership-activation) |
| Q30 | ANSWERED (approved 25 October 2026, Q49) | Q (reports and moderation) | What can members report, with which categories, is free text allowed, and what does the reporter hear back? | Shapes the report form, the queue and what personal content Swoovr stores | Report a person, a connection request or a bot. Categories: harassment or abuse; threats or violence (urgent); may be under 18 (urgent); fake profile or impersonation; scam or spam; unwanted sexual content or solicitation; hate; misrepresented intentions; something else. Optional text up to 1,000 characters, seen only by moderators. The form offers to block as well. The reporter sees "Thanks, we've received it" and later "We've reviewed your report", never the outcome. 10 reports a day per member; repeat reports join the same case | See [Q30](#q30--member-reporting-categories-and-notifications) |
| Q31 | ANSWERED | Later (photos); limits P and Q | Photos, private media and photo verification stay out of this run (D100)? For later: which storage provider and region (Azure Blob Storage, Australia East, given D39's Azure note?), and should Photo verified use Stripe's selfie or a separate photo matcher? | Founder activation is "Photo and/or ID"; until photos exist only ID counts, and reports can't include photos | Out of this run. Ask the provider and region again when photos are the next slice | See [Q31](#q31--photo-storage-regional-hosting-and-photo-verification) |
| Q32 | ANSWERED | Q | Which actions can moderators take, who may take each, and what does a restricted or suspended member experience? | Q07 rules 13–16 and Q11 rule 42; actions must be enforced everywhere and be appealable | Actions: dismiss, warning, restriction (hidden from discovery and can't send requests, up to 30 days), suspension (member area closed with a notice, up to 90 days), termination, and removal of staff affiliation. Support may dismiss, warn and restrict; admins may also suspend, terminate and remove affiliation. A restricted member keeps existing connections; a suspended one sees "Your account is suspended until …" with an appeal link | See [Q32](#q32--moderation-actions-permissions-and-account-restrictions) |
| Q33 | ANSWERED | Q | For reports against staff-affiliated members (Q11 rules 39–43): who are the "designated senior reviewers", and how do we link a staff-realm sign-in to that person's own member account so nobody reviews a report about themselves? | Rule 41's conflict-of-interest check needs a staff-to-member link that doesn't exist | Only admins (not support) decide staff cases, and automated steps can't close them. When an admin grants a staff affiliation they also record which staff-realm account belongs to that member account; a reviewer linked to the reported account, or reported in the same case, is refused | See [Q33](#q33--staff-affiliated-member-accounts-and-report-handling) |
| Q34 | ANSWERED | Q | How do appeals, member notices and urgent escalation work in the beta? Email notices need MailerSend, which isn't configured (local Mailpit only). Who is the escalation contact for threats or a suspected minor outside business hours, and where should that contact live (a config value in `.local/docker.env`, not the repo)? | Q07 rules 11, 15 and 16; urgent cases must reach a person | One appeal per action within 30 days, decided by a different moderator, final. Notices are in-app; email goes to Mailpit locally until MailerSend exists. Urgent categories go to the top of the queue and email the escalation address from `SWOOVR_ESCALATION_EMAIL` in `.local/docker.env` (Mailpit locally) | See [Q34](#q34--appeals-member-notifications-and-urgent-escalation) |
| Q35 | ANSWERED | Q | Should moderation get a minimal staff web screen now (queue, case, action, appeal) or stay API-only like the other admin routes? Staff sign in with Entra only (D32), which isn't configured, so locally only the root admin can sign in to the staff realm. May I add fictional password-based support and admin users in the **local** staff realm only, to test the role split? | The India-based team can't moderate through raw API calls; testing support versus admin needs local staff users | Build a minimal staff area in Web.Client with its own staff BFF allow-list (D84 keeps admin routes off the member BFF), and allow fictional local password users in the local staff realm only (never in UAT or production realm settings) | See [Q35](#q35--administration-interface-and-local-staff-testing) |
| Q36 | ANSWERED | R (filters and compatibility) | Which filters belong to each mode in the beta's Free tier ("standard search filters")? Should locals also choose whether they see current visitors, upcoming visitors, both or none (`02-BEHAVIOUR…` §5), which the built feed (D122) doesn't offer? | D92 confirms per-mode filters without listing them; the Free/Plus split (Q08 matrix) depends on the list | All modes: age range, required chips (any or all), excluded chips (any), and "verified only" (ID verified). Dating adds gender and "show me" (Q37). Business adds nothing. Locals get the visitors choice (default both). Expanded (Plus) filters wait for paid tiers | See [Q36](#q36--discovery-filters-by-mode-and-subscription-tier) |
| Q37 | ANSWERED | R | Dating needs gender and "who I want to see". Which gender options (and is free text allowed), is it shown on the profile, is "show me" reciprocal, and do Friends, Activities or Business use it at all? Is sexual orientation asked at all? | No gender or orientation exists in the profile today; these are sensitive and must not become abusive filter categories (`02-BEHAVIOUR…` §2) | Gender: woman, man, non-binary, or "another" with optional text of up to 30 characters; shown only in Dating and only if the member chooses. "Show me" is multi-select and reciprocal in Dating only. Not used in other modes. Orientation is not asked | See [Q37](#q37--gender-orientation-and-reciprocal-dating-preferences) |
| Q38 | ANSWERED | R | Is a member's age shown to others, and how does an age filter treat people with no age (DOB and age are optional, D69)? | An age filter is useless if most people have no age; showing age is a privacy choice | Age shows on cards and profiles unless the member turns it off; an age filter includes people with no age unless the viewer ticks "only people with an age" | See [Q38](#q38--age-display-and-age-filtering) |
| Q39 | ANSWERED | R | The brief requires "require and exclude intentions" (`01-PRODUCT-BRIEF.md` §2, `02-BEHAVIOUR…` §1, acceptance criteria 1–2), but Q16 rule 10 says chips never exclude anyone on their own. Is it right that exclusion happens only through a viewer's own filter (Q36), applied both ways? How is compatibility shown? | The two documents conflict as written | Yes: chips alone never hide anyone; a member's required/excluded chip filters hide people both ways (if you exclude a chip, people with it don't see you either). Compatibility = the chips you share in that mode, with no score (`03-SCREENS…` §4); Explore orders by shared chips, then recency. The old intention vocabulary in `02-BEHAVIOUR…` §1 is replaced by the forty chips | See [Q39](#q39--intention-chips-filtering-and-compatibility) |
| Q40 | ANSWERED | S (MAUI parity) | What is the app's final tab bar? The product docs say Explore, Connections, Chats, Me; the app has Me, Profile, Explore, Travel (D123). And should badge artwork in the app reuse the web's drawings exactly? | Navigation must match across clients (Q11 rule 68) | Explore, Connections, Chats (shown as not built until messaging), Me, like the web. Profile and Travel move under Me and the Explore heading. Badge artwork reuses the web's shapes and fixed colours | See [Q40](#q40--maui-navigation-and-badge-consistency) |
| Q41 | ANSWERED | T (traveller search) | Q17 rule 36 deferred traveller search; build it now? Before arrival a traveller may not start contact (D60), so what can they do from the results: only look, or also save someone or send a request? Is it basic (Free) or "extended travel discovery" (Plus)? | Decides whether the slice is worth building before messaging | Build it as recorded in `08-PROFILE-FLAGS-AND-MODES.md` (own trip id, active market, Explore's rules). Look only before arrival: no requests or saves. Free in the beta | See [Q41](#q41--traveller-search) |
| Q42 | ANSWERED | U (messaging) | Lift D112 and build real messaging in this run? If yes: text only, one conversation per connected pair (across modes) or per mode, realtime (SignalR, D59), no attachments, read receipts or not, edit/delete or not? How long are messages kept, after a block, after account deletion, and can moderators read messages only when they are reported? | Messaging is the largest slice, stores private content, and changes moderation (report a message) | Yes, after slices O–T: text only, one conversation per pair across modes, SignalR, no attachments, no read receipts, delete for both within 5 minutes. D60 enforced. Bots reply only to a member-started conversation. Kept while both accounts exist; a block hides it; a reported message and the 20 before it are copied into the case; moderators see nothing else | See [Q42](#q42--messaging-conversations-and-retention) |
| Q43 | ANSWERED | Later (deletion and export) | Add account deletion and data export to this run? If yes: grace period before final deletion, what is kept and for how long (audit ids, moderation cases, founder places), and does deletion end the Keycloak account too? | App Store requirement, `04-IDENTITY…` §5, acceptance criterion 16, and D65 emails the founder token at a deletion request | After this run. Proposed later: deactivate at once, delete after 30 days, keep audit ids and moderation cases for 2 years pending legal review, delete the Keycloak user at the end, email the founder token at the request | See [Q43](#q43--account-deletion-data-export-and-retention) |

### Defaults I'll use unless you object

DEFAULT, builder 24 October 2026. Reversible implementation choices; they become D-rows when built.

1. One branch (`feature/swoovr-verification-moderation-filters`) and one draft PR for every slice; slices are lettered O onwards.
2. The market rule (Q24) joins the shared eligibility check in `Swoovr.Domain`, so Explore, person pages and requests use one rule; a viewer outside a market gets 403 `market-not-open`, and anything about another person stays the same 404 `not-available`.
3. Stripe goes in `Swoovr.Infrastructure` behind a Domain interface, using the official `Stripe.net` package at a pinned version with a clean NuGet audit. No other project references it.
4. Routes: `POST /api/v1/me/verifications/id` creates the session on the server and returns only the Stripe-hosted URL; `GET /api/v1/me/verifications` returns the member's own status; `POST /api/v1/webhooks/stripe` takes no sign-in, checks the signature on the raw body with Stripe's 5-minute tolerance, stores each event id once (`provider_events`: id, type, received; never the payload) and then re-reads the session from Stripe by the id Swoovr stored. The browser's return redirect and anything from the client never change a status. Session metadata carries only an opaque verification id, never the account id or email.
5. Return to `/me/verification` on the web and `com.swoovr.app:/verified` in the app, opened in the system browser (no native Stripe SDK in MAUI).
6. `identity_verifications` (id, account, provider, provider session id, status, error code, created, completed, redacted) map onto the existing `VerificationStatus`; the ID badge still comes only from `BadgePolicy`.
7. Support and admins get an audited "request an age check" route that sets `NeedsVerification` (D40's AI review is not built).
8. Moderation tables: reports, cases (one open case per reported account), case events, restrictions, appeals and Admin verified trust reviews (Q11 rules 14–19: admin only, reason from a fixed list plus an optional note in the restricted table, never in public responses). Report text and notes are never copied into the audit log.
9. Restriction and suspension are enforced in the shared eligibility check and the member gate, so every route gets them.
10. Audit entries hold ids, kinds and machine codes only, never report text, notes, dates of birth, Stripe payloads or verification evidence (D70).
11. Filters are stored per member and mode (`member_mode_filters`) and applied on the server in both directions where Q39 says so; Explore stays at 50 people in a stable order.
12. New browser suites follow `tests/connections/requests.browser.cjs`: `tests/discovery/market.browser.cjs`, `tests/verification/identity.browser.cjs`, `tests/moderation/reports.browser.cjs`, `tests/profiles/filters.browser.cjs`. Negative cases include other members' ids, blocked pairs, bots, retired and unknown ids, tampered bodies over client limits, and forged, replayed, stale and wrong-account webhook events.
13. Problem codes stay kebab-case; limits are configuration values.

## Answers

### Q24 — Market-scoped discovery, local connections and travel access

ANSWERED — Owner, 24 October 2026

Swoovr is a local-first dating and social discovery platform. Members primarily discover and connect with people in their own community. Travel Mode allows members to discover and establish connections in other locations when they genuinely intend to visit those locations.

Swoovr will launch markets progressively, beginning with Adelaide, followed by Melbourne, Sydney, other Australian locations, New Zealand and eventually international markets.

The following rules apply.

#### 1. Active markets

1. Swoovr operates a centrally managed registry of active markets. Each market defines its geographic coverage, supported discovery modes and operational status.
2. A market may be `Planned`, `Beta`, `Active` or `Suspended`.
3. Adelaide is the initial active beta market. Melbourne, Sydney and subsequent markets are enabled independently when ready.
4. A member's home city determines their home market using the market's geographic boundaries.
5. Only markets enabled for the relevant member population and discovery mode participate in discovery.
6. Market boundaries must be configurable without application releases. A market can cover a metropolitan area or wider region, rather than requiring an individual market for every suburb.
7. Market activation controls new discovery, not the validity of existing relationships.

#### 2. Local discovery

8. Explore defaults to the member's home market.
9. Members can discover, open profiles and initiate connections with eligible members whose home locations fall within the same active market.
10. A member living in Adelaide cannot use ordinary Explore to discover members living in Melbourne or Sydney.
11. Market eligibility is enforced server-side for Explore results, direct profile access and connection requests. Hiding profiles in the UI alone is insufficient.
12. Members cannot bypass market restrictions using profile URLs, API calls, modified location parameters or alternative clients.
13. Existing connections and conversations remain accessible when members change home cities, travel or move between markets, subject to normal account and safety restrictions.
14. Staff-affiliated members follow the same discovery restrictions as ordinary members.

#### 3. Travel Mode

15. Travel Mode is the authorised mechanism for discovering people outside the member's home market.
16. A member selects a destination in an enabled market and supplies an intended arrival and departure date.
17. Travel visibility begins no earlier than 14 days before arrival, consistent with the existing travel policy.
18. Members can maintain multiple planned trips to different enabled markets.
19. Before arrival, travellers may browse eligible profiles in their destination through Traveller Search, but cannot initiate connection requests or conversations with destination locals.
20. Once the traveller arrives and their presence is established under the existing location-verification rules, they may initiate connections with eligible people in that market.
21. Destination locals may discover and initiate contact with eligible incoming travellers during their permitted pre-arrival visibility window, subject to both members' discovery preferences.
22. Travel Mode must not grant unrestricted access to every market simply because a member enables it.
23. Members cannot use an arbitrary remote-city selector in ordinary Explore to bypass market restrictions.

#### 4. Remote browsing and genuine travel

24. Swoovr does not require members to prove they have purchased flights, booked accommodation or provided travel documents. This would create unnecessary friction and require additional personal data.
25. A declared trip is sufficient to enable limited pre-arrival Traveller Search, but not permission to initiate contact with destination locals.
26. Actual arrival, rather than merely selecting a destination or changing a trip date, is required to unlock traveller-initiated contact.
27. Members may change or cancel trips. Cancelling a trip removes its destination discovery eligibility without affecting existing connections.
28. Repeated destination switching, implausible travel patterns or attempts to manipulate location may trigger rate limiting, additional location assurance or moderation review.
29. Remote browsing alone is not treated as misconduct. The system prevents its misuse through discovery and initiation restrictions rather than assuming dishonest intent.

Product distinction: Swoovr allows people to explore where they genuinely plan to go, without turning Travel Mode into a global catalogue of people they can contact remotely.

#### 5. Members outside active markets

30. Members must provide a home city before participating in discovery.
31. Members without a home city see: "Add your home city to discover people in your community."
32. Members living outside all enabled markets can register, complete their profiles, obtain verification, maintain existing connections and manage planned trips.
33. Their local Explore feed displays: "Swoovr isn't available in your area yet. We're expanding to new cities."
34. Members outside active markets cannot create an artificial home location in Adelaide or another enabled market simply to access its local discovery.
35. They may use Travel Mode for an actual planned visit to an enabled market, subject to the same traveller restrictions.
36. Existing connections and pending requests are preserved when a market is not active or when a member moves outside active market coverage, subject to normal account and safety rules.
37. Members may register interest in their home market being launched, allowing Swoovr to measure demand for future expansion.

#### 6. Official bots

38. Official Swoovr bots may be assigned to one or multiple active markets independently of a physical home city.
39. A centrally authorised official bot may appear in every active market for its authorised modes and functions.
40. Bots must display the official Bot badge and must never appear to be ordinary local members.
41. Bots cannot be used as a mechanism to expose real member profiles outside permitted markets.
42. Staff-affiliated human accounts are not bots and receive no special market-discovery privileges.

#### 7. Enforcement

43. The API is authoritative for market membership, discovery eligibility, travel status and permission to initiate contact.
44. Explore, Traveller Search, direct profile access and connection-request endpoints must use consistent eligibility rules.
45. Market eligibility must also respect the existing mode, intention, blocking, age, visibility, verification and moderation requirements.
46. Previously authorised connections remain available across market boundaries; market restrictions are not retroactively applied to established relationships.
47. A suspended market stops new discovery under the configured suspension policy without automatically deleting member accounts, requests or conversations.
48. Administrators may activate, suspend and configure markets without redeploying the applications. Any exceptional administrative access must be authorised and audited.

#### Decision summary

Approve Q24 with the rules above, rather than accepting the recommended default unchanged.

The governing principle is:

Local discovery is based on where a member lives. Remote discovery is based on legitimate travel to an enabled market. Remote contact initiation requires arrival. Existing relationships are not restricted by market boundaries.

This preserves Swoovr's community-first design while supporting its planned geographic expansion and the existing Travel Mode decisions (Q17, D60, D122).

One additional architectural distinction is important: markets should not be synonymous with cities. Adelaide, Melbourne and Sydney can each launch as metropolitan markets, while future regional markets might cover multiple towns or a wider geographic area. This avoids building a system that becomes cumbersome as Swoovr expands across Australia and New Zealand.

### Q25 — Identity verification provider and integration

ANSWERED — Owner, 25 October 2026

Swoovr will use [Didit](https://didit.me) instead of Stripe Identity. An account with Didit already exists.

1. Use Didit's hosted identity verification workflow.
2. The Swoovr API creates a verification session and receives a hosted URL.
3. The member completes verification through Didit's hosted interface.
4. Didit sends the result to Swoovr's API through a webhook.
5. The API validates the webhook and updates the member's verification status in the database.
6. The member's verification status and badges update in the application shortly after processing.
7. API credentials and webhook secrets remain server-side and are never exposed to the client.
8. The final choice between hosted redirects, embedded verification and other integration options will be determined during implementation.

A hosted workflow is preferred to minimise development complexity and avoid handling sensitive identity documents directly.

Didit documents hosted session URLs, server-side session creation and verification-result retrieval.

### Q26 — Optional ID verification and mandatory age assurance

ANSWERED — Owner, 25 October 2026

ID verification is always optional, including during beta and after public launch.

1. ID verification is provided free of charge to Swoovr members.
2. Successful verification provides the public ID Verified badge and contributes to member trust.
3. Members can participate without ID verification unless their account is specifically flagged for age or identity review.
4. Account registration requires a valid email address and mobile phone number.
5. Members may register using email and password, but must complete email and SMS verification before their account becomes active.
6. A member flagged as `NeedsVerification` for age assurance cannot use the application until the required check is completed or an authorised administrative resolution is recorded.
7. An unresolved age-verification restriction must not be bypassed through another account using the same verified email or mobile number.
8. Accounts confirmed to belong to people under 18 are not permitted to use Swoovr.
9. Future duplicate-account detection using AI and profile photographs is deferred.

Important distinction: Optional ID verification does not mean optional compliance with mandatory age-assurance requirements when an account is flagged.

### Q27 — Identity documents, liveness and verification flags

ANSWERED — Owner, 25 October 2026

Swoovr will use Didit for document verification, liveness detection and facial matching.

1. Accept passports, driving licences and other supported government-issued photographic identification.
2. Supported documents depend on Didit's availability in each country, initially Australia and New Zealand.
3. Didit compares the live selfie against the photograph on the identity document.
4. Successful verification establishes that the presented document passed validation and that the live subject matched the document photograph.
5. This does not independently establish that the photographs on the member's Swoovr profile depict the verified person.
6. Administrators may manually review suspected mismatches, add internal account notes, revoke ID Verified status and permit re-verification.
7. Identity documents, selfies and biometric data remain with Didit under its applicable retention and privacy policies. The intended Australian retention period is 30 days, subject to confirmation with Didit.
8. Swoovr stores only the minimal verification outcome and operational metadata. No document photographs, document numbers, biometric templates or identity-document copies are retained.

Two principal database flags govern ID verification:

| Flag                       | Purpose                                                     |
| -------------------------- | ----------------------------------------------------------- |
| `CanRequestIdVerification` | Whether the member can initiate another verification        |
| `IsIdVerified`             | Whether the member currently holds valid ID Verified status |

Rules:

- Initially: `CanRequestIdVerification = true`, `IsIdVerified = false`.
- Once verification is attempted: `CanRequestIdVerification = false`.
- On successful verification: `IsIdVerified = true`.
- On failed verification: `IsIdVerified = false`.
- Administrators may reset `CanRequestIdVerification = true` to authorise another attempt.
- Administrators may revoke verification by setting `IsIdVerified = false`.
- Maintain an internal attempt record containing the Didit session reference, result, timestamp and administrative changes for auditability.
- Only successful verification results produce the public ID Verified badge.

### Q28 — Verification attempts, failures and administration

ANSWERED — Owner, 25 October 2026

1. Members are allowed one self-service ID verification attempt.
2. The attempt is recorded in the database and visible in the administration interface.
3. After an attempt, members cannot initiate another verification themselves.
4. Failed, declined or incomplete verifications are visible to authorised administrators.
5. Administrators determine what further action is appropriate on a case-by-case basis.
6. Administrators may authorise another attempt by resetting `CanRequestIdVerification`.
7. ID Verified status remains valid until revoked; no automatic expiry is introduced for beta.
8. Administrators may revoke verification following evidence of fraud, account misuse or identity mismatch.
9. Age-assurance restrictions remain active until the required verification or an authorised resolution is completed.
10. All administrator actions affecting verification status must be audited.

A session that expires or encounters a technical failure before verification is actually attempted should be distinguishable from a completed failed check. Administrative review determines whether a new attempt is permitted.

### Q29 — Founding membership activation

ANSWERED — Owner, 25 October 2026

Founding membership activation will be handled by a separate scheduled database process, not automatically by the verification API.

1. Swoovr has exactly 1,000 founding membership positions.
2. Members qualify after completing the required profile setup and successfully completing either ID verification or Photo Verification.
3. Founding memberships are allocated in chronological order of the member's eligibility completion.
4. A scheduled script queries eligible members who have not already received founding membership.
5. The script assigns available founding membership positions and updates the appropriate SQL database records.
6. It records founding status and the associated membership entitlements.
7. The script may execute once daily using an external cron job or equivalent scheduler.
8. Once all 1,000 positions are allocated, the scheduled job is disabled or stopped.
9. Founding membership assignment must be idempotent, ensuring members cannot receive multiple positions and the allocation cannot exceed 1,000.
10. The scheduled process must use a transaction and database constraints to prevent duplicate allocations.
11. Administrative exceptions remain possible but must respect the verification requirement and 1,000-position limit.

Existing founding benefits remain unchanged:

- Permanent gold Founding Member badge.
- Three months of Swoovr Plus, free of charge.
- A 50% discount on Plus subscriptions for the subsequent 12 months, available for monthly or annual plans.
- No payment method required for the three free months.
- No automatic conversion to a paid subscription.
- Founder recovery provisions remain applicable if a member later deletes and recreates their account using the approved recovery mechanism.

Implementation note: Record the timestamp when a member first meets all founding eligibility conditions, rather than simply ordering by registration date. Allocation and entitlement activation dates must be retained independently.

### Q30 — Member reporting, categories and notifications

**ANSWERED — Owner, 25 October 2026** (drafted as "pending owner approval"; approved as written on 25 October 2026, [Q49](#q49--approve-the-q30-reporting-answer))

Swoovr must provide a consistent reporting system across the application, allowing members to report inappropriate behaviour, suspicious accounts, unwanted content and safety concerns.

Reporting is available to all members, regardless of subscription tier or verification status.

#### 1. What members can report

Members can submit reports from the following locations:

| Location                | What can be reported                                               |
| ----------------------- | ------------------------------------------------------------------ |
| Member profile          | Fake identities, harassment, suspicious behaviour, profile content |
| Connection request      | Inappropriate or unwanted requests                                 |
| Conversation            | Harassment, threats, scams or inappropriate behaviour              |
| Individual chat message | Specific offensive, abusive or inappropriate messages              |
| Shared photographs      | Unwanted sexual content, inappropriate or prohibited images        |
| Private albums          | Inappropriate content or misuse of album sharing                   |
| Official bots           | Incorrect, harmful or inappropriate bot behaviour                  |

A report should automatically identify its source and relevant member, message, photograph or conversation.

Members must also be able to report someone they have already blocked, where sufficient account or conversation history remains available.

#### 2. Reporting categories

Use a centrally managed list of categories rather than hardcoding them into each client.

| Category                                         | Priority |
| ------------------------------------------------ | -------- |
| Harassment, bullying or abuse                    | Normal   |
| Threats, violence or personal safety             | Urgent   |
| Suspected underage member                        | Urgent   |
| Fake profile or impersonation                    | Normal   |
| Scams, fraud or spam                             | High     |
| Unwanted sexual content                          | High     |
| Hate speech or discrimination                    | High     |
| Sexual harassment or solicitation                | High     |
| Misrepresented intentions or deceptive behaviour | Normal   |
| Inappropriate profile or album content           | Normal   |
| Other concern                                    | Normal   |

Priority may be increased automatically based on severity, repeated incidents or account history.

#### 3. Reporting form

The reporting interface should be simple and consistent.

1. Select a reporting category.
2. Optionally provide additional information (maximum 2,000 characters).
3. Include the originating content reference automatically, where available.
4. Offer the option to block the reported member immediately.
5. Submit the report and receive confirmation.

For reports involving individual messages or photographs, preserve the relevant content as moderation evidence even if the sender subsequently deletes it, subject to retention and privacy rules.

Members should not be required to confront or contact the reported person.

#### 4. Reporting restrictions and duplicate reports

1. Reporting is free and available to all members.
2. Members may submit multiple reports where different incidents occur.
3. Duplicate reports about the same incident should be consolidated into an existing case.
4. Apply rate limiting and abuse detection to prevent malicious or automated report submissions.
5. Do not impose a rigid limit of 10 reports per day, particularly for serious safety concerns.
6. Repeated malicious reporting may itself trigger moderation review.

#### 5. What the reporter receives

Immediately after submission:

> Thanks for your report. Our moderation team will review it. You can also block this member to prevent further contact.

Once reviewed:

> Your report has been reviewed. Thank you for helping keep Swoovr safe.

The reporter should be able to see the status of their own reports:

- Submitted
- Under review
- Reviewed
- Closed

Specific disciplinary actions, private investigation details and information about other members must not be disclosed.

#### 6. Internal moderation

1. Reports create a moderation case accessible through the authorised staff administration interface.
2. Cases include the reporting member, reported member, category, priority, relevant content references, timestamps and optional explanation.
3. Moderators may add internal notes and supporting evidence.
4. Reports involving staff-affiliated members automatically require senior administrative review, consistent with Q11.
5. Urgent reports must be escalated promptly to the designated safety team.
6. All case access, status changes and disciplinary decisions must be audited.
7. Reporters' identities must not be disclosed to reported members through reporting notifications.

#### 7. Privacy and evidence retention

Reports and related evidence are accessible only to authorised moderation personnel.

Relevant evidence should be preserved when a report is submitted, including content that might otherwise be deleted. Retention must follow Swoovr's moderation evidence policy, which will be finalised separately.

#### Q30 — My recommendations for Swoovr

I would make three adjustments to Claude's proposed default:

1. Don't restrict reporting to people, requests and bots. Messaging, individual photographs and private albums must be reportable once those features become available. The reporting architecture should support all these content types from the beginning, even if their interfaces are implemented later.

2. Don't impose a hard 10-report daily limit. A member could encounter multiple genuine safety incidents. Rate-limit suspected abuse instead, while always accepting urgent safety reports.

3. Let members see their own report history. A simple report status screen provides accountability without revealing private moderation decisions.

I would also distinguish between reporting a member and blocking a member. Blocking is an immediate personal safety control; reporting initiates a moderation investigation. Neither should require the other.

The above is suitable for Q30. Q32–Q35 can define moderator permissions, disciplinary actions, appeals and urgent escalation procedures without duplicating them here.

### Q31 — Photo storage, regional hosting and Photo Verification

ANSWERED — Owner, 25 October 2026

Swoovr will use Cloudflare R2 for storing member photographs, profile images, private albums and other user-uploaded media.

1. Cloudflare R2 is the approved object storage provider.
2. Storage is organised into regional buckets according to the member's registered home region.
3. Australian member media will initially be stored in an Australia-designated bucket, subject to verification of physical data residency.
4. As Swoovr expands into New Zealand and other countries, additional regional storage configurations will be introduced.
5. All media access is authorised through the Swoovr API, with private buckets and short-lived signed URLs where appropriate.
6. Private albums must never be publicly accessible through permanent URLs.
7. Media ownership, moderation flags, classifications, storage location and access permissions are recorded in the database.
8. Changing a member's registered country does not automatically migrate their stored media. Regional migration will be handled through a controlled process.
9. Media scanning, sensitive-content classification and Photo Verification are deferred to a later implementation slice.
10. Photo Verification will use a separate process for matching the member's live selfie against their profile photographs, rather than automatically treating Didit's document-to-selfie match as Photo Verification.

Cloudflare consideration: R2 provides free egress, but storage and Class A/Class B operations are billable beyond free allowances ([Cloudflare R2 pricing](https://developers.cloudflare.com/r2/pricing/)).

Cloudflare currently offers an `oc` (Oceania) location hint, but this is not a guarantee of Australian data residency. Its jurisdictional restrictions are separate from location hints. Swoovr must confirm that the chosen R2 configuration meets its Australian hosting requirement before storing production media ([Cloudflare R2 data location](https://developers.cloudflare.com/r2/reference/data-location/)).

### Q32 — Moderation actions, permissions and account restrictions

ANSWERED — Owner, 25 October 2026

Swoovr will use role-based moderation with separate permissions for support staff and administrators.

| Moderation action               | Support          | Admin |
| ------------------------------- | ---------------- | ----- |
| Review reports and evidence     | Yes              | Yes   |
| Dismiss reports                 | Yes              | Yes   |
| Issue warnings                  | Yes              | Yes   |
| Apply temporary restrictions    | Yes              | Yes   |
| Remove temporary restrictions   | Yes              | Yes   |
| Suspend accounts                | No               | Yes   |
| Terminate accounts              | No               | Yes   |
| Revoke staff affiliation        | No               | Yes   |
| Review staff-affiliated members | No               | Yes   |
| Handle appeals                  | Authorised cases | Yes   |

Rules:

1. Warnings notify members of inappropriate behaviour without immediately restricting their account.
2. Restrictions may last up to 30 days. Restricted members are removed from discovery and cannot initiate new connection requests, but retain access to existing connections unless the specific restriction prevents it.
3. Suspensions may last up to 90 days. Suspended members cannot access normal member functionality and receive a suspension notice with an appeal option.
4. Termination permanently disables the account, subject to the account lifecycle and retention policies.
5. Staff-affiliated accounts are subject to the same rules, but disciplinary decisions are handled exclusively by administrators.
6. All moderation actions require a reason, timestamp, acting staff identity and audit record.
7. Restrictions and suspensions must be enforced server-side across all clients, APIs and active sessions.
8. Support staff cannot override administrator-imposed restrictions or suspensions without appropriate authorisation.
9. Expired temporary sanctions are automatically lifted unless another active restriction applies.
10. Members have access to the appeals process defined in Q34.

Additional recommendation: moderation actions should support specific restrictions rather than only restricting the entire account. For example, temporarily preventing new connection requests or media sharing may be more appropriate than suspension for certain violations.

### Q33 — Staff-affiliated member accounts and report handling

ANSWERED — Owner, 25 October 2026

Swoovr employees may maintain ordinary personal member accounts. Employment does not grant additional account privileges or exempt members from platform rules.

1. Employee member accounts must use personal email addresses, not company-issued work email addresses.
2. Swoovr will periodically run an SQL audit to identify member accounts registered with company email domains.
3. Accounts identified through this audit will be reviewed with the employee directly.
4. Employees may request the public Staff affiliation badge through HR.
5. HR verifies employment eligibility and forwards approved requests to an administrator.
6. Only authorised administrators may set or remove the employee affiliation flag.
7. Staff affiliation does not provide administrative permissions, access to internal systems or special discovery privileges.
8. Employees must complete Photo Verification and ID Verification separately if they want those badges.
9. Reports against staff-affiliated member accounts are automatically directed to the administrator moderation queue, bypassing the normal support queue.
10. Support personnel cannot dismiss, resolve or impose disciplinary action on staff-affiliated accounts.
11. Administrators review those cases manually, applying the same platform rules and the higher conduct standards established in Q11.
12. No administrator may review or decide a case involving their own member account.
13. All affiliation changes and moderation decisions are audited.
14. If employment ends, administrators revoke the staff affiliation without affecting the employee's ordinary membership unless a separate moderation action is warranted.

The public Staff badge identifies employment affiliation only. It must not imply that the account is an official Swoovr customer-support channel.

### Q34 — Appeals, member notifications and urgent escalation

ANSWERED — Owner, 25 October 2026

Swoovr will support moderation appeals, in-app notifications and urgent incident escalation.

1. Members may submit one appeal per disciplinary action within 30 days of being notified.
2. Appeals must be reviewed by a different authorised moderator or administrator who was not responsible for the original decision.
3. Appeals involving staff-affiliated accounts are handled exclusively by administrators.
4. Appeal outcomes are final within Swoovr's standard internal process, without removing any external legal or regulatory rights.
5. Members receive in-app notifications for warnings, restrictions, suspensions, terminations and appeal outcomes.
6. Important notices may additionally be delivered by email.
7. Local development uses Mailpit until MailerSend is configured for transactional delivery.
8. Urgent reports, including threats of violence and suspected underage users, are prioritised and escalated immediately.
9. Urgent incidents generate notifications to a designated escalation contact or team, including outside normal business hours.
10. Escalation contact details are stored in secure environment configuration, initially `SWOOVR_ESCALATION_EMAIL`, and are not committed to source control.
11. Urgent reports remain open until acknowledged by an authorised reviewer.
12. All appeals, notifications, escalations and outcomes are recorded in the moderation audit history.

Additional requirement: Email alone should not be considered sufficient for critical incidents. The production system should support an acknowledged escalation mechanism, with secondary escalation if the primary contact does not respond.

### Q35 — Administration interface and local staff testing

ANSWERED — Owner, 25 October 2026

Swoovr will implement a dedicated administration web application, separate from the public/member-facing application.

1. Administration and moderation functionality will be provided through a dedicated Blazor Web App using server-side rendering.
2. The administration interface will have its own authentication boundary, using the dedicated Keycloak staff realm and Microsoft Entra integration when available.
3. Administrative operations remain server-side and communicate with protected administration API endpoints.
4. The member-facing `Swoovr.Web.Client` project must not contain administration or moderation screens.
5. The administration application will initially provide a moderation queue, case details, evidence review, moderation actions, appeals and member account management.
6. Subsequent functions include identity verification review, staff affiliation management, subscriptions, founding memberships, market configuration and operational dashboards.
7. Role-based access separates support personnel from administrators.
8. Administrative API endpoints must never be accessible through the member BFF.
9. Fictional local support and administrator accounts may be created in the local Keycloak staff realm for development and automated testing.
10. Password-based local test accounts must not be included in UAT or production realm configurations.
11. All sensitive administrative actions are authorised and audited server-side.
12. The administration application may use interactive components where necessary, but administrative business logic, credentials and privileged access remain server-side.

Architecture decision: A dedicated administration application is preferred over adding `/admin` routes to the member web application.

This allows independent deployment, tighter network restrictions, separate session policies and reduced exposure of administrative functionality.

### Q36 — Free and paid discovery filters (first version)

Editorial note, builder 25 October 2026: the owner answered Q36 twice on the same day. The two versions agree; the second ([Q36 — Discovery filters by mode and subscription tier](#q36--discovery-filters-by-mode-and-subscription-tier)) adds the defaults (excluded chips match any, visitors default to both) and is the one recorded in D137. Both are kept unchanged.


ANSWERED — Owner, 25 October 2026

Swoovr will provide useful discovery filters to every member while offering additional search precision and compatibility tools through paid subscription tiers.

The beta will include Free and Plus feature sets, allowing both experiences to be tested before paid subscriptions are activated.

#### 1. Standard filters — Free

Available across all modes:

- Age range.
- Intentions and profile chips, including required and excluded chips.
- Verification preference (ID Verified only).
- Local discovery within the member's active home market.
- Visitor visibility: current visitors, upcoming visitors, both or neither.

Dating also provides gender and reciprocal discovery preferences, subject to Q37.

Business, Friends and Activities use their own applicable intentions and profile attributes.

#### 2. Expanded filters — Plus

Plus provides more precise discovery and compatibility options.

| Feature                           | Free | Plus |
| --------------------------------- | ---- | ---- |
| Age range                         | Yes  | Yes  |
| Gender preferences in Dating      | Yes  | Yes  |
| Required/excluded intentions      | Yes  | Yes  |
| Verification preference           | Yes  | Yes  |
| Current/upcoming visitors         | Yes  | Yes  |
| Height range (Dating)             | —    | Yes  |
| Body type (Dating)                | —    | Yes  |
| Distance radius within market     | —    | Yes  |
| Lifestyle and interest filters    | —    | Yes  |
| Education and profession filters  | —    | Yes  |
| Advanced activity preferences     | —    | Yes  |
| Advanced compatibility filters    | —    | Yes  |
| Sort by compatibility             | —    | Yes  |
| Saved searches and filter presets | —    | Yes  |

Weight and other personal attributes may be considered later, subject to user disclosure preferences and the final profile schema.

#### 3. Filter behaviour

1. Every filter must respect the member's selected discovery mode.
2. Filters only use information voluntarily provided by members.
3. Missing or undisclosed attributes should not automatically exclude members unless the viewer explicitly requires that attribute.
4. Reciprocal eligibility, safety controls and market restrictions always take precedence over subscription filters.
5. Free members must retain meaningful discovery functionality; paid membership improves precision rather than granting access to otherwise prohibited members.
6. Compatibility ranking and advanced filtering must be treated as separate features.
7. Filter definitions and subscription entitlements must be configurable server-side rather than hardcoded into the clients.

#### 4. Beta access and subscription testing

1. Free and Plus functionality will both be available during beta.
2. Selected beta testers may receive complimentary Plus access through promotional entitlements, coupons or zero-cost subscriptions.
3. Stripe remains the payment and subscription provider.
4. Test subscriptions must exercise the same feature-entitlement rules as future paid subscriptions.
5. Beta testers must not be automatically charged when complimentary access expires.
6. Paid subscription activation and billing will be introduced separately from feature testing.
7. Founding members retain their previously approved three-month Plus entitlement and subsequent discount.
8. Beta testing will assess whether expanded filters provide meaningful value before final subscription pricing and limits are established.

#### 5. Compatibility

The expanded search functionality will eventually integrate with Swoovr's Contextual Weighted Compatibility Algorithm.

The initial beta can support shared intention and interest matching, while the advanced weighted compatibility engine is implemented in a later development slice.

Compatibility calculations must remain mode-specific. A relevant compatibility factor in Dating may have no relevance in Friends, Activities or Business.

### Q36 — Discovery filters by mode and subscription tier

**ANSWERED — Owner, 25 October 2026**

Swoovr will provide useful discovery filters to all members, with expanded filtering available to Plus and eventually Premium subscribers.

**Free tier — all modes:**

1. Age range.
2. Required intention chips (match any or all).
3. Excluded intention chips (match any).
4. ID Verified only.
5. Local discovery within the member's active market.
6. Visitor visibility: current visitors, upcoming visitors, both or neither. Default is both.

**Mode-specific filters:**

| Mode | Additional Free filters |
| --- | --- |
| Dating | Gender and reciprocal "Show me" preferences |
| Friends | Friendship intentions and shared interests |
| Activities | Activity categories and interests |
| Business | Business intentions and relevant interests |

**Plus — expanded filters:**

1. Distance radius within the permitted discovery market.
2. Height and body type filters for Dating.
3. Additional lifestyle, interests and activity filters.
4. Education and profession filters where relevant.
5. Advanced compatibility filtering and sorting.
6. Saved searches and filter presets.
7. Additional mode-specific filtering as profile fields become available.

Premium may introduce additional features later, but will not be required for the initial beta.

All filters are mode-specific, enforced server-side and respect privacy, blocking, market boundaries and reciprocal discovery rules.

Missing profile attributes are not automatically excluded unless a filter specifically requires that information.

During beta, selected testers will receive Plus entitlements through complimentary subscriptions, promotional codes or administrative grants. A zero-cost subscription may be used where appropriate. Beta testers will not be automatically charged after their complimentary access expires.

The subscription entitlement system must support both complimentary and paid access without separate implementations.

### Q37 — Gender, orientation and reciprocal Dating preferences

**ANSWERED — Owner, 25 October 2026**

Swoovr is designed for people of all genders and sexual orientations.

Gender identity, sexual orientation and attraction preferences are separate profile attributes.

**Gender options:**

1. Man.
2. Woman.
3. Non-binary.
4. Another gender (with an optional custom description).
5. Prefer not to say.

Members may optionally specify a more detailed gender identity. Pronouns are stored separately and are optional.

**Sexual orientation:**

Sexual orientation will be an optional profile field, using a configurable list that includes heterosexual, gay, lesbian, bisexual, pansexual, asexual, queer, questioning, another orientation and prefer not to say.

Orientation may be displayed on the member's profile at their discretion. It must not be used to infer who a member wants to see.

**Dating discovery:**

1. Members select one or more genders they are interested in seeing.
2. Dating discovery is reciprocal. Both members must satisfy one another's discovery preferences before appearing as eligible matches.
3. Gender and attraction preferences apply only to Dating.
4. Friends, Activities and Business do not use gender or sexual orientation to determine discovery eligibility.
5. Members control whether their gender identity, orientation and pronouns are publicly displayed.
6. Withholding public display does not automatically prevent a member from participating in reciprocal Dating discovery if the required matching preferences have been provided privately.
7. Gender and orientation options must be data-driven and extensible without application releases.

Preferences must not override explicit blocking, safety restrictions or other discovery eligibility requirements.

### Q38 — Age display and age filtering

**ANSWERED — Owner, 25 October 2026**

Swoovr is an adults-only platform. All active members must be at least 18 years old.

1. Members provide their date of birth during registration.
2. Date of birth is never displayed publicly.
3. A member's calculated age is displayed on discovery cards and profiles by default.
4. Members may hide their public age.
5. Hiding age does not remove the member from age-filtered discovery because the system still knows their age.
6. Age filtering uses the member's privately stored date of birth, not whether their age is publicly displayed.
7. Members with missing or invalid dates of birth cannot participate in discovery until corrected.
8. Where ID verification provides a verified date of birth, this replaces the self-declared date for eligibility and age calculations.
9. Members under age review remain restricted until the review is resolved.
10. Date of birth changes require appropriate validation and may trigger administrative review.

Age filtering must never return members outside the selected age range merely because they have hidden their age.

Date of birth is self-declared unless independently verified. Mandatory age assurance remains separate from optional ID Verification.

### Q39 — Intention chips, filtering and compatibility

**ANSWERED — Owner, 25 October 2026**

Swoovr distinguishes between intention chips, discovery filters and compatibility calculations.

**Intention chips:**

1. Chips describe a member's interests, intentions and preferences within a specific mode.
2. Selecting a chip does not automatically exclude other members.
3. The approved forty-chip vocabulary replaces the earlier intention vocabulary.
4. Members may have different chips across Dating, Friends, Activities and Business.

**Discovery filtering:**

5. Required chips allow members to specify intentions they want prospective connections to possess.
6. Required chips support matching any or all selected values.
7. Excluded chips prevent discovery of members possessing explicitly excluded intentions.
8. Explicit exclusions are reciprocal: where a member excludes an intention, members with that intention are not eligible to discover or initiate contact with them through that mode.
9. Required chips are applied to the requesting member's discovery results. They do not automatically become reciprocal exclusions unless the member has explicitly configured that restriction.
10. All eligibility rules are enforced by the API, including direct profile access and connection requests.

**Compatibility:**

11. Swoovr will develop its own Contextual Weighted Compatibility Algorithm.
12. Compatibility is calculated independently for each mode.
13. The initial beta may use shared chips and interests as a preliminary compatibility indicator.
14. The full compatibility model will incorporate weighted questions, member preferences, shared values and category-specific importance.
15. Members may eventually receive an overall compatibility percentage and a category breakdown.
16. Missing answers must not be treated as incompatible answers.
17. Compatibility scores are advisory and do not automatically exclude people unless explicit filters or safety rules require exclusion.
18. Discovery may use compatibility as a ranking factor alongside relevance, activity and other permitted signals.

The initial chip-based implementation must not prevent the later weighted compatibility engine from being introduced.

### Q40 — MAUI navigation and badge consistency

**ANSWERED — Owner, 25 October 2026**

Swoovr will use consistent primary navigation across iOS, Android and Web.

The primary navigation consists of:

1. **Explore** — Local discovery, discovery modes and eligible profiles.
2. **Connections** — Connection requests, accepted connections and relationship management.
3. **Chats** — Member conversations and messaging.
4. **Me** — Member profile, albums, settings, subscriptions, verification and Travel Mode management.

Additional rules:

5. Chats may initially display a not-yet-available state until messaging is implemented.
6. Travel planning is accessible through Me, while destination discovery is accessed through Explore.
7. Profile editing and account settings belong under Me.
8. The navigation structure must remain consistent between MAUI and the Blazor Web App.
9. Platform-specific visual adaptations are permitted without changing the underlying information architecture.
10. All verification, Founder, Staff and Bot badges must use the same approved shapes, colours and meanings across clients.
11. Shared design tokens and reference artwork must be maintained to prevent visual inconsistencies.

### Q41 — Traveller Search

**ANSWERED — Owner, 25 October 2026**

Traveller Search will be implemented in the current development run.

1. Traveller Search operates within an enabled destination market.
2. The member must have an active or upcoming trip associated with that market.
3. Pre-arrival discovery begins no earlier than 14 days before the declared arrival date.
4. Before arrival, travellers may browse eligible destination profiles but cannot initiate connection requests, messages or save destination members to a personal shortlist.
5. Destination locals may discover and initiate contact with eligible upcoming travellers, subject to the existing travel and reciprocal discovery rules.
6. After arrival is established, travellers may initiate connections under the normal eligibility rules.
7. Traveller Search respects blocking, profile visibility, mode compatibility, safety restrictions and the destination market's enabled status.
8. Members cannot select arbitrary remote markets without creating a corresponding trip.
9. Cancelling or ending a trip removes the associated destination discovery permissions without deleting existing connections.
10. Basic Traveller Search is included in Free during beta.
11. Plus may later offer extended travel discovery filters and planning features, but cannot bypass arrival or market restrictions.

Traveller Search must not function as an unrestricted global member directory.

### Q42 — Messaging, conversations and retention

**ANSWERED — Owner, 25 October 2026**

Lift D112 and implement real messaging after slices O–T.

**Initial implementation:**

1. Real-time messaging using SignalR.
2. Text-only messages initially.
3. One conversation per connected member pair, shared across modes.
4. Messaging requires an accepted connection unless a separately authorised system interaction applies.
5. No attachments, photographs, voice messages or videos in the initial implementation.
6. No read receipts initially.
7. No public last-online status or typing activity exposed outside the conversation.
8. Members may delete a message for both participants within five minutes of sending it.
9. Members may hide messages or conversations from their own view separately from deleting them for both participants.
10. Members can report an individual message or an entire conversation.
11. Official bots may respond only within member-initiated conversations or separately authorised system interactions and must always identify themselves as bots.

**Blocking and restrictions:**

12. Blocking immediately prevents new messages between the affected members.
13. Existing conversation history becomes inaccessible to the blocking relationship through normal member interfaces.
14. Unblocking does not automatically restore the connection or permission to message.
15. Suspended and terminated accounts cannot participate in messaging.
16. Server-side eligibility checks apply to every message submission.

**Privacy and retention:**

17. Messages are retained while the relevant accounts remain active, subject to deletion, moderation and retention rules.
18. A reported message and a limited amount of relevant surrounding context may be preserved as moderation evidence.
19. The initial context limit is the reported message and up to twenty preceding messages, with additional evidence access requiring authorised escalation and auditing.
20. Moderators cannot freely browse private conversations.
21. Only authorised personnel may access reported message evidence through an active moderation case.
22. Message deletion does not remove evidence already preserved for a legitimate moderation investigation.
23. Account deletion triggers the applicable message deletion and retention policies defined under Q43.
24. Message history, deletion and moderation evidence must be handled consistently across all clients.

Messaging must be designed to support attachments and private-media sharing later without requiring a complete conversation-system rewrite.

### Q43 — Account deletion, data export and retention

**ANSWERED — Owner, 25 October 2026**

Account deletion and data export will be implemented in a later development slice, but both must be completed before public launch and any externally distributed beta requiring compliance with applicable platform policies.

**Account deletion:**

1. Members may request deletion directly from account settings.
2. Deletion requests require reauthentication or equivalent account-owner confirmation.
3. The account is immediately deactivated and removed from discovery.
4. A 30-day recovery period applies by default.
5. Members may cancel deletion during the recovery period after appropriate authentication.
6. Members must also have an option to request immediate permanent deletion rather than being forced to wait 30 days, subject to necessary processing time and lawful retention obligations.
7. At final deletion, the application removes or anonymises personal profile data, photographs, albums, ordinary messages, location information and other associated personal information.
8. The associated Keycloak identity is deleted and active authentication sessions are revoked.
9. Connected third-party records are deleted or disconnected where applicable.
10. Active subscriptions and billing entitlements are handled appropriately, with clear cancellation instructions where store-managed billing cannot be cancelled automatically.

**Founding membership recovery:**

11. Founding membership status is permanent and may be recovered after account deletion using the approved recovery-token mechanism.
12. The member receives their Founder recovery token before deletion is finalised.
13. Recovery tokens are generated using the previously approved HMAC-SHA256 mechanism.
14. The token permits restoring the original Founding Member entitlement, not allocating a second founding position.
15. Previously assigned founding positions remain reserved and must never be reassigned merely because a member deletes their account.

**Data retention:**

16. Moderation evidence and security audit records may be retained where necessary for legitimate legal, safety or security purposes.
17. A proposed retention period of up to two years for qualifying moderation and audit records is subject to legal and privacy review.
18. Ordinary deleted account content must not be retained merely because it could theoretically be useful later.
19. Retained records must be access-controlled, minimised and excluded from ordinary member-facing systems.
20. Backup expiry and deletion must be covered by the formal data-retention policy.

**Data export:**

21. Members may request an export of their personal account data.
22. Exports will include profile information, preferences, connections and other eligible account records.
23. Data exports must not disclose private information belonging to other members beyond what the requesting member is entitled to receive.
24. Exports are generated securely, with time-limited download access.
25. Data export and deletion requests are audited.

The account lifecycle must distinguish between temporary deactivation, suspension, deletion pending completion and permanently deleted accounts.

---

Important decisions changed from Claude's defaults:

- Q37: Sexual orientation is included as an optional profile attribute. It is relevant to Swoovr, but must remain separate from reciprocal attraction preferences.
- Q38: Date of birth is required privately; only public age display is optional. Hidden ages should still participate in age filtering.
- Q39: The chip system is not the final compatibility algorithm. Weighted scoring remains part of the planned product.
- Q42: Moderation evidence access is restricted and audited, and deletion for both parties is distinct from hiding a conversation.
- Q43: Account deletion needs an immediate-deletion option and cannot simply be postponed until after public launch. Apple requires in-app account deletion, while Google Play additionally requires an accessible web deletion route.

## Follow-up questions from the answers — 25 October 2026

Raised by the builder while recording Q24–Q43 (D125–D146). Earliest blocker first. Each has a recommended default; a reversible default is built if no answer arrives. Provider values never go in this file or in chat.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q44 | ANSWERED (webhook route: Q50) | P (Didit ID verification) | For the Didit account: is there a sandbox or test mode, and which workflow should Swoovr use (ID document + liveness + face match)? Will you put the API key, the webhook secret and the workflow id in `.local/docker.env` as `SWOOVR_DIDIT_API_KEY`, `SWOOVR_DIDIT_WEBHOOK_SECRET` and `SWOOVR_DIDIT_WORKFLOW_ID`? How should Didit's webhook reach a local machine: a tunnel you run (for example `cloudflared tunnel --url http://127.0.0.1:5080`) with its URL set in the Didit console, or polling the session result instead of webhooks locally? Has Didit confirmed the 30-day retention and the data region for Australia, and the price per check? | Without keys nothing can be tested against Didit; a webhook needs a public HTTPS URL; retention and cost were assumptions in Q27 and Q28 | Keys in `.local/docker.env` under those names, never committed. Locally, the API also re-reads the session result from Didit when the member returns, so verification works without a tunnel; the webhook is tested with signed fixtures and, when you run a tunnel, live. Until keys exist the slice builds against a clearly labelled simulated provider that never shows "verified" as real || See [Q44](#q44--didit-test-mode-workflow-keys-and-webhooks) |
| Q45 | ANSWERED (provider: Twilio, 25 October 2026) | Later (registration); affects P | Q26 requires a verified mobile number by SMS before an account is active. Which SMS provider (for example Twilio Verify, MessageBird, AWS SNS) and which Keycloak approach (a Keycloak SMS authenticator extension, or the API sending codes)? Does it apply to people who sign up with Apple or Google, and to the existing beta testers? What does it cost per message in Australia? | It's a new registration gate, a new provider and a recurring cost; Keycloak has no built-in SMS | Its own slice after U: a provider you choose, codes sent by the API (not a Keycloak extension), required for every member including Apple/Google sign-ups, and existing testers asked on their next visit. Until then accounts activate as today || See [Q45](#q45--mobile-number-verification) |
| Q46 | ANSWERED | T (Traveller Search) | Q24 rule 20 and Q41 say a traveller may initiate contact once "arrival is established under the existing location-verification rules", and rule 26 says the trip dates alone are not enough. No location check exists yet. What counts as arrival: a device location check in the app inside the destination market (web can't unlock), a check-in the member confirms, or the arrival date? | Decides whether slice T can unlock traveller-initiated contact at all, and whether the app needs location permission | Arrival = a device location check from the mobile app inside the destination market's area, on or after the arrival date, kept for that trip only; the location itself isn't stored, only "arrived at" and the market id. Until it's built, travellers can browse but not initiate || See [Q46](#q46--traveller-arrival-verification-and-contact-eligibility) |
| Q47 | ANSWERED (IP location source: Q51) | O (does not block) | Q24 rule 34 forbids an artificial home location, but a home city is self-declared. Is limiting changes enough for the beta, or do you want stronger assurance (a device location check when the home city enters a market, or admin review of home-market changes)? | Without some check a member can declare an Adelaide home and browse Adelaide locally | Slice O builds a limit: the home city can move into a different market once every 30 days, and each move is audited with market ids only. Device-location assurance arrives with Q46's check, applied to home cities as well || See [Q47](#q47--home-city-changes-and-location-abuse-prevention) |
| Q48 | ANSWERED | R (Plus filters in the beta) | Q36 gives selected beta testers Plus. For the first build, may an admin grant a complimentary Plus entitlement with an end date through the admin app, with Stripe test-mode subscriptions added later? If Stripe is wanted now: which Stripe account, and will the test-mode keys go in `.local/docker.env` as `SWOOVR_STRIPE_SECRET_KEY` and `SWOOVR_STRIPE_WEBHOOK_SECRET`? | Decides whether slice R needs a Stripe account and webhooks | Admin-granted complimentary entitlements first (same entitlement records a paid plan will use, D90), Stripe test-mode subscriptions in a later payment slice || See [Q48](#q48--beta-plus-access-through-administrative-grants) |
| Q49 | ANSWERED | Q (reports and moderation) | Your Q30 answer is marked "Recommended answer — Pending owner approval". Do you approve it as written, including report targets that don't exist yet (messages, photos, albums), the 2,000-character limit and no fixed daily cap? | Slice Q builds it | Approved as written; targets that don't exist yet are in the data model but not shown until those features exist || See [Q49](#q49--approve-the-q30-reporting-answer) |

### Q44 — Didit test mode, workflow, keys and webhooks

**ANSWERED — Owner, 25 October 2026**

Didit has a test mode, and Swoovr uses it until launch. The testing workflow is Didit's KYC workflow that matches the ID document against the member's face as they move closer to the camera. Its id, the API key and (once created) the webhook secret are in `.local/docker.env` as `SWOOVR_DIDIT_WORKFLOW_ID`, `SWOOVR_DIDIT_API_KEY` and `SWOOVR_DIDIT_WEBHOOK_SECRET`; they are never committed.

1. Creating a session returns a link (or a QR code). The member opens the link, and the session is created and stored against the member's account in the API so the session and its result belong to that member.
2. Verification happens on Didit's platform, in the member's external browser. Swoovr doesn't hold or maintain the verification data.
3. The account has 500 scans a month. Didit keeps the data for 30 days.
4. Each verification costs between 0.00 and 0.23 (currency as billed by Didit), charged to Swoovr's account, never to the member.
5. Swoovr's privacy policy and terms are linked from Didit's verification pages (before launch; test mode now).
6. The webhook isn't set up yet. The owner offered a server reachable by SSH for testing; see [Q50](#follow-up-questions-from-q44q49--25-october-2026).

Didit's integration and webhook notes, as supplied by the owner from the Didit console, are kept in [DIDIT-INTEGRATION.md](../architecture/DIDIT-INTEGRATION.md).

Editorial note, builder 25 October 2026: the workflow id was removed from this file; it lives only in `.local/docker.env`.

### Q45 — Mobile number verification

**ANSWERED — Owner, 25 October 2026.** The provider was settled the same day as Twilio ([Q50–Q52](#answers-to-q50q51--25-october-2026)).

Swoovr will generate and validate its own six-digit OTP codes through the API, using a third-party messaging provider solely for SMS/WhatsApp delivery.

Implement a provider-independent `ISmsSender` / `IVerificationMessageSender` abstraction, starting with SMS and adding WhatsApp support later.

Members enter the received six-digit code into the mobile or web application to complete verification. The API validates the code and updates the member's mobile verification status.

**Security requirements:**
- Five-minute code expiry.
- Maximum five incorrect attempts.
- Resend cooldowns and rate limiting by account, phone number and IP address.
- Single-use verification codes.
- Store a keyed hash of the OTP, never the plaintext code.

Use a separate verification record containing the phone number, keyed code hash, expiry, failed-attempt counter and delivery status. The OTP must not be stored directly on the member profile.

Mobile verification is required once per account, with re-verification only when the phone number changes or a security review requires it.

~~**SMS provider selection remains open.**~~ Twilio, owner 25 October 2026 (see below).

### Q46 — Traveller arrival verification and contact eligibility

**ANSWERED — Owner, 25 October 2026**

A traveller is considered **arrived** when their declared trip has started and Swoovr has sufficient evidence that they are physically present within the destination market.

Arrival is determined by the API, not simply by the member's declared arrival date.

**Rules:**

1. A trip must have a valid destination, arrival date and departure date within an enabled market.
2. Before arrival, travellers may browse eligible destination profiles but cannot initiate connection requests or conversations with destination locals.
3. Arrival requires both:
   - The declared arrival date/time has been reached.
   - The device's GPS location confirms presence within the destination market.
4. Once confirmed, the trip status changes to `Arrived`, enabling traveller-initiated connections under normal discovery rules.
5. The API validates location and trip eligibility. Client-provided arrival flags are never trusted.
6. GPS permission is requested when the member attempts to confirm arrival, rather than requiring continuous background tracking.
7. If GPS is unavailable, denied or inconclusive, the trip remains `Upcoming` or `ArrivalUnconfirmed`. The member may request manual review through support.
8. IP geolocation alone is insufficient to confirm arrival.
9. A member cannot bypass arrival verification by changing their home city, modifying trip dates or manually selecting a destination.
10. Members can confirm arrival again if their location status becomes invalid or requires reassessment.
11. Arrival verification records the confirmation timestamp, destination market and verification method. Precise GPS coordinates need not be retained.
12. Once the trip ends, traveller-specific discovery and contact initiation permissions expire. Existing accepted connections and conversations remain available.
13. Multiple trips are evaluated independently.
14. Normal blocking, moderation, age, mode and reciprocal discovery restrictions continue to apply.

**Location privacy:**

- Exact device coordinates are never exposed to other members.
- Location is processed server-side to determine market eligibility.
- Continuous GPS tracking is not required.
- Arrival confirmation does not automatically publish the member's precise location.
- A successfully confirmed traveller may display an approximate status such as "Visiting Adelaide".

**Implementation:** Use an explicit `ConfirmArrival` API operation that validates the trip, checks a fresh device location reading against the destination market boundary, and records the arrival confirmation. Subsequent requests use the server-side confirmed status, subject to trip expiry and safety checks.

**Core principle:** A declared trip permits limited pre-arrival discovery. Confirmed physical presence permits initiating connections. Neither grants unrestricted access to other markets.

One recommendation: Don't require repeated GPS verification for every connection request. Confirm arrival once, store its validity against the trip, and enforce that authorisation until the trip expires or is revoked. This avoids unnecessary friction and location tracking.

Editorial notes, builder 25 October 2026:

- Rule 2 matches Q41 (D142): pre-arrival browsing is Traveller Search only; there is still no feed of locals for travellers.
- A device location comes from the member's device, so a modified device can report a false position. The API treats it as evidence checked against the trip, the date and the market's areas, not as proof; the Q47 abuse checks apply.
- "GPS" includes the browser's location permission on the web as well as the app's, so the web isn't locked out (DEFAULT, D152).

### Q47 — Home city changes and location abuse prevention

**ANSWERED — Owner, 25 October 2026**

A 30-day restriction on changing home cities is a useful anti-abuse measure, but is not sufficient on its own to prevent members from falsely claiming to live in Adelaide or another active market.

1. Members select their home city during registration.
2. The API uses available location signals, including GPS and approximate IP geolocation, to identify potential discrepancies.
3. Members may change their home city once every 30 days.
4. Changing home city does not automatically establish local residency or grant access to a new market.
5. Where GPS is available, the API may validate that the member is physically present within their selected home market.
6. Members whose declared home city conflicts with available location evidence may be flagged for review or classified as travelling rather than automatically treated as local residents.
7. GPS permission is not mandatory. Members without GPS access may use their declared city, subject to additional abuse detection and review.
8. Repeated location changes, suspicious activity or attempts to bypass market restrictions may trigger administrative review.
9. Administrators may override the 30-day restriction for legitimate relocations or incorrect location information.
10. Travel Mode remains the approved mechanism for accessing other markets temporarily.

**Decision:** Retain the 30-day home-city change limit, supplemented by location consistency checks, server-side market eligibility and abuse detection. Location discrepancies alone must not automatically result in suspension or account termination.

Editorial note, builder 25 October 2026: rule 2's IP geolocation needs a source; see [Q51](#follow-up-questions-from-q44q49--25-october-2026). The home city stays on the profile (required before discovery, as built in slice O) rather than in the Keycloak registration form (DEFAULT, D152).

### Q48 — Beta Plus access through administrative grants

**ANSWERED — Owner, 25 October 2026**

Yes. Beta testers may receive Swoovr Plus through an **administrative entitlement grant**, without requiring Stripe integration, payment details or a subscription.

**Rules:**

1. Authorised administrators can grant Plus access directly through the administration interface.
2. Grants are recorded in the database as subscription entitlements, independently of Stripe.
3. Each grant records the member, subscription tier, activation date, expiry date, grant reason and issuing administrator.
4. Administrators may extend, revoke or change granted entitlements.
5. The API determines available features from the member's effective entitlement, regardless of whether access comes from an administrative grant, promotion, founding membership or paid subscription.
6. Beta grants do not create Stripe customers, subscriptions or payment obligations.
7. Expired or revoked grants automatically remove Plus access unless another valid entitlement exists.
8. Members revert to Free when no active paid or complimentary entitlement remains.
9. Members must never be automatically charged when a complimentary grant expires.
10. All entitlement changes must be audited.
11. The administration interface must distinguish complimentary grants from paid subscriptions.
12. Stripe integration can be introduced later without changing how the application checks subscription entitlements.
13. Founding membership entitlements remain separate from ordinary beta grants and retain their previously approved benefits.
14. Where multiple entitlements overlap, the API applies the highest currently valid tier without duplicating benefits or extending unrelated entitlements.

**Implementation:** Build a central entitlement service that resolves a member's effective subscription tier. Discovery filters and other Plus features must use this service rather than checking Stripe subscription records directly.

**Decision:** Administrative Plus grants are approved for beta testing before Stripe integration.

### Q49 — Approve the Q30 reporting answer

**ANSWERED — Owner, 25 October 2026**

Q30 is approved as written. D131 is CONFIRMED.

## Follow-up questions from Q44–Q49 — 25 October 2026

Raised by the builder while recording Q44–Q49 (D147–D152). Neither blocks a build; each default is reversible.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q50 | ANSWERED | P (live webhook test only) | How should Didit's webhook reach the API while testing: a tunnel from this Mac, or a test server reached over SSH? | Didit only calls a public HTTPS URL. A server means deployment work this run hasn't been authorised for; a tunnel doesn't | A tunnel, no server: `brew install cloudflared`, then `cloudflared tunnel --url http://127.0.0.1:5080` while the stack runs; in the Didit console add a webhook destination `https://<tunnel host>/api/v1/webhooks/didit` subscribed to `status.updated` and `data.updated`, and put its secret in `.local/docker.env` as `SWOOVR_DIDIT_WEBHOOK_SECRET`. A quick tunnel's host changes on every run, so a named tunnel is better if testing repeats. Until then slice P reads the session result from Didit when the member returns and tests the webhook with locally signed, replayed and forged payloads || See [answers](#answers-to-q50q51--25-october-2026) |
| Q51 | ANSWERED | T (home-city consistency checks) | Q47 rule 2 uses approximate IP geolocation. Which source: Cloudflare's visitor location headers (city level, no extra service, only where traffic passes through Cloudflare), a licensed IP database such as MaxMind GeoLite2, or none for the beta? | A database is a new provider with its own licence and update job; headers only exist in hosted environments | Cloudflare visitor location headers in hosted environments, nothing locally; the IP is never stored, only "consistent" or "inconsistent" with the home market || See [answers](#answers-to-q50q51--25-october-2026) |
| Q52 | ANSWERED | SMS slice (later; doesn't block P–U) | Where will the Twilio test credentials go? Recommended: `.local/docker.env` as `SWOOVR_TWILIO_ACCOUNT_SID`, `SWOOVR_TWILIO_AUTH_TOKEN` and `SWOOVR_TWILIO_MESSAGING_SERVICE_SID` (or `SWOOVR_TWILIO_FROM` for a single sender number). Which sender for Australia: an alphanumeric sender id ("Swoovr", needs registration) or a Twilio number? | Twilio needs an account, a registered sender and a per-message cost before any code can be sent | Those names, never committed. Twilio Programmable Messaging (not Twilio Verify, because Swoovr generates and checks its own codes, D148), sender "Swoovr" once registered, a Twilio number for testing || Owner, 25 October 2026: the test credentials go in `.local/docker.env` once the Twilio account is set up (names as recommended, `SWOOVR_TWILIO_FROM` for the number). A Twilio phone number is the sender while prototyping; the "Swoovr" sender id isn't registered yet |

### Answers to Q50–Q51 — 25 October 2026

**Q50 — ANSWERED, owner 25 October 2026.** A Cloudflare quick tunnel from the owner's Mac (`cloudflared tunnel --url http://127.0.0.1:5080`). The Didit test console has a webhook destination at `https://<tunnel host>/api/v1/webhooks/didit`, webhook version V3.0, subscribed to `status.updated` and `data.updated`. Its secret is in `.local/docker.env` as `SWOOVR_DIDIT_WEBHOOK_SECRET`. The tunnel host isn't recorded here: a quick tunnel gets a new host each time it starts, and the Didit destination must then be updated.

Editorial note, builder 25 October 2026: a POST through the tunnel reached the local API (404, since the route arrives with slice P).

**Q51 — ANSWERED, owner 25 October 2026.** Use Cloudflare's visitor location headers in hosted environments; no IP-based geolocation locally. The approximate country or city is used only for location consistency checks. Neither the IP address nor the location derived from it is stored permanently.

**Q45 provider — ANSWERED, owner 25 October 2026.** Twilio is the first SMS provider for six-digit codes, behind a provider-independent `IVerificationMessageSender`, so the provider can change and WhatsApp can be added later. Swoovr generates the codes, stores them as a keyed hash, checks them and expires them itself.

## Questions raised while building P–U — 25 October 2026

Raised by the builder during the overnight run (D155 onwards). None blocks the build; each recommended default is built and reversible.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q53 | ANSWERED | Nothing now (R reads it) | D139 says a verified date of birth from ID verification replaces the declared one; D128 says Swoovr stores only the outcome, never document data. Should the date of birth from Didit be stored on the profile? | Storing it is document data; not storing it means an ID-verified member can still have a declared date of birth that differs from their document | Don't store it (built). Keep only adult yes/no; when the declared age and the document disagree by more than a year, flag the account for review rather than overwriting. Didit keeps the full result for 30 days (D147) | ANSWERED, owner 26 October 2026 (D158): compare the date of birth from Didit with the profile's and mark the date of birth verified when they match; when they don't, just don't mark it verified |
| Q54 | ANSWERED | Nothing (production gate) | Which address should receive urgent-report escalations (`SWOOVR_ESCALATION_EMAIL`, D135), and who is the secondary contact? | Locally the hosts fall back to a fictional address caught by Mailpit; production needs a real, acknowledged contact pair | Add `SWOOVR_ESCALATION_EMAIL` to `.local/docker.env` now (Mailpit catches it locally); decide the production pair before launch | ANSWERED, owner 26 October 2026 (D159): the RePass Cloud contact address is both the urgent-escalation contact and the backup (held in `.local/docker.env` as `SWOOVR_ESCALATION_EMAIL` and `SWOOVR_ESCALATION_BACKUP_EMAIL`, D135) |
| Q55 | ANSWERED (superseded by Q132 B, D325) | Nothing now | While restricted (not suspended), may a member still browse Explore and answer existing requests? | D133 lists "out of discovery, no new requests, existing connections kept"; it doesn't say whether they may still look | Yes (built): a restriction removes them from others' discovery and blocks what it names; browsing and existing connections continue | ANSWERED, owner 26 October 2026 (D160): no. Restricted members can't do anything in the app and see a restricted screen until the restriction is removed; a restriction is most likely a safety and security concern |
| Q56 | ANSWERED | Nothing now | Members created before slice R have no date of birth and drop out of discovery (and of connected members' profile pages) until they add one. Should existing connections still open each other's profiles meanwhile? | D139 says a missing date of birth keeps a member out of discovery; profile pages for existing connections are a grey area | Keep them out until corrected (built) and show the "Add your date of birth" notice; revisit if beta members complain | ANSWERED, owner 26 October 2026 (D162): every member needs a date of birth, which calculates their age. They may hide their age on their profile, and still appear in results whose age range matches their age |
| Q57 | ANSWERED | Nothing now | How should "Prefer not to say" and "Another gender" behave in Dating's reciprocal "show me"? | D138 makes "show me" reciprocal but doesn't say whether "everyone" includes these | They match only people who include them explicitly; the web form's checkboxes list every gender, so "everyone" means ticking all five (built) | ANSWERED, owner 26 October 2026 (D161): "Prefer not to say" is a privacy choice, not a gender, and must not be a "show me" option; people who choose it stay discoverable unless other filters exclude them. "Another gender" is an inclusive "show me" category matching people who identify outside the main categories. Neither option limits discovery to people who chose the same option |

## Questions raised while building S–U — 26 October 2026

Raised by the builder on branch `feature/swoovr-app-parity-traveller-search` (D163 onwards), earliest blocker first. Where a default is reversible it is built; Q58 is not built because it amends a CONFIRMED decision.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q58 | ANSWERED | Issue [#33](https://github.com/repasscloud/wanderconnect/issues/33) only (date-of-birth reconciliation); nothing in S–U | When an approved ID check's date of birth differs from the account's, the member is shown "Document DOB" and asked to confirm the correction. D128 says Swoovr stores no document data and D158 drops the document's date after comparing. How should the document's date be held while the correction is pending: (a) not held: the API re-reads Didit's decision each time the member or support opens the correction (Didit keeps it 30 days, D147; reading a decision is not a new scan); or (b) held in a restricted, access-controlled table only while `PendingCorrection` or `Disputed`, deleted when resolved? | (a) keeps D128 intact but stops working after Didit's 30 days and needs Didit reachable to show the screen; (b) is document data in Swoovr's database, even if short-lived, and amends D128/D158 | (a), re-read at display time. If Didit no longer has the decision, the status falls back to Unverified (badge kept, DOB unverified) and support can allow another check (`allow-again`). The correction history (original and corrected account DOB, provider session reference, time, reason) goes in its own restricted table, never in `audit_entries` || ANSWERED, owner 27 October 2026 (D166): Swoovr keeps the date of birth on the account. When it doesn't match the date Didit passes through, a one-time prompt asks the member to set it to the date on their ID to complete verification; Swoovr then checks it against Didit again. If it still doesn't match, a ticket goes to the admin queue and the date of birth isn't verified: it goes to manual review, so the member can have ID_VERIFIED but not DOB_VERIFIED while an admin inspects it |
| Q59 | ANSWERED | Nothing (D165 built the default) | When a member deletes a message for both within five minutes, should its text be erased at once, or kept hidden for a while as moderation evidence (so a harasser can't send and delete to avoid reports)? | Rule 8 lets senders delete; rule 22 protects evidence only once preserved | Keep it hidden for 30 days, usable only through a report, then erase (`messages purge` by cron) || ANSWERED, owner 27 October 2026 (D167): keep everything on the system for 30 days so it can be retrieved if required. In the app the message appears deleted, but it stays in the back end so harassment and similar issues can be investigated |

## Product review backlog (Codex, 26 October 2026) — tracked as GitHub issues

The owner pasted Codex's product review and strategic roadmap on 27 October 2026. Each item is a GitHub issue labelled `product-review` (and `trust-and-safety` or `decision-needed` where relevant) for later; none is approved. Tracking issue with the review's summary and its ten recommended decisions: [#64](https://github.com/repasscloud/wanderconnect/issues/64).

| Issue | Topic | Status |
| --- | --- | --- |
| [#35](https://github.com/repasscloud/wanderconnect/issues/35) | Groups and communities | UNDECIDED |
| [#36](https://github.com/repasscloud/wanderconnect/issues/36) | Events, meetups and group activities | UNDECIDED |
| [#37](https://github.com/repasscloud/wanderconnect/issues/37) | Separate personal and professional identities (mode-specific profile views and cross-mode privacy) | UNDECIDED |
| [#38](https://github.com/repasscloud/wanderconnect/issues/38) | Connection lifecycle per mode (state machine) | UNDECIDED |
| [#39](https://github.com/repasscloud/wanderconnect/issues/39) | In-app voice and video calls | UNDECIDED |
| [#40](https://github.com/repasscloud/wanderconnect/issues/40) | Complete meeting-safety system | UNDECIDED |
| [#41](https://github.com/repasscloud/wanderconnect/issues/41) | Messaging quality and conversation management | UNDECIDED |
| [#42](https://github.com/repasscloud/wanderconnect/issues/42) | Business accounts and organisations | UNDECIDED |
| [#43](https://github.com/repasscloud/wanderconnect/issues/43) | Discovery when the local network is small (network density) | D270 DEFAULT implemented under the owner’s Phase0 queue; PR125 closes43 |
| [#44](https://github.com/repasscloud/wanderconnect/issues/44) | Explainable compatibility and preference management | UNDECIDED |
| [#45](https://github.com/repasscloud/wanderconnect/issues/45) | Places and Experiences system | UNDECIDED |
| [#46](https://github.com/repasscloud/wanderconnect/issues/46) | Recurring social interaction (event series, following organisers) | UNDECIDED |
| [#47](https://github.com/repasscloud/wanderconnect/issues/47) | Reputation and positive conduct signals (research first) | UNDECIDED |
| [#48](https://github.com/repasscloud/wanderconnect/issues/48) | Accessibility as a release requirement | D271 DEFAULT gate and automated coverage implemented; human/device sign-off remains OPEN before external UAT |
| [#49](https://github.com/repasscloud/wanderconnect/issues/49) | Notification and privacy controls | UNDECIDED |
| [#50](https://github.com/repasscloud/wanderconnect/issues/50) | Account standing, appeals and support transparency (self-service) | UNDECIDED |
| [#51](https://github.com/repasscloud/wanderconnect/issues/51) | Product analytics, experimentation and the beta scorecard | D272 DEFAULT bounded scorecard under the owner’s current Phase0 queue; historical D179 deferral superseded for this scope |
| [#52](https://github.com/repasscloud/wanderconnect/issues/52) | Commercial operations and transaction handling | UNDECIDED |
| [#53](https://github.com/repasscloud/wanderconnect/issues/53) | Trust, safety and privacy roadmap | UNDECIDED |
| [#54](https://github.com/repasscloud/wanderconnect/issues/54) | Decide the policy for private explicit albums against App Store and Google Play rules | UNDECIDED |
| [#55](https://github.com/repasscloud/wanderconnect/issues/55) | Adopt the Australian Online Dating Code as a design benchmark | UNDECIDED |
| [#56](https://github.com/repasscloud/wanderconnect/issues/56) | Platform-aware subscription billing (in-app purchase vs Stripe) in one entitlement service | UNDECIDED |
| [#57](https://github.com/repasscloud/wanderconnect/issues/57) | Formal privacy, application-security and app-store-policy review before unrestricted external beta | UNDECIDED |
| [#58](https://github.com/repasscloud/wanderconnect/issues/58) | Define each mode's purpose, success outcome and next improvements | UNDECIDED |
| [#59](https://github.com/repasscloud/wanderconnect/issues/59) | New experiences: Available for…, open invitations, shared planning, travel and professional introductions | UNDECIDED |
| [#60](https://github.com/repasscloud/wanderconnect/issues/60) | Proposed phased roadmap (Phase 0–4) for owner review | UNDECIDED |
| [#61](https://github.com/repasscloud/wanderconnect/issues/61) | Architecture: logical domains and early cross-cutting decisions | UNDECIDED |
| [#62](https://github.com/repasscloud/wanderconnect/issues/62) | Product principles: what Swoovr should deliberately avoid | UNDECIDED |

## Other tracked issues — 27 October 2026

| Issue | Topic | Status |
| --- | --- | --- |
| [#63](https://github.com/repasscloud/wanderconnect/issues/63) | Members suite: passkey registration callback escapes the test's route on fresh builds (bug, not caused by S–U) | OPEN |
| [#33](https://github.com/repasscloud/wanderconnect/issues/33) | Date-of-birth reconciliation (Q58) | BUILT in PR #34 (D166); close when the owner is satisfied |

## Questions before the V–X run — 27 October 2026

Raised by the builder on branch `feature/swoovr-deletion-export-mode-privacy` before the overnight run, earliest blocker first. Each is also a GitHub issue (labels `question`, `decision-needed` where it fits). Reversible defaults are built if no answer arrives and are labelled DEFAULT in DECISIONS.md.

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q60 | [#66](https://github.com/repasscloud/wanderconnect/issues/66) | ANSWERED | Everything in this run | Build tonight, in this order: V account deletion and data export (D144, with the founder-token notice from #22) → W cross-mode privacy (#37) and the per-mode connection lifecycle (#38) → X mobile number verification by SMS at registration (D148, D153)? | D145 puts deletion and export before any external beta; the review ranks #37 and #38 as the highest-priority architecture items | Yes, in that order. X is built with the labelled simulated sender unless Q66 says the Twilio keys are test credentials. Photos on R2 (D132) are not built tonight (Q70). || ANSWERED, owner 27 October 2026: recommended default accepted (D168) |
| Q61 | [#67](https://github.com/repasscloud/wanderconnect/issues/67) | ANSWERED | V (deletion) | Final deletion must delete the Keycloak user and revoke its sessions (Q43 rule 8), but the API has no Keycloak admin access today and must never mint tokens (D35). May the API use a confidential service-account client in the members realm for this? And how is re-authentication proved? | A new privileged credential; without it the Keycloak identity outlives the account | A client `swoovr-api-accounts` in the members realm with only realm-management `view-users` and `manage-users`, its secret in `.local/docker.env` as `SWOOVR_MEMBERS_API_ADMIN_SECRET`, created by `realms.mjs`; used only to sign the member out everywhere at the request and delete the user at final deletion. Re-authentication: requesting, cancelling or exporting needs a token whose `auth_time` is under 5 minutes old; the web and app send the member through Keycloak with `prompt=login` (password, passkey or a linked Apple/Google identity all count) and the API checks `auth_time`. || ANSWERED, owner 27 October 2026: recommended default accepted (D169) |
| Q62 | [#68](https://github.com/repasscloud/wanderconnect/issues/68) | ANSWERED | V (deletion) | How should the 30-day recovery period and final deletion behave in detail? | Q43 sets the rules; these are the behaviours members and their connections will see | (1) At the request the account becomes Pending deletion: out of discovery at once, every session signed out. Signing in again shows only a "Your account will be deleted on <date>" screen with Cancel deletion and Delete now (both need a fresh sign-in). (2) Pending requests by or to them are cancelled; accepted connections and the conversation show as "not available" to the other member and come back if they cancel. (3) `accounts purge` (cron, hourly; "Delete now" completes on the next run) removes the profile, identity, date of birth, modes, chips, filters, home city, trips, arrival and home-location records, grants, verification attempts (outcomes), connections and the member's message text (copies in moderation cases stay); the account row becomes a tombstone (id, Deleted, time) so audit entries and cases still point at an id; the other member sees "Deleted member" in an ended conversation and keeps their own messages. (4) Audit entries and moderation cases involving them are kept 2 years and then purged by the same command (DEFAULT pending legal review, D144). || ANSWERED, owner 27 October 2026: recommended default accepted (D170) |
| Q63 | [#69](https://github.com/repasscloud/wanderconnect/issues/69) | ANSWERED | V (deletion), partly | May suspended, restricted and terminated members delete their account, and should anything be kept to spot a terminated member registering again? | Stores require deletion for everyone; deletion must not become a way around a ban | Everyone may delete; open cases continue with their evidence. For terminated accounts only, keep a keyed hash (HMAC) of the verified email for 2 years; a new registration with the same address is flagged to the admin queue, never blocked automatically. Nothing is kept for other members. || ANSWERED, owner 27 October 2026: recommended default accepted (D171) |
| Q64 | [#70](https://github.com/repasscloud/wanderconnect/issues/70) | ANSWERED | V (export) | What goes in a member's data export, in what format, and how is it delivered? | Q43 rules 21–25: secure, time-limited, nothing private about other members | "Download my data" on the web account page and the app's Me tab, after a fresh sign-in. The API builds one JSON file on request and returns a one-time link valid 15 minutes; nothing is stored on disk; one export per 24 hours; both audited (ids only). Contents: account (email, created, state), profile, identity, date of birth, modes, chips, filters, home city, trips, connections and requests (the other member's display name, mode, status, dates), both sides of their own conversations (deleted messages as deleted, no text), reports they filed (category, date, status), notices and appeals about them, verification outcomes, entitlements, founder place number. Not included: reports about them or who filed them, other members' private fields, staff notes. || ANSWERED, owner 27 October 2026: recommended default accepted (D172) |
| Q65 | [#71](https://github.com/repasscloud/wanderconnect/issues/71) | ANSWERED | V (deletion) | At final deletion, should Swoovr ask Didit to delete the member's sessions? And how should the founder recovery token be handled (#22)? | Q43 rule 9 (third parties) and rules 11–15 (founders); #22 asks for disclosure and an option to delete the token | Yes: delete the member's Didit sessions through Didit's delete-session API (test mode, no charge); a failure is logged and retried on the next purge run. Founders: the recovery code is emailed at the request (D65); the deletion screen explains it is kept so founding status can be restored, with an unticked "Also delete my founder recovery token"; support can delete it later on request. Complimentary Plus grants end at final deletion; no paid subscriptions exist yet. || ANSWERED, owner 27 October 2026: recommended default accepted (D173) |
| Q66 | [#72](https://github.com/repasscloud/wanderconnect/issues/72) | ANSWERED | X (SMS) live sending only | `.local/docker.env` has `SWOOVR_TWILIO_ACCOUNT_SID`, `SWOOVR_TWILIO_AUTH_TOKEN` and `SWOOVR_TWILIO_FROM`, but the From number is a real +1 number, not Twilio's test number +15005550006, so they look like live credentials. Are they Twilio test credentials? May SMS be sent tonight, and to which number? | Live credentials cost money and deliver real messages; test credentials deliver nothing | Send nothing. X uses the labelled simulated sender, and the Twilio adapter is tested against a local fake HTTP server only. If they are test credentials: call Twilio with From +15005550006 and Twilio's magic To numbers only (nothing delivered, no charge). || ANSWERED, owner 27 October 2026: recommended default accepted (D174) |
| Q67 | [#73](https://github.com/repasscloud/wanderconnect/issues/73) | ANSWERED | W (#37) | Which profile fields, photos, identifiers and presence signals may cross between Dating, Friends, Activities and Business? | Review decision 1 (#64): a professional contact must never see dating details; expensive to retrofit | Cross every mode: display name, age (unless hidden), area, badges, gender and pronouns (under the member's show-gender switch), and the main photo once photos exist (the member picks other photos per mode). Per mode: an "about" per mode (falls back to the core about until set) and the intention chips (already per mode). Dating only: orientation and "show me". Business only, when #42 adds them: profession, company, website. A profile is opened in one mode and shows only that mode, plus modes in which the pair is already connected; it never lists the person's other modes. No online or last-active signals in any mode for the beta. Enforced by the API's per-mode profile view, not by hiding things in the UI. || ANSWERED, owner 27 October 2026: recommended default accepted (D175) |
| Q68 | [#74](https://github.com/repasscloud/wanderconnect/issues/74) | ANSWERED | W (#38) | What are the per-mode connection states, and how does disconnecting without blocking work? | Review decision 2 (#64); today the only way to end a connection is a block | Per mode: None → Requested → Connected, with Declined and Withdrawn (built), a new Disconnected, and Ended by a block (built, both members' modes). Either member may disconnect a mode without blocking: the other isn't notified and it leaves both members' Connections. The conversation stays open while any other mode connects them, otherwise it closes read-only (history kept under D143/D167). The member who was disconnected can't request again in that mode for 30 days; the one who disconnected may request again any time. Labels: Dating "Match", Friends "Friend", Activities "Activity partner", Business "Contact"; the same rules in every mode for now. Completed meetings wait for Plan a Date (#59). || ANSWERED, owner 27 October 2026: recommended default accepted (D176) |
| Q69 | [#75](https://github.com/repasscloud/wanderconnect/issues/75) | ANSWERED | Live Didit webhook test only | A named cloudflared service now runs (token-based, so the hostname isn't readable locally). Which public host is the Didit webhook destination now, and does it route to 127.0.0.1:5080? | The docs still describe the quick tunnel whose host changes per run | Change nothing in the Didit console; verification keeps working through the re-read on return (D152). With the host, update DIDIT-INTEGRATION.md and LOCAL-DEVELOPMENT.md to `https://<host>/api/v1/webhooks/didit` and test a live delivery. || ANSWERED, owner 27 October 2026: recommended default accepted (D177) |
| Q70 | [#76](https://github.com/repasscloud/wanderconnect/issues/76) | ANSWERED | Photos slice (not tonight) | Are the R2 credentials and a private bucket ready? And what is the decision on private explicit albums (#54)? | No R2 values are in `.local/docker.env`; albums must not be built before #54 | Photos aren't built tonight. When ready: `SWOOVR_R2_ACCOUNT_ID`, `SWOOVR_R2_ACCESS_KEY_ID`, `SWOOVR_R2_SECRET_ACCESS_KEY` and `SWOOVR_R2_BUCKET` (private bucket, no public access, Swoovr serves photos through authorised short-lived links). #54: no explicit content anywhere in the beta (profile photos and albums alike; nudity is moderated); private albums later only for non-explicit photos shared per connection, decided after re-reading both stores' current policies. || ANSWERED, owner 27 October 2026: recommended default accepted (D178) |
| Q71 | [#77](https://github.com/repasscloud/wanderconnect/issues/77) | ANSWERED | Planning only | Which review issues go into Phase 0 (before an external beta), and which wait? | The review's main risk is building too many interconnected products at once (#60) | Phase 0: #37 and #38 (tonight), #22 (with deletion), #41 (messaging consent and ending conversations, partly built), #43 (a decision on small pools), #48 (accessibility as a release gate), #49 (notification privacy, with push notifications), #50 (self-service account standing, partly built), #53 (the trust and safety roadmap), #54 (decision only), #55 (benchmark document), #57 (external review before an external beta). Later: #35, #36, #39, #40, #42, #44, #45, #46, #47, #51, #52, #56, #58, #59, #61, #62. #60 is accepted as the planning frame, not commitments. || ANSWERED, owner 27 October 2026: recommended default accepted (D179) |

## Questions raised while building V–X — 27 October 2026

Raised by the builder on branch `feature/swoovr-deletion-export-mode-privacy` during the overnight run. Each is a GitHub issue; the reversible default is built (D181).

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q72 | [#79](https://github.com/repasscloud/wanderconnect/issues/79) | ANSWERED | X (built with the default) | Should a mobile number be verifiable on only one live member account at a time? | Without it one number can back many accounts (ban evasion, spam); with it the refusal must not reveal whose account holds the number | Yes (built, D181): a unique index on the verified number; a second account gets 409 `phone-in-use` with the neutral "That number can't be used. Try another." The number is released when the holding account is deleted. Support can't move a number between accounts yet. || ANSWERED, owner 27 October 2026 (D184): one active account per number; a verified number is required to finish registration (no access without it); changing number needs the new one verified, which releases the old one; log that it changed, not which number (previous numbers kept in encrypted contact history, D187) |
| Q73 | [#80](https://github.com/repasscloud/wanderconnect/issues/80) | ANSWERED | X going live (nothing else) | The registration gate is built behind `Phone:Required` (default off), because the only sender allowed tonight is the simulated one (D174). When should it turn on, and when may the Twilio adapter send real messages (Sms:Provider=twilio with the keys in .local/docker.env)? | Turning the gate on with the simulated sender would lock every real member out; turning on Twilio costs money and texts real phones | Keep it off locally. Turn on Twilio and the gate together in UAT once the owner confirms the Twilio account (live or test credentials) and a test handset; existing members are then asked on their next visit (Q45). || ANSWERED, owner 27 October 2026 (D183): the number is the owner's Twilio-provided number; simulate for now and finalise live sending when deployed |
| Q74 | [#81](https://github.com/repasscloud/wanderconnect/issues/81) | ANSWERED | V (final deletion of Didit sessions; retries until then) | Final deletion now asks Didit to delete the member's sessions (`DELETE /v3/session/{id}/delete/`), which needs `delete:sessions` on the key. Does the current test key have it? | Without it each deletion keeps the attempt row (session id and outcome only) and `accounts purge` retries every hour | Check the key's permissions in the Didit console and add `delete:sessions`; until then deletion completes everything else and retries Didit. || ANSWERED, owner 27 October 2026 (D186): follow the internal trust and safety policy: Didit is the processor, delete its sessions at final deletion; check the key has `delete:sessions` |

Feedback to factor in later:

| Issue | Topic | Status |
| --- | --- | --- |
| [#82](https://github.com/repasscloud/wanderconnect/issues/82) | A block closes the conversation but never pushes "closed" to live clients (found building slice W) | BUILT in slice Z (D193), PR #97; D270 verifies actual direct/report-block live closures and PR #125 closes the tracking issue |

## Policy implementation — 27 October 2026

The owner added the [internal trust and safety policy](INTERNAL-TRUST-SAFETY-EVIDENCE-PROTECTION-POLICY.md) (treated as legal-approved, D185). New work it requires is tracked as issues; tracking issue #92.

| Issue | Topic | Status |
| --- | --- | --- |
| #83 | Verified identity store with field-level encryption (§2) | BUILT in slice AA (D191, D194), PR #97; Key Vault and database roles wait for hosting |
| #84 | Legal holds, evidence preservation, law-enforcement disclosure (§3, §13) | BUILT in slice Y (D189, D192), PR #97; the owner's police-contact procedure (D195) and law-enforcement case management in slice AB (D196), PR #101. Operational items (§15: mailbox, on-call roster, authorisers, counsel) are the owner's |
| #85 | Media quarantine and CSAM / nudity screening (§5); before photos (#76) | OPEN |
| #86 | Serious Safety Incident pathway; reporting after block or deactivation (§9) | BUILT in slice Z (D190, D193), PR #97; attachments wait for #85 |
| #87 | Contact-change security and ban tokens (§4, §8); history built (D187) | Provider-free parts BUILT in slice AA (D194), PR #97; live SMS waits for deployment (D183) |
| #88 | Staff access controls and sensitive-access audit (§11, §15) | BUILT in slice AC (D197), PR #101; Entra conditional access and a staff-realm Keycloak sign-out client wait for hosting |
| #89 | Security operations and breach response (§12) | Host-free parts BUILT in slice AD (D198), PR #101: key rotation, alerts, incident severity, the breach runbook. Monitoring, backups, penetration testing, Key Vault wait for hosting |
| #90 | Location privacy hardening (§10) | BUILT in slice AE (D199), PR #101: distance bands, search limits and a triangulation test. Venue check-ins and photo EXIF come with venues and photos (Q79) |
| #91 | Mode privacy follow-ups (§6) | BUILT in slice AE (D199), PR #101: consent before the main about crosses modes, profile versions for moderation and holds. Per-mode photos come with photos (Q79) |
| [#99](https://github.com/repasscloud/wanderconnect/issues/99) | Privacy policy: disclose the verified identity record, ban matching and retention after termination (owner, Q78) | OPEN; updated by the owner 27 October 2026 with the attachment [SWOOVR-POLICE-CONTACT-PROCEDURE.md](https://github.com/user-attachments/files/34006144/SWOOVR-POLICE-CONTACT-PROCEDURE.md) (the police-contact procedure #84 left to the owner). Folded in as [POLICE-CONTACT-PROCEDURE.md](POLICE-CONTACT-PROCEDURE.md), D195 (CONFIRMED, owner Q1–Q37); built against in slice AB. The privacy notice DRAFT is built in slice AF (D200), PR #101; legal review (#57) and the contact address (Q80, #102) remain |

## Questions before the Y–AA run — 27 October 2026

Raised by the builder on branch `feature/swoovr-legal-holds-incidents-identity` before the overnight run, earliest blocker first. Each is also a GitHub issue (labels `question`, `decision-needed`, `trust-and-safety`). After the owner's first answers the run doesn't stop: an unanswered question gets its reversible default built, labelled DEFAULT in DECISIONS.md.

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q75 | [#93](https://github.com/repasscloud/wanderconnect/issues/93) | ANSWERED | Everything in this run | Build tonight in this order: Y legal holds and evidence preservation (#84) → Z the Serious Safety Incident pathway and reporting after a block, disconnect or deletion (#86) → AA the verified identity store with field-level encryption (#83, as Q78 decides)? Folded in: #82 (block and report-and-block push "closed") and the parts of #87 that need no provider: notify the previous email (mail sink) and previous number (simulated SMS sender) without the new value; a fresh sign-in for a number change and signing out other sessions after it; the verified number's HMAC added to terminated-member holds (flag only); an admin app screen for the contact-history lookup. | Policy §17 puts legal holds, serious incident reporting and the identity store on the launch checklist; holds come first because Z and AA both rely on them (an incident report places a hold; a held identity record outlives deletion). | Yes, in that order. Photos (#85, #76) stay out. Phone numbers stay simulated (D183); nothing is sent. | ANSWERED, owner 27 October 2026: recommended default accepted (D188). |
| Q76 | [#94](https://github.com/repasscloud/wanderconnect/issues/94) | ANSWERED | Y (legal holds) | Who may place and release a legal hold, how often is it reviewed, what does a hold preserve while deletion goes ahead, and what does an evidence export look like (format, hashing, who may download it)? | Policy §3, §11 and §13: each hold needs a reason, authoriser, scope, review date and release conditions; exports need hashes, provenance and chain of custody; investigators must not get broad access. Today the staff roles are only support and admin (D133). | (1) A new staff-realm role `legal` (policy §11 "security / legal officer"; a fictional local user, and the root admin may hold it). Only `legal` places, extends and releases holds, records law-enforcement disclosures and downloads evidence. Admins (not support) may open a provisional hold from a case; a serious-incident report (Q77) opens one automatically for 30 days; `legal` confirms or releases it. (2) A hold records reason, authoriser, scope (accounts, optionally specific conversations, a from–to date range), review date (default 90 days, at most 12 months ahead), release conditions and status. It never expires on its own; an overdue review heads the admin queue. Releasing needs a reason. The member is never told. (3) While a hold is active, `accounts purge`, `messages purge` and the contact-history purge skip in-scope records. A member's deletion still completes for everything they and others see (signed out, Keycloak user deleted, tombstone), but in-scope data is kept in the restricted evidence store and erased when the last hold on it is released. (4) Placing a hold captures an evidence package: in-scope messages (including deleted-for-both text still within 30 days and hidden ones), the profile and per-mode snapshot, connections and requests, reports and cases, verification outcomes and audit entries, as JSON items each with a SHA-256, source, record id and capture time; encrypted at rest with a versioned key (as D187); immutable; a later capture adds items, never edits them. (5) Export: one `swoovr-evidence/1` JSON with a manifest (item hashes, a package hash, timestamps, provenance) and the chain-of-custody log; `legal` only, after entering a purpose (for a disclosure also the agency, officer, reference, legal basis such as the APP 6.2(e) exception, and how the request was verified); a one-time 15-minute link, nothing written to disk; every view and download is appended to the chain of custody and audited with ids only. The verified law-enforcement contact procedure is written up as a document for you to approve, not automated. | ANSWERED, owner 27 October 2026: recommended default accepted (D189). |
| Q77 | [#95](https://github.com/repasscloud/wanderconnect/issues/95) | ANSWERED | Z (serious incidents) | Which serious-incident categories, what does the reporting member see (safety information, police, Australian support services), how is it escalated, and may reporters be told the general outcome? | Policy §9 asks for a clearly labelled pathway separate from routine reports, reporting after block, disconnect or deactivation, and telling reporters the general outcome where safe. D131 (CONFIRMED) says reporters never see the outcome, so that part needs you. | (1) A separate "Report a serious safety incident" entry on the person page, the conversation header (next to Report and Block, now also there), past connections (including blocked, disconnected and deleted members where a request, connection or conversation existed) and Me → Safety. (2) Categories as data, all urgent: sexual assault; physical violence; threats to safety (today's "threats" moves here); stalking; coercion, extortion or sextortion; child sexual exploitation or a suspected minor ("suspected underage" stays urgent); other immediate danger. (3) Before the form: "If you're in immediate danger, call 000" (tap to call in the app), Police Assistance Line 131 444, 1800RESPECT 1800 737 732 (24/7), Lifeline 13 11 14, eSafety (esafety.gov.au/report), and for child exploitation the ACCCE (accce.gov.au/report). Australian services only for the Adelaide beta. Swoovr never contacts police on the member's behalf; the member may say they have contacted or want to contact police and add a police reference (kept encrypted as case evidence). (4) On submit: an urgent case in the admin queue only (never support), the D159 escalation email (case id and "serious incident", no content), an automatic 30-day provisional hold over both accounts and their conversation (Q76), an optional block in the same step; the reported person is never notified. The reporter can pick messages from the conversation as evidence; file and image attachments wait for media screening (#85). (5) Outcomes: follow the policy and replace D131's "never the outcome" with a general outcome where safe ("We took action" / "We reviewed this and didn't find a breach"), never the specific sanction, for every report. | ANSWERED, owner 27 October 2026: recommended default accepted (D190). |
| Q78 | [#96](https://github.com/repasscloud/wanderconnect/issues/96) | ANSWERED | AA (identity store) | D128 and D158 (CONFIRMED) say Swoovr never stores document data or the document's date of birth; policy §1 principle 2 and §2.1 require a minimum verified identity record that outlives Didit's deletion. Do you supersede D128/D158 for ID-verified members, and which fields are kept, for how long, who may decrypt them and with what keys locally? | A CONFIRMED owner answer stands until you revisit it (D185); without this the store can't be built. | Yes, for members who complete ID verification. (1) Kept encrypted (AES-GCM, versioned key): legal first and last name as on the document, the document's date of birth, document type, issuing country and expiry date. Not kept: document number, address, images, selfie, liveness or biometric data. In clear: Didit session id, verification date, method and decision. A keyed HMAC of normalised name plus date of birth for exact ban-enforcement matches only (never proof on its own). (2) Decryption only by the `legal` role (Q76) with a recorded purpose, audited with ids only; admins and support see "verified identity on file" with the date and issuing country; the member's own export (D172) includes their record. (3) Retention (§2.2 periods are ceilings): active account — its lifetime; member deletion — erased at final deletion; terminated — kept 2 years for ban enforcement (as D171), then erased; under a hold — until released. No Didit PDF report is kept (no approved purpose). (4) Keys: `IdentityStore:Keys:{n}` and `IdentityStore:HmacKey`, separate from the contact-history keys, locally derived from the founder secret with their own labels unless set in `.local/docker.env`; Azure Key Vault when hosted (not built). Tables in a separate `restricted` schema now; a separate database login when hosted (#89). (5) Backfill: re-read Didit's decision for existing approved attempts still within its 30-day window; others show no record until they verify again (never forced). | ANSWERED, owner 27 October 2026: recommended default accepted (D191). Disclosure in the privacy policy: [#99](https://github.com/repasscloud/wanderconnect/issues/99) |

## Questions before the AB–AF run — 27 October 2026

Raised by the builder on branch `feature/swoovr-police-cases-staff-access`, earliest blocker first. Each is also a GitHub issue, linked both ways. The run doesn't stop for them: an unanswered question gets its reversible default built, labelled DEFAULT in DECISIONS.md.

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q79 | [#100](https://github.com/repasscloud/wanderconnect/issues/100) | ANSWERED | Photos and every media upload path (#85, #76, D132, D178); nothing else in this run | Policy §5 says no upload may be visible to anyone until it passes known-CSAM matching and nudity/abuse classification (#85). Which providers (storage: R2 keys; known-CSAM matching: Cloudflare CSAM Scanning Tool, PhotoDNA Cloud or Project Arachnid; classification: Azure AI Content Safety, Sightengine, Hive or AWS Rekognition), and may photos be built now? | Photos can't be shown to anyone without both checks; each provider needs an account and terms only the owner can accept. Identity selfies and documents stay in Didit's pipeline | Build the quarantine pipeline behind `IMediaStore` / `IMediaScreening`: a local private disk store under `.local/media` (R2 when the keys arrive) and a labelled simulated screener that never passes anything outside Development; every upload private ("pending") until a real provider passes it; uncertain results to a restricted review queue; blocked ones to the CSAM / serious-incident workflow (Y/Z) with no staff download. Providers when ready: Azure AI Content Safety plus PhotoDNA Cloud (apply now). Photos stay out of this run until #100 is answered || ANSWERED, owner 28 October 2026 (D201): known-CSAM matching through Project Arachnid Shield (credentials tested, agreement in place); Cloudflare's CSAM Scanning Tool enabled in Cloudflare (no project work); screening through the owner's own service [image-moderation-api](https://github.com/repasscloud/image-moderation-api) (NudeNet, Freepik NSFW detector, Arachnid Shield) as a Docker container; storage on R2 with the keys in `.local/docker.env`. Built in slice AH. |
| Q80 | [#102](https://github.com/repasscloud/wanderconnect/issues/102) | ANSWERED | Nothing in the prototype; a real monitored address before the privacy notice goes live (#99, #57) | Which address should the privacy notice give for privacy questions, access and correction requests and complaints, and who answers it? | APP 1: the notice must say how to contact Swoovr and complain; the procedure (§12) aims to answer complaints within 30 days | `privacy@swoovr.app`, answered by the security and legal officer, with the OAIC named as the external channel (built in the draft notice, D200; the page stays DRAFT until #57) || ANSWERED, owner 28 October 2026 (D202): privacy@swoovr.com; mail to privacy@swoovr.app forwards to it. Built in slice AG. |

## Questions raised while building AG–AI — 28 October 2026

Raised by the builder on branch `feature/swoovr-photos-safety-resources` (draft PR #104), earliest blocker first. Each is also a GitHub issue, linked both ways. The run doesn't stop for them: the reversible default is built and labelled DEFAULT in DECISIONS.md.

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q81 | [#105](https://github.com/repasscloud/wanderconnect/issues/105) | ANSWERED | Nothing in the prototype; the screening policy before real members upload photos | image-moderation-api returns signals, not decisions. Which NudeNet classes and scores send a photo to review; should a high-confidence explicit signal be refused automatically; should a known-content match also restrict the uploader automatically; should local hosts require the Arachnid check? | Uncalibrated thresholds decide what reviewers see and what members can show; an automatic restriction can tip off an offender or punish a false match | As built (D203): exposed genitalia/anus/female breast/buttocks ≥ 0.40 or the NSFW flag → review; no automatic refusal; no automatic restriction (urgent admin case, escalated at once); hosted environments always require Arachnid, local hosts only once its credentials are in image-moderation-api's `.env` || ANSWERED, owner 28 October 2026: recommended default accepted (D205). |
| Q82 | [#106](https://github.com/repasscloud/wanderconnect/issues/106) | ANSWERED | Nothing in the prototype; each country's launch readiness | #103 leaves review intervals to Trust & Safety operations. Who verifies a country's contacts, how often, and may AU start launch-ready on the list approved in Q77 (D190)? | A pack marked ready tells members its numbers were checked | As built (D204): review every 90 days; Trust & Safety owns every pack; AU ready from 27 October 2026; NZ not ready until checked; GB demo; re-check AU before the external beta || ANSWERED, owner 28 October 2026: recommended default accepted (D206). |

## Questions raised while building AJ–AL — 29 October 2026

Raised by the builder on branch `feature/swoovr-notifications-standing-attachments` (draft PR [#107](https://github.com/repasscloud/wanderconnect/pull/107)), earliest blocker first. Each is also a GitHub issue, linked both ways. The run doesn't stop for them: the reversible default is built and labelled DEFAULT in DECISIONS.md.

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q83 | [#108](https://github.com/repasscloud/wanderconnect/issues/108) | ANSWERED | Real push only | Real push needs an APNs auth key (`.p8`, Key ID, Team ID, bundle `com.swoovr.app`) and a Firebase project for FCM HTTP v1. Create them now, and keep the values in `.local/docker.env`? | Without them nothing reaches a lock screen; members see only the in-app centre and email | Push behind `IPushSender` with a labelled simulated sender that writes what the lock screen would show to Mailpit (`push-<device>@simulated.swoovr.invalid`, marked SIMULATED); devices register through `POST /api/v1/me/devices` | ANSWERED, owner 29 October 2026; D210, D211; provider/device implementation DEFAULTs D226–D228 (physical-device credentials/acceptance remain open operational gates). Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q84 | [#109](https://github.com/repasscloud/wanderconnect/issues/109) | ANSWERED | Nothing | Photo attachments in chat: every mode or only some; 20 a day per sender; may recipients turn off receiving photos (policy §14)? | Private photos are the main route for unwanted sexual images; every one is screened first (D203, D205) | Every mode between connected members; 20 a day; "Photos in chat" recipient setting, on by default; no explicit content (D178) | ANSWERED, owner 29 October 2026; D212. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q85 | [#110](https://github.com/repasscloud/wanderconnect/issues/110) | ANSWERED | Nothing | Should "End conversation" also disconnect every mode (D176, 30-day wait for the other), and is the closing message fixed or free text? | Leaving connections lets the other person reopen the chat at once; free text invites a last unkind word | An optional fixed closing message (three kind choices or none), then every mode disconnected and the chat closed read-only for both | ANSWERED, owner 29 October 2026; D213. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q86 | [#111](https://github.com/repasscloud/wanderconnect/issues/111) | ANSWERED | Telling members where to write | Where do members quote their support reference (`SW-XXXXXX`): a support email, an in-app form or both; who answers, and when? | A reference helps only if there's somewhere to send it; no support channel exists yet | "Quote this reference if you contact Swoovr support" with no address; staff look it up in the admin app's Support lookup | ANSWERED, owner 29 October 2026; D214. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q87 | [#112](https://github.com/repasscloud/wanderconnect/issues/112) | ANSWERED | Nothing in the prototype | Will Swoovr sign the voluntary Online Dating Industry Code, and when? | Signing brings a published compliance rating, an annual report to eSafety, a public transparency report and the Compliance Committee's enforcement | Use it as the benchmark now ([ONLINE-DATING-CODE.md](ONLINE-DATING-CODE.md)); decide before the external beta with the legal review (#57) | ANSWERED, owner 29 October 2026; D215. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q88 | [#122](https://github.com/repasscloud/wanderconnect/issues/122) | ANSWERED | #40 trusted contacts and check-ins (not built) | Trusted contacts for meetings: who, what they receive, by which channel; check-in prompts and what a missed one does | Sends personal information outside Swoovr and sets expectations no app can guarantee | Any contact who confirms a link (up to 3), SMS or email; first name, time window, the venue the member typed and a check-in time only; never the other person's details, chats or live location; a missed check-in tells the contact only, never police | ANSWERED, owner 29 October 2026; D218. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |
| Q89 | [#123](https://github.com/repasscloud/wanderconnect/issues/123) | ANSWERED | #40 emergency and after-meeting parts (not built) | Emergency button, location, the after-meeting prompt, and whether to wait for Plan a Date (#59) | Location is the most sensitive data; an emergency feature that fails silently is worse than none | Call the country's verified emergency number; an optional SMS the phone composes itself with a maps link (Swoovr never sees location); "How did it go?" leading to private feedback or the serious-incident pathway; a simple "I'm meeting someone" form first | ANSWERED, owner 29 October 2026; D219. Full requirements: [owner record](OWNER-DECISIONS-2026-10-29.md). |

### Online Dating Code gaps — 29 October 2026

From [ONLINE-DATING-CODE.md](ONLINE-DATING-CODE.md) (#55, DRAFT, not legal advice). Feedback to factor in later, each a GitHub issue that links back to the analysis: [#113](https://github.com/repasscloud/wanderconnect/issues/113) terms of use and community guidelines; [#114](https://github.com/repasscloud/wanderconnect/issues/114) staff removal of profile text and single messages; [#115](https://github.com/repasscloud/wanderconnect/issues/115) detecting harm in text; [#116](https://github.com/repasscloud/wanderconnect/issues/116) moderator guidance and training; [#117](https://github.com/repasscloud/wanderconnect/issues/117) complaints about handling, outcome notices, support after reports; [#118](https://github.com/repasscloud/wanderconnect/issues/118) retention schedule; [#119](https://github.com/repasscloud/wanderconnect/issues/119) organisational items; [#120](https://github.com/repasscloud/wanderconnect/issues/120) transparency and Code reports; [#121](https://github.com/repasscloud/wanderconnect/issues/121) onboarding safety advice and policy-change notices.

## Phase 0 takeover — hosting question, 29 October 2026

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q90 | [#124](https://github.com/repasscloud/wanderconnect/issues/124) | CONFIRMED D291; owner-action | Provisioning and actual UAT deployment; independent implementation continues | D291 confirms topology; code preparation only; owner provisions subscription and deploys. | Establishes residency, costs and operator ownership | D221: Australia East, Container Apps, PostgreSQL Flexible Server, Key Vault, Cloudflare proxy, MailerSend HTTP, private screening, isolated Blob backups; owner creates/selects subscription. Full proposal in issue | Owner subscription/provisioning and hosted acceptance |

## Active contact retirement — 29 October 2026

| ID | Issue | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Q91 | [#131](https://github.com/repasscloud/wanderconnect/issues/131) | ANSWERED | Implemented D254 mobile and D257–D259 durable email/login retirement; original-account recovery remains separate and never automatic | After approved identifier release, retire only the reviewed active email/login or verified-mobile binding, preserving original termination/history and unrelated credentials; what original-member recovery/notification conditions apply? | Registry revocation alone does not free unique provider/contact bindings; mailbox recovery must not expose the old account to a new owner | D249: separate restricted audited retirement, revoke old sessions, disable the retired mailbox recovery path, preserve unrelated credentials/history/ban, independently review old-account recovery, fail closed on uncertain provider update | ANSWERED owner in chat, 29 October 2026: approve recommended default (D249) |

## Local Docker runtime cleanup — 1 November 2026

| ID | Issue | Status | Blocks | Feedback / consequence | Owner direction |
| --- | --- | --- | --- | --- | --- |
| Q92 | [#132](https://github.com/repasscloud/wanderconnect/issues/132) | OPEN cleanup; CONFIRMED backlog scope | No observed local password-authentication/startup blocker | Successful local container operator commands emit a missing `libgssapi_krb5.so.2` diagnostic. Identify its native consumer and fix the owning runtime dependency/configuration without hiding genuine failures; verify fresh/reuse, operator commands, admin smoke and repository checks. [Roadmap](Swoovr-Product-Review-and-Strategic-Roadmap.md#phase-0-maintenance-backlog). | Owner, 1 November 2026: create a GitHub issue and add cleanup to the main-branch roadmap for later. No implementation or Kerberos acceptance claim. |

## Member responsive validation follow-ups — 1 November 2026

| ID | Issue | Status | Blocks | Feedback / consequence | Recommended default |
| --- | --- | --- | --- | --- | --- |
| Q93 | [#134](https://github.com/repasscloud/wanderconnect/issues/134) | OPEN maintenance feedback | Legacy member-auth harness acceptance, not member layout implementation | `tests/auth/members.browser.cjs` hard-codes MAIN API and bypasses shared versioned/owned helpers; owned-runner attempt failed404 at line151. Separate account122 and auth-navigation17 pass. [Evidence](MEMBER-RESPONSIVE-LAYOUT.md). | DEFAULT: adapt the legacy fixture to paired owned API/database and current version headers, retaining negative cases; never weaken product authentication to satisfy it. |
| Q94 | [#135](https://github.com/repasscloud/wanderconnect/issues/135) | OPEN staff accessibility feedback | Staff enlarged-text acceptance; outside member-only layout scope | Expanded interactions twice reported only `staff text resized200% at320px reflows`. Member floor25 and responsive356 pass; no staff implementation changed. [Evidence](MEMBER-RESPONSIVE-LAYOUT.md). | DEFAULT: reproduce baseline, distinguish actual overflowing elements from render timing and fix the owning staff layout/test with evidence; do not hide overflow or weaken checks. |

## Issue intake investigations — 2 November 2026

Owner-authorised tracking/publication only. All 15 substantive reports processed; no fixes, redesigns or policy changes implemented. OPEN findings below distinguish evidence from diagnosis; DEFAULT follow-up is current-baseline reproduction without resetting data. See [evidence review](evidence/2026-11-01-issue-intake/README.md).

<a id="q95"></a>

### Q95 — Admin account search fails to find existing users by email address

Owner answer recorded 2 November 2026 — **CONFIRMED D289** ([source](https://github.com/repasscloud/wanderconnect/issues/137#issuecomment-6570109341)): Account email lookup works; owner requests resolution in this PR. Original cause remains unproven. Closes #137 on merge; phone search/browse is separate Q110. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

Current status checked 2 November 2026: [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) is merged into main; issue remains OPEN. Owner confirmed working lookup on #137 (2 November); original cause remains unproven and closure is deferred to a next PR. New scope: Q110/#155.

Implementation progress (2 November 2026): the false no-match on API refusal is fixed on [PR #152](https://github.com/repasscloud/wanderconnect/pull/152). Actual Edge regression failed before and passed after the fix: denied staff lookup, exact/normalised email, an account without a profile and genuine no-match. Full `./scripts/check.sh` passed with 596 PostgreSQL cases and three smoke suites. Original private account linkage is unverified; no sync defect or provider search is claimed. See [handoff](ISSUE-FIX-HANDOFF.md).


- Issue: [#137](https://github.com/repasscloud/wanderconnect/issues/137); [intake item 2](ISSUE-INTAKE.md#intake-2).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Accounts.razor discards unsuccessful lookup status with Value ?? [], so an API refusal can appear as no match. The API normalises email, queries application Accounts and left-joins optional MemberProfiles; it does not search Keycloak directly. Neither missing application records nor provider/database mismatch is established. Capture status/code and canonical subject/account linkage with a fictional fixture before diagnosing.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #88 (staff permissions), #134 (auth harness); neither covers this lookup failure.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q96"></a>

### Q96 — Admin session incorrectly reports expiration and displays inconsistent, unbranded authentication pages

Current status checked 2 November 2026: [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) adds staff token renewal and a branded anonymous signed-out page. Reproduced expired-token/cookie mismatch with an owned temporary provider client; actual Edge verified renewal and reauthentication after owned-provider-session revocation. Ten unit checks cover concurrency, absolute deadline, unchanged authentication evidence, refusal/malformed expiry and late renewal after logout. The review-discovered logout race is fixed. Final full `./scripts/check.sh` passed, including 596 PostgreSQL cases and all three smoke suites. Hosted/multi-instance and original private-session timing remain unverified; no security gates were relaxed. See [handoff](ISSUE-FIX-HANDOFF.md).


- Issue: [#138](https://github.com/repasscloud/wanderconnect/issues/138); [intake item 3](ISSUE-INTAKE.md#intake-3).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: AdminText maps any API 401 to session-ended; StaffTokenHandler forwards the stored access token. The signed-out endpoint returns minimal raw HTML. The header can reflect the cookie while the API refuses a token; expiration/renewal failure remains a hypothesis, not a reproduced cause. Screenshots show date-of-birth and media routes with different message treatment.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #88 and #135; neither investigates the cookie/API lifecycle or signed-out presentation.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q97"></a>

### Q97 — Improve Admin photo moderation workflow, reveal controls and standardised review decisions

Current status checked 2 November 2026: [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) is merged into main; issue remains OPEN. Follow-ups: Q114/#159 (workspace), Q115/#160 (appeals/retention), Q113/#158 (Safari refresh). Approval-note retention/case controls remain dependencies; adult-media policy is deferred.

Implementation progress (2 November2026): independently actionable queue changes are included in merged [PR #152](https://github.com/repasscloud/wanderconnect/pull/152): factual automatic review purpose, blurred preview, audited keyboard reveal, stable decision-bound codes, server-side Other-note requirement and retained coded counts. Synthetic-only actual Edge acceptance and full `./scripts/check.sh` passed (607 PostgreSQL cases, three smoke suites, zero build warnings/errors). Review-discovered cross-item note restoration is fixed and regressed. D178 eligibility remains deferred/unchanged; no adult material or owner review is needed. Case-specific legacy controls remain, and private approval notes require a governed retained-history design rather than ungoverned retention; coded approvals refuse unsupported notes. See [handoff](ISSUE-FIX-HANDOFF.md) and [reason boundaries](../architecture/MEDIA-REVIEW-REASONS.md). Issue remains partially fixed/open pending remaining acceptance and these precise dependencies.


- Issue: [#139](https://github.com/repasscloud/wanderconnect/issues/139); [intake item 4](ISSUE-INTAKE.md#intake-4).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Code confirms required purpose before a media link, CSS blur/reveal checkbox and free-text decision reason; escalation already exists. The second screenshot shows browser required-field validation, not a revealed photograph. OPEN: intake permission for explicit private-chat media conflicts with CONFIRMED D178 (no explicit content anywhere in beta). Preserve screening, conflict exclusions, audit and evidence holds; no permission change is approved here.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #85 (screening), #88 (sensitive access), #116 (playbook), #54 (explicit-media policy); these do not cover the combined review-controls/reason-code UX.
- Owner input / missing evidence: Resolve explicit private-chat permission conflict with D178 before policy implementation.

<a id="q98"></a>

### Q98 — Redesign Beta Enrollments screen with searchable table, improved layout and scalable administration

Implementation checkpoint: [Q98 evidence](Q98-BETA-VISUALS-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Current status checked 2 November 2026: [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) is merged into main; issue remains OPEN. Amended by intake item 20 G–L: email width, filter/button consistency and first-line cell alignment reuse #140; no duplicate visual issue. CSS already top-aligns td; paragraph-margin cause is unverified. Registration control is independently Q112/#157.

Implementation progress (2 November2026): [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) adds server-side search/pagination and a structured enrolment table. Actual Edge verified1005 records, role/anonymous refusal, paging/sort/search/status/date controls and confirmed revision-aware iOS/revoke actions. Eleven new database search checks passed; full `./scripts/check.sh` passed with607 PostgreSQL cases, three smoke suites and zero build warnings/errors. Names use canonical account linkage, never email-based identity inference; Apple/provider states remain distinct. See [handoff](ISSUE-FIX-HANDOFF.md) and [contract](../architecture/BETA-ENROLMENT-SEARCH.md). Issue remains open pending remaining acceptance.


- Issue: [#140](https://github.com/repasscloud/wanderconnect/issues/140); [intake item 5](ISSUE-INTAKE.md#intake-5).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Code renders a list of up to 1,000 enrolments with per-record forms and no table search/sort/pagination controls. Screenshot supports dense stacked controls, not a measured 1,000-record performance failure. Preserve distinction between enrolment, invitation delivery and staff-reported Apple status.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #25 covers original enrolment portal/delivery follow-ups; this is a distinct scalable table/search redesign, not a duplicate of the original missing portal.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q99"></a>

### Q99 — Redesign public landing page and remove authenticated navigation and development messaging

Owner answer recorded 2 November 2026 — **CONFIRMED D290** ([source](https://github.com/repasscloud/wanderconnect/issues/141#issuecomment-6572956911)): 141.1 A; 141.2 Yes; 141.3 A; 141.4 A; 141.5–141.7 Yes. Anonymous pages are only /, /terms, /community-guidelines, /privacy, /safety/advice and country-only /safety. Other page routes redirect to / silently. Sign-in/create buttons only on home, no top-right links; remove Private beta and draft labels; Support goes to https://swoovr.com/support. Standard only for guests; successful sign-in lands on Explore. Same pre-sign-in rule on native. Supersedes D288 runtime draft deferral for these approved operative readers and D20 guest theme choice; authentication callbacks/assets remain necessary protocol resources, not guest pages. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

Historical intake status before this delivery: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Historical implementation before this delivery: Guest navigation/landing fixed; private-beta and draft-policy disclosures were retained then; D290 now explicitly supersedes that presentation. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#141](https://github.com/repasscloud/wanderconnect/issues/141); [intake item 7](ISSUE-INTAKE.md#intake-7).
- Status: D290 implemented in draft PR171; OPEN Q123/#173 blocks trusted-contact capability reconciliation. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Home source confirms work-in-progress copy and appearance CTA. Shared navigation is visible in the screenshot. OPEN: removing genuine draft/legal/readiness disclosures must not imply approved documents or a live service. Keep Standard default and personal choices; no rebranding.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #48 (accessibility) and #57 (release review); neither covers public entry navigation/copy.
- Owner input: D290 explicitly approves activation/presentation of the public readers; Q123 remains OPEN. No deployment or compliance approval is inferred.

<a id="q100"></a>

### Q100 — Correct unauthenticated /account routing and prevent account content being displayed before sign-in

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Configured guests route directly into sign-in; retry/unconfigured fallbacks retained; BFF still401. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#142](https://github.com/repasscloud/wanderconnect/issues/142); [intake item 8](ISSUE-INTAKE.md#intake-8).
- Status: OPEN investigation / feedback; processing complete. Priority: Medium.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: The unauthenticated branch renders sign-in/create links and public settings/legal links. Private MemberAccountPanel is in the authenticated branch with prerender disabled. Screenshot supports routing/UX dissatisfaction; private account data exposure is not demonstrated. Verify guest HTML/API responses and safe return paths independently.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related intake 7/9/14; #134 is a harness issue, not this route change.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q101"></a>

### Q101 — Restore approved Swoovr sign-in design and correct desktop and mobile authentication layouts

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Desktop auth frame implemented; canonical420px form/mobile layout retained; native acceptance outstanding. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#143](https://github.com/repasscloud/wanderconnect/issues/143); [intake item 9](ISSUE-INTAKE.md#intake-9).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Custom Keycloak theme already exists. Compare current browser viewports and system-browser mobile auth against reference, rather than assuming no theme. Read-only reference is a layout concept; canonical Standard sign-in tokens govern. Native presentation not reproduced.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #48; separate from intake 10/12 page-specific registration/recovery acceptance.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q102"></a>

### Q102 — Redesign account registration screen to meet Swoovr's production design standards

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Registration shares the responsive frame; email-first/adult/draft requirements preserved. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#144](https://github.com/repasscloud/wanderconnect/issues/144); [intake item 10](ISSUE-INTAKE.md#intake-10).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Theme code includes registration, adult checkbox, legal draft wording and development footer. Registration screenshot for intake 11 supports narrow layout. OPEN: cosmetic removal of draft/acceptance caveats cannot make draft legal documents operative; coordinate legal gate #57. Do not change provider or age checks.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #57 and intake 9/11; registration layout is independently actionable.
- Owner input / missing evidence: Legal text cannot imply draft documents are in force; resolve with #57 before removing caveats.

<a id="q103"></a>

### Q103 — Registration errors are difficult to identify when an email address is already registered

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Duplicate-email error focus and accessible descriptions verified with fictional fixture. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#145](https://github.com/repasscloud/wanderconnect/issues/145); [intake item 11](ISSUE-INTAKE.md#intake-11).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Screenshot visibly contains an existing-email error; it does not establish broken registration or invitation activation. Field macro renders error text with aria-live, but prominence/focus need actual browser/assistive-technology checks. Balance recovery guidance with neutral responses and enumeration controls; beta invitation is not a new identity.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related intake 10 and #25; neither specifically covers validation prominence/recovery.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q104"></a>

### Q104 — Redesign Forgot Password screen to match Swoovr authentication standards

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Reset frame and email input/autocomplete corrected without changing provider token handling. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#146](https://github.com/repasscloud/wanderconnect/issues/146); [intake item 12](ISSUE-INTAKE.md#intake-12).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Custom reset form uses shared theme, email input and send-reset-link action; this is a refinement request rather than evidence of an unthemed default provider page. Screenshot supports layout criticism, not failed email delivery. Preserve provider reset token semantics.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related intake 9/13; form styling and post-submit confirmation have different acceptance.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q105"></a>

### Q105 — Replace password reset notification with dedicated confirmation screen and prevent account enumeration

Current status checked 2 November 2026: [PR #153](https://github.com/repasscloud/wanderconnect/pull/153) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): Dedicated neutral acknowledgement verified for enabled/absent/disabled fixtures; timing resistance remains unverified. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.


- Issue: [#147](https://github.com/repasscloud/wanderconnect/issues/147); [intake item 13](ISSUE-INTAKE.md#intake-13).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Screenshot shows a generic email acknowledgement embedded above a sign-in form, with a populated masked password field. It does not prove account enumeration, password disclosure, email delivery or automatic sign-in. Compare existing/nonexistent/disabled addresses including timing and email effects using safe fixtures; keep confirmation neutral.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related intake 12; no existing issue specifically covers reset confirmation or comparative enumeration testing.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q106"></a>

### Q106 — Redesign Swoovr Me / Account page for a desktop-first experience and clearer onboarding flow

Owner answer recorded 2 November 2026 — **CONFIRMED D292** ([source](https://github.com/repasscloud/wanderconnect/issues/148#issuecomment-6572313253)): 148.1 A; 148.2 Yes; 148.3 A; 148.4 A; 148.5 B; 148.6 Yes; 148.7 A. Mobile verification then DOB, only DOB if phone gate off; tabs remain and offer direct setup recovery. Safety notice once per server-stored wording revision, thereafter footer only. Coupon redemption in Membership & billing; desktop account sidebar with specified sections. 148.8 adds small version/build footer text. Supersedes intake onboarding deferral; retain actual phone-gate configuration and essential account rights. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

Historical intake status, before the owner answer and this implementation: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154) is merged into main; issue remains OPEN. New DOB policy scope is Q116/#161; onboarding order and safety-dismissal decisions remain deferred.

Historical implementation progress before this delivery: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: grouped settings and truthful account heading. Onboarding order and revision-persisted safety-dismissal proposals remain deferred. Validation and remaining acceptance: [member evidence](ISSUE-MEMBER-FIXES.md). PR #154 is merged; the issue remains open for remaining acceptance or deferred scope.

- Issue: [#148](https://github.com/repasscloud/wanderconnect/issues/148); [intake item 14](ISSUE-INTAKE.md#intake-14).
- Status: Owner answer CONFIRMED; source implementation in draft PR171; acceptance gaps tracked in the linked evidence. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Signed-in screenshot shows dense account/safety/status/settings composition. Recent responsive work already widened account layout; verify current baseline before attributing all desktop problems to old width. OPEN: fixed phone-then-DOB order and revision-persisted safety dismissal need reconciliation with configured backend gates and existing safety notices; do not weaken them.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #50 (standing), #121 (safety advice), #48; none covers this account/onboarding composition.
- Owner input / missing evidence: Reconcile required phone → DOB sequence and revision-based safety acknowledgement with existing backend gates.

<a id="q107"></a>

### Q107 — Redesign Profile and Modes page for responsive layouts, clearer controls and improved profile management

Current status checked 2 November 2026: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154) is merged into main; issue remains OPEN. Initial registered-name mapping is now Q117/#162; independent mode names are Q118/#163 and conflict with D175.

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: responsive profile/mode workspace, non-overlapping city labels, compact controls and scoped saves. Custom names and explicit about sharing preserved; visibility off/reload passed. Automatic initial-name mapping remains an unapproved product/data-mapping follow-up. Validation and remaining acceptance: [member evidence](ISSUE-MEMBER-FIXES.md). PR #154 is merged; the issue remains open for remaining acceptance or deferred scope.

- Issue: [#149](https://github.com/repasscloud/wanderconnect/issues/149); [intake item 15](ISSUE-INTAKE.md#intake-15).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Screenshots support long narrow form, empty display name in one capture and home-city label overlap in another. Code uses checkboxes (styled like large controls), automatically enables visibility when enabling a mode, and also exposes a separate visibility checkbox: inability to turn visibility off is reported, not established. Check persistence and stale updates. Preserve customised names and explicit cross-mode about consent (D199).
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #37/#91 (mode privacy), #48; no exact profile-editor UX/label/initialisation duplicate.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

<a id="q108"></a>

### Q108 — Redesign Your Photos page with shared photo library, independent mode galleries and membership limits

Owner answer recorded 2 November 2026 — **CONFIRMED D293** ([source](https://github.com/repasscloud/wanderconnect/issues/150#issuecomment-6572392572)): 150.1 A; 150.2 60 unless ridiculous overhead, otherwise 30; 150.3 Yes; 150.4 A; 150.5 B; 150.6 No. Independent main photo per mode, explicitly selected with no automatic migration; placeholders allowed because members may have no profile photos. Free/Plus/Premium mode totals 8/16/24. Downgrade hides excess by order without deleting. Explicitly supersedes D175 shared-main-photo requirement. DEFAULT choose 60 library objects: same existing storage path, no eager duplication; measure storage rather than claim cost acceptance. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

Historical intake status, before the owner answer and this implementation: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154) is merged into main; issue remains OPEN. Q111/#156 and Q114–Q115/#159–#160 must preserve the shared main photo and current quotas; independent mains/photo limits remain deferred.

Historical implementation progress before this delivery: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: one mode-assignment gallery at a time with shared library/main preserved. Independent main photos and proposed photo limits remain deferred; API quotas and screening rules unchanged. Validation and remaining acceptance: [member evidence](ISSUE-MEMBER-FIXES.md). PR #154 is merged; the issue remains open for remaining acceptance or deferred scope.

- Issue: [#150](https://github.com/repasscloud/wanderconnect/issues/150); [intake item 16](ISSUE-INTAKE.md#intake-16).
- Status: Owner answer CONFIRMED; source implementation in draft PR171; acceptance gaps tracked in the linked evidence. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Code confirms shared main photo and optional per-mode assignments; screenshot shows a photo library and repetitive mode rows. OPEN: proposal for independent mode main photos changes CONFIRMED D175; 60 library / 8 Free / 16 Plus / 24 Premium limits are intake proposals pending reconciliation with current quotas/entitlements. Preserve existing assignments, moderation and downgrade data; no migration is authorised.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #37/#73 (cross-mode policy), #85 (screening); new gallery/limits proposal is not covered in full.
- Owner input / missing evidence: Resolve independent mode main photos versus D175 and confirm proposed 60-library / 8–16–24 tier limits.

<a id="q109"></a>

### Q109 — Redesign "Who I'd Like to Meet" with responsive layouts, clearer discovery preferences and improved filtering controls

Current status checked 2 November 2026: [PR #154](https://github.com/repasscloud/wanderconnect/pull/154) is merged into main; issue remains OPEN. Remaining acceptance and scope boundaries below still apply; merge is not production or device acceptance.

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Implementation merged: per-mode unsaved filter drafts survive switching and failed reads; stale fields hidden while loading and controls disabled during save. Responsive workspace and selection counts added; Any/All, reciprocal exclusions and Plus rules preserved. Drafts remain local to this page. Validation and remaining acceptance: [member evidence](ISSUE-MEMBER-FIXES.md). PR #154 is merged; the issue remains open for remaining acceptance or deferred scope.

- Issue: [#151](https://github.com/repasscloud/wanderconnect/issues/151); [intake item 17](ISSUE-INTAKE.md#intake-17).
- Status: OPEN investigation / feedback; processing complete. Priority: High.
- Blocks: acceptance of the reported journey or proposed change; independent investigations continue.
- Evidence/consequence: Actual route is /me/matching, linked from Account. Screenshot supports long narrow combined identity/filter forms and separate saves. Current delivery explicitly retains 420px on matching/profile forms. Reorganise only after validating independent mode persistence, Any/All, five required/ten excluded, reciprocal exclusions and backend entitlements; no matching-semantic change is approved.
- DEFAULT follow-up: reproduce current baseline using fictional adult data; record actual behaviour and status codes; preserve confirmed policies and existing data. Full reproduction, impact and acceptance criteria are in the issue and intake.
- Duplicate assessment: Related #44 is compatibility scoring/explanations, #48 accessibility; neither covers this settings composition.
- Owner input / missing evidence: No immediate owner blocker. Exact capture revision, browser/viewport and runtime reproduction remain unrecorded; request fresh fictional evidence if needed.

## Intake refresh — 2 November 2026 (items 18–25)

Processing complete, investigation/decisions OPEN. Q110–Q120 follow actual prior maximum Q109. [Run record and split map](ISSUE-INTAKE-2026-11-02.md). These records do not amend CONFIRMED decisions. Existing Q98/#140 also receives item 20 styling feedback.

<a id="q110"></a>

### Q110 — Extend Admin account lookup with phone search and permission-scoped account browsing

Owner answer recorded 3 November 2026 — **CONFIRMED D307** ([source](https://github.com/repasscloud/wanderconnect/issues/155#issuecomment-6596651120)): search by mobile (optional +, spaces stripped), lowercased email, or registered first/last name with `%` wildcards; results show first name, last name, age, city, ID verified, email and phone; includes lawful third-party investigations. Permissions/audit/paging are DEFAULT; browse-all was not requested. Recording is not completion of the code.

- Issue: [#155](https://github.com/repasscloud/wanderconnect/issues/155); [intake item 18](ISSUE-INTAKE.md#intake-18).
- Status: ANSWERED (D307), implementation pending; intake processing complete. Priority: High — account lookup is central to support and investigation; bulk personal-data access needs explicit scoping.
- Earliest blocker/consequence: acceptance of this proposed Admin / account search slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Entire item 18; registered-name search also depends on Q117.
- Source-supported finding: Accounts.razor uses an exact-email GET form and a basic result list. AdminEndpoints.cs normalises the verified email, left-joins profiles, returns at most 10 matches and audits a result count. It has no phone lookup or browse/filter contract. PR #152 merged the refusal/no-match correction; the owner confirmed lookup works on #137. This is an extension, not a recurrence of Q95.
- Expected/proposed follow-up: Retain working exact-email lookup; add canonical registered-phone lookup and a separate paginated, sortable account browser with permitted columns and combined filters.
- Requirements / decisions: CONFIRMED D38/D55 issuer/subject account identity and D185 least privilege remain. DEFAULT: reuse existing phone normalisation, account routes and pagination conventions. OPEN: demographic-filter purposes/field matrix and name-search authority (Q117); no broad support access is inferred.
- Duplicate assessment: distinct follow-up; related [#137](https://github.com/repasscloud/wanderconnect/issues/137), [#140](https://github.com/repasscloud/wanderconnect/issues/140), [#88](https://github.com/repasscloud/wanderconnect/issues/88). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q111"></a>

### Q111 — Organise Admin account details and add scoped profile inspection

Owner answer recorded 2 November 2026 — **CONFIRMED D294** ([source](https://github.com/repasscloud/wanderconnect/issues/155#issuecomment-6572427017)): 156.1–156.3 A; 156.4–156.5 Yes (posted on #155). All staff see calculated age/DOB assurance, full DOB only with elevation and recorded purpose. Read-only mode previews for Support/Admin/Root without visits/impersonation. Hidden/unassigned/pending/rejected photos Admin-only via audited reveal. Chats only through report/case. Legal gains no moderation; Root gains no Legal privilege. Answers belong to Q111/#156 despite posting on #155. Q110/#155 permissions remain OPEN. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#156](https://github.com/repasscloud/wanderconnect/issues/156); [intake item 19](ISSUE-INTAKE.md#intake-19).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — misleading age labelling and fragmented account investigation affect safety operations.
- Earliest blocker/consequence: acceptance of this proposed Admin / account investigation slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Entire item 19; independent search, appeals, DOB/name and shared presentation work is cross-linked.
- Source-supported finding: Actual route is /accounts/{Id:guid}. Account.razor labels AgeStatus as Age and renders raw wire values, decision IDs, Plus grants and two staff-badge actions. ModerationEndpoints.SummaryAsync returns no numeric age/full DOB, but already supplies sanctions, entitlements, trips/review flags and an identity-on-file marker. D191 deliberately keeps decrypted verified identity separate. Existing case-scoped profile-at-report/evidence surfaces must be reused, not replaced by unrestricted chat inspection.
- Expected/proposed follow-up: Create a permission-aware account workspace with truthful overview, profile/mode inspection and read-only preview, scoped photo/case/conversation links, membership/badge state, sanctions and paged action history.
- Requirements / decisions: CONFIRMED D134 affiliation grants no permissions, D175 shared display name/main photo, D185/D191 restricted identity and D197 elevated access remain. DEFAULT: reuse existing capabilities and case links. OPEN: new sensitive-field inspection/preview scopes; per-mode main photos remain deferred under Q108.
- Duplicate assessment: distinct follow-up; related [#88](https://github.com/repasscloud/wanderconnect/issues/88), [#83](https://github.com/repasscloud/wanderconnect/issues/83), [#84](https://github.com/repasscloud/wanderconnect/issues/84), [#91](https://github.com/repasscloud/wanderconnect/issues/91), [#148](https://github.com/repasscloud/wanderconnect/issues/148). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q112"></a>

### Q112 — Define authoritative beta/public/closed registration controls across Keycloak and applications

Implementation checkpoint: [Q112 evidence](Q112-REGISTRATION-CONTROLS-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D295** ([source](https://github.com/repasscloud/wanderconnect/issues/157#issuecomment-6572579483)): 157.1 Yes; 157.2a A; 157.2b A; 157.3–157.5 Yes. Two persisted flags: closed, invite-only beta, public, public with beta tracking. Root-only changes with reason/confirmation/audit; all staff see state. Closure covers Keycloak direct/brokered creation, fails closed if unreadable while existing sign-in/reset remains. UAT starts invite-only. 157.6 title: Swoovr is currently invite-only; text: We're not accepting public registrations just yet. If you'd like early access, you can register your interest in our beta.; link: Join the beta waitlist → https://swoovr.com/beta. Existing Keycloak remains authentication authority; no API-minted sessions and no public launch authorization. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#157](https://github.com/repasscloud/wanderconnect/issues/157); [intake item 20](ISSUE-INTAKE.md#intake-20).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — registration closure must cover identity-provider creation as well as application access.
- Earliest blocker/consequence: acceptance of this proposed Registration / Admin configuration / identity provider slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 20 A–F and registration criteria only. Visual refinements G–L reuse Q98/#140.
- Source-supported finding: AccountResolver reads static Beta:Enabled (default true) for application access. The generated member realm separately has registrationAllowed=true. D237 explicitly distinguishes Keycloak user creation from application registration/adoption; the beta access check is not evidence that Keycloak creation is closed. No persisted two-flag Root-managed policy was found in these sources. PR #153 merged guest registration navigation but did not add this policy.
- Expected/proposed follow-up: After policy approval, persist and audit beta/public availability, display effective state and apply it to every web/native/provider registration entry point while existing sign-in/recovery remains usable.
- Requirements / decisions: CONFIRMED D62 invitation-only beta, D127 age/email/mobile gates and Keycloak ownership remain. OPEN: approve the proposed four-state model, Root-only mutation/view matrix, shared authority and supported provider enforcement/propagation contract before implementation. DEFAULT: fail closed on unavailable authority. No public launch or provider configuration change is authorised.
- Duplicate assessment: distinct follow-up; related [#140](https://github.com/repasscloud/wanderconnect/issues/140), [#25](https://github.com/repasscloud/wanderconnect/issues/25), [#141](https://github.com/repasscloud/wanderconnect/issues/141), [#142](https://github.com/repasscloud/wanderconnect/issues/142), [#143](https://github.com/repasscloud/wanderconnect/issues/143), [#144](https://github.com/repasscloud/wanderconnect/issues/144). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q113"></a>

### Q113 — Investigate Safari Admin refresh replaying missing named SSR forms

Owner answer recorded 2 November 2026 — **CONFIRMED D296** ([source](https://github.com/repasscloud/wanderconnect/issues/158#issuecomment-6572996253)): 158.1 Yes; 158.2 any Admin page in private Safari; 158.3 Cmd+R/reload prompts resend form each time; 158.4 independent of time open, navigation works. Safari 27.2 (22625.2.8.11.9). Owner reproduction evidence supports POST refresh investigation, not a proven session-expiry cause. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#158](https://github.com/repasscloud/wanderconnect/issues/158); [intake item 21](ISSUE-INTAKE.md#intake-21).
- Status: OPEN investigation / feedback; intake processing complete. Priority: Medium — intermittent refresh blocks the route, with a reported recovery through the root page.
- Earliest blocker/consequence: acceptance of this proposed Admin / Safari / SSR forms slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Entire item 21, including the additional approve-form report.
- Source-supported finding: User reports both missing form 'search' and a removed approve-{mediaId} form after refreshing /media. MediaReview.razor renders dynamic item-bound POST form names; successful decisions reload a queue in which that item may disappear. ContactHistory.razor contains the separate POST form 'search'; Accounts/Beta search uses GET. Program.cs registers static SSR and enables exception handling only outside Development. These facts support investigating stale POST replay but do not establish the request method, route or cause.
- Expected/proposed follow-up: GET/direct navigation and refresh load the current route; POST recovery handles stale form state without duplicate decisions and shows branded, actionable failures.
- Requirements / decisions: DEFAULT hypothesis: Safari refresh may replay an SSR POST whose named form no longer exists. OPEN cause: request/response trace is absent; not proven to be a session-expiry defect. Keep dynamic form identity/antiforgery and avoid navigation rewrites without evidence.
- Duplicate assessment: distinct follow-up; related [#138](https://github.com/repasscloud/wanderconnect/issues/138), [#139](https://github.com/repasscloud/wanderconnect/issues/139). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q114"></a>

### Q114 — Provide a compact photo-review queue, focused review controls and scoped workload history

Implementation checkpoint: [Q114 evidence](Q114-PHOTO-REVIEW-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D297** ([source](https://github.com/repasscloud/wanderconnect/issues/159#issuecomment-6572633682)): 159.1 A; 159.2 B; 159.3 A; 159.4 B; 159.5 A. Server-stored per-staff/per-notice safety acknowledgement valid four hours. Support may review alongside Admin; counts only for reviewers. No approval free text, codes only. Tablet full-screen review dialog; phone hides review tools. Extends routine review capability to Support without granting Legal or protected specialist evidence access; aligns D302 tablets as desktop presentation. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#159](https://github.com/repasscloud/wanderconnect/issues/159); [intake item 22](ISSUE-INTAKE.md#intake-22).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — current inline review design limits operational throughput; no measured throughput claim is available.
- Earliest blocker/consequence: acceptance of this proposed Admin / media moderation workspace slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 22 A–K/T, queue/history/interface acceptance; L–S split to Q115.
- Source-supported finding: MediaReview.razor renders inline list items and approve/reject/escalate details, separate reveal/conceal forms and a 30-day coded-count expander. MediaEndpoints review selects up to 100 uncertain items, with a separate pending-screening count, not a paginated workload query. Home.razor shows case/appeal/hold counts but no photo-review total. PR #152 merged factual purpose, audited reveals and stable decision codes. No server-side four-hour/versioned safety acknowledgement was found in the inspected review path.
- Expected/proposed follow-up: Build a compact paginated/filterable review workspace with focused accessible review, one reveal/conceal control, bounded image size, next-item progression, authorised dashboard totals and separate paged history.
- Requirements / decisions: CONFIRMED D178 screening/eligibility and restricted evidence remain. DEFAULT: reuse coded controls and existing lifecycle locks. OPEN: four-hour acknowledgement and governed private approval-note history (Q97). Support access is not implied: the current general media queue requires Admin. Retention and appeals are split to Q115.
- Duplicate assessment: distinct follow-up; related [#139](https://github.com/repasscloud/wanderconnect/issues/139), [#85](https://github.com/repasscloud/wanderconnect/issues/85), [#88](https://github.com/repasscloud/wanderconnect/issues/88), [#116](https://github.com/repasscloud/wanderconnect/issues/116). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q115"></a>

### Q115 — Define photo rejection notices, appeals and retention-safe reconsideration

Implementation checkpoint: [Q115 evidence](Q115-PHOTO-APPEALS-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D298** ([source](https://github.com/repasscloud/wanderconnect/issues/160#issuecomment-6572660549)): 160.1 A; 160.2 A; 160.3 Yes; 160.4 A; 160.5 A; 160.6 Yes. In-app photo rejection/outcome notices; one appeal within 30 days, different Admin decides, final. Suspected CSAM/known illegal/specialist media never appealable and no detailed reason. Keep rejected file privately 30 days then delete unless held. Admin may reconsider independently with reason and re-screening. Rejection remains hidden; ordinary approval cannot bypass screening/specialist/preservation. Existing D135 sanction appeals remain distinct. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#160](https://github.com/repasscloud/wanderconnect/issues/160); [intake item 22](ISSUE-INTAKE.md#intake-22).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — members need safe rejection transparency; reconsideration cannot rely on objects already deleted.
- Earliest blocker/consequence: acceptance of this proposed Member photos / notifications / moderation appeals slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 22 L–S and rejected-photo/member-notification/appeal acceptance.
- Source-supported finding: MediaPipeline.RejectAsync records rejection and invokes DeleteObjectIfAllowedAsync; rejected objects can be deleted when preservation permits. MediaEndpoints review/view handles uncertain items; ordinary decisions accept uncertain/passed states, not a general rejected-photo override. Existing sanction appeals (D135/#50) do not themselves implement photo-specific appeals. The current review code does not establish member rejection notifications or durable photo-appeal history.
- Expected/proposed follow-up: Provide private member-facing rejection explanations, eligible one-active-appeal-per-decision submission, scoped appeals/rejected queues and independent authorised reconsideration/outcome notices.
- Requirements / decisions: CONFIRMED D135 sanction appeal independence/30-day window must be reconciled explicitly for photo decisions, not silently generalised or weakened. CONFIRMED D178/D185/D191 evidence restrictions remain. OPEN: photo-specific eligibility/window/finality, reviewer capability and bounded retention; no blanket retention or ordinary approval of specialist-blocked media.
- Duplicate assessment: distinct follow-up; related [#139](https://github.com/repasscloud/wanderconnect/issues/139), [#50](https://github.com/repasscloud/wanderconnect/issues/50), [#85](https://github.com/repasscloud/wanderconnect/issues/85), [#118](https://github.com/repasscloud/wanderconnect/issues/118), [#88](https://github.com/repasscloud/wanderconnect/issues/88). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q116"></a>

### Q116 — Review universal DOB locking and temporary Admin-authorised member correction

Implementation checkpoint: [Q116 evidence](Q116-DOB-CORRECTION-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D299** ([source](https://github.com/repasscloud/wanderconnect/issues/161#issuecomment-6572733484)): 161.1 A; 161.2–161.4 Yes; 161.5 A; 161.6 A. Lock DOB after first confirmation, including existing populated dates immediately. Confirm before first save. Support request at https://swoovr.com/support; Admin/Root one-use audited 24-hour correction grant, never Support. Corrected ID-verified DOB compares encrypted D191 identity; match verified, mismatch manual DOB review with ID badge retained (D166). Previous DOB encrypted Legal-only under retention schedule. Explicitly supersedes D158 unverified-edit permission; preserves D166 provider-mismatch correction and non-punitive ID-badge handling. No existing DB deletion is authorized. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#161](https://github.com/repasscloud/wanderconnect/issues/161); [intake item 23](ISSUE-INTAKE.md#intake-23).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — changes affect adult eligibility, verification and sensitive historical identity data.
- Earliest blocker/consequence: acceptance of this proposed Member account / age integrity / Admin correction slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Entire item 23; linked follow-up to #33 rather than a duplicate of verified-DOB mismatch handling.
- Source-supported finding: MemberAccountPanel keeps an editable DOB form when DobCheck is none, even for verified DOB (with explanatory text). AccountEndpoints /me/age rejects verified-date changes and open correction/review states, but permits validated unverified-date changes; underage entries lock the account. That is consistent with CONFIRMED D158, not proof of a backend bypass. D166 already provides a one-use provider-mismatch correction and preserves ID Verified during manual DOB review. #33 covers that mismatch, not universal first-save locking.
- Expected/proposed follow-up: If the owner revises D158, confirm and lock initial DOB, expose read-only saved state, route support requests and allow one temporary member-bound correction authorised by the reviewed staff capability.
- Requirements / decisions: OPEN: universal locking supersedes CONFIRMED D158 allowing unverified changes; blanket re-verification/badge changes must reconcile D166. Historical DOB retention/access needs D191/D185 review. DEFAULT: 24-hour single-use grant is only a proposal. Onboarding order remains deferred. No existing issue is reopened or closed.
- Duplicate assessment: distinct follow-up; related [#33](https://github.com/repasscloud/wanderconnect/issues/33), [#148](https://github.com/repasscloud/wanderconnect/issues/148), [#83](https://github.com/repasscloud/wanderconnect/issues/83), [#88](https://github.com/repasscloud/wanderconnect/issues/88). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q117"></a>

### Q117 — Define registered-name collection and preserve separation from public and verified identity

Implementation checkpoint: [Q117 evidence](Q117-REGISTERED-NAMES-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D300** ([source](https://github.com/repasscloud/wanderconnect/issues/162#issuecomment-6572808962)): 162.1 A; 162.2 A; 162.3 Yes; 162.4 A; 162.5 Yes; 162.6 A; 162.7 A with owner condition; 162.8 B. Required surname with one-name exception; provider names confirmed/completed on first sign-in. Existing incomplete accounts complete before continuing (owner expects later DB reset, not authorized here). Initialize public name from first name only if unset. Support/Admin/Root see registered names. Members can edit registered names with audit, but ID-verified members lose ID Verified and must submit ID again. Staff see registered-name/ID match flag without decrypted names. Registered and verified identity remain separate; explicit owner badge revocation on name edits supersedes any general assumption that ID badge persists through that edit. D166 DOB mismatch handling remains. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#162](https://github.com/repasscloud/wanderconnect/issues/162); [intake item 24](ISSUE-INTAKE.md#intake-24).
- Status: OPEN investigation / feedback; intake processing complete. Priority: High — name collection, staff visibility and migration change privacy and identity semantics.
- Earliest blocker/consequence: acceptance of this proposed Keycloak registration / account identity / Admin / verification slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 24 except independent mode-specific public-name overrides; links Q110 search, Q111 details and Q116 DOB.
- Source-supported finding: scripts/keycloak_realms.py generates required firstName/email but no lastName in the member user profile; register.ftl renders those dynamic fields. Account stores issuer/subject and accepted email, with no registered FirstName/LastName fields. MemberProfile has a separate nullable DisplayName; AccountResolver does not initialise it from provider names. D191 already stores encrypted verified names behind Legal-only access, which must not become ordinary Admin registered-name data.
- Expected/proposed follow-up: After a reviewed collection/authority decision, collect registered first/last names, keep them distinct from verified identity and public display, optionally initialise an unset public name from first name only, and present permitted registered names consistently in Admin.
- Requirements / decisions: OPEN: required surname, registered-name authority/field matrix, provider comparison and existing-account completion policy. Item 24's 'Core product decisions' are report proposals, not amendments to the confirmed register. DEFAULT: keep custom public names and declared/verified identity separate. Per-mode names are split to Q118; mandatory verification is not authorised.
- Duplicate assessment: distinct follow-up; related [#149](https://github.com/repasscloud/wanderconnect/issues/149), [#144](https://github.com/repasscloud/wanderconnect/issues/144), [#145](https://github.com/repasscloud/wanderconnect/issues/145), [#83](https://github.com/repasscloud/wanderconnect/issues/83), [#99](https://github.com/repasscloud/wanderconnect/issues/99), [#88](https://github.com/repasscloud/wanderconnect/issues/88). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q118"></a>

### Q118 — Decide optional per-mode public display-name overrides and safe fallback

Implementation checkpoint: [Q118 evidence](Q118-MODE-NAMES-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D301** ([source](https://github.com/repasscloud/wanderconnect/issues/163#issuecomment-6572853394)): 163.1–163.3 A; 163.4 Yes. Optional display-name override in all four modes; empty override uses main public display name only, never registered name. Chat uses name for the mode where conversation began (one conversation across modes, D143). Explicitly supersedes D175 shared-display-name requirement; retain all other per-mode privacy rules and historical evidence. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#163](https://github.com/repasscloud/wanderconnect/issues/163); [intake item 24](ISSUE-INTAKE.md#intake-24).
- Status: OPEN investigation / feedback; intake processing complete. Priority: Medium — independent product/data-model change; the historical shared-name rule is superseded by D301.
- Earliest blocker/consequence: acceptance of this proposed Profile / mode privacy slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 24 D and mode-name portions of B/E/L/M and public-profile acceptance.
- Source-supported finding: MemberProfile owns DisplayName; MemberMode stores Enabled/Visible/About/ShareCoreAbout and intentions, with no per-mode name. CONFIRMED D175 explicitly shares display name across all modes. PR #154 preserved custom names and did not implement provider-name initialisation or per-mode overrides.
- Expected/proposed follow-up: If D175 is revised, allow optional independent mode names falling back to the main public name, with no registered-surname fallback or other-mode leakage.
- Requirements / decisions: OPEN: approval to replace shared D175 names, fallback/edit semantics and required migrations/projections. DEFAULT: no automatic override creation or overwriting. Registered-name collection/initialisation is separate Q117; independent main photos remain deferred Q108.
- Duplicate assessment: distinct follow-up; related [#149](https://github.com/repasscloud/wanderconnect/issues/149), [#37](https://github.com/repasscloud/wanderconnect/issues/37), [#73](https://github.com/repasscloud/wanderconnect/issues/73), [#91](https://github.com/repasscloud/wanderconnect/issues/91). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q119"></a>

### Q119 — Standardise Admin presentation and contextual guidance across routes

Implementation checkpoint: [Q119 evidence](Q119-ADMIN-PRESENTATION-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

- Issue: [#164](https://github.com/repasscloud/wanderconnect/issues/164); [intake item 25](ISSUE-INTAKE.md#intake-25).
- Status: source implementation in draft PR171; validation and acceptance gaps in the linked evidence. Priority: High — repeated development prose and inconsistent controls affect every operational workflow.
- Earliest blocker/consequence: acceptance of this proposed Admin / shared presentation / accessibility slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 25 A–I/M–O and shared presentation/security acceptance; J–L split to Q120.
- Source-supported finding: SupportLookup.razor contains the quoted #50/permission/logging paragraph. Account.razor exposes D-code headings; MediaReview.razor includes beta and implementation guidance. App.razor loads themes.css/admin.css, not a Bootstrap bundle; admin.css already provides shared controls, responsive layout and top-aligned td. The report's 'existing Bootstrap' premise is not established by the current shell. Functional beta/provider/safety distinctions remain operational facts.
- Expected/proposed follow-up: Audit every Admin route, use concise headings and consistent controls/tables/feedback, move useful guidance into accessible contextual panels and preserve required warnings and logging.
- Requirements / decisions: CONFIRMED docs/design is read-only, Standard staff styling and D185/D197 audit/security remain. DEFAULT: reuse current CSS/components; framework adoption needs a deliberate dependency choice. OPEN: any removal of required disclosures requires owning policy review; legal-draft approval remains deferred. Device availability is separately Q120.
- Duplicate assessment: distinct follow-up; related [#140](https://github.com/repasscloud/wanderconnect/issues/140), [#139](https://github.com/repasscloud/wanderconnect/issues/139), [#88](https://github.com/repasscloud/wanderconnect/issues/88), [#48](https://github.com/repasscloud/wanderconnect/issues/48), [#135](https://github.com/repasscloud/wanderconnect/issues/135). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

<a id="q120"></a>

### Q120 — Assess Admin workflow availability on desktop, tablet and mobile

Implementation checkpoint: [Q120 evidence](Q120-ADMIN-DEVICE-MATRIX-2026-11-02.md) contains the code, actual validation and remaining acceptance. Source implementation is in this draft PR; provider/physical/hosted gaps are not represented as passed.

Owner answer recorded 2 November 2026 — **CONFIRMED D302** ([source](https://github.com/repasscloud/wanderconnect/issues/165#issuecomment-6572882262)): 165.1 Yes; 165.2 A; 165.3 A; 165.4 Yes. Approve route matrix in owner questions; desktop-only is presentation, not trusted-device authorization. Tablets have desktop access, including photo review. Phones see This page needs a larger screen and overview link on desktop workflows. Backend role/elevation/conflict controls remain authoritative. Corrects D297 cross-reference: tablet policy is D302. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

- Issue: [#165](https://github.com/repasscloud/wanderconnect/issues/165); [intake item 25](ISSUE-INTAKE.md#intake-25).
- Status: source implementation in draft PR171; validation and acceptance gaps in the linked evidence. Priority: Medium — new availability restrictions require operational and accessibility review before enforcement.
- Earliest blocker/consequence: acceptance of this proposed Admin / supported devices / access policy slice; independent intake work continues. No deployment or provider change authorised.
- Scope: Item 25 J–L and responsive availability criteria; conflicts with item 22 D recorded explicitly.
- Source-supported finding: Current Admin shell/CSS adapts layouts to narrow viewports; no route-level supported-device matrix is established by the inspected shell. The proposed table labels media/legal/root routes desktop-only, while item 22 proposes an adapted mobile/tablet review dialog. This conflict requires a route decision. A resized viewport or user-agent is not evidence of a trusted device and cannot enforce sensitive backend access.
- Expected/proposed follow-up: Agree a route/workflow availability matrix, retain useful permitted mobile functions, and provide branded unsupported-workflow recovery where justified.
- Requirements / decisions: OPEN: route classifications, meaning of desktop-only and device trust/enforcement requirements; the report table is a proposed starting point. DEFAULT: preserve existing role permissions and accessible reflow while policy is unresolved. No device deny rule or new authentication requirement is approved by this intake.
- Duplicate assessment: distinct follow-up; related [#88](https://github.com/repasscloud/wanderconnect/issues/88), [#48](https://github.com/repasscloud/wanderconnect/issues/48), [#135](https://github.com/repasscloud/wanderconnect/issues/135), [#139](https://github.com/repasscloud/wanderconnect/issues/139). Split dependencies and detailed criteria are in the issue and [run record](ISSUE-INTAKE-2026-11-02.md).
- Validation gap: no runtime reproduction or image review; exact capture revision and browser traces unavailable. Use fictional/synthetic evidence only; current source does not establish native/device, throughput or hosted acceptance.

## Legal review update — 2 November 2026

CONFIRMED D288: owner confirms legal approval of the supplied policy/privacy/terms revisions. [Exact source snapshot and scope](LEGAL-APPROVAL-2026-11-02.md). This resolves the pending legal wording review for those revisions; it is not a new question. Issue57 retains separate application-security/app-store gates;119 retains organisational work. Existing runtime draft/catalogue state is unchanged pending publication/activation.

## Owner follow-up #124 — 2 November 2026

Owner answer recorded 2 November 2026 — **CONFIRMED D291** ([source](https://github.com/repasscloud/wanderconnect/issues/124#issuecomment-6572260299)): 124.1 A; 124.2 A; 124.3–124.4 Yes; 124.5 A; 124.6 A; 124.7–124.8 Yes. Australia East, Container Apps, separate Keycloak Container App/PostgreSQL databases/logins and nondev realms; uat.swoovr.app plus admin/api/auth.uat.swoovr.app; paid edge coverage and origin certificate in Key Vault; HTTPS Cloudflare proxy without Tunnel. Approved encrypted backup schedule and naming/tags from owner questions. Owner creates subscription later and deploys personally or by script; 124.10 A until first UAT run. Approves D221/D283 topology preparation only. No purchase, provisioning or deployment; subscription remains owner-action. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

## Owner follow-up #57 — 2 November 2026

Owner answer recorded 2 November 2026 — **CONFIRMED D303** ([source](https://github.com/repasscloud/wanderconnect/issues/57#issuecomment-6576118805)): 57.1a not chosen yet; 57.1b B; 57.2 owner reviews app-store policy personally; 57.3 Yes. Invited UAT can precede application-security and store-policy reviews; unrestricted external beta waits. Legal wording approved under D288/D290; no penetration test, store compliance or public launch certified. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

## Owner follow-up #119 — 2 November 2026

Owner answer recorded 2 November 2026 — **CONFIRMED D304** ([source](https://github.com/repasscloud/wanderconnect/issues/119#issuecomment-6576252928)): 119.1 owner (me for now) is Trust and Safety Leader; 119.2 C, decide police cooperation later; 119.3 self-assessment just before public beta; 119.4 later, needs a list of forums. Appointment recorded; no completed training, staffing/urgent coverage, agreement, assessment or Code compliance claim. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

## Owner follow-up #127 — 2 November 2026

Owner answer recorded 2 November 2026 — **CONFIRMED D305** ([source](https://github.com/repasscloud/wanderconnect/issues/127#issuecomment-6576814746)): 127.1 Firebase swoovr-uat/com.swoovr.app exists and is approved for local tests for now; separate dev project later. Apple account/key not ready. 127.2 Samsung Galaxy A14 5G Android15; iPhone16 Pro Max iOS27.0.1. 127.3 Firebase files ready at .local/firebase/google-services.json and fcm-service-account.json. Wire Android FCM per D210 with backed-up private env/launcher; never publish keys. Physical-device delivery remains owner acceptance; APNs later. Keep #127 open. Implementation/acceptance is tracked in [overnight delivery](OVERNIGHT-DELIVERY-2026-11-02.md); recording is not completion of the code.

<a id="q121"></a>

### Q121 — Firebase Android package mismatch

Owner answer recorded 3 November 2026 — **CONFIRMED D306** ([source](https://github.com/repasscloud/wanderconnect/issues/170#issuecomment-6596650785)): two projects; UAT/local = `swoovr-uat` + `com.swoovr.app.uat` (`-uat` files), production = `swoovr-prod` + `com.swoovr.app`. Files ready in `.local/firebase/`. Recording is not completion of the code.

- Issue: [#170](https://github.com/repasscloud/wanderconnect/issues/170); follows Q83/#127 and D305.
- Status: ANSWERED (D306), built 5 November 2026: Android UAT and local builds use `com.swoovr.app.uat` with the `swoovr-uat` Firebase client file (default `.local/firebase/google-services-uat.json`); iOS and the future production Android target keep `com.swoovr.app`. A Firebase file registered for another package fails the build. Physical-device delivery stays with #127.
- 170.1 A: register current `com.swoovr.app` in Firebase and replace local config (**Suggested**). B: explicitly approve different provider-test package and review callback/signing implications.
- 170.2 Confirm corrected file ready at `.local/firebase/google-services.json` after A. Never paste keys.
- No private file was changed because the consistency check failed first. Physical-device/APNs acceptance stays OPEN. Independent work continues.

<a id="q122"></a>

### Q122 — Safety industry forum shortlist

- Issue: [#172](https://github.com/repasscloud/wanderconnect/issues/172); owner feedback 119.4, CONFIRMED D304.
- Status: OPEN later / product-review, no current code blocker.
- 172.1 A: research a concise evidence-backed Australian safety/dating industry forum shortlist before public-beta review; owner selects memberships later (**Suggested**). B: defer until after invited UAT.
- No joining, payments, outreach, membership or compliance claim is authorised. Preserve the owner's request for a list rather than silently choosing organisations.

<a id="q123"></a>

### Q123 — Guest allowlist and trusted-contact safety capability pages

Owner answer recorded 3 November 2026 — **CONFIRMED D308** ([source](https://github.com/repasscloud/wanderconnect/issues/173#issuecomment-6596651502)): signed-out/signed-in `/safety` versions; "I'm meeting someone" members only; guest incident reports go to https://swoovr.com/support; trusted-contact links open while unexpired/unrevoked, valid 72 hours from generation (sole guest exception). Recording is not completion of the code.

- Issue: [#173](https://github.com/repasscloud/wanderconnect/issues/173); conflict between latest CONFIRMED D290/Q99 and earlier D263 non-member trusted-contact readers.
- Status: ANSWERED (D308). 173.1 built 5 November 2026 (#211): the trusted-contact and meeting-status capability pages (`/safety/contact/…`, `/safety/meeting/…` and their forms) now open signed out; before, the signed-out gate sent them to `/`. Token, expiry, consent, code and rate checks are unchanged. Verified locally: a signed-out contact opened an emailed invitation and accepted, and the API recorded Confirmed. Remaining D308 items (signed-out/signed-in `/safety` versions, guest routing) are tracked on #173.
- 173.1 A: narrowly allow existing token/code-protected contact/meeting workflows with all consent/expiry/rate/privacy controls (**Suggested**). B: keep the absolute six-page guest rule and explicitly remove/disable that non-member sharing journey.
- The current implementation follows the latest strict six-page instruction, so contact/meeting capability pages redirect home. These rendered pages are not honestly classifiable as static/authentication protocol resources. No anonymous exception or feature removal is inferred; independent work continues.

### Q124 — Record policy acceptance when an account is created

- Issue: [#179](https://github.com/repasscloud/wanderconnect/issues/179#issuecomment-6619031661). Status: ANSWERED A (D316), owner 4 November 2026.
- Registration now uses the owner's wording (D312): "By creating an account, you agree to our Terms of Use and acknowledge our Privacy Notice and Community Guidelines."
- A (**Suggested**): record an acceptance of version 2026-11-02-0.2 (account, version, server time) automatically at account creation; the 18+ checkbox stays the only tick. B: wording only, no stored record.
- Built meanwhile: B (wording only).

### Q125 — Registered name change and the ID Verified badge

- Issue: [#180](https://github.com/repasscloud/wanderconnect/issues/180#issuecomment-6619036937). Status: ANSWERED A (D317), owner 4 November 2026.
- A (**Suggested**): staff compare the change with the verified identity record; the badge stays until staff decide. B: the member repeats the Didit check; the badge stays until the new result. C: keep D300 (badge removed at once).
- Built meanwhile: the D300 rule is unchanged and stated on the change form.

### Q126 — Security & access scope

- Issue: [#180](https://github.com/repasscloud/wanderconnect/issues/180#issuecomment-6619036937), [update](https://github.com/repasscloud/wanderconnect/issues/180#issuecomment-6619358193). Status: ANSWERED A (D317), owner 4 November 2026. Follow-up: sessions and linked sign-ins, #185.
- A (**Suggested**): Keycloak application-initiated actions now (change password, add passkey; change email once the realm enables update-email); active sessions and linked sign-ins as a follow-up API. B: enable Keycloak's account console, themed.
- Built meanwhile: A for password and passkey (D314). Email change waits for the realm's update-email step.
- Follow-up built: active sessions and linked sign-ins in Security & access (#185, D341 DEFAULT).

### Q127 — "Deactivate account"

- Issue: [#180](https://github.com/repasscloud/wanderconnect/issues/180#issuecomment-6619036937). Status: ANSWERED A (D317), owner 4 November 2026.
- A (**Suggested**): no separate deactivate; Delete account hides the account at once and can be recovered for 30 days (D170). B: a new reversible deactivated state.
- Built meanwhile: A.

### Q128 — Reporting to Swoovr through Zammad

- Own issue: [#188](https://github.com/repasscloud/wanderconnect/issues/188) (decision-needed).
- Issue: [#181](https://github.com/repasscloud/wanderconnect/issues/181#issuecomment-6619038197). Status: OPEN (decision-needed; owner-action for Zammad URL, API token secret and group names).
- A (**Suggested**): ship the redesign; "Report to Swoovr" keeps D308 routing (guests → https://swoovr.com/support, members → in-app serious report) until Zammad details are supplied. B: hold the reporting change.
- Built meanwhile: A.

### Q129 — Version number in the public footer

- Own issue: [#189](https://github.com/repasscloud/wanderconnect/issues/189) (decision-needed).
- Issue: [#181](https://github.com/repasscloud/wanderconnect/issues/181#issuecomment-6619038197). Status: ANSWERED B (D320), owner 4 November 2026: version visible everywhere, centralised Major.Minor.Build versioning. Conflicts with the 4 November chat request to keep the version under the footer links.
- A: remove from signed-out pages; keep for members and on the sign-in page. B (current): keep everywhere.
- Built meanwhile: B.

### Q130 — Remembering the Safety & Support country vs the Privacy Notice

- Issue: [#181](https://github.com/repasscloud/wanderconnect/issues/181#issuecomment-6619048054). Status: ANSWERED A (D318), owner 4 November 2026; Privacy Notice update tracked in #183.
- The approved notice lists essential cookies/device storage "only" for sign-in, form protection, trusted-contact access and appearance choices.
- A: remember the country for one year once "your Safety & Support country" is added to the notice through the legal process. B (**Suggested for now**): keep the browsing-session cookie until then.
- Built meanwhile: B.

### Q131 — Staff roles in Keycloak as well as the API?

- Issue: [#192](https://github.com/repasscloud/wanderconnect/issues/192). Status: ANSWERED A (D324), owner 4 November 2026; tracking [#191](https://github.com/repasscloud/wanderconnect/issues/191).
- The owner asked for the Trust & Safety role to "exist in Keycloak etc too". The current design keeps roles in the API only; Keycloak has no realm roles and no role is read from a token.
- A (**Suggested**): API-only roles, plus local fictional staff sign-ins per role. B: also mirror realm roles in the staff realm for display only. C: Keycloak roles become authoritative (reverses the design).
- Built: A (D324).

### Q132 — Restriction: full bar or selected capabilities?

- Issue: [#194](https://github.com/repasscloud/wanderconnect/issues/194). Status: ANSWERED B (D325, supersedes D160), owner 4 November 2026; tracking [#191](https://github.com/repasscloud/wanderconnect/issues/191).
- D160 (Q55) bars restricted members from the whole app; issue 190 §12 asks for restrictions that limit only chosen capabilities.
- A: keep D160. B (**chosen and built**): enforce only the ticked capabilities, with suspension as the full bar.

### Q133 — How do staff reach sensitive support queues from Admin?

- Issue: [#204](https://github.com/repasscloud/wanderconnect/issues/204). Status: ANSWERED B plus queue matrix and direct Zammad access (D327), owner 5 November 2026; built in PR [#203](https://github.com/repasscloud/wanderconnect/pull/203).
- Sensitive helpdesk queues (Legal, Restricted T&S, Privacy, Security) require elevation, a recent sign-in and a pinned TLS client certificate per staff account (DEFAULT). The Admin host presents none, so those queues are unreachable (not leaked) in Admin.
- A: per-staff certificates for the Admin host. B (**chosen and built**): drop the certificate rule; role + elevation + fresh direct sign-in. C: B now, desktop/managed-device check once Swoovr has attestation.
- Built: B, the owner's queue matrix (Appeals Trust & Safety and Admin only, confirmed), and Swoovr-mirrored Zammad web access through the staff realm (D327).

### Q134 — Show emailed messages from a member's address inside the app?

- Issue: [#205](https://github.com/repasscloud/wanderconnect/issues/205). Status: ANSWERED A (D328), owner 5 November 2026; PR [#203](https://github.com/repasscloud/wanderconnect/pull/203).
- Email senders can be forged. DEFAULT (built): emailed customer messages and email-opened tickets aren't shown to members/guests; staff see them labelled "sender not verified".
- A (**chosen**): keep; no change. B: show them marked "received by email". C: show only when SPF/DKIM/DMARC aligns with the verified address (needs the real mailbox).

### Q135 — Malware scanning for member support attachments

- Issue: [#221](https://github.com/repasscloud/wanderconnect/issues/221). Status: ANSWERED 6 November 2026, Option A with the pinned ClamAV digest → **D336**, built on branch `feature/helpdesk-retention-attachment-scanning`. Raised 6 November 2026 while building #220 (D330, PR [#218](https://github.com/repasscloud/wanderconnect/pull/218)). Follow-ups Q137 and Q138.
- Members can now attach images, documents, text, video and audio to support requests. The API checks each file's content matches its type and never renders files in the page; no malware scanner is configured, and staff download the files.
- A (recommended before production): scan on upload (for example ClamAV beside the API) and refuse or quarantine anything flagged. B (DEFAULT, built): signature checks only, staff download with endpoint protection. C: fewer binary types until a scanner exists.

### Q136 — How to run helpdesk retention and customer retirement

- Issue: [#224](https://github.com/repasscloud/wanderconnect/issues/224). Status: ANSWERED 6 November 2026, "136.1 A, 136.2 2a, 136.3 3a" → **D337**, built on branch `feature/helpdesk-retention-attachment-scanning`. Raised 6 November 2026 from [#207](https://github.com/repasscloud/wanderconnect/issues/207) (policy decided, D333).
- Options in [zammad/RETENTION-OPTIONS.md](zammad/RETENTION-OPTIONS.md). 136.1 where the job runs: A (recommended) step in the API's hourly `RetentionWorker` / `retention purge`; B external scheduler (crontab, Container Apps Job, supercronic sidecar) calling the same CLI; SQL-only and Zammad's own scheduler not recommended. 136.2 category: 2a (recommended) Zammad group plus audited Admin override; 2b Zammad custom field; 2c group only. 136.3 retired login: 3a (recommended) `retired-{random}`; 3b `retired-{memberId}`.
- Customer retirement on final deletion is built first whatever is chosen.

### Q137 — Emailed attachments downloaded directly in Zammad

- Issue: [#226](https://github.com/repasscloud/wanderconnect/issues/226). Status: OPEN, raised 6 November 2026 from #221 (D336).
- Swoovr scans everything it sends to Zammad, and every download through Swoovr. Emailed files reach Zammad without passing Swoovr, and staff signed in to Zammad's own web UI (D327) can download them there unscanned. A (recommended): files only through Admin, by procedure plus a notice on the Zammad hand-off. B: turn off direct Zammad web sign-in for staff. C: accept the gap and rely on endpoint antivirus.

### Q138 — ClamAV image vulnerabilities, UnRAR and signatures

- Issue: [#227](https://github.com/repasscloud/wanderconnect/issues/227). Status: OPEN, raised 6 November 2026 from #221 §8 (D336); reports in `supply-chain/clamav-c21aa18cc0a3/`.
- 9 Critical (libcurl, libxml2; Debian wont-fix) and 101 High base-package matches; the UnRAR library under its own licence; publisher signatures not verified (no cosign here). A (recommended): accept for UAT with compensating controls, then review a newer digest before production. B: pick a new digest now. C: accept, but drop RAR scanning (needs an attachment-type change).

### Q139 — Does a not-approved photo use a library spot while it can be appealed?

- Issue: [#233](https://github.com/repasscloud/wanderconnect/issues/233). Status: OPEN, raised 7 November 2026 while building #231 (D338).
- A refused photo stays in the member's library, blurred, for the 30-day appeal window. A (recommended, current behaviour): it uses a library spot until deleted or the window closes. B: it doesn't count, so a refusal never blocks uploads, but the library can hold more than 60 items.

### Q140 — Operational identity review for MFA recovery

- Issue: [#243](https://github.com/repasscloud/wanderconnect/issues/243). Status: OPEN, raised 7 November 2026 while implementing [#193](https://github.com/repasscloud/wanderconnect/issues/193).
- Which approved identity-review procedure, evidence and training/record requirements should independent Admins use before resetting lost MFA? The implementation requires elevated independent Admin, case reference, identity-review attestation and confirmation, prevents self/Root reset and preserves mandatory staff enrolment. An attestation does not prove identity or case outcome. Operational assisted recovery awaits the approved runbook; independent enrolment, enforcement and native recovery work continues. No new document collection or bypass is proposed.

## Location redesign — [#257](https://github.com/repasscloud/wanderconnect/issues/257), 8 November 2026

Asked on #257 in three rounds: [Q141–Q148](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6717972325), [Q149–Q158](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6718580356) and [Q152–Q169](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719049155). The owner answered in the [7:06](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6718555917), [7:59](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719013875), [8:00](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719019159) and [8:13](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510) comments. Answered questions are recorded on #257. Each question still open has its own issue, which links back here. Decisions: D345–D351.

### Q141–Q148 — First-round Area and GPS questions

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6717972325). Status: ANSWERED or SUPERSEDED, owner 8 November 2026 (D345).
- Q141 (suburbs as Home City): Home City is the registered market; existing GeoNames ids stay (D345).
- Q142, Q144 and Q146 (typed Area fallback, existing values, Area on Home City change): superseded by retiring Area; see Q158.
- Q143 (GeoNames `cities500`): not needed, because there is no locality picker.
- Q145 (GPS outside Home City): superseded by physical presence (D346).
- Q147 (distance bands): D347 and D348.
- Q148 (Privacy Notice): see Q165.

### Q149 — What is the "Grid"?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257). Status: ANSWERED, owner 8 November 2026 (D345): the Grid is Explore.

### Q150 — Cross-market scope for GPS nearby

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719013875). Status: ANSWERED, owner 8 November 2026 (D346). Not limited to the home market: physical presence in any enabled market, with no trip needed.

### Q151 — What does a manual Current Location show?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719013875). Status: SUPERSEDED, owner 8 November 2026 (D345): no manual location and no IP fallback.

### Q152 — Do you see others' distance bands only while showing your own?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D348): yes.

### Q153 — Existing Home City bands and the Plus distance filter

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719019159). Status: ANSWERED, owner 8 November 2026 (D347, D348). Filters by tier (Free 5–50 km; paid tiers also 100/250/500 km). Bands are optional, off by default, and labelled with their basis.

### Q154 — Order of Near me results

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D348): band first, then Recent/Compatibility, never exact distance.

### Q155 — Refuse registration outside enabled markets?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D345): no change.

### Q156 — Do Nearby updates count as D150 home checks?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719019159). Status: ANSWERED, owner 8 November 2026 (D349). D150 does not run on Near me updates; a separate extended-stay evaluation does.

### Q157 — One "Appear in nearby searches" setting, or one per mode?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D348): one master switch plus the existing per-mode visibility.

### Q158 — Retiring Area: existing data and old clients

- Own issue: [#269](https://github.com/repasscloud/wanderconnect/issues/269) (decided). Status: ANSWERED, owner 8 November 2026 (D350).
- Area is removed outright from the schema, screens and contracts. Current data is fictional, so there is no preservation period and no staged migration.
- Compatibility check: no distributed mobile builds, so the field is not kept for old clients. Historical test snapshots are not treated as evidence.

### Q159 — How Explore presents Home market and Near me

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED (D345): a separate Home market / Near me control, with Home market as the default.

### Q160 — Who a present member sees in Near me

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D346): opted-in members present by GPS, plus the market's locals by Home City.

### Q161 — Near me: freshness, the 0–2 km band and approximate location

- Own issue: [#270](https://github.com/repasscloud/wanderconnect/issues/270) (decided). Status: ANSWERED, owner 8 November 2026 (D348).
- A reading is used for exactly 2 hours, and a 5-minute purge deletes it within the following 5 minutes. The immediate deletions stay.
- "Appear" is checked live on every query, so browsing never makes a member discoverable.
- `nearby` needs both readings ≤ 250 m and ≤ 15 minutes old, otherwise `unknown`.
- Near me presence needs a reading accurate to 2 km or better.

### Q162 — Display when GPS isn't available but Home City distance is

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D348): a Home City band labelled as such (`basis: homeCity`), never presented as GPS.

### Q163 — What 100/250/500 km mean

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D345): within the market the member is in; no remote reach.

### Q164 — Not used

- Number skipped on #257.

### Q165 — Privacy Notice 0.20 and permission wording

- Own issue: [#271](https://github.com/repasscloud/wanderconnect/issues/271) (decided). Status: ANSWERED, owner 8 November 2026 (D351). The [final wording](https://github.com/repasscloud/wanderconnect/issues/271#issuecomment-6720038803) was [recorded as approved by the owner](https://github.com/repasscloud/wanderconnect/issues/271#issuecomment-6720045158).
- Published by [#283](https://github.com/repasscloud/wanderconnect/issues/283) with a member notification.

### Q166 — Scheduled Home City check: evidence, vicinity, Zammad item and retention

- Own issue: [#272](https://github.com/repasscloud/wanderconnect/issues/272) (decided). Status: ANSWERED, owner 8 November 2026 (D349): [approved in principle](https://github.com/repasscloud/wanderconnect/issues/272#issuecomment-6719818807) and [vicinity option B](https://github.com/repasscloud/wanderconnect/issues/272#issuecomment-6719983753), a configurable 25 km radius around the registered Home City.
- Implementation: [#281](https://github.com/repasscloud/wanderconnect/issues/281).

### Q167 — What happens to D150's current mechanism

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: ANSWERED, recommendation accepted (D349): retire the three-checks flag and the IP evidence; keep the button and the admin override.

### Q168 — Location review routing and roles

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: SUPERSEDED, owner 8 November 2026 (D349): a Zammad support queue item from the scheduled Home City check replaces the Admin list (see Q166).

### Q169 — Ask the member first?

- Issue: [#257](https://github.com/repasscloud/wanderconnect/issues/257#issuecomment-6719138510). Status: SUPERSEDED, owner 8 November 2026 (D349). There is no in-app prompt; Support may send a friendly suggestion from the Zammad item.

### Q170 — Travel: checked-in destination as the active discovery location

- Own issue: [#274](https://github.com/repasscloud/wanderconnect/issues/274) (decided). Status: ANSWERED, owner 8 November 2026 (D352).
- Two independent pre-arrival choices (amends the D164 default).
- The most recently checked-in trip is the active location, and the Home City comes back when none is active.
- Home-market members still see the member; destination members see them in Explore and the visitors feed, marked as visiting.
- GPS bands only with fresh readings and permission on both sides, otherwise `unknown`.
- Near me follows GPS and Active location follows the check-in; neither changes silently.


### Q171 — Vultr environment, capacity and latency targets

- Issue: [#295](https://github.com/repasscloud/wanderconnect/issues/295); parent [#294](https://github.com/repasscloud/wanderconnect/issues/294). Status: OPEN. Earliest blocking phase: Before hosted sizing/acceptance.
- Region, CPU architecture, VM resources, peak/sustained uploads and queue-to-ready latency/budget. DEFAULT: measure current upload limits and one-VM maintenance capacity before choosing a paid tier.
- Design: [Vultr image pipeline](../superpowers/specs/2026-11-08-vultr-image-pipeline-design.md). Independent contracts/build/local verification can continue. Never post private credentials to the issue.


### Q172 — Raw-original retention and privacy approval

- Issue: [#296](https://github.com/repasscloud/wanderconnect/issues/296); parent [#294](https://github.com/repasscloud/wanderconnect/issues/294). Status: OPEN. Earliest blocking phase: Before raw-image ingress is enabled.
- Approve raw-original lifetimes for pass, unresolved review/failure, rejection and appeals, plus privacy/provider disclosure. Existing photos discard originals. DEFAULT: minimise retention; existing stronger review/evidence/hold rules and active appeals override disposal.
- Design: [Vultr image pipeline](../superpowers/specs/2026-11-08-vultr-image-pipeline-design.md). Independent contracts/build/local verification can continue. Never post private credentials to the issue.


### Q173 — Scanner freshness and re-screen policy

- Issue: [#297](https://github.com/repasscloud/wanderconnect/issues/297); parent [#294](https://github.com/repasscloud/wanderconnect/issues/294). Status: OPEN. Earliest blocking phase: Before hosted release.
- Maximum signature age, result freshness and policy/model-change re-screen triggers. DEFAULT: unknown/stale mandatory evidence fails closed; preserve mandatory fresh scan on successful appeal/reconsideration; callback retries reuse valid durable results.
- Design: [Vultr image pipeline](../superpowers/specs/2026-11-08-vultr-image-pipeline-design.md). Independent contracts/build/local verification can continue. Never post private credentials to the issue.


### Q174 — Provider/model hosted-use readiness

- Issue: [#298](https://github.com/repasscloud/wanderconnect/issues/298); parent [#294](https://github.com/repasscloud/wanderconnect/issues/294). Status: OPEN. Earliest blocking phase: Before hosted release.
- Confirm Shield account credentials/agreement, two-VM rate-limit/IP acceptance and permitted harmless provider acceptance; model licences including NudeNet discrepancy; region/disclosure and TLS readiness. DEFAULT: configured adapters and mocks are not live provider proof.
- Design: [Vultr image pipeline](../superpowers/specs/2026-11-08-vultr-image-pipeline-design.md). Independent contracts/build/local verification can continue. Never post private credentials to the issue.

### Q175 — Origin TLS for the Vultr UAT host

- Issue: [#307](https://github.com/repasscloud/wanderconnect/issues/307). Status: ANSWERED (owner, 10 November 2026; closed). Earliest blocking phase: Before the first Vultr UAT deployment.
- **CONFIRMED** by [owner #307](https://github.com/repasscloud/wanderconnect/issues/307): Cloudflare Origin CA certificate, PEM, RSA 2048, 15 years, explicit SANs only (`uat`, `api-uat`, `admin-uat`, `helpdesk-uat`, `auth-uat`, `venues-uat` `.swoovr.app`), no wildcard; stock Caddy loads `/etc/caddy/certs/uat-origin.pem` and `uat-origin.key` from read-only mounts, provisioned on the host outside Git; Full (strict); no ACME and no Cloudflare API token on the host; system CA trust preserved, with the Origin CA roots added only for containers that call Origin CA endpoints directly; UAT certificate and key independent of production and never committed.
- Implemented in #309: `bin/origin-cert-check` runs before every command that starts Caddy and fails unless both files exist, are PEM, RSA 2048, carry all six SANs and no wildcard, match each other and the key is 0600. It never generates a certificate. Compose binds `/etc/caddy/certs` read-only without creating it.
- Original DEFAULT (prepared in `deploy/vultr-uat`): Cloudflare Origin CA certificate for `swoovr.app` + `*.swoovr.app` on the stock owner-selected Caddy. There is no DNS token on the host, and containers trust it through a CA bundle. Alternative: Let's Encrypt DNS-01, which needs a custom Caddy image and a zone-edit token on the host.
- Plan: [images and bundle](UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md). Independent work continues.

### Q176 — UAT helpdesk hostname and Zammad outgoing mail

- Issue: [#308](https://github.com/repasscloud/wanderconnect/issues/308). Status: ANSWERED (owner, 10 November 2026). Earliest blocking phase: Before staff use the hosted UAT helpdesk.
- **CONFIRMED** (owner in chat, 10 November 2026): `helpdesk-uat.swoovr.app`, now listed in D355.
- **CONFIRMED** by the [owner on #308](https://github.com/repasscloud/wanderconnect/issues/308#issuecomment-6759649647): MailerSend SMTP (`smtp.mailersend.net`, 587 or 2525). Recorded as D357. The bundle reads the SMTP username, password and sender from `env/mail.env` (D358).
- Original DEFAULT: notifications stay unsent until chosen.
- Plan: [images and bundle](UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md).

### Q177 — One email configuration (host, port, username, password, From) for every sender

- Issue: [#311](https://github.com/repasscloud/wanderconnect/issues/311). Status: ANSWERED (owner, 11 November 2026, [#316](https://github.com/repasscloud/wanderconnect/issues/316)): providers are adaptive by env vars; the API (and Keycloak, DEFAULT) use the Twilio Email API, Zammad uses SendGrid SMTP, and SMTP stays available for anything else. Recorded as D360.
- Owner request (chat, 10 November 2026): wherever email credentials are used, configure host, port, username, password and From, so From can change by editing one `.env` file and restarting. Sender and single file done as D358 (`env/mail.env`, `bin/swoovr mail-apply`).
- **OPEN:** should the API and Keycloak move from the MailerSend HTTP API (D39, CONFIRMED) to SMTP with the same four values? **DEFAULT (A):** keep D39; SMTP host/port/username/password are used by Zammad only. **B:** supersede D39; needs a hosted TLS SMTP sender in the API, Keycloak's built-in SMTP instead of the Swoovr SPI, and a re-check of the acceptance/rejection outcome handling.

### Q178 — Inbound support email into Zammad on UAT

- Issue: [#312](https://github.com/repasscloud/wanderconnect/issues/312). Status: ANSWERED (owner, 11 November 2026): **B**, an IMAP mailbox on the owner's own mail server, queued to General Support. Implemented as D360 (`env/mail.env` `SWOOVR_ZAMMAD_IMAP_*`).
- UAT has no inbound email channel. **DEFAULT (A):** none; members use in-app support. **B:** an IMAP/POP3 mailbox for a UAT-only support address. **C:** MailerSend inbound routing to Zammad.
