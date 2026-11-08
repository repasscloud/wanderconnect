# Terms step links and guided first sign-in

Issues: https://github.com/repasscloud/wanderconnect/issues/253 and https://github.com/repasscloud/wanderconnect/issues/254. Decision: D342 (owner direction CONFIRMED in #254; builder details DEFAULT).

## What was wrong

- **#253.** A beta member who resets their password meets Keycloak's `TERMS_AND_CONDITIONS` required action. The theme had no `terms.ftl`, so Keycloak rendered the plain `termsText` message: "Terms of Use", "Privacy Notice" and "Community Guidelines" were words, not links.
- **#254.** After that, the member landed on Account settings, where the setup steps were mixed into the normal settings cards ("Step 1 of 2 · Verify your mobile number" above the *name* form). The mobile row said "Loading…" forever, and no field to enter a number ever appeared. Root cause: `GET /api/me/phone` is exempt from the phone gate but not from the registered-names gate, so before the name was confirmed it answered 403; `PhonePanel` kept its view null (shown as "Loading…" with no form) and never reloaded after the name was saved.

## What changed

- `containers/keycloak/themes/swoovr/login/terms.ftl` (copied from keycloak.v2 26.8.1 and changed) renders the adults-only line and the agreement sentence with the three policy links, using the same messages and `theme.properties` addresses as registration. `PublicPolicyChecks` asserts all three links and the accept action.
- `GET /api/me` returns `setupStep` (`AccountSetup.Next`): names → phone → policies → dateOfBirth. API gates are unchanged.
- `GET /api/me/contact` returns the member's own email and mobile masked (`ContactMask.Email`, `PhoneVerificationPolicy.Masked`), or in full with `?reveal=true`.
- The web gate redirects unfinished setup (names, phone, date of birth) to `/welcome` from every signed-in page except account sub-routes, notices, support, safety, appearance and the public policies. Signing out and back in lands on the same step.
- `/welcome` (`OnboardingFlow.razor`): one screen per step, progress bar, no tab bar, sign out and contact support below.
- Account settings: setup forms removed (a "Continue setup" card shows if the page loads before setup is done); masked email and mobile with hover/click reveal (`MaskedContact.razor`); the mobile row shows "Not added yet" + "Add" instead of a stuck "Loading…".
- One refusal for numbers in use or banned: "This number cannot be used. Contact support."

## Evidence

Recorded 8 November 2026 on branch `fix/253-254-onboarding-flow`:

- `dotnet build` (Release) of `Swoovr.Api`, `Swoovr.Web` (with `Swoovr.Web.Client`) and `tests/Swoovr.Checks`: succeeded, 0 warnings. The full `Swoovr.sln` build fails only on the `net10.0-ios` target because the local Xcode is 27.2 and .NET for iOS 27.1.10018 requires Xcode 27.1 (environment, unrelated to this change).
- `./scripts/check.sh`: exit 0. Includes `PASS: mobile number verification (95)` (with the new onboarding checks), `PASS: account standing (33 checks)`, `PASS: approved public policy checks` (with the new terms-step link check), design, naming and realm checks, and the HTTP smoke suites. PostgreSQL persistence checks were skipped (`SWOOVR_TEST_DATABASE` not set).
- Layout: the `/welcome` cards (name, mobile with a refusal shown, code) and the masked Contact information rows were rendered with the real `themes.css`, `app.css` and `member-settings.css` (dark Standard theme) at 1280 px and 390 px in Edge: no horizontal overflow at either width. That harness was hand-written HTML matching the components, so the compiled `Swoovr.Web.Client.dll` was also searched for leaked Razor code text (none found).
- New checks in `PhoneChecks`: setup order, contract and domain step names, email masking (owner example `alex.original@example.test` → `a...l@e...e.test`), country code composition, and the shared refusal text.

## Gaps

- Browser suites `tests/auth/members.browser.cjs`, `tests/accounts/onboarding.browser.cjs` and `tests/accounts/phone.browser.cjs` were updated for `/welcome` but not run in this change; they need the owned local platform.
- The Keycloak `terms.ftl` page has not been rendered against a running realm in this change; the next local platform restart picks it up (themes are not cached in start-dev).
- MAUI keeps its existing setup screen (web first).
