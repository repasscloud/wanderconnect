# Member web responsive layout — 1 November 2026

CONFIRMED scope: member-facing web, existing black-and-white Standard default, private personal palettes, existing type/badges/components and all privacy/authentication behavior. Design references remain read-only. No native or staff/admin implementation change; no Actions, merge or deployment.

## Constraint and layout choices

The shared `.column` class capped discovery, profiles, connections, chat and account pages at420px even inside the1100px shell. Baseline actual-browser geometry at1440px measured420px for discovery. The old card grid consequently only fitted two170px cards. Widening `main` alone would have left that constraint intact.

DEFAULT reversible composition: preserve the1100px outer shell and explicit420px form columns. Discovery opts into the shell and adds columns of equal reference-size tiles. A375px phone fits two slightly narrower tiles; at390px and above ordinary text retains170px tiles. Relative tile measures, wrapping and a one-column fallback accommodate enlarged text. Mode controls and informational notices remain bounded.

Profiles separate their story/photos from connection/safety actions on desktop. Connections place received/sent/connected/blocked groups in two columns when each has room. Chat lists use broader rows; conversations stop at760px and bubbles at a comfortable reading width. Account details sit beside settings navigation; sensitive forms remain capped. Travel places trips and their composer side by side, with visitors below. Photos gain a gallery width. Home balances its existing introduction and information card. Reading pages stop at44rem; long articles retain65ch paragraphs. Navigation, safe-area spacing, dialogs and long actions/names can wrap.

## Route inventory

| Routes | Result |
| --- | --- |
| `/`, `/account` (guest and signed in) | Balanced introduction / account details and settings; bounded sign-in actions |
| `/explore`, `/travel/search/{trip}` | Full browsing shell, fixed-size tile grid; mode/guidance controls bounded |
| `/people/{id}` | Story and actions columns; same approved-photo/mode projections |
| `/connections` | Two-column groups at52rem; single column on smaller/text-enlarged views |
| `/chats`, `/chats/{id}` | Wider list rows; bounded conversation and wrapping safety actions |
| `/travel`, `/me/photos` | Travel composition and photo gallery width; forms bounded |
| `/settings/appearance` | Existing responsive controls/preview retained; palette infrastructure untouched |
| `/privacy`, `/terms`, `/community-guidelines`, `/safety/advice`, `/law-enforcement`, `/me/notices`, `/me/notifications` | Readable44rem content width |
| `/me/profile`, `/me/matching`, `/me/verification` (including return), `/me/meeting-safety`, `/account/data` (and deletion), `/account/policies`, `/account/restricted` |420px form width retained; shared reflow/navigation improvements apply |
| `/policies/{version}`, `/Error`, not-found | Bounded reading/status states retained |
| `/safety`, `/me/standing` (+ `/history`, `/reports`), `/me/safety` (+ `/people`, `/report`) | Issue 217 (D329): full settings-shell width; two columns from about 720px of content (history/tools left, help/privacy right), one column below; rows move their button under the text on narrow cards. `/safety` puts the country beside the heading, or under Emergency assistance on phones. Below 900px the Me list opens from the header's menu button. |
| Development previews | Existing widths retained; shared tile/navigation/reflow rules apply |

## Browser evidence

Before/after PNGs and geometry are in [evidence/member-responsive](evidence/member-responsive/). All identity/chat content is fictional adult test data. No real verification, screening, location or delivery claim. The suite uses actual local Keycloak + BFF and a disposable database/API, not a signed-in UI mock. No storage-state/token/credential files are committed.

Representative comparisons (light; dark desktop captures are alongside them):

| Page | Before | After |
| --- | --- | --- |
| Discovery375 | [Before](evidence/member-responsive/before/explore-375-light.png) | [After](evidence/member-responsive/after/explore-375-light.png) |
| Discovery768 | [Before](evidence/member-responsive/before/explore-768-light.png) | [After](evidence/member-responsive/after/explore-768-light.png) |
| Discovery1440 | [Before](evidence/member-responsive/before/explore-1440-light.png) | [After](evidence/member-responsive/after/explore-1440-light.png) |
| Profile1440 | [Before](evidence/member-responsive/before/profile-1440-light.png) | [After](evidence/member-responsive/after/profile-1440-light.png) |
| Connections1440 | [Before](evidence/member-responsive/before/connections-1440-light.png) | [After](evidence/member-responsive/after/connections-1440-light.png) |
| Chat768 | [Before](evidence/member-responsive/before/chat-768-light.png) | [After](evidence/member-responsive/after/chat-768-light.png) |
| Account1440 | [Before](evidence/member-responsive/before/account-1440-light.png) | [After](evidence/member-responsive/after/account-1440-light.png) |

