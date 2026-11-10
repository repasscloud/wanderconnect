# Swoovr Vultr stack review

**Follow-up (D356, 10 November 2026):** the deployment bundle, multi-arch image pipeline and Azure promotion path are in [UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md](UAT-VULTR-IMAGES-AND-BUNDLE-PLAN-2026-11-10.md) and [deploy/vultr-uat](../../deploy/vultr-uat/README.md). They set DEFAULTs for the Debian PostgreSQL image and the internal hostname method below; Q175/Q176 remain open.

The repository contains the pieces to build the Swoovr application images, but it does **not yet contain a complete Vultr deployment configuration**. Zammad is a separate multi-container stack that must be included alongside Swoovr. The Cloudflare image services remain external dependencies; their live state and compatibility with a future UAT deployment were not inspected in this review.

**CONFIRMED by owner in this conversation:** UAT is also used for development testing, contains no real public client data, and can be discarded and rebuilt with fresh data regularly. UAT data is not refreshed into production. For this environment, reproducible setup and a documented reset are higher priorities than preserving every UAT record. Rebuilds still need to recreate the separate databases, roles, migrations, Keycloak configuration, Zammad provisioning and private provider configuration in the correct order. This does not establish a production backup policy or change existing retention decisions for environments holding real data.

Locally, the API reaches Zammad at `http://swoovr-zammad:8080/` over the `swoovr-ticketing` Docker network. Zammad reaches the local API at `swoovr-platform:5080`. The local Keycloak forwarder is a separate workaround for localhost identity URLs. These details are documented in the [Zammad container README](../../containers/zammad/README.md).

## Images to publish and run

| Component | Image source | Deployment role |
| --- | --- | --- |
| Swoovr API | Build `api` target from [UAT Dockerfile](../../containers/uat/Dockerfile) | API, background workers and one-shot migration/admin commands |
| Swoovr Web | Build `web` target from the same Dockerfile | Member application and BFF |
| Swoovr Admin | Build `admin` target | Staff administration |
| Swoovr Keycloak | Build `keycloak` target from [Keycloak Dockerfile](../../containers/uat/Keycloak.Dockerfile) | Authentication, custom providers and Swoovr theme |
| Swoovr operator | Build `operator` target from the UAT Dockerfile | Database/identity bootstrap, place import and backup tooling |
| Zammad | Pinned upstream `ghcr.io/zammad/zammad` image | Reused for init, nginx, Rails, scheduler, WebSocket and backup services |
| PostgreSQL | Pinned upstream image | Swoovr and Keycloak databases in one private container; a separate PostgreSQL instance in the Zammad stack |
| Redis, Memcached, Elasticsearch | Pinned upstream images | Zammad dependencies |
| ClamAV | Pinned upstream image | Scans support attachments for Swoovr |
| Caddy | `caddy:2.11.9-alpine@sha256:0bbb055bda6793c999bde78b1b5ce3e89f829fb031af2cf50056ea1246559fee` (owner selected; digest not independently verified) | HTTPS ingress for application services |

There is no custom `swoovr-zammad` image in this repository. That name is the local network alias and Compose project name. The application image currently used is upstream Zammad **7.2.2**, pinned by digest in [the local override](../../containers/zammad/compose.local.yaml). The provisioning scripts and configuration are separate repository files. If you have another custom image in Docker Hub or another repository, it would need a separate review before choosing between them.

The Swoovr images can be published to private Docker Hub repositories. Third-party images can remain pinned to their upstream registries; mirroring them is optional and requires preserving provenance, platform support and licensing information.

## Building and publishing

For the x86 Vultr server, build `linux/amd64` images from the **same reviewed commit**. The target architecture should match the chosen machine. The following is the shape of the commands; replace `YOUR_DOCKERHUB_ORG` and the commit tag with your values:

