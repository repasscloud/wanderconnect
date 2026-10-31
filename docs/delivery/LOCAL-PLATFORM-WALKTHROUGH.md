# Run Swoovr locally with Docker infrastructure

Written 31 October 2026. Run these commands from the `main` checkout at
`/Users/danijeljw/Developer/wanderconnect-brief`.

**Current boundary:** `compose.yaml` is not a complete platform deployment. It
has PostgreSQL, Keycloak, Mailpit and basic API/web images, but the web container
has no member sign-in configuration and there is no admin service. To explore
the integrated platform today, run the infrastructure in Docker and the three
.NET hosts locally using the steps below. An entirely containerised interactive
platform needs additional Compose wiring; these instructions do not claim it
exists. Mobile apps require native tooling. See [current status](STATUS.md) for
implemented features and outstanding release gates.

CONFIRMED: Keycloak authentication, separate staff administration, native mobile
clients and the product requirements remain authoritative. DEFAULT: the local
setup below uses Development, fictional adult accounts, loopback HTTP, local
media and caught email. OPEN/provider-dependent: external verification, social
login, actual push delivery and hosted acceptance need their own configuration
and evidence. Payments and venue commerce are not a complete live journey.

## 1. Prerequisites

Install/start Docker Desktop (or Docker Engine with Compose v2), Python 3,
Node.js 22 or newer and the .NET SDK selected by `global.json`. The SDK is
required for this integrated setup even though Docker image builds do not need
it on the host. First restores/image downloads need internet.

```sh
cd /Users/danijeljw/Developer/wanderconnect-brief
git branch --show-current
git status --short
docker version
docker compose version
python3 --version
node --version
dotnet --version
```

Use `main`; update with `git pull --ff-only` only when your checkout is clean
and you intend to update it. Keep ports 5080 (API), 5081 (member web), 5082
(admin), 8180 (Keycloak), 8025 (mail UI), 1025 (SMTP), 5433 (PostgreSQL) and,
when screening photos, 8080 free. Do not stop unrelated processes to free ports.

## 2. Start the Docker infrastructure

```sh
./scripts/docker-local.sh init
docker compose --env-file .local/docker.env up -d --wait postgres mailpit keycloak
./scripts/docker-local.sh realms
./scripts/docker-local.sh status
```

`init` retains existing secrets and adds missing ones to ignored, private
`.local/docker.env`. Do not share that file or resolved Compose configuration.
`realms` configures member, venue and staff realms, local email and application
clients. It needs Node on the host. Keycloak imports existing realms only once;
rerunning `realms` applies supported settings without deleting accounts.

If you previously used `./scripts/docker-local.sh up`, its API/web containers
occupy the application ports. Stop those two before starting local hosts:

```sh
docker compose --env-file .local/docker.env stop api web
```

## 3. Load local API configuration

Open three terminals at the repository root. In **each terminal**, paste this
block. It reads generated secrets without printing them. Run it in Bash or zsh;
do not enable shell tracing. Keep the same founder key across restarts so
previously encrypted local data stays readable.

```sh
val() { sed -n "s/^$1=//p" .local/docker.env | head -n 1; }
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export ConnectionStrings__Swoovr="Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=$(val SWOOVR_DB_PASSWORD)"
export Founders__Secrets__1="$(val SWOOVR_FOUNDER_SECRET_V1)"
export MemberAdmin__ClientSecret="$(val SWOOVR_MEMBERS_API_ADMIN_SECRET)"
export Keycloak__BaseUrl=http://localhost:8180
export Keycloak__RequireHttpsMetadata=false
export Staff__LocalPasswords=true
export Email__Smtp__Host=127.0.0.1
export Email__Smtp__Port=1025
export Moderation__EscalationEmail=local-escalation@example.test
export Moderation__EscalationBackupEmail=local-backup@example.test
export Didit__ApiKey="$(val SWOOVR_DIDIT_API_KEY)"
export Didit__WorkflowId="$(val SWOOVR_DIDIT_WORKFLOW_ID)"
export Didit__WebhookSecret="$(val SWOOVR_DIDIT_WEBHOOK_SECRET)"
export Media__Screening__BaseUrl=http://127.0.0.1:8080
export Media__Screening__RequireKnownContentCheck=false
```

These are host addresses: `localhost` inside a container would refer to that
container, so this block must not be copied unchanged into Compose.
Development SMTP is caught in Mailpit; it is not real email/SMS delivery.
The relaxed known-content requirement is Development-only and is not a clean
bill of health for uploaded content. Use synthetic images only.

## 4. Build, initialise and start the applications

In terminal 1, after the configuration block:

```sh
dotnet restore Swoovr.Local.sln --ignore-failed-sources -p:NuGetAudit=false
dotnet build Swoovr.Local.sln -c Debug --no-restore
dotnet run --project src/Swoovr.Api --no-build -- migrate
dotnet run --project src/Swoovr.Api --no-build -- geonames import tests/fixtures/geonames
dotnet run --project src/Swoovr.Api --no-build --no-launch-profile --urls http://127.0.0.1:5080
```

