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

Unattended follow-up: the [discovery policy slice](PHASE-2-DISCOVERY-POLICY.md) advances independent Phase 2 Domain invariants while Phase 1 remains IN PROGRESS. It enables no application browsing or provider authentication and does not satisfy end-to-end media/direct URL or device gates.

The [guest web appearance slice](PHASE-2-WEB-APPEARANCE.md) adds real local preference controls and generated canonical runtime palettes. Account sync, mobile rendering and feature-wide acceptance remain open; static SSR does not answer Q03.

The [portable appearance synchronization slice](PHASE-2-APPEARANCE-SYNC.md) adds shared-client generation/retry/conflict rules and DTOs. It is not connected account persistence and does not complete account-switch/offline integration acceptance.

The [fictional discovery preview](PHASE-2-DISCOVERY-PREVIEW.md) demonstrates a partial local API/client/web journey and explicit denial/empty/error states. It does not supply real authentication/verification/location, chat or mobile evidence; both Phase 1 and Phase 2 remain incomplete.

The [actual local signing-key rotation investigation](PHASE-1-SIGNING-ROTATION.md) advances Phase 1 with real browser/provider overlap, restoration and removed-key rejection. It does not validate a future production API/BFF adapter or complete mail/social/MFA/mobile gates.