```sh
docker login

for component in api web admin operator; do
  docker buildx build \
    --platform linux/amd64 \
    -f containers/uat/Dockerfile \
    --target "$component" \
    --tag "YOUR_DOCKERHUB_ORG/swoovr-$component:COMMIT_ID" \
    --push .
done

docker buildx build \
  --platform linux/amd64 \
  -f containers/uat/Keycloak.Dockerfile \
  --target keycloak \
  --tag "YOUR_DOCKERHUB_ORG/swoovr-keycloak:COMMIT_ID" \
  --push .
```

Record the pushed manifest digest for each image and configure the servers to pull `repository@sha256:…`. Keep the commit tag for human identification. The images contain no deployment secrets; provide those at runtime. The [container runbook](CONTAINERS-UAT.md) describes the build targets and their boundaries.

The operator image deserves special attention: its database bootstrap, Keycloak bootstrap and place import commands are useful on Vultr, but its **prepared remote backup transport is Azure-specific**. Publishing that image does not create a working Vultr backup system. A Vultr-compatible encrypted backup and restore procedure is needed only if UAT data or configuration is to be preserved; the owner accepts a fresh rebuild for disposable UAT.

## Single Vultr server and Caddy

**CONFIRMED by owner in this conversation:** run the complete disposable UAT stack on one Vultr bare-metal server with one Caddy container handling the incoming routes for all five application hostnames. This supersedes the earlier separate-VM and per-VM Caddy plan. The server selected is Intel E-2286G (6 physical cores / 12 threads), 32 GB RAM, 2 × 960 GB SSD with software RAID 1, and Debian as host OS. Confirm the final order and actual RAID layout during provisioning. This describes the plan; no server, DNS or service deployment is established by this document.

The image-processing services remain independently hosted and are not moved to this server. The Zammad stack retains its own PostgreSQL container and persistent volume. Swoovr and Keycloak use separate databases in a second PostgreSQL container.

```yaml
image: caddy:2.11.9-alpine@sha256:0bbb055bda6793c999bde78b1b5ce3e89f829fb031af2cf50056ea1246559fee
```

```text
Internet / Cloudflare
        │ HTTPS :443  (HTTP :80 for redirects / certificate validation)
        ▼
Single Caddy container
  ├── uat.swoovr.app ──────────── Web
  ├── api-uat.swoovr.app ──────── API
  ├── admin-uat.swoovr.app ────── Admin
  ├── auth-uat.swoovr.app ─────── Keycloak
  └── helpdesk-uat.swoovr.app ─── Zammad nginx ─ Rails / WebSocket

Private Docker networks on the same server
  API + Keycloak ── Swoovr / Keycloak PostgreSQL
  Zammad stack ── its own PostgreSQL, Redis, Memcached and Elasticsearch
  API ── private ClamAV for support attachments
  API / Web / Admin / Keycloak / Zammad ── required HTTPS service routes
  API ── HTTPS ── independently managed image services
```

This is a logical design. One host, one Caddy and the host's storage are shared failure and security boundaries: a host outage affects all UAT services. Docker networks provide service isolation but do not create the same machine boundary as separate VMs. No VPC is required for traffic entirely within this one server; if external machines later need private access, their connectivity and VPC rules are a separate configuration decision.

### Hostnames and endpoints

| Service | Address | Route |
| --- | --- | --- |
| Member Web | `https://uat.swoovr.app` | Caddy → Web container port 8080; passkey RP. |
| API | `https://api-uat.swoovr.app` | Caddy → API container port 8080. |
| Admin | `https://admin-uat.swoovr.app` | Caddy → Admin container port 8080. |
| Keycloak | `https://auth-uat.swoovr.app` | Caddy → Keycloak HTTP port 8080; management port 9000 remains private. |
| Zammad | `https://helpdesk-uat.swoovr.app` | Caddy → Zammad nginx port 8080, which routes to Rails and WebSocket. |
| Future venue | `https://venues-uat.swoovr.app` | Reserved; no running portal is established. |
| Upload and delivery | `https://images-upload-uat.swoovr.app`, `https://images-uat.swoovr.app` | Independently managed Cloudflare services; not routed to this Caddy. |
| PostgreSQL, Redis, Memcached, Elasticsearch, ClamAV | Private Docker service names and protocol ports | Internal container connections; no public URL or Caddy route. |

