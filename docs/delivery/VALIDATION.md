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

## Investigation session continuity — 16 October 2026

Verified PR #4 MERGED, main 78b6628, no inline review findings and completed Codex review with no findings. Untracked owner design assets preserved. Baseline `./scripts/check.sh` passed before edits.

- Added HTTP regressions with signed synthetic ID tokens. Before fixes, stale reauthentication, all three silent continuity conflicts, changed reauth subject and superseded callback tests failed (303 instead of 400); overlap test timed out because provider calls could overlap. After locking and transaction checks they passed. A separate logout/pending-callback regression failed 303 versus 400 before generation invalidation, then passed.
- `node --test tests/auth-harness/*.test.mjs`: 31 cases pass. Includes synthetic signing-key rollover with overlap, new-key acceptance, removed-key rejection and unknown-key rejection. This is NOT live provider rotation. Negative authentication epoch rejected.
- `./scripts/docker-local.sh up`, `auth-init` and `node scripts/auth/provider-check.mjs`: retained-volume ARM64 API/web/realm smoke, idempotent local fixture/client setup and four actual provider rejection probes passed. No resets or fixture replacements.
- Actual headless installed Edge using Playwright: fictional fixture password login/code/S256, validated callback, silent SSO continuity, fresh password reauthentication, refresh, logout and actual refresh invalid_grant passed. Private driver reads the ignored fixture directly; no identities, credentials, codes or tokens printed. No screenshots or browser trace exported. Manual/visible browser and device accessibility acceptance not claimed.
- Read-only independent review checked consistency, asynchronous overlap and key tests; pending-callback logout issue was reproduced and fixed with a regression test.

The email-verified fixture remains administrator-asserted SIMULATION ONLY. No application authorization, production adapter, real email, social federation, linked-account transition, optional MFA/recovery, real signing-key rotation or mobile/device gate passed. Phase 1 remains incomplete. Actions remain disabled. No merge or deployment performed.

Final session-slice check: one run failed during NuGet restore with MSB4166 child-node termination (reported temporary diagnostic directory no longer existed on inspection). A clean rerun of the unchanged `./scripts/check.sh` passed: 31 Node cases, Release build zero warnings/errors, 50 access cases, typed client, canonical design/72 contrast pairs, disabled-workflow and HTTP smoke checks. Headless Edge journey reran successfully after the final logout guard. This records the transient build failure rather than omitting it.

## Independent discovery policy — 16 October 2026

Partial Phase 2 work; Phase 1 stays incomplete. Branch codex/discovery-eligibility-policy starts at e161f10 and depends on open PR #5 (both target main). No merge/deployment.

- Test-first build failed with missing Domain types, then 71 named discovery cases passed after implementation. They exercise all five resources, hidden FWB exclusion versus LTR+friendship allowance, reciprocal filters, ANY/ALL, contradictions/unknown values, inactive/underage/unverified members, account binding, contexts/blocks/incognito/sparse, request limits, disclosure confirmation and travel assertion identity/area/time/consent/risk. Snapshots are hypothetical trusted facts, not provider or API evidence.
- Temporary guard-removal mutations for exclusion, block, incognito refusal and missing traveller presence caused named failures; original code restored. An initial inverted travel branch mutation also failed a positive case; the actual bypass mutation was separately checked against the pre-arrival denial.
- `./scripts/check.sh` passed: Release build zero warnings/errors, 50 account and 71 discovery cases, 31 Node tests, typed client, design/72 contrast pairs, commented-workflow checks, API/web HTTP smoke and whitespace hygiene.
- Independent read-only review found no actionable issues and reran 71 discovery/50 access checks.

