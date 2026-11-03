# Codex / Claude Prompt — Swoovr Zammad Integration Review

You are acting as a senior .NET solutions architect, application security engineer, and delivery lead reviewing the **existing Swoovr repository**. This is a **discovery, design review, gap analysis and effort-estimation task**, **not authorisation to implement features yet**.

## Primary objective

Determine the technical design and full scope of work needed to adopt **self-hosted Zammad as the sole general-purpose ticketing/helpdesk engine for Swoovr**. The integration must deliver:

1. A public support experience on **swoovr.com**, in the existing static website / npm build and deployment architecture, where guests can submit requests and subsequently receive updates and reply after appropriate verification.
2. Authenticated ticket submission, viewing, attachments where permitted, and replies inside Swoovr's web and .NET MAUI applications.
3. Native ticket management features in the **Swoovr Admin WebUI**; agents must not need Zammad's UI for routine actions.
4. The **Swoovr API** as the only trusted integration boundary to Zammad for Swoovr clients, including customer mapping, ticket creation, correspondence, status, groups, macros, attachments, webhooks, notification handling and error recovery.
5. An assessment of the complete implementation effort, gaps, dependencies, rollout phases, acceptance criteria, risks and estimated developer effort.

**Do not create another ticketing engine, ticket conversation store, ticket assignment system or support queue inside Swoovr.** Zammad owns those concepts. The Swoovr database may hold integration references, security metadata, idempotency state and the distinct Swoovr-specific Trust & Safety / enforcement records.

## How to work

- Inspect the repository first. Identify the real solution structure, frameworks, deployed apps, relevant APIs, authentication, access controls, web UIs, static site, existing support and notification components, database schema, background workers, storage, email integration, infrastructure and tests. Provide file paths and line references for important findings.
- **Do not assume** that a capability already exists merely because it is mentioned in the requirements. Label each item **Exists**, **Partially exists**, **Missing**, or **Needs decision**.
- Research current Zammad API documentation and verify each proposed endpoint, payload, role/permission, webhook format and supported operation against the actual deployed Zammad version if configuration or source is available. Do not invent endpoints or claim an API feature is supported without verification.
- Do not modify production configuration, run migrations, create external resources, publish anything or change code unless separately instructed.
- Where this prompt mentions a potential implementation choice, evaluate it against the repository and propose the best fit rather than blindly implementing it.
- Preserve Swoovr's existing conventions. In particular, Swoovr public API paths **do not use `/v1`, `/v2` or `/vX`**; use the existing date-based API versioning convention. Zammad's own `/api/v1` paths are third-party implementation details only.
- Prefer concise, maintainable .NET/C# code and clear separation of concerns. If samples are useful, provide C# interfaces, DTOs, route examples and pseudocode, but keep the primary deliverable an actionable assessment.

## Current product and integration decisions

### Identity and customers

- Swoovr identity is managed by **Keycloak**; member and profile data belong to Swoovr.
- Swoovr members have a permanent GUID. Their Zammad customer **login** should be `swoovr-{member-guid}` (canonical lowercase GUID). Email is a separate mutable property.
- External contacts who are not Swoovr members (journalists, guests, partners, applicants, former members) can have an email-based Zammad login.
- A single Zammad customer account should be reused across multiple tickets. Do **not** provision Zammad accounts for every Swoovr registration unless compelling evidence supports it; default to provisioning on first support interaction.
- Keep a durable mapping of Swoovr Member GUID to Zammad numeric User ID. For registered members, resolve tickets through this mapping, not the email address. Sync verified email changes to Zammad without losing tickets.
- For guests, email address, ticket number or Swoovr GUID **must not be considered proof of identity**. Design email verification / magic-link sessions or equivalent access verification for customer ticket viewing and replies.
- Address changing emails, duplicate accounts, collisions between guest and member records, member deletion, suppression, suspended accounts and appeals.
- Admin API accounts should be service identities, separate from ordinary staff and customers.

### Ticketing model

