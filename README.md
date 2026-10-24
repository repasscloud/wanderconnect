# Swoovr

Adult human connection platform. **Swoovr** is the confirmed product name (owner, 22 October 2026). Engineering namespaces and projects use `Swoovr`; lowercase technical identifiers use `swoovr`.

This repository contains the product definition and a local .NET prototype: Keycloak member sign-in on web and in the MAUI app, PostgreSQL persistence, and signed-in profiles, connection modes, Explore, connection requests, blocks and Travel. It does **not** yet implement identity verification, payments, messaging, moderation or any hosted deployment. Fictional previews are explicitly simulated.

## Start here

- [Documentation index](docs/README.md): what each document owns and how to keep it current.
- [Current status](docs/delivery/STATUS.md): implemented work, evidence, gaps and next steps.
- [Naming conventions](docs/architecture/NAMING.md) and [rename/migration record](docs/delivery/SWOOVR-RENAME-2026-10-22.md).

- [Product brief](docs/product/01-PRODUCT-BRIEF.md), [behaviour](docs/product/02-BEHAVIOUR-AND-RULES.md), [screens](docs/product/03-SCREENS-AND-DESIGN.md).
- [Identity and safety](docs/product/04-IDENTITY-TRUST-AND-SAFETY.md), [commerce](docs/product/05-VENUES-VOUCHERS-AND-COMMERCE.md), [technical handoff](docs/product/06-TECHNICAL-AND-AI-HANDOFF.md), [names and sources](docs/product/07-NAMES-DECISIONS-AND-SOURCES.md), [profile flags and modes](docs/product/08-PROFILE-FLAGS-AND-MODES.md) (planned).
- [Appearance specification](docs/design/USER-APPEARANCE-THEMES.md), [colour studio](docs/design/swoovr-colour-studio.html), [canonical palette data](docs/design/themes.json), [screen board](docs/design/screen-board.svg).
- [Architecture](docs/architecture/README.md), [authentication](docs/architecture/AUTHENTICATION.md), [local development](docs/delivery/LOCAL-DEVELOPMENT.md).
- [Phases](docs/delivery/PHASES.md), [questions](docs/delivery/QUESTIONS.md), [decision process](docs/delivery/DECISIONS.md), [validation evidence](docs/delivery/VALIDATION.md).

## Layout

```text
Swoovr.sln                    Full solution; mobile workloads required
Swoovr.Local.sln              Server/shared/checks; no mobile workloads
src/
  Swoovr.Mobile/              MAUI Android + iOS
  Swoovr.Web/                 Blazor Web App
  Swoovr.Api/                 ASP.NET Core API
  Swoovr.Contracts/           Public DTOs and stable wire types
  Swoovr.Client/              Typed HTTP clients
  Swoovr.Domain/              Authoritative business rules
  Swoovr.Infrastructure/      Persistence and provider adapters
tests/Swoovr.Checks/         Dependency-free executable contract checks
docs/                          Product, design, architecture and delivery
scripts/                       Local checks and design synchronization
```

## Local validation

.NET SDK 10.0.400 with latest patch roll-forward, Python 3 and Node >=22 are required. Run `./scripts/check.sh`. Restore uses locally installed SDK assets for current non-mobile projects; no third-party packages or GitHub runner minutes are needed. First-time SDK/workload installation may require internet access. This is local execution, not an air-gapped dependency guarantee.

Start API: `dotnet run --project src/Swoovr.Api --no-launch-profile --urls http://localhost:5080`.
Start web separately: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Swoovr.Web --no-launch-profile --urls http://localhost:5081`.
API: `/health/live` and `/api/v1/status`; web: `/`. Signed-in pages need the local stack and Keycloak; see [local development](docs/delivery/LOCAL-DEVELOPMENT.md).

The GitHub Actions template in `.github/workflow-templates/` is fully commented out by owner request and kept outside the active workflow directory. Keep checks local until explicitly re-enabled. Open feature PRs to `main`; review before merging.

Phase 1 is in progress, starting from the [account access policy slice](docs/delivery/PHASE-1-ACCESS-POLICY.md); the API now validates Keycloak tokens and protects member routes locally (see [current status](docs/delivery/STATUS.md)).

Keycloak is the confirmed authentication service following the owner correction. [Development pin, realm proposal and evidence boundaries](docs/architecture/KEYCLOAK-DEVELOPMENT.md) are documented. A [local Docker sandbox](docs/delivery/LOCAL-DEVELOPMENT.md#docker-local-stack) runs API, web, PostgreSQL and the local Keycloak realms; member sign-in works locally only.

## Requirement status

CONFIRMED = explicit owner requirement; DEFAULT = reversible engineering recommendation; OPEN = unresolved decision. Defaults may unblock prototypes, but cannot masquerade as owner approval, provider capabilities or production readiness. See the decision process for change ownership.

For container testing: `./scripts/docker-local.sh init` then `./scripts/docker-local.sh up`. See [local development](docs/delivery/LOCAL-DEVELOPMENT.md#docker-local-stack) for lifecycle, credentials, reset and deployment boundaries.

An opt-in [local browser investigation harness](docs/delivery/PHASE-1-BROWSER-INVESTIGATION.md) tests code + PKCE and signed ID-token observations. Its email-verified fixture is explicitly simulated; it grants no application access.

Independent partial Phase 2 work adds a [Domain discovery policy](docs/delivery/PHASE-2-DISCOVERY-POLICY.md), including hidden intention exclusions, incognito/sparse/block enforcement and travel initiation evidence checks. It is not wired to an API or live profile data.

[Settings → Appearance](docs/delivery/PHASE-2-WEB-APPEARANCE.md) now runs at `/settings/appearance` in the web host: all eight schemes, independent System/Light/Dark, immediate previews and browser-local guest saving. Account sync remains unconnected. These settings do not affect another member's view or authorization.

An opt-in [fictional discovery preview](docs/delivery/PHASE-2-DISCOVERY-PREVIEW.md) now connects local API → typed Client → static web grid/profile. Verification/location are labelled simulations; hidden-intention, incognito, sparse, block and travel gates run server-side before fictional projections. Default/Production hosts expose no prototype API routes. No actual member sign-in, profile storage or message sending is enabled.


A Development-only [connection scenario preview](docs/delivery/PHASE-3-CONNECTION-WEB.md) now runs at `/prototype/conversation` when explicitly enabled: request, accept/decline, canned reply, block and close across fictional roles. It uses the Domain [connection rules](docs/delivery/PHASE-3-CONNECTION-RULES.md), with separate portable [meeting-plan consent](docs/delivery/PHASE-3-MEETING-PLANS.md) and [private-feedback](docs/delivery/PHASE-3-PRIVATE-FEEDBACK.md) rules not yet wired to web journeys. No real messages or member sessions are enabled.
