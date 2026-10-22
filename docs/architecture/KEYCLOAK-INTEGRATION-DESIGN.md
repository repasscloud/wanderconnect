# Keycloak application integration design

22 October 2026. Status: APPROVED by the owner on 22 October 2026. Owner answers in this document are CONFIRMED and recorded as D31–D41 in [decisions](../delivery/DECISIONS.md). Everything marked DEFAULT is a reversible implementation call made by the builder (Claude) and may be changed without an owner decision. Anything marked VERIFY is an unproven assumption about Keycloak 26.8.1 or a third-party service; it must be checked against the pinned version before code depends on it. This document supersedes conflicting parts of [authentication](AUTHENTICATION.md) and [Keycloak development](KEYCLOAK-DEVELOPMENT.md) where noted; those remain the record of earlier investigation evidence.

## Intent

Connect the Swoovr API, web and mobile clients to Keycloak so that real people can sign in, with authentication kept in Keycloak and every access decision kept in the Swoovr API. Swoovr is a dating app, not an enterprise system: sign-in should be quick and familiar for members, strict for the people who run the service, and never trade member safety for convenience.

## Goals

1. Members sign in on web and mobile with Apple, Google, email and password, an emailed sign-in link, or a passkey. MFA is optional.
2. Venue partners sign in to the web venue portal with email and password or an emailed sign-in link; later, each venue organisation may connect its own SSO.
3. Repass Cloud employees sign in to the web admin/support portal with Microsoft 365 (Entra ID) SSO only. Mobile access for staff is the mobile browser view of the web portal, not the app.
4. One root admin account uses username and password with MFA enforced; it is a break-glass account, not a daily login.
5. The API validates every token and session server-side and derives access from application-owned records.
6. Sign-in pages and emails use the supplied Standard Keycloak theme.

## Non-goals

