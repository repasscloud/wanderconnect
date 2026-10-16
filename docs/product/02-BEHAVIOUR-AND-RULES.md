# Behaviour and eligibility rules

## 1. Intentions and timing

DEFAULT intention vocabulary: long-term relationship, dating/romance, casual connection, friends with benefits, friendship, conversation, activity companion. Use respectful language in product copy. Owner examples such as “quick fuck” describe the distinction required, not final store-facing wording. Timing is separate: now, later today, this week, flexible. Relationship structure preferences may be optional and separately disclosed; do not assume monogamy or conflate sexual orientation with relationship structure.

Store general openness, currently sought intentions and context-specific visibility. Exclusions apply to the candidate's applicable declared intentions, not only a conveniently selected invitation label. Hiding a casual label from display must not nullify another member's exclusion. Explain that eligibility checks may evaluate undisplayed selections without revealing them.

Required intentions support explicit ANY/ALL semantics, default ANY. Excluded intentions always use ANY: one overlap fails. Define compatible reciprocal intent pairs in configuration rather than assuming every distinct label is mutually compatible.

```text
intentEligible(candidate, rule):
  required empty OR (ANY overlap / ALL subset as selected)
  AND no overlap(candidate.applicableIntentions, rule.excluded)
```

Example: LTR required; casual and FWB excluded. LTR+FWB fails. LTR+friendship passes unless friendship is excluded. Contradictory filter inputs must be explained before saving. Selecting LTR alone does not automatically create exclusions.

## 2. Discovery and interaction

Separate view rules from contact rules. Apply reciprocal demographic/preferences where explicitly configured. Owner demographic preferences are not a justification for arbitrary abusive filter categories.

Evaluate authentication, adulthood/verification status, suspension, block relationship, owner view policy, current context, intentions, incognito acceptance, approximate area and sparse-profile filters before returning data. Contact additionally evaluates owner interaction policy, request limits and travel presence. Never return forbidden profiles and rely on hiding them in the client. Media, search, favourites, direct URLs, chat events and push payloads need equivalent enforcement.

DEFAULT contact policy: one message request from eligible people, with accept/decline and an optional mutual-interest-only setting. No photo/link attachments in an unaccepted request. Repeated requests after decline are disabled. This policy is proposed, not owner-approved.

Connections track interest sent/received, mutual interest and optional saved profiles; no public like counts. Saving is private and does not override subsequent restrictions. Hiding is personal feed suppression; blocking prevents discovery and contact in both directions, including other contexts.

Changing filters controls future viewing/contact. DEFAULT: preserve an existing accepted conversation after ordinary intention changes, but explain changed compatibility and offer close/block. Explicit blocking, suspension and privacy revocation override access immediately. Retain only necessary historical conversation evidence; distinguish it from live profile/media access.

## 3. Incognito

CONFIRMED: no member-visible visit trace; recipient may refuse incognito visits. Internal abuse/audit records remain available under access controls and retention rules. Do not promise invisibility to platform staff or technical impossibility of stalking.

If incognito is refused, omit the profile from discovery and deny direct opening; do not reveal that a particular excluded person is present. Turning incognito off is the user's explicit choice. Display an incognito indicator. Starting an interaction discloses the initiating profile with a clear confirmation; it must not silently change the recipient's acceptance policy. Enforce limits on scraping, repeated location queries and enumerable IDs. Never expose exact coordinates or fine-grained activity timestamps.

## 4. Sparse profiles

No mandatory photo. DEFAULT substantive profile: meaningful introduction plus two descriptive elements such as answered prompts, interests, or connection preferences. Name, age and ticked intentions alone do not necessarily provide substance. Avoid rewarding repeated boilerplate or punctuation. Length thresholds must be configurable and language-aware; do not use a simplistic word count as the sole judge.

Provide an explainable completeness checklist and preview to the owner. Let them save and maintain a sparse profile. Other users can enable Hide sparse profiles. Sparse is not fraud and must not automatically trigger enforcement. Do not publicly shame the member. A text-rich photo-free card qualifies; a portrait-only profile can remain sparse.

## 5. Travel

Maintain separate concepts: current approximate presence, home area, planned destination and visit dates. Planning a destination never establishes physical presence. Support timezone-aware dates and local display; store instants in UTC where applicable.

Visitors can be shown with clear “Visiting soon” or “Visiting now” labels. Locals choose current visitors, upcoming visitors, both or neither. Do not expose precise accommodation or an itinerary trail. Future visit publication is opt-in.

CONFIRMED: no traveller-initiated contact before entering the destination's eligible discovery area. DEFAULT: allow replies if a local explicitly initiated a permitted request; this is OPEN for owner confirmation. Existing accepted conversations continue after departure by default. Owner filters still apply.

Use fresh, consented device location, accuracy information and risk checks. Handle permission denial, stale location and suspected spoofing with an honest “location not confirmed” state; do not fabricate arrival. Exact radius, presence freshness and grace periods are configuration decisions. Browser IP alone is not reliable physical presence proof.

## 6. Plans and meetings

A plan includes context, intention, participants, activity, public venue or approximate location, time/timezone, cost responsibility and visibility. Invitations may be private between connections or discoverable under eligibility rules. No automatic acceptance from an expression of interest.

Lifecycle: proposed → accepted → completed; proposed/accepted may become cancelled; rescheduling creates a proposal/version requiring agreement. Both must confirm material changes. Attendance and voucher redemption are distinct from chemistry or satisfaction.

## 7. Private experience feedback

Feedback allowed after a genuine connection or agreed meeting; label whether chat-only or met in person. Do not claim both attended merely because one reports it. DEFAULT one independently submitted feedback item per participant per meeting, amendable with audit history. Unmatching must not eliminate reporting access to prior interactions.

Questions: did you meet; did the profile represent them; were intentions aligned; were boundaries respected; would you reconnect and in what capacity; optional private chemistry question; separate report concern action.

Never public star scores or hot-or-not voting. Never disclose the counterpart's answers. Personal attraction informs the rater's preferences, not global human worth. Lack of attraction and rejection do not establish misconduct. Repeated independent intention-misrepresentation or boundary reports trigger a review queue. Use evidence, recency, confidence and appeal, not automatic sanctions from raw negative votes. No emotional sentiment model as proof of wrongdoing. Treat coordinated retaliation and reporting abuse as risks.

Prototype recommendation logic: deterministic preference/eligibility filters and explainable ordering. Later ranking experiments require sufficient data, controlled evaluation and checks that protected or marginalised groups are not systematically buried. Keep safety adjudication separate from recommendation ranking.