- Zammad is authoritative for tickets, articles, states, owner assignments, groups, replies, notes, macros, triggers, SLAs and ticket history.
- Swoovr API exposes a secure provider abstraction (e.g., `ITicketingProvider` with `ZammadTicketingProvider`), typed HTTP clients, DTOs, mapping and explicit privilege controls.
- Swoovr support references may have a public `SW-XXXXXX` form in addition to Zammad's ticket number. Establish whether and where public/internal references are stored, their uniqueness, and how to look them up.
- Public replies are customer-visible; internal notes must never leak to a customer, including through attachments, exports, notifications or API error responses.
- Ticket classification should use department/group, category, subcategory, priority, sensitivity, source, resolution and optional Swoovr case references; avoid creating hundreds of Zammad groups. Check feasibility of Zammad custom ticket attributes and data-driven dependent categories.
- A customer can create tickets for themselves, and authorised staff can create tickets on behalf of customers. Clients may not submit arbitrary Zammad customer IDs to impersonate others.
- Group/agent permissions are enforced twice: Zammad restrictions AND Swoovr API authorisation. Moving a ticket into a restricted group must immediately remove unauthorised visibility.
- Design handling for closed ticket replies, whether follow-up creates a new ticket, and how customer UIs reflect that.

### Departments / groups

The agreed initial groups are:

1. General Support
2. Billing & Subscriptions
3. Account Recovery
4. Appeals
5. Trust & Safety
6. Trust & Safety — Restricted
7. Legal & Law Enforcement
8. Privacy & Data Protection
9. Public Relations & Media
10. Partnerships & Business Development
11. Marketing & Community
12. Internal Operations
13. Security

There is also an initial Zammad `Users` group. Determine whether it should remain enabled and how unroutable or misclassified tickets are handled.

Evaluate staff roles and group permissions, including the ability to create/transfer into a restricted group without permission to read that group's tickets. Propose a matrix of staff role × group × read/create/change/ownership. Treat Legal and Restricted Trust & Safety as specially controlled.

### Trust & Safety boundary

- Use Zammad as the workflow and correspondence/queue engine **for Trust & Safety too**; do not implement a duplicate T&S ticket system.
- Retain **Swoovr-specific** authoritative reports, incident cases, preserved evidence, chain of custody, enforcement actions, bans, appeals outcomes, legal holds and law-enforcement disclosures in Swoovr's controlled infrastructure.
- Ticket-to-case links should avoid copying original evidence, sensitive identification material, or child-safety content into Zammad's attachments, search indexes, outgoing email and backups.
- Review the existing safeguards and model separation, and specify precisely what should be visible in Zammad vs Swoovr Admin.
- Identify escalation paths, permissions, auditable actions and retention obligations; flag jurisdiction-specific legal requirements for legal review instead of inventing policy.

### Public website, guest support and email

- The public support UI must fit the existing **swoovr.com static website built/deployed via npm**. Verify whether the actual repository is a static SPA, SSR, generated HTML or something else; do not assume a Node server must be hosted.
- The public frontend must call **Swoovr API**, never Zammad directly and never expose a Zammad token. Consider CORS, CSRF/session handling, abuse controls, rate limiting, CAPTCHA/bot protection, file-type restrictions, privacy notices and localisation.
- Guest ticket submission should be accessible without a Swoovr membership. Consider optional contact categories such as support, recovery, privacy, media, partnerships and security; the full internal 13-group list must not be exposed to visitors.
- Public ticket follow-up needs an authorised customer portal or verified email magic-link flow, including secure session lifetime, token revocation, replay resistance and non-enumeration of ticket IDs.
- `support@swoovr.com` is/will be connected as a Zammad email channel. Assess inbound ticket creation by email, outbound replies, conversation threading, sender identity, spam handling, channel routing and avoiding duplicate emails or notification loops.
- Other possible public aliases include billing, appeals, safety, legal, privacy, security, press, partnerships and community. Determine which require real independent channels vs aliases/routing.
- Zammad's embeddable public form can be considered a local prototype, but the intended production public interface should be Swoovr-branded and API-mediated.

### Admin WebUI

Investigate existing Swoovr Admin WebUI and propose native screens or extensions for:

- Ticket lists, queue views, statuses, sorting, search, filters and bulk operations.
- Ticket detail with threaded customer-visible articles vs clearly marked private notes.
- Reply as staff, attachment handling where permitted, internal notes, follow-up tickets, close/reopen where permitted.
- Assignment, escalation, group transfer, priority, categories/subcategories, sensitivity and resolution.
- Swoovr member context and linked internal Trust & Safety report or case.
- Customer lookup and creation: by Zammad numeric ID, Swoovr GUID / `swoovr-{GUID}`, or email (with precise ambiguity handling).
- Administrative settings/configuration for the integration: connection health, group mapping, authorised role mapping, macros, triggers, categories, webhook delivery and reconciliation diagnostics.
- Restricted views and permissions, with desktop-only access for particularly sensitive operations when required by current Swoovr Admin design standards. Routine support can remain usable on mobile/tablet if justified.
- UI consistency with existing Swoovr admin controls; no prototype-only developer language or excessive header explanations.

