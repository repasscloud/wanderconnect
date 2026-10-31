# Owner decision source record — 29 October 2026

CONFIRMED owner requirements, recorded for D210–D220. These are requirements, not implementation or live-provider evidence. Legal gates explicitly stated by the owner remain in force. Historical builder comments are labelled by their GitHub authors; the final owner governance decision on issue 106 governs D220.

## Issue 108: Q83 — Push notification providers (APNs and FCM)

https://github.com/repasscloud/wanderconnect/issues/108

### danijeljw-RPC — 2026-10-28T13:01:23Z

https://github.com/repasscloud/wanderconnect/issues/108#issuecomment-6469652014

**Q83 — APPROVED, with additional implementation requirements.**

Proceed with the existing `IPushSender` abstraction and simulated sender. We will also implement real push notifications using **Apple Push Notification service (APNs)** for iOS and **Firebase Cloud Messaging (FCM HTTP v1)** for Android.

### 1. Provider configuration

- **iOS:** APNs using token-based `.p8` authentication, Key ID, Apple Team ID and bundle ID (`com.swoovr.app`).
- **Android:** Firebase Cloud Messaging using HTTP v1 and service-account credentials.
- Store local development configuration in `.local/docker.env`, following existing project conventions.
- Store production credentials in secure secret management, never in source control.
- Support separate development, staging and production configurations.
- I will provision the required Apple and Firebase credentials separately.

### 2. Simulated sender

Retain the simulated sender for local development and automated testing.

- Continue using Mailpit with `push-<device>@simulated.swoovr.invalid`.
- Clearly mark every simulated notification as `SIMULATED`.
- Never claim simulated delivery was successful on a physical device.
- Select the appropriate provider through dependency injection and environment configuration.
- Never silently fall back to simulation in production.

### 3. Device registration

Keep the device registration endpoint, but follow **Swoovr's date-based API versioning convention**.

Swoovr does not use `/v1`, `/v2`, or any numerical API version prefixes.

Endpoints use stable paths with a date-based contract version supplied through the `Swoovr-Version` HTTP header, following the Stripe-style approach.

Example:

```http
POST /api/me/devices
Swoovr-Version: 2026-10-28
Authorization: Bearer <token>
Content-Type: application/json
```

The date represents the published API contract version, not the date of each request or application deployment.

API versions only change when a breaking contract change requires a new version. Existing supported versions must remain functional for older mobile clients.

Device registration must support:

- Multiple devices per member.
- Device platform (iOS/Android).
- Push token registration, replacement and rotation.
- Device-specific registration identifiers.
- Device removal on logout where appropriate.
- Automatic invalidation of expired or rejected push tokens.
- Authentication and ownership checks.
- Secure storage and restricted logging of device tokens.

### 4. Delivery behaviour

Push notifications must:

- Respect member notification preferences.
- Respect block relationships and privacy restrictions.
- Avoid exposing sensitive message content unless permitted by notification settings.
- Support messages, connection requests, account activity and safety alerts.
- Use appropriate notification priorities.
- Support deep linking into the MAUI application.
- Handle provider errors, retries and invalid device tokens.
- Record provider acceptance separately from actual delivery, display or read status.
- Ensure security-critical notifications are not dependent exclusively on push delivery.

### 5. Implementation requirements

The architecture must:

- Keep push delivery behind `IPushSender`.
- Support simulated, APNs and FCM providers without modifying notification business logic.
- Use configuration to select the provider.
- Maintain compatibility with existing supported API contract dates.
- Never introduce `/v1`, `/v2`, `/vX` or similar numerical API version prefixes into Swoovr endpoints.
- Apply the date-based API versioning convention consistently throughout the entire Swoovr API, not just notification endpoints.
- Treat missing, invalid or unsupported API version headers according to a documented, consistent policy.
- Keep internal provider-specific versions, such as FCM HTTP v1, separate from Swoovr's public API versioning.

### 6. Implementation decision

**APPROVED — proceed with AJ using the simulated sender.**

Do not block completion of the notification infrastructure while waiting for Apple and Firebase credentials.

Real APNs and FCM integrations must be implemented and tested on physical devices before production mobile launch.

Track outstanding provider configuration and integration tasks separately.

**Final decision:** Approve the abstraction, retain simulation for development, implement real providers for production, and enforce Stripe-style date-based API contract versioning via the `Swoovr-Version` HTTP header across the Swoovr API. No numerical URL version prefixes.

## Issue 109: Q84 — Photo attachments in chat: which modes, how many, and recipient choice

https://github.com/repasscloud/wanderconnect/issues/109

### danijeljw-RPC — 2026-10-28T13:08:33Z

https://github.com/repasscloud/wanderconnect/issues/109#issuecomment-6469804146

**Q84 Decision: Approved — proceed with the implemented defaults.**

- Photo attachments are permitted in all modes between connected members.
- Maximum 20 photo attachments per sender per rolling 24-hour period, across all conversations and modes.
- Recipients can disable incoming photo attachments using the "Photos in chat" setting, enabled by default.
- Senders receive a clear message when a recipient has disabled photo attachments.
- All attachments must pass quarantine and content screening before delivery.
- Explicit content remains prohibited under D178.
- Limits and preferences should remain configurable for future policy changes.

No further changes required. **Q84 resolved.**

## Issue 110: Q85 — Ending a conversation politely

