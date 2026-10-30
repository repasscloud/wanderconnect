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

Before every host restart (including restoring main), preserve the actual `.local/logs/api.log`, `.local/logs/web.log` and `.local/logs/admin.log` in a timestamped `.local/log-backups/` directory with private0600 files. The launcher overwrites those files; `.local/api.log` and the other legacy root paths are not the current log locations. Back up any shared configuration file before editing it too.

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
| Hosted retention worker/restart | `tests/retention/worker.browser.cjs` | New slice; see VALIDATION.md |
| Safety retention clocks/authority | `tests/retention/schedule.browser.cjs` | New slice; see VALIDATION.md |
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


### Blocked-photo recovery (D229 / issue126)

After Debug API build: `node --test tests/media/recovery-startup.test.cjs` (five Production hook rejections). `tests/media/recovery.browser.cjs` runs staged synthetic LocalMediaStore recovery against the local database/Mailpit; start the main worktree host with `Media__Screening__Worker=false` for this isolated suite so its temporary local object keys are not processed by the R2 worker. The suite starts its own extra API and cleans only its own synthetic local files/rows after legal hold release. Restart normal worktree hosts before photos/attachments/holds/serious/deletion regressions. Do not rebuild during either browser run. Full check.sh includes recovery scheduling and fictional loopback SMTP accepted/retry/rejected/unknown checks.

Operator/reconciliation details: [BLOCKED-MEDIA-RECOVERY.md](BLOCKED-MEDIA-RECOVERY.md). Pending/unknown preservation work is not a passing readiness condition. The member's refusal, a hold link or an email appearing in Mailpit are earlier events than the durable preservation-completed marker; photo/attachment suites wait for that marker before stopping the extra API.


### Concurrent media transitions (D230)

With worktree Debug hosts running and `Media__Screening__Worker=false`, run `SWOOVR_PLAYWRIGHT_PATH=/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright node tests/media/transitions.browser.cjs`. It starts two extra APIs with isolated synthetic local files, tests both reviewer orderings, responsive removal during processing, competing automatic workers against a delayed fake screening service, and orphan clear-copy cleanup. It releases only its fixture holds and cleans its own temporary files/rows. Never run it concurrently with another media suite or rebuild during execution. Restart ordinary R2-configured worktree hosts before integrated photo/attachment regressions. `node --test tests/media/recovery-startup.test.cjs` also rejects both publication barrier stages outside Development.

### Safety retention fixtures

`tests/retention/schedule.browser.cjs` uses fictional accounts, unique case/hold UUIDs and backdated SQL clocks to exercise actual PostgreSQL/operator retention. It also races an exclusive preservation writer against the targeted purge and checks the member finalization screen at 390px. It changes no real person's clocks. It restores its synthetic finalization marker and removes its own case/hold fixtures in `finally`. `tests/evidence/holds.browser.cjs` checks that release preserves copies, retained export works, independent no-review disposition is constrained, and linked review evidence cannot bypass its schedule.

Do not backdate arbitrary cases or release real holds to clean tests. A released hold can still require evidence and photo bytes. Use only positively identified fictional test-owned cases and targeted `retention purge --case <id>` after the fixture's applicable schedule has been advanced. Operator `accounts purge <id>` restricts finalization/media sweeping; a global purge must use the actual configured object store, never a substitute local store for R2 metadata.

`tests/retention/worker.browser.cjs` creates and drops its own `swoovr_retention_<uuid>` database, starts only an isolated API on an allocated loopback port with its content root in `src/Swoovr.Api`, and uses local synthetic files. It enables the actual retention worker only after seeding its fixtures, checks fail-closed unavailable hold status, resumes expiry, then restarts to check idempotence. It does not point the local-file store at the owner's database or contact a delivery/screening provider. The two fictional Keycloak sign-ins are local authentication evidence only. Allow roughly two minutes; there is no solution rebuild during the test.

### Actual key-configuration regression checks

