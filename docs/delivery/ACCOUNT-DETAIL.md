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
- **Refusals keep their reason.** The badge override, adult override, Staff badge, sign out everywhere and confirm arrival calls returned only the HTTP status, so the API's `403 stale-session` (badge overrides need a sign-in within the last 15 minutes) showed as "Your role can't do this." with no way forward. They now return the problem code, so the page says the action needs a fresh sign-in and shows *Sign in again*. That link signs in again at Keycloak (`prompt=login`, `max_age=0`) and returns to the same tab with the same dialog open (`?resume=<dialog>`), so the action is one more click. On success the badge, source (Staff override, Manual) and date update on the same page. The 15-minute rule itself is unchanged.

## Not yet built (tracked in #191)

- Selecting message ranges or whole threads into an evidence package and referring it to Trust & Safety or Legal (issue 190 §11). Today, conversations open through their case.
- Warning acknowledgement status in the enforcement history; a list of active sessions.
- Staff edits of email, mobile and names beyond the existing member and name-review flows (issue 190 §9).
- Native app locked-screen check on a device (shared text is wired, not run on a simulator).
- Deferred by the owner (focus is the admin and member web apps): the native restriction banner and tailored member wording when a paused feature is used, in [#197](https://github.com/repasscloud/wanderconnect/issues/197).
