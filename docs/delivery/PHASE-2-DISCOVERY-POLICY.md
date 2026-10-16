# Phase 2 — discovery eligibility policy slice

This partial Phase 2 slice proceeds independently of incomplete Phase 1 integration. It implements the behaviour rules in Domain; it does not enable a protected endpoint or public/member browsing. Read [behaviour](../product/02-BEHAVIOUR-AND-RULES.md) and [handoff acceptance](../product/06-TECHNICAL-AND-AI-HANDOFF.md).

CONFIRMED: exclusions evaluate all applicable intentions, including undisplayed ones; incognito refusal denies discovery/direct opening; photos are optional; block applies in both directions/across contexts; selecting a destination cannot establish physical presence; no pre-arrival traveller initiation.

DEFAULT D19: require active adult verified viewer and subject for this policy; both context/area preferences apply; evaluate both participants' intention rules. One reusable gate covers discovery list, profile opening, profile media, saved-profile rereads and new contact. Existing accepted conversations are outside this policy (ordinary intention changes need not close them). Contact additionally needs owner contact permission, request-limit allowance and explicit incognito identity disclosure confirmation. Traveller contact needs a fresh, consented, risk-approved destination presence assertion bound to the actor and selected discovery area. Future assertions do not count. No pre-arrival reply exception is introduced; Q05 remains OPEN.

Inputs are **internal trusted server snapshots**, never request DTOs. Application identity/roles come from a validated member session; active/verified status, block, substance, context intentions (including hidden selections), area matching, rate-limit status and presence evidence must come from fresh authoritative services. The policy does not manufacture those services. Verification and location test values are hypothetical; they are not live or simulated provider integration. There is no fake provider adapter, data endpoint, exact coordinate, photo requirement or paid override.

Unknown enums, missing facts, empty IDs, mismatched actor/presence, stale/future evidence, conflicting required/excluded sets and absent verification fail closed. Public callers must return generic unavailability; denial details cannot disclose another member's private intentions. The same evaluator must be invoked before any later profile/media projection; no list-only enforcement is sufficient.

OPEN: actual identity provider Q04, presence source/radius/freshness policy, demographic preference representation and compatible reciprocal intention-pair configuration remain integration work. This slice implements explicit intention filters, not a complete matching/ranking system. No API or UI consumes it yet. Phase 2 and Phase 1 remain incomplete.

Implementation plan: add named executable checks first; observe missing policy compilation failure; implement focused `IntentionRule` and `DiscoveryAccessPolicy` types within Domain; run checks and guard-removal mutations; independent review; document actual evidence; commit and open a dependent PR to main. Preserve all seven projects and disabled Actions.

## Review correction — Travel presence

DEFAULT fail-closed refinement: Contact in Travel context always requires the bound, fresh destination presence assertion, even if the separate RequiresDestinationPresence flag is false. That flag can additionally require presence for a traveller in another context; it cannot weaken the Travel gate. The server still must derive travel/presence facts from authoritative services. This adds a conservative Travel-context requirement; it does not manufacture arrival or change Q05's unanswered reply exception. Regression first reproduced an incorrectly allowed Travel contact, then passed alongside a fresh-presence positive case.
