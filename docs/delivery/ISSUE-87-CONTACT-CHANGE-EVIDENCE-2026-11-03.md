# Issue87: bounded contact-change security evidence

Scope: [issue87](https://github.com/repasscloud/wanderconnect/issues/87), approved internal policy §4, D185/D187/D194/D244. Policy requires structured contact history with request/verification/activation clocks, requesting account/session and relevant security signals. This change does not broaden staff permissions or collect device/IP data.

## Protected receipt

New phone requests can retain a nullable encrypted receipt on the existing one-day `phone_codes` row (`request_evidence`, migration `20261102160000_ContactChangeReceipts`). It contains only requester account id, actual request time, keyed session correlation where the validated token supplies a session, observed authentication method/MFA/freshness/authentication/token-issue clocks and source. It contains no new number, code, token, raw session id, IP address, user agent or device fingerprint.

On successful replacement, actual code-verification and activation times and the activation-session correlation are attached to that receipt. Previous contact and receipt are sealed together in a versioned bounded envelope inside the existing `ContactHistory.EncryptedValue`. This creates no separate unbounded security log and no plaintext metadata columns. Existing exact-match HMAC lookup and 730-day history retention are unchanged. Current one-day code purge removes the temporary receipt; ordinary contact-history purge, account deletion and legal-hold protection still govern the final receipt.

Legacy contact ciphertext remains readable and has no invented receipt. Legacy pending phone codes without trustworthy request evidence cannot replace an existing number; the member must request a fresh code. New replacement requests fail closed when contact evidence keys are unavailable. First-time number enrolment retains existing behavior. PostgreSQL microsecond precision is respected when binding a request receipt to its code's creation clock.

Key rotation re-seals the complete original plaintext envelope, preserving security metadata, and includes temporary encrypted code receipts. Own data export includes only the member's contact-change evidence; missing keys are represented as unavailable. Legal-hold capture includes the receipt alongside the previous contact inside the existing encrypted evidence package. Ordinary staff lookup remains account ids/date ranges only. Standard audit details contain ids/machine actions, not contact or session contents.

## Provider clock contract preparation

DEFAULT engineering contract: five bounded, admin-write-only Keycloak attributes hold the latest verification-request binding/time, verification binding/time and an optional SHA-256 correlation of the requesting provider session. Bindings cover exact subject plus exact email, without duplicating the email value in those attributes. SHA session material is further purpose-separated and keyed by the API before storage; no raw session id is persisted in these receipts.

The pinned `swoovr-contact-clocks` event listener records successful `SEND_VERIFY_EMAIL` and `VERIFY_EMAIL` events. The request clock means the successful issuance of a verification message, not a guessed time when the member began editing. Repeated sends for the same pending verification preserve the original issuance clock. Failures and admin/broker assertions of verification do not fabricate clocks. A changed email binding cannot reuse a previous email's timestamps.

Pinned source review found an important second path: Keycloak26.8.1's UpdateEmail action-token handler emits `UPDATE_EMAIL.success()` before setting `emailVerified=true`, rather than a `VERIFY_EMAIL` event. Therefore a higher-order handler observes only after the inherited validated, single-use update-email action completes successfully, and records a clock only when the current exact email matches the action token and is verified. The original token validation, pending-email checks and session behavior remain inherited.

Primary source references:

- [VerifyEmail.java](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/authentication/requiredactions/VerifyEmail.java): successful SEND_VERIFY_EMAIL after send; failed sends are errors.
- [VerifyEmailActionTokenHandler.java](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/authentication/actiontoken/verifyemail/VerifyEmailActionTokenHandler.java): exact current-email token validation; verified flag is set before successful VERIFY_EMAIL.
- [UpdateEmail.java](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/authentication/requiredactions/UpdateEmail.java): SEND_VERIFY_EMAIL names the pending destination, which can differ from current email.
- [UpdateEmailActionTokenHandler.java](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/authentication/actiontoken/updateemail/UpdateEmailActionTokenHandler.java): pending/exact-email checks, success event ordering and subsequent verified flag.

Generated access-token mappers carry these provider-owned attributes; members cannot edit them through the declared user profile. The API accepts clocks only from an already validated token with verified email, matching exact binding, sane ordering, no future date and no clock later than token issuance. Missing, legacy, malformed, out-of-order or differently bound evidence remains null. API observation/activation time is still recorded accurately. Missing upstream request-session evidence remains absent; it is never replaced by an assumption that the API activation session requested provider verification.

This prepares source, not installed provider acceptance. Existing main realms, providers, credentials and services were not changed. Legacy/admin/broker verification paths may lack trustworthy upstream clocks. No historical times are backfilled. Actual hosted/listener/action-token selection and real verification-email workflows remain acceptance gates; unknown clocks must remain explicitly distinguishable from recorded evidence.

## Verification handoff

Added policy/receipt checks cover observed clock precision, purpose-separated subject/issuer-bound session HMAC, missing-session uncertainty, encryption/tamper refusal, exact provider email binding, future/order rejection, legacy reads, unchanged lookup/retention and full-envelope key rotation. The existing phone browser suite now checks encrypted request evidence is actually persisted. Own-export and hold projections are wired to the same decryption boundary. Generated-profile tests protect admin-only bounded provider attributes.

No .NET build, database migration application, browser execution or owner service change ran in this subtask. The coordinating agent owns the next full check and owned phone/deletion browser window. Java compilation/policy verification is recorded separately after the final source-build completes. This record must not be read as real provider mailbox ownership or delivery assurance.

Coordinated current-source verification: `integrated-check-8` passed690 PostgreSQL persistence cases and all three HTTP smoke stages, zero build warnings/errors. Current-source Docker provider-build target passed. Actual installed email listener/action-token selection and phone browser acceptance remain unverified; the owner Keycloak realm was preserved.

Observed owned phone browser pass:43 checks against actual local API/Keycloak/PostgreSQL, with simulated SMS and the phone-required registration gate off. The encrypted request receipt was persisted and asserted. Evidence `e01430e008f64c3aa796ed74de35635f/suite-phone.log`; this runner later stopped at an unrelated mocked-Didit fixture assertion and cleaned up without errors. No real SMS, installed email listener or live mailbox acceptance is claimed.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
