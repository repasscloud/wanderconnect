# Swoovr — Cloudflare Image Storage, Moderation, Delivery and API Integration

**Status:** Proposed architecture / implementation planning  
**Audience:** Codex, Claude, engineering reviewers  
**Scope:** Swoovr web, iOS, Android, API, administration and Cloudflare infrastructure  
**Prepared:** 8 November 2026

> **Instructions to implementers:** These are outcome-focused GitHub issue drafts, not instructions to implement a specific code structure. Inspect the current repository, existing decisions, data model, deployment and moderation services first. Do not assume that the current API stores images locally or that the proposed Cloudflare architecture is already implemented. Raise questions where the existing implementation or requirements disagree. Produce a plan and identify dependencies before making changes; use separate, reviewable PRs.

## Agreed architectural principles

1. **R2 holds all uploaded/processed image bytes.** PostgreSQL holds image ownership, storage references, state, permissions, audit data and other application metadata—not the binary images. Temporary container scratch space is permitted during processing, subject to secure cleanup.
2. **Only the Swoovr API accesses PostgreSQL.** No Cloudflare Worker or Container gets a PostgreSQL connection string or direct database connectivity. Cloudflare sends authenticated status/results to the API; the API validates and persists them.
3. **Keep image processing largely within Cloudflare.** Prefer Cloudflare R2, Queues and Cloudflare Containers for the CPU-based image pipeline if feasibility, operational requirements, cost, data location and model compatibility permit. Avoid transferring image bytes repeatedly between Cloudflare and Azure just to scan or resize them.
4. **Separate incoming, quarantined, processed and deliverable states.** Never make an upload available for public/member delivery merely because it exists in R2 or has been resized.
5. **Moderation decisions belong to Swoovr policy.** Automated models report findings and confidence; the application determines whether content is approved, rejected, quarantined or sent for restricted manual review, subject to existing safeguards.
6. **Keep responsibilities separate; do not create Workers merely for each HTTP verb.** Suggested logical roles are an upload ingress Worker, a delivery Worker and an optional queue/orchestration Worker. The third Worker is not mandatory if a suitable Queue consumer and Container coordination path makes it redundant.
7. **New deployment, no legacy-image migration required.** Validate whether any existing features need compatibility behaviour, but do not plan a historic production image migration.
8. **Do not replace or weaken existing trust-and-safety controls.** Preserve upload validation, malware scanning, explicit-content policy, suspected-CSAM escalation, ownership checks, access revocation, deletion and audit obligations.

## Target interaction diagram (conceptual, not a prescribed implementation)

```text
Web / iOS / Android
       |
       | 1. Request upload authorisation; record pending image
       v
 Swoovr API <------------------------------ Cloudflare processing results
       |                                         (authenticated, idempotent)
       | owns permissions + state                          |
       v                                                   |
 PostgreSQL                                               |
       ^                                                   |
       |                                                   |
       | access grants / status decisions                  |
       |                                                   |
Clients -- upload bytes --> [Upload Worker]                |
                                |                          |
                                v                          |
                         [R2 Quarantine]                   |
                                | object-create event      |
                                v                          |
                         [Cloudflare Queue]                |
                                |                          |
                                v                          |
                [Optional Orchestration Worker]            |
                                |                          |
                                v                          |
                 [Cloudflare Container service] -----------+
                    |   |   |   |   |
                    |   |   |   |   +-- resize / WebP variants
                    |   |   |   +------ NSFW / nudity classifiers
                    |   |   +---------- known-hash matching integration
                    |   +-------------- malware checks (ClamAV)
                    +------------------ image validation / metadata handling
                                |
                         approved & authorised
                                v
                          [R2 Approved]
                                |
                                v
                          [Delivery Worker]
                                |
                                v
                        Web / iOS / Android

Deletion / changing profile photo / album access / report / moderation override:
Client or Admin -> Swoovr API -> authorised storage action / queued cleanup
                                -> database state and audit update
```

