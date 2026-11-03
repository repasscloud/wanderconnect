# Swoovr — Internal Privacy, Trust, Safety and Evidence Protection Policy

**Owner:** RePass Cloud Pty Ltd\
**Applies to:** Swoovr iOS, Android, web, API, administration, contractors, support, hosting and external processors\
**Scope:** All four connection modes and Travel, public profiles, private content, messages, identity verification and moderation\
**Classification:** Internal — Restricted\
**Version:** 1.1 — approved 2 November 2026 (supersedes 1.0 of 27 October 2026)\
**Status:** Approved mandatory policy (D185; legal approval D288, 2 November 2026; D310). Operational readiness evidence is tracked separately ([57](https://github.com/repasscloud/wanderconnect/issues/57)).

## Purpose and precedence

This policy sets the rules every Swoovr feature, staff member and contractor must follow when handling personal information, safety reports and evidence. Its controls are built into Swoovr's software rather than relying on people remembering procedures.

The policy distinguishes three categories of information, which have different access permissions, retention periods and deletion rules:

1. what Swoovr needs for day-to-day operation;
2. what it needs to prevent repeat abuse; and
3. what it must preserve when a serious incident is reported.

Where an owner decision, the [retention schedule](RETENTION-SCHEDULE.md) or the [police-contact procedure](POLICE-CONTACT-PROCEDURE.md) sets a more specific rule or period, that rule governs. Member-facing commitments are in the privacy notice (`Swoovr.Client.PrivacyNotice`) and the [terms and community guidelines](TERMS-AND-COMMUNITY-GUIDELINES.md); this policy must never permit something those documents tell members we don't do.

## 1. Core principles

These principles govern every feature Swoovr develops.

1. **Safety by design:** safety controls are designed into features before release.
2. **Identity accountability:** Swoovr can identify an ID-verified user through information lawfully retained after Didit deletes its verification records.
3. **Privacy by default:** personal information is not exposed unnecessarily to other users, employees, advertisers or external providers.
4. **Evidence preservation:** credible reports of assault, stalking, threats, exploitation or serious fraud trigger an assessment for preservation of relevant evidence.
5. **Data minimisation:** retain what is necessary, not everything that is technically possible to collect.
6. **Controlled disclosure:** law-enforcement requests are authenticated, authorised, documented and limited to relevant information.
7. **Accountability:** staff access, moderation decisions, identity changes and evidence retrieval are auditable.
8. **Transparency:** users understand what is collected, why it is retained and what happens when they delete their accounts.
9. **Separation of concerns:** public profile data, verification records, sensitive evidence and moderation intelligence are separately protected.
10. **Consistent enforcement:** safety standards apply equally across Dating, Friends, Activities, Business and Travel, including staff-affiliated accounts.

Australian Privacy Principle 11 requires reasonable protection of personal information and its destruction or de-identification when it is no longer needed for a permitted purpose, subject to applicable exceptions. This includes archived and backup copies.

## 2. Identity verification and retention

### 2.1 Didit and Swoovr responsibilities

Didit is the identity verification processor, not Swoovr's permanent identity repository (D126, D186). After successful ID verification, Swoovr:

- retrieves the verified fields from Didit and validates the result server-side;
- stores only the minimum identity record in its restricted identity store: legal first and last name, the document's date of birth, document type, issuing country and expiry date (D191);
- records the Didit session reference, verification date, method and decision;
- never stores the document number, address, document images, selfie, liveness or biometric data; and
- asks Didit to delete the member's sessions at final account deletion, retrying on failure (D173).

Swoovr does not export or keep Didit's underlying evidence. Any future proposal to keep it requires a documented purpose, legal approval and an update to this policy and the privacy notice.

The verified date of birth is never overwritten because a user changes their profile. A discrepancy triggers the controlled correction workflow (D158, D166).

### 2.2 Retention

The [retention schedule](RETENTION-SCHEDULE.md) is authoritative. In summary:

| Account status | Verified identity record | Raw ID and biometric evidence |
| --- | --- | --- |
| Active | Account lifetime while required for verification and safety | Held only by Didit, for about 30 days |
| Pending deletion (30-day recovery) | Kept, hidden, until final deletion | Held only by Didit |
| Deleted by the user | Erased at final deletion | Didit asked to delete sessions |
| Suspended | Kept for the suspension, reviewed regularly | Held only by Didit |
| Permanently terminated | 2 years, plus keyed fingerprints for ban enforcement under the registry rules | Held only by Didit |
| Legal or investigation hold | Until the valid preservation need ends, then the applicable schedule | Only what the hold captured |

These periods are ceilings for an approved purpose, not periods during which every record must be retained. Ordinary deletion must never become a blanket retention of former users' identity details. The optional restricted administrative retention of up to 90 days in D216 is a legal-gated proposal, not a current practice.

### 2.3 Securing verified identity data

Azure Database for PostgreSQL Flexible Server encrypts storage, backups and logs at rest. That does not stop an authorised database connection or a compromised application from reading plaintext fields, so Swoovr also requires:

- application-level authenticated encryption (AES-GCM) for legal names, dates of birth and other highly sensitive identity fields;
- encryption keys in Azure Key Vault or Managed HSM, never in source code, configuration files or database records;
- versioned keys and controlled rotation (`keys rotate`);
- separate keyed HMAC indexes for approved exact-match searches;
- database role separation between normal application functions and restricted identity operations;
- mandatory audit events for decryption, export and disclosure; and
- no sensitive identity data in ordinary application logs, traces or analytics.

No user can retrieve another user's legal identity through any API.

### 2.4 Encrypted evidence archives

Where Swoovr has a justified purpose to keep evidence outside the live database, it uses encrypted evidence packages with separately managed keys, isolated storage, a separate redundant copy, recorded integrity hashes and export timestamps, individual retention dates and a defensible deletion process. Routine paper archives are not permitted.

An offline archive remains personal information subject to privacy law; it is not exempt because it is disconnected from the internet.

## 3. Critical incidents and law-enforcement preservation

Reports involving sexual assault, serious threats, stalking, child exploitation, coercion, extortion or significant violence follow a formal response workflow:

1. **Report received** from a user, staff member, law enforcement or a trusted reporting channel.
2. **Immediate safety triage:** assess danger, escalation and preservation needs.
3. **Restricted evidence preservation:** relevant identity, messages, media, account and event records.
4. **Authorised investigation and disclosure:** verify the legal basis, protect the chain of custody and record access, under the police-contact procedure.

Required rules:

- A report never depends on the alleged offender's account remaining active.
- Deactivation, deletion, blocking and unsending never destroy evidence already under a valid hold.
- A preservation hold is narrowly scoped to relevant accounts, conversations, media and dates.
- Each hold records its reason, authoriser, scope, review date and release conditions.
- Evidence exports include integrity hashes, timestamps, provenance and chain-of-custody records.
- Investigators do not have unrestricted access to unrelated user information.
- Swoovr maintains a verified law-enforcement contact and escalation process.
- Where there is immediate danger, staff prioritise emergency response over routine moderation queues.

APP 6 contains an enforcement-related disclosure exception with specific record-keeping requirements (APP 6.5). It does not give police unrestricted access to the platform on request.

Swoovr can preserve only information that still exists when an incident or legal hold is identified. It cannot recover evidence already lawfully destroyed, and internal procedures and member-facing documents say so.

## 4. Mobile number and email address history

Historical contact information is kept in dedicated database tables, not only in application logs (D187). Every change to a verified mobile number or email address creates a structured, immutable audit event and a versioned contact record.

Mandatory rules:

- New email addresses and mobile numbers are verified before activation (D184).
- The previous verified value is kept encrypted, in restricted history, for 2 years from the change.
- Record when the change was requested, verified and activated, the requesting account and session, and relevant security signals.
- Notify the previous address or number where appropriate, never revealing the new value.
- Revoke relevant sessions or require fresh authentication for sensitive changes.
- Prevent reuse of old verification credentials.
- Never include plaintext contact information in standard application logs.
- For banned accounts, keep only the minimum keyed matching tokens needed for justified ban enforcement (D217).

Reference structure:

```text
AccountContactHistory
  Id
  AccountId
  ContactType           // Email, Mobile
  EncryptedValue
  LookupHmac
  ValidFrom
  ValidUntil
  VerifiedAt
  ChangedBy
  ChangeReason
  AuditEventId
  RetainUntil
```

Keep a limited, versioned history, not a permanent collection of every number someone has used: old numbers can be reassigned to innocent people. The same principle applies to changed usernames, display names, dates of birth, verification status and account ownership indicators, which may have different retention periods.

## 5. Image uploads, moderation and child sexual abuse material

Every image is screened before it becomes accessible to another user: public profile photos, chat attachments, temporary photos, group content and media in any mode (D203, D205).

### Mandatory image-processing pipeline

1. **Upload** into private quarantine, inaccessible to other users.
2. **Screening:** file validation, metadata (EXIF) removal and re-encoding, malware checks, known-CSAM matching through Project Arachnid (fingerprint only), and nudity classification.
3. **Outcome:**
   - **Allowed:** published within the member's visibility rules.
   - **Uncertain:** restricted manual review by a trained, authorised person.
   - **Known-content match:** blocked, sealed as evidence, escalated at once as urgent, and handled through the legal process.

Screening never refuses a photo or restricts an account automatically; a person decides (D205).

### Mandatory rules

- No uploaded media is available to anyone else until required checks pass.
- Replacements and edited versions are rescanned.
- Moderation covers profile pictures and private attachments without exception.
- Public profile images comply with Swoovr's public content rules, including the prohibition on nudity and explicit sexual content.
- Any private album feature requires a separately approved policy and recipient consent preferences before launch.
- Sexual content involving minors is prohibited in every context.
- Suspected CSAM triggers a specialised incident process, with access restricted to trained, authorised personnel.
- Staff never download or circulate suspected CSAM for informal review, and known-content media is never rendered.
- Only the material and metadata lawfully required are preserved, through a legally reviewed evidence-handling process.
- Applicable reporting and preservation obligations under Australian law are documented in the police-contact procedure.
- A clean hash-matching result is not proof that a photo is safe.

Known-CSAM matching cannot detect newly created abusive material, and automated nudity classification cannot reliably determine a person's age or consent.

Australia's Online Safety Act industry codes and standards, including the Relevant Electronic Services Standard that eSafety applies to dating services, impose requirements about child sexual exploitation material and other harmful content. The [compliance register](CODE-COMPLIANCE-REGISTER.md) tracks Swoovr's assessment.

Identity selfies, document images and biometric evidence never enter this pipeline or third-party content-moderation services; they stay with Didit.

## 6. Profiles across the modes

One account, one core identity, with separate mode-specific profile configurations. Users do not have unrelated identity records per mode.

| Overall profile | Mode-specific profile |
| --- | --- |
| Account identifier | Mode-specific description |
| Verified identity status | Intentions |
| Age and optional display information | Mode-specific photographs |
| Gender, pronouns and orientation (optional, separately shown or hidden) | Discovery preferences |
| Shared interests | Compatibility answers and priorities |
| Privacy and safety settings | Mode visibility |
| Moderation and account status | Who can contact the user |

For example, someone may show a casual first name in Dating but a professional introduction in Business. Identity verification, bans and safety restrictions still apply across the whole account.

Mandatory rules:

- One account may have several mode-specific presentations.
- Users explicitly control which shared data appears in each mode (D199).
- Images and biography content never cross modes without the user's consent.
- Blocks apply account-wide.
- Serious safety restrictions apply across all modes.
- Contact permissions may differ by mode.
- A user's dating activity is never disclosed to business contacts merely because both use Swoovr.
- Moderation can identify the profile version visible at the time of a reported incident.
- Mode switching never reveals hidden information about blocked or restricted users.

Sexual orientation is sensitive information. It is optional, hidden by default and never used to decide who sees whom (D138).

## 7. Messaging, unsending and deletion

Users can unsend messages, but unsending is not an unconditional immediate purge of server records.

| Action | User experience | Server behaviour |
| --- | --- | --- |
| Hide for me | Disappears from that user's view | The other participant's copy remains |
| Delete for both (within five minutes of sending) | Shown as deleted for both | Text kept in a restricted state for 30 days, then erased (D143, D167) |
| Report conversation | Report and selected messages preserved for review | Evidence protected from ordinary deletion pending case resolution |

Mandatory rules:

- The interface makes clear whether a user is hiding a message or deleting it for both.
- Deleting never removes evidence already preserved under an authorised hold.
- Normal application staff cannot browse deleted messages.
- Deleted message text never stays in ordinary tables beyond the 30-day window without a permitted purpose or preservation requirement.
- Reported conversations are preserved in a restricted evidence store.
- Staff removal of a message is shown as removed by Swoovr, not as unsent (D265).
- Message edits, if introduced, keep version history only for a limited, documented purpose.
- Messages carry server-side timestamps, sender identifiers and immutable message IDs.

For example, an abusive user might send a threat and immediately delete it. Swoovr can preserve that threat if it is reported within the 30-day window. Users are never told that a deleted message has been permanently erased while a restricted copy remains; the privacy notice explains this.

Report and Block are available directly from every conversation, with no need to visit the sender's profile first.

## 8. Blocking, bans and repeat offenders

Ban enforcement is account-wide and based on several verified signals. Swoovr keeps separate records for suspensions, permanent terminations, identifier restrictions and identity-based review (D217).

Mandatory controls:

- Bans apply across all modes.
- A banned person cannot escape enforcement by changing their display name, email or mobile number.
- Device reputation, if introduced, uses platform-supported signals, never a supposedly permanent hardware identifier.
- Identity references and approved keyed matching tokens support detection of previously banned users; an identity match alone goes to staff review.
- Phone numbers, IP addresses and shared devices never independently establish that two accounts belong to the same person.
- Serious bans record the reason, evidence reference, authorising moderator, date and review schedule.
- Banned users have an appeal process, except where specific lawful restrictions apply (D135).
- Moderators cannot reverse serious bans without an audit record and appropriate authorisation; identifier release and account reinstatement are separate actions.
- Repeat-offender detection never exposes another user's identity or previous account details.
- Staff-affiliated accounts receive enhanced independent review, never preferential treatment.

A user blocked by another user can never discover that person's profile, location, presence, photos or activities through another mode.

## 9. Reporting, consent and incident response

Reporting is available where misconduct happens.

| Location | What users can report |
| --- | --- |
| Profile | Harassment, impersonation, deception, threats |
| Individual photo | Nudity violations, stolen imagery, non-consensual content |
| Individual message | Abuse, threats, scams, unwanted sexual content |
| Conversation | Repeated harassment, coercion, stalking |
| Meeting plan or venue | Safety concerns, misconduct, fraudulent listings |
| Past connections, including blocked, disconnected and deleted members | Additional evidence or repeat incidents |

Mandatory rules:

- Reporting remains available after a block, disconnection or deactivation where a relevant interaction existed (D190).
- Reports never automatically notify the person reported.
- Reporters can attach relevant evidence.
- Serious incidents receive priority triage, with urgent escalation at any hour (D135, D159).
- Reporters are told the general outcome where safe and appropriate (D190, D267).
- Victims are never required to contact alleged offenders to resolve reports.
- Allegations are investigated fairly and never treated as established facts without assessment.
- Reports involving employees require independent review and escalation.
- Staff never investigate complaints against themselves.
- Reporters are protected against retaliation where practicable.
- Temporary restrictions may apply during an investigation where risk warrants them.

A clearly labelled **Report a serious safety incident** pathway is separate from routine content complaints. For someone reporting sexual assault it prioritises immediate safety information, the option to contact police, preservation of platform evidence and access to support services.

The eSafety Commissioner identifies blocking, reporting, location privacy and identity verification as important online dating safety features.

## 10. Location privacy and physical safety

- Never expose exact GPS coordinates through any API response.
- Calculate proximity server-side and show only rounded distance bands (D199).
- Rate-limit repeated proximity queries to prevent triangulation.
- Never expose location history to other users.
- Travel locations and dates are independently controllable.
- Users can hide proximity, activity and online presence.
- Incognito preferences are enforced by the API, not only the interface.
- Blocking prevents location-based discovery of the blocked person.
- Venue check-ins are private by default.
- Venue partners never receive a user's identity, visit history or contact information merely because the user views a sponsored placement.
- Meeting-safety check-ins and mutual confirmations require affirmative participation and have their own retention rules (D263).
- GPS history is not retained; device location is read once for a specific check and only the outcome is kept.
- Photos have GPS EXIF metadata removed before distribution.

A privacy risk assessment must test whether an attacker could infer someone's home, workplace or routine through repeated discovery requests.

## 11. Employee access and internal controls

| Role | Default access |
| --- | --- |
| Customer support | Limited account details and case information |
| Content moderator | Reported content and necessary moderation context |
| Trust and Safety investigator | Restricted case evidence under authorisation |
| Identity review officer | Necessary verification data for assigned cases |
| Developer | Sanitised non-production data only |
| Database administrator | Infrastructure access; no routine plaintext identity access |
| Security and legal officer | Controlled evidence and identity retrieval |
| System administrator | Operational permissions without unrestricted sensitive-data access |

Mandatory rules:

- All internal access requires named individual accounts and multi-factor authentication.
- Shared support accounts are prohibited.
- Least-privilege access is mandatory.
- Sensitive identity retrieval requires a recorded purpose and, where practicable, case-specific approval.
- Bulk exports require elevated authorisation.
- Staff never search for friends, partners, acquaintances or public figures without a legitimate work purpose.
- Every restricted-data access is audited.
- Production debugging never exposes private images or messages unnecessarily.
- Staff never transfer user information to personal email, personal cloud storage or unapproved AI services.
- Access is revoked promptly when employment or contracts end.
- Staff receive privacy, CSAM-handling, safeguarding and incident-response training appropriate to their role.
- Contractors and overseas support personnel require access controls and data-handling agreements.

Restricted information is reached through just-in-time elevated access with automatic expiry, not permanent privileges (D197).

## 12. Security operations and data breaches

Required controls:

- centralised security event monitoring;
- automated detection of unusual identity-data access and bulk exports;
- database and infrastructure access auditing;
- encrypted, tested backups;
- key rotation and documented recovery procedures;
- periodic permission reviews;
- independent penetration testing before launch and after major security changes;
- vulnerability disclosure and remediation procedures;
- formal incident severity classifications;
- evidence-preserving security investigation processes;
- supplier risk assessments for Didit, Microsoft, Cloudflare, Twilio, MailerSend and moderation providers; and
- a documented data breach response plan: the [breach response runbook](BREACH-RESPONSE-RUNBOOK.md).

Under the Notifiable Data Breaches scheme, suspected eligible breaches must be assessed expeditiously, taking reasonable steps to complete the assessment within 30 days, and eligible breaches notified to the OAIC and affected individuals as soon as practicable. Swoovr's response begins immediately; the statutory period is a limit, not an operational target.

## 13. Account lifecycle and deletion

Deactivation, suspension, deletion and permanent termination are different states with different retention rules.

| Account state | Expected behaviour |
| --- | --- |
| Active | Normal use and applicable verification |
| Pending deletion | Hidden from discovery, signed out, recoverable for 30 days (D170) |
| Restricted or suspended | Access limited pending review or enforcement |
| Deleted | Final deletion erases eligible personal information; a minimal tombstone remains |
| Permanently terminated | Access denied; only justified enforcement and evidence data retained |
| Legal hold | Relevant records preserved independently of the account lifecycle |

Additional rules:

- Users can delete their accounts in the app and on the web without contacting support.
- Deactivation is never represented as deletion.
- The privacy notice explains what is retained after deletion and why.
- A deletion request never silently removes evidence under a valid hold.
- Deleted accounts stop appearing in discovery and public profiles immediately.
- A permanently terminated account cannot be reactivated through ordinary recovery flows.
- Retention and deletion jobs cover external processors, backups, archives and verification references.
- Every deletion action produces an auditable completion or failure record.
- Founder recovery tokens stay independent of retained identity documents and ban enforcement (D65, D173).

## 14. Additional product safety rules

| Area | Requirement |
| --- | --- |
| Age verification | 18+ eligibility is enforced; an under-18 date of birth locks the account; credible underage reports are reviewed at once |
| Photo verification | Revalidate the badge when relevant face photographs change |
| Identity badges | Photo Verified and ID Verified remain independent |
| Date of birth corrections | The verified date of birth takes precedence; conflicts enter review |
| Screenshot protection | Best-effort deterrence, never a guarantee |
| Private albums | Explicit recipient opt-in; revoke access where possible; not launched without a separate approved policy |
| Sensitive images | Honour recipient content preferences before delivery |
| Impersonation | A dedicated identity-misuse reporting process |
| Scam detection | Detect suspicious bulk outreach, payment requests and repetitive messaging (D266) |
| AI-generated content | Deceptive synthetic profile imagery is prohibited |
| Links and attachments | Screen unsafe links, malicious files and suspicious redirects |
| Spam | Rate-limit new and unverified accounts |
| Account takeover | Notify users about significant security changes |
| Staff accounts | An employee badge is not a substitute for identity verification |
| Official bots | Clearly identified and never impersonating humans |
| Advertising | Never expose private messages, verified identities or private content to advertising partners |
| Analytics | Prefer aggregated information; prohibit sensitive-attribute profiling |
| Moderation appeals | Independent decisions and escalation pathways (D135) |
| Automated decisions | Every automated decision that can significantly affect a user is listed in the privacy notice (APP 1.7, from 10 December 2026) before it ships |

## 15. Additional internal rules

**No unrestricted staff impersonation.** Administrators cannot silently sign in as users, read their messages or alter their accounts. Any approved support impersonation feature must be time-limited, logged and narrowly permissioned.

**No production data in development.** Real identity documents, private media and conversation histories are never copied to developer machines, test databases or CI logs.

**No invisible message modification.** Neither staff nor users can alter delivered message content without the system recording the change under the messaging rules.

**No silent security downgrade.** Removing MFA, changing contact details or recovering an account never reduces verification requirements without recording and assessing the security event.

**No trusting client-side moderation.** The .NET MAUI and web clients improve the experience, but upload approval, access restrictions and enforcement happen server-side.

**No blanket employee access to sensitive content.** Private images and messages are available to staff only through justified case workflows.

**No commercial reuse of member data.** Identity verification images, legal identity details, private messages, photos and biometric data are never repurposed for advertising, AI model training or unrelated profiling.

**No automatic guilty finding from an allegation.** Serious reports may justify precautionary restrictions, but enforcement follows a documented, fair assessment.

**No suppression of user safety evidence.** Blocking, disconnecting and ordinary account closure never circumvent legally justified evidence holds.

**No indefinite default retention.** Every sensitive data category has an owner, purpose, retention rule, deletion mechanism and review process.

## 16. Technical implementation architecture

Logical services within Swoovr's .NET backend (not necessarily separate deployments):

```text
Swoovr API
│
├── Identity & Verification
│   ├── IIdentityVerificationService
│   ├── IVerifiedIdentityStore
│   ├── IIdentityEncryptionService
│   └── IIdentityHistoryService
│
├── Trust & Safety
│   ├── IContentModerationService
│   ├── ICsamScreeningService
│   ├── IReportManagementService
│   ├── IBanEnforcementService
│   └── IDeviceReputationService
│
├── Evidence & Compliance
│   ├── IEvidencePreservationService
│   ├── ILegalHoldService
│   ├── IEvidenceArchiveService
│   ├── ILawEnforcementDisclosureService
│   └── IRetentionPolicyService
│
├── User Privacy
│   ├── IAccountLifecycleService
│   ├── IPrivacyPreferenceService
│   ├── IDataExportService
│   └── IDataDeletionService
│
└── Auditing & Security
    ├── ISecurityAuditService
    ├── ISensitiveAccessAuthorisationService
    └── ISecurityIncidentService
```

### Database separation

Avoid one large `Users` table.

| Logical data area | Purpose |
| --- | --- |
| `Accounts` | Account ownership and state |
| `Profiles` | Shared public-facing data |
| `ModeProfiles` | Per-mode content and settings |
| `VerifiedIdentities` | Restricted encrypted identity fields |
| `VerificationHistory` | Verification events, references and outcomes |
| `ContactHistory` | Previous verified contact details |
| `AccountRestrictions` | Suspensions, bans and enforcement |
| `Messages` | Current conversation data |
| `MessageRetention` | Deletion lifecycle and limited recovery |
| `ModerationCases` | Reports, decisions and appeals |
| `EvidenceRecords` | Evidence references, hashes and provenance |
| `LegalHolds` | Preservation instructions and reviews |
| `SensitiveAccessAudit` | Restricted-data access history |
| `RetentionSchedules` | Record-level retention and deletion instructions |

These can live in one Azure PostgreSQL instance in Sydney with separate schemas and database roles. Sensitive evidence archives remain separately protected.

### Data residency

Swoovr is operated from Australia. All member data and photos are stored and processed in Sydney, Australia (the primary region, within JAPAC). For availability, data may be replicated to storage in the other hosting regions: JAPAC, EMEA (Europe, the Middle East and Africa), NAMER (North America) and LATAM (Latin America). Replicas are storage-only: no application, moderation, analytics or staff processing runs against them. They use the same encryption and key controls, are deleted when the Sydney copy is deleted or disposed of, and are never restored or promoted without the deletion and hold reconciliation that applies to backups. The only processing outside Sydney is by named providers, such as Didit and MailerSend in the European Union. EU and UK members give their data directly to Swoovr in Australia; under EDPB Guidelines 05/2021 that collection is not a GDPR transfer, but the GDPR applies to Swoovr directly (Article 3(2)). Transfers arise only when an EU or UK processor (Didit, MailerSend, EU hosting for replicas) sends personal data to Swoovr in Australia; those agreements must include the processor-to-controller standard contractual clauses (Module 4) or the UK addendum.

### Encryption design

1. Azure PostgreSQL storage encryption, protecting database storage, logs and backups.
2. Application-level authenticated encryption, protecting highly sensitive fields even when rows are read without decryption authority.
3. Separate archival encryption for evidence packages.

Keys are managed in Azure Key Vault or Managed HSM with managed identities and narrowly scoped permissions. The API provides a controlled decryption pathway based on authorisation, never a general-purpose endpoint able to decrypt arbitrary identity records.

## 17. Minimum launch requirements

Before public launch, each item needs dated evidence in the [compliance register](CODE-COMPLIANCE-REGISTER.md):

1. Verified identity storage with field-level protection and restricted access.
2. Didit deletion and retention integration.
3. Approved account-state retention schedule.
4. Legal hold and evidence preservation workflow.
5. Law-enforcement request and disclosure procedure.
6. CSAM screening on every applicable media upload path.
7. Nudity, explicit-content and abuse moderation.
8. Message deletion, report and retention semantics.
9. Historical email and mobile number tracking.
10. Account-wide blocking and ban enforcement.
11. Mode-specific privacy and profile visibility controls.
12. Serious incident reporting and escalation.
13. Staff access auditing, MFA and least privilege.
14. Data deletion, archive expiry and backup lifecycle testing, including a restore drill.
15. Independent Australian privacy and online-safety legal review, including approval of the privacy notice, terms and community guidelines.

## 18. External standards and legal reviews

Swoovr assesses the following before public launch:

- [Australian Privacy Principles](https://www.oaic.gov.au/privacy/australian-privacy-principles), including APP 1.7–1.9 automated-decision transparency from 10 December 2026.
- [Notifiable Data Breaches scheme](https://www.oaic.gov.au/privacy/notifiable-data-breaches).
- [Online Safety codes and standards](https://www.esafety.gov.au/industry/codes), including the Relevant Electronic Services Standard as applied to dating services.
- [eSafety Safety by Design](https://www.esafety.gov.au/industry/safety-by-design).
- The voluntary Australian Online Dating Industry Code: see the [gap analysis](ONLINE-DATING-CODE.md).
- [Project Arachnid](https://www.protectchildren.ca/en/programs-and-initiatives/project-arachnid/): industry integration terms and operational requirements.
- [Azure Database for PostgreSQL data encryption](https://learn.microsoft.com/en-us/azure/postgresql/security/security-data-encryption): storage and customer-managed key options.
- The Australian Consumer Law, including unfair contract terms, for member-facing terms.
- The privacy, platform and biometric laws of each market before Swoovr is offered there: see the market entry table in the [compliance register](CODE-COMPLIANCE-REGISTER.md).

## Policy commitments

**Protect people's identities.** Verified identity data is segregated, encrypted, access-controlled and never publicly exposed.

**Protect people from harm.** Reports are taken seriously, with clear escalation and platform-wide enforcement.

**Preserve appropriate evidence.** When a serious incident is identified, relevant available records are secured independently of account deletion.

**Help lawful investigations.** Swoovr retrieves and provides lawfully retained records through a controlled, documented process.

**Respect privacy and deletion.** Ordinary users are not subjected to indefinite surveillance or blanket retention of sensitive documents because a future crime is hypothetically possible.

**Hold Swoovr staff accountable.** Every sensitive operation is restricted, justified and auditable.

The policy choice needing the most careful legal assessment is how long Swoovr may keep an identifiable record after a voluntarily deleted account where there was no misconduct or report. Swoovr's current answer (D170, D216) is a minimal tombstone without identity details for 2 years, with identity records erased at final deletion. For an active ID-verified account, keeping the minimum verified identity fields is justified as part of providing an ongoing verification and safety service. The combination that best supports police after a serious incident is minimum verified identity records during active membership, justified post-closure retention for terminated accounts, accessible serious-incident reporting and rapid evidence preservation.
