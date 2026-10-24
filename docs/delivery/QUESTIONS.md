# Ordered question backlog

No answer is needed to review the phase 0 foundation. Ask these when approaching their blocking phase; do not ask all at once.

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
5. **Private albums:** Free members may create one private photo album. Plus and Premium members may create additional albums, with exact allowances defined separately.
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

Raised by the builder's [review of 24 October 2026](REVIEW-2026-10-24.md) for slices O–U (order and reasons there). All OPEN. Earliest blocker first. Answer in the **Owner answer** column; "default" accepts the recommendation. Provider values (keys, secrets) never go in this file or in chat: the questions say where they should be put.

| ID | Status | Blocks | Question | Why it matters | Recommended default | Owner answer |
| --- | --- | --- | --- | --- | --- | --- |
| Q24 | OPEN | O (market-scoped home discovery) | Should Explore, opening a profile from Explore and sending a request without a trip work only between members whose home cities are in the same active market? What does a member with no home city, or one living outside every active market, see? Where do official bots appear? | Today Explore ignores markets, so a member from Sydney sees Adelaide members and can request them before arriving. That bypasses "travellers get no feed of locals" and "no traveller initiation before arrival" (Q17, D60, D122) and Q07 rule 2 | Yes. Both need a home city in the same active market (Adelaide). No home city: "Add your home city to see people near you". Outside every active market: "Swoovr isn't open where you live yet"; they can still edit their profile, plan trips, answer requests and see existing connections. Existing connections and pending requests across markets are kept. Staff follow the rule like anyone else; an official bot appears in every active market for its authorised modes | |
| Q25 | OPEN | P (ID verification) | Which Stripe account will the Identity sandbox use (RePass Cloud Pty Ltd's, test mode only), and has its Stripe Identity application been submitted and approved? Stripe decides Identity eligibility by business location and use case, so an adult-connection app may need review. Will you put a restricted test-mode key and the webhook signing secret in `.local/docker.env` as `SWOOVR_STRIPE_SECRET_KEY` and `SWOOVR_STRIPE_WEBHOOK_SECRET`, and run `stripe login` so I can forward events with `stripe listen`? | Without your keys and an approved application no real test-mode verification can run; I won't invent either | A restricted `rk_test_` key limited to Identity verification sessions (read and write); events forwarded locally by the Stripe CLI to `http://localhost:5080/api/v1/webhooks/stripe`, with the `whsec_` secret `stripe listen` prints copied into the env file by you. Until then I build against a signed-event simulator used only by tests, shown as "Simulated — not Stripe", and report the real test-mode run as not done | |
| Q26 | OPEN | P | In the beta, is ID verification optional (it gives the ID badge, a Verified age status and founder activation) or required before discovery and contact (the DEFAULT gate in `04-IDENTITY-TRUST-AND-SAFETY.md` §2)? Does an age review (D40 `NeedsVerification`) require it? | Decides who can browse and contact; each check is charged to your Stripe account | Optional in the beta. Required only when staff ask for an age check: that account stays out of discovery and requests until verified or overridden (as `NeedsVerification` already does). Revisit the gate before public launch | |
| Q27 | OPEN | P | Which checks and what Swoovr keeps: document plus a matching live selfie? Which documents (passport, driver licence, national ID card)? Do we keep the verified date of birth (replacing what the member typed) or only "adult: yes" and the check date? Should Swoovr ask Stripe to redact each session, and after how long? Will you supply the consent and privacy wording shown before the check, or use my draft marked "draft — not legal advice"? | Minimum data (`04-IDENTITY…` §2, Q11 rule 65); dates of birth are sensitive; Stripe keeps documents and selfies unless redacted | Document plus live selfie, all three document types, live capture only. Keep the Stripe session id, status, error code, check time and the verified date of birth (it replaces a typed date or age; never name, document number, address or images). Redact each session 30 days after a final outcome, so support can look into a dispute in the Stripe Dashboard first. Use my draft wording until yours replaces it | |
| Q28 | OPEN | P | How many attempts may a member start (each costs a fee)? What happens after a failure or "consent declined"? Does ID verified expire? Who handles verification appeals in the beta? | Cost control, fairness, and the manual fallback that `04-IDENTITY…` §2 says is necessary | 3 started checks per member per 30 days, then "try again later". A failure shows "Needs action" with Stripe's reason; consent declined or repeated failure points to support. The manual route is support or an admin recording Admin verified after an in-person or video check, which never gives the ID badge (D58). No expiry in the beta; staff can revoke on evidence. An under-18 result locks the account (D40) | |
| Q29 | OPEN | P | Does the first successful ID verification activate founding membership automatically when a place is free? What is the "required profile setup" (Q11 rule 21)? Should the admin grant route (D79) still work for an account with no verification? | Q11 rule 21 and D63 make verification a condition; D79 was a stop-gap | Automatic on ID verified once the profile has a display name, a home city and at least one mode on, and a place is free (Photo verified will also count when it exists). The admin route stays for exceptions but refuses an account with neither verification. The founder offer is recorded as an entitlement (D90) with its start date; no billing | |
| Q30 | OPEN | Q (reports and moderation) | What can members report, with which categories, is free text allowed, and what does the reporter hear back? | Shapes the report form, the queue and what personal content Swoovr stores | Report a person, a connection request or a bot. Categories: harassment or abuse; threats or violence (urgent); may be under 18 (urgent); fake profile or impersonation; scam or spam; unwanted sexual content or solicitation; hate; misrepresented intentions; something else. Optional text up to 1,000 characters, seen only by moderators. The form offers to block as well. The reporter sees "Thanks, we've received it" and later "We've reviewed your report", never the outcome. 10 reports a day per member; repeat reports join the same case | |
| Q31 | OPEN | Later (photos); limits P and Q | Photos, private media and photo verification stay out of this run (D100)? For later: which storage provider and region (Azure Blob Storage, Australia East, given D39's Azure note?), and should Photo verified use Stripe's selfie or a separate photo matcher? | Founder activation is "Photo and/or ID"; until photos exist only ID counts, and reports can't include photos | Out of this run. Ask the provider and region again when photos are the next slice | |
| Q32 | OPEN | Q | Which actions can moderators take, who may take each, and what does a restricted or suspended member experience? | Q07 rules 13–16 and Q11 rule 42; actions must be enforced everywhere and be appealable | Actions: dismiss, warning, restriction (hidden from discovery and can't send requests, up to 30 days), suspension (member area closed with a notice, up to 90 days), termination, and removal of staff affiliation. Support may dismiss, warn and restrict; admins may also suspend, terminate and remove affiliation. A restricted member keeps existing connections; a suspended one sees "Your account is suspended until …" with an appeal link | |
| Q33 | OPEN | Q | For reports against staff-affiliated members (Q11 rules 39–43): who are the "designated senior reviewers", and how do we link a staff-realm sign-in to that person's own member account so nobody reviews a report about themselves? | Rule 41's conflict-of-interest check needs a staff-to-member link that doesn't exist | Only admins (not support) decide staff cases, and automated steps can't close them. When an admin grants a staff affiliation they also record which staff-realm account belongs to that member account; a reviewer linked to the reported account, or reported in the same case, is refused | |
| Q34 | OPEN | Q | How do appeals, member notices and urgent escalation work in the beta? Email notices need MailerSend, which isn't configured (local Mailpit only). Who is the escalation contact for threats or a suspected minor outside business hours, and where should that contact live (a config value in `.local/docker.env`, not the repo)? | Q07 rules 11, 15 and 16; urgent cases must reach a person | One appeal per action within 30 days, decided by a different moderator, final. Notices are in-app; email goes to Mailpit locally until MailerSend exists. Urgent categories go to the top of the queue and email the escalation address from `SWOOVR_ESCALATION_EMAIL` in `.local/docker.env` (Mailpit locally) | |
| Q35 | OPEN | Q | Should moderation get a minimal staff web screen now (queue, case, action, appeal) or stay API-only like the other admin routes? Staff sign in with Entra only (D32), which isn't configured, so locally only the root admin can sign in to the staff realm. May I add fictional password-based support and admin users in the **local** staff realm only, to test the role split? | The India-based team can't moderate through raw API calls; testing support versus admin needs local staff users | Build a minimal staff area in Web.Client with its own staff BFF allow-list (D84 keeps admin routes off the member BFF), and allow fictional local password users in the local staff realm only (never in UAT or production realm settings) | |
| Q36 | OPEN | R (filters and compatibility) | Which filters belong to each mode in the beta's Free tier ("standard search filters")? Should locals also choose whether they see current visitors, upcoming visitors, both or none (`02-BEHAVIOUR…` §5), which the built feed (D122) doesn't offer? | D92 confirms per-mode filters without listing them; the Free/Plus split (Q08 matrix) depends on the list | All modes: age range, required chips (any or all), excluded chips (any), and "verified only" (ID verified). Dating adds gender and "show me" (Q37). Business adds nothing. Locals get the visitors choice (default both). Expanded (Plus) filters wait for paid tiers | |
| Q37 | OPEN | R | Dating needs gender and "who I want to see". Which gender options (and is free text allowed), is it shown on the profile, is "show me" reciprocal, and do Friends, Activities or Business use it at all? Is sexual orientation asked at all? | No gender or orientation exists in the profile today; these are sensitive and must not become abusive filter categories (`02-BEHAVIOUR…` §2) | Gender: woman, man, non-binary, or "another" with optional text of up to 30 characters; shown only in Dating and only if the member chooses. "Show me" is multi-select and reciprocal in Dating only. Not used in other modes. Orientation is not asked | |
| Q38 | OPEN | R | Is a member's age shown to others, and how does an age filter treat people with no age (DOB and age are optional, D69)? | An age filter is useless if most people have no age; showing age is a privacy choice | Age shows on cards and profiles unless the member turns it off; an age filter includes people with no age unless the viewer ticks "only people with an age" | |
| Q39 | OPEN | R | The brief requires "require and exclude intentions" (`01-PRODUCT-BRIEF.md` §2, `02-BEHAVIOUR…` §1, acceptance criteria 1–2), but Q16 rule 10 says chips never exclude anyone on their own. Is it right that exclusion happens only through a viewer's own filter (Q36), applied both ways? How is compatibility shown? | The two documents conflict as written | Yes: chips alone never hide anyone; a member's required/excluded chip filters hide people both ways (if you exclude a chip, people with it don't see you either). Compatibility = the chips you share in that mode, with no score (`03-SCREENS…` §4); Explore orders by shared chips, then recency. The old intention vocabulary in `02-BEHAVIOUR…` §1 is replaced by the forty chips | |
| Q40 | OPEN | S (MAUI parity) | What is the app's final tab bar? The product docs say Explore, Connections, Chats, Me; the app has Me, Profile, Explore, Travel (D123). And should badge artwork in the app reuse the web's drawings exactly? | Navigation must match across clients (Q11 rule 68) | Explore, Connections, Chats (shown as not built until messaging), Me, like the web. Profile and Travel move under Me and the Explore heading. Badge artwork reuses the web's shapes and fixed colours | |
| Q41 | OPEN | T (traveller search) | Q17 rule 36 deferred traveller search; build it now? Before arrival a traveller may not start contact (D60), so what can they do from the results: only look, or also save someone or send a request? Is it basic (Free) or "extended travel discovery" (Plus)? | Decides whether the slice is worth building before messaging | Build it as recorded in `08-PROFILE-FLAGS-AND-MODES.md` (own trip id, active market, Explore's rules). Look only before arrival: no requests or saves. Free in the beta | |
| Q42 | OPEN | U (messaging) | Lift D112 and build real messaging in this run? If yes: text only, one conversation per connected pair (across modes) or per mode, realtime (SignalR, D59), no attachments, read receipts or not, edit/delete or not? How long are messages kept, after a block, after account deletion, and can moderators read messages only when they are reported? | Messaging is the largest slice, stores private content, and changes moderation (report a message) | Yes, after slices O–T: text only, one conversation per pair across modes, SignalR, no attachments, no read receipts, delete for both within 5 minutes. D60 enforced. Bots reply only to a member-started conversation. Kept while both accounts exist; a block hides it; a reported message and the 20 before it are copied into the case; moderators see nothing else | |
| Q43 | OPEN | Later (deletion and export) | Add account deletion and data export to this run? If yes: grace period before final deletion, what is kept and for how long (audit ids, moderation cases, founder places), and does deletion end the Keycloak account too? | App Store requirement, `04-IDENTITY…` §5, acceptance criterion 16, and D65 emails the founder token at a deletion request | After this run. Proposed later: deactivate at once, delete after 30 days, keep audit ids and moderation cases for 2 years pending legal review, delete the Keycloak user at the end, email the founder token at the request | |

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
