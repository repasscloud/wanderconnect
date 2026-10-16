# Architecture baseline

16 October 2026. Seven application projects are the confirmed direction. The skeleton is phase 0, not a completed product. No database, IdentityCloak connector or policy engine exists yet.

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

DEFAULT: modular monolith with one API, PostgreSQL persistence when needed, private object media storage, durable background processing when introduced. No database container is required for this foundation because it stores nothing. No Kubernetes, extra portal hosts, queue, Redis or worker project is required by the scaffold.

The Blazor shell uses static SSR to keep the initial solution small. Select an interactive mode in phase 1: Interactive Server fits the existing host; WASM/Auto adds Web.Client and needs a documented architecture change. This replaces the older handoff's assumed Interactive WASM requirement. MAUI shares contracts and clients, not Razor markup.

IdentityCloak handles authentication; API policies own application roles, venue scope, member privacy and access. Authentication is not identity verification. The role matrix and provider-contract gate are in [authentication](AUTHENTICATION.md).

Dependency versions: SDK pinned in global.json; net10.0 server/shared, net10.0-android and net10.0-ios mobile. Mobile uses the installed workload's MauiVersion and template package references; workload/Xcode/Android SDK compatibility must be pinned and verified before phase 1 device acceptance. Full solution build is deliberately distinct from the portable local solution.

Official reference used for template/render-mode selection: [Microsoft Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0), [MAUI overview](https://learn.microsoft.com/dotnet/maui/what-is-maui?view=net-maui-10.0). Recheck workload support when installing mobile tooling.
