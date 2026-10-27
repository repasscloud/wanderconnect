# Swoovr — Internal Privacy, Trust, Safety & Evidence Protection Policy

Proposed policy — legal review required

Owner: RePass Cloud Pty Ltd\
Applies to: Swoovr iOS, Android, Web, API, administration, contractors, support, hosting and external processors\
Scope: All four application modes, public profiles, private content, messages, identity verification and moderation\
Policy classification: Internal — Restricted\
Version: Draft 1.0 — 27 October 2026

I recommend establishing this as a mandatory internal policy, with technical controls built into Swoovr rather than relying on employees remembering procedures.

The most important distinction is between what Swoovr needs for day-to-day operation, what it needs to prevent repeat abuse, and what it must preserve when a serious incident is reported.

Those categories should have different access permissions, retention periods and deletion rules.

## 1. Core principles

These should govern every feature Swoovr develops.

1. Safety by design: Safety controls must be designed into features before release.
2. Identity accountability: Swoovr must be able to identify an ID-verified user through information lawfully retained after Didit removes its verification records.
3. Privacy by default: Personal information must not be exposed unnecessarily to other users, employees, advertisers or external providers.
4. Evidence preservation: Credible reports of assault, stalking, threats, exploitation or serious fraud must trigger assessment for preservation of relevant evidence.
5. Data minimisation: Retain what is necessary, not everything that is technically possible to collect.
6. Controlled disclosure: Law-enforcement requests must be authenticated, authorised, documented and limited to relevant information.
7. Accountability: Staff access, moderation decisions, identity changes and evidence retrieval must be auditable.
8. Transparency: Users must understand what is collected, why it is retained and what happens when they delete their accounts.
9. Separation of concerns: Public profile data, verification records, sensitive evidence and moderation intelligence must be separately protected.
10. Consistent enforcement: Safety standards apply equally across dating, friendship, business and other Swoovr modes, including staff-affiliated accounts.

Australian Privacy Principle 11 requires reasonable protection of personal information and its destruction or de-identification when it is no longer needed for a permitted purpose, subject to applicable exceptions. This includes archived copies.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.oaic.gov.au\&sz=32)

OAIC



## 2. Identity verification and retention

### 2.1 Didit and Swoovr responsibilities

Didit should be treated as the identity verification processor, not the permanent authoritative identity repository for Swoovr.

Following successful ID verification:

- Retrieve the verified fields from Didit.
- Validate the verification result server-side.
- Store the minimum necessary verified identity information in Swoovr's restricted identity store.
- Record the Didit session reference, verification date, method and decision.
- Export an encrypted verification report only where an approved retention purpose justifies keeping the underlying evidence.
- Delete Didit data once the applicable retention period expires, subject to lawful preservation requirements.

Never overwrite the verified date of birth simply because the user changes their public profile. A discrepancy must trigger a controlled correction or verification workflow.

### 2.2 Retention schedule

Proposed maximum periods for consideration, not Australian statutory minimums. All post-closure retention requires documented justification.

| Account status             | Verified identity record                                                    | Raw ID / biometric evidence                                |
| -------------------------- | --------------------------------------------------------------------------- | ---------------------------------------------------------- |
| Active                     | Account lifetime while required for verification and safety                 | Short verification/review period; longer only if justified |
| User-deactivated           | Up to 12 months                                                             | Normally 30 days; up to 90 for a documented review         |
| Swoovr-suspended           | Suspension period, reviewed regularly                                       | Up to 90 days if relevant                                  |
| User-deleted               | Up to 12 months for a specific justified purpose                            | Normally 30 days                                           |
| Permanently banned         | Minimal identifiers while needed for ban enforcement, periodically reviewed | Only while independently justified                         |
| Legal / investigation hold | Until the valid preservation need ends                                      | Until the valid preservation need ends                     |

Important implementation rule: These periods are ceilings for an approved purpose, not automatic periods during which every record must be retained. Ordinary deletion should not be converted into a blanket 12-month collection of former users' identity details.

### 2.3 Securing verified identity data

Azure Database for PostgreSQL Flexible Server already provides encryption at rest, including backups and logs. However, its storage encryption does not prevent an authorised database connection or compromised application from reading plaintext identity fields.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://learn.microsoft.com\&sz=32)

