# Swoovr UAT on one Vultr bare-metal host

This is the runbook for the deployment bundle. The bundle runs the complete, disposable UAT stack on one Debian host with Docker. Plan, decisions and open gates: [images and bundle plan](../../docs/delivery/UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md). Background: [Vultr stack review](../../docs/delivery/VULTR-STACK-REVIEW-2026-11-10.md).

**Status: prepared, not deployed.** Two parts were validated locally on 10 November 2026: the TLS-only PostgreSQL container with the real `database-bootstrap` job, and the Caddyfile. Nothing has run on the Vultr host yet. No real provider, DNS record or hosted sign-in has been accepted.

## What runs on the host

| Service | Image (from `release.env`) | Public? |
| --- | --- | --- |
| `caddy` | Caddy 2.11.9 (owner-selected digest) | Yes: 80/443, Cloudflare ranges only |
| `web`, `api`, `admin` | `repasscloud/swoovr-{web,api,admin}@sha256:…` | Through Caddy only |
| `keycloak` | `repasscloud/swoovr-keycloak@sha256:…` | Through Caddy. `/admin` and the master realm are only reachable internally |
| `swoovr-postgres` | `postgres:17.11-trixie` (Debian) | No. TLS-only; holds the `swoovr` and `keycloak` databases |
| `clamav` | Pinned ClamAV | No |
| `zammad-*` (nginx, rails, scheduler, websocket, init) | Zammad 7.2.2 | nginx through Caddy (`helpdesk-uat`) |
| `zammad-postgresql`, `-redis`, `-memcached`, `-elasticsearch` | Pinned upstream | No |
| Jobs: `db-bootstrap`, `migrate`, `places-import`, `keycloak-bootstrap`, `api-command` | operator / api images | No; one-shot only |

Directory layout on the host is `/opt/swoovr/swoovr-uat/`, the unpacked bundle:

```text
compose.yaml  Caddyfile  release.env  thirdparty.env
bin/   swoovr (operator CLI), host-setup, tls-setup, cloudflare-ips, envtool.py
env/   *.env.example (in Git)  ->  *.env (host only, 0600, generated + owner-filled)
tls/   postgres/ (private CA + server cert)  trust/ (pg-ca.crt, Cloudflare Origin CA roots, origin-trust.pem)

/etc/caddy/certs/  uat-origin.pem  uat-origin.key   (owner-provisioned, outside the bundle and Git; read-only into Caddy)
postgres/  pg_hba.conf  initdb/01-databases.sql
zammad/    provision.rb  10-create-zammad-role.sh
```

## 0. Before you start (owner)

