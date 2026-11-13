# Swoovr UAT on one Vultr VM

Runbook for the UAT deployment (D361 host, D362 automation). The complete, disposable UAT stack runs on one Debian cloud VM with Docker Compose, behind Cloudflare and one Caddy. **Normal releases need no server commands:** push a `vX.Y.Z` tag from `main`, and [`.github/workflows/deploy-uat.yml`](../../.github/workflows/deploy-uat.yml) builds the images, publishes them to GHCR, connects to the VM as root over verified SSH, deploys, provisions and checks everything.

- GitHub configuration, secrets and variables, releasing: [docs/delivery/UAT-GITHUB-DEPLOYMENT.md](../../docs/delivery/UAT-GITHUB-DEPLOYMENT.md)
- Creating the VM: [VULTR-VM.md](VULTR-VM.md). Private admin access: [UAT-TAILSCALE-SETUP.md](../../docs/delivery/UAT-TAILSCALE-SETUP.md)
- What the server held before automation: [UAT-DEPLOYMENT-PREREQUISITES.md](../../docs/delivery/UAT-DEPLOYMENT-PREREQUISITES.md)

**Status (12 November 2026):** the stack was first deployed by hand on 12 November 2026 (realms still disabled, no accounts). The automated deployment is implemented and tested locally (`tests/deploy`, including a fake-Docker end-to-end run of every mode). It has not yet run on GitHub or on the VM.

## What runs on the host

| Service | Image (from `release.env`) | Public? |
| --- | --- | --- |
| `caddy` | Caddy 2.11.9 (owner-selected digest) | Yes: 80/443, Cloudflare ranges only |
| `web`, `api`, `admin` | `ghcr.io/repasscloud/swoovr-{web,api,admin}@sha256:…` | Through Caddy only |
| `keycloak` | `ghcr.io/repasscloud/swoovr-keycloak@sha256:…` | Through Caddy. `/admin` and the master realm only from the edge network (and optional Tailscale devices) |
| `swoovr-postgres` | `postgres:17.11-trixie` | No. TLS-only; the `swoovr` and `keycloak` databases |
| `clamav` | pinned ClamAV | No |
| `zammad-*` (init, railsserver, scheduler, websocket, nginx) | Zammad 7.2.2 | nginx through Caddy (`helpdesk-uat`) |
| `zammad-postgresql`, `-redis`, `-memcached`, `-elasticsearch` | pinned upstream | No |
| `mailpit` | Mailpit 1.31.0 | No. SMS sink; UI on the VM's loopback `127.0.0.1:8025` |
| Jobs: `db-bootstrap`, `migrate`, `places-import`, `keycloak-bootstrap`, `keycloak-reconcile`, `api-command` | `swoovr-operator` / `swoovr-api` | No; one-shot only |

Public names: `uat`, `api-uat`, `admin-uat`, `auth-uat`, `helpdesk-uat` `.swoovr.app` (D355). `venues-uat` is in the certificate and the realm templates, but nothing serves it and its realm stays disabled.

Host layout:

```text
/opt/swoovr/swoovr-uat/      the live Compose project (name swoovr-uat)
  compose.yaml Caddyfile release.env release.env.previous bin/ zammad/ postgres/ realms/   <- replaced by each deployment
  env/*.env  env/.generated   host-held configuration and secrets (0600; never overwritten, never in Git)
  tls/                        PostgreSQL CA/server certificate, Cloudflare Origin CA roots, origin-trust.pem
  state/releases.jsonl        deployment history; state/current-release.env; state/initial-admin
/opt/swoovr/releases/<tag>-<commit12>/swoovr-uat/   staged, checksummed releases (newest five kept, rollback sources)
/opt/swoovr/backups/<time>-pre-upgrade-<tag>/       automatic database backups (newest five; pre-refresh: newest three)
/etc/caddy/certs/uat-origin.{pem,key}               Cloudflare Origin CA certificate (owner-provisioned, read-only to Caddy)
/etc/swoovr/                                         firewall state from host-setup and cloudflare-ips
```

