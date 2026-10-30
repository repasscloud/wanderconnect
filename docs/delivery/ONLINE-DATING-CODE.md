# Swoovr against the Australian Online Dating Industry Code — gap analysis (DRAFT)

**DRAFT — not legal advice.** Written by the builder on 29 October 2026 for [#55](https://github.com/repasscloud/wanderconnect/issues/55) (D179: phase 0 benchmark document). It compares the local prototype with the *Code of Practice — Online Safety Code for dating services* (July 2024), commenced 1 October 2024 with enforcement from 1 April 2025, as published at australianonlinedatingcode.com.au. The Code is voluntary and binds only the services that sign it; eSafety confirms it is overseen by the industry's Code Oversight Body and an independent Code Compliance Committee, not enforced by eSafety. Swoovr intends to sign before public launch, subject to legal review and operational readiness (CONFIRMED Q87, D215). Intention is not signatory status, a verified rating or verified compliance. Use this as a design benchmark either way; an Australian lawyer should confirm it before anyone relies on it (#57).

Status key: **Built** (in the prototype, with a decision and tests), **Partly built**, **Gap** (an issue is open), **Organisational** (a company or operations matter, not software), **N/A**.

## Current implementation snapshot — 30 October 2026

DEFAULT D264 adds reviewable [terms/community draft](TERMS-AND-COMMUNITY-GUIDELINES-DRAFT.md), [moderator playbook](MODERATOR-GUIDANCE-DRAFT.md), [leader charter](TRUST-SAFETY-LEADER-ROLE-DRAFT.md), [law-enforcement/industry plan](LAW-ENFORCEMENT-COOPERATION-PLAN-DRAFT.md), [self-assessment template](SAFETY-BY-DESIGN-SELF-ASSESSMENT-DRAFT.md) and [compliance register](CODE-COMPLIANCE-REGISTER-DRAFT.md). These are drafts, not approved live terms, a named appointment, completed training/assessment, executed agreements or compliance verification. Legal57 and actual operating evidence remain required. D269 now supplies public DRAFT readers and explicit versioned-acceptance preparation with an empty approved catalogue; legal57 remains. No member is bound to a draft.

D236–D262 now cover the local registry/admission/reinstatement/contact-retirement/expiry and retention/recovery implementation; hosted backup/network/job acceptance remains under118. D263 adds locally tested voluntary meeting safety, which supports member empowerment but does not guarantee physical safety. D265 adds locally tested specific profile-text/message removal; D266 adds optional locally tested text-harm candidates and human review, without verified accuracy; D267 adds locally tested centre-only reporter outcome checkpoints and complaint/support guidance;D268 adds prospective annual aggregate preparation with explicit historical/accuracy/training gaps;D269 adds public DRAFT policies, closed-by-default explicit acceptance preparation and centre-only safety updates/advice; legal57 and actual operations remain;48 remains a release gate. D215 is an intention to sign only. The benchmark text below retains its original dated Code section mapping and awaits legal applicability review.

## Summary

| Area | Code | Status | Where / issue |
| --- | --- | --- | --- |
| Terms that prohibit illegal content, sexual misconduct and non-consensual intimate images | §5.1 | Partly built | D269 public DRAFT readers and explicit versioned acceptance preparation; approved catalogue remains empty pending legal57 → [#113](https://github.com/repasscloud/wanderconnect/issues/113) |
| Detecting online-enabled harm | §5.2(1)(a) | Partly built | Photos screened before anyone sees them (D203, D205, D209); D266 optional local profile/message hooks create encrypted human-review candidates; bounded history/false positives/accuracy limits remain explicit → [#115](https://github.com/repasscloud/wanderconnect/issues/115) |
| Enforcing policies, moderating content | §5.2(1)(b), §5.2(2) | Built | Reports, cases, sanctions, appeals (D131–D135, D160); photo review queue (D203) |
| Blocking or removing content | §5.3 | Partly built | Photos can be rejected or taken down (D203); D265 adds specific text/message removal with independent retained evidence and member standing; local acceptance is recorded in VALIDATION → [#114](https://github.com/repasscloud/wanderconnect/issues/114) |
| Staff guidance, escalation channels, actions | §5.4(1)(a)–(c) | Partly built | Urgent escalation and admin queue (D135, D159, D190), restrict/suspend/terminate (D133, D160); D264 draft moderator playbook/training scenarios; actual approval/training/coverage remain119/57 → [#116](https://github.com/repasscloud/wanderconnect/issues/116) |
| Stopping banned people coming back | §5.4(1)(d) | Partly built | D236–D260 add typed/versioned registry, actual admission/contact checks, separate release/reinstatement and staff screens, contact retirement/reviewed recovery, necessity/expiry/disposal and a fail-closed legacy-import inventory. Real provider/hosted acceptance and legal57 remain separate; no verified Code-compliance claim |
| Terminate for serious, limit or suspend for lesser violations | §5.4(2) | Built | D133, D160 |
| Same action across a company's other services | §5.4(3) | N/A | Swoovr runs one service |
| Keep account information for a period after deletion | §5.5 | Built | 30-day pending deletion (D170), deleted messages kept 30 days (D167), tombstones (D170), typed/versioned ban registry with independent necessity/expiry/disposal (D236–D260), legal holds (D188, D189). Legal57 must assess the periods against the dated benchmark → [#118](https://github.com/repasscloud/wanderconnect/issues/118) |
| A complaints and reports mechanism (conduct; how complaints are handled; Code compliance) | §6.1(1) | Partly built | Reports about conduct everywhere it happens (D131, D190), appeals of actions (D135), approved support channels (D214) and references (D208); D267 explicitly guides handling/Code complaints through the approved channels; corporate forms/ticketing remain out of scope, actual support operation unverified → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Prominent, transparent, plain-language, accessible | §6.1(2) | Partly built | Report and Block on profiles and in chats, Safety & Support (D204), account standing (D208); accessibility floor only (#48) |
| Review every genuine report, promptly | §6.2(1)(a)–(b) | Built | Every report joins a case in a queue; urgent ones escalate at once (D135) |
| Tell the complainant the outcome for sexual misconduct and serious violations | §6.2(1)(c) | Partly built | The general outcome shows on Notices and Account standing (D190, D208); D267 adds atomic generic reporter centre updates for actual source-decision changes, without private action details → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Keep review information at least 12 months | §6.2(2) | Partly built | D231–D233 add the [written schedule](RETENTION-SCHEDULE.md), calendar review clocks, independently retained copies/photo references and guarded disposal. Local byte/recovery, operational/audit-history, registry and encrypted backup preparation D234–D262 are tested; hosted backup jobs/network/acceptance and legal57 remain → [#118](https://github.com/repasscloud/wanderconnect/issues/118) |
| Support resources for complainants | §6.3 | Partly built | Country-aware Safety & Support (D204, D206) and the serious-incident pathway show them; D267 offers resources after every routine/serious report and retains follow-up after report-and-block → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Sharing with other participants | §6.4 | N/A | Only if Swoovr signs (Q87) |
| Law-enforcement point of contact; acting on their information | §6.5(1)–(2) | Built | `/law-enforcement` page and LE cases (D195, D196), legal holds (D188) |
| Invite police to an MOU; report imminent threats; risk-assess every report | §6.5(3)–(4) | Partly built | Police-contact procedure (D195), urgent escalation (D135, D190); MOUs and the ANZPAA information-sharing protocol are operational → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Trust and Safety Leader, team, oversight, training | §7.1(1)–(2), §7.2 | Organisational | Roles exist in software (support, admin, legal; D134, D197); D264 charter/plan/templates; named people, actual staffing/training/urgent coverage remain → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Collect data aligned with the reports | §7.1(3) | Partly built | D268 prospective action receipts and anonymous totals; incomplete historical coverage → [#120](https://github.com/repasscloud/wanderconnect/issues/120) |
| Safe-dating advice at onboarding and in relevant communications | §7.3(1) | Local implementation | D269 shared first-use/profile/meeting advice and public/native readers; physical native UI unverified → [#121](https://github.com/repasscloud/wanderconnect/issues/121) |
| Policies easy to find, plain, accessible, kept up to date | §7.3(2) | Partly built | Privacy Draft0.11 and D269 public DRAFT policies/versioned-acceptance preparation; legal57 and accessibility release gate48 remain |
| Tell members about significant policy changes | §7.3(3) | Local default-off implementation | D269 explicit public-source safety publication, current-member snapshot and resumable centre-only delivery; no draft legal broadcast → [#121](https://github.com/repasscloud/wanderconnect/issues/121) |
| Industry collaboration and forums | §7.4 | Organisational | Only if Swoovr signs (Q87) → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Compliance rating and Safety by Design self-assessment | §3.2, §4.1 | Partly built | D264 template/register; completed assessment, legal review and external process outstanding → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Records of compliance measures for 2 years | §8.2(2) | Partly built | DECISIONS.md, VALIDATION.md and the audit log are a start; no compliance register → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Annual Code report to eSafety; published transparency report | §8.3, §8.4 | Internal preparation; publication/operations open | D268 protected aggregate export/revisions, explicit unknown history, unmeasured accuracy/training; no publication/submission → [#120](https://github.com/repasscloud/wanderconnect/issues/120) |

## Section by section

### 3–4 Compliance and frameworks

- **§3.1–3.2 Compliance rating.** A signatory self-assesses as exceeding, complying with or partly complying with the Code (tiers one to three), the Oversight Body verifies it, and the service publishes it. Tier one asks for automation under human oversight (Swoovr's photo screening sends uncertain photos to people and never refuses automatically, D205), automatic blocking of content meeting criteria (blocked known-content matches, D203), the same safety features for everyone regardless of payment (true today: no paid tier gates any safety feature), and support resources the complainant is actively told about (partly). Nothing is self-assessed yet.
- **§4.1 Frameworks.** Have regard to the Basic Online Safety Expectations, eSafety's Safety by Design principles and the Five Country voluntary principles against child sexual exploitation. The internal trust and safety policy (D185) and the police-contact procedure (D195) cover much of the substance; D264 supplies an incomplete assessment template and register, not a completed assessment.
- **§4.2 Other obligations.** Registered industry codes and standards under the Online Safety Act apply regardless; the policy (§5) already asks for that assessment for social and messaging functions.

### 5 Online safety of end-users

- **§5.1 Terms.** The terms must at least prohibit illegal content (including sexual misconduct) and non-consensual intimate images. D269 supplies public DRAFT readers and explicit versioned-acceptance preparation; the approved catalogue is empty pending legal57. No implied acceptance.
- **§5.2 Detection and moderation.** Photos (profile and chat) are screened before anyone sees them, with known-content matching when Arachnid credentials are present (D201–D205, D209). D266 adds disabled-by-default local text hooks for narrow threat/payment/repetition/bulk-outreach signals, encrypted transactional processing, actual bounded server history and human review. No automated sanction or measured accuracy is claimed. Configured operation and real staffing remain separate from local acceptance. Partly built.
- **§5.3 Blocking and removing content.** Photos: reject, take down or escalate (D203). D265 adds exact-source case staff removal of profile text/reported messages, neutral member notices and independent preserved evidence. Local acceptance and hosted limits remain distinct.
- **§5.4 Actions.** Escalation and prioritisation (D135, D159, D190), proportionate actions (D133, D160), appeals (D135), typed banned-identifier enforcement and independent reinstatement/recovery (D236–D260). D264 supplies a draft playbook; actual training and operational acceptance remain119/57.
- **§5.5 Retention after deletion.** Built with different numbers from the Code's example (30-day recovery, 30-day deleted messages, 2-year tombstones; holds keep anything needed). Worth confirming with the lawyer that these are "appropriate".

### 6 Complaints and reports

- **§6.1** Reports about conduct are available on profiles, requests, conversations, messages, chat photos and after a block, disconnect or deletion (D131, D190, D209). Q86/D214 confirms https://swoovr.com/support and support@swoovr.com, without an SLA or support ticketing scope. D267 adds explicit handling/Code complaint guidance through those approved channels; corporate intake/ticketing remains out of scope, and actual staffing/operation is not established.
- **§6.2** Every report is reviewed in a case; the reporter sees the status and, once closed, whether action was taken (D190) on Notices and Account standing (D208); D267 adds generic centre-only updates for actual source-decision changes, including lift/appeal changes whose broad outcome may stay the same. Review information should be kept at least 12 months: the written schedule and locally tested guarded expiry D231–D262 now exist; legal57 and hosted operational acceptance remain separate.
- **§6.3** Support resources: country-aware packs (D204, D206) and the serious-incident pathway. D267 now points every routine/serious reporter to them without inferring a sexual-misconduct category.
- **§6.5** Law enforcement: a clearly identifiable electronic point of contact (`/law-enforcement`, D196), acting on police information within the policy and the law, disclosure rules (procedure D195), imminent-threat escalation. Inviting each police agency to an MOU (or using the ANZPAA protocol) is an operational step.

### 7 Trust and safety

- **§7.1–7.2** A designated senior Trust and Safety Leader, a sufficient team, oversight, internal controls and training. The software has the roles (support, admin, legal; staff access controls D197); D264 adds the draft role charter, playbook and review records; naming/accepting people, resources and training evidence remains organisational under119.
- **§7.3** Safe-dating advice at onboarding and in relevant communications; policies easy to find, plain, accessible and current; proactive notice of significant policy changes. D269 supplies shared advice during first use/profile/meeting planning and explicit default-off centre-only safety publications. Legal drafts are not broadcast/activated; actual publication and native OS UI remain unverified.
- **§7.4** Industry forums and stakeholder engagement apply to signatories.

### 8 Administration

- **§8.2** Keep records of compliance measures for 2 years.
- **§8.3** An annual Code report to eSafety by 31 August (steps taken, volume of complaints, why the steps are proportionate), and updates about significant functional changes.
- **§8.4** A published transparency report by 31 August each year for the year to 30 June: Australian accounts terminated by policy basis, content moderated by detection and enforcement mechanism, information about own-initiative moderation and training, and the use of automation with its purposes and indicators of accuracy and error. D268 now prepares prospective aggregate subsets; historical completeness, measured classifier accuracy, actual training and publication/submission remain unverified. See [implementation](MODERATION-ANNUAL-DATA.md).

## Issues opened for the gaps

| Issue | Gap | Code |
| --- | --- | --- |
| [#112](https://github.com/repasscloud/wanderconnect/issues/112) (Q87) | ANSWERED D215: intends to sign; formal participation remains unverified | all |
| [#113](https://github.com/repasscloud/wanderconnect/issues/113) | Terms of use and community guidelines | §5.1, §7.3(2) |
| [#114](https://github.com/repasscloud/wanderconnect/issues/114) | Staff removal of profile text and single messages | §5.3 |
| [#115](https://github.com/repasscloud/wanderconnect/issues/115) | Detecting harm in text: scams, bulk outreach, abusive messages | §5.2(1)(a); policy §14 |
| [#116](https://github.com/repasscloud/wanderconnect/issues/116) | D264 draft moderator guidance delivered; actual training/coverage remains119/57 | §5.4(1)(a), §7.2(4) |
| [#117](https://github.com/repasscloud/wanderconnect/issues/117) | Complaints about handling and Code compliance; outcome notices; support resources after reports | §6.1(1)(b)–(c), §6.2(1)(c), §6.3 |
| [#118](https://github.com/repasscloud/wanderconnect/issues/118) | Retention schedule: review information ≥ 12 months; periods after deletion | §5.5, §6.2(2) |
| [#119](https://github.com/repasscloud/wanderconnect/issues/119) | Organisational: Trust and Safety Leader, team, police MOUs, forums, Safety by Design self-assessment, compliance register | §3.2, §4.1, §6.5(3), §7.1, §7.2, §7.4, §8.2 |
| [#120](https://github.com/repasscloud/wanderconnect/issues/120) | Transparency report and annual Code report to eSafety | §7.1(3), §8.3, §8.4 |
| [#121](https://github.com/repasscloud/wanderconnect/issues/121) | Safe-dating advice in onboarding; in-app notice of policy changes | §7.3(1), §7.3(3) |

Sources: the Code of Practice PDF (July 2024) on australianonlinedatingcode.com.au; Department of Infrastructure, "New industry code now operational to make online dating safer"; ANZPAA, *Protocol for information sharing between the online dating industry and Australian policing jurisdictions*.

### Retention implementation progress — 29 October 2026

Under owner D216, the local implementation separates ordinary account/message deletion, qualifying review content, minimal compliance records, police records and backup expiry. Hold release resumes the applicable schedule; it does not by itself erase evidence. Unknown provenance is preserved pending restricted review. The [schedule](RETENTION-SCHEDULE.md) identifies DEFAULT periods, remaining implementation and legal review57. This supports intended Code readiness; no signatory status or independently verified compliance is claimed. See [validation](VALIDATION.md) for actual local database/browser evidence rather than treating a written schedule as operational acceptance.

### Governance drafting progress — 30 October 2026

The D264 document set implements the drafting portion of D220 and116.D269 supplies113 public/versioned acceptance preparation and121 advice/notice software, with legal57 still open;119 remains open for named appointments, operational coverage/training, real protocol/outreach/industry work and a completed assessment. No external organisation has been contacted, no policy has been activated, and no public rating/signatory claim is authorised. Record actual evidence and residual actions in the compliance register rather than treating document presence as fulfilment.
