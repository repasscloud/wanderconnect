# Foundation validation — 16 October 2026

## Passed locally

- `./scripts/check.sh`: .NET 10.0.400 portable restore and Release build; zero warnings, zero errors.
- Dependency-free executable checks: typed API route, status JSON contract and empty response rejection.
- Design checks: six stable IDs, twelve complete token sets, 72 specified solid text contrast pairs at ≥4.5:1, specification/studio parity, relative Markdown destinations and fully commented Actions files.
- HTTP smoke: built API `/health/live` and `/api/v1/status` returned expected JSON; built Blazor root returned foundation HTML. Both processes stopped automatically.
- `node --check` against the extracted studio JavaScript: syntax valid. Export selector inspected for both scheme ID and mode.
- Read-only independent review: no blocking foundation findings; corrected the old handoff sentence that delayed authentication beyond phase 1.
- `git diff --check`: no whitespace errors.

## Explicit gaps

- Android/iOS builds and device interaction were not run: no MAUI workloads installed. Full DatingApp.sln includes Mobile; DatingApp.Local.sln validates only portable projects.
- Local HTML browser preview was attempted but blocked by the browser tool's file-URL security policy. No workaround was attempted. Studio visual/interactivity inspection remains unverified; data parity, contrast and syntax are checked.
- No Keycloak contract, endpoint, sandbox or credentials supplied. Authentication policies are specified, not connected or validated against a live backend. Document/liveness verification is separately unresolved.
- No database, billing, real messages, deployment or production acceptance tests exist in this foundation. Docker was unnecessary for these dependency-free host checks.
- GitHub created an invalid-workflow failure for the fully commented file under `.github/workflows` (zero jobs). Moved the still-commented YAML to `.github/workflow-templates` to prevent further workflow entries. No runner jobs executed.

Phase 0 provides a reviewable scaffold and delivery specification. Phases 1–5 remain planned in PHASES.md. This record does not certify accessibility, provider security or mobile readiness.


## Phase 1 account access policy slice — 16 October 2026 (before owner correction)

Passed locally on .NET SDK 10.0.400:

- `./scripts/check.sh`: portable restore and Release build, zero warnings/errors; 50 account access policy cases; existing typed-client checks; 72 palette contrast pairs and canonical documentation checks; disabled workflow checks; API and Blazor HTTP smoke; staged/unstaged whitespace checks.
- Test-first compilation initially failed because the new Domain types did not exist. During implementation, collection-expression compilation errors in test fixtures were corrected using explicit HashSet values.
- Four temporary guard-removal mutations each produced a named policy failure: social-to-staff bypass, wrong venue, role elevation and inactive/revoked session. Original guards were restored before the passing full check. This establishes that the negative cases detect those regressions.
- Read-only independent code review: no blocking findings; reviewer also ran the executable checks.
- `dotnet workload list`: no installed workloads. No Android/iOS build or device test claimed.

Evidence is for pure application policy, not live authentication. No Keycloak source/version/sandbox is configured, no adapter maps trusted method evidence, and no API handler invokes the policy yet. Direct API/callback replay, account-linking, issuer/audience/signature/expiry/rotation, email-link scanner/replay/concurrency and optional MFA enrolment/recovery remain untested integration gates. Application role provisioning, persistence and revocation propagation remain unimplemented. See [slice](PHASE-1-ACCESS-POLICY.md).

No UI changed; no new screenshot or browser/device acceptance is claimed. Six schemes × System/Light/Dark still require phase 2 implementation. Docker was unnecessary for these dependency-free checks. Actions remain commented under workflow-templates, with no active workflow added. Phase 1 remains IN PROGRESS; nothing was merged or deployed by this slice.


## Keycloak owner correction — 16 October 2026 (before Q10 answer)

Owner supplied the official Keycloak documentation and source; earlier entries above describe the pre-clarification snapshot. Repository inspection found no existing version pin, realm export, sandbox/container deployment or provider settings. Official upstream downloads/release and admin/container documentation were read. D11–13 and the [development proposal](../architecture/KEYCLOAK-DEVELOPMENT.md) record the corrected provider, proposed 26.8.1 pin, isolated realm and four distinct evidence categories.

The exclusive staff-account assumption was withdrawn. The Domain input is now named `HasApprovedPrivilegedEmailEvidence`, a reserved future server verdict, not an account classifier. Its evaluator remains unimplemented pending Q10; no adapter or realm was configured. Existing policy checks use hypothetical verdicts only. Linked identity records do not determine a session's method. Government ID/liveness remains separate.

