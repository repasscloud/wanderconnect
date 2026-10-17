# How questions and decisions work

Keep owner attention on decisions that change the product. Before asking, read the brief and decision register, inspect existing evidence, and check whether a confirmed requirement already answers it.

1. Record each uncertainty in QUESTIONS.md with ID, status, earliest blocking phase, consequence and proposed default.
2. Ask one concrete question at a time, highest dependency first. Explain the affected user moment, give a recommendation and at most two alternatives when useful. Bundle closely related facts such as provider URL/version in one request.
3. Ask only when the answer materially changes scope, policy, cost, privacy or integration feasibility. Choose reversible implementation details autonomously and mark DEFAULT. Do independent work while awaiting required answers.
4. Put the answer, date, source (owner or verified provider reference), affected documents and implementation consequence in the decision register. Update the product docs in the same PR. Do not leave conflicting copies.
5. Before each phase, present a concrete reviewable slice, evidence, remaining questions and the next phase's entry conditions. Unanswered questions stay OPEN; silence is not approval.

Q10 was answered by the owner on 16 October 2026 (D14). Configured Keycloak session evidence remains a phase 1 integration gate.

## Decision register — 16 October 2026

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D01 | CONFIRMED, owner | Six personal schemes, both modes | Theme spec replaces original muted palettes |
| D02 | SUPERSEDED by D11 | Earlier IdentityCloak naming | Owner corrected provider to Keycloak |
| D03 | CONFIRMED, owner | Partner/admin/support dedicated email/password or magic link; optional MFA | Backend-enforced login matrix; separate support role |
| D04 | CONFIRMED, owner | Other users Apple/Google/email; extra options uncertain | Member links DEFAULT proposal; no extra social providers |
| D05 | CONFIRMED direction, owner | MAUI, Blazor, API, Contracts, Client, Domain, Infrastructure | Seven projects plus tests |
| D06 | CONFIRMED, owner | Local testing, Actions commented out, PR to main | No hosted workflow execution |
| D07 | DEFAULT, implementation | DatingApp namespace; .NET 10; SSR shell; separate local solution | Brand rename stays cheap; mobile tooling isolated |
| D08 | PARTLY SUPERSEDED by D12 | Invitation provisioning remains DEFAULT; separate staff/member account assumption withdrawn | Q10 defines dedicated-email access; no email-based classification |
| D09 | DEFAULT, implementation | Electric violet + System; member email links; 10-minute link expiry | Provider and phase review can amend defaults |
| D10 | DEFAULT, implementation, 16 October 2026 | Coarse account-area policy uses one application-owned role per evaluated session; no implicit role hierarchy; venue access requires an explicit nonempty venue ID and current membership | Phase 1 Domain slice only; separate-account portion of D08 superseded by D12. Additional roles/permissions require explicit design, not email matching or admin inheritance. See [slice](PHASE-1-ACCESS-POLICY.md) and [authentication](../architecture/AUTHENTICATION.md). |
| D11 | CONFIRMED, owner reply, 16 October 2026 | Keycloak is the central authentication service; official docs and source supplied | Supersedes D02 naming; inspect/reuse configuration before proposing a pin and realm |
| D12 | CONFIRMED, owner reply, 16 October 2026 | Accounts may have direct and linked Apple/Google credentials; registration, email ownership and session authentication are distinct evidence | Supersedes exclusive staff/member account assumption in D08/D10. Dedicated-email definition subsequently resolved by D14; provider evidence remains unvalidated |
| D13 | DEFAULT proposal, implementation, 16 October 2026 | Keycloak 26.8.1 and isolated datingapp-dev realm | No existing pin/realm/sandbox found; not deployed, configured or owner-approved. See [proposal](../architecture/KEYCLOAK-DEVELOPMENT.md) |
| D14 | CONFIRMED, owner Q10 reply, 16 October 2026 | Allow linked identities; privileged access requires verified email and a fresh direct email/password or approved email-link session | No separate social-free account requirement. Social sessions remain denied even on accounts with direct credentials. Configure and validate positive session evidence before enabling access; email-link capability remains unverified |

A default does not imply the corresponding feature has been implemented. New decisions append rows and supersede old ones explicitly.