`FallbackKeyChecks` in the existing Checks project references API key classes directly (permitted test-layer dependency). It uses fictional founder1/founder2 and explicit feature keys, never `.local` secret values. `FallbackKeyPersistenceChecks` uses the throwaway PostgreSQL database to check the real legacy email/mobile lookup consumers. `check.sh` includes both. The security browser suite temporarily uses its private `.local/test-rotation-keys.json` to reseal every set2 and back1 in `finally`; back up that file before changes and verify `keys status` after testing. The phone browser suite must use default reauthentication, because its flow waits61 seconds; restart with defaults for it, then the documented overrides for the other suites.


### Staff conflict continuity (D235)

`./scripts/check.sh` includes actual API-service checks in the disposable PostgreSQL database: unconfirmed then confirmed fake provider deletion; finalizing cancellation refusal; held-copy continuity; historical-link backfill; minimum-period/manual-reason preservation; police/unknown membership; expiry without resurrection; and a deterministic SQL-snapshot read/archive race. No external provider uncertainty is simulated as live evidence.

`tests/staff/access.browser.cjs` also deletes a fictional personal account through local Keycloak and checks independent access versus linked refusal before/after deletion, replacement and hold release. `tests/moderation/moderation.browser.cjs` tests historical exclusions on lower/mixed/upper-case route literals. Run the existing hold, law-enforcement, identity, deletion and isolated-worker suites after changes to shared conflict authority.

### Banned-identifier foundation (D236)

`./scripts/check.sh` includes typed HMAC/status/calendar and actual server key-ring checks plus disposable PostgreSQL registry constraints, immutable UPDATE/DELETE/TRUNCATE refusals and all-or-nothing revocation/event/case-note behavior. Test data is fictional; no shared database migration or host change is required. These checks do not prove registration/email/mobile enforcement or authorized admin workflows: run the integrated suites once those paths are wired, before calling118 complete.

### Registry enforcement (D237–D239)

`SWOOVR_PLAYWRIGHT_PATH=... node tests/enforcement/registry.browser.cjs` creates/drops an isolated `swoovr_bans_<uuid>` PostgreSQL database, starts API replicas on allocated loopback ports and uses real local Keycloak authentication for fictional adults. It does not replace the owner's hosts or mutate the owner application database. It seeds restrictions and synthetic code/hold/clock/fault fixtures directly, so it does not establish staff ban/revocation workflows or real contact ownership/delivery. Build Debug first; never rebuild during the run.

Coverage includes blocked first application registration with no mapping/profile, approved neutral/no-store messages, refusal before mobile challenge creation, late restriction at confirmation, legitimate old contacts/session retained, verified Keycloak email change refused by the application, historical-key enforcement/missing-key failure, caller quota across two replicas, address quota and key rotation, actual registration wait during a database transition, concurrent registration uniqueness, profile/case failure rollback and retry, quota hold/unknown/release expiry, and referenced case metadata with separate content expiry. Run the default-host phone suite and the normal accounts/identity/deletion/retention suites for shared resolver/job changes.


`SWOOVR_PLAYWRIGHT_PATH=... node tests/enforcement/workflows.browser.cjs` builds on the same isolated API/throwaway PostgreSQL pattern. It drives authenticated termination, reviewed release, necessity review and explicit account reinstatement; tests source/note rollback, independent restrictions, deleted-account non-resurrection, deletion/backfill non-reactivation, personal conflicts and sensitive-access audit including anonymous/exception paths. It uses the existing fictional MFA root helper and local Keycloak member administration (actual local fictional deletion), not hosted Entra or real delivery. Build Debug before starting; do not rebuild during the suite. The suite drops its DB and temporary media directory. No shared application database migration/host replacement is needed for this suite.


### Refused email-change recovery (D243–D245)

