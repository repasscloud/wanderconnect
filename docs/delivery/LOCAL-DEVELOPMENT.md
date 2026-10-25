# Local development

Install .NET SDK matching global.json and Python 3. Run all commands from repository root.

```sh
./scripts/check.sh
dotnet run --project src/Swoovr.Api --no-launch-profile --urls http://localhost:5080
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Swoovr.Web --no-launch-profile --urls http://localhost:5081
```

Run API/web in separate terminals. `/health/live` is liveness only and does not certify database/provider readiness. `/api/v1/status` returns foundation metadata. Web is a static Blazor shell; there are no sign-in buttons claiming working authentication. Local HTTP is for loopback development; real authentication requires configured HTTPS callbacks and secure cookies.

`Swoovr.Local.sln` includes all non-mobile projects and executable checks. `Swoovr.sln` also includes MAUI. Do not run full-solution restore on a server-only environment and assume missing MAUI workloads are code defects. For mobile, install the supported MAUI Android/iOS workloads plus Android SDK/JDK and compatible Xcode; pin/record their versions before phase 1. Then:

```sh
dotnet build src/Swoovr.Mobile -f net10.0-android
dotnet build src/Swoovr.Mobile -f net10.0-ios
```

Simulator runtime/signing arguments depend on the local platform. Do not claim these builds passed unless actually run. No automatic heavyweight workload installation is part of local checks.

`./scripts/check.sh` also starts the built hosts briefly on loopback ports 5187/5188, verifies API JSON and web HTML, and stops them. Keep these ports free.

Infrastructure references Npgsql.EntityFrameworkCore.PostgreSQL and Microsoft.EntityFrameworkCore.Design (D68); the first restore needs nuget.org or a warm cache. Local restore disables NuGet audit network access and tolerates unavailable sources. Future dependencies need an explicit cache/restore step and later security audit; offline checks do not replace dependency vulnerability review.

## Design and docs

Open docs/design/swoovr-colour-studio.html directly in a browser. It is self-contained. After editing themes.json, run `python3 scripts/check_design.py --write`, then `--check`. The generator preserves all other spec text and embedded portrait. Earlier colour-palettes.svg is historical, not a second theme authority.

## Docker local stack

Docker Engine/Desktop with Compose v2+ and Python 3 are required. No host .NET SDK is needed for the container build. From the repository root:

```sh
./scripts/docker-local.sh init
./scripts/docker-local.sh up
./scripts/docker-local.sh status
./scripts/docker-local.sh smoke
./scripts/docker-local.sh down
```

`up` builds API and web, waits for Keycloak readiness and runs HTTP smoke. First use downloads pinned images and needs internet. Leave ports 5080, 5081 and 8180 free. API is http://localhost:5080, web http://localhost:5081 and Keycloak http://localhost:8180. Every published port binds only 127.0.0.1. No mobile build or provider login is performed. The hosts are independent foundation shells, not an authenticated end-to-end app.

`init` generates a random bootstrap password in ignored `.local/docker.env` with private permissions and retains an existing file. The bootstrap username is `local-bootstrap`; inspect the private file locally if admin-console access is needed. Never paste it into issues/logs or run `docker compose config` with secrets in shared output. Docker administrators can inspect container environment values. This is a disposable local bootstrap mechanism, not a production secret store. No credentials or fictional user accounts ship in the realm import.

Keycloak uses a Compose-owned volume for its development database. `down` stops/removes this project's containers/network and preserves its volume. To deliberately reset only this sandbox, stop it, then run `docker compose --env-file .local/docker.env down --volumes` and start again. This deletes local realm changes/accounts/sessions; do not use against another environment. Changing the bootstrap password file does not rotate an existing admin password. Realm import skips an existing realm; edit through the console for investigation or explicitly reset to reimport. Never export real users or secrets into the tracked realm template.

DEFAULT: digest-pinned Linux multi-stage builds publish API/web separately and run as the ASP.NET image's non-root app user on port 8080. Image indexes support ARM64 and AMD64; only the platform recorded in VALIDATION.md has been executed. The Docker build context excludes mobile, local environment and host build outputs. Containers do not modify host bin/obj. Keep normal `./scripts/check.sh` as the pre-commit gate; Docker smoke adds runtime observations, not a replacement for policy checks.

