# Investigation session continuity implementation plan

**Goal:** Reject stale or conflicting browser investigation evidence and concurrent session mutations; preserve the investigation-only boundary.

**Spec:** docs/delivery/PHASE-1-BROWSER-INVESTIGATION.md and docs/architecture/AUTHENTICATION.md.

**Architecture:** Keep Node built-ins and the existing loopback harness. Transactions capture creation time and a browser generation; a per-session operation lock prevents overlapping token/logout writes. No application adapter or privileged verdict is introduced.

**Constraints:** Seven application projects; Swoovr namespace; no real identity data; disabled Actions; no merge/deployment. Existing owner files remain untouched.

**DEFAULT:** Five seconds of reauthentication clock tolerance, measured against transaction creation; this is investigation request validation, not a product privilege freshness window. Most recent browser begin supersedes earlier unfinished transactions. Silent SSO must retain any previously observed subject/session/auth_time; explicit login may switch identity. Reauthentication must retain an existing subject but may establish a new session.

**Review focus:** stale reauthentication, silent subject/session/time substitution, callback reordering, refresh/logout overlap, signing-key replacement. Tests use signed synthetic tokens and real loopback HTTP; they do not prove provider behavior.

- [x] Add HTTP regression tests for stale reauthentication, silent continuity, superseded callbacks and overlap. Observe failures.
- [x] Add transaction timestamps/generation and enforce session operation locking and evidence consistency; run all Node checks.
- [x] Add signed JWKS rollover/removal checks; run local container/provider probes and actual browser login/SSO/reauth/logout.
- [x] Record evidence/gaps and ledger, run ./scripts/check.sh, review, commit and open PR to main.

Next independent slice after delivery: Phase 2 pure Domain discovery eligibility, which needs no provider credentials or web render decision. Reassess all phases again after that slice.
