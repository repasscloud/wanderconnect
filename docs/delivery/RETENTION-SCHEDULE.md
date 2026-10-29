# Retention schedule

Status: implementation in progress under owner decision D216 / issue 118. This schedule describes CONFIRMED owner instructions and explicitly identified DEFAULT implementation choices. Legal review issue 57 remains open. Swoovr intends to sign the Online Dating Industry Code; neither signatory status nor verified compliance is claimed.

| Data | Trigger and period | Disposal and exceptions |
| --- | --- | --- |
| Ordinary account recovery | CONFIRMED: 30 days after request; immediate deletion may be requested | Hidden immediately. Cancellation ends when final deletion begins. Provider uncertainty leaves a retryable finalizing state. |
| Ordinary deleted message text | CONFIRMED: 30 days after deletion | Erase text; retain a neutral deleted-message reference. Required case snapshots and holds remain independent. |
| Qualifying review evidence | CONFIRMED: at least 12 months after completion or referral | DEFAULT: all moderation cases qualify; calendar-month clock uses latest closure, referral, appeal decision or evidence export. Pending appeals/open reviews have no disposal clock. |
| Serious incident evidence | DEFAULT pending legal review: 24 calendar months after latest completed review/referral | Active and overlapping holds override. Classification includes historical serious reports and known-content cases. |
| Minimal review/compliance records | CONFIRMED baseline: at least 2 years | DEFAULT: 24 calendar months after latest completed review/referral; detailed evidence may expire earlier. Never reopen a case whose evidence has been disposed. |
| Account tombstone | Existing DEFAULT: 730 days after final deletion | Required review foreign keys extend retention. If another member retains their own messages, remove the deleted member's sign-in mapping and retain a minimal referential stub. |
| Terminated-member identity/contact history | Existing policy: 2 years | Live holds override; independent encrypted copies follow their own applicable schedule. Not a blanket requirement to retain every identity document or profile. |
| Police-case records | Existing procedure: 7 years after closure | Active holds and retained linked evidence postpone disposal; missing closure/provenance fails closed. |
| Sealed hold copies and referenced photos | Hold release resumes applicable review/police schedule | Release does not itself erase copies. Unknown provenance requires restricted review. Unlinked staff holds require independent legal attestation before disposal; absence of a case link is insufficient. |
| Minimal staff conflict association | DEFAULT D235: at least 24 calendar months after the latest explicit association/declaration, extended while related data remains | No email-based inference. Preserve original assertion provenance across deletion/relinking; unknown held membership stops expiry. Remove expired source assertions atomically to prevent reconstruction. |
| Banned identifiers | Owner-approved typed, versioned HMAC registry | Registry/status/reinstatement implementation is a separate remaining slice of issue 118. Do not infer that the older expiring fingerprints implement the approved registry. |
| Backups | CONFIRMED: daily 30 days; weekly 4 weeks; monthly 12 months | Encrypted, isolated, automatic expiry. UAT infrastructure/restore reconciliation is still to be implemented and tested. Backups are not the primary evidence store. |

The [Code, section 6.2(2)](https://www.australianonlinedatingcode.com.au/wp-content/uploads/2025/07/Australian-Voluntary-Code-for-Online-Dating-Services-Code-of-Practice.pdf) addresses information relating to complaint/report reviews. It does not justify indefinite retention of every member's ordinary content. D185's minimisation and restricted-access requirements continue to apply.

## Disposal authority

Account state changes use a short per-account PostgreSQL advisory lease. Screening/publication uses a separate media lease. Coverage changes take the global preservation barrier exclusively; source destruction, capture and ordinary committed writes take it shared. Case and hold mutexes serialize retention decisions and evidence numbering/custody commits across processes. Order is account state, media when needed, preservation barrier, case/hold mutex. Shared authority is never upgraded to exclusive. Conversation row locks follow the preservation barrier.

Disposal rechecks live holds under authority, commits before releasing it, and retries interrupted due work. No trustworthy hold status means no disposal. Case snapshots retain explicit media references; unreadable legacy references fail closed. Released retained copies remain accessible only through existing legal-role, conflict, purpose, elevation, approval and expiring one-use export controls. Known-content media is never rendered.

## Operations and remaining evidence

`retention purge` reconciles explicit historical staff associations, coordinates case, account, message and media expiry, then expires eligible minimal conflict records. `staff conflicts reconcile` independently materialises authoritative current/audited links and runs the same guarded expiry. `retention purge --case <id>` restricts case/copy disposal to one case; `retention purge --account <id>` restricts review/copy expiry to the named member’s cases and known copies. Case scans use keyset paging so held or unresolved batches cannot starve later eligible records. `accounts purge <id>` restricts account finalization and media sweeping to one account. The hosted worker runs hourly; Development requires explicit opt-in. Counts and machine reasons are logged; content is not.

The migration is additive. Tests must establish closure/referral boundaries, overlapping holds, release, actual photo bytes, partial failures, finalization/cancellation, provider uncertainty and cross-process ordering. Operational hold/custody/export/disclosure histories and duplicated free-text notes require a separately defined expiry implementation before issue 118 can close. Backup restore must reconcile completed deletions, expired records and legal holds before any member traffic resumes; no restore drill is claimed yet.

D236 foundation defines a DEFAULT24-calendar-month inactive identifier-history minimum from the latest revocation/expiry/review and a proposed6-month active necessity-review interval. Active restrictions have no automatic history disposal. The clocks are domain rules only at this stage: authorization, hold-aware jobs and a bounded disposal authority for immutable history must be implemented before this is an operational retention control. Legal57 still governs the proposed longer retention.