**Important interpretation:** The diagram shows intended responsibilities, not guaranteed Cloudflare products, deployment topology or a requirement to use precisely three Workers. A Queue notification describes an object event, not proof that the object is safe or that the upload is authorised. The R2-approved object should not be served until Swoovr API state and access policies permit it.

## Suggested ticket breakdown and sequence

| GH | Ticket | Title | Primary responsibility | Suggested dependency |
| --- | --- | --- | --- | --- |
| #260 | 1 | Architecture and Cloudflare feasibility | Confirm design, constraints, contracts and open decisions | First |
| #261 | 2 | R2 storage and upload lifecycle | Secure ingest, quarantine, pending records, events | 1 |
| #262 | 3 | Independent image scanning and moderation pipeline | Containers, scanning, classification, processing and review | 1, 2 |
| #263 | 4 | Secure image delivery and lifecycle operations | GET/HEAD, access, caching, PATCH/DELETE semantics | 1, 2, 3 |
| #264 | 5 | API integration, callbacks and idempotency | API-only database access, reliable event handling | 1; coordinate with 2–4 |
| #265 | 6 | Security, operations and release verification | Secrets, observability, cost, resilience, testing | 1–5 |
| #266 | 7 | Separate feature: optional unanswered-message limit | Messaging privacy setting discussed earlier | Independent |
| #267 | - | Main thread ticket | - | - |

Tickets 1–6 form the image infrastructure epic. **Ticket 7 is intentionally independent** because it was part of the wider conversation but is unrelated to Cloudflare image processing. It can be omitted from the image epic.

Tickets 1–6 form the image infrastructure epic. **Ticket 7 is intentionally independent** because it was part of the wider conversation but is unrelated to Cloudflare image processing. It can be omitted from the image epic.

# TICKET 1 — Plan and validate Cloudflare-based Swoovr image architecture

## Objective

Design and validate a Cloudflare-centred image architecture that keeps image upload, quarantine, analysis and delivery within Cloudflare where practical, while keeping the Swoovr API solely responsible for application state and PostgreSQL access.

## Outcomes / requirements

- Evaluate the proposed separation between **Upload Worker**, **Delivery Worker**, **R2 buckets**, **Cloudflare Queue**, and an **independent Cloudflare Container-based image processing service**.
- Decide whether a third **orchestration/Queue consumer Worker** adds real value or would simply duplicate an existing mechanism. **Three Workers is a possibility, not a fixed requirement.**
- Assess direct R2 access by the processing Container using documented supported mechanisms (for example R2 bindings mediated by Workers, or scoped S3-compatible API credentials). Use least privilege.
- Assess whether the selected Linux Docker images/models run within currently supported Cloudflare Container CPU, memory, disk, execution and concurrency constraints. No GPU assumption.
- Confirm Cloudflare-to-Azure API traffic is limited primarily to requests for authorisation and small metadata/status callbacks, rather than transfer of every image.
- Identify where upload authorisation, policy decisions, moderation state transitions, staff review, notification, image variants and delete decisions live.
- Define which flows require synchronous acknowledgement versus asynchronous completion.
- Document environments (development, UAT, production), infrastructure configuration, disaster recovery and deployment ownership.
- Verify Australian data-location and cross-border-processing expectations against actual Cloudflare R2 jurisdiction/location and Container execution behaviour. **Do not claim that using Cloudflare guarantees Australian processing or residency.**
- Review likely Cloudflare costs: Worker requests, Queues, R2 operations/storage, Container CPU/memory/runtime, logs, image variants and egress to clients.

## Suggested configuration topics (decide in planning)

- Two private R2 buckets as a starting point: `quarantine` and `approved` (exact names, lifecycle and region/jurisdiction to be determined). A separate restricted evidence location may be needed for sensitive cases.
- One upload ingress Worker and one delivery Worker; Queue consumer/orchestration only if justified.
- Separate worker/service identities and credentials, restricted to the relevant bucket, prefixes and operations.
- Container sizing based on measured memory/CPU for the real models and maximum permitted image dimensions; do not commit to an instance class before benchmarking.
- Private-by-default buckets, explicit CORS/origin handling, short-lived access permissions and carefully controlled caching.

