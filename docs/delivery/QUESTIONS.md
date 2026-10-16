# Ordered question backlog

No answer is needed to review the phase 0 foundation. Ask these when approaching their blocking phase; do not ask all at once.

| ID | Status | Blocks | Question / consequence | Recommended starting point |
| --- | --- | --- | --- | --- |
| Q01 | OPEN | Phase 1 live auth | Which IdentityCloak docs/repo, version and sandbox? Which trusted signal distinguishes dedicated from social authentication? | Verify actual contract; keep access disabled until known |
| Q02 | OPEN | Phase 1 auth UX | Enable member magic links in addition to Apple/Google/email-password? What cross-device email-link behavior is expected? | Yes, if backend supports the required safe exchange |
| Q03 | OPEN | Phase 1 web interaction | Should authenticated web use Interactive Server or justify adding WASM client? | Server initially; measure scale needs before adding project |
| Q04 | OPEN | Phase 2 live verification | Which document/liveness provider, gate timing, retention and manual appeal route? | Gate browsing/contact; separate from IdentityCloak auth |
| Q05 | OPEN | Phase 3 travel | Can travellers reply before arrival when a local initiates? | Allow only replies to local initiation, subject to privacy rules |
| Q06 | OPEN | Phase 4 real commerce | Merchant of record, settlement, voucher benefit, refund and cancellation owner? | Sandbox only until contracts are decided |
| Q07 | OPEN | Phase 5 pilot | Initial city/markets and staffed support/moderation coverage? | Compact pilot; owner chooses market and operations |
| Q08 | OPEN | Phase 5 publishing | Final name, verified availability, first paid entitlements and prices? | Keep working brand; no invented pricing |
| Q09 | OPEN | Phase 1 mobile acceptance | Which Apple/Xcode and Android toolchain versions/devices are available? | Pin supported workload set after checking official compatibility |

Extra social sign-in providers/passkeys are deferred. Do not reopen approved six-scheme personalisation or optional MFA as unanswered questions. See [decision process](DECISIONS.md).
