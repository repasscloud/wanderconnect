# Architecture baseline

Updated 25 October 2026. Nine application projects: the original seven plus `Swoovr.Web.Client`, added for Interactive WebAssembly under Q03/D59, and `Swoovr.Admin`, the separate administration app under Q35/D136 (both owner-authorised). The foundation and partial discovery/connection prototypes are integrated; the product is incomplete. Application persistence exists for accounts (PostgreSQL through EF Core in Infrastructure, D68); the API validates Keycloak tokens itself. The local Keycloak investigation harness is separate from application authentication. See [current status](../delivery/STATUS.md) for delivered slices and [naming](NAMING.md) for Swoovr identifiers.

| Project | Owns | References |
| --- | --- | --- |
| Contracts | DTOs, explicit stable wire values | None |
| Client | Typed HTTP transport; client-safe helpers | Contracts |
| Domain | Entities and authoritative business policies | None |
| Infrastructure | Database, identity, media, billing adapters | Domain |
| Api | Composition root, endpoints, server authorization | Contracts, Domain, Infrastructure |
| Web | Blazor Web App host: Static SSR pages, sign-in, the backend-for-frontend (`/bff/`) that holds member tokens server-side | Client, Web.Client |
| Web.Client | Interactive WebAssembly components for signed-in features; calls the API only through `/bff/` with `MemberClient` | Client |
| Mobile | MAUI native XAML Android/iOS presentation | Client |
| Admin | Separate administration web app (D136): Blazor with server-side rendering only, staff-realm sign-in, calls the admin API server-side with `AdminClient`; moderation queue, cases, appeals and account tools | Client |

Test projects can reference layers under test. Do not reference Infrastructure or Domain from UI code to bypass API enforcement. Domain interfaces belong with the rules that need them; application orchestration initially lives in API feature folders. Split an Application project only once complexity justifies it.

DEFAULT: modular monolith with one API, PostgreSQL persistence (CONFIRMED for local development, D68; `SwoovrDbContext` and migrations in Infrastructure, used from API feature folders), private object media storage, durable background processing when introduced. The local Compose stack runs PostgreSQL 17 on 127.0.0.1:5433; without a connection string the API stores nothing and every account-dependent route answers 503 or denies (fail closed). No Kubernetes, extra portal hosts, queue, Redis or worker project is required by the scaffold.

CONFIRMED (Q03, D59): `swoovr.app` is one Blazor Web App. Public, sign-in and callback pages are Static SSR in Web; signed-in features are Interactive WebAssembly components in Web.Client, rendered without prerendering (DEFAULT, so member data is never rendered into server HTML); no Interactive Server or Auto. The browser holds only an HttpOnly session cookie; Web's backend-for-frontend adds the API token and forwards an allow-list of member routes, and only requests carrying `X-Swoovr-Bff: 1`. `scripts/check_naming.py` enforces the project set, that Web, Web.Client, Mobile and Admin never reference Domain or Infrastructure, that the member app never references Admin and that the BFF allow-list holds no admin route. MAUI shares contracts and clients (including `MemberClient`), not Razor markup.

Keycloak handles authentication; API policies own application roles, venue scope, member privacy and access. Authentication is not identity verification. The role matrix and provider-contract gate are in [authentication](AUTHENTICATION.md).

Dependency versions: SDK pinned in global.json; net10.0 server/shared, net10.0-android and net10.0-ios mobile. Mobile uses the installed workload's MauiVersion and template package references; workload/Xcode/Android SDK compatibility must be pinned and verified before phase 1 device acceptance. Full solution build is deliberately distinct from the portable local solution.