## Open questions for Codex/Claude

1. What does the existing image upload, photo moderation, image transformation, private album and deletion implementation actually do?
2. Which moderation tools/images are currently in the repository and at what versions? Are NudeNet, `nsfw-image-detector`, ClamAV, Project Arachnid integration and DeepFace actually wired up, experimental or merely planned?
3. Is Cloudflare Containers available in the target account and region, and can the actual Docker images run there within limits?
4. Which image types (public profile, private album, chat attachments, verification/liveness submissions, support attachments) are in scope? Should identity documents/biometrics remain in a separate, more restricted workflow?
5. How will processing workloads avoid exposure of sensitive images to logs, diagnostics, caches and third-party services?
6. What happens if Cloudflare processing is unavailable? Is delayed processing acceptable, and is there a fallback that doesn't weaken quarantine rules?

## Acceptance / deliverables

- [ ] Architecture decision record and final diagrams.
- [ ] Current-code inventory, dependencies and gaps documented.
- [ ] Proposed infrastructure resources, permissions and configuration by environment.
- [ ] Cost, residency, performance, availability and security assumptions verified or explicitly marked open.
- [ ] Contract between Cloudflare and Swoovr API documented.
- [ ] Subtickets and implementation order agreed before major code changes.

# TICKET 2 — Introduce secure R2 image ingress and quarantine lifecycle

## Objective

Allow Swoovr clients to upload images into private Cloudflare R2 quarantine without exposing R2 credentials or bypassing the Swoovr API's ownership and policy decisions.

## Required behaviour

- Web, iOS and Android initiate an upload via the authenticated Swoovr API; the API authorises who may upload and creates/maintains a **pending image record**.
- The client receives a short-lived, narrowly scoped upload authorisation and submits bytes to a Cloudflare-managed ingress path.
- The ingress path only accepts an authorised upload, with appropriate size, object-key and content restrictions. Clients must not choose arbitrary bucket names, object keys or overwrite another member's files.
- Store initial bytes in private quarantine; block all member delivery from quarantine.
- Confirm successful storage and enqueue the next processing step using a reliable signal (for example an R2 object-create notification to Cloudflare Queues). Account for upload retries, abandoned pending records and duplicate notifications.
- Validate actual file signatures/decoding downstream as well as inexpensive request checks at ingress; do not trust file extensions or client-supplied MIME types.
- API owns all database writes and all relationships between account, mode, photo pool, album, privacy and moderation records.
- Upload completion must not imply approval.

## Method/ownership expectations

| Operation | Suggested owner | Meaning |
| --- | --- | --- |
| `POST` authorise upload | Swoovr API | Authenticate member, enforce quotas and policy, create upload intent |
| `PUT` image bytes | Upload Worker / approved Cloudflare upload path | Write authorised bytes to quarantine |
| R2 event / Queue | Cloudflare infrastructure | Signal a new/changed object exists, not that it is trustworthy |
| Upload status | Swoovr API | Record lifecycle changes and expose status to clients |

**Alternative to review:** Whether direct-to-R2 presigned upload is preferable to proxying bytes through a Worker. Evaluate security, platform support, upload size, observability and client complexity rather than assuming one approach.

## Testing / acceptance

- [ ] Valid web/iOS/Android upload reaches quarantine and is tracked as pending in API.
- [ ] Unauthenticated, expired, replayed and cross-member authorisations cannot be abused.
- [ ] Object keys and bucket selection cannot be manipulated to read/write other objects.
- [ ] Oversized, malformed, unsupported and incomplete uploads fail safely.
- [ ] Parallel upload requests cannot bypass tier-based image quotas.
- [ ] Duplicate events and upload retries do not create duplicate active images.
- [ ] Quarantine objects cannot be obtained through delivery URLs.
- [ ] Stale incomplete uploads are reconciled/cleaned up safely.

# TICKET 3 — Independent Cloudflare image scanning, content moderation and WebP processing

