# Admin desktop redesign — 1 November 2026

CONFIRMED scope: owner-authorized desktop-first visual redesign of the existing Static SSR Blazor administration app. DEFAULT visual choices: 248px grouped sidebar, compact staff menu, fluid workspace (up to 1800px), Standard monochrome tokens, restrained status badges and form/table spacing. No new package, application project, auth mechanism or API change.

The original 16 navigation destinations remain, with Overview and the existing disclosure register made explicit. Detail routes keep their parent selected. Both desktop and drawer render one shared navigation component; the native modal dialog supplies focus containment and Escape, and closes before enhanced navigation replaces its DOM. Without JavaScript the navigation stays inline and usable. The drawer scrolls independently. Navigation visibility grants no authority; existing API refusals, server form handlers, antiforgery and confirmation fields remain unchanged.

## Evidence

All screenshots use a newly created fictional staff identity and 14 synthetic cases in a disposable database. No existing member data, production export, credentials or intake-log screenshots were used. The before captures use the original shell/layout and CSS published separately; underlying handlers are the same. Screenshots are development evidence, not a live moderation or identity-verification claim.

| Workflow | Before | After |
| --- | --- | --- |
| Queue, wide desktop | [1440px](before-queue-1440.png) | [1440px](after-queue-1440.png), [1100px](after-queue-1100.png), [390px](after-queue-390.png) |
| Elevated access | [1440px](before-access-1440.png) | [1440px](after-access-1440.png) |
| Case decision | — | [Desktop](after-case-1440.png), [narrow](after-case-390.png), [server validation](after-validation-1440.png) |
| Overview | — | [Desktop](after-overview-1440.png) |
| Navigation drawer | — | [390px](after-drawer-390.png) |

Validation: `./scripts/check.sh` passed, including 33 Python tests, 95 and 32 Node tests, Release solution build with zero warnings/errors, canonical design/naming checks, 596 PostgreSQL persistence cases and the HTTP smoke suites. The database connection was provided privately; checks created their own throwaway databases. The initial baseline check passed but skipped persistence until that configuration was supplied.

77 real Microsoft Edge/Playwright checks use actual Keycloak code flow, an owned temporary OIDC client, fictional staff, disposable PostgreSQL and API plus two owned Admin hosts. They cover 1440/1100/390px, every top-level route, local table overflow, drawer focus/keyboard/Escape/resize and enhanced navigation, skip-to-main, nested route selection, keyboard account actions, no-JavaScript fallback, 200% text, forced colors/reduced motion, a missing-reason refusal followed by a recorded warning, and an elevated-access form submission. These do not establish screen-reader/native assistive-technology acceptance or exhaustive legal/media/provider workflow acceptance. The legal-only pages were inspected in their real permission-denied states for this admin role; no legal permission was added merely to make screenshots.

Review found and resolved a modal top-layer issue during enhanced navigation. CSS was consolidated and the design check's prohibited dropdown shadow removed. Business logic and all server authorization sources are unchanged. The issue-intake log and its screenshots were left alone. GitHub Actions remain disabled. Nothing was merged or deployed.

## Preview without merging

From the PR worktree (or a checkout of its branch), keep the existing managed Docker installation running. The preview borrows its local PostgreSQL/Keycloak infrastructure, creates an independent database and temporary staff identity/client, and starts hosts only on **15880, 15882 and 15883**. Occupied ports cause refusal rather than takeover.

```sh
dotnet build src/Swoovr.Api --configuration Release
dotnet publish src/Swoovr.Admin --configuration Release -o /tmp/swoovr-admin-preview-publish
SWOOVR_ADMIN_TEST_ENV=/Users/danijeljw/Developer/wanderconnect-brief/.local/platform/docker.env \
SWOOVR_ADMIN_TEST_PUBLISH=/tmp/swoovr-admin-preview-publish \
SWOOVR_ADMIN_PREVIEW=1 node tests/admin/run-isolated.cjs
```

The runner checks the browser first, then prints the preview URL ([localhost:15882](http://localhost:15882)) and the path to a private temporary JSON file containing fictional sign-in details. Open that file locally to sign in. Press Ctrl+C in the runner terminal to remove its temporary identity, client, database, browser and hosts. It does not stop Docker or reset existing data/configuration. The published binaries remain in the chosen output directory. Omit `SWOOVR_ADMIN_PREVIEW=1` for a test-only run with automatic cleanup. `SWOOVR_PLAYWRIGHT_PATH` can override the bundled Playwright module; Edge must be installed (or set `SWOOVR_EDGE_EXECUTABLE`). Screenshots default to ignored `.local/admin-design-evidence/`.

Optional before/after reruns accept `SWOOVR_ADMIN_BASELINE_PUBLISH` pointing to an original Admin publish directory and `SWOOVR_ADMIN_SCREENSHOTS` for an output directory. The committed original captures are sufficient for review; do not replace your running Admin to obtain them.

## Owned resources and limits

During delivery, owned API/Admin hosts ran on 15880/15882/15883; disposable databases used the `swoovr_admin_design_` prefix. Temporary clients/users used `swoovr-admin-design-` identifiers. All were removed after each test run, along with the owned Edge profile. The preview hold-and-Ctrl+C path was also exercised successfully, including its private fixture deletion. Private diagnostic logs and temporary build outputs were kept under `/tmp`; no credentials were committed. Existing platform containers and private configuration were preserved. No provider/owner decision blocked this visual scope, so no new Q-number or issue was needed.