Follow-up `./scripts/check.sh` passed: portable Release build with zero warnings/errors, all 50 policy cases, existing client checks, design/disabled-workflow checks, API/web HTTP smoke and diff hygiene. Independent read-only follow-up review found no blocking findings and reran the executable checks. No Docker image pull, sandbox startup, login, provider credential setup or device validation is claimed.


## Q10 owner decision — 16 October 2026

D14 confirms linked Apple/Google identities are allowed, with verified email and fresh direct email/password or approved email-link authentication required for privileged access. Updated repository instructions, product and architecture specifications and decision/question registers. No evidence evaluator, freshness window or provider configuration was invented; integration remains disabled pending validated evidence. Existing 50 policy cases still demonstrate downstream composition with hypothetical verdicts, not live provider authentication.

Q10 follow-up validation: `./scripts/check.sh` passed with zero build warnings/errors, all 50 policy cases, typed-client checks, design/disabled-workflow checks, API/web HTTP smoke and diff hygiene. Only policy documentation and a source comment changed; no provider integration was enabled.

## Local Docker and Keycloak sandbox — 16 October 2026

Current inspection supersedes the earlier no-sandbox snapshots above. PR #2 is MERGED at main e0e76ff (head 99665b5); GitHub review submissions/comments showed no newer review beyond the superseded finding and owner resolution. Working tree was clean before this slice; new branch codex/local-docker-keycloak. Nothing was merged or deployed by this work.

Passed locally:

- Docker Desktop 4.95.0, Engine 29.8.2, Compose v5.6.0; executed Linux ARM64 images. Registry index inspection confirmed ARM64 and AMD64 manifests, but AMD64 runtime was not tested.
- Official Keycloak downloads/release rechecked at 26.8.1; registry resolved to the index digest pinned in compose.yaml. SDK 10.0.400 and ASP.NET 10.0 runtime index digests are pinned in containers/Dockerfile.
- `./scripts/docker-local.sh init`: generated private ignored .local/docker.env without printing credentials; repeated init retained it. Permissions verified: directory 700, file 600. `git check-ignore` confirmed exclusion.
- `./scripts/docker-local.sh up`: separate API and web Release publish/image builds, successful Keycloak startup and realm import, management readiness health check healthy. API and web process identity checked as uid/gid 1654 (non-root). Published ports inspected as 127.0.0.1 only; Keycloak management port is not published.
- Docker HTTP smoke passed: API liveness/status JSON, actual web foundation HTML, exact local realm issuer, nonempty published signing keys and advertised PKCE S256. No login or token-validation evidence is inferred from discovery.
- `down` followed by `up`: startup and HTTP smoke passed again with the retained volume. Keycloak logs explicitly showed existing datingapp-dev realm import skipped. No destructive volume reset was needed or tested.
- `./scripts/check.sh`: portable Release restore/build, zero warnings/errors; 50 policy cases, typed-client checks, canonical design/72 contrast pairs, disabled-workflow checks, API/web host HTTP smoke and diff hygiene.
- `bash -n scripts/docker-local.sh` and Python smoke-script compilation passed. Independent read-only container review found no blocking findings; final evidence recording was the only follow-up requested.

Gaps: no app OAuth clients, direct/social/linked-account browser login, token validation, AMR/broker mappings, verified-email delivery, SSO/refresh/reauthentication, optional MFA/recovery, logout/revocation/rotation or email-link capability was tested. Only metadata and runtime readiness were observed. The [investigation matrix](PHASE-1-KEYCLOAK-SANDBOX.md) preserves these gates. No evidence evaluator or protected endpoint is enabled. Mobile workloads/build/device acceptance, interactive web choice, public/dev production container settings and all remaining Phase 1 exit criteria are open. No visual/browser acceptance is claimed; product UI and theme tokens are unchanged. Actions remain untouched and disabled.

## Local browser investigation — 16 October 2026

Started from clean main bc9fed1 after verifying PR #3 MERGED, no inline review comments and no Codex findings. GitGuardian's Compose finding is an environment-variable requirement expression, not a literal secret. New branch codex/keycloak-browser-investigation; no merge/deployment performed.

Passed locally:

- Node 26.11.1 built-in test/runtime APIs, no npm dependencies. Initial tests failed because the security module did not exist; implemented signature/claim/transaction checks and watched them pass. Refresh nonce test failed before adding the narrowly scoped OIDC 12.2 refresh exception. Sanitized provider-status and stale-fixture regression cases also failed before implementation. Final suite has 20 cases, including a signed positive callback, refresh subject/session/auth_time conflicts, logout invalid_grant versus network failure, Host/Origin/CSRF, duplicate/unbound/replayed callback, state expiry, signature/algorithm/key, issuer/audience/azp/nonce/time/subject/session negatives. Injected-provider cases are automated harness tests, not real Keycloak evidence.
- `./scripts/docker-local.sh auth-init` succeeded repeatedly against the retained local Keycloak volume. It configured the investigation client, copied flow, AMR maxAge, broker-note mapper and explicit basic/profile/email scopes; client/scopes were read back. Initial missing auth_time exposed that updating the client representation alone did not attach the basic scope; the supported default-client-scope endpoint fixed this. Custom unmanaged user attributes were not retained under the realm profile policy; private fixture ID binding replaced that assumption. No password/identity/token was printed.
- `node scripts/auth/provider-check.mjs` passed against Keycloak 26.8.1: unlisted callback rejected with HTTP 400/no redirect; missing PKCE and plain PKCE rejected with invalid_request to the single allowed callback; application password grant rejected with unauthorized_client. The bootstrap admin grant is management setup only.
- Actual macOS Edge browser: authorization-code/S256 redirect, private fictional-adult password entry, browser-bound callback, RS256 ID-token validation and positive `datingapp:password` AMR. Forced login/max_age=0 displayed a password challenge; successful reauthentication reported authentication age zero. Refresh at 7 seconds and silent SSO at 24 seconds retained the original auth_time and session; newly issued tokens did not reset freshness. Password AMR was present at 52 seconds and absent at 80 seconds with the configured 60-second execution maxAge. Repeated final-flow tests passed after adding the ownership-marked v1 flow (fresh login, refresh age 6, silent SSO age 11). Provider logout then actual HTTP 400 invalid_grant on refresh was observed. No access/refresh tokens or fixture identities appear in the report. Private screenshots retained under ignored .local; no public browser trace exported.
- Browser testing caught no-referrer producing null Origin for POST and form-action self blocking the provider redirect. Corrected to same-origin referrers and a fixed explicit provider form destination; exact Origin enforcement remains enabled. Chrome connector was unavailable, so native installed Edge was used. No claim of cross-browser/device acceptance.
- Created a separate Compose project `datingapp-auth-repro` with a fresh isolated volume, leaving the original volume intact. API/web/realm smoke, provisioning twice, provider negatives and a real password browser login all passed on Linux ARM64. Fixture user-ID replacement during this development check exposed the reviewer's stale-ID concern; final code now refuses a missing recorded ID and tests that guard. No reset/volume deletion is claimed. See private fixture/volume lifecycle notes in the browser slice.
- Private directory/file modes verified as 700/600, and `git check-ignore .local/auth-fixture.json` confirmed exclusion.
- `./scripts/check.sh` passed: portable Release build with zero warnings/errors, 50 Domain access-policy cases, typed-client checks, design/72 contrast pairs, disabled Actions checks and real API/web HTTP smoke. Final post-review command/results are recorded below.

Read-only independent review found one P2 stale-fixture issue; fixed it to refuse silent rebinding, and added successful-path/continuity/logout regression coverage requested by the reviewer. No production application code or theme tokens changed. The fixture's verified-email status is an administrator-asserted SIMULATION; no actual email delivery/ownership or government-ID/liveness was tested. AMR demonstrates only the configured local password execution; broker/linked-account/optional-MFA transitions remain untested. The handwritten verifier is isolated investigation tooling, not an API/BFF adapter. API token authorization, product freshness policy, persisted roles/venue scope, real mail/recovery, Apple/Google credentials, rotation, mobile builds/devices and all remaining Phase 1 exit criteria stay OPEN. Privileged access remains disabled. Actions remain commented. Nothing is merged or deployed by this slice.

Final post-review verification: `./scripts/check.sh` exited 0 (20 harness tests, 50 Domain cases, portable Release build with zero warnings/errors, typed client, design/workflow and API/web smoke). Independent follow-up review reran 20/20 tests and found no remaining blockers. Shell/Node syntax and diff hygiene checks passed. Restored the original retained volume's matching private fixture, reran Docker startup/smoke, provisioning twice and all four provider negatives successfully. Both Compose projects and the harness were then stopped; both disposable volumes and matching private fixture files retained. No real credentials, tokens, private screenshots or identities were staged.