## Releasing (normal operation)

```sh
git checkout main && git pull --ff-only
python3 scripts/release_version.py next             # the next free version on the committed Major.Minor
git tag -a vX.Y.Z -m "Swoovr X.Y.Z"
git push origin vX.Y.Z
```

The run's summary shows the mode, the commit, every image digest, each check, the backup taken and the first-administrator state. Details, tag rules and refresh authorisation: [UAT-GITHUB-DEPLOYMENT.md](../../docs/delivery/UAT-GITHUB-DEPLOYMENT.md#releasing).

### What a deployment does on the VM

The workflow checks the host (`UAT_EXPECTED_HOSTNAME`, Debian amd64, Docker, 15 GB free, origin certificate present). It copies the bundle to `/opt/swoovr/releases/`, verifies its SHA-256 and every file's checksum, then runs `bin/swoovr deploy` from the staged copy. That command takes the host lock (`/run/lock/swoovr-uat-deploy.lock`), refuses a tag already deployed from another commit, installs the bundle into `/opt/swoovr/swoovr-uat` (never touching `env/` or `tls/`), creates any missing env file from its template (generating new secrets only for missing values), writes the provider values sent on stdin, loads the realm templates, and runs the mode:

| Step | `upgrade` (`DEPLOYMENT_MODE=upgrade`, default) | `refresh` (`DEPLOYMENT_MODE=refresh`) |
| --- | --- | --- |
| Guard | refuses if the Swoovr database volume doesn't exist | needs `--confirm-refresh swoovr-uat:<tag>` (passed by the workflow) |
| Check | `swoovr check` (all env values filled, digests pinned, TLS, origin certificate, Compose valid) | same, after the reset, allowing the four Zammad tokens |
| Backup | `pg_dump` of `swoovr`, `keycloak`, Zammad + Zammad storage; verified; **stops on failure** | same (pre-refresh); **stops before deleting anything** |
| Data | `migrate` once, with Web/Admin/API stopped | `docker compose down --volumes` for `swoovr-uat` only, **empty the R2 photo bucket** (owner, 12 November 2026; the bucket is kept), then a full bootstrap: `db-bootstrap` → `migrate` → `places-import` → Keycloak → `keycloak-bootstrap` (3 disabled realms) |
| Services | `up -d --remove-orphans`: only changed services are recreated | all started in dependency order |
| Provisioning | `zammad-provision` (idempotent, reuses its tokens), `keycloak-reconcile` | the same, with new Zammad tokens |
| Then | first-administrator stage 2, smoke checks, history | same |

`keycloak-reconcile` ([`scripts/uat/keycloak_reconcile.py`](../../scripts/uat/keycloak_reconcile.py)) runs every time and never deletes or disables a realm or touches a person's account:

1. It signs in as the master-realm service client `swoovr-deploy`. On a fresh Keycloak, the temporary admin creates that client. Its secret is generated on the host (`env/automation.env`), and its roles are limited to managing the three Swoovr realms plus reading master users.
2. It creates or updates the staff realm's `swoovr-zammad` helpdesk client.
3. It syncs the `entra` broker (tenant, client ID, secret, `trustEmail=false`).
4. It **verifies** the web clients' callbacks, and refuses on drift without rewriting them.
5. It enables the realms in `UAT_KEYCLOAK_ENABLED_REALMS` (`swoovr-members,swoovr-staff`; venues is refused).
6. It deletes the temporary admin `swoovr-bootstrap` once a personal master admin with OTP exists.
7. It checks the first administrator.

Nothing in a normal deployment changes the firewall, SSH, routing, Docker's configuration or the certificates. Docker pruning is never used.

## First deployment from the current server

The VM was set up by hand (images from Docker Hub; the temporary Keycloak admin already replaced, so no `swoovr-deploy` client exists). An `upgrade` would reach Keycloak reconciliation and stop there, with "No Keycloak deployment credential". The owner chose to start clean (12 November 2026): **the first automated deployment is a refresh.**

