# Phase 3 — portable connection lifecycle

Partial Domain slice implementing behaviour specification §2/3/5. No real requests, private messages, API mutations, persistence, realtime or UI are enabled.

CONFIRMED constraints remain: blocking in either direction prevents contact; no traveller initiation before confirmed arrival; no member-visible visit trace. Q05 pre-arrival reply exception remains OPEN. This slice conservatively applies full current new-contact eligibility to both initial request and recipient acceptance, including arrival where applicable; it does not silently approve that exception.

DEFAULT: one retained unordered participant pair across contexts, no repeated request after decline/close; pending text-only request (no media/link attachment API), recipient-only accept/decline, accepted two-party text chat, either participant may close. No public counts. Ordinary intention/area changes do not end accepted chat; current block, suspension, adulthood/verification loss or explicit conversation privacy revocation deny every read/send. Live profile/media still use separate discovery rules. Historical reporting eligibility survives decline/close/block/privacy revocation but never grants live chat/profile access.

Internal server-owned facts only: actor ID and AccessSession from the same validated context, current pair safety and privacy facts; DiscoveryAccessRequest is refreshed for initial request/acceptance. API must never bind these facts or an eligibility boolean from client JSON. No evaluator authenticates its caller. Keep application privileges disabled.

Use an immutable privately constructed ConnectionThread. Every successful mutation returns a new state with revision+1, checks expected revision, and preserves original state; generic null denial. Only text supported; strings validated nonempty/length and never logged. Revision ordering and duplicate/declined pair protection require a future transactional store to load retained pair state, enforce unique unordered pair identity and compare-and-swap the revision with fresh safety reads. Domain immutability alone is not distributed concurrency or realtime revocation proof. A future delivery layer must recheck recipients at delivery and reconnect, remove subscriptions and suppress push after revocation.

## Implementation plan

Native execution in the existing managed worktree, branch codex/connection-lifecycle-rules based on236ddc6/PR#11. User unattended instructions authorize reversible defaults and waive optional question/approval pauses. Seven-project boundary and .NET10 preserved.

- [x] Add tests/DatingApp.Checks/ConnectionChecks.cs plus Program.cs invocation. Tests cover eligible request/recipient acceptance; sender/outsider denial; no pending second message; text validation; decline/close terminal; stale revision; swapped fact IDs; cross-context repeat; pending travel denial; accepted continuity and current safety revocation across read/send; historical reporting only. Run and observe missing-type build failure.
- [x] Add src/DatingApp.Domain/Connections/ConnectionThread.cs with ConnectionState enum, immutable message and trusted access records; private constructor/static Empty; TryRequest/TryAccept/TryDecline/TrySend/TryClose, CanRead/CanReport. Run portable checks; add any missing edge-case regressions before fixes.
- [x] Independent read-only review; fix valid findings with failing regressions. Run ./scripts/check.sh; synchronize D25/validation/phases/ledger. Commit/push dependent PR main; no merge/deploy. Then continue next unblocked slice.

Review focus: stale safety facts must not be cached; identity-swapped snapshots deny; declined pair must not be recreated by integration; acceptance must not approve Q05; closed history/reporting must not reopen live messages. Tests use fictional IDs/text only. No browser/container claim is appropriate for this pure Domain slice.


## Local evidence

Initial build failed on missing ConnectionThread/ConnectionState/ConnectionAccess types before implementation. Portable executable then passed81 lifecycle cases; expanded fresh-arrival, pending-block, backwards-time and swapped-order cases pass88. Existing50 account/73 discovery/39 appearance-state and typed client checks pass. No browser/container/device validation is claimed for this Domain-only slice.
