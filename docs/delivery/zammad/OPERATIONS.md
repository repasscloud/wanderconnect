# Zammad operations guide

Local evidence only. UAT/production hosting, the real support mailbox and DNS are the owner's and have not been exercised.

## Local start

```sh
./scripts/local-platform.sh up --reuse          # Swoovr platform (keeps data)
python3 scripts/zammad_local.py up              # Zammad (127.0.0.1:8095); private network; local Keycloak forwarder
python3 scripts/zammad_local.py provision --events   # groups, attributes, mail filter, staff sign-in, webhook + trigger ACTIVE
python3 scripts/zammad_local.py runtime         # private API settings (.local/zammad/api.env); staff are synchronised by the API
./scripts/local-platform.sh up --reuse          # restart the API with those settings
```

Plain `provision` (without `--events`) leaves the webhook and trigger inactive. `tests/zammad/webhook_acceptance.py` also restores them inactive, so re-run `provision --events` afterwards. Staff need no operator step: the API creates, updates and switches off their Zammad identities from Swoovr roles (see below).

Acceptance (each owns or restores what it touches): `runtime_acceptance.py`, `guest_acceptance.py`, `webhook_acceptance.py`, `events_acceptance.py` (stops and restarts containers), `restore_acceptance.py`, `backup_acceptance.py`, `ui_journey.browser.cjs` and `staff_sso.browser.cjs` (Playwright; set `SWOOVR_PLAYWRIGHT_PATH`, optionally `SWOOVR_BROWSER_CHANNEL=chrome`).

## Configuration (Swoovr API)

Template with placeholders: `containers/zammad/api.env.example`. Names match the implementation.

| Name | Purpose |
| --- | --- |
| `Ticketing:Zammad:BaseUrl` | Zammad base URL (HTTPS, or HTTP only to loopback/the private `swoovr-zammad` host) |
| `Ticketing:Zammad:IdentityToken` | Customer lifecycle token (cannot read tickets on its own) |
| `Ticketing:Zammad:StaffDirectoryToken` | Staff directory credential; enables staff synchronisation (none: staff can't use the helpdesk) |
| `Ticketing:Staff:Worker`, `SyncSeconds`, `VerifyMinutes` | Staff synchronisation on/off (default on), interval (60 s), full verification against Zammad (10 min) |
| `Ticketing:Zammad:WebhookSecret` | HMAC secret shared with the Zammad webhook (≥32 chars); enables the event worker |
| `Ticketing:Events:Worker`, `PollSeconds`, `ReconcileMinutes` | Event worker on/off (default on), poll interval (5 s), missed-event sweep (10 min) |
| `Ticketing:Guest:AllowedOrigins:{n}`, `FrontendUrl`, `Turnstile:Secret` | Guest API (see `GUEST-API.md`) |
| `Ticketing:ConfigurationFile` | Optional JSON file holding the settings above |

## Remote deployment inputs (owner)

- Host Zammad (VPS or similar) with its database, Redis, memcached and Elasticsearch (≥4 GB RAM for Zammad). It must not be publicly reachable except its agent UI if wanted; the Swoovr API needs HTTPS to it, and Zammad needs HTTPS to the API's `/api/integrations/zammad/events`.
- Run provisioning with the Rails runner as locally, with `sso` set to the hosted staff realm's HTTPS issuer and a public PKCE client `swoovr-zammad` whose only redirect is `https://{zammad-host}/auth/openid_connect/callback`. Give the API the settings above; staff are then synchronised automatically.
- The local Keycloak forwarder (`containers/zammad/local-keycloak-forward.rb`, throwaway self-signed certificate) exists only because the local issuer is plain `http://localhost:8180`. It is not needed with a hosted HTTPS issuer.
- Mailbox: connect `support@swoovr.com` as a Zammad email channel (IMAP/SMTP or a provider), set SPF/DKIM/DMARC, choose aliases (billing, appeals, safety, legal, privacy, security, press, partnerships, community) as Zammad email filters into queues. A sender address is never authorisation; Swoovr hides emailed customer messages from in-app views.
- Search: after any restore or upgrade, rebuild the index (`rake zammad:searchindex:rebuild`).
- Attachments (D330): set `NGINX_CLIENT_MAX_BODY_SIZE=220M` on Zammad's nginx (150 MB of files is about 200 MB once base64-encoded), as `containers/zammad/compose.local.yaml` does locally. Consider Zammad's filesystem attachment storage rather than the database for large files.

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

## Staff access (D327, owner decision on Q133)

Swoovr roles decide every staff member's queues; Zammad is never managed as a separate permission system.

| Queue | Support | Trust & Safety | Legal | Admin | Sensitive |
| --- | :-: | :-: | :-: | :-: | --- |
| General Support, Billing & Subscriptions, Account Recovery, Public Relations & Media, Partnerships & Business Development, Marketing & Community | ✅ | ✅ | — | ✅ | |
| Appeals | — | ✅ | — | ✅ | |
| Trust & Safety | — | ✅ | — | ✅ | |
| Trust & Safety — Restricted | hand off | ✅ | — | hand off | Yes |
| Legal & Law Enforcement | hand off | hand off | ✅ | hand off | Yes |
| Privacy & Data Protection | — | — | ✅ | ✅ | Yes |
| Internal Operations | — | — | — | ✅ | |
| Security | — | — | — | ✅ | Yes |

- Trust & Safety also acts as Support, and root admin as Admin. No break-glass: Admin never reads Legal.
- **In Swoovr Admin:** sensitive queues also need the matching elevated access and a sign-in within 15 minutes. Conflict-of-interest exclusions apply to every request.
- **In Zammad directly:** staff sign in only with "Swoovr staff sign-in" (the staff Keycloak realm). There is no password form or reset, and unknown people get no account. A staff member gets the ordinary queues of their roles plus hand-off into sensitive ones, never read. Legal therefore has no web sign-in.
- **When web access is switched off:** while a staff member has any conflict-of-interest record (including a link to their own member account), no role, a suspension, or a Swoovr session revocation, their web identity is switched off and its sessions are ended. Zammad also refuses any request from a switched-off user (403).
- **Timing:** changes apply within a minute, or at once after a staff revocation or a new conflict recorded in Swoovr. Every 10 minutes Swoovr compares Zammad with Swoovr, reverts and audits any difference, and deletes API tokens it didn't issue.
- **In Admin:** Integration health shows how many staff are copied, failures, and active Zammad agents Swoovr doesn't manage, which should be none. **Check staff access now** runs the comparison immediately.
- **Operator break-glass:** the local `swoovr-provisioner` admin, which can use Zammad's admin-only "request a one-time password login". Use it for helpdesk administration only.

## Known limitations and owner decisions

- Emailed customer messages aren't shown to members or guests in Swoovr (D328, owner kept the default).
- Device attestation for sensitive work (Q133 option C) is not built.
- A Zammad session already open when access is removed ends within a minute (the next synchronisation). Zammad's own session lifetime otherwise applies.
- Retention periods and the AGPL position are decided (D333, #207). Not built yet: automated ticket retention, customer retirement on final account deletion, 35-day backups (`HOLD_DAYS`, `TZ=Australia/Adelaide`) and reconciliation after a restore. Options are in `RETENTION-OPTIONS.md` (Q136, #224). Until customer retirement is built, a finally deleted member's Zammad customer is only switched off when Swoovr next touches it.
- The staff queue uses Zammad's asynchronous search index; new tickets appear after indexing (about a second locally). `TK-` lookups don't depend on it. A newly created staff identity also appears in owner choices only after indexing.
