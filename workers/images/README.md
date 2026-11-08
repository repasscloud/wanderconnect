# Swoovr image Workers

Implementation for #294: separate upload, queue processing and delivery entrypoints. **Current API integration is staging-only.** An accepted processing report records findings; it does not publish an image or authorise member delivery. The existing application upload/review/evidence path stays in place until the remaining lifecycle/custody/client cutover is verified. Do not switch real clients to this package merely because dry-run succeeds.

## Local checks

Node >=22, npm and a supported workerd host are needed. Dependencies are pinned in package-lock.json; no global Wrangler install is needed.

```sh
cd workers/images
npm ci
npm test
npm run check
npm audit
```

`npm test` runs the rule, capability, bounded transfer, result persistence/callback retry, malformed variant, denied delivery and actual local workerd/R2 upload tests. `npm run check` also bundles all three Workers through Wrangler dry-run; it does not create resources or deploy. Miniflare runs a real local workerd process for upload replay validation. Its current pinned v5 API uses the package's compatibility conversion helper to keep explicit module definitions.

An optional cross-repository contract test consumes the actual FastAPI multipart response fixture generated from safe synthetic imagery and fake scanners in the processor repository:

```sh
PROCESSOR_RESPONSE_FIXTURE=/tmp/swoovr-294-process-response npm test
```

The fixture basename must have `.bin` response bytes and `.json` metadata with `contentType`. Without it only that cross-repository test is explicitly skipped; no provider acceptance is claimed. This fixture is not real member imagery or a real Shield match.

## Files and contracts

- `src/policy.mjs`: executable automatic rules, version `swoovr-images-2026-11-08-v1`. Selected exposed-anatomy NudeNet score >=0.40 or NSFW -> review; complete no-signal -> pass; known-content match dominates; every required engine must complete. Unknown status/class/score fails closed.
- `src/upload.mjs`: `PUT /uploads`, Bearer signed capability, immutable R2 object. PNG/JPEG/WebP MIME allowlist is an ingress check; processor validates actual bytes. Fixed current limit <=10MiB. No client bucket/key selection.
- `src/processing.mjs`: queue consumer; HEAD object, API claim, retrieve exact ETag, bounded multipart processor request and response, verify all manifests/digests before writes, persist result then callback. Terminal duplicate replay reads durable result instead of rerunning inference. Pending report is durably accepted then manifest cleared for a fresh retry. No image/raw provider payload logs.
- `src/delivery.mjs`: `GET|HEAD /images/{imageId}/{display|thumbnail}`, Bearer access grant checked through API on EVERY request, private no-store response. No grant in URL/referrer. Initial API deliberately denies delivery until cutover.
- `src/shared.mjs`: strict capabilities, bounded bodies, service transport and common errors.

Upload capability is `base64url(UTF8 JSON).base64url(HMAC-SHA256(signingKey UTF8,payloadSegment UTF8))`. Payload: `{protocolVersion:1,imageId,uploadId,jobId,sourceKey:"incoming/{imageId}/{uploadId}",purpose:"profile|album|chat",maxBytes,expiresAt:unixSeconds,lifecycleRevision}`; optional lowerhex `sourceSha256`. All IDs are UUIDs. Expiry must be in the next 15 minutes. API currently issues profile intents only; do not manufacture album/chat grants.

API service calls use HTTPS, `Authorization: Bearer API_SERVICE_TOKEN`, `Swoovr-Version: 2026-10-28` and bounded JSON:

| Route | Input / authority |
| --- | --- |
| `POST /api/media/pipeline/claim` | `{protocolVersion:1,sourceKey,sourceEtag,sourceUploadedAt,sourceBytes}`. HEAD upload time is used, rather than processing time, so queue backlog does not expire a valid uploaded image. API returns trusted IDs, source identity, purpose, lifecycleRevision, maxBytes, policyVersion and action process/complete/ignore. |
| `POST /api/media/pipeline/results` | Durable UUID eventId, job/image IDs, sourceKey/ETag/SHA256, revision, policyVersion, outcome/reason, sanitised engine results and exact variant manifest. API inbox treats identical replay as success; accepted result remains staged until lifecycle adapters are complete. |
| `POST /api/media/pipeline/access` | `{protocolVersion:1,imageId,variant,grant}`. API must return current allowed/key/contentType/sha256; otherwise never read image. Initial staging API denies this. |

Processor: authenticated `POST /v1/process` multipart fields `file` original bytes and `job` JSON `{protocolVersion:1,jobId,imageId,sourceSha256,purpose}`. Response multipart fields `manifest` JSON and named binary `display`/`thumbnail`. Manifest echoes job/source identity and `results` engine statuses, plus `variants:[{name,contentType,width,height,bytes,sha256}]`. Complete safe pass/review candidates have WebP display <=2048 and thumbnail <=320, no upscaling. Known-content provider-test review has no variants; specialist match, incomplete required engines and malware have none. Input cap10MiB, aggregate output20MiB plus256KiB bounded envelope. All valid variant digests verified before R2 output. Permanent processor invalid-input statuses400/413/415/422 report unsupported;409 integrity/auth/server failures remain non-deliverable and retry to recovery.

Prepared objects live at `staged/{imageId}/{jobId}/{eventId}/{name}.webp`; the per-attempt event ID prevents competing attempts overwriting bytes. The single immutable result key is `results/{imageId}/{jobId}.json`. A concurrent loser uses the winning durable report. Orphan partial variants need API-authorised cleanup/reconciliation; they never become deliverable by existing in storage. Worker does not delete raw originals or evidence: retention/custody approvals are still required.