No UI changed; browser/device accessibility checks add no evidence for this pure policy. Docker unnecessary for dependency-free rules. No authenticated endpoint, live profiles/media, persistence, demographic/compatible-pair model, real identity or location source exists; no permission/verification/arrival simulation is presented as live. Existing chat/realtime membership and account/device acceptance remain separate gates.
GitHub review follow-up (finding on dependent PR #6): initial pending login had no refresh token, so the old logout guard returned before transaction invalidation. New HTTP regression failed (400 instead of successful local cancellation), then passed after moving generation invalidation before the refresh requirement. The same test proves the pending callback is denied without any provider call. 32 Node cases pass; cancellation without a provider token reports no revocation proof. The existing real-provider logout path is unchanged.

## Guest web appearance — 16 October 2026

Independent partial Phase 2 surface on branch codex/personal-web-appearance, based on a2991d6 and dependent on open PRs #5/#6. Concurrent owner branding commits were preserved in the original checkout; only task-owned appearance files moved to an isolated managed worktree. No owner naming decision inferred.

- Test-first Node run failed because appearance-core.js was absent; four tests then passed for 18 preference combinations/12 resolutions, whole-pair fallback, versioned minimal storage, malformed/unknown versions and storage denial. Unit/Node suite integrated into `./scripts/check.sh`.
- Canonical JSON now generates runtime `themes.css` and `appearance-palettes.js` via `python3 scripts/check_design.py --write`; `--check` enforces exact parity plus existing 72 contrast pairs. No canonical palette values changed.
- Actual headless installed Edge via `DATINGAPP_PLAYWRIGHT_PATH=<bundled playwright> node tests/appearance/web.browser.cjs`: all 18 radio choices and actual computed body/primary/onPrimary values match the 12 canonical pairs. System responds to emulated OS mode while explicit Light ignores it; reload, reset, actual two-tab storage events, native-radio arrow keys, complete enhanced navigation, malformed storage and 320px/200% text passed. Blocked-storage path uses an injected throwing getter and preserves in-memory selection with an unsaved notice; no actual cloud/network save exists.
- Browser tests initially exposed overflow at 320px/200% text. Stronger reflow, bounded padding and wrapping fixed it. Completed-enhancedload assertions then exposed root theme restoration to the SSR default during navigation; reapply state on every enhanced load, including routes without a form, fixed the regression. Browser suite passed afterward. Screenshots visually inspected; desktop Solar light and 320px Orchid dark retained in the slice document. A Playwright getByLabel probe included hidden preview text; final checks use the actual accessible radio role/name.
- Local host initially launched in Production from unpublished build output, causing missing fingerprinted assets. Restarting with ASPNETCORE_ENVIRONMENT=Development fixed it; commands clarified. Smoke now fetches appearance HTML and every linked JS/CSS asset with content-type checks, preventing HTML-only false positives.
- `./scripts/check.sh` passed in the isolated worktree: Release build zero warnings/errors, 32 auth tests, four appearance tests, 71 discovery and 50 account cases, typed client, canonical design, disabled workflows, runtime assets and API/web HTTP smoke.
- Retained-volume Compose runtime/asset smoke passed. One initial container startup conflicted with the host web process on 5081; stopped the host and reran successfully. The final isolated source additionally built with `docker build --target web -t datingapp-appearance-review -f containers/Dockerfile .` and ran only on 127.0.0.1:5281. The complete Edge suite passed against that published ARM64 container. No AMD64 runtime claim.
- Independent review found no remaining actionable issues; it also ran Node/canonical checks. Guest state never contains account identifiers, tokens or entitlement flags.

Gaps: no authenticated account sync/revision conflict or account switching, no MAUI rendering/build/device acceptance, no screen-reader/forced-colors/low-bandwidth first-paint certification. Full feature-surface appearance and semantic states remain future work. Static SSR plus external preference scripts does not resolve Q03 or complete Phase 1/2. No merge or deployment performed; local-only container is stopped after checks. Existing auth fixtures and volumes remain retained in the original checkout.

## Portable appearance account-state rules — 16 October 2026

Branch codex/appearance-account-sync-state based on 2baf863; depends on PR #7 (and inherited #5/#6). No real accounts, transport or persistence added.

- Test-first portable build failed because DTO/state types did not exist. First implementation build then failed CS8669 because generated code needs an explicit nullable context; generator now emits #nullable enable. Final 39 named state checks pass.
- Checks cover 18 supported pairs, loading/default display, existing-account precedence, guest initialization only for known absence, stable offline retry/change ID, newer local choices surviving old success, monotonic/content-checked revisions, duplicate/stale loads/saves/conflicts, switching away and back, sign-out clearing pending/conflict state and malformed inputs. An added unresolved-conflict test failed before including conflicts in HasPendingSave, then passed after the guard change.
- `./scripts/check.sh` passed: portable Release build zero warnings/errors, 39 appearance-state/71 discovery/50 account cases, 32 auth/four appearance Node cases, canonical generation/72 text pairs, disabled workflows, API/web/runtime-asset smoke and diff hygiene. Independent read-only review found no actionable issues and reran portable state and design checks.
- `dotnet workload list` still reports no installed mobile workloads (manifest 10.0.400.1); `xcode-select -p` reports installed Xcode. No mobile build/device acceptance attempted or claimed.

No UI changed in this slice, so no additional visual acceptance is claimed; prior screenshots apply only to the guest surface. No server revision/idempotency enforcement, real offline cloud retry, authenticated switching or MAUI integration is demonstrated. These remain integration gates. Client generation checks protect local state; they never authenticate or authorize server writes. No Docker needed for these pure client rules. Nothing merged or deployed.

## Fictional discovery vertical slice — 16 October 2026

Branch codex/fictional-discovery-preview based on d02ac61, dependent on PR #8 and inherited #5–7; all target main. Domain gate now runs before fixed fictional list/profile/media/contact projections. No real member account/provider/storage or message sending is enabled.

- `python3 scripts/prototype_smoke.py` failed enabled route 404 versus200 before implementation. Typed-client checks failed missing-class compilation before implementation. Initial Razor build exposed a page/DTO name collision, corrected with explicit DTO qualification.
- HTTP acceptance now passes API/Web default-off and Production-with-flag-off gates; fictional allowed list, hidden FWB/inactive/block omission, generic direct profile/media/contact404, substantive photo-free profile, sparse/incognito filters, upcoming versus simulated arrival, explicit disclosure, malformed/duplicate/unknown API inputs and safe metadata projection. The activity+ALL empty-result test failed400 before support, then passed.
- Independent review found Web bool query conversion threw500 before page error handling. Reproduced in actual Edge; replaced typed bool binding with validated raw string arrays. Web regressions now verify controlled invalid/unavailable states for malformed and duplicate booleans and unsupported options. No developer exception or real data is returned for these cases.
- Actual headless installed Edge browser suite passed grid → fictional profile → contact eligibility, sparse/incognito filters, disclosure checkbox (no message), ANY/ALL and empty result, upcoming versus simulated arrival, hidden direct profile, malformed queries, persisted personal Cobalt/Dark and320px/200% text. First browser tests caught select labels containing option text and enlarged dropdown overflow; explicit label-for, bounded grid/select sizing and shorter options fixed these. Screenshots visually inspected and selected fictional evidence committed in the slice document.
- API process deliberately stopped; actual Edge showed the People-unavailable/retry state without cards. No offline persistence or actual network retry implementation claimed. Loading is an SSR in-progress state, not separately certified under network throttling.
- Separate final ARM64 API/web images built with existing digest-pinned Docker targets. Ran only on loopback5280/5281 and an isolated disposable Docker network, using Development+explicit preview flag and server-to-server API name. Full Edge suite passed against published containers; direct published API positive/list and hidden profile/media/contact negatives passed. Initial readiness curl hit an empty response before startup; the unchanged ready hosts then passed. No public/dev deployment, AMD64 or device claim.
- `./scripts/check.sh` passes: portable Release zero warnings/errors,39 sync/71 discovery/50 account cases, typed prototype route and non-simulation payload rejection,32 auth/four appearance Node cases, canonical72 contrast/drift, disabled Actions, API/Web/assets and prototype HTTP acceptance. A first Web test harness readiness probe used the API health path and was corrected to Web root; production behavior was not implicated.

Verification, adult status, session and arrival facts in the preview are **SIMULATION ONLY**. Media is an abstract demo placeholder. No real ownership/adulthood/liveness, browser-authenticated member/privileged matrix, real media source, request/chat lifecycle, persistence, mobile/device or screen-reader acceptance. Phase 1/2 remain incomplete. Test hosts/containers are stopped; auth fixtures/volumes and owner branding work retained. Nothing merged or deployed.

## Actual local signing-key rotation — 16 October 2026

Investigation branch codex/keycloak-signing-rotation based on d100e3c; depends on PR #9 and inherited stack. Uses original matching retained-volume .local fixture in place; no secret copied to the managed worktree.

- New helper tests initially failed because rotation module did not exist. Final 39 auth cases pass, including temporary-provider success cleanup, failed exercise, existing-prefix refusal, lost create response and management-token expiry/401 handling. Expiry-guard removal produced the expected renewal-test failure; restored code passes.
- Actual headless Edge and local Keycloak 26.8.1/ARM64 passed twice: password/code/S256 login, validated original ID token, temporary higher-priority RSA provider, real published old/new JWKS overlap, original/new signature validation, refresh retaining subject/session/auth_time, removal of only the temporary component, restored active original key, removed-key token rejection against current JWKS, original-token acceptance, post-cleanup refresh and actual logout invalid_grant. No token, password, identity, key material or raw browser trace printed.
- Independent review identified the captured 60-second admin token could expire before finally cleanup. Refactored management helper to reacquire before expiry and retry once after 401; injected-provider tests pass. A real `rotation-check.mjs --delay-cleanup` run held the temporary key for 65 seconds and observed two actual management authorization acquisitions, then verified original-key restoration/JWKS removal and browser refresh/logout. Management grants are setup only, not browser-login evidence.
- Graceful SIGINT/SIGTERM close the browser to unwind into finally; force-kill/crash/provider-unavailability cleanup remains explicitly unguaranteed. A leftover experiment prefix makes reruns fail closed pending deliberate local inspection. No original key provider edited/deleted, no realm/volume reset.

The fixture's email ownership remains administrator-asserted SIMULATION. No production token validator/cache, live application authorization, social/linking, actual email, optional MFA/recovery or device acceptance was established. Privileged access stays disabled; Phase 1 remains incomplete. Full pre-commit check results and travel-review propagation are recorded in the progress ledger. Nothing merged or deployed.
