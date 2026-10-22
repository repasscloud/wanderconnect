# Ordered question backlog

No answer is needed to review the phase 0 foundation. Ask these when approaching their blocking phase; do not ask all at once.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q01 | PARTLY ANSWERED, owner 16 October 2026 | Phase 1 live auth | Keycloak docs/source confirmed; no existing pin/realm/sandbox found. Proposed pin/realm in D13; configured session evidence still needs sandbox validation | Use official sources; keep privileged integration disabled pending validated session evidence (Q10 answered in D14) |
| Q02 | OPEN | Phase 1 auth UX | Enable member magic links in addition to Apple/Google/email-password? What cross-device email-link behavior is expected? | Yes, if backend supports the required safe exchange |
| Q03 | OPEN | Phase 1 web interaction | Should authenticated web use Interactive Server or justify adding WASM client? | Server initially; measure scale needs before adding project |
| Q04 | OPEN | Phase 2 live verification | Which document/liveness provider, gate timing, retention and manual appeal route? | Gate browsing/contact; separate from Keycloak auth |
| Q05 | OPEN | Phase 3 travel | Can travellers reply before arrival when a local initiates? | Allow only replies to local initiation, subject to privacy rules |
| Q06 | OPEN | Phase 4 real commerce | Merchant of record, settlement, voucher benefit, refund and cancellation owner? | Sandbox only until contracts are decided |
| Q07 | OPEN | Phase 5 pilot | Initial city/markets and staffed support/moderation coverage? | Compact pilot; owner chooses market and operations |
| Q08 | PARTLY ANSWERED, owner 22 October 2026 | Phase 5 publishing | Swoovr is confirmed; trademark/store availability, first paid entitlements and prices remain unresolved | Use Swoovr conventions; no invented clearance or pricing |
| Q09 | OPEN | Phase 1 mobile acceptance | Which Apple/Xcode and Android toolchain versions/devices are available? | Pin supported workload set after checking official compatibility |

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
| Q11 | OPEN | Profile persistence and badges | Final flag set and rules: what admin verified means and its badge; founding-member criteria; employee badge = ruby "Swoovr staff" bird; several badges at once; bot/employee accounts in discovery | Use the five drawn badges; add admin verified when it is defined |
| Q12 | OPEN | Profile, discovery and the segmented control | Final set of modes (brief: Connect / Friends & company / Travel & exploring; reference screens: Dating / Friends / Activities; owner examples: dating, friends, business) | Keep mode labels data-driven until the set is final |

## Owner answers — 23 October 2026

The owner answered Q03 and Q05–Q09 below (decisions D59–D64) and part of Q11 (D57, D58; the founding criteria come from the Q08 answer). The chat style is settled by D56. The tables above keep their original snapshots.

| ID | Status | Answer in brief |
| --- | --- | --- |
| Q03 | ANSWERED, owner 23 October 2026; D59 | `swoovr.app` is a Blazor Web App: Static SSR for public and sign-in pages, Interactive WebAssembly for everything signed in, no Interactive Server or Auto. `swoovr.com` stays a separate Astro site |
| Q05 | ANSWERED, owner 23 October 2026; D60 | Travellers may reply before arrival to conversations a local started; they cannot start one until they arrive |
| Q06 | ANSWERED, owner 23 October 2026; D61 | Several payment/booking models per vendor (Swoovr-managed, vendor-managed, hold only, free); provider-agnostic payments, Stripe first; real money stays off until validated |
| Q07 | ANSWERED, owner 23 October 2026; D62 | Private, invitation-only beta in Adelaide (about 100 testers); business-hours support in beta, 24/7 at public launch; India-based team with escalation to RePass Cloud |
| Q08 | ANSWERED, owner 23 October 2026; D63 | Published by RePass Cloud Pty Ltd; Free/Plus/Premium tiers with AUD prices; Free only during beta; 1,000 founding memberships. Trademark, store and launch checks remain outstanding |
| Q09 | ANSWERED, owner 23 October 2026; D64 | .NET 10 + MAUI for iOS and Android, built and tested locally on macOS; CI/CD only for authorised store releases. Workload installation and validation remain outstanding |
| Q11 | PARTLY ANSWERED, owner 23 October 2026 | Employee = ruby staff bird (D57); admin verified = backend flag, no badge (D58); founding = gold Swoovr Founder badge, permanent, after onboarding and Photo and/or ID verification (Q08). Still OPEN: whether admin verified changes age status, several badges at once, bot/employee accounts in discovery |
| Q12 | OPEN | Final set of modes |

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