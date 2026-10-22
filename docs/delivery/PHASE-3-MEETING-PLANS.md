# Phase 3 — private meeting plan revisions

> Slice record from 16–18 October 2026. Read [current status](STATUS.md) for integration and remaining work; [D30](DECISIONS.md#naming-confirmation--22-october-2026) governs current naming.

Partial Domain implementation of product behaviour §6. No live plan API, booking, attendance tracking, payment or venue redemption is enabled. Namespace Swoovr; seven projects unchanged.

DEFAULT: private plan between an accepted connection's two participants. Terms name context, intention, activity, public venue/coarse area, instant/timezone and cost responsibility. Every proposal (including creator's initial proposal) needs explicit confirmation from both participants. Interest/request/chat acceptance is never meeting acceptance. Rescheduling/material edits create a new proposal version and clear proposal confirmations; the last mutually agreed terms remain authoritative until both accept the new version. Cancelling ends the plan and any pending proposal. Closing/blocking the connection prevents further collaboration; either authenticated participant may still cancel.

Completion requires a mutually agreed plan, its scheduled instant reached, and separate completion confirmations from both participants. DEFAULT completion acknowledgements are lifecycle acknowledgements, NOT attendance, satisfaction, chemistry, payment or voucher evidence. One person's confirmation never asserts the other attended. Both confirm the exact agreed version; accepting a replacement clears old completion acknowledgements. Completion can finish the agreed plan even if an unaccepted edit is pending; the pending proposal cannot activate afterward.

Immutable privately constructed MeetingPlan consumes a current ConnectionThread plus fresh ConnectionAccess on every operation. Server orchestration supplies validated actor/session/current safety and performs retained identity/revision compare-and-swap; no client-supplied safety or acceptance flags. Terms are immutable scalar fields, bounded text, valid enums, future DateTimeOffset instant normalized to UTC and a resolvable timezone ID. Client ambiguity/DST parsing is outside this slice: future clients must submit an explicit instant and display/confirm the selected zone/offset.

## Plan — native test-first execution

Branch codex/meeting-plan-revisions based ona6709c3/PR#12; dependent proposals explicit. User unattended authorization waives optional plan approval/questions; reversible choices labelled DEFAULT.

- [x] Add MeetingPlanChecks to portable executable. Observe missing-type failure before implementation. Cover two explicit confirmations, stale versions/revisions, outsider/swapped-pair/current block denial, unchanged prior agreement during edits, both reapproval, cancellation after closure, completion timing/version/two acknowledgements, no attendance inference, invalid terms/timezone, immutable originals.
- [x] Implement Domain/Connections/MeetingPlan.cs only: MeetingPlanTerms, MeetingPlanState and private immutable MeetingPlan with TryCreate, TryPropose, TryConfirm, TryComplete, TryCancel. No provider/transport/UI dependencies. Run portable cases.
- [x] Independent review and regressions for valid findings; full ./scripts/check.sh; D26/evidence/ledger/phase documentation; commit/PR main, no merge/deploy. Then continue private feedback or venue simulation rules.

Review focus: unilateral proposals never rewrite an agreement; old-version consent never carries forward; completion is distinct from attendance; block/closed connection cannot edit/confirm but cancellation remains; stale delivery requires future atomic persistence. No browser/device acceptance claimed.

## Evidence —17 October2026

Initial build failed missing MeetingPlan/MeetingPlanTerms/MeetingPlanState. Final70 meeting-plan cases pass, plus50 account/73 discovery/88 connection/39 appearance-state and typed-client cases. Full ./scripts/check.sh passed53 auth/four appearance Node, Release zero warnings/errors, design/workflow/HTTP checks. Independent read-only review found no actionable findings and reran portable checks. These are Domain hypothetical snapshots, not live invitations, device journeys, persistence/concurrency, attendance, payment or redemption evidence. No browser/container claim; nothing merged/deployed.