- Age verification. Sign-in records only the registration-time "I'm 18 or over" attestation; age status lives in the application ([age model](#age-and-adulthood-belong-to-the-application)).
- Additional social providers. Facebook, X and others remain out of scope.
- Organisation SSO for venues in the first build. The realm is designed for it; enabling it is a later slice.
- Custom QR-code sign-in (see [dropped features](#dropped-or-rejected)).
- Production deployment, UAT billing split, App Store submission or trademark clearance.

## Anti-goals: failure even if it "works"

- **The Swoovr API must never mint sign-in tokens or obtain sessions on a user's behalf.** No impersonation role, no token exchange that issues user tokens to the API, no API-created login links that bypass Keycloak authentication. CONFIRMED, owner (D35). A compromised API must not be able to sign in as anyone.
- A member's Apple/Google session reaching venue or staff areas by any route: direct API call, callback replay, account linking or a shared cookie.
- Accounts merged because two emails match.
- Personal colour schemes, or a remembered scheme, appearing before sign-in.
- Simulated email delivery, age verification or provider integration described as live.
- Making MFA mandatory for members or venue partners without an owner decision.

## Shape: three realms, one application authority

CONFIRMED, owner (D31): Keycloak is split into three realms. A realm is a fully separate set of accounts, sign-in methods, sessions and signing keys.

| Realm (DEFAULT IDs) | Who | Sign-in methods | MFA | Clients (DEFAULT IDs) |
| --- | --- | --- | --- | --- |
| `swoovr-members` | Members | Apple, Google, email/password, email link, passkey | Optional | `swoovr-web` (confidential, server-side), `swoovr-mobile` (public, PKCE) |
| `swoovr-venues` | Venue partner staff | Email/password, email link; later per-organisation SSO | Optional | `swoovr-venue-web` (confidential) |
| `swoovr-staff` | Repass Cloud employees and root admin | Microsoft Entra ID SSO; root admin password only | Entra enforces for employees; enforced OTP for root admin | `swoovr-admin-web` (confidential) |

Why three (builder's reasoning, accepted by owner): each audience signs in differently, and a realm boundary makes those differences structural rather than a hidden button. The venue and staff realms have no Apple/Google identity providers configured, so a social session cannot exist there. A person who is both a member and a venue partner has two unconnected accounts, which keeps a work identity out of the dating profile. A brute-force or misconfiguration incident in the members realm cannot reach staff credentials.

This supersedes the "accounts need not be separate" part of D12 and the D14 rule that privileged access is proven by a fresh direct email session inside a shared realm. The new privileged evidence is: a token from the venue or staff realm issuer, validated for that realm's audience, plus the method checks below. D14's underlying intent — no consumer-social session ever reaches privileged areas — is kept and becomes easier to enforce.

The Keycloak `master` realm is used only for Keycloak server administration, never by Swoovr clients. Its admin account is separate from the Swoovr root admin and needs the same password-plus-OTP protection (DEFAULT).

Development realms keep a `-dev` suffix (`swoovr-members-dev` etc.) in the local Compose stack; the existing single `swoovr-dev` realm is retired by the first implementation slice, with its volume left untouched as before.

### Keycloak authenticates; the API authorises

DEFAULT (owner delegated the choice): Keycloak answers "who is this and how did they sign in". The API answers "what may they do".

Keycloak owns: credentials, linked Apple/Google/Entra identities, passkeys, OTP, email verification, sign-in method evidence (`amr`/`acr`, broker `identity_provider` note), sessions, brute-force lockout, the registration-time adult attestation, and (later) venue organisation membership.

The API owns: the member/venue/staff profile keyed by `(issuer, subject)`; roles (admin, support, venue roles) and venue membership; date of birth and age status; bans, locks and moderation state; consent records; audit logs. Keycloak realm roles and groups are not used for Swoovr authorization, and no role is accepted from a token claim, request body, email domain or Entra group (DEFAULT; Entra group mapping can be revisited later).

Tokens carry only identity and session evidence. The API looks up everything else on each request (with a short cache), so a ban or role change takes effect without waiting for token expiry.

## Sign-in methods

### Members realm

| Method | Design | Status |
| --- | --- | --- |
| Email and password | Keycloak username/password form; email is the username; email must be verified before the API grants a member session | CONFIRMED method |
| Apple | Keycloak identity broker for Sign in with Apple, web and mobile via the system browser | CONFIRMED method. VERIFY: whether 26.8.1 ships an Apple provider; if not, an Apple-specific extension is needed because Apple's client secret is a signed JWT that expires at most every six months, which a generic OIDC provider cannot regenerate |
| Google | Keycloak's Google identity provider | CONFIRMED method |
| Passkey | Keycloak WebAuthn passwordless with a discoverable credential; relying-party ID `swoovr.app` in production, `uat.swoovr.app` in UAT (`localhost` locally). VERIFIED on 26.8.1: passkeys run inside the sign-in form (button and autofill) and register via the `webauthn-register-passwordless` action. Limitation (D45): a passkey sign-in carries the form's `pwd` AMR, so passkeys stay off in the venues and staff realms where `pwd` is privileged evidence | CONFIRMED in scope (D33, supersedes "passkeys OPEN"). VERIFY: 26.8.1 passkey/conditional-UI support and its flow configuration |
| Email sign-in link | Keycloak custom authenticator ([below](#email-sign-in-link)) | CONFIRMED in scope (D34, answers Q02 for members) |
| MFA | Optional OTP (and passkey as second factor where Keycloak supports it); enrolment and recovery from account settings | CONFIRMED optional |

Registration is email-first (D44): with email verification on, Keycloak 26.8.1 asks for email, first name and the 18+ checkbox, sends the verification link, and only then asks for a password. Nobody can set a password on an address they don't control. The session Keycloak starts after that sequence has no AMR, so the API reports its method as Unknown; member access does not depend on the method (D42).

Mobile uses the system browser (`ASWebAuthenticationSession` / Custom Tabs) to the members realm with authorization code + PKCE, returning through a verified universal/app link on `swoovr.app`. Passkeys created on the web work on the phone because both use the `swoovr.app` relying-party ID; the mobile apps need the matching `apple-app-site-association` and `assetlinks.json` files under `https://swoovr.app/.well-known/`.

Cross-device sign-in is covered by passkeys: a desktop browser asking for a passkey shows its own QR code, and scanning it with a phone that holds the passkey signs the desktop in.

### Venues realm

Email/password and email sign-in link only. No Apple, Google or passkey in the first build (DEFAULT; passkeys can be added later without changing the model). Registration is disabled: venue accounts are created by invitation from a Swoovr admin, and the invitation is bound to the account's `(issuer, subject)` on first sign-in. Venue membership and roles are API records.

Later, organisation SSO uses Keycloak's Organizations feature: each venue organisation can link its own identity provider, routed by verified email domain (VERIFY against 26.8.1). Those sessions count as privileged venue sessions only for that organisation's venues.

### Staff realm

- **Repass Cloud employees:** CONFIRMED (D32) Microsoft Entra ID SSO through a Keycloak OIDC identity provider restricted to the Repass Cloud tenant (single-tenant issuer, not `common`). MFA and device policy are enforced by Repass Cloud's Entra Conditional Access, outside this repository. Brokered users get no local password. The staff login page shows only "Sign in with Microsoft"; Keycloak's IdP redirector may skip the page entirely (DEFAULT).
- **Root admin:** CONFIRMED (D36) a single local user with password and mandatory OTP, enforced by a conditional flow that requires OTP for that account, not by trusting the user to enrol. Reached through a separate, unlinked login route. Recovery codes are stored offline by the owner. Used for setup and emergencies; every use is audited.
- **Roles:** an admin creates a staff invitation (email, role: admin or support) in the API. On first Entra sign-in, the API matches the invitation to the Entra-verified email from the staff-realm token once, then binds the staff record to `(issuer, subject)`; later sign-ins use the subject only. This one-time email match is acceptable because the email comes from Repass Cloud's own tenant through a realm that accepts nothing else (DEFAULT). Support and admin keep distinct permissions.
- Staff use only the web portal; there is no staff mobile client registration.

### Privileged session evidence (replaces D14 mechanics)

The API grants venue or staff access only when all of these hold; otherwise it denies and treats the session as Unknown:

- Token signature, issuer (the venue or staff realm), audience (the portal client), expiry and session are valid and the session is not revoked.
- Venue realm: `email_verified` is true, and the session method is password or email link (from the configured `amr` mapper).
- Staff realm: `identity_provider` is the configured Entra alias, or the account is the root admin and the session `amr` includes both password and OTP.
- AMR references are configured with a 12-hour `maxAge` (D46); Keycloak's default of 0 drops the reference from any token issued after the authentication second.
- Freshness: privileged portal sessions last at most 12 hours (DEFAULT); sensitive actions (role grants, age overrides, data export, account deletion) require reauthentication within the last 15 minutes using OIDC `max_age` (DEFAULT). Refresh never resets freshness.

IMPLEMENTED: `SessionEvidencePolicy` (Domain) maps validated token facts to method, MFA and freshness; `AccountAccessPolicy` now takes the realm and those facts instead of the reserved `HasApprovedPrivilegedEmailEvidence` verdict. Venue and staff roles, venue membership and the root-admin flag still need application persistence, so those areas remain closed in practice.

## Email sign-in link

CONFIRMED (D34): build it, and do not let the API create the link or the session. The "login token" lives inside Keycloak.

Design: a small Keycloak authenticator extension (Java, Keycloak SPI) added to the members and venues browser flows.

1. The user enters their email and chooses "Email me a sign-in link". Keycloak always replies "If that address has an account, a link is on its way" (no account enumeration) and applies resend throttling.
2. Keycloak creates a random single-use token, stores only its hash with the realm, client, target user, the pending authentication session and a 10-minute expiry (DEFAULT), and sends the link through MailerSend.
3. Opening the link shows a page with a "Sign me in" button. Only the button's POST consumes the token, so an email scanner fetching the link does nothing.
4. Keycloak atomically consumes the token, rejects reuse, expiry, wrong client or wrong realm, and completes its own authentication flow, recording an `amr` value for the email-link method.
5. If the link is opened on a different device or browser from the one that asked, it signs in that device only after the confirmation button; the original tab is not signed in remotely (DEFAULT, simplest safe behaviour).

Cost and trade-off: this is a Java (Maven) component outside the seven .NET projects, placed at `keycloak/extensions/swoovr-email-link/` and copied into the Keycloak image. The extension must be rebuilt and retested on every Keycloak upgrade. The same extension also holds the MailerSend API email sender and, if needed, the lockout-clearing listener; it is the only custom Keycloak code in this design. If it proves impractical, it is removed and the reason recorded here; the API-minted alternative stays rejected regardless.

## Account protection

- **Lockout:** CONFIRMED (D37, revised by owner 22 October 2026) three failed password attempts lock the account for 15 minutes; completing a password reset by email clears the lock immediately. Implemented with Keycloak brute-force detection in temporary-lockout mode (3 failures, 15-minute wait, no permanent lockout) on the members and venues realms and for the root admin, plus per-IP rate limiting at the reverse proxy (DEFAULT). The temporary lock limits deliberate lockout of someone else's account to 15 minutes at a time. VERIFIED on 26.8.1 (22 October 2026, [members slice](../delivery/PHASE-1-KEYCLOAK-MEMBERS.md)): 3 failures set a 900-second lock, the correct password is refused while locked, and a completed reset-credentials flow clears the brute-force record immediately, so no lockout-clearing listener is needed. Entra handles lockout for employees.
- **Linking:** CONFIRMED (D38) never link or merge accounts because emails match. Keycloak's first-broker-login flow is configured to ask the existing account holder to sign in before linking, never to link automatically. Linking or unlinking from Swoovr settings requires a fresh sign-in (`max_age=0`), and the last remaining sign-in method cannot be removed.
- **Apple private relay:** relay addresses are stored as delivered and never compared with other addresses. To deliver verification and sign-in emails to relay addresses, the `swoovr.app` sending domain must be registered in Apple's Private Email Relay settings.
- **Account deletion:** in-app deletion (App Store requirement) is handled by the API: delete application data per the retention policy, then delete the Keycloak user through a narrowly scoped service account (`manage-users` in the members realm only; no impersonation, no token exchange), and revoke Apple tokens. Subscriptions are not assumed cancelled.
- **Backups:** CONFIRMED (D39) daily backups of Keycloak's database and the application database. DEFAULT additions: encrypted at rest, retained 30 days, with a restore test before launch. Realm configuration is also kept as secret-free JSON in the repository so it can be rebuilt.
- **Upgrades:** the image is pinned by digest. Upgrades go to UAT first, rerun the sign-in test suite (including the email-link extension and the theme), then production.

## Age and adulthood belong to the application

CONFIRMED (D40) owner rules, with Keycloak's part kept to one fact.

Keycloak: registration requires a blocking "I'm 18 or over" checkbox plus the Terms/Privacy line, enforced server-side before any account or email exists. IMPLEMENTED (D43): Keycloak's built-in registration terms step, relabelled, with the `TERMS_AND_CONDITIONS` required action enabled as a default action. That combination records the attestation time on the account (`terms_and_conditions`, emitted as the `swoovr_adult_attested_at` claim) and asks Apple/Google and other new accounts the same question before their first session. A declarative user-profile checkbox was tried first and rejected: on 26.8.1 it refused even a ticked box. The attestation is passed to the API in the token; the API will copy it once persistence exists.

Application (API and database):

| Rule | Behaviour |
| --- | --- |
| Date of birth | Required during profile setup after sign-up; stored on the member record. Changing it after it is set goes through support (DEFAULT, prevents editing to pass) |
| Under 18 by DOB | Account locked immediately |
| AI profile review | Reviews each profile for signs the person may be under 18. A flag requires age verification before the member can continue (DEFAULT: restricted, not deleted); a flag alone never permanently bans |
| Age verification | Stripe Identity document check (answers the provider part of Q04). Sandbox results are never labelled live |
| Staff override | Support or admin may set the account to adult with a recorded reason; audited |
| Status meaning | An 18+ DOB is "adult by declaration", not "age verified". Only a completed Stripe check or a recorded staff/in-person verification sets "age verified" |

Suggested status values (DEFAULT): `declared`, `dob_under_18_locked`, `flagged_needs_verification`, `verification_pending`, `verified_document`, `verified_staff_override`, `verification_failed`.

## Sessions and clients

- **Web (members, venues, staff):** `Swoovr.Web` signs in as a confidential client per realm with authorization code + PKCE and keeps tokens server-side; the browser holds only an HttpOnly, Secure, SameSite cookie. Mutations keep antiforgery protection. Each audience is served on its own host so cookies never overlap (DEFAULT: `swoovr.app` members, `venues.swoovr.app`, `admin.swoovr.app`, all from the same `Swoovr.Web` deployment; no new project). Q03 (render mode) stays open and does not block this.
- **Mobile (members only):** public client, PKCE, system browser, tokens in Keychain/Keystore, refresh-token rotation. No client secret in the app.
- **API:** validates JWTs per realm (issuer, audience, signature from that realm's JWKS with key-rotation handling, expiry), maps `(issuer, subject)` to the application account and applies `AccountAccessPolicy`. Three realms mean three trusted issuers, each accepted only on its own API routes.
- **Hosts:** Keycloak at `auth.swoovr.app` in production and `auth.uat.swoovr.app` in UAT (DEFAULT). UAT moves to separate Azure billing later; nothing here depends on sharing infrastructure with production.

## Email

CONFIRMED (D39, owner 22 October 2026): MailerSend, sending through its HTTP API rather than SMTP. Keycloak's email sender is replaced by a MailerSend API sender in the Swoovr Keycloak extension (Keycloak email-sender SPI; VERIFY the SPI shape in 26.8.1), so verification, password-reset and sign-in-link email all go through the API. The API token is a deployment secret, never committed. `swoovr.app` must be verified as a sending domain (SPF, DKIM, DMARC). Local development keeps Keycloak's built-in SMTP sender pointed at a mail sink; nothing in development is described as delivered email.

## Sign-in pages and emails

CONFIRMED by the owner and the design handoff (`docs/design/keycloak-theme/`, `keycloak-reply.md`):

- Standard scheme only; light/dark follows the device (`darkMode=true` on `keycloak.v2`). No scheme picker and no remembered-scheme cookie.
- Match the sign-in prototype; every page shows "In development · Starting in Adelaide · Adults 18+"; no "safe", "trusted" or document promises.
- Apple and Google buttons follow their own brand rules; the passkey button is an outline button.
- Emails use Standard light with the same footer.
- The same theme is applied to all three realms. Staff and venue pages hide member-only copy such as "New to Swoovr?" (DEFAULT via realm-specific messages).
- VERIFIED on 26.8.1: the theme's `styles=` line matches `keycloak.v2` (`css/styles.css`, PatternFly 5 in `stylesCommon`), dark mode uses `pf-v5-theme-dark`, and login, registration, password, reset and passkey pages were inspected in light and dark at desktop and 390px. Fixes applied: card top colour, input height/focus ring, reveal button, block-button alignment, checkbox label, alert colours; the claims line moved from CSS `::after` into `footer.ftl` so it is real, readable text. The email-link page does not exist yet.
- Note: the prototype's passkey sheet reads "swoovr.com"; the real prompt will show `swoovr.app`.

The theme was copied from `docs/design/keycloak-theme/` to `containers/keycloak/themes/swoovr/`, because `docs/design/` is read-only and copied from swoovr-marketing (handoff step 5). This design does not perform the separate eight-scheme migration in the handoff; that is its own slice.

## Dropped or rejected

| Item | Outcome | Why |
| --- | --- | --- |
| Custom QR-code sign-in (desktop to app) | Dropped, owner (D33) | Passkeys already provide cross-device sign-in with a browser-shown QR code. A custom flow adds session-transfer risk (someone else's phone scanning your screen) and code for little benefit in a dating app |
| API-minted login tokens or sessions | Rejected, owner (D35) | Gives the API the power to sign in as any user |
| Social sign-in for venue or staff | Rejected (D03, now structural) | Realms have no Apple/Google providers |

## Delivery order (DEFAULT)

1. Realm-as-code for the three dev realms, theme mounted, mail sink; email/password with verification and lockout; API JWT validation and `(issuer, subject)` account mapping; members web sign-in.
2. Google and Apple brokering with safe first-broker-login and the adult attestation; linking/unlinking.
3. Optional MFA; passkeys.
4. Email sign-in link extension.
5. Venues realm and portal sign-in with invitations; MailerSend API sender.
6. Staff realm: Entra SSO, root admin with enforced OTP, staff invitations, privileged freshness.
7. Mobile sign-in (blocked on Q09 toolchain).
8. Age model in the API (DOB, statuses, staff override); Stripe Identity and AI review are separate later slices.

Each slice updates [status](../delivery/STATUS.md), adds negative tests from [authentication](AUTHENTICATION.md#required-negative-tests-before-enabling-access) that apply, and records evidence in [validation](../delivery/VALIDATION.md).

## Left to the builder

Exact client IDs, mapper names, token and session lifetimes beyond those stated, Keycloak flow layouts, cache duration for API account lookups, extension internals and test fixture design — within the constraints above.

## Needed from outside the repository

Apple Developer account (Services ID, key, Private Email Relay domain), Google OAuth client, Entra app registration in the Repass Cloud tenant, MailerSend account and DNS records for `swoovr.app`, and hosting for Keycloak and its database. None are configured or claimed by this document.
