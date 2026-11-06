# Helpdesk retention and customer retirement: how it can work (#207)

Status: **decided and built.** The owner chose 136.1 **A**, 136.2 **2a** and 136.3 **3a** on [#224](https://github.com/repasscloud/wanderconnect/issues/224) (D337). The options below are kept as the record of what was considered. How to operate it is in [OPERATIONS.md](OPERATIONS.md#helpdesk-retention-d337).

## What's decided (D333, from #207)

- Retention periods start at final closure or resolution. General Support and guest/email-only: 24 months. Billing correspondence: 24 months (financial evidence: at least 7 years). Trust & Safety: 5 years. Legal and law enforcement: 7 years. Privacy: 3 years. Security: 5 years. Logs: 90 days. Helpdesk backups: no more than 35 days.
- Reopening a ticket recalculates its period. Reclassifying or linking it to a case triggers a reassessment. Legal holds win, and are rechecked just before anything is deleted.
- After **final** account deletion (after the 30-day grace period), the member's Zammad customer is retired automatically: deactivated, `retired-` login, display name "Retired Swoovr Member", personal fields removed, tickets kept. The retirement is audited, idempotent and retried.
- Swoovr decides eligibility. Zammad's own unrestricted deletion schedules are not the authority. No new retention system if an existing worker can do it. No database triggers as a substitute.

## What already exists

| Piece | Where | Reuse |
| --- | --- | --- |
| Hourly retention run, with per-record leases and fail-closed hold checks | `RetentionWorker` / `RetentionJobs` (`src/Swoovr.Api/Retention/`) | Add one helpdesk step |
| Operator command for the same run | `Swoovr.Api.dll retention purge` (`Cli.cs`) | Same code, can be run from cron |
| Durable helpdesk operations (retry, idempotent markers) | `TicketingOperation`, `TicketingOperations.cs` | The customer-retirement operation goes here |
| Helpdesk events (IDs only) plus a periodic sweep | `TicketingWebhookWork`, `TicketingEventWorker` | Feed close/reopen/state changes |
| Ticket ↔ case/appeal links | `TicketingCaseLink` | How a legal hold or case reaches a ticket |
| Legal holds and preservation leases | `LegalHoldLifecycleFilter`, `PreservationLease` | Check just before each delete |
| Final account deletion | `AccountDeletion.PurgeAsync(finalize: true)` | The trigger for retirement (today it does **not** touch Zammad: the gap #207 found) |
| Zammad backups | `zammad-backup` in `containers/zammad/compose.upstream.yaml` (`HOLD_DAYS` default 10, `TZ` default Europe/Berlin) | Set by environment, without editing the upstream file |

## Decision 1: where the scheduled job runs

**A. Inside the API, as a step in the existing hourly `RetentionWorker` (recommended).**
Add `HelpdeskRetention` to `RetentionJobs.RunAsync`, after the hold checks the run already does. `retention purge` runs the same code on demand.
- Pros: no new infrastructure; it reuses leases, fail-closed holds, audit and logging; it's safe across replicas because rows are claimed with `FOR UPDATE SKIP LOCKED`; it matches "don't introduce a separate retention system".
- Cons: it runs wherever the API runs, so a busy API host also does deletion work. That's small: one hourly batch with a bounded size.

**B. Outside the API: a scheduler calls the same CLI.**
Something on a schedule runs `dotnet Swoovr.Api.dll retention purge` with the API's settings, and the in-API worker is switched off by configuration. The scheduler could be:
- host `crontab` on the UAT/production VM (`0 * * * *`);
- an **Azure Container Apps Job** or a Kubernetes `CronJob` using the API image (fits Q90 hosting once it's decided); or
- a small `swoovr-retention` compose service using the API image, running `supercronic` (a cron for containers) inside it.
- Pros: separate logs, alerts and resource limits; a run can't be affected by API restarts; easy to see "did tonight's run happen".
- Cons: another thing to deploy and monitor; the schedule lives outside the repo's runtime (in a crontab or the hosting config). **Same code as A**, so this can be decided later, at hosting time.

**C. SQL only (`pg_cron` or `psql` from crontab). Not recommended.**
SQL can work out what's *due* in Swoovr's database. It can't safely delete from Zammad. Deleting rows from Zammad's own Postgres skips its attachment store, search index and history, and breaks its consistency. It also can't call Zammad's API or recheck holds just before deleting. At most it's useful as a read-only "due next week" report.

**D. Zammad's own scheduler (delete by tag/group and age). Rejected.**
Zammad can delete tickets on a schedule, but it knows nothing about Swoovr legal holds, case links or reopen history. #207 excludes it as the authority.

> **Recommendation:** A now, built so B is only a configuration change (`Retention:Worker:Enabled=false`, plus a crontab or a Container Apps Job calling `retention purge`). Choose between A and B when the UAT hosting decision (Q90, #124) lands.

## Decision 2: how a ticket gets its retention category

- **2a. From its Zammad group (queue), with a staff override in Swoovr Admin (recommended).** General Support → General; Billing → Billing; Trust & Safety and T&S Restricted → Trust & Safety; Legal & Law Enforcement → Legal; Privacy & Data Protection → Privacy; Security → Security; guest/email-only customers → General. Moving a ticket to another group reclassifies it. On the support request page, staff can mark "financial evidence: keep 7 years" or override the category with a reason (audited).
- **2b. A Zammad custom ticket field (`swoovr_retention`) that staff set in Zammad.** It's visible in Zammad's own UI, but staff can forget it, and Swoovr then has to trust a value edited in Zammad.
- **2c. Group only, no override.** Simplest, but it can't express "this billing ticket is financial evidence".

## Decision 3: the retired customer's identifier

- **3a. `retired-{random}`, a new opaque ID (recommended).** #207 prefers a non-identifying ID where the link back isn't needed. Swoovr keeps only the Zammad user ID → retired ID mapping, so it can delete the customer later, and stores no member ID once the account is purged.
- **3b. `retired-{memberId}`.** This is #207's example. It's easy to trace, but it stays personal information for as long as anything links that member ID to a person.

## How it fits together (for options A + 2a + 3a)

1. **Ledger.** A new Swoovr table, `ticketing_retention`, holds one row per Zammad ticket: category, reason, `closed_at`, `retain_until`, financial-evidence flag, `last_reviewed_at`, attempts, outcome, `disposed_at`. It stores IDs only, no content.
2. **Feeding it.** Helpdesk events and the existing sweep record close, reopen (`retain_until` cleared), group changes (reclassify) and case links (reassess). A daily catch-up asks Zammad for tickets closed since the last sweep, so a missed event can't leave a ticket untracked.
3. **Each hourly run:** take due rows (`retain_until <= now`, not disposed) using `SKIP LOCKED`, then for each one:
   1. recheck holds, linked cases and appeals under a preservation lease;
   2. delete it in Zammad through its API (articles, attachments and search entries go with the ticket; this must be confirmed against our pinned 7.2.2 before building on it);
   3. record the outcome and audit it (ticket ID, category, rule, no content);
   4. back off on failure, and show persistent failures in Admin diagnostics.
4. **Customer retirement.** `AccountDeletion` (finalize) writes a `retire-customer` `TicketingOperation` in the **same transaction** as the purge. The operation worker then:
   1. deactivates the Zammad user;
   2. sets the `retired-…` login and the name "Retired Swoovr Member";
   3. clears email, phone, mobile and other profile fields.

   Re-running it is harmless. Sign-in is refused because the user is inactive with no password, and Swoovr never reactivates a retired mapping. Once the last ticket of a retired customer is disposed, the run deletes the customer too.
5. **Logs, 90 days.** Swoovr purges completed `ticketing_webhook_work` and operation rows after 90 days, in the same run. Zammad's own HTTP/communication logs and other provider data are cleaned up using Zammad's own cleanup settings. Which settings to use is confirmed when they're configured.
6. **Backups, 35 days.** Set `HOLD_DAYS=35` (or less) and `TZ=Australia/Adelaide` in the Zammad environment file. The upstream compose file stays unedited. Backups stay encrypted at rest on the host or storage.
7. **Restore reconciliation.** The ledger keeps disposed ticket IDs and retired customer IDs, with no content, for longer than the backup window. After any Zammad restore, `retention helpdesk --reconcile` deletes those tickets again and retires those customers again before staff use the helpdesk.

## Suggested order of build

1. **Customer retirement on final deletion.** Small, needed whatever else is chosen, and closes the gap #207 found.
2. Backup `HOLD_DAYS`/`TZ`, and the 90-day purge of Swoovr's own helpdesk logs.
3. The ledger plus classification (Decision 2).
4. The deletion step in the hourly run (Decision 1), and restore reconciliation.
5. An end-to-end test on the local Zammad with fictional tickets: a ticket under hold, a reopened ticket, a ticket left after retirement, and a restore from a backup.
