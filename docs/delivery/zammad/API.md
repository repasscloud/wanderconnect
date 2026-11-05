# Zammad API boundary and implementation status

CONFIRMED: stable Swoovr `/api/...` paths with `Swoovr-Version: 2026-10-28`. Provider paths under Zammad `/api/v1` belong only to the external adapter. Clients never receive provider credentials. Zammad owns conversations/assignment/queues; Swoovr owns identities/evidence/enforcement/outcomes.

## Implemented webhook intake

`POST /api/integrations/zammad/events` is a server-to-server endpoint, not a member/guest session endpoint. It requires configured `Ticketing:Zammad:WebhookSecret`, application persistence, `X-Hub-Signature: sha1=<40 hex chars>` over exact bytes and `X-Zammad-Delivery: <UUID>`. Both headers must be single-valued. No permissive CORS or cookie credential flow is added.

Provisioned payload (no body, filename, address or evidence):

```json
{"ticket":{"id":"42","updated_at":"2026-11-04T00:00:00Z"},"article":{"id":"123"}}
```

Article may be null/absent, or its ID empty for article-free changes. Ticket ID must be positive and its signed update timestamp must parse. Payload maximum: 8192 bytes, including streamed/chunked requests. Zammad 7.2.2's durable ActiveJob job ID is the delivery ID; captured signed bodies are also uniquely deduplicated regardless of unsigned delivery-header changes. Including the authoritative ticket update clock prevents ordinary later article-free changes from having identical payloads.

Responses: 204 only after ids-only inbox persistence or confirmed replay; 400 invalid metadata/schema; 401 invalid signature; 409 reused delivery ID with changed signed body; 413 oversized payload; 503 unconfigured secret/persistence. Database errors do not acknowledge work. Bodies/error content are never logged by this handler. Inbox completion/reconciliation/notification dispatch remains **pending**, so the provisioned trigger and webhook remain inactive after normal provisioning. The real acceptance harness temporarily activates them against a disposable database, then restores their inactive configuration.

Only IDs, a digest, receipt/retry clocks, attempt count and bounded error code are stored in Swoovr. A future worker must re-fetch provider data and recheck visibility/customer/account status before emitting notifications. Intake is not evidence that notifications or customer workflows are delivered.

## Provider adapter

`ITicketingProvider`/`ITicketingWorkspaceProvider` (Domain) are implemented by `ZammadTicketingProvider`/`ZammadWorkspaceProvider` (Infrastructure). HTTP responses are bounded to 4 MiB, requests to 20 seconds, redirects are disabled and writes are never automatically retried. A missing response schema, or cancellation after a write was sent, is **uncertain** and must be reconciled before another write. Indexed-search absence is never proof that a write did not happen.

## Actor credentials (DEFAULT, verified against pinned 7.2.2)

- **No application-wide agent token.** Swoovr is the only source of staff permissions (owner, D327, Q133). The API's staff synchronisation (`TicketingStaffSync`, every minute; immediately after a staff revocation or a new conflict record; verified against Zammad every 10 minutes) gives each Swoovr staff member two Zammad identities from `TicketingPolicy`:
  - `swoovr-staff-{accountGuid}`, the **API identity** Swoovr Admin acts as. Role `Agent` only, no email or password, so it can't sign in. It holds every queue the roles allow. Its token is issued to it by the synchronisation (acting on its behalf) and stored encrypted in `ticketing_staff`; any other token on it is deleted.
  - `swoovr-staff-web-{accountGuid}`, the **web sign-in** for Zammad's own interface, through the staff Keycloak realm only. It holds only what Zammad can enforce by itself: ordinary queues, and hand-off (`create`) into sensitive ones. It is switched off, with its sessions ended, while there is any conflict-of-interest record, no role, a suspension or deletion, or a Swoovr session revocation. It never holds a token.
  - The synchronisation uses `Ticketing:Zammad:StaffDirectoryToken`, whose Zammad role has `admin.user` and `admin.session`, plus `ticket.agent` only to read the group and role lists. It belongs to no group, so it sees no tickets. It never writes a role other than `Agent`, and changes made in Zammad are reverted and audited.
- **Members** act through `Ticketing:Zammad:IdentityToken` plus the Zammad `From` header. The numeric target comes **only** from the durable `TicketingIdentity` mapping for the signed-in account; before every request the effective provider user must resolve to login `swoovr-{guid}`, be active, hold only `Customer` and no groups. A client-supplied `From` header or provider ID is ignored (tested).
- **Every staff read/write/download** checks: a staff-realm session with a Support/Admin/TrustSafety/Legal role; the live provider actor (Agent only, canonical login); live provider group grant for the action (`read`/`change`/`create`/`full`); the Swoovr role's group policy; staff conflict-of-interest exclusions against the customer and linked case subjects; and admin-queue case links. Unknown groups fail closed.
- **Queue matrix** (D327): see `TicketingPolicy` and the table in `OPERATIONS.md`. Appeals is Trust & Safety and Admin only; Support, Trust & Safety and Admin can hand off into Legal; Support and Admin can hand off into Restricted. Hand-off moves a request in, and the sender can't see it afterwards.
- **Sensitive groups** (`Legal & Law Enforcement`, `Trust & Safety — Restricted`, `Privacy & Data Protection`, `Security`) additionally require a sign-in within 15 minutes and an active elevated capability (`ticketing-legal`, `ticketing-restricted`, `ticketing-privacy`, `ticketing-security`). No client certificate (Q133 option B). They are never granted to a web sign-in, so Swoovr Admin is the only way in.
- `POST /api/admin/tickets/staff-sync` (Admin) runs and verifies the synchronisation now. `GET /diagnostics` includes `staff`: configured, mirrored, web sign-ins, failures, last applied/verified, and active Zammad agents Swoovr doesn't manage.
- Detail and download re-check access after the provider fetch, so a transfer or revocation between calls cannot release content. Group transfer removes access immediately (tested).

