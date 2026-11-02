# Q112: Registration availability authority

Issue [#157](https://github.com/repasscloud/wanderconnect/issues/157), Q112, intake item20; owner confirmation recorded as D295. This record describes source implementation and explicitly separates it from installed provider acceptance.

## Confirmed behavior and implementation

Two persisted flags distinguish closed, invite-only beta, public, and public with beta tracking. Migration `20261102110000_RegistrationAvailability` seeds UAT invite-only (`beta=true`, `public=false`, revision0). It does not use a runtime configuration flag to silently open registration. The SQL singleton is intentionally unmapped: all changes go through explicit revision predicates rather than an entity upsert. Existing beta records are retained.

`RegistrationEndpoints` provides version-selected, no-store public availability, staff read, Root-only change and a separate provider-only admission operation. Change requires current Root authority, recent authentication, confirmation, bounded reason and expected revision. The transaction records UTC time, actor, previous/new flags and revisions, and reason in the existing audit ledger. Competing updates cannot both win the same revision. Support/Admin/Legal can read current state; only Root sees the mutation form at `/registration`.

`AccountResolver` checks new member application adoption inside the account-creation transaction, sharing the singleton row lock so a simultaneous close cannot overtake application creation. A missing singleton refuses adoption. Beta admission requires an active unbound invitation matching the verified email; unverified token email cannot claim it. Public with beta tracking permits public members while continuing to activate matching invitations. Existing identity authentication/password-reset handlers remain Keycloak-owned and unchanged.

The member web `/account/create` route reads authority with a bounded request. Closed/unreadable availability does not launch registration. Invite-only shows the exact owner copy and an explicit invitation-holder continuation; this continuation grants nothing and remains subject to provider admission. Public registration launches Keycloak's existing create prompt. Native clients and direct/brokered provider entry must be guarded at Keycloak rather than relying on this web navigation gate.

Exact owner copy:

- Title: Swoovr is currently invite-only
- Text: We're not accepting public registrations just yet. If you'd like early access, you can register your interest in our beta.
- Link: [Join the beta waitlist](https://swoovr.com/beta)

## Provider source and required installation gate

The existing pinned contact-policy JAR gains factory overrides for the built-in `registration-user-creation` and `idp-create-user-if-unique` providers (higher factory order100). These preserve inherited Keycloak validation/linking behavior and check authority before calling the inherited account-creation operation. Direct registration is checked both during validation and again immediately before creation. Existing broker identities take the inherited linking path; new broker identities need admission. The guard applies to `swoovr-members*` realms or an explicit `swoovr.registration-policy=required` attribute. The artifact remains pinned to Keycloak26.8.1.

The provider calls the API admission operation with a dedicated private credential, never a browser token or an API-minted authentication session. Configure the same randomly generated secret (at least32 characters, at most512) privately as API `Registration:ProviderToken` and Keycloak `SWOOVR_REGISTRATION_AUTHORITY_TOKEN`; set `SWOOVR_REGISTRATION_AUTHORITY_URL` to the fixed `/api/registration/admission` endpoint on the trusted API origin. HTTPS is required except on the explicitly marked local-development realm. Do not put this credential in realm JSON, logs, issue bodies or committed files. Admission has no cache, refuses redirects, malformed/oversized replies and missing configuration, and has a total five-second deadline including the response body.

**No provider is installed or realm modified by this source change.** The current contact-policy README explicitly distinguishes provider source from installed integration. Therefore this change alone cannot establish closure of the existing provider's identity creation routes. Keep registration administratively blocked until the pinned JAR is installed on every Keycloak instance, the two effective guarded factories are verified, private authority configuration is present, and isolated direct/broker/alternative-creation acceptance passes. Administrative user provisioning, custom user-storage plugins, organization invite extensions and alternative flow overrides need explicit inventory and acceptance; do not assume the two standard guarded creation operations cover an unreviewed custom path. No public launch is authorized.

An in-flight provider check is a request-time admission decision; the provider/API databases do not share an atomic transaction. A request admitted immediately before a close can already be in flight. Application adoption separately rechecks current policy. No instantaneous distributed cancellation guarantee is claimed.

## Tests and evidence boundaries

New dependency-free domain checks cover all four states, absent policy, invalid revision and reason/confirmation requirements. New disposable PostgreSQL checks exercise the migration's beta-only seed, verified/unverified adoption across all four states, revoked invitation refusal, and missing-singleton refusal. Java checks cover admission-response schema/refusal/unavailable handling. The isolated Admin `registration` scenario covers ordinary Admin read-only state, Root confirmation/reason/revision update, previous/new audit and stale-form refusal.

Existing disposable fixtures that previously used `Beta:Enabled=false` now seed explicit public availability in their owned databases. Installed/UAT data is not changed. The owned meeting and Admin harnesses similarly seed public availability only in their newly created disposable databases.

At implementation handoff, these new Q112 tests and provider compilation have not run; the coordinating agent owns the verification window. Required checks: `./scripts/check.sh`, isolated Admin registration scenario, pinned Java provider checker, isolated Keycloak direct registration and first broker creation with all four states, missing/slow/invalid authority, repeated/stale submissions, and existing sign-in/password reset. Verify two API instances against the same policy database and two Keycloak instances with identical effective guarded factories. Browser-only success cannot establish preidentity provider enforcement or native acceptance.

No credentials, screenshots, identity material or private message evidence is included. No service start, build, browser session, provider installation, deployment, merge, commit or Actions change was performed by this subtask.

## Subsequent source verification

The coordinating agent authorized a disposable Docker source-build target (no provider service or installation). `docker build -f containers/uat/Keycloak.Dockerfile --target provider-build -t swoovr-registration-provider:source-check .` completed successfully on the pinned Java21/Keycloak26.8.1 sources. It compiled all provider sources and tests, passed15 fictional retirement-policy checks,162 MailerSend loopback HTTP assertions, and the registration-admission/name-validation checks. The pre-existing unsupported JPA `getEntities()` removal warning remains explicit. Private source-build log: `/tmp/swoovr-registration-java-check.log`; no secrets or runtime realm/database material were supplied. This establishes compilation and local policy checks only, not installed factory selection or precreation enforcement.

Final source checkpoint: local `services.bicep` compilation passed using Bicep0.48.1; the registration authority URL is derived from the API origin and the shared secret uses only private API/Keycloak bindings. No provisioning ran. Disposable Keycloak26.8.1 runtime passed45 assertions, selecting all three guarded factories, exercising four-state Create-link rendering, direct/broker refusal, unavailable authority and existing sign-in/reset visibility. Evidence `/tmp/swoovr-registration-runtime-check.log`. The authority and upstream OIDC were synthetic local servers; this is not installed-provider, real social or distributed hosted acceptance. Final current-source Docker provider-build target also passed (`.local/overnight-delivery/keycloak-final-build.log`).

Observed Admin registration browser pass: actual local Keycloak password+TOTP fixture, ordinary Admin read-only, Root confirmed/reasoned CAS change, previous/new audit and stale-revision refusal. `admin-registration-retry.log`, exit0 and successful owned cleanup. The initial test expected a numeric409 marker; the typed API correctly returned `revision-conflict`, and the assertion now uses that contract code. The new provider still requires owner installation/private configuration; this does not change the installed realm.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
