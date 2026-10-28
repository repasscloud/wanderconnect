# Serialize media verdicts and publication

Issue128, source-confirmed race under D203/D185; no observed exposure. Reproduce first with synthetic local files and Development-only publication barriers. No new project/provider.

- [x] Red deterministic concurrent reviewer reproduction: pause approval after raw read, commit escalation, resume approval; old state must demonstrate unsafe Passed overwrite. Local synthetic bytes only, cleanup after hold release.
- [x] Shared per-account advisory lease for screening, publication, block, rejection, removal/object purge and final account deletion boundary. Revalidate authoritative state under the lease; Blocked is terminal for publication/rejection. Approval reports conflict rather than claiming success when stale. Do not read/scan sealed data.
- [x] Keep member removal responsive: record hidden/removal intent immediately; if processing owns the lease, defer physical deletion to worker. Never erase a source while in-flight classification owns it. Final account purge waits on the same lease.
- [x] Test both concurrent reviewer orderings and competing screening workers using a delayed fake service and configured simulation, no real known-content material. Fail startup for barriers outside Development. Check no orphan clear photo copy after blocked completion.
- [x] Full checks, photo/chat/hold/deletion/privacy browser regressions and cleanup; source review, actual evidence/gaps, docs, main-host restoration, one commit and explicit push; draft PR Closes128 plus issue comment. Then continue retention schedule/purge/registry.


The deterministic old-code reproduction ended Passed after blocked preservation; fixed reviewer orderings, responsive removal and competing automatic workers pass 26 checks. Source review also corrected stale escalation and false approval responses; an audit added lease protection to placements and message-photo expiry. Clear-copy crash recovery now retries failed deletes and checks both deterministic locations. A recovery regression exposed the local store's absent-parent deletion exception; local DELETE is now idempotent, with a focused portable check. Final integrated regressions and completion evidence follow in VALIDATION.md.
