# Guest support API (for the separately built public website)

Status: **implemented and locally accepted** against a disposable Swoovr API, real local Zammad 7.2.2 and real local Mailpit (`tests/zammad/guest_acceptance.py`, 31 checks). The public website does **not** exist yet. CONFIRMED (owner): it is a separate Cloudflare application, framework unknown, built before launch. Nothing here assumes Node.js, SSR, static HTML or a repository location. No external mailbox, DNS or Cloudflare acceptance has been done.

## Model

A guest is someone without a Swoovr membership who proves control of an email address with an emailed one-time code. The code is exchanged once, by `POST`, for a **grant**: an opaque 43-character string that lets the website act for that guest's own requests only, for 2 hours, until revoked.

- Not a member sign-in. A grant never works on member routes, and a member token never works here.
- Ticket numbers (`TK-…`), email addresses and member IDs are **not** credentials.
- A guest using the same address as a member gets a separate helpdesk customer (`swoovr-guest-{id}`, no helpdesk email). Nothing is merged, then or after a later registration.
- Swoovr stores only a keyed hash of the address, an encrypted copy (for future reply notices), the hashed code and the hashed grant. Conversations stay in Zammad.

## Flow

```
website                                Swoovr API
  | POST /api/support/guest/codes  {email}      -> 202 {"status":"sent-if-allowed"}   (always, for any valid address)
  |   ... guest reads the 8-digit code from their email and types it into the website ...
  | POST /api/support/guest/grants {email, code} -> 200 {grant, expiresAt}  | 400 guest-code-invalid
  | keep the grant in memory (or sessionStorage); never in a URL, cookie or log
  | GET/POST /api/support/guest/tickets...   Authorization: SwoovrGuest {grant}
  | DELETE /api/support/guest/grants/current  (sign out)  -> 204
```

The email contains the code and the configured page address only, never a link carrying a token, so mail scanners and link previews cannot consume anything, and nothing sensitive reaches URLs, referrers or server logs. Exchange is `POST`-only (`GET` is 405).

## Endpoints

Base path `/api/support/guest` on the Swoovr API. Requests and responses are JSON (camelCase). The optional `Swoovr-Version` header follows the API's date-based convention; omitted means current.

| Method | Path | Auth | Body | Success |
| --- | --- | --- | --- | --- |
| GET | `/categories` | none | — | `[{id, label}]` |
| POST | `/codes` | none | `{email, botToken?}` | 202 `{status:"sent-if-allowed"}` |
| POST | `/grants` | none | `{email, code}` | 200 `{grant, expiresAt}` |
| DELETE | `/grants/current` | grant | — | 204 |
| GET | `/tickets?page=1` | grant | — | `{items:[TicketSummary], page, pageSize, hasMore}` |
| POST | `/tickets` | grant | `{operationId, category, title, body}` | `TicketSummary` |
| GET | `/tickets/{id}` | grant | — | `TicketDetail` (public messages only; `staff` is null) |
| POST | `/tickets/{id}/replies` | grant | `{operationId, body, attachments?}` | `TicketDetail` |
| GET | `/tickets/{id}/articles/{a}/attachments/{x}` | grant | — | `{filename, contentType, data(base64)}` |

`TicketSummary` = `{id, reference, title, stateId, priorityId, updatedAt}`. `TicketArticle` = `{id, body (plain text), private:false, sender ("Customer"|"Agent"|"System"), createdAt, attachments:[{id, filename, contentType, size}]}`. `canReply` is false once a request is closed and its queue doesn't accept follow-ups. Message bodies are plain text: render them as text, never as HTML.

