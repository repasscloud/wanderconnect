# Phase 1 — optional local MFA investigation

Partial provider investigation only. Keycloak remains the backend, MFA remains optional, and no application/privileged access is enabled. The existing fictional-adult fixture has simulated administrator-asserted email status; this does not prove email ownership/adulthood.

DEFAULT: opt-in browser test on the owned fixture, refusing an existing OTP credential or outstanding required action. Temporarily request CONFIGURE_TOTP for that one fixture, enroll using a uniquely labelled test authenticator, demonstrate password+OTP with wrong/missing-code denials, then delete only that labelled credential and remove only the temporary required action. Preserve the password and every unrelated credential/action. Verify a final password-only browser login, proving MFA did not become a global requirement. Never log or persist the OTP secret, QR, generated codes, password, token or raw browser trace.

The test secret is read from Keycloak's enrollment screen into process memory and used by a narrow SHA-1 TOTP generator tested against RFC6238 vectors. This is test automation, not an application MFA implementation or recovery flow. It does not establish real social/linked-account behavior, email recovery, production sessions or D14 approval. Fresh password AMR remains an investigation observation only.

Plan: test TOTP vectors and cleanup ownership/failure first; implement temporary fixture action/credential lifecycle, exact user-ID binding, graceful cleanup and optional actual Edge driver; exercise non-enrolled, enrolled, invalid OTP and restored non-enrolled browser paths; full checks/review/evidence/commit/dependent PR. Reruns must fail closed on a leftover or foreign OTP; no silent credential reset. Force kill or provider failure can still require deliberate local fixture inspection.


## Actual evidence — 17 October 2026

Actual installed Edge against retained local Keycloak26.8.1/ARM64 passed password-only baseline, temporary manual TOTP enrollment, configured OTP challenge, actual empty/wrong-code form POSTs with invalid field and no ID-token issuance, successful password+OTP authorization-code/S256 callback validation, provider logout/invalid_grant, owned cleanup and final password-only login. Four successful browser logins were counted in memory; no private values persisted or printed.

The installed Keycloak theme renders invalid OTP as `#otp[aria-invalid="true"]`, with error ID `input-error-otp`; the initial upstream-base-template error selector timed out after a real rejection. Corrected against the actual rendered field, then reran the full browser journey successfully. Keycloak canonicalizes fixture username casing; exact stored user ID remains mandatory. Unit regressions allow casing normalization and deny unrelated rename.

`./scripts/check.sh` passed: Release build zero warnings/errors;53 auth/four appearance Node cases;50 account/73 discovery/39 appearance-state cases; typed client/design/disabled workflow and API/web/prototype HTTP checks. No email ownership/recovery, social/linked-account matrix, production auth adapter, privileged grant or mobile acceptance is established. MFA remains optional.

## Local reproduction

Start the original matching fixture/volume via `./scripts/docker-local.sh up`; from that checkout run `DATINGAPP_PLAYWRIGHT_PATH=/absolute/path/to/playwright node /absolute/path/to/this/worktree/scripts/auth/mfa-check.mjs`. Installed Edge is required. This optional mutating provider investigation is deliberately separate from portable checks. Run only against the owned local fixture with no preexisting OTP/required actions. Do not reset or rebind the private fixture. Afterward stop the stack with `./scripts/docker-local.sh down`, retaining volumes/private files.

References: [RFC6238](https://www.rfc-editor.org/rfc/rfc6238), [Keycloak OTP policy](https://www.keycloak.org/docs/latest/server_admin/index.html#otp-policies), [pinned Admin REST](https://www.keycloak.org/docs-api/26.8.1/rest-api/index.html). The SHA1/6-digit/30-second/window1 settings are checked against the actual local realm; they are investigation settings, not a product security-policy decision.
