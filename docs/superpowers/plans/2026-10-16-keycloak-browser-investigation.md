# Keycloak browser investigation implementation plan

Goal: reproduce local authorization-code + S256 browser observations without enabling application authentication.
Architecture: standalone Node built-in tooling under scripts, using the existing isolated Docker realm. No application dependency or project changes. DEFAULT: Node >=22, callback http://127.0.0.1:8280/callback, five-minute transactions and experimental AMR lifetime 60 seconds. CONFIRMED D14 remains enforced by leaving privileged integration disabled.
Spec: [sandbox investigation matrix](../../delivery/PHASE-1-KEYCLOAK-SANDBOX.md).

## Tasks

- [x] Add failing Node tests for RS256 ID-token verification (signature, issuer, audience/azp, nonce, time, subject/session), one-shot browser-bound state and expiry; implement helpers until green.
- [x] Add idempotent, local-only Admin REST provisioning for a dedicated harness client and copied browser flow. Configure positive password-execution AMR and broker-note mapper; generate a private fictional-adult fixture. Do not overwrite unmanaged users/clients. Never print secrets. Admin-asserted email status is simulation only.
- [x] Add loopback browser harness: explicit callback, S256/state/nonce, HttpOnly binding cookie, POST/origin protection for starts/refresh/logout, fixed destinations, bounded responses/timeouts, no token logging/persistence. Render only redacted observations. Exercise direct browser login, SSO, forced reauth, refresh and logout where supported.
- [x] Improve local lifecycle commands and checks; record commands, actual evidence and gaps. Run ./scripts/check.sh before committing. Review changes, commit and open PR to main. Do not merge/deploy.

## Review focus

Reject wrong/duplicate/replayed/expired callbacks without token exchange; require browser binding. Never follow arbitrary discovery endpoints. Do not turn token decoding, refreshed iat, absent broker notes or fixture email assertions into privileged grants. Keep passwords/tokens outside Git and HTTP reports. Existing realm startup import must not require volume deletion.

Execution: inline under owner's autonomous instruction. Dependencies on real social credentials, mail and mobile tooling remain explicit gaps; continue local reproducible work.

Review ruling: fail closed for missing stored fixture IDs; explicit private fixture replacement is required after realm reset. Added signed HTTP positive/continuity/logout tests after independent review. Product/API integration and owner/provider gates remain out of scope.
