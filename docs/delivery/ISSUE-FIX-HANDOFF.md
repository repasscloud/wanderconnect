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
| Q98 / #140 | Pending | Check existing admin redesign before table work. |
| Q99 / #141 | Pending | Public landing/navigation; retain draft disclosures. |
| Q100 / #142 | Pending | Verify guest account routing and BFF privacy. |
| Q101 / #143 | Pending | Provider sign-in layout; safe synthetic browser evidence. |
| Q102 / #144 | Pending | Registration layout; preserve adult gate and legal draft wording. |
| Q103 / #145 | Pending | Validation prominence/focus; preserve neutral recovery. |
| Q104 / #146 | Pending | Reset layout; preserve provider semantics. |
| Q105 / #147 | Pending | Neutral dedicated reset acknowledgement. |
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
