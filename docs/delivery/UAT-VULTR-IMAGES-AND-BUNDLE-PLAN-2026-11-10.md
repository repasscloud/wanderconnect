# Multi-arch images and the Vultr UAT bundle (plan, 10 November 2026)

This plan covers three things. It builds every Swoovr image once per commit as `repasscloud/{image}:{commit}` for linux/amd64 and linux/arm64 on GitHub's Ubuntu runners. It ships one deployment bundle (a single Compose file plus all configuration) to the Vultr UAT VM (D361). It promotes the **same image digests** to Azure for production. It builds on the [Vultr stack review](VULTR-STACK-REVIEW-2026-11-10.md) (single host, single Caddy, separate Zammad PostgreSQL) and D355 hostnames. Decision records: D356 (DEFAULT), D357 (Zammad mail), D358 (one mail file and sender), D359 (workflow enabled), D360 (email and SMS providers), D361 (one Vultr VM). Questions: Q175, Q176.

**Delivered in this PR:**

- The workflow `.github/workflows/images.yml`. Enabled for manual runs on the owner's request (D359); the repository is public, so it uses no paid minutes.
- The bundle `deploy/vultr-uat/` with its [runbook](../../deploy/vultr-uat/README.md).
- A backward-compatible parameter in `containers/zammad/provision.rb` (webhook endpoint and notification SMTP).

Nothing was published, provisioned or deployed.

## Decisions

