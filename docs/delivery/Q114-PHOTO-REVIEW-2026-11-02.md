# Q114 — Routine photo-review workspace

CONFIRMED D297 ([owner answer](https://github.com/repasscloud/wanderconnect/issues/159#issuecomment-6572633682), 2 November 2026) authorises Support and Admin routine photo review, a server-stored per-staff/per-notice safety acknowledgement valid four hours, coded approvals without free text, a focused tablet review dialog and no phone review tools. Legal and protected specialist evidence permissions remain unchanged.

## Implementation

The queue is a compact server-paged table (25 rows) with kind/reason filters, separate pending-screening figures and focused review. A single reveal/conceal control deliberately records each reveal; conceal/close/advance removes the image. Native dialog enhancement provides focus trapping, Escape close, return focus and a bounded image. Tablet presentation fills the screen; the existing AdminLayout phone restriction also covers history/appeal subroutes. Dynamic forms retain antiforgery and enhanced SSR, including the existing POST-refresh handling.

`MediaSafetyAcknowledgement` is keyed by staff account and notice version. Current notice version is `2026-11-02`; its text/version live in `PhotoReviewPolicy`. Bump the version whenever the required safety notice changes. Queue metadata can be read before acknowledgement, but reveal/decision endpoints and the human pipeline recheck it at the current clock after waiting for media authority. Review link expiry is capped by the acknowledgement's remaining lifetime and normal ten-minute photo-link lifetime. Wrong versions, future dates and the exact four-hour boundary fail closed. Q111 private inspection uses the same link helper; public approved projections retain their normal audience.

Workload, queue and history queries apply actual reviewer authorisation and self/current/historical staff-affiliation exclusions. Appeal workload and appeal pages share one independent-reviewer query. Support sees routine review counts and cannot read protected specialist images or decide appeals. Coded approval notes are refused even for legacy clients. Routine decision history remains append-only through these application routes; prior private rejection notes never become member explanations.

Migration `20261102140000_PhotoReviewWorkflows` also contains the Q115 decision/appeal tables and nullable rejection retention clock. Scaffold used the existing Infrastructure design-time factory after compiling API; it did not connect/update a database. No project/package was added.

## Validation at handoff

- Tests were added first; `/tmp/swoovr-photo-review-red.log` captured missing PhotoReviewPolicy compilation errors before implementation.
- API compilation succeeded before the final UI/concurrency refinements; root owns the final coordinated gate.
- `python3 scripts/check_design.py`: passed (96 contrast pairs; canonical tokens, fonts, links and disabled workflows).
- JavaScript syntax checks passed for the dialog enhancement and updated media fixtures.
- `tests/admin/media-review.browser.cjs` now covers explicit acknowledgement, exactly one audit per reveal, conceal/refresh without replay, structured approval/rejection/escalation and validation preservation.
- New owned `tests/media/review-workflows.browser.cjs` covers 51 synthetic queue items, stable paging, roles, version/expiry refusal and actual keyboard dialog. Its fresh specialist result uses explicitly labelled blocked simulation; run with `Media__Screening__Simulate=blocked` through the coordinated owned runner.

Full browser execution, final C#/native builds, human screen-reader/physical-device acceptance and measured reviewer throughput remain pending at this implementation handoff. No throughput claim is made. No service start, shared database reset, sensitive screenshot inspection, provider change, commit, merge, deployment or Actions enablement was performed by this agent.


### 3 November coordinated follow-up

Actual owned Admin media acceptance passed against the published artifact after synchronising the test with the completed server acknowledgement and selecting the visible `Photo review` heading during refresh. Reveal/conceal, access audit counts, Other-note validation, approval/escalation and no document-POST replay assertions remain. The test previously navigated away while the acknowledgement POST was still pending; the server correctly withheld reveal controls. A second generic-heading wait selected the hidden phone-floor heading. Run log: `.local/overnight-delivery/admin-media-ack-wait-2.log`; process exit0 and owned hosts/browser/database/client/staff cleanup were confirmed.

D134 follow-up: Support-capable routine queries now pass an explicit staff-affiliation capability to the shared eligibility query. Staff-badged member photos are excluded from Support queue/history/counts and direct reveal/decision, while an unrelated Admin retains access. Decision eligibility is rechecked after the account lifecycle wait. Existing self/current/historical staff-link exclusions remain for every reviewer. Added actual PostgreSQL eligibility and owned API queue/workload/direct-access tests; their coordinated build/browser results remain separate from the published-artifact UI test above.

Observed owned browser pass on current Release artifacts: paging/roles/acknowledgement, retained rejection, durable generic notices, one independent final appeal, specialist exclusion, two immutable rejection receipts and actual keyboard dialog. Evidence run `8c1c6ac10c4c43c3b1c7f33d05696dc3`, `suite-review-workflows.log`. Specialist re-screen was explicitly blocked simulation, not real provider acceptance; overall runner cleanup/result is recorded in final delivery.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
