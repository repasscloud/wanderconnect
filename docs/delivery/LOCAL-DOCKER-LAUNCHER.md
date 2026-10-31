# Managed localhost Docker installation

Run from the `main` checkout. The entry point is **`scripts/local-platform.sh`**.
This is separate from the older `scripts/docker-local.sh` foundation sandbox.
Requires Bash 3.2+ (ships with macOS), Docker Desktop/Engine and Compose v2.
Invoke the executable from Bash or zsh; no host .NET, Node, Python or browser
installation is needed. Internet is needed for first image pulls/restores.

## Start and stop

```sh
cd /Users/danijeljw/Developer/wanderconnect-brief
./scripts/local-platform.sh up --take-ports
./scripts/local-platform.sh status
```

`up` builds the API, member web and admin images, starts PostgreSQL, Keycloak and
Mailpit, applies migrations, imports the small GeoNames fixture, enrols two
fictional beta members, starts screening and all portals, configures realm flows,
creates missing fictional identities, enrols the admin's test TOTP, obtains a
real Keycloak session, grants application root authority and checks admin access.
Repeated startup retains credentials, existing identities and application data,
and applies current migrations/reference data. Demo members are seeded only on
initial/fresh startup, so reuse preserves test account deletions and beta revocations.

| Command | Effect |
| --- | --- |
| `up` or `up --reuse` | Start/create missing resources; retain existing data and member changes. |
| `up --fresh --yes` | Delete managed data, then start with new DB/realm and fictional fixtures. |
| `down` | Remove managed containers/network; retain all data volumes and credentials. |
| `reset --yes` | Remove managed containers/network and all managed data volumes/identity fixtures. |
| `reset --yes --forget-credentials` | Also remove the managed credential file. |
| `init` | Generate missing credentials without starting the platform. |
| `smoke` | Recheck portals/mail and a real password+TOTP admin session. |
| `admin-code` | Print a current fictional admin TOTP code. |
| `logs [service]` | Follow all managed logs, or e.g. `logs api`. |
| `help` | Print command/credential instructions. |

Destructive commands require `--yes`. They act only on Compose project
`swoovr-platform`, its declared volumes and managed identity fixture files.
Container and volume ownership labels bind this installation to this checkout;
commands from another checkout refuse to adopt or delete it, including while
its containers are down. They do not delete the old `swoovr-local` data or the sibling moderation API's
volumes. Named volumes hold the DB, Keycloak, email catcher, media, portal
protection keys and screening model cache. Reset retains credentials by default
so you can reuse your chosen passwords; `--forget-credentials` explicitly removes
them. Build images/caches and diagnostic/takeover records are retained.

## URLs and sign-in

| Service | URL |
| --- | --- |
| Member web | http://localhost:5081/account |
| Staff admin | http://localhost:5082 |
| Mailpit (email and simulated SMS/push) | http://localhost:8025 |
| Keycloak console | http://localhost:8180/admin/ |
| API liveness | http://localhost:5080/health/live |
| Photo-screening readiness | http://localhost:8080/ready |

Use **localhost** for browser sign-in, matching registered callbacks and passkeys.

Credentials are private in **`.local/platform/docker.env`**, mode0600:

- Staff admin: `SWOOVR_LOCAL_ADMIN_EMAIL` and `SWOOVR_LOCAL_ADMIN_PASSWORD`.
- Keycloak console: username `local-bootstrap`, password `KC_BOOTSTRAP_ADMIN_PASSWORD`.
- Fictional members: `fictional.alex@example.test` and
  `fictional.sam@example.test`, password `SWOOVR_LOCAL_MEMBER_PASSWORD`.

The admin's TOTP is enrolled automatically through the real Keycloak flow and
stored privately in `.local/platform/admin.json`. When the admin portal requests
an OTP, run `./scripts/local-platform.sh admin-code` and enter that code. If it
has just been used by a smoke check, wait for the next30-second code. This is a
disposable local test factor, not production MFA/key custody.

The two seeded members have **simulated verified email**, not real ownership
proof. Complete the 18+ action, simulated mobile verification (read its code in
Mailpit), home city/profile and other gates through the UI. Use fictional adult
data and synthetic images. Importing cities does not create a matching community;
reciprocal eligibility, verification and travel/location rules still apply.
See [testing](TESTING.md) for richer explicitly synthetic scenarios and
[current status](STATUS.md) for delivered functionality.

## Choose credentials before the first start

Either put these literal settings in the repository's ignored `.env` before
`init`, or run `init` and edit `.local/platform/docker.env` before the first `up`:

```dotenv
SWOOVR_LOCAL_ADMIN_EMAIL=local-owner@example.test
SWOOVR_LOCAL_ADMIN_PASSWORD="choose at least twelve characters"
KC_BOOTSTRAP_ADMIN_PASSWORD="choose another private password"
```

Use a fictional email ending `@example.test`. Missing passwords are generated
randomly and saved; the launcher never prints them. It reads dotenv as data,
never sources it as shell code. Use literal single-line values, optionally
quoted; embedded quotes, backslashes, dollar signs and backticks are rejected.
PostgreSQL password and founder key are generated64-character hexadecimal values.
The root `.env` supplies only recognised defaults when a managed value is absent;
existing managed values take priority. Keep provider secrets out of Git.

