# Q93 — managed-stack owned browser runner

`tests/auth/run-owned.cjs` adds a separate runner for the managed Docker infrastructure. The older meeting harness remains separate; its fixture policy/authentication assertions were adapted, but it is not used against the managed Docker stack. This runner does not adopt listeners, stop Docker containers, replace owner Web/Admin processes, write shared configuration, change realms/flows or use the owner's root account.

Run only after the coordinating build window has produced the selected Debug/Release artifacts:

```sh
SWOOVR_OWNED_TEST_ENV="$PWD/.local/platform/docker.env" \
SWOOVR_OWNED_CONFIGURATION=Release \
SWOOVR_OWNED_SUITES=onboarding,mode-names,mode-photos,review-workflows,accounts \
SWOOVR_PLAYWRIGHT_PATH=/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright \
node tests/auth/run-owned.cjs
```

The finite explicit selector accepts `members`, `onboarding`, `mode-names`, `mode-photos`, `review-workflows`, `accounts`, `didit`, `phone`, `deletion`, `messaging-lifetime` and (since 8 November 2026, #257) `modes`, `badges`, `matching`, `location-privacy`, `traveller-search`, `near-me` and `near-me-discovery`; there is no default suite set and no legacy meeting flow selected implicitly. Unknown, duplicate and path-shaped selectors refuse before provisioning. The `members` suite exercises registration, reset/passkey and sign-in; it uses the cloned member client in its handwritten code exchanges.

`messaging-lifetime` is the focused [#240 browser regression](ACCOUNT-DETAIL.md#browser-messaging-across-token-expiry-240-8-november-2026).
Use the command above with `SWOOVR_OWNED_SUITES=messaging-lifetime`; allow about seven minutes for provisioning,
the 370-second chat observation and revocation checks. It uses the unchanged local 300-second token lifetime,
two disposable adult member accounts and owned Edge contexts. The owned API's `Mfa:StaffClientId` matches the
cloned staff OIDC client; the suite completes actual Admin same-session MFA elevation before using Sign out
everywhere. It does not mutate realm settings or owner sessions.

## Ownership and secrets

- Reads only the explicitly selected absolute managed `docker.env`, with private-file permissions. It derives a temporary private environment containing only the infrastructure credentials needed for local PostgreSQL/Keycloak, cloned-client secrets and new synthetic application/provider keys. Real Didit/R2/provider secrets are not copied.
- Reserves distinct free loopback API/Web/Admin ports and registers temporary cloned OIDC clients with those callback ports. Existing clients, realm policy and browser flows remain unchanged. Before acceptance, listener PID checks require each selected port to belong to the runner's spawned host; no unrelated listener is signalled.
- Creates a unique `swoovr_registry_ui_<32 hex>` database, applies migrations and imports committed public GeoNames fixtures. Root authority is granted by the existing CLI only in that database to a new fictional staff subject with its own seeded test TOTP credential. The owner root fixture is never read or modified.
- Shared helpers accept extended Web/Admin/env/PostgreSQL/root/client overrides only as a matching private manifest pair. Legacy API/database overrides retain their previous behavior. Operator commands use the selected artifact and owned connection; SQL addresses only the disposable database through the managed PostgreSQL container on port 5433.
- Test-created users request a run-specific Keycloak attribute and their actual creation-Location provider IDs are journalled. The managed user profile strips optional attributes, so ownership independently requires that journalled creation ID, the predeclared exact fictional email/username containing the run ID, and the provider creation timestamp at or after run start; an ID or tag alone never authorises mutation/deletion. Browser self-registration journals its unique expected fictional address first, then claims and tags that exact newly created identity. Cleanup verifies ownership before deleting a user ID or client. An unknown/mismatched identity is retained and cleanup fails visibly.
- Suite processes run in owned process groups. Logs and browser output are private under `.local/owned-browser-runs/<run id>/`; secrets/MFA fixtures and local media live in a private temporary directory removed after successful cleanup. Cleanup removes only spawned hosts/process groups, run-owned clients/users, the disposable database and temporary media. A cleanup error makes the run fail, retaining private recovery evidence rather than reporting success.

The shared Mailpit sink and existing local Keycloak/PostgreSQL infrastructure remain running. SMTP destinations are fictional fixtures. API deletion jobs may use the existing local members-management service credential only against subjects in the disposable database; no owner account subject is seeded into it.

## Simulation boundaries

Photo screening is explicitly Development-only `blocked` simulation, never a false passing/clean result. Mode-photo fixtures seed labelled synthetic approved metadata to test placement/projection, not screening accuracy. The review suite gets its required blocked re-screen simulation.

The `didit` selector uses an owned loopback HTTP stand-in with synthetic keys/workflow/session records. No request or media goes to Didit. Its output explicitly says mocked adapter acceptance, not live provider validation; generated hosted links are inspected but not navigated. This does not establish Didit availability or identity verification. The legacy non-owned Didit suite retains its existing external test-mode behavior.

## Evidence

`node --test tests/auth/owned-runner.test.cjs` passed 7 tests: finite selection; paired ownership/loopback/path/client refusal; dotenv data parsing; RFC email local-part limits (including renamed fixtures); explicit Keycloak Base32 TOTP encoding checked against the independent RFC6238 vector; and journalled fixture ownership even when optional tags are stripped, with foreign ID/name/time refusals. These tests use mocked management transport, no browser, database, host or actual Keycloak mutation. JavaScript syntax checks and task-owned diff whitespace checks passed.

The first coordinated actual onboarding run created its owned hosts/clients/database, then failed fixture creation with Keycloak `error-invalid-email`: including a full run ID, long label and timestamp exceeded the RFC64-character email local-part limit. Cleanup initially refused the owned root because the realm strips optional attributes. Exact-run recovery removed only the journalled root after matching its expected run-bound identity and creation time, confirmed provider404, confirmed zero remaining cloned clients and removed the private runtime directory; evidence is `.local/owned-browser-runs/d2392d133c094952a482d612f75b32ed/recovery.json`. Compact fixture addresses and the independent ownership proof correct both failures. Root integration owns the retry/browser window. Successful browser/MFA/cleanup acceptance remains to be observed before claiming a passing run. No merge, deployment or Actions change is implied.


Coordinated retry: **onboarding passed, process exit0** using Release artifacts and actual owned Edge/Keycloak/API/BFF. It verified DOB-only setup recovery with tabs retained, confirmed first DOB and saved read-only lock, advice dismissal across reload/session, unknown revision refusal, exact retry timestamp and registered-name recovery on Explore/Connections/Chats. Evidence: `.local/owned-browser-runs/e5eefeb965d1412c86ce3e5eb1fd6f81/`, runner log `.local/overnight-delivery/owned-onboarding-retry.log`. Cleanup reported no errors; independent read-only checks found the disposable database absent, zero run-matching users and cloned clients in both realms, and zero listeners on all three owned ports. The private runtime directory was removed. No owner listener/container/configuration changed and no .NET build was run during this slice.

This pass does not exercise the separate phone-enabled setup path, root MFA login, other selected suites, live Didit or screening accuracy. The seeded OTP credential explicitly declares `secretEncoding: BASE32` under Keycloak26.8.1; its fixture constructor is checked against the independent RFC6238 vector. [Pinned native credential decoding](https://github.com/keycloak/keycloak/blob/26.8.1/server-spi/src/main/java/org/keycloak/models/credential/OTPCredentialModel.java) distinguishes this from the default raw UTF8 secret encoding.

Further fixture corrections: the legacy accounts scenario now expects owner-approved invite-only wording and uses an absolute fictional beta-import path when the owned CLI runs from the API content root. The runner has explicit phone/deletion selectors; deletion alone sets a45-second reauthentication window only inside its owned hosts. The seven ownership-safety tests are included in `check.sh`.

Observed independent suite passes on owned resources: accounts168 checks and phone43 (run e01430e008f64c3aa796ed74de35635f), mocked Didit144 (f6fe8728f3ba4a5f97b1b7bc667354c3), deletion/export123 (309c9dbb51c6446686cf5063b621cd68), mode names, mode photos and review workflows. The combined e014 runner later stopped at a stale mocked-Didit fixture; that fixture was corrected and the independent144-check retry passed. All these runs completed cleanup without errors. Deletion now requires its own selection because its45-second reauthentication configuration differs from the other suites. The legacy members installed-theme gap is recorded separately; it is never a full suite pass by inference.

Final legacy members run `fc8072f43de1441ea9d0cebbc2b8cf5f` completed the independent authentication checks, including all seven API negative cases returning401, then exited1 deliberately for collected installed-surface failures: light background250 instead of255, dark10 instead of22 (#143), and no installed surname field (#162 provider/profile rollout). The missing surname is recorded as a failure while the application declaration-completion path still runs; no product check is represented as passing or disabled. Cleanup succeeded. The owner realm/theme/provider was not changed. Q93 acceptance remains partial pending that owner rollout/verification, not a full suite pass.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
