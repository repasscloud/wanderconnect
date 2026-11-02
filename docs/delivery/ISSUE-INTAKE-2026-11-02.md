# Intake investigation — 2 November 2026

Scope: intake, read-only investigation, issue tracking and documentation only, directly on `main` as explicitly requested. No application fixes, branch/worktree/PR, merge, deployment or Actions enablement. No confirmed decision or design source changed.

## Baseline and discovery

- Repository `/Users/danijeljw/Developer/wanderconnect-brief`, branch `main`, remote `origin` = [repasscloud repository](https://github.com/repasscloud/wanderconnect), using SSH.
- Initial and fetched baseline `53ebacd9dd73e6c7c9cd4fd5a0fbb3f6fafab21e`; working tree clean; `HEAD...origin/main` was `0 0`. No fast-forward was needed, no forcing/reset/stash/cleanup performed. Owner additions were already committed, not uncommitted.
- Commit `53ebacd` adds reports 18–25; comparison of the earlier reports against its parent found only blank lines/table-separator changes, not substantive report amendments. Processing metadata was stale after merges, and Q98 additionally receives new item 20 styling. Q95 also receives the owner's existing successful-lookup feedback from #137.
- Existing maximum Q109 confirmed from QUESTIONS and existing issue titles. Complete open/closed issue inventory inspected, including related closed #25/#48/#73/#116/#118 and open #33/#50/#83–#91/#137–#151. Closed issues were not reopened; no issue was closed.
- Connected GitHub search returned no open PRs. Live PR metadata confirms #152 merged at 2026-11-02T01:28:43Z (`98a1fa7`), #153 at 01:29:04Z (`b142cec`), #154 at 01:29:41Z (`f5ee1d6`); their merge commits are in this main baseline. Historical per-PR evidence is not fresh runtime reproduction of the new reports.
- Owner comment on #137 at 2026-11-02T03:49:09Z confirms account search now works, expresses uncertainty about the original cause and asks to mark resolved on a next PR. This task keeps it open and does not invent a cause.

## Dispositions and split boundaries

| Intake | Q | GitHub issue | Disposition | Scope / dependency |
| --- | --- | --- | --- | --- |
| [18](ISSUE-INTAKE.md#intake-18) | [Q110](QUESTIONS.md#q110) | [#155](https://github.com/repasscloud/wanderconnect/issues/155) | Created | Entire item 18; registered-name search also depends on Q117. |
| [19](ISSUE-INTAKE.md#intake-19) | [Q111](QUESTIONS.md#q111) | [#156](https://github.com/repasscloud/wanderconnect/issues/156) | Created | Entire item 19; independent search, appeals, DOB/name and shared presentation work is cross-linked. |
| [20](ISSUE-INTAKE.md#intake-20) | [Q112](QUESTIONS.md#q112) | [#157](https://github.com/repasscloud/wanderconnect/issues/157) | Created | Item 20 A–F and registration criteria only. Visual refinements G–L reuse Q98/#140. |
| [21](ISSUE-INTAKE.md#intake-21) | [Q113](QUESTIONS.md#q113) | [#158](https://github.com/repasscloud/wanderconnect/issues/158) | Created | Entire item 21, including the additional approve-form report. |
| [22](ISSUE-INTAKE.md#intake-22) | [Q114](QUESTIONS.md#q114) | [#159](https://github.com/repasscloud/wanderconnect/issues/159) | Created | Item 22 A–K/T, queue/history/interface acceptance; L–S split to Q115. |
| [22](ISSUE-INTAKE.md#intake-22) | [Q115](QUESTIONS.md#q115) | [#160](https://github.com/repasscloud/wanderconnect/issues/160) | Created | Item 22 L–S and rejected-photo/member-notification/appeal acceptance. |
| [23](ISSUE-INTAKE.md#intake-23) | [Q116](QUESTIONS.md#q116) | [#161](https://github.com/repasscloud/wanderconnect/issues/161) | Created | Entire item 23; linked follow-up to #33 rather than a duplicate of verified-DOB mismatch handling. |
| [24](ISSUE-INTAKE.md#intake-24) | [Q117](QUESTIONS.md#q117) | [#162](https://github.com/repasscloud/wanderconnect/issues/162) | Created | Item 24 except independent mode-specific public-name overrides; links Q110 search, Q111 details and Q116 DOB. |
| [24](ISSUE-INTAKE.md#intake-24) | [Q118](QUESTIONS.md#q118) | [#163](https://github.com/repasscloud/wanderconnect/issues/163) | Created | Item 24 D and mode-name portions of B/E/L/M and public-profile acceptance. |
| [25](ISSUE-INTAKE.md#intake-25) | [Q119](QUESTIONS.md#q119) | [#164](https://github.com/repasscloud/wanderconnect/issues/164) | Created | Item 25 A–I/M–O and shared presentation/security acceptance; J–L split to Q120. |
| [25](ISSUE-INTAKE.md#intake-25) | [Q120](QUESTIONS.md#q120) | [#165](https://github.com/repasscloud/wanderconnect/issues/165) | Created | Item 25 J–L and responsive availability criteria; conflicts with item 22 D recorded explicitly. |
| [20](ISSUE-INTAKE.md#intake-20) | [Q98](QUESTIONS.md#q98) | [#140](https://github.com/repasscloud/wanderconnect/issues/140) | Updated / duplicate visual scope | G–L and Beta layout acceptance reuse the existing table issue; policy A–F is Q112. |

## Evidence boundaries and requirements

All new reports were read in full. Findings below are source-supported; the reported UI behavior is not independently reproduced. Safari stale POST replay, paragraph-margin effects and throughput are unverified hypotheses. No screenshot was opened, copied, retrieved from history, uploaded or staged. The six retained sensitive screenshots remain untouched. New report image embeds were converted to textual references, correcting the directory mismatch for items 21–25 to their already-tracked filenames. Original captions and report claims remain attributed user descriptions, not visual findings or publication clearance. No new evidence publication approval is claimed.

D158 explicitly permits validated unverified DOB changes; the universal first-save lock needs an explicit supersession. D166's provider mismatch correction, non-punitive handling and ID Verified distinction remain. D175 currently shares display name and main photo; per-mode names are not an approved exception. D191 protected verified identity must never be reused as ordinary registered-name data. New four-hour safety acknowledgement and two registration flags are proposals, not existing confirmed controls. Current routine media queue is Admin-only; the broader Support suggestion needs a reviewed capability decision. Root MFA/hosted authentication exceptions and Legal separation remain as specified; no new global MFA policy is introduced.

Adult-media policy, independent main photos/photo limits, onboarding order and legal-draft approval remain deferred. Existing issue/Q dependencies were updated without blocking independent investigations. Reported “Core decisions” and checklists do not silently amend DECISIONS.md.

## Q110: Extend Admin account lookup with phone search and permission-scoped account browsing

Area: Admin / account search. Priority: High — account lookup is central to support and investigation; bulk personal-data access needs explicit scoping.

Accounts.razor uses an exact-email GET form and a basic result list. AdminEndpoints.cs normalises the verified email, left-joins profiles, returns at most 10 matches and audits a result count. It has no phone lookup or browse/filter contract. PR #152 merged the refusal/no-match correction; the owner confirmed lookup works on #137. This is an extension, not a recurrence of Q95.

CONFIRMED D38/D55 issuer/subject account identity and D185 least privilege remain. DEFAULT: reuse existing phone normalisation, account routes and pagination conventions. OPEN: demographic-filter purposes/field matrix and name-search authority (Q117); no broad support access is inferred.

Sources:

- [src/Swoovr.Admin/Components/Pages/Accounts.razor](../../src/Swoovr.Admin/Components/Pages/Accounts.razor)
- [src/Swoovr.Api/Accounts/AdminEndpoints.cs](../../src/Swoovr.Api/Accounts/AdminEndpoints.cs)
- [src/Swoovr.Api/Accounts/PhoneEndpoints.cs](../../src/Swoovr.Api/Accounts/PhoneEndpoints.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Known-country phone formats resolve through existing canonical phone semantics; ambiguous international input is refused rather than guessed.
- [ ] Quick result summaries distinguish membership tier/status, account standing and assurance; mask phone data unless authorised.
- [ ] Browse/filter/sort/page/count happens server-side; include incomplete profiles and preserve return state; never fetch every account or introduce unrestricted exports.
- [ ] Enforce field/filter/purpose permissions in responses, not hidden columns; test anonymous and each staff capability, enumeration limits, literal filters and large synthetic datasets.

## Q111: Organise Admin account details and add scoped profile inspection

Area: Admin / account investigation. Priority: High — misleading age labelling and fragmented account investigation affect safety operations.

Actual route is /accounts/{Id:guid}. Account.razor labels AgeStatus as Age and renders raw wire values, decision IDs, Plus grants and two staff-badge actions. ModerationEndpoints.SummaryAsync returns no numeric age/full DOB, but already supplies sanctions, entitlements, trips/review flags and an identity-on-file marker. D191 deliberately keeps decrypted verified identity separate. Existing case-scoped profile-at-report/evidence surfaces must be reused, not replaced by unrestricted chat inspection.

CONFIRMED D134 affiliation grants no permissions, D175 shared display name/main photo, D185/D191 restricted identity and D197 elevated access remain. DEFAULT: reuse existing capabilities and case links. OPEN: new sensitive-field inspection/preview scopes; per-mode main photos remain deferred under Q108.

Sources:

- [src/Swoovr.Admin/Components/Pages/Account.razor](../../src/Swoovr.Admin/Components/Pages/Account.razor)
- [src/Swoovr.Api/Moderation/ModerationEndpoints.cs](../../src/Swoovr.Api/Moderation/ModerationEndpoints.cs)
- [src/Swoovr.Api/Verification/IdentityEndpoints.cs](../../src/Swoovr.Api/Verification/IdentityEndpoints.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Separate calculated age, declared DOB, DOB verification and age assurance; show missing values honestly; full DOB requires a reviewed field-access policy.
- [ ] Use sections/tabs and readable labels; remove internal D codes; preserve grant source/history and distinguish complimentary entitlement from paid subscription.
- [ ] Read-only previews create no impersonation, profile visits or account mutations; hidden configuration and private photos require independent authority.
- [ ] Preserve Support ordinary-case/30-day limits, Admin 90-day suspension/termination rules, conflict exclusions and elevated termination checks; Legal has no implicit moderation authority and Root no implicit Legal access.
- [ ] Test every section/action/response across Support/Admin/Legal/Root and inspect audit/history accessibility with synthetic fixtures.

## Q112: Define authoritative beta/public/closed registration controls across Keycloak and applications

Area: Registration / Admin configuration / identity provider. Priority: High — registration closure must cover identity-provider creation as well as application access.

AccountResolver reads static Beta:Enabled (default true) for application access. The generated member realm separately has registrationAllowed=true. D237 explicitly distinguishes Keycloak user creation from application registration/adoption; the beta access check is not evidence that Keycloak creation is closed. No persisted two-flag Root-managed policy was found in these sources. PR #153 merged guest registration navigation but did not add this policy.

CONFIRMED D62 invitation-only beta, D127 age/email/mobile gates and Keycloak ownership remain. OPEN: approve the proposed four-state model, Root-only mutation/view matrix, shared authority and supported provider enforcement/propagation contract before implementation. DEFAULT: fail closed on unavailable authority. No public launch or provider configuration change is authorised.

Sources:

- [src/Swoovr.Api/Accounts/AccountResolver.cs](../../src/Swoovr.Api/Accounts/AccountResolver.cs)
- [scripts/keycloak_realms.py](../../scripts/keycloak_realms.py)
- [containers/keycloak/realms/swoovr-members-dev.json](../../containers/keycloak/realms/swoovr-members-dev.json)

Proposed acceptance (full original detail remains in the intake):

- [ ] Validate all four proposed flag combinations: beta-only, closed, public-only, public with beta tracking; never open silently when policy retrieval fails.
- [ ] Enforce before identity-provider account creation, including direct registration/brokered/alternative entry paths as applicable; separately protect application registration/adoption.
- [ ] Only reviewed capabilities may modify policy, with reason/confirmation, previous/new values, UTC audit and safe concurrent revision handling.
- [ ] Policy survives restarts and is consistent across instances; navigation/closed-state pages match backend policy without disrupting current sessions, password recovery or historical enrolments.

## Q113: Investigate Safari Admin refresh replaying missing named SSR forms

Area: Admin / Safari / SSR forms. Priority: Medium — intermittent refresh blocks the route, with a reported recovery through the root page.

User reports both missing form 'search' and a removed approve-{mediaId} form after refreshing /media. MediaReview.razor renders dynamic item-bound POST form names; successful decisions reload a queue in which that item may disappear. ContactHistory.razor contains the separate POST form 'search'; Accounts/Beta search uses GET. Program.cs registers static SSR and enables exception handling only outside Development. These facts support investigating stale POST replay but do not establish the request method, route or cause.

DEFAULT hypothesis: Safari refresh may replay an SSR POST whose named form no longer exists. OPEN cause: request/response trace is absent; not proven to be a session-expiry defect. Keep dynamic form identity/antiforgery and avoid navigation rewrites without evidence.

Sources:

- [src/Swoovr.Admin/Components/Pages/MediaReview.razor](../../src/Swoovr.Admin/Components/Pages/MediaReview.razor)
- [src/Swoovr.Admin/Components/Pages/ContactHistory.razor](../../src/Swoovr.Admin/Components/Pages/ContactHistory.razor)
- [src/Swoovr.Admin/Program.cs](../../src/Swoovr.Admin/Program.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Capture Safari version, exact app revision, route, last action, request method/status and navigation chain using fictional fixtures; compare GET refresh and POST refresh after a queue item disappears.
- [ ] Verify approve/reject/reveal/search, back/forward and multi-tab/concurrent-review paths in Safari and one comparison browser.
- [ ] No duplicated moderation action, CSRF bypass or weakened session gate; existing search and provider renewal remain.
- [ ] Handle framework refusal and genuine exceptions with safe recovery without suppressing the underlying defect or exposing raw internal form details.

## Q114: Provide a compact photo-review queue, focused review controls and scoped workload history

Area: Admin / media moderation workspace. Priority: High — current inline review design limits operational throughput; no measured throughput claim is available.

MediaReview.razor renders inline list items and approve/reject/escalate details, separate reveal/conceal forms and a 30-day coded-count expander. MediaEndpoints review selects up to 100 uncertain items, with a separate pending-screening count, not a paginated workload query. Home.razor shows case/appeal/hold counts but no photo-review total. PR #152 merged factual purpose, audited reveals and stable decision codes. No server-side four-hour/versioned safety acknowledgement was found in the inspected review path.

CONFIRMED D178 screening/eligibility and restricted evidence remain. DEFAULT: reuse coded controls and existing lifecycle locks. OPEN: four-hour acknowledgement and governed private approval-note history (Q97). Support access is not implied: the current general media queue requires Admin. Retention and appeals are split to Q115.

Sources:

- [src/Swoovr.Admin/Components/Pages/MediaReview.razor](../../src/Swoovr.Admin/Components/Pages/MediaReview.razor)
- [src/Swoovr.Admin/Components/Pages/Home.razor](../../src/Swoovr.Admin/Components/Pages/Home.razor)
- [src/Swoovr.Api/Media/MediaEndpoints.cs](../../src/Swoovr.Api/Media/MediaEndpoints.cs)
- [docs/architecture/MEDIA-REVIEW-REASONS.md](../../docs/architecture/MEDIA-REVIEW-REASONS.md)

Proposed acceptance (full original detail remains in the intake):

- [ ] Map item 22 A–K/T to compact queue and focus-safe modal or equivalent; conceal on close/advance, deliberately audit each reveal and preserve keyboard operation.
- [ ] Separate pending screening, manual review, specialist and appeal counts; aggregate only authorised rows and use actual backend counts.
- [ ] Preserve reason-code binding, Other-note requirements, concurrency/stale-state refusal and specialist escalation; do not infer approval-note retention support.
- [ ] Decide notice version/acknowledgement policy before adding server-side expiry; proposed four-hour acknowledgement is new feedback, not an existing confirmed rule.
- [ ] Exercise synthetic high-volume paging, simultaneous reviewers, stale form recovery, keyboard focus and viewport/zoom accessibility; do not claim hundreds/hour without measurement.

## Q115: Define photo rejection notices, appeals and retention-safe reconsideration

Area: Member photos / notifications / moderation appeals. Priority: High — members need safe rejection transparency; reconsideration cannot rely on objects already deleted.

MediaPipeline.RejectAsync records rejection and invokes DeleteObjectIfAllowedAsync; rejected objects can be deleted when preservation permits. MediaEndpoints review/view handles uncertain items; ordinary decisions accept uncertain/passed states, not a general rejected-photo override. Existing sanction appeals (D135/#50) do not themselves implement photo-specific appeals. The current review code does not establish member rejection notifications or durable photo-appeal history.

CONFIRMED D135 sanction appeal independence/30-day window must be reconciled explicitly for photo decisions, not silently generalised or weakened. CONFIRMED D178/D185/D191 evidence restrictions remain. OPEN: photo-specific eligibility/window/finality, reviewer capability and bounded retention; no blanket retention or ordinary approval of specialist-blocked media.

Sources:

- [src/Swoovr.Api/Media/MediaPipeline.cs](../../src/Swoovr.Api/Media/MediaPipeline.cs)
- [src/Swoovr.Api/Media/MediaEndpoints.cs](../../src/Swoovr.Api/Media/MediaEndpoints.cs)
- [src/Swoovr.Domain/Media/MediaReviewReasons.cs](../../src/Swoovr.Domain/Media/MediaReviewReasons.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Approve a bounded rejected-object/review-note retention and disposal design before promising a reviewable queue; distinguish unavailable historical media from a live appealable object.
- [ ] Map codes to safe member explanations; omit screening scores, internal notes, reporter identity and protected evidence; deduplicate durable notices.
- [ ] Keep rejected photos hidden while appealed, prevent duplicate/replayed appeals, retain prior outcomes and enforce independently authorised reviewers and finality rules.
- [ ] Overrides record old/new decision, actor/reason/case and revalidate current screening, holds, specialist blocks, visibility and account eligibility; never erase earlier decisions.
- [ ] Test expiry, source deletion, legal holds, concurrent overrides, notification retries and role/conflict refusals using generated synthetic media.

## Q116: Review universal DOB locking and temporary Admin-authorised member correction

Area: Member account / age integrity / Admin correction. Priority: High — changes affect adult eligibility, verification and sensitive historical identity data.

MemberAccountPanel keeps an editable DOB form when DobCheck is none, even for verified DOB (with explanatory text). AccountEndpoints /me/age rejects verified-date changes and open correction/review states, but permits validated unverified-date changes; underage entries lock the account. That is consistent with CONFIRMED D158, not proof of a backend bypass. D166 already provides a one-use provider-mismatch correction and preserves ID Verified during manual DOB review. #33 covers that mismatch, not universal first-save locking.

OPEN: universal locking supersedes CONFIRMED D158 allowing unverified changes; blanket re-verification/badge changes must reconcile D166. Historical DOB retention/access needs D191/D185 review. DEFAULT: 24-hour single-use grant is only a proposal. Onboarding order remains deferred. No existing issue is reopened or closed.

Sources:

- [src/Swoovr.Web.Client/Account/MemberAccountPanel.razor](../../src/Swoovr.Web.Client/Account/MemberAccountPanel.razor)
- [src/Swoovr.Api/Accounts/AccountEndpoints.cs](../../src/Swoovr.Api/Accounts/AccountEndpoints.cs)
- [src/Swoovr.Api/Verification/VerificationEndpoints.cs](../../src/Swoovr.Api/Verification/VerificationEndpoints.cs)
- [src/Swoovr.Domain/Verification/DobCorrection.cs](../../src/Swoovr.Domain/Verification/DobCorrection.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Resolve D158/D166 conflicts before changing policy; preserve required private DOB, hidden-age filtering, existing provider-mismatch prompt and no punitive mismatch assumption.
- [ ] Proposed correction grant expires (24 hours is DEFAULT proposal), is revocable and consumed atomically with one confirmed valid update; refuse wrong-member, expired, replay and concurrent use.
- [ ] Recalculate eligibility/discovery and restrict underage access; explicitly decide verification reassessment rather than silently revoking/retaining badges contrary to D166.
- [ ] Existing populated DOB values are preserved and migration does not create a free-change window; no historical dates enter ordinary audit/logs.
- [ ] Test initial confirmation, UI/server parity, locked and unverified states, request/support routing, grants/expiry, migration and verification/underage handling across web/native.

## Q117: Define registered-name collection and preserve separation from public and verified identity

Area: Keycloak registration / account identity / Admin / verification. Priority: High — name collection, staff visibility and migration change privacy and identity semantics.

scripts/keycloak_realms.py generates required firstName/email but no lastName in the member user profile; register.ftl renders those dynamic fields. Account stores issuer/subject and accepted email, with no registered FirstName/LastName fields. MemberProfile has a separate nullable DisplayName; AccountResolver does not initialise it from provider names. D191 already stores encrypted verified names behind Legal-only access, which must not become ordinary Admin registered-name data.

OPEN: required surname, registered-name authority/field matrix, provider comparison and existing-account completion policy. Item 24's 'Core product decisions' are report proposals, not amendments to the confirmed register. DEFAULT: keep custom public names and declared/verified identity separate. Per-mode names are split to Q118; mandatory verification is not authorised.

Sources:

- [scripts/keycloak_realms.py](../../scripts/keycloak_realms.py)
- [containers/keycloak/themes/swoovr/login/register.ftl](../../containers/keycloak/themes/swoovr/login/register.ftl)
- [src/Swoovr.Domain/Accounts/Account.cs](../../src/Swoovr.Domain/Accounts/Account.cs)
- [src/Swoovr.Domain/Accounts/MemberProfile.cs](../../src/Swoovr.Domain/Accounts/MemberProfile.cs)
- [src/Swoovr.Api/Accounts/AccountResolver.cs](../../src/Swoovr.Api/Accounts/AccountResolver.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Define authoritative Keycloak/application ownership and validated claim/synchronisation paths including social registration; preserve issuer/subject linkage, not email-based identity merging.
- [ ] Validate Unicode/international names server-side, preserve form values on errors and define support for people without a two-part name.
- [ ] Initial public-name mapping occurs only on creation/unset state with explicit privacy semantics; never overwrite customised names or expose surnames by fallback.
- [ ] Admin name search/presentation returns only permitted self-declared fields; never decrypt D191 identity for ordinary listings or label declared names verified.
- [ ] Define provider-supported matching/variation handling and post-verification correction; preserve optional ID verification and never silently overwrite either identity.
- [ ] Migration preserves existing names, requests missing data safely, invents no surnames and retains existing public names; test registration/error/social, sync conflicts and disclosure boundaries.

## Q118: Decide optional per-mode public display-name overrides and safe fallback

Area: Profile / mode privacy. Priority: Medium — independent product/data-model change; shared display name is the current confirmed rule.

MemberProfile owns DisplayName; MemberMode stores Enabled/Visible/About/ShareCoreAbout and intentions, with no per-mode name. CONFIRMED D175 explicitly shares display name across all modes. PR #154 preserved custom names and did not implement provider-name initialisation or per-mode overrides.

OPEN: approval to replace shared D175 names, fallback/edit semantics and required migrations/projections. DEFAULT: no automatic override creation or overwriting. Registered-name collection/initialisation is separate Q117; independent main photos remain deferred Q108.

Sources:

- [src/Swoovr.Domain/Accounts/MemberProfile.cs](../../src/Swoovr.Domain/Accounts/MemberProfile.cs)
- [src/Swoovr.Domain/Discovery/ConnectionModes.cs](../../src/Swoovr.Domain/Discovery/ConnectionModes.cs)
- [src/Swoovr.Api/Profiles/ProfileEndpoints.cs](../../src/Swoovr.Api/Profiles/ProfileEndpoints.cs)
- [src/Swoovr.Api/Profiles/ProfilePrivacy.cs](../../src/Swoovr.Api/Profiles/ProfilePrivacy.cs)

Proposed acceptance (full original detail remains in the intake):

- [ ] Record an explicit D175 supersession before implementation; preserve one underlying account and cross-account safety/ban rules.
- [ ] Mode override changes affect only the selected mode; clearing it uses main public DisplayName and never legal/registered full name.
- [ ] Preserve customised existing names, mode visibility, related-pair privacy and historical profile-at-report snapshots.
- [ ] Test member/profile/discovery/chat projections, exports/deletion and safe migration across all modes; retain existing length/content validation.

## Q119: Standardise Admin presentation and contextual guidance across routes

Area: Admin / shared presentation / accessibility. Priority: High — repeated development prose and inconsistent controls affect every operational workflow.

SupportLookup.razor contains the quoted #50/permission/logging paragraph. Account.razor exposes D-code headings; MediaReview.razor includes beta and implementation guidance. App.razor loads themes.css/admin.css, not a Bootstrap bundle; admin.css already provides shared controls, responsive layout and top-aligned td. The report's 'existing Bootstrap' premise is not established by the current shell. Functional beta/provider/safety distinctions remain operational facts.

CONFIRMED docs/design is read-only, Standard staff styling and D185/D197 audit/security remain. DEFAULT: reuse current CSS/components; framework adoption needs a deliberate dependency choice. OPEN: any removal of required disclosures requires owning policy review; legal-draft approval remains deferred. Device availability is separately Q120.

Sources:

- [src/Swoovr.Admin/Components/Pages/SupportLookup.razor](../../src/Swoovr.Admin/Components/Pages/SupportLookup.razor)
- [src/Swoovr.Admin/Components/Pages/Account.razor](../../src/Swoovr.Admin/Components/Pages/Account.razor)
- [src/Swoovr.Admin/Components/App.razor](../../src/Swoovr.Admin/Components/App.razor)
- [src/Swoovr.Admin/wwwroot/admin.css](../../src/Swoovr.Admin/wwwroot/admin.css)
- [docs/delivery/ACCESSIBILITY-RELEASE-GATE.md](../../docs/delivery/ACCESSIBILITY-RELEASE-GATE.md)

Proposed acceptance (full original detail remains in the intake):

- [ ] Produce a route inventory and intentional exception list covering actual pages, headings, controls, table spacing, errors and guidance; use shared components only where useful.
- [ ] Remove internal development identifiers while retaining member/case/audit references and truthful environment/provider/draft status.
- [ ] Retain mandatory legal/security acknowledgements, destructive confirmations and specialist guidance; audit remains unchanged independently of notice copy.
- [ ] Verify Standard tokens/light-dark applicability, focus, keyboard, screen-reader reading order, zoom/reflow and consistent control states across representative routes.
- [ ] Coordinate page-specific Q98/Q110/Q111/Q114 work rather than duplicating it; do not add a framework/library by assumption.

## Q120: Assess Admin workflow availability on desktop, tablet and mobile

Area: Admin / supported devices / access policy. Priority: Medium — new availability restrictions require operational and accessibility review before enforcement.

Current Admin shell/CSS adapts layouts to narrow viewports; no route-level supported-device matrix is established by the inspected shell. The proposed table labels media/legal/root routes desktop-only, while item 22 proposes an adapted mobile/tablet review dialog. This conflict requires a route decision. A resized viewport or user-agent is not evidence of a trusted device and cannot enforce sensitive backend access.

OPEN: route classifications, meaning of desktop-only and device trust/enforcement requirements; the report table is a proposed starting point. DEFAULT: preserve existing role permissions and accessible reflow while policy is unresolved. No device deny rule or new authentication requirement is approved by this intake.

Sources:

- [src/Swoovr.Admin/Components/App.razor](../../src/Swoovr.Admin/Components/App.razor)
- [src/Swoovr.Admin/wwwroot/admin.css](../../src/Swoovr.Admin/wwwroot/admin.css)
- [src/Swoovr.Admin/Program.cs](../../src/Swoovr.Admin/Program.cs)
- [docs/delivery/ACCESSIBILITY-RELEASE-GATE.md](../../docs/delivery/ACCESSIBILITY-RELEASE-GATE.md)

Proposed acceptance (full original detail remains in the intake):

- [ ] Inventory each actual route/action for desktop/tablet/mobile capability and document rationale, role scope and accessibility alternatives; reconcile item 22's mobile review proposal.
- [ ] If a true device restriction is required, define supported backend/device trust evidence and direct-request refusal; do not treat CSS, viewport or user-agent as a security boundary.
- [ ] Unsupported presentation exposes no protected response/content; authorised mobile overview/support/basic lookup remains usable as agreed.
- [ ] Verify actual Safari/iOS/Android/tablet and desktop/zoom behavior; no resize-based lockout of essential accessible operations.

## Validation and publication

Initial `./scripts/check.sh` exited 1 after naming and 33 backup tests passed: the Markdown destination check found the item 21 screenshot directory mismatch. This was a documentation path error, not a database failure. References were corrected without opening or moving any evidence. A second run stopped at the naming gate because this new record used the legacy repository name in an SSH URL; the record now links the actual GitHub repository using the supported URL form. Final full `./scripts/check.sh` passed (exit 0): naming, 33 backup tests, design/Markdown links/disabled workflows, realm checks, Node suites, restore and Release build (zero warnings/errors), 607 actual PostgreSQL persistence cases, typed-client checks and all three HTTP smoke suites. No persistence SKIP was reported. This verifies the repository checks on the combined main baseline plus documentation; it is not runtime reproduction or acceptance of the new reports.

The existing managed stack private source `.local/platform/docker.env` is read only inside the validation subprocess and its connection supplied through `SWOOVR_TEST_DATABASE`, never printed or committed. Tests own isolated databases. Existing services, data, authentication and private configuration are preserved.

Publication review: only the six task-owned delivery Markdown files are staged; no image, application source, private configuration, design source or workflow changes. Original item 18–25 prose outside evidence formatting is byte-equivalent after removing inserted metadata/notes. Eleven sequential Q/issue pairs, unique anchors and complete dispositions were verified; all new GitHub issues are OPEN with reproduction, proposed acceptance, existing labels and valid local-source/Q/intake destinations. Existing issue comments updated #33/#137/#139/#140/#148/#149/#150. Commit and push outcome is reported by the completing chat; no PR is created.