Categories (DEFAULT; a small routing subset, never the internal queues): `account`, `recovery` (can't sign in), `billing`, `safety`, `privacy`, `media`, `partnerships`, `other`.

## Idempotency

`operationId` is a UUID the website creates **once per draft** and reuses for every retry of that draft (double click, timeout, network error). The same key with the same body returns the original result; the same key with a different body is `409 ticket-operation-conflict`. After `409 ticket-reconciliation-required` or a network failure, resend the **same** body with the **same** key: Swoovr matches it against the helpdesk and never sends it twice. Create a new key only after a confirmed success.

## Errors

`application/problem+json` with `code`:

| Status | code | Meaning |
| --- | --- | --- |
| 400 | `invalid-request` | Malformed address, text or attachment; unknown category |
| 400 | `guest-code-invalid` | Wrong, used, superseded, expired or exhausted code (always the same answer) |
| 400 | `guest-check-failed` | Bot check configured and failed |
| 401 | `guest-grant-required` | Missing, malformed, expired or revoked grant: ask for a new code |
| 403 | (none) | Browser `Origin` not allowed; or a private note attempted |
| 404 | `ticket-unavailable` | Not this guest's request (identical for "doesn't exist") |
| 409 | `ticket-operation-conflict`, `ticket-reconciliation-required`, `ticket-follow-up-unavailable` | See above |
| 413 | — | Body over 300,000 bytes |
| 415 | — | `POST` without `Content-Type: application/json` |
| 429 | `guest-limit-reached` | Too many new requests or replies |
| 503 | `ticket-provider-unavailable`, `guest-support-unavailable` | Try later; show the support email address |

## Origin, CSRF and sessions

- **Allowed origins** are listed exactly in `Ticketing:Guest:AllowedOrigins` (e.g. `https://swoovr.com`). A request carrying any other `Origin` is refused with 403, preflight included. Responses never allow credentials.
- There are no cookies. The grant travels only in `Authorization: SwoovrGuest {grant}`, which a cross-site page cannot attach. `POST` requires `application/json`, so cross-site HTML forms can't reach it either.
- Requests without an `Origin` (for example a Cloudflare Worker calling server-to-server) are allowed through the same rules: every ticket route still needs a valid grant.
- Store the grant in memory or `sessionStorage`, send it only to the Swoovr API origin, and call `DELETE /grants/current` on sign-out.

## Lifetimes and quotas (DEFAULT, reversible)

Codes: 8 digits, 10 minutes, single use, five wrong tries end a code, and a newer code supersedes the older one. Code requests per address: 1 per minute, 3 per hour, 10 per day; per client network address: 10 per hour, 50 per day. Over quota, the API still answers `202 sent-if-allowed` and sends nothing. Grants last 2 hours, with at most 5 live per guest (oldest revoked first). New requests: 5 per guest per day; replies: 30 per guest per hour. Quotas are stored in PostgreSQL, so they hold across API instances. The client network address comes from `X-Forwarded-For` only behind a trusted proxy (`Phone:TrustedProxies`).

## Attachments

Ordinary UTF-8 `.txt` only, up to 64 KiB each and three per reply, as base64 in `attachments: [{filename, contentType:"text/plain", data}]`. Text that looks like identity documents, card numbers, private keys or evidence is refused. Secure identity-document collection is **deferred**: the website must not offer document upload.

## Bot protection

Optional Cloudflare Turnstile on `POST /codes`. When `Ticketing:Guest:Turnstile:Secret` is configured, `botToken` is required and verified with Cloudflare's siteverify; a missing token, a refusal or an unreachable verifier fails closed. When it isn't configured, no check is made and none is claimed. The verifier was tested with a fake HTTP verifier only; no real Cloudflare credentials exist here.

## Configuration (Swoovr API)

| Name | Example (placeholder) | Notes |
| --- | --- | --- |
| `Ticketing:Guest:AllowedOrigins:0` | `https://swoovr.example` | One entry per exact origin |
| `Ticketing:Guest:FrontendUrl` | `https://swoovr.example/support` | Named in the code email; no token added |
| `Ticketing:Guest:Turnstile:Secret` | *(unset)* | Optional; enables bot checks |
| `Ticketing:Zammad:BaseUrl`, `Ticketing:Zammad:IdentityToken` | see `zammad/README` | Required for any support |
| `ContactHistory:LookupKey`, `ContactHistory:Keys:{n}` | *(secret)* | Keyed address hashing and encryption (derived from the founder secret when unset) |
| `Email:*` | *(provider)* | Code delivery; locally Mailpit only |

## Example (placeholders)

```http
POST /api/support/guest/codes
Origin: https://swoovr.example
Content-Type: application/json

{"email":"someone@example.test"}

HTTP/1.1 202 Accepted
Access-Control-Allow-Origin: https://swoovr.example

{"status":"sent-if-allowed"}
```

```http
POST /api/support/guest/tickets
Origin: https://swoovr.example
Authorization: SwoovrGuest <grant>
Content-Type: application/json

{"operationId":"<uuid>","category":"recovery","title":"I can't sign in","body":"My reset email doesn't arrive."}
```

## Local evidence versus external acceptance

Local: real Mailpit delivery and code extraction, real Zammad customers/tickets/articles, disposable PostgreSQL. Not done: a real mailbox provider, SPF/DKIM/DMARC and DNS, Cloudflare hosting, Turnstile with real keys, or any public-website integration. Reply notices to guests by email are handled by the event processing work (see `IMPLEMENTATION.md`).
