# Owner answers and issue fixes implementation plan

Goal: record the 2 November owner answers and implement every independent tracked slice in one draft PR to main.

Architecture: retain the existing modular monolith, Keycloak identity boundary, typed contracts and shared web/native policy. No projects, deployments, Actions or design-source changes.

Spec: OWNER-QUESTIONS-2026-11-02.md, ISSUE-INTAKE.md and the owner's issue comments, captured in DECISIONS.md D289 onward.

Execution: inline, as requested; no approval pause. Work on the dedicated branch in the existing checkout to retain private local paths. Never rebuild while browser suites are running. Keep per-issue commits and actual check.sh evidence; preserve all owner services and data.

Review focus: anonymous protocol endpoints versus page allowlist; provider identity versus public mode names; downgrade hiding versus deletion; independent staff capabilities/elevation; preservation and replay/concurrency across mutations.

- [ ] Record owner answers and supersessions, including misfiled 156.* answers on #155; keep unanswered Q110 open.
- [ ] Q99/#141: server-side public-page allowlist, country-only guest safety, home authentication entry, approved operative policy presentation, Standard guest appearance and native parity. Regression requests must distinguish protocol/static resources from navigable pages.
- [ ] Q90/#124: reconcile existing UAT preparation with approved topology; subscription/deployment remains owner action.
- [ ] Q83/#127: back up ignored env/launcher, wire existing Android provider from approved external files without exposing keys; physical device/APNs remains open.
- [ ] Q106/#148: phone-first two-step setup, revisioned server safety dismissal, account sections/sidebar and version footer; test persistence and gate-off behavior.
- [ ] Q108/#150: mode-owned explicit main photos, tier limits/retained library/downgrade projection; test migration with no auto-selection and placeholders.
- [ ] Q110/#155: leave unanswered lookup/browser permissions blocked; no invented authorisation. Q111/#156: implement approved scoped account detail/preview without unrestricted chat/identity disclosure.
- [ ] Q112/#157: authoritative revisioned registration settings and provider creation enforcement, Root mutation/all-staff read/fail-closed; test four states and provider outages.
- [ ] Q114/#159 and Q115/#160: separately commit acknowledged review workspace/capabilities and bounded appeal/reconsideration/notice retention, including stale/concurrent/hold refusals.
- [ ] Q116/#161: atomic one-use 24-hour DOB grants, existing-date locks/confirmation, protected history and identity comparison/manual review; preserve D166 mismatch flow.
- [ ] Q117/#162: required self-declared registered names/mononym/provider confirmation, public-name initialization only if unset, name-edit verification revocation; never decrypt identity for ordinary staff.
- [ ] Q118/#163: optional per-mode names, safe public fallback and conversation-origin name; verify export/deletion/snapshots.
- [ ] Q120/#165: presentation-only device matrix, tablets full tools, narrow phone recovery; keep backend controls.
- [ ] Q119/#164: inventory actual Admin routes and remove implementation prose/IDs; retain operational warnings and confirmations. Reuse existing CSS.
- [ ] Q98/#140: residual G–L controls/table spacing with filters/paging/actions intact.
- [ ] Q113/#158: reproduce POST refresh in owned SSR browser; implement replay-safe recovery without losing reveal/form validation state or weakening antiforgery.
- [ ] Q94/#135: identify actual 320px/200% overflowing staff element, fix layout and rerun.
- [ ] Q93/#134: current versioned owned API helpers in legacy auth suite; retain negative cases.
- [ ] Q92/#132: identify native GSS dependency and fix runtime package; prove actual image/operator behavior without suppressing diagnostics.
- [ ] Review branch, run final repository gate and targeted suites, restore/verify owner's main hosts, push explicit branch refspec and open one draft PR. Comment touched issues with Q/D/commit, remove processed labels, close only through appropriate PR keywords. List remaining gaps honestly.
