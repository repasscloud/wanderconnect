# Phase 3 — independent private experience feedback

> Slice record from 16–18 October 2026. Read [current status](STATUS.md) for integration and remaining work; [D30](DECISIONS.md#naming-confirmation--22-october-2026) governs current naming.

Partial Domain implementation of behaviour §7. No real user answers, feedback API/storage, reporting service, moderation scoring, ranking or enforcement is enabled.

DEFAULT: one feedback record per interaction and author, independently submitted and amendable with immutable version history. Internal historical server facts bind an interaction ID, two participants, validated actor/session and evidence of genuine connection or an agreed meeting. Current unmatching/blocking is not an input to eligibility; retained legitimate interaction facts permit feedback/reporting access without reopening chat/profile. Suspended/unauthenticated sessions do not receive app access. Reporting remains a separate action/service, outside these private answers.

Answers cover the author's own chat-only/met-in-person experience, profile representation, intention alignment, boundaries, reconnect willingness/capacity, and optional private chemistry. The author's claim that they met never asserts the other participant attended. No public score, no negative-vote sanction, no aggregation or counterpart answers. Free text is excluded from this first slice to avoid implying a moderation/report transport exists.

PrivateFeedback keeps answer history private. ReadOwnHistory verifies exact interaction/participant/author and current authenticated member context. Counterpart, staff role and anonymous access return null. Each participant's own record is independent; no waiting for the other's submission, no counts revealing submission. TryAmend requires revision, matching identity and nondecreasing server time. Answers are validated enums/scalars; reconnect capacity is required only when reconnect=Yes. Optional chemistry may be unanswered; chat-only remains labelled.

Future persistence must enforce unique(interaction,author), retain authorized historical interaction evidence under reviewed retention policy, and atomically compare revisions. These Domain checks do not authenticate facts, prove a genuine meeting, implement ID verification or grant staff access. No public serialization of internal access facts.

## Native implementation plan

Branch codex/private-feedback-rules based on4c9760f/PR#13, dependent proposal chain. User unattended authorization waives optional approval/questions; no owner material decision is assumed.

- [x] Add PrivateFeedbackChecks and Program invocation. Observe missing-type build failure. Cover independent author submissions, exact pair/interaction binding, unmatch-compatible historical facts, no eligibility on mere discovery, owner-only history, counterpart/staff/anonymous denial, amendment audit/revision/time, reconnect capacity/enums and optional chemistry, one person's met claim not shared.
- [x] Implement Domain/Connections/PrivateFeedback.cs: historical FeedbackAccess, answer/context enums, immutable FeedbackAnswers/FeedbackRevision; private constructed PrivateFeedback.TrySubmit/TryAmend/ReadOwnHistory. No transport/provider/UI references.
- [x] Independent review/regressions, full ./scripts/check.sh, D27/evidence/phase/ledger, commit/dependent PR main; no merge/deploy. Reassess remaining independent journeys afterward.

Review focus: no counterpart answer path; fake history cannot be bound by a future API; closed chat does not erase feedback eligibility; mutable audit arrays cannot escape; negative answers never authorize enforcement. Tests use fictional scalar answers only. No browser/device/live-feedback claim.

## Evidence

Initial missing-type build failure observed;70 feedback cases pass with existingportablechecks. Independent read-only review found no actionable findings. No browser/container/device claim for this Domain-only change; no merge/deployment. Full check results recorded in ledger.