Microsoft Learn



Therefore Swoovr should additionally implement:

- Application-level encryption for legal names, DOB, verified addresses and other highly sensitive identity fields where practical.
- Encryption keys stored in Azure Key Vault or Managed HSM, not embedded in source code, configuration files or database records.
- Versioned keys and controlled rotation.
- Separate, keyed HMAC indexes for approved exact-match searches.
- Database role separation between normal application functions and restricted identity operations.
- Mandatory audit events for decryption, export and disclosure.
- No sensitive identity data in ordinary application logs, traces or analytics.

A user should never be able to retrieve another user's legal identity through the public API.

### 2.4 Encrypted offline archives

If Swoovr decides it has a justified purpose to keep full verification evidence, use:

- Encrypted digital evidence packages.
- Separately managed encryption keys.
- Offline or tightly isolated archive storage.
- A redundant backup stored separately.
- Recorded integrity hashes and export timestamps.
- Individual retention dates and defensible deletion processes.

Avoid routine paper archives. Paper introduces access, indexing, physical security and destruction difficulties.

An offline archive remains personal information subject to privacy law; it is not exempt because it is disconnected from the internet.

## 3. Critical incident and law-enforcement preservation

This is the part I would treat as one of the highest-priority safety capabilities before launch.

For reports involving sexual assault, serious threats, stalking, child exploitation, coercion, extortion or significant violence, Swoovr must have a formal response workflow.

Serious incident reported

User, staff, law enforcement or trusted reporting channel

Immediate safety triage

Assess danger, escalation and preservation needs

Restricted evidence preservation

Relevant identity, messages, media, account and event records

Authorised investigation and disclosure

Verify legal basis, protect chain of custody and record access

Required rules:

- A report must not depend on the alleged offender's account remaining active.
- Deactivation, deletion, blocking and unsending must not destroy evidence already under a valid hold.
- A preservation hold must be narrowly scoped to relevant accounts, conversations, media and dates.
- Each hold must record its reason, authoriser, scope, review date and release conditions.
- Evidence exports must include integrity hashes, timestamps, provenance and chain-of-custody records.
- Investigators must not have unrestricted access to unrelated user information.
- Swoovr must establish a verified law-enforcement contact and escalation process.
- Where there is immediate danger, staff must prioritise emergency response rather than routine moderation queues.

Australian APP 6 contains an enforcement-related disclosure exception and specific recordkeeping requirements when relying on that exception. It does not mean police can obtain unrestricted access to the platform on request.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.oaic.gov.au\&sz=32)

OAIC



A fundamental limitation: Swoovr can preserve information that still exists when an incident or legal hold is identified. It cannot guarantee recovery of evidence already lawfully destroyed. This needs to be clearly reflected in internal procedures and user-facing documentation.

## 4. Mobile number and email address history

Decision: Keep historical contact information in dedicated database tables, not only in application logs.

Every change to a verified mobile number or email address should create a structured, immutable audit event and a versioned contact record.

### Mandatory rules

- New email addresses and mobile numbers must be verified before activation.
- Preserve the previous verified value in restricted, encrypted history only for the documented security and account-recovery period.
- Record when the change was requested, verified and activated.
- Record the requesting account, device/session and relevant security signals.
- Notify the previous contact address or number where appropriate, without leaking the new value.
- Revoke relevant sessions or require fresh authentication for sensitive changes.
- Prevent immediate reuse of old verification credentials.
- Never include plaintext contact information in standard application logs.
- For banned accounts, retain the minimum necessary matching tokens for justified ban enforcement.

Suggested database structure:

