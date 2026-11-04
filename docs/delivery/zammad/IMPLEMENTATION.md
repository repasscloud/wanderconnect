# Zammad implementation ledger

Authority: `docs/superpowers/plans/2026-11-04-zammad-implementation-handoff.md` supersedes discovery-only instructions in `swoovr-zammad-integration-review-prompt.md`.

CONFIRMED: API-mediated clients, Zammad-owned correspondence, Swoovr-owned evidence and enforcement, thirteen groups, local real-provider acceptance, no public website implementation, no merge/deployment. Sensitive-document collection is explicitly deferred.

## Phases and coverage

All items remain pending until evidence is recorded. Each source catalogue section maps below; no capability is established merely by this plan.

| Phase | Source catalogue coverage | Deliverables and acceptance |
| --- | --- | --- |
| 1 | Provisioning, infrastructure, groups, operational prerequisites | Pinned Compose, isolated database/secrets, private network, lifecycle, idempotent provisioning twice, mutual reachability |
| 2 | Identity/customers, ticketing model, data model | Domain provider boundary, bounded HTTP adapter, member mapping, minimal durable operations, uncertainty recovery, unchanged report references |
| 3 | Member/Admin frontend, webhooks | Member Web/MAUI and Admin create/list/read/reply, notes, assignment, state, signed durable events and notifications; real end-to-end acceptance |
| 4 | Admin operations, permissions, attachments/configuration | Per-operation/group permissions in API and Zammad, paging/filter/search/bulk/macros, attachment validation, diagnostics, restricted transfer access loss |
| 5 | Guest/public website backend | API contract, limited grants, verification/exchange/revocation, origin/CSRF/rate protection, Mailpit harness; independent website excluded |
| 6 | T&S, appeals, support entry points | Link safe references, preserve authoritative outcomes/evidence/conflicts, no automatic historical backfill (DEFAULT) |
| 7 | Webhooks/notifications/email, operations | Durable acceptance, dedupe/reconcile/dead letters, local inbound/outbound threading, outage/restart, backup/restore, upgrade guide |
| 8 | Acceptance/UAT, delivery | Full checks, available native builds, security review, requirements evidence, operator/API/future-frontend handoff, PR to main |

## Decisions

- DEFAULT: work on `codex/feature/zammad-integration` in the existing checkout; its only initial untracked file is the authorised handoff. No unrelated tracked modifications were present.
- DEFAULT: preserve existing platform ports and OIDC issuers. Connect its Mailpit network anchor to a dedicated shared network using aliases; Zammad uses a separate Compose project and database.
- OPEN external gates: production mailbox/DNS/provider inputs, remote hosting, actual device acceptance, legal retention/licensing review. These do not block authorised local engineering.

## Evidence and next actions

- 2026-11-04: Docker engine 29.9.2 reachable; existing Swoovr platform is running, using shared Mailpit network namespace. Host disk has approximately 52 GiB available.
- Read README, architecture, current identity/safety specification, delivery status, handoff and detailed source catalogue. Loaded executing-plans/TDD/worktree, Postman API engineering/discovery, Microsoft code-reference skills.
- Baseline `./scripts/check.sh` started; log `.local/zammad/baseline.log` (untracked private scratch).
- Phase 1 in progress: verify upstream release/images, install isolated stack, reproduce provider operations before implementing transport assumptions.

## Continuation

Read this ledger and `git status` first. Do not restart completed checks without reason. Phases 1–8 are not complete. No implementation, integration success or PR is claimed yet.

### Evidence update

- Real pinned Zammad is running: image `ghcr.io/zammad/zammad:7.2.2-0000`, application `7.2.2-295796a2.docker`. PostgreSQL/Redis/memcache/Rails healthy; nginx loopback 8095. Upstream Compose v17.5.2 is vendored with license.
- Provisioning ran successfully twice; thirteen named groups discovered, no environment-specific IDs hard-coded. Initial Users group retained as a record and disabled only if empty. Provisioning and identity tokens are separate.
- Live API probes returned 200 for groups (14 including disabled Users), roles, ticket states, priorities, macros, text modules, webhooks, triggers and object-manager attributes. This establishes read availability only; mutation and permission acceptance remain pending.
- Bootstrap fixes reproduced: Rails requires `group_ids_access_map` rather than association `group_ids`; current Token permissions use an array, despite a stale map-shaped comment in upstream Token source. Corrected and re-provisioned; authenticated probes now succeed.
- Actual pinned webhook job sets `X-Zammad-Delivery` to durable ActiveJob `job_id`. Signature is raw-body HMAC-SHA1 with `sha1=` prefix. Real signed-delivery integration remains pending.
- `./scripts/check.sh` initial run failed PostgreSQL 28P01 because `.local/docker.env` belongs to the older stack. Re-run with `.local/platform/docker.env` credentials passed (exit 0), including persistence and HTTP smoke suites. Secrets were read inside Python and never printed.
- New lifecycle checks: 2/2 pass; new .NET ticketing checks pass for canonical identity, fail-closed group defaults, signature validation, paging, forbidden responses, uncertain write/no auto-retry and missing visibility schema. Transport is not yet composed into API routes.
- Postman Orbit discovery returned unrelated providers, no usable Zammad contract. Direct official documentation plus pinned runtime/source provide provider evidence. No Postman workspace or shared publication was created.