1. **Images:** run the `Images` workflow for the reviewed commit (see the plan, step 2). Download the `swoovr-release-<commit>` artifact. It contains `release-manifest.env` and `swoovr-uat-bundle-<commit>.tar.gz`. Without Actions minutes, build the images locally instead (plan, "Building without Actions").
2. **Cloudflare (swoovr.app zone):**
   - Proxied (orange-cloud) `A` records to the host's public IPv4 for `uat`, `api-uat`, `admin-uat`, `auth-uat` and `helpdesk-uat`. Add `AAAA` records too if IPv6 is used.
   - SSL/TLS mode: **Full (strict)**.
   - SSL/TLS → Origin Server → **Create certificate** (owner issue #307, Q175): PEM, RSA 2048, 15 years, explicit SANs only (no wildcard): `uat.swoovr.app`, `api-uat.swoovr.app`, `admin-uat.swoovr.app`, `helpdesk-uat.swoovr.app`, `auth-uat.swoovr.app`, `venues-uat.swoovr.app`. This certificate is for UAT only; production gets its own. Keep the certificate and key private for step 2.
   - Cache rule: **bypass cache** for all five hostnames, because their content is personalised.
3. **Provider values** for `env/*.env`: MailerSend, Twilio, Didit, R2, screening endpoint and key, media pipeline token and signing key, escalation mailboxes, Entra client secret, and the Entra tenant and client IDs for realm generation. These are the same values the Azure Key Vault inputs use.

## 1. Prepare the host (once)

```sh
# As root on the new server.
cat /proc/mdstat                       # confirm the RAID 1 array is active and in sync ([UU])
mkdir -p /opt/swoovr && cd /opt/swoovr
# Copy the bundle from your machine:  scp swoovr-uat-bundle-<commit>.tar.gz root@HOST:/opt/swoovr/
tar -xzf swoovr-uat-bundle-<commit>.tar.gz && cd swoovr-uat
SWOOVR_SSH_CIDRS="<your.admin.ip>/32" ./bin/host-setup
```

`host-setup` does the following:

- Installs Docker Engine and the Compose plugin from Docker's Debian repository.
- Sets log rotation and `live-restore`.
- Sets `vm.max_map_count` for Elasticsearch and `vm.overcommit_memory` for Redis.
- Installs a host input firewall in its own nftables table. It never touches Docker's rules.
- Enables unattended security updates.
- Points the five hostnames at Caddy's private address in `/etc/hosts`.

Afterwards, harden SSH yourself: use key-only authentication with `PasswordAuthentication no`, and check that you can still log in before closing the session.

## 2. TLS and the Cloudflare edge filter

```sh
# The owner provisions the certificate outside Git and outside this bundle (never echo it in a shell):
install -m 700 -d /etc/caddy/certs
nano /etc/caddy/certs/uat-origin.pem; nano /etc/caddy/certs/uat-origin.key
chmod 644 /etc/caddy/certs/uat-origin.pem; chmod 600 /etc/caddy/certs/uat-origin.key
./bin/origin-cert-check  # both files exist, PEM, RSA 2048, six explicit SANs, no wildcard, key matches
./bin/tls-setup          # private PostgreSQL CA, Cloudflare Origin CA roots, origin-trust.pem; reruns the check
./bin/cloudflare-ips     # Caddy trusted proxies + DOCKER-USER: 80/443 from the internet accept Cloudflare ranges only
systemctl start swoovr-edge-firewall
```

Caddy reads the files read-only at `/etc/caddy/certs/` (the same path inside the container). `bin/swoovr` runs `origin-cert-check` before every command that starts Caddy and stops if it fails; Compose also refuses to create the directory if it is missing. Never generate a replacement certificate on the host or commit certificate material.

The edge filter only applies to packets arriving on the public interface (detected from the default route; override with `SWOOVR_EXT_IF=<name>`). Containers' own outbound HTTPS, such as the GeoNames download and provider calls, is not filtered.

Container trust keeps the system CAs. Only the services that call Caddy's public names directly (API, Web, Admin, the `api-command` and Keycloak bootstrap jobs, and Zammad's Rails and scheduler) get `SSL_CERT_FILE=origin-trust.pem`, which is the system CAs plus the Cloudflare Origin CA roots. Keycloak adds the Cloudflare roots to its default truststore with `KC_TRUSTSTORE_PATHS`. Database clients trust only the private PostgreSQL CA (`VerifyFull`).

`tls-setup` prints the Cloudflare Origin CA root fingerprints. Compare them with Cloudflare's documentation. `origin-cert-check` refuses a certificate that is missing, not RSA 2048, has a wildcard, lacks one of the six hostnames, expires within 30 days, or doesn't match its key.

## 3. Private configuration

```sh
python3 bin/envtool.py init            # creates env/*.env, generating every password and data key once
```

Open each `env/*.env` in an editor and replace every `__OWNER:…__` value. Generated values are kept consistent across files: for example, the API's database password matches the bootstrap role password. The generated values are also stored in `env/.generated` (0600). Never paste values into a shell command, issue, PR or chat.

Generate the three **disabled** realm templates on your own machine, then copy the output directory to the host:

```sh
# On your machine, from the reviewed checkout (output outside any checkout):
python3 scripts/uat/keycloak_realms.py --admin-origin https://admin-uat.swoovr.app \
  --venue-origin https://venues-uat.swoovr.app --entra-tenant-id "<tenant>" --entra-client-id "<client>" \
  --output /absolute/private/dir/swoovr-uat-realms
# On the host:
./bin/swoovr realms-env /path/to/swoovr-uat-realms
./bin/swoovr check       # before bootstrap, only the Zammad tokens may still be placeholders
```

## 4. Fresh bootstrap

```sh
./bin/swoovr bootstrap
```

The bootstrap runs these steps in order and stops at the first failure:

1. Pulls every pinned image.
2. Starts PostgreSQL and runs `db-bootstrap`. This creates the least-privilege roles and the ownership of the `swoovr` and `keycloak` databases.
3. Runs `migrate` (all EF migrations, including `20261108133228_ImageProcessingStaging`).
4. Runs `places-import` (GeoNames, recorded SHA-256 values).
5. Starts Caddy and Keycloak, then runs `keycloak-bootstrap`. This creates the three realms, which stay **disabled**, plus the service client.
6. Runs `zammad-sso`, which creates the staff realm's `swoovr-zammad` PKCE client with the hosted callback.
7. Starts ClamAV and Zammad. When Rails is healthy, it runs `zammad-provision`: groups, identities, tokens, the signed webhook to `https://api-uat.swoovr.app/api/integrations/zammad/events`, and SSO. The tokens are written into `env/api.env`.
8. Starts the API, then Web and Admin, and prints health through Caddy.

## 5. After bootstrap (manual gates; same as DEPLOYMENT-UAT.md)

These gates are identical on Azure and Vultr. Follow the identity, provider and admission sections of [DEPLOYMENT-UAT.md](../../docs/delivery/DEPLOYMENT-UAT.md) in this order:

1. Verify the realms. Then protect the temporary master bootstrap administrator and retire it after setup.
2. Register the Entra broker callback `https://auth-uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint`.
3. Run the provider checks (MailerSend, Twilio, Didit with its webhook `https://api-uat.swoovr.app/api/webhooks/didit`, R2, screening). Then enable the realms.
4. Sign the approved staff member in once, then grant root:

   ```sh
   ./bin/swoovr job api-command root-admin grant <verified-staff-subject>
   ./bin/swoovr job api-command founders allocate
   ```

5. Run the hosted smoke test (`scripts/uat/smoke.cjs --base-url https://uat.swoovr.app`) and the checks in DEPLOYMENT-UAT.md "Smoke and final admission gate". That includes SignalR, Zammad WebSockets, passkeys and staff helpdesk SSO.

The Keycloak admin console isn't public. To reach it, open an SSH SOCKS tunnel with `ssh -D 1080 root@HOST`. Point a browser profile at the SOCKS proxy with remote DNS and open `https://auth-uat.swoovr.app/admin/`. The host resolves that name to Caddy's private address. The browser will warn about the origin certificate; that is expected for this path only.

## Day-2 operations

| Task | Command |
| --- | --- |
| Deploy a new release | Copy the new `release-manifest.env` to the host, then run `cat release-manifest.env thirdparty.env > next.env && ./bin/swoovr upgrade next.env`. This pulls, stops the apps, migrates once, then recreates. |
| Roll back | `./bin/swoovr rollback`. This switches images only, so use it only when the previous release understands the current schema. |
| Status / logs | `./bin/swoovr status`, `./bin/swoovr logs api` |
| Refresh the Cloudflare ranges | `./bin/cloudflare-ips && ./bin/swoovr compose restart caddy` (monthly) |
| **Reset UAT** (owner: disposable) | `./bin/swoovr reset --destroy-uat-data`, then `./bin/swoovr bootstrap` |
| Optional Zammad backup | `./bin/swoovr compose --profile backup up -d zammad-backup`. This writes to a local volume only and is not off-host. |

A reset removes **only** this Compose project's volumes (`swoovr-uat_*`). It keeps `env/` and `tls/`, so the next bootstrap reuses the same generated keys. To start with new keys as well, move `env/` aside first and run `envtool.py init` again.

## Known gaps (do not treat UAT as accepted until closed)

- **No off-host backup** of UAT. The owner accepted rebuild-from-scratch for UAT. The operator image's backup transport is Azure-only.
- **Zammad outgoing mail** isn't configured until an SMTP account is chosen (Q176). Without it, staff notifications are not sent.
- **Single host, single replica.** A host or Docker restart signs members out, because BFF sessions are in memory.
- **Resource sizing is unmeasured.** Watch RAM: Elasticsearch has a 2 GB heap, and Zammad plus the .NET apps add more. Also watch disk and I/O during the first week.
- The **Keycloak image bakes UAT policy URLs**. Production needs its own build (see the plan).