Assess whether an authenticated deep link to Zammad Admin is a useful fallback for rare advanced operations, but the day-to-day workflow must occur in Swoovr Admin.

### Webhooks and notification delivery

A working local prototype has already demonstrated:

- Zammad creates a ticket or article via API.
- A trigger calls an existing configured webhook.
- The webhook payload contains `ticket` and `article` objects, including `ticket.id`, `ticket.number`, `ticket.customer_id`, `ticket.state`, `article.id`, `article.ticket_id`, `article.sender`, `article.internal`, `article.type` and `article.attachments`.
- Internal agent notes arrive with `article.internal = true`, so they must not be sent to members.

Design Swoovr's production webhook consumer:

- Validate signature on the **raw** request body with a shared secret (confirm current Zammad header/signature format).
- Validate event schema, reject unauthenticated data, deduplicate delivery IDs and article IDs, queue work durably and acknowledge promptly.
- Re-fetch authoritative ticket/article data via Zammad API as appropriate instead of trusting a large webhook payload.
- Handle updates that contain no article, internal notes, public agent replies, ticket close/reopen, moves, ownership changes and priority changes.
- Trigger the existing Swoovr notification infrastructure for relevant external replies/status changes, and do **not** duplicate messages when Zammad also sends email.
- Provide retries, dead-letter handling, monitoring, logging, metrics, alerting and periodic reconciliation if webhooks are missed.
- Avoid notification loops when Swoovr-originated API updates generate Zammad webhooks.
- Evaluate whether webhook ingestion should run in the Azure Swoovr API or a dedicated Cloudflare Worker that validates/queues then delegates processing to Azure, consistent with current infrastructure choices.
- Public Swoovr API routes follow existing date-based versioning (no `/v1` segments).

### File uploads and recovery verification

- Ordinary support attachments may use Zammad when authorised and appropriate; assess limits and scanning.
- Sensitive account recovery uploads (photo ID, selfie/liveness, personal identifiers) must **not** be sent over ordinary email or stored casually as Zammad attachments.
- We evaluated self-hosted dropbox products, but **will likely build a dedicated Swoovr secure upload capability**. This is a separate workstream; do not assume Sprag, Gokapi or Nextcloud are the chosen solution.
- The intended flow is Zammad ticket → Swoovr API issues short-lived, single-purpose, ticket-bound upload invitation → customer proves control → encrypted, restricted upload → authorised case review → deletion/retention policy/audit.
- Ticket number alone is not a credential or authorisation. The signed/upload token and identity checks must be independent of guessable references.
- Evaluate whether the existing R2 storage and Swoovr identity-verification provider can be reused. Treat all retention/deletion rules as decisions to validate, not defaults.

### Operational / infrastructure considerations

- Existing stack includes .NET API hosted in Azure, PostgreSQL, Keycloak, MAUI and web frontends, Cloudflare/R2 and Swoovr notifications. **Verify current repository details rather than assuming these are already implemented.**
- Zammad is expected to be self-hosted, reachable only through appropriate private/secured network boundaries, with its own backing services and backups.
- Distinguish staff UI authentication from service-to-service Zammad API authentication. Propose runtime least-privilege token(s) and separate admin/provisioning credentials.
- Evaluate outages, timeouts, retry/backoff, circuit breakers, idempotent customer creation and ticket creation, consistent correlation IDs, request logging without leaking PII/secrets, S3/R2 and DB security.
- Evaluate Zammad version compatibility, upgrade testing, data migration, backup/restore, disaster recovery and exit strategy.
- Identify Zammad licensing obligations (including AGPL aspects) for the intended deployment, without providing unsupported legal conclusions.

## Explicit discovery questions

Answer all of the following:

