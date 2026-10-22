# Ordered question backlog

No answer is needed to review the phase 0 foundation. Ask these when approaching their blocking phase; do not ask all at once.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q01 | PARTLY ANSWERED, owner 16 October 2026 | Phase 1 live auth | Keycloak docs/source confirmed; no existing pin/realm/sandbox found. Proposed pin/realm in D13; configured session evidence still needs sandbox validation | Use official sources; keep privileged integration disabled pending validated session evidence (Q10 answered in D14) |
| Q02 | OPEN | Phase 1 auth UX | Enable member magic links in addition to Apple/Google/email-password? What cross-device email-link behavior is expected? | Yes, if backend supports the required safe exchange |
| Q03 | OPEN | Phase 1 web interaction | Should authenticated web use Interactive Server or justify adding WASM client? | Server initially; measure scale needs before adding project |
| Q04 | OPEN | Phase 2 live verification | Which document/liveness provider, gate timing, retention and manual appeal route? | Gate browsing/contact; separate from Keycloak auth |
| Q05 | OPEN | Phase 3 travel | Can travellers reply before arrival when a local initiates? | Allow only replies to local initiation, subject to privacy rules |
| Q06 | OPEN | Phase 4 real commerce | Merchant of record, settlement, voucher benefit, refund and cancellation owner? | Sandbox only until contracts are decided |
| Q07 | OPEN | Phase 5 pilot | Initial city/markets and staffed support/moderation coverage? | Compact pilot; owner chooses market and operations |
| Q08 | PARTLY ANSWERED, owner 22 October 2026 | Phase 5 publishing | Swoovr is confirmed; trademark/store availability, first paid entitlements and prices remain unresolved | Use Swoovr conventions; no invented clearance or pricing |
| Q09 | OPEN | Phase 1 mobile acceptance | Which Apple/Xcode and Android toolchain versions/devices are available? | Pin supported workload set after checking official compatibility |

Extra social sign-in providers/passkeys are deferred. Do not reopen approved six-scheme personalisation or optional MFA as unanswered questions. See [decision process](DECISIONS.md).

## Phase 1 follow-up — 16 October 2026

Q01 was asked again in the implementation chat as one focused request for Keycloak documentation/source, version, sandbox configuration and trusted dedicated-email versus social authentication evidence. No answer or provider contract was available when this slice was prepared; Q01 remains OPEN. No protocol, endpoint or provider claim was inferred. Independent Domain policy checks proceed; real login remains blocked. Q02/Q03/Q09 remain OPEN and are not silently approved by this slice.


## Owner answer — 16 October 2026

Owner corrected the provider name to Keycloak and supplied https://www.keycloak.org/documentation and https://github.com/keycloak/keycloak. This supersedes the preceding unanswered-provider snapshot. Inspection found no existing version pin, realm export, sandbox deployment or credentials configuration in the repository. D13 proposes a version/realm without claiming an existing environment. Direct email registration, verified email ownership, linked identities and the current session method are separate; email never determines exclusive account type. Government ID/liveness remains Q04.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q10 | ANSWERED, owner 16 October 2026; D14 | Policy resolved; configured evidence still blocks access | Allow linked Apple/Google identities; require verified email and a fresh direct email/password or approved email-link session for privileged access | No separate social-free account required; no configured Keycloak evidence or adapter yet |

Q10 owner selected “Allow linked identities; require direct email session” in response to the verified-email/fresh-session proposal. D14 records this confirmation. Exact freshness window and the configured proof remain implementation/validation work, not invented provider claims.

## Local sandbox follow-up — 16 October 2026

Q01 now has a repository-owned reproducible local sandbox (D16), superseding earlier no-environment snapshots. Discovery and realm startup are a separate validation layer from configured session evidence. Q01 remains partly answered until direct/brokered/linking/SSO/refresh/reauthentication and token-validation tests establish the adapter contract. No owner answer is needed for disposable container defaults. Q02/Q03/Q09 remain OPEN; neither a magic-link feature nor a web render-mode change is introduced.

## Browser investigation follow-up — 16 October 2026

Q01 advances with an opt-in [browser harness](PHASE-1-BROWSER-INVESTIGATION.md): local ID-token validation and configured method/session observations. Fixture email status is administrator-asserted simulation; no ownership delivery is demonstrated. Real broker/linked-account transitions, optional MFA/recovery, rotation and application authorization remain OPEN validation gates. D17's 60-second AMR lifetime is an experiment, not an owner-approved freshness policy. Q02/Q03/Q09 remain unchanged; none blocks this isolated investigation.

## Owner naming answer — 22 October 2026

D30 confirms Swoovr for the product and engineering identifiers. It supersedes the previous namespace exception and naming shortlist. Q08 remains partly answered because publishing clearance and paid terms were not decided. See [naming](../architecture/NAMING.md) and [current status](STATUS.md).

## Keycloak integration answers — 22 October 2026

Q02 is ANSWERED for members and venue partners by D34: email sign-in links are wanted, built inside Keycloak; the API never mints them (D35). Cross-device behaviour is a DEFAULT in the [integration design](../architecture/KEYCLOAK-INTEGRATION-DESIGN.md#email-sign-in-link). Q04 is PARTLY ANSWERED by D40: Stripe Identity is the document provider and the AI-flag/DOB/override rules are confirmed; liveness, retention and appeal route remain OPEN. Q03 and Q09 remain OPEN and do not block the first integration slices. The table above keeps its original snapshot.

## Profile flags and modes — 23 October 2026

D55 confirms that flags are application-owned. The owner is still defining the profile, so these stay OPEN and block only the profile slice. See [profile flags and modes](../product/08-PROFILE-FLAGS-AND-MODES.md).

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q11 | OPEN | Profile persistence and badges | Final flag set and rules: what admin verified means and its badge; founding-member criteria; employee badge = ruby "Swoovr staff" bird; several badges at once; bot/employee accounts in discovery | Use the five drawn badges; add admin verified when it is defined |
| Q12 | OPEN | Profile, discovery and the segmented control | Final set of modes (brief: Connect / Friends & company / Travel & exploring; reference screens: Dating / Friends / Activities; owner examples: dating, friends, business) | Keep mode labels data-driven until the set is final |
