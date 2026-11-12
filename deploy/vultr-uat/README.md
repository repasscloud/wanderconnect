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
   - **Didit** (API key, workflow ID, webhook secret; webhook URL set after bootstrap, §5), **Cloudflare R2** (endpoint, bucket, and an R2 API token with Object Read & Write on that bucket only), the screening endpoint and key, the media pipeline token and signing key, two escalation mailboxes you read, and the Entra staff app registration (tenant ID, client ID, client secret; how to create it: §3b).

## 1. Prepare the host (once)

### 1a. Get the bundle (on your machine)

The bundle `swoovr-uat-bundle-<commit>.tar.gz` is **not in Git, and you don't build it by hand**. The **Images** GitHub Actions workflow makes it in the same run that pushes the five images to Docker Hub (§0 step 2). `<commit>` is the full 40-character commit the images were built from.

1. Download the run's artifact **`swoovr-release-<commit>`**:
   - **Browser:** this repository on GitHub → **Actions** → **Images** → open the successful run → **Artifacts** (bottom of the run's summary page) → `swoovr-release-<commit>`. It downloads as a zip; unzip it.
   - **GitHub CLI**, from inside your checkout of this repository (`gh` unzips it for you):

     ```sh
     # 1. List recent runs: <run-id>  <commit>  <result>  <date>
     gh run list --workflow images.yml -L 5 --json databaseId,headSha,conclusion,createdAt \
       -q '.[] | "\(.databaseId)  \(.headSha)  \(.conclusion)  \(.createdAt)"'

     # 2. Put the newest successful run in $1 (run ID) and $2 (commit), show the command, then download
     set -- $(gh run list --workflow images.yml --status success -L 1 --json databaseId,headSha \
       -q '.[0] | "\(.databaseId) \(.headSha)"')
     [ -n "$2" ] || echo "No successful Images run found"
     echo gh run download "$1" -n "swoovr-release-$2" -D ~/swoovr-release
     gh run download "$1" -n "swoovr-release-$2" -D ~/swoovr-release
     ```

     To download a different run, set it yourself first: `set -- <run-id> <commit>`, using a line from step 1, then run the two last commands.

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

Two separate parts: **3a** fills the env files on the host; **3b** registers the staff sign-in app in Microsoft Entra, generates the three Keycloak realm templates on your own machine and loads them on the host. Do 3a first.

### 3a. Env files (on the host)

```sh
cd /opt/swoovr/swoovr-uat
python3 bin/envtool.py init            # creates env/*.env, generating every password and data key once
```

Open each `env/*.env` in an editor and replace every `__OWNER:…__` value. **All email and SMS settings live in one file, `env/mail.env`** (D358, D360): the sender `SWOOVR_MAIL_FROM` (`hello@repasscloud.com`) and display names, which service each sender uses (the table in §0), the Twilio Email key, the SendGrid SMTP relay, the optional Zammad IMAP mailbox, and the SMS choice. Its header lists which service uses what. The owner keeps a prepared copy at `.local/uat-host/mail.env` on their workstation; copy its values in with an editor.

Replace every `__OWNER:…__` value in the other files too, **except `SWOOVR_UAT_ENTRA_CLIENT_SECRET` in `env/realms.env`**: you create that secret in 3b step 1. Leave the `__RUN_…__` values alone; commands fill them (`realms-env` in 3b, `zammad-provision` during bootstrap). Generated values are kept consistent across files: for example, the API's database password matches the bootstrap role password. The generated values are also stored in `env/.generated` (0600). Never paste values into a shell command, issue, PR or chat. If a value contains `$`, wrap the whole value in single quotes; `swoovr check` refuses it otherwise.

### 3b. Staff sign-in (Entra) and the Keycloak realms

Staff sign in through Microsoft Entra only (no passwords, no social sign-in). Keycloak's staff realm needs three values from an Entra **app registration**: the **Directory (tenant) ID** and the **Application (client) ID** (both UUIDs, not secret; they go into the realm templates) and a **client secret** (secret; it goes into `env/realms.env` on the host only).

**1. Register the app in Entra** (once; needs at least the Application Developer role in your Microsoft tenant):

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com). If you have several tenants, switch to the one your staff accounts are in (Settings icon → Directories + subscriptions).
2. **Entra ID** → **App registrations** → **New registration**:
   - **Name:** `Swoovr UAT staff` (staff may see it on Microsoft's sign-in page).
   - **Supported account types:** single tenant (**Accounts in this organizational directory only**). Never a multitenant or personal-accounts option: the generator only builds a tenant-specific issuer.
   - **Redirect URI:** platform **Web**, value exactly `https://auth-uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint`.
   - **Register.**
3. On the app's **Overview** page, note the two IDs:
   - **Directory (tenant) ID** → `--entra-tenant-id` in step 2.
   - **Application (client) ID** → `--entra-client-id` in step 2.

   Don't use the Object ID; it's a different UUID. The generator rejects anything that isn't a UUID, and the all-zeros UUID.
4. **Certificates & secrets** → **Client secrets** → **New client secret**. Pick an expiry (24 months at most; put the date in your calendar, because staff sign-in stops when it expires). Copy the **Value** column straight away (not the Secret ID); it's shown only once. On the host, paste it into `env/realms.env` as `SWOOVR_UAT_ENTRA_CLIENT_SECRET` with an editor.
5. **API permissions:** Keycloak requests `openid profile email` and never calls Microsoft Graph. Recommended: **Add a permission** → Microsoft Graph → **Delegated** → `openid`, `profile`, `email`, then **Grant admin consent**, so staff don't see a consent prompt.
6. Limit who can sign in: **Entra ID** → **Enterprise apps** → `Swoovr UAT staff` → **Properties** → **Assignment required?** = **Yes** → Save. Then **Users and groups** → **Add user/group** → only the approved staff. Without this, anyone in the tenant (guests included) can reach the staff realm.

**2. Generate the realm templates (on your machine).** The generator reads the realm baseline from the repository, so run it from a checkout of **the same commit as the bundle**. That is the `<commit>` in the bundle's file name, `swoovr-uat-bundle-<commit>.tar.gz` (§1a); it's also `headSha` in the `gh run list` output. It writes three **disabled** realm templates and a `manifest.json`. They hold the two IDs and `${SWOOVR_UAT_…}` placeholders, no secrets; Keycloak's bootstrap fills the placeholders in memory. The output directory must be:

- an **absolute** path;
- **new**: the generator refuses to overwrite, so use a new name each time;
- inside a folder that **already exists**;
- **outside every Git checkout**, including the temporary one below.

Make a temporary, **detached** checkout of that commit with `git worktree`. It's a second working folder from your existing clone, fixed at one commit with no branch, so your normal checkout and its uncommitted changes aren't touched:

```sh
# 1. Set the commit once (all 40 characters from the bundle's file name):
COMMIT=<commit>

# 2. From your existing clone of this repository, fetch it and make the detached checkout:
cd <your clone of this repository>
git fetch origin
git cat-file -e "$COMMIT^{commit}" && echo "commit found"   # stop here if this errors: wrong or unfetched commit
git worktree add --detach ~/swoovr-uat-checkout-$COMMIT "$COMMIT"

# 3. Generate from inside it, writing outside it:
mkdir -p -m 700 ~/swoovr-uat-private                         # parent folder, not in any checkout
cd ~/swoovr-uat-checkout-$COMMIT
git rev-parse HEAD                                           # must print the same 40 characters as $COMMIT
# No member-origin flag: https://uat.swoovr.app (also the passkey RP ID) and https://auth-uat.swoovr.app
# are built into the generator, so they can't change by mistake. Only the two portal origins are flags.
python3 scripts/uat/keycloak_realms.py \
  --admin-origin https://admin-uat.swoovr.app \
  --venue-origin https://venues-uat.swoovr.app \
  --entra-tenant-id "<Directory (tenant) ID>" \
  --entra-client-id "<Application (client) ID>" \
  --output ~/swoovr-uat-private/realms-$COMMIT

# 4. Remove the temporary checkout (the generated folder in ~/swoovr-uat-private stays):
cd <your clone of this repository>
git worktree remove ~/swoovr-uat-checkout-$COMMIT
```

No clone yet? `git clone git@github.com:repasscloud/wanderconnect.git ~/swoovr-uat-checkout-$COMMIT && cd ~/swoovr-uat-checkout-$COMMIT && git checkout --detach "$COMMIT"` does the same; afterwards delete that folder instead of step 4.

Type the two origins exactly as shown (CONFIRMED D355; the generator accepts only single-level `https://*-uat.swoovr.app` origins). The member origin `https://uat.swoovr.app` and the authentication origin `https://auth-uat.swoovr.app` are built in. On success it prints `Prepared three DISABLED UAT realms and manifest in …`, and the folder contains `swoovr-members.json`, `swoovr-venues.json`, `swoovr-staff.json` and `manifest.json`.

**3. Copy the folder to the host and load it:**

```sh
# On your machine (-p keeps the 0700/0600 permissions):
scp -rp ~/swoovr-uat-private/realms-$COMMIT root@<VM IPv4>:/root/swoovr-uat-realms

# As root on the host:
cd /opt/swoovr/swoovr-uat
./bin/swoovr realms-env /root/swoovr-uat-realms    # writes the three templates into env/realms.env
rm -r /root/swoovr-uat-realms                      # env/realms.env now holds them; keep your local copy
./bin/swoovr check
```

`realms-env` prints `Stored three disabled realm templates in env/realms.env.` Before the first bootstrap, `swoovr check` is expected to print exactly these four `FAIL` lines and nothing else:

```text
FAIL api.env:43: Ticketing__Zammad__IdentityToken is not filled
FAIL api.env:44: Ticketing__Zammad__StaffDirectoryToken is not filled
FAIL api.env:45: Ticketing__Zammad__RetentionToken is not filled
FAIL api.env:46: Ticketing__Zammad__HomeCityCheckToken is not filled
```

Bootstrap fills those tokens itself. If you see only these four, go to §4. Fix **any other** `FAIL` first; for example, `realms.env: SWOOVR_UAT_ENTRA_CLIENT_SECRET is not filled` means 3b step 1.4 is missing. The check stops after these lines, so it hasn't checked the origin certificate or the Compose configuration yet. You don't run those yourself: `swoovr bootstrap` runs the whole check first (allowing only these four tokens) and stops before changing anything if any part fails.

To change the tenant or client ID later, generate into a new folder and run `realms-env` again. Keycloak only imports realms that don't exist yet, so on a host that has already bootstrapped you also need **Reset UAT** (Day-2 operations).

## 4. Fresh bootstrap

```sh
./bin/swoovr bootstrap
```

The bootstrap runs these steps in order and stops at the first failure:

1. Runs the full check: env files (only the four Zammad tokens may be unfilled), the origin certificate and the Compose configuration. Then pulls every pinned image.
2. Starts PostgreSQL and runs `db-bootstrap`. This creates the least-privilege roles and the ownership of the `swoovr` and `keycloak` databases.
3. Runs `migrate` (all EF migrations, including `20261108133228_ImageProcessingStaging`).
4. Runs `places-import` (GeoNames, recorded SHA-256 values).
5. Starts Caddy and Keycloak, then runs `keycloak-bootstrap`. This creates the three realms, which stay **disabled**, plus the service client.
6. Runs `zammad-sso`, which creates the staff realm's `swoovr-zammad` PKCE client with the hosted callback.
7. Starts ClamAV and Zammad. When Rails is healthy, it runs `zammad-provision`: groups, identities, tokens, the signed webhook to `https://api-uat.swoovr.app/api/integrations/zammad/events`, SSO, the SendGrid notification relay, and the IMAP support mailbox when `env/mail.env` sets one (into General Support). The tokens are written into `env/api.env`.
8. Starts Mailpit with the API.
9. Starts the API, then Web and Admin, and prints health through Caddy.

### Check the result

The last lines show every container and `health through Caddy`, with each of the five hostnames at `200`. Then make two more checks.

1. **Zammad's one-off setup finished** (on the host). `zammad-init` may still show `Up` for a few minutes after bootstrap ends:

   ```sh
   ./bin/swoovr compose ps -a zammad-init      # wait until it shows Exited (0)
   ```

   If it shows `Exited` with any other number, read `./bin/swoovr logs zammad-init` before continuing.

2. **The public route through Cloudflare works** (on your machine). The health check above runs inside the VM, so it doesn't test Cloudflare, DNS or the edge firewall:

   ```sh
   for h in uat api-uat admin-uat auth-uat helpdesk-uat; do
     printf '%-14s ' "$h"; curl -s -o /dev/null -w '%{http_code}\n' "https://$h.swoovr.app/"
   done
   ```

   Any number from the app is fine (200, 302, 404). A `52x` is Cloudflare failing to reach the VM: check the DNS records and SSL mode (§0 step 3) and `./bin/cloudflare-ips` (§2). `000` means DNS or the network on your side.

Then go to §5.

### If `keycloak-bootstrap` fails

The job prints only `Operator command failed; no success or fallback. Review private job diagnostics before retrying.`, with no details. To see why, run the same bootstrap again with each step printed. It prints each request, its HTTP status and Keycloak's error message, never a password, token or secret:

```sh
cd /opt/swoovr/swoovr-uat
./bin/swoovr compose --profile jobs run --rm -T --entrypoint /opt/operator-venv/bin/python keycloak-bootstrap - <<'PY'
import json, os, sys
sys.path.insert(0, '/opt/uat')
import keycloak_bootstrap as kb
env = os.environ
try:
    kb.prepared_realms(env)
    print('PASS realm inputs: three disabled templates, every secret placeholder has a value')
except (KeyError, ValueError) as error:
    sys.exit(f'FAIL realm inputs: {type(error).__name__}: {error}')
transport = kb.Transport(env['SWOOVR_KEYCLOAK_BASE'])
def logged(path, method='GET', data=None, token=None, form=False):
    label = f" ({data['realm']})" if isinstance(data, dict) and 'realm' in data else ''
    try:
        code, body = transport(path, method, data, token, form)
    except OSError as error:
        print(f'{method} {path}{label} -> {type(error).__name__}: {error}')
        raise
    print(f'{method} {path}{label} -> HTTP {code}')
    if method == 'GET' and path == '/admin/realms' and isinstance(body, list):
        print('  existing realms:', ', '.join(r.get('realm', '?') for r in body))
    if code >= 400:
        print('  Keycloak says:', json.dumps(body)[:800] if body is not None else '(no JSON body)')
    return code, body
try:
    kb.bootstrap(logged, env)
except (KeyError, ValueError, OSError) as error:
    sys.exit(f'FAIL {type(error).__name__}: {error}')
PY
./bin/swoovr logs keycloak 2>&1 | grep -iE 'error|warn' | tail -20    # Keycloak's side of a refused request
```

How to read it:

| Last lines | Meaning |
| --- | --- |
| `FAIL realm inputs: KeyError: '<NAME>'` | `<NAME>` is missing from `env/keycloak.env` or `env/realms.env`. Fill it (§3). |
| `FAIL realm inputs: ValueError: Missing or unsupported realm secret binding.` | `SWOOVR_UAT_ENTRA_CLIENT_SECRET` (or a web secret) in `env/realms.env` is empty (§3b step 1.4). |
| `POST /realms/master/protocol/openid-connect/token -> HTTP 503`, and the Keycloak log shows `Request received during bootstrapping, returning a 503 error` | Keycloak's health check said ready while its public side was still starting (a timing gap, not a configuration problem). Run the block again a minute later. |
| `POST /realms/master/protocol/openid-connect/token -> HTTP 401` | Keycloak refused the bootstrap admin. Keycloak creates it only on its **first** start with an empty database, so a changed `KC_BOOTSTRAP_ADMIN_PASSWORD` no longer matches. Reset (below). |
| `-> URLError` / `SSLError` | The job couldn't reach `https://auth-uat.swoovr.app` through Caddy, or didn't trust its certificate. Check `./bin/swoovr status` and §2. |
| `POST /admin/realms (swoovr-…) -> HTTP 400/409` | Keycloak rejected that realm template. `Keycloak says:` and the Keycloak log lines give the reason. Copy them into an issue (they hold no secrets). |
| `existing realms:` lists a `swoovr-…` realm, then `FAIL ValueError: UAT realms already exist` | An earlier attempt got partway. Reset (below), then look at the first run's failure instead. |
| `Disabled UAT realms and restricted member service client created.` | It worked this time; Keycloak is done. **Don't reset.** Finish the rest of bootstrap with the commands below. |

**If the block ended with `… client created.`**, run bootstrap steps 6–9 yourself, in this order. They are the same commands `swoovr bootstrap` runs after `keycloak-bootstrap`:

```sh
cd /opt/swoovr/swoovr-uat
wait_healthy() { # waits for one service to report healthy; stop with Ctrl-C
  until [ "$(docker inspect -f '{{.State.Health.Status}}' "$(./bin/swoovr compose ps -q "$1")")" = healthy ]; do
    echo "waiting for $1 ..."; sleep 15
  done; echo "$1 healthy"
}

./bin/swoovr zammad-sso                       # 6. staff realm's swoovr-zammad client
./bin/swoovr compose up -d clamav zammad-postgresql zammad-redis zammad-memcached zammad-elasticsearch
./bin/swoovr compose up -d zammad-init
./bin/swoovr compose up -d zammad-railsserver zammad-scheduler zammad-websocket zammad-nginx
wait_healthy zammad-railsserver               # 7. can take up to 15 minutes on first start
./bin/swoovr zammad-provision                 #    writes the four Zammad tokens into env/api.env
./bin/swoovr compose up -d api                # 8. also starts Mailpit
wait_healthy api
./bin/swoovr compose up -d web admin          # 9.
wait_healthy web; wait_healthy admin
./bin/swoovr status                           # health through Caddy
```

Then do **Check the result** (above) and continue with §5.

**For any other failure**, fix the cause, then start again from a clean state. UAT is disposable, and a re-run refuses realms that already exist. `reset` asks you to type `swoovr-uat`, and keeps `env/` and `tls/`:

```sh
./bin/swoovr reset --destroy-uat-data
./bin/swoovr bootstrap
```

## 5. After bootstrap (manual gates; same as DEPLOYMENT-UAT.md)

These gates are the same on Azure and Vultr. The Vultr commands are here, so you don't need [DEPLOYMENT-UAT.md](../../docs/delivery/DEPLOYMENT-UAT.md) to do them; it has the Azure commands and the reasons behind each gate. Do them in this order:

1. Verify the realms, then replace the temporary Keycloak admin with your own admin account (with MFA). Steps: **§5.1** below.
2. Check that the Entra app's Web redirect URI is exactly `https://auth-uat.swoovr.app/realms/swoovr-staff/broker/entra/endpoint` (§3b step 1).
3. Check every provider with real messages, enabling the members and staff realms along the way. Steps: **§5.3** below.
4. Sign the approved staff member in once (§5.3 part G, which also gives you the subject), then grant root. Then return to §5.3 part H:

   ```sh
   ./bin/swoovr job api-command root-admin grant <verified-staff-subject>
   ./bin/swoovr job api-command founders allocate
   ```

5. Run the hosted smoke test (`scripts/uat/smoke.cjs --base-url https://uat.swoovr.app`) and the checks in DEPLOYMENT-UAT.md "Smoke and final admission gate". That includes SignalR, Zammad WebSockets, passkeys and staff helpdesk SSO.

### 5.1 Verify the realms and replace the temporary admin

Bootstrap created a **temporary** Keycloak administrator, `swoovr-bootstrap`, in the `master` realm. Its password is `KC_BOOTSTRAP_ADMIN_PASSWORD` in `env/keycloak.env`, with no MFA. It's for infrastructure setup only. It isn't a Swoovr staff account and has nothing to do with the Swoovr root admin (step 4). Do these four parts **in order**: part A signs in with that temporary admin, and part D deletes it.

**A. Check the three realms (on the host; read-only, changes nothing).** This checks exactly what DEPLOYMENT-UAT.md asks for: all three realms disabled and without `-dev`, the exact callbacks and passkey RP ID, the Entra broker, and the member service client's roles. It prints no passwords, tokens or secrets.

```sh
cd /opt/swoovr/swoovr-uat
./bin/swoovr compose --profile jobs run --rm -T --entrypoint /opt/operator-venv/bin/python keycloak-bootstrap - <<'PY'
import os, sys
sys.path.insert(0, '/opt/uat')
import keycloak_bootstrap as kb
env = os.environ
request = kb.Transport(env['SWOOVR_KEYCLOAK_BASE'])
code, body = request('/realms/master/protocol/openid-connect/token', 'POST', {'client_id': 'admin-cli', 'grant_type': 'password',
    'username': env.get('KC_BOOTSTRAP_ADMIN_USERNAME', 'swoovr-bootstrap'), 'password': env['KC_BOOTSTRAP_ADMIN_PASSWORD']}, form=True)
if code != 200:
    sys.exit(f'FAIL temporary admin sign-in: HTTP {code}. Already deleted (part D)? Then check the realms in the console instead.')
token = body['access_token']
def get(path):
    code, value = request('/admin/realms' + path, token=token)
    if code != 200:
        sys.exit(f'FAIL GET {path}: HTTP {code}')
    return value
failures = 0
def check(ok, text):
    global failures
    print(('PASS ' if ok else 'FAIL ') + text)
    failures += not ok
def client(realm, client_id):
    found = get(f'/{realm}/clients?clientId={client_id}')
    check(len(found) == 1, f'{realm}: client {client_id} exists once')
    return found[0] if len(found) == 1 else {}
realms = sorted(r['realm'] for r in get(''))
check(realms == ['master', 'swoovr-members', 'swoovr-staff', 'swoovr-venues'], f'realms: {", ".join(realms)}')
for name in ['swoovr-members', 'swoovr-venues', 'swoovr-staff']:
    realm = get('/' + name)
    check(realm.get('enabled') is False, f'{name} is disabled')
    check(realm.get('sslRequired') == 'all', f'{name} requires HTTPS for every request')
    people = [u['username'] for u in get(f'/{name}/users?max=10') if not u['username'].startswith('service-account-')]
    check(not people, f'{name} has no user accounts yet {people or ""}')
check(get('/swoovr-members').get('webAuthnPolicyPasswordlessRpId') == 'uat.swoovr.app', 'member passkey RP ID is uat.swoovr.app')
for realm, client_id, origin in [('swoovr-members', 'swoovr-web', 'https://uat.swoovr.app'),
                                 ('swoovr-venues', 'swoovr-venue-web', 'https://venues-uat.swoovr.app'),
                                 ('swoovr-staff', 'swoovr-admin-web', 'https://admin-uat.swoovr.app')]:
    c = client(realm, client_id)
    check(c.get('redirectUris') == [origin + '/signin-oidc'], f'{client_id} sign-in callback {c.get("redirectUris")}')
    check(c.get('attributes', {}).get('post.logout.redirect.uris') == origin + '/signout-callback-oidc', f'{client_id} sign-out callback')
    check(c.get('publicClient') is False and not c.get('directAccessGrantsEnabled') and not c.get('implicitFlowEnabled')
          and not c.get('serviceAccountsEnabled'), f'{client_id}: confidential, sign-in code flow only')
mobile = client('swoovr-members', 'swoovr-mobile')
check(mobile.get('publicClient') is True and not mobile.get('directAccessGrantsEnabled'), f'swoovr-mobile: public PKCE client {mobile.get("redirectUris")}')
zammad = client('swoovr-staff', 'swoovr-zammad')
check(zammad.get('redirectUris') == ['https://helpdesk-uat.swoovr.app/auth/openid_connect/callback'], 'swoovr-zammad helpdesk callback')
api = client('swoovr-members', 'swoovr-api-accounts')
check(api.get('serviceAccountsEnabled') is True and not api.get('standardFlowEnabled') and not api.get('directAccessGrantsEnabled')
      and api.get('fullScopeAllowed') is False, 'swoovr-api-accounts: service account only, no full scope')
if api:
    management = client('swoovr-members', 'realm-management')
    user = get(f"/swoovr-members/clients/{api['id']}/service-account-user")
    roles = sorted(r['name'] for r in get(f"/swoovr-members/users/{user['id']}/role-mappings/clients/{management['id']}"))
    check(roles == ['manage-users', 'view-users'], f'swoovr-api-accounts roles {roles}')
    scope = sorted(r['name'] for r in get(f"/swoovr-members/clients/{api['id']}/scope-mappings/clients/{management['id']}"))
    check(scope == ['manage-users', 'view-users'], f'swoovr-api-accounts scope {scope}')
entra = get('/swoovr-staff/identity-provider/instances/entra')
issuer = entra.get('config', {}).get('issuer', '')
check(entra.get('trustEmail') is False and entra.get('storeToken') is False, 'Entra broker: trustEmail and storeToken off')
check(issuer.startswith('https://login.microsoftonline.com/') and issuer.endswith('/v2.0')
      and not any(word in issuer for word in ('/common/', '/organizations/')), f'Entra issuer {issuer}')
print(f"INFO Entra client ID {entra.get('config', {}).get('clientId')}: compare both IDs with the app's Overview page (§3b)")
print('INFO master realm users: ' + ', '.join(u['username'] for u in get('/master/users?max=20')))
sys.exit(1 if failures else 0)
PY
```

Every line must start `PASS` or `INFO`. Compare the tenant ID in the issuer URL and the Entra client ID with the app's **Overview** page in Entra (§3b step 1.3). `master realm users` should be just `swoovr-bootstrap`. On any `FAIL`, stop and open an issue with the output (it holds no secrets).

**B. Open the Keycloak admin console (on your machine).** The console answers only from inside the VM, so you reach it through an SSH tunnel and a browser profile used only for this. Every `.app` domain is on the browsers' HSTS preload list, so a certificate warning on `auth-uat.swoovr.app` usually can't be clicked through. Instead, the profile trusts Cloudflare's Origin CA root, which signed the UAT origin certificate.

1. Download that root and check it's the same one `tls-setup` pinned:

   ```sh
   mkdir -p -m 700 ~/swoovr-uat-private
   curl -fsSL https://developers.cloudflare.com/ssl/static/origin_ca_rsa_root.pem -o ~/swoovr-uat-private/origin_ca_rsa_root.pem
   openssl x509 -in ~/swoovr-uat-private/origin_ca_rsa_root.pem -noout -fingerprint -sha256
   # must print: sha256 Fingerprint=D3:C7:E8:5C:91:70:7F:C0:A1:2A:BC:5D:88:26:67:47:AA:4F:A8:E7:B1:62:F6:33:FF:B3:C9:D9:89:94:76:20
   ```

   If the fingerprint is different, stop: don't import it.
2. Open the tunnel and leave that terminal open while you use the console:

   ```sh
   ssh -D 1080 -N root@<VM IPv4>      # prints nothing while it runs; Ctrl-C closes it
   ```

3. Make a separate **Firefox** profile, so the extra trust and the proxy stay out of your normal browsing. Open `about:profiles` → **Create a New Profile** → name it `swoovr-uat-admin` → **Launch profile in new browser**. Do the rest **in that new window**. (To check: `about:profiles` shows **This is the profile in use** under `swoovr-uat-admin`.)
   - **Proxy.** Open Settings: **⌘,**, or **Firefox** → **Settings…** in the macOS menu bar. Settings opens as a tab. Type `proxy` in **Find in Settings** (top right); without the search, it's at the very bottom of **General**. Under **Network Settings**, click **Settings…**. In **Connection Settings**:
     - Choose **Manual proxy configuration**.
     - **HTTP Proxy:** leave it empty, and leave **Also use this proxy for HTTPS** unticked.
     - **SOCKS Host:** `127.0.0.1`, **Port:** `1080`, **SOCKS v5**.
     - Near the bottom, tick **Proxy DNS when using SOCKS v5**.
     - **OK**.
   - **Certificate.** In the same Settings tab, type `certificates` in **Find in Settings** → **View Certificates…** → **Authorities** tab → **Import…**. Choose `~/swoovr-uat-private/origin_ca_rsa_root.pem` (press ⌘⇧G in the file dialog to type the path). Tick **Trust this CA to identify websites** → **OK** → **OK**.
4. In that profile, open `https://auth-uat.swoovr.app/admin/`. You should get the Keycloak sign-in page with no certificate warning. If you get a `404`, the request didn't go through the tunnel: check the proxy settings, including **Proxy DNS**.
5. Sign in as `swoovr-bootstrap`. For the password, open `env/keycloak.env` on the host in an editor and copy the `KC_BOOTSTRAP_ADMIN_PASSWORD` value. Keycloak shows a banner saying this is a temporary admin account; that's expected.

**C. Create your own admin account** (the realm selector at the top left must show **master**):

1. **Users** → **Add user** (or **Create new user**):
   - **Required user actions:** **Configure OTP**.
   - **Email verified:** On.
   - **Username:** your own name. Not `admin`, and not shared with anyone.
   - **Email, First name, Last name:** yours.
   - **Create.**
2. **Credentials** tab → **Set password**: a new, long password from your password manager. Set **Temporary** to **Off**, then **Save**.
3. **Role mapping** tab → **Assign role** → set the filter to **Filter by realm roles** → tick **`admin`** → **Assign**.
4. Sign out (top right). Sign in again as the new user. Scan the QR code with your authenticator app and type the code to finish. Every later sign-in asks for that code.

**D. Delete the temporary admin.** Signed in as your new user: **Users** → `swoovr-bootstrap` → **Action** (top right) → **Delete** → confirm. Then:

1. Sign out (top right).
2. Sign in again with your new username, password and authenticator code. If that fails, no Keycloak administrator can sign in any more. UAT is disposable, so recover with `./bin/swoovr reset --destroy-uat-data` and `./bin/swoovr bootstrap`, then do §5.1 again from part A.
3. Open **Users** in the **master** realm. The list must show **only your account**. The yellow temporary-admin banner must be gone.

**E. Close the console.**

1. Close the `swoovr-uat-admin` Firefox window.
2. In the terminal running `ssh -D 1080 -N …`, press **Ctrl-C**.

Use the `swoovr-uat-admin` profile only for this console. Never use it for normal browsing: it trusts the Cloudflare Origin CA and sends everything through the VM.

**F. What changes now that the temporary admin is gone.**

| Item | From now on |
| --- | --- |
| Keycloak console | Sign in with **your** account and authenticator code (part H). Use it for all console work, including enabling the realms in §5 step 3. |
| `KC_BOOTSTRAP_ADMIN_PASSWORD` in `env/keycloak.env` | **Don't edit or delete it.** Keycloak reads it only when its database is empty, which happens only after **Reset UAT**. `swoovr check` also expects it to be filled. |
| `./bin/swoovr zammad-sso`, `./bin/swoovr job keycloak-bootstrap`, and part A of this section | These now fail with `HTTP 401`. That's correct: they sign in as the deleted temporary admin and are needed only during a fresh bootstrap. Don't run them on this host. |
| After **Reset UAT** and a new `./bin/swoovr bootstrap` | Keycloak starts with an empty database and creates `swoovr-bootstrap` again. Do §5.1 again from part A: the account you made in part C is gone with the old database. |

**G. Check that the temporary admin stays deleted.** Do this every time Keycloak restarts: after `./bin/swoovr mail-apply`, `./bin/swoovr upgrade`, `./bin/swoovr rollback`, or a VM reboot.

1. Open the console (part H).
2. Choose the **master** realm → **Users**.
3. The list must show only your account. If `swoovr-bootstrap` is back, delete it as in part D, and record in an issue which command restarted Keycloak.

**H. Open the Keycloak console again later.** The console isn't public: Caddy answers `/admin` only to callers inside the VM, and the VM resolves `auth-uat.swoovr.app` to Caddy's private address. Each time:

1. On your machine, start the tunnel and leave the terminal open:

   ```sh
   ssh -D 1080 -N root@<VM IPv4>
   ```

2. Start the `swoovr-uat-admin` Firefox profile: open `about:profiles` in any Firefox window, then **Launch profile in new browser** under `swoovr-uat-admin`. The proxy and certificate settings from part B are already saved in it.
3. Open `https://auth-uat.swoovr.app/admin/` and sign in with your account and authenticator code.
4. When you're finished, close the console as in part E.

**I. Read SMS codes and test emails in Mailpit.** While `SWOOVR_SMS_SERVICE=mailpit` (in `env/mail.env`), every SMS goes to Mailpit instead of a phone. So does API email, if you set `SWOOVR_API_MAIL_SERVICE=mailpit`. Nothing in Mailpit reaches anyone.

1. On your machine, start a tunnel to Mailpit's web page and leave the terminal open:

   ```sh
   ssh -L 8025:127.0.0.1:8025 -N root@<VM IPv4>
   ```

2. Open `http://127.0.0.1:8025` in any browser on your machine.
3. Each SMS appears as an email to `sms-<phone number>@simulated.swoovr.invalid`, marked **SIMULATED**. Open it to read the code.
4. When you're finished, press **Ctrl-C** in the tunnel's terminal.

### 5.3 Provider checks and enabling the realms

**Order: DEFAULT, for the owner to confirm.** DEPLOYMENT-UAT.md says to enable a realm only after its checks pass. But the email, phone and Entra checks need a real sign-in, and no one can sign in to a disabled realm. So this section enables each realm just before the check that needs it. That's low-risk because:

- sign-up on UAT starts **invite-only** (D295): only addresses added with `beta add` can register;
- the staff realm accepts only the Entra users assigned to the app (§3b step 1.6).

**`swoovr-venues` stays disabled.** No venue portal runs on UAT (`venues-uat` isn't served).

Do the parts in order. When a part fails, use part J, and don't continue until it passes.

**A. Health checks (on your machine).**

```sh
curl -s https://api-uat.swoovr.app/health/ready; echo           # must be {"status":"ready"}
curl -s https://api-uat.swoovr.app/health/attachments; echo     # must show "status":"healthy" (ClamAV answers)
```

**B. Startup logs (on the host).** The API and Keycloak refuse to start when a selected provider's settings are incomplete. Both are running, so their settings are complete. This checks they logged no provider errors:

```sh
cd /opt/swoovr/swoovr-uat
./bin/swoovr logs api 2>&1 | grep -iE 'fail|error|refus' | tail -20
./bin/swoovr logs keycloak 2>&1 | grep -iE 'error|swoovr-email' | tail -20
```

No output, or only lines you can explain, is a pass. Copy anything else into an issue: these logs hold fixed outcomes, not addresses or message content.

**C. Provider consoles (in each provider's website).** These are settings only the provider shows:

1. **Didit:** in the Didit console, open the application whose workflow ID is `Didit__WorkflowId` in `env/api.env`. Set the webhook URL to exactly `https://api-uat.swoovr.app/api/webhooks/didit`. Its webhook secret must be the same value as `Didit__WebhookSecret` in `env/api.env`.
2. **Twilio Email and SendGrid:** in each console, the sender `hello@repasscloud.com` (its domain, `repasscloud.com`) must show as **authenticated** or **verified** (§0 step 4).
3. **Cloudflare R2:** the bucket in `Media__R2__Bucket` must have **public access off** (R2 → the bucket → Settings). The API token must have **Object Read & Write** on that bucket only.

**D. Enable the members realm** (Keycloak console, §5.1 part H):

1. Realm selector (top left) → **swoovr-members**.
2. **Realm settings** → **General** tab → **Enabled**: On → **Save**.
3. Leave the console open for part G.

**E. Admit one test address and sign up (one person, an inbox you own).**

1. On the host, add your test address to the beta list. It prints counts only, never the address:

   ```sh
   ./bin/swoovr job api-command beta add <your test email>
   # expect: Beta enrolments: 1 added, 0 already enrolled, 0 invalid.
   ```

2. In a normal browser (not the `swoovr-uat-admin` profile), open `https://uat.swoovr.app` and sign up with that address. Accept the 18+ terms.
3. **Keycloak email (Twilio Email):** a verification email arrives in that inbox. Open its link. If nothing arrives within 5 minutes, use part J.
4. **SMS code (Mailpit):** add your phone number when the app asks. The code goes to Mailpit, not your phone (§5.1 part I). Read it there and type it in.
5. **API email (Twilio Email):** trigger one email from the API, for example by changing a security setting under **Account settings → Security & access** (it sends a security notice). Check it arrives.
6. **Media (R2 + screening):** upload one profile photo. It should appear once screening has checked it. If it's still pending after 10 minutes, use part J.
7. **Didit (optional; uses a real identity document):** start ID verification from the app and finish it with your own document. The app should then show the result. That proves the Didit workflow and its webhook (part C1). This is a real, billed check.

**F. Enable the staff realm** (Keycloak console): realm selector → **swoovr-staff** → **Realm settings** → **General** → **Enabled**: On → **Save**.

**G. First staff sign-in (Entra) and your staff subject.** The root grant in §5 step 4 needs this.

1. Make sure your Microsoft account is assigned to the Entra app (§3b step 1.6). Unassigned accounts are refused by Microsoft.
2. In a normal browser, open `https://admin-uat.swoovr.app` and sign in with Microsoft. If Keycloak asks you to verify your email address, use the link it emails you. Admin may then say you have no access yet; that's expected before the root grant.
3. In the Keycloak console: **swoovr-staff** → **Users** → your user → copy the **ID** at the top (a UUID). That ID is `<verified-staff-subject>` in §5 step 4.
4. Now do **§5 step 4** (root grant), then come back for part H.

**H. Helpdesk (Zammad) and its email (SendGrid).** After §5 step 4 has made you root admin:

1. Open `https://helpdesk-uat.swoovr.app` and choose **Swoovr staff sign-in**. You should get in through the same Microsoft sign-in.
2. **SendGrid:** in a private window, open `https://uat.swoovr.app/support` and send a support request. Use a different address from your staff one. A Zammad notification email should arrive at your staff address.
3. **IMAP (only if `SWOOVR_ZAMMAD_IMAP_HOST` is set in `env/mail.env`):** send an email to `SWOOVR_ZAMMAD_SUPPORT_ADDRESS`. Within a few minutes it should appear as a ticket in **General Support**.

**I. Record the result.** For each of A–H, note in the UAT issue: pass or fail, the date, and for messages "arrived" or "accepted by provider but not arrived". Never paste addresses, codes, links or message content.

**J. When a check fails or a message doesn't arrive.**

| What failed | Where to look |
| --- | --- |
| Keycloak verification or reset email | `./bin/swoovr logs keycloak 2>&1 \| tail -50`, then the Twilio console's email activity. Check the sender domain is authenticated (C2). |
| API email | `./bin/swoovr logs api 2>&1 \| tail -50`, then the Twilio console. Check `SWOOVR_API_MAIL_SERVICE` in `env/mail.env`. |
| Zammad notification | In Zammad, your avatar → **Profile** → **Notifications**: new tickets in **General Support** must be on. Then the SendGrid console's activity feed. Check the `apikey` username and Mail Send key in `env/mail.env`, then `./bin/swoovr mail-apply`. |
| SMS code not in Mailpit | `SWOOVR_SMS_SERVICE=mailpit` in `env/mail.env`; `./bin/swoovr status` shows `mailpit` running. |
| Sign-up refused ("not invited") | Part E1 again, with exactly the address you're signing up with. |
| Microsoft refuses the staff sign-in | The account isn't assigned to the Entra app (§3b step 1.6), or the client secret in `env/realms.env` expired or was mistyped. |
| Photo stays pending, or Didit shows no result | `./bin/swoovr logs api 2>&1 \| grep -iE 'media\|screen\|didit' \| tail -30`, then part C1 and C3. |

After changing anything in `env/mail.env`, run `./bin/swoovr mail-apply` and repeat the failed check. After changing `env/api.env`, run `./bin/swoovr compose up -d --force-recreate api`.

## Day-2 operations

| Task | Command |
| --- | --- |
| Change the sender, a mail service, credentials or the IMAP mailbox | Edit `env/mail.env`, then `./bin/swoovr mail-apply`. It checks the file, starts Mailpit, recreates Keycloak and the API, and re-applies Zammad's relay, sender and IMAP mailbox (existing tokens are reused). Then send one of each: a Keycloak verification email, an API email and a Zammad notification. |
| Switch SMS from Mailpit to Twilio | Fill `Sms__Twilio__AccountSid`, `Sms__Twilio__AuthToken` and `Sms__Twilio__From` in `env/api.env`, set `SWOOVR_SMS_SERVICE=twilio` in `env/mail.env`, then `./bin/swoovr mail-apply`. `swoovr check` refuses the switch while those values are empty. |
| Read SMS codes | Mailpit over the SSH tunnel (§5.1 part I). |
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