## Objective

Run image inspection and transformation in a service operationally independent from the main Swoovr API, preferably on Cloudflare Containers using existing CPU-capable Docker images where feasible. Keep the actual image bytes within Cloudflare R2/processing wherever practical.

## Required pipeline outcomes

- Consume a trusted job correlated to the API-created image record and its exact R2 object version/key. Treat R2 event data as untrusted metadata until checked.
- Retrieve the quarantined image using dedicated least-privilege credentials/bindings.
- Validate file type, signature, decoder safety, image dimensions, decompression-bomb risks, metadata and processing limits.
- Run existing **ClamAV** malware checks, preserving the previously selected pinned scanner image/digest if still approved after reassessment. Record scanner and signature versions.
- Run the selected nudity/NSFW classifiers (previously discussed: **NudeNet** and **nsfw-image-detector**) and capture model versions, outputs and confidence scores. Verify the actual repository implementations before committing to names or models.
- Integrate **authorised known-CSAM hash-matching** checks if credentials/access and terms permit (Project Arachnid PDQ was previously investigated). **Do not assume that a general NSFW classifier detects CSAM or that a negative known-hash match rules out CSAM.**
- Apply the correct Swoovr rules according to intended image usage and the member's required permissions/verification status. Public profiles must not expose explicit sexual content; eligible private-album content follows the separate ID-verification and SEM consent rules. Flag ambiguous cases for review instead of blindly approving.
- Hold suspected CSAM and other high-risk content in a restricted, auditable escalation path, with access controls, retention and reporting decisions reviewed against applicable obligations. Do not automatically copy sensitive evidence into normal approved storage, expose it to ordinary moderators, or create unnecessary copies.
- After all mandatory checks pass **and approval has been authorised**, generate the required pre-sized WebP variants, strip unneeded sensitive EXIF/metadata, and write them into private approved R2 storage. Final promotion must depend on an up-to-date policy decision, not solely a model score.
- Communicate status/result metadata to an authenticated API endpoint; the API validates it and updates PostgreSQL.
- Use explicit retry, timeout, dead-letter, poison-message and manual reprocessing/review rules. **Fail closed:** unscanned, scanning, errored, timed-out, unsupported and unresolved images remain unavailable.
- Keep raw and derived image lifecycle, retention and deletion expectations explicit.

## Proposed lifecycle vocabulary for review

```text
PendingUpload -> Quarantined -> Queued -> Scanning -> AwaitingDecision
                                                     |           |
                                          Approved/Published   ManualReview
                                                     |           |
                                                     |       Reject / Approve
                                                     v           v
                                                 Deliverable  terminal state

At any processing step: Failed / TimedOut / Unsupported / Suspicious
-> remain inaccessible; controlled retry or restricted review
```

Names must align with the existing domain model. Avoid implying that `Approved` automatically means every member is authorised to view a private photo.

## Questions to resolve

- What models are actually deployed, their licensing and runtime dependencies, and are their results acceptable across the supported content types?
- Can the entire workload run on Cloudflare Containers CPU without cold-start, memory or operational problems? Benchmark representative large images and failures.
- Is malware scanning in the same container as ML analysis operationally sensible, or should ClamAV be isolated due to signature updates/resource needs?
- How is external Project Arachnid access provisioned, authenticated, rate-limited and audited, if available?
- What is the policy for manual review, appeals, false positives and false negatives?
- Should images be transformed before model input (safe bounded decode) and again into final variants after approval?

## Testing / acceptance

- [ ] Clean acceptable image can progress from quarantine to approved variants and API approval state.
- [ ] Malware positive blocks release and records sufficient detection metadata.
- [ ] Explicit-content public-profile upload follows Swoovr restrictions.
- [ ] Eligible private-album content follows ID verification and receiver SEM controls.
- [ ] Known-hash match, suspicious content and uncertain classifier results enter appropriate restricted review flows.
- [ ] Scan timeout, crash, OOM, unavailable model, invalid decoder input or queue retry never releases an image.
- [ ] Duplicate/late events do not overwrite newer results or revive deleted/rejected images.
- [ ] Final variants are correctly sized, WebP encoded and stripped of unwanted metadata.
- [ ] Processing performance, resource consumption and recovery are measured on representative workloads.