D355 confirms the first four and reserves the venue/image names; see [hostname record](D355-UAT-HOSTNAMES-2026-11-10.md). The helpdesk hostname comes from this conversation and has not been recorded as a D355 decision. Staff callback is `https://helpdesk-uat.swoovr.app/auth/openid_connect/callback`.

### Private databases and networking

The host runs Debian. **OPEN:** the exact Debian-based PostgreSQL 17 image and manifest digest for Swoovr/Keycloak still need selection and validation. The local repository pin below is Alpine-based and is retained as historical reference, not the selected image:

```yaml
image: postgres:17.11-alpine@sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb
```

Create separate Swoovr and Keycloak databases and least-privilege roles. Zammad retains its own independently configured PostgreSQL container and storage. Persist each database in a distinct volume across ordinary container restarts; a deliberate UAT reset may remove data only after selecting the exact owned volumes. PostgreSQL has no public listener or Caddy route. Restrict access to its authorised containers; use TLS with certificate verification where configured and validate the actual network path.

Publish TCP 80/443 only for the single Caddy ingress. Keep PostgreSQL, Redis, Elasticsearch, Memcached, ClamAV, Keycloak management and backend application ports off the public interface. Review Docker's published-port behavior and host firewall rules. Control SSH separately. The server needs outbound internet and DNS for registry pulls, OS updates, certificate issuance, email and external services; a Docker network marked internal alone does not provide that access.

For browser and server-to-server HTTPS, preserve canonical hostnames and exact OIDC issuer/callbacks. **OPEN implementation detail:** make internal clients resolve required canonical names to Caddy using explicitly configured Docker network aliases or controlled DNS, while preserving the correct host and TLS certificate name. Containers on separate Compose projects must share only the intended networks; `localhost` in one container does not mean another container. Validate DNS, certificate trust, allowed hosts and reachability from each client container. Avoid depending on public-IP hairpin routing unless tested.

Configure actual proxy trust in Swoovr, Keycloak and Caddy for the deployed path. When Cloudflare is in front, restrict origin access appropriately, set Full (strict), and bypass caching of personalised content. Keep Caddy's admin API private. Ensure DNS and challenge reachability permit certificate issuance and renewal. Persist Caddy `/data` and `/config` and test renewal. These settings have not been deployed or validated.

### Image versions and release inventory

| Component | Version / image to use as current repository baseline |
| --- | --- |
| Caddy | Owner-selected `caddy:2.11.9-alpine@sha256:0bbb055bda6793c999bde78b1b5ce3e89f829fb031af2cf50056ea1246559fee`; verify registry digest and target architecture before release. |
| Swoovr API / Web / Admin / operator | Build each target from the same reviewed source commit. SDK 10.0.400 and .NET 10 runtime are pinned in the Dockerfile; publish each image and deploy its resulting immutable digest. Web image contains the WASM client and server host. |
| Keycloak | Custom repository-built image based on pinned Keycloak 26.8.1 and Java 21; build and publish its own immutable digest. |
| Zammad | Upstream 7.2.2 image `ghcr.io/zammad/zammad@sha256:17dec4f83fc18bad7195d7d44df8dca35e04df882e6315385adf230ea759a614`, used for nginx, Rails, init, scheduler, WebSocket and backup roles. |
| Swoovr / Keycloak PostgreSQL | Debian-based PostgreSQL 17 container requested; exact image and digest **OPEN**. The repository's local Alpine pin is not the chosen hosted image. |
| Zammad PostgreSQL | Separate instance; repository baseline 17.11 Alpine pinned to `sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb`. Check architecture and hosted compatibility. |
| Zammad Redis / Memcached / Elasticsearch | Vendored Compose defaults Redis 8.10.2 Alpine, Memcached 1.6.45 Alpine, Elasticsearch 9.5.5; local override pins their digests. Use the exact reviewed pins from [Zammad override](../../containers/zammad/compose.local.yaml), not floating defaults. |
| Attachment ClamAV | `clamav/clamav@sha256:c21aa18cc0a358678255a3dfb343a4555b58f71b97d7b869df8fdf7ea0a5b4b1`; persist and update signatures. |

