# Phase 3 — fictional connection scenarios API

> Slice record from 16–18 October 2026. Read [current status](STATUS.md) for integration and remaining work; [D30](DECISIONS.md#naming-confirmation--22-october-2026) governs current naming.

Partial API → typed Client slice. No real sign-in, private messages, profile/identity/location evidence, persistence, production member access or realtime delivery. UI/browser journey follows separately. Development AND Prototype:Enabled required; no routes in Production even with flag. Every projection IsSimulation=true, no-store; canned fictional adults/messages only.

DEFAULT: disposable in-memory scenario, fixed Alex/Jamie adult pair, simulation actor selector (alex/jamie; NEVER authentication). Fixed canned request/reply only, no supplied message text. Each scenario holds one retained ConnectionThread across actions. No repeat after decline/close in that scenario; creating a fresh scenario is explicitly resetting a disposable simulation, not a production repeat-request policy. Coarse local/upcoming/arrived presence at creation is labelled simulated and stays fixed; Q05 prearrival acceptance remains denied. Block is irreversible within scenario, suppresses all message projections and sends for both actors immediately. Close/report eligibility remains Domain controlled; no report service.

One process lock covers state load, fresh simulated facts, revision compare and replacement, so concurrent equal-revision actions yield exactly one success within this process. This is NOT distributed concurrency, provider authorization, durable uniqueness or realtime subscription/push proof. Limit32 live scenarios and64 canned messages each (safety actions remain available at the cap), expiry20min, bound JSON16KiB, no raw request logs/real identities. Generic resource404, malformed400, stale/denied409, capacity429. Cookie/session/CSRF and client UI are not added by this API slice; it is a local opt-in fictional service, no credentials attached.

## Local contract

Base /api/v1/prototype/conversations.

- POST base JSON exactly {"travel":"local|upcoming|arrived"}.201 returns scenario projection for alex, including id, revision0, state None, actor alex, canRead=false, canReport=false, messages[]. Location is simulation, never device/IP evidence.
- GET /{id:guid}?actor=alex|jamie. Exactly one actor query, no other params.200 projection with current state/revision and safe canned message list only if current Domain CanRead; absent/expired404.
- POST /{id:guid}/actions JSON exactly {"actor":"alex|jamie","action":"request|accept|decline|send|close|block","revision":N}. No unknown/duplicate/missing JSON members or query parameters; integer nonnegative revision.200 updated safe projection; stale/invalid lifecycle/current safety409. Block requires a participant and current revision, one-way safety state, increments scenario revision and suppresses all messages. Non-block transitions run Domain lifecycle rules; scenario revision is separate because safety changes also increment it.

Projection: isSimulation,id,revision,state,actor,canRead,canReport,messages[{sender,text}]. Block returns state Unavailable with no message count/history and canRead=false, canReport still hypothetical historical permission. No tokens, legal identity, undisplayed intentions or exact location. IDs are disposable scenario selectors, not real account authorization.

## Native implementation plan

Branch codex/fictional-connection-scenarios based onc297a74/PR#14. Same seven projects. API owns fixture orchestration, Contracts scalar DTOs, Client typed transport only, Domain unchanged.

- [x] Add scripts/connection_smoke.py to scripts/check.sh; fail404 before implementation. HTTP tests default/Production gates, malformed/unknown/duplicate/body bounds, request→accept→reply→block suppression both actors; sender/self/outsider/stale denials; decline repeat; upcoming vs arrived; concurrent same-revision exactly one commit; capacity32/unknown IDs.
- [x] Add Contracts/PrototypeConversation.cs, API/Prototype/PrototypeConversations.cs, Map in API; typed Client/PrototypeConversationClient.cs and portable transport/false-simulation cases. No new provider, package or application project.
- [x] Independent review/regressions, fullcheck, docs/D28/ledger, commit/PR main. Then continue web scenario journey/remaining phases, no merge/deploy.

## Evidence

See [validation](VALIDATION.md#fictional-connection-apiclient-17-october2026): actualHTTPscenario/gates/concurrentCAS/bodybounds/capacity pass; initial404/missingclient/uncappedmessage failures observed before implementation/fix. Independent review no actionable findings. Fullcheck results inledger. No browser/container/mobile claim;20minute expiry wait untested. All fixture facts are SIMULATION. Nothing merged/deployed.
