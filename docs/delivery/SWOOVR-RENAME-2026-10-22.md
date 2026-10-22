# Swoovr repository rename — 22 October 2026

## What, who and why

CONFIRMED, owner request on 22 October 2026: replace WanderConnect and DatingApp throughout the codebase with Swoovr/swoovr according to each platform's naming convention. This explicitly supersedes the original instruction to retain a neutral engineering namespace. [D30](DECISIONS.md#naming-confirmation--22-october-2026) records the answer; [naming](../architecture/NAMING.md) is the current convention reference.

The change is developed on `codex/swoovr-repository-rename` as a PR to `main`. Pushing the branch does not merge it or deploy any service. The scope is identity consistency and accurate documentation, not implementation of missing product features.

## Where and how it changed

| Before | After | Scope |
| --- | --- | --- |
| `DatingApp.sln`, `DatingApp.Local.sln` | `Swoovr.sln`, `Swoovr.Local.sln` | Solution filenames and project paths |
| `DatingApp.*` | `Swoovr.*` | Seven application projects, check project, C#/Razor/XAML namespaces, assemblies and references |
| WanderConnect | Swoovr | UI wordmark/titles, mobile title, design studio/SVGs, README and specifications |
| `DatingApp*` JS globals | `Swoovr*` | Generated palettes, appearance bootstrap/core/settings and tests |
| `datingapp.appearance.guest.v1` | `swoovr.appearance.guest.v1` | New guest preference key; no access/account state involved |
| `DATINGAPP_*` | `SWOOVR_*` | Optional browser test environment variables |
| `datingapp-local`, `datingapp-dev` | `swoovr-local`, `swoovr-dev` | Compose project, realm import, issuer/admin paths and smoke checks |
| `datingapp-investigation*` | `swoovr-investigation*` | Local investigation OAuth client, flow/resource ownership markers and cookie |
| `.local/auth-fixture.json` | `.local/swoovr-auth-fixture.json` | Isolated private fixture for the renamed realm |
| `com.companyname.datingapp.mobile` | `com.swoovr.app` | DEFAULT development application ID; no registered store/signing identity claimed |
| `wanderconnect-colour-studio.html` | `swoovr-colour-studio.html` | Design filename, links, generator and export names |

All build/check/smoke/browser scripts use the renamed paths. The palette generator emits Swoovr globals/namespaces from the same six-scheme canonical data. API routes, prototype gating and domain safety behavior are retained; the status response service label changes to `Swoovr.Api`.

## Documentation organisation and authority

[Documentation index](../README.md) answers where to find each what/how/why. [Current status](STATUS.md) owns today's delivered scope, integration boundaries, evidence links and next steps. Product specifications own requirements; architecture owns dependencies and naming; the decision/question registers own confirmed answers and unresolved choices. README and phase summary point to those authorities.

Historical slice records remain useful for when/how evidence was gathered. Their code identifiers/examples are updated, but original commit hashes, PR URLs, real absolute locations and binary captures retain their historical meaning. Prior investigation evidence does not certify the newly named realm. Old screenshots/concept artwork are not relabelled as new captures.

## Local migration and compatibility

1. Open `Swoovr.sln`, or use `Swoovr.Local.sln` without mobile workloads. Use the new `src/Swoovr.*` paths in commands and IDE configurations. Existing generated `bin/obj` contents are ignored; they are not source or migration evidence.
2. Existing browser preferences under the former key are left intact. The renamed app starts with Electric violet + System until a choice is saved under `swoovr.appearance.guest.v1`. There is no automatic migration or deletion of old browser data.
3. Stop the previous Compose stack before starting the new stack if it occupies ports 5080/5081/8180. From the previous checkout/configuration use its documented `down` command; alternatively `docker compose -p datingapp-local --env-file .local/docker.env down` targets that historical project. Do not add `--volumes`. No stop/reset/migration is performed automatically by this source rename.
4. `./scripts/docker-local.sh init` retains the private bootstrap environment. `up` uses `swoovr-local` and its own new volume; its import creates `swoovr-dev`. The previous `datingapp-local_keycloak-data` volume is retained. A new volume does not contain earlier accounts, sessions, optional MFA or signing experiments.
5. Run `auth-init` only after the new sandbox is ready. It creates `.local/swoovr-auth-fixture.json`; leave the old fixture untouched. Never copy a realm's user ID or credentials into another realm's fixture. The investigation OAuth client and cookie are renamed, so old sessions do not carry over.
6. Update optional browser command variables to `SWOOVR_PLAYWRIGHT_PATH`, `SWOOVR_WEB_URL` and `SWOOVR_BROWSER_OUTPUT`. Mobile consumers must use the new development app ID; installing it is a separate identity from any former build. Signing/provisioning and store availability remain OPEN.

