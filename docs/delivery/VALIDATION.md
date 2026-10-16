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
