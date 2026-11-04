# Zammad requirements coverage

Every row of the original catalogue (`swoovr-zammad-integration-review-prompt.md`, inventoried as R001–R062) mapped to implementation and evidence, an exclusion authorised by the owner's handoff (`docs/superpowers/plans/2026-11-04-zammad-implementation-handoff.md`), or a genuine external gate. Labels: **Done** (implemented and locally accepted), **DEFAULT** (routine engineering choice, reversible), **Excluded** (authorised by the handoff), **External** (needs inputs this environment can't supply), **OPEN** (owner decision). Evidence names refer to harnesses in `tests/zammad/` and results in `IMPLEMENTATION.md`.

## Identity and customers

| ID | Status | Where / evidence |
| --- | --- | --- |
| R001 | Done | Keycloak sessions (real code+PKCE in `runtime_acceptance.py`); Swoovr owns member/profile data; Zammad holds only a customer login |
| R002 | Done | `swoovr-{guid}` login (`TicketingPolicy.MemberLogin`); email synced as a mutable attribute; runtime "verified email" checks |
| R003 | Done (DEFAULT variant) | Guests: `swoovr-guest-{id}` customers with no helpdesk email (Zammad enforces unique emails, `user_email_multiple_use=false`); mailbox senders get Zammad's email-based customers, labelled unverified in Admin |
| R004 | Done | Customers created on first support interaction only (`EnsureCustomerAsync`, `EnsureGuestCustomerAsync`); reused across tickets |
| R005 | Done | `TicketingIdentity` GUID→numeric mapping; access resolves via mapping, never email; verified email sync (runtime checks) |
| R006 | Done | Guest code→grant flow (`GUEST-API.md`); ticket numbers/emails/GUIDs not credentials (guest checks) |
| R007 | Done | Email change continuity, guest/member same address never merged, deletion pending/cancelled, disabled/promoted provider customer fails closed, barred members keep support (runtime + guest checks) |
| R008 | Done | Separate provisioning token, identity-lifecycle token and per-staff agent tokens; no application-wide agent token (`API.md` actor design) |

## Ticketing model

| ID | Status | Where / evidence |
| --- | --- | --- |
| R009 | Done | Zammad authoritative; Swoovr stores only mappings, operations, inbox ids, links and guest grants |
| R010 | Done | `ITicketingProvider`/`ITicketingWorkspaceProvider` (Domain), Zammad adapters (Infrastructure), Contracts + typed clients |
| R011 | Done | `TK-{id}` request references, `SW-XXXXXX` report references unchanged; no collision or renumbering (portable check) |
| R012 | Done | Private notes/attachments excluded from member/guest projections, notices and errors (runtime, guest, journey, events checks) |
| R013 | Done | Queue + `swoovr_category/subcategory/sensitivity/resolution` attributes, priority, source (`swoovr_source`), case links; 13 groups only |
| R014 | Done | Server-derived customer; raw `From` ignored; staff create-on-behalf by exact lookup (runtime checks) |
| R015 | Done | Per-staff Zammad grants AND Swoovr role/conflict checks on every read/write/download; transfer removes access immediately (runtime) |
| R016 | Done | Follow-up follows the queue's Zammad policy; closed requests show "start a new request" (`canReply`); journey shows Closed |

## Trust & Safety boundary

| ID | Status | Where / evidence |
| --- | --- | --- |
| R017 | Done | T&S queues are Zammad groups (Trust & Safety, Restricted, Legal, Privacy, Security); no second ticket system |
| R018 | Done | Cases, evidence, sanctions, appeals stay in Swoovr; links are references; closing a request changes nothing (runtime check) |
| R019 | Done | No evidence copied; sensitive-looking uploads refused; `.txt` only; secure document collection deferred |
| R020 | Done | Visibility split documented in `API.md`/`IMPLEMENTATION.md`; Admin shows linked cases with deep links into Swoovr case screens |
| R021 | Partly / OPEN | Sensitive queues need elevation + fresh sign-in + pinned channel; conflict exclusions enforced. Jurisdiction-specific retention for helpdesk data is a legal decision (OPEN, see `OPERATIONS.md`) |

## Public website, guest support and email

| ID | Status | Where / evidence |
| --- | --- | --- |
| R022 | Excluded (owner) | The public site is a separate Cloudflare app, framework unknown, built later; only the backend contract is supplied |
| R023 | Done | API-only guest contract, exact origins, no cookies, JSON-only POST, quotas, optional Turnstile (guest checks) |
| R024 | Done | Membership-free submission with 8 public categories routed to safe queues |
| R025 | Done | Expiring, revocable, hash-stored grants; POST-only single-use code exchange; non-enumerating answers |
| R026 | Done locally / External for real mailbox | MailStdin ingestion, ticket-number threading, new-mail routing filter, unverified-sender handling (journey). Real `support@swoovr.com` mailbox, DNS, spam filtering: External |
| R027 | DEFAULT / External | One support address routed by Zammad email filters into queues; alias set and DNS are external inputs (`OPERATIONS.md`) |
| R028 | Excluded | Zammad's public form not used; Swoovr-branded API-mediated interface only |