`tests/enforcement/registry.browser.cjs` additionally tests real local Keycloak email changes with only the newly issued token: adoption warning while existing account/profile/standing stay usable, strict first/profileless/identifierless admission, correction to the accepted email, quota/key failure and resumed matching, newest-first pre-registration token order, null→email acceptance, frozen legacy clocks and older-token non-rollback. Clock/key/quota/source gaps are explicit SQL fixtures; tolerated issuer-skew ordering is deterministic domain policy evidence, not a live shifted provider clock.

`SWOOVR_PLAYWRIGHT_PATH=... SWOOVR_SCREENSHOTS=<existing-directory> node tests/enforcement/recovery.browser.cjs` needs the normal phone-off/45-second hosts and Debug operator DLL. It creates a real authenticated fictional termination, deletes that fictional provider user with the targeted account command, then changes a different fictional member's verified provider email to the restricted value. It tests actual API/BFF/account/standing warnings and approved support links at desktop/390px, followed by corrected provider email and the normal web reauthentication flow. No token/session is minted by the API. Contact ownership is admin-provisioned; email delivery, native UI and hosted authentication are not established. Use `X-Swoovr-Bff: 1` for direct cookie-authenticated BFF probes, as the actual client does.

For session/contact changes run root-admin and members auth, accounts, identity, deletion, mobile parity and the default-host phone suite, plus the isolated registry/workflow suites. Keep root-MFA suites serial. Back up the private root fixture if a suite may enroll/write it, and use a fresh private temporary `SWOOVR_BROWSER_OUTPUT` instead of overwriting `.local/auth-browser` files. Build Android and unsigned iOS simulator separately before browser suites. The acceptance-clock migration test starts its throwaway DB at the previous schema, inserts fictional legacy email/null rows, upgrades and checks the clock/null/idempotent results; it never rolls back the owner database.


`tests/enforcement/workflows.browser.cjs` also covers D246: linked staff cannot open associated case detail/queue, a201-case conflicted prefix does not hide a later allowed case, the detail request actually waits for exclusive membership authority, content-free refusal auditing, continued refusal after source deletion/relinking, and reporter conflicts cannot leak a source sanction through a different case for the same subject. The synthetic bulk cases/report and advisory reader are fixture SQL in the throwaway DB, not live report/evidence verification. `tests/moderation/moderation.browser.cjs` adds retained reporter/subject read checks, mixed/encoded routes and filtered appeal text. Re-run moderation and staff access after changes; do not rebuild while either runs.


### Registry staff UI (D247–D248)

`SWOOVR_PLAYWRIGHT_PATH=... SWOOVR_SCREENSHOTS=<existing-directory> node tests/enforcement/admin-isolated.browser.cjs` needs normal phone-off/45-second Debug hosts and the local fictional MFA-root fixture. It drives actual SSR forms in Edge: grant separation, POST-only non-echoing lookup, case links, necessity review, required release confirmations, deleted-source reuse without original reinstatement, independent restrictions after explicit reinstatement, personal conflicts and Support denial. It uses fictional provider administration to establish verified contacts and targeted original-account deletion; the due clock is SQL-backdated. It proves no real contact ownership, email delivery, active-provider binding reassignment or hosted Entra. Screenshots include desktop release plus separate390px preview/form viewports (full-page screenshot after viewport resize produced a repeated-paint artifact in Edge). Run isolated workflows and staff-access regressions serially with the MFA helper; build before browser tests and never during them. Typed HTTP checks in `RegistryClientChecks` verify contracts and strict cursor parsing, not live access control.


### Scheduled registry expiry (D250)

`./scripts/check.sh` includes exact/future/indefinite/inactive expiry policy, actual PostgreSQL case-note failure rollback, two-worker convergence, system attribution, original clock retention and more than200 due rows. `tests/retention/worker.browser.cjs` also seeds a separate fictional due registry/source case: unavailable hold status leaves Active; resumed hourly coordination appends Expired event/note once across restart. These are throwaway DB/synthetic clock fixtures; no real ownership/provider delivery or production scheduler evidence. Build Debug first and do not rebuild while the browser suite runs.


### Bounded registry disposal (D251–D253)

