# Swoovr UAT on one Vultr VM

This is the runbook for the deployment bundle. The bundle runs the complete, disposable UAT stack on one Debian cloud VM with Docker (D361). **Create the VM first: [VULTR-VM.md](VULTR-VM.md).** Plan, decisions and open gates: [images and bundle plan](../../docs/delivery/UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md). Background: [Vultr stack review](../../docs/delivery/VULTR-STACK-REVIEW-2026-11-10.md).

**Status: prepared, not deployed.** Two parts were validated locally on 10 November 2026: the TLS-only PostgreSQL container with the real `database-bootstrap` job, and the Caddyfile. Nothing has run on the Vultr VM yet. No real provider, DNS record or hosted sign-in has been accepted.

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
| `mailpit` | Mailpit 1.31.0 | No. SMS (and optional API email) sink; UI on the VM's loopback `127.0.0.1:8025`, through an SSH tunnel |
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

0. **The VM:** create it on Vultr ([VULTR-VM.md](VULTR-VM.md) §1–§3) and note its public IPv4.
1. **Docker Hub (`repasscloud`):** create five **private** repositories first: `swoovr-api`, `swoovr-web`, `swoovr-admin`, `swoovr-operator`, `swoovr-keycloak`. A push to a missing repository creates it with the namespace's default visibility, which may be public. Check that the Docker Hub plan allows five private repositories. GitHub uses the organisation secrets `DOCKER_HUB_USERID` and `DOCKER_HUB_PAT` (owner, 11 November 2026); create a separate **Read-only** access token for this host (§1).
2. **Images:** the `Images` workflow is enabled for manual runs (D359). It logs in to Docker Hub with the organisation secrets `DOCKER_HUB_USERID` and `DOCKER_HUB_PAT`, which the org shares with every repository. Then Actions → **Images** → **Run workflow** on `main`. About 15–25 minutes. **This run also makes the deployment bundle; it is not in Git.** Download the `swoovr-release-<commit>` artifact from the run's summary (Artifacts, at the bottom). It contains `release-manifest.env`, `release-manifest.json` and **`dist/swoovr-uat-bundle-<commit>.tar.gz`**: the bundle is inside the `dist/` folder. Artifacts expire after 30 days; to download again, run again, or rebuild only the bundle, see [VULTR-VM.md §5a](VULTR-VM.md#5a-get-the-bundle-on-your-machine). To build without Actions instead, see the plan, "Building without Actions".
3. **Cloudflare (swoovr.app zone):**
   - Proxied (orange-cloud) `A` records to the host's public IPv4 for `uat`, `api-uat`, `admin-uat`, `auth-uat` and `helpdesk-uat`. **No `AAAA` records:** the Docker networks are IPv4-only, so Cloudflare must reach the origin over IPv4. Visitors still get IPv6 from Cloudflare's edge.
   - SSL/TLS mode: **Full (strict)**.
   - SSL/TLS → Origin Server → **Create certificate** (owner issue #307, Q175): PEM, RSA 2048, 15 years, explicit SANs only (no wildcard): `uat.swoovr.app`, `api-uat.swoovr.app`, `admin-uat.swoovr.app`, `helpdesk-uat.swoovr.app`, `auth-uat.swoovr.app`, `venues-uat.swoovr.app`. This certificate is for UAT only; production gets its own. Keep the certificate and key private for step 2.
   - Cache rule: **bypass cache** for all five hostnames, because their content is personalised.
4. **Provider accounts.** These are the same values the Azure Key Vault inputs use. UAT uses real providers (D321):
   - **Who sends what** (D360; every value in `env/mail.env`):

     | Sender | Service | What it needs |
     | --- | --- | --- |
     | API email (notices, invitations, founder codes, safety) | **Twilio Email API** (`SWOOVR_API_MAIL_SERVICE=twilio`) | Twilio Email API key SID and secret (#316's `TWILIO_EMAIL_SID_RK` / `TWILIO_EMAIL_SECRET_RK`) |
     | Keycloak email (verify email, reset password) | **Twilio Email API** (`SWOOVR_KEYCLOAK_MAIL_SERVICE=twilio`) | the same Twilio key |
     | Zammad outgoing (staff notifications, replies) | **SendGrid SMTP** | `smtp.sendgrid.net:587`, username `apikey`, a SendGrid API key with Mail Send as the password |
     | Zammad incoming (support mailbox → General Support) | **IMAP** (your mail server) | host, port, SSL/STARTTLS, username, password, folder, support address. Optional: leave the host empty until it's ready |
     | API SMS (phone codes, safety notices) | **Mailpit** now (`SWOOVR_SMS_SERVICE=mailpit`), **Twilio** after SMS verification | Twilio Account SID, Auth Token and From number in `env/api.env` when you switch |

   - **Sender:** `hello@repasscloud.com` for now (D358, #313). It must be an authenticated sender for **both** Twilio Email and SendGrid (domain authentication for `repasscloud.com`: the DNS records each console lists), or those messages are refused.
   - **Twilio Email:** create the Email API key in the Twilio console with access to send email only, and check the account's sending limits cover testing.
   - **SendGrid:** Settings → API Keys → Create (restricted, **Mail Send** only). The SMTP username is the literal word `apikey`.
   - **Twilio SMS (later):** Swoovr makes and checks its own codes; Twilio only delivers the text through Programmable Messaging (not Verify). When verification completes: an upgraded account; Messaging → Settings → **Geo permissions** for every tester country (at least Australia); an SMS-capable `From` number in E.164 form (`+61…`). Put the Account SID, Auth Token and From in `env/api.env` (`Sms__Twilio__*`), set `SWOOVR_SMS_SERVICE=twilio`, then `./bin/swoovr mail-apply`. #315 tracks restricted API keys.
   - **Until then, SMS codes go to Mailpit, not to phones.** Members can't receive a code themselves: phone verification is required on UAT, so either read the code in Mailpit and pass it on, or keep sign-ups to people you can help until Twilio SMS is live.
   - **Didit** (API key, workflow ID, webhook secret; webhook URL set after bootstrap, §5), **Cloudflare R2** (endpoint, bucket, and an R2 API token with Object Read & Write on that bucket only), the screening endpoint and key, the media pipeline token and signing key, two escalation mailboxes you read, the Entra client secret, and the Entra tenant and client IDs for realm generation.

## 1. Prepare the host (once)

### 1a. Get the bundle (on your machine)

The bundle `swoovr-uat-bundle-<commit>.tar.gz` is **not in Git, and you don't build it by hand**. The **Images** GitHub Actions workflow makes it in the same run that pushes the five images to Docker Hub (§0 step 2). `<commit>` is the full 40-character commit the images were built from.

1. Download the run's artifact **`swoovr-release-<commit>`**:
   - **Browser:** this repository on GitHub → **Actions** → **Images** → open the successful run → **Artifacts** (bottom of the run's summary page) → `swoovr-release-<commit>`. It downloads as a zip; unzip it.
   - **GitHub CLI**, from inside your checkout of this repository (`gh` unzips it for you):

     ```sh
     gh run list --workflow images.yml -L 5      # note the run ID of the successful run
     gh run download <run-id> -n swoovr-release-<commit> -D ~/swoovr-release
     ```

2. Inside the downloaded folder:

   ```text
   release-manifest.env
   release-manifest.json
   dist/swoovr-uat-bundle-<commit>.tar.gz      <- this is the bundle; note the dist/ folder
   ```

Artifacts expire **30 days** after the run. After that, run **Images** again (Actions → Images → **Run workflow** on `main`), or rebuild only the bundle from a checkout at the same commit with that run's `release-manifest.env` (its contents are also printed on the run's summary page):

```sh
git checkout <commit>
./deploy/vultr-uat/bin/make-bundle.sh release-manifest.env dist     # writes dist/swoovr-uat-bundle-<commit>.tar.gz
```

The bundle holds no secrets: `compose.yaml`, the Caddyfile, `bin/`, `env/*.env.example`, the Zammad provisioner and `release.env` (the image digests). Secrets are created on the VM in §3.

### 1b. Copy it to the VM and prepare the host

Run every VM command in this runbook **as root**; if you log in as another user, `sudo -i` first and stay root for the whole runbook. Mixing `sudo` and a normal user leaves files with mixed owners.

```sh
# On your machine, from the folder that holds the unzipped artifact's dist/:
scp dist/swoovr-uat-bundle-<commit>.tar.gz root@<VM IPv4>:/tmp/

# As root on the new VM (VULTR-VM.md §5b covers the first login and system update).
mkdir -p /opt/swoovr && mv /tmp/swoovr-uat-bundle-*.tar.gz /opt/swoovr/ && cd /opt/swoovr
tar -xzf swoovr-uat-bundle-<commit>.tar.gz && cd swoovr-uat
./bin/host-setup        # optional: SWOOVR_SSH_CIDRS="<your public IP>/32" ./bin/host-setup (see VULTR-VM.md §5b)
```

`host-setup` does the following:

- Installs Docker Engine and the Compose plugin from Docker's Debian repository.
- Sets log rotation and `live-restore`.
- Sets `vm.max_map_count` for Elasticsearch and `vm.overcommit_memory` for Redis.
- Installs a host input firewall in its own nftables table. It never touches Docker's rules.
- Enables unattended security updates.
- Points the five hostnames at Caddy's private address in `/etc/hosts`.

The `repasscloud/swoovr-*` repositories are private (D356), so the host must sign in to Docker Hub before `bin/swoovr pull` or `bootstrap`. Use a separate Docker Hub access token with **Read-only** access, never the workflow's Read & Write token. Type the token at the prompt; never put it on the command line:

```sh
docker login -u <dockerhub-user>      # paste the read-only token at the password prompt; stored in /root/.docker/config.json
```

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

Client addresses: Caddy believes `CF-Connecting-IP` only from Cloudflare's ranges, and `X-Forwarded-For` only from the Web and Admin BFFs at their fixed addresses (172.30.0.10 and .11). It sends every upstream exactly one `X-Forwarded-For` value, so per-address limits see the member, not a Cloudflare edge or container. Other containers get dynamic addresses from 172.30.0.128/25 and can't take a trusted one.

The edge filter only applies to packets arriving on the public interface (detected from the default route; override with `SWOOVR_EXT_IF=<name>`). Containers' own outbound HTTPS, such as the GeoNames download and provider calls, is not filtered.

Container trust keeps the system CAs. Only the services that call Caddy's public names directly (API, Web, Admin, the `api-command` and Keycloak bootstrap jobs, and Zammad's Rails and scheduler) get `SSL_CERT_FILE=origin-trust.pem`, which is the system CAs plus the Cloudflare Origin CA roots. Keycloak adds the Cloudflare roots to its default truststore with `KC_TRUSTSTORE_PATHS`. Database clients trust only the private PostgreSQL CA (`VerifyFull`).

`tls-setup` refuses Cloudflare Origin CA roots whose SHA-256 fingerprints differ from the pinned values (both roots expire 15 August 2029). `origin-cert-check` refuses a certificate that is missing, not RSA 2048, has a wildcard, lacks one of the six hostnames, expires within 30 days, or doesn't match its key.

## 3. Private configuration

```sh
python3 bin/envtool.py init            # creates env/*.env, generating every password and data key once
```

Open each `env/*.env` in an editor and replace every `__OWNER:…__` value. **All email and SMS settings live in one file, `env/mail.env`** (D358, D360): the sender `SWOOVR_MAIL_FROM` (`hello@repasscloud.com`) and display names, which service each sender uses (the table in §0), the Twilio Email key, the SendGrid SMTP relay, the optional Zammad IMAP mailbox, and the SMS choice. Its header lists which service uses what. The owner keeps a prepared copy at `.local/uat-host/mail.env` on their workstation; copy its values in with an editor.

Replace every `__OWNER:…__` value in the other files too. Generated values are kept consistent across files: for example, the API's database password matches the bootstrap role password. The generated values are also stored in `env/.generated` (0600). Never paste values into a shell command, issue, PR or chat.

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
7. Starts ClamAV and Zammad. When Rails is healthy, it runs `zammad-provision`: groups, identities, tokens, the signed webhook to `https://api-uat.swoovr.app/api/integrations/zammad/events`, SSO, the SendGrid notification relay, and the IMAP support mailbox when `env/mail.env` sets one (into General Support). The tokens are written into `env/api.env`.
8. Starts Mailpit with the API.
9. Starts the API, then Web and Admin, and prints health through Caddy.

## 5. After bootstrap (manual gates; same as DEPLOYMENT-UAT.md)

These gates are identical on Azure and Vultr. Follow the identity, provider and admission sections of [DEPLOYMENT-UAT.md](../../docs/delivery/DEPLOYMENT-UAT.md) in this order:

1. Verify the realms. Then protect the temporary master bootstrap administrator and retire it after setup.
2. Register the Entra broker callback `https://auth-uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint`.
3. Run the provider checks (Twilio Email, SendGrid, Didit with its webhook `https://api-uat.swoovr.app/api/webhooks/didit`, R2, screening). Prove delivery with an inbox you control: sign up and verify the email (Keycloak via Twilio Email), add the phone and read the code in Mailpit ("Mailpit" below), trigger an API email (for example an MFA change notice), and get a Zammad notification (SendGrid). If you set IMAP, send a message to the support address and see it arrive in General Support. If one doesn't arrive: `./bin/swoovr logs api` and `./bin/swoovr logs keycloak` show the fixed outcome without the address or content; the Twilio and SendGrid consoles show their side. Then enable the realms.
4. Sign the approved staff member in once, then grant root:

   ```sh
   ./bin/swoovr job api-command root-admin grant <verified-staff-subject>
   ./bin/swoovr job api-command founders allocate
   ```

5. Run the hosted smoke test (`scripts/uat/smoke.cjs --base-url https://uat.swoovr.app`) and the checks in DEPLOYMENT-UAT.md "Smoke and final admission gate". That includes SignalR, Zammad WebSockets, passkeys and staff helpdesk SSO.

**Mailpit** (SMS codes while `SWOOVR_SMS_SERVICE=mailpit`, and API email if you select `mailpit`): `ssh -L 8025:127.0.0.1:8025 root@HOST`, then open http://127.0.0.1:8025 on your machine. Each SMS is an email to `sms-<number>@simulated.swoovr.invalid` marked SIMULATED. Nothing in Mailpit was delivered to anyone.

The Keycloak admin console isn't public. To reach it, open an SSH SOCKS tunnel with `ssh -D 1080 root@HOST`. Point a browser profile at the SOCKS proxy with remote DNS and open `https://auth-uat.swoovr.app/admin/`. The host resolves that name to Caddy's private address. The browser will warn about the origin certificate; that is expected for this path only.

## Day-2 operations

| Task | Command |
| --- | --- |
| Change the sender, a mail service, credentials or the IMAP mailbox | Edit `env/mail.env`, then `./bin/swoovr mail-apply`. It checks the file, starts Mailpit, recreates Keycloak and the API, and re-applies Zammad's relay, sender and IMAP mailbox (existing tokens are reused). Then send one of each: a Keycloak verification email, an API email and a Zammad notification. |
| Switch SMS from Mailpit to Twilio | Fill `Sms__Twilio__AccountSid`, `Sms__Twilio__AuthToken` and `Sms__Twilio__From` in `env/api.env`, set `SWOOVR_SMS_SERVICE=twilio` in `env/mail.env`, then `./bin/swoovr mail-apply`. `swoovr check` refuses the switch while those values are empty. |
| Read SMS codes | Mailpit over the SSH tunnel (above). |
| Deploy a new release | Copy the new `release-manifest.env` to the host, then run `cat release-manifest.env thirdparty.env > next.env && ./bin/swoovr upgrade next.env`. This pulls, stops the apps, migrates once, then recreates. |
| Roll back | `./bin/swoovr rollback`. This switches images only, so use it only when the previous release understands the current schema. |
| Status / logs | `./bin/swoovr status`, `./bin/swoovr logs api` |
| Refresh the Cloudflare ranges | `./bin/cloudflare-ips && ./bin/swoovr compose restart caddy` (monthly) |
| **Reset UAT** (owner: disposable) | `./bin/swoovr reset --destroy-uat-data`, then `./bin/swoovr bootstrap` |
| Optional Zammad backup | `./bin/swoovr compose --profile backup up -d zammad-backup`. This writes to a local volume only and is not off-host. |

A reset removes **only** this Compose project's volumes (`swoovr-uat_*`). It keeps `env/` and `tls/`, so the next bootstrap reuses the same generated keys. To start with new keys as well, move `env/` aside first and run `envtool.py init` again.

## Known gaps (do not treat UAT as accepted until closed)

- **No off-host backup** of UAT. The owner accepted rebuild-from-scratch for UAT. The operator image's backup transport is Azure-only.
- **SMS goes to Mailpit** until Twilio SMS verification completes (D360). Members can't receive codes on their phones until then.
- **Zammad inbound mail** works only once the IMAP values are set in `env/mail.env` (Q178 answered, D360).
- **Keycloak emails use Keycloak's stock templates** (no Swoovr `emailTheme` yet, #314). The API's own emails are plain text written in code.
- **Every email comes from `hello@repasscloud.com`** until the Swoovr domain is authenticated with Twilio Email and SendGrid (#313).
- **Single host, single replica.** A host or Docker restart signs members out, because BFF sessions are in memory.
- **Logs and capability tokens:** Caddy redacts trusted-contact `/safety/meeting|contact/<token>` paths and Keycloak/OIDC `key`, `code`, `token`, `state` query values in its access and error logs, and drops Referer. Keep it that way. Cloudflare-side logging (Logpush, if ever enabled) must apply the same redaction.
- **Resource sizing is unmeasured.** Watch RAM: Elasticsearch has a 2 GB heap, and Zammad plus the .NET apps add more. Also watch disk and I/O during the first week.
- The **Keycloak image bakes UAT policy URLs**. Production needs its own build (see the plan).
