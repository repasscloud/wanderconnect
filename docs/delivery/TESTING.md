# Testing runbook

How to build, run and test Swoovr locally, end to end, the way the overnight runs do. Written 28 October 2026 after slices AG–AI (PR #104). Local only: fictional adults at `example.test`, synthetic images, no real personal data. See [local development](LOCAL-DEVELOPMENT.md) for the background on each piece and [validation](VALIDATION.md) for recorded results.

## Where to work

- The owner's hosts run from the **main checkout** (`~/Developer/wanderconnect-brief`, Debug build). Never develop there.
- Each run works in its own worktree under `.claude/worktrees/`, created **without an upstream** and with the main checkout's `.local` symlinked in:

  ```sh
  cd ~/Developer/wanderconnect-brief && git fetch -q origin && git worktree add --no-track -b <branch> .claude/worktrees/<name> origin/main && ln -s ~/Developer/wanderconnect-brief/.local .claude/worktrees/<name>/.local
  ```

  The run starting 29 October 2026 uses **`.claude/worktrees/next-run`** on branch **`feature/swoovr-notifications-standing-attachments`** (already created).
- Push only with an explicit refspec and check where it went:

  ```sh
  git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push origin refs/heads/<branch>:refs/heads/<branch>
  git ls-remote origin refs/heads/<branch> refs/heads/main
  ```

  Never push to `main`; merge only when the owner asks. `.local/` is shared with main through the symlink: back a file up before changing it.
- Remove a worktree once its PR is merged: `git worktree remove .claude/worktrees/<name> && git branch -d <branch>` from the main checkout (after `git pull --ff-only`).

## Before anything

1. Docker stack (PostgreSQL 5433, Keycloak 8180, Mailpit 8025), from the **main checkout** so Keycloak serves main's theme:

   ```sh
   cd ~/Developer/wanderconnect-brief && docker compose --env-file .local/docker.env up -d --wait postgres mailpit keycloak
   ```

   If Keycloak shows the stock theme: `docker compose --env-file .local/docker.env up -d --force-recreate --wait keycloak && ./scripts/docker-local.sh realms`.
2. Photo screening (D201): the owner's image-moderation-api on 127.0.0.1:8080.

   ```sh
   cd ~/Developer/image-moderation-api && docker compose up -d && curl -s http://localhost:8080/ready
   ```

   Arachnid Shield is on only when its credentials are in that project's own `.env` (never copy them into this repository). Without them the local hosts pass photos with `known_content_checked = false` (Development only).
3. Edge and Playwright: `SWOOVR_PLAYWRIGHT_PATH=/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright`.
4. The fictional root admin must be enrolled once: `node tests/auth/root-admin.browser.cjs` (with the Playwright path).

## Portable checks (before every commit)

```sh
./scripts/check.sh
```

Builds Release, runs the policy/Node checks (`tests/Swoovr.Checks`, persistence checks against a throwaway database when PostgreSQL is up), design/naming/link checks and short host smoke tests, then `git diff --check`. Common failures: trailing whitespace or "new blank line at EOF" (files must end with exactly one newline); raw colours in CSS or Razor (`#103` in a Razor comment reads as a colour: write "issue 103"); a new project not allowed by `scripts/check_naming.py`.

## Hosts for the browser suites

Build Debug **in the worktree**, then start its hosts (this stops whatever hosts are running, including the owner's, and signs everyone out):

```sh
cd ~/Developer/wanderconnect-brief/.claude/worktrees/<name> && dotnet build Swoovr.Local.sln -c Debug && Phone__Required=false Accounts__ReauthSeconds=45 bash .local/run-hosts.sh > /tmp/hosts.log 2>&1; tail -1 /tmp/hosts.log
```

- API http://127.0.0.1:5080, web http://localhost:5081 (always `localhost`), admin http://localhost:5082. Logs in `.local/logs/`.
- Redirect host output to a file; don't pipe it.
- `.local/run-hosts.sh` passes the R2 (`Media__R2__*`), screening (`Media__Screening__BaseUrl`, default http://127.0.0.1:8080) and `Media__Screening__RequireKnownContentCheck` (default false) settings, and any `Media__Screening__*` already in the environment. `SWOOVR_EXTRA_API_PORT=5090 bash .local/run-hosts.sh` starts only one more API on that port (the photos suite does this itself).
- For the phone suite, restart without the overrides: `bash .local/run-hosts.sh`.

## The suites

Run each from the worktree root:

```sh
SWOOVR_REAUTH_SECONDS=45 SWOOVR_SCREENSHOTS=$PWD/.local/suite-shots SWOOVR_PLAYWRIGHT_PATH=/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright node <suite>
```

| Suite | File | Last result (29 Oct 2026, AJ–AL run; all passed) |
| --- | --- | --- |
| API contract dates | `tests/api/versioning.browser.cjs` | New slice; see VALIDATION.md |
| root admin | `tests/auth/root-admin.browser.cjs` | 9 |
| accounts | `tests/auth/accounts.browser.cjs` | 121 |
| members | `tests/auth/members.browser.cjs` | 50 |
| components | `tests/ui/components.browser.cjs` | 60 |
| modes | `tests/profiles/modes.browser.cjs` | 93 |
| badges | `tests/profiles/badges.browser.cjs` | 58 |
| requests | `tests/connections/requests.browser.cjs` | 104 |
| travel | `tests/travel/travel.browser.cjs` | 106 |
| Traveller Search | `tests/travel/traveller-search.browser.cjs` | 108 |
| market | `tests/discovery/market.browser.cjs` | 148 |
| matching | `tests/discovery/matching.browser.cjs` | 80 |
| moderation | `tests/moderation/moderation.browser.cjs` | 144 |
| serious incidents | `tests/moderation/serious.browser.cjs` | 61 |
| legal holds | `tests/evidence/holds.browser.cjs` | 83 |
| identity store | `tests/verification/identity.browser.cjs` | 38 |
| Didit | `tests/verification/didit.browser.cjs` | 138 |
| mobile client | `tests/auth/mobile-client.browser.cjs` | 31 |
| app parity | `tests/mobile/app-parity.browser.cjs` | 104 |
| messaging | `tests/messaging/messaging.browser.cjs` | 111 |
| deletion and export | `tests/accounts/deletion.browser.cjs` | 115 |
| lifecycle and privacy | `tests/connections/lifecycle-privacy.browser.cjs` | 60 |
| law enforcement | `tests/evidence/law-enforcement.browser.cjs` | 130 |
| staff access | `tests/staff/access.browser.cjs` | 66 |
| security operations | `tests/security/operations.browser.cjs` | 36 |
| location privacy | `tests/discovery/location-privacy.browser.cjs` | 28 |
| privacy notice | `tests/privacy/notice.browser.cjs` | 10 |
| photos | `tests/media/photos.browser.cjs` | 119 |
| Safety & Support | `tests/safety/resources.browser.cjs` | 58 |
| accessibility floor | `tests/accessibility/pages.browser.cjs` | 19 pages |
| notifications (AJ) | `tests/notifications/notifications.browser.cjs` | 98 |
| account standing (AK) | `tests/moderation/standing.browser.cjs` | 71 |
| neutral conversation endings/support | `tests/messaging/endings.browser.cjs` | See current VALIDATION.md |
| chat photos, endings, starters (AL) | `tests/messaging/attachments.browser.cjs` | 98 |
| phone (**default hosts**) | `tests/accounts/phone.browser.cjs` | 52 |

Run them one at a time (they share rate limits and the database). Never rebuild the solution while they run: on 29 October a mid-run rebuild of the web project left the running web host serving mismatched WebAssembly files and four web suites timed out until the hosts were restarted. A loop that writes `PASS`/`FAIL` per suite to a summary file and runs in the background works well; see the AG–AI run.

## Test data hygiene

- **Visible trips**: before travel and market, cancel old fictional trips (the visitors feed shows 50):

  ```sh
  docker exec swoovr-local-postgres-1 psql -U swoovr -d swoovr -tAc "UPDATE trips SET cancelled_at = now() WHERE cancelled_at IS NULL AND created_at < now() - interval '10 minutes' AND account_id IN (SELECT id FROM accounts WHERE email LIKE 'fictional.%@example.test')"
  ```

- **R2 objects**: the photos suite deletes its objects at the end. After an aborted run, `node tests/media/cleanup.browser.cjs` releases leftover `media-screening` holds on fictional accounts only and deletes their objects (expect "Fictional photo objects left: 0").
- **Fictional numbers**: 0491 570 156–159 (release them in SQL first if a suite needs them). The identity suite also clears older `terminated_phone_holds` (keyed hashes of those numbers), or yesterday's hold stops a fresh two-year one.
- **Rate limits**: messages 30/min, location checks 10/h, phone codes 1/min per number and 5/h, searches 60/h and filter changes 10/h per member. Seed bulk data in SQL, not through the API.
- **Safety test country**: `ZZ` belongs to the safety suite (never offered to members); the suite clears its contacts each run.

## Writing a suite

- Use `tests/auth/local-tokens.cjs`: `createMember`, `memberToken`, `createStaff` + a root-admin invitation + `staffToken` and a first call to `/api/session` (staff accounts exist only after it), `matchReady`, `elevate` (identity opening, evidence export and termination need elevated access), `cli`, `sql`, `call`, `mailTo`.
- Call `/api/me` (or any member route) before selecting member ids in SQL.
- Cover negative cases: other members' ids, blocked pairs, bots, staff without the role, retired or unknown ids, restricted and suspended members, tampered bodies.
- Sensitive actions on test hosts need a sign-in within 45 seconds: get a fresh `memberToken` just before (export, deletion, number change). Keycloak `auth_time` has whole seconds: wait 1.5 s after a revocation before signing in again.
- Releasing a hold needs a `legal` officer who neither placed nor confirmed it, and `overlapsChecked: true` when holds overlap.
- `sensitive_access_log` refuses UPDATE, DELETE and TRUNCATE by trigger.
- Blazor drops `false` boolean attributes: render "yes"/"no" for data attributes tests read. On phone-width pages the sticky header intercepts clicks: use `el.click()`.
- Admin SSR pages must load their data on POST too. Person links carry `?mode=`.
- Raw SignalR test clients must send pings (type 6).
- Photos: synthetic images only (`tests/media/synthetic-images.cjs`); drive uncertain and blocked outcomes with `Media__Screening__Simulate` on an extra API (Development only), never with real imagery. Chat photos (`tests/messaging/attachments`) upload to `/api/conversations/{id}/photos` the same way.
- Messages come back **newest first** (`items[0]` is the latest).
- Things that happen right after screening (the recipient's notification, the chat moving up) land a moment after the sender sees "approved": wait for them rather than asserting at once.
- Once `accounts purge` deletes a removed media item's object it deletes the row too: assert "no object left" (`object_key IS NOT NULL` count 0), not a status.
- Mail: `mailTo()` returns bodies only; the notifications suite has a `mail()` helper with subjects (simulated pushes land at `push-<device id without dashes>@simulated.swoovr.invalid`).
- `cli()` passes the Mailpit settings, so `notifications sweep` and other jobs can email; a cron needs the same `Email__Smtp__*` settings as the hosts.
- Urgent report categories must be acknowledged before a case can be closed (`/cases/{id}/acknowledge` first).
- Creating issues from a script: in zsh a variable holding `--label a --label b` isn't split; pass `--label a,b` (or use Python's `subprocess`).

## Code rules that bite

- Web checkboxes inside `label.check` need the `<span class="box">…</span>` after the input or they're invisible; radios there use their own visible style.
- Don't rebuild while the regression loop runs: the hosts load from the same `bin/Debug`. Mobile builds are safe (they write only the mobile and shared-library outputs).

- Services resolved by DI get `SwoovrDbContext` lazily through `IServiceProvider` (the smoke test runs the API without a database). Middleware that writes to the database needs its own scope.
- Migrations add new tables or nullable columns only (the owner's hosts share the database); `dotnet ef migrations add <Name> --project src/Swoovr.Infrastructure --startup-project src/Swoovr.Infrastructure`.
- New member routes go on the BFF allow-list (`src/Swoovr.Web/Auth/Bff.cs`); never an admin route.
- Audit entries hold ids only.
- Keycloak serves the theme from the main checkout: theme changes are visible live only after merge.

## Apps

```sh
cd ~/Developer/wanderconnect-brief/.claude/worktrees/<name> && JAVA_HOME=$HOME/Library/Developer/Java/jdk ANDROID_HOME=$HOME/Library/Android/sdk dotnet build src/Swoovr.Mobile -f net10.0-android -c Debug -t:SignAndroidPackage -p:EmbedAssembliesIntoApk=true
cd ~/Developer/wanderconnect-brief/.claude/worktrees/<name> && dotnet build src/Swoovr.Mobile -f net10.0-ios -c Debug
```

(In the worktree sandbox, set `JAVA_HOME`/`ANDROID_HOME` inside a script file.) Emulator `swoovr-pixel`; signing in through Chrome's custom tab under uiautomator is unreliable: record it as not run rather than looping. The owner taps through iOS: `dotnet build src/Swoovr.Mobile -f net10.0-ios && dotnet build src/Swoovr.Mobile -f net10.0-ios -t:Run` (a plain build first; `-t:Run` stays attached).

## After testing

- Record evidence and gaps in [VALIDATION.md](VALIDATION.md) and [STATUS.md](STATUS.md) (counts, what wasn't run and why).
- Restore the owner's hosts from main with defaults:

  ```sh
  cd ~/Developer/wanderconnect-brief && git pull --ff-only && dotnet build Swoovr.Local.sln -c Debug && bash .local/run-hosts.sh
  ```

  Check the API's working directory is the main checkout (`lsof -p <pid> | grep cwd`).

### Closure outbox recovery

D224: `notifications sweep` also resumes committed closure notifications with no dispatch completion marker. Per-device attempting/unknown outcomes are not blindly resent; inspect `notification_deliveries` by notification/device id (no tokens or message bodies in operator output). A provider acknowledgement is distinct from device delivery. The ending suite seeds interrupted progress using fictional rows, including an ambiguous first device and a second device not yet attempted, and checks repeated sweeps do not multiply sends or centre entries.


### Configured push tests (D226–D228)

After a Debug API build: `node --test tests/notifications/startup.test.cjs` verifies seven unsafe/incomplete Production configurations (including public-only keys) fail before serving. `./scripts/check.sh` includes generated-key/fake-HTTP APNs/FCM and encryption checks; no live provider request is made. `tests/notifications/notifications.browser.cjs` covers encrypted registration/rotation/ownership/limits and muted retries against the local API; `tests/messaging/endings.browser.cjs` covers interrupted per-device dispatch.

Opt-in native compilation: Android with `-p:EnableFcmPush=true`; iOS with `-p:EnableApnsPush=true -p:EnableCodeSigning=false` verifies simulator code only. Signed APNs requires the owner's provisioning profile; FCM Release requires external `-p:SwoovrGoogleServicesJson=/absolute/private/path/google-services.json`. Use [provider setup and acceptance gates](../architecture/PUSH-NOTIFICATIONS.md). Run native builds sequentially because restores share mobile assets. No native compilation or Mailpit result proves delivery.
