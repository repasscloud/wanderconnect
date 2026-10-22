# Architecture baseline

Updated 22 October 2026. Seven application projects remain the confirmed direction. The foundation and partial discovery/connection prototypes are integrated; the product is incomplete. Application persistence exists for accounts (PostgreSQL through EF Core in Infrastructure, D68); the API validates Keycloak tokens itself. The local Keycloak investigation harness is separate from application authentication. See [current status](../delivery/STATUS.md) for delivered slices and [naming](NAMING.md) for Swoovr identifiers.

| Project | Owns | References |
| --- | --- | --- |
| Contracts | DTOs, explicit stable wire values | None |
| Client | Typed HTTP transport; client-safe helpers | Contracts |
| Domain | Entities and authoritative business policies | None |
| Infrastructure | Database, identity, media, billing adapters | Domain |
| Api | Composition root, endpoints, server authorization | Contracts, Domain, Infrastructure |
| Web | Blazor presentation and future BFF/session boundary | Client |
| Mobile | MAUI native XAML Android/iOS presentation | Client |

Test projects can reference layers under test. Do not reference Infrastructure or Domain from UI code to bypass API enforcement. Domain interfaces belong with the rules that need them; application orchestration initially lives in API feature folders. Split an Application project only once complexity justifies it.

DEFAULT: modular monolith with one API, PostgreSQL persistence (CONFIRMED for local development, D68; `SwoovrDbContext` and migrations in Infrastructure, used from API feature folders), private object media storage, durable background processing when introduced. The local Compose stack runs PostgreSQL 17 on 127.0.0.1:5433; without a connection string the API stores nothing and every account-dependent route answers 503 or denies (fail closed). No Kubernetes, extra portal hosts, queue, Redis or worker project is required by the scaffold.

The Blazor shell uses static SSR to keep the initial solution small. Select an interactive mode in phase 1: Interactive Server fits the existing host; WASM/Auto adds Web.Client and needs a documented architecture change. This replaces the older handoff's assumed Interactive WASM requirement. MAUI shares contracts and clients, not Razor markup.

Keycloak handles authentication; API policies own application roles, venue scope, member privacy and access. Authentication is not identity verification. The role matrix and provider-contract gate are in [authentication](AUTHENTICATION.md).

Dependency versions: SDK pinned in global.json; net10.0 server/shared, net10.0-android and net10.0-ios mobile. Mobile uses the installed workload's MauiVersion and template package references; workload/Xcode/Android SDK compatibility must be pinned and verified before phase 1 device acceptance. Full solution build is deliberately distinct from the portable local solution.

Official reference used for template/render-mode selection: [Microsoft Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0), [MAUI overview](https://learn.microsoft.com/dotnet/maui/what-is-maui?view=net-maui-10.0). Recheck workload support when installing mobile tooling.
