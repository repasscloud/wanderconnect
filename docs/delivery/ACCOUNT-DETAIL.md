# Admin account detail and staff roles

Issue [#190](https://github.com/repasscloud/wanderconnect/issues/190) (requirements), tracking [#191](https://github.com/repasscloud/wanderconnect/issues/191). Decisions D322 (roles, CONFIRMED), D323 (structure, DEFAULT), D324 (Q131 A, CONFIRMED) and D325 (Q132 B, CONFIRMED). Answered: Q131 A [#192](https://github.com/repasscloud/wanderconnect/issues/192) (D324) and Q132 B [#194](https://github.com/repasscloud/wanderconnect/issues/194) (D325, supersedes D160). The owner-approved mockup is local only (`.local/mockups/account-detail-190.html`, fictional data).

## Roles (D322)

| Role | Moderation | Other powers |
| --- | --- | --- |
| Support | Warn; restrict up to 30 days; lift Support's own actions | Account read views, date of birth with a purpose |
| Trust & Safety | Everything Support does; restrict or suspend up to 90 days; lift Support and Trust & Safety actions | Sign-in locks (not legal locks), sign out everywhere |
| Admin | Everything; only role that terminates, decides appeals, revokes staff affiliation and works staff-affiliated cases | Verification overrides, complimentary access, Staff badge, legal locks |
| Legal | No member sanctions | Legal holds, identity, disclosures (D189); legal sign-in locks |

`AccountRole.TrustSafety` is stored as text like the other roles, so no role migration was needed. `AccountResolver` treats Trust & Safety as also holding Support, so every Support route accepts it. `StaffRole` resolves Admin, then Trust & Safety, then Support. Roles come only from API records (invitation or `dotnet Swoovr.Api.dll staff-role grant|revoke <subject> support|trust-safety|admin`). Keycloak authenticates and never grants a role (Q131).

## Account detail (D323)

`/accounts/{id}` and `/accounts/{id}/{section}`. The old section links were `#fragments`, which resolved against `<base href="/">` and sent staff to the home page. Each tab is now a real address, so direct links, refresh and back work, and an unknown section shows a "doesn't exist" notice rather than redirecting.

- **Overview:** registered name, masked date of birth, age, display name, verified identity on file, masked email and mobile (`GET /contact`), joined and last active, location and trips, registration. **View date of birth** records a purpose, takes `profile-dob-open` access if not already held, then shows the date (issue 190 §2: before this, the access had to be requested on another page first, and the result appeared inside a collapsed section). Admins also get the D299 one-time correction row.
- **Verification:** ID Verified and Photo Verified with source (provider, or Staff override marked Manual), apply or revoke with set reasons (`POST /verification-override`, Admin, fresh sign-in), override history, Didit attempts, age assurance, and the Staff badge as a switch with a set reason.
- **Media:** one always-rendered inventory form carries the viewing purpose, so blurred preview and reveal work on the first submit (issue 190 §5: the old reveal form existed only after an earlier POST, so static rendering never dispatched it). The API still requires the media safety acknowledgement and records each view. The opened photo shows inside its own card (blurred or revealed), and a click on it opens a larger dialog (issue 199: it used to open in a panel below the grid).
- **Membership:** effective plan, paid subscription untouched, set grant and revoke reasons, optional internal note.
- **Restrictions (D325):** limit only the ticked features (discovery, new requests, messages, photo uploads; nothing ticked means all four). Each feature refuses with 403 `restricted`; the member keeps the rest of the app and sees what is paused on their account page.
- **Enforcement:** actions limited by role, set reason code, member message, internal note, duration capped by role. Termination takes its `termination` access inline and needs typed confirmation. History shows reason, case, role, dates and state; lifting needs a set reason.
- **Sign-in & access:** lock with a set reason, optional `SW-` reference (a new one otherwise), expiry and internal note. The API member gate refuses every protected request while a lock is active (`403 account-locked` with `reference`), whatever session or token the member holds, and the lock ends the member's Keycloak sessions. `/api/me` reports `accountState: "locked"` and `statusReference`; the web shows "Your account is temporarily locked" with the reference and support link, and the app uses the same shared text. Locks are never deleted.
- **Conversations:** cases for this member, each opening the existing case page with its evidence and access checks.
- **Audit:** the account's audit history, including the new actions.

New data (migration `AccountDetailControls`, tables and nullable or defaulted columns only): `account_locks`, `verification_overrides`, `member_profiles.photo_manual/id_manual`, `sanctions.reason_code/internal_note`, `entitlements.note/revoked_reason`.

## Owner review fixes (issue 199, 4 November 2026)

- **Full button labels.** Account detail buttons no longer end in "…", which read as cut off: *View date of birth*, *Lift wait*, *Clear review*, *Confirm arrival*, *Apply badge* / *Revoke badge*, *Record adult override*, *Lift sanction*, *Revoke grant*, *Remove lock*, *Sign out everywhere*, *Allow correction*. Each still opens its confirmation dialog.
- **Refusals keep their reason.** The badge override, adult override, Staff badge, sign out everywhere and confirm arrival calls returned only the HTTP status, so the API's `403 stale-session` (badge overrides need a sign-in within the last 15 minutes) showed as "Your role can't do this." with no way forward. They now return the problem code, so the page says "For your security, this action needs a recent sign-in." with a separate *Sign in again* button (the same notice-and-button layout on Case, Content review, Holds, Home and Cases). That button signs in again at Keycloak (`prompt=login`, `max_age=0`) and returns to the same tab with the same dialog open (`?resume=<dialog>`), so the action is one more click. On success the badge, source (Staff override, Manual) and date update on the same page. The 15-minute rule itself is unchanged.

## Not yet built (tracked in #191)

- Selecting message ranges or whole threads into an evidence package and referring it to Trust & Safety or Legal (issue 190 §11). Today, conversations open through their case.
- Warning acknowledgement status in the enforcement history; a list of active sessions.
- Staff edits of email, mobile and names beyond the existing member and name-review flows (issue 190 §9).
- Native app locked-screen check on a device (shared text is wired, not run on a simulator).
- Deferred by the owner (focus is the admin and member web apps): the native restriction banner and tailored member wording when a paused feature is used, in [#197](https://github.com/repasscloud/wanderconnect/issues/197).

## Retired sessions (D339, 7 November 2026)

- **Sign out everywhere closes live messaging (#198).** Open messaging connections from before the cutoff now close at once on
  the API instance that handled the action. A 15-second sweep closes the rest (other instances, contact retirement, restores).
  A sign-in lock closes all of the member's connections.
- **A locked member sees the locked screen again (#201).** After a lock retires the member's tokens, the web ends its own session
  on the first refused call and sends the member to sign in again; after signing in they see "Your account is temporarily
  locked" with the same `SW-` reference. `tests/admin/account-detail.platform.cjs` now requires this (it used to accept either).
- Evidence: `SessionRetirementChecks` (registry, cutoff and redirect rules; sweep against disposable PostgreSQL). The managed
  platform run of `account-detail.platform.cjs` and a live two-device hub check are listed as not yet run in the PR.

### Browser messaging across token expiry (#240, 8 November 2026)

The five-minute closure comes from PR #239 (`ac20fe6`, D341), which enabled
`CloseOnAuthenticationExpiration` to bound messaging access for an ended Keycloak session on any API instance.
It is separate from D339's 15-second account-revocation sweep. The closure remains required; it is not a new idle timeout.

The real browser regression was the relay's `Context.Abort()`: it sent a terminal SignalR close, so
`WithAutomaticReconnect()` never reconnected. On base `a098b02`, two fictional adult Edge chat pages closed
at 298–299 seconds and neither reopened through a 370-second idle observation. They had exchanged messages in
both directions before the closure. Initial fixture/host setup and cleanup used the owned browser runner.
The first run's Edge profile cleanup raised `ENOTEMPTY` and masked the assertion in the suite output; the enclosing
runner still removed its private runtime directory and reported no resource cleanup errors. The recorded socket
lifecycle independently establishes the failed reconnect.

DEFAULT implementation correction: the upstream SignalR client's `Reconnecting` event preserves the API's
reconnectable close through `IConnectionLifetimeNotificationFeature.RequestClose()`. A terminal upstream
`Closed` event still aborts the browser, preventing retries after revocation or an API account refusal. A 30-second
upstream retry delay lets the browser reauthenticate first; browser disconnect disposes the old upstream and
cancels that retry. The relay registers before starting so a rejection immediately after the protocol handshake
cannot be missed. Missing transport support, refused authentication/origin and failed initial upstream connections
still abort. Token expiry, per-session closure, account cutoffs, locks and the sweep are unchanged. No client token
is exposed, no session is extended, and the existing history reconciliation runs after reconnect.

The focused `messaging-lifetime` owned suite opens actual chat pages, waits 370 seconds without navigation or
polling, checks bidirectional send/receive before and after expiry and history without duplicates, then retires
one account's sessions through the staff API. It requires prompt socket closure, no further received push and
401 responses to attempts to send or read using the old browser session, with no post-revocation reconnect loop.
The staff action uses actual Admin sign-in and fresh same-session MFA elevation. Lifecycle output contains timestamps,
browser ordinal and close/reconnect metadata only. See the [owned runner](Q93-OWNED-BROWSER-RUNNER-2026-11-02.md)
for isolation and execution instructions. Hosted, multi-instance and physical-device acceptance remain separate.

Final local acceptance (8 November, Adelaide): owned run `8fce5b41999643759c48ae9535c84185`, exit 0.
Both browser sockets closed at their original token expiry and reopened automatically in 83 ms and 59 ms.
The 370-second observation, bidirectional delivery before/after and four-message history without duplicates passed.
After real Admin MFA elevation and Sign out everywhere, the target socket closed in 214 ms with
`allowReconnect: false`; no replacement socket opened, no further push appeared, and old-session send/read
returned 401. Cleanup succeeded, and independent checks found zero run-owned users, cloned clients, databases
or listeners. Private lifecycle evidence is under `.local/owned-browser-runs/8fce5b41999643759c48ae9535c84185/`.

`./scripts/check.sh` passed (exit 0), including 765 PostgreSQL persistence cases, 14 session-retirement checks,
38 sign-in checks, all HTTP smoke stages and Release build with zero warnings/errors. Its automatic selection
of the older `.local/docker.env` initially failed database authentication; the successful check explicitly used
the managed stack's credentials in `SWOOVR_TEST_DATABASE` (the suite creates its own throwaway database).
Independent review found no remaining actionable issues. Sending specifically during the reconnect gap,
hosted/multi-instance behavior and native physical devices were not exercised.
