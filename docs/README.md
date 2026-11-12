# Swoovr documentation

Updated 22 October 2026. Start with [current delivery status](delivery/STATUS.md), then use the map below. Swoovr is confirmed; the application remains a partial prototype.

| Need | Read / location | What it owns and why |
| --- | --- | --- |
| What are we building and for whom? | [Product brief](product/01-PRODUCT-BRIEF.md) | Adult audiences, connection goals, confirmed scope and success measures |
| How must features behave? | [Behaviour and rules](product/02-BEHAVIOUR-AND-RULES.md) | Privacy, discovery, travel and interaction requirements |
| What should users see? | [Screens and design](product/03-SCREENS-AND-DESIGN.md) | Navigation, screens, denied/empty/error states and accessibility |
| Which profile flags, badges and modes? | [Profile flags and modes](product/08-PROFILE-FLAGS-AND-MODES.md) | App-owned flags the API turns into badges; four modes (D92) and Travel |
| Who may sign in or access data? | [Identity and safety](product/04-IDENTITY-TRUST-AND-SAFETY.md), [authentication architecture](architecture/AUTHENTICATION.md), [Keycloak integration design](architecture/KEYCLOAK-INTEGRATION-DESIGN.md) | Keycloak boundary, privileged session evidence and separate identity verification |
| How do venues and payments work? | [Commerce](product/05-VENUES-VOUCHERS-AND-COMMERCE.md) | Proposed lifecycle, financial responsibilities and unresolved contracts |
| Where does code belong? | [Architecture](architecture/README.md), [technical handoff](product/06-TECHNICAL-AND-AI-HANDOFF.md) | Nine projects (Web.Client D59, Admin D136), dependencies, implementation boundaries |
| Which name or identifier? | [Naming](architecture/NAMING.md), [names and sources](product/07-NAMES-DECISIONS-AND-SOURCES.md) | Owner-confirmed conventions and source references |
| Which colours and assets? | [Appearance spec](design/USER-APPEARANCE-THEMES.md), [tokens](design/themes.json), [brand pack](design/swoovr-brand-pack/README.md) | Eight personal schemes (Standard default); brand artwork has a separate asset pipeline |
| What is built, when and with what evidence? | [Status](delivery/STATUS.md), [phases](delivery/PHASES.md), [validation](delivery/VALIDATION.md) | Current summary, exit criteria and dated test records |
| How do I run and check it? | [Local development](delivery/LOCAL-DEVELOPMENT.md), [rename migration](delivery/SWOOVR-RENAME-2026-10-22.md) | Commands, ports, tools, secrets and compatibility |
| Why was a choice made; what is still open? | [Decisions](delivery/DECISIONS.md), [questions](delivery/QUESTIONS.md) | CONFIRMED/DEFAULT/OPEN status, owner answers and blockers |
| What happened in earlier slices? | [Progress ledger](delivery/PROGRESS.md), `delivery/PHASE-*.md`, [integration record](delivery/MAIN-INTEGRATION-2026-10-18.md) | Historical implementation provenance; not a current completion claim |

## Image infrastructure design — 8 November 2026

- [Vultr image processing, Cloudflare orchestration and migration requirements](superpowers/specs/2026-11-08-vultr-image-pipeline-design.md), tracked in [#294](https://github.com/repasscloud/wanderconnect/issues/294): independent processor/ClamAV builds, Worker rules, R2/Queue contracts, review/appeals, Arachnid credentials and cutover. Design only; not implemented/deployed.

- [Vultr VM and Cloudflare operator runbook](delivery/VULTR-IMAGE-PIPELINE.md): independent processor setup, private input checks, secrets, rolling maintenance and cutover gates.

- [Cloudflare image Workers handoff](../workers/images/CLOUDFLARE-HANDOFF.md): self-contained instructions for a separate session to create the UAT R2 buckets, Queues, event notification, private Wrangler configs, secrets and Worker custom domains, then verify the staging-only path.

## UAT deployment redesign — 12 November 2026

- [UAT deployment through GitHub Actions](delivery/UAT-GITHUB-DEPLOYMENT.md) (D362): push a `vX.Y.Z` tag on `main` to build public GHCR images and deploy UAT; upgrade/refresh modes, refresh authorisation, secrets and variables inventory, `gh` setup ([`scripts/uat/setup-github-uat.sh`](../scripts/uat/setup-github-uat.sh)). Operator runbook: [deploy/vultr-uat/README.md](../deploy/vultr-uat/README.md).
- [Tailscale on the UAT VM](delivery/UAT-TAILSCALE-SETUP.md): manual install, private SSH and private Keycloak administration, verification and rollback.
- [UAT deployment prerequisites](delivery/UAT-DEPLOYMENT-PREREQUISITES.md): how the Vultr UAT deployment really works, the local backup inventory, the certificates, secrets and persistent data to keep, GitHub Actions prerequisites and open questions. Server inventory script: [`scripts/uat/collect-server-state.sh`](../scripts/uat/collect-server-state.sh) (read-only). Preparation only; nothing deployed.

## Standalone messaging specification

- [Optional unanswered-message limit](product/UNANSWERED-MESSAGE-LIMIT.md), tracked in [#300](https://github.com/repasscloud/wanderconnect/issues/300).

## How to keep this organised

Product specifications own requirements; architecture owns code/provider boundaries; delivery owns implementation status and evidence. Update affected documents in the same PR as code. Each new slice records what changed, where it lives, when and why it changed, how to run/validate it, the actual outcome, and remaining gaps. Mark owner answers CONFIRMED, reversible implementation choices DEFAULT and unresolved material choices OPEN.

Keep dated evidence append-only in meaning: retain original commits, external URLs, actual paths and screenshots. Current identifiers in text/examples follow Swoovr conventions. Older screenshots under `delivery/evidence/` and concept PNGs may contain previous branding; they are captures of earlier builds, not a preview of current source. New screenshots belong in a dated evidence directory with their validation record.

Before committing, run `./scripts/check.sh`; after canonical token edits run `python3 scripts/check_design.py --write` first. Optional real-browser, Docker/provider and mobile checks are separate evidence layers. Keep GitHub Actions commented until the owner re-enables minutes. PRs target `main`; merging and deployment need explicit authorization.
