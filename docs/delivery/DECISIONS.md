# How questions and decisions work

Keep owner attention on decisions that change the product. Before asking, read the brief and decision register, inspect existing evidence, and check whether a confirmed requirement already answers it.

1. Record each uncertainty in QUESTIONS.md with ID, status, earliest blocking phase, consequence and proposed default.
2. Ask one concrete question at a time, highest dependency first. Explain the affected user moment, give a recommendation and at most two alternatives when useful. Bundle closely related facts such as provider URL/version in one request.
3. Ask only when the answer materially changes scope, policy, cost, privacy or integration feasibility. Choose reversible implementation details autonomously and mark DEFAULT. Do independent work while awaiting required answers.
4. Put the answer, date, source (owner or verified provider reference), affected documents and implementation consequence in the decision register. Update the product docs in the same PR. Do not leave conflicting copies.
5. Before each phase, present a concrete reviewable slice, evidence, remaining questions and the next phase's entry conditions. Unanswered questions stay OPEN; silence is not approval.

Example next question: “Which repository or documentation defines IdentityCloak, and which version/environment should this app integrate with?” This is about the chosen provider's contract, not reconsidering the confirmed backend choice.

## Decision register — 16 October 2026

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D01 | CONFIRMED, owner | Six personal schemes, both modes | Theme spec replaces original muted palettes |
| D02 | CONFIRMED, owner | IdentityCloak backend | Verify contract before adapter implementation |
| D03 | CONFIRMED, owner | Partner/admin/support dedicated email/password or magic link; optional MFA | Backend-enforced login matrix; separate support role |
| D04 | CONFIRMED, owner | Other users Apple/Google/email; extra options uncertain | Member links DEFAULT proposal; no extra social providers |
| D05 | CONFIRMED direction, owner | MAUI, Blazor, API, Contracts, Client, Domain, Infrastructure | Seven projects plus tests |
| D06 | CONFIRMED, owner | Local testing, Actions commented out, PR to main | No hosted workflow execution |
| D07 | DEFAULT, implementation | DatingApp namespace; .NET 10; SSR shell; separate local solution | Brand rename stays cheap; mobile tooling isolated |
| D08 | DEFAULT, implementation | Separate staff/member accounts; invitation provisioning | No accidental social-to-staff elevation |
| D09 | DEFAULT, implementation | Electric violet + System; member email links; 10-minute link expiry | Provider and phase review can amend defaults |
| D10 | DEFAULT, implementation, 16 October 2026 | Coarse account-area policy uses one application-owned role per evaluated session; no implicit role hierarchy; venue access requires an explicit nonempty venue ID and current membership | Phase 1 Domain slice only; separate staff/member default D08 retained. Additional roles/permissions require explicit design, not email matching or admin inheritance. See [slice](PHASE-1-ACCESS-POLICY.md) and [authentication](../architecture/AUTHENTICATION.md). |

A default does not imply the corresponding feature has been implemented. New decisions append rows and supersede old ones explicitly.

D10 is an implementation default, not an owner answer to Q01–03/Q09. D02–04 remain authoritative. Member email links remain a proposal under D09 and are denied by the current policy until the decision and provider contract are resolved.
