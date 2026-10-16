# Delivery phases

Each phase is a reviewable PR to main, with a concrete demo and recorded local evidence. No phase implies production deployment. Keep GitHub Actions fully commented. Implement the product acceptance criteria progressively rather than attempting the whole brief in the foundation PR.

| Phase | Scope / dependencies | Exit evidence |
| --- | --- | --- |
| 0 — Foundation | Organize docs; seven projects; local solution/checks; canonical palette; auth policy; decision process | Portable build, contract/design checks, local web/API smoke, PR and explicit mobile gaps |
| 1 — Identity and client foundations | Q01–03/Q09; verify Keycloak contract; role provisioning, optional MFA, login/logout/recovery, typed transport, web render choice, mobile shell | Real sandbox member/privileged login matrix; negative auth tests; Android and iOS build/device evidence; no social staff path |
| 2 — Appearance and safe discovery | Six schemes × requested modes, offline/account persistence; policy evaluator; profiles and identity gate (Q04 for live verification) | 18 preferences/12 visuals, account-switch/conflict tests, accessible controls; discovery/media/direct URL deny tests; labelled verification simulation until provider ready |
| 3 — Connection loop | Requests/chat, block propagation, plans, travel/incognito, private feedback; Q05 | Allowed/denied journeys on web and both mobile targets, realtime membership revocation; no pre-arrival initiation or feedback disclosure |
| 4 — Venue and commerce sandbox | Partner/admin/support areas, offers/holds/bookings/vouchers, provider webhook adapters; Q06 for real commerce | Concurrent no-oversell/no-double-redemption tests; idempotent billing; scoped staff access; clearly labelled sandbox purchases |
| 5 — Pilot readiness | Q07–08, contracts, verified identity/privacy/policy review, operations, backups, app-store work | Security/accessibility/device reviews; restore drill; moderated compact pilot plan; explicit production authorization |

Phase 0 was merged into main. Phase 1 is IN PROGRESS: the [account access policy slice](PHASE-1-ACCESS-POLICY.md) delivers provider-independent Domain rules and negative checks. Live authentication, client foundations and all phase 1 exit evidence remain incomplete; phases 2–5 remain planned. Do not mark a phase complete from a passing build alone. A blocked external integration can use a clearly labelled simulation while independent work continues; its production gate remains open.

Each PR states: problem/result, requirement IDs, changed defaults, commands/results, screenshots when UI changes, unresolved gaps and next blocking question. Re-enable hosted checks only on owner request, by moving the template from `.github/workflow-templates` to `.github/workflows`, uncommenting and reviewing it. Never spend runner minutes merely to reproduce local evidence while disabled.

The next partial Phase 1 slice adds [local Docker and Keycloak sandbox](PHASE-1-KEYCLOAK-SANDBOX.md): independently containerised server hosts, pinned isolated realm and startup/discovery checks. This does not meet the live login/session or mobile exit criteria. D15 records containerised server deployment direction without authorizing deployment.

A further partial Phase 1 [browser investigation slice](PHASE-1-BROWSER-INVESTIGATION.md) adds an opt-in local client/flow, validated ID-token observations and provider rejection probes. It does not connect the API/web hosts or satisfy email ownership, social/linked-account, MFA, persistence or mobile exit criteria.