# TICKET 4 — Secure R2 image delivery, revocation and deletion lifecycle

## Objective

Serve approved Swoovr image variants efficiently and securely through Cloudflare while keeping authorisation and state decisions in the Swoovr API.

## Required behaviour

- Use a dedicated delivery surface (typically a **Delivery Worker**) for member-facing `GET` and `HEAD` on approved variants.
- The API continues to control member access based on ownership, profile visibility, album membership/sharing, SEM preference, ID-verification requirements, blocks, restrictions and moderation status.
- The Worker should only deliver approved objects when a valid access decision is presented or can be checked safely. Prevent object-key enumeration and direct public bucket access.
- Decide on appropriate short-lived access tokens or API-issued signed URLs. Include safeguards against replay, token scope misuse, expiry confusion and leaks via logs/referrers.
- Define caching independently for public/eligible profile imagery versus private albums. Do not assume CDN caching is safe for all private media.
- Private access revocation should have a defined maximum effective delay; assess token expiry, cache keying, no-store behaviour and purge requirements.
- Support approved size variants without performing repeated heavy image conversion on every GET.
- When images are replaced, unassigned, rejected or deleted, the API controls status transitions and authorised cleanup of all associated R2 objects and cached variants.
- Do not treat changing a member's primary photo as a file-level HTTP `PATCH`; it is primarily an API/database relationship change.

## Responsibility table

| Operation | Suggested owner | Notes |
| --- | --- | --- |
| `GET` / `HEAD` image variant | Delivery Worker | Approved objects only, secure cache behaviour |
| `PATCH` profile photo order, main photo, visibility, album assignment | Swoovr API | Mutates metadata/business state, not image bytes |
| `DELETE` user image | Swoovr API initiates | Authorise deletion, prevent new delivery, arrange R2/variant cleanup |
| Storage `PUT`/`DELETE` | Restricted Cloudflare processing/cleanup service or authorised backend | Exact mechanism to be selected during design |

## Testing / acceptance

- [ ] Approved public-profile images load efficiently across web/iOS/Android.
- [ ] Private image access works only for authorised members and is denied after revoke/block/permission change within the defined revocation window.
- [ ] Quarantined, rejected, deleted or moderation-pending images never load.
- [ ] Guessing R2 keys or reusing another member's grant fails.
- [ ] Cache headers and CDN behaviour do not expose private images.
- [ ] `HEAD` does not leak unauthorised object existence or sensitive metadata.
- [ ] Replacing and deleting an image updates API state and removes/invalidates all affected variants according to the agreed lifecycle.
- [ ] Cleanup is retryable and idempotent; missing objects are handled safely.

# TICKET 5 — Swoovr API contracts, authenticated callbacks and idempotent event handling

## Objective

Make Swoovr API the **exclusive PostgreSQL gateway** and sole authority for application metadata, while allowing Cloudflare services to report processing outcomes reliably.

## Non-negotiable boundaries

- The API performs all PostgreSQL reads/writes, applies business rules, validates the current image state and records audit information.
- Cloudflare Workers and Containers **must not have PostgreSQL credentials or direct database access**.
- Cloudflare services may read/write R2 only with tightly scoped storage permissions; they do not directly grant profile/album access or mutate member/business records.
- Provide authenticated service-to-service API endpoints with suitable origin restrictions, credentials/rotations, replay controls and observability. Evaluate mTLS, signed requests, scoped service tokens or an equivalent approach appropriate to current architecture.
- Do not trust callback claims about image ownership, verification status or whether content is allowed without reconciling them with API records and the intended processing job.

## Example logical API interactions (illustrative, not prescribed routes)

