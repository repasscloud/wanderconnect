# Zammad operations guide

Local evidence only. UAT/production hosting, the real support mailbox and DNS are the owner's and have not been exercised.

## Local start

```sh
./scripts/local-platform.sh up --reuse          # Swoovr platform (keeps data)
python3 scripts/zammad_local.py up              # Zammad (127.0.0.1:8095); reconnects the private network
python3 scripts/zammad_local.py provision --events   # groups, attributes, mail filter, webhook + trigger ACTIVE
python3 scripts/zammad_local.py runtime         # per-staff tokens + private API settings (.local/zammad/api.env)
./scripts/local-platform.sh up --reuse          # restart the API with those settings
```

Plain `provision` (without `--events`) leaves the webhook and trigger inactive. `tests/zammad/webhook_acceptance.py` also restores them inactive, so re-run `provision --events` afterwards. Re-run `runtime` whenever staff accounts or roles change; it creates or deactivates per-staff Zammad agents to match current Swoovr roles.

Acceptance (each owns or restores what it touches): `runtime_acceptance.py`, `guest_acceptance.py`, `webhook_acceptance.py`, `events_acceptance.py` (stops and restarts containers), `restore_acceptance.py`, `backup_acceptance.py`, and `ui_journey.browser.cjs` (Playwright; set `SWOOVR_PLAYWRIGHT_PATH`, optionally `SWOOVR_BROWSER_CHANNEL=chrome`).

## Configuration (Swoovr API)

Template with placeholders: `containers/zammad/api.env.example`. Names match the implementation.

| Name | Purpose |
| --- | --- |
| `Ticketing:Zammad:BaseUrl` | Zammad base URL (HTTPS, or HTTP only to loopback/the private `swoovr-zammad` host) |
| `Ticketing:Zammad:IdentityToken` | Customer lifecycle token (cannot read tickets on its own) |
| `Ticketing:Zammad:Staff:{staffAccountGuid}:Token` | One agent token per Swoovr staff account |
| `Ticketing:Zammad:Staff:{staffAccountGuid}:ClientCertificatePins` | SHA-256 pins for the trusted channel required by sensitive queues |
| `Ticketing:Zammad:WebhookSecret` | HMAC secret shared with the Zammad webhook (≥32 chars); enables the event worker |
| `Ticketing:Events:Worker`, `PollSeconds`, `ReconcileMinutes` | Event worker on/off (default on), poll interval (5 s), missed-event sweep (10 min) |
| `Ticketing:Guest:AllowedOrigins:{n}`, `FrontendUrl`, `Turnstile:Secret` | Guest API (see `GUEST-API.md`) |
| `Ticketing:ConfigurationFile` | Optional JSON file holding the settings above |

## Remote deployment inputs (owner)

- Host Zammad (VPS or similar) with its database, Redis, memcached and Elasticsearch (≥4 GB RAM for Zammad). It must not be publicly reachable except its agent UI if wanted; the Swoovr API needs HTTPS to it, and Zammad needs HTTPS to the API's `/api/integrations/zammad/events`.
- Run provisioning with the Rails runner as locally, then create per-staff agents (`runtime` logic) and give the API the settings above.
- Mailbox: connect `support@swoovr.com` as a Zammad email channel (IMAP/SMTP or a provider), set SPF/DKIM/DMARC, choose aliases (billing, appeals, safety, legal, privacy, security, press, partnerships, community) as Zammad email filters into queues. A sender address is never authorisation; Swoovr hides emailed customer messages from in-app views.
- Search: after any restore or upgrade, rebuild the index (`rake zammad:searchindex:rebuild`).

## Backup and restore

- The pinned dataset stores attachments in PostgreSQL (`storage_provider=DB`), so a database dump includes files. Upstream's `zammad-backup` service produces the same database dump plus a storage archive.
- `restore_acceptance.py` proves full recovery into an isolated clone: identical counts (including file content), restored credentials work, tickets and articles read, attachment bytes identical, no network route out.
- Never restore over a live dataset. Restore into a new stack, keep its webhook/mail disconnected (isolated network) until verified, rebuild the search index, then switch traffic.
- Swoovr-side state (`ticketing_*` tables) is in the Swoovr database and its backups. After restoring either side, the reconciliation sweep re-reads recent tickets; uncertain operations are matched by their operation markers, never replayed.

## Upgrades and rollback

1. Images are pinned by digest in `containers/zammad/compose.local.yaml`. Change the digest only after reading the release notes.
2. Back up (database dump), restore into an isolated clone, upgrade the clone, run the acceptance harnesses against it.
3. Upgrade production in a maintenance window; Swoovr keeps accepting signed events and retries re-fetches with backoff while Zammad is down.
4. Rollback: restore the pre-upgrade dump into the previous digest (Zammad migrations are not reversible in place).

## Known limitations and owner decisions

- Sensitive queues (Legal, Restricted T&S, Privacy, Security) need a pinned TLS client certificate per staff account. The Admin host presents none, so those queues are invisible in Admin until a trusted-workstation mechanism is decided (owner question).
- Emailed customer messages aren't shown to members or guests in Swoovr (DEFAULT, conservative; owner question).
- Retention periods for helpdesk data, jurisdiction-specific obligations and AGPL deployment obligations need legal review.
- The staff queue uses Zammad's asynchronous search index; new tickets appear after indexing (about a second locally). `TK-` lookups don't depend on it.
