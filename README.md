# WanderConnect

Adult human connection platform. Working product name: WanderConnect; stable engineering namespace: `DatingApp`. Final public naming remains open.

This repository contains the product definition and a .NET foundation. It does **not** yet implement member journeys, live sign-in, identity verification, payments or persistence.

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

.NET SDK 10.0.400 with latest patch roll-forward and Python 3 are required. Run `./scripts/check.sh`. Restore uses locally installed SDK assets for current non-mobile projects; no third-party packages or GitHub runner minutes are needed. First-time SDK/workload installation may require internet access. This is local execution, not an air-gapped dependency guarantee.

Start API: `dotnet run --project src/DatingApp.Api --no-launch-profile --urls http://localhost:5080`.
Start web separately: `dotnet run --project src/DatingApp.Web --no-launch-profile --urls http://localhost:5081`.
API: `/health/live` and `/api/v1/status`; web: `/`. The web shell is not an API-backed member app yet.

The GitHub Actions template in `.github/workflow-templates/` is fully commented out by owner request and kept outside the active workflow directory. Keep checks local until explicitly re-enabled. Open feature PRs to `main`; review before merging.

## Requirement status

CONFIRMED = explicit owner requirement; DEFAULT = reversible engineering recommendation; OPEN = unresolved decision. Defaults may unblock prototypes, but cannot masquerade as owner approval, provider capabilities or production readiness. See the decision process for change ownership.
