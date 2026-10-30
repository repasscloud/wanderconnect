# Individual profile text and message removal

DEFAULT D265 implements114 under the existing D133/D134/D185/D235 authority and preservation boundaries. It is a specific content action, independent of warnings, sanctions, account standing restrictions, identity matching and account deletion.

## Scope and authority

An authorised staff reviewer selects a current case source: core about, one known mode about, or one text message present in that report's captured conversation/message evidence. The preview and removal recheck live staff role, case queue, employee affiliation and retained subject/reporter conflicts. DEFAULT Support can handle ordinary non-affiliated Support cases; Admin can handle Admin/affiliated cases. Member, Legal-only and conflicted staff cannot use the action. No BFF route grants staff access.

The staff page shows encoded source text, asks for a recorded reason and explicit confirmation, and submits IDs, exact-source SHA256 and a command UUID. Raw source/reason never belongs in URLs, hidden source attributes, generic audit logs or member notices. The API compares the exact current source under owner/preservation/case authority. A later edit is a conflict, never an instruction to remove a different version. Same-command replay returns the recorded result without a second decision/notice/realtime event. Closed or content-expired reviews do not permit new removals.

## Result and evidence

Core/per-mode removal clears only that field; a mode removal also disables core-about fallback to avoid immediately showing replacement text through inheritance. Other profile fields, mode activation and account sanctions remain independent. A reported text message is marked removed-by-moderation and shown to both members as “Removed by Swoovr.” It is not labelled as a sender unsend. Current REST/export projections omit the removed text; the existing typed message upsert carries the neutral marker to connected clients. Clients remember observed removal markers so delayed responses cannot restore earlier text. Reconnect/current-page loading reconciles missed realtime updates; realtime transport is not guaranteed delivery.

Before changing the source, capture applicable preservation obligations. Store the exact source/reason in an independent versioned EvidenceSeal envelope with an immutable integrity hash; the case holds an IDs-only pointer. Restricted same-case evidence reads reauthorise and verify integrity. Later holds can capture this independent source after ordinary message/profile loss. Existing evidence key rotation includes these envelopes. A missing key or unreliable preservation authority must leave the source intact.

The member receives neutral Account standing decisions and a centre notice, without reporter, staff reason, case identity or hidden mode disclosure. This DEFAULT is centre-only; no email/push acceptance or delivery is implied. Removal does not automatically sanction the account, notify an accused member of an incident report, or change anyone's reputation. Existing approved support channels remain available for review/help.

## Independent clocks

The latest removal is a case review event. Existing safety retention computes case-content and minimal-record clocks, including appeals, referrals, disclosures, active/overlapping/unknown holds and independent copies. Atomic content disposal clears both the envelope and its now-unneeded fingerprint; minimal decision metadata remains until the separate case-record deadline. SQL guards reject decision/hash rewriting, truncation, premature erasure, evidence resurrection and premature record deletion. Ciphertext-only key rotation preserves the original hash and requires a different key version. Database owners remain privileged; hosted role separation is still deployment work.

Implementation evidence, actual failures and browser/native limits are recorded in VALIDATION.md. This document is not proof of live moderation staffing, training, hosted configuration or verified Code compliance.