1. Complete [the prerequisites](../../docs/delivery/UAT-GITHUB-DEPLOYMENT.md#prerequisites-one-time).
2. `gh variable set DEPLOYMENT_MODE --body refresh`.
3. Push the tag (for example `v1.0.0`). On the first run the deploy job stops at *Images are public*: make the five packages public, then re-run the failed job.
4. After success: `gh variable set DEPLOYMENT_MODE --body upgrade`, so the next tag doesn't refresh again.
5. Do [First administrator](#first-administrator), [Zammad mail](#zammad-mail-microsoft-365) and, once, [the Keycloak console steps](#keycloak-admin-console) to replace the temporary admin.

The old Docker Hub images and `/root/.docker/config.json` are no longer used; GHCR images are public. You may remove the Docker Hub login (`docker logout`) and the five private Docker Hub repositories when you no longer need them.

## First administrator

The root admin is a **break-glass account** (D364): `UAT_INITIAL_ADMIN_EMAIL` is a dedicated address with no connection to the Entra tenant, with its own password and authenticator app. It still works when Entra or Microsoft 365 is down. Everyday administration uses Entra accounts with the Admin role (D363). Swoovr never grants privileges because an address matches.

1. **Every deployment (automatic):** if no staff user has that address, `keycloak-reconcile` creates one (username = the address, no password) and Keycloak emails it a link, valid 12 hours, to verify the address, choose a password and add an authenticator app. It never creates a second account or touches an existing one. A mail failure is shown as `root-account=…-email-failed` and never fails the deployment; **Actions → UAT maintenance → admin-invite** sends the link again.
2. **The owner completes the email** (verify, password of 12+ characters, scan the QR code with an authenticator app). Keep the authenticator's backup somewhere safe and offline.
3. **First sign-in:** `https://admin-uat.swoovr.app` → the **username/password form** (not "Sign in with Microsoft"), then the 6-digit code. Admin shows "no access", which is expected.
4. **Actions → UAT maintenance → admin-finalize** (or the next deployment). It grants root only if **all** hold:
   - exactly one enabled staff-realm user has that address, with no required action pending;
   - Keycloak shows the address **verified**;
   - it has a **password** and a **one-time code** credential;
   - **no** identity provider (Entra) is linked to it;
   - the Swoovr staff account exists (step 3), is active and carries the same address;
   - **no** root admin exists yet.

   It never moves root from another account and never re-grants (`root-admin ensure`, audited). Then choose **Sign in again** in Admin.

The summary and `state/initial-admin` show the state: `pending-invite`, `invite-not-completed`, `unverified-email`, `missing-password-or-authenticator`, `linked-to-identity-provider`, `pending-first-sign-in`, `ambiguous`, `disabled`, `granted`, `already-granted` or `held-by-another-account`. A deployment never fails because the admin is pending. **A refresh deletes the Keycloak users**, so after each refresh the account is created again, a new email arrives, and steps 2–4 repeat. Root's privileged actions still need password + code within the freshness rules (D36, D322).

Other staff roles: Entra app roles (below), invitations in Admin, or `./bin/swoovr job api-command staff-role grant <subject> support|trust-safety|admin|business` on the host.

## Zammad mail (Microsoft 365)

Zammad (helpdesk, `https://helpdesk-uat.swoovr.app`) is deployed and provisioned by every deployment:
- the queues and the Swoovr identities and tokens;
- the signed webhook to the API;
- staff single sign-on through Keycloak (button **Swoovr staff sign-in**).

Its **mail is configured in Zammad by you** (owner, 12 November 2026): the Microsoft 365 support mailbox and the notification sender. With `UAT_ZAMMAD_MANAGE_MAIL` unset or `false` (default), deployments never change Zammad's mail channels or sender, so your settings survive every upgrade.

Staff who sign in with SSO are Zammad *Agents*. The staff sync keeps them that way, so channel settings are changed as Zammad's built-in administrator:

```sh
ssh root@<VM IPv4>
cd /opt/swoovr/swoovr-uat && ./bin/swoovr zammad-admin-login on
# Open https://helpdesk-uat.swoovr.app in a private window. Sign in as `swoovr-provisioner`, with the password
# SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD from env/zammad-provision.env (copy it from an editor; don't echo it).
# Settings -> Channels -> Email -> add the Microsoft 365 account (or Settings -> Channels -> Microsoft 365, issue #319),
# with destination group "General Support"; then Settings -> Channels -> Email -> Settings for the notification sender.
./bin/swoovr zammad-admin-login off
```

Every deployment switches the password form off again, so a forgotten `on` lasts only until the next release.

**A refresh deletes Zammad's data, including this mail setup:** repeat it after each refresh. To have deployments manage Zammad mail from GitHub values instead (SendGrid out, IMAP in), set `UAT_ZAMMAD_MANAGE_MAIL=true` and the `UAT_SMTP_*` / `UAT_ZAMMAD_*` values.

## Microsoft Entra

Reuse the existing app registration **Swoovr UAT staff** (single tenant). Nothing here creates or changes it; the deployment configures only Keycloak's side.

| Entra setting | Value |
| --- | --- |
| Platform **Web** redirect URI | `https://auth-uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint` (exactly; the only one Keycloak uses) |
| Supported account types | this organisational directory only (the realm accepts only the tenant-specific issuer) |
| API permissions | Microsoft Graph delegated `openid`, `profile`, `email` (admin consent recommended) |
| Enterprise app → Assignment required | **Yes**, with only approved staff assigned |
| Client secret | max. 24 months; put the expiry in your calendar; when renewed, update `UAT_ENTRA_CLIENT_SECRET` and deploy |

### Staff roles from Entra app roles (D363)

| Entra | Setting |
| --- | --- |
| App registration → App roles | `Support`, `TrustSafety`, `Admin`, `Legal` (Value exactly as written; allowed member types Users/Groups; enabled) |
| Security groups | one per role, e.g. "Swoovr UAT Support", members added directly (nested groups don't pass roles) |
| Enterprise app → Users and groups | "Swoovr UAT" → Default Access (may sign in); each role group → its role |

At every Microsoft sign-in Keycloak copies the `roles` claim into `swoovr_entra_roles`, and the API adds or removes the person's **Entra-managed** roles to match. Roles granted in Swoovr Admin, by invitation or the CLI are never changed by Entra; Admin shows Entra roles as "managed in Entra". A change in Entra applies at the person's next Microsoft sign-in. After Microsoft, staff also enter a Keycloak one-time code (authenticator app, set up at the first sign-in) and verify their email once by link.

GitHub: `UAT_ENTRA_TENANT_ID` (Directory ID), `UAT_ENTRA_CLIENT_ID` (Application ID, not the Object ID), secret `UAT_ENTRA_CLIENT_SECRET`. Each deployment generates the realm templates from these and the tagged commit. `keycloak-reconcile` corrects the broker's tenant/client/secret if they differ, and verifies the Keycloak client callbacks: `https://uat.swoovr.app/signin-oidc`, `https://admin-uat.swoovr.app/signin-oidc` (and `/signout-callback-oidc`), and the helpdesk `https://helpdesk-uat.swoovr.app/auth/openid_connect/callback`. Those are Keycloak callbacks and are not registered in Entra.

## Keycloak admin console

The console answers only inside the VM: Caddy returns 404 for `/admin` and `/realms/master` from the public internet (the workflow checks this on every run). Two private routes:

- **Tailscale** (after you set it up): [UAT-TAILSCALE-SETUP.md](../../docs/delivery/UAT-TAILSCALE-SETUP.md) §5.
- **SSH SOCKS tunnel** (always works; keep it until Tailscale is verified):
  1. Once: download Cloudflare's Origin CA RSA root and check its fingerprint: `curl -fsSL https://developers.cloudflare.com/ssl/static/origin_ca_rsa_root.pem -o ~/swoovr-uat-private/origin_ca_rsa_root.pem`, then `openssl x509 -in … -noout -fingerprint -sha256` must print `D3:C7:E8:5C:91:70:7F:C0:A1:2A:BC:5D:88:26:67:47:AA:4F:A8:E7:B1:62:F6:33:FF:B3:C9:D9:89:94:76:20`.
  2. Once: a separate Firefox profile `swoovr-uat-admin` (`about:profiles`) with **Manual proxy configuration**: SOCKS `127.0.0.1:1080`, SOCKS v5, **Proxy DNS when using SOCKS v5**. Import that root under Settings → Certificates → Authorities, trusted for websites. Use the profile only for this.
  3. Each time: `ssh -D 1080 -N root@<VM IPv4>`, then open `https://auth-uat.swoovr.app/admin/` in that profile. The VM resolves the name to Caddy's internal address (`/etc/hosts`, from `host-setup`).

**After every refresh**, Keycloak starts empty, and the temporary admin `swoovr-bootstrap` exists again (password: `KC_BOOTSTRAP_ADMIN_PASSWORD` in `env/keycloak.env`; no MFA). The summary shows `bootstrap-admin=kept-no-personal-admin`. Once, in the console (master realm), sign in as `swoovr-bootstrap`:

1. **Users → Add user**: your own username, email verified, required action **Configure OTP**. **Credentials**: a new long password, Temporary off. **Role mapping**: realm role `admin`.
2. Sign out, sign in as yourself, and enrol your authenticator app.

The next deployment (or `admin-finalize`) then deletes `swoovr-bootstrap` automatically (`bootstrap-admin=deleted`). Never edit `KC_BOOTSTRAP_ADMIN_PASSWORD`: Keycloak reads it only for an empty database.

If `swoovr-deploy` is ever missing on an existing Keycloak (for example, someone deleted it), deployments stop at Keycloak reconciliation. Either refresh, or recreate it by hand in the console (master realm → Clients → `swoovr-deploy`, confidential, service accounts only, its secret = `SWOOVR_KC_DEPLOY_CLIENT_SECRET` from `env/automation.env`; service-account roles: `swoovr-members-realm`, `swoovr-staff-realm` and `swoovr-venues-realm` → `view-/manage-realm`, `view-/manage-clients`, `view-/manage-users`, `view-/manage-identity-providers`; `master-realm` → `view-users`; client scope mappings the same, full scope off).

## Health checks

| Level | Checked by | Meaning |
| --- | --- | --- |
| Container running | `bin/swoovr` (`docker inspect`) | process up; restarting or unhealthy containers fail the deployment |
| Container healthy | Compose health checks: PostgreSQL (both), Keycloak `/health/ready` (port 9000), API/Web/Admin `/health/live`, Zammad Rails, Redis, Memcached; `zammad-init` must exit 0 | each service answers its own probe |
| Application ready | through Caddy inside the VM: API `/health/ready` (database, no pending migrations, restore fence), API `/version` = the tag's version, `/health/attachments` (ClamAV), Web and Admin `/health/ready` (sign-in secret + API ready), helpdesk 200, OpenID discovery of each enabled realm with the right issuer | the release is live and its dependencies answer |
| Public smoke | from the GitHub runner through Cloudflare: the same readiness and version, the home page, realm discovery, `/admin/` and the master realm **404**, the messaging hub **401** without sign-in, a helpdesk WebSocket upgrade **101** | DNS, Cloudflare, TLS, the edge filter and WebSockets work |

Any failure fails the workflow and names the stage. The guided human journey (two members, real email/SMS/Didit) stays a manual acceptance test: `node scripts/uat/smoke.cjs --base-url https://uat.swoovr.app`.

## Rollback

`./bin/swoovr rollback --to vX.Y.Z` (on the host; manual, exceptional). It reinstalls that **staged** release (the newest five are kept under `/opt/swoovr/releases`) and recreates the services. It records `rollback` in the history and runs the checks. **It does not roll back the database:** migrations applied by later releases stay applied. Use it only when the older release understands the current schema. EF Core migrations are forward-only: an older API image refuses no extra migration, but it may break on changed columns. For a schema rollback, restore the pre-upgrade backup (below). Never move a tag to "redeploy" an old commit: tags are immutable, and the host refuses a tag it saw from another commit.

## Recovery

| Situation | What happened | Do |
| --- | --- | --- |
| Failed before `migrate` (check, pull, backup) | nothing changed in data; the new bundle files may be installed | fix the cause, re-run the workflow (same tag) |
| `migrate` failed | the API, Web and Admin are stopped; EF Core normally applies each migration in its own transaction, so the failing one is rolled back, but earlier migrations of this release may be applied | read `./bin/swoovr logs` / the job output; fix forward with a new tag, **or** restore the pre-upgrade backup (below) and `./bin/swoovr rollback --to <previous tag>` |
| Failed after `migrate` (start, provisioning, smoke) | schema is new; services partly recreated | re-run the workflow after fixing; or `./bin/swoovr rollback --to <previous>` if that release tolerates the schema |
| Refresh failed during bootstrap | old data gone (backup in `/opt/swoovr/backups/*-pre-refresh-*`); stack partly built | fix and re-run with `DEPLOYMENT_MODE` still `refresh`; a refresh always starts with a fresh `down --volumes` |
| Anything | `./bin/swoovr history`, `./bin/swoovr status`, `state/releases.jsonl` (stage of the failure) | |

**Restoring a backup** (destructive; only by decision). Stop the apps, then restore into the existing databases with the same superuser, then start again:

```sh
cd /opt/swoovr/swoovr-uat; B=/opt/swoovr/backups/<directory>
( cd "$B" && sha256sum -c SHA256SUMS )
./bin/swoovr compose stop api web admin keycloak zammad-railsserver zammad-scheduler zammad-websocket zammad-nginx
PG=$(./bin/swoovr compose ps -q swoovr-postgres); ZP=$(./bin/swoovr compose ps -q zammad-postgresql)
# Dumps keep object owners (swoovr_migrator, swoovr_keycloak); the roles exist on the same host, so restore as superuser.
for db in swoovr keycloak; do
  docker exec -i "$PG" sh -c 'pg_restore -U "$POSTGRES_USER" -d "$1" --clean --if-exists --single-transaction' sh "$db" < "$B/$db.dump"
done
docker exec -i "$ZP" sh -c 'pg_restore -U "$POSTGRES_USER" -d "$ZAMMAD_DB" --clean --if-exists --single-transaction' < "$B/zammad.dump"
./bin/swoovr job db-bootstrap        # re-applies role passwords, ownership and grants (idempotent)
./bin/swoovr rollback --to <the tag the backup was taken for>   # the images that match the restored schema
```

This procedure has not been rehearsed on the VM. On UAT a **refresh is the normal recovery** (owner: disposable). A restored Swoovr database needs the same data keys (`env/` is never changed by a refresh or upgrade). After a Zammad restore, run `./bin/swoovr job api-command retention helpdesk --reconcile` ([BACKUP-RESTORE.md](../../docs/delivery/BACKUP-RESTORE.md#helpdesk-zammad-backups-and-restore)).

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| *Strict version tag* fails | tag isn't `vX.Y.Z`; delete it **only if nothing deployed it** (`git push origin :refs/tags/<bad>`) and push a correct one |
| *Tagged commit is on main* fails | tag created on a branch or an unmerged commit; merge first, tag the merge commit on `main` |
| *Version matches Directory.Build.props* fails | `X.Y` differs from the committed Major.Minor; bump in a PR, or use the right tag |
| *Deployment mode* fails | `DEPLOYMENT_MODE` is neither `upgrade` nor `refresh` (or empty) |
| a release refreshed unexpectedly | `DEPLOYMENT_MODE` was still `refresh`; set it to `upgrade` after every refresh |
| *Images are public* fails | make the GHCR packages public, re-run the job |
| *Server preflight*: host key verification failed | `UAT_SSH_HOST_KEY` doesn't match; re-check the fingerprint (never disable checking) |
| ssh: connection timed out | something in front of the VM blocks port 22 (no Vultr firewall group is used today) |
| `this host is …, expected …` | `UAT_EXPECTED_HOSTNAME` or `UAT_SSH_HOST` points at another machine |
| `the first deployment must be a refresh` | no Swoovr database volume |
| `another deployment is running` | a run or a maintenance job holds `/run/lock/swoovr-uat-deploy.lock` |
| `… was deployed before from commit …` | a reused tag; push a new version |
| `FAIL …: … is not filled` | a provider value is unset on GitHub and on the host; set it in the `uat` environment |
| `Keycloak reconcile failed: No Keycloak deployment credential` | see [Keycloak admin console](#keycloak-admin-console), last paragraph |
| `… callbacks do not match …` | someone edited a client in Keycloak; restore the callback in the console |
| `keycloak-bootstrap` fails on a refresh | `./bin/swoovr logs keycloak`; the six retries cover Keycloak's start-up 503 |
| SMS code needed | Mailpit: `ssh -L 8025:127.0.0.1:8025 -N root@<VM IPv4>`, open http://127.0.0.1:8025 |

## Day-2 commands (on the host, exceptional)

| Task | Command |
| --- | --- |
| Status and checks | `./bin/swoovr status` (or Actions → UAT maintenance → status) |
| History | `./bin/swoovr history` |
| First admin stage 2 | `./bin/swoovr admin-finalize` (or the maintenance workflow) |
| Mail settings changed on GitHub | deploy a new tag (values are applied on every deployment), or edit `env/mail.env` and `./bin/swoovr mail-apply` |
| Zammad admin sign-in for mail setup | `./bin/swoovr zammad-admin-login on` … `off` ([Zammad mail](#zammad-mail-microsoft-365)) |
| Backup now | `./bin/swoovr backup` |
| Logs | `./bin/swoovr logs api` |
| Refresh Cloudflare ranges (monthly; host firewall) | `./bin/cloudflare-ips && ./bin/swoovr compose restart caddy` |
| Optional Zammad backup container | `./bin/swoovr compose --profile backup up -d zammad-backup` |

The manual `upgrade` and `reset` commands of the earlier bundle are gone: a deployment is always a tagged release, and a reset is always a refresh deployment.

## Host-level changes

| Change | When | Why |
| --- | --- | --- |
| `/opt/swoovr/releases`, `/opt/swoovr/backups`, `/opt/swoovr/swoovr-uat/state` (0700) | every deployment | staged releases, backups and history |
| `/run/lock/swoovr-uat-deploy.lock` | every deployment | one deployment at a time |
| `env/automation.env` (generated `swoovr-deploy` secret) | first automated deployment | Keycloak automation credential |
| the deploy key in `/root/.ssh/authorized_keys` | once, by the owner | GitHub runner SSH |
| `env/caddy-admin.env` | optional, by the owner | Keycloak admin over Tailscale |

`host-setup`, `tls-setup` (except when TLS files are missing) and `cloudflare-ips` don't run during deployments. They stay for building a new VM: VULTR-VM.md §5, then this file's [first deployment](#first-deployment-from-the-current-server) with refresh.

## Known gaps

- **Single host, single replica:** every deployment restarts the apps and signs members out (in-memory BFF sessions).
- **Backups stay on the VM**; there are no off-host copies (owner: UAT is disposable).
- **A refresh empties the configured R2 photo bucket** (`Media__R2__Bucket`), but not the Cloudflare image-pipeline buckets or queues (`workers/images`) or provider-side records (Didit sessions).
- **SMS goes to Mailpit** until Twilio SMS verification (D360). Zammad inbound mail needs the IMAP values.
- **The Keycloak image bakes UAT policy URLs**; production needs its own build. The removed `images.yml` also held the Azure ACR promotion job; production promotion from GHCR digests is a follow-up.
- **`founders allocate`** (a daily job, D130) has no schedule on Vultr.
- **The personal Keycloak admin** after a refresh needs a browser and an authenticator (cannot be automated without inventing a password).
