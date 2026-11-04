# Swoovr Zammad implementation handoff

Copy the prompt below into a new Codex chat opened on this repository. It authorises implementation in that new chat; this document itself does not deploy or change application behavior.

---

Implement the Swoovr–Zammad integration in `/Users/danijeljw/Developer/wanderconnect-brief`. Continue through implementation, local integration testing, fixes, documentation and a reviewable PR. Do not stop after discovery, a design proposal, scaffolding, a first vertical slice or mocked tests. Do not merge or deploy to UAT/production.

## Authority and owner corrections

Read `AGENTS.md`, `README.md`, `docs/architecture/README.md`, current product/security specifications and delivery status first. Read `swoovr-zammad-integration-review-prompt.md` as the detailed requirements catalogue. Its original discovery-only/no-implementation instructions are superseded by this implementation request. The corrections and scope below take precedence over conflicting assumptions in that file.

CONFIRMED by the owner in this conversation:

- No Zammad instance exists. Install and provision a real local instance with its supporting services; reproduce any historical prototype claims yourself.
- Native Swoovr member web, Admin web and MAUI screens communicate through Swoovr API. Only Swoovr API communicates with Zammad for client operations. Zammad calls a secured API webhook endpoint for events.
- Zammad is the authoritative ticket/conversation/assignment/queue engine. Do not build a second ticket engine or persist a mirror of conversations in Swoovr.
- Swoovr retains authoritative member identity, reports, T&S cases, preserved evidence, legal holds, enforcement actions and appeal outcomes.
- `https://swoovr.com/support` DOES NOT EXIST. The public website is a separately deployed Cloudflare application. Its framework is unknown; do not assume Node.js, SSR, static HTML or a repository location.
- That public website and its support UI will be built independently later, before launch. Do not build or deploy that frontend in this task. Supply the documented, secured guest API contract and backend capabilities it will need, with local integration tests.
- Local Zammad and Swoovr Compose stacks must communicate on this host. Published ports can change where necessary. Prefer a shared private Docker network with unique service names and distinct host ports; accommodate the existing Swoovr shared network namespace rather than blindly combining Compose files.
- In UAT/production the owner will separately deploy Zammad, potentially on a VPS or another suitable hosting platform. Swoovr connects through environment configuration. Do not select/provision paid infrastructure or assume Azure App Service can run the entire Zammad stack unchanged.
- No development/test/UAT notices in application UI. Use fictional adult data for tests and evidence.

Implement ordinary support, native staff operations, guest backend access, and connections to existing appeals/T&S workflows. The separate secure identity-document upload product is deferred: document its future boundary, prevent sensitive evidence from being copied into Zammad, and do not add an unfinished upload flow. Do not claim that deferred capability exists.

Routine engineering choices are delegated to you. Mark them DEFAULT; keep owner-confirmed requirements CONFIRMED and unresolved policy decisions OPEN. Do not repeatedly request approval for local reversible work already authorised here. Ask only for material unresolved product/security/legal decisions, credentials or destructive actions; follow the repository question/issue workflow and continue independent work. Unknown permission grants must fail closed.

## Working method and scope tracking

1. Inspect the actual checkout and preserve unrelated changes. Use a descriptive `codex/` branch and isolation where needed. Load applicable skills, including Postman's API engineering entry point and relevant .NET references. Do not enable GitHub Actions.
2. Create a concrete implementation plan and requirements coverage table in the existing documentation structure. Map each original prompt requirement to an implementation task, an explicit exclusion here, or an OPEN external dependency. Use this handoff as the authorised direction; continue directly into implementation without another generic design-approval pause.
3. Maintain a durable checklist and evidence log. At context limits, leave precise continuation instructions identifying completed work, failing tests, next steps and remaining requirements; resume rather than restarting discovery.
4. Use existing projects and dependency boundaries. No new application/worker project by habit. Do not use arbitrary fixed function counts or earlier rough estimates as requirements.
5. Verify the chosen pinned Zammad release using official documentation and the running local instance. Specifically test operation permissions, article visibility, signature format, pagination/search, attachment handling, macros and configuration APIs. Never invent endpoints or rely on undocumented browser endpoints without explicitly justifying compatibility and security.

## Architecture and likely code ownership

Confirm exact filenames during inspection; the following are placement guidance, not claims that files already exist:

| Area | Ownership |
| --- | --- |
| Provider interface and ticketing policies | Existing Domain project, no Zammad transport DTOs in Domain |
| Zammad typed HTTP adapter, configuration and persistence mappings | Existing Infrastructure project |
| Member, guest, staff and webhook endpoints; orchestration and background recovery | Existing API project, focused Ticketing feature files |
| Public DTOs and typed transport | Contracts and Client projects |
| Member UI and BFF allow-list | Web/Web.Client, existing BFF security conventions |
| Native member screens | Mobile project, shared typed client |
| Staff screens and navigation | Admin SSR project, existing AdminClient/security conventions |
| Durable integration state | Existing EF Core context and migrations |
| Local stack and lifecycle | Compose files, containers configuration, existing launch scripts |
| Checks and integration evidence | Existing Checks/browser/native test conventions and delivery docs |