```text
Client -> API: initiate image upload (member, purpose, size/type)
API -> Client: imageId + short-lived upload authorisation

Cloudflare -> API: upload completed / processing started (imageId, jobId, object ref)
Cloudflare -> API: processing outcome (eventId, imageId, jobId, result, versions)
API -> PostgreSQL: validate + transactionally update record/status and event receipt
API -> Cloudflare: acknowledgement or retryable error

Client -> API: request photo access or delete/change photo relationship
API -> Client/Cloudflare: authorised access decision or storage cleanup request
```

### Idempotency: why and how it must behave

Queue delivery and service-to-service callbacks can be repeated. Example: Cloudflare calls the API to report that an image was approved; the API commits the update but the response is lost; Cloudflare retries.

**Expected response:** The repeated, identical, successfully processed event is a successful **no-op** (`200 OK` or another agreed success code), not an error that triggers endless retries. A real processing/database failure must not be acknowledged as a success.

Suggested contract fields to assess:

```json
{
  "eventId": "unique-event-id",
  "eventType": "ImageProcessingCompleted",
  "imageId": "image-identifier",
  "jobId": "processing-job-identifier",
  "objectVersion": "R2-object-version-or-etag",
  "result": "ApprovedOrReviewOrRejected",
  "processedAt": "2026-11-08T00:00:00Z",
  "scannerVersions": {},
  "modelVersions": {},
  "findings": {}
}
```

Fields and privacy of the `findings` object must be designed before implementation. Never include raw image data, sensitive evidence or secrets in routine callback payloads/logs.

### Suggested database-side concept (not mandated schema)

A dedicated processed-events/inbox table could hold the unique event ID, processing job ID, image ID, canonical request digest, outcome, processed timestamp and retention metadata. PostgreSQL should enforce uniqueness; the API should insert the event receipt and apply the corresponding image-state update **inside one transaction**. Alternatives are acceptable if they deliver the same concurrency guarantees.

```text
Incoming event
     |
     v
Authenticate + validate payload / job / object identity
     |
     v
Event ID already committed?
   /      \
 yes       no
  |         |
Same       Transaction: record event ID + validate state transition
payload?   + update image status/audit
 /   \       |
yes  no      +-- commit succeeded -> success acknowledgement
 |    |      +-- failed -> rollback and return genuine error
200  reject /
no-op investigate
```

A repeated event ID with **different content** is not a harmless duplicate. Detect the mismatch, reject/escalate and audit it. Late callbacks from obsolete jobs must not revive a deleted image or overwrite the result of a newer scan.

### Response/retry guidance to evaluate

| Situation | Expected response category | Retry intent |
| --- | --- | --- |
| New valid event committed | `200 OK` / `204 No Content` | Stop |
| Exact event already committed | `200 OK` | Stop |
| Malformed/inconsistent payload | `400` or suitable permanent error | Do not blindly retry; alert/triage |
| Invalid service authentication | `401` / `403` | Do not retry unchanged credentials; alert |
| Stale/invalid state transition | `409` or explicit domain error | Reconcile; no automatic endless retry |
| Temporary API/database failure | `500` / `503` | Retry with bounded exponential backoff |

HTTP code choices are examples; ensure the actual Cloudflare consumer retry policy agrees with them. **Returning `200` means the caller may discard the event**, so only acknowledge after safe durable processing, including the duplicate-success case.

## Testing / acceptance

- [ ] No Worker or Container has a DB credential or can access PostgreSQL directly.
- [ ] Invalid/unsigned callbacks cannot modify image state.
- [ ] New event updates image state and event receipt atomically.
- [ ] Identical repeat is a successful no-op.
- [ ] Same event ID with altered payload is detected and not silently accepted.
- [ ] Parallel duplicate callbacks cannot double-apply side effects.
- [ ] Database rollback permits a valid retry after transient failure.
- [ ] Delayed events for superseded jobs/deleted images cannot publish files.
- [ ] Invalid status transitions and unrecognised images/jobs are handled safely.
- [ ] Callback failures are observable and recoverable without exposing private content.

# TICKET 6 — Security, reliability, rollout and operational verification

## Objective

