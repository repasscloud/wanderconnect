# Q95–Q109 implementation handoff

2 November 2026. Work is isolated from the main checkout and existing design worktrees. No merge, deployment, Actions change or sensitive evidence publication is authorised.

## Execution order

DEFAULT: reproduce security/access and workflow defects first, then independently actionable usability changes. For each slice: inspect linked issue/specification, write a failing regression, implement the smallest fix, run relevant real-browser workflows and `./scripts/check.sh`, review, commit/push a branch and open a main-target PR. Update issue/Q/intake records without closing issues.

## Queue

| Q / issue | Status | Result / next step |
| --- | --- | --- |
| Q95 / #137 | Partial fix proposed in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Failed lookups now show the API refusal instead of a false no-match. Actual Edge: denied staff, exact/case/whitespace email, account without profile, genuine no-match, 375px reflow. Original reported identity's linkage remains unverified; no private data inspected. |
| Q96 / #138 | Fix validated; proposed in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Staff cookie validation renews expiring server-held tokens through configured Keycloak metadata; a renewal refusal clears the local session before authenticated rendering. Concurrent rotation is shared; cookie deadline/auth_time/method remain unchanged. Branded signed-out page has no staff identity. |
| Q97 / #139 | Pending independent work | Improve safe review controls without eligibility/policy changes. |
| Q98 / #140 | Fix proposed in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Database search/page/count beyond legacy1000 cap; structured SSR table, canonical linked names, status/date/sort controls and confirmed actions. Actual Edge1005-row acceptance passed;607 PostgreSQL checks passed, including11 new search cases. |
| Q99 / #141 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Guest sign-in/create entry and accurate landing copy; truthful status/draft disclosures retained. |
| Q100 / #142 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Configured guest account redirect, retry/unconfigured escape and BFF401 verified. |
| Q101 / #143 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Desktop frame/canonical form and phone layout verified; native/device acceptance outstanding. |
| Q102 / #144 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Responsive registration frame; email-first/adult/draft requirements preserved. |
| Q103 / #145 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Fictional duplicate-email field focus/description verified. No identity-sync diagnosis claimed. |
| Q104 / #146 | Proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Reset frame/email input and shared theme verified; provider tokens unchanged. |
| Q105 / #147 | Partial fix proposed in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Dedicated equal acknowledgement for enabled/absent/disabled fixtures. Timing/rate-limit acceptance remains unverified. |
| Q106 / #148 | Pending independent work | Account organisation; onboarding order deferred. |
| Q107 / #149 | Pending | Profile controls/persistence and responsive layout. |
| Q108 / #150 | Pending independent work | Preserve D175, assignments, screening and quotas. |
| Q109 / #151 | Pending | Responsive filters; preserve per-mode semantics/entitlements. |

## Deferred owner decisions

Adult-media policy is deferred. Preserve D178 and all current screening/access/evidence rules; no adult-media review is requested or needed for this run. Independent main photos and photo limits, onboarding order and legal-draft approval remain OPEN. Do not change them or infer approval.

## Database investigation and validation

The active stack is `swoovr-platform-*`, with PostgreSQL on 127.0.0.1:5433. Its private source is the original checkout's `.local/platform/docker.env`; `check.sh` auto-discovery still reads legacy `.local/docker.env`. Explicitly supplying `SWOOVR_TEST_DATABASE` from the managed source resolves intake's 28P01 mismatch. No rotation, installation reset, database authentication change or existing data deletion occurred. Persistence checks create/drop their own randomly named databases.

Baseline on 094dc97: full `./scripts/check.sh` passed, including 596 PostgreSQL cases, zero build warnings/errors and all three smoke suites. Q95 post-change full `./scripts/check.sh`: exit 0, including 596 PostgreSQL cases and three smoke suites; zero build warnings/errors. No hosted/native or original private-account acceptance claim.

Q95 regression first failed in actual Edge because a denied lookup rendered no-match; it then passed against the changed published Admin app and real API/Keycloak with disposable fictional staff/database resources. Harness cleanup succeeded.

