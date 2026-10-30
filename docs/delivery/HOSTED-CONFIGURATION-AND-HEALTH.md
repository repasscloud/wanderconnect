# Hosted configuration and health

D279, DEFAULT implementation under the original UAT queue and D221; D31/D39/D184 requirements remain CONFIRMED. Preparation only: no subscription, DNS, resource, deployment or provider delivery is established by these checks.

The three existing server hosts compile the same server-only source from `src/Hosting`; no project or UI reference to Domain/Infrastructure/API was added. Outside exact Development, a serving host rejects unsafe prototype/staff-password/simulated-delivery/media-bypass/hold-check-test/detailed-error configuration before listening. Api requires explicit hosted realms, Entra alias, database, identity administration, Didit, MailerSend, Twilio, private R2/screening and independent configured crypto material. Phone verification, retention and media screening workers cannot be switched off in a serving hosted API. Selected optional features still require their workers. Push may be explicitly disabled while credentials/device evidence remain owner work; it never falls back to simulation. Provider constructors retain their own validation.

Common hosted values:

| Setting | Meaning |
| --- | --- |
| `Hosting:PublicOrigin` | Exact HTTPS public origin, without a path/query/credentials; API/Web/Admin have their own origin |
| `AllowedHosts` | Explicit semicolon-separated public/default internal hostnames; no wildcard; includes the public origin hostname |
| `Hosting:TrustedProxyNetworks` | Explicit comma-separated CIDRs for actual ingress/proxy hops; zero-prefix networks refused |
| `Keycloak:RequireHttpsMetadata` | Must remain true outside Development |
| `Keycloak:BaseUrl` / portal `Keycloak:Authority` | HTTPS Keycloak; `swoovr-members`, `swoovr-venues`, `swoovr-staff`, never development realms |

Trusted forwarded headers run before HTTPS redirection, cookies and authentication. Only forwarded scheme/address are consumed, with at most three trusted hops; forwarded Host is not accepted. The default loopback trust is cleared for hosted ingress. Phone budgets use the already-selected address rather than parsing a leftover chain a second time. Azure ingress and any Cloudflare edge CIDRs must be supplied deliberately by the final infrastructure; the policy is not a network firewall and does not prove that an origin is reachable only through Cloudflare. Unknown proxy headers do not override the address or scheme. Final ingress/restriction configuration and hosted acceptance remain deployment work.

The API requires founder secret1 (retained for existing cryptographic history), phone-code secret, versioned banned-identifier HMAC/contact/evidence/identity/push keyrings and their lookup/HMAC keys. Key values belong in Key Vault references; no values are in this document. Keyring versions/lengths are validated; existing provider and key constructors remain authoritative. The infrastructure/rotation manifest must retain old versions needed by records/backups.

One-shot commands run a reduced profile: explicit unsafe Development switches are still refused, while a migration does not require unrelated live email/media services or running workers. Commands keep their existing restore-fence and per-operation checks. They do not start an HTTP listener. This does not grant permission to run an operator job.

SMS registration is explicit: disabled, Development-only simulated, or complete Twilio. An incomplete/unknown selected provider fails; unset outside Development resolves to disabled for non-serving commands. Hosted Twilio uses its HTTPS API origin and refuses redirects; HTTP logging is removed. These are selection/security boundaries, not a delivery or device claim. Real acceptance/transport verification remains separate from recipient delivery.

Health:

- `/health/live`: process liveness only, minimal `ok` response.
- API `/health/ready`: bounded database connection, no pending migrations and current restore readiness/fence. No configuration/connection/error detail is returned.
- Web/Admin `/health/ready`: configured sign-in secret plus bounded HTTP200 from the configured API readiness path. Redirects are refused. This does not probe Entra/Keycloak/MailerSend/Twilio/APNs/FCM/Didit or establish a member journey.
- Readiness failures return503 `not-ready`; all readiness responses use no-store. Health has no date-version header requirement.

CSP applies in local browser acceptance too. Web permits `wasm-unsafe-eval` for the existing WASM runtime, without general `unsafe-eval`; Admin remains SSR. Scripts/fonts/media are restricted to the needed local sources. Inline styles remain allowed for existing components (DEFAULT); external scripts, objects and framing are denied. Token-page no-referrer controls remain stricter than the ordinary referrer policy. Actual browser regressions, startup refusals and remaining gaps are in VALIDATION.md.

References: [Microsoft readiness/liveness](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0), [trusted forwarded headers](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0), [Twilio Programmable Messaging](https://www.twilio.com/docs/messaging/api/message-resource). Orbit discovery for Twilio was attempted and returned403; provider facts were checked against primary Twilio documentation instead. No provider request was sent.

OIDC sign-in/create/reauthentication links use `data-enhance-nav="false"` so the browser performs a full document handoff rather than an enhanced fetch to Keycloak. This keeps connect-src restricted; it does not disable ordinary in-app enhanced navigation or change routing/prerendering. [Microsoft navigation contract](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#enhanced-navigation-and-form-handling).
