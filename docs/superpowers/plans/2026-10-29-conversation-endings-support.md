# Conversation endings and support implementation plan

Execute natively under the owner's autonomous handover. Specification: CONFIRMED D213/Q85 and D214/Q86, full owner comments in OWNER-DECISIONS-2026-10-29.md. Use Swoovr's existing visual system and the attached neutral closed-screen reference.

**Goal:** A neutral, persistent, idempotent end flow across web/app, and official support links in standing/admin/privacy.
**Architecture:** Existing conversation/message/notification records and API. System events projected per viewer without exposing the initiating member; closing transaction serialises competing end operations. No new project or provider. Additive wire fields only, same date contract.

## Tasks

- [x] Red: pin the three exact owner farewell strings in ChatAttachmentChecks; integration tests for system events, neutral notifications, duplicate/concurrent ends, retained history, all modes/cooldown, no initiating metadata and support URLs.
- [x] Domain/client: exact farewells, distinct system/farewell kinds, MemberEnded reason, participant-specific system text and shared neutral closed/help/support copy. Enforce no free-text closing.
- [x] API: transaction + conversation row lock, disconnect/audit each mode, store farewell then neutral system event, close with MemberEnded, idempotent replay; read-only history remains available. Coordinate message writes with closure. Notify only recipient with dedupe, generic push regardless of preview setting, no farewell in notification. Respect block/privacy/preferences. Preserve original closed conversation when later valid connection starts a new one.
- [x] Projections: system Mine=false, no initiator field/reason in public DTO; viewer-specific text. Export identifies system as system rather than either member. UI has no member-message actions on system events.
- [x] Web and MAUI: confirmation, exact choices + none, neutral closed banner and read-only history, report/block after closure, live closure reload. Add official support page/email and support-reference access on standing (web/app/admin) and draft privacy notice; do not build corporate form/ticketing or claim an SLA.
- [x] Validate: targeted ending/standing/attachments/messaging/lifecycle/privacy/notification/accessibility browser suites, full check.sh, Android/iOS builds; UI screenshots with fictional adults. Record native UI/device gaps honestly. Restore main hosts after testing.
- [x] Same-commit STATUS/DECISIONS/VALIDATION and affected product docs; explicit push/verify, update draft PR and issue comments (Q/D/commit), then consume owner labels and continue to push.

## Review focus

- Concurrent duplicate ends produce one event/notification/audit and leave every mode closed.
- A late/concurrent message cannot be delivered after closure; old closed history never reopens implicitly.
- Recipient payload/export/notification does not leak initiating-party metadata or the optional farewell preview.
- Existing block/serious-report behavior and fail-closed member access survive the new closed state.
- A muted or private-preview recipient still gets correct centre behavior and no sensitive push content.