The image versions are repository evidence, not a claim that hosted compatibility or provider acceptance has been tested. Record per-image platform, digest, source and tested configuration. Never assume a new hardened image is a drop-in replacement for the pinned stack.

### Single Caddy routing example

**Illustrative only:** one Caddyfile on the server contains all five routes. The backend names must resolve on networks shared with Caddy; these blocks do not configure Docker networks, certificate challenges, proxy trust or secrets.

```caddyfile
uat.swoovr.app {
    reverse_proxy web:8080
}
api-uat.swoovr.app {
    reverse_proxy api:8080
}
admin-uat.swoovr.app {
    reverse_proxy admin:8080
}
auth-uat.swoovr.app {
    reverse_proxy keycloak:8080
}
helpdesk-uat.swoovr.app {
    reverse_proxy zammad-nginx:8080
}
```

Configure Zammad `NGINX_SERVER_SCHEME=https` and `NGINX_CLIENT_MAX_BODY_SIZE=220M`. Test SignalR and Zammad WebSockets, registration callbacks, passkeys and certificate renewal through the final deployed route.

### Operator jobs

The operator image is a short-lived job image, not a continuously serving application. Start it as a short-lived container on the UAT server when a bootstrap, import, backup or other operator command is needed; the job launcher and scheduler are **OPEN**. Give each job only the private network access and credentials required for that command. It has no public hostname or Caddy route. Its prepared Azure backup transport needs a reviewed non-Azure replacement before relying on it for Vultr recovery.

The selected server provides 6 physical cores / 12 threads and 32 GB RAM for the complete stack, including Zammad and both database containers. This is an unmeasured starting configuration. Zammad's current [hardware guidance](https://docs.zammad.org/en/latest/prerequisites/hardware.html) lists 2 cores and 6 GB RAM plus 4 GB for same-host Elasticsearch. Measure RAM, CPU, I/O and restore/index-rebuild peaks before relying on the size.

## How Swoovr and Zammad will connect

The Swoovr API is the integration owner. Member and staff Swoovr screens call the Swoovr API; the API calls Zammad. Zammad sends signed events back to the API at `/api/integrations/zammad/events`. Swoovr also synchronises staff identities and permissions into Zammad.

The hosted API needs private values for at least:

```text
Ticketing__Zammad__BaseUrl
Ticketing__Zammad__IdentityToken
Ticketing__Zammad__StaffDirectoryToken
Ticketing__Zammad__RetentionToken
Ticketing__Zammad__HomeCityCheckToken
Ticketing__Zammad__WebhookSecret
Location__HomeCityCheck__AdminBaseUrl
Attachments__Scanning__ClamAv__Host
Attachments__Scanning__ClamAv__Port
```

The complete placeholders and purposes are in [api.env.example](../../containers/zammad/api.env.example) and [Zammad operations](zammad/OPERATIONS.md). The credentials have separate purposes; they should not be replaced by one broad shared agent token.

The Zammad stack needs provisioning of its groups, attributes, identities, tokens, webhook and trigger. Locally, `provision --events` activates the event path. The hosted deployment needs a reviewed procedure for supplying its actual HTTPS API endpoint, webhook secret and staff identity settings; the local launcher and its generated secrets cannot simply be promoted.