`./scripts/check.sh` now includes actual PostgreSQL bounded-delete guards, hold/unknown membership refusal, required terminal provenance, rollback, immutable post-disposal review, separate record clocks, stale isolation refusal and a two-connection hold/delete wait. The writer locks source tuples NOWAIT while deletion waits, proving statement authority precedes tuple work; after commit the waiting delete sees the newly placed hold. Full generated migration SQL is also executable; the earlier email-clock migration gained its missing exported-SQL semicolon (no replay/change to already-applied data).

`tests/enforcement/workflows.browser.cjs` includes a consistent historical SQL fixture (fictional current authenticated account, synthetic old enforcement/events, not a claim of historical provider activity). Actual coordinated CLI runs refuse unavailable preservation, dispose once across replicas, preserve minimal source, resist backfill reconstruction, authorize explicit reinstatement from a receipt and retain its new immutable review without rewriting original closure. `historical-fixture.cjs` centralizes only temporal fixture setup.

`tests/enforcement/admin-isolated.browser.cjs` runs the full `admin.browser.cjs` journey against a disposable API/database behind the registered localhost:5082 Admin callback, including a minimized receipt and its new review at 390px. Its targeted SQL fixture uses actual bounded authority for its own IDs; the isolated workflow independently drives the full coordinator. The shared developer database may contain unresolved preservation fixtures, which must remain untouched. The isolated workflow provides that coordinator evidence. Apply migrations locally only after a private backup and Debug build; never rebuild or restart shared hosts during browser suites. New native/member UI is not part of this slice.


The registry/worker and affected shared regression suites use `tests/auth/managed-edge.cjs`: an owned macOS Edge process with ignored stdio, unique private temporary profile and ephemeral loopback DevTools port, attached with Playwright's documented [connectOverCDP](https://playwright.dev/docs/api/class-browsertype#browser-type-connect-over-cdp). CDP has lower protocol fidelity than Playwright's own transport; these suites establish only their exercised DOM/auth/API assertions. Contexts disconnect, then only the owned engine is terminated and its profile removed; personal profiles and the shared updater are untouched. This avoids a measured Edge updater/crash-helper inherited-pipe hang after assertions. Other browser suites keep their existing launcher. Require actual process exit and fixture cleanup; printed PASS before a hung finally block is not a clean suite run.

The inner `admin.browser.cjs` refuses an unpaired/shared direct invocation before opening a browser or touching data (exit2). Use the isolated entry point; it retains the registered callback and restores the normal worktree Admin launcher.


### Reviewed mobile binding retirement (D254)

Build Debug before browser execution. `tests/enforcement/workflows.browser.cjs` exercises a live fictional source binding, independent restriction release, explicit review, mismatched current binding, declared staff conflict, invalid service-credential/provider refusal, required-note transactional rollback after logout, retry and actual Keycloak session revocation. It verifies original email/termination and encrypted contact archive, idempotency, refusal to remove a later binding, and normal verification by another fictional member. The final confirmation code is a SQL-seeded hash fixture: no delivered SMS/current-ownership claim. API restarts affect only its disposable database/loopback host.

`tests/enforcement/admin-isolated.browser.cjs` covers the separate SSR action, disabled active-restriction control, server-enforced confirmation, successful retirement and disabled completed action at narrow width. Use the isolated entry and normal worktree Admin as above. Full gate includes domain event invariants and separate typed-client POST transport; provider/session/contact regressions follow the session/contact matrix. Do not rebuild during browser suites; preserve private runtime fixtures/logs and restore main hosts afterward.

D254 also records a monotonic `SessionsRevokedAt` watermark atomically with retirement: old self-contained JWTs remain refused after any later independent reinstatement. Mark only that source contact's pending phone challenges used, preserving their retained history and unrelated challenges. New authentication must be later than the cutoff; Keycloak auth_time has whole-second resolution. The integrated regression checks note-failure rollback of this watermark, old-token refusal before/after separately reviewed recovery and a fresh sign-in using preserved credentials.