CONFIRMED deployment direction: all server components will be containerised on public/dev infrastructure; mobile apps remain native. These image targets prepare that path. This Compose file is local-only: Keycloak `start-dev`, embedded database, loopback HTTP and bootstrap admin must not be promoted. Public/dev deployment still needs production Keycloak/database settings, HTTPS and proxy trust, public issuer/callbacks, secret storage, persistent web data-protection keys when sessions exist, resource limits, backup/restore and readiness/security validation. No deployment is included or authorized.

See [sandbox evidence and investigation matrix](PHASE-1-KEYCLOAK-SANDBOX.md). PostgreSQL 17 is the application database (D68): Compose service `postgres`, database `swoovr`, published only on 127.0.0.1:5433, password generated into `.local/docker.env` as `SWOOVR_DB_PASSWORD`. No social provider, application OAuth client or fake authorization adapter is configured. Linux containers cannot validate iOS/Xcode tooling.

## Member sign-in (local Keycloak)

The [members slice](PHASE-1-KEYCLOAK-MEMBERS.md) runs against the Docker stack. Local only: fictional adult accounts at `example.test`, every email caught by the mail sink at http://127.0.0.1:8025.

```sh
./scripts/docker-local.sh init     # adds any missing local secrets to .local/docker.env (600), never prints them
./scripts/docker-local.sh up       # Keycloak, mail sink, API/web containers; applies realm settings
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Swoovr.Api --no-launch-profile --urls http://127.0.0.1:5080
ASPNETCORE_ENVIRONMENT=Development Keycloak__Authority=http://localhost:8180/realms/swoovr-members-dev \
  Keycloak__ClientSecret="$(grep '^SWOOVR_MEMBERS_WEB_SECRET=' .local/docker.env | cut -d= -f2)" Keycloak__RequireHttpsMetadata=false \
  Api__BaseUrl=http://127.0.0.1:5080/ dotnet run --project src/Swoovr.Web --no-launch-profile --urls http://localhost:5081
```

The first sign-in visit of a browser session plays the launch splash before Keycloak; add `?intro=done` to skip it, or open a new private window to see it again.

Stop the API/web containers first (`docker compose --env-file .local/docker.env stop api web`) because they hold ports 5080/5081. Use `localhost` (not `127.0.0.1`) for the web host: it is the registered redirect URI and the passkey RP ID. Open http://localhost:5081/account.

After changing `scripts/keycloak_realms.py`, run it with `--write`. Keycloak imports a realm only when it does not exist, so recreate the local stack's own volume to re-import (`docker compose --env-file .local/docker.env down` then `docker volume rm swoovr-local_keycloak-data`), and never remove volumes you have not inspected. `./scripts/docker-local.sh realms` reapplies flow settings at any time.

`./scripts/docker-local.sh root-admin` creates the fictional local root admin in the staff realm; its password and, after the first acceptance run, its test TOTP secret stay in `.local/swoovr-root-admin.json`.

Optional acceptance with installed Edge and Playwright, with the stack and both hosts running:

```sh
SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/auth/members.browser.cjs
SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/auth/root-admin.browser.cjs   # needs only the API host
```

It registers and signs in fictional accounts, uses a virtual passkey authenticator, deliberately locks one account and resets it, and saves screenshots to ignored `.local/auth-browser/`.

## Optional local browser investigation

Node >=22 is required for the dependency-free harness and its tests. No npm install is needed. After the Docker stack is running, `./scripts/docker-local.sh auth-init` configures only local investigation resources; `./scripts/docker-local.sh auth` starts the fixed 127.0.0.1:8280 callback host. `node scripts/auth/provider-check.mjs` checks actual provider rejection behaviour. Follow the [browser slice](PHASE-1-BROWSER-INVESTIGATION.md) for private fixture handling, simulated-email limits, lifecycle and acceptance gaps. Application API/web authentication remains disconnected.

## Web appearance checks

`/settings/appearance` offers all eight schemes with independent System/Light/Dark and guest browser persistence. Account sync is not connected. Local builds must run with `ASPNETCORE_ENVIRONMENT=Development` to resolve development static web assets; published container output uses its production asset manifest. `./scripts/check.sh` now fetches the appearance page and linked scripts/styles as well as the foundation HTML.

Optional actual-browser acceptance uses Playwright and installed Edge (not installed by checks). Start the local web host, then run:

```sh
SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/appearance/web.browser.cjs
SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/ui/components.browser.cjs   # web with Prototype__Enabled=true and the API host
```

