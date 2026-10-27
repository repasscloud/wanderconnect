# Swoovr — Police Contact, Legal Holds & Law-Enforcement Disclosure Procedure

**Classification:** Internal — Restricted\
**Version:** 1.0 — Owner-approved policy draft\
**Date:** 27 October 2026\
**Related issue:** https://github.com/repasscloud/wanderconnect/issues/84\
**Status:** Approved owner decisions Q1–Q37; legal and operational readiness review required before production activation.

## 1. Purpose and principles

This procedure governs requests by police, law-enforcement bodies, regulators and other legally authorised authorities for Swoovr member information, evidence preservation or related assistance. It also governs internal legal holds, evidence exports, chain of custody, member notification and case closure.

Swoovr will cooperate with lawful requests while protecting member privacy, preserving evidential integrity, avoiding excessive disclosures and maintaining independently auditable decisions. Preservation of data and disclosure of data are distinct authorisations. Neither a police request nor preservation automatically permits disclosure.

The procedure does not expand collection or retention beyond Swoovr's lawful product and privacy practices. All actions are subject to applicable law, including special rules for telecommunications data and content where applicable. Legal counsel must resolve uncertainty and conflicts with binding obligations.

## 2. Contact channels and request intake (Q1, Q16, Q22–Q23)

- Official intake: **lawenforcement@swoovr.app**. Publish it as the designated authority-contact channel.
- Maintain a separate, protected **24/7 emergency escalation mechanism**, with an on-call roster and tested escalation pathway. Do not publish an unstaffed emergency promise.
- Redirect informal calls, personal emails, direct messages and other approaches to the official channel, without acknowledging that a named person has an account or disclosing member information.
- Record each material approach in a restricted case system. Assign a unique case identifier and log channel, receipt time, request documents, agency, officer, contact details, claimed urgency, affected subjects, deadlines and case owner.
- Target **initial response**: critical emergencies within one hour at any time; urgent requests within four hours; standard acknowledgement within two business days; complex-case updates within five business days. Statutory and court deadlines override these service targets. These are response targets, not promises to disclose information.

## 3. Requester verification and legal basis (Q2–Q3, Q11, Q17, Q21)

1. Obtain agency name, officer identity, rank or role, service number where applicable, case reference, formal request, requested information, relevant period, purpose and asserted legal authority.
2. Independently verify the officer and request using contact details obtained from an authentic official agency directory or switchboard, not solely contact details supplied in the request. A government email address alone is insufficient.
3. Assess whether the request is mandatory or voluntary, what specific statutory or judicial authority applies, its territorial reach, scope and deadline, and whether disclosure is necessary, proportionate and permitted.
4. Require and verify the applicable legal instrument where necessary: warrant, court order, subpoena, statutory notice or other effective legal process. Do not assume such an instrument is universally necessary or universally sufficient.
5. For foreign authorities, consider Australian jurisdiction, cross-border disclosure safeguards, recognised mutual legal assistance channels and any lawful emergency exception. Foreign orders do not automatically compel Swoovr in Australia.
6. For regulators, child-protection bodies and other agencies, identify their actual statutory powers; government status alone confers no blanket access.
7. Clarify, narrow or lawfully challenge unclear, overbroad, contradictory or potentially unlawful requests. Escalate substantive disputes to legal counsel. Seek extensions or judicial directions where needed rather than missing binding deadlines.

**APP 6 note:** Where Swoovr relies on the enforcement-related activities exception under APP 6.2(e), the case record must contain the written note required by APP 6.5, including the factual basis for reasonable belief, extent of disclosure, recipients and date, unless prohibited by law.

## 4. Emergency and child-safety handling (Q4, Q19)

Credible immediate threats to life or serious safety, child exploitation and comparable acute risks receive expedited triage. Verify the requesting authority through an accelerated independent route, establish the applicable lawful basis, approve necessary and minimally scoped disclosure through the designated emergency approver and create a contemporaneous audit record. Complete retrospective senior/legal review promptly.

For suspected child sexual abuse material, grooming or underage exploitation: prioritise safety, restrict harmful activity where justified, preserve relevant records through specialised workflows, escalate to designated Trust & Safety staff and notify appropriate authorities where required or justified. Do not permit routine staff downloading, duplication, redistribution or casual viewing of suspected CSAM. Do not alert potential offenders if notification is unlawful or creates a material risk to child safety or the investigation.

## 5. Approval and conflicts of interest (Q6, Q18, Q27)

Only individually designated senior administrators or authorised Trust & Safety personnel may approve a disclosure, after appropriate legal assessment. Highly sensitive, unusual, disputed, direct-access or legally complex requests go to a director and/or legal counsel as specified below. An authorised on-call approver may act in a qualifying emergency, with mandatory retrospective review.

The requester, collector, reviewer and approver should be separated where practical. Training alone does not confer evidence access or approval permissions. Grant individual least-privilege access; require initial and annual refresher training, annual permission reviews, and immediate revocation on role change or exit.

