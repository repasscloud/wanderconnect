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

`ITicketingProvider` provides canonical-login lookup/customer creation/email update, paged ticket search, detail/create/change and article read/reply. It uses an explicitly supplied actor credential; it is **not yet composed into client-facing API endpoints**. HTTP responses are bounded to 4 MiB, requests to 20 seconds, and writes are never automatically retried. Missing response schema or cancellation after sending a write is uncertain and must be reconciled before another write. Composition must use a redirect-disabled handler, current server-derived actor identity and per-operation permissions.

Live evidence confirms customer isolation, private note/attachment denial, public replies, group transfer with immediate source-agent access loss and identity-token inability to read ticket content. Advanced configuration mutation, macro execution and every client operation remain subject to their explicit implementation/acceptance gates.

## Pending client contracts

Member create/list/detail/reply, native staff operations and guest verification/follow-up are not implemented. No public support website exists or is claimed. The full secured guest contract must be supplied with origin allowlisting, abuse controls, non-enumeration, expiring/revocable grants, replay-resistant exchange and Mailpit acceptance before the independent Cloudflare website can integrate.

Sensitive identity-document collection is deferred; no upload invitation or unfinished upload flow is introduced. Existing reports keep their `SW-...` references. New helpdesk reference policy and case-link orchestration remain pending; ticket closure must never change a Swoovr enforcement/appeal outcome.