If Playwright resolves through normal Node modules, omit that variable. `SWOOVR_WEB_URL` may select another loopback port (default http://127.0.0.1:5081). Screenshots go to ignored `.local/appearance-browser/`; override `SWOOVR_BROWSER_OUTPUT` if needed. The test verifies 24 preferences/16 computed palette pairs, requested/System behavior, reload/reset, cross-tab events, keyboard, completed enhanced navigation and 320px/200% text. Storage denial is injected for that error-path test. This is not screen-reader/device or account-sync acceptance.

After canonical token edits, `python3 scripts/check_design.py --write` also regenerates production `themes.css` and `appearance-palettes.js`; `--check` rejects drift. Do not edit generated assets directly.

## Fictional discovery preview

Follow the [opt-in demo commands](PHASE-2-DISCOVERY-PREVIEW.md#local-demo). No provider login is performed. The development flag enables only fixed fictional adult read/eligibility routes; default and Production remain closed. `./scripts/check.sh` starts/stops additional test hosts on ports 5191–5196 for these API/Web gates. Keep them free. Browser checks remain optional and use installed Edge/Playwright; no heavyweight tooling installation occurs in the check script.

## Optional signing-key investigation

[Rotation commands and cleanup boundaries](PHASE-1-SIGNING-ROTATION.md) use only the existing local investigation realm and matching private fixture. The optional Edge/Playwright driver creates a unique temporary key provider and restores the original active provider. The delayed-cleanup option exercises management authorization renewal. Do not run concurrently with another rotation or reset/mix fixture volumes; do not force-kill during mutation. Normal Node checks test the lifecycle with injected providers and need no private credentials/browser.

Optional provider mutation checks: [signing rotation](PHASE-1-SIGNING-ROTATION.md) and [temporary MFA](PHASE-1-OPTIONAL-MFA.md) require a matching private fixture/volume for the current realm and installed Edge. Pre-rename fixtures/volumes are historical and must not be reused with the renamed realm. They are separate from portable checks and restore owned temporary state; no force-kill cleanup guarantee.

## Swoovr naming transition — 22 October 2026

Use `Swoovr.Local.sln`, `src/Swoovr.*`, the `swoovr-dev` realm and `SWOOVR_*` browser variables. The renamed Compose project uses a separate volume and `.local/swoovr-auth-fixture.json`. Before restarting retained sandbox work, follow the [migration steps](SWOOVR-RENAME-2026-10-22.md#local-migration-and-compatibility); old volumes and fixtures must not be mixed or deleted. See [current status](STATUS.md) for what is connected.

## Application database — 23 October 2026

`./scripts/docker-local.sh init` adds `SWOOVR_DB_PASSWORD` and `SWOOVR_FOUNDER_SECRET_V1` to an existing `.local/docker.env`; `up` starts PostgreSQL with the rest of the stack (to start only it: `docker compose --env-file .local/docker.env up -d --wait postgres mailpit`). Run the API from the repository root with the connection string; Development applies migrations on start:

```sh
ConnectionStrings__Swoovr="Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=$(grep '^SWOOVR_DB_PASSWORD=' .local/docker.env | cut -d= -f2)" \
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Swoovr.Api --no-launch-profile --urls http://127.0.0.1:5080
```

Run the API through `dotnet run --project` (or from its output folder): started from the repository root, the built DLL does not find `appsettings.Development.json`, so Keycloak validation stays unconfigured and every token gets 401.

Operator commands use the same configuration: `dotnet run --project src/Swoovr.Api -- migrate`, `-- root-admin grant <staff-realm subject>` (D71) and `-- beta add <email>...`, `-- beta add --file containers/postgres/beta-enrolment.fictional.txt`, `-- beta revoke <email>`, `-- beta list` (D76). The members area needs an enrolment for the account's verified email (D62, D78); `tests/auth/members.browser.cjs` and `tests/auth/accounts.browser.cjs` enrol their own fictional addresses. `./scripts/check.sh` runs the persistence checks in a throwaway database when the container is reachable and prints a SKIP line otherwise.

`tests/auth/accounts.browser.cjs` (Edge and Playwright, as above) creates fictional members through the local Keycloak admin API, signs them in for real tokens and exercises the account, age and admin routes. It needs the enrolled fictional root admin from `tests/auth/root-admin.browser.cjs` and a Debug build of the API for the operator commands.

If Keycloak pages appear in the stock Keycloak theme, Docker Desktop's bind mount of `containers/keycloak/themes` has gone stale (the folder is empty inside the container). Recreate only that container: `docker compose --env-file .local/docker.env up -d --force-recreate --wait keycloak`, then `./scripts/docker-local.sh realms`.

## Didit ID verification (test mode) — 25 October 2026

The API needs `Didit__ApiKey`, `Didit__WorkflowId` and `Didit__WebhookSecret`, taken from `SWOOVR_DIDIT_API_KEY`, `SWOOVR_DIDIT_WORKFLOW_ID` and `SWOOVR_DIDIT_WEBHOOK_SECRET` in `.local/docker.env`. `compose.yaml` passes them to the api container (recreate it after pulling: `docker compose --env-file .local/docker.env up -d --no-deps api`). A host started with `dotnet run` needs them in its environment too; `.local/run-hosts.sh` should add `Didit__ApiKey="$(val SWOOVR_DIDIT_API_KEY)" Didit__WorkflowId="$(val SWOOVR_DIDIT_WORKFLOW_ID)" Didit__WebhookSecret="$(val SWOOVR_DIDIT_WEBHOOK_SECRET)"` to the API line. Without them ID verification answers 503 and webhooks are refused.

Webhooks reach whichever API listens on 127.0.0.1:5080 through the quick tunnel (`cloudflared tunnel --url http://127.0.0.1:5080`; its host changes per run, so update the Didit destination when it does). Without the tunnel, verification still completes: the member's return to `/me/verification/return` re-reads the result from Didit.

Founding places are allocated by `dotnet src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll founders allocate` with the API's connection string, founder secret and mail settings (D130; cron daily in hosted environments).

### Didit test verification walkthrough

One-time manual check by a person (the automated suite can't complete a verification; it needs a real ID and face):

1. Start the stack and the tunnel, then the hosts: stop the Docker api and web (`docker compose --env-file .local/docker.env stop api web`) and run `bash .local/run-hosts.sh` from a checkout built in Debug. The Docker web container has no sign-in configured, so http://localhost:5081 must be the host from the runner. Confirm the Didit console's webhook destination is `https://<tunnel host>/api/v1/webhooks/didit` (V3.0, `status.updated` and `data.updated`). Your member email must be on the beta list (`beta add`, see Application database).
2. Sign in at http://localhost:5081 as a fictional-named local member on the beta list with a home city, open **Me → ID verification** and choose **Verify my ID**. The page says *Test mode*.
3. Choose **Continue on Didit** (a new tab). Complete Didit's test flow with your own document and selfie, or use Didit's test-mode options if the console offers them.
4. Come back to the Swoovr tab and choose **Check result** (or follow Didit's redirect to `/me/verification/return`). Expected: *In review* or *Your ID is verified*; the ID Verified badge appears on the account page once approved.
5. Evidence to record in VALIDATION.md: `SELECT state, provider_status, started, adult FROM id_verification_attempts ORDER BY created_at DESC LIMIT 1;` and `SELECT webhook_type, outcome FROM didit_webhook_events ORDER BY received_at DESC LIMIT 5;` (ids and outcomes only). Swoovr stores no document data; Didit keeps it for 30 days (D147).
6. To try again with the same member, an admin uses `POST /api/v1/admin/accounts/{id}/verification/allow-again` (or revoke first if approved).

## Admin app (Swoovr.Admin) — 25 October 2026

The separate administration app (D136) runs on http://localhost:5082 and signs staff in through the local staff realm (`swoovr-admin-web`, redirect `http://localhost:5082/signin-oidc`; run `./scripts/docker-local.sh realms` once to move the client's redirect from the web host). Start it with `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5082 Keycloak__Authority=http://localhost:8180/realms/swoovr-staff-dev Keycloak__ClientSecret="$(val SWOOVR_STAFF_WEB_SECRET)" Keycloak__RequireHttpsMetadata=false Api__BaseUrl=http://127.0.0.1:5080/ dotnet run --project src/Swoovr.Admin --no-launch-profile`.

The API needs `Staff__LocalPasswords=true` for fictional local support/admin users with a password (Development only; `compose.yaml` sets it) and `Moderation__EscalationEmail` and `Moderation__EscalationBackupEmail` for urgent reports (from `SWOOVR_ESCALATION_EMAIL` and `SWOOVR_ESCALATION_BACKUP_EMAIL` in `.local/docker.env`, D159; Mailpit catches them locally). `.local/run-hosts.sh` sets all of these and starts the admin app when `src/Swoovr.Admin` exists. Invite a support person with `POST /api/v1/admin/invitations` (`{"email": ..., "role": "support"}`) as the root admin, create the same address in the staff realm, and sign in at the admin app. The root admin signs in there with password and OTP.
