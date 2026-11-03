# Q117: Registered names and confirmation

Issue [#162](https://github.com/repasscloud/wanderconnect/issues/162), Q117, intake item24; owner decisions are recorded in D300. No database reset is authorized or performed.

## Authority and privacy

CONFIRMED: registered first and last names are required with an explicit one-name exception; provider-supplied names must be confirmed/completed at first sign-in; existing incomplete accounts complete before using member features. Public display names and restricted verified identity remain separate. Support/Admin/Root may see registered declarations and a registered-name/ID comparison flag without decrypting document names. Member name edits remove ID Verified and require a fresh optional ID check.

DEFAULT implementation, agreed during coordination: only validated Keycloak `given_name`, `family_name` and `swoovr_mononym` claims supply initial suggestions. Nothing from email or an unvalidated browser form establishes identity linkage. The application Account becomes the authority for declared registered names after explicit confirmation. Subsequent provider claims cannot overwrite member corrections, restore an old surname or reverse a revoked badge. There is no provider/application dual-write; Keycloak remains authentication authority, keyed by the same issuer and subject.

Registered names are plain private account data, not verified identity. Ordinary staff routes do not open/decrypt the D191 store. Names and prior names are not copied into audit details; audits contain account ids, revision numbers and machine actions. An approved Didit result computes only a nullable comparison flag from the in-memory provider fields already being processed. Null means no current comparison; false means the strict comparison did not match, not proof of impersonation. DEFAULT comparison is Unicode-normalized, case-insensitive exact first/last comparison, with absent surname for a mononym; no transliteration, fuzzy matching or inferred equivalence is claimed. It does not make optional ID verification mandatory.

## Implementation

Migration `20261102130000_RegisteredNames` adds nullable registered name/confirmation data, explicit mononym and revision fields, a nullable ID comparison flag, and the registered-name revision attached to each ID attempt. Existing names/public profiles are preserved. No surname, confirmation or verified match is invented during migration.

The member own-name GET/PUT routes use existing authentication and API version selection. A confirmed edit needs an expected revision; account lifecycle serialization prevents competing writers and verification decisions from racing. Invalid names and stale revisions do not mutate state. The API validates international Unicode letters/combining marks, name punctuation and bounded length, rejects controls/markup, and requires either a valid surname or an explicit one-name declaration. The public name is initialized from first name only when unset, never from surname; existing custom public and per-mode names are retained.

On a changed or initially confirmed declaration, an existing ID Verified badge is revoked, current document-DOB badge evidence is cleared, and a fresh optional verification attempt becomes available. In-flight checks for the old declarations are expired. Every new Didit session captures the current registered-name revision. A late old-name provider approval cannot restore ID Verified or its comparison flag. A confirmed underage result still locks the account even when the name revision is stale.

MemberAsync blocks ordinary member feature requests for incomplete declarations, while essential account summary/data/notices/appeals remain available under their existing exemptions. Account summary exposes a completion-required flag. Web Me shows the declaration form and completion gate; native Me routes to RegisteredNamesPage and exposes a registered-names editor after completion. Submitted values remain available on validation errors. Both explain that an edit revokes ID Verified and that declared names are distinct from public/verified names.

Admin owns an authorized registered-name projection at `/api/admin/accounts/{id}/registered-names`. Q110/Q111 staff search/detail integration is coordinated separately from this slice. The member data export must include the declared fields as own account data. Deletion tombstoning clears names, mononym, confirmation and comparison data; verified-record retention remains independently governed.

## Provider registration source and installation boundary

The generated Keycloak user profile adds surname and a private one-name declaration attribute; first/last names are bounded at100 characters. The theme adds a one-name checkbox and native surname-required behavior, retaining values on validation errors. The pinned guarded registration factory validates the surname/one-name condition server-side before inherited account creation and stores the explicit exception as a mapped claim. Surname is not unconditionally required in declarative profile JSON because that cannot express the cross-field mononym exception; the guarded creation action enforces the condition. First broker login can supply incomplete names; the application completion gate requires confirmation before feature access.

These changes are source, not proof of installed provider behavior. As with Q112, the existing provider artifact must be compiled against pinned Keycloak26.8.1, installed and verified on every instance before claiming direct-registration enforcement. This run did not install a JAR, apply realm configuration or start/modify a provider. Existing browser sessions can lack new claims; they must complete names in the application rather than receiving fabricated values. No Apple/Google account or delivery capability was provisioned.

## Verification handoff

New domain checks cover international names, mononym requirements, controls/markup, public-name isolation and strict comparison. New disposable PostgreSQL checks cover safe migration, unconfirmed provider suggestions, explicit confirmation, stale provider claims, revision conflict, audit privacy, badge revocation/retry, late old-session approval refusal, fresh comparison without legal decryption, mononym editing and tombstone erasure. Java policy checks cover the same surname/one-name and unsafe-input conditions. Generated-profile tests assert the declared surname/mononym schema. Fictional fixture token helpers explicitly confirm their invented names; this is test setup, not runtime auto-confirmation.

At handoff, new .NET/Java/browser acceptance has not run. The pure source realm generator was run to update the three committed imports and reported its internal invariants passed. Required coordinated checks: `./scripts/check.sh`, pinned Java provider compilation/tests, owned browser direct-name and existing-account completion/edit/error scenarios, stale/concurrent edits, optional Didit recheck and late-result refusal, authorized staff projection/privacy, deletion/export, and native build/device acceptance. Synthetic/unit provider responses do not establish actual Apple/Google or Didit interoperability.

No real identity data, screenshot or credential was accessed or published. No commit, merge, deployment, service start, browser run, database reset or Actions change was performed by this subtask.

## Subsequent source verification

The coordinating agent authorized a disposable Docker source-build target (no provider service or installation). `docker build -f containers/uat/Keycloak.Dockerfile --target provider-build -t swoovr-registration-provider:source-check .` completed successfully on the pinned Java21/Keycloak26.8.1 sources. It compiled all provider sources and tests, passed15 fictional retirement-policy checks,162 MailerSend loopback HTTP assertions, and the registration-admission/name-validation checks. The pre-existing unsupported JPA `getEntities()` removal warning remains explicit. Private source-build log: `/tmp/swoovr-registration-java-check.log`; no secrets or runtime realm/database material were supplied. This establishes compilation and local policy checks only, not installed factory selection or precreation enforcement.

Final native compilation checkpoint (3 November): Android Debug and iOS simulator-arm64 Debug both passed, zero warnings/errors (`android-final.log`, `ios-final.log` in private overnight evidence). These are compilation checks, not physical-device, provider or store acceptance.

Observed full owned accounts suite pass:168 checks using real local Keycloak tokens, API, PostgreSQL and BFF, including role boundaries, Root MFA evidence, DOB correction grants/replay and account recovery. Evidence `e01430e008f64c3aa796ed74de35635f/suite-accounts.log`; subsequent unrelated mocked-Didit fixture failure did not invalidate this suite pass. Owned cleanup completed without errors.

Observed mocked Didit adapter pass:144 checks, including stored-ID matching/mismatching DOB corrections, grant/review behavior, signed webhook negatives and BFF flows. Run `f6fe8728f3ba4a5f97b1b7bc667354c3`, runner exit0 and cleanup without errors. Provider session creation/decisions used an owned local stand-in with synthetic keys; no live Didit, document or identity acceptance.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