| Topic | Status | Choice |
| --- | --- | --- |
| UAT host | CONFIRMED (owner, D361, 11 November 2026) | One Vultr **cloud VM** (DEFAULT 8 vCPU / 32 GB / NVMe, Sydney, Debian 13), Docker; one Caddy for all hostnames. [VULTR-VM.md](../../deploy/vultr-uat/VULTR-VM.md). Replaces the earlier bare-metal E-2286G / RAID 1 choice. |
| PROD | CONFIRMED (owner, D355) | Azure (Container Apps, PostgreSQL Flexible Server, Key Vault). Existing `infra/uat` Bicep is the template. |
| Registry and names | CONFIRMED (owner request) | Docker Hub `repasscloud/swoovr-api`, `-web`, `-admin`, `-operator`, `-keycloak`, tag = full 40-character commit SHA. DEFAULT: private repositories; no mutable tags. |
| Architectures | CONFIRMED (owner request) | One OCI index per image with linux/amd64 + linux/arm64, built natively on `ubuntu-24.04` and `ubuntu-24.04-arm`. |
| Deploy reference | DEFAULT | Servers and Azure use `repo@sha256:<index digest>` from `release-manifest.env`. The tag exists for people, not for deployment. |
| Build once, promote | DEFAULT | Production imports the UAT-tested digests into ACR (`az acr import`); it never rebuilds them. Keycloak is the exception (see below). |
| Swoovr/Keycloak PostgreSQL image | DEFAULT (was OPEN) | `postgres:17.11-trixie` @ `sha256:ff87138949ad9a46f32ff23bd6852b52ce024a13dffe9b4087be98292cdd3b4f`. Debian 13 index (amd64/arm64), digest checked 10 November 2026. TLS-only (`hostssl` + SCRAM; plaintext rejected) with a private CA, so `VerifyFull` is the same contract as Azure. |
| Internal hostname resolution | DEFAULT (was OPEN) | Caddy has a fixed address 172.30.0.2 on the `edge` network and Docker network aliases for the five public names. Containers calling `https://auth-uat.swoovr.app` etc. reach Caddy directly with the right Host and SNI. There is no hairpin through the public IP. Only 172.30.0.2/32 is a trusted proxy for the apps and Keycloak. |
| Origin TLS | CONFIRMED (owner [#307](https://github.com/repasscloud/wanderconnect/issues/307)), **Q175** | Cloudflare Origin CA, PEM, RSA 2048, 15 years, explicit SANs for the six UAT hosts, no wildcard. The owner provisions `/etc/caddy/certs/uat-origin.pem` and `.key` on the host outside Git; stock Caddy mounts them read-only. `bin/origin-cert-check` gates every Caddy start. No ACME, no Cloudflare API token on the host. System CA trust is preserved: only API, Web, Admin, the `api-command` and Keycloak bootstrap jobs, and Zammad Rails/scheduler get `SSL_CERT_FILE=origin-trust.pem` (system CAs + Origin CA roots); Keycloak adds the roots with `KC_TRUSTSTORE_PATHS`. Database clients use the private PostgreSQL CA only. The UAT certificate is independent of production. |
| Email and SMS | CONFIRMED (owner, D360, #316) | API and Keycloak: Twilio Email API. Zammad: SendGrid SMTP out, IMAP in (General Support). SMS: Mailpit until Twilio SMS verification, then Twilio. All in `env/mail.env`. |
| Helpdesk hostname and Zammad mail | CONFIRMED (owner, D355 and D357; mail superseded by D360), **Q176** | `helpdesk-uat.swoovr.app`. Staff notifications go through MailerSend SMTP (`smtp.mailersend.net:587`, STARTTLS) as `Swoovr Helpdesk <hello@repasscloud.com>`. Every mail setting for the API, Keycloak and Zammad is in `env/mail.env` (D358). |
| Backups | CONFIRMED (owner) | UAT is disposable: rebuild instead of restore. Zammad's local backup container is off by default (`--profile backup`). |

## 1. Images and what each one is for

| Image | Dockerfile / target | Runs on UAT as | Runs on PROD as |
| --- | --- | --- | --- |
| `swoovr-api` | `containers/uat/Dockerfile` / `api` | `api` service, plus `migrate` and `api-command` jobs | Container App + jobs |
| `swoovr-web` | same / `web` | `web` | Container App |
| `swoovr-admin` | same / `admin` | `admin` | Container App |
| `swoovr-operator` | same / `operator` | `db-bootstrap`, `places-import`, `keycloak-bootstrap` jobs | Container Apps jobs (including Azure backups) |
| `swoovr-keycloak` | `containers/uat/Keycloak.Dockerfile` / `keycloak` | `keycloak` | **Separate production build** (below) |

The .NET stages already cross-publish from `$BUILDPLATFORM` with `-a $TARGETARCH`. The base images (dotnet aspnet 10.0, Keycloak 26.8.1, Temurin 21) and every third-party pin in [thirdparty.env](../../deploy/vultr-uat/thirdparty.env) were checked as amd64 + arm64 indexes. The operator's `postgresql-client-17` pin targets Ubuntu noble pgdg, which publishes both architectures. The first arm64 build confirms this.

**Keycloak image is environment-specific.** `Keycloak.Dockerfile` rewrites the theme's privacy, terms and community links to `https://uat.swoovr.app/...` at build time. Production must not reuse that digest. Follow-up (needs its own PR and image tests): add a `SWOOVR_PUBLIC_ORIGIN` build argument (default `https://uat.swoovr.app`). Then the workflow builds `swoovr-keycloak` with the UAT origin and production with `https://swoovr.app`, published under a separate tag such as `repasscloud/swoovr-keycloak:<commit>-prod`. Until then, `promote-to-azure` deliberately skips Keycloak.

## 2. GitHub pipeline (`.github/workflows/images.yml`)

The workflow has three jobs:

1. **build**: a matrix of 5 components × 2 architectures. Each cell runs on its native Ubuntu runner, so there's no QEMU. Each cell runs `docker/build-push-action` with `push-by-digest`, SBOM and max provenance, and a GitHub Actions cache per cell. Build arguments are only `SWOOVR_VERSION` (D320); there are no secrets.
2. **merge**: runs `docker buildx imagetools create` once per image, tagging `:<commit>` from the two per-architecture digests. It refuses an index that lacks either platform. It writes `release-manifest.env` and `release-manifest.json`, then runs `deploy/vultr-uat/bin/make-bundle.sh`. Artifact: `swoovr-release-<commit>`, containing the manifest and `swoovr-uat-bundle-<commit>.tar.gz`.
3. **promote-to-azure**: optional. It runs only with the dispatch input and approval of the `production` GitHub environment. It uses an OIDC `azure/login` (federated credential, no stored Azure secret) and runs `az acr import` per digest into the production ACR.

Owner steps (the file is enabled in D359; steps 3 and 4 are done):

1. Create the private Docker Hub repositories `repasscloud/swoovr-{api,web,admin,operator,keycloak}`. Then create an access token with Read & Write on those repositories only.
2. Add the repository secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`. For production, also add the variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `AZURE_ACR_NAME`, and a `production` environment with required reviewers.
3. Done (D359): the file is uncommented and listed in `ENABLED_WORKFLOWS` in `scripts/check_design.py`.
4. Done: the repository is public, and GitHub's `ubuntu-24.04-arm` runners are available to public repositories. If that changes, set both matrix runners to `ubuntu-24.04` and add `docker/setup-qemu-action@v3` before Buildx. That works, but is slower.
5. Run it with **Actions → Images → Run workflow** on the reviewed commit of `main`.

Trigger: `workflow_dispatch` only, so minutes are spent only when someone asks for a release.

### Building without Actions (same result, from a workstation)

```sh
docker login                      # repasscloud account
docker buildx create --use --name swoovr-multi   # once; Docker Desktop provides QEMU for the other arch
SHA=$(git rev-parse HEAD)         # clean, reviewed checkout of main
for c in api web admin operator; do
  docker buildx build --platform linux/amd64,linux/arm64 -f containers/uat/Dockerfile --target "$c" \
    --provenance=mode=max --sbom=true -t "repasscloud/swoovr-$c:$SHA" --push .
done
docker buildx build --platform linux/amd64,linux/arm64 -f containers/uat/Keycloak.Dockerfile --target keycloak \
  --provenance=mode=max --sbom=true -t "repasscloud/swoovr-keycloak:$SHA" --push .
{ echo "SWOOVR_COMMIT=$SHA"; for c in api web admin operator keycloak; do
    echo "SWOOVR_$(echo $c | tr a-z A-Z)_IMAGE=docker.io/repasscloud/swoovr-$c@$(docker buildx imagetools inspect repasscloud/swoovr-$c:$SHA --format '{{json .Manifest.Digest}}' | tr -d '"')"
  done; } > release-manifest.env
deploy/vultr-uat/bin/make-bundle.sh release-manifest.env dist
```

## 3. The Vultr host bundle (`deploy/vultr-uat/`)

There is one `compose.yaml` for the whole stack, with every image from `release.env` by digest. The full operator procedure is in the [runbook](../../deploy/vultr-uat/README.md):

1. Host setup.
2. Origin certificate, TLS setup and the Cloudflare filter.
3. Generated and owner-filled environment files, plus the disabled realm templates.
4. `bin/swoovr bootstrap`.
5. The manual identity, provider and admission gates from DEPLOYMENT-UAT.md.
6. Upgrade, rollback and reset.

```text
Cloudflare (Full strict, proxied) ─▶ host :80/:443 (DOCKER-USER: Cloudflare ranges only)
                                    └▶ caddy 172.30.0.2  [aliases: uat / api-uat / admin-uat / auth-uat / helpdesk-uat]
edge (egress)     caddy, web, admin, api, keycloak, zammad-nginx/rails/scheduler, jobs needing egress
data (internal)   swoovr-postgres ◀─ TLS VerifyFull ─ api, keycloak, db-bootstrap, migrate, places-import, api-command
ticketing (int.)  api ─▶ swoovr-zammad:8080 (Zammad nginx), api ─▶ clamav:3310
zammad (internal) zammad-postgresql, redis, memcached, elasticsearch + Zammad services
egress            clamav signature updates
```

Configuration contract: the environment-variable names in `env/*.env.example` are exactly the Azure `infra/uat/secret-bindings.json` names. The non-secret values in `compose.yaml` mirror `infra/uat/services.bicep`. One application configuration therefore serves UAT on Vultr and PROD on Azure, and only the delivery mechanism differs (an env file with mode 0600 versus a Key Vault reference). Database roles are created by the same `database-bootstrap` code as on Azure.

Security choices:

- Only Caddy publishes ports, and only to Cloudflare ranges.
- Keycloak `/admin` and the master realm are refused unless the caller is on the edge network. The operator reaches them through an SSH SOCKS tunnel.
- Management port 9000, the databases, caches, Elasticsearch and ClamAV are never routed.
- PostgreSQL rejects non-TLS network logins.
- The host firewall lives in its own nftables table, so Docker's rules are untouched.

## 4. Production on Azure (kept, not replaced)

Production keeps the Azure design in [DEPLOYMENT-UAT.md](DEPLOYMENT-UAT.md) and [INFRASTRUCTURE-UAT.md](INFRASTRUCTURE-UAT.md). The steps:

1. **Infrastructure:** copy the `infra/uat` stages (foundation → services → edge) to production parameters, either as `infra/prod` or as parameterised prefixes. This is a separate PR. Use a separate subscription or resource group, production hostnames (`swoovr.app`, `api.swoovr.app`, `admin.swoovr.app`, `auth.swoovr.app`; OPEN until confirmed), production Entra and providers, and the D261 backup jobs, which do apply to production.
2. **Images:** promote by digest with the workflow's `promote-to-azure` job, or by hand:

   ```sh
   az acr import --name <prodAcr> --source docker.io/repasscloud/swoovr-api@sha256:<digest> --image swoovr-api:<commit> \
     --username <dockerhub-user> --password <token>
   ```

   ACR keeps the multi-arch index, and Container Apps pulls its linux/amd64 entry. `services.parameters.json` then references `<prodAcr>.azurecr.io/swoovr-api@sha256:<same digest>`, and `scripts/uat/preflight.py` rejects tags. This keeps the UAT-to-PROD chain intact: the image tested on Vultr is byte-for-byte the image in production.
3. **Keycloak:** a production-origin build, as described in §1.
4. **Configuration:** the same environment names, sourced from Key Vault through `secret-bindings.json`. Production keys and secrets are never copied from UAT.
5. **Zammad in production:** hosting is OPEN, and it isn't part of the current Azure Bicep. Either run this same Zammad Compose section on a production VM, or use Zammad's hosted service. This is to be decided before production support opens.

## 5. Order of work (checklist)

| # | Step | Who | State |
| --- | --- | --- | --- |
| 1 | Review and merge this PR | Owner | Open |
| 2 | Q175 origin TLS (answered #307); Q176 helpdesk name and Zammad SMTP (answered #308, D357) | Owner | Answered |
| 3 | Docker Hub repositories and token; enable the workflow, or build locally (§2). A second, read-only token for the host's `docker login` (runbook §1) | Owner | Open |
| 4 | First image run on `main`; record the five index digests in the PR or issue | Owner / builder | Open |
| 5 | Create the Vultr VM ([VULTR-VM.md](../../deploy/vultr-uat/VULTR-VM.md)); run `host-setup` | Owner | Open |
| 6 | Cloudflare DNS (proxied), Full (strict), cache bypass; Origin CA certificate (generated, #307) placed in `/etc/caddy/certs` | Owner | Certificate generated; rest open |
| 7 | `tls-setup`, `cloudflare-ips`, `envtool.py init`, fill provider values, realm templates | Owner (private values) | Open |
| 8 | `bin/swoovr bootstrap` | Owner / operator | Open |
| 9 | DEPLOYMENT-UAT.md identity, provider and admission gates; hosted smoke; measure RAM, CPU and I/O | Owner + builder | Open |
| 10 | Keycloak production-origin build argument (follow-up PR) | Builder | Open |
| 11 | `infra/prod` Bicep and the production ACR import | Builder → owner deploys | Open |

## Evidence (10 November 2026, local only)

- `docker compose config -q` passes with all profiles. The test used generated env files and digest-shaped placeholders.
- `bin/tls-setup` ran in `debian:trixie`. It created the PostgreSQL CA and server certificate, fetched both Cloudflare Origin CA roots and printed their fingerprints (RSA `D3:C7:E8:5C…`, ECC `AA:63:69:7A…`), and built the trust bundle. After #307, `bin/origin-cert-check` accepted a self-signed RSA 2048 test certificate with the six SANs and refused wildcard, ECDSA, RSA 4096, missing-SAN, mismatched-key, group-readable-key and missing-file cases (`debian:trixie`). `caddy validate` accepted the Caddyfile with that test certificate at `/etc/caddy/certs`.
- Real `postgres:17.11-trixie` started with the bundle's `pg_hba.conf`, SSL and initdb. It created the `keycloak` and `swoovr` databases. A `verify-full` client connected over TLS 1.3, and a plaintext login was rejected by `pg_hba`.
- The unmodified `scripts/uat/uat_jobs.py database-bootstrap` ran against that container with generated passwords. It created the roles; `swoovr` is owned by `swoovr_migrator`; `swoovr_api` can log in with `VerifyFull` and has no database CREATE privilege.
- `caddy validate` with the owner-selected Caddy digest reports "Valid configuration". The registry digest equals the owner's pin (multi-arch).
- `shellcheck -S warning` is clean for all bundle scripts. `ruby -c` passes for `provision.rb`.
- `make-bundle.sh` produced a tarball with no env values, TLS files or caches.
- Builder security review of #309 (10 November 2026), each checked locally:
  - **Client address chain.** The pinned Caddy with an echo upstream showed the previous Caddyfile sending `198.51.100.7, 172.30.0.100` (client plus Cloudflare edge) and the Web BFF's own address to the API. The apps trust only Caddy, so they would have keyed per-address limits (phone codes, meeting-safety, MFA) on the Cloudflare edge or BFF. Now each upstream receives one value: the Cloudflare client, or the browser address forwarded by Web/Admin at fixed trusted addresses. A container at another address that sends spoofed `X-Forwarded-For` and `CF-Connecting-IP` is logged as itself.
  - `tls-setup` pins the Cloudflare Origin CA root SHA-256 fingerprints and uses a private temporary directory instead of fixed `/tmp` names.
  - `swoovr upgrade` restores `release.env` if the new release fails `swoovr check` (previously it stayed switched).
  - `host-setup` splits IPv4 and IPv6 SSH CIDRs into `ip`/`ip6` rules and rejects anything else; `nft -c` accepts the generated table.
  - `envtool.py check` refuses an unquoted `$` in an env value, which Compose would interpolate.
  - The image workflow pins every action to a commit SHA, disables checkout credential persistence and validates the `version` input. It stays fully commented.
  - Runbook: no `AAAA` records, because the Docker networks are IPv4-only.
- Capability-token redaction in Caddy logs (Codex on #309; MEETING-SAFETY-IMPLEMENTATION.md): the pinned Caddy ran the bundle Caddyfile with a test certificate. Requests to `/safety/meeting/<token>`, `/safety/contact/<token>`, a Keycloak `action-token?key=` URL and an OIDC callback with `code`/`state`/`session_state` were logged as `[redacted]` in both the access log and the error log (proxy failures). The Referer header was dropped, and a fake token appeared 0 times in all Caddy output. Cloudflare-side logs are separate and not configured by this bundle.
- Codex review fixes on #309: `bin/cloudflare-ips --apply-cached` ran in a `debian:trixie` container with `NET_ADMIN` and a stub `DOCKER-USER` chain. It removed the earlier unscoped jump, added only `-i eth0` jumps for 80/443 (IPv4 and IPv6), stayed idempotent on a second run, and refused an unknown interface. The .NET runtime image (`aspnet:10.0` digest, Ubuntu 24.04) has `/bin/sh` → dash and Bash 5.2, so the health probes now call Bash explicitly and send each service's `AllowedHosts` name.

**Not verified:**

- The workflow has not run, because Actions minutes are off. No image was built for arm64 or published.
- The API, Web, Admin, Keycloak and Zammad containers were not started from this bundle.
- `keycloak-bootstrap`, `zammad-sso` and `zammad-provision` were not run against the hosted layout.
- No Vultr host, DNS, Cloudflare, Entra or real provider was touched.
- `bin/host-setup` was not run, and `bin/cloudflare-ips` ran only against a stub chain, not real Docker forwarding. Container egress under the scoped rule and the health probes against running app images are not verified.