https://github.com/repasscloud/wanderconnect/issues/110

### danijeljw-RPC — 2026-10-28T13:12:31Z

https://github.com/repasscloud/wanderconnect/issues/110#issuecomment-6469900489

### Q85 — Decision: Approved with additional closing-message behaviour

Approve the recommended implementation with the following requirements.

**1. Ending a conversation**

- "End conversation" disconnects the two members across all currently connected modes.
- Apply the existing D176 30-day reconnection restriction to those disconnected modes.
- Close the conversation as read-only for both participants.
- Preserve conversation history according to D143/D167 and applicable retention/legal-hold policies.
- Do not automatically block, report or penalise either member.
- Existing block and report functionality remains available, including from closed conversations.
- Display a confirmation before ending, clearly explaining that all connected modes will be disconnected.

**2. Optional farewell messages**

The member ending the conversation may select one of the following fixed messages:

1. "Thanks for chatting. I don't think we're the right match, but I wish you all the best."
2. "I've enjoyed our conversation, but I've decided not to continue. Take care."
3. "I'm moving in a different direction, but thank you for your time. Wishing you well."
4. "No farewell message."

No free-text farewell messages are permitted as part of the End Conversation workflow.

**3. Automatic system messages**

Regardless of whether a farewell is selected, Swoovr should insert a neutral system event in the conversation history.

For the member ending the conversation:

> You've ended this conversation. Your connections with this member have been closed, and neither of you can send further messages here.

For the other member:

> This conversation has ended. You can still view your previous messages, but replies are no longer available.

These must be system-generated events, visually distinguishable from member-authored messages.

Do not impersonate either member, imply wrongdoing, or identify an unprovided reason for ending the conversation.

If the member selects a farewell, display that farewell as a member-selected predefined message immediately before the system event.

**4. Notifications**

- Send a normal in-app notification to the other member indicating that the conversation has ended.
- Push notifications, where enabled, should use neutral wording: "A conversation has ended on Swoovr."
- Do not include the optional farewell or private conversation content in push notification previews.
- Respect existing notification preferences and privacy controls.
- Do not generate additional repeated notifications for the same closure.

**5. Reconnection and history**

- The D176 restriction applies to the modes disconnected by this action, subject to any stricter existing restrictions.
- Expiry of the restriction must not automatically restore connections or reopen the conversation.
- Any later reconnection requires the normal request/acceptance workflow.
- A new conversation may only be opened after the applicable restrictions expire and both members establish a valid connection.
- Preserve the original conversation as closed history rather than silently reopening it.

**6. Safety and audit**

- The member may still block or report the other person after ending the conversation.
- Preserve the closing farewell and system event as part of conversation history.
- Record the closure timestamp, initiating member ID, affected mode connections and closure reason (`MemberEnded`) in the appropriate internal audit records.
- Do not publicly expose which party initiated the closure through API responses or system event metadata intended for the other member.
- Ensure the operation is idempotent and consistently closes the conversation and applicable connections.

**Decision: Approved. Proceed with implementation using these requirements.**

### danijeljw-RPC — 2026-10-28T13:13:16Z

https://github.com/repasscloud/wanderconnect/issues/110#issuecomment-6469916827

Additional thoughts
There is one UX distinction worth keeping: a person ending a conversation is not necessarily rejecting the other member personally. They might be cleaning up old chats, changing their intentions, or simply no longer interested.

The automatic message should therefore remain neutral, without suggesting rejection, blame, or misconduct.

I'd also avoid the phrase "You've been disconnected by this member" in the recipient's interface. It is unnecessarily confrontational.

For the conversation screen, I would use this:

[swoovr-conversation-closed.html](https://github.com/user-attachments/files/34080619/swoovr-conversation-closed.html)

## Issue 111: Q86 — Where members contact support with their reference

https://github.com/repasscloud/wanderconnect/issues/111

### danijeljw-RPC — 2026-10-28T13:06:32Z

https://github.com/repasscloud/wanderconnect/issues/111#issuecomment-6469760899

### Q86 — Decision: Support channels and response handling

**Approved: Both web form and direct email.**

Swoovr will provide two official support channels:

1. **Primary:** https://swoovr.com/support — hosted on the Swoovr corporate website.
2. **Alternative:** `support@swoovr.com` — direct email support.

The support page will provide an extensible form that allows members to:

- Submit a new support request.
- Optionally enter an existing `SW-XXXXXX` support reference.
- Add comments or additional information relating to an existing case.
- Upload supporting attachments, subject to appropriate file validation, security and size restrictions.
- Submit requests without needing access to an active Swoovr account, including members whose accounts are suspended or restricted.

Where a member already has a support reference, the application should provide a direct link to the support page and allow the reference to be copied or passed into the form automatically.

**Case routing and handling**

Cases will be processed by authorised Swoovr support staff and administrators.

Cases are prioritised based on severity, urgency and the nature of the request. Existing `SW-XXXXXX` references will eventually allow submissions to be associated with their corresponding case and routed to the appropriate internal team or queue.

The internal ticketing platform, routing rules, escalation procedures, case management functionality and attachment storage implementation are outside the scope of Q86 and will be designed separately.

**Support hours and response times**

- Both support channels accept submissions 24 hours a day, seven days a week.
- This does not constitute a commitment to 24/7 staffed support.
- No fixed public support operating hours are established at launch.
- Swoovr will endeavour to respond as soon as reasonably practicable, according to case priority and available resources.
- No contractual response-time or resolution-time SLA is established at this stage.
- Serious safety incidents, threats, suspected child exploitation and other high-risk reports must receive priority handling under Swoovr's separate trust-and-safety and incident-response policies.

Swoovr may introduce published support hours, service levels and additional support channels as the service grows.

**Implementation for the current slice**

Update the account-standing page to display:

- **Contact Support:** https://swoovr.com/support
- **Email Support:** `support@swoovr.com`
- **Reference:** `SW-XXXXXX`, where applicable.

The privacy notice and relevant help pages should identify the official support channels.

There is no requirement to build the corporate support form, email processing, ticket routing or case management system as part of this slice. Those components will be delivered separately.

**Q86 is resolved. No further decisions are required for the current implementation.**

## Issue 112: Q87 — Will Swoovr sign the Australian Online Dating Industry Code?

https://github.com/repasscloud/wanderconnect/issues/112

### danijeljw-RPC — 2026-10-28T13:16:57Z

https://github.com/repasscloud/wanderconnect/issues/112#issuecomment-6470002412

**Decision: YES — Swoovr intends to become a signatory to the Australian Online Dating Industry Code.**

We will adopt the Code as a compliance and operational benchmark immediately, with the intention of formally signing before public launch, subject to legal review (#57) and confirmation that all mandatory obligations can be met.

Our preference is to complete compliance readiness before the external beta wherever practical.

**Implementation requirements:**
- Address identified compliance gaps (#113–#121).
- Implement and document the retention, review, ban and evidence preservation policies outlined in #118.
- Establish the required compliance reporting, transparency reporting and internal accountability processes.
- Complete the legal compliance review before signing.
- Ensure Swoovr can maintain ongoing compliance rather than simply meeting the requirements at launch.

**Reference:** #118 — Retention schedule and evidence preservation.

The intention is for Swoovr to operate to the Code's standards from the outset, even before formal signatory status is obtained.

**Status: Decision confirmed — YES, intending to sign. Formal signing remains subject to compliance readiness and legal approval.**

## Issue 118: Retention schedule: review information at least 12 months; periods after deletion (Code §5.5, §6.2(2))

https://github.com/repasscloud/wanderconnect/issues/118

### danijeljw-RPC — 2026-10-28T12:40:16Z

https://github.com/repasscloud/wanderconnect/issues/118#issuecomment-6469223558

### Swoovr — Data Retention, Evidence Preservation and Backup Policy

**Decision: APPROVED IN PRINCIPLE — subject to Australian legal review.**

Swoovr intends to become a signatory to the Australian *Code of Practice — Online Safety Code for dating services* (July 2024). All retention, moderation, deletion and backup processes must be designed to support compliance with the Code and applicable Australian laws.

1. Retain the existing 30-day account recovery window (D170). Consider a separate, restricted administrative retention period of up to 90 days after deletion requests, subject to legal approval. Account recovery expires after 30 days regardless of the administrative retention period.
2. Retain ordinary deleted messages for 30 days (D167), except where the information is required for an investigation, an applicable legal obligation or an authorised preservation hold.
3. Retain all information used in a qualifying safety review for at least 12 months after review completion and/or referral, in accordance with Code §6.2(2).
4. Investigation evidence must remain independently retained and accessible to authorised staff even when associated accounts, messages or media are deleted.
5. Retain serious incident evidence for a proposed 24 months after final closure, subject to legal review and longer where preservation obligations apply.
6. Retain banned-user tombstones for the existing two-year period (D171), subject to review of applicable statutory requirements, data minimisation and anti-abuse justification.
7. Retain records demonstrating Code compliance measures for at least two years under §8.2(2).
8. Legal holds override scheduled deletion. Upon release, retained information returns to the applicable retention schedule and is deleted when no longer lawfully required.
9. Implement daily backups retained for 30 days, weekly backups for four weeks, and monthly backups for 12 months.
10. Backups must be encrypted, access-controlled, periodically restore-tested, and isolated from production. Backup expiry must operate automatically.
11. Backup restoration must reconcile previously completed deletions, legal holds and expired records before production access resumes. Backups must not be used as the primary mechanism for regulatory evidence retention.
12. Maintain a documented retention schedule covering data classifications, retention triggers, statutory requirements, minimum and maximum periods, deletion procedures, legal holds, backup handling and audit logging.

**Implementation requirements:** Add automated tests proving that case evidence survives account deletion, survives ordinary message expiry, remains retained for at least 12 months after the applicable review closure/referral event, cannot expire during an active hold, and is destroyed according to policy following hold release.

**Legal gate:** All proposed periods and exceptions must be reviewed by an Australian lawyer under #57 before unrestricted external beta and before Swoovr represents itself as compliant with the Code.

Record the approved decision in `DECISIONS.md`, implement the schedule under #118, and update `ONLINE-DATING-CODE.md` to reflect Swoovr's intention to sign, while maintaining a clear distinction between intended compliance and verified compliance.

### danijeljw-RPC — 2026-10-28T12:49:13Z

https://github.com/repasscloud/wanderconnect/issues/118#issuecomment-6469402753

<html><head></head><body><h2><span>Additional Decision — Persistent Banned Identifier Registry and Reinstatement Process</span></h2><p><strong><span>Decision: APPROVED IN PRINCIPLE — subject to Australian legal review.</span></strong></p><p><span>Swoovr will maintain a persistent, restricted registry of banned email addresses and mobile phone numbers to help prevent permanently banned users from returning to the platform.</span></p><p><span>This extends the existing D171 tombstone mechanism and supports the objectives of §5.4(1)(d) and §5.5 of the Australian Online Dating Code.</span></p><h3><span>1. Banned Identifier Registry</span></h3><p><span>Create or extend a </span><code><span>BannedIdentifiers</span></code><span> table with the following fields:</span></p>
Field | Purpose
-- | --
Id | Unique registry record
IdentifierType | Email or Mobile
IdentifierHash | Deterministic HMAC-SHA256 value
KeyVersion | HMAC key version for rotation
CaseId | Associated trust and safety case
BanReasonCode | Standardised enforcement reason
Status | Active, Revoked or Expired
BannedAt | Original ban timestamp (UTC)
BanExpiresAt | Nullable UTC timestamp; NULL means no scheduled expiry
LastReviewedAt | Most recent review timestamp (UTC)
RevokedAt | When staff revoked the restriction (UTC)
RevokedBy | Authorised staff member responsible
LastMatchedAt | Most recent attempted reuse (UTC)
MatchCount | Number of matching attempts

<p><span>All timestamps must use UTC (</span><code><span>DateTimeOffset</span></code><span> recommended).</span></p><p><span>Identifiers must be normalised before hashing. Email addresses should use consistent, conservative normalisation; mobile numbers must use E.164 formatting.</span></p><p><span>Use HMAC-SHA256 with a securely managed server-side secret, never ordinary unsalted SHA-256. The secret must not be stored in the database. Support key rotation without losing the ability to identify existing banned identifiers.</span></p><p><span>Plaintext email addresses and mobile numbers must not be stored in this registry.</span></p><h3><span>2. Registry Matching Rules</span></h3><p><span>Whenever a user registers, changes an email address, adds a mobile number or changes an existing mobile number, the API must calculate the corresponding identifier hash and check the registry.</span></p><p><span>Only active, unexpired restrictions must produce a blocking match:</span></p><pre><code><span>var now = DateTimeOffset.UtcNow;

var isBanned = await db.BannedIdentifiers.AnyAsync(x =&gt;
    x.IdentifierType == identifierType &amp;&amp;
    x.IdentifierHash == identifierHash &amp;&amp;
    x.Status == BanStatus.Active &amp;&amp;
    (x.BanExpiresAt == null || x.BanExpiresAt &gt; now));</span></code></pre><p><span>The production query must also account for HMAC key versions.</span></p><p><strong><span>Status behaviour:</span></strong></p><ul><li><p><code><span>Active</span></code><span>: The identifier is blocked while </span><code><span>BanExpiresAt</span></code><span> is NULL or greater than the current UTC time.</span></p></li><li><p><code><span>Revoked</span></code><span>: The identifier has been manually released by authorised staff and must no longer produce a blocking match.</span></p></li><li><p><code><span>Expired</span></code><span>: The restriction has passed its expiry date and must no longer produce a blocking match.</span></p></li></ul><p><span>Expiry must be enforced by the matching query itself; it must not depend on a background process having updated the status.</span></p><p><span>A scheduled worker may transition expired records to </span><code><span>Expired</span></code><span> for consistency and reporting.</span></p><p><span>Revoked and expired records remain in the historical enforcement registry only for the period authorised by the retention schedule. They must not prevent legitimate registration.</span></p><h3><span>3. User-Facing Behaviour</span></h3><p><span>When an identifier matches an active restriction:</span></p><p><strong><span>Email:</span></strong></p><blockquote><p><span>This email address cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance.</span></p></blockquote><p><strong><span>Mobile:</span></strong></p><blockquote><p><span>This mobile number cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance.</span></p></blockquote><p><span>Do not reveal the original case, ban reason or identity of the previously banned account.</span></p><p><span>Apply appropriate rate limiting, enumeration protections and audit logging.</span></p><p><span>A match against an identifier added to an existing account must block that identifier change without automatically banning the otherwise legitimate account.</span></p><h3><span>4. Manual Unban and Review Process</span></h3><p><span>Authorised Swoovr Trust and Safety staff must be able to review and revoke a banned identifier independently of the original account ban.</span></p><p><span>Reasons may include:</span></p><ul><li><p><span>A recycled mobile number now belongs to a different person.</span></p></li><li><p><span>An email address has legitimately changed ownership.</span></p></li><li><p><span>A restriction was applied incorrectly.</span></p></li><li><p><span>An appeal has been upheld.</span></p></li><li><p><span>Swoovr has otherwise determined that the restriction is no longer justified.</span></p></li></ul><p><span>When manually revoking an identifier, staff must:</span></p><ol start="1"><li><p><span>Open the registry entry and review its associated </span><code><span>CaseId</span></code><span>.</span></p></li><li><p><span>Verify the circumstances sufficiently to justify releasing the identifier.</span></p></li><li><p><span>Change </span><code><span>Status</span></code><span> to </span><code><span>Revoked</span></code><span>.</span></p></li><li><p><span>Set </span><code><span>BanExpiresAt</span></code><span> to the current UTC timestamp.</span></p></li><li><p><span>Update </span><code><span>LastReviewedAt</span></code><span>, </span><code><span>RevokedAt</span></code><span> and </span><code><span>RevokedBy</span></code><span>.</span></p></li><li><p><span>Record a mandatory explanation.</span></p></li><li><p><span>Add an internal case note to the associated </span><code><span>CaseId</span></code><span>.</span></p></li><li><p><span>Create an immutable audit event containing the old and new values, staff identity, timestamp and reason.</span></p></li></ol><p><span>The registry update, case note and audit event must be committed atomically in one database transaction.</span></p><p><strong><span>Example case note:</span></strong></p><blockquote><p><span>Mobile number restriction revoked following support review. Applicant provided sufficient evidence of current ownership. Available information indicates that the number was reassigned after the original account was banned. The mobile number is now permitted for registration. The original account ban remains in effect.</span></p></blockquote><h3><span>5. Identifier Unban Is Not Account Unban</span></h3><p><span>These are separate enforcement actions.</span></p><p><span>Revoking a banned email address or mobile number must </span><strong><span>not</span></strong><span> automatically:</span></p><ul><li><p><span>Reinstate the original banned account.</span></p></li><li><p><span>Remove the underlying trust and safety case.</span></p></li><li><p><span>Revoke sanctions against the original account.</span></p></li><li><p><span>Release other banned identifiers associated with that account.</span></p></li><li><p><span>Delete historical enforcement or audit records.</span></p></li></ul><p><span>Similarly, reinstating an account must explicitly reconcile the applicable identifier restrictions to avoid accidentally leaving a reinstated user blocked.</span></p><p><span>Staff must have separate, clearly identified actions for </span><strong><span>Revoke Identifier Restriction</span></strong><span> and </span><strong><span>Reinstate Account</span></strong><span>.</span></p><h3><span>6. Retention and Privacy</span></h3><p><span>Swoovr's intention is to support indefinite-duration identifier restrictions for serious or permanent bans, while preserving a mechanism for review and reinstatement.</span></p><p><span>An indefinite restriction means that it has no automatic expiry. It does not mean that the record is exempt from privacy law or can never be deleted.</span></p><p><span>The registry must therefore:</span></p><ul><li><p><span>Retain active restrictions while their continued use remains justified and legally permissible.</span></p></li><li><p><span>Support periodic necessity reviews, recording </span><code><span>LastReviewedAt</span></code><span>.</span></p></li><li><p><span>Allow records to be revoked or expired without deleting their enforcement history immediately.</span></p></li><li><p><span>Apply a defined retention period to revoked and expired records.</span></p></li><li><p><span>Preserve only the necessary enforcement summary and case reference, rather than retaining all original investigation evidence indefinitely.</span></p></li><li><p><span>Follow legal-hold requirements where applicable.</span></p></li><li><p><span>Treat hashed identifiers as potentially personal information, since they remain linkable to individuals through Swoovr's systems.</span></p></li></ul><p><span>The existing two-year tombstone policy (D171) remains the baseline for ordinary tombstones. The proposed longer retention of active serious-ban identifiers requires separate justification and legal approval under #57.</span></p><h3><span>7. Required Automated Tests</span></h3><p><span>Implement tests demonstrating that:</span></p><ul><li><p><span>A banned email prevents registration.</span></p></li><li><p><span>A banned mobile number prevents registration or attachment to an account.</span></p></li><li><p><span>Normalisation produces consistent matches.</span></p></li><li><p><span>Different identifier types cannot collide through matching logic.</span></p></li><li><p><span>Active restrictions with no expiry remain enforced.</span></p></li><li><p><span>Active restrictions with a future expiry remain enforced.</span></p></li><li><p><span>Expired restrictions do not block registration.</span></p></li><li><p><span>Revoked restrictions do not block registration.</span></p></li><li><p><span>Manual revocation updates the relevant timestamps and staff attribution.</span></p></li><li><p><span>Revocation creates an immutable audit event and associated case note.</span></p></li><li><p><span>Identifier revocation does not reinstate the original banned account.</span></p></li><li><p><span>Account deletion does not remove an active, lawfully retained ban identifier.</span></p></li><li><p><span>HMAC key rotation preserves matching behaviour.</span></p></li><li><p><span>Concurrent registration attempts and revocation operations behave consistently.</span></p></li><li><p><span>Unauthorised staff cannot modify the registry.</span></p></li><li><p><span>Registry expiry and deletion comply with the approved retention schedule.</span></p></li></ul><h3><span>8. Implementation and Documentation</span></h3><p><span>This must be implemented as an extension of the existing enforcement, tombstone and trust and safety infrastructure, not as a competing or disconnected system.</span></p><p><span>Update the following documentation as appropriate:</span></p><ul><li><p><code><span>DECISIONS.md</span></code><span> — record the approved behaviour.</span></p></li><li><p><code><span>ONLINE-DATING-CODE.md</span></code><span> — describe how it supports the Code's banned-user prevention requirements.</span></p></li><li><p><span>Internal trust and safety policies — document revocation, appeals and review procedures.</span></p></li><li><p><span>Data retention schedule — specify active, revoked and expired identifier retention.</span></p></li><li><p><span>Privacy notice — disclose the retention and use of identifier fingerprints for safety and anti-abuse purposes.</span></p></li><li><p><code><span>VALIDATION.md</span></code><span> — record automated test coverage.</span></p></li></ul><p><strong><span>Legal review:</span></strong><span> Australian counsel must approve the indefinite-ban retention justification, privacy notice and record-expiry policy under #57 before unrestricted external beta.</span></p><p><strong><span>Final principle:</span></strong><span> Swoovr must be able to prevent known banned identifiers from being reused, while retaining the ability to correct mistakes and permit legitimate new owners of recycled or reassigned identifiers to use the service. Every restriction and subsequent revocation must be traceable to an auditable decision.</span></p></body></html>

## Issue 122: Q88 — Meeting safety: trusted contacts and check-ins (#40)

https://github.com/repasscloud/wanderconnect/issues/122

### danijeljw-RPC — 2026-10-28T13:32:46Z

https://github.com/repasscloud/wanderconnect/issues/122#issuecomment-6470371378

# Q88 — Decision: meeting safety, trusted contacts and check-ins

**Decision: APPROVED with the implementation requirements below.** This applies to #122 and the meeting-safety scope of #40. Offer the feature voluntarily to **all Swoovr members, including Free-tier members**. It is a safety aid, **not a guarantee of physical safety**.

## 1. Trusted contacts and consent

- A member may save **up to three trusted contacts**, whether or not those contacts have Swoovr accounts. Accept an email address or E.164-format mobile number.
- Each contact must **opt in through an expiring, single-use confirmation link or code** before they can receive meeting-safety notifications. A contact can decline, withdraw consent, or unsubscribe at any time. Changing contact details requires fresh confirmation.
- Members choose **one, two, or all three confirmed contacts for each meeting**. Never notify contacts who have not opted in.
- Apply invitation/send rate limits and anti-harassment controls. Contact records must never be used for marketing.

## 2. Meeting plans and shared information

- A member can create a meeting safety session **without having planned the meeting through Swoovr**.
- Required: meeting start date/time, time zone, check-in or expected-end time, and selected confirmed contacts. Optional: venue name, general area, brief member-authored note, additional check-in times.
- Shared contact-facing information: **member's preferred first name, meeting date and time (including time zone), member-provided venue or general area, next check-in deadline, and safety-session status**. Clearly label details as member-provided and not independently verified.
- **Never share the other participant's identity, Swoovr profile, photographs, messages, private address, or live GPS location by default.** Do not show another member's details in links, metadata or previews either.
- Meeting safety must remain separate from mutual QR meeting confirmation. QR confirmation can be added as an optional enhancement; it is not required to start safety monitoring.

## 3. Member check-ins and escalation

- Members can schedule one or more optional check-ins and can mark **I'm OK**, **Extend by 30 minutes**, **I'm home safe**, **End meeting**, **Cancel meeting**, or **Need help**.
- For each scheduled check-in, send an app/push prompt and use a **30-minute grace period**. Send a reminder before expiry when feasible.
- If the member does not acknowledge by the deadline, notify the **selected trusted contacts only**, identifying the check-in as **missed/unconfirmed**. This is **not proof of an emergency**. Do not automatically notify police or the other meeting participant.
- **Need help** immediately notifies the selected trusted contacts and provides appropriate country-specific emergency assistance and safety resources. It does **not** automatically call emergency services.
- **I'm home safe** ends monitoring and updates contacts accordingly. **End meeting** ends monitoring without asserting safety. **Cancel meeting** cancels a planned/active session. Trusted contacts cannot submit these member-only actions.
- All scheduling and missed-check-in escalation must run **server-side**, including if the MAUI app is closed, the device is offline, or push delivery fails. Use idempotent workers/outbox events to avoid duplicate escalations.

## 4. Dedicated trusted-contact web page on swoovr.app

- Host a responsive, accessible, **read-only** page at a route such as `https://swoovr.app/safety/meeting/{token}`. This page is available to confirmed external trusted contacts without requiring a Swoovr account.
- `token` is a **cryptographically secure, uniformly random opaque bearer token** (at least 256 bits), generated by the server. It must **not** be a predictable meeting ID or a hash of a meeting ID/email/phone. Store **only a keyed hash or SHA-256 digest** of the token in the database, never the raw token.
- Issue an **individual token per meeting/contact**, so revoking one contact never affects another. Require the contact's confirmed consent before issuing or exposing the detail page; for stronger privacy, additionally verify the contact with a one-time email/SMS code before showing sensitive details (recommended for launch if feasible).
- The shared page shows the member-approved meeting summary, current status, date/time and check-in deadline, the last status update, and a safety disclaimer. It may show **Contact Swoovr support** and emergency guidance, but **must not contain an unauthenticated 'I'm home safe' action**. Only the authenticated member can confirm safety.
- Support `planned`, `active`, `check_in_due`, `missed_check_in`, `help_requested`, `home_safe`, `ended`, `cancelled`, `expired` and `revoked` states (or equivalent). Show a neutral **Link unavailable or expired** screen after expiry/revocation, without leaking member or meeting details.
- Set `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex, nofollow, noarchive`, restrictive CSP, and avoid third-party analytics, trackers, external fonts, or CDN scripts on token pages. Redact tokens from application/proxy/Cloudflare logs, telemetry, exception reports and support tooling. Rate-limit requests and never include tokens in social metadata. Prefer redacted page paths in analytics. Never put meeting details in notification URL query parameters.

## 5. Expiry and retention — two separate concepts

**Approve 30 days as the *absolute maximum lifetime for a shared-page token*, but not as its default usable duration after the meeting has finished.**

- A token has `CreatedAtUtc`, `ExpiresAtUtc`, and optional `RevokedAtUtc`. Set `ExpiresAtUtc = min(meeting's final expected/check-in end + 24 hours, token creation + 30 days)`; if a session is extended, a server-side recalculation can extend it **up to the original 30-day cap**. Revoke early when the contact is removed, consent is withdrawn, or a safety session is deleted according to policy.
- For a completed meeting (`home_safe`, `ended`, `cancelled`), keep the contact page available for **up to 24 hours after completion** to show its final status, unless revoked sooner. Never show sensitive details after link expiry.
- Retain **ordinary meeting-safety database records for 30 days after final session closure**, then delete or irreversibly de-identify them, subject to documented legal holds, abuse/incident reports and other applicable retention obligations. This is a proposed product policy, not a claimed statutory period. Retain only necessary event/audit metadata; define separate privacy notices for contact data and messaging delivery records.
- Separate these fields/concepts explicitly: **token expiry**, **meeting session closure**, **ordinary record deletion**, and **legal-hold/incident preservation**. Do not use one `ExpiresAt` value for all four.
- Times must be stored in UTC and displayed in the member's selected meeting time zone, with a visible zone abbreviation/offset. Do not rely on a visitor's local time to interpret the meeting time.

## 6. Suggested minimum data model

- `TrustedContact`: `Id`, `MemberId`, `DisplayName`, `Channel`, `DestinationEncrypted`, `DestinationLookupHash`, `ConsentStatus`, `ConfirmedAtUtc`, `WithdrawnAtUtc`, `CreatedAtUtc`.
- `MeetingSafetySession`: `Id`, `MemberId`, `StartsAtUtc`, `MeetingTimeZoneId`, `VenueOrArea`, `MemberNote`, `Status`, `ClosedAtUtc`, `PurgeAfterUtc`, `CreatedAtUtc`, `UpdatedAtUtc`.
- `MeetingSafetyRecipient`: `Id`, `SessionId`, `TrustedContactId`, `TokenHash`, `TokenCreatedAtUtc`, `TokenExpiresAtUtc`, `TokenRevokedAtUtc`, `NotificationStatus`.
- `MeetingSafetyCheckIn`: `Id`, `SessionId`, `DueAtUtc`, `GraceEndsAtUtc`, `RespondedAtUtc`, `Response`, `EscalatedAtUtc`.
- `MeetingSafetyEvent` / outbox: chronological status changes, notification attempts, delivery outcomes, idempotency key. Keep event details minimal and apply retention/legal-hold controls.

## 7. Delivery and safeguards

- Use Swoovr's provider-independent notification abstractions: email initially (Mailpit in local development) and SMS when a delivery provider is configured. Never claim a message was delivered without provider confirmation. If delivery fails, expose a meaningful status to the member.
- Verify invited contacts, avoid contact enumeration, rate-limit invitations and resend attempts, and secure all public-page endpoints. Account removal, contact withdrawal, and member-initiated safety revocation should invalidate links promptly.
- Meet accessibility and mobile responsiveness requirements. Never describe alerts as guaranteed to arrive. Show the member a clear reminder that a trusted contact should know what to do if they receive an alert.
- Add integration tests for token generation, uniqueness and hashing, per-recipient revocation, expired tokens, missing/late check-ins, duplicate worker retries, time-zone changes and DST, notification failure, cancellation, consent withdrawal, and legal-hold retention exceptions.

**Implement #40/#122 on this basis.** The companion HTML demo illustrates the member check-in, contact notifications, meeting details page, missed check-in alert, and final/expired states. It is a UI reference, not a production security implementation.

[meeting-safety-examples.html](https://github.com/user-attachments/files/34081458/meeting-safety-examples.html)

Note: The designs in the attached HTML file are not using swoovr's design style, it's just for reference

## Issue 123: Q89 — Meeting safety: emergency button, location and after-meeting reports (#40)

https://github.com/repasscloud/wanderconnect/issues/123

### danijeljw-RPC — 2026-10-28T13:36:37Z

https://github.com/repasscloud/wanderconnect/issues/123#issuecomment-6470468075

Decision: Approve the proposed approach, with four clarifications.
- Emergency button: Display a large, clearly labelled button that opens the device's emergency dialler using the verified emergency number for the selected country. Never automatically place a call.
- Alert trusted contact: Keep this separate from calling emergency services. Open the device's SMS composer containing the meeting information. A location link is optional, explicitly chosen by the member, and generated on the device. Swoovr receives no exact coordinates.
- After-meeting report: Show an optional "How did it go?" prompt after the scheduled meeting end, with private feedback, report an incident, and dismiss options. Feedback should not automatically affect another member's reputation.
- Standalone meeting plans: Implement the simple "I'm meeting someone" form now, independently of the future Plan a Date feature #59.
Additional safeguards worth including:
1. Do not describe an SMS as successfully delivered merely because the composer opened.
2. Make it clear that Swoovr does not monitor emergencies or dispatch responders.
3. Allow a meeting to be cancelled or ended early, avoiding unnecessary reminders.
4. If an incident report is submitted, link it to the meeting and follow the existing serious safety incident procedure without notifying the accused member automatically.
This fits the existing privacy architecture and avoids collecting unnecessary location information.

## Issue 119: Organisational Code items: Trust and Safety Leader, team, police MOUs, forums, Safety by Design self-assessment, compliance register

https://github.com/repasscloud/wanderconnect/issues/119

### danijeljw-RPC — 2026-10-28T13:43:05Z

https://github.com/repasscloud/wanderconnect/issues/119#issuecomment-6470622408

**Owner decision received.** The owner's governance decision for this issue (Trust & Safety Leader, operations, law-enforcement contact and ANZPAA investigation, industry engagement, Safety by Design self-assessment and compliance register, two-year records, annual review) was posted on #106: https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6470617112. It will be recorded as a decision in DECISIONS.md in the next run, and this issue will close with that PR.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

## Issue 106: Q82: Safety & Support pack reviews: who verifies, how often, AU ready from D190? (D204)

https://github.com/repasscloud/wanderconnect/issues/106

### danijeljw-RPC — 2026-10-28T00:08:58Z

https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6456932895

Approve with defaults.

### danijeljw-RPC — 2026-10-28T07:36:33Z

https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6463411622

Answered by the owner on 28 October 2026: recommended default accepted. Recorded as **D206** (CONFIRMED) in docs/delivery/DECISIONS.md and ANSWERED in QUESTIONS.md (commit 1008271, PR #104). Packs are reviewed every 90 days; AU is ready from 27 October 2026; Trust & Safety re-checks every AU contact before the external beta.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

### danijeljw-RPC — 2026-10-28T13:37:34Z

https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6470491905

Decision: Already approved. Keep D206 as confirmed.

Your existing approval establishes:
- Trust & Safety owns the resource packs.
- Review occurs every 90 days.
- Only administrators may change the packs, with revision history.
- Australia is recorded as ready from 27 October 2026.
- New Zealand remains unverified until its contacts are checked.
- Great Britain remains demo-only.
The Australian pack must still undergo an official-source verification before the external beta. Owner approval of the list is not equivalent to verifying each number and URL.

I recommend retaining the planned next review of 25 January 2027, while requiring an immediate review whenever an emergency service or authority changes its contact information.

No further decision is necessary.

### danijeljw-RPC — 2026-10-28T13:38:23Z

https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6470511302

## Owner decision — Approved for implementation

Swoovr will establish a proportionate Trust & Safety governance framework in preparation for external beta, public launch, and potential participation in the Australian Voluntary Online Dating Code.

**1. Trust & Safety leadership**

- Designate a named senior Trust & Safety Leader with appropriate authority.
- The founder may initially hold this responsibility.
- Document responsibilities, escalation authority, decision-making and reporting.
- Review staffing requirements as membership and incident volumes increase.

**2. Trust & Safety operations**

- Establish an appropriately resourced Trust & Safety function.
- Internal employees and qualified external specialists may perform operational responsibilities.
- Maintain documented training, escalation procedures, case handling and audit records.
- Provide a mechanism for urgent incidents to be escalated without relying solely on routine support hours.
- Do not advertise 24/7 human monitoring unless it is actually provided.

**3. Law-enforcement cooperation**

- Establish a designated law-enforcement contact and secure request-handling process.
- Investigate participation in the ANZPAA information-sharing protocol and appropriate police agreements.
- Document all requests, disclosures, legal authorities, approvals and outcomes.
- No personal information is disclosed solely because a person claims to represent law enforcement.
- Follow the established emergency disclosure, preservation and serious incident procedures.
- Record outreach and implementation progress without claiming agreements exist before they are executed.

**4. Industry engagement**

- Identify appropriate Australian online safety and dating industry organisations.
- Participate in relevant forums where reasonably practicable.
- Maintain a record of engagement, correspondence, findings and actions.

**5. Safety by Design and Code assessment**

- Complete and document a Safety by Design self-assessment before public launch.
- Map Code requirements against implemented features, policies and operational controls.
- Maintain an evidence-backed compliance register identifying fulfilled, partially fulfilled and outstanding obligations.
- Review gaps before applying for Code participation or publishing any verified compliance rating.
- Do not claim Code signatory status or external verification before it has been formally obtained.

**6. Records and accountability**

- Maintain compliance evidence for at least two years, or longer where another applicable retention rule requires it.
- Record policy changes, staff training, incident-handling processes, audits, governance decisions and reviews.
- Ensure sensitive personal information is retained only as necessary and subject to access controls and relevant retention policies.
- Conduct a formal governance and compliance review at least annually, and following material regulatory or operational changes.

**Implementation:** These are operational launch-readiness requirements, not blockers for unrelated software development.

**Acceptance criteria:** Named accountable owner, documented operational procedures, law-enforcement contact and cooperation plan, completed self-assessment, compliance register and identified outstanding actions.

Approved to proceed on this basis.

### danijeljw-RPC — 2026-10-28T13:42:50Z

https://github.com/repasscloud/wanderconnect/issues/106#issuecomment-6470617112

**Q82 answered and recorded on main.** Decision: **D206** in [docs/delivery/DECISIONS.md](https://github.com/repasscloud/wanderconnect/blob/main/docs/delivery/DECISIONS.md), marked ANSWERED in [QUESTIONS.md](https://github.com/repasscloud/wanderconnect/blob/main/docs/delivery/QUESTIONS.md). Recorded in commit [`1008271`](https://github.com/repasscloud/wanderconnect/commit/10082712cd8b48997f34feeb0e925e2151aa0181). Closing.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
