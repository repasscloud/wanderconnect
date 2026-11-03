# Q116 — confirmed dates and authorised correction

2 November 2026. Implements CONFIRMED [D299](DECISIONS.md), [#161 owner answer](https://github.com/repasscloud/wanderconnect/issues/161#issuecomment-6572733484). D299 supersedes D158's unverified self-edit permission. D166's separate provider-mismatch prompt remains available.

## Behavior and authority

The first ordinary date save needs explicit confirmation. Every populated date, including every existing unverified date, locks immediately by its presence. The migration never rewrites dates and creates no temporary edit window. Web shows the saved date read-only; native disables its date control. Both require confirmation before saving and link to `https://swoovr.com/support` for correction requests.

Admin/Root can grant one member-bound correction for exactly 24 hours, with a reason and an audit event; Support and Legal have no grant permission by themselves. A new grant revokes the old unused grant. Admin can revoke an unused grant. Existing staff direct-session, verified-email and conflict rules apply. Ordinary correction and grant/revoke operations serialize by account lifecycle authority. The corrected date, encrypted prior-date history, audit and conditional one-use consumption commit in one transaction; invalid or unconfirmed submissions do not consume the grant. A database conditional update independently refuses expired, revoked, consumed and wrong-member grants; a partial unique index prevents multiple unreleased grants.

An ID-verified correction compares the saved encrypted D191 document date, without a new provider scan. A match marks DOB verified; mismatch/missing stored document date opens manual DOB review. The public ID badge stays verified. Underage dates still lock access, and discovery derives age from the updated date. Existing provider-mismatch correction now also serializes, preserves the previous date encrypted and recalculates underage eligibility. Its provider-failure behavior and one-time prompt remain authoritative.

## Restricted data lifecycle

`restricted.dob_change_grants` stores correction authority metadata and no date. `restricted.dob_change_histories` stores versioned AES-GCM ciphertext under the identity-store encryption keys; the encrypted payload includes its member binding. No prior date enters ordinary audit events. Legal history opening requires the Legal role, purpose, a live `identity-open` elevation and existing conflict checks, and records access with IDs/purpose. Admin/Root do not inherit Legal decryption.

DEFAULT application of the existing D191 identity schedule: prior dates remain during account life, erase at ordinary final deletion, and receive the existing two-year identity window for terminated accounts. Holds preserve both history and grant provenance into encrypted `identity-record` evidence; held history sources defer expiry. Member-owned export includes their previous dates without staff IDs or grant reasons. Identity key rotation/status includes prior-date ciphertext. Grant metadata erases at final deletion after any required preservation capture. No policy draft is approved by this source change.

New API resources: Admin `GET/POST /api/admin/accounts/{id}/dob-correction`, `POST /api/admin/accounts/{id}/dob-correction/{grantId}/revoke`; Legal `POST /api/admin/accounts/{id}/dob-history`. `AgeUpdate.confirmed` defaults false; own account summary adds nullable `dateOfBirthCorrectionUntil`. Existing clients must explicitly confirm first saves. The Admin account page embeds grant/revoke tools and a separate purpose-gated Legal history page.

## Evidence and gaps

Regression cases were written before the implementation. No builds were run by this slice while the integration build gate was reserved by the root task. `node --check` passed for the changed account, settings, profile, verification and shared token harnesses; `git diff --check` passed.

Added domain checks cover initial confirmation, all-populated locking, role policy, wrong member, exact expiry, revocation/replay and encrypted-history member binding. New real-PostgreSQL checks use the existing runner's disposable database: conditional consumption, rollback of date/history/consumption together, two independent concurrent consumers with exactly one winner, ciphertext storage, own export and member-scoped history. Account browser/API cases cover confirmation, locking, Admin grant/revoke/replay, root denial of Legal history and read-only presentation. Didit cases cover stored-identity mismatch/manual review/retained ID badge followed by a matching authorised correction. Fixtures now confirm ordinary initial dates explicitly; the existing underage correction scenario obtains an actual Admin grant first.

These new executable checks have not yet been run at this record's creation. Root integration owns `./scripts/check.sh` and the browser validation window. No installed database migration, service restart/reset, image rebuild, commit, merge, deployment or Actions change was performed by this slice. Native compilation/device acceptance, assistive technology acceptance and hosted key/database separation remain unverified.

Retention implementation DEFAULT, aligned with D191: encrypted previous-DOB history remains during the active account lifetime; ordinary deletion erases it unless covered by a hold; terminated-account retention follows the existing two-year schedule and hold override. This is an implementation schedule, not a claim of hosted retention acceptance.

Final native compilation checkpoint (3 November): Android Debug and iOS simulator-arm64 Debug both passed, zero warnings/errors (`android-final.log`, `ios-final.log` in private overnight evidence). These are compilation checks, not physical-device, provider or store acceptance.

Observed full owned accounts suite pass:168 checks using real local Keycloak tokens, API, PostgreSQL and BFF, including role boundaries, Root MFA evidence, DOB correction grants/replay and account recovery. Evidence `e01430e008f64c3aa796ed74de35635f/suite-accounts.log`; subsequent unrelated mocked-Didit fixture failure did not invalidate this suite pass. Owned cleanup completed without errors.

Observed mocked Didit adapter pass:144 checks, including stored-ID matching/mismatching DOB corrections, grant/review behavior, signed webhook negatives and BFF flows. Run `f6fe8728f3ba4a5f97b1b7bc667354c3`, runner exit0 and cleanup without errors. Provider session creation/decisions used an owned local stand-in with synthetic keys; no live Didit, document or identity acceptance.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