Where an employee, contractor, director or privileged user is implicated, prevent them from handling the case or accessing its evidence. Assign an uninvolved director or senior representative, obtain external legal support where warranted, preserve relevant security records and consider proportionate temporary access restrictions without assuming guilt.

## 6. Information availability and disclosure scope (Q5, Q20, Q29, Q32)

Potentially responsive categories include account registration and status, verification outcomes and identity information, retained messages and conversations, public and private media, reports and moderation evidence, audit activity, and location-related records **only if actually collected and retained**. No category is automatically released.

Identify precisely which accounts, counterparties, records, media and date ranges are covered. Minimise unrelated members' data. Redact or filter disclosure copies where lawful and appropriate, preserving unmodified originals and documenting redactions. If inseparable third-party information is legally required, document the justification.

Differentiate records never collected, lawfully and permanently deleted, currently inaccessible, or held by third parties. Never fabricate missing information or claim that deleted evidence can be recovered. Assess relevant backups and externally controlled data where required or justified by valid authority. For providers such as Didit or Cloudflare, determine whether Swoovr has possession or control, the relevant contract, legal authority and jurisdiction; coordinate authorised retrieval or direct authorities to the provider's lawful process as appropriate. Record provider actions and transfers.

## 7. Legal holds and temporary preservation (Q8, Q14–Q15, Q28, Q33)

Every legal hold must record its case ID, purpose, lawful justification, requesting body, authoriser, creation time, precise account/conversation/media/date scope, applicable systems and providers, review date, restrictions, release conditions and status.

A valid hold must prevent every affected deletion route, including user account deletion, message and media purge, automated retention cleanup, administrator deletion and relevant backup expiry where technically controllable and lawful. Affected data should be secured in a restricted evidence store. Original account suspension or public-content removal may continue without deleting preserved evidence. Overlapping holds operate independently: a record remains protected while any valid hold applies.

For a verified police request received before formal process, an authorised reviewer may impose a **temporary, specifically scoped preservation hold of up to 30 days**, but only where a lawful basis exists and applicable deletion or retention duties permit it. Review extensions before expiry; 30 days is not an automatic legal entitlement or permission to disclose. Hold review dates are not automatic release dates.

Only an independent senior approver who did not originally authorise the hold may approve its release after checking its legal basis and every overlapping hold; contested or complex releases require legal counsel. Log the release and resume ordinary retention/delete processes for records no longer protected or otherwise lawfully needed. Preservation must not become indefinite by default.

## 8. Evidence collection, integrity and transfer (Q9, Q31, Q34, Q37)

Authorised Swoovr personnel, not external agencies, collect evidence through controlled tooling. Authorities are not given production database credentials, member account impersonation, unrestricted administrator access or direct infrastructure access. A legally compulsory direct-access demand requires legal counsel assessment, director approval and a controlled implementation plan.

For each evidence package:

1. Export original-format material where feasible, with source identifiers, provenance, relevant metadata and UTC times. Document timestamp sources and conversions.
2. Create a manifest with case ID, authorised scope, source systems, export operator, timestamps, files, identifiers and SHA-256 hashes.
3. Preserve immutable originals and log access, processing, copying, redactions and transfers in a tamper-evident chain-of-custody record.
4. Use encrypted, authenticated, access-controlled transfer. Exchange secrets out-of-band where feasible; use expiring links and confirm delivery to the verified recipient.
5. Record package hash, recipient, receipt/acknowledgement, transfer method and completion time.

Corrections must not overwrite original evidence or packages. Where a material inaccuracy is found, investigate it, issue a traceable supplemental/correction package with its own manifest and hashes, and notify recipients where appropriate or required. Subpoenas, witness summonses and court requests are verified and referred to legal counsel; nominate a knowledgeable representative, preserve supporting logs and provide factual testimony without speculation.

## 9. Ongoing monitoring, deletion and sensitive directives (Q12, Q30)

Swoovr does not voluntarily conduct ongoing surveillance or build hidden monitoring facilities for authorities. Any prospective monitoring or technical assistance requires specific applicable legal authority, legal counsel review, senior approval, narrow limits, a defined duration and an audit trail. Assess technical feasibility and relevant telecommunications, interception and surveillance restrictions independently.

Lawful public-content removal, account restriction and safety measures may occur while original evidence remains preserved. Do not falsify, secretly rewrite or improperly destroy evidence. Refer conflicting preservation, secrecy or deletion directives for urgent legal review.

## 10. Confidentiality and member notification (Q7, Q13)

Notify affected members where legally permitted and reasonably safe. Do not notify where law or an order prohibits it, or where notice may materially jeopardise safety, evidence or a legitimate investigation. Record the precise reason for non-notification or delay, set a periodic reconsideration date and record any eventual notice.

Protect case information on a need-to-know basis. Non-binding agency confidentiality requests receive their own documented assessment; blanket secrecy is not automatic. Secrecy must not improperly prevent lawful legal advice, internal escalation, regulatory reporting or other mandatory disclosures.

