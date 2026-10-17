# Phase 3 — fictional connection web journey

Partial static SSR web journey over PR#15's isolated scenario API. No authenticated member experience, real messages, persistence or realtime. Existing six personal schemes remain canonical, no global brand selection. Q03 authenticated interaction decision remains OPEN; this uses the existing static SSR mode and built-in named forms/antiforgery.

DEFAULT: distinct Connection preview navigation entry only when Development+Prototype:Enabled; /prototype/conversation renders an off state otherwise. Start a disposable scenario with labelled simulated local/upcoming/arrived presence. View as fictional Alex/Jamie is a conspicuous demo role switch, never sign-in. One canned request; recipient accept/decline; accepted canned reply; close/block. No free text or attachments. Explanation of one request/wait, terminal decline/close, hidden blocked history and hypothetical reporting eligibility. Fresh GET after each mutation; stale/denied409 shows a controlled reload/retry state; expired/missing/unavailable/APIoffline shows a controlled start-over state. GET never mutates.

Design: reuse canonical generated --surface/--primary/--soft/--text/--muted/--border tokens and existing Georgia display/system body typography. Narrow single conversation column, left-aligned heading and chronological labelled messages; native wrapped controls,44px targets, clear simulation notice and per-state actions. No new palette, dashboard tile grid, animation or provider claim. Theme switches remain independent personal settings.

## Native plan

Branch codex/fictional-connection-web based onb823d6c/PR#15. Web references Client only.

- [x] Actual Edge test first: new route/heading missing before implementation. Test start/request/wait/roleswitch/accept/reply/block suppression/close, decline repeat, upcoming denied/arrived allowed, malformed query, stale revision, missing CSRF, APIoffline, theme/navigation,320px+200% text and keyboard. No browser secret traces.
- [x] Add SSR page with named POST form, SupplyParameterFromForm string model and AntiforgeryToken. Validate query/posted values before typed client calls, retain posted revision (never silently overwrite stale), redirect GET after success. Register typed client and conditional nav. Default/Production off tests in existing smoke. Shared tokens and responsive CSS only.
- [x] Actual Edge/fullcheck/independent review; optional published container journey, evidence screenshots; D29/validation/ledger; commit/PR main. Continue next authorized slice after delivery, no merge/deploy.


## Evidence —17 October2026

Actual installed Edge suite passed on local hosts and separately published ARM64 API/web containers. APIoffline was tested by stopping the host API; controlled unavailable state contained no sample messages. Missing CSRF returned400. A stale posted revision produced controlled error rather than closing the changed conversation. Both Alex-first and Jamie-first flows pass; the Jamie regression caught an initial sender assumption before the fix. Local Development/default-off and Production/flag-on render off with no create action. Full portable check passed; independent review no findings. [Detailed evidence/gaps](VALIDATION.md#fictional-connection-web-17-october2026).

Selected fictional evidence: [default desktop](evidence/connection/pending.png), [Cobalt/Dark](evidence/connection/cobalt-dark.png), [320px/200% controls](evidence/connection/narrow-controls.png). Canonical token generation unchanged. No live messaging/authentication or mobile/realtime proof.

Reproduce: start opt-in API/Web at loopback ports with Prototype:Enabled=true and Web Prototype:ApiBaseUrl pointing to API. With installed Edge and Playwright available, run `DATINGAPP_WEB_URL=http://127.0.0.1:5301 DATINGAPP_PLAYWRIGHT_PATH=/absolute/path/to/playwright node tests/prototype/conversation.browser.cjs`. Optional suite stays separate from dependency-free portable checks. Local test hosts/container network stopped after validation; no merge or public/dev deployment.
