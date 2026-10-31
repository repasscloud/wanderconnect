# Didit identity verification — integration notes

Status: reference notes, 25 October 2026. Decisions: D126–D129 and D147 in [DECISIONS.md](../delivery/DECISIONS.md); answers Q25–Q28 and Q44 in [QUESTIONS.md](../delivery/QUESTIONS.md).

The notes below were supplied by the owner from the Didit Business Console. They have not yet been checked against Didit's documentation; slice P checks them against <https://docs.didit.me/llms.txt> and the linked pages before building, and records any difference here.

## Swoovr settings

- Test mode until launch. Values live only in `.local/docker.env` (never committed, never in chat):
  - `SWOOVR_DIDIT_API_KEY` — the API key (the console's notes call it `DIDIT_API_KEY`).
  - `SWOOVR_DIDIT_WORKFLOW_ID` — the KYC workflow (ID document matched against the face, with liveness).
  - `SWOOVR_DIDIT_WEBHOOK_SECRET` — the webhook destination's shared secret (the console's notes call it `DIDIT_WEBHOOK_SECRET`). Set; the test destination uses webhook version V3.0, `status.updated` and `data.updated`, through a Cloudflare quick tunnel (D153).
- The API creates the session with the member's internal account id as `vendor_data`, stores the session id against that account and gives the member the hosted link. Swoovr keeps only the outcome and operational metadata (D128), never document images, numbers, selfies or biometric data.
- The webhook reaches the API directly; it is not on the member BFF allow-list. Audit entries carry ids only.

## Webhook notes from the Didit console

Add a Didit webhook endpoint to my application to receive real-time identity verification, entity, and transaction events.

### What to build

A POST endpoint (Swoovr: `POST /api/webhooks/didit` on the API, not the BFF) that:

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

## Checked against Didit's documentation — 25 October 2026

Slice P read <https://docs.didit.me/llms.txt>, the [webhooks guide](https://docs.didit.me/integration/webhooks), [create session](https://docs.didit.me/sessions-api/create-session) and [retrieve session](https://docs.didit.me/sessions-api/retrieve-session) on 25 October 2026. Differences from the console notes above, and what Swoovr does:

- **Freshness.** The docs' sample checks the `X-Timestamp` header; the console notes (and D152) say never to trust it because no signature covers it. Swoovr follows D152: only the body's signed `timestamp` is checked (±5 minutes), after the signature. Didit re-signs retries with a fresh timestamp, so a retry still passes.
- **Statuses.** The docs spell `Kyc Expired` (the notes say `KYC Expired`) and add `Awaiting User`. Swoovr maps statuses case-insensitively: both spellings mean Expired; Awaiting User and Resubmitted mean In Progress.
- **Signatures.** X-Signature-V2 is the hex HMAC-SHA256 of canonical JSON (keys sorted recursively, compact separators, Unicode unescaped, whole floats as integers); X-Signature covers the raw bytes; X-Signature-Simple is deprecated. Swoovr accepts V2 or the raw signature and refuses Simple-only deliveries. `Swoovr.Checks` compares the C# canonical form against Didit's documented Python rules. During a secret rotation Didit also sends `-Previous` headers for 24 hours; Swoovr checks one secret only, so deliveries signed with a replaced secret are refused until `SWOOVR_DIDIT_WEBHOOK_SECRET` is updated (rotation support is a production follow-up).
- **Retries.** Didit retries 5xx, 404, timeouts and connection failures twice (about 1 and 5 minutes later) with a 5-second timeout, and won't call private or localhost URLs (hence the tunnel). Swoovr answers 2xx for anything signed and fresh, including unknown sessions, so only real failures are retried.
- **Sessions.** `POST https://verification.didit.me/v3/session/` with the `x-api-key` header; the response's hosted link is `url`. The return address gets `?verificationSessionId=…&status=…` appended; Swoovr ignores both and re-reads `GET /v3/session/{id}/decision/` (the key needs `read:sessions`). The docs describe a `sandbox_scenario` field for sandbox applications; Swoovr doesn't use it. Test mode is the owner's console setting (D147).
- **Decision data.** `id_verifications[]` carries `status`, `age`, `date_of_birth`, `document_type` and more. Swoovr reads only the approved items' `age` (or, failing that, the date of birth, used and dropped) to record adult yes/no (D155, Q53).

## As built — slice P, 25 October 2026

- Routes: `GET/POST /api/me/verification` (members, BFF allow-listed), `POST /api/webhooks/didit` (anonymous, signature-checked, not on the BFF), and admin `GET /api/admin/accounts/{id}/verification`, `POST …/verification/allow-again`, `POST …/verification/revoke`, `PUT …/age-assurance`.
- Code: `Swoovr.Domain/Verification/IdVerification.cs` (rules), `Swoovr.Infrastructure/Verification/DiditClient.cs` and `DiditSignature.cs` (adapter and webhook check), `Swoovr.Api/Verification/VerificationEndpoints.cs`, web page `/me/verification` (and `/me/verification/return`).
- Local settings: the API reads `Didit:ApiKey`, `Didit:WorkflowId` and `Didit:WebhookSecret` (from `SWOOVR_DIDIT_*` in `.local/docker.env`; `compose.yaml` passes them to the api container). Without them ID verification answers 503 and every webhook is refused.
- Live evidence (25 October 2026, test mode): the suite created real Didit sessions; within seconds Didit's own `status.updated` delivery arrived through the quick tunnel and passed the V2 signature and timestamp checks (recorded as applied). No verification was completed at Didit: that needs a person with an ID (see the walkthrough in [LOCAL-DEVELOPMENT.md](../delivery/LOCAL-DEVELOPMENT.md#didit-test-verification-walkthrough)).

## Account deletion and the named tunnel — 27 October 2026

- Final deletion asks Didit to delete each of the member's sessions: `DELETE https://verification.didit.me/v3/session/{session_id}/delete/` with `x-api-key` (the key needs `delete:sessions`), answering 200 (older versions 204); 404 counts as already gone ([docs](https://docs.didit.me/sessions-api/delete-session), read 27 October 2026). A failure keeps the attempt row (session id and outcome only) and `accounts purge` retries it (D173, D180).
- A named Cloudflare tunnel service now runs on the owner's Mac instead of the quick tunnel. Its public host isn't readable locally (the service runs from a token), so the Didit console's webhook destination is unchanged (D177, Q69 / issue #75); verification keeps working through the re-read when the member returns (D152). Once the owner names the host, the destination is `https://<host>/api/webhooks/didit`, routed to 127.0.0.1:5080.

## Stable API path migration — 29 October 2026

CONFIRMED D211: the receiver is now `POST /api/webhooks/didit`, with the pinned missing-header policy in [API versioning](API-VERSIONING.md). This repository change does not update the Didit console or the owner's tunnel. Before switching hosted/local traffic permanently, the operator must configure the destination for the stable path and verify a real signed delivery. No console change or live webhook acceptance is claimed by this migration; the return re-read remains available. Historical owner decisions and validation records retain their original URLs.
