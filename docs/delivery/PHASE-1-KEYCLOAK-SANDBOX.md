# Phase 1 — local containers and Keycloak contract investigation

16 October 2026. Partial Phase 1 slice. Foundation PR #1 and access-policy PR #2 were already merged when inspected; new work starts from main merge e0e76ff. No merge or public/dev deployment is part of this slice.

## Reviewable scope

CONFIRMED: D11/D14 Keycloak and privileged session policy; D15 server containerisation except mobile. Preserve seven projects, DatingApp namespace, replaceable public name, six personal schemes and independent mode choices. Actions remain disabled.

DEFAULT D16: separate API/web multi-stage image targets; pinned SDK/runtime indexes; loopback Compose stack; pinned Keycloak 26.8.1 development mode with embedded disposable database and isolated datingapp-dev realm; privately generated bootstrap secret. No app persistence service is needed yet. No application OAuth clients are registered until the callback/evidence harness is specified. No users or provider credentials are seeded. This is infrastructure preparation, not simulated login.

Run [the local commands](LOCAL-DEVELOPMENT.md#docker-local-stack). Smoke checks real HTTP API JSON and web HTML, exact sandbox issuer, signing-key publication and advertised S256. These are provider metadata observations only. Health/readiness does not establish authenticated access. Application hosts remain independent of Keycloak, so no artificial dependency implies that app authentication is working.

## Contract findings and next executable investigation

Official [downloads](https://www.keycloak.org/downloads) and [release](https://github.com/keycloak/keycloak/releases/tag/26.8.1) still identify 26.8.1. The [container guide](https://www.keycloak.org/server/containers) documents start-dev and bootstrap variables; the [realm import guide](https://www.keycloak.org/server/importExport) documents startup import and skipping existing realms. [OIDC endpoints](https://www.keycloak.org/securing-apps/oidc-layers) document discovery and certificates. Image digests in Compose/Dockerfile are the reproducibility source; review updates deliberately.

The [server administration guide](https://www.keycloak.org/docs/latest/server_admin/index.html) describes configurable authentication-method references and broker session notes. The guide confirms that authenticator reference values and an AMR mapper must be configured to emit completed execution references. It separately documents the `identity_provider` broker alias session note and a User Session Note mapper. These are candidate positive evidence sources, not a deployed contract. Cookie/automatic execution and interactive execution differ, so AMR contents still need SSO and refresh testing. No absence-of-broker inference is safe. A standard realm is not sufficient evidence of D14. Preserve four independent facts: registration origin, verified email ownership, linked provider identities and authentication performed for this particular session. No application code interprets claims in this slice.

| Next scenario | Evidence and acceptance gate | Current status |
| --- | --- | --- |
| Direct password login | Code + PKCE harness, state/nonce, validated issuer/audience/signature/expiry; configured positive method evidence bound to session and auth_time | OPEN; no app client/harness |
| Verified email | Development mail delivery plus ownership verification; never manually mark a fixture as real email proof | OPEN; SMTP not configured |
| Social and linked account | Real sandbox provider configuration; same subject may have direct credentials and links; broker method remains denied for privileged access | OPEN; no Apple/Google credentials |
| Existing SSO and refresh | Compare original auth_time and positive execution evidence; token issuance alone must not refresh privileged freshness | OPEN; no trusted mapping |
| Forced reauthentication | Prove fresh direct execution despite existing social SSO; choose/document bounded freshness policy; absent/conflicting evidence denies | OPEN; no evaluator |
| Optional MFA | Enrol/recover both with and without MFA; MFA never substitutes for direct email evidence | OPEN |
| Logout/revocation/rotation | Validate session revocation, refresh rejection, expired/wrong issuer/audience/signature tokens and signing-key rotation | OPEN; JWKS publication alone is not rotation testing |
| Email links | Verify supported implementation, scanner GET safety, atomic replay/expiry/audience enforcement; verify-email/reset-password links do not prove magic-link support | OPEN; Q02 remains unanswered |

For the next harness use fictional adults and explicit local redirect allowlists, separate web/mobile clients and no public-client secrets. Keep raw tokens, passwords, mail and user exports out of Git and reports; record only minimal redacted observations with provenance. A genuine login test must report its exact configured flow and token-validation steps. Do not substitute a password grant for browser/SSO evidence. Do not infer direct authentication from missing broker notes or credential existence.

No server evidence evaluator or authorization wiring is added. Real privileged access remains disabled; HasApprovedPrivilegedEmailEvidence must remain false until validated evidence proves D14. Login, roles/venue persistence, mobile/device tests and all remaining Phase 1 exit criteria stay open. Actual results and limitations belong in [VALIDATION](VALIDATION.md).