D10 is an implementation default, not an owner answer to Q01–03/Q09. D03–04 and D11–12 remain authoritative. Member email links remain a proposal under D09 and are denied by the current policy until the decision and provider contract are resolved.

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D15 | CONFIRMED, owner 16 October 2026 | All server components containerised for public/dev infrastructure; mobile apps excepted | Prepare local Docker builds/testing; no deployment authorization |
| D16 | DEFAULT, implementation 16 October 2026; advances D13 | Separate digest-pinned API/web images and local-only Keycloak 26.8.1, empty datingapp-dev realm, generated private bootstrap secret, development embedded database | Reversible local infrastructure only; no app clients or login adapter. See [sandbox slice](PHASE-1-KEYCLOAK-SANDBOX.md). Production database/HTTPS/secrets remain a later deployment slice |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D17 | DEFAULT, implementation 16 October 2026 | Opt-in local Node browser investigation client, fixed loopback callback, copied password-execution AMR with experimental 60-second lifetime, private fictional-adult fixture with simulated administrator-asserted email status | Advances Q01 with browser/code/PKCE evidence only. No app authentication, privileged grant, real email ownership or product freshness policy. See [browser slice](PHASE-1-BROWSER-INVESTIGATION.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D18 | DEFAULT, implementation 16 October 2026 | Investigation transactions supersede older begins; serialize local session provider operations; logout invalidates pending callbacks; silent SSO preserves known identity/session/time; reauth preserves subject and validates auth_time against request start with five-second clock tolerance | Investigation-only consistency checks, no product privileged freshness policy or access grant. See [browser investigation](PHASE-1-BROWSER-INVESTIGATION.md#session-consistency-follow-up--16-october-2026) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D19 | DEFAULT, implementation 16 October 2026 | Pure Domain discovery gate requires active verified adults, reciprocal explicit intention/context/area rules, both-direction block enforcement and shared resource checks; contact adds permission, request limit, explicit incognito disclosure and actor/area-bound fresh travel presence | No endpoint/provider enabled. Hidden intentions remain checked; no-photo status never denies. Accepted-chat continuity and Q05 reply exception are outside this new-contact gate. See [scope](PHASE-2-DISCOVERY-POLICY.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D20 | DEFAULT, implementation 16 October 2026 | Static SSR appearance settings with a synchronous external guest preference bootstrap; versioned browser-local scheme/requested mode only; retain explicit guest choice on this browser; storage denial keeps in-memory choice with unsaved notice | No account sync, authentication or web render-mode decision. Generated web CSS/data come from themes.json. See [scope](PHASE-2-WEB-APPEARANCE.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D21 | DEFAULT, implementation 16 October 2026 | Portable appearance state uses per-activation response tickets, default display while loading, versioned server compare-and-swap/idempotency DTOs, unchanged retry identity offline and explicit conflict resolution; generic guest preference deliberately survives sign-out | No HTTP/account integration. Revision long.MaxValue is an explicit terminal read-only state: accept its acknowledgement and prevent further saves. Future authenticated transport must bind outgoing work to its activation. See [scope](PHASE-2-APPEARANCE-SYNC.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D22 | DEFAULT, implementation 16 October 2026 | Opt-in Development-only fictional discovery API/typed client/static SSR preview, fixed adult viewer, explicit verification/location simulations and generic resource denials; optional intention/activity filters demonstrate allowed/empty/denied states | No real session, privileged verdict, provider/storage connection or messages. Production suppresses routes regardless of flag. See [scope and local demo](PHASE-2-DISCOVERY-PREVIEW.md) |

| ID | Status / source | Decision | Consequence |
| --- | --- | --- | --- |
| D23 | DEFAULT, implementation 16 October 2026 | Opt-in unique temporary local RSA signing provider; preserve existing keys; verify JWKS overlap/removal and restore original active key; renew short-lived management authorization for cleanup | Provider/browser investigation only, no production validator/cache policy or privileges. See [scope](PHASE-1-SIGNING-ROTATION.md) |

| D24 | DEFAULT, implementation17 October2026 | Optional owned-fixture TOTP browser investigation with unique temporary authenticator, preexisting-credential refusal and exact cleanup; RFC6238 generator used only in test memory | No global MFA mandate, recovery or application access. See [evidence](PHASE-1-OPTIONAL-MFA.md) |

| D25 | DEFAULT, implementation17 October2026 | Immutable retained-pair Domain connection lifecycle; one text request, recipient accept/decline, terminal decline/close, accepted-chat continuity under fresh safety gates, independent historical reporting eligibility | Future store must enforce unique pair and CAS; no messaging/realtime enabled. Q05 pre-arrival acceptance stays denied. See [slice](PHASE-3-CONNECTION-RULES.md) |