## 11. Multi-agency and international conflicts (Q11, Q21, Q28)

Maintain individual cases, requests, holds, disclosure assessments and approvals for each authority. If legal processes overlap or conflict, preserve under each valid hold and refer contested confidentiality, scope and disclosure requirements to legal counsel. Do not assume the first or most recent agency prevails. Meet binding obligations and seek extensions, variations or court guidance where necessary. Cross-border transfers require a separate jurisdictional/privacy review.

## 12. Security incidents and complaints (Q24, Q36)

Suspected unauthorised access, modification, transfer or disclosure of protected evidence is a high-priority security incident. Contain access, preserve forensic material, alert designated Security/Trust & Safety/Legal stakeholders, investigate independently when privileged staff are implicated, and assess mandatory notices to police, members, regulators and the OAIC. Record root causes, remediation, approvals and outcomes. The Australian Notifiable Data Breaches scheme is applied where relevant; reporting is not automatic merely because an incident is alleged.

Member complaints concerning police disclosure receive independent privacy review by someone uninvolved in the original release. Review the legal basis, permissions, data scope and audit record; escalate suspected unlawful conduct to legal counsel and the designated privacy officer; aim to respond within 30 calendar days where practicable; explain external complaint channels, including the OAIC where applicable. Do not reveal protected investigations or others' personal data in responses.

## 13. Case closure, audit retention and transparency (Q10, Q25–Q26, Q35)

A case may close only after an authorised officer verifies outstanding legal obligations, required communications, evidence manifests, transfer receipts, notification decisions, legal holds and follow-up duties. Continuing holds may outlive closure only if separately assigned, tracked and periodically reviewed. Case closure never silently releases holds or destroys evidence.

Retain case requests, identity-verification checks, legal assessments, authorisations, APP 6 disclosure notes, export manifests, transfer receipts and access logs for a **target of seven years from case closure**, where lawful and still justified. This is an internal default subject to APP 11, applicable laws, court orders and specific retention needs. Retained member evidence follows its distinct schedules and applicable holds. Restrict access and securely delete or de-identify records when no longer lawfully needed.

Conduct **quarterly internal reviews**, an **annual independent audit**, and immediate targeted reviews following major incidents or significant legal changes. Maintain findings, owners, remedies and deadlines. Publish an **annual aggregated law-enforcement transparency report** covering request types, agency/country, outcomes, emergencies, legal holds and affected account counts where safe, lawful and non-identifying. Suppress sensitive small counts and confidential cases.

## 14. Restricted case-management and implementation requirements (Q23 and issue #84)

Implement restricted case management in Swoovr Admin, with:

- Case record and individual request records; verified agency/officer/contact details and independently verified callback evidence.
- Legal assessment and legal-authority attachments; APP 6 written disclosure note; privilege/confidentiality flags.
- Scoped `LegalHolds` covering accounts, conversations, messages, media and dates, with authoriser, review date, extension and release actions.
- Hold-aware account purge, message purge, media cleanup, retention jobs, exports and applicable provider processes. Fail closed when hold status cannot be reliably checked.
- Restricted evidence store for reported conversations and other preserved evidence, with immutable originals and controlled access.
- Evidence package manifests, SHA-256 checksums, timestamps, provenance, redaction records and chain-of-custody events.
- Segregated role-based approvals, independent release controls, dual-person separation where practical, conflict-of-interest exclusions and break-glass emergency auditing.
- SLA/escalation monitoring, member-notification review dates, retention schedules, complaint links and final closure checklist.
- Append-only/tamper-evident auditing for every view, query, export, modification, authorisation, handoff, release and attempted prohibited operation.
- Annual transparency-report aggregates and quarterly/annual audit exports without exposing protected case content.

## 15. Operational completion checklist

Before launch, confirm the real monitored contact address and 24/7 on-call service; identify named authorisers and counsel; verify legal process templates and disclosure checklists; harden evidence-store storage and cryptographic key management; test deletion blocking across all purge paths; test overlapping legal holds and hold release; validate secure export and audit integrity; rehearse emergency and CSAM response; and have qualified Australian legal counsel review the procedure, particularly telecommunications legislation, APP applicability, international disclosure, preservation authority and data-retention rules.

---

**Decision source:** Owner Q1–Q37 selections (all Option A). Subsequent details follow the same recommended, privacy-protective approach and are operational defaults rather than additional owner decisions.\
**Legal reference points:** OAIC APP 6 guidelines (https://www.oaic.gov.au/privacy/australian-privacy-principles/australian-privacy-principles-guidelines/chapter-6-app-6-use-or-disclosure-of-personal-information); OAIC APP 11 guidelines (https://www.oaic.gov.au/privacy/australian-privacy-principles/australian-privacy-principles-guidelines/chapter-11-app-11-security-of-personal-information); OAIC NDB guidelines (https://www.oaic.gov.au/privacy/notifiable-data-breaches/).
