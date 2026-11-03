# Q120 / issue165 — Admin presentation availability

CONFIRMED D302. Tablets (768 CSS pixels and above) retain full layout; phones see the approved larger-screen recovery and overview link for photo review, identifier review, Legal holds/disclosures/law enforcement, identity/elevation/security, text/evidence review and reinstatement. Admin-queue cases (including serious incidents) also receive recovery; ordinary Support cases keep their basic workflow but hide protected evidence and preservation tools. Phone Beta and account detail retain summaries without mutation forms. Overview, Support lookup, basic account search/contact search remain available within existing role checks.

DEFAULT breakpoint768px is a layout choice, never trusted-device evidence. CSS and server-delivered markup are not an authorisation boundary; API roles, purpose, elevation, conflict and token checks remain unchanged. Browser zoom may select the narrow layout just as a phone does. No viewport/user-agent refusal was added to the API.

Tests cover 375/768/1440px workflow/recovery visibility and overview recovery link. Baseline owned browser RED observed missing phone recovery. Integrated source/browser verification pending; physical tablet/phone/Safari and assistive-technology acceptance remain separate.

Observed final owned Admin presentation pass:375/768/1440 CSS-pixel recovery/workflow visibility and concise copy assertions passed with an actual local Keycloak staff session, disposable PostgreSQL/API and published SSR artifact. `admin-presentation-final.log`, exit0; owned hosts/browser/database/client/staff cleanup succeeded. Backend roles/purpose checks remain separate, and no physical-device or assistive-technology acceptance is implied.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