**Hosted Zammad SSO is an unresolved setup step.** PR #305 deliberately removed the localhost Zammad client from generated UAT realms. The hosted staff realm therefore needs a separately configured `swoovr-zammad` public PKCE client with the chosen helpdesk’s exact callback. The repository reports local SSO acceptance, but hosted Keycloak/Entra-to-Zammad sign-in has not been accepted.

Zammad’s nginx needs `NGINX_CLIENT_MAX_BODY_SIZE=220M` for the supported attachment payloads. Its database, caches, Elasticsearch, Rails and ClamAV ports should remain private. The Zammad agent UI needs a controlled HTTPS route for authorised staff.

## Swoovr runtime configuration

For each of API, Web and Admin, the hosted runtime requires its own `Hosting__PublicOrigin`, explicit `AllowedHosts` and actual `Hosting__TrustedProxyNetworks`. Web and Admin also need their API URL, Keycloak realm authority, client ID and client secret. The API needs its database connection, Keycloak realms and service account, provider settings, and durable versioned encryption keys.

The hosted API guard requires configured MailerSend, Twilio, Didit, R2, screening, escalation contacts and ClamAV. This matters even though the image service is independently managed: the current API still has a **legacy media store and screening path**, while the newer image pipeline is staging-only. Before deployment, its runtime URLs and credentials must be checked against the live external services’ actual contracts. A healthy external service alone does not establish that the application’s legacy path and the staging path are interchangeable.

The exact API-only staging settings from PR #306 are documented in [UAT image staging](UAT-IMAGE-STAGING-2026-11-10.md). Staged results remain non-deliverable; the current API does not enable the new pipeline as the member photo delivery path.

## Persistence, startup and operations

The deployment configuration must explicitly provide:

- Persistent PostgreSQL data, separate application/Keycloak/Zammad databases and restricted roles.
- Persistent Web and Admin Data Protection keys, Keycloak database state, Zammad storage and ClamAV signatures.
- One-shot database bootstrap, Swoovr migrations and GeoNames place import before serving traffic.
- Keycloak realm import/bootstrap, then controlled activation and root-admin provisioning.
- Zammad init, provisioning, staff synchronisation and signed webhook setup.
- Restart policies, private secrets, TLS renewal, service health monitoring, logging and disk-capacity alerts.
- A documented fresh rebuild and reset sequence for Swoovr, Keycloak and Zammad, plus encrypted off-host backup and restore for any UAT data or configuration the owner chooses to preserve.

Zammad’s own backup container is configured locally for daily backups with `HOLD_DAYS=33`, supporting the approved maximum retention of 35 days. Its database currently contains attachments. UAT can instead be rebuilt with fresh data under the owner's instruction above; any backups retained still need appropriate protection and retention. After a restore, the search index must be rebuilt and Swoovr’s helpdesk retention reconciliation run before reopening access. See [backup and restore](BACKUP-RESTORE.md).

Use one replica of each Swoovr serving host for the initial UAT design. The current BFF session and SignalR state are in memory, so restarts interrupt sessions and multiple replicas need further coordination work.

## What remains to make this deployable

The main missing deliverable is a **reviewed Vultr deployment bundle**: hosted Compose definitions or equivalent, reverse-proxy configuration, private environment templates, Zammad hosted provisioning and SSO instructions, image build/publish instructions, persistent volumes, scheduled jobs, and a repeatable fresh UAT rebuild. If selected UAT data or configuration needs preserving, add a Vultr backup/restore implementation. Use the existing image pins and runtime guards and validate on disposable infrastructure before relying on the bundle.

The existing [UAT deployment guide](DEPLOYMENT-UAT.md) is primarily an Azure implementation. Its image and application configuration are useful inputs, but its Bicep, Key Vault, managed identity and Azure backup commands are not a Vultr deployment.

This was a source and documentation review of merged `main` at `334a950`. Images were not built, published or run, live Cloudflare services were not inspected, and a Vultr host was not validated. The available evidence for Zammad and the application remains local acceptance evidence; hosted integration and restore remain unverified.