1. Which current repository projects and files would require changes? Which already contain suitable functionality?
2. Is Zammad an appropriate source of truth for Swoovr's planned support and T&S correspondence, and where must Swoovr remain authoritative?
3. What complete set of API operations must the integration implement? Provide a route/method matrix for Swoovr and the corresponding verified Zammad operations.
4. What customer lifecycle rules handle new members, external guests, email changes, duplicate identities, deactivation, deletion and later registration of former guests?
5. What is the right permission model across the 13 groups, staff roles and member/guest views? Where do restrictions require Swoovr-specific enforcement beyond Zammad?
6. How should the public static site communicate with the API, authenticate guests for follow-ups and present a branded support experience?
7. What UI changes are needed in Swoovr Admin to manage all day-to-day tickets without going to Zammad?
8. How will Zammad agent replies cause Swoovr member notifications? How will delivery be reliable and idempotent?
9. What is the plan for email channels, aliases, inbound/outbound responses, message threading, sender verification and suppression of duplicate auto-replies?
10. What custom metadata, object attributes, macros, triggers, tags, text modules and SLA configuration are needed in Zammad, and how are they provisioned consistently across environments?
11. What APIs or integration limitations does the current Zammad release impose? What functionality would still require the Zammad UI?
12. How do we avoid double-handling and duplicated ticket state while maintaining Swoovr-specific safety records?
13. How should sensitive-document collection be designed and scoped separately?
14. What is the deployment, testing and rollback strategy?
15. What is the **total realistic effort**, including implementation, integration testing, UAT, security testing, documentation and production readiness?

## Required deliverables

Create a comprehensive written assessment **in the repository's existing documentation location**, without changing application code. Use these sections:

1. **Executive decision** — viability, recommendation, explicit constraints and the major architecture decision.
2. **Current-state inventory** — repository project/file map and existing capabilities with evidence and citations to file paths.
3. **Target architecture** — clear end-to-end component and data-flow diagrams (Mermaid is fine), including website, web/mobile, API, Zammad, email, webhook intake, notification service and distinct T&S evidence/cases.
4. **Ticket and identity lifecycle** — state diagrams for guest creation, member creation, email changes, ticket creation, reply, agent response, closure, guest ownership verification and account recovery.
5. **API contract matrix** — proposed Swoovr endpoints (without `/vX` prefixes), authorised actor, DTOs and actual Zammad endpoint mappings; label unverified functionality clearly.
6. **Data model** — minimal Swoovr PostgreSQL integration tables/columns, identity mappings, ticket mapping, reference mapping, webhook inbox/outbox, audit and reconciliation state; distinguish existing schema from proposed migrations.
7. **Permissions / security matrix** — roles × Zammad groups × actions; guest/session checks; internal note handling; trust boundaries and secrets.
8. **Frontend requirements** — public npm/static site, logged-in member support, admin screens, UX/accessibility/localisation, with mock route/screen inventory.
9. **Webhooks / notifications / email** — trigger strategy, example minimal payloads, retries, reconciliation and duplicate suppression.
10. **Zammad provisioning/configuration** — queues, custom ticket attributes, macros, triggers, webhooks, text modules, email channels and per-environment configuration.
11. **Risk and decisions register** — security, privacy, retention, operations, technical unknowns, trade-offs and recommendations.
12. **Delivery breakdown** — granular epics/issues or work packages with dependencies, complexity, *estimated engineering days or story points*, confidence range, validation plan and critical path. Separate MVP, launch-ready and later enhancements. Do not present estimates as facts without explaining assumptions.
13. **Acceptance criteria / UAT plan** — concrete scenarios, including guest/member ticket creation, customer identity updates, group transfer/access loss, public replies vs private notes, signed webhooks, missed webhooks, email threading, ticket closures, rate limits, cross-tenant leaks and sensitive evidence handling.
14. **Open questions** — only genuinely blocking choices after repository inspection; include a recommended default for each.
15. **Implementation order** — a pragmatic phased plan that minimises rework and gets a working end-to-end vertical slice first.

For each proposed work package include: **what changes, why, affected files/services, dependencies, risk, effort estimate, test plan and definition of done**.

## Output constraints

- Prioritise concrete recommendations over generic architecture prose.
- Clearly distinguish **confirmed repository facts**, **verified Zammad capabilities**, **assumptions** and **recommendations**.
- Do not expose real API tokens, secrets, emails from test data or personal customer records in deliverables.
- Do not modify or delete existing GitHub issues or create new issues unless separately asked.
- **Do not begin implementation.** Finish by presenting the proposed architecture, scope, phased plan and estimates for review and approval.