Changing a saved password does **not** rotate an existing Keycloak account or
PostgreSQL role. Change credentials before first use, or intentionally reset the
managed sandbox before changing them. Retained encrypted data needs its original
founder/data keys. Never replace those keys to resolve a startup problem.

## Port ownership, processes and failures

Required loopback ports: **5080,5081,5082,8180,8025,1025,5433,8080**. The launcher
uses a shared container network namespace so browser and container OIDC URLs
agree, while keeping ports bound to127.0.0.1. It uses no host-network mode and
keeps normal issuer/audience/signature validation. See Docker's
[service network mode](https://docs.docker.com/reference/compose-file/services/#network_mode).

Without `--take-ports`, conflicting containers cause startup to stop. With it,
the launcher stops only Docker containers publishing those ports; it preserves
their data and records their IDs in `.local/platform/displaced.tsv`. It also
records and gracefully stops positively identified API/Web/Admin host executables
from this exact checkout. It refuses other host processes. It never kills Docker
Desktop's forwarding process. Previously stopped services are not automatically
restarted, because they would compete for the same ports.

`.local/platform/containers.tsv` records every managed container's ID, name,
Linux PID, start time and status; `status` refreshes it. Linux container PIDs are
not macOS host PIDs. Stop targets exact Compose project labels/container IDs,
never broad process-name matches or stale numeric PIDs. No background host app
process is launched. `.local/platform/displaced-hosts.tsv` records any prior host
PIDs stopped during takeover. One-shot setup containers are removed on exit.

Concurrent lifecycle commands are locked in `.local/platform/lock`; the launcher
PID is recorded there. If a force-kill leaves a stale lock, verify that PID has
exited before removing the lock directory. On startup failure/interruption,
managed containers are stopped, data is retained and the command exits nonzero.
Inspect `logs`, fix the reported cause and rerun `up --reuse`. Stop/start signs
users out because portal ticket stores remain in memory.

## Screening, optional providers and limits

DEFAULT: screening uses the owner-maintained local Docker image
`image-moderation-api-moderation-api:latest`. If absent, the launcher builds it
from `../image-moderation-api` when that checkout exists. This separate service's
source and model downloads are a prerequisite for photos, not bundled Swoovr
source. You can supply a compatible `SWOOVR_SCREENING_IMAGE` in the managed env.
No Arachnid credentials or other secrets are copied from the sibling repository.
Its model image needs several GB of memory/disk and can take time to build.

`up --no-screening` explicitly starts everything else; photo processing cannot
complete. `Media:Screening:RequireKnownContentCheck=false` is local Development
only. Missing/disabled known-content checks never prove an image is clean.

Optional Didit **test** settings in the managed env are
`SWOOVR_DIDIT_API_KEY`, `SWOOVR_DIDIT_WORKFLOW_ID`,
`SWOOVR_DIDIT_WEBHOOK_SECRET`; restart with `up --reuse` after changing them.
Without them ID verification remains unavailable. See the
[provider walkthrough](LOCAL-PLATFORM-WALKTHROUGH.md#7-provider-dependent-features-and-background-work).
Social providers, real SMS/email/push, actual ID completion, native iOS/Android,
commerce and public hosting acceptance are not manufactured by this launcher.

CONFIRMED authentication, access and product rules stay authoritative. DEFAULT
launcher choices are local SMTP, filesystem media, fictional seeded accounts,
small reference data and enabled existing background workers. No policy draft
is marked approved and no production data/provider is seeded. OPEN external
provider/device/hosted acceptance remains described in delivery status.

## Local operator commands and verification

The API container already has the same database, keys and email settings as the
serving application. For an operator scenario, run its existing CLI directly:

```sh
docker exec swoovr-platform-api-1 dotnet Swoovr.Api.dll notifications sweep
```

Other existing commands include `founders allocate`, `accounts purge` and
`messages purge`; purge commands deliberately remove eligible local data.
The launcher does not install cron schedules or run destructive purge commands
as a side effect of startup.

Optional actual-browser acceptance (installed Edge and Playwright) is
`SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/local-platform/browser.cjs`.
It checks staff dashboard data, member BFF session forwarding and both HttpOnly
portal sessions. Do not run it concurrently with lifecycle commands/smoke,
since the test admin's OTP codes cannot be reused within a30-second window.

Verified31 October2026 on Docker Desktop LinuxARM64: fresh startup, stop/reuse
with a retained DB marker, preservation of a revoked demo beta enrolment, full
reset of all seven volumes/identity fixtures, intentional bad-password cleanup,
port takeover and exclusion of an unrelated60080→8080 container. Both actual
browser sign-ins and `./scripts/check.sh` passed, including10 launcher tests and
596 PostgreSQL cases. Contributor checks used `SWOOVR_TEST_DATABASE` explicitly
pointing at the managed database; the older automatic check-script fallback
still reads `.local/docker.env` for the legacy sandbox. External-provider,
real-device and hosted acceptance were not performed.
