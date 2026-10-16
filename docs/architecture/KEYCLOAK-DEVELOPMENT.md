# Keycloak development and authentication evidence proposal

16 October 2026. CONFIRMED: owner selected Keycloak and supplied its [documentation](https://www.keycloak.org/documentation) and [source repository](https://github.com/keycloak/keycloak). This corrects the earlier IdentityCloak name. Authentication and verified email ownership are separate from government-ID verification and liveness (Q04).

## Initial repository inspection and proposed environment

Historical inspection before the sandbox slice: no existing Keycloak version pin, realm export, Compose/container deployment, sandbox configuration or configured credentials were found in tracked project files or the working tree. The API and web settings contain no provider integration. No external machine or account was inspected; this finding describes this repository only.

| Item | Status | Proposal |
| --- | --- | --- |
| Server version | DEFAULT proposal | Pin `quay.io/keycloak/keycloak:26.8.1`; official downloads and release page list 26.8.1 at review time. This is an upstream community release, not a paid support or LTS commitment. Recheck maintenance/security updates before installation and promotion. |
| Realm | DEFAULT proposal | `datingapp-dev`, isolated from `master`, pilot and production; fictional adult test accounts only |
| Deployment | DEFAULT proposal | Separate disposable local container, `start-dev`, loopback-only host binding such as `127.0.0.1:8180:8080`; never use development mode for production |
| Bootstrap credentials | OPEN setup | Generated locally and supplied out of band; none invented, committed, deployed or configured by this proposal |
| Clients | DEFAULT proposal | Separate web and mobile registrations, explicit callback allowlists and PKCE; exact IDs, redirect URIs, audiences, secrets and issuer remain unconfigured |
| Social federation | CONFIRMED intent | Apple and Google; Google broker documented upstream. Apple integration route, app registrations, signing/secret handling and both providers' callbacks need verification against the pinned version; no working federation claimed |
| Email delivery | OPEN setup | Development-only mail sink or owner-approved sandbox SMTP; verified email and recovery must be demonstrated, not simulated as live |

No container, realm, account, client or credential was created by the original proposal slice. The pin is a documented proposal, not an existing deployed version. Before deployment resolve a registry digest for reproducibility and verify image/platform availability. This adds no application projects and does not change the seven-project architecture.

Sources: [official downloads](https://www.keycloak.org/downloads), [26.8.1 release](https://github.com/keycloak/keycloak/releases/tag/26.8.1), [container guide](https://www.keycloak.org/server/containers). The container guide documents development mode and bootstrap variables. Runtime and realm validation remain future evidence.

## Keep four evidence questions separate

| Question | Authoritative evidence to collect | What it cannot prove |
| --- | --- | --- |
| Was registration direct email registration? | Server-controlled registration/provisioning audit records correlated to issuer/subject; mark unknown if absent | Email address, a current password credential or missing social links cannot reconstruct registration history |
| Is email ownership verified? | Validated Keycloak token email-verification claim and/or authorized server-side user record under a documented trust-email policy | Does not establish local registration, password execution, adulthood, legal identity or liveness |
| Are Apple/Google identities linked? | Documented server-side federated identity records for the user; collect minimum provider aliases/opaque references | A link does not establish which method was used in this session; local credentials may coexist |
| How was this session authenticated? | Positively evidenced authentication flow execution correlated to the validated session; configured AMR mapping and broker session notes are candidate inputs to test | Missing broker notes, an email-verified flag, password existence, email domain or UI button selection is not proof of direct authentication |

The [administration guide](https://www.keycloak.org/docs/latest/server_admin/index.html) documents authenticator reference values and the AMR protocol mapper, broker session notes including `identity_provider`, credential management, email verification and identity linking. These are configurable mechanisms, not a guarantee that a default token contains adequate evidence. The [versioned Admin REST reference](https://www.keycloak.org/docs-api/26.8.1/rest-api/index.html) documents user and federated-identity records. Use supported server APIs with least privilege, not direct database reads or browser-supplied facts.

An adapter must validate issuer, audience, signature, expiry and session binding before interpreting tokens. Define trusted mapper configuration and allowed authenticator references, and test direct, brokered, linked-account, SSO-cookie reuse, refresh and reauthentication paths. Where evidence is absent, conflicting or stale, retain Unknown and deny privileged access. A broker alias is not automatically a trusted Apple/Google proof until the realm's configured provider instance is validated. Do not invent a provider-issued `login_method=email` claim or treat absence of `identity_provider` as direct-login proof.

## Dedicated email — confirmed policy, unvalidated integration

CONFIRMED (D14, owner Q10 reply, 16 October 2026): linked Apple/Google identities are allowed. Privileged access requires verified email and a fresh direct email/password or approved email-link session. No separate social-free staff account is required. Registration origin does not grant or deny staff access; application-owned role and venue permissions still apply. Government ID/liveness remains separate.

OPEN implementation details: define and validate a bounded freshness window and reauthentication behaviour against the configured realm. Existing SSO/refresh sessions must not be assumed fresh merely because a new token was issued. Fail closed when original direct-authentication execution, verified-email status or freshness cannot be positively established.

The Domain policy's `HasApprovedPrivilegedEmailEvidence` is a reserved verdict for that future server policy, not a Keycloak claim or account attribute. Current checks supply hypothetical verdicts to test downstream rule composition. Any real adapter must leave it false until the configured session evidence validates D14, including email verification and fresh direct authentication. A social session always fails the privileged sign-in-method gate, even if its account has direct credentials. Optional MFA does not by itself convert a social login into an approved email flow.

Email magic-link support, scanner safety, atomic consumption and cross-device exchange remain unverified. Do not equate verify-email or reset-password links with magic-link authentication. Member magic links remain Q02; staff links are a permitted product option, not a configured Keycloak capability.

## Next reviewable integration evidence

With Q10 resolved, the next slice can provision an isolated sandbox; record exact image digest, realm/client configuration and test identities without secrets; capture redacted validated-token observations and documented server-record provenance. Demonstrate the member/privileged method matrix, account linking and SSO/refresh negatives, optional MFA enrolment/recovery, logout/revocation and email verification. Keep privileged access disabled until these pass. API authorization wiring and mobile/web sessions remain separate implementation work. Actions remain disabled; use local checks.

## Local sandbox follow-up — 16 October 2026

The proposal above is now partly implemented as reversible D16 defaults. Official downloads/release were rechecked and the 26.8.1 multi-platform registry index was resolved to `sha256:79c623a22abacf3fe6c00a5fb30f5b7843d717b234033b80995891c06116d244`. Compose pins that digest and imports an empty `datingapp-dev` realm. No application clients, users, social federation, SMTP or method mappers are supplied. Keycloak creates its normal built-in realm clients; the template's empty client list is not a claim that the running realm has no built-in clients. Local bootstrap credentials are generated privately by the operator script.

See [local commands](../delivery/LOCAL-DEVELOPMENT.md#docker-local-stack), [investigation scope](../delivery/PHASE-1-KEYCLOAK-SANDBOX.md) and [actual validation](../delivery/VALIDATION.md). The application is not connected to Keycloak. Startup/discovery/JWKS observations cannot establish a trusted authentication method, verify a token or satisfy D14. `HasApprovedPrivilegedEmailEvidence` still has no evaluator and real privileged access remains disabled.