Next: custom attributes/webhook/trigger provisioning and local capability acceptance, durable minimal integration mappings/inbox/operation state, API orchestration and secured member/guest/staff clients. No phase is complete yet.

### Security checkpoint and additional acceptance

- Real typed adapter creates/reads tickets, distinguishes public/private replies and preserves ticket access after verified email mutation. Indexed operation search returned zero immediately after creation: asynchronous search is not sufficient proof of write absence. No automatic uncertain-write replay is implemented.
- Real Zammad trigger -> host Swoovr API -> disposable PostgreSQL inbox passed. Also tested exact replay, tamper denial, article-free changes and changed-body delivery conflict. Harness restores inactive webhook/trigger and drops its owned database.
- Review found unsigned-header replay, caller-cancelled write uncertainty, nullable case-link uniqueness and permissive existing secret-output files. Fixes add signed-body uniqueness with signed update clock, uncertain write cancellation/schema handling, filtered case-link uniqueness and atomic private replacement. Targeted regression tests cover these cases; full final check pending.
- Database backup restored into a separate disposable database with matching ticket/article/user/attachment-store counts. Filesystem storage restoration and full application disaster recovery remain unverified.
- Local SMTP notifications configured to `swoovr-platform:1025` (Mailpit only); inbox count increased locally. Inbound threading and external email delivery are not established.
- All actual images now pinned by locally verified registry digests in the local override. Upstream file remains unedited.
- Ruling: naming guard explicitly exempts only the external Zammad adapter's required `/api/v1` URL; Swoovr endpoint path checks remain enforced.

### Remaining authorised implementation (not completed)

Phase 1 still needs supported runtime staff identities/group mappings and complete provisioning mutation acceptance. Phase 2 still needs identity/operation orchestration and concurrent/lost-response recovery. Phases 3–6 remain unimplemented: member Web/MAUI and Admin ticket journeys, full staff capabilities/permissions/attachments, guest backend contract/grants, appeals/report/support links. Phase 7 needs inbox processing, notifications, reconciliation/dead letters, inbound threading, outage/restart and complete dataset restore. Phase 8 needs client/native acceptance and final complete-scope PR. These are engineering work remaining, not external blockers or owner decisions.

### Foundation checkpoint verification

- Full `./scripts/check.sh` passed (exit 0): zero build warnings/errors, **721 PostgreSQL checks**, policy/client checks and all three HTTP smoke suites. Baseline had 708 PostgreSQL checks. The gate used the running platform's credentials through `SWOOVR_TEST_DATABASE`; the legacy automatic credential-selection gap remains documented above.
- Digest-pinned stack recreation/start passed `docker compose up --wait`; init subsequently exited 0, Rails/database/cache healthy, original Swoovr hosts retained. Repeated provisioning passed after recreation.
- Real mutual reachability passed: helper executing in the existing Swoovr API network namespace reached Zammad nginx HTTP 200; Zammad Rails reached Swoovr `/health/live` HTTP 200.
- Latest real webhook acceptance passed with actual interpolated UTC timestamps. Fresh-delivery replay of the same signed bytes persists exactly one inbox row. Real Zammad renders timestamps as `yyyy-MM-dd HH:mm:ss UTC`; the parser explicitly accepts that tested representation as well as ISO timestamps.
- PostgreSQL operation checks cover lost customer/ticket responses, verified email continuity, negative asynchronous search without blind creation, payload-bound idempotency keys and concurrent contexts creating one customer/ticket. These inject uncertain provider responses; they are not claims of a real network-fault experiment.
- Fresh-context security checkpoint completed and actionable foundation findings fixed. Later orchestration received an author review; complete-integration review remains pending. No native UI/runtime acceptance is claimed because native ticket screens are not implemented.

### Exact continuation checkpoint

Branch `codex/feature/zammad-integration`, original base `68104d4`. Local stack: `python3 scripts/zammad_local.py up`, then `provision`; ordinary provision leaves webhook/trigger inactive. `provider_acceptance.py` creates only fictional fixtures; `webhook_acceptance.py` launches a temporary API on 5095 and drops its owned database in finally. Private token/config/log state: ignored `.local/zammad/`; never commit/export it.

Next implement actor credential resolution with per-staff Zammad permissions and current Swoovr account/group/conflict/device gates, then authenticated member/staff endpoints/DTOs/clients and the complete native vertical slice. `TicketingOperations` is internal orchestration only, not an authorization boundary; neither it nor the transport is composed into member/staff endpoints. Recover uncertain operations conservatively; indexed-search absence is never proof that a write did not happen. Exact operation-reference equality must be checked on search results.

After the vertical slice continue phases 4–8 in order. Do not describe this foundation checkpoint as the requested completed integration or as an external blocker. The remaining client/guest/appeals/reliability work remains authorised and required.
