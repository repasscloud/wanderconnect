# Retention schedule

Status: **Approved, 2 November 2026 (legal approval D288; D310).** Owner decision D216 (issue [118](https://github.com/repasscloud/wanderconnect/issues/118)) approved the framework in principle. This schedule records CONFIRMED owner instructions and explicitly labelled DEFAULT implementation choices; DEFAULT periods are proposals until Australian legal review ([57](https://github.com/repasscloud/wanderconnect/issues/57)) approves them. Swoovr intends to sign the Online Dating Industry Code (D215); neither signatory status nor verified compliance is claimed. The member-facing summary is the [privacy notice](../../src/Swoovr.Client/PrivacyNotice.cs) (Draft 0.17); keep both consistent.

## Principles

1. **Periods are ceilings for a stated purpose, not targets.** Information is deleted or de-identified when no permitted purpose remains (APP 11.2). No category is kept indefinitely by default.
2. **Holds override expiry.** An active legal hold, an open review or an overlapping obligation postpones disposal. Releasing a hold resumes the applicable schedule; it never erases anything by itself.
3. **Fail closed.** Unknown provenance, unknown hold status or missing closure dates stop disposal and send the record to restricted review. Nothing is disposed of without trustworthy hold status.
4. **Evidence is separate from source.** Ordinary account and message deletion proceeds; independently preserved, encrypted evidence copies follow their own review or hold clock.
5. **Calendar clocks use UTC.** Month periods are calendar months from the latest qualifying event (closure, referral, appeal decision, export, transfer receipt, review or revocation). Backdating an earlier event never shortens a clock.
6. **Replicas follow the primary.** Member data is stored and processed in Sydney, Australia. Storage-only replicas in other hosting regions (JAPAC, EMEA, NAMER, LATAM) are never a separate retention source: deleting or disposing of the Sydney copy deletes the replicas, and holds are applied and released in Sydney.
7. **The Code does not justify blanket retention.** Code §6.2(2) concerns information relating to complaint and report reviews. It does not justify keeping every member's ordinary content; D185's minimisation and restricted-access rules continue to apply.

## Schedule

| Data | Period and trigger | Status | Disposal and exceptions |
| --- | --- | --- | --- |
| Active account and profile | While the account is open | CONFIRMED | Member can edit or remove optional data at any time. |
| Account recovery after a deletion request | 30 days after the request; immediate deletion on request | CONFIRMED D170, D216 | Hidden from discovery at once. Cancellation ends when final deletion begins. Provider uncertainty leaves a retryable finalising state. |
| Account tombstone | 730 days after final deletion | DEFAULT D170 | Required review foreign keys extend retention. If another member keeps their own messages, the deleted member's sign-in mapping is removed and a minimal referential stub remains. |
| Deleted message text (delete for both) | 30 days after deletion | CONFIRMED D167, D216 | Text erased; a neutral deleted-message reference remains. Case snapshots and holds are independent. |
| Verified identity record | Active account: account lifetime. Ordinary deletion: erased at final deletion. Terminated account: 2 years | CONFIRMED D186, D191 | Encrypted, restricted. Live holds override. |
| Didit verification evidence (held by Didit) | About 30 days at Didit; deletion of the member's sessions requested at final deletion | CONFIRMED D173, D186 | Swoovr never stores document images, selfie, liveness, biometric data, document number or address. Failed deletion requests are retried. |
| Previous verified email and mobile (contact history) | 2 years from the change | CONFIRMED D187 | Erased at final deletion unless the account was terminated. Live holds override. |
| Terminated member's identity fingerprint (legal name and date of birth) | 2 years | CONFIRMED D191 | Match sends to staff review only. |
| Banned-identifier registry: active restriction | No automatic expiry; necessity review every 6 months | CONFIRMED D217; review interval DEFAULT D236, D240–D242 | Requires legal approval for indefinite serious restrictions. Review records why the restriction remains justified; it never renews an inactive restriction. |
| Banned-identifier registry: revoked or expired history | 24 calendar months after the latest effective end, review, non-expiry event or note, and related case and hold obligations | DEFAULT D236, D251–D253 | Bounded disposal replaces matchable snapshots, reasons and notes with a minimal non-matchable disposition. Missing provenance or unknown preservation refuses erasure. |
| Minimal registry dispositions and reinstatement reviews | While the original termination or related preservation or compliance obligations continue; each new reinstatement review has its own 24-month obligation | DEFAULT D251–D253 | Prevent reconstruction of released identifiers; then dispose of reviews and receipts so case and tombstone disposal can resume. |
| Identifier-check quota metadata | 24 hours | DEFAULT D239 | Purpose-separated HMAC keys and opaque match references only. Held accounts or matched cases extend; unknown membership stops expiry. |
| Email-retirement commands and resume reviews | Pending or NeedsReview: kept while unresolved. Completed: 24 calendar months after completion, and only when the case and preservation window permits | DEFAULT D257, D259 | Encrypted exact snapshot included in key rotation. A minimal retired-contact flag without the old contact may remain while needed to prevent unsafe claim adoption. |
| Mobile-binding retirement | Archived number follows contact history; case event restarts only its registry-history window | DEFAULT D254 | Preserves original termination, closure and revocation dates. |
| Qualifying review evidence | At least 12 calendar months after the latest closure, referral, appeal decision or evidence export | CONFIRMED minimum D216; DEFAULT that all moderation cases qualify | Pending appeals and open reviews have no disposal clock. |
| Serious incident evidence | 24 calendar months after the latest completed review or referral | DEFAULT pending legal review | Active and overlapping holds override. Includes historical serious reports and known-content cases. |
| Minimal review and compliance records | At least 2 years; implemented as 24 calendar months after the latest completed review or referral | CONFIRMED baseline D216, D220 | Detailed evidence may expire earlier. A case whose evidence has been disposed of is never reopened. |
| Police-case records | 7 years (84 calendar months) after closure, including the latest transfer receipt | CONFIRMED D195 (procedure §13) | Active holds and retained linked evidence postpone disposal; missing closure or provenance fails closed. |
| Released hold operational group (reason, conditions, release notes, scopes, custody chain, export purposes, corrections, redactions, approvals, disclosures, transfers) | The later of the applicable review or police window and 24 calendar months after the group's latest activity; police and disclosure groups 84 calendar months | DEFAULT D260 | Whole chain and related free text removed atomically, leaving an ids-only completion audit. A chain link is never deleted on its own. |
| Sealed hold copies and referenced photos | Hold release resumes the applicable review or police schedule | DEFAULT D231–D233 | Unknown provenance requires restricted review. Unlinked staff holds require independent legal attestation before disposal. |
| Staff conflict association | At least 24 calendar months after the latest explicit association or declaration, extended while related data remains | DEFAULT D235 | No email-based inference. Expired source assertions are removed atomically. |
| Restricted-access log and operational audit | 24 calendar months | DEFAULT D262 | Oldest verified prefix replaced by a minimal chain checkpoint. Deferred while any retained hold group, copy, review or police source, or unclosed or recent security incident may need it. |
| Closed security incidents | 24 calendar months after the latest closure, assessment or containment | DEFAULT D262 | Subject to source and preservation obligations. |
| Moderation action receipts and daily inspection totals (annual reporting) | 36 calendar months; the completed UTC reporting year is archived as aggregates before first expiry | DEFAULT D268 | Holds, unknown preservation and retained evidence copies prevent disposal of personal receipts. |
| Text-review processing receipts | 30 days | DEFAULT D266 | Contain no message text. Candidate evidence follows case retention. |
| Meeting-safety sessions and trusted contacts | Shared links: final expected end plus 24 hours, at most 30 days from creation. Ordinary records: 30 days after final closure | DEFAULT D263 | Linked incidents, reviews and holds preserve. Withdrawn or expired contact records follow their own schedule. |
| Search events (rate limiting) | 1 day | DEFAULT D199 | No query content. |
| Policy acceptance records | Until final ordinary deletion | DEFAULT D269 | Held accounts require valid encrypted acceptance copies before provider deletion and before source erasure. |
| Safety notice delivery, centre rows and publication operator fields | 60 days after publication; unused staging 60 days after staging | DEFAULT D269 | Version dispositions without member or operator identifiers are kept to prevent reuse. |
| Beta journey observations | Current UTC day plus 89 previous days | DEFAULT D272 | Holds, retained copies and unknown provenance defer. |
| Beta enrolments | Unactivated unlinked: 90 days after creation. Revoked: 90 days after revocation. Linked: until final deletion. Pending invitation effects: 72 hours after queueing | DEFAULT D275 | Unresolved held or copy populations conservatively defer unlinked expiry. |
| Appearance preference | Until final deletion | DEFAULT D277 | Included in the owner's export. |
| Founder recovery token | Kept after deletion until the member asks for it to be deleted | CONFIRMED D65, D173 | Independent of identity records and ban enforcement. |
| Backups | Daily 30 days; weekly 4 weeks (28 days); monthly 12 calendar months | CONFIRMED D216; DEFAULT tooling D261 | Encrypted, isolated, automatic expiry from immutable capture clocks; Blob lifecycle is a delayed backstop. Not the primary evidence store. |

## Disposal authority

Account state changes use a short per-account PostgreSQL advisory lease. Screening and publication use a separate media lease. Coverage changes take the global preservation barrier exclusively; source destruction, capture and ordinary committed writes take it shared. Case and hold mutexes serialise retention decisions and evidence numbering and custody commits across processes. Lock order is: account state, media when needed, preservation barrier, case or hold mutex. Shared authority is never upgraded to exclusive. Conversation row locks follow the preservation barrier.

Disposal rechecks live holds under authority, commits before releasing it, and retries interrupted due work. Case snapshots retain explicit media references; unreadable legacy references fail closed. Released retained copies remain accessible only through the existing legal-role, conflict, purpose, elevation, approval and expiring one-use export controls. Known-content media is never rendered.

The global audit chain expires through a validated, bounded database operation under preservation and chain authority. Ordinary update, delete and truncate remain refused, null or future purge authority is refused, and the read window shares chain authority with append and expiry. Hosted runtime roles receive only the required expiry-function execution, never checkpoint writes; database owners can alter the schema and are outside this integrity boundary.

## Operations

| Command | Effect |
| --- | --- |
| `retention purge` | Reconciles explicit historical staff associations; coordinates operational-group, case, account, message, media, registry-history and audit expiry; then expires eligible minimal conflict records. |
| `retention purge --case <id>` | Restricts case and copy disposal to one case. |
| `retention purge --account <id>` | Restricts review and copy expiry to the named member's cases and known copies. |
| `accounts purge <id>` | Restricts account finalisation and media sweeping to one account. |
| `staff conflicts reconcile` | Materialises authoritative current and audited links and runs the same guarded expiry. |
| `bans legacy-status` | Counts-only gate for unversioned legacy identifier markers; no guessed cryptographic migration. |

The hosted worker runs hourly; Development requires explicit opt-in. Case scans use keyset paging so held or unresolved batches cannot starve later eligible records. Counts and machine-readable reasons are logged; content is not. Backup generation and quarantined paired recovery are described in [BACKUP-RESTORE.md](BACKUP-RESTORE.md): an old archive without current deletion and hold authority cannot be promoted, and restore must reconcile completed deletions, expired records and legal holds before any member traffic resumes.

## Decision record

These notes record how the schedule was implemented. The table above is authoritative where wording differs.

- **D231–D233** added the written schedule, calendar review clocks, independently retained copies and photo references, and guarded disposal.
- **D235** set the staff-conflict association minimum and preserves original assertion provenance across deletion and relinking; unknown held membership stops expiry.
- **D236–D239** added the typed, versioned HMAC banned-identifier registry with admission and contact-change checks, the 24-month inactive-history minimum, the 6-month active necessity-review interval and identifier-check quota expiry.
- **D240–D242** populate current verified contact restrictions atomically with permanent termination; they survive ordinary final deletion. Backfill uses positive active-sanction or case provenance and never reactivates revoked rows.
- **D250** appends scheduled Active→Expired registry transitions with linked system case notes. Expiry stops matches at the original instant; delayed job processing does not extend the inactive clock.
- **D251–D253** perform bounded inactive-history disposal and keep minimal dispositions as described in the table, superseding the earlier gaps noted under D236–D250.
- **D254** implements the mobile part of CONFIRMED D249 separately from restriction release or account restoration. Same-contact pending verification challenges become used without erasing their independent rate-limit or evidence history; session cutoffs contain no contact value.
- **D257 and D259** set email-retirement command and resume-review retention. No foreign-key cascade erases uncertain intent; eligible review and parent deletion commit atomically, and failure restores both. Reviews keep reviewer, grant, time and a bounded explanation, never raw contact values. Provider minimal login-policy markers remain provider-owned until subject deletion.
- **D260** groups released hold operational records for single atomic expiry, run before case disposal.
- **D261** added encrypted, isolated backup generation and quarantined paired recovery.
- **D262** set audit and residual operational metadata retention. Query events do not record result sets, so prefixes are conservatively pinned while potentially relevant source records remain; not every unrelated row expires at exactly 24 months. Old grant purposes expire 24 months after final expiry or revocation once retained access-log and email-retirement references permit.
- **D268** set annual reporting receipts. Country is member-declared at the time of action, never verified residence. A year outside the complete retained window cannot be recomputed manually. See [annual data](MODERATION-ANNUAL-DATA.md).
- **D269** set policy acceptance and safety notice retention. Reading a policy page creates no acceptance. See [scope](PUBLIC-POLICIES-AND-SAFETY-ADVICE.md).
- **D272** set beta journey observations: no event body, trait, mode, location, counterpart, IP or device field exists; own exports include only the caller's daily metadata; collection can be disabled without disabling expiry.
- **D275** set beta enrolment and invitation retention; no duplicate address or body outbox exists.
- **D277** set appearance preference retention; the appearance-change audit omits the chosen scheme and mode.

## Outstanding before production

1. Legal approval (57) of every DEFAULT period, and specifically of indefinite active restrictions, serious-incident evidence at 24 months, police records at 7 years and terminated-member identity retention at 2 years.
2. Hosted acceptance: deployment scheduling, networking and the hourly worker in the production environment; an Azure backup restore drill with deletion, expiry and hold reconciliation (D261). No restore drill is claimed yet.
3. Provider email and login retirement (D249) remains incomplete.
4. Unversioned hash-only legacy identifier sources require review before any import; unknown original ownership, key or case remains a review requirement.
5. Remaining issue 118 work: global sensitive-access-chain and other operational minimisation, and further minimal-case and tombstone trimming.

Local implementation evidence is in [VALIDATION.md](VALIDATION.md). Synthetic fixture clocks are not a production restoration drill, and this schedule does not close issue 118 or establish UAT readiness.