```
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

Recommended: Keep a limited, versioned history rather than a permanent collection of every phone number someone has used. Old numbers can be reassigned to innocent people.

The same history principle should apply to changed usernames, display names, dates of birth, verification status and account ownership indicators. However, these fields need not all have identical retention periods.

## 5. Image uploads, moderation and CSAM prevention

Decision: Every image must be screened before it becomes accessible to another user.

That includes public profile photos, private albums, chat attachments, temporary photos, group content and media uploaded through business or activity modes.

The Canadian service you're referring to is likely Project Arachnid, operated by the Canadian Centre for Child Protection.

Project Arachnid offers industry API capabilities to help platforms proactively identify known child sexual abuse material. Its public reporting describes industry API trials, but you should confirm eligibility, current production access, coverage and commercial terms directly with the provider.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.protectchildren.ca\&sz=32)

protectchildren.ca+1



### Mandatory image-processing pipeline

New image or video uploaded

Private quarantine — inaccessible to other users

Security and content scanning

File validation, malware, known-CSAM matching, nudity and abuse classification

Allowed

Publish within applicable visibility rules

Uncertain

Restricted manual review

Prohibited

Block, escalate and follow legal process

### Mandatory rules

- No uploaded media is publicly or privately available until required scans pass.
- Replacements and edited versions must be rescanned.
- Image moderation must include profile pictures and private attachments without exception.
- Public profile images must comply with Swoovr's public content rules, including restrictions on nudity and explicit sexual content.
- Private album content must follow a separately approved policy and recipient consent preferences.
- Sexual content involving minors is prohibited in every context.
- Suspected CSAM must trigger a specialised incident process, with access restricted to trained, authorised personnel.
- Staff must not download or circulate suspected CSAM for informal review.
- Preserve only the material and metadata lawfully required, through a legally reviewed evidence-handling process.
- Document applicable reporting and preservation obligations under Australian law.
- Do not treat a clean hash-matching result as proof that a photo is safe.

Important limitation: Known-CSAM matching cannot guarantee detection of newly created abusive material, and automated nudity classification alone cannot reliably determine a person's age or consent.

Australia's Online Safety Codes and Standards impose requirements addressing serious harmful content, including child sexual exploitation material, with different obligations depending on the service classification. Swoovr should assess both its social networking and messaging functions.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.esafety.gov.au\&sz=32)

eSafety Commissioner



Also, normal identity selfies, document images and biometric evidence should be handled in a separate secured verification pipeline, not indiscriminately submitted to third-party content-moderation services.

## 6. Profiles across the four Swoovr modes

Decision: One account, one core identity, with separate mode-specific profile configurations.

Users should not have four unrelated identity records. They should have one underlying account with independently controlled profile presentations and visibility rules.

### Recommended data model

| Overall profile                        | Mode-specific profile                |
| -------------------------------------- | ------------------------------------ |
| Account identifier                     | Mode-specific description            |
| Verified identity status               | Looking-for / intentions             |
| Age and optional display information   | Mode-specific photographs            |
| Pronouns and selected identity details | Discovery preferences                |
| Shared interests                       | Compatibility answers and priorities |
| Privacy and safety settings            | Mode visibility                      |
| Moderation/account status              | Who can contact the user             |

For example, someone may want to display a casual first name in dating mode but a professional introduction in business mode.

Their identity verification, bans and safety restrictions must still apply across the entire account.

### Mandatory rules

- One underlying account may have multiple mode-specific profile presentations.
- Users must explicitly control which shared data is exposed in each mode.
- Images and biography content must not automatically cross modes without the user's consent.
- A block should apply account-wide, not just within one mode.
- Serious safety restrictions must apply across all modes.
- Contact permissions may differ by mode.
- A user's dating activity must never be disclosed to business contacts merely because both use Swoovr.
- Moderation must be capable of identifying the profile version and content visible at the time of a reported incident.
- Mode switching must not reveal hidden information about blocked or restricted users.

This separation is especially important because Swoovr may contain sensitive personal information about users' relationships, sexuality and social activities.

## 7. Messaging, unsending and deletion

Decision: Allow users to unsend messages, but do not treat unsending as an unconditional immediate purge of server records.

I would distinguish three separate actions:

| Action              | User experience                                       | Server behaviour                                                  |
| ------------------- | ----------------------------------------------------- | ----------------------------------------------------------------- |
| Delete for me       | Disappears from that user's conversation              | Other participant's copy remains                                  |
| Unsend for everyone | Disappears from both normal conversation views        | Content enters a restricted, time-limited deletion state          |
| Report conversation | Report and relevant messages are preserved for review | Evidence protected from ordinary deletion pending case resolution |

### Mandatory rules

- Users must understand whether they are deleting locally or unsending for everyone.
- Unsending must not delete already-preserved evidence under an authorised hold.
- Normal application staff must not be able to browse unsent messages.
- Deleted messages must not remain indefinitely in ordinary database tables without justification.
- A short, disclosed unsend recovery window, provisionally 30 days, may be considered for safety investigations, subject to legal assessment.
- Once that period expires, remove content unless a permitted purpose or preservation requirement remains.
- Reported conversations must be preserved in a restricted evidence store.
- Message edits should have controlled version history only for a limited, documented purpose.
- Messages must include server-side timestamps, sender identifiers and immutable message IDs.

For example, an abusive user might send a threat and immediately unsend it.

Swoovr should be able to preserve that threat if reported while it is still within the applicable preservation period.

However, users should not be told that an unsent message has been permanently erased when a restricted copy is still retained. This must be clearly explained in the privacy policy.

I would also add Report and Block directly from any conversation, with no requirement to first visit the sender's profile.

## 8. Blocking, bans and repeat offenders

Decision: Ban enforcement is account-wide and based on multiple verified signals.

Swoovr should maintain separate records for account suspension, permanent bans, device restrictions and identity-based enforcement.

Mandatory controls:

- Bans apply across all four application modes.
- A banned account cannot escape enforcement by changing its display name, email or phone number.
- Device reputation should use platform-supported signals rather than relying on a supposedly permanent hardware serial number.
- Identity references and approved matching tokens should support detection of previously banned users.
- Phone numbers, IP addresses and shared devices must not independently establish that two accounts belong to the same person.
- Serious bans must include the reason, evidence reference, authorising moderator, date and review schedule.
- Banned users must have an appeal process, except where specific lawful restrictions apply.
- Moderators must not be able to quietly reverse serious bans without an audit record and appropriate authorisation.
- Repeat offender detection must not expose another user's identity or previous account details.
- Staff-affiliated accounts must be subject to enhanced independent review rather than preferential treatment.

Additional rule: A user blocked by another user must not be able to discover that person's private profile, location, presence, albums or activities through another application mode.

## 9. Reporting, consent and incident response

A trustworthy platform should provide reporting at the point where misconduct occurs.

### Required report entry points

| Location                | What users can report                                     |
| ----------------------- | --------------------------------------------------------- |
| Main profile            | Harassment, impersonation, deception, threats             |
| Individual photo        | Nudity violations, stolen imagery, non-consensual content |
| Private album           | Sexual exploitation, coercion, inappropriate sharing      |
| Individual message      | Abuse, threats, scams, unwanted sexual content            |
| Conversation            | Repeated harassment, coercion, stalking                   |
| Venue / activity        | Safety concerns, misconduct, fraudulent listings          |
| Blocked-account history | Additional evidence or repeat incidents                   |

### Mandatory rules

- Reporting must remain available even after someone blocks, unmatched with or deactivates another account, where a relevant interaction history exists.
- Reports must not automatically notify the person being reported.
- Reporters must be able to attach relevant evidence.
- Serious incidents must receive priority triage at all hours.
- Users must be informed about the general outcome where safe and appropriate.
- Victims must not be required to contact alleged offenders to resolve reports.
- Allegations must be investigated fairly and not treated as established facts without appropriate assessment.
- Reports involving employees require independent review and escalation.
- Moderation employees must not independently investigate complaints against themselves.
- Reporters must be protected against retaliation where practicable.
- Account restrictions may be applied temporarily during investigations where risk warrants them.

I would also implement a clearly labelled Serious Safety Incident reporting pathway, separate from routine content complaints.

For someone reporting sexual assault, it should prioritise immediate safety information, the option to contact police, preservation of existing platform evidence and access to relevant support services.

The Australian eSafety Commissioner specifically identifies blocking, reporting, location privacy and identity verification as important online dating safety features.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.esafety.gov.au\&sz=32)

eSafety Commissioner



## 10. Location privacy and physical safety

Given Swoovr's location-based discovery, travel mode and venue check-ins, these controls are particularly important.

- Never publicly expose exact GPS coordinates through profile API responses.
- Calculate proximity server-side and display approximate distances where appropriate.
- Prevent repeated proximity queries designed to triangulate a person's precise location.
- Do not expose location history to other users.
- Travel locations and dates must be independently controllable.
- A user must be able to hide proximity, activity and online presence.
- Incognito preferences must be enforced by the API, not only the interface.
- Blocking must prevent location-based discovery of the blocked person.
- Venue check-ins must be private by default.
- Venue partners must not receive a user's identity, visit history or contact information merely because the user views a sponsored placement.
- Safety check-ins and mutual QR confirmations must require affirmative participation and have independent retention rules.
- GPS history should not be retained by default unless needed for a specific, disclosed feature.
- Photos should have GPS EXIF metadata removed before distribution.

A privacy risk assessment should specifically test whether an attacker could infer someone's home, work location or daily routine through repeated discovery requests.

## 11. Employee access and internal controls

This is essential because an internal employee with broad database access can create a privacy breach even if external security is strong.

| Employee role               | Default access                                                     |
| --------------------------- | ------------------------------------------------------------------ |
| Customer support            | Limited account details and case information                       |
| Content moderator           | Reported content and necessary moderation context                  |
| Trust & Safety investigator | Restricted case evidence under authorisation                       |
| Identity review officer     | Necessary verification data for assigned cases                     |
| Developer                   | Sanitised non-production data                                      |
| Database administrator      | Infrastructure access; no routine plaintext identity access        |
| Security / legal officer    | Controlled evidence and identity retrieval                         |
| System administrator        | Operational permissions without unrestricted sensitive-data access |

Mandatory rules:

- All internal access requires named individual accounts and MFA.
- Shared support accounts are prohibited.
- Least-privilege access is mandatory.
- Sensitive identity retrieval requires a recorded purpose and preferably case-specific approval.
- Bulk data exports require elevated authorisation.
- Staff must not search for friends, partners, acquaintances or celebrities without a legitimate work purpose.
- Every restricted-data access must be audited.
- Production debugging must not expose private images or messages unnecessarily.
- Staff must not transfer user information to personal email, personal cloud storage or unauthorised AI services.
- Access must be revoked promptly when employment or contracts end.
- Employees must receive privacy, CSAM-handling, safeguarding and incident-response training appropriate to their responsibilities.
- Contractors and overseas support personnel require appropriate access controls and data-handling agreements.

Recommended: Implement just-in-time access to restricted information, with automatic expiry rather than permanent elevated privileges.

## 12. Security operations and data breaches

Required internal controls:

- Centralised security event monitoring.
- Automated detection of unusual identity-data access and bulk exports.
- Database and infrastructure access auditing.
- Encrypted, tested backups.
- Key rotation and documented recovery procedures.
- Periodic permission reviews.
- Independent penetration testing before launch and after major security changes.
- Vulnerability disclosure and remediation procedures.
- Formal incident severity classifications.
- Evidence-preserving security investigation processes.
- Supplier risk assessments for Didit, hosting providers, moderation providers and messaging services.
- A documented data breach response plan.

For organisations covered by Australia's Notifiable Data Breaches scheme, eligible breaches generally require notification to affected individuals and the OAIC. Suspected eligible breaches must be assessed expeditiously, with reasonable steps taken to complete the assessment within 30 calendar days.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.oaic.gov.au\&sz=32)

OAIC



Swoovr's internal incident response should begin immediately, rather than using that statutory assessment period as an operational target.

## 13. Account lifecycle and deletion

Decision: Deactivation, disabling, deletion and permanent bans must be different states with explicitly different retention policies.

| Account state      | Expected behaviour                                                            |
| ------------------ | ----------------------------------------------------------------------------- |
| Active             | Normal use and applicable verification                                        |
| User-deactivated   | Hidden from discovery, recoverable during defined window                      |
| Swoovr-disabled    | Access restricted pending review or enforcement                               |
| User-deleted       | Closure workflow begins; eligible personal information scheduled for deletion |
| Permanently banned | Access denied; only justified enforcement and evidence data retained          |
| Legal hold         | Relevant records preserved independently of ordinary account lifecycle        |

Additional rules:

- Users must be able to request account deletion without contacting support.
- Deactivation must not be represented as deletion.
- The system must clearly explain what information is retained after deletion and why.
- A deletion request must not silently remove evidence subject to a valid hold.
- Deleted accounts must cease appearing in discovery and public profiles promptly.
- Users must not be able to reactivate a permanently banned account through ordinary recovery flows.
- Retention and deletion jobs must handle external processors, backup copies, offline archives and verification references.
- All deletion actions must generate auditable completion or failure records.
- Founder status recovery tokens must remain independent of retained identity documents and ban enforcement.

## 14. Additional product safety rules

These are additional decisions I would include in Swoovr's product requirements.

| Area                  | Internal requirement                                                                                |
| --------------------- | --------------------------------------------------------------------------------------------------- |
| Age verification      | 18+ eligibility must be enforced; credible underage reports require immediate review                |
| Photo verification    | Revalidate the badge when relevant face photographs change                                          |
| Identity badges       | Photo Verified and ID Verified remain independent                                                   |
| DOB corrections       | Verified DOB takes precedence; conflicts enter review                                               |
| Screenshot protection | Best-effort deterrence, never a guarantee                                                           |
| Private albums        | Explicit recipient opt-in; revoke access where possible                                             |
| Sensitive images      | Honour recipient content preferences before delivery                                                |
| Impersonation         | Provide a dedicated identity misuse reporting process                                               |
| Scam detection        | Detect suspicious bulk outreach, payment requests and repetitive messaging                          |
| AI-generated content  | Establish rules for deceptive synthetic profile imagery                                             |
| Links and attachments | Screen unsafe links, malicious files and suspicious redirects                                       |
| Spam                  | Rate-limit new accounts and unverified users                                                        |
| Account takeover      | Notify users about significant security changes                                                     |
| Staff accounts        | Employee badge is not a substitute for identity verification                                        |
| Official bots         | Clearly identified and prohibited from impersonating humans                                         |
| Advertising           | Never expose private messages, verified identities or private album content to advertising partners |
| Data analytics        | Prefer aggregated information; prohibit unauthorised sensitive-attribute profiling                  |
| Moderation appeals    | Record independent decisions and escalation pathways                                                |

## 15. Additional internal rules I consider essential

These address issues that are easy to miss while building the application.

No unrestricted staff impersonation. Administrators must not be able to silently sign in as users, read their messages or alter their accounts. Any approved support impersonation feature must be time-limited, logged and narrowly permissioned.

No production data in development. Real identity documents, private media and conversation histories must not be copied into developer machines, test databases or CI logs.

No invisible message modification. Neither staff nor users should be able to alter delivered message content without the system recording the relevant change under the messaging policy.

No silent security downgrade. Removing MFA, changing contact details or recovering an account must not reduce verification requirements without recording and assessing the security event.

No trusting client-side moderation. MAUI and WebAssembly controls improve the user experience, but upload approval, access restrictions and enforcement must occur server-side.

No blanket employee access to sensitive content. Private images and messages should only become available to appropriate staff through justified case workflows.

No commercial reuse of identity evidence. Identity verification images, legal identity details, private messages and biometric data must not be repurposed for advertising, AI model training or unrelated profiling.

No automatic guilty finding from an allegation. Serious safety reports may justify precautionary restrictions, but subsequent enforcement must follow a documented, fair assessment process.

No suppression of user safety evidence. Blocking, unmatching and ordinary account closure must not circumvent legally justified evidence holds.

No indefinite default retention. Every sensitive data category needs an owner, purpose, retention rule, deletion mechanism and review process.

## 16. Technical implementation architecture

I would introduce the following logical services within Swoovr's .NET backend. They don't necessarily need to be separate deployable applications.

```
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

