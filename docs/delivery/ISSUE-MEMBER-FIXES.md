# Q106–Q109 member workflow fixes

Current status checked 2 November 2026: the implementation PR is merged into main (#153 for authentication, #154 for member workflows). The validation and worktree instructions below are historical per-slice evidence; remaining gaps still apply. [Current intake and follow-up dependencies](ISSUE-INTAKE-2026-11-02.md). No new runtime acceptance is claimed by this status correction.

2 November 2026. These independently actionable changes preserve the existing member gates and confirmed product rules. The coordination handoff is maintained on [the Admin work branch](https://github.com/repasscloud/wanderconnect/blob/codex/fix/intake-admin-account-errors/docs/delivery/ISSUE-FIX-HANDOFF.md).

## Changes and boundaries

- Q106 / #148 — partial: grouped account settings and a truthful account heading while prerequisites remain incomplete. Account already had a wider desktop layout. Phone-before-DOB, existing safety advice and draft legal disclosures remain. Onboarding-order and revision-persisted dismissal proposals remain deferred.
- Q107 / #149 — partial: 960px responsive profile workspace, paired desktop fields/mode cards, labels above values (including selected home city), compact mode checkboxes, explicit Save profile and scoped mode-save feedback. Custom names and explicit main-about consent remain. Fictional testing verifies visibility can be switched off and persists. A blank initial display name comes from a separate unset application profile field; no automatic overwrite or provider-to-public-name mapping is introduced. Any automatic initial-name rule remains a follow-up product/data-mapping dependency.
- Q108 / #150 — partial: compact mode selector shows one assignment gallery at a time; shared library remains visible. Existing shared main photo, API-supplied quotas, per-mode assignments and screening/downgrade rules remain. Independent main photos and proposed 60/8/16/24 limits require the deferred owner decision; no data migration is proposed.
- Q109 / #151 — proposed fix: wider matching workspace with readable labels, explicit Save identity, selected-intention counts and unavailable/overlapping choices disabled. Unsaved filter drafts survive mode changes; they persist only after Save filters. Loading hides previous-mode fields. Failed reads retain drafts; successful saves clear that mode's draft. Busy controls prevent cross-mode save races. Drafts live only on the current page and are lost on reload/navigation; no claim of durable draft storage. Any/All, reciprocal exclusions, five/ten limits and Plus enforcement remain server-owned.

DEFAULT layout choices: 960px maximum width, two columns from768px, native checkboxes and buttons, existing theme tokens. No global palette or design-source changes.

## Validation

The owned runner uses real local Keycloak OIDC with a new temporary confidential client, fictional adult account and a disposable PostgreSQL database. Beta and phone gates are enabled. It pre-enrols only that fixture, completes the real18+ action, verifies the phone-required refusal, then completes the clearly simulated phone code through its own Mailpit sink and submits an adult DOB through the UI. It never turns off gates or modifies the existing realm/client/installation.

Two JPEGs are generated from canvas and authored as approved **synthetic fixture state** in that disposable database, solely to test photo placement; this is not a live moderation or identity-verification result. No sensitive screenshots, real people, private messages or production exports are used.

Browser coverage: saved custom name/city and non-overlapping selected label; visibility off/reload; mode-local matching draft/save; delayed/503 read failure recovery; All/required/excluded persistence and per-mode independence; disabled Plus controls; shared main and independent photo assignments; four screens across375/768/1024/1440px in light/dark without document overflow. Representative safe screenshots are in [evidence](evidence/2026-11-02-member-settings/). Native devices, assistive-technology certification, timing/security guarantees and hosted acceptance remain outside this local evidence.

Independent read-only review found two matching edge cases (failed-read draft deletion and editable stale fields while loading). Both were corrected and covered by browser regressions; follow-up found no concrete blockers.

Final `./scripts/check.sh` passed:596 PostgreSQL persistence cases, all three smoke suites, zero build warnings/errors. The final real Edge run passed every workflow and32 viewport/theme combinations, including fault-injected delayed/failed filter reads. Owned resources were removed. Source diagnosis established the old mode handler overwrote unsaved fields; a separate baseline browser build was not run for this member slice.

## Reproduce without merging

In this branch/worktree, build the Release API and publish Web to a separate temporary directory:

```sh
dotnet build Swoovr.Local.sln -c Release
dotnet publish src/Swoovr.Web -c Release --no-restore -o /tmp/swoovr-intake-member-web
SWOOVR_MEMBER_TEST_ENV=/absolute/path/to/existing/managed/docker.env \
SWOOVR_MEMBER_TEST_PUBLISH=/tmp/swoovr-intake-member-web \
node tests/member/run-isolated.cjs
```

The existing stack must supply Keycloak8180 and PostgreSQL5433; the runner refuses occupied test ports15880/15881/11025/18025. It reads the existing credentials privately, creates an owned mail container and fixture media directory, then removes its own hosts/browser/database/client/user/mail/media resources in `finally`. It does not take over5080/5081/5082 or rotate credentials. No staff session is used. Never publish private logs or the configuration file.
