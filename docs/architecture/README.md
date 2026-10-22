# Architecture baseline

Updated 23 October 2026. Eight application projects: the original seven plus `Swoovr.Web.Client`, added for Interactive WebAssembly under Q03/D59 (owner-authorised). The foundation and partial discovery/connection prototypes are integrated; the product is incomplete. Application persistence exists for accounts (PostgreSQL through EF Core in Infrastructure, D68); the API validates Keycloak tokens itself. The local Keycloak investigation harness is separate from application authentication. See [current status](../delivery/STATUS.md) for delivered slices and [naming](NAMING.md) for Swoovr identifiers.

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

Test projects can reference layers under test. Do not reference Infrastructure or Domain from UI code to bypass API enforcement. Domain interfaces belong with the rules that need them; application orchestration initially lives in API feature folders. Split an Application project only once complexity justifies it.

DEFAULT: modular monolith with one API, PostgreSQL persistence (CONFIRMED for local development, D68; `SwoovrDbContext` and migrations in Infrastructure, used from API feature folders), private object media storage, durable background processing when introduced. The local Compose stack runs PostgreSQL 17 on 127.0.0.1:5433; without a connection string the API stores nothing and every account-dependent route answers 503 or denies (fail closed). No Kubernetes, extra portal hosts, queue, Redis or worker project is required by the scaffold.

CONFIRMED (Q03, D59): `swoovr.app` is one Blazor Web App. Public, sign-in and callback pages are Static SSR in Web; signed-in features are Interactive WebAssembly components in Web.Client, rendered without prerendering (DEFAULT, so member data is never rendered into server HTML); no Interactive Server or Auto. The browser holds only an HttpOnly session cookie; Web's backend-for-frontend adds the API token and forwards an allow-list of member routes, and only requests carrying `X-Swoovr-Bff: 1`. `scripts/check_naming.py` enforces the project set and that Web, Web.Client and Mobile never reference Domain or Infrastructure. MAUI shares contracts and clients (including `MemberClient`), not Razor markup.

Keycloak handles authentication; API policies own application roles, venue scope, member privacy and access. Authentication is not identity verification. The role matrix and provider-contract gate are in [authentication](AUTHENTICATION.md).

Dependency versions: SDK pinned in global.json; net10.0 server/shared, net10.0-android and net10.0-ios mobile. Mobile uses the installed workload's MauiVersion and template package references; workload/Xcode/Android SDK compatibility must be pinned and verified before phase 1 device acceptance. Full solution build is deliberately distinct from the portable local solution.

Official reference used for template/render-mode selection: [Microsoft Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0), [MAUI overview](https://learn.microsoft.com/dotnet/maui/what-is-maui?view=net-maui-10.0). Recheck workload support when installing mobile tooling.
