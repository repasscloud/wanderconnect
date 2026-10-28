# Swoovr against the Australian Online Dating Industry Code — gap analysis (DRAFT)

**DRAFT — not legal advice.** Written by the builder on 29 October 2026 for [#55](https://github.com/repasscloud/wanderconnect/issues/55) (D179: phase 0 benchmark document). It compares the local prototype with the *Code of Practice — Online Safety Code for dating services* (July 2024), commenced 1 October 2024 with enforcement from 1 April 2025, as published at australianonlinedatingcode.com.au. The Code is voluntary and binds only the services that sign it; eSafety confirms it is overseen by the industry's Code Oversight Body and an independent Code Compliance Committee, not enforced by eSafety. Swoovr has not decided whether to sign ([Q87](QUESTIONS.md#questions-raised-while-building-ajal--29-october-2026)). Use this as a design benchmark either way; an Australian lawyer should confirm it before anyone relies on it (#57).

Status key: **Built** (in the prototype, with a decision and tests), **Partly built**, **Gap** (an issue is open), **Organisational** (a company or operations matter, not software), **N/A**.

## Summary

| Area | Code | Status | Where / issue |
| --- | --- | --- | --- |
| Terms that prohibit illegal content, sexual misconduct and non-consensual intimate images | §5.1 | Gap | No terms of use or community guidelines yet → [#113](https://github.com/repasscloud/wanderconnect/issues/113) |
| Detecting online-enabled harm | §5.2(1)(a) | Partly built | Photos screened before anyone sees them (D203, D205, D209); profiles and messages only through reports → [#115](https://github.com/repasscloud/wanderconnect/issues/115) |
| Enforcing policies, moderating content | §5.2(1)(b), §5.2(2) | Built | Reports, cases, sanctions, appeals (D131–D135, D160); photo review queue (D203) |
| Blocking or removing content | §5.3 | Partly built | Photos can be rejected or taken down (D203); staff can't remove a profile's text or a single message, only sanction the account → [#114](https://github.com/repasscloud/wanderconnect/issues/114) |
| Staff guidance, escalation channels, actions | §5.4(1)(a)–(c) | Partly built | Urgent escalation and admin queue (D135, D159, D190), restrict/suspend/terminate (D133, D160); no written moderator guidance → [#116](https://github.com/repasscloud/wanderconnect/issues/116) |
| Stopping banned people coming back | §5.4(1)(d) | Partly built | Terminated-email keyed hash flags a returning registration (D171); previous mobile numbers (D187); ban-matching fingerprints described in the privacy notice draft (D200). Flags go to people, never automatic |
| Terminate for serious, limit or suspend for lesser violations | §5.4(2) | Built | D133, D160 |
| Same action across a company's other services | §5.4(3) | N/A | Swoovr runs one service |
| Keep account information for a period after deletion | §5.5 | Built | 30-day pending deletion (D170), deleted messages kept 30 days (D167), tombstones and terminated-email hashes 2 years (D170, D171), legal holds (D188, D189). Check the periods against the Code's 90-day example → [#118](https://github.com/repasscloud/wanderconnect/issues/118) |
| A complaints and reports mechanism (conduct; how complaints are handled; Code compliance) | §6.1(1) | Partly built | Reports about conduct everywhere it happens (D131, D190), appeals of actions (D135), support references (D208); no way to complain about how a report was handled or about Code compliance → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Prominent, transparent, plain-language, accessible | §6.1(2) | Partly built | Report and Block on profiles and in chats, Safety & Support (D204), account standing (D208); accessibility floor only (#48) |
| Review every genuine report, promptly | §6.2(1)(a)–(b) | Built | Every report joins a case in a queue; urgent ones escalate at once (D135) |
| Tell the complainant the outcome for sexual misconduct and serious violations | §6.2(1)(c) | Partly built | The general outcome shows on Notices and Account standing (D190, D208); the reporter isn't told when it changes → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Keep review information at least 12 months | §6.2(2) | Partly built | Cases and evidence stay; no written retention schedule that guarantees 12 months → [#118](https://github.com/repasscloud/wanderconnect/issues/118) |
| Support resources for complainants | §6.3 | Partly built | Country-aware Safety & Support (D204, D206) and the serious-incident pathway show them; a routine report of sexual misconduct doesn't point the reporter to them → [#117](https://github.com/repasscloud/wanderconnect/issues/117) |
| Sharing with other participants | §6.4 | N/A | Only if Swoovr signs (Q87) |
| Law-enforcement point of contact; acting on their information | §6.5(1)–(2) | Built | `/law-enforcement` page and LE cases (D195, D196), legal holds (D188) |
| Invite police to an MOU; report imminent threats; risk-assess every report | §6.5(3)–(4) | Partly built | Police-contact procedure (D195), urgent escalation (D135, D190); MOUs and the ANZPAA information-sharing protocol are operational → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Trust and Safety Leader, team, oversight, training | §7.1(1)–(2), §7.2 | Organisational | Roles exist in software (support, admin, legal; D134, D197); people and training → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Collect data aligned with the reports | §7.1(3) | Partly built | Audit log and LE transparency aggregates (D196); no moderation metrics → [#120](https://github.com/repasscloud/wanderconnect/issues/120) |
| Safe-dating advice at onboarding and in relevant communications | §7.3(1) | Gap | Safety & Support exists (D204) but isn't part of onboarding → [#121](https://github.com/repasscloud/wanderconnect/issues/121) |
| Policies easy to find, plain, accessible, kept up to date | §7.3(2) | Partly built | Privacy notice DRAFT (D200, D202); no terms or community guidelines (#113); accessibility floor (#48) |
| Tell members about significant policy changes | §7.3(3) | Gap | The notification centre (D207) could carry them → [#121](https://github.com/repasscloud/wanderconnect/issues/121) |
| Industry collaboration and forums | §7.4 | Organisational | Only if Swoovr signs (Q87) → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Compliance rating and Safety by Design self-assessment | §3.2, §4.1 | Gap | No self-assessment written → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Records of compliance measures for 2 years | §8.2(2) | Partly built | DECISIONS.md, VALIDATION.md and the audit log are a start; no compliance register → [#119](https://github.com/repasscloud/wanderconnect/issues/119) |
| Annual Code report to eSafety; published transparency report | §8.3, §8.4 | Gap | Terminations by policy basis, moderation by mechanism, automation and its error rates → [#120](https://github.com/repasscloud/wanderconnect/issues/120) |

## Section by section

### 3–4 Compliance and frameworks

- **§3.1–3.2 Compliance rating.** A signatory self-assesses as exceeding, complying with or partly complying with the Code (tiers one to three), the Oversight Body verifies it, and the service publishes it. Tier one asks for automation under human oversight (Swoovr's photo screening sends uncertain photos to people and never refuses automatically, D205), automatic blocking of content meeting criteria (blocked known-content matches, D203), the same safety features for everyone regardless of payment (true today: no paid tier gates any safety feature), and support resources the complainant is actively told about (partly). Nothing is self-assessed yet.
- **§4.1 Frameworks.** Have regard to the Basic Online Safety Expectations, eSafety's Safety by Design principles and the Five Country voluntary principles against child sexual exploitation. The internal trust and safety policy (D185) and the police-contact procedure (D195) cover much of the substance; no written Safety by Design assessment exists.
- **§4.2 Other obligations.** Registered industry codes and standards under the Online Safety Act apply regardless; the policy (§5) already asks for that assessment for social and messaging functions.

### 5 Online safety of end-users

- **§5.1 Terms.** The terms must at least prohibit illegal content (including sexual misconduct) and non-consensual intimate images. Swoovr has no terms of use or community guidelines in the product. Gap.
- **§5.2 Detection and moderation.** Photos (profile and chat) are screened before anyone sees them, with known-content matching when Arachnid credentials are present (D201–D205, D209). Text in profiles and messages is reviewed only when reported; policy §14 also asks for scam and bulk-outreach detection. Partly built.
- **§5.3 Blocking and removing content.** Photos: reject, take down or escalate (D203). Profile text and individual messages: no staff removal; the only tool is a sanction on the account. Partly built.
- **§5.4 Actions.** Escalation and prioritisation (D135, D159, D190), proportionate actions (D133, D160), appeals (D135), returning terminated members flagged to people (D171, D187). Written guidance for staff is missing.
- **§5.5 Retention after deletion.** Built with different numbers from the Code's example (30-day recovery, 30-day deleted messages, 2-year tombstones; holds keep anything needed). Worth confirming with the lawyer that these are "appropriate".

### 6 Complaints and reports

- **§6.1** Reports about conduct are available on profiles, requests, conversations, messages, chat photos and after a block, disconnect or deletion (D131, D190, D209). Complaints about *how a report was handled* and about Code compliance have no channel; the support reference (D208) is ready for one once Q86 names it.
- **§6.2** Every report is reviewed in a case; the reporter sees the status and, once closed, whether action was taken (D190) on Notices and Account standing (D208), but isn't notified when it changes. Review information should be kept at least 12 months: cases and evidence stay today, but no written schedule says so.
- **§6.3** Support resources: country-aware packs (D204, D206) and the serious-incident pathway. A routine report about sexual misconduct should also point the reporter to them.
- **§6.5** Law enforcement: a clearly identifiable electronic point of contact (`/law-enforcement`, D196), acting on police information within the policy and the law, disclosure rules (procedure D195), imminent-threat escalation. Inviting each police agency to an MOU (or using the ANZPAA protocol) is an operational step.

### 7 Trust and safety

- **§7.1–7.2** A designated senior Trust and Safety Leader, a sufficient team, oversight, internal controls and training. The software has the roles (support, admin, legal; staff access controls D197); naming people and training them is organisational.
- **§7.3** Safe-dating advice at onboarding and in relevant communications; policies easy to find, plain, accessible and current; proactive notice of significant policy changes. Safety & Support exists (D204) and the notification centre (D207) can carry notices, but neither is used for this yet.
- **§7.4** Industry forums and stakeholder engagement apply to signatories.

### 8 Administration

- **§8.2** Keep records of compliance measures for 2 years.
- **§8.3** An annual Code report to eSafety by 31 August (steps taken, volume of complaints, why the steps are proportionate), and updates about significant functional changes.
- **§8.4** A published transparency report by 31 August each year for the year to 30 June: Australian accounts terminated by policy basis, content moderated by detection and enforcement mechanism, information about own-initiative moderation and training, and the use of automation with its purposes and indicators of accuracy and error. The audit log has the raw events; nothing aggregates them.

## Issues opened for the gaps

| Issue | Gap | Code |
| --- | --- | --- |
| [#112](https://github.com/repasscloud/wanderconnect/issues/112) (Q87) | Decide whether Swoovr signs the Code | all |
| [#113](https://github.com/repasscloud/wanderconnect/issues/113) | Terms of use and community guidelines | §5.1, §7.3(2) |
| [#114](https://github.com/repasscloud/wanderconnect/issues/114) | Staff removal of profile text and single messages | §5.3 |
| [#115](https://github.com/repasscloud/wanderconnect/issues/115) | Detecting harm in text: scams, bulk outreach, abusive messages | §5.2(1)(a); policy §14 |
| [#116](https://github.com/repasscloud/wanderconnect/issues/116) | Written moderator guidance and training | §5.4(1)(a), §7.2(4) |
| [#117](https://github.com/repasscloud/wanderconnect/issues/117) | Complaints about handling and Code compliance; outcome notices; support resources after reports | §6.1(1)(b)–(c), §6.2(1)(c), §6.3 |
| [#118](https://github.com/repasscloud/wanderconnect/issues/118) | Retention schedule: review information ≥ 12 months; periods after deletion | §5.5, §6.2(2) |
| [#119](https://github.com/repasscloud/wanderconnect/issues/119) | Organisational: Trust and Safety Leader, team, police MOUs, forums, Safety by Design self-assessment, compliance register | §3.2, §4.1, §6.5(3), §7.1, §7.2, §7.4, §8.2 |
| [#120](https://github.com/repasscloud/wanderconnect/issues/120) | Transparency report and annual Code report to eSafety | §7.1(3), §8.3, §8.4 |
| [#121](https://github.com/repasscloud/wanderconnect/issues/121) | Safe-dating advice in onboarding; in-app notice of policy changes | §7.3(1), §7.3(3) |

Sources: the Code of Practice PDF (July 2024) on australianonlinedatingcode.com.au; Department of Infrastructure, "New industry code now operational to make online dating safer"; ANZPAA, *Protocol for information sharing between the online dating industry and Australian policing jurisdictions*.