Q96 cause reproduced with a temporary client whose access tokens expire after five seconds: the old cookie stayed usable while its API token expired. Real-provider regression failed before and passed after renewal; revoking only the owned fictional provider user caused renewal to fail and reauthentication before staff HTML. Ten fake-provider/ticket-store checks also passed, covering concurrency, original deadline/authentication evidence, malformed expiry and refusal. These checks do not claim hosted/multi-instance operation; the existing ticket store is process-local. Final full `./scripts/check.sh` passed (exit 0; 596 PostgreSQL cases, three smoke suites, zero build warnings/errors). Final real Edge acceptance passed after the review fix, including renewal, revocation, anonymous branded signed-out rendering and resource cleanup. Review found a late-renewal/logout race in the existing ticket store; a deterministic regression failed before the fix. Renewal now checks that the key remains active under the same lock as removal. Follow-up independent review found no remaining concrete blockers.

## Preview and resume

Worktree: `/Users/danijeljw/.codex/worktrees/issue-intake-fixes/wanderconnect-brief`.

For isolated Q95 acceptance, publish Admin to a temporary directory, then set `SWOOVR_ADMIN_TEST_PUBLISH` to it, `SWOOVR_ADMIN_TEST_ENV` to the absolute existing managed env path, and `SWOOVR_ADMIN_TEST_SCENARIO=accounts`; run `node tests/admin/run-isolated.cjs`. For Q96 use the same command with `SWOOVR_ADMIN_TEST_SCENARIO=sessions`; it gives only its owned temporary client a five-second access-token lifetime and revokes only its owned fictional provider user.

The harness refuses occupied 15880/15882/15883 ports, creates its own DB/client/staff fixture and cleans them up. It does not take over localhost services. Existing browser harness requires installed Edge and Playwright.

Owned running resources: none after Q95 browser cleanup. Temporary build/log files are under `/tmp/swoovr-intake-*`; they contain no intentionally logged credentials and are not publication artifacts. Retained six untracked screenshots in the main checkout remain untouched.

## Q99–Q105 evidence and preview

[PR #153 implementation/evidence](https://github.com/repasscloud/wanderconnect/blob/codex/fix/intake-auth-journeys/docs/delivery/ISSUE-AUTH-FIXES.md) owns detailed results and safe synthetic screenshots. Its full check passed (596 PostgreSQL cases, three smoke suites, zero warnings/errors), plus actual Edge guest routing/BFF privacy and isolated provider UI in light/dark at375/768/1440px. One reset request sample per enabled/absent/disabled category differed; no timing-resistance guarantee is made. All owned provider/mail/network/browser/import resources were removed. Its worktree is `/Users/danijeljw/.codex/worktrees/intake-auth-fixes/wanderconnect-brief`, branch `codex/fix/intake-auth-journeys`.

## Q98 enrolment workflow

[Search contract](../architecture/BETA-ENROLMENT-SEARCH.md): the existing legacy list is unchanged; the new Admin-only/no-store endpoint filters and pages in PostgreSQL, counts before paging and uses canonical account/profile linkage for names. Search supports email/name/reference, state, stable sorting, inclusive UTC created dates and25/50/100 rows. New SSR table preserves revision/confirmation/audit and separates enrolment, email provider acceptance and staff-reported Apple state.

Actual Edge regression failed before the table change and passed with1005 synthetic records: nongranted role refusal, anonymous API401, next/previous pages, selectable size, sorting, search beyond the old1000 cap, status/date filters, confirmed iOS update and confirmed revocation. At375px horizontal table scrolling remains local. Eleven new real-PostgreSQL checks cover ties, references, name linkage, literal percent/underscore searches, date bounds, empty/overflow pages and invalid input. Final full `./scripts/check.sh` passed:607 PostgreSQL cases, three smoke suites, ten staff-session checks and zero build warnings/errors. Independent review found no concrete blockers. All owned resources were removed.

Safe synthetic captures: [desktop table](evidence/2026-11-02-admin-workflows/beta-1440.png), [narrow filtered table](evidence/2026-11-02-admin-workflows/beta-375.png). For the existing isolated Admin runner use `SWOOVR_ADMIN_TEST_SCENARIO=beta`. The harness creates1005 fictional enrolments in its own database and removes the database, provider client/user, hosts and browser on completion. This does not establish performance at arbitrary production scale.
