# UAT edge and certificate preparation

D285 DEFAULT preserves D221: Cloudflare DNS/proxy to HTTPS Azure origins, no Tunnel dependency. This is an operator plan, not configured DNS/TLS. Q90/#124 still covers topology, subscription and billing approval. No paid feature is purchased or enabled by these files.

Two independent certificates are necessary: Cloudflare's browser-facing edge certificate and the Azure origin certificate. In a normal full `swoovr.app` zone, Universal SSL covers `uat.swoovr.app` but not deeper `api.uat.swoovr.app`, `admin.uat.swoovr.app` or `auth.uat.swoovr.app`. Before proxying, the owner must provide edge coverage for all four, using an already approved advanced/custom certificate or an approved Advanced Certificate Manager/Total TLS option. Do not assume a paid option exists or authorise its purchase. DNS-only is a reversible diagnostic/alternative, not the approved proxy topology. Tunnel remains optional owner hosting scope and requires its own connector, origin restriction and certificate acceptance.

A proxied CNAME does not satisfy Azure Container Apps managed-certificate issuance/renewal requirements. Therefore `infra/uat/edge.bicep` imports an operator-approved origin certificate by a versioned Key Vault secret reference, using a dedicated managed identity with access only to that certificate secret. No private certificate value/password enters Bicep or parameter files. The foundation attaches that identity to the environment; the edge stage returns the certificate resource ID. The owner provides `swoovr-uat-edge-tls` in Key Vault, with SANs for `uat.swoovr.app` and `*.uat.swoovr.app` (or all four exact hosts), a supported exportable PFX/PEM private key and expiry/renewal ownership. Prefer the owner's approved publicly trusted issuer. Validate actual Key Vault/network/certificate import in Azure before binding. Rotation creates a new physical secret version and explicitly reapplies the edge stage/bindings; monitor expiry rather than assuming rotation occurred.

Create apps initially with `bindPublicHostnames=false`, schedules/invitations off. Their Azure default domains already have platform HTTPS; canonical hostnames are not yet admitted as working tester entry points. Read each app's `properties.customDomainVerificationId` and `properties.configuration.ingress.fqdn`. In Cloudflare dashboard → selected `swoovr.app` zone → DNS → Records, create these records from those actual outputs:

| Type | Name in swoovr.app | Content | Proxy |
| --- | --- | --- | --- |
| CNAME | uat | Web generated FQDN | Proxied only after edge certificate coverage is active |
| CNAME | api.uat | API generated FQDN | Same |
| CNAME | admin.uat | Admin generated FQDN | Same |
| CNAME | auth.uat | Keycloak generated FQDN | Same |
| TXT | asuid.uat | Web domain verification ID | DNS only |
| TXT | asuid.api.uat | API domain verification ID | DNS only |
| TXT | asuid.admin.uat | Admin domain verification ID | DNS only |
| TXT | asuid.auth.uat | Keycloak domain verification ID | DNS only |

At SSL/TLS → Overview, use **Full (strict)**. Keep HTTPS redirects and TLS1.2 minimum in the owner-approved zone configuration; inspect existing rules before changing shared-zone defaults. At Caching → Cache Rules create a UAT-hostname-only **Bypass cache** rule for all four hosts, including HTML, `/api`, authentication, SignalR, token safety pages and private media. Do not enable Cache Everything, response/header transformations or challenges that break OAuth callbacks, Didit webhooks or WebSockets. Preserve Set-Cookie/no-store. Existing corporate-site rules must remain unaffected. No personal analytics tag is required by this setup.

After DNS ownership and certificate readiness, set `bindPublicHostnames=true` and supply the returned certificate ID for `certificates.api`, `.web`, `.admin`, `.keycloak` for each app stage being deployed. The nonsecret preflight requires certificate IDs for active hostname bindings and refuses invitation activation before host binding. Maintain the complete staged parameter inventory on every reapply; this is an incremental deployment, not a promise that omitted switches retain configuration.

Configure exact comma-separated trusted ingress/loopback and current official Cloudflare CIDRs. Never trust0/0 or arbitrary client-supplied forwarded headers. The repository cannot prove Azure's actual forwarded-hop path without a deployment. Verify canonical scheme, redirects, login/logout, SignalR, requester rate limits and untrusted-header refusal in the approved smoke test; correct exact trusted CIDRs if needed. Any origin IP restriction must preserve BFF→API, health probes and operator access and must be accepted in the deployed topology before activation. No broad trust or origin-bypass claim is inferred from local proxy tests.

Primary references: [Cloudflare Universal SSL coverage](https://developers.cloudflare.com/ssl/edge-certificates/universal-ssl/limitations/), [Full (strict)](https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/), [dynamic/login caching](https://developers.cloudflare.com/cache/troubleshooting/dynamic-content-and-login-issues/), [official Cloudflare IP ranges](https://www.cloudflare.com/ips/), [Azure managed certificate constraints](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates), [Key Vault certificate resource contract](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/2025-07-01/managedenvironments/certificates).