### Important database separation

I would avoid putting everything into one large `Users` table.

| Logical data area      | Purpose                                          |
| ---------------------- | ------------------------------------------------ |
| `Accounts`             | Account ownership and state                      |
| `Profiles`             | Shared public-facing data                        |
| `ModeProfiles`         | Per-mode content and settings                    |
| `VerifiedIdentities`   | Restricted encrypted identity fields             |
| `VerificationHistory`  | Verification events, references and outcomes     |
| `ContactHistory`       | Previous verified contact details                |
| `AccountRestrictions`  | Suspensions, bans and enforcement                |
| `Messages`             | Current conversation data                        |
| `MessageRetention`     | Deletion lifecycle and limited recovery          |
| `ModerationCases`      | Reports, decisions and appeals                   |
| `EvidenceRecords`      | Evidence references, hashes and provenance       |
| `LegalHolds`           | Preservation instructions and reviews            |
| `SensitiveAccessAudit` | Restricted-data access history                   |
| `RetentionSchedules`   | Record-level retention and deletion instructions |

These can initially live in one Azure PostgreSQL instance, with separate schemas and database roles. Sensitive evidence archives should remain separately protected.

### Encryption design

I would use three levels of protection:

1. Azure PostgreSQL storage encryption, protecting database storage, logs and backups.
2. Application-level authenticated encryption, protecting highly sensitive identity fields even when database rows are accessed without the correct decryption authority.
3. Separate archival encryption, protecting exported verification reports and evidence packages.

