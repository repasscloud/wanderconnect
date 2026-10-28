# API contract dates implementation plan

Execute natively in the authorised worktree, under the owner's explicit autonomous execution instruction.

**Goal:** D211 stable Swoovr paths with an explicit, consistent date contract across API and clients.
**Architecture:** Contract constants/policy in Contracts; API middleware selects before auth/side effects; typed clients pin a date, BFF relays explicit dates and response contract. SignalR protocol paths remain unchanged.
**Tech stack:** Existing .NET 10, Blazor BFF/WASM, MAUI, Python HTTP and Node browser suites; no new projects/packages.
**Spec:** D211 and the full Q83 owner comment in docs/delivery/OWNER-DECISIONS-2026-10-29.md.

## Constraints and review focus

- No Swoovr numerical URL versions; preserve third-party Mailpit/Didit/provider version paths.
- Missing header pins the initial 2026-10-28 contract forever (DEFAULT D222); invalid/duplicate headers and unsupported dates return distinct 400 codes before mutation. Never echo input.
- Add future supported dates explicitly; never silently repoint old dates to a breaking contract.
- Preserve auth/BFF allow-list and CSRF controls; member BFF cannot relay admin endpoints.
- Signed media/webhooks can use pinned missing-header policy; health/SignalR use separate protocol paths.

## Tasks

- [x] Extend scripts/smoke.py with stable status route, missing/explicit selection, malformed/calendar/duplicate/unsupported dates, legacy 404 and health exclusion. Run against baseline: expected 404 failure observed.
- [x] Add Contracts ApiContract with Header/Initial/Current/Supported and strict Select policy. Test exact dates, invalid leap days, duplicates, stable defaults and no input reflection in Swoovr.Checks.
- [x] Add API middleware for /api segment, response header and Vary; reject errors before authentication and persistence. Keep old paths unregistered.
- [x] Migrate all Swoovr paths, typed clients, relays, allow-lists, tests/scripts and current docs. Preserve third-party paths and dated source/evidence records.
- [x] Pin all typed clients, SSR requests and native/WASM calls; BFF preserves explicit request and selected response header.
- [x] Run full check.sh; build Debug once, run affected browser suites sequentially without rebuilding; record results/gaps. Restore main hosts after testing.
- [x] Update STATUS/DECISIONS/VALIDATION and API policy/runbook; commit one slice, explicit push/verify, update draft PR and issue, consume new owner labels, continue queue.