The GeoNames fixture supplies a small reference-data set, not a populated member
community. For wider place coverage follow `scripts/geonames.sh` and its usage.
Development also applies database migrations on API startup. Never point these
commands at production. Leave terminal 1 running.

In terminal 2, after the same configuration block:

```sh
export Keycloak__Authority=http://localhost:8180/realms/swoovr-members-dev
export Keycloak__ClientSecret="$(val SWOOVR_MEMBERS_WEB_SECRET)"
export Api__BaseUrl=http://127.0.0.1:5080/
dotnet run --project src/Swoovr.Web --no-build --no-launch-profile --urls http://localhost:5081
```

In terminal 3, after the same configuration block:

```sh
export Keycloak__Authority=http://localhost:8180/realms/swoovr-staff-dev
export Keycloak__ClientSecret="$(val SWOOVR_STAFF_WEB_SECRET)"
export Api__BaseUrl=http://127.0.0.1:5080/
dotnet run --project src/Swoovr.Admin --no-build --no-launch-profile --urls http://localhost:5082
```

Always browse the portals with `localhost`, not `127.0.0.1`, to match registered
callbacks and the passkey relying-party ID. Do not rebuild while browsing or
running suites; stop the three hosts, rebuild, then restart them together.

The existing private `.local/run-hosts.sh`, if present, is an alternative for
the owner's configured machine, documented in [testing](TESTING.md). It is not
tracked or required by these steps. It replaces hosts and overwrites logs:
back up `.local/logs/` privately before using it.

## 5. Open the platform and create fictional accounts

| Component | Address | Purpose |
| --- | --- | --- |
| Member web | http://localhost:5081/account | Register/sign in and account gates |
| Admin | http://localhost:5082 | Staff sign-in and administration |
| Mailpit | http://127.0.0.1:8025 | Email links and simulated SMS/push |
| Keycloak | http://localhost:8180/admin/ | Local identity administration |
| API liveness | http://127.0.0.1:5080/health/live | Process check only |
| API metadata | http://127.0.0.1:5080/api/status | Foundation metadata |

Use Keycloak bootstrap username `local-bootstrap`; inspect
`KC_BOOTSTRAP_ADMIN_PASSWORD` in the private environment file locally for the
password. There is no committed universal password.

1. Open a fourth terminal, load the configuration block from section 3 and enrol
   two fictional adult emails before registering them:

   ```sh
   dotnet run --project src/Swoovr.Api --no-build -- beta add fictional.alex@example.test fictional.sam@example.test
   ```

2. Register each address through the member web account page. Use separate
   browser profiles/private windows so you can act as two people. Complete age
   attestation and email verification using Mailpit. These receipts establish
   only the local simulation, not real destination ownership.
3. Complete the required profile and mobile-number steps. Use fictional numbers
   from the documented Australian range 0491 570 006–159. Read simulated SMS
   codes in Mailpit at `sms-<number>@simulated.swoovr.invalid`.
4. Set compatible modes/preferences and a home city available in the imported
   reference data. Complete the eligibility/verification gates shown by the
   application. Enrolling an email does not bypass those gates. An empty
   discovery list is expected until other accounts meet reciprocal rules.
5. Explore profiles, intentions/exclusions, travel, connections, accepted chats,
   blocking/reporting, meeting safety, notification centre, private appearance
   sync, export and deletion. Some journeys require eligible accounts, provider
   configuration or scheduled operator work; see sections 6–7. Do not treat the
   static design gallery or `/prototype/` scenarios as authenticated evidence.

For scripted, explicitly synthetic eligible-account scenarios, use the suites
in [TESTING.md](TESTING.md), which create and prepare their own fictional data.
Do not remove production access rules to populate a manual demo.

## 6. Set up the local root administrator

In the configured fourth terminal:

```sh
./scripts/docker-local.sh root-admin
```

This creates `root-admin@example.test` in the staff realm and stores its password
and user ID in private `.local/swoovr-root-admin.json`. Inspect it locally.
First sign in to the admin portal and complete TOTP enrolment so the API creates
the staff account row; access may still be denied before the grant. Then grant
application authority using that fixture's `userId` (the Keycloak subject):

```sh
dotnet run --project src/Swoovr.Api --no-build -- root-admin grant "$(python3 -c 'import json; print(json.load(open(".local/swoovr-root-admin.json"))["userId"])')"
```

Sign in to the admin portal with that password. Complete its local TOTP setup,
then sign out and sign in again with password and an OTP: enrolment alone does
not prove a fresh password-plus-OTP session. Root-admin authority is an
application grant, not merely a Keycloak role. Staff invitations and sensitive
operations have additional permission/elevated-access requirements. Never use
social sign-in for staff/partner administration.

Optional automated TOTP enrolment uses installed Edge and Playwright:

```sh
SWOOVR_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/auth/root-admin.browser.cjs
```

The suite stores its test TOTP secret privately. Run browser suites serially;
they share local data and rate limits. Follow [TESTING.md](TESTING.md) for each
suite's prerequisites, overrides and cleanup.

## 7. Provider-dependent features and background work