Important existing integration points include `src/Swoovr.Api/Moderation/StandingEndpoints.cs`, `ModerationEndpoints.cs`, `src/Swoovr.Admin/Components/Pages/SupportLookup.razor`, `src/Swoovr.Web.Client/Moderation/ReportSupport.razor`, mobile standing/notices/photo appeal/support pages, notification services and `SwoovrDbContext`.

Swoovr API paths remain stable `/api/...` paths with the existing date-based header convention; do not introduce `/v1` path segments. Zammad's own `/api/v1` is an external provider detail.

## Implementation order and completion gates

### 1. Real Zammad deployment and repeatable provisioning

- Integrate a pinned upstream-supported Zammad Compose stack, including its database, cache, search, application/background processes and persistent volumes as required by that release. Keep Zammad storage and credentials logically isolated from Swoovr's application database.
- Make Swoovr API and Zammad mutually reachable through intentional Docker networking. Avoid conflicts with screening and existing ports; update scripts, URLs, issuer/redirect settings and documentation if any Swoovr ports change. Prefer avoiding unnecessary auth URL changes.
- Add non-destructive start/stop/status lifecycle support, health/readiness checks and explicit opt-in destructive reset behavior. Existing Swoovr launch behavior must remain usable.
- Generate secrets outside Git and logs. Provision idempotently: runtime/provisioning identities, 13 named groups from the source prompt, safe handling of the initial Users group, role/group mappings, categories, ticket attributes, webhook and triggers. Discover IDs; do not hard-code environment-specific numeric IDs.
- Gate: a clean local installation starts successfully, provisioning can run twice without duplication, API reaches Zammad and Zammad reaches the intended webhook route. Record versions, ports and resource requirements.

### 2. Provider adapter, identity and minimal durable state

- Implement a provider abstraction and bounded typed HTTP transport with redacted logs, explicit timeouts and operation-aware retry/error behavior.
- Create customers on first support interaction. Use `swoovr-{canonical-lowercase-member-guid}` as member login and a durable GUID-to-numeric-user-ID mapping. Email is mutable, not identity proof.
- Handle verified email changes, guest/member collisions and later registration without automatically merging identities based on matching email. Handle suspension/deletion and uncertain customer creation safely.
- Persist only justified integration state: identity mappings, linked references/cases, idempotency/operation records, durable webhook processing and reconciliation metadata. Keep Zammad authoritative for conversations, queues, ownership and ticket status.
- Existing SW report references must remain valid and unambiguous. Define ticket reference behavior without renumbering reports or causing collisions.
- Gate: repeat/concurrent creation and simulated lost responses do not silently produce duplicate tickets/customers; a verified email change preserves access to existing tickets.

### 3. First complete native vertical slice

- Implement authenticated member create/list/detail/reply operations with server-derived customer identity and current account-access checks.
- Add real native member web and MAUI ticket screens using existing clients/BFF patterns.
- Add a basic Admin queue and ticket detail with public reply, private note, assignment and close/reopen.
- Connect real signed Zammad events to the existing notification infrastructure.
- Gate: member submits in Swoovr, staff reads and replies in Swoovr Admin, member sees the reply in Swoovr, all backed by real Zammad. Private notes and their attachments never appear in customer results or notifications.
- This is an intermediate milestone; continue with the remaining tasks.

### 4. Complete day-to-day staff functionality and restrictions

- Implement paging, search, queue filters, sorting, status, priority, owner assignment, escalation/group transfer, categories/subcategories, sensitivity, resolution, customer lookup/create-on-behalf, ordinary permitted attachments and safe bulk operations.
- Support macros/text modules where verified APIs permit them. Implement integration health, mappings and delivery/reconciliation diagnostics in Admin. Assess trigger/category/configuration management from the source prompt; use a secured Zammad admin fallback only for documented rare advanced configuration operations, not routine agent work.
- Maintain an operation-by-operation capability matrix; record any genuine upstream limitation with evidence rather than silently dropping functionality.
- Enforce actual current Swoovr staff roles and group permissions both at API boundaries and in Zammad. Resolve the service-account access design explicitly: a broadly privileged token must never substitute for per-staff authorisation or provide a route around restricted-group controls.
- Check permissions on every read/write/download; group transfers must remove unauthorised access immediately. Treat Legal and Restricted T&S specially. Preserve existing staff conflict exclusions and sensitive-operation device/session requirements.
- Sanitise provider HTML and filenames; prevent script execution, untrusted remote content, token leakage and attachment-based internal-note leaks.
- Gate: role/group/action tests cover allowed and denied operations, customer impersonation attempts, restricted transfers, direct API access and attachment access.

