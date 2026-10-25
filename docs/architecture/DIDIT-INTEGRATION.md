# Didit identity verification — integration notes

Status: reference notes, 25 October 2026. Decisions: D126–D129 and D147 in [DECISIONS.md](../delivery/DECISIONS.md); answers Q25–Q28 and Q44 in [QUESTIONS.md](../delivery/QUESTIONS.md).

The notes below were supplied by the owner from the Didit Business Console. They have not yet been checked against Didit's documentation; slice P checks them against <https://docs.didit.me/llms.txt> and the linked pages before building, and records any difference here.

## Swoovr settings

- Test mode until launch. Values live only in `.local/docker.env` (never committed, never in chat):
  - `SWOOVR_DIDIT_API_KEY` — the API key (the console's notes call it `DIDIT_API_KEY`).
  - `SWOOVR_DIDIT_WORKFLOW_ID` — the KYC workflow (ID document matched against the face, with liveness).
  - `SWOOVR_DIDIT_WEBHOOK_SECRET` — the webhook destination's shared secret (the console's notes call it `DIDIT_WEBHOOK_SECRET`). Not created yet; see Q50.
- The API creates the session with the member's internal account id as `vendor_data`, stores the session id against that account and gives the member the hosted link. Swoovr keeps only the outcome and operational metadata (D128), never document images, numbers, selfies or biometric data.
- The webhook reaches the API directly; it is not on the member BFF allow-list. Audit entries carry ids only.

## Webhook notes from the Didit console

Add a Didit webhook endpoint to my application to receive real-time identity verification, entity, and transaction events.

### What to build

A POST endpoint (Swoovr: `POST /api/v1/webhooks/didit` on the API, not the BFF) that:

1. Reads the raw request body (do NOT parse JSON before signature verification)
2. Verifies the HMAC-SHA256 signature (X-Signature-V2 recommended)
3. Validates the SIGNED body `timestamp` field is fresh (within 5 minutes) -- never the
   unsigned X-Timestamp header, which is excluded from the signature and can be freshened
   on a replayed request without invalidating it
4. Parses the JSON body and processes the event by webhook_type
5. Returns 2xx ASAP and updates state idempotently (dedupe on event_id, atomic + durable)

### Signature Headers

- X-Signature-V2: HMAC-SHA256 over the sorted, Unicode-preserved canonical JSON (recommended).
- X-Signature: HMAC-SHA256 over the exact raw bytes (works only if your stack does not re-encode the body).
- X-Signature-Simple: HMAC-SHA256 over "{timestamp}:{session_id}:{status}:{webhook_type}" (fallback; does not authenticate the decision body).
- X-Timestamp: Unix epoch seconds when Didit dispatched the webhook. NOT covered by any
  signature -- do not use it for freshness. Use the body's own `timestamp` field instead,
  which is part of the signed payload.

Verification:

1. Recompute the signature using your destination secret and compare with constant-time comparison
2. Only after the signature is valid, check abs(now - body.timestamp) <= 300 seconds
3. Dedupe on event_id using an atomic, durable store before applying the event

### Webhook Event Types (use exact values in destination.subscribed_events)

- "status.updated": session or business session status changed
- "data.updated": session or business-session verification data was edited
- "user.status.updated": User entity status changed
- "user.data.updated": User entity profile or aggregate data changed
- "business.status.updated": Business entity status changed
- "business.data.updated": Business entity profile or aggregate data changed
- "activity.created": activity timeline record created
- "transaction.created": transaction created and initial verdict ready
- "transaction.status.updated": transaction status changed

There is no wildcard; include every event family you want to receive. You can create multiple destinations to fan out to different URLs.

### Webhook Envelope (session events)

```jsonc
{
"event_id": "uuid",
"session_id": "uuid",
"status": "Approved" | "Declined" | "In Review" | "In Progress" | "Not Started" | "Abandoned" | "Expired" | "KYC Expired" | "Resubmitted",
"webhook_type": "status.updated",
"created_at": 1627680000,
"timestamp": 1627680000,
"application_id": "uuid",
"workflow_id": "uuid",
"workflow_version": 1,
"vendor_data": "your-internal-user-id",
"metadata": { "user_type": "premium" },
"decision": { ... } // only when Approved / Declined / In Review / Abandoned
}
```

KYB / business-session payloads additionally include "business_session_id" and "session_kind": "business".

### Decision Object (V3)

The decision mirrors the GET /v3/session/decision/ response. Per-feature data is in plural arrays so a workflow can include multiple instances of the same feature:

- id_verifications[], nfc_verifications[], liveness_checks[], face_matches[]
- aml_screenings[], poa_verifications[], phone_verifications[], email_verifications[]
- ip_analyses[], database_validations[], questionnaire_responses
- reviews[]

Each item carries a node_id so you can correlate it with the workflow graph.

### Database Update Logic

```text
switch (status):
"Approved" -> user.verified = true; store decision data
"Declined" -> user.verification_status = "declined"; log decision.*.warnings
"In Review" -> user.verification_status = "pending_review"
"In Progress" -> user.verification_status = "in_progress"
"Resubmitted" -> reopen the listed feature nodes per resubmit_info
"Abandoned" -> optionally trigger reminder
"Expired" / "KYC Expired" -> mark session expired; optionally create a new one
```

### Retry Policy

On 5xx or 404 Didit retries up to 2 times:

- 1st retry: ~1 minute after the initial failure
- 2nd retry: ~4 minutes after the 1st retry
  After that the delivery is dropped. Each delivery attempt is a new WebhookLog row.

### Error Handling

- Return 2xx as fast as possible; do heavy work asynchronously.
- Log the raw body for debugging signature failures.
- Treat duplicate webhooks idempotently using an atomic, durable store keyed on event_id (or session_id + status + webhook_type as a fallback).
- HTTPS endpoints only. Webhook receivers behind Cloudflare must whitelist 18.203.201.92.

### Environment variable

`SWOOVR_DIDIT_WEBHOOK_SECRET` in `.local/docker.env` -- from the Business Console (per-destination secret_shared_key).

### Testing

Send test webhooks from the Business Console (API & Webhooks > Try Webhook) before going live. The console covers approved, declined, in-review, KYB, entity, activity, and transaction scenarios.

### Docs

- Webhooks guide: https://docs.didit.me/integration/webhooks
- Webhook destinations API: https://docs.didit.me/management-api/webhook/list-destinations
- Verification statuses: https://docs.didit.me/integration/verification-statuses
- Decision schema: https://docs.didit.me/sessions-api/retrieve-session

### Important

- ALWAYS verify the signature before processing any webhook
- Use constant-time comparison to prevent timing attacks
- Store and HMAC the raw JSON string -- never re-stringify after parsing
- Return 2xx quickly; do heavy processing asynchronously if needed