Revocation clock DEFAULT: the retiring original subject's cutoff includes the shared30-second admitted issuer skew, so a recently issued token from an issuer ahead of the API cannot survive retirement. Revoked-account acceptance requires both authenticated-at and issued-at later than the cutoff; missing/invalid/excessively future issuance fails closed. Original-account recovery sign-in may need to wait through that brief cutoff. This does not delay the legitimate new owner's registration/verification. Exact skew and malformed-clock cases are deterministic policy evidence; no real Keycloak clock shift is claimed.


### Keycloak contact policy component (D255; not main-realm installation)

Use an explicit Java21+ JDK and public runtime libraries from pinned26.8.1. Do not export realm/database data. `python3 scripts/check_keycloak_contact_policy.py --java-home /path/to/jdk --keycloak-libs /private/public-libs` compiles toJava21 in private temporary space and runs15 fictional unit checks. `--output-jar /new/path/provider.jar` exports public code only and refuses overwrites. The internal JPA getEntities interface is marked for removal; its compiler warning is explicit and is a compatibility limitation, not a clean-warning claim.

`SWOOVR_KEYCLOAK_JAVA_HOME=/path/to/jdk SWOOVR_KEYCLOAK_LIBS=/private/public-libs node tests/enforcement/keycloak-contact.integration.cjs` builds first, then creates a separate Keycloak container/loopback port and disposable `swoovr_kc_retire_<uuid>` PostgreSQL DB. It never restarts/imports the main provider. Only local Postgres credential is read, never printed or changed; bootstrap/client passwords are random process-memory fixtures. Private logs/state are retained outside the repo; owned container/DB cleanup must succeed and actual process exit is required. The underlying locally available26.8.1 image is reused; a different explicit image must pass the version guard.

The suite tests real provider service authorization/date protocol/CAS/cache/session effects, idempotent and competing requests, independent stale SQL refusal, unavailable guard and inconsistent receipt refusal, unrelated login/password/linked-record preservation and freed-mailbox new-subject creation. A public password-grant client and disabled fictional OIDC metadata exist only inside this disposable protocol fixture; product clients remain PKCE. It does not prove real Apple/Google/Entra login, browser registration/mailbox ownership/delivery, application pending-state integration, production clustering or deployment. No application/UI browser suite is needed for this component-only artifact; run relevant staff/member suites when wiring the API/UI. Never overlap provider compilation with running browser suites.

D256 typed transport probe: build `dotnet build tests/Swoovr.Checks --no-restore` first, then add `SWOOVR_TEST_CONTACT_ADAPTER=1` to the isolated component command above. The runner supplies only its owned loopback realm and ephemeral service secret to a7-check .NET probe, then verifies the committed marker in its owned database. It refuses hosted/nonloopback realms. Current combined result:93 provider/PostgreSQL checks plus7 typed-adapter and15 Java checks.16 additional fake-HTTP checks run through check.sh. No product browser/ownership/delivery/native proof is implied. Do not enable the feature on main merely to run these checks.

### Durable email backend (D257, disabled pending staff integration)

`check.sh` includes9 state checks and actual owned PostgreSQL orchestration with fake provider outcomes. The dedicated email DB creates/drops its own name; fixtures test post-provider/pre-API failure, deferred completion rollback, revocation and late restrictions, token-fact refusal, bound ciphertext/key rotation and201-command held-prefix expiry. These are not genuine JWT/ownership/provider-browser proofs. The real typed transport probe remains D256's separate provider suite.

For the changed staff writers, run `tests/staff/access.browser.cjs` on built worktree hosts:86 checks include an owned PostgreSQL preservation transaction and a verified target lifecycle waiter; roles/grants must remain until authority releases. The probe uses fictional staff and always closes its own transaction. Use the complete session/contact suite set above, with `tests/accounts/deletion.browser.cjs` (not a deletion/ directory), isolated registry/workflow/Admin, moderation and default-gate phone; build before, never during browser runs. Native builds establish compilation only. Restore main afterwards and confirm the shared retirement table is empty before returning an older main binary.

