# Keycloak integration boundary

Update, 22 October 2026: the [application integration design](KEYCLOAK-INTEGRATION-DESIGN.md) (owner approved 22 October 2026) records D31–D41 — three realms, Entra SSO for staff, passkeys, Keycloak-side email links and no API-minted sessions. Where it conflicts with this page, the design and those decisions govern.

CONFIRMED backend selection: Keycloak. Owner supplied official documentation and source on 16 October 2026. No existing version, realm or sandbox configuration was found. Proposed development pin and evidence mapping are in [Keycloak development proposal](KEYCLOAK-DEVELOPMENT.md). This document defines required behaviour, not claims about provider features. Do not substitute another product.

The [product policy](../product/04-IDENTITY-TRUST-AND-SAFETY.md) is authoritative: members Apple/Google/email; partner/admin/support dedicated email/password or email link; optional MFA. Member magic links are a DEFAULT proposal. Extra providers remain OPEN.

## Phase 1 contract discovery

Obtain authoritative provider documentation or source and sandbox settings. Verify discovery/issuer and signing-key rotation if OIDC is supported; audience/client registrations, authorization code + PKCE, redirect allowlists, logout/revocation, role provisioning, trusted login-method evidence, password reset and optional MFA/recovery. Verify whether single-use email links are provided natively and what callback/exchange semantics are available. Choose an adapter only after this evidence exists.

If Keycloak cannot prove a dedicated email authentication flow for privileged identities, keep privileged access disabled until a supported design is agreed. Hiding social buttons is insufficient. Do not create guessed claims such as `login_method=email` and treat them as provider evidence. Domain role/venue scope is application-owned and never accepted from a request body.

## Target session flows (defaults pending contract)

Web: provider redirect → validated server callback → server session with Secure/HttpOnly cookie, CSRF defense for mutations, session revocation. Keep provider credentials and access/refresh tokens out of rendered HTML and browser local storage.

Mobile: system browser → verified app/universal link → code exchange with PKCE → secure OS token storage. Register separate mobile/web clients and callback URIs. Never include client secrets in the app.

Email: generic request acknowledgement → audience-bound, short-lived, single-use opaque token delivered to email → intended app route → explicit consume/exchange → normal session. A GET from an email scanner must not sign in or consume it. Reject open redirects, expired/replayed tokens and mismatched audience; redact tokens from logs. DEFAULT lifetime: 10 minutes, pending provider validation. Cross-device behavior must be reviewed rather than weakening request binding silently.

The earlier separate-staff-account default is superseded by the owner clarification: accounts may support direct and social authentication. Q10 is resolved by D14: require verified email and a fresh direct email/password or approved email-link session; linked identities are allowed. Do not infer the session method from email strings or linked identities. Staff invitation and venue membership are server-managed; support has distinct permissions from admin. Optional MFA must not become a hidden mandatory requirement. Account deletion, session revocation, linked-login safeguards and Apple relay handling are required integration work.

## Required negative tests before enabling access

- Social-authenticated sessions cannot enter partner/admin/support via UI, direct API, callback replay or account linking.
- Missing/untrusted authentication-method evidence denies privileged access.
- Member email matching staff email does not merge accounts or grant roles.
- Wrong issuer/audience, expired signature, revoked session and rotated signing keys behave correctly.
- Email-link replay, scanner GET, open redirect, wrong audience, expiry and concurrent consumption fail safely.
- Optional MFA enrolment/recovery works without making non-enrolled accounts unusable.
- Venue staff cannot access another venue; support cannot perform admin-only actions.

These are acceptance criteria. No live authentication endpoints, fake JWT validator or production integration are included in phase 0.

## Phase 1 independent policy slice

`Swoovr.Domain.AccountAccessPolicy` now evaluates trusted application facts for coarse account-area access. It is not registered as an API authorization handler and does not authenticate any request. [Slice scope and adapter obligations](../delivery/PHASE-1-ACCESS-POLICY.md) explain the trust boundary. SignInMethod values are internal application categories, not Keycloak claims or capabilities. Provider uncertainty must map to no session or Unknown, never an optimistic email-method default. No adapter is configured. `HasApprovedPrivilegedEmailEvidence` is a reserved server policy verdict, not an exclusive account classification. No production code can currently supply that verdict; it must remain false until the configured session-evidence contract validates verified email and fresh direct email authentication under D14.


## Retained conflict authority (D235)

Explicit staff-personal links are also minimal staff exclusions, saved atomically with link changes and final member deletion. `StaffConflictHistory` reconciles historical `staff-link.set` assertions with recorded actor/time; no email inference. `ExcludedAsync` reads current links, exclusions and historical assertions in one SQL statement, preventing an archive/delete race between separate reads. Moderation write guards use case subject/reporter membership and the matched endpoint’s route metadata rather than request-path spelling. Existing identity/hold/police guards consume the same exclusion authority.

Retention takes exclusive preservation authority. It keeps associations while related account/history, case, hold/copy metadata or police records remain; unknown membership fails closed. Once the DEFAULT24-month minimum and every related obligation end, source assertions and the exclusion expire in one transaction. No new identity credential or authentication session is created.


### Application email adoption (D243–D245)

Authentication remains Keycloak's responsibility. Swoovr accepts a verified token email as an application contact only after registry admission. An already admitted member with a profile/accepted email retains the last accepted contact and receives a neutral recoverable warning if a new one is restricted, throttled, unavailable or based on older token evidence. It does not automatically ban the member or remove existing access. Initial/profileless/identifierless admission remains blocking. Persist the initial and subsequent email-acceptance watermark, including tolerated issuer-ahead iat, to prevent older tokens undoing a newer accepted contact. See [registry boundary](BANNED-IDENTIFIERS.md). No API-owned authentication token/session or provider email-update endpoint is introduced.


## Application session revocation (D254 DEFAULT)

Keycloak owns authentication/session issuance. Provider logout alone does not revoke offline signature validation of an issued JWT. A persisted `SessionsRevokedAt` cutoff requires both `auth_time` and `iat` later than it. Missing/invalid issuance, issuance beyond admitted future skew and authentication later than issuance fail closed for a revoked account. Nonrevoked account behavior is unchanged. The common `SessionRevocationPolicy.IssuerClockSkew` supplies JWT validation and the conservative revocation cutoff (server now plus30seconds), which never moves backwards. Depending on issuer clock offset, a fresh original-subject sign-in must wait beyond that cutoff. New legitimate owners of recycled contacts use their own subject and have no such cutoff.

Reviewed mobile retirement commits cutoff, encrypted history, same-contact pending-code invalidation, binding removal and immutable case history atomically after successful provider logout. Existing staff revocation shares the cutoff policy; target account-state authority serializes overlapping revocations before loading/reloading state. Removing roles/grants remains independent of a later authentication. No API-issued token/session is introduced. Boundary clock tests are deterministic; no actual provider clock shift is claimed.
