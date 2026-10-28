# Blocked-photo preservation recovery

DEFAULT D229 implements issue [126](https://github.com/repasscloud/wanderconnect/issues/126) under existing D203 and the internal policy D185. A refusal becoming visible is not evidence that preservation finished.

The blocked state/work commits first. Preservation serializes per account, reuses the urgent case and original hold, authenticates the deterministic sealed object and verifies its hash inside the worker. Ciphertext never reaches the screener or a member/staff view route. The canonical evidence object key commits before deleting quarantine, so an interrupted object move is recoverable. A unique SourceMediaId on the hold commits before its media link, so it is found and reused after a crash. New uploads can establish their own hold in the same open case, rather than inheriting an older released hold. Legacy unlinked records are matched only through their original screen/review decision timestamp; ambiguous origins require review. A released original is linked and stops for legal/operator review; recovery never quietly creates a replacement. Account deletion waits across the blocked-save/before-hold gap. Once a hold exists, the established before-deletion capture protects ordinary records.

Capture and accepted-alert progress commit separately. An unavailable key/store/capture or explicitly refused SMTP attempt can recover later without repeating accepted recipient slots. Emails contain case id/category/queue only, never image/member ids, object keys or content. Primary/backup destinations are resolved from configuration; identical addresses use one slot. The ledger stores neither addresses nor bodies; an operator reconciliation explanation is separate restricted metadata. A legacy case-level escalation audit is labelled legacy-recorded and requires review; it does not prove each current destination accepted an alert.

A network interruption can occur after SMTP accepted DATA but before acknowledgement. Exactly-once end-to-end email delivery cannot be established by a database transaction. Durable attempting states therefore become unknown, never automatic resend candidates. The urgent case remains visible independently of email. An operator must reconcile from independent provider/sink evidence before confirming acceptance or authorising a retry. Provider/sink acceptance never proves delivery/read.

## Operator commands

Run in the configured API job/container with its runtime database/store/evidence keys and escalation-email provider. Secrets stay in secure configuration, never command arguments or this document.

```sh
dotnet Swoovr.Api.dll media preserve
dotnet Swoovr.Api.dll media preserve <media-id>
```

The full sweep processes up to 100 due items; completed-total/waiting/review counts contain no content. Exit 1 means unfinished work remains; do not treat it as a successful readiness gate. The existing worker checks every 30 seconds, with retry backoff from 30 seconds to one hour. Inspect only restricted metadata when diagnosing:

```sql
SELECT id, case_id, hold_id, preservation_error, preservation_attempts,
       next_preservation_at, preservation_completed_at
FROM media_items
WHERE preservation_pending_at IS NOT NULL AND preservation_completed_at IS NULL;
```

For an unknown/rejected/legacy-recorded **email outcome**, verify the provider/sink evidence first. Save a 20–2000 character explanation in a private temporary file outside the repository. These commands run under the trusted operator workload (same privilege boundary as other CLI bootstrap/purge commands), store the explanation only in the restricted delivery row and audit ids/status/CLI attribution:

```sh
dotnet Swoovr.Api.dll media alerts resolve <media-id> primary accepted --note-file /private/path/explanation.txt
dotnet Swoovr.Api.dll media alerts resolve <media-id> backup retry --note-file /private/path/explanation.txt
dotnet Swoovr.Api.dll media preserve <media-id>
```

Accepted means independent confirmation of provider acceptance. Retry requires confirmation that the previous attempt was not accepted; never use it merely because the recipient has not read an email. Accepted outcomes cannot be reset by this command. A released hold is a different legal disposition; these email commands do not replace/release/recreate a hold. Follow the legal process and the applicable retention schedule, not an automatic reset.

## Tests and boundaries

`tests/media/recovery.browser.cjs` uses synthetic local files, labelled simulated screening, PostgreSQL, Mailpit and fictional adult accounts. It stages interruption after refusal/hold creation/sealing/capture, capture failure, concurrent recovery, SMTP refusal, uncertain intent/reconciliation and legal release before media linkage. It also exercises the account-deletion guard. No remote known-content service or actual abuse imagery is used. Local fixture holds are explicitly released before deleting the test-owned temporary files/rows. Existing R2 photo/chat suites separately verify the integrated pipeline.

Development-only `Media:Preservation:Fault` accepts blocked-save, hold-create, object-sealed, capture-complete, capture-error. Every nonempty hook fails startup outside Development; it is never a hosted workaround. No shared local configuration edit is needed. No public DTO, media-serving permission, design token or UI content changed.

Actual results/gaps: [VALIDATION.md](VALIDATION.md). This slice does not implement D216/D217's broader case-retention schedule, backups or identifier registry. Those remain in the authorised queue. The SMTP outcome classification uses the [Microsoft SMTP status definitions](https://learn.microsoft.com/dotnet/api/system.net.mail.smtpstatuscode?view=net-10.0); uncertain transport failure remains unknown.


## Concurrent verdicts (D230, issue128)

Screening, reviewer decisions, recovery, disposal and final account purge share the per-account session lease. A pending approval rechecks state after acquiring it; blocked/rejected/removed state cannot be republished. Reviewer escalation after source disposal is refused. Member removal writes hidden intent promptly; physical deletion waits for in-flight classification, with deferred disposal recovered by the worker. Known-content matches preserve evidence despite a concurrent removal intent. An aborted approval returns conflict rather than reporting success. Sealing also disposes both deterministic clear locations, including an orphan left by a process stopping after photo-write but before location-save; failed disposal remains retryable.

`tests/media/transitions.browser.cjs` uses two actual API processes and synthetic LocalMediaStore files. Development-only `Media:Publication:BarrierDirectory` / `BarrierStage=before-lease|after-read` pause operations using empty marker files, never image contents. Both stages are rejected outside Development. These hooks require no shared configuration edits.