Ensure the Cloudflare image pipeline is maintainable and safe to deploy to Swoovr UAT and then production, with strong boundaries around private and potentially illegal media.

## Planning / configuration review

- **Secrets and identities:** environment-specific service credentials, least privilege R2 access, API callback authentication, rotation, no secrets in clients, git or image metadata.
- **Infrastructure isolation:** dev/UAT/prod buckets, Queues, Workers/Containers and service identities should not cross-access one another.
- **Storage protection:** no public access to quarantine or restricted evidence; examine approved-bucket exposure independently of delivery authorization; object lifecycle and secure cleanup.
- **Content safety:** account for malicious image formats, size/dimension limits, antivirus signature refresh, decoder vulnerabilities, ML model supply-chain and container images pinned by digest.
- **Safety governance:** restricted handling/escalation for suspected CSAM; authorised hash providers; lawful reporting, evidence retention and review permissions. Do not infer CSAM merely from an NSFW score.
- **Privacy:** EXIF removal, retention periods, access logs, image backups, encrypted transport, whether identity verification files require a separate data class.
- **Availability:** queue retry limits, dead-letter queues, manual replay/recovery, stuck jobs, cold starts, partial writes, failure cleanup and idempotent reprocessing.
- **Operations:** processing time, failure rates, scan coverage, model versions, moderation outcomes, callback latency and errors, R2 operations, Queue depth, Container CPU/memory and costs. Avoid logging actual image contents or unredacted sensitive object URLs.
- **Delivery performance:** CDN cache policies, token expiry, private media revocation, mobile latency, size variants and bandwidth.
- **Deployment:** documented environment variables and Cloudflare resources (no real credentials in repo), rollback, UAT test matrix and staged release.
- **Policy consistency:** adult-content permissions require approved ID status and the established SEM rules. Validate photo pool limits, per-mode assignment and private albums against current code and decisions.

## Integration acceptance matrix

| Scenario | Expected outcome |
| --- | --- |
| Safe image | Scanned, classified, processed, approved, metadata committed, then retrievable when authorised |
| Explicit public profile upload | Blocked/reviewed according to current public profile policy |
| Eligible private album upload | Permitted only through applicable ID/SEM and moderation rules |
| Suspicious/known-CSAM match | Restricted, non-deliverable, escalated safely |
| Malware detected | Non-deliverable with auditable result |
| Model offline / ClamAV error / decoder crash | Non-deliverable; retry or manual intervention |
| API callback timeout followed by retry | Single durable state change; repeat acknowledged safely |
| User deletes image during scan | Old result cannot republish; cleanup completes safely |
| Private album access revoked | No new authorised delivery; cache/token rules enforce agreed expiry |
| Queue duplicate, out-of-order and poison messages | No unintended release, robust retries and alerting |
| UAT load / large images | Acceptable latency, memory, concurrency and cost demonstrated |

## Acceptance

- [ ] Security review completed with findings resolved or documented.
- [ ] UAT tests cover success, abuse, failure, concurrency, retries and cleanup.
- [ ] Monitoring, alerting and recovery runbooks exist.
- [ ] Cost limits, retention, residency assumptions and operational owners documented.
- [ ] Production rollout has explicit rollback and fail-closed behaviour.

# TICKET 7 (INDEPENDENT) — Optional profile-wide unanswered-message limit

> This ticket covers the earlier messaging discussion. **It is unrelated to the Cloudflare image epic** and should be raised separately if not already tracked.

## Objective

Give members a voluntary messaging boundary to reduce repeated unanswered messages without forcing a member to block or disconnect someone, while preserving normal adult communication.

## Key decision: OFF by default

**Default for all new and existing accounts: Off (unlimited).** A member may enable the profile-wide setting and select **3, 5 or 10** consecutive unanswered messages. Do **not** set the default to 5; earlier draft language suggesting that was superseded by the explicit decision to default **Off**.

## Requirements