## Admin WebUI

| ID | Status | Where / evidence |
| --- | --- | --- |
| R029 | Done | `/tickets`: search, queue/status/priority/owner filters, sort, paging, bulk (journey + runtime) |
| R030 | Done | `/tickets/{id}`: threaded conversation, private notes marked "staff only" |
| R031 | Done | Public reply/private note, `.txt` attachments, close/reopen via status, follow-up per queue policy |
| R032 | Done | Owner, escalation/transfer, priority, category/subcategory, sensitivity, resolution, supported macros, text modules |
| R033 | Done | Member account link, guest/email-sender labels, linked cases/appeals panel |
| R034 | Done | Lookup by GUID, `swoovr-{GUID}`, mapped numeric ID or exact email; shared email → ambiguity (runtime) |
| R035 | Done (DEFAULT) | `/tickets/diagnostics`: reachability, mappings, uncertain work, inbox and dead letters. Group/role/trigger/webhook configuration is provisioned and changed in Zammad's own admin by an administrator (not routine work) |
| R036 | Done / OPEN | Sensitive queues enforce elevation + pinned TLS channel; Admin host presents no certificate, so those queues are invisible in Admin until the owner decides the trusted-workstation mechanism (question opened) |
| R037 | Done | Existing Admin controls and tokens; no environment notices |

## Webhooks and notification delivery

| ID | Status | Where / evidence |
| --- | --- | --- |
| R038–R039 | Done | Real trigger → configured webhook (webhook + events harnesses) |
| R040 | DEFAULT | Payload is deliberately ids-only (`ticket.id`, `updated_at`, `customer_id`, state/group/owner/priority ids, `article.id`); content is re-fetched |
| R041 | Done | Internal notes never notify (events harness) |
| R042 | Done | Raw-body HMAC-SHA1 `X-Hub-Signature` verified against the pinned release |
| R043 | Done | Schema validation, delivery and signed-body dedupe, durable inbox before 204, article-level notice dedupe |
| R044 | Done | Worker re-fetches as the mapped customer |
| R045 | Done (DEFAULT) | Article-free changes (status, queue, owner, priority) complete without notices; members see current state when opening |
| R046 | Done | In-app/push `Support` notices; never emailed for members, so no duplicate with helpdesk mail; guests get one content-free email |
| R047 | Done | Backoff, dead letters, diagnostics, structured warnings, reconciliation sweep. External alerting/metrics backends: External |
| R048 | Done | Event processing writes nothing back to Zammad |
| R049 | DEFAULT | Ingestion stays in the Swoovr API (no new project or Worker); the inbox makes a later edge queue optional |
| R050 | Done | `/api/...` with `Swoovr-Version`; Zammad `/api/v1` only inside the adapter |

## File uploads and recovery verification

| ID | Status | Where / evidence |
| --- | --- | --- |
| R051 | Done (DEFAULT) | `.txt` only, 64 KiB, 3 per reply; binary formats refused until a scanning product is configured |
| R052–R054 | Excluded (deferred) | Secure identity-document upload is a separate workstream; no upload invitation or unfinished flow; sensitive-looking content refused |
| R055 | Done | Ticket numbers are never credentials (guest + runtime checks) |
| R056 | Excluded (deferred) | Reuse of R2 and the verification provider belongs to the deferred upload workstream |

## Operational / infrastructure

| ID | Status | Where / evidence |
| --- | --- | --- |
| R057 | Done | Verified against the actual repository: .NET API, PostgreSQL, Keycloak, Blazor web/Admin, MAUI |
| R058 | Done locally / External | Separate Compose project, private network, loopback-only port; remote hosting is the owner's (External) |
| R059 | Done | Staff OIDC sessions vs per-staff provider tokens vs provisioning/identity tokens |
| R060 | Done | Timeouts, no blind write retry, operation keys, lost-response reconciliation, outage behaviour (events harness), redacted logs |
| R061 | Done / External | Pinned digests, upgrade/rollback procedure and full isolated restore (`OPERATIONS.md`, restore harness); production DR drills: External |
| R062 | OPEN (legal) | Upstream Compose and AGPL-3.0 licence retained; deployment obligations need legal review (no legal conclusion given) |

## Thirteen groups and the stock Users group

The 13 named groups are provisioned idempotently (twice without duplication) with role mappings in `TicketingPolicy`. The stock `Users` group is disabled only when empty; new email is routed to General Support by a provisioned filter. Public guest categories route only to non-restricted queues.

## Original discovery questions

Answered by the implementation rather than by a separate discovery report: identity mapping (R002–R007), actor/token design (`API.md`), visibility and leakage controls (R012, R041), Trust & Safety boundary (R017–R021), guest flow (`GUEST-API.md`), email (R026–R027), Admin capabilities (R029–R037), webhooks (R038–R050), uploads (R051–R056), operations and recovery (`OPERATIONS.md`), licensing (R062, OPEN).