`MemberAdmin:ContactRetirementEnabled` stays false by default. No current runbook command enables it for testers: finish SSR/reviewed resume, application/provider journey, narrower remote-call locking and compatible-release/rollback checks first. Do not manually mark an uncertain operation Completed or reattach a recycled contact with database/ordinary provider edits. Pending work is durable and restricted; known provider confirmation is the processor's authority.

### Isolated staff approval/availability regressions (D258)

Build Debug first, then run `SWOOVR_TEST_STAFF_ACCESS=1 SWOOVR_PLAYWRIGHT_PATH=... node tests/enforcement/admin-isolated.browser.cjs` for the91-check staff suite. The existing paired loopback/owned-database validation still applies; bootstrap the fictional root's application flag only inside the owned DB. This mode covers actual root revocation of another staff owner's grant waiting on that owner's lifecycle authority, plus the existing preservation-before-bulk probe and staff SSR. No hosted Entra/ownership/delivery claim. Omit the selector for the standard53-check registry UI suite; other selector values are refused.

The runner validates all Admin listener cwd paths before any signal and accepts only this worktree or main positively derived from the shared .local symlink. It restores that same checkout, preserving actual logs first; API/Web hosts stay running. Mixed/unrelated listeners are refused. This removes the need to replace all main hosts for these staff regressions. Root MFA remains serial; never restore its counter backwards. Run isolated workflows and registry as well. check.sh now includes the paused-provider actual-PostgreSQL availability/authority checks; no schema or native/client behavior changed in D258.

### Owned staff email-retirement journey (D259)

Build Debug and native targets before browser execution. Add `SWOOVR_TEST_CONTACT_WORKFLOW=1 SWOOVR_TEST_CONTACT_ADAPTER=1` to the isolated Keycloak component command above; optionally set `SWOOVR_SCREENSHOTS` to an evidence directory. The workflow refuses direct/shared-provider invocation, imports actual member/staff configurations with ephemeral credentials into its owned provider, uses actual theme and authorization-code/PKCE exchanges, and starts an owned API/database. It replaces only a positively identified main/worktree Admin at5082 and restores that original checkout/logs. API/Web stay running. Mailpit catches fictional example.test verification links; this is sandbox link consumption, not real delivery/ownership, hosted Entra or native UI.

The47-check journey exercises member/Support denial, separate elevated approval/revocation, pending refusal, withdrawn grant preventing dispatch, immutable fresh review resuming the same command, real provider completion, preserved original credentials/profile/termination, independent recovery refusal, and distinct new registration/profile after released-email verification. Provider93 and typed-adapter7 checks report separately. For scheduling use `MemberAdmin:ContactRetirementWorker=false` and `contacts reconcile`; never infer approval from operator execution. Main feature stays off. Run the registry/workflow/staff/Admin regressions serially, and the session/contact matrix above for shared contact changes.

### Operational group expiry / legacy import gate (D260)

Full check.sh includes an owned `swoovr_operational_<uuid>` PostgreSQL database: missing/overlapping/unknown hold status, late export clocks, transaction rollback after child deletion, complete-group disposal/idempotence, seven-year police metadata extension and subsequent actual police-source expiry, and read-only legacy counts/unchanged bytes/no invented restrictions. It creates/drops only its own DB. No provider delivery or ownership evidence. `bans legacy-status` is operator-only, counts-only, and exits1 while unresolved historic markers remain.

After Debug build, run isolated `tests/retention/worker.browser.cjs`; on normal phone-off/45-second worktree hosts run schedule, holds, law-enforcement and deletion suites above, serially. The law-enforcement fixture now expects a recent export/receipt/custody group to retain source provenance despite a backdated case clock; owned PostgreSQL separately proves eventual expiry after the full operational window. No new UI/native behavior or screenshot requirement. Restore main after shared-host testing.