Use Azure Key Vault or Managed HSM for key management, with managed identities and narrowly scoped permissions. Avoid placing recoverable encryption keys directly in the software or database.

The API should provide a controlled decryption pathway based on authorisation, rather than a general-purpose endpoint capable of decrypting arbitrary identity records.

## 17. Minimum launch requirements

Pre-launch control checklist

0 / 15

Verified identity storage with field-level protection and restricted access

Didit export, deletion and retention integration

Documented account-state retention schedule

Legal hold and evidence preservation workflow

Law-enforcement request and disclosure procedure

CSAM scanning integrated into every applicable media upload path

Nudity, explicit-content and abuse moderation

Message unsend, report and deletion semantics

Historical email and mobile number tracking

Account-wide blocking and ban enforcement

Mode-specific privacy and profile visibility controls

Serious incident reporting and escalation

Staff access auditing, MFA and least privilege

Data deletion, archive expiry and backup lifecycle testing

Independent Australian privacy and online-safety legal review

&#x20;Copy checklist

## 18. External standards and legal reviews

Swoovr should assess the following before its public launch:

- [Australian Privacy Principles](https://www.oaic.gov.au/privacy/australian-privacy-principles?utm_source=chatgpt.com) — collection, sensitive information, access, disclosure and retention.
- [Online Safety Codes and Standards](https://www.esafety.gov.au/industry/codes?utm_source=chatgpt.com) — including obligations relevant to messaging and child sexual exploitation material.
- [eSafety Safety by Design](https://www.esafety.gov.au/industry/safety-by-design?utm_source=chatgpt.com) — platform-level safety practices.
- [Project Arachnid](https://www.protectchildren.ca/en/programs-and-initiatives/project-arachnid/?utm_source=chatgpt.com) — confirm industry integration and operational requirements.
- [Azure PostgreSQL encryption](https://learn.microsoft.com/en-us/azure/postgresql/security/security-data-encryption?utm_source=chatgpt.com) — storage and customer-managed encryption key options.

Swoovr should also assess the Australian voluntary Online Dating Industry Code, including incident reporting, enforcement transparency and user protections.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.esafety.gov.au\&sz=32)

eSafety Commissioner



## Final policy direction

I would make the following commitments central to Swoovr:

Protect people's identities: Verified identity data is segregated, encrypted, access-controlled and never publicly exposed.

Protect people from harm: Reports are taken seriously, with clear escalation procedures and platform-wide safety enforcement.

Preserve appropriate evidence: When a serious incident is identified, relevant available records can be secured independently of account deletion.

Help lawful investigations: Swoovr can retrieve and provide lawfully retained records through a controlled, documented process.

Respect privacy and deletion: Ordinary users are not subjected to indefinite surveillance or blanket retention of sensitive documents solely because a future crime is hypothetically possible.

Hold Swoovr employees accountable: Every sensitive operation is restricted, justified and auditable.

The one policy choice requiring particularly careful legal assessment is how long Swoovr can retain an identifiable record after a voluntarily deleted account, especially when there has been no misconduct or report. For an active ID-verified account, keeping the minimum verified identity fields is easier to justify as part of providing an ongoing verification and safety service. For deleted accounts, the justification must be independently assessed.

If your priority is ensuring that Swoovr can assist police following a sexual assault, the most effective combination is persistent minimum verified identity records during active membership, properly justified post-closure retention, accessible serious-incident reporting, and rapid evidence preservation. An offline archive can help protect retained evidence, but it cannot eliminate the need for a defensible retention policy.