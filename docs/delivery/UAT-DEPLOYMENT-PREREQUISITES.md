# UAT deployment prerequisites (before the deployment redesign)

12 November 2026. **Investigation and preparation only.** Nothing was deployed, changed on the UAT server, committed or published. This document records what the current Vultr UAT deployment actually does (verified against the scripts, not only the runbook), what the owner's local backup `~/swoovr-uat-backups/swoovr-uat-backup-20261112-0024` holds, what must still be collected from the server, and what the planned GitHub Actions deployment needs.

Labels follow the repository rules: **CONFIRMED** = owner requirement, **DEFAULT** = reversible builder recommendation, **OPEN** = unresolved. The target design in §6 is the owner's brief for the next phase (12 November 2026); several parts of it change CONFIRMED or DEFAULT decisions and are listed as OPEN questions in §9 until the owner confirms them.

**Follow-up (12 November 2026):** the owner's next brief answered most of §9, and the automated deployment is implemented as **D362**. See the [runbook](../../deploy/vultr-uat/README.md) and [GitHub configuration](UAT-GITHUB-DEPLOYMENT.md).
- **Answered:** Q-A (GHCR public, amd64 only), Q-C (root SSH), Q-E (refresh keeps server-held secrets), Q-F (automate Keycloak with a two-stage first admin), Q-G (provider values from GitHub, generated values on the host), Q-H (a verified backup before upgrade and refresh), Q-I (strict `vX.Y.Z` on `main`) and Q-J (`bin/` restored as the single entry point).
- **Answered later the same day:** Q-B: no Vultr firewall group exists and the host already accepts key-only SSH, so the runners connect directly; Tailscale stays manual. Q-D: a refresh empties the R2 photo bucket. The UAT_REFRESH_CONFIRM/approval gate was dropped: `DEPLOYMENT_MODE` alone selects refresh.
- The owner wants **no** database dump of the current UAT (fresh start), so §8.4 is not needed.

### Server collection results (12 November 2026, `collect-20261112-2225`)

Collected with `scripts/uat/collect-server-state.sh` and checked by names, hashes and metadata only.

- **Host:** Debian 13 (trixie) amd64, 31 GiB RAM, 601 GB disk (4 % used), hostname `uat-01`, public interface `enp1s0`. Docker Engine 29.9.3, Compose v5.7.1, `daemon.json` as written by `host-setup` (`userland-proxy: false`, `live-restore`).
- **Firewall:**
  - the nftables table `inet swoovr_host` (input drop, SSH 22 from any address);
  - `DOCKER-USER` → `SWOOVR-EDGE` on `enp1s0` for 80/443 with 23 Cloudflare ranges;
  - `swoovr-host-firewall` and `swoovr-edge-firewall` enabled;
  - **UFW is also enabled** (allows 22), which `host-setup` doesn't install.
- **SSH:** key-only (`passwordauthentication no`, `permitrootlogin without-password`). Three host keys (ED25519 fingerprint recorded in the collection). Tailscale is not installed.
- **Docker:** project `swoovr-uat` only (16 running containers; `zammad-init` and `volume-permissions` exited). All images are the `5a7fa88` digests from Docker Hub, with a Docker Hub login. Twelve volumes: `swoovr-pgdata` 637 MB, `zammad-pgdata` 220 MB, `clamav-db` 168 MB, the rest small.
- **State:**
  - all three realms **disabled**;
  - the master realm has no `swoovr-bootstrap` (already replaced by a personal admin), so **no automation credential exists**;
  - the Entra broker is configured (`trustEmail=false`, `storeToken=false`);
  - **no Swoovr accounts** and no root admin;
  - the EF history table is `__ef_migrations_history` (the collection script queried the default name, so that one query failed; harmless).
- **Compared with the 11 November backup:** only the origin certificate and key are unchanged. Every env file, `.generated` and the PostgreSQL TLS material were regenerated on the server after the backup. **The local backup is stale; the collection's sensitive archive holds the current values.**
- **Script fix:** the health-through-Caddy probe wrote to a directory that didn't exist yet, so `health/through-caddy.txt` is missing from this collection. That is fixed in the script.

Related: [Vultr runbook](../../deploy/vultr-uat/README.md), [VM guide](../../deploy/vultr-uat/VULTR-VM.md), [images and bundle plan](UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md), [Azure deployment handoff](DEPLOYMENT-UAT.md), [Keycloak UAT](KEYCLOAK-UAT.md), [backup/restore](BACKUP-RESTORE.md), [data keys](../architecture/DATA-KEYS.md). Collection script: [`scripts/uat/collect-server-state.sh`](../../scripts/uat/collect-server-state.sh).

## 1. Current architecture summary

### 1.1 What is deployed

The UAT stack runs on one Vultr cloud VM (D361, CONFIRMED) named `uat-01` (seen in #323), Debian, Docker, as the Compose project `swoovr-uat` from the unpacked bundle at `/opt/swoovr/swoovr-uat/`.

- **Release on the host:** `release.env` pins commit `5a7fa887d7b415818f74c691117084a575d315be` (the merge of PR #317). Its five Swoovr digests are identical to the committed [`swoovr-release/release-manifest.env`](../../swoovr-release/release-manifest.env). Its nine third-party pins are identical to [`deploy/vultr-uat/thirdparty.env`](../../deploy/vultr-uat/thirdparty.env).
- **Bundle provenance:** every bundle file in the local backup (`compose.yaml`, `Caddyfile`, `bin/*`, `zammad/*`, `postgres/*`, `README.md`, `VULTR-VM.md`) is byte-identical to `deploy/vultr-uat/` at that commit.
- **State observed in issues:** bootstrap has run and the API, Web and Admin are healthy (#321, #322). Member sign-in fails with Keycloak `Realm not enabled` (#323). That's the runbook's expected state before §5.3 D, because the realms are created disabled. Web surfaces it as an unhandled exception rather than a friendly page.

**Services** ([`compose.yaml`](../../deploy/vultr-uat/compose.yaml)):

| Service | Image | Networks | State kept in |
| --- | --- | --- | --- |
| `caddy` | Caddy 2.11.9 digest | edge (fixed 172.30.0.2, aliases for the 5 public names) | `caddy-data`, `caddy-config` (no ACME, so nothing important) |
| `web`, `admin` | `repasscloud/swoovr-web` / `-admin` digests | edge (fixed .10 / .11) | `web-dataprotection`, `admin-dataprotection` |
| `api` | `repasscloud/swoovr-api` digest | edge, data, ticketing, mail | none (PostgreSQL, R2) |
| `keycloak` | `repasscloud/swoovr-keycloak` digest (bakes UAT policy URLs) | edge, data | `keycloak` database |
| `swoovr-postgres` | `postgres:17.11-trixie` digest, TLS-only, SCRAM | data | `swoovr-pgdata` (databases `swoovr` and `keycloak`) |
| `clamav` | pinned | ticketing, egress | `clamav-db` (signatures) |
| `mailpit` | 1.31.0 | mail, mailpit-ui (127.0.0.1:8025) | `mailpit-data` |
| `zammad-*` (init, railsserver, scheduler, websocket, nginx) | Zammad 7.2.2 | zammad (+ edge for rails/scheduler/nginx, ticketing for nginx) | `zammad-storage`, `zammad-backup` |
| `zammad-postgresql`, `-redis`, `-memcached`, `-elasticsearch` | pinned | zammad | `zammad-pgdata`, `zammad-redis`, `zammad-elasticsearch` |
| `volume-permissions` | API image, one-shot chown | none | — |
| Jobs (profile `jobs`): `db-bootstrap`, `migrate`, `places-import`, `keycloak-bootstrap`, `api-command` | operator / API images | data, edge, mail | — |
| `zammad-backup` (profile `backup`, off) | Zammad | zammad | `zammad-backup` |

**Ingress and TLS:** Cloudflare (proxied, Full strict) → host 80/443 → Caddy. The Vultr firewall group, the host nftables table `inet swoovr_host` and the `DOCKER-USER`/`SWOOVR-EDGE` iptables chain all restrict 80/443 to Cloudflare. Caddy uses the owner-provisioned Cloudflare Origin CA certificate at `/etc/caddy/certs/uat-origin.{pem,key}`, mounted read-only, and never requests ACME. It trusts `CF-Connecting-IP` only from Cloudflare ranges (`env/caddy.env`), and `X-Forwarded-For` only from the Web/Admin BFFs. `/admin` and `/realms/master` on `auth-uat` answer only to the edge network. The host's `/etc/hosts` maps the five names to 172.30.0.2, which is what makes the SSH SOCKS tunnel to the Keycloak console work.

**Internal TLS:** a private PostgreSQL CA generated by `bin/tls-setup` (`tls/postgres/ca.key`, `tls/trust/pg-ca.crt`, `tls/postgres/server.{crt,key}` with SAN `swoovr-postgres`). Every database client uses `VerifyFull`/`verify-full`. `tls/trust/origin-trust.pem` = Debian system CAs + both Cloudflare Origin CA roots (fingerprint-pinned in `tls-setup`). It is `SSL_CERT_FILE` for the API, Web, Admin, `api-command`, `keycloak-bootstrap` and Zammad Rails/scheduler. Keycloak gets the roots through `KC_TRUSTSTORE_PATHS`.

### 1.2 Lifecycle operations as implemented (`bin/swoovr`)

| Operation | What the script really does | Notes |
| --- | --- | --- |
| Host preparation | `host-setup`: Docker CE + Compose from Docker's apt repository, `daemon.json` (json-file 20m×5, live-restore, no userland proxy), sysctls, nftables input table, two systemd units (`swoovr-host-firewall`, `swoovr-edge-firewall`), `/etc/hosts` line, unattended upgrades. `tls-setup`, `cloudflare-ips`. | Does **not** change SSH; key-only login is a manual step. |
| Initial deployment (`bootstrap`) | `check --bootstrap` → pull → start PostgreSQL → `db-bootstrap` → `migrate` → `places-import` → Caddy + Keycloak → `keycloak-bootstrap` (3 **disabled** realms + `swoovr-api-accounts`) → `zammad-sso` (staff realm `swoovr-zammad` PKCE client, through the master realm with the temporary admin) → ClamAV + Zammad → `zammad-provision` (writes 4 tokens into `env/api.env`) → API → Web, Admin. | Ends with the realms disabled and no root admin. |
| Manual gates (§5) | Verify realms; create a personal master-realm admin with OTP; delete `swoovr-bootstrap`; enable `swoovr-members` and `swoovr-staff` in the console; `beta add`; first Entra staff sign-in; `root-admin grant <staff subject>`; `founders allocate`; provider checks. | All by hand. |
| Upgrade (`upgrade <release.env>`) | Validate manifest → copy `release.env` to `release.env.previous` → `check` (restores on failure) → pull → stop api/web/admin → `migrate` → `up -d --remove-orphans` → wait healthy. | No database backup first. No `zammad-provision`, no realm changes, no `places-import`. Keycloak, Zammad and others are recreated only if their image changed. |
| Rollback | Image swap to `release.env.previous`. | Safe only if the old release understands the migrated schema. No data rollback. |
| Database migration | `migrate` job: API image, `dotnet Swoovr.Api.dll migrate` as `swoovr_migrator`. Hosted serving never migrates on start. | `geonames import` also migrates first. |
| Complete refresh (`reset --destroy-uat-data` then `bootstrap`) | `docker compose down --volumes` for the project (every named volume), keep `env/` and `tls/`, rewrite the 4 Zammad tokens to `__RUN_…__`. | Same generated passwords and **same data keys** are reused. Keycloak recreates `swoovr-bootstrap` from `KC_BOOTSTRAP_ADMIN_PASSWORD`; the §5 manual gates must be repeated. External state (R2 objects, Didit sessions, Cloudflare Worker queues) is untouched. |
| Mail change (`mail-apply`) | `check` → recreate Keycloak + API → re-run `zammad-provision` (idempotent, reuses tokens). | Restarts Keycloak (see §5.1 G of the runbook). |
| Backups | None for Swoovr/Keycloak on Vultr (operator backup transport is Azure-only). Zammad's local backup container exists but is off. | CONFIRMED (owner): UAT is disposable, rebuild rather than restore. |

### 1.3 First administrator and privileges

1. **Keycloak infrastructure admin:** Keycloak creates the temporary master-realm admin `swoovr-bootstrap` only on first start with an empty database, from `KC_BOOTSTRAP_ADMIN_PASSWORD`. The runbook replaces it with a personal admin with OTP, then deletes it. After that, `zammad-sso` and `keycloak-bootstrap` deliberately fail with 401.
2. **Realm enablement:** manual, in the Keycloak console (members, then staff; venues stays disabled because no venue portal runs).
3. **Swoovr root admin:** `api-command root-admin grant <subject>`. It requires a staff-realm account row, which exists only after that person has signed in once through Entra. The subject is the Keycloak user ID, so **it changes on every refresh**. Exactly one root admin is allowed. Other staff roles are granted in Admin, or with `staff-role grant` / `legal grant`.
4. **Staff sign-in** is Entra only (D281). The staff realm has no local password, reset or direct grant, `trustEmail=false`, and creates a user only if unique. Zammad staff SSO links by verified email through the `swoovr-zammad` client.

### 1.4 Image build and publication today

`.github/workflows/images.yml` (enabled for manual dispatch, D359) builds `api`, `web`, `admin`, `operator` from `containers/uat/Dockerfile` and `keycloak` from `containers/uat/Keycloak.Dockerfile`. It builds amd64 and arm64 natively, pushes them by digest to **private** Docker Hub `repasscloud/swoovr-*`, merges a multi-arch index tagged with the full commit SHA, writes `release-manifest.env/json`, packages the bundle with `deploy/vultr-uat/bin/make-bundle.sh`, and uploads it as an artifact (30 days). Deployment is manual: download the artifact, `scp` it, `tar`, `bin/swoovr`. An optional `promote-to-azure` job imports the same digests into ACR (not Keycloak).

### 1.5 Discrepancies found (documentation vs implementation)

1. **`deploy/vultr-uat/bin/` was deleted from `main`** in commit `fbe7a17` ("clean up for release", 12 November 2026). It removed `swoovr`, `envtool.py`, `host-setup`, `tls-setup`, `cloudflare-ips`, `origin-cert-check`, `make-bundle.sh` and `manifest-json.py`. The runbook, `VULTR-VM.md` and the plan still tell the operator to run them. **`images.yml`'s merge job still calls `deploy/vultr-uat/bin/manifest-json.py` and `make-bundle.sh`, so the next Images run on `main` will fail after pushing images.** The scripts exist on the server, in the local backup, and in Git history at `5a7fa88`.
2. **Origin certificate validity:** D356/#307 and the runbook say 15 years. The installed certificate expires **9 November 2028** (about 2 years). It is otherwise as specified: RSA 2048, 6 explicit SANs including the unused `venues-uat`, and it matches its key.
3. **Realm templates left on the host:** the backup contains `/opt/swoovr/swoovr-uat-realms/` (3 realm JSONs + manifest, mode 0664). The runbook says to copy them to `/root/swoovr-uat-realms` and delete them after `realms-env`. They hold the Entra tenant and client IDs, not secrets.
4. **Stale UAT docs:** [DEPLOYMENT-UAT.md](DEPLOYMENT-UAT.md), [KEYCLOAK-UAT.md](KEYCLOAK-UAT.md), [HOSTED-CONFIGURATION-AND-HEALTH.md](HOSTED-CONFIGURATION-AND-HEALTH.md) and [UAT-IMAGE-STAGING](UAT-IMAGE-STAGING-2026-11-10.md) still say UAT hosting is OPEN and the email provider is MailerSend. The code ([`HostingPolicy.cs`](../../src/Hosting/HostingPolicy.cs), [`ApplicationEmailSetup.cs`](../../src/Swoovr.Api/ApplicationEmailSetup.cs)) implements D360 (Twilio Email, smtp, Mailpit on UAT) and D361 (Vultr). The plan's decision table still lists MailerSend SMTP for Zammad (superseded by D360).
5. **AGENTS.md** says only `locked-closed-issues.yml` is enabled. `scripts/check_design.py` `ENABLED_WORKFLOWS` also enables `auto-request-review.yml` (8 Nov) and `images.yml` (D359). The script is authoritative for the check; AGENTS.md is out of date.
6. **Health probe paths:** `bin/swoovr status` probes `auth-uat` at `/realms/master`, which the edge network may reach but the public cannot (by design). The runbook's public `curl` loop accepts any non-52x code.
7. `scripts/check.sh` doesn't run `tests/uat/*`, so the realm generator and image fixtures are not part of routine checks.
8. `upgrade` stops the apps before `migrate` but takes no backup. The runbook calls rollback "image only", which is accurate, but nothing records which migration a release introduced.

## 2. Local backup inventory

Inspected 12 November 2026 by names, sizes, permissions, certificate metadata, key/certificate matching, placeholder state and cross-file equality. No value was printed. 64 files, 640 KB. Files are dated **11 November**, before bootstrap ran on 12 November (#322 logs). **The backup is a pre-bootstrap snapshot.**

| Path in backup | Present | Check result |
| --- | --- | --- |
| `etc/caddy/certs/uat-origin.pem` | yes | Cloudflare Origin CA leaf, RSA 2048, SANs `uat`, `api-uat`, `admin-uat`, `auth-uat`, `helpdesk-uat`, `venues-uat` `.swoovr.app`, no wildcard, expires 2028-11-09. Single certificate (no chain, which is correct for Origin CA). |
| `etc/caddy/certs/uat-origin.key` | yes, 0600 | Matches the certificate. **Private key.** |
| `opt/swoovr/swoovr-uat/tls/postgres/ca.key` | yes, 0600 | Matches `pg-ca.crt`. **Private CA key.** |
| `.../tls/trust/pg-ca.crt`, `pg-ca.srl` | yes | `CN=Swoovr UAT PostgreSQL CA`, expires 2036-11-08. |
| `.../tls/postgres/server.crt`, `server.key` | yes | `CN=swoovr-postgres`, SAN `swoovr-postgres`, verifies against the CA, expires 2029-02-13; key matches. **Private key.** |
| `.../tls/trust/cloudflare-origin-ca.pem` | yes | 2 roots (RSA and ECC). Public, downloadable. |
| `.../tls/trust/origin-trust.pem` | yes | System CAs + roots. Regenerable. |
| `.../env/*.env` (14 files) + `env/.generated` | yes, 0600 | All keys of every template present; no unquoted `$`. Unfilled: only the 4 `Ticketing__Zammad__*Token` values in `api.env` (filled on the server later by bootstrap). Empty by choice: MailerSend token and the Zammad IMAP values. Cross-file values agree: DB passwords, registration authority token, member-admin secret, web/staff client secrets, Zammad webhook and DB passwords. All 8 base64 data keys decode to 32 bytes; founder and phone keys are ≥32 bytes. Mail services: API `twilio`, Keycloak `twilio`, SMS `mailpit`, SMTP host SendGrid on 587. **All secret.** |
| `.../env/*.env.example` | yes | Same as Git at `5a7fa88`. |
| `.../release.env` | yes | Commit `5a7fa88`; 5 Swoovr + 9 third-party digests (all match Git). |
| `.../compose.yaml`, `Caddyfile`, `thirdparty.env`, `bin/*`, `zammad/*`, `postgres/*`, `README.md`, `VULTR-VM.md`, `.gitignore` | yes | Byte-identical to Git at `5a7fa88`. |
| `opt/swoovr/swoovr-uat-realms/*.json` | yes, 0664 | 3 disabled realm templates + manifest (tenant/client IDs; no secrets). |
| `etc/.DS_Store`, `etc/caddy/.DS_Store` | yes | macOS artefacts; delete. |

**Not in the backup** (must be collected, §3): the post-bootstrap `env/api.env` (the 4 Zammad tokens), `release.env.previous` (if any), `env/.zammad-provision-error.log` (if any), everything under `/etc/swoovr/`, systemd units, `/etc/docker/daemon.json`, `/etc/hosts`, `/etc/sysctl.d/90-swoovr.conf`, nftables/iptables state, SSH configuration and authorized keys, Docker engine/Compose versions, running containers, image IDs, networks, volumes and their sizes, any data from the Docker volumes, Keycloak's current realm state (enabled flags, admin users, the Entra-created staff users), the Zammad state, and whether a Tailscale or Docker Hub login exists.

## 3. Missing server information

The collection script (§8) gathers all of this read-only:

| Area | Collected | Why it matters for the redesign |
| --- | --- | --- |
| OS | `/etc/os-release`, Debian version, kernel, architecture, CPU, RAM, disks, `df`, sysctls, relevant packages, apt sources, unattended-upgrades, time sync | Base image for automation; amd64 vs arm64 (the target design is amd64 only); sizing. |
| Networking | interfaces, routes, listening sockets, `/etc/hosts`, `resolv.conf` | Public interface name for the edge filter; the `/etc/hosts` alias trick. |
| Firewall | `nft list ruleset`, `iptables-save`, `ip6tables-save`, `/etc/swoovr/{host.nft,cloudflare-ranges,edge-interface}` | Must be reproduced exactly, and must keep SSH reachable for GitHub runners (§6). |
| SSH | `sshd -T` (effective), drop-ins, host-key fingerprints, authorized-key fingerprints (sensitive file) | Root SSH deployment needs `PermitRootLogin prohibit-password`, and a dedicated deploy key; host-key fingerprints feed `known_hosts` in Actions. |
| Tailscale | version and status, if installed | Possible private path for Actions instead of public SSH. |
| systemd | failed/running/enabled units; status and unit files for the swoovr firewall units, Docker, SSH | Boot ordering of the firewall vs Docker. |
| Docker | engine/Compose versions, `info`, `daemon.json`, `system df`, Compose projects, registry logins (names only) | Version floor for automation; private Docker Hub login to replace with GHCR. |
| Containers | `ps -a`, redacted `inspect` (env **names** only), one-line summary (service, image, image ID, restart policy, health, mounts, networks/IPs) | Confirms the running digests equal `release.env` and the volumes in use. |
| Images | `images --digests`, repo digests and architecture per image ID | Same. |
| Networks/volumes | `network inspect`, `volume inspect`, per-volume size, Zammad backup volume listing | Persistent state sizing; whether any backups exist on the host. |
| Bundle | file tree with modes and owners; non-secret files copied; SHA-256 of all non-secret files; env variable names and their filled/empty/unfilled state | Detect drift from Git (e.g. hand edits) and the post-bootstrap token state. |
| Certificates | Subject, issuer, validity, SAN and SHA-256 fingerprint of the origin, PostgreSQL CA/server and Cloudflare roots; key/certificate match; file modes and owners | Validity and permissions (PostgreSQL needs UID 999 and 0600). |
| Health | `bin/swoovr status`-style probes through Caddy, API readiness, Caddy version | Baseline before change. |
| Databases (read-only transaction) | PostgreSQL version, databases/owners/sizes, roles and attributes, applied EF migrations, non-empty table row counts, accounts by realm/status and root-admin count; Keycloak realms (enabled, SSL, users, clients), Swoovr clients and redirect URIs, identity providers, Keycloak schema version; Zammad database sizes, ticket/user/group/channel/token counts, groups, channels, key settings (credentials masked); Elasticsearch health and indices; Zammad version | Shows what an upgrade must preserve and what a refresh destroys, and whether the realms were enabled and root granted. |
| Sensitive (separate archive) | current `env/*.env` + `.generated`; SHA-256 of every secret file; Keycloak master-realm and staff usernames/subjects; Zammad agent logins; authorized-key fingerprints; realm templates on the host; optionally TLS keys, origin key and logs | Restorable configuration; compare with the local backup without copying keys again. |

## 4. Required certificates and secrets

Source column: **G** = generated once on the host by `envtool.py init` (stored in `env/.generated`), **O** = owner-supplied, **R** = produced at runtime by a command, **T** = produced by `tls-setup`.

| Item | Where it lives | Source | Upgrade | Refresh | Notes |
| --- | --- | --- | --- | --- | --- |
| Cloudflare Origin certificate + key | `/etc/caddy/certs/uat-origin.{pem,key}` | O (Cloudflare dashboard) | keep | keep | Expires 2028-11-09. Candidate GitHub secret (PEM). UAT-only. |
| PostgreSQL CA key + cert, server key + cert | `tls/postgres/`, `tls/trust/pg-ca.crt` | T | keep | may regenerate | Clients trust only this CA. Regenerating with the data kept is harmless if all clients get the new CA. |
| Cloudflare Origin CA roots, `origin-trust.pem` | `tls/trust/` | T (download + pinned fingerprints) | regenerate | regenerate | Public. Pins expire 2029-08-15. |
| Cloudflare edge ranges | `env/caddy.env`, `/etc/swoovr/cloudflare-ranges` | T (`cloudflare-ips`) | refresh monthly | regenerate | Public. 22 ranges in the backup. |
| PostgreSQL superuser (`POSTGRES_*`, `SWOOVR_PG_ADMIN`, `PGUSER/PGPASSWORD`) | `postgres.env`, `bootstrap.env` | G | **keep** (stored in the data directory) | new allowed | Changing it on upgrade breaks `db-bootstrap` unless the role is altered. |
| Role passwords `swoovr_api`, `_migrator`, `_operator`, `_backup`, `swoovr_keycloak` | `bootstrap.env`, connection strings in `api/migrate/operator.env`, `keycloak.env` | G | keep (or rotate with `db-bootstrap`, which re-applies passwords) | new allowed | Must stay equal across files (they are now). |
| `KC_BOOTSTRAP_ADMIN_PASSWORD` | `keycloak.env` | G | keep (ignored once the DB exists) | used | Temporary master admin; deleted after §5.1. |
| Registration authority token | `api.env` + `keycloak.env` | G | keep together | new allowed | Keycloak → API admission. |
| Member-admin client secret | `api.env`, `realms.env` | G | keep (lives in the Keycloak DB too) | new allowed | `swoovr-api-accounts` service client. |
| Members/venues/staff web client secrets | `web.env`, `admin.env`, `realms.env` | G | keep (in the Keycloak DB) | new allowed | Import-time only. Changing the file alone does nothing on upgrade. |
| Entra client secret + tenant/client IDs | `realms.env` (secret), realm templates (IDs) | O | keep (in the Keycloak DB) | reused | Expiry ≤ 24 months; staff sign-in stops when it expires. |
| Realm templates `SWOOVR_UAT_REALM_*` | `realms.env` | R (`keycloak_realms.py` from the bundle commit) | n/a | regenerate from the deployed commit | Disabled, userless, placeholders only. |
| Data keys: founder, phone code, bans HMAC, contact history + lookup, evidence, identity store + HMAC, push tokens + lookup | `api.env` | G | **keep, byte-identical** | may regenerate (data is destroyed too) | Losing them while keeping the DB makes encrypted records unreadable. Rotation keeps old versions ([DATA-KEYS.md](../architecture/DATA-KEYS.md)). |
| Zammad tokens: identity, staff directory, retention, Home City check | `api.env` | R (`zammad-provision`, created inside Zammad) | keep (live in the Zammad DB) | recreated | Not in the local backup. Must be re-captured after every refresh. |
| Zammad webhook secret | `api.env`, `zammad-provision.env` | G | keep together | new allowed | |
| Zammad bootstrap password, DB passwords | `zammad-provision.env`, `zammad.env`, `zammad-postgres.env` | G | keep | new allowed | DB superuser is stored in `zammad-pgdata`. |
| Didit API key, workflow ID, webhook secret | `api.env` | O | keep | keep | The webhook URL and secret are also set in the Didit console. |
| Cloudflare R2 endpoint, bucket, access key, secret | `api.env` | O | keep | keep | Objects persist outside the VM (§5). |
| Screening base URL + key; media pipeline service token + upload signing key | `api.env` | O | keep | keep | Must equal the Cloudflare Worker secrets (`workers/images`). |
| Moderation escalation mailboxes | `api.env` | O | keep | keep | Personal addresses: sensitive. |
| Twilio SMS SID/token/From | `api.env` | O | keep | keep | Filled although SMS is `mailpit`. |
| Twilio Email key SID/secret, SendGrid key, sender, IMAP | `mail.env` | O | keep | keep | IMAP empty today. |
| Web/Admin Data Protection key rings | `web-dataprotection`, `admin-dataprotection` volumes | R | keep (or users sign in again) | discard | Not secrets in env; ASP.NET keys in volumes. |
| Keycloak realm signing keys | `keycloak` database | R | keep (or every token and session is invalid) | discard | |
| Docker Hub read-only token | `/root/.docker/config.json` | O | — | — | Not needed once images are public on GHCR. |
| SSH host keys | `/etc/ssh/ssh_host_*` | system | keep | keep | Pin their fingerprints in Actions `known_hosts`. |

## 5. Persistent data inventory

Named volumes are `swoovr-uat_<name>`. The local backup contains **no volume data**.

| Volume / location | Holds | Upgrade | Refresh | Regenerable? |
| --- | --- | --- | --- | --- |
| `swoovr-pgdata` | `swoovr` DB (accounts, profiles, messages, cases, audit, beta list, places), `keycloak` DB (realms, clients, users, credentials, signing keys, sessions, the personal Keycloak admin), roles and superuser | **Preserve** | **Discard** | Rebuilt by bootstrap (places from GeoNames). Realm enablement, the personal Keycloak admin, root grant and beta list are lost and must be redone. |
| `zammad-pgdata` | Zammad DB: tickets, articles, attachments (storage is in the DB), users, groups, tokens, channels, settings | **Preserve** | **Discard** | `zammad-provision` recreates the configuration and new tokens. |
| `zammad-storage` | `/opt/zammad/storage` (small with DB storage) | Preserve | Discard | — |
| `zammad-elasticsearch` | Search index | Preserve (saves a rebuild) | Discard | Yes: `rails searchindex:rebuild`. |
| `zammad-redis` | Sessions, jobs | Preserve | Discard | Yes (transient). |
| `zammad-backup` | Zammad's own backups (profile off; probably empty) | Preserve if not empty | Discard | — |
| `web-dataprotection`, `admin-dataprotection` | ASP.NET Data Protection keys | Preserve (avoids sign-outs) | Discard | Yes; losing them signs everyone out. |
| `clamav-db` | Virus signatures | Optional | Discard | Yes (download). |
| `mailpit-data` | Captured SMS codes and sink mail | Optional | **Discard deliberately** | — (contains phone numbers and codes) |
| `caddy-data`, `caddy-config` | Autosave config; no ACME certificates | Optional | Discard | Yes. |
| Host `env/`, `tls/`, `release.env*` | Configuration and keys | **Preserve** | Keep certificates and provider secrets; may regenerate generated passwords and data keys (only together with the data) | Partly (§4). |
| Host `/etc/caddy/certs` | Origin certificate | **Preserve** | **Preserve** | No (reissue in Cloudflare). |
| Host `/etc/swoovr`, systemd units, `/etc/hosts`, `daemon.json`, sysctl | Host configuration | Preserve | Preserve | Yes, by `host-setup`. |
| Off-host: R2 bucket objects (member photos), Cloudflare image Worker queues/state, Didit sessions, Twilio/SendGrid logs | Provider-side data | Untouched | **Untouched today**: a refresh leaves orphan photos in R2 | OPEN (§9 Q-D). |

The BFF sessions and the SignalR state are in memory: every restart signs members out (single replica, documented).

## 6. GitHub Actions prerequisites

Target (owner brief, 12 November 2026): build linux/amd64 images, publish **public** images to GHCR, deploy over SSH as root using Actions secrets, deploy only version tags reachable from `main`, support **upgrade** and **refresh**, provision the full stack, preserve data on upgrade, and reinitialise it on refresh.

**Owner decisions needed first (OPEN, §9):** GHCR public replaces D356's private Docker Hub DEFAULT; amd64-only replaces the CONFIRMED multi-arch request; a new deploy workflow must be enabled by name in `ENABLED_WORKFLOWS`; and version tags need a tag scheme (D320/`scripts/release_version.py` already define `X.Y.Z`).

**Repository:**

1. Restore or replace the deleted `deploy/vultr-uat/bin/` logic (`make-bundle.sh`, `manifest-json.py` and `swoovr`), or change `images.yml`. Today the workflow cannot finish on `main`.
2. Tag rule: a workflow on `push: tags: ['v*.*.*']` that refuses unless `git merge-base --is-ancestor $GITHUB_SHA origin/main`, and refuses tags whose version differs from the committed product version (`scripts/check_version.py`). Use a protected tag ruleset so only maintainers can create `v*` tags.
3. GHCR: `ghcr.io/repasscloud/swoovr-{api,web,admin,operator,keycloak}`, `permissions: packages: write` with `GITHUB_TOKEN`, package visibility public (a package setting, not a workflow setting; first push creates it private). Deploy by digest; keep the tag for humans. Labels: `org.opencontainers.image.source` links the package to the repository.
4. Public images must stay secret-free. The Dockerfiles bake none (verified: no build arguments other than `SWOOVR_VERSION`). The Keycloak image bakes the UAT policy URLs, so production still needs its own build (existing follow-up).
5. Third-party images stay pinned by digest (`thirdparty.env`); decide whether to mirror them to GHCR (OPEN, optional).
6. Deploy job: an `environment: uat` with required reviewers, concurrency group `uat-deploy` (no overlap), `workflow_dispatch` input `mode: upgrade|refresh`, plus a typed confirmation for refresh.
7. Server-side entry point: a versioned, idempotent script in the bundle (successor to `bin/swoovr`) that the job calls over SSH. Upgrade = pull, (optional backup), migrate once, recreate. Refresh = down with volumes, regenerate data keys and passwords (or reuse; OPEN), bootstrap, re-provision Zammad, re-capture its tokens.
8. Realm automation: today realm enablement and admin replacement are manual and the temporary master admin is deleted. Automating upgrade/refresh needs a decision on a persistent, least-privilege Keycloak automation credential (e.g. a master-realm service-account client limited to the three realms), or keeping these steps manual (OPEN).
9. Root admin after refresh: the staff subject changes on each refresh, and the grant needs a prior Entra sign-in. Options: keep it manual; or a CLI that grants root to the account matching a configured Entra object ID claim on first sign-in (needs design and owner approval; never by email match, per AUTHENTICATION.md).

**GitHub secrets / variables (names DEFAULT):**

| Name | Kind | Purpose |
| --- | --- | --- |
| `UAT_SSH_HOST` | variable | VM IPv4 or Tailscale name |
| `UAT_SSH_KNOWN_HOSTS` | variable | Host-key line(s) from the collection (fingerprints in `ssh/host-key-fingerprints.txt`) |
| `UAT_SSH_PRIVATE_KEY` | secret | Dedicated ed25519 deploy key; its public key restricted in `authorized_keys` (`restrict`, optional `from=`) |
| `UAT_ORIGIN_CERT_PEM`, `UAT_ORIGIN_KEY_PEM` | secret | Only if the workflow installs the certificate; otherwise leave it on the host |
| `UAT_ENV_*` (one per env file, or one per value) | secret | Owner-supplied provider values (Didit, R2, screening, media pipeline, escalation, Twilio SMS/Email, SendGrid, IMAP, Entra secret) |
| `UAT_DATA_KEYS` (or individual) | secret | Only if keys move off the host. Recommended DEFAULT: keep generated passwords and data keys on the host (`env/.generated`) and never send them through Actions, so an upgrade cannot change them by accident |
| `UAT_ENTRA_TENANT_ID`, `UAT_ENTRA_CLIENT_ID` | variable | Realm generation (not secret) |
| `GITHUB_TOKEN` | built in | GHCR push |

**Network access for the runner:** the Vultr firewall group allows SSH only from the admin IP, and `host-setup` may restrict SSH with `SWOOVR_SSH_CIDRS`. GitHub-hosted runners use many changing IP ranges. Options (OPEN): Tailscale on the VM plus the Tailscale GitHub Action (no public SSH); a self-hosted runner; or opening SSH to GitHub's published Actions ranges (large and changing; least preferred).

**Root SSH:** the brief says root. `sshd` must allow `PermitRootLogin prohibit-password` with key-only authentication. Today's state is unknown until collected. A dedicated non-root deploy user with a narrow `sudo` rule is the safer alternative (OPEN).

## 7. Server collection checklist

| # | Item | Status |
| --- | --- | --- |
| 1 | Origin certificate + key | **Available locally** (valid; sensitive). Collect hashes only, to confirm unchanged. |
| 2 | PostgreSQL CA + server key/cert | **Available locally** (valid; sensitive). Hashes only. |
| 3 | Cloudflare roots, `origin-trust.pem`, `caddy.env` | Available locally; **regenerable**. |
| 4 | `env/*.env`, `.generated` | Available locally but **pre-bootstrap**. **Collect current `env/api.env`** (Zammad tokens) and any other edited file. |
| 5 | `release.env` | Available locally. Collect `release.env.previous`/`.failed` if present. |
| 6 | Bundle files | Available locally, identical to Git `5a7fa88`; collect hashes to detect hand edits. |
| 7 | Realm templates | Available locally (`/opt/swoovr/swoovr-uat-realms`); regenerable; sensitive-ish (tenant ID). |
| 8 | OS, kernel, architecture, packages, sysctl | **Missing: collect.** |
| 9 | Docker engine/Compose versions, `daemon.json`, registry logins (names) | **Missing: collect.** |
| 10 | nftables, iptables `DOCKER-USER`/`SWOOVR-EDGE`, `/etc/swoovr/*`, systemd units | **Missing: collect.** |
| 11 | SSH effective config, host-key fingerprints, authorized-key fingerprints | **Missing: collect** (fingerprints are sensitive-ish). |
| 12 | Tailscale | **Missing: collect** (installed or not). |
| 13 | Containers, images, digests, health, restart policies, mounts, networks, volumes, sizes | **Missing: collect.** |
| 14 | Keycloak state: realms enabled, master admins, staff users/subjects, clients, IdPs | **Missing: collect** (read-only SQL). |
| 15 | Swoovr DB: migrations applied, row counts, root admin present | **Missing: collect** (read-only SQL). |
| 16 | Zammad: version, counts, channels, settings, Elasticsearch health | **Missing: collect** (read-only). |
| 17 | Database dumps / volume archives | **Optional, separate approval** (§8.4). UAT is disposable (CONFIRMED), so not required unless the owner wants the current test accounts or tickets kept. |
| 18 | Container logs | Optional (`--with-logs`); sensitive. |
| 19 | `etc/.DS_Store` files in the backup | **Unnecessary**: delete locally. |
| 20 | Mailpit data, ClamAV signatures, Caddy data, Redis | **Unnecessary / regenerable.** |

## 8. Collection script and execution instructions

### 8.1 What the script does

[`scripts/uat/collect-server-state.sh`](../../scripts/uat/collect-server-state.sh) runs on Debian as root and is read-only:

- Only read commands (`cat`, `ls`, `ip`, `ss`, `nft list`, `iptables-save`, `sshd -T`, `systemctl status/cat`, `docker ps/inspect/images/network/volume/system df/version`, `openssl x509/pkey`, `sha256sum`, `du`).
- `docker exec` only for: `wget` health probes from Caddy (same as `bin/swoovr status`); `psql` inside the two PostgreSQL containers, with `default_transaction_read_only=on`, local socket, names and counts only; and `curl` to Elasticsearch's read endpoints. It never runs Compose `up/down/restart`, jobs, migrations, `bin/swoovr`, or `docker compose config` (which would expand secrets).
- `docker inspect` output has every environment **value** removed (names kept).
- Public archive: no secret values. Sensitive archive: current env files, secret-file hashes, admin usernames and subjects, key fingerprints. Optional flags copy TLS keys, the origin key and logs.
- Everything is created with `umask 077` in a private temporary directory under `/root`, archived as `0600` files, with an `INVENTORY.txt` in each archive and a `SHA256SUMS` file. Failed probes are listed in `errors.txt`; the script never aborts half-way.

It was checked with `bash -n` and `shellcheck -S warning` (clean), and smoke-run in a `debian:trixie` container without Docker. It produced both 0600 archives, recorded the unavailable probes in `errors.txt` and showed env names and placeholder state only. It has **not** been run against the UAT server.

### 8.2 Run it

On your Mac, from this repository:

```sh
scp scripts/uat/collect-server-state.sh root@<VM IPv4>:/root/
```

On the VM, as root:

```sh
ssh root@<VM IPv4>                       # or: ssh <user>@<VM IPv4>, then sudo -i
bash /root/collect-server-state.sh        # default: no TLS/origin key copies, no logs
# Optional extras (all go into the sensitive archive only):
#   --with-logs           last 300 log lines per container
#   --include-tls         copy tls/ (only if the hashes differ from the local backup)
#   --include-origin-key  copy /etc/caddy/certs (only if the hashes differ)
#   --no-du               skip volume sizes if du is slow
ls -l /root/swoovr-uat-collect-*
```

### 8.3 Copy it back and protect it

On your Mac:

```sh
DEST=~/swoovr-uat-backups/collect-$(date +%Y%m%d-%H%M)
mkdir -m 700 "$DEST"
scp -p 'root@<VM IPv4>:/root/swoovr-uat-collect-*' "$DEST"/
cd "$DEST" && shasum -a 256 -c swoovr-uat-collect-*.SHA256SUMS
chmod 600 "$DEST"/*
mkdir -m 700 public sensitive
tar -xzf swoovr-uat-collect-*-public.tar.gz -C public
tar -xzf swoovr-uat-collect-*-sensitive.tar.gz -C sensitive
```

Compare the server's secret files with the local backup without opening them (prints only `OK`/`FAILED` per path):

```sh
B=~/swoovr-uat-backups/swoovr-uat-backup-20261112-0024
sed "s#  /#  $B/#" sensitive/*/secret-files-sha256.txt | shasum -a 256 -c - 2>/dev/null
```

`FAILED` for `env/api.env` is expected (tokens filled at bootstrap); any other `FAILED` means the server changed after the backup. Then delete the copies on the VM: `rm /root/swoovr-uat-collect-*` (and `/root/collect-server-state.sh`). Keep the sensitive archive only in `~/swoovr-uat-backups` (outside every Git checkout; `.local/` would also be ignored). Never attach it to an issue or PR.

### 8.4 Stateful exports (separate, explicitly approved operation; not in the script)

Only if the owner wants the current UAT data kept across a redesign. Dumps contain member personal data and credentials hashes; treat them like the sensitive archive. These are read-only for the services (online `pg_dump`, no locks beyond normal reads):

```sh
cd /opt/swoovr/swoovr-uat
umask 077; D=/root/swoovr-uat-dumps-$(date -u +%Y%m%dT%H%M%SZ); mkdir -m 700 "$D"
PG=$(docker ps -q --filter label=com.docker.compose.project=swoovr-uat --filter label=com.docker.compose.service=swoovr-postgres)
ZP=$(docker ps -q --filter label=com.docker.compose.project=swoovr-uat --filter label=com.docker.compose.service=zammad-postgresql)
docker exec "$PG" sh -c 'pg_dump -U "$POSTGRES_USER" -d swoovr -Fc'   > "$D/swoovr.dump"
docker exec "$PG" sh -c 'pg_dump -U "$POSTGRES_USER" -d keycloak -Fc' > "$D/keycloak.dump"
docker exec "$ZP" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$ZAMMAD_DB" -Fc' > "$D/zammad.dump"
sha256sum "$D"/*.dump > "$D/SHA256SUMS"; ls -l "$D"
```

A restore of these dumps is **not** a supported procedure on Vultr today (the D261 fenced restore is Azure-oriented, and a Swoovr restore needs the same data keys). Zammad attachments are inside its database, so `zammad.dump` covers them; Elasticsearch is rebuilt.

## 9. Risks and unresolved questions

**Risks**

1. The Images workflow on `main` is broken until `deploy/vultr-uat/bin/` is restored or replaced (§1.5.1). The runbook on `main` also references missing scripts.
2. The local backup is pre-bootstrap. Without collecting the current `env/api.env`, a rebuild from the backup loses the Zammad tokens (recoverable by re-provisioning, but the token values change).
3. Data keys and the database must move together. A deployment that regenerates `api.env` on upgrade, or injects different keys from Actions, makes encrypted data unreadable, and the API refuses to start or decrypt.
4. Opening SSH to GitHub-hosted runners widens the attack surface; root deployment keys are full-host credentials.
5. Refresh leaves R2 photos and provider-side records orphaned, and Didit/Cloudflare Worker callbacks may target deleted rows.
6. Manual Keycloak steps (admin replacement, realm enablement, root grant) block a fully automated refresh, and automating them needs a long-lived privileged Keycloak credential.
7. The Keycloak image is environment-specific (UAT URLs); publishing it publicly is fine, but promoting it to production is not.
8. Single replica: every deploy signs members out (in-memory BFF sessions).
9. The origin certificate expires 2028-11-09, sooner than the documented 15 years.
10. Elasticsearch (2 GB heap) and ClamAV memory: resource use on the VM has not been measured yet.

**Questions for the owner** (to be added to [QUESTIONS.md](QUESTIONS.md) as Q179+ and opened as GitHub issues, linked both ways, once you approve; not created by this task):

- **Q-A** GHCR public images instead of private Docker Hub (changes D356 DEFAULT/D359), and amd64-only instead of multi-arch (CONFIRMED earlier request). Confirm both.
  - Public images, amd64-only on GHCR (leave any yaml files that create them on dockerhub alone)
- **Q-B** How does the runner reach SSH: Tailscale (recommended DEFAULT), a self-hosted runner, or GitHub IP ranges? Is Tailscale already installed?
  - The runner will reach SSH by being provided the SSH private key as a variable in the gh-actions as a base64 string that can be written to a file each time it needs to deploy, tailscale is out of scope for deployment
- **Q-C** Root over SSH (as briefed) or a dedicated deploy user with sudo?
  - Root over SSH (for UAT)
- **Q-D** On refresh, should the R2 bucket (UAT photos) be emptied, and should provider-side state (Didit sessions) be ignored?
  - Emptied on refresh, provider-side state can be ignored
- **Q-E** On refresh, regenerate database passwords and data keys (clean slate), or reuse `env/.generated`? DEFAULT recommendation: regenerate on refresh, never on upgrade.
  - Regenerate on refresh
- **Q-F** Keycloak automation: keep admin replacement, realm enablement and the root grant manual after each refresh, or approve a least-privilege automation client and a root-grant design?
  - all realms should be enabled by default, or provide a script that is run from the server as post-refresh instructions to: a) verify, b) enable all the services and remove any test accounts once the admin account setup by me is setup
- **Q-G** Where do owner-supplied provider values live: GitHub `uat` environment secrets (pushed on deploy) or only on the host (current)? DEFAULT recommendation: host-only for generated values; Actions secrets for owner-supplied values if you want the server to be rebuildable from GitHub alone.
  - All settings will be put into the github runner as environment secrets, they can either be base64 string of a file (eg - an SSH key, SSH keys, etc) or a string value provided as a variable
- **Q-H** Backups before upgrade: take an automatic `pg_dump` before each `migrate` (kept on the host for N days), or continue with no backups (CONFIRMED disposable)?
  - pg_dump for each upgrade, this way it can be restored manually if an upgrade fails, kept on host for 7 days (use a crontab job to delete the dump folder by checking each day for contents and delete anything over 7 days old)
- **Q-I** Version tags: confirm the `vX.Y.Z` scheme tied to the committed product version (D320), and who may create tags.
  - version tags will be commited product version, they are updated manually in code first by human, the tag should check the vX.Y.Z code in the source to ensure it matches else don't allow it to proceed
- **Q-J** Should `deploy/vultr-uat/bin/` be restored to `main` (it was deleted in `fbe7a17`) as the base for the new automation?
  - I leave that to your decision, if it's a good idea then restore it to main, you are running the show

## 10. Readiness checklist for the next implementation phase

- [ ] Owner runs the collection script and copies both archives to `~/swoovr-uat-backups/` (§8). << copied to here: `/Users/danijeljw/swoovr-uat-backups/collect-20261112-2225`
- [ ] Hash comparison shows only `env/api.env` changed since the backup; any other change explained.
- [ ] Current `env/api.env` (with Zammad tokens) is stored in the protected backup.
- [ ] Keycloak state recorded: which realms are enabled, master admin replaced, staff subject, root granted or not.
- [ ] OS/Docker versions, public interface, firewall rules and SSH settings recorded (from the public archive).
- [ ] Decision on stateful exports (§8.4): not needed (disposable) or taken and stored.
- [ ] Owner answers Q-A to Q-J (they become Q179+ with GitHub issues).
- [ ] Decision on `deploy/vultr-uat/bin/` (restore vs replace), and the Images workflow fixed accordingly.
- [ ] GHCR packages decided (names, public visibility), and the `uat` GitHub environment created with reviewers and secrets.
- [ ] Runner network path in place (Tailscale or alternative) and a dedicated deploy key installed, with its host key pinned.
- [ ] New workflows added to `ENABLED_WORKFLOWS` only after the owner enables them by name; `AGENTS.md` updated to list the enabled workflows.
- [ ] Stale UAT docs (§1.5.4) scheduled for correction with the implementation PR.
- [ ] `.DS_Store` files removed from the local backup; local `realms` templates removed from the VM (or kept knowingly).
