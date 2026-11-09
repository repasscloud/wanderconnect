# Vultr Image Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Build as much of #294 as can be verified locally, with independent processor releases and actionable operator instructions; preserve fail-closed existing media behaviour until a proven cutover.

**Architecture:** Three Cloudflare Workers orchestrate private R2 and Queues; two Vultr VMs each run warm ML and local ClamAV. Versioned Worker rules decide automatic outcomes; API owns authenticated durable lifecycle state, human review and database access.

**Tech Stack:** Existing Python/FastAPI/CPU engines, ClamAV sidecar, JavaScript Workers, .NET/PostgreSQL API.

**Spec:** `docs/superpowers/specs/2026-11-08-vultr-image-pipeline-design.md`.

## Global Constraints

- No hosted provisioning, merge, deployment, real secrets or claims of real provider acceptance.
- No new .NET project; existing support scanner/review/appeals/evidence safeguards preserved.
- Automatic pass rules versioned in Worker; successful appeal/reconsideration still requires a fresh scan.
- Each VM has local ClamAV; processor release independent of Swoovr web.
- Actual checks and gaps recorded; no enabling hosted Actions.

## Review Focus

- Forged or stale upload/job/result must not publish content.
- Incomplete/disabled/malformed required scan must never pass.
- API callback retries must reuse durable results and not duplicate effects.
- Deletion, specialist preservation and review races must fail closed.
- Private variants and real secrets must never leak through delivery/logs.

## Shared protocol v1 (implementation contract)

Processor: authenticated `POST /v1/process`, multipart `file` (original), `job` JSON `{protocolVersion:1,jobId,imageId,sourceSha256,purpose}`. UUID strings for IDs; purpose `profile|album|chat`. Response multipart/form-data: `manifest` JSON `{protocolVersion:1,jobId,imageId,sourceSha256,results,variants:[{name,contentType,width,height,bytes,sha256}]}` and each named WebP variant part. `results` retains existing engine shapes `nudenet`, `nsfw`, `arachnid` and adds `malware:{status,classification,engineVersion,signatureVersion,...}`; successful completed malware classification `clean`, infection `infected`; no variants on specialist match/required failure/malware/invalid. Manifest variant names `display` and `thumbnail` initially; exact bounds documented with source limits. Processor inbound Bearer credential separate from Shield Basic-auth pair.

Worker reports: API routes to be agreed by API implementer and communicated in ledger; report includes protocolVersion, eventId,jobId,imageId,sourceSha256,policyVersion,outcome,reason,results,variants. Worker outcomes `passed|review|blocked|malware|pending|unsupported`. Do not promise whole application cutover before its integration and preservation tests pass. New path opt-in and refused when required configuration absent.

### Task 1 — Independent processor and deployment stack

Files in sibling image-moderation-api repository: new processing endpoint/module and tests; deploy/vultr Compose/env/build/release tools; existing app registration/settings changes.
- [ ] Write failing endpoint/ClamAV/result tests; run red.
- [ ] Implement authenticated bounded processing with original-byte malware scan, required engines, prepared variants and strict manifest.
- [ ] Build independent two-host deployment stack, runtime-only secrets, readiness/drain and operator commands.
- [ ] Run unit/contracts, Docker config/build and safe scan tests where available; record gaps.

### Task 2 — Cloudflare pipeline and policy

Files: `workers/images/` package, tests, Wrangler templates/configuration examples, private binding setup and protocol examples.
- [ ] Test strict policy and input validation before implementation.
- [ ] Implement upload, processing and delivery workers with explicit API authority and immutable objects, bounded streaming/response limits.
- [ ] Persist outcomes before callback; retries skip inference where valid; cover duplicate/stale and unknown results.
- [ ] Verify Node/runtime tests and Wrangler dry-run; no deploy.

### Task 3 — API integration foundation and safe opt-in

Files: existing Media/Contracts/Domain/Infrastructure and Checks, additive migration if required; no unrelated changes.
- [ ] Inspect existing upload/review/hold/lease boundaries and establish failing validation/inbox tests.
- [ ] Implement signed upload/claim/completion/access contract and durable idempotent lifecycle, where safe with existing preservation. Coordinate exact transport with task 2.
- [ ] Preserve old pipeline by default; expose explicit readiness/cutover blockers if full safe wiring cannot be completed.
- [ ] Verify build/checks and isolated database scenarios with disposable credentials; never mutate managed credentials/data.

### Task 4 — Operator integration, cross-service tests and review

Files: `docs/delivery/VULTR-IMAGE-PIPELINE.md`, protocol/operations examples, test runner and delivery record; current docs/ticket disposition updates.
- [ ] Establish local processor/Worker/API contract compatibility with safe fixtures and explicit fake-provider evidence.
- [ ] Run required check.sh, meaningful focused suites, Docker validation; record any skipped provider acceptance.
- [ ] Review whole branch and fix material findings; create reviewable PRs to main in both repos.
- [ ] Close only superseded/completed tickets, add explicit evidence and remaining dependencies. Leave #294 and release gates open when incomplete.

## Execution authority and rulings

Owner explicitly requested overnight autonomous implementation on 8 November 2026; proceed without further plan approval. Engineering defaults remain reversible. Provider credentials, VM purchase/setup and hosted cutover remain owner inputs. Review/implementation may narrow an unsafe partially integrated feature behind a disabled switch rather than weaken existing safety.


## Delivery status — 9 November 2026

Tasks1 and2 have locally tested processor and Worker implementations. Task3 delivered a disabled-by-default durable staging API with an additive migration and hard publication gate; full member lifecycle cutover is explicitly incomplete. Task4 supplies operator/configuration instructions and local verification; independent review could not finish because the reviewer hit a usage limit. See the delivery runbook for exact tests and remaining gates. No hosted deployment or merge.