## Configuration and secrets

Templates are `wrangler.upload.jsonc`, `wrangler.processing.jsonc`, `wrangler.delivery.jsonc`. They use deliberately non-routable example origins, no public workers.dev endpoint and no preview URLs. Make environment-specific private copies before deployment. Do not edit committed templates to contain real secrets.

| Worker | Bindings | Variables | Secret names |
| --- | --- | --- | --- |
| Upload | `QUARANTINE` private ingress bucket | `ALLOWED_ORIGINS` comma-separated exact origins | `UPLOAD_SIGNING_KEY` |
| Processing | `QUARANTINE`, `PROCESSED`, queue consumer | `API_BASE_URL`, `PROCESSOR_BASE_URL`, `QUARANTINE_BUCKET_NAME` | `API_SERVICE_TOKEN`, `PROCESSOR_API_KEY` |
| Delivery | `PROCESSED` | `API_BASE_URL` | `API_SERVICE_TOKEN` |

Upload signing key matches API `Media:Pipeline:UploadSigningKey`. API service token matches `Media:Pipeline:ServiceToken`. Processor API key matches VM `API_KEY`. Each >=32 private characters. Shield username/password belong ONLY on processor VMs and must never be Worker secrets. Workers have no PostgreSQL credentials or evidence-decryption key. Use separate resources/identities/secrets for each environment. `ALLOW_LOCAL_HTTP=true` permits loopback localhost/127.0.0.1 ONLY for local integration, not arbitrary plaintext hosts; do not enable hosted.

Copy `.dev.vars.example` to ignored `.dev.vars` for local-only private values. For hosted configuration install each Worker secret interactively, without echoing values or putting them in shell arguments:

```sh
npx wrangler secret put UPLOAD_SIGNING_KEY --config /private/path/uat.upload.jsonc
npx wrangler secret put API_SERVICE_TOKEN --config /private/path/uat.processing.jsonc
npx wrangler secret put PROCESSOR_API_KEY --config /private/path/uat.processing.jsonc
npx wrangler secret put API_SERVICE_TOKEN --config /private/path/uat.delivery.jsonc
```

These commands change hosted configuration and must only be run during an authorised operator setup. `.dev.vars` is not automatically a hosted secret store. Do not paste real values into issues or docs.

## Resource setup example — operator action, not executed

Before setup select real region/jurisdiction, resources and deployment environment with the owner. Replace example names below consistently in your private configs, including each Worker `name`, bindings, processing `QUARANTINE_BUCKET_NAME`, main queue and dead-letter queue. Use short unique environment suffixes; never point local/test configuration at production buckets. Private R2 buckets must not have public custom domains or r2.dev access enabled. No broad raw bucket expiry until hold/appeal retention is implemented.

The following flags were checked against the pinned Wrangler's `--help` during implementation:

```sh
npx wrangler r2 bucket create swoovr-images-quarantine-uat
npx wrangler r2 bucket create swoovr-images-processed-uat
npx wrangler queues create swoovr-images-dlq-uat --message-retention-period-secs 1209600
npx wrangler queues create swoovr-images-jobs-uat --message-retention-period-secs 1209600
npx wrangler r2 bucket notification create swoovr-images-quarantine-uat --event-types object-create --prefix incoming/ --queue swoovr-images-jobs-uat
```

14-day retention requires the appropriate paid Queue plan; free-plan retention is limited. Region/jurisdiction requirements must be checked before accepting defaults. Evidence storage is separate and not bound to these routine Workers. The consumer config attaches a DLQ with max12 retries; max_concurrency1 and batch1 are conservative starting values, **not measured capacity**. Increase only after full-host and one-VM maintenance benchmarks. New variant writes cannot recursively enqueue processing because only the ingress bucket/prefix emits events.

Once real resources, TLS, secrets and endpoints exist, validate config and only then perform separately authorised deployments:

```sh
npx wrangler deploy --dry-run --config /private/path/uat.upload.jsonc
npx wrangler deploy --dry-run --config /private/path/uat.processing.jsonc
npx wrangler deploy --dry-run --config /private/path/uat.delivery.jsonc
# Hosted deployment only after owner authorisation and closed release gates:
# npx wrangler deploy --config /private/path/uat.upload.jsonc
# npx wrangler deploy --config /private/path/uat.processing.jsonc
# npx wrangler deploy --config /private/path/uat.delivery.jsonc
```

Add explicit approved routes/domains to private configs; do not enable public upload or delivery merely to test a model. All scripts/configs here are independent of Swoovr web releases.

## Current limitations and release gates

This is a tested transport/rules/staging foundation. It is not an end-to-end replacement for the live member-photo lifecycle. Remaining gates include raw-original retention/disclosures; API evidence sealing/holds; Admin staged-image viewing/appeal/re-screen adapters; account/member cleanup and lifecycle outbox; final delivery eligibility/revocation; all client upload cutover; real Shield harmless acceptance; selected architecture/container/resource benchmarks and production security review. Queue expiry and DLQ need API-led reconciliation, not indefinite trust that a message will remain. Existing support-attachment ClamAV is unchanged.

See the main [design](../../docs/superpowers/specs/2026-11-08-vultr-image-pipeline-design.md) and [operator runbook](../../docs/delivery/VULTR-IMAGE-PIPELINE.md) for migration and release gates. Passing local tests/dry-runs does not establish provider availability, safety accuracy, cloud deployment, residency or current hosted health.