### 5. Guest backend for the future Cloudflare website

- Provide a documented public submission and verified follow-up API without building the independent website. Do not use non-existent public pages as proof of a working flow.
- Implement limited-purpose, expiring, revocable guest access using the existing security architecture; do not create a competing member identity system. Ticket numbers, email addresses and member GUIDs are not credentials.
- Include non-enumerating responses, abuse/rate controls, replay-resistant exchanges, explicit origin/session/CSRF behavior and configured allowed origins. Provide bot-protection integration where justified without inventing provider credentials.
- Document how the future Cloudflare frontend initiates verification and exchanges grants. Test through a disposable local harness and Mailpit, with configurable public frontend URLs; no committed production secrets or fake live-delivery claims.
- Gate: guest A cannot access guest B's ticket; expired/revoked/replayed grants fail; no membership is required to submit; the future frontend can integrate from the documented contract.

### 6. Connect existing support, appeals and T&S entry points

- Replace appropriate authenticated web/mobile Contact Support actions with native ticket flows and prefilled safe context. Handle signed-out routes explicitly; do not route completed local journeys to the non-existent website.
- Link account appeals, photo appeals and report/case follow-up to Zammad correspondence/work queues without breaking existing appeal eligibility, reviewer conflict rules, retention or enforcement decisions.
- Swoovr remains authoritative for case and appeal outcomes. Closing a ticket must not implicitly overturn a sanction. Preserve existing historic records; choose and document a conservative DEFAULT backfill/linking policy rather than creating a ticket for every historical report automatically.
- Store only safe summaries/references in Zammad. Preserve evidence and sensitive documents in Swoovr's controlled systems, with authorised deep links or API-mediated access.
- Preserve support and appeals access permitted by existing locked/suspended/terminated account policy; don't accidentally apply normal discovery/member eligibility gates to support.
- Gate: existing appeal/report/enforcement checks still pass; staff can handle linked correspondence natively; cases and evidence are not duplicated into the helpdesk.

### 7. Reliability, email and operations

- Verify webhook signatures over raw request bytes against the pinned release. Persist accepted work before acknowledging; deduplicate actual supported delivery identifiers and ticket/article event identity. Do not assume a unique delivery ID exists.
- Re-fetch current authoritative provider data as needed. Handle article-free changes, private/public articles, transfer, status changes, retries, dead letters and missed-event reconciliation. Prevent loops from Swoovr-originated writes.
- Integrate in-app/push notifications without duplicating Zammad email; preserve provider acceptance versus actual delivery distinctions.
- Configure/test local outgoing mail via Mailpit. Determine a supported local inbound-email test mechanism and test threading/sender handling. Document real support mailbox/DNS/provider inputs separately; do not send external messages without explicit authorisation.
- Add outage behavior, bounded backoff, correlation, health diagnostics, log redaction, safe attachment limits/scanning and documented email alias/routing configuration. Do not treat email sender address alone as authorisation for sensitive actions.
- Exercise backup/restore of the local Zammad dataset and document version upgrades, rollback, reconciliation and remote environment configuration. Keep sensitive evidence out of helpdesk backups/search/email.
- Gate: duplicate/reordered/missed events, Zammad restart/outage and uncertain write outcomes do not leak data, lose acknowledged work or create uncontrolled duplicate correspondence/notifications.

### 8. Final verification and delivery

- Run real local Zammad integration tests in addition to focused unit/contract checks. Verify member web, Admin and available MAUI build/runtime paths; report native-device limitations honestly.
- Run the repository-required `./scripts/check.sh` before every commit. Record actual evidence and distinguish pre-existing failures from regressions. Never claim success when required checks have not passed.
- Exercise the full acceptance matrix: identity changes/collisions, idempotent creation, staff permissions, guest grants, internal notes/attachments, transfer access loss, email threading, notifications, outages/reconciliation, appeals and evidence separation.
- Perform a final security/code review and fix actionable findings. Stage only task-owned files. Open a PR to `main`; do not merge/deploy. If GitHub access is unavailable, leave the tested branch and a ready PR description and report the exact blocker.
- Update status, architecture/API contract, operator instructions, setup/configuration guide, validation evidence and future public-website integration handoff. Include an example environment template with placeholders only; configuration names must match implementation.
- Finish with implemented scope, requirements coverage, test results, PR/branch, local start instructions and precise external acceptance gaps.

## Stopping rules

Keep working through all authorised local implementation and verification. Do not stop at the first vertical slice or repeatedly ask whether to continue. External mailbox credentials, production hosting, legal retention decisions, the independent Cloudflare frontend and actual device access may remain external gates; isolate them and complete everything else. Never call the entire integration production-ready merely because local checks pass, and never fabricate success to satisfy persistence.

Do not create a new chat yourself. Execute this work in the chat receiving this prompt.