- **Photos:** start the existing `image-moderation-api` checkout in a separate
  terminal with its own Docker Compose setup and verify
  `http://localhost:8080/ready`. See [photo setup](LOCAL-DEVELOPMENT.md#photos-and-media-screening--28-october-2026).
  Without R2 settings Development uses local `.local/media`; it is persistent
  host data and is not removed by Compose shutdown. R2 is optional and requires
  private credentials passed as `Media__R2__*`; never use a production bucket.
- **Didit:** add test credentials as `SWOOVR_DIDIT_API_KEY`,
  `SWOOVR_DIDIT_WORKFLOW_ID`, `SWOOVR_DIDIT_WEBHOOK_SECRET` in the private env
  file, reload section 3 and restart API. Without them verification returns 503.
  Follow [Didit test steps](LOCAL-DEVELOPMENT.md#didit-test-verification-walkthrough)
  for callback/tunnel configuration and evidence boundaries.
- **Apple/Google:** not configured by local realm bootstrap. Test email/password
  and passkeys locally; external social acceptance requires provider setup.
- **Email, SMS and push:** local caught messages/simulated delivery do not prove
  real delivery. Actual native push needs APNs/FCM credentials and devices; see
  [push architecture](../architecture/PUSH-NOTIFICATIONS.md).
- **Background jobs:** the configured fourth terminal can run, for example,
  `dotnet run --project src/Swoovr.Api --no-build -- notifications sweep`,
  `-- accounts purge`, `-- messages purge` or `-- founders allocate`.
  These mutate local data and may delete eligible expired data; run only the
  job needed for your scenario. Keep the same database, keys, mail and provider
  settings as API. Starting containers does not install hosted cron schedules.
- **Native apps:** Docker cannot run the MAUI iOS/Android UI. Follow
  [mobile tooling](MOBILE-TOOLING-SETUP.md) and [native UAT](NATIVE-UAT.md).
- **Venue bookings/payments/subscriptions:** consult [status](STATUS.md) and
  [commerce requirements](../product/05-VENUES-VOUCHERS-AND-COMMERCE.md) for
  delivered versus planned functionality. Local startup does not supply venues,
  provider credentials or live commerce.

## 8. Checks, troubleshooting and shutdown

```sh
curl --fail http://127.0.0.1:5080/health/live
curl --fail http://127.0.0.1:5080/api/status
curl --fail http://localhost:5081/account
curl --fail http://localhost:5082/
./scripts/check.sh
```

Checks need their loopback smoke ports free (see the script). They use a
throwaway persistence database when PostgreSQL is available. Liveness and HTML
responses alone do not establish successful authentication or full acceptance.
`docker-local.sh smoke` targets the basic container sandbox and is not the
acceptance test for this mixed Docker/.NET setup.

| Symptom | Check/action |
| --- | --- |
| Port already in use | Stop the old owned host or Compose API/web; do not run both on 5080/5081. |
| No sign-in or wrong callback | Use the .NET web host, correct authority/secret, `localhost:5081`, and rerun `realms`. |
| Token 401 | Start with `dotnet run --project` and Development configuration; check Keycloak is reachable. |
| Beta/phone/profile denial | Complete enrolment and the corresponding account gates. |
| No people found | Import places, prepare two eligible reciprocal profiles; respect modes, blocks and location/travel gates. |
| Verification 503 | Didit test configuration is missing; follow section 7. |
| Photo processing unavailable | Check screening readiness and local storage permissions/provider settings. |
| Stock Keycloak theme | Recreate only Keycloak with `docker compose --env-file .local/docker.env up -d --force-recreate --wait keycloak`, then rerun `realms`. |
| Admin denied after TOTP setup | Sign in again with OTP and verify the application root grant. |

Stop each .NET host with Ctrl-C. Then:

```sh
./scripts/docker-local.sh down
```

This preserves PostgreSQL and Keycloak volumes, private fixtures, local media
and keys for your next run. Start section 2 and restart the hosts to resume.

For an intentional **destructive sandbox reset**, first stop all three hosts,
back up anything wanted and confirm you are operating on `swoovr-local`:

```sh
docker compose --env-file .local/docker.env ps -a
docker compose --env-file .local/docker.env down --volumes
```

This deletes this Compose project's database and Keycloak volumes, including
accounts and sessions. After a reset, retained `.local/swoovr-root-admin.json`
and other auth fixtures reference removed identities: archive them privately
and create fresh fixtures rather than editing their IDs. Retain keys needed to
read any preserved local media/evidence/backups. Do not run broad Docker prune
commands or delete unrelated/pre-rename volumes.

## Validation boundary for this guide

Commands and configuration were checked against tracked Compose, scripts and
application sources. On 31 October 2026, `./scripts/check.sh` passed, including
596 PostgreSQL persistence cases and host smoke checks. All 14 shell blocks
passed `bash -n`; relative document links resolved. See [VALIDATION.md](VALIDATION.md) for previous execution
evidence. Documentation verification does not assert a fresh full browser,
provider or native-device run; those must be recorded separately when performed.
