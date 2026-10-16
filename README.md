# WanderConnect

Adult human connection platform. Working product name: WanderConnect; stable engineering namespace: `DatingApp`. Final public naming remains open.

This repository contains the product definition and a .NET foundation with provider-independent account and discovery access policies. It does **not** yet implement authenticated member journeys, live application sign-in, identity verification, payments or persistence. Fictional previews are explicitly simulated.

## Start here

- [Product brief](docs/product/01-PRODUCT-BRIEF.md), [behaviour](docs/product/02-BEHAVIOUR-AND-RULES.md), [screens](docs/product/03-SCREENS-AND-DESIGN.md).
- [Identity and safety](docs/product/04-IDENTITY-TRUST-AND-SAFETY.md), [commerce](docs/product/05-VENUES-VOUCHERS-AND-COMMERCE.md), [technical handoff](docs/product/06-TECHNICAL-AND-AI-HANDOFF.md), [names and sources](docs/product/07-NAMES-DECISIONS-AND-SOURCES.md).
- [Appearance specification](docs/design/USER-APPEARANCE-THEMES.md), [colour studio](docs/design/wanderconnect-colour-studio.html), [canonical palette data](docs/design/themes.json), [screen board](docs/design/screen-board.svg).
- [Architecture](docs/architecture/README.md), [authentication](docs/architecture/AUTHENTICATION.md), [local development](docs/delivery/LOCAL-DEVELOPMENT.md).
- [Phases](docs/delivery/PHASES.md), [questions](docs/delivery/QUESTIONS.md), [decision process](docs/delivery/DECISIONS.md), [validation evidence](docs/delivery/VALIDATION.md).

## Layout

```text
DatingApp.sln                    Full solution; mobile workloads required
DatingApp.Local.sln              Server/shared/checks; no mobile workloads
src/
  DatingApp.Mobile/              MAUI Android + iOS
  DatingApp.Web/                 Blazor Web App
  DatingApp.Api/                 ASP.NET Core API
  DatingApp.Contracts/           Public DTOs and stable wire types
  DatingApp.Client/              Typed HTTP clients
  DatingApp.Domain/              Authoritative business rules
  DatingApp.Infrastructure/      Persistence and provider adapters
tests/DatingApp.Checks/         Dependency-free executable contract checks
docs/                          Product, design, architecture and delivery
scripts/                       Local checks and design synchronization
```

## Local validation

.NET SDK 10.0.400 with latest patch roll-forward, Python 3 and Node >=22 are required. Run `./scripts/check.sh`. Restore uses locally installed SDK assets for current non-mobile projects; no third-party packages or GitHub runner minutes are needed. First-time SDK/workload installation may require internet access. This is local execution, not an air-gapped dependency guarantee.

Start API: `dotnet run --project src/DatingApp.Api --no-launch-profile --urls http://localhost:5080`.
Start web separately: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/DatingApp.Web --no-launch-profile --urls http://localhost:5081`.
API: `/health/live` and `/api/v1/status`; web: `/`. The web shell is not an API-backed member app yet.

The GitHub Actions template in `.github/workflow-templates/` is fully commented out by owner request and kept outside the active workflow directory. Keep checks local until explicitly re-enabled. Open feature PRs to `main`; review before merging.

Phase 1 is in progress: [account access policy slice](docs/delivery/PHASE-1-ACCESS-POLICY.md). The Domain policy is executable in local checks; no protected API or provider adapter is enabled.

Keycloak is the confirmed authentication service following the owner correction. [Development pin, realm proposal and evidence boundaries](docs/architecture/KEYCLOAK-DEVELOPMENT.md) are documented. A [local Docker sandbox](docs/delivery/LOCAL-DEVELOPMENT.md#docker-local-stack) runs API, web and an isolated Keycloak realm; application sign-in is not connected.

## Requirement status

CONFIRMED = explicit owner requirement; DEFAULT = reversible engineering recommendation; OPEN = unresolved decision. Defaults may unblock prototypes, but cannot masquerade as owner approval, provider capabilities or production readiness. See the decision process for change ownership.

For container testing: `./scripts/docker-local.sh init` then `./scripts/docker-local.sh up`. See [local development](docs/delivery/LOCAL-DEVELOPMENT.md#docker-local-stack) for lifecycle, credentials, reset and deployment boundaries.

An opt-in [local browser investigation harness](docs/delivery/PHASE-1-BROWSER-INVESTIGATION.md) tests code + PKCE and signed ID-token observations. Its email-verified fixture is explicitly simulated; it grants no application access.

Independent partial Phase 2 work adds a [Domain discovery policy](docs/delivery/PHASE-2-DISCOVERY-POLICY.md), including hidden intention exclusions, incognito/sparse/block enforcement and travel initiation evidence checks. It is not wired to an API or live profile data.

[Settings → Appearance](docs/delivery/PHASE-2-WEB-APPEARANCE.md) now runs at `/settings/appearance` in the web host: all six schemes, independent System/Light/Dark, immediate previews and browser-local guest saving. Account sync remains unconnected. These settings do not affect another member's view or authorization.

An opt-in [fictional discovery preview](docs/delivery/PHASE-2-DISCOVERY-PREVIEW.md) now connects local API → typed Client → static web grid/profile. Verification/location are labelled simulations; hidden-intention, incognito, sparse, block and travel gates run server-side before fictional projections. Default/Production hosts expose no prototype API routes. No actual member sign-in, profile storage or message sending is enabled.


A Development-only [connection scenario preview](docs/delivery/PHASE-3-CONNECTION-WEB.md) now runs at `/prototype/conversation` when explicitly enabled: request, accept/decline, canned reply, block and close across fictional roles. It uses the Domain [connection rules](docs/delivery/PHASE-3-CONNECTION-RULES.md), with separate portable [meeting-plan consent](docs/delivery/PHASE-3-MEETING-PLANS.md) and [private-feedback](docs/delivery/PHASE-3-PRIVATE-FEEDBACK.md) rules not yet wired to web journeys. No real messages or member sessions are enabled.
