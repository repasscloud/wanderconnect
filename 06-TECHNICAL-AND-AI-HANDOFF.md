# Technical direction and AI implementation handoff

## 1. Instruction to the implementing AI

Read every document in order. Deliver a coherent end-to-end prototype rather than a collection of unrelated screens. Honour CONFIRMED requirements. Use DEFAULT decisions for reversible implementation choices. Record assumptions and deviations; do not silently promote them to owner decisions. Do not ask routine questions. Stop dependent production integrations only when credentials, contractual terms or material unresolved decisions actually prevent safe implementation; continue independent work using labelled adapters/simulations.

Never create real purchases, collect real ID documents, contact users/venues, publish or deploy production resources without appropriate owner authorisation. Prototype seeded data must be fictional adults. Do not represent mock verification, billing or location as real. No subagent/delegation requirement is implied by this brief.

## 2. Architecture

Proposed .NET solution: Mobile (MAUI iOS/Android); Web (Blazor Web App host); Web.Client (Interactive WASM); Api (ASP.NET Core); Contracts; Client; Domain; Application; Infrastructure; Worker; Admin; VenuePortal; relevant Tests. Choose a currently supported .NET SDK/workload at implementation time, document pinned versions and validate required provider libraries. Do not assume an older personal preference overrides mobile support requirements.

Static SSR for public marketing/help/venue public information where appropriate; Interactive WASM for authenticated application. MAUI XAML UI and Razor UI are separate presentation layers. Share DTOs, typed clients and appropriate non-authoritative helpers; server owns authoritative policies. Mobile billing uses platform-specific StoreKit/Google implementations behind shared interfaces. Blazor client has no secrets. Use a BFF cookie session for browser if selected, backed by API/service access rather than per-user UI circuits.

One API codebase and initially one logical deployment for all clients. Horizontally replicate behind routing/load balancing when required. Separate ios-api/android-api/web-api hostnames may route to the same or separate pools later, without branching business logic. Cloudflare can front origins; DNS alone does not supply health-aware load balancing. Restrict trusted proxy headers. No personalised API/media caching at public edges by default.

PostgreSQL proposed persistence, with geospatial support if useful; private object storage for media; SignalR for live events; distributed coordination/backplane when replicas require it. Durable job queue/worker for notifications and billing reconciliation; transactional outbox where events must follow committed writes. Redis is optional until coordination/cache needs justify it. No Kubernetes requirement. Containerised local development and environment-specific deployment config.

## 3. Domain model

| Area | Core entities |
| --- | --- |
| Account | UserAccount, AuthIdentity, Session, ConsentRecord, VerificationRecord |
| Profile | Profile, PromptAnswer, Interest, MediaAsset, SocialLink, SubstanceAssessment |
| Intent/access | IntentionSelection, ContextVisibility, DiscoveryPreference, AccessPolicy, Block |
| Location/travel | PresenceAssertion, ApproximateArea, TravelPlan |
| Interaction | InterestExpression, MessageRequest, Conversation, Membership, Message |
| Meetings | Invitation, MeetingPlan, PlanParticipant, PlanRevision, AttendanceClaim |
| Learning/safety | ExperienceFeedback, SafetyReport, ModerationCase, Appeal, RiskSignal, AuditEvent |
| Commerce | InternalOffer, ProviderProductMapping, Purchase, Subscription, EntitlementGrant, WebhookEvent |
| Venues | Venue, VenueStaff, VenueOffer, CapacityHold, Booking, Voucher, Redemption, SettlementRecord |

Separate sensitive identity records from public profile projection. Stable opaque identifiers, explicit ownership and timestamps. Use concurrency control for capacity, redemptions, blocks and payment state. Currency amounts stored in appropriate exact units, never floating point. Snapshot purchased terms and price; don't change historical bookings when editing an offer.

## 4. API areas

Versioned endpoints for authentication/account; profile editing; media grants; discovery/filtering; privacy/access settings; interests/requests; chat; travel; plans; venues/availability; checkout/bookings/vouchers; feedback/reports; provider webhooks; admin and partner operations. APIs derive acting user identity from validated session/token, never a submitted userId. Policy evaluator reused for search, direct profile, media and interaction; deny-by-default. Protect against object-reference attacks and inference leaks; ordinary “not available” response for inaccessible profiles.

Rate limits and pagination; idempotency keys for purchases, plan acceptance, redemption and retried writes; consistent validation/problem responses; correlation IDs without sensitive payload logging. Authorise SignalR subscriptions and re-evaluate membership after blocks. Deletion and block propagation need explicit handling across caches and realtime connections.

## 5. Prototype deliverables

Runnable source, exact local start commands, environment variable example with no secrets, database migrations/seed data if using real persistence, deterministic demo personas, feature toggles for mocked integrations, mobile/web screens, admin/venue examples, architecture notes, decision log, and tests for the security/commercial invariants below. Use provider sandboxes before real production. Preserve design flexibility through tokens; do not hardcode WanderConnect into database/domain identifiers.

Demo personas include LTR-only owner excluding casual/FWB; LTR+FWB candidate; substantive photo-free member; sparse photo-only member; incognito visitor; owner refusing incognito; upcoming traveller; physically present traveller; ordinary venue staff; limited moderator. Cover varied adult ages and orientations without stereotypes.

## 6. Acceptance criteria

1. LTR+FWB candidate cannot access an owner requiring LTR and excluding FWB; direct URL/media access also fail.
2. LTR+friendship passes that intention rule if other checks pass.
3. Incognito visits never enter member-visible visit history; incognito-refusing profiles aren't returned.
4. A substantive photo-free profile passes substance assessment; photo-only sparse profile is hidden by Hide sparse.
5. Block applies across contexts, requests, chat actions, media and realtime delivery.
6. Manually selecting a destination never unlocks traveller-initiated messages; arrival simulation changes eligibility only in demo mode.
7. Declined message requests cannot be resent to repeatedly notify the recipient.
8. Private feedback is unavailable to its subject and cannot create public ratings; duplicate feedback submissions don't multiply votes.
9. Duplicate image match enters review without automatically banning or revealing another account.
10. Provider login links cannot be hijacked by matching email strings; verification badge never grants admin privileges.
11. Client-edited subscription flags cannot unlock server features; duplicate provider events create one effective grant.
12. Capacity cannot oversell under concurrent checkouts; late payment after expired hold is reconciled.
13. One voucher cannot be redeemed twice; wrong-venue staff cannot redeem it.
14. Exact location, ID documents, private intentions and chat text do not appear in analytics/public DTOs or notifications by default.
15. Loading, empty, offline, denied and failure states are demonstrated; keyboard/large-text layouts remain usable.
16. Users can start deletion/export and understand subscription cancellation separately.

## 7. Engineering sequence

Implement policy evaluator and sample fixtures first; then profiles/discovery; requests/chat/plans; travel/incognito; feedback/admin; venue voucher workflow; authentication/payment adapters; responsive/accessibility pass. Validate the entire loop on web and both mobile targets before broad feature expansion. Unit tests focus on rule invariants; integration/concurrency tests cover billing, holds and redemption. Add end-to-end tests for core journeys rather than duplicating markup assertions.

## 8. Production gates

Live ID provider/privacy handling; real Apple/Google configurations; app-store policy assessment; community guidelines/moderation staffing; venue contracts/settlement/refunds; launch-market legal/privacy review; security testing; operational backup/restore and incident response. These gates do not prevent a fully reviewable sandbox prototype.