## Implemented runtime API

All routes require a Swoovr bearer session, return `Cache-Control: no-store`, follow the `Swoovr-Version` header, and accept at most 300,000 request bytes. Errors are `application/problem+json` with a `code`: `ticket-unavailable` (403/404, deliberately non-specific), `conflict-of-interest`, `ticket-changed` (stale `ExpectedUpdatedAt`), `ticket-operation-conflict` (operation ID reused with a different payload), `ticket-reconciliation-required` (uncertain earlier write; not replayed), `ticket-follow-up-unavailable`, `ticket-customer-ambiguous`, `ticket-provider-unavailable`, `invalid-request`.

Member (`/api/me/tickets`; members-realm; existing support exemptions for age, phone and staff locks/sanctions, matching notices/appeals — accounts whose status is not `Active` stay blocked as on every member route):

| Method | Path | Contract |
| --- | --- | --- |
| GET | `/api/me/tickets?page=&pageSize=` | `TicketPage` (own tickets only; unmapped accounts get an empty page, no provider call) |
| GET | `/api/me/tickets/options` | `TicketMemberOptions` |
| POST | `/api/me/tickets` | `TicketCreate` -> `TicketSummary`; creates the canonical customer on first use |
| GET | `/api/me/tickets/{id}` | `TicketDetail`; private notes and their attachments are never included |
| POST | `/api/me/tickets/{id}/replies` | `TicketReply` (public only) -> `TicketDetail` |
| GET | `/api/me/tickets/{id}/articles/{a}/attachments/{x}` | `TicketDownload` |

Staff (`/api/admin/tickets`): list with `query`, `group`, `state`, `priority`, `owner`, `sort`, `descending`, `page`, `pageSize` (`TK-{id}` searches exact reference); `GET /capabilities`, `/diagnostics` (Admin only), `/text-modules`, `/macros`; `POST /customers` (lookup by account GUID, canonical login, mapped numeric ID or exact email; shared email returns `409 ticket-customer-ambiguous`, never a guess); `POST ""` (create on behalf); `GET/PUT /{id}`; `POST /{id}/replies` (public or private); `POST /{id}/macros/{macro}`; attachment download; `POST /bulk` (1–20 distinct tickets, per-item status, no blind retry).

Writes carry a client `OperationId` bound to a SHA-256 digest of the payload. Replays of the same key and payload return the applied result; a lost provider response is reconciled by finding the exact operation marker (stored in the article `message_id`) authored by the same actor, never by resending.

Ticket references are `TK-{zammadTicketId}`; report references keep their `SW-...` form, so the two never collide and no report is renumbered.

Content: provider HTML is projected to plain text (scripts/styles/frames removed, tags stripped, entities decoded, control characters removed). No remote content is fetched or rendered. Filenames are reduced to a safe ASCII basename.

Attachments (DEFAULT): ordinary UTF-8 `.txt`, at most 64 KiB each and three per reply. Binary formats need a configured content-scanning product, so they are refused rather than passed unscanned. Text that looks like identity documents, card numbers, private keys or sensitive evidence is refused. Secure identity-document collection is **deferred** and not offered.

## Capability matrix

| Operation | Status | Evidence |
| --- | --- | --- |
| Member create/list/detail/reply, concurrency, isolation | Implemented | `tests/zammad/runtime_acceptance.py` (real Keycloak, Zammad, PostgreSQL) |
| Private notes/attachments hidden from members | Implemented | same |
| Paging, search, filters, sort, `TK-` reference | Implemented | same |
| State, priority, owner, group transfer/escalation | Implemented | same; owners limited to agents with `full` on the destination group |
| Category/subcategory/sensitivity/resolution | Implemented as Zammad object attributes (`swoovr_*`) | same |
| Customer lookup, create on behalf | Implemented | same |
| Bulk changes | Implemented (≤20, per-item results) | same |
| Macros | Implemented for state/priority/owner/group actions only; other actions are listed with `Supported=false` and refused | same (verified macro closes real ticket) |
| Text modules | Read and inserted as plain text | same |
| Diagnostics (mappings, uncertain work, inbox) | Implemented, Admin only | same |
| Trigger, webhook, group, role and object-attribute configuration | Not routine agent work: managed by `scripts/zammad_local.py provision` locally; remote changes use the Zammad admin UI by an administrator | provisioning ran twice without duplication |

## Client and guest surfaces

Native member Web/MAUI and Admin screens use the routes above (see `IMPLEMENTATION.md`, Task 2). Guest support for the separately built public website is specified in `GUEST-API.md`. Webhook inbox processing, notifications and appeals/case links are delivered by later work (see `IMPLEMENTATION.md`). No public support website exists or is claimed.

Sensitive identity-document collection is deferred; no upload invitation or unfinished upload flow is introduced. Ticket closure must never change a Swoovr enforcement/appeal outcome.
