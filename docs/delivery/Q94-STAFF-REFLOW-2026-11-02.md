# Q94 / issue135 — staff enlarged-text reflow

Actual owned Edge/Keycloak/Admin/PostgreSQL regression reproduces 365px document width at a 320px viewport and 200% root text. The overflow diagnostic identifies the header staff menu and Beta input/select controls. Waiting for fonts and two animation frames still reproduces it, so this is not just measurement timing.

The narrow header now wraps and grows in height, its page context can shrink, and Beta filter labels/controls are bounded and can shrink. The data table retains its independent horizontal scroll; no document overflow suppression was added. The test covers all existing Beta paging/search/status/revoke checks before measuring enlarged text, with fictional fixtures and a disposable database.

Owned Beta scenario passed after a fresh Admin publish: 1,005-row paging/search/status/revocation plus settled 320px/200% reflow. First CSS-only rerun still loaded old compressed assets; the fresh publish resolves that validation artifact. Repository q94-check passed build (zero warnings/errors) and portable suites, then stopped at PostgreSQL migration with PendingModelChangesWarning while the Q108 model/snapshot was in progress; no persistence or later HTTP smoke pass is claimed. Native screen readers, real zoom and physical Safari remain separate acceptance.