- One **global profile/privacy setting per recipient**; it applies to that member's incoming messages across all one-to-one conversations. **No per-conversation overrides**, no relationship-based algorithm and no time-based reset.
- If Member B enables a limit of 5, Member A may send five consecutive messages to B without a response. The sixth is rejected until B replies at least once, even if the five messages were sent over weeks or years.
- The converse direction is assessed independently using Member A's own chosen receiving limit.
- Any successful recipient reply—even a one-word reply—resets the other member's consecutive unanswered-message count for that conversation.
- Opening, reading, marking as unread, editing or deleting messages does not reset the counter. Message attachment types count in a consistent way; system messages, typing/read receipts do not.
- Changing a recipient's profile setting takes effect immediately, including on existing conversations. Disabling the limit permits messaging subject to all other access and safety rules; re-enabling evaluates the existing unanswered sequence.
- The Swoovr API enforces it atomically across web, iOS and Android; clients show a neutral message such as: "You've reached the unanswered message limit for this conversation. You can send more when they reply."
- Don't disclose the exact recipient preference unless there is an established UX reason; don't pressure the recipient to reply.
- Preserve reporting, blocking, disconnecting and existing membership-tier restrictions; this is a safety/comfort feature available to **Free and paid members**.

## Tests / acceptance

- [ ] Default is Off for new and existing members, with no messaging restriction imposed by this feature.
- [ ] Off / 3 / 5 / 10 settings persist across devices and sessions.
- [ ] Message at threshold succeeds; next unanswered message is rejected.
- [ ] Reply resets allowance; read receipt and elapsed time do not.
- [ ] Text, media and supported attachments count; system messages do not.
- [ ] Independent counts in each direction and each conversation.
- [ ] Edit/delete/clock-wait workarounds fail.
- [ ] Changing 10 to 3 immediately restricts someone with 5 unanswered messages; turning Off permits messages again.
- [ ] Re-enabling does not inadvertently wipe unanswered history.
- [ ] Concurrent sends cannot bypass limit; rejected sends do not become persisted messages.
- [ ] Web/iOS/Android provide consistent sender feedback.

## Clarifications before implementation

- Determine how the current data model represents conversations, participants and message deletion/editing, then select a method that reliably counts the consecutive sequence without expensive per-send queries.
- Define whether group conversations, reaction-only events and support/staff/system messages are out of scope. The intended initial target is member-to-member one-to-one chat.
- Avoid modifying existing messaging tier limits without review.

# Implementation coordination notes

- Do not combine all issues into one large PR. Ticket 1 should yield a reviewed plan, then tickets 2–6 should be delivered in small, testable stages; ticket 7 can be scheduled independently.
- Treat exact service names, endpoint paths, database schema, Docker layout, Queue strategy and implementation framework as **design choices** for Codex/Claude to evaluate against the actual repository.
- Do not provision resources, commit secrets, change Cloudflare/Azure billing, or enable production public access without explicit approval.
- Surface contradictions with prior Swoovr decisions rather than silently choosing an interpretation.

## Official Cloudflare references (checked 8 Nov 2026)

- Cloudflare R2 event notifications and Queue consumption: https://developers.cloudflare.com/r2/buckets/event-notifications/
- Cloudflare Containers getting started (Docker image and Worker integration): https://developers.cloudflare.com/containers/get-started/
- Cloudflare Containers limits and instance sizes: https://developers.cloudflare.com/containers/platform/limits/
- Cloudflare Containers pricing: https://developers.cloudflare.com/containers/platform/pricing/
- Cloudflare Container-to-Worker bindings: https://developers.cloudflare.com/containers/configuration/workers-connections/
- R2 Workers API: https://developers.cloudflare.com/r2/api/workers/workers-api-reference/
- Python Worker package compatibility: https://developers.cloudflare.com/workers/languages/python/packages/

**Verification note:** Cloudflare provides Containers for Docker-based workloads, and R2 event notifications can feed Cloudflare Queues. This establishes architectural feasibility, **not** that specific Swoovr Python models, ClamAV, third-party CSAM matching, resource budgets or residency requirements have been tested and approved on the actual account.
