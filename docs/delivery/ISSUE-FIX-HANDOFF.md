# Q95–Q109 implementation handoff

Current status refresh, 2 November 2026: PRs [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged into `main`, verified against GitHub metadata and local history at `53ebacd`. Issues remain open; remaining acceptance and deferred scope still apply. [New intake Q110–Q120 and dependencies](ISSUE-INTAKE-2026-11-02.md). The execution/resource/validation narrative below is historical evidence from the individual implementation runs, not a current combined validation claim. This intake run performs no merge/deployment/Actions change or sensitive-evidence publication.

## Execution order

DEFAULT: reproduce security/access and workflow defects first, then independently actionable usability changes. For each slice: inspect linked issue/specification, write a failing regression, implement the smallest fix, run relevant real-browser workflows and `./scripts/check.sh`, review, commit/push a branch and open a main-target PR. Update issue/Q/intake records without closing issues.

## Queue

| Q / issue | Status | Result / next step |
| --- | --- | --- |
| Q95 / #137 | Partial implementation merged in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Failed lookups now show the API refusal instead of a false no-match. Actual Edge: denied staff, exact/case/whitespace email, account without profile, genuine no-match, 375px reflow. Original reported identity's linkage remains unverified; no private data inspected. |
| Q96 / #138 | Merged in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Staff cookie validation renews expiring server-held tokens through configured Keycloak metadata; a renewal refusal clears the local session before authenticated rendering. Concurrent rotation is shared; cookie deadline/auth_time/method remain unchanged. Branded signed-out page has no staff identity. |
| Q97 / #139 | Partial implementation merged in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Automatic factual queue purpose, audited deliberate reveal, stable decision-bound codes, Other-note validation and retained coded counts. Adult-media eligibility remains deferred; case-specific controls and governed private approval-note history remain separate follow-ups. |
| Q98 / #140 | Merged in [#152](https://github.com/repasscloud/wanderconnect/pull/152) | Database search/page/count beyond legacy1000 cap; structured SSR table, canonical linked names, status/date/sort controls and confirmed actions. Actual Edge1005-row acceptance passed;607 PostgreSQL checks passed, including11 new search cases. |
| Q99 / #141 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Guest sign-in/create entry and accurate landing copy; truthful status/draft disclosures retained. |
| Q100 / #142 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Configured guest account redirect, retry/unconfigured escape and BFF401 verified. |
| Q101 / #143 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Desktop frame/canonical form and phone layout verified; native/device acceptance outstanding. |
| Q102 / #144 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Responsive registration frame; email-first/adult/draft requirements preserved. |
| Q103 / #145 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Fictional duplicate-email field focus/description verified. No identity-sync diagnosis claimed. |
| Q104 / #146 | Merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Reset frame/email input and shared theme verified; provider tokens unchanged. |
| Q105 / #147 | Partial implementation merged in [#153](https://github.com/repasscloud/wanderconnect/pull/153) | Dedicated equal acknowledgement for enabled/absent/disabled fixtures. Timing/rate-limit acceptance remains unverified. |
| Q106 / #148 | Partial implementation merged in [#154](https://github.com/repasscloud/wanderconnect/pull/154) | Grouped settings/truthful account heading. Onboarding order and revision-based safety dismissal deferred. |
| Q107 / #149 | Partial implementation merged in [#154](https://github.com/repasscloud/wanderconnect/pull/154) | Wider profile/mode workspace, city/long-label overlap and compact controls. Custom name and visibility-off persistence verified. Automatic initial-name mapping remains a product/data-mapping follow-up. |
| Q108 / #150 | Partial implementation merged in [#154](https://github.com/repasscloud/wanderconnect/pull/154) | One mode-assignment gallery at a time; shared library/main and independent assignments verified. Independent mains/proposed limits deferred. |
| Q109 / #151 | Merged in [#154](https://github.com/repasscloud/wanderconnect/pull/154) | Per-mode unsaved drafts survive switches and failed reads; no stale editable fields during load. Responsive workspace, counts, disjoint choices and Plus controls verified. Page-local drafts are not durable. |

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

## Q97 moderation workflow

[Reason contract and boundaries](../architecture/MEDIA-REVIEW-REASONS.md). Routine queue access records its factual purpose automatically; the preview starts blurred, a keyboard-operable reveal POST records another view, and concealment is explicit. Codes are decision-bound, serious concerns go through existing escalation, and Other rejection requires notes server-side. Latest code/terminal notes and machine-only audit history commit inside the existing lifecycle/source-save boundary. Existing legacy clients are labelled honestly. Retained coded counts cover30 days; older uncoded decisions are excluded.

The nullable-column upgrade was applied only to disposable test databases. No active installation/database was migrated. Approval notes are deliberately unavailable until a governed retained private-note history exists; coded approval notes refuse rather than silently losing them. Terminal reject/escalate notes use the existing final-review field. Case-specific forms retain their explicit purpose and legacy controls. These are precise remaining scope/dependencies, not an adult-policy approval request. D178 and every existing screening/conflict/evidence gate are unchanged.

Actual Edge regression first failed on mandatory manual queue purpose. Final synthetic-only acceptance passed three generated canvas JPEG fixtures: automatic purpose, blurred preview, keyboard reveal and two recorded views, coded approval, Other-without-notes refusal/no mutation, coded rejection, protected escalation, three audited codes/counts,375px live-control reflow and cleanup. Review found a cross-item form-state leak; a regression failed with two panels opened for one failed submission. Values now restore only for the matching failed item and clear on success, with a passing no-note-copy regression and follow-up independent review. Nine Domain reason checks cover unknown/mismatched/serious codes, bounds, Other, legacy classification and refusing unsupported approval notes. Final full `./scripts/check.sh` passed:607 PostgreSQL cases, three smoke suites, ten staff-session checks and zero build warnings/errors.

Safe synthetic captures: [desktop review](evidence/2026-11-02-admin-workflows/media-synthetic-1440.png), [narrow review](evidence/2026-11-02-admin-workflows/media-synthetic-375.png). For isolated preview use `SWOOVR_ADMIN_TEST_SCENARIO=media` with the existing Admin runner. It creates its own media directory and database, generates JPEGs from canvas, and removes those resources plus its provider client/user, hosts and browser. No original sensitive images, real people or adult material were used. No owned test resources remain after cleanup.

## Q106–Q109 member workflow

[PR #154](https://github.com/repasscloud/wanderconnect/pull/154), branch `codex/fix/member-settings-workflows`, worktree `/Users/danijeljw/.codex/worktrees/intake-member-settings/wanderconnect-brief`. [Detailed member evidence and preview instructions](https://github.com/repasscloud/wanderconnect/blob/codex/fix/member-settings-workflows/docs/delivery/ISSUE-MEMBER-FIXES.md). Member and Auth PRs use separate stylesheet files and different Account-page regions; no merge is performed or integration result claimed.

The member runner creates its own API/Web15880/15881, disposable PostgreSQL database, temporary provider client/user, owned Mailpit11025/18025 and media directory. Beta and phone stay enabled. The fictional adult completes the real provider18+ action, encounters the phone-required API refusal, confirms a clearly simulated local phone code, and submits an adult DOB through the UI. No gate is disabled and no existing provider client/realm configuration is changed. Canvas-only photo fixtures are authored approved states for placement testing, not live moderation results.

Final Edge acceptance passed: saved custom name/city, no selected-city overlap, visibility off/reload, per-mode draft/save and delayed/503 failure recovery, All/required/excluded persistence and independent modes, disabled Plus, shared main and independent photo assignments. All four screens passed375/768/1024/1440px in light/dark without document overflow. Representative synthetic screenshots were visually inspected. Independent review's two matching-state findings were corrected and covered; follow-up found no concrete blockers. Full `./scripts/check.sh` passed:596 PostgreSQL cases, three smoke suites, zero warnings/errors. No baseline member browser comparison is claimed. All owned test resources were removed.

## Final acceptance and resource state

Historical implementation handoff: all fifteen issues had independently validated changes in three PRs, which are now merged; partial scopes/dependencies remain listed above. Issues remain open. No merge, deployment, GitHub Actions enablement, installed database migration, credential rotation or private configuration change occurred. Sensitive retained evidence was neither accessed nor staged. No owned test hosts, browser profiles, mail containers, provider fixtures, temporary databases or media directories remain running after the completed harnesses.

Still outstanding: combined acceptance beyond individual historical evidence; hosted/provider acceptance; native devices and assistive technology; the original private-account linkage; reset timing/rate limits; governed retained approval-note history and legacy case-specific moderation controls; initial public-name mapping; and the explicitly deferred owner decisions. These are not claims of production readiness.

## New intake dependencies — 2 November 2026

Q95: owner confirmed working lookup; new lookup/browse scope is Q110/#155. Q98: item 20 visual refinements remain on #140; registration policy is Q112/#157. Q97: compact workspace Q114/#159 and photo appeals/retention Q115/#160 preserve existing codes and blocked-media gates; approval-note retention/case controls remain unresolved. Q107: initial registered-name mapping Q117/#162 and mode-name decision Q118/#163. Q106: universal DOB proposal Q116/#161 does not change onboarding order. Q108: shared main/quotas remain unchanged. Safari refresh Q113/#158, account inspection Q111/#156 and Admin consistency/device assessment Q119–Q120/#164–#165 are separate follow-ups. See the run record for all bidirectional links and precise source-supported boundaries.