Validation results are recorded below. Local acceptance does not establish a full WCAG audit, screen-reader/device/browser coverage or hosted readiness. Existing home-page prototype status copy is outside this layout change.

The200% text pass found two pre-existing intrinsic-width problems: unbroken intention chips and the native file input. Chips now wrap within their available width and uploads have a maximum width. Long mode-specific profile labels can wrap above their textareas. Mobile mode controls use two balanced columns rather than an uneven wrap. Text-size/viewport changes settle before geometry is read.

## Executed evidence and limitations

- Baseline:208 actual-browser route/theme/width observations; a deliberate desktop-width assertion failed at420px before the change.
- Full responsive acceptance:356 assertions and240 observations across375/768/1024/1440px, Standard light/dark, all16 resolved palettes on appearance controls,12 signed-in routes at200% text (320/768/1440px), keyboard navigation/profile focus and message sending. Empty/error/loading checks use explicitly simulated response/transport states after actual API eligibility. Their PNG filenames start `synthetic-`.
- Actual desktop geometry: discovery420→1052px; profile, connections, chat list and account420→960px; conversation420→760px. Reference tile dimensions still pass the existing component suite (170×270 at390px).
- Screenshots manually inspected: mobile and tablet discovery, desktop discovery/profile/connections, tablet conversation, dark account and enlarged-text mobile. Before/after captures cover eight representative pages; full-page mobile PNGs retain the fixed navigation at the viewport's bottom within the longer document.
- The account suite initially failed because its strict expected `/api/me` object omitted the existing `policyRequired` field. The one-line expectation now includes `false`; rerun passed122 checks. No API response or policy behavior changed.
- The older `tests/auth/members.browser.cjs` was attempted and failed404 at line151. It hard-codes the MAIN API (`127.0.0.1:5080`) rather than the owned API and does not use the shared versioned test helper. It is not counted as passing and is excluded from the owned selector. Account/BFF and auth-navigation evidence are recorded separately. Repairing that legacy harness is outside this layout change.
- No screen-reader certification, Safari/Firefox/device acceptance or live-provider/hosted claim. Native and staff/admin implementation and design tokens remain untouched.

The required repository gate passed, including596 PostgreSQL cases and a Release build with0 warnings/errors.

At320px with200% text, Explore's mode control falls back to one column and the bottom navigation wraps to two rows while reserving the corresponding5rem height plus safe area. Standard375px navigation retains four tabs. The complete356-assertion responsive run was repeated after this visual refinement.

| Existing suite | Actual result |
| --- | --- |
| Components | PASS60 |
| Accounts | PASS122 after the expected existing field was added |
| Profile modes | PASS93 |
| Connection requests / blocks | PASS104 |
| Market discovery | PASS148 |
| Travel / Traveller Search | PASS105 /108 |
| Messaging | PASS111 |
| Guest appearance / account appearance | PASS24 choices,16 palettes /68 checks |
| Public/member accessibility floor | PASS25 pages at390 and320px |
| Import-map / authentication navigation | PASS17 |
| Privacy notice / safety resources | PASS12 /58 |
| Expanded accessibility interactions | FAIL, repeated: single staff320px/200% assertion; no member finding reported. Staff remains outside scope. |

Follow-ups are tracked both ways in [QUESTIONS.md](QUESTIONS.md): [Q93 / issue134](https://github.com/repasscloud/wanderconnect/issues/134) for the incompatible legacy member-auth harness; [Q94 / issue135](https://github.com/repasscloud/wanderconnect/issues/135) for staff enlarged-text reflow. Neither failure is counted as passing. All owned runners removed their disposable database/API and restored their positively identified original MAIN web/admin hosts. No serving binaries were rebuilt during browsers.

## Restoration and handoff

The original seven `swoovr-platform` containers were restarted from their existing images/volumes without rebuild/reset. PostgreSQL, Keycloak, Mailpit and screening returned healthy; API liveness, member safety page, admin signed-out page, member-realm metadata and screening readiness all returned200. The temporary native MAIN listeners were positively identified by exact checkout/project working directories before stopping; the three newly created legacy infrastructure containers/network were removed without deleting volumes. Actual `.local/logs/{api,web,admin}.log` and initial Docker logs were preserved under private timestamped `.local/log-backups/` directories with0600 files before replacements/restoration. MAIN checkout remained clean. No shared credential/configuration file was edited, no merge/deploy or Actions activation occurred.
