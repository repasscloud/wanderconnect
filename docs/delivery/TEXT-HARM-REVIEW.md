# Text-harm hooks and human review

DEFAULT D266 implements115 under D185-approved policy. It is an optional, disabled-by-default local processing hook, not a measured classifier or a claim of Code compliance. No new external text provider receives messages.

## Exact committed sources

When `TextHarm:Enabled=true`, core about, per-mode about and ordinary Text-message writes save an encrypted inspection intent in the same transaction. Profile about changes carry a fresh server revision; messages use their immutable message ID. Clearing an about field invalidates an older pending revision without inspecting a replacement. The worker validates active ownership, exact revision and current text; unsent/moderated/deleted/replaced sources are skipped. Missing keys leave pending work intact. Do not disable processing with a backlog without an explicit operator plan: disabled detectors must not drain earlier work as clean results.

The replaceable detector interface currently uses narrow English threat/payment phrase combinations, repetition5 and distinct-recipient10 thresholds over the preceding hour. Repetition includes the current message once; bulk outreach counts distinct actual counterparts including the current peer once. A501-row bounded server-history query marks coverage incomplete and disables count rules. Profiles do not inherit message count rules. Normalisation is bounded FormKC/case/whitespace processing; no reusable public text-fingerprint index is created. Rules can miss harmful text or flag legitimate requests, jokes and quotations. No measured accuracy is asserted.

## Review and preservation

A candidate joins the subject's existing open review or creates an own-initiative moderation case, never a fabricated member report. Its exact text/context is independently encrypted. Ordinary candidates stay review-only; threat candidates enter urgent Admin review with a provisional legal hold and the existing escalation/acknowledgement attempt. Provider acceptance is not proof someone received or acted on an email. No automated sanction, reputation change, member accusation or notice follows a match.

Staff list metadata before opening audited restricted evidence. Current staff roles, queue/affiliation and retained subject/reporter/message-participant conflicts are checked for every list/read/decision. Support handles ordinary Support reviews; Admin also handles Admin/affiliated reviews. Members, Legal-only staff and conflicted staff cannot use this moderation pathway. Human review can record no action, including legitimate quoted text. An actioned outcome must reference a real later same-case subject sanction or matching content removal; a rule match cannot invent an action. Ordinary enforcement notices/review pathways remain attached to actual human actions.

Applicable holds capture pending intents and independent candidate evidence before source loss. Evidence key rotation/status includes both new envelopes. Unreviewed candidates prevent case closure and disposal. After review, the existing case-content and later minimal-record clocks apply, including appeals, disclosures and holds. Completed inspection receipts retain no original text and expire after30days when preservation and retained-copy checks allow. Holds/copy release and case closure are separate events. Missing provenance or unavailable hold status fails closed.

Draft privacy0.9 describes optional text processing. This remains a draft pending legal57. Actual test commands/results and native/provider gaps are recorded in VALIDATION.md; prepared source alone is not validation.