The GitHub repository currently remains at `https://github.com/repasscloud/wanderconnect`; original PR links must continue pointing there. The host checkout remains `/Users/danijeljw/Developer/wanderconnect-brief`. Neither is a code identifier, and changing text would not rename the actual resource. This PR does not move the working directory, rename the hosted repository, rewrite Git history, or modify ignored private files/retained volumes.

## Validation

Executed locally on 22 October 2026:

- `python3 scripts/check_design.py --write`: regenerated canonical copies with Swoovr identifiers; all six schemes and 72 contrast pairs retained.
- `./scripts/check.sh`: PASS. Portable restore/Release build with zero warnings/errors; 431 named policy/state cases (50 access, 78 discovery, 88 connection, 70 meeting, 70 feedback, 75 appearance); 57 Node tests; actual local API/Web, discovery and conversation HTTP gates. Source/path naming, relative Markdown links, generated palette parity and disabled Actions also pass.
- `python3 scripts/check_naming.py`: PASS. A temporary introduced legacy namespace was rejected; the probe was removed and the clean audit passed. Historical exceptions are explicit and limited to the migration record and actual external/absolute historical references in documentation.
- `tests/appearance/web.browser.cjs`, `tests/prototype/discovery.browser.cjs`, `tests/prototype/conversation.browser.cjs`: PASS against renamed Development API/Web hosts on loopback ports 5380/5381, with `Prototype__Enabled=true` and `SWOOVR_WEB_URL=http://127.0.0.1:5381`. Used existing bundled Playwright through `SWOOVR_PLAYWRIGHT_PATH` and installed headless Edge. Appearance covers 18 preferences/12 visuals, persistence/navigation/keyboard and narrow 200% text; fictional discovery/connection suites cover allowed/denied/stale/CSRF/error states. Hosts/browser were stopped after checks.
- `docker build --target api ...` and `docker build --target web ...`: PASS using `containers/Dockerfile`, tagged `swoovr-rename-api:local` and `swoovr-rename-web:local`. Separate Linux ARM64 containers started using the renamed assembly entrypoints; loopback status/web/appearance HTTP probes passed on ports 5382/5383. Disposable containers were stopped/removed; no retained Keycloak volumes were touched.
- Compose service configuration resolves to API/Web/Keycloak using a nonsecret placeholder bootstrap value; renamed realm JSON and mobile XML/XAML metadata parse. Python compilation, Bash syntax and auth-module Node syntax checks pass. These are structural checks, not provider login or mobile builds.

Fresh captures: [appearance desktop](evidence/2026-10-22-swoovr/appearance-desktop.png), [discovery desktop](evidence/2026-10-22-swoovr/discovery-desktop.png), [connection narrow controls](evidence/2026-10-22-swoovr/connection-narrow.png). Appearance desktop and narrow controls were visually inspected; browser reflow assertions passed. Profiles/messages in captures are fictional.

A separate read-only reviewer found no actionable defects in the staged rename and independently passed naming/whitespace checks. Its validation-record completion reminder was addressed; final outcome is appended to the progress ledger. GitHub checks are a separate remote observation after push, not implied by local results.

Gaps: no MAUI workloads are installed (`dotnet workload list`); Android/iOS compilation/device/signing/store acceptance remains unverified. No newly provisioned `swoovr-dev` login/MFA/rotation journey was run; prior provider evidence remains historical and application sign-in remains disconnected. No trademark/store clearance, screen-reader audit, full authenticated app, deployment or production validation is claimed.
