# UAT edge and certificate preparation

CONFIRMED **D355** ([owner #124, 10 November 2026](https://github.com/repasscloud/wanderconnect/issues/124#issuecomment-6742633182)) supersedes D291's hostnames and paid edge certificate requirement. Current hosts are `uat.swoovr.app`, `api-uat.swoovr.app`, `admin-uat.swoovr.app`, `auth-uat.swoovr.app`, future `venues-uat.swoovr.app`, `images-upload-uat.swoovr.app` and `images-uat.swoovr.app`. They are single-level names covered by Universal SSL in the normal full `swoovr.app` zone. Verify the actual active certificate before opening traffic; no paid edge certificate purchase is required by this design.

**UAT application hosting is OPEN**, including Vultr; production remains Azure (owner clarification, 10 November 2026). The Azure steps below are one optional implementation. Application configuration, Keycloak callbacks and Worker hosts do not depend on selecting Azure. No provisioning, certificate purchase or DNS change has occurred.

Cloudflare's browser-facing edge certificate and the origin's TLS certificate remain separate. Use Full (strict) to a valid HTTPS origin on the selected host. For Vultr, point the application DNS names at the selected load balancer/reverse proxy, install the origin certificate there using private runtime custody, restrict origin access and configure only the actual trusted proxy networks. Use A/AAAA or CNAME according to the selected origin; do not create Azure `asuid` TXT records for a Vultr origin. No Tunnel is required by the prepared design.

## Optional Azure origin certificate and DNS path

A proxied CNAME does not satisfy Azure Container Apps managed-certificate issuance/renewal requirements. Therefore `infra/uat/edge.bicep` imports an operator-approved origin certificate by a versioned Key Vault secret reference, using a dedicated managed identity with access only to that certificate secret. No private certificate value/password enters Bicep or parameter files. The foundation attaches that identity to the environment; the edge stage returns the certificate resource ID. The owner provides `swoovr-uat-edge-tls` in Key Vault, with SANs for all four exact application hosts (`uat.swoovr.app`, `api-uat.swoovr.app`, `admin-uat.swoovr.app`, `auth-uat.swoovr.app`), or an appropriately controlled `*.swoovr.app` certificate, a supported exportable PFX/PEM private key and expiry/renewal ownership. Prefer the owner's approved publicly trusted issuer. Validate actual Key Vault/network/certificate import in Azure before binding. Rotation creates a new physical secret version and explicitly reapplies the edge stage/bindings; monitor expiry rather than assuming rotation occurred.

Create apps initially with `bindPublicHostnames=false`, schedules/invitations off. Their Azure default domains already have platform HTTPS; canonical hostnames are not yet admitted as working tester entry points. Read each app's `properties.customDomainVerificationId` and `properties.configuration.ingress.fqdn`. In Cloudflare dashboard → selected `swoovr.app` zone → DNS → Records, create these records from those actual outputs:

| Type | Name in swoovr.app | Content | Proxy |
| --- | --- | --- | --- |
| CNAME | uat | Web generated FQDN | Proxied only after edge certificate coverage is active |
| CNAME | api-uat | API generated FQDN | Same |
| CNAME | admin-uat | Admin generated FQDN | Same |
| CNAME | auth-uat | Keycloak generated FQDN | Same |
| TXT | asuid.uat | Web domain verification ID | DNS only |
| TXT | asuid.api-uat | API domain verification ID | DNS only |
| TXT | asuid.admin-uat | Admin domain verification ID | DNS only |
| TXT | asuid.auth-uat | Keycloak domain verification ID | DNS only |

Worker custom domains are `images-upload-uat.swoovr.app` and `images-uat.swoovr.app`; use the separate Worker handoff, with no Azure ownership TXT records. The future venue host is reserved, not a deployed portal.

At SSL/TLS → Overview, use **Full (strict)**. Keep HTTPS redirects and TLS1.2 minimum in the owner-approved zone configuration; inspect existing rules before changing shared-zone defaults. At Caching → Cache Rules create a UAT-hostname-only **Bypass cache** rule for all four hosts, including HTML, `/api`, authentication, SignalR, token safety pages and private media. Do not enable Cache Everything, response/header transformations or challenges that break OAuth callbacks, Didit webhooks or WebSockets. Preserve Set-Cookie/no-store. Existing corporate-site rules must remain unaffected. No personal analytics tag is required by this setup.

After DNS ownership and certificate readiness, set `bindPublicHostnames=true` and supply the returned certificate ID for `certificates.api`, `.web`, `.admin`, `.keycloak` for each app stage being deployed. The nonsecret preflight requires certificate IDs for active hostname bindings and refuses invitation activation before host binding. Maintain the complete staged parameter inventory on every reapply; this is an incremental deployment, not a promise that omitted switches retain configuration.

Configure exact comma-separated trusted ingress/loopback and current official Cloudflare CIDRs. Never trust0/0 or arbitrary client-supplied forwarded headers. The repository cannot prove Azure's actual forwarded-hop path without a deployment. Verify canonical scheme, redirects, login/logout, SignalR, requester rate limits and untrusted-header refusal in the approved smoke test; correct exact trusted CIDRs if needed. Any origin IP restriction must preserve BFF→API, health probes and operator access and must be accepted in the deployed topology before activation. No broad trust or origin-bypass claim is inferred from local proxy tests.

Primary references: [Cloudflare Universal SSL coverage](https://developers.cloudflare.com/ssl/edge-certificates/universal-ssl/limitations/), [Full (strict)](https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/), [dynamic/login caching](https://developers.cloudflare.com/cache/troubleshooting/dynamic-content-and-login-issues/), [official Cloudflare IP ranges](https://www.cloudflare.com/ips/), [Azure managed certificate constraints](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates), [Key Vault certificate resource contract](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/2025-07-01/managedenvironments/certificates).
