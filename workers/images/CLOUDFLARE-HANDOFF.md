# Cloudflare image Workers — handoff for a new session

**Current owner direction, 10 November 2026 (D355):** UAT uses the confirmed single-level hosts in [the D355 record](../../docs/delivery/D355-UAT-HOSTNAMES-2026-11-10.md). UAT application hosting is OPEN, including Vultr; production remains Azure. Azure-specific instructions are an optional deployment path. No live hosting or DNS is implied.


Written 9 November 2026 for a fresh Claude session that will **set up the Cloudflare side of the Swoovr image pipeline** (GitHub issue #294 in this repository). Read this whole file before running anything. It is self-contained, but you may read the referenced files in this repository (the Swoovr implementation repo that contains this file) at any time; when this file and the source disagree, **the source wins** and you should note the difference for the owner.

Labels used below follow the repository rules: **CONFIRMED** (owner-approved, preserve it), **DEFAULT** (a sensible engineering choice you may use unless the owner says otherwise) and **OPEN** (needs an owner decision; ask, don't guess).

---

## 1. Your job in one paragraph

The Worker **code already exists and is tested** in `workers/images/` of this repository. Do **not** rewrite it. Your job is to (a) create the Cloudflare resources it needs (two private R2 buckets, two Queues, one R2 event notification), (b) write private per-environment Wrangler configs that point at those resources, (c) install Worker secrets, (d) deploy the three Workers on approved hostnames, and (e) prove the chain works end to end in **UAT** in *staging-only* mode. Production comes later and only after the owner says so. Every remote change (`wrangler … create`, `wrangler secret put`, `wrangler deploy`, DNS) is an outward-facing action: tell the owner exactly what you are about to run and get a yes first.

## 2. Hard rules (do not break)

1. **Staging only.** The new pipeline must not serve real member photos yet. The API's `/api/media/pipeline/access` route always answers "denied" (HTTP 503 `pipeline-cutover-incomplete`) and there is no config switch to change that. A Delivery Worker that returns 403/503 in UAT is **correct**, not a bug. Do not try to "fix" it.
2. **The old media pipeline stays the default.** Don't touch existing R2 buckets, media settings or the API's current upload path.
3. **No secrets in Git, issues, PRs, chat transcripts or shell history.** Never print a secret value. Never commit a private Wrangler config. Install secrets with the interactive prompt of `wrangler secret put` (it hides input), not as command arguments.
4. **Buckets stay private.** No `r2.dev` access, no public custom domain on either bucket. Workers have `workers_dev: false` and `preview_urls: false` — keep it that way.
5. **Least privilege per Worker.** Only give each Worker the bindings/secrets in the table in §6. Never give any Worker PostgreSQL, Shield (Project Arachnid) or evidence-storage credentials.
6. **Separate environments.** UAT and production get separate buckets, queues, Workers and secrets. Never point UAT at production resources or vice versa. Never run the pipeline against the existing production media bucket.
7. **No test/development/UAT wording in anything members can see** (owner D321). Resource names may include `-uat`; responses and pages must not.
8. **Don't change policy or code casually.** If you find a real bug in `workers/images/src/`, make a minimal fix with a test on a branch, open a PR to `main` (don't merge), and tell the owner. `src/policy.mjs` changes need a new `POLICY_VERSION` and a matching API `Media__Pipeline__AcceptedPolicyVersion` change.
9. **Report honestly.** A dry-run is not a deployment; a deployment is not end-to-end proof; a UAT pass is not production readiness. Say which one you actually achieved.

## 3. How the whole thing works

```
Member app ──(1) POST /api/me/photos/uploads──▶ Swoovr API (selected host, api-uat.swoovr.app)
    │                                             │ creates job row, signs an upload capability
    │◀──────── uploadUrl + uploadToken ───────────┘
    │
    └─(2) PUT /uploads  Authorization: Bearer <uploadToken> ──▶ UPLOAD Worker
                                                                   │ verifies HMAC capability
                                                                   ▼
                                     R2 QUARANTINE bucket: incoming/{imageId}/{uploadId}
                                                                   │ (3) object-create event (prefix incoming/)
                                                                   ▼
                                                     Queue: swoovr-images-jobs-<env>
                                                                   │
                                                                   ▼
                                                        PROCESSING Worker (queue consumer)
       (4) POST /api/media/pipeline/claim  ◀──────────────────────┤  (service token)
       (5) GET original from QUARANTINE                            │
       (6) POST /v1/process (multipart) ──▶ Vultr load balancer ──▶ processor VM1/VM2
                                            (ClamAV + NudeNet + NSFW + Shield)
       (7) write variants + result JSON to R2 PROCESSED bucket     │
       (8) POST /api/media/pipeline/results ◀──────────────────────┘
                                                     failures retry; after 12 → DLQ swoovr-images-dlq-<env>

Viewer ──GET /images/{imageId}/{display|thumbnail}  Bearer <grant>──▶ DELIVERY Worker
                     asks API /api/media/pipeline/access on every request → currently always denied
```

Step by step:

1. **Intent.** A signed-in member asks the API for an upload (`purpose: "profile"` is the only purpose the API issues today). The API checks limits, creates an `image_processing_jobs` row and returns `uploadUrl` (`https://<upload-worker-host>/uploads`), a separate `uploadToken`, `method: "PUT"`, `maxBytes` (10 MiB) and an expiry (≤15 minutes).
2. **Upload Worker** (`src/upload.mjs`). Accepts only `PUT /uploads` (plus CORS `OPTIONS`). It verifies the token (HMAC-SHA256 with `UPLOAD_SIGNING_KEY`), checks the browser `Origin` against `ALLOWED_ORIGINS`, accepts only `image/jpeg|png|webp`, enforces the size limit, hashes the bytes and writes them **once** (no overwrite) to `QUARANTINE` at `incoming/{imageId}/{uploadId}`. Returns `201 {imageId, status:"pending"}`.
3. **Event.** R2 sends an object-create notification for the `incoming/` prefix to the jobs Queue. Notifications are only hints; the API's job row is the authority.
4. **Processing Worker** (`src/processing.mjs`), queue consumer, one message at a time:
   - validates the event (bucket name must equal `QUARANTINE_BUCKET_NAME`, key must match `incoming/<uuid>/<uuid>`),
   - replays any previously stored specialist (known-content) report first,
   - **claims** the job from the API (`action` = `process` | `complete` | `ignore`),
   - reads the exact original (ETag-pinned), checks size and SHA-256,
   - calls the processor `POST {PROCESSOR_BASE_URL}/v1/process` with `Authorization: Bearer PROCESSOR_API_KEY` (180 s timeout),
   - validates every returned byte, writes WebP variants to `PROCESSED` at `staged/{imageId}/{jobId}/{eventId}/{display|thumbnail}.webp`, writes one immutable result at `results/{imageId}/{jobId}.json` (known-content matches go to `specialist/{sourceKey}.json` instead),
   - reports to the API `POST /api/media/pipeline/results`.
   - On any failure it retries with backoff (30 s doubling, max 1 h); after `max_retries` (12) the message goes to the DLQ. A "pending" result (a required engine didn't finish) is reported and then deliberately retried.
5. **Rules** (`src/policy.mjs`, version `swoovr-images-2026-11-08-v1`): known-content match → `blocked`; malware → `malware`; any required engine incomplete → `pending`; exposed-anatomy score ≥ 0.40 or NSFW → `review`; otherwise `passed`. Unknown values fail closed.
6. **Delivery Worker** (`src/delivery.mjs`). `GET|HEAD /images/{imageId}/{display|thumbnail}` with a Bearer grant. It asks the API every time; only on `allowed:true` with a matching key/hash does it stream from `PROCESSED`, `Cache-Control: private, no-store`. Today the API always denies, so it returns 503/403.

Every Worker → API call uses `Authorization: Bearer API_SERVICE_TOKEN`, `Content-Type: application/json`, `Swoovr-Version: 2026-10-28`, HTTPS only, 15 s timeout, no redirects.

## 4. Files you should read (in this order)

| File | Why |
| --- | --- |
| `workers/images/README.md` | Worker contracts, bindings table, `wrangler` resource commands, review corrections. |
| `workers/images/src/*.mjs` | The actual behaviour. Small files; read them all. |
| `workers/images/wrangler.{upload,processing,delivery}.jsonc` | Committed **templates** with `-local` names and `.invalid` URLs. Don't deploy them as they are. |
| `workers/images/.dev.vars.example` | Local secret names. |
| `docs/delivery/VULTR-IMAGE-PIPELINE.md` | Operator runbook: VM side, API settings, acceptance gates, rollback. |
| `docs/superpowers/specs/2026-11-08-vultr-image-pipeline-design.md` | Full design, especially §2, §5–§7, §11. |
| `src/Swoovr.Api/Media/ImagePipelineEndpoints.cs`, `ImagePipelineProtocol.cs`, `ImagePipelineStore.cs` | The API side the Workers talk to. |
| `docs/delivery/EDGE-UAT.md` | How the `swoovr.app` zone and UAT hostnames are set up (certificates, cache-bypass rules). |
| `AGENTS.md`, `docs/delivery/QUESTIONS.md` (Q171–Q174) | Repo rules and the open owner decisions. |

Processor side (separate repo, read-only for you): `repasscloud/image-moderation-api`, `deploy/vultr/README.md`. It exposes `GET /health` (liveness), `GET /ready` (models + scanner ready) and `POST /v1/process`. The current build is **v1.0.1** (includes the PNG transparency fix). The owner runs the VMs; you only need the load-balancer HTTPS hostname and the processor `API_KEY`.

## 5. What to create in Cloudflare (per environment)

Use the environment suffix `uat` first. Names below are **DEFAULT** — the owner may rename; if so, change them consistently everywhere (Worker configs, `QUARANTINE_BUCKET_NAME`, the notification, the queue consumer and DLQ).

| # | Resource | DEFAULT name (UAT) | Settings |
| --- | --- | --- | --- |
| 1 | R2 bucket (originals) | `swoovr-images-quarantine-uat` | Private. No public access, no custom domain, no `r2.dev`. **No lifecycle/expiry rule** (raw-original retention is OPEN Q172). |
| 2 | R2 bucket (outputs) | `swoovr-images-processed-uat` | Private, same rules. No expiry rule (specialist reports must never expire through ordinary cleanup). |
| 3 | Queue (DLQ) | `swoovr-images-dlq-uat` | Create **before** the main queue. Retention 14 days (1209600 s) — needs the paid Queues plan; on the free plan retention is shorter, tell the owner. |
| 4 | Queue (jobs) | `swoovr-images-jobs-uat` | Retention 14 days. Consumer = Processing Worker (configured in its Wrangler file). |
| 5 | R2 event notification | on bucket 1 | Event `object-create`, **prefix `incoming/`**, target Queue 4. Never on bucket 2 (would loop). Create this **last** (see §8). |
| 6 | Worker | `swoovr-images-upload-uat` | Custom domain, binding `QUARANTINE`, var `ALLOWED_ORIGINS`, secret `UPLOAD_SIGNING_KEY`. |
| 7 | Worker | `swoovr-images-processing-uat` | **No public route** (queue only; its `fetch` returns 404). Bindings `QUARANTINE`, `PROCESSED`, queue consumer. |
| 8 | Worker | `swoovr-images-delivery-uat` | Custom domain, binding `PROCESSED`. |

Data location — **OPEN (Q171/Q172)**: Australian residency has not been decided. The pinned `wrangler r2 bucket create` accepts `--location` (`weur`, `eeur`, `apac`, `wnam`, `enam`, `oc` — `oc` is Oceania) and `--jurisdiction`. A location hint is only honoured the first time a bucket name is created, and it is a hint, not a residency guarantee. Ask the owner which to use **before** creating buckets.

Hostnames — **CONFIRMED D355**: `images-upload-uat.swoovr.app` (Upload Worker) and `images-uat.swoovr.app` (Delivery Worker), attached as Workers **Custom Domains** in the `swoovr.app` zone. Custom Domains create the DNS record and an edge certificate for that exact host. Check `docs/delivery/EDGE-UAT.md`: existing UAT cache rules bypass cache for the four app hosts; make sure no zone rule (Cache Everything, transformations, bot challenges) affects the image hosts. For production, the owner picks the hosts (e.g. under `swoovr.app`) — OPEN.

## 6. Bindings, variables and secrets

| Worker | R2 bindings | Plain vars | Secrets (`wrangler secret put`) |
| --- | --- | --- | --- |
| Upload | `QUARANTINE` → quarantine bucket | `ALLOWED_ORIGINS` = exact comma-separated browser origins. UAT DEFAULT: `https://uat.swoovr.app` | `UPLOAD_SIGNING_KEY` |
| Processing | `QUARANTINE`, `PROCESSED`; queue consumer of jobs queue with DLQ | `API_BASE_URL` = `https://api-uat.swoovr.app`; `PROCESSOR_BASE_URL` = `https://<vultr-lb-hostname>` (from owner); `QUARANTINE_BUCKET_NAME` = exact quarantine bucket name | `API_SERVICE_TOKEN`, `PROCESSOR_API_KEY` |
| Delivery | `PROCESSED` → processed bucket | `API_BASE_URL` = `https://api-uat.swoovr.app` | `API_SERVICE_TOKEN` |

`ALLOW_LOCAL_HTTP=true` is only for local `wrangler dev` against `http://127.0.0.1`/`localhost`. **Never set it on a deployed Worker.**

Secrets must match values configured elsewhere. Each must be **at least 32 characters**, and the service token and signing key must be **different**:

| Secret | Same value must be in | Who sets the other side |
| --- | --- | --- |
| `UPLOAD_SIGNING_KEY` (Upload) | API `Media__Pipeline__UploadSigningKey` | Owner, private API runtime secret (Key Vault only for the optional Azure path) |
| `API_SERVICE_TOKEN` (Processing **and** Delivery) | API `Media__Pipeline__ServiceToken` | Owner, private API runtime secret |
| `PROCESSOR_API_KEY` (Processing) | Processor `API_KEY` on **both** VMs | Owner, VM env files |

`PROCESSOR_API_KEY` is the processor's inbound key, **not** a Shield/Arachnid credential.

Recommended handling: the owner generates each value once (e.g. `openssl rand -base64 48` in their own terminal, or their password manager), stores it in the password manager, and pastes it into the hidden `wrangler secret put` prompt and the matching Key Vault/VM setting. If you generate a value, do it so it's never shown in the transcript, and hand it to the owner through their password manager, not chat. The local worksheet from `python3 scripts/image_pipeline_operator.py init --directory .local/image-pipeline/operator` (`workers.env`) lists these names; `check` confirms they line up. It never uploads anything.

The API must also be running with (owner sets these on the selected API host, not you):

```text
Media__Pipeline__Enabled=true
Media__Pipeline__ServiceToken=<API_SERVICE_TOKEN>
Media__Pipeline__UploadSigningKey=<UPLOAD_SIGNING_KEY>
Media__Pipeline__UploadOrigin=https://images-upload-uat.swoovr.app
Media__Pipeline__AcceptedPolicyVersion=swoovr-images-2026-11-08-v1
```

`UploadOrigin` must be the Upload Worker's HTTPS origin with path `/` only. The `ImageProcessingStaging` database migration must already be applied in UAT (owner/API deployment work).

## 7. Private Wrangler configs

Never edit the committed templates with real values. Make private copies in the gitignored folder `.local/image-pipeline/cloudflare/` at the repository root (`.local/` is already in `.gitignore`). Wrangler resolves `main` **relative to the config file**, so the path climbs back to the source.

`.local/image-pipeline/cloudflare/uat.upload.jsonc`:

```jsonc
{
  "name": "swoovr-images-upload-uat",
  "main": "../../../workers/images/src/upload.mjs",
  "compatibility_date": "2026-11-08",
  "workers_dev": false,
  "preview_urls": false,
  "observability": { "enabled": true, "traces": { "enabled": true }, "logs": { "enabled": true, "invocation_logs": false } },
  "routes": [{ "pattern": "images-upload-uat.swoovr.app", "custom_domain": true }],
  "r2_buckets": [{ "binding": "QUARANTINE", "bucket_name": "swoovr-images-quarantine-uat" }],
  "vars": { "ALLOWED_ORIGINS": "https://uat.swoovr.app" }
}
```

`.local/image-pipeline/cloudflare/uat.processing.jsonc`:

```jsonc
{
  "name": "swoovr-images-processing-uat",
  "main": "../../../workers/images/src/processing.mjs",
  "compatibility_date": "2026-11-08",
  "workers_dev": false,
  "preview_urls": false,
  "observability": { "enabled": true, "traces": { "enabled": true }, "logs": { "enabled": true, "invocation_logs": false } },
  "r2_buckets": [
    { "binding": "QUARANTINE", "bucket_name": "swoovr-images-quarantine-uat" },
    { "binding": "PROCESSED", "bucket_name": "swoovr-images-processed-uat" }
  ],
  "queues": { "consumers": [{
    "queue": "swoovr-images-jobs-uat",
    "max_batch_size": 1, "max_batch_timeout": 1, "max_concurrency": 1,
    "max_retries": 12, "retry_delay": 60,
    "dead_letter_queue": "swoovr-images-dlq-uat"
  }] },
  "vars": {
    "API_BASE_URL": "https://api-uat.swoovr.app",
    "PROCESSOR_BASE_URL": "https://<vultr-lb-hostname-from-owner>",
    "QUARANTINE_BUCKET_NAME": "swoovr-images-quarantine-uat"
  }
}
```

`.local/image-pipeline/cloudflare/uat.delivery.jsonc`:

```jsonc
{
  "name": "swoovr-images-delivery-uat",
  "main": "../../../workers/images/src/delivery.mjs",
  "compatibility_date": "2026-11-08",
  "workers_dev": false,
  "preview_urls": false,
  "observability": { "enabled": true, "traces": { "enabled": true }, "logs": { "enabled": true, "invocation_logs": false } },
  "routes": [{ "pattern": "images-uat.swoovr.app", "custom_domain": true }],
  "r2_buckets": [{ "binding": "PROCESSED", "bucket_name": "swoovr-images-processed-uat" }],
  "vars": { "API_BASE_URL": "https://api-uat.swoovr.app" }
}
```

Consumer settings: `max_concurrency: 1`, batch 1 is a **conservative DEFAULT, not measured capacity**. Raise it only after the owner has benchmarks for both VMs and for one VM during maintenance (Q171). Observability keeps invocation logs off; the Worker logs only content-free codes (`image-job-failed`, code, attempt). Don't add logging of keys, image IDs with content, tokens or provider payloads.

## 8. Order of work

Do these in order. Stop and ask the owner at each **[ask]**.

**A. Local preparation (no remote changes)**

```sh
cd workers/images
node --version          # need >= 22
npm ci                  # pinned wrangler 4.158.0 + miniflare
npm test                # expect all tests to pass
npm run check           # tests + three template dry-runs
npm audit
npx wrangler whoami     # confirm the right Cloudflare account; [ask] if more than one account
```

Then write the three private configs (§7) and dry-run them:

```sh
npx wrangler deploy --dry-run --config ../../.local/image-pipeline/cloudflare/uat.upload.jsonc
npx wrangler deploy --dry-run --config ../../.local/image-pipeline/cloudflare/uat.processing.jsonc
npx wrangler deploy --dry-run --config ../../.local/image-pipeline/cloudflare/uat.delivery.jsonc
```

**B. Inputs to collect from the owner [ask]**

- Cloudflare account to use (and that `swoovr.app` is in it).
- Bucket location/jurisdiction (Q171/Q172).
- Worker hostnames (confirm or replace the DEFAULTs).
- Queues plan (paid needed for 14-day retention).
- Vultr load-balancer HTTPS hostname, and confirmation that `GET https://<lb>/ready` returns ready from both VMs.
- That the UAT API has the `Media__Pipeline__*` settings and the migration applied, and that the three secret values exist in their password manager.

**C. Create resources [ask before running]** — check flags with `--help` first; these come from the pinned Wrangler:

```sh
npx wrangler r2 bucket create swoovr-images-quarantine-uat   # add the agreed location/jurisdiction flag
npx wrangler r2 bucket create swoovr-images-processed-uat
npx wrangler queues create swoovr-images-dlq-uat  --message-retention-period-secs 1209600
npx wrangler queues create swoovr-images-jobs-uat --message-retention-period-secs 1209600
```

Afterwards confirm in the dashboard that both buckets have public access **off**.

**D. Deploy Workers, then secrets [ask]**

```sh
npx wrangler deploy --config ../../.local/image-pipeline/cloudflare/uat.upload.jsonc
npx wrangler deploy --config ../../.local/image-pipeline/cloudflare/uat.processing.jsonc
npx wrangler deploy --config ../../.local/image-pipeline/cloudflare/uat.delivery.jsonc

npx wrangler secret put UPLOAD_SIGNING_KEY --config ../../.local/image-pipeline/cloudflare/uat.upload.jsonc
npx wrangler secret put API_SERVICE_TOKEN  --config ../../.local/image-pipeline/cloudflare/uat.processing.jsonc
npx wrangler secret put PROCESSOR_API_KEY  --config ../../.local/image-pipeline/cloudflare/uat.processing.jsonc
npx wrangler secret put API_SERVICE_TOKEN  --config ../../.local/image-pipeline/cloudflare/uat.delivery.jsonc
```

The owner types/pastes each value into the hidden prompt. Until the notification exists, nothing reaches the processing queue, so the order is safe. Check with `npx wrangler secret list --config …` (shows names only).

**E. Turn on events last [ask]**

```sh
npx wrangler r2 bucket notification create swoovr-images-quarantine-uat \
  --event-types object-create --prefix incoming/ --queue swoovr-images-jobs-uat
npx wrangler r2 bucket notification list swoovr-images-quarantine-uat
```

## 9. Verifying it in UAT

Use only **safe synthetic images** (e.g. a generated solid-colour PNG). Never real member photos, never anything illegal, never "test" CSAM. Shield acceptance testing is a separate owner-run process with an authorised harmless fixture.

1. **Hostnames up:** `curl -i -X OPTIONS https://images-upload-uat.swoovr.app/uploads -H 'Origin: https://uat.swoovr.app'` → `204` with `Access-Control-Allow-Origin: https://uat.swoovr.app`. A different origin → `403 origin-not-allowed`. `curl -i https://images-uat.swoovr.app/images/<any-uuid>/display` (no auth) → `403 access-denied`.
2. **API readiness** (owner or you with the service token, never echoed): `GET https://api-uat.swoovr.app/api/media/pipeline/readiness` with the Bearer service token → `200 {"ready":true,"mode":"staging-only","deliverable":false,…}`.
3. **Full path** with a UAT test member account the owner provides:
   - `POST https://api-uat.swoovr.app/api/me/photos/uploads` body `{"purpose":"profile"}` (member auth + `Swoovr-Version` header as the app sends it) → `uploadUrl`, `uploadToken`.
   - `PUT <uploadUrl>` with `Authorization: Bearer <uploadToken>`, `Content-Type: image/png`, the PNG body → `201 {"status":"pending"}`.
   - Watch `npx wrangler tail swoovr-images-processing-uat` (content-free logs) and the queue dashboard.
   - Poll `GET /api/me/photos/uploads/{imageId}` → should move from `pending` to `pending-review` (staged). Members never see `passed`/`blocked` detail; that's intentional.
   - In R2 `PROCESSED`, expect `results/{imageId}/{jobId}.json` and two `staged/…/*.webp` objects for a clean image.
4. **Negative checks:** reuse the same token → `409 upload-already-stored`; expired or altered token → `401`; >10 MiB → `413`; `text/plain` → `415`.
5. **Failure handling:** if the processor is unreachable, messages retry with backoff and finally land in the DLQ. Don't purge or replay the DLQ without the owner; recovery is an API-led reconciliation (not built yet).

Record what passed, what you couldn't test, and the deployed Worker version IDs (`npx wrangler deployments list --config …`). Put the evidence in a short section at the end of `docs/delivery/VULTR-IMAGE-PIPELINE.md` on a branch, open a PR to `main`, and don't merge it.

## 10. Troubleshooting map (code → cause)

| Code / symptom | Likely cause |
| --- | --- |
| `missing-signing-key`, `missing-api-service-token`, `missing-processor-key` | Secret not set or shorter than 32 chars. |
| `invalid-api-url`, `invalid-processor-url` | Var isn't a clean `https://` URL (no credentials, `?query`, `#fragment` or plain `http`). |
| `invalid-upload-permission` (401) | Signing key differs from the API's `UploadSigningKey`, token expired (>15 min) or altered. |
| `origin-not-allowed` (403) | Browser origin not in `ALLOWED_ORIGINS` (exact match, scheme included). |
| `api-401` in processing logs | `API_SERVICE_TOKEN` differs from API `ServiceToken`. |
| `api-404` | API has `Media__Pipeline__Enabled` off. |
| `api-503` / `persistence-unavailable` | API database unavailable or migration missing. |
| `invalid-object-event` | Notification on the wrong bucket/prefix, or `QUARANTINE_BUCKET_NAME` doesn't match. |
| `processor-401` | `PROCESSOR_API_KEY` differs from the VM `API_KEY`. |
| `processor-503` | VM draining / not ready; check the LB and `/ready`. |
| `mandatory-check-pending` (keeps retrying) | An engine (often Shield or ClamAV) didn't complete; expected to retry, check VM health. |
| Delivery always 503 | **Expected** during staging (`pipeline-cutover-incomplete`). |

## 11. Out of scope for you

- Building or running the Vultr VMs, load balancer or processor (owner, separate repo).
- API settings, private runtime secrets and database migrations on the selected host (owner).
- Any client/app change to use the new upload path, member-visible delivery, review/appeal adapters, evidence custody, raw retention rules, or production cutover. These are the release gates listed in `docs/delivery/VULTR-IMAGE-PIPELINE.md` and stay open on #294.
- Production resources, until the owner explicitly asks. When they do, repeat §5–§9 with `-prod` names, production hostnames and **new** secret values.

## 12. Repository conventions if you change files

- Work on a descriptive branch (e.g. `docs/image-workers-uat-evidence`), open a PR to `main`, never merge or push to `main`.
- Run `./scripts/check.sh` from the repo root before committing, and `npm run check` in `workers/images` if you touch Worker code. Record real results, including failures.
- Keep GitHub Actions files commented out (owner rule). Don't add projects or dependencies by habit.
- Questions for the owner go in `docs/delivery/QUESTIONS.md` with a GitHub issue linked both ways (next free Q number), using `docs/delivery/DECISIONS.md` for format.
- Commit only the files you changed; never `.local/`, `.dev.vars` or anything with a secret.