Official reference used for template/render-mode selection: [Microsoft Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0), [MAUI overview](https://learn.microsoft.com/dotnet/maui/what-is-maui?view=net-maui-10.0). Recheck workload support when installing mobile tooling.

API paths and client contract dates follow [API versioning](API-VERSIONING.md), D211/D222.

D223/D224: conversation closure and its recipient centre/outbox item commit together. Existing API notification services own deduplicated dispatch/recovery; per-device attempts record acceptance or uncertainty rather than claiming device delivery. No separate worker project is introduced.

Push provider boundaries, secure device registration and required operator/native acceptance steps are in [PUSH-NOTIFICATIONS.md](PUSH-NOTIFICATIONS.md) (D226–D228).

### Retention authority (D231–D233)

The API retention services use existing Domain policy and Infrastructure persistence; no project is added. See [retention schedule](../delivery/RETENTION-SCHEDULE.md). Account-state, media, global preservation and per-case/per-hold advisory leases order source mutation and disposal across replicas. Coverage changes take exclusive global authority; ordinary saves and destructive work take shared authority. Explicit outer leases cover bulk SQL/object deletion and commits. Conversation row locks follow the global barrier. Evidence numbering and custody chain writes commit while the hold mutex is owned. Unknown provenance or hold status postpones disposal.

Data-key configuration and the stable legacy fallback boundary are documented in [DATA-KEYS.md](DATA-KEYS.md), D234. The existing Checks project references the API to test actual key consumers; this is a test-layer reference permitted above, not a new application project or a UI bypass.


D255 DEFAULT adds a provider artifact inside the existing Keycloak authentication boundary, not another .NET application project. `containers/keycloak/contact-policy` compares/retire bindings inside the provider transaction and enforces its minimal policy in the provider database. The API must never directly write that database. The Java/internal-JPA interfaces are pinned to26.8.1, compile toJava21 and fail on an untested provider version; upgrades require actual isolated compatibility tests. Main realm/image is unchanged; application orchestration and staff authorization/durable recovery remain to be connected.

D261 recovery stays inside the existing API/infrastructure boundaries: API-owned snapshot/private-media bundling and hold-aware reconciliation, operator-only Python encryption/storage transport and an excluded operations readiness schema. It adds no application project or public authentication endpoint. [Backup/restore boundary and operator procedure](../delivery/BACKUP-RESTORE.md).

D262 bounded audit expiry uses the existing API/persistence projects: a database-owned checkpoint and validated prefix-expiry function preserve surviving hashes. Append/read/expiry share chain authority; operational minimisation joins preservation authority. Hosted runtime roles receive only required function execution, not checkpoint writes; migration/database owners remain privileged. No additional project. See the delivery retention schedule for conservative source guards.

### Meeting safety resource boundary (DEFAULT D263)

Meeting safety remains within existing projects: Domain policies/entities, Infrastructure mappings/migrations, Contracts/Client transport, API orchestration/schedule/outbox/retention and Web/MAUI presentation. Authenticated member operations use Keycloak and existing API account gates. Web's anonymous Static SSR contact pages call narrowly scoped public API resources server-side; destination-code verification issues only a read-only resource grant, never a member session. The protected HttpOnly SSR cookie is link-bound; every API read revalidates consent, account/recipient/link availability and expiries.

The API owns server clocks, idempotent scheduling, quotas, outcome records and hold/report-aware disposal. Device dialler/SMS composition and optional coordinates remain client-side; no location feed or emergency dispatch subsystem is introduced. [Meeting safety boundaries](../delivery/MEETING-SAFETY-IMPLEMENTATION.md) distinguish resource expiry from session closure and ordinary/preserved records. Final hosted logging, provider/device and integration acceptance remain separate from these architectural choices.

Individual content moderation (D265) remains in existing feature layers: case-scoped exact-source previews/mutations and authorisation in API; domain decision/source policies; independent EvidenceSeal original/reason plus immutable integrity hash in Infrastructure; shared neutral standing/message projections in Client; existing Admin SSR, member WASM and native chat views. No new project or authentication mechanism. The existing typed message upsert carries removal markers; initial/reconnected histories reconcile and observed markers cannot be downgraded by older responses. New encrypted sources participate in preservation, evidence key rotation and independent case-content/record expiry. See [boundary and clocks](../delivery/INDIVIDUAL-CONTENT-REMOVAL.md).
