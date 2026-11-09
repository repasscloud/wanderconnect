# Independent Vultr image processor and Cloudflare integration

Tracking: [#294](https://github.com/repasscloud/wanderconnect/issues/294). Design: [requirements and migration](../superpowers/specs/2026-11-08-vultr-image-pipeline-design.md). Implementation: [task plan](../superpowers/plans/2026-11-08-vultr-image-pipeline.md).

**Read the implementation evidence below before enabling anything.** This runbook prepares independent processor VMs and local integration. A successful image build, local test or health check is not a completed Swoovr production cutover. The existing media path remains default until lifecycle, evidence, review, delivery and clients are verified together. No VM, Worker, Queue or R2 resource is provisioned by running the local operator-input commands.

## UAT API staging configuration

UAT application hosting remains **OPEN** (owner, 10 November 2026); it may run on Vultr. Production remains Azure. The processor VMs remain independently operated. [Exact API staging settings, private secret inputs and migration procedure](UAT-IMAGE-STAGING-2026-11-10.md) apply to either UAT application host. UAT upload: `https://images-upload-uat.swoovr.app`; delivery: `https://images-uat.swoovr.app`; Worker `API_BASE_URL`: `https://api-uat.swoovr.app`; upload `ALLOWED_ORIGINS`: `https://uat.swoovr.app`. An API private environment file on Vultr supplies the same names as the optional Azure Key Vault bindings. Staging does not enable member delivery or switch the legacy media path.

## What the operator supplies

- Two Vultr VMs in the selected region, with a CPU architecture supported by the built images. VM size is selected after measurement of the processor AND local ClamAV, including signature reload peaks. During maintenance one VM must sustain the agreed capacity or queue delay.
- A Vultr load balancer, its private backend connectivity, stable processor HTTPS hostname, valid TLS and network/firewall restrictions. Raw backend addresses are operational inputs, not URLs handed to members.
- Shield username and password authorised for both backends and their shared rate limit. These are not the inbound processor API key.
- Container registry destination and private registry pull identity if required. Deploy exact immutable processor/ClamAV digests, not mutable `latest`.
- Cloudflare account/zone/resource identities, separate private quarantine/processed storage, processing and dead-letter queues, Worker hostnames and secrets. Evidence storage has separate privileged custody controls; routine Workers must not gain evidence read/decryption access.
- A reachable Swoovr API HTTPS origin and API/Worker service identities, current pipeline eligibility configuration and monitored incident contact.
- Owner decisions Q171–Q174: sizing/region/latency/budget, original retention/disclosure, scan freshness/re-screen triggers, and model/provider hosted-use acceptance. Local files with credentials do not resolve these decisions by themselves.

## Prepare private input files locally

From the Swoovr implementation checkout:

```sh
python3 scripts/image_pipeline_operator.py init --directory .local/image-pipeline/operator
python3 scripts/image_pipeline_operator.py check --config .local/image-pipeline/operator/operator.json
```

`init` creates a private directory (0700) and files (0600), refuses to overwrite an existing destination, and generates placeholders rather than real credentials. The initial `check` intentionally reports missing values/approvals. Fill values locally using a text editor. Never paste private values into an issue, commit, shell command history or this document.

The `vm1.env` and `vm2.env` templates contain the inbound `API_KEY` and `ARACHNID_API_USERNAME` / `ARACHNID_API_PASSWORD`. Both backends use the shared inbound key initially because one Worker contacts the load balancer. Rotating that key needs a coordinated overlap strategy, not changing one live VM unexpectedly. Shield identities may differ if the provider permits it. The Worker processor key must match the inbound processor key; it must not be a Shield credential.

`workers.env` is an operator-input worksheet. Use the exact secret names in `workers/images/README.md` when installing deployed Worker secrets. A private `.env` file on your laptop is not automatically installed in Cloudflare or copied to a VM. Never source these files as shell scripts; the checker treats them as literal data, refuses interpolation and never prints values. Its pass means local input consistency only, not that the endpoints or provider have accepted them.

## VM image build and independent release

The processor implementation lives in the separate `repasscloud/image-moderation-api` repository: [processor PR #2](https://github.com/repasscloud/image-moderation-api/pull/2). The Swoovr web deployment must not rebuild/restart these VMs. Use the linked service PR/release and its `deploy/vultr` runbook for exact build/config/run commands; do not mix source contracts from incompatible commits.

Required release metadata: protocol version, processor image digest, source revision, NudeNet package/model digest, Freepik package/model revision, image-transform recipe, ClamAV image/engine and signature versions. Runtime secrets are injected on each VM and never copied into image layers. Models should already be available in the release image/cache and run offline; starting multiple HTTP worker processes must not duplicate models beyond measured RAM capacity.

Each VM runs its image processor and its OWN ClamAV sidecar. ClamAV signatures have a local persistent volume updated by freshclam. Its unauthenticated TCP scanning socket must not be published on the VM public interface. The processor scans original bytes through the private scanner connection before full decode or resize. Exceeded limits, old/unknown signatures, failed/disabled engines and Shield errors cannot produce a pass.

Initial rollout order:

1. Install a supported Docker Engine and Compose on both owner-created VMs; apply host updates and restrictive network settings. Validate target CPU architecture.
2. Place the reviewed Compose/configuration and digest-pinned release on VM1, inject runtime secrets, start the stack, wait for models and local signature database to load, then perform safe smoke checks.
3. Repeat independently on VM2. Neither VM needs PostgreSQL or broad Cloudflare credentials.
4. Point the load balancer at the processor port over the chosen protected backend transport. Health checks must test readiness, not merely that the process exists; account for model warm-up.
5. Verify the authenticated processor through the load-balancer HTTPS hostname with a safe synthetic image. Confirm both backend identities can handle work, ClamAV is reached, and Shield real-provider acceptance is tested separately using an authorised harmless fixture.
6. Record source/image digests, model versions and actual timing/resource results. No public Swoovr upload cutover yet.

Do not assume a shared load balancer terminates backend encryption or authenticates Worker requests automatically. Document the selected TLS and origin-access controls; the inbound API key is checked by the processor even behind the load balancer.

## Cloudflare resource and Worker preparation

For a separate Cloudflare setup session, use the step-by-step [Cloudflare image Workers handoff](../../workers/images/CLOUDFLARE-HANDOFF.md).

Source and test commands are in `workers/images/`. Pin the package lock and use `npm ci`; use local Wrangler dry runs before remote deployment. Follow the exact generated/configuration templates there rather than deploying placeholder resource names.

Resources to prepare, separately per environment:

1. Private R2 quarantine storage for immutable original uploads. Public bucket access stays disabled.
2. Private processed storage for staged variants and durable result manifests. A stored object alone never authorises member delivery.
3. Processing Queue and DLQ. Limit consumer concurrency to measured processor capacity, including one-backend maintenance conditions. Configure backoff, retention, alerting and deliberate DLQ replay.
4. R2 object-created notifications for only the intended quarantine upload prefix. Do not trigger processing recursively from result/variant writes. Notifications are hints; API-created job/object identity is authoritative.
5. Upload, Processing and Delivery Workers, each with only necessary bindings/service identities. Never give a Worker PostgreSQL or Shield secrets.
6. Processor origin and inbound key in Processing Worker secrets; API service identity and upload/access signing material according to the exact implemented contract. Avoid collecting every secret into every Worker.
7. Explicit CORS/client origin policy for uploads and secure routes/TLS. Separate development/UAT/production resources and identities.

Cloudflare `wrangler secret put` is a remote mutation/deployment action. Execute it only when deliberately configuring the intended environment. Do not upload a whole private worksheet indiscriminately. Verify actual API paths and enabled readiness mode before wiring real event consumers.

## Exact API staging configuration and checks

The new path is opt-in with these API environment names (supply values privately through the host's normal configuration mechanism):

```text
Media__Pipeline__Enabled=true
Media__Pipeline__ServiceToken=<same as Worker API_SERVICE_TOKEN; at least32 characters>
Media__Pipeline__UploadSigningKey=<same as upload Worker UPLOAD_SIGNING_KEY; distinct, at least32 characters>
Media__Pipeline__UploadOrigin=https://<upload-worker-hostname>
Media__Pipeline__AcceptedPolicyVersion=swoovr-images-2026-11-08-v1
```

These enable **staging only**. There is no configuration switch that makes staged results deliverable. Do not switch member clients or send real member images to this path before the remaining custody/retention adapters and disclosure gates are resolved. The legacy pipeline remains default.

The additive migration is `ImageProcessingStaging`. It adds `image_processing_jobs` and `image_processing_receipts`; it does not rename or delete existing `media_items`. Use the existing API migration procedure after database backup/review; do not run migrations against a managed environment merely to test these instructions.

Implemented service routes: authenticated `GET /api/media/pipeline/readiness`, `POST /api/media/pipeline/claim`, `POST /api/media/pipeline/results`, and `POST /api/media/pipeline/access`. Service requests use the configured bearer and `Swoovr-Version: 2026-10-28`. Access is deliberately denied during staging. Readiness explicitly reports `staging-only`, `deliverable:false`, and cutover blockers; missing persistence is unavailable.

Member API profile-intent routes are `POST /api/me/photos/uploads`, `GET /api/me/photos/uploads/{imageId}`, and `DELETE /api/me/photos/uploads/{imageId}`. Intent returns a clean `uploadUrl` plus a SEPARATE `uploadToken` and `method:PUT`; put the token in the Authorization Bearer header, never in the URL. Profile intents are implemented; no fabricated album/chat capability, UI/BFF switch or mobile readiness is implied.

Local verification from the Swoovr checkout:

```sh
./scripts/check.sh
python3 tests/image-pipeline/api_transport.py
python3 -m unittest discover -s tests/image-pipeline -p 'test_*.py'
dotnet run --project tests/Swoovr.Checks --configuration Release -- --image-pipeline-checks
# Set SWOOVR_TEST_DATABASE privately to an OWNED disposable PostgreSQL admin connection first:
dotnet run --project tests/Swoovr.Checks --configuration Release -- --image-pipeline-persistence-checks
```

The HTTP probe launches/stops its own loopback API processes without a database and checks registration/authentication/unavailability; it does not alter the existing managed hosts. The persistence runner creates/drops its own named database, exercises the actual additive migrations and concurrent receipts, and never uses member data. The broader existing database suite has a separately recorded GeoNames fixture-count failure; do not mislabel its failure as a new pipeline failure or silently skip it without reporting.

## Acceptance before Swoovr cutover

A local pass is not permission to bypass these checks:

- Every client and upload purpose uses an authorised intent, correct limits and immutable source identity; replay/cross-member/overwrite requests fail.
- Processor runs on both actual VMs with mandatory engines and updated ClamAV. Signature updates, one-host loss, saturated slots and provider errors keep uploads private.
- API inbox commits outcome/audit once; identical retry after a lost response succeeds without repeat inference or duplicate notice; mismatched event content is refused.
- API publication adapter records correct prepared-byte identity, actual dimensions/content type and source identity. The current pipeline assumes a single processed JPEG object; do not substitute WebP/raw source refs without updating all consumers.
- Review, owner rejected-preview, album-only approvals, appeals, mandatory appeal re-screen, specialist preservation/sealing, export, account removal and every deletion job use the new storage mappings and current lifecycle authority.
- Delivery GET/HEAD verifies ready state and current viewer permissions, blocks private variants and responds correctly after block/revocation. Test cache isolation and token expiry explicitly.
- Original retention and provider/region/privacy changes are approved. In-progress appeals and qualifying evidence/holds prevent premature deletion. Restricted known-content material never enters normal review/export/delivery.
- Old and new upload generations have one publisher each. Drain/reconcile before disabling the old worker and remove obsolete startup/config assumptions only after equivalent coverage.

## Maintenance and rollback

Drain VM1 from new load-balancer work, let existing requests finish or safely time out/retry, update to a compatible pinned release, warm models/scanner, safe smoke test and restore rotation. Only then repeat for VM2. Monitor backend health, queue oldest age, provider limits and signature freshness throughout. Do not drain both together.

Worker policy changes need reviewed source/docs/tests and a new policy version. Record which old jobs/results remain accepted. A rollback restores compatible application/Worker/processor versions but does not undo R2 objects or committed database decisions. Stop new uploads if compatibility is uncertain. Keep evidence/holds and durable outcomes intact, and reconcile pending jobs instead of replaying obsolete approvals.

## Validation and delivery evidence

Local evidence, 9 November 2026:

- Three Worker bundles pass Wrangler dry-run;18 tests pass including actual local workerd/R2 replay and the Python-produced multipart fixture; npm audit reports0 vulnerabilities.
- API protocol checks:11 passed. Scoped actual PostgreSQL checks:20 passed, including fresh migrations/model drift, duplicate concurrency, stale revisions, source changes, delayed queue processing and late specialist metadata after cancellation. These do not establish evidence custody handoff.
- Actual loopback API HTTP checks:11 passed for disabled/enabled staging, auth and unavailable persistence/delivery. Operator-input tests:5 passed.
- `./scripts/check.sh` passed with no database configured (persistence explicitly skipped). A separate baseline run against disposable PostgreSQL authenticated successfully but failed the existing GeoNames import count assertion in `PersistenceChecks.Travel`; new scoped pipeline database checks passed independently. No managed database credentials changed.
- Companion processor:165 Python tests passed,2 provider integrations skipped; Ruff checks and ARM64 Docker source build passed. Real ClamAV INSTREAM clean/EICAR handling used tiny synthetic test definitions; real NudeNet/Freepik CPU inference used a generated image. Nonroot readiness/drain tested in Docker. Live Shield not run.
- Source/controller review fixed dropped model-version metadata and weak processing-key acceptance. The fresh independent whole-branch review attempt hit an agent usage limit; no completed independent review is claimed.
- Postman CLI was unavailable; Orbit discovery returned an HTTP error. Local source contracts, actual HTTP/multipart/R2/PostgreSQL tests and official provider docs were used; no Postman cloud acceptance is claimed.

Still required before real member cutover: complete human review/appeal and owner-preview adapters; mandatory fresh appeal scan; restricted specialist sealing/incident handoff; hold-aware original/staged cleanup/export/account-purge mappings; transactional lifecycle outbox/reconciliation; all client/BFF/photo-purpose integration; authorised viewer delivery/revocation; raw retention/privacy/model licence decisions; real provider and actual VM/network/capacity acceptance. #294 and subsystem release issues remain open. Superseded planning trackers #260 and #267 were closed as duplicates, with remaining gates preserved under #294. No image publication, provisioning, remote deployment or merge is claimed by this change.


## Codex PR review follow-up — 9 November 2026

- PR303 specialist-race finding: separate immutable specialist reports survive a competing ordinary result and are replayed before a complete/cancelled job claim can discard them. This does not claim the deferred specialist custody implementation is complete.
- PR303 rate-limit finding: cancelled intents still count toward hourly upload limits; only kept-photo capacity is released. Shared usage queries are exercised against real disposable PostgreSQL.
- Merged processor PR2 transparency finding: a separate follow-up PR normalises palette/color-key/alpha PNG input to RGBA before compositing on white, with transform recipe v2 and regressions for P/RGB/L/RGBA formats. Existing published images predate that fix until rebuilt.
- The owner's existing successful GitHub build workflow is preserved; it publishes multi-platform images to Docker Hub (`repasscloud/image-moderation-api`). Source321dd43 build: https://github.com/repasscloud/image-moderation-api/actions/runs/42535592295. Use the next corrected release's immutable index digest for Vultr. No release/tag/publication was triggered during these review fixes.

Review validation: Worker20 tests and three dry-run bundles passed with the actual processor multipart fixture; scoped PostgreSQL checks25 passed plus protocol11; processor169 passed and2 provider tests skipped, Ruff and local Docker source build passed. The first broad check hit an existing timing-sensitive auth-harness assertion; its focused rerun passed, and the final broad rerun result is recorded on the PR. No existing managed database/container configuration was changed.
