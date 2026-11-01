# Admin desktop redesign

CONFIRMED: owner brief authorizes implementation and a PR without design approval. Preserve Static SSR, API authority, confirmation forms, Standard tokens and every route. No intake-log/screenshot edits, deployment, new framework or application project.

DEFAULT: a 248px persistent sidebar, compact staff header and fluid workspace. Navigation groups: Overview; Moderation (queue, appeals, photos, birth date); Members (accounts, beta, support, contact history, identifiers); Governance (holds, law enforcement, disclosure register, elevated access, security); Reporting and resources. Detail routes inherit their parent selection. Existing API refusals remain authoritative; navigation grants no permission. Staff identity comes only from the current authenticated SSR principal, using the given name rather than adding contact data.

1. Audit all Admin routes and forms; capture the original rendered shell in an isolated browser environment. Record baseline checks.
2. Replace AdminLayout with grouped navigation and local inline SVG icons. Add an accessible modal drawer with Escape, focus containment/return, backdrop dismissal and desktop resize handling. Retain usable navigation if JavaScript fails. Keep server forms and antiforgery unchanged.
3. Apply shared spacing, headings, cards, filter tabs, controls, validation/status, empty states and horizontally scrollable labelled table regions. Wider forms use available width while prose stays readable. Remove redundant decision identifiers from visible explanatory copy only where they do not assist staff.
4. Browser-check fictional sessions at 1440, 1100 and 390px; verify active nested routes, keyboard skip/drawer/focus, table scrolling, confirmation controls and form submission. Capture before/after screenshots. Run scripts/check.sh and relevant checks.
5. Review diff for auth/business changes, resolve findings, document evidence/gaps and owned resource cleanup. Commit branch codex/admin-desktop-redesign and open PR to main.

No owner/provider decision is currently required. Reversible layout defaults are recorded here rather than creating questions about already-authorized details.

## Execution evidence

All implementation and validation tasks completed. `./scripts/check.sh` exited 0 with 596 PostgreSQL persistence cases (the initial baseline skipped those until private connection configuration was supplied). Real Edge passed 77 browser checks against owned hosts/database/fictional staff; representative screenshots and detailed limits are in `docs/delivery/evidence/2026-11-01-admin-redesign/README.md`.

Ruling: render the same navigation component separately for desktop and modal instead of moving server-rendered DOM — enhanced navigation can otherwise retain stale dialog state. Browser tests cover link navigation and reopening. Native dialog supplies modality; JavaScript remains presentation-only.

Final review found no server authorization or confirmation changes. Its concrete cleanup finding was addressed: the preview refreshes management auth after its wait and attempts resource cleanup independently. Preview startup/shutdown was exercised. Existing Docker containers and the main checkout's unrelated intake file were preserved. No unresolved owner/provider decision.
