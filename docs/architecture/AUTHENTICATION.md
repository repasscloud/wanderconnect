# Keycloak integration boundary

CONFIRMED backend selection: Keycloak. Owner supplied official documentation and source on 16 October 2026. No existing version, realm or sandbox configuration was found. Proposed development pin and evidence mapping are in [Keycloak development proposal](KEYCLOAK-DEVELOPMENT.md). This document defines required behaviour, not claims about provider features. Do not substitute another product.

The [product policy](../product/04-IDENTITY-TRUST-AND-SAFETY.md) is authoritative: members Apple/Google/email; partner/admin/support dedicated email/password or email link; optional MFA. Member magic links are a DEFAULT proposal. Extra providers remain OPEN.

## Phase 1 contract discovery

Obtain authoritative provider documentation or source and sandbox settings. Verify discovery/issuer and signing-key rotation if OIDC is supported; audience/client registrations, authorization code + PKCE, redirect allowlists, logout/revocation, role provisioning, trusted login-method evidence, password reset and optional MFA/recovery. Verify whether single-use email links are provided natively and what callback/exchange semantics are available. Choose an adapter only after this evidence exists.

If Keycloak cannot prove a dedicated email authentication flow for privileged identities, keep privileged access disabled until a supported design is agreed. Hiding social buttons is insufficient. Do not create guessed claims such as `login_method=email` and treat them as provider evidence. Domain role/venue scope is application-owned and never accepted from a request body.

## Target session flows (defaults pending contract)

Web: provider redirect → validated server callback → server session with Secure/HttpOnly cookie, CSRF defense for mutations, session revocation. Keep provider credentials and access/refresh tokens out of rendered HTML and browser local storage.

Mobile: system browser → verified app/universal link → code exchange with PKCE → secure OS token storage. Register separate mobile/web clients and callback URIs. Never include client secrets in the app.

Email: generic request acknowledgement → audience-bound, short-lived, single-use opaque token delivered to email → intended app route → explicit consume/exchange → normal session. A GET from an email scanner must not sign in or consume it. Reject open redirects, expired/replayed tokens and mismatched audience; redact tokens from logs. DEFAULT lifetime: 10 minutes, pending provider validation. Cross-device behavior must be reviewed rather than weakening request binding silently.

The earlier separate-staff-account default is superseded by the owner clarification: accounts may support direct and social authentication. Dedicated-email classification remains OPEN (Q10); do not infer it from email strings or linked identities. Staff invitation and venue membership are server-managed; support has distinct permissions from admin. Optional MFA must not become a hidden mandatory requirement. Account deletion, session revocation, linked-login safeguards and Apple relay handling are required integration work.

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

`DatingApp.Domain.AccountAccessPolicy` now evaluates trusted application facts for coarse account-area access. It is not registered as an API authorization handler and does not authenticate any request. [Slice scope and adapter obligations](../delivery/PHASE-1-ACCESS-POLICY.md) explain the trust boundary. SignInMethod values are internal application categories, not Keycloak claims or capabilities. Provider uncertainty must map to no session or Unknown, never an optimistic email-method default. No adapter is configured. `HasApprovedPrivilegedEmailEvidence` is a reserved server policy verdict, not an exclusive account classification. No production code can currently supply that verdict; it must remain false until Q10 and the configured session-evidence contract are resolved.
