# Issue intake

Use this file to investigate reported items and create GitHub issues.
Do not implement fixes unless separately requested.

Processing refresh: 2 November 2026. Items 2–5 and 7–17 retain Q95–Q109 / GitHub #137–#151; PRs #152–#154 are now merged. Items 18–25 are processed as Q110–Q120 / GitHub #155–#165, with item 20 styling reusing Q98/#140. Every substantive report has a disposition; item 1 remains a template and item 6 was not supplied. See the [current investigation and scope map](ISSUE-INTAKE-2026-11-02.md) and historical [evidence publication review](evidence/2026-11-01-issue-intake/README.md). Original “issue #N” references mean intake item numbers unless a GitHub URL is supplied. Original reports and proposed checklists remain feedback; CONFIRMED policy is unchanged and OPEN conflicts are identified in the investigation notes.

## 1. Short, descriptive title

- Status: Template only — not a report
- Area: Member web / Admin / API / Docker / Mobile
- Page or URL:
- Type: Bug / Improvement / Question
- Priority: High / Medium / Low
- GitHub issue: Not created
- Tracking Q: Not assigned

### What happened

Describe the behaviour you observed.

### What I expected

Describe what should happen instead, or say “Needs investigation”.

### Steps to reproduce

1.
2.
3.

### Evidence

Paste relevant error messages or link screenshots.
Do not include passwords, tokens or real personal information.

### Impact

Who is affected, and what does it prevent?

### Notes or constraints

Optional context, suggested explanations or requirements.

<a id="intake-2"></a>

## 2. Admin account search fails to find existing users by email address

Implementation progress (2 November 2026): the false no-match on API refusal is included in merged [PR #152](https://github.com/repasscloud/wanderconnect/pull/152). Actual Edge regression failed before and passed after the fix: denied staff lookup, exact/normalised email, an account without a profile and genuine no-match. Full `./scripts/check.sh` passed with 596 PostgreSQL cases and three smoke suites. Original private account linkage is unverified; no sync defect or provider search is claimed. See [handoff](ISSUE-FIX-HANDOFF.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/accounts?email=fictional.alex%40example.test`
- **Type:** Bug
- **Priority:** High
- **GitHub issue:** [#137](https://github.com/repasscloud/wanderconnect/issues/137)
- **Tracking Q:** Q95

Current disposition (2 November 2026): the owner confirmed successful lookup in [#137](https://github.com/repasscloud/wanderconnect/issues/137) and requested resolution on a next PR; this task leaves the issue open. The original cause is unproven. New phone/browse scope is [Q110 / #155](https://github.com/repasscloud/wanderconnect/issues/155).

### Historical intake investigation — pre-merge source snapshot

Q95: Accounts.razor discards unsuccessful lookup status with Value ?? [], so an API refusal can appear as no match. The API normalises email, queries application Accounts and left-joins optional MemberProfiles; it does not search Keycloak directly. Neither missing application records nor provider/database mismatch is established. Capture status/code and canonical subject/account linkage with a fictional fixture before diagnosing.

Source inspection: `src/Swoovr.Admin/Components/Pages/Accounts.razor; src/Swoovr.Api/Accounts/AdminEndpoints.cs:195`.

Duplicate review: Related #88 (staff permissions), #134 (auth harness); neither covers this lookup failure.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The Admin application fails to locate an existing member account when searching by its registered email address.

The account is confirmed to exist, and the member can successfully sign in to Swoovr using the same email address. The email address has also been verified.

However, searching for that exact email address in the Admin application's Accounts section returns:

> No account with that email.

This incorrectly indicates that the account does not exist.

### What I expected

When an administrator searches for an existing member using their registered email address, the application should:

1. Find the matching member account.
2. Display the relevant account details in the search results.
3. Allow the administrator to open the account in the Admin application.
4. Provide access to the appropriate account management, verification, standing and review functions according to the administrator's permissions.

Searching using a valid, registered email address should reliably return the corresponding account, regardless of whether the member has completed optional profile setup.

### Steps to reproduce

1. Sign in to the Swoovr Admin application using an authorised administrator account.
2. Navigate to `/accounts`.
3. Enter the registered email address of an existing, verified Swoovr member.
4. Submit the search.
5. Observe the message: "No account with that email."
6. Confirm that the member account exists by successfully signing in to the member application using the same email address.

### Evidence

![Admin account search returning no results](evidence/2026-11-01-issue-intake/02-cannot-search-for-account-by-email-address.png)

### Impact

**Affected users:** Swoovr administrators and authorised support staff.

Administrators cannot reliably locate member accounts using their registered email addresses.

This prevents or delays:

- Account investigations and support requests.
- Identity and verification reviews.
- Account standing and restriction reviews.
- Trust and Safety investigations.
- Member account recovery assistance.
- General account administration.

This is a **high-priority bug** because account lookup is a fundamental administrative capability, particularly for support and Trust and Safety operations.

### Notes or constraints

Investigate the account search implementation across the Admin frontend, API and underlying identity/account storage.

Areas to verify:

- Whether the search queries the correct member account data source.
- Whether registered email addresses are correctly associated with member account records.
- Whether the search relies on a separate account table, projection or synchronisation process that may not contain the member.
- Whether email lookup is affected by case sensitivity, whitespace or normalisation.
- Whether URL-encoded email addresses are correctly decoded and handled.
- Whether verified accounts are incorrectly excluded by account status or other filters.
- Whether the Admin application is querying the same environment and database as the member application.
- Whether identity-provider user records and Swoovr member records are correctly linked.

Do not assume the cause is a database or synchronisation issue without investigation.

Email lookup should use the canonical identity/account relationship and must respect existing administrator permissions and audit requirements.

### Acceptance criteria

- [ ] Searching by the exact registered email address returns the correct member account.
- [ ] Email lookup handles case differences and surrounding whitespace appropriately.
- [ ] Email addresses containing valid special characters are handled correctly.
- [ ] Verified member accounts are searchable.
- [ ] Accounts with incomplete optional profile information remain searchable.
- [ ] Administrators can open matching accounts and access functions permitted by their role.
- [ ] Search results do not expose accounts outside the administrator's authorised scope.
- [ ] The underlying cause is identified and corrected rather than bypassed with a frontend workaround.
- [ ] Appropriate automated tests cover existing-account lookup and genuine no-match scenarios.
- [ ] The "No account with that email" message is displayed only when no matching account exists within the administrator's authorised search scope.

<a id="intake-3"></a>

## 3. Admin session incorrectly reports expiration and displays inconsistent, unbranded authentication pages

Implementation progress (2 November 2026): [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) adds staff token renewal and a branded anonymous signed-out page. Reproduced expired-token/cookie mismatch with an owned temporary provider client; actual Edge verified renewal and reauthentication after owned-provider-session revocation. Ten unit checks cover concurrency, absolute deadline, unchanged authentication evidence, refusal/malformed expiry and late renewal after logout. The review-discovered logout race is fixed. Final full `./scripts/check.sh` passed, including 596 PostgreSQL cases and all three smoke suites. Hosted/multi-instance and original private-session timing remain unverified; no security gates were relaxed. See [handoff](ISSUE-FIX-HANDOFF.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/media` (and other Admin pages)
- **Type:** Bug
- **Priority:** High
- **GitHub issue:** [#138](https://github.com/repasscloud/wanderconnect/issues/138)
- **Tracking Q:** Q96

### Historical intake investigation — pre-merge source snapshot

Q96: AdminText maps any API 401 to session-ended; StaffTokenHandler forwards the stored access token. The signed-out endpoint returns minimal raw HTML. The header can reflect the cookie while the API refuses a token; expiration/renewal failure remains a hypothesis, not a reproduced cause. Screenshots show date-of-birth and media routes with different message treatment.

Source inspection: `src/Swoovr.Admin/StaffSession.cs; src/Swoovr.Admin/Program.cs; src/Swoovr.Admin/Components/Pages/MediaReview.razor`.

Duplicate review: Related #88 and #135; neither investigates the cookie/API lifecycle or signed-out presentation.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

When navigating between pages in the Swoovr Admin application, an authentication-related message intermittently appears:

> Your session has ended. Sign in again.

However, the administrator does not appear to have been completely signed out.

The authenticated user's name remains visible in the top-right corner of the application, suggesting an active session.

The affected page displays two options:

- **Sign in again** — a text hyperlink.
- **Sign out** — a button.

This creates a confusing and contradictory state where the application simultaneously suggests that the session has expired while still displaying the administrator as signed in.

**Observed behaviour**

1. While navigating through the Admin application, the session-ended message appears unexpectedly.
2. The administrator's name remains visible in the navigation header.
3. The page offers both "Sign in again" and "Sign out".
4. Clicking "Sign out" navigates to a plain, text-based page displaying:

   > You're signed out. Sign in again.

5. This page does not use the Swoovr design system, layout or branding.
6. Clicking the "Sign in again" hyperlink initiates authentication and returns the administrator to the application.
7. The administrator can then resume normal work.

The session-ended message also appears differently depending on which Admin page is being viewed.

Two examples have been captured, demonstrating inconsistent presentation.

**Additional concerns**

- The application does not clearly distinguish between an expired session, an authentication failure and a deliberate sign-out.
- It is unclear whether authentication credentials remain valid and are being silently reused during sign-in.
- It is unclear whether the issue is related to expired access tokens, authentication cookies, session state, token renewal or another authentication mechanism.
- The current recovery process requires unnecessary navigation and multiple actions.
- The authentication and sign-out pages do not provide a consistent Swoovr-branded experience.

### What I expected

The Admin application should maintain a reliable and consistent authentication state throughout navigation.

An administrator should either be authenticated and able to use the application, or unauthenticated and directed through an appropriate sign-in flow.

The interface must not display contradictory authentication states.

#### A. Correct authentication and session handling

When the administrator has a valid authenticated session:

- Navigation between Admin pages should work normally.
- The application should not unexpectedly display session-expired messages.
- Access tokens should be renewed when appropriate, using the configured authentication mechanism.
- The application should correctly handle token expiration and session renewal.
- Authentication state should remain consistent between the frontend, API and identity provider.

Do not require the administrator to sign out and sign back in simply to recover from a temporary authentication state mismatch.

#### B. Handle genuinely expired sessions correctly

If the session has genuinely expired and cannot be renewed:

1. Display a consistent, Swoovr-branded session-expired screen.
2. Clearly explain that the administrator must authenticate again.
3. Provide a prominent **Sign in again** action.
4. Preserve the intended destination where safe and appropriate.
5. Return the administrator to the requested Admin page after successful authentication.

The expired session must not continue to provide access to protected resources.

Do not display the administrator as actively signed in when the application has determined that authentication is no longer valid.

#### C. Separate session expiration from deliberate sign-out

These are different events and should not share confusing or contradictory messaging.

**Session expired**

Message:

"Your session has expired."

Supporting text:

"For security, please sign in again to continue."

Action:

**Sign in again**

**Signed out**

Message:

"You've signed out of Swoovr Admin."

Supporting text:

"Your session has ended."

Action:

**Sign in**

Both states should use Swoovr's standard design system and provide clear navigation.

Do not display a Sign out button on a session-expired page unless there is a specific, technically valid reason to do so.

#### D. Ensure sign-out behaves consistently

When an administrator deliberately selects Sign out:

- Clear the appropriate application authentication state.
- Complete the configured identity-provider logout flow where required.
- Invalidate or clear local authentication cookies and tokens as appropriate.
- Redirect to a properly designed Swoovr Admin signed-out page or sign-in experience.
- Do not leave protected Admin content accessible through stale application state.

If the identity provider still has an active single sign-on session, clicking Sign in may authenticate the administrator again without requiring credentials.

That behaviour may be valid, depending on the configured authentication and logout policy.

However, the application should not present it as an authentication error, and sign-out behaviour must follow the intended security policy.

#### E. Standardise authentication-related pages

All Admin authentication states should use the same visual design and interaction patterns.

This includes:

- Session expired.
- Sign-in required.
- Signed out.
- Authentication failed.
- Access denied.

These pages should:

- Use Swoovr branding.
- Use consistent typography and spacing.
- Provide clear, concise explanations.
- Offer appropriate recovery actions.
- Avoid raw text-only pages.
- Render consistently regardless of which Admin route triggered the state.

Prefer reusable authentication-state components rather than separate inconsistent implementations across individual pages.

#### F. Avoid unnecessary authentication interruptions

If a valid identity-provider session exists and the application can securely renew its local authentication state, investigate whether renewal can occur without disrupting the administrator's workflow.

A temporary expired API access token should not automatically result in an interactive sign-out process if secure renewal is supported.

However, this must not bypass:

- Explicit session expiration.
- Administrator account suspension or revocation.
- Security-required reauthentication.
- Logout actions.
- Multi-factor authentication requirements.
- Configured session lifetime policies.

Security controls must remain authoritative.

### Steps to reproduce

1. Sign in to the Swoovr Admin application using an authorised administrator account.
2. Navigate between Admin pages, including `/media`.
3. Continue navigating until the intermittent message appears:

   "Your session has ended. Sign in again."

4. Observe that the administrator's name remains visible in the top-right navigation.
5. Observe the simultaneous "Sign in again" and "Sign out" actions.
6. Click Sign out.
7. Observe the unbranded, text-only signed-out page.
8. Click the Sign in again hyperlink.
9. Observe that authentication completes and the Admin application becomes accessible again.
10. Repeat the process from different Admin pages and compare the presentation of the session-ended message.

**Note:** The issue is intermittent. Investigation should include session lifetime, navigation patterns and authentication renewal behaviour.

### Evidence

![Admin session-ended message — example 1](evidence/2026-11-01-issue-intake/03-session-ended-01.png)

![Admin session-ended message — example 2](evidence/2026-11-01-issue-intake/03-session-ended-02.png)

The screenshots demonstrate inconsistent session-ended presentation across Admin pages.

### Impact

**Affected users:** Swoovr administrators, support staff and Trust and Safety personnel.

**Impact:**

- Administrators experience unexpected interruptions while navigating the application.
- Authentication state becomes unclear and potentially misleading.
- Additional sign-out and sign-in steps create unnecessary workflow friction.
- Administrative investigations and moderation tasks may be interrupted.
- Inconsistent authentication messaging reduces confidence in the Admin application's reliability.
- Unbranded authentication pages create an unfinished and unprofessional experience.
- Incorrect authentication state handling may indicate a deeper session lifecycle issue requiring investigation.

Because the Admin application provides access to sensitive member information and administrative functions, authentication state consistency is particularly important.

### Notes or constraints

**Investigation requirements**

Review the existing authentication implementation before making changes.

Relevant areas may include:

- Authentication middleware.
- OpenID Connect / OAuth session handling.
- Identity-provider integration.
- Access token expiration and renewal.
- Refresh token handling, where applicable.
- Authentication cookie lifetime and validation.
- Frontend authentication state management.
- API responses to expired or invalid credentials.
- HTTP 401 and 403 handling.
- Authentication redirects and return URLs.
- Differences between full-page navigation and client-side navigation.
- Server-side rendering and interactive component authentication state.
- Session expiration and sign-out endpoints.

Determine whether the problem results from actual session expiration, failed renewal, stale frontend state or inconsistent error handling.

Do not simply suppress the session-ended message without addressing the underlying authentication behaviour.

**Security considerations**

- Do not extend session lifetimes merely to hide the problem.
- Do not bypass authentication checks.
- Do not automatically reauthenticate an administrator after an explicit logout.
- Preserve configured identity-provider and MFA requirements.
- Ensure revoked, suspended or expired accounts cannot continue accessing protected resources.
- Do not expose authentication tokens or sensitive session details in application logs.
- Use appropriate audit logging for sign-in, sign-out and security-relevant session events.
- Preserve safe return URL handling and prevent open redirects.

**Design requirements**

The Admin application should use a consistent Swoovr-branded authentication experience.

Authentication-related messages must be presented uniformly across all Admin routes.

Where technically appropriate, use a shared authentication layout that is separate from the authenticated Admin layout, preventing stale user information from appearing on signed-out or session-expired pages.

**Suggested implementation order**

1. Reproduce the intermittent session-ended behaviour.
2. Identify whether the underlying session is actually expired.
3. Review frontend, API and identity-provider authentication state.
4. Investigate token renewal and cookie lifecycle behaviour.
5. Correct the underlying authentication state inconsistency.
6. Standardise handling of expired sessions and unauthorised responses.
7. Implement consistent Swoovr-branded authentication and signed-out pages.
8. Verify sign-out and subsequent sign-in behaviour.
9. Test navigation, session renewal, expiration and account revocation scenarios.

### Acceptance criteria

- [ ] Valid administrator sessions remain authenticated during normal navigation.
- [ ] Session-ended messages are not displayed while the application has a valid, usable session.
- [ ] Authentication state is consistent between the Admin UI, API and identity provider.
- [ ] Access tokens are renewed appropriately when supported by the existing authentication configuration.
- [ ] Genuine session expiration is handled clearly and securely.
- [ ] Expired sessions cannot access protected Admin resources.
- [ ] Session-expired pages do not misleadingly display the administrator as actively signed in.
- [ ] Session-expired and signed-out states are clearly distinguished.
- [ ] Sign-out correctly clears or terminates the appropriate authentication state.
- [ ] All authentication-related pages use consistent Swoovr branding and styling.
- [ ] Authentication messages render consistently across Admin routes.
- [ ] Administrators can return to their intended page after reauthentication where safe.
- [ ] Authentication handling does not weaken existing security policies.
- [ ] Automated tests cover valid sessions, token expiration, session renewal, explicit logout and unauthorised access.
- [ ] The underlying cause is identified and resolved rather than hidden through UI changes.

<a id="intake-4"></a>

## 4. Improve Admin photo moderation workflow, reveal controls and standardised review decisions

Implementation progress (2 November2026): independently actionable queue fixes are included in merged [PR #152](https://github.com/repasscloud/wanderconnect/pull/152): factual automatic review purpose, blurred preview, audited keyboard reveal, stable decision-bound codes, server-side Other-note requirement and retained coded counts. Synthetic-only actual Edge acceptance and full `./scripts/check.sh` passed (607 PostgreSQL cases, three smoke suites, zero build warnings/errors). Review-discovered cross-item note restoration is fixed and regressed. D178 eligibility remains deferred/unchanged; no adult material or owner review is needed. Case-specific legacy controls remain, and private approval notes require a governed retained-history design rather than ungoverned retention; coded approvals refuse unsupported notes. See [handoff](ISSUE-FIX-HANDOFF.md) and [reason boundaries](../architecture/MEDIA-REVIEW-REASONS.md). Issue remains partially fixed/open pending remaining acceptance and these precise dependencies.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/media`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#139](https://github.com/repasscloud/wanderconnect/issues/139)
- **Tracking Q:** Q97

### Historical intake investigation — pre-merge source snapshot

Q97: Code confirms required purpose before a media link, CSS blur/reveal checkbox and free-text decision reason; escalation already exists. The second screenshot shows browser required-field validation, not a revealed photograph. OPEN: intake permission for explicit private-chat media conflicts with CONFIRMED D178 (no explicit content anywhere in beta). Preserve screening, conflict exclusions, audit and evidence holds; no permission change is approved here.

Source inspection: `src/Swoovr.Admin/Components/Pages/MediaReview.razor; docs/delivery/DECISIONS.md (D178, D203, D205)`.

Duplicate review: Related #85 (screening), #88 (sensitive access), #116 (playbook), #54 (explicit-media policy); these do not cover the combined review-controls/reason-code UX.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The Swoovr Admin photo moderation interface introduces unnecessary steps when reviewing member photographs and does not provide sufficiently structured approval and rejection decisions.

When an administrator attempts to review a photo, the interface requires a manually entered reason before the photo can be displayed.

This is confusing because the administrator is already working within the dedicated photo moderation queue. The purpose of accessing the photograph is to perform a moderation review.

The current process is:

1. Open the Media moderation page.
2. Enter a free-text reason for viewing the photograph.
3. Click "Show photo".
4. The photograph appears blurred.
5. Click "Reveal" to display the actual image.
6. Enter another reason when approving or rejecting the photograph.

The workflow creates avoidable administrative overhead.

There are also problems with how moderation decisions are recorded.

**1. Viewing a photograph requires an unnecessary free-text explanation**

Administrators must manually enter a reason to view content already assigned to the photo moderation queue.

The review context itself should provide the reason for access.

**2. The reveal process is unnecessarily complicated**

The two-stage blurred/revealed presentation is useful, particularly when reviewing potentially sensitive content.

However, the administrator should not need to enter free text before reaching the blurred preview.

**3. Approval and rejection reasons are not standardised**

The interface relies on manually entered explanations instead of structured moderation categories.

This permits inconsistent values such as ".", "OK", "No" or arbitrary wording.

It also makes moderation statistics and subsequent investigations difficult.

**4. The purpose of Reject is not sufficiently clear**

The interface appears to treat approval and rejection as separate actions without clearly explaining how rejection reasons should be classified.

A rejection should be a moderation decision supported by a predefined reason, rather than an unexplained action.

**5. Adult private-chat media requires a different moderation workflow**

Swoovr allows permitted adult content within private messaging under the applicable member preferences and platform policies.

This content should not automatically enter the same routine photo approval queue as public profile photos.

The distinction between public profile media and permitted private-chat media must be reflected in the moderation workflow.

### What I expected

The Admin photo moderation interface should provide a fast, consistent and auditable review process.

Routine photo reviews should require minimal unnecessary input while preserving appropriate access controls, privacy protections and moderation records.

#### A. Remove mandatory free-text reasons for routine photo review

When an administrator opens a photo assigned to the moderation queue:

- Display the photograph in its blurred state.
- Do not require a manually typed reason to access the review.
- Provide a clear "Reveal photo" action.
- Allow the administrator to reveal the photograph when ready.
- Record the access automatically in the audit system.

The reason for access should be derived from the workflow.

For example:

**Access reason:** Routine photo moderation

This should be recorded automatically rather than entered manually.

The audit record should include:

- Administrator ID.
- Media ID.
- Associated moderation case or queue item.
- Access timestamp (UTC).
- Access purpose.
- Whether the photograph was revealed.

Do not allow administrators to bypass auditing by omitting a reason.

**Important distinction:** Routine assigned moderation should not require free-text justification. Access to media outside an assigned review or investigation may still require a documented justification and additional permissions.

#### B. Keep the blurred preview and deliberate reveal action

The blurred image preview is useful and should be retained.

Expected workflow:

1. Administrator opens a photo review item.
2. A blurred preview appears.
3. Administrator selects **Reveal photo**.
4. The photograph becomes visible.
5. Administrator makes a moderation decision.

The reveal action should be clearly labelled and consistently positioned.

Avoid confusing sequences involving "Show photo" followed by another "Reveal" control.

The same preview behaviour should be available across relevant moderation interfaces.

Revealing a photo must remain restricted to authorised administrators and should be auditable.

#### C. Introduce predefined approval reasons

When approving a photograph, administrators should choose from standardised approval categories.

Suggested approval reasons:

| Code | Approval reason |
| --- | --- |
| APPROVED_STANDARD | Meets Swoovr photo guidelines |
| APPROVED_NON_EXPLICIT | Non-explicit image permitted |
| APPROVED_AFTER_REVIEW | Acceptable following manual review |
| APPROVED_FALSE_POSITIVE | Automated screening incorrectly flagged content |

For routine approvals, **Meets Swoovr photo guidelines** may be the default selection.

The administrator should not need to type a free-text explanation for every routine approval.

Optional notes may be provided when additional context is useful.

The selected reason must be stored as a structured code rather than relying solely on display text.

#### D. Introduce predefined rejection reasons

Rejection should remain available because some photographs must not be displayed on Swoovr.

However, the administrator should select a clear reason for rejection.

Suggested rejection categories:

| Code | Rejection reason |
| --- | --- |
| REJECT_EXPLICIT_PUBLIC | Nudity or explicit sexual content in a public profile |
| REJECT_UNDERAGE | Suspected underage or child-safety concern |
| REJECT_NON_CONSENSUAL | Suspected non-consensual or intimate-image abuse |
| REJECT_IMPERSONATION | Suspected impersonation or unauthorised use of another person's image |
| REJECT_PRIVACY | Contains prohibited personal or location information |
| REJECT_VIOLENCE | Graphic violence or disturbing content |
| REJECT_HATE | Hateful or prohibited extremist imagery |
| REJECT_SPAM | Spam, advertising or prohibited promotional content |
| REJECT_IMAGE_QUALITY | Image does not meet applicable technical or quality requirements |
| REJECT_OTHER | Other policy violation |

The selected category should determine the recorded moderation outcome.

For "Other", require an additional explanation.

For serious categories, such as suspected child sexual abuse material or non-consensual intimate imagery, use the established Trust and Safety escalation workflow rather than treating the matter as an ordinary rejected photo.

Do not automatically delete or permanently destroy evidence subject to a legitimate preservation requirement.

#### E. Separate moderation decisions from escalation actions

The interface should distinguish between ordinary moderation and content requiring further investigation.

Suggested actions:

- **Approve** — The photograph meets applicable requirements.
- **Reject** — The photograph violates an applicable policy.
- **Escalate** — The photograph requires specialist Trust and Safety review.

An escalated item should be assigned to the appropriate restricted review queue.

Do not require the reviewer to make a definitive policy finding when the evidence is uncertain.

All outcomes should be auditable.

#### F. Ensure rejection decisions are useful for reporting

Store moderation outcomes in a structured format suitable for reporting and investigation.

At minimum:

- Media ID.
- Member ID.
- Media context: public profile, private album or private chat.
- Moderation case ID.
- Reviewer ID.
- Decision.
- Reason code.
- Optional notes.
- Decision timestamp (UTC).
- Previous and resulting moderation status.

This should allow administrators to report on:

- Total approved photographs.
- Total rejected photographs.
- Rejection counts by category.
- Escalated moderation cases.
- Automated screening false positives.
- Moderation outcomes over time.
- Reviewer activity and decision history.

Reason codes should remain stable even if their display labels are updated.

#### G. Separate public profile photos from permitted private-chat media

Swoovr has different content rules depending on where media is shared.

**Public profile photographs**

- Must comply with public profile content requirements.
- Nudity and explicit sexual content are prohibited.
- Continue through the appropriate automated screening and moderation workflow.
- Photographs must not become publicly visible until the applicable approval requirements are met.

**Permitted adult private-chat media**

Where adult media is permitted under Swoovr's policies and the applicable member preferences:

- Do not automatically send every permitted adult private-chat image into the routine public-profile photo approval queue.
- Do not require routine human approval before delivery solely because the image contains permitted adult nudity.
- Respect the receiving member's media preferences and existing reveal protections.
- Continue applying automated safety screening where required.
- Route flagged, reported, prohibited or high-risk content into the appropriate Trust and Safety workflow.
- Preserve existing blocking, reporting and evidence-handling requirements.

**Important:** Adult-media permission must not disable safety screening.

Permitted consensual adult nudity is different from suspected child sexual abuse material, non-consensual intimate imagery and other prohibited content.

Media with serious safety indicators must remain subject to the appropriate detection, restriction, escalation and reporting processes.

The system should distinguish between **content permitted by policy** and **content requiring intervention**.

#### H. Improve the Admin moderation interface

The photo review screen should be redesigned around the reviewer's task.

Suggested layout:

**Media review panel**

- Member reference.
- Media type and source.
- Upload timestamp.
- Automated screening status.
- Blurred photo preview.
- Reveal photo action.
- Relevant moderation history.

**Decision panel**

- Approve.
- Reject.
- Escalate.
- Contextual reason dropdown.
- Optional reviewer notes.
- Submit decision.

The reason dropdown should change depending on the selected action.

For example, choosing Reject should display rejection categories rather than approval categories.

Keep controls consistent across all photo review screens.

Avoid excessive forms, unnecessary confirmation steps and inconsistent button placement.

#### I. Provide clear outcomes and confirmation

After submitting a moderation decision:

- Confirm the action was saved.
- Update the media status.
- Record the structured reason.
- Update the relevant queue.
- Move to the next review item where appropriate.
- Prevent accidental duplicate submissions.

Require additional confirmation only for sensitive or irreversible actions where justified.

Do not add unnecessary confirmation dialogs to ordinary approval workflows.

### Steps to reproduce

1. Sign in to the Swoovr Admin application using an authorised administrator account.
2. Navigate to `http://localhost:5082/media`.
3. Open a photograph requiring moderation.
4. Observe that a free-text reason is required before viewing.
5. Enter a reason and click "Show photo".
6. Observe that the photo appears blurred.
7. Click "Reveal" to display the image.
8. Attempt to approve or reject the photograph.
9. Observe the current reason-entry controls.
10. Review whether approval and rejection reasons use predefined categories.
11. Compare the workflow with private-chat media handling, where applicable.

### Evidence

![Photo moderation — reason required before viewing](evidence/2026-11-01-issue-intake/04-photos-asking-for-reason-to-view-to-verify-01.png)

![Photo moderation — blurred preview and reveal action](evidence/2026-11-01-issue-intake/04-photos-asking-for-reason-to-view-to-verify-02.png)

### Impact

**Affected users:** Swoovr administrators and authorised Trust and Safety reviewers.

**Impact:**

- Routine moderation requires unnecessary manual input.
- Reviewers spend additional time accessing content already assigned for review.
- Unstructured reasons produce inconsistent moderation records.
- Arbitrary free-text explanations undermine reporting accuracy.
- Approval and rejection statistics cannot be reliably categorised.
- Moderation decisions are more difficult to audit.
- Private adult media may be unnecessarily delayed if routed through the wrong moderation workflow.
- Inconsistent presentation increases reviewer workload.

Structured moderation reasons will improve reporting, consistency and subsequent case investigations.

### Notes or constraints

**Access control and auditing**

Removing the mandatory free-text access reason must not remove access auditing.

Routine access reasons should be recorded automatically based on the moderation workflow.

Access outside assigned moderation workflows may require a separate justification and stricter authorisation.

**Private media**

Do not implement a blanket bypass of media safety checks merely because a member has enabled adult private-chat content.

Distinguish between:

- Routine public photo moderation.
- Permitted private adult content.
- Automatically flagged content.
- Member-reported content.
- Serious Trust and Safety incidents.

Private media should not routinely require manual approval solely because permitted adult content is present, but prohibited and high-risk material must still be handled appropriately.

**Moderation reason codes**

Use stable reason codes suitable for database storage, reporting and future localisation.

Do not use arbitrary user-entered text as the primary moderation classification.

Optional notes should supplement, not replace, structured reasons.

**Suggested implementation order**

1. Review the existing media access and moderation workflow.
2. Remove unnecessary free-text justification for assigned routine reviews.
3. Preserve automatic access and reveal auditing.
4. Standardise the blurred preview and Reveal photo interaction.
5. Introduce structured approval and rejection reasons.
6. Add a distinct escalation action where needed.
7. Store structured moderation outcomes and optional reviewer notes.
8. Separate public photo moderation from permitted private-chat media handling.
9. Ensure automated screening and serious safety escalation remain effective.
10. Validate review permissions, audit records and reporting.

### Acceptance criteria

- [ ] Assigned routine photo reviews do not require manually typed access reasons.
- [ ] Routine review access is automatically recorded with an appropriate purpose.
- [ ] Photos initially display blurred where required.
- [ ] Administrators can deliberately reveal photos using a clearly labelled control.
- [ ] Photo reveal actions are audited.
- [ ] Approval reasons are selected from predefined categories.
- [ ] Rejection reasons are selected from predefined categories.
- [ ] An optional notes field is available.
- [ ] "Other" rejection reasons require an explanation.
- [ ] Serious safety concerns can be escalated to the appropriate restricted Trust and Safety workflow.
- [ ] Moderation outcomes are stored using stable reason codes.
- [ ] Approval, rejection and escalation statistics can be reported by category.
- [ ] Media review controls and messages are consistent across Admin pages.
- [ ] Permitted private adult-chat media is not automatically routed into routine public-profile approval queues.
- [ ] Private-chat media remains subject to applicable automated safety checks and reporting controls.
- [ ] Prohibited and high-risk media continues to be restricted or escalated appropriately.
- [ ] Existing permissions, audit logging and evidence preservation requirements remain enforced.

<a id="intake-5"></a>

## 5. Redesign Beta Enrollments screen with searchable table, improved layout and scalable administration

Implementation progress (2 November2026): [PR #152](https://github.com/repasscloud/wanderconnect/pull/152) adds server-side search/pagination and a structured enrolment table. Actual Edge verified1005 records, role/anonymous refusal, paging/sort/search/status/date controls and confirmed revision-aware iOS/revoke actions. Eleven new database search checks passed; full `./scripts/check.sh` passed with607 PostgreSQL cases, three smoke suites and zero build warnings/errors. Names use canonical account linkage, never email-based identity inference; Apple/provider states remain distinct. See [handoff](ISSUE-FIX-HANDOFF.md) and [contract](../architecture/BETA-ENROLMENT-SEARCH.md). Issue remains open pending remaining acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/beta`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#140](https://github.com/repasscloud/wanderconnect/issues/140)
- **Tracking Q:** Q98

Amended scope (2 November 2026): [item 20](#intake-20) G–L reuses this issue for field width, filter/action consistency and first-line cell alignment. The merged table remains; CSS already top-aligns cells. Registration policy is separate [Q112 / #157](https://github.com/repasscloud/wanderconnect/issues/157).

### Historical intake investigation — pre-merge source snapshot

Q98: Code renders a list of up to 1,000 enrolments with per-record forms and no table search/sort/pagination controls. Screenshot supports dense stacked controls, not a measured 1,000-record performance failure. Preserve distinction between enrolment, invitation delivery and staff-reported Apple status.

Source inspection: `src/Swoovr.Admin/Components/Pages/BetaEnrolments.razor; src/Swoovr.Api/Accounts/AdminEndpoints.cs`.

Duplicate review: Related #25 covers original enrolment portal/delivery follow-ups; this is a distinct scalable table/search redesign, not a duplicate of the original missing portal.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Beta Enrollments screen is poorly organised and difficult to navigate.

Beta participants are displayed as individual blocks or cards rather than in a structured table. This creates excessive vertical scrolling, wastes available desktop screen space and makes it difficult to find, compare and manage enrollments.

While this layout may be manageable with a small number of beta participants, it will become increasingly impractical as Swoovr approaches 1,000 or more beta members.

There are also spacing and presentation problems with administrative actions.

Specifically:

1. **Enrollment records are displayed as separate blocks.** This makes scanning and comparing participants unnecessarily difficult.
2. **There is no effective tabular overview.** Administrators cannot easily review enrollment status, dates and relevant account information across multiple participants.
3. **Searching and navigating enrollments is inefficient.** The current layout does not scale well as the number of participants increases.
4. **Administrative actions are poorly positioned.** The "Record staff-reported state" and "Confirm revocation" buttons are vertically touching, making the interface visually cramped and difficult to use.
5. **The visual hierarchy is weak.** Enrollment information, administrative controls and potentially sensitive actions compete for attention.
6. **The page does not make effective use of desktop screen space.** The design should prioritise efficient administrative workflows rather than member-facing card layouts.

### What I expected

The Beta Enrollments page should be redesigned as a professional administrative management interface built around a searchable, sortable and paginated data table.

Administrators should be able to find a participant, review enrollment status and perform authorised actions without navigating through a long collection of cards.

#### A. Replace enrollment cards with a data table

Use a responsive table as the primary enrollment overview.

Suggested columns:

| Column | Description |
| --- | --- |
| Member | Display name and member reference |
| Email | Registered email address, subject to permissions |
| Enrollment date | Date the member joined the beta |
| Beta status | Current enrollment status |
| Account status | Relevant member account standing |
| Last updated | Most recent enrollment state change |
| Actions | View, manage or open enrollment details |

Use the existing data model and supported enrollment states rather than introducing new statuses unnecessarily.

Additional columns may be included if they provide meaningful administrative value.

Keep the table concise. Detailed information should be available through an enrollment detail view rather than overcrowding every row.

#### B. Add search and filtering

Provide a search field above the table.

Administrators should be able to find beta enrollments using relevant identifiers, including:

- Member name.
- Registered email address.
- Member or enrollment reference.

Provide appropriate filters for:

- Enrollment status.
- Enrollment date or date range.
- Account status, where relevant.

Search and filters should work together.

Provide a clear action to reset active filters.

When no results match, display a useful empty-state message rather than a blank table.

#### C. Support sorting and pagination

The table must remain responsive and performant with at least 1,000 beta enrollments, with capacity to scale beyond that.

Requirements:

- Server-side pagination.
- Server-side searching and filtering.
- Sortable relevant columns.
- Configurable page sizes, such as 25, 50 and 100 records.
- Clear total result count.
- Previous and next page navigation.
- Stable sorting to avoid duplicate or missing records between pages.

Do not retrieve every enrollment into the browser and perform all filtering locally.

Where practical, preserve filters, sorting and page selection when navigating into an enrollment and returning to the table.

#### D. Move enrollment management actions into a detail view

The main table should focus on locating and reviewing enrollments.

Selecting an enrollment should open a dedicated detail view, drawer or side panel.

This should display:

- Member information.
- Current beta enrollment state.
- Enrollment history.
- Relevant timestamps.
- Available administrative actions.
- Existing staff-reported state information.
- Revocation information, where applicable.

Actions such as "Record staff-reported state" and "Confirm revocation" should not dominate the primary enrollment listing.

Keep administrative actions close to the information they affect.

#### E. Correct button spacing and visual hierarchy

The current "Record staff-reported state" and "Confirm revocation" buttons are vertically touching.

All administrative controls should have consistent spacing and clearly distinguish between different actions.

Requirements:

- Buttons must not touch or visually merge.
- Use consistent button heights and padding.
- Maintain appropriate gaps between adjacent controls.
- Avoid oversized full-width buttons where unnecessary.
- Use clear primary and secondary action styling.
- Visually distinguish destructive or sensitive actions.

Revocation should use appropriate warning or destructive-action styling.

On narrower screens, buttons may stack vertically, but must retain sufficient spacing.

#### F. Improve revocation workflow

Revoking beta enrollment is a sensitive administrative action and should be clearly separated from routine status management.

Expected behaviour:

1. Administrator opens an enrollment.
2. Selects the relevant revoke action.
3. Reviews a confirmation dialog showing the affected member and intended change.
4. Selects an appropriate reason, using structured categories where supported.
5. Confirms the action.
6. Receives confirmation that the change was saved.

Do not make accidental revocation possible through a poorly positioned button.

Preserve existing business rules determining what beta revocation actually changes.

Revoking beta enrollment must not automatically suspend or delete the member's account unless that is explicitly part of the established workflow.

#### G. Improve staff-reported state management

The "Record staff-reported state" functionality should be presented in its own clearly labelled section or action dialog.

The interface must explain:

- What state is being recorded.
- Whether it is an observed external state or an actual system state change.
- Whether the action affects the member's eligibility or access.
- Who last updated the record.
- When the change was recorded.

Use structured state selections rather than arbitrary free text where the underlying workflow supports predefined states.

Allow optional notes for additional context.

Do not confuse manually recorded staff observations with authoritative enrollment state.

#### H. Provide consistent status indicators

Enrollment statuses should be easy to identify when scanning the table.

Use concise text labels and restrained visual indicators.

For example, existing statuses might be displayed as Active, Pending, Revoked or Expired, where those states are supported by the current implementation.

Do not invent new enrollment states solely for the interface.

Status must be conveyed through text, not colour alone.

#### I. Ensure responsive behaviour

**Desktop**

- Use the available horizontal space effectively.
- Display enrollment records in a structured table.
- Keep search and filters clearly accessible.
- Avoid unnecessary scrolling through cards.
- Provide a clean detail panel or page for administrative actions.

**Tablet and narrower screens**

- Adapt table columns appropriately.
- Allow horizontal scrolling where necessary.
- Preserve access to important actions.
- Avoid overlapping controls or clipped information.

The Admin application is primarily a desktop-oriented operational tool. Its design should prioritise efficient desktop use while remaining functional at smaller viewport sizes.

#### J. Preserve auditing and permissions

All enrollment state changes must continue to respect existing administrative permissions.

Record relevant administrative actions, including:

- Administrator ID.
- Enrollment/member reference.
- Action performed.
- Previous state.
- New state.
- Reason, where applicable.
- Timestamp in UTC.

Routine viewing and state-changing actions must follow their appropriate existing audit requirements.

The redesigned interface must not bypass existing authorisation, enrollment rules or audit logging.

### Steps to reproduce

1. Sign in to the Swoovr Admin application using an authorised administrator account.
2. Navigate to `http://localhost:5082/beta`.
3. Observe that beta enrollments are displayed as separate blocks rather than a table.
4. Review the amount of scrolling required to locate and compare enrollment records.
5. Locate the "Record staff-reported state" and "Confirm revocation" buttons.
6. Observe that these buttons are vertically touching with inadequate spacing.
7. Review how administrators locate specific participants and perform enrollment management actions.
8. Consider the usability of the current presentation with 1,000 or more enrollment records.

### Evidence

![Beta Enrollments screen](evidence/2026-11-01-issue-intake/05-beta-enrollments-screen-is-messy.png)

The screenshot demonstrates the existing card-based enrollment layout and poorly spaced administrative actions.

### Impact

**Affected users:** Swoovr administrators and staff responsible for beta enrollment management.

**Impact:**

- Administrators cannot efficiently scan and compare beta enrollment records.
- Finding individual participants becomes increasingly difficult as enrollment numbers grow.
- Excessive scrolling slows routine administration.
- Poorly positioned buttons increase the risk of selecting the wrong action.
- Enrollment management becomes more time-consuming as the beta program expands.
- The current layout does not scale effectively to 1,000 or more beta participants.
- The interface lacks the consistency and efficiency expected from a production administrative system.

This is a high-priority improvement because Swoovr's beta enrollment program needs to support a growing member population without creating unnecessary operational overhead.

### Notes or constraints

**Design direction**

The Swoovr Admin application should use a professional, data-oriented design appropriate for operational administration.

Card-based layouts may be appropriate for individual record details, but tables should be preferred for managing larger collections of structured records.

**Technical considerations**

- Preserve existing beta enrollment records and state transitions.
- Use server-side search, sorting, filtering and pagination.
- Avoid loading the entire enrollment dataset into the browser.
- Preserve existing administrative permissions.
- Maintain audit records for state-changing actions.
- Keep member and enrollment status distinct.
- Do not unintentionally alter member account standing when managing beta enrollment.
- Reuse existing Admin design components where appropriate.
- Ensure sensitive actions require deliberate confirmation.
- Maintain accessibility and keyboard navigation.
- Use existing API conventions without introducing unnecessary versioned routes.
- Ensure the interface remains responsive with at least 1,000 enrollment records.

**Suggested implementation order**

1. Review the existing beta enrollment data model and administrative functions.
2. Replace the enrollment card listing with a structured data table.
3. Implement server-side search, filtering, sorting and pagination.
4. Introduce a dedicated enrollment detail view or side panel.
5. Relocate administrative actions into the appropriate detail context.
6. Fix button sizing, spacing and visual hierarchy.
7. Improve staff-reported state and revocation workflows.
8. Verify permissions, auditing and state transitions.
9. Test with realistic datasets containing 1,000 or more enrollments.

### Acceptance criteria

- [ ] Beta enrollments are displayed in a structured table rather than individual cards.
- [ ] Administrators can search by member name, email address and relevant reference identifiers.
- [ ] Table columns support appropriate sorting.
- [ ] Enrollment records can be filtered by status and relevant dates.
- [ ] Pagination is handled server-side.
- [ ] Page size can be selected.
- [ ] Total result count is displayed.
- [ ] The table performs acceptably with 1,000 or more enrollments.
- [ ] Enrollment details and administrative actions are accessible without overcrowding the table.
- [ ] "Record staff-reported state" and "Confirm revocation" controls no longer touch or overlap.
- [ ] Buttons use consistent spacing, sizing and styling.
- [ ] Revocation requires clear confirmation.
- [ ] Staff-reported state is clearly distinguished from authoritative enrollment state.
- [ ] Status information is easy to understand and visually consistent.
- [ ] Existing enrollment data and business rules remain unchanged unless explicitly required.
- [ ] Administrative permissions and audit logging remain enforced.
- [ ] Desktop and narrower viewport layouts remain usable and accessible.

<a id="intake-7"></a>

## 7. Redesign public landing page and remove authenticated navigation and development messaging

Implementation progress (2 November 2026): Guest navigation/landing fixed; truthful private-beta and draft-policy disclosures retained. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `http://localhost:5081/`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#141](https://github.com/repasscloud/wanderconnect/issues/141)
- **Tracking Q:** Q99

### Historical intake investigation — pre-merge source snapshot

Q99: Home source confirms work-in-progress copy and appearance CTA. Shared navigation is visible in the screenshot. OPEN: removing genuine draft/legal/readiness disclosures must not imply approved documents or a live service. Keep Standard default and personal choices; no rebranding.

Source inspection: `src/Swoovr.Web/Components/Pages/Home.razor; src/Swoovr.Web/Components/Layout/MainLayout.razor`.

Duplicate review: Related #48 (accessibility) and #57 (release review); neither covers public entry navigation/copy.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Swoovr landing page displays development-oriented messaging and application navigation that should not be visible to unauthenticated visitors.

The page currently includes:

- "A work in progress".
- "Choose your appearance".
- Explore, Connections, Chats and Me navigation.
- Messaging explaining that the platform is still being developed.
- "A connection platform in progress" in the footer.

This gives the impression of an unfinished application rather than a professional dating and social discovery platform.

The unauthenticated landing page should have a clearly defined purpose: introduce Swoovr and direct visitors to sign in or create an account.

### What I expected

The `/` route should present a professionally designed Swoovr welcome and authentication entry page.

#### A. Preserve the existing brand identity

Retain:

- The Swoovr wordmark in the top-left corner.
- The existing top header/bar styling.
- The tagline **"Your kind of connection"**.
- The introductory messaging and overall brand direction.

Refine the supporting copy to describe Swoovr as a dating, meeting and social discovery platform.

Remove references suggesting the platform is unfinished, under development or not ready for use.

Do not introduce replacement marketing slogans without reviewing existing approved brand copy.

#### B. Simplify unauthenticated navigation

Unauthenticated visitors should not see the authenticated application navigation:

- Explore.
- Connections.
- Chats.
- Me.

Instead, the header should provide clear authentication actions:

- **Sign in**
- **Create account**

These should be the primary actions.

Do not expose member-only navigation before authentication.

#### C. Remove unnecessary controls

Remove "Choose your appearance" from the public landing page.

Appearance preferences belong in appropriate application settings, not as a prominent landing-page feature.

The page should remain focused on introducing Swoovr and entering the application.

#### D. Replace development messaging

Remove all development disclaimers and placeholders, including:

- "A work in progress".
- "A connection platform in progress".
- "We're building a little more room..." and similar unfinished-product language.
- Development version labels.
- Unnecessary references to UAT or testing.

Development diagnostics and build information may remain available to authorised developers through appropriate non-public tools.

They should not appear in the normal member-facing interface.

#### E. Implement a proper public footer

The unauthenticated landing page should have a professional business footer.

Include appropriate links to:

- Terms of Use.
- Privacy.
- Community Guidelines.
- Connection & Meeting Advice.
- Support or Contact, where available.

Use clear, concise labels.

Do not display development status, internal build identifiers or draft labels in the footer.

Footer links must navigate to actual pages containing the correct content.

#### F. Support desktop, tablet and mobile layouts

The landing page must be responsive and professionally designed for each viewport.

**Desktop**

- Use the available space effectively.
- Present the brand and authentication actions with a balanced layout.
- Avoid oversized buttons and unnecessarily narrow content columns.

**Mobile web**

- Use an appropriately adapted layout.
- Keep Sign in and Create account prominent.
- Avoid horizontal overflow or cramped content.

The web experience should align visually with native mobile applications without forcing desktop users into a mobile-sized design.

### Steps to reproduce

1. Open `http://localhost:5081/` in a browser without an authenticated member session.
2. Observe the development-related text and unfinished-product messaging.
3. Observe the appearance selector and authenticated navigation.
4. Review the footer.
5. Compare the current presentation against the expected production-ready Swoovr welcome experience.

### Evidence

![Current unauthenticated landing page](evidence/2026-11-01-issue-intake/07-loading-screen-shoult-not-have-anything-except-login-register-options.png)

### Impact

**Affected users:** All new and returning visitors accessing Swoovr without an authenticated session.

The current page:

- Creates an unprofessional first impression.
- Suggests the application is incomplete.
- Exposes navigation irrelevant to unauthenticated users.
- Makes the intended sign-in and registration journey less clear.
- Undermines the quality of UAT because the interface does not represent the intended production experience.

### Notes or constraints

- Preserve existing Swoovr branding and approved copy where appropriate.
- Do not expose authenticated navigation to unauthenticated visitors.
- Keep public landing-page content separate from authenticated member layouts.
- Do not use development disclaimers to compensate for unfinished functionality.
- UAT should reflect the intended production user experience.
- Coordinate the authentication navigation with issues #8, #9 and #10.
- Do not remove legally necessary disclosures or links simply to improve visual appearance.

### Acceptance criteria

- [ ] `/` presents a professionally designed public Swoovr landing page.
- [ ] The Swoovr wordmark and "Your kind of connection" tagline are retained.
- [ ] Sign in and Create account are the primary actions.
- [ ] Explore, Connections, Chats and Me are not displayed to unauthenticated visitors.
- [ ] "Choose your appearance" is removed from the landing page.
- [ ] Development-oriented messaging is removed.
- [ ] A professional business footer is provided.
- [ ] Footer links navigate correctly.
- [ ] Desktop, tablet and mobile layouts are responsive and visually polished.
- [ ] The UAT experience reflects the intended production appearance.

<a id="intake-8"></a>

## 8. Correct unauthenticated /account routing and prevent account content being displayed before sign-in

Implementation progress (2 November 2026): Configured guests route directly into sign-in; retry/unconfigured fallbacks retained; BFF still401. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `http://localhost:5081/account`
- **Type:** Improvement
- **Priority:** Medium
- **GitHub issue:** [#142](https://github.com/repasscloud/wanderconnect/issues/142)
- **Tracking Q:** Q100

### Historical intake investigation — pre-merge source snapshot

Q100: The unauthenticated branch renders sign-in/create links and public settings/legal links. Private MemberAccountPanel is in the authenticated branch with prerender disabled. Screenshot supports routing/UX dissatisfaction; private account data exposure is not demonstrated. Verify guest HTML/API responses and safe return paths independently.

Source inspection: `src/Swoovr.Web/Components/Pages/Account.razor`.

Duplicate review: Related intake 7/9/14; #134 is a harness issue, not this route change.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

When an unauthenticated visitor navigates directly to `/account`, the application displays account-related content and authentication options that should not appear on this route without a valid member session.

The page also exposes appearance controls and presents authentication information separately from the main landing page.

This creates an inconsistent navigation experience because unauthenticated visitors encounter different login-related interfaces depending on which URL they access.

The `/account` route should be part of the authenticated member application, not an alternative public login or registration page.

### What I expected

The application must distinguish between authenticated and unauthenticated requests.

#### A. Redirect unauthenticated visitors

When a visitor without a valid authenticated session requests:

`/account`

The application should redirect them to:

`/`

The public landing page should provide the appropriate Sign in and Create account actions, as defined in issue #7.

Do not render the member account page before the redirect.

#### B. Preserve authenticated access

When a properly authenticated member navigates to `/account`, the application should display their account page normally.

Existing onboarding requirements must continue to apply.

In particular:

- Mobile number verification.
- Date-of-birth completion.
- Minimum age eligibility.

If an authenticated member has not completed required onboarding, they should follow the established onboarding sequence rather than gaining unrestricted access to the account experience.

#### C. Prevent unauthorised content exposure

The redirect must not be the only security control.

- Protect account data at the appropriate server and API layers.
- Do not render member information before verifying authentication.
- Do not rely solely on client-side navigation guards.
- Do not expose private account information in page source, initial application state or API responses to unauthenticated visitors.

Authentication and authorisation remain backend responsibilities.

#### D. Maintain consistent authentication entry points

Sign in and registration should originate from the public Swoovr experience.

The `/account` page should not introduce a separate login screen with different styling or navigation.

Coordinate the visual design and routing with issues #7, #9 and #10.

#### E. Handle expired sessions

If an authenticated member's session expires while accessing `/account`:

- Follow the established session expiration and authentication recovery process.
- Do not display stale member information.
- Redirect to the appropriate public or authentication entry point.
- Preserve a safe return destination where supported.

Avoid redirect loops between `/`, `/account` and the identity provider.

### Steps to reproduce

1. Open a new private browser window.
2. Confirm there is no authenticated Swoovr member session.
3. Navigate directly to `http://localhost:5081/account`.
4. Observe that account-related information and login controls are displayed.
5. Confirm that the application does not immediately redirect to `/`.
6. Repeat the test with an authenticated member session.

### Evidence

![Account route displayed while logged out](evidence/2026-11-01-issue-intake/08-account-route-when-logged-out.png)

### Impact

**Affected users:** Unauthenticated visitors and members with expired sessions.

The issue:

- Creates inconsistent sign-in navigation.
- Exposes account-related UI before authentication.
- Duplicates functionality that belongs on the public landing page.
- Allows account settings such as appearance to be accessed in an unintended context.
- Complicates the authentication and onboarding journey.

### Notes or constraints

- Unauthenticated requests to `/account` must redirect to `/`.
- Authenticated members must retain normal account access, subject to onboarding requirements.
- Do not change the authentication provider or existing account identity model unnecessarily.
- Preserve secure return URL handling.
- Prevent redirect loops.
- Ensure server-side route protection remains effective.
- Follow the public landing-page design defined in issue #7.
- Coordinate login and registration with issues #9 and #10.

### Acceptance criteria

- [ ] Unauthenticated requests to `/account` redirect to `/`.
- [ ] Unauthenticated visitors do not see member account content.
- [ ] Authenticated members can access `/account`.
- [ ] Incomplete onboarding remains correctly enforced.
- [ ] Account information is protected server-side.
- [ ] Expired sessions are handled consistently.
- [ ] No redirect loops occur.
- [ ] Login and registration options are presented through the correct public entry points.

<a id="intake-9"></a>

## 9. Restore approved Swoovr sign-in design and correct desktop and mobile authentication layouts

Implementation progress (2 November 2026): Desktop auth frame implemented; canonical420px form/mobile layout retained; native acceptance outstanding. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web / Mobile
- **Page or URL:** Member sign-in flow, including `/account` where currently used
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#143](https://github.com/repasscloud/wanderconnect/issues/143)
- **Tracking Q:** Q101

### Historical intake investigation — pre-merge source snapshot

Q101: Custom Keycloak theme already exists. Compare current browser viewports and system-browser mobile auth against reference, rather than assuming no theme. Read-only reference is a layout concept; canonical Standard sign-in tokens govern. Native presentation not reproduced.

Source inspection: `containers/keycloak/themes/swoovr/login/login.ftl; containers/keycloak/themes/swoovr/login/resources/css/swoovr.css; docs/product/03-SCREENS-AND-DESIGN.md`.

Duplicate review: Related #48; separate from intake 10/12 page-specific registration/recovery acceptance.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Swoovr sign-in interface does not follow the previously established visual design.

The page feels generic, poorly proportioned and inconsistent with the intended Swoovr authentication experience.

Several controls, particularly authentication buttons, are excessively wide.

The interface appears to have been implemented around functional authentication requirements without properly following the approved mobile design references or adapting them for desktop.

The existing sign-in reference design was already established and should be used as the visual starting point.

### What I expected

The member sign-in interface should follow the previously approved Swoovr aesthetic and be adapted appropriately for desktop, tablet, mobile web and native applications.

#### A. Use the existing design reference

Primary reference:

`../design/reference/screens/04-sign-in.png`

Additional visual reference:

`evidence/2026-11-01-issue-intake/13-forgot-password-screen-notification-incorrect.png`

Use the first reference as the intended sign-in concept.

The second reference provides additional styling guidance.

Do not simply reproduce a mobile screenshot at desktop dimensions.

Use the existing design language to create layouts appropriate for each platform.

#### B. Improve the desktop sign-in layout

The desktop sign-in experience should:

- Make appropriate use of available screen space.
- Use a balanced authentication panel.
- Avoid excessively wide input fields or buttons.
- Apply consistent spacing between controls.
- Preserve the Swoovr visual identity.
- Maintain readable typography and clear visual hierarchy.

A centred, appropriately constrained authentication panel is acceptable on desktop.

However, the entire desktop page should not resemble a mobile screen stretched or placed awkwardly in the browser.

The surrounding layout should feel intentional and professionally designed.

#### C. Correct button sizing

Sign-in actions should use sensible dimensions.

- Avoid ultra-wide buttons spanning unnecessarily large containers.
- Use consistent control heights.
- Provide adequate spacing between buttons.
- Clearly distinguish primary and secondary actions.
- Keep touch-friendly dimensions on mobile.
- Avoid controls touching or visually merging.

Full-width buttons may be appropriate within a reasonably sized mobile authentication form, but should not automatically span the full desktop viewport.

#### D. Maintain a consistent authentication journey

The sign-in experience should provide the appropriate existing options, including:

- Email and password sign-in.
- Create account.
- Forgot password.
- Other supported authentication methods, where implemented.

Do not invent or expose authentication methods that are not supported.

Provide clear feedback for:

- Invalid credentials.
- Required fields.
- Authentication failures.
- Account restrictions.
- Network or service errors.

Error messages should be understandable without exposing sensitive authentication details.

#### E. Ensure consistency across platforms

The same Swoovr design system should apply to:

- Desktop web.
- Tablet web.
- Mobile web.
- Native iOS.
- Native Android.

The layouts do not need to be identical.

Each platform should receive appropriate spacing, navigation and interaction patterns.

Native mobile applications should use platform-appropriate authentication behaviour while maintaining Swoovr branding.

#### F. Remove unprofessional authentication presentation

Authentication pages must not contain:

- Development disclaimers.
- Internal build information.
- Unfinished-product messaging.
- Unstyled browser-like controls.
- Inconsistent typography or spacing.

Sign-in, registration, password recovery and other authentication screens should feel like one coherent application.

### Steps to reproduce

1. Open the unauthenticated Swoovr member web application.
2. Select Sign in.
3. Observe the current sign-in page.
4. Review the width of buttons and form controls.
5. Compare the interface with `04-sign-in.png`.
6. Repeat at desktop, tablet and mobile viewport sizes.
7. Review consistency with the forgot-password design.

### Evidence

**Approved design reference:**

![Swoovr sign-in design reference](../design/reference/screens/04-sign-in.png)

**Additional styling reference:**

Evidence withheld from publication: `13-forgot-password-screen-notification-incorrect.png` (authentication transaction identifiers and populated masked credential field). See [evidence review](evidence/2026-11-01-issue-intake/README.md).

### Impact

**Affected users:** All members signing in to Swoovr.

The current design:

- Does not reflect the previously approved authentication concept.
- Creates an inconsistent first impression.
- Makes the interface appear unfinished.
- Uses unnecessarily large controls.
- Reduces visual consistency between web and native mobile experiences.

Sign-in is a primary interaction and must meet the same design standards as the rest of the application.

### Notes or constraints

- Follow the approved Swoovr sign-in reference.
- Preserve existing authentication functionality.
- Do not alter security or identity-provider behaviour merely to change visual presentation.
- Apply consistent design to authentication-related errors and recovery states.
- Coordinate with issue #7 for the public landing page.
- Coordinate with issue #8 for unauthenticated routing.
- Coordinate with issue #10 for registration and identity-provider styling.
- Ensure responsive layouts are properly designed rather than mechanically scaled.

### Acceptance criteria

- [ ] Sign-in presentation follows the approved Swoovr design direction.
- [ ] Desktop sign-in is properly proportioned.
- [ ] Buttons are not excessively wide.
- [ ] Inputs and buttons use consistent spacing and sizing.
- [ ] Mobile web remains touch-friendly.
- [ ] Native iOS and Android use visually consistent authentication styling.
- [ ] Forgot-password and other authentication screens follow the same design system.
- [ ] Existing authentication functionality remains intact.
- [ ] Error states are styled consistently.
- [ ] No development messaging appears in the user-facing sign-in experience.

<a id="intake-10"></a>

## 10. Redesign account registration screen to meet Swoovr's production design standards

Implementation progress (2 November 2026): Registration shares the responsive frame; email-first/adult/draft requirements preserved. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web / Mobile
- **Page or URL:** `http://localhost:8180/realms/swoovr-members-dev/` (Keycloak registration flow)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#144](https://github.com/repasscloud/wanderconnect/issues/144)
- **Tracking Q:** Q102

### Historical intake investigation — pre-merge source snapshot

Q102: Theme code includes registration, adult checkbox, legal draft wording and development footer. Registration screenshot for intake 11 supports narrow layout. OPEN: cosmetic removal of draft/acceptance caveats cannot make draft legal documents operative; coordinate legal gate #57. Do not change provider or age checks.

Source inspection: `containers/keycloak/themes/swoovr/login/register.ftl; containers/keycloak/themes/swoovr/login/register-commons.ftl; containers/keycloak/themes/swoovr/login/footer.ftl`.

Duplicate review: Related #57 and intake 9/11; registration layout is independently actionable.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Swoovr "Create your account" screen does not meet the expected design standards for a professional dating and social discovery application.

The registration page is poorly proportioned, unnecessarily constrained and visually inconsistent with the previously established Swoovr authentication designs.

Several elements make the application appear unfinished or still under development, despite this being a user-facing registration experience intended for UAT and ultimately production.

Specific issues include:

**1. Poor layout and proportions**

The registration form is constrained to a narrow area without making effective use of the available desktop screen space.

The overall layout feels more like a basic identity-provider form than a purpose-designed Swoovr registration experience.

**2. Awkward back navigation**

The back arrow (`<`) appears above the surrounding text rather than being aligned within a proper navigation control.

This looks unfinished and creates an inconsistent navigation experience.

**3. Legal links are poorly presented**

Links to the following documents are displayed within the registration content without an appropriate visual hierarchy:

- Privacy Notice.
- Terms of Use.
- Community Guidelines.

These should be presented professionally, preferably through a dedicated footer for general navigation, while keeping any legally required acknowledgement near the registration action.

**4. Draft labels are exposed to users**

The legal links include `(draft)` labels.

This makes the application appear unfinished and is inappropriate for a production-ready UAT experience.

**5. Unprofessional age and legal messaging**

The registration page currently displays text similar to:

> Swoovr is for adults 18+. This checkbox confirms your age. The proposed legal documents linked below are drafts, not in force; continuing does not accept them.

This is unnecessarily technical and draws attention to unfinished legal arrangements.

The wording is confusing and does not provide a polished registration experience.

**6. Development information is displayed in the footer**

The footer includes messaging such as:

> In development · Starting in Adelaide · Adults 18+ · v0.1.0-dev

This should not be visible as part of the normal user-facing registration experience.

Internal build numbers, environment information and development status do not belong in the public interface.

**7. Inconsistent authentication design**

The registration screen does not follow the previously established Swoovr sign-in design.

The registration experience should feel like a natural continuation of the Swoovr application rather than a separate, unrelated website.

**8. Responsive design requires improvement**

The registration experience must be properly adapted for desktop, tablet, mobile web and authentication launched from the native iOS and Android applications.

A single narrow layout should not be used indiscriminately across every screen size.

### What I expected

The account registration page should be redesigned as a polished, professional and responsive Swoovr authentication experience.

The design should follow the existing Swoovr branding and approved authentication concepts rather than introducing a separate visual style.

UAT should represent the intended production experience, including the appearance, layout and language shown to members.

#### A. Apply the approved Swoovr design

Use the established authentication design reference:

`../design/reference/screens/04-sign-in.png`

The registration page should use:

- The Swoovr wordmark.
- Consistent typography and brand colours.
- Appropriate form proportions.
- Clear headings and supporting text.
- Consistent input fields and validation styling.
- Proper spacing between controls.
- Appropriately sized primary and secondary buttons.
- A professional page background and layout.

The visual design must remain consistent with the Sign in and Forgot password experiences.

Do not simply use the default Keycloak registration template with minor colour changes.

#### B. Design properly for desktop, tablet and mobile

**Desktop**

- Use an intentionally designed desktop layout.
- Avoid an excessively narrow form floating awkwardly in a large viewport.
- Keep the form at a readable, practical width.
- Use balanced spacing and alignment.
- Avoid oversized or full-viewport-width buttons.
- Position supporting content and footer links appropriately.

**Tablet**

- Adapt the layout to the available width.
- Maintain appropriate padding and control spacing.
- Avoid awkwardly constrained or stretched content.

**Mobile web, iOS and Android**

- Use a responsive, touch-friendly layout.
- Ensure controls remain accessible when the keyboard is visible.
- Respect safe areas and smaller viewports.
- Avoid clipped labels, overlapping controls or horizontal scrolling.
- Maintain the same Swoovr visual identity.

The screens do not need identical layouts, but they must feel like the same application.

#### C. Correct the back-navigation control

Replace the awkward standalone `<` arrow with a properly aligned navigation action.

Suggested label:

**Back to sign in**

Use an appropriate back-arrow icon alongside the label if desired.

The navigation control should:

- Be clearly visible.
- Have proper spacing.
- Align with the surrounding content.
- Return the user to the correct sign-in screen.
- Preserve the supported authentication flow.

Do not position an isolated arrow above unrelated text.

#### D. Simplify the registration messaging

The registration page should use concise, understandable wording.

Suggested heading:

**Create your account**

Supporting text:

"Join Swoovr and start making connections your way."

For age eligibility:

**You must be 18 or older to use Swoovr.**

If an age declaration checkbox is required, use clear wording such as:

"I confirm that I am at least 18 years old."

Do not include technical explanations about draft legal documents or incomplete development work.

The age declaration must remain separate from the actual date-of-birth eligibility checks performed during onboarding.

#### E. Correct legal document placement and presentation

Provide professionally styled links to:

- Terms of Use.
- Privacy.
- Community Guidelines.

General navigation links should be placed in a dedicated footer.

Where registration requires explicit agreement to the Terms of Use or Community Guidelines, the relevant links and consent control should remain visible near the registration action.

Do not hide legally required acknowledgements in the footer.

Use **Privacy** as the visible label rather than "Privacy Notice", consistent with the wider Swoovr application.

Remove `(draft)` labels only when the referenced documents have been finalised and are suitable for use.

If the documents are not yet operative, resolve the legal content and consent requirements before production release rather than presenting misleading acceptance language.

#### F. Replace the development footer

Remove user-facing footer text such as:

- "In development".
- "Starting in Adelaide".
- "Adults 18+" as a decorative footer label.
- `v0.1.0-dev`.
- Internal environment or build information.

Replace it with a consistent Swoovr business footer.

Suggested footer:

- Terms of Use.
- Privacy.
- Community Guidelines.
- Connection & Meeting Advice.
- Support, where available.
- © Swoovr.

Keep the footer visually separated from the registration form.

It should remain at the bottom of short pages while flowing naturally below longer content.

Build versions and environment diagnostics may remain available through developer tooling or restricted administrative interfaces.

#### G. Apply consistent styling across Keycloak authentication pages

The registration experience uses Keycloak.

Review the existing Swoovr Keycloak theme and ensure the same design system is applied across relevant authentication screens, including:

- Sign in.
- Create account.
- Forgot password.
- Reset password.
- Email verification.
- Authentication errors.
- Account recovery.
- Other applicable identity-provider flows.

These screens should not look like unrelated applications.

Prefer a maintainable shared Keycloak theme using supported customisation mechanisms.

Do not unnecessarily modify Keycloak internals or replace the existing authentication architecture.

#### H. Preserve the registration and onboarding sequence

The registration redesign must not change the approved member onboarding workflow.

After initial account registration, members must complete:

1. Mobile number registration and verification.
2. Date-of-birth collection and age eligibility validation.
3. Entry into the Me / Account experience.

The registration page should collect only the information required at that stage.

Avoid asking members to enter information repeatedly.

Ensure the transition between Keycloak registration and Swoovr onboarding feels continuous and professionally integrated.

#### I. Ensure UAT matches production quality

The UAT environment should present the intended production design.

This includes:

- Complete and consistent branding.
- Professional registration and authentication layouts.
- Correct responsive behaviour.
- Appropriate finalised legal messaging.
- No user-facing development placeholders.
- No incomplete-product disclaimers.
- No unnecessary debug or build information.

The purpose of UAT is to validate the application experience members will actually receive.

Environment-specific behaviour may exist where required for testing, but it should not degrade the user-facing interface.

### Steps to reproduce

1. Open the Swoovr member web application without an authenticated session.
2. Select **Create account**.
3. Observe the Keycloak-hosted registration page.
4. Review the overall form width, spacing and visual hierarchy.
5. Observe the awkwardly positioned back arrow.
6. Review the legal links and `(draft)` labels.
7. Review the age confirmation and legal disclaimer text.
8. Observe the development-related footer.
9. Compare the registration screen against the approved Swoovr sign-in design.
10. Repeat the review using desktop, tablet and mobile viewport sizes.
11. Verify the same registration flow when initiated from iOS and Android.

### Evidence

Approved Swoovr authentication design reference:

![Approved Swoovr sign-in reference](../design/reference/screens/04-sign-in.png)

Also refer to issue intake **#9 — Login/registration screen incorrect** for related authentication styling requirements.

The existing Keycloak registration implementation can be inspected directly to reproduce the current design problems.

### Impact

**Affected users:** All new members registering for Swoovr through web or native mobile authentication flows.

**Impact:**

- Creates a poor first impression during account registration.
- Makes the application appear unfinished.
- Introduces confusion around age eligibility and legal agreements.
- Breaks visual consistency between the member application and Keycloak.
- Makes registration less intuitive across different devices.
- Reduces confidence in the quality of the application.
- Prevents UAT from accurately representing the intended production user experience.

Registration is one of the most important interactions in the member journey. Its design must reflect the same quality standard as the rest of Swoovr.

### Notes or constraints

**Design requirements**

- Follow the approved Swoovr authentication design.
- Do not invent a completely new visual identity.
- Maintain consistency with the landing page, sign-in screen and password recovery flows.
- Use responsive layouts purpose-built for desktop, tablet and mobile.
- Ensure fields and buttons have appropriate spacing and dimensions.
- Avoid default-looking identity-provider screens.
- Use concise, professional member-facing language.

**Keycloak considerations**

- Preserve existing OpenID Connect functionality.
- Use supported Keycloak theme customisation.
- Maintain correct redirects and authentication state.
- Do not expose temporary authentication request parameters in user-facing content.
- Ensure authentication error states are styled consistently.
- Test the theme in both desktop browsers and mobile authentication sessions.

**Legal and eligibility requirements**

- Keep Swoovr restricted to members aged 18 or older.
- Preserve the approved date-of-birth onboarding process.
- Do not treat an age checkbox as verified proof of age.
- Ensure required legal documents are finalised before production release.
- Do not misrepresent draft documents as binding terms.
- Preserve appropriate consent collection and version tracking.
- Keep required legal disclosures accessible.

**Related issues**

- **#7:** Public landing page redesign.
- **#8:** Unauthenticated `/account` routing.
- **#9:** Sign-in screen redesign.

These issues should share consistent branding and authentication design components but remain independently trackable.

**Suggested implementation order**

1. Review the existing Keycloak registration templates and theme.
2. Compare the implementation with the approved Swoovr design reference.
3. Establish shared authentication design tokens and layout components.
4. Redesign the desktop registration experience.
5. Implement responsive tablet and mobile layouts.
6. Correct navigation controls and form spacing.
7. Reorganise legal links and consent presentation.
8. Replace development-oriented text with appropriate member-facing content.
9. Standardise the registration footer.
10. Verify the updated theme across other Keycloak authentication screens.
11. Test registration and onboarding end-to-end.
12. Validate the final experience across supported devices and viewport sizes.

### Acceptance criteria

- [ ] Registration follows the approved Swoovr authentication design.
- [ ] The desktop page uses a balanced, professional layout.
- [ ] Tablet and mobile layouts are properly responsive.
- [ ] Native iOS and Android authentication experiences display correctly.
- [ ] The back-navigation control is clearly labelled and correctly aligned.
- [ ] Form fields and buttons use consistent sizing and spacing.
- [ ] No controls overlap or visually touch unnecessarily.
- [ ] Legal links are professionally positioned and functional.
- [ ] Required consent controls remain clearly visible.
- [ ] Draft legal labels are removed once the underlying documents are finalised.
- [ ] Age eligibility messaging is concise and understandable.
- [ ] Development disclaimers and internal version details are absent from normal member-facing pages.
- [ ] A consistent Swoovr business footer is provided.
- [ ] Keycloak authentication pages use consistent Swoovr branding.
- [ ] Existing registration, authentication and onboarding functionality remains intact.
- [ ] UAT presents the intended production-quality registration experience.

<a id="intake-11"></a>

## 11. Registration errors are difficult to identify when an email address is already registered

Implementation progress (2 November 2026): Duplicate-email error focus and accessible descriptions verified with fictional fixture. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** Keycloak registration flow (`http://localhost:8180/realms/swoovr-members-dev/`)
- **Type:** Bug
- **Priority:** High
- **GitHub issue:** [#145](https://github.com/repasscloud/wanderconnect/issues/145)
- **Tracking Q:** Q103

### Historical intake investigation — pre-merge source snapshot

Q103: Screenshot visibly contains an existing-email error; it does not establish broken registration or invitation activation. Field macro renders error text with aria-live, but prominence/focus need actual browser/assistive-technology checks. Balance recovery guidance with neutral responses and enumeration controls; beta invitation is not a new identity.

Source inspection: `containers/keycloak/themes/swoovr/login/field.ftl; containers/keycloak/themes/swoovr/login/register.ftl`.

Duplicate review: Related intake 10 and #25; neither specifically covers validation prominence/recovery.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

When attempting to register a beta-invited member using an email address that already exists in Swoovr, the registration process does not communicate the problem clearly.

The test account `fictional.alex@example.test` already exists in the system and has an associated beta enrollment record.

During registration, the application displays an error indicating that the email address is already registered. However, the error message is too small, poorly positioned and does not attract sufficient attention.

As a result, the registration appears to fail without a clear explanation, potentially giving the impression that the application is looping or experiencing a technical problem.

The issue was initially mistaken for a registration failure because the existing-account error was not sufficiently visible.

**Specific problems identified:**

1. The existing-email error is not visually prominent.
2. The message is easy to overlook when completing the registration form.
3. The page does not provide sufficiently clear guidance on what the member should do next.
4. The error presentation does not meet the expected Swoovr design standards.
5. The distinction between a beta invitation and an existing registered account is not obvious.
6. Members may repeatedly attempt registration without understanding why it cannot proceed.

This is primarily a registration error presentation and user experience issue, rather than a confirmed failure of account creation or beta enrollment.

### What I expected

The registration interface should clearly communicate validation failures and provide actionable guidance.

When an email address is already associated with an account, the member should immediately understand that they cannot create a second account using that address.

#### A. Display prominent registration errors

Registration errors should be displayed in a clearly visible error summary near the top of the registration form.

The error summary should:

- Be positioned below the page heading and above the relevant input fields.
- Use a visually distinct error treatment consistent with Swoovr's design system.
- Have readable text, appropriate padding and spacing.
- Include an appropriate error icon where useful.
- Remain visible after the form is submitted.
- Clearly explain what went wrong.

Do not rely on small, low-contrast helper text that can be overlooked.

For validation errors, the affected field should also display an inline error message.

#### B. Improve the existing-email error message

Suggested member-facing message:

**Unable to create account**

"An account may already exist with this email address. Try signing in or resetting your password."

Provide clearly labelled actions:

- **Sign in**
- **Reset password**

The links must preserve the appropriate authentication client and redirect flow.

Do not require the member to restart the entire registration journey manually.

**Security consideration:** Account-existence messaging must follow the established account-enumeration protection policy. Where revealing whether an email is registered would create an information-disclosure risk, use generic messaging and a secure account-recovery flow rather than explicitly confirming account existence.

#### C. Highlight the affected field

If the registration form identifies an email validation error:

- Highlight the Email field using the standard error style.
- Display a short explanation immediately below the field.
- Ensure the error is programmatically associated with the input.
- Avoid displaying unrelated technical error codes.

For example:

**Email**

"Unable to register with this email address. Try signing in instead."

The error summary and inline validation should work together rather than showing contradictory messages.

#### D. Improve validation feedback

Apply consistent error presentation across registration fields, including:

- Required first name.
- Invalid email format.
- Existing or unavailable email address.
- Missing age declaration.
- Invalid or expired registration session.
- Server-side registration failures.

Where multiple validation errors occur, display a concise error summary and highlight the affected fields.

Ensure errors are not hidden below the visible viewport.

Focus should move appropriately to the error summary or first invalid field after unsuccessful submission, with accessible announcements for screen readers.

#### E. Preserve form input after validation failure

When registration validation fails:

- Retain the member's entered first name.
- Retain the entered email address.
- Preserve applicable non-sensitive selections where appropriate.
- Display the error without unnecessarily resetting the form.
- Do not create a duplicate account.
- Do not trigger an uncontrolled authentication redirect loop.

The member should be able to correct the problem or choose an alternative action without re-entering everything.

#### F. Clarify beta enrollment versus account registration

A beta invitation or enrollment record is not necessarily equivalent to an active authenticated member account.

The application should distinguish between:

1. A beta invitation or pending enrollment for an email address.
2. An existing identity-provider account.
3. An existing Swoovr member profile.
4. An account that has completed onboarding.

Confirm that pending beta enrollment does not incorrectly prevent an invited member from registering when no identity account exists.

If the identity account already exists, provide an appropriate sign-in or account-recovery path rather than attempting duplicate registration.

The beta enrollment should be associated with the correct member identity according to the existing enrollment workflow.

Do not automatically merge accounts solely because email addresses match without appropriate identity verification.

#### G. Ensure consistent styling across Keycloak registration

The registration form is served through Keycloak and must follow Swoovr's established authentication design.

Error messages should use the same visual language as:

- Sign-in errors.
- Password recovery errors.
- Email verification errors.
- Session expiration messages.
- Account restrictions.

Avoid default-looking Keycloak error text that does not match the rest of the application.

This should be coordinated with issue #10, which addresses the wider registration screen redesign.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Confirm that `fictional.alex@example.test` exists and has a beta enrollment record.
3. Open a new private browser session.
4. Navigate to `http://localhost:5081/`.
5. Open the member authentication flow.
6. Select **Create an account**.
7. Enter `Alex` in the First Name field.
8. Enter `fictional.alex@example.test` in the Email field.
9. Select the **I'm 18 or over** confirmation.
10. Click **Create an account**.
11. Observe the registration error message.
12. Confirm that the existing-email error is difficult to identify and does not provide sufficiently prominent recovery guidance.

### Evidence

Evidence withheld from publication: `11-fictional-alex-account-cannot-register.png` (authentication transaction identifiers in browser URL). See [evidence review](evidence/2026-11-01-issue-intake/README.md).

The screenshot demonstrates the current registration error presentation.

### Impact

**Affected users:** New members, beta-invited participants and existing members who attempt to register again.

**Impact:**

- Members may not understand why registration failed.
- Small or poorly positioned errors can be overlooked.
- Members may repeatedly submit the same registration details.
- Existing members may not realise they should sign in or recover their account.
- Beta participants may mistakenly believe their invitation or registration is broken.
- Support requests may increase due to unclear authentication messaging.
- The registration process appears less reliable than it actually is.

This is a high-priority issue because registration is a critical entry point and unclear errors can directly prevent members from successfully accessing Swoovr.

### Notes or constraints

**Confirmed observation**

The test email address already has an account.

The reported registration behaviour therefore does not, by itself, establish a backend account creation defect.

The confirmed problem is the poor visibility and presentation of the existing-account error.

**Investigation requirements**

- Review how Keycloak registration validation errors are generated.
- Review how those errors are rendered in the Swoovr Keycloak theme.
- Check whether error messages are visible without scrolling.
- Confirm whether failed submissions preserve entered form values.
- Verify that existing-account errors do not cause redirect loops.
- Verify that beta invitations are correctly associated with existing or newly created member identities.
- Confirm the appropriate account-enumeration protection policy.
- Review accessibility behaviour for validation summaries and inline errors.

**Design requirements**

- Follow the approved Swoovr authentication styling.
- Use clear, accessible error summaries.
- Provide inline validation near affected fields.
- Use consistent spacing, typography and contrast.
- Keep recovery actions clearly visible.
- Avoid raw technical messages and default identity-provider styling.
- Ensure errors display correctly on desktop, tablet and mobile.

**Related issues**

- **#7:** Public landing page redesign.
- **#8:** Unauthenticated account routing.
- **#9:** Sign-in screen redesign.
- **#10:** Registration screen redesign.

The validation improvements should reuse the same design components introduced for the wider authentication redesign.

### Acceptance criteria

- [ ] Registration errors are prominently displayed near the top of the form.
- [ ] Affected fields display clear inline validation messages.
- [ ] Existing-email registration attempts provide understandable guidance.
- [ ] Sign-in and password recovery actions are available where appropriate.
- [ ] Error messages comply with the account-enumeration protection policy.
- [ ] Validation errors do not appear only as small, easily overlooked text.
- [ ] Form values are preserved appropriately after validation failure.
- [ ] Registration errors do not cause uncontrolled redirect loops.
- [ ] Duplicate member identities are not created.
- [ ] Pending beta invitations do not incorrectly block valid account registration.
- [ ] Existing accounts remain correctly associated with their beta enrollment records.
- [ ] Validation errors use consistent Swoovr styling across Keycloak authentication pages.
- [ ] Error summaries and inline validation are accessible to keyboard and screen-reader users.
- [ ] Desktop, tablet and mobile layouts display errors correctly.
- [ ] Automated tests cover duplicate-email registration, invalid inputs and beta-invited account scenarios.

<a id="intake-12"></a>

## 12. Redesign Forgot Password screen to match Swoovr authentication standards

Implementation progress (2 November 2026): Reset frame and email input/autocomplete corrected without changing provider token handling. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** Keycloak password recovery flow (`http://localhost:8180/realms/swoovr-members-dev/`)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#146](https://github.com/repasscloud/wanderconnect/issues/146)
- **Tracking Q:** Q104

### Historical intake investigation — pre-merge source snapshot

Q104: Custom reset form uses shared theme, email input and send-reset-link action; this is a refinement request rather than evidence of an unthemed default provider page. Screenshot supports layout criticism, not failed email delivery. Preserve provider reset token semantics.

Source inspection: `containers/keycloak/themes/swoovr/login/login-reset-password.ftl; containers/keycloak/themes/swoovr/login/template.ftl; containers/keycloak/themes/swoovr/login/resources/css/swoovr.css`.

Duplicate review: Related intake 9/13; form styling and post-submit confirmation have different acceptance.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Swoovr Forgot Password screen does not meet the expected visual or usability standards for the application.

The page appears unfinished and is inconsistent with the previously approved Swoovr authentication design.

The layout, spacing, typography and navigation do not provide the professional, polished experience expected of a production-ready dating and social discovery platform.

The page also includes unnecessary development-oriented messaging and navigation elements that distract from the password recovery task.

Specific issues include:

1. **Inconsistent authentication styling.** The Forgot Password page does not sufficiently resemble the approved Swoovr sign-in design.
2. **Poor layout and proportions.** The form does not make effective use of desktop screen space and lacks an appropriately balanced presentation.
3. **Unnecessary development messaging.** Text implying the application is incomplete or under development should not appear in the member-facing experience.
4. **Unclear navigation hierarchy.** The password recovery screen should only provide a route back to Sign in, rather than offering additional navigation branches.
5. **Inconsistent spacing and controls.** Buttons, fields, descriptions and navigation elements should follow the same design standards as other authentication screens.
6. **Insufficient responsive design.** Desktop, tablet and mobile layouts should be purpose-built rather than reusing an awkwardly constrained form.

### What I expected

The Forgot Password screen should be a dedicated, professionally designed password recovery page using the approved Swoovr authentication design system.

#### A. Apply the approved authentication design

Use the established Swoovr sign-in design:

`../design/reference/screens/04-sign-in.png`

Maintain consistency with:

- Swoovr wordmark and branding.
- Typography and colours.
- Form components.
- Input and button styling.
- Spacing and alignment.
- Error and validation presentation.
- Page background and footer.

The screen should look like a natural continuation of Sign in, not a separate identity-provider application.

#### B. Simplify the screen

The Forgot Password page should contain only the information necessary to request a password reset.

Suggested content:

**Forgot your password?**

"Enter your email address and we'll send you instructions to reset your password, if an account is associated with it."

**Email address**

An appropriately sized email input.

**Send reset instructions**

The primary action.

**Back to sign in**

A secondary navigation link.

Do not display an additional Create account action or other unnecessary authentication navigation on this page.

The Sign in screen is responsible for offering access to both registration and password recovery.

#### C. Establish a clear authentication navigation flow

The expected navigation structure is:

**Sign in**

- Create account → Registration.
- Forgot password → Password recovery.

**Forgot password**

- Back to sign in → Sign in.
- Send reset instructions → Dedicated request confirmation screen.

**Request confirmation**

- Back to sign in → Sign in.
- Support link, where appropriate.

Registration and password recovery should not link directly to one another.

Avoid circular or confusing authentication navigation.

#### D. Correct form dimensions and button sizing

**Desktop**

- Use a balanced, appropriately constrained form.
- Avoid excessively wide inputs and buttons.
- Maintain consistent spacing between fields.
- Use a comfortable maximum width.
- Ensure the layout feels intentionally designed for desktop.

**Tablet and mobile**

- Adapt the layout to available screen space.
- Maintain appropriate touch targets.
- Avoid controls touching or overlapping.
- Ensure the form remains usable when the keyboard is open.
- Respect mobile safe areas.

Buttons may occupy the available form width on mobile, but should not unnecessarily span a large desktop viewport.

#### E. Remove development and placeholder messaging

Remove user-facing text such as:

- "In development".
- "Work in progress".
- Internal build numbers.
- Draft or incomplete-product notices.
- Unnecessary test environment information.

UAT should represent the intended production design.

The user-facing screen should not advertise the application's development state.

#### F. Use a consistent authentication footer

Use the same professional footer as the Sign in and Registration screens.

Include appropriate links to:

- Terms of Use.
- Privacy.
- Community Guidelines.
- Connection & Meeting Advice.
- Support, where available.

These are footer links, not additional authentication navigation branches.

Do not include internal version numbers or development status.

#### G. Improve validation and error handling

The form should clearly handle:

- Missing email address.
- Invalid email format.
- Network failures.
- Temporary password recovery service failures.
- Expired authentication sessions.

Validation errors should appear near the affected field and, where useful, in a clearly visible error summary.

Use the same validation components introduced for issue #11.

Do not reveal whether an email address is associated with an existing account.

#### H. Preserve secure password recovery behaviour

The visual redesign must not weaken password recovery security.

- Use the existing supported Keycloak reset credentials flow.
- Preserve token expiry and single-use behaviour.
- Apply existing rate limiting and abuse controls.
- Do not reveal whether an account exists.
- Do not expose sensitive authentication transaction information in the interface.
- Do not automatically authenticate the user merely because a reset request was submitted.

The confirmation experience after submission is addressed separately in issue #13.

### Steps to reproduce

1. Open the Swoovr member web application without an authenticated session.
2. Navigate to Sign in.
3. Select **Forgot password**.
4. Observe the current Keycloak password recovery page.
5. Review the page layout, form width, typography and button sizing.
6. Review the available navigation links.
7. Observe development-oriented text and footer content.
8. Compare the appearance with the approved Swoovr sign-in reference.
9. Repeat at desktop, tablet and mobile viewport sizes.

### Evidence

Evidence withheld from publication: `12-forgot-password-screen-is-ugly.png` (authentication transaction identifiers in browser URL). See [evidence review](evidence/2026-11-01-issue-intake/README.md).

Approved design reference:

![Approved Swoovr sign-in design](../design/reference/screens/04-sign-in.png)

### Impact

**Affected users:** Members attempting to recover access to their accounts.

**Impact:**

- Password recovery does not feel consistent with the rest of Swoovr.
- The interface appears unfinished.
- Members may be confused by unnecessary navigation options.
- Poor visual hierarchy makes the recovery process less intuitive.
- Inconsistent styling reduces confidence in the authentication experience.
- UAT does not accurately represent the intended production-quality interface.

Password recovery is a security-sensitive account function and should be clear, predictable and professionally presented.

### Notes or constraints

**Design requirements**

- Reuse the approved Swoovr authentication design.
- Keep the recovery form concise.
- Provide only a Back to sign in navigation action within the authentication flow.
- Keep required business and legal links in the standard footer.
- Avoid oversized buttons and awkward desktop layouts.
- Maintain accessible validation and keyboard navigation.
- Ensure consistent behaviour on desktop, tablet, mobile web, iOS and Android.

**Keycloak considerations**

- Preserve the existing Keycloak password reset functionality.
- Use supported Keycloak theme customisation.
- Avoid modifying core Keycloak authentication behaviour unnecessarily.
- Maintain secure redirect and recovery token handling.
- Ensure password reset requests cannot be used to enumerate registered accounts.

**Related issues**

- **#7:** Public landing page.
- **#8:** Unauthenticated account routing.
- **#9:** Sign-in design.
- **#10:** Registration design.
- **#11:** Registration validation presentation.
- **#13:** Password reset request confirmation.

The authentication screens should share one coherent design system.

### Acceptance criteria

- [ ] Forgot Password follows the approved Swoovr authentication design.
- [ ] The page contains a clear heading, description, email input and primary action.
- [ ] Back to sign in is the only authentication navigation link on the recovery form.
- [ ] No unnecessary registration navigation appears on the recovery screen.
- [ ] Desktop layout is properly proportioned.
- [ ] Tablet and mobile layouts are responsive.
- [ ] Form controls have consistent dimensions and spacing.
- [ ] Validation errors are clearly presented.
- [ ] Development messaging and internal version information are removed.
- [ ] The standard Swoovr footer is displayed.
- [ ] Password recovery remains secure and does not disclose account existence.
- [ ] Successful submission navigates to the dedicated confirmation experience defined in issue #13.

<a id="intake-13"></a>

## 13. Replace password reset notification with dedicated confirmation screen and prevent account enumeration

Implementation progress (2 November 2026): Dedicated neutral acknowledgement verified for enabled/absent/disabled fixtures; timing resistance remains unverified. See [implementation and actual-browser evidence](ISSUE-AUTH-FIXES.md). Full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero build warnings/errors); Merged in [PR #153](https://github.com/repasscloud/wanderconnect/pull/153); the issue stays open pending outstanding acceptance.

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** Keycloak password recovery confirmation flow (`http://localhost:8180/realms/swoovr-members-dev/`)
- **Type:** Bug
- **Priority:** High
- **GitHub issue:** [#147](https://github.com/repasscloud/wanderconnect/issues/147)
- **Tracking Q:** Q105

### Historical intake investigation — pre-merge source snapshot

Q105: Screenshot shows a generic email acknowledgement embedded above a sign-in form, with a populated masked password field. It does not prove account enumeration, password disclosure, email delivery or automatic sign-in. Compare existing/nonexistent/disabled addresses including timing and email effects using safe fixtures; keep confirmation neutral.

Source inspection: `containers/keycloak/themes/swoovr/login/login.ftl; containers/keycloak/themes/swoovr/login/template.ftl; containers/keycloak/realms/swoovr-members-dev.json`.

Duplicate review: Related intake 12; no existing issue specifically covers reset confirmation or comparative enumeration testing.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

After submitting a password reset request, the application redirects the member to a sign-in screen and displays the notification:

> You should receive an email shortly with further instructions.

The email address entered during the password reset request is also automatically populated into the sign-in form.

This is not the expected behaviour.

The current implementation has several problems.

**1. There is no dedicated confirmation screen**

The member is returned to a sign-in page rather than seeing a clear confirmation that the password reset request has been processed.

The interface does not distinguish between requesting a reset and actually completing one.

**2. The email address is automatically populated**

The email address supplied during password recovery appears in the sign-in field.

This should not happen as part of the password recovery workflow.

A password reset request should not automatically prepare the sign-in form using the submitted email address.

**3. The notification wording is ambiguous**

The message:

"You should receive an email shortly with further instructions."

may lead members to assume the submitted email address is recognised by Swoovr.

Although the wording does not explicitly confirm account existence, the recovery interface should make the conditional nature of the process clear.

**4. The screen encourages an immediate return to sign-in**

Returning directly to the login form, particularly with the email address populated, creates a confusing workflow.

The member has just requested a reset and should instead be shown what to do next.

**5. The confirmation is not presented consistently**

The current notification appears within a sign-in interface rather than on a dedicated, properly styled Swoovr confirmation page.

This is inconsistent with the intended authentication experience.

### What I expected

After submitting a password reset request, the member should be taken to a dedicated Swoovr-branded confirmation screen.

The confirmation must use generic wording and must not disclose whether the submitted email address belongs to an existing account.

#### A. Introduce a dedicated confirmation screen

After a syntactically valid password reset request is accepted for processing, display a dedicated confirmation page.

Suggested content:

**Check your email**

"If an account exists with the email address you entered, we'll send password reset instructions to that address."

Supporting text:

"Please check your inbox and spam folder. If you need help accessing your account, contact Swoovr Support."

Actions:

**Back to sign in**

**Contact support** — `https://swoovr.com/support`

The support link should open the official Swoovr support page.

Do not claim that a message was definitely sent.

#### B. Do not reveal account existence

The password recovery flow must not disclose whether a submitted email address is registered.

For example, do not display different public messages such as:

- "Account found. Email sent."
- "No account exists with that email."
- "This user has not registered."

The confirmation message should remain the same regardless of whether the account exists.

The application should also avoid exposing account existence through:

- Different HTTP responses.
- Distinct error messages.
- Different redirects.
- Obvious response-time differences, where reasonably preventable.
- Other observable recovery-flow behaviour.

Rate limiting and abuse protection must remain enforced.

Invalid email formats and genuine technical failures may still display appropriate errors, without revealing membership information.

#### C. Do not prepopulate the sign-in email field

The email address entered during password recovery should not automatically populate the sign-in form.

Expected behaviour:

1. Member enters an email address on Forgot Password.
2. Member submits the request.
3. Member sees the generic confirmation screen.
4. The sign-in form is not displayed on the confirmation screen.
5. If the member selects Back to sign in, the sign-in form opens without carrying over the recovery email.

This requirement applies specifically to data transferred from the password recovery process.

Other intentional sign-in conveniences, such as a previously selected remembered account, should be evaluated separately and must not override the expected recovery behaviour.

Do not automatically initiate sign-in after a reset request.

#### D. Separate reset requests from completed password resets

The interface should distinguish between:

**Password reset requested**

The member submitted their email address.

No password has been changed at this stage.

**Password reset completed**

The member followed a valid reset link and successfully established a new password.

These are different outcomes and should not use the same confirmation page or message.

A successful password change should have its own clearly labelled completion state, consistent with the authentication design.

#### E. Preserve privacy during recovery

The confirmation page should not unnecessarily display the submitted email address.

Avoid including the email address in:

- Query string parameters.
- Public page URLs.
- Visible confirmation messages.
- Client-side diagnostics.
- Analytics events.

If internal correlation is necessary, use an appropriate opaque identifier rather than exposing the email address.

Do not retain the submitted email address in client-side state longer than necessary for the recovery process.

#### F. Maintain consistent authentication navigation

Expected recovery journey:

**Sign in**

- Forgot password → Password recovery.
- Create account → Registration.

**Forgot password**

- Enter email.
- Send reset instructions.
- Back to sign in.

**Reset request confirmation**

- Generic confirmation message.
- Back to sign in.
- Contact support.

**Password reset completed**

- Password successfully changed.
- Return to sign in, or follow an explicitly approved secure authentication flow.

Do not redirect the member to the sign-in form immediately after requesting a reset.

#### G. Apply the approved Swoovr design

The confirmation screen should follow the established authentication styling.

Requirements:

- Swoovr wordmark and branding.
- Clear confirmation heading.
- Readable supporting text.
- Appropriate iconography, where useful.
- Properly sized navigation buttons.
- Consistent spacing and alignment.
- Standard business footer.
- Responsive desktop, tablet and mobile presentation.

Avoid displaying a small notification above an otherwise unrelated login form.

The confirmation page should feel like a complete screen with a clear purpose.

#### H. Preserve secure recovery behaviour

The change should not weaken Keycloak's existing password recovery protections.

Maintain:

- Single-use password reset tokens.
- Appropriate token expiry.
- Existing email delivery protections.
- Rate limiting.
- Secure reset links.
- Appropriate authentication session handling.
- Audit records for recovery requests and successful password changes.

Do not automatically sign in a member simply because they requested a password reset.

Ensure the confirmation screen only represents that the request was accepted for processing, not that email delivery succeeded or the password was changed.

#### I. Handle delivery failures appropriately

The member-facing confirmation should remain generic when an account does not exist.

However, genuine infrastructure or email delivery failures must not be silently ignored internally.

Record appropriate diagnostic information without exposing sensitive account details to unauthorised users.

Support staff should have suitable means to investigate delivery problems through authorised systems.

Do not expose internal email-provider errors or registered-account status in the public confirmation screen.

### Steps to reproduce

1. Open the Swoovr member web application without an authenticated session.
2. Navigate to Sign in.
3. Select Forgot password.
4. Enter an email address.
5. Submit the password reset request.
6. Observe that the application returns to the sign-in screen.
7. Observe the notification: "You should receive an email shortly with further instructions."
8. Observe that the submitted email address is automatically populated into the sign-in field.
9. Repeat using an email address that is not registered with Swoovr.
10. Compare the resulting messages and navigation behaviour.

### Evidence

Evidence withheld from publication: `13-forgot-password-screen-notification-incorrect.png` (authentication transaction identifiers and populated masked credential field). See [evidence review](evidence/2026-11-01-issue-intake/README.md).

The screenshot demonstrates the current notification presentation and sign-in form state following a password reset request.

### Impact

**Affected users:** Members requesting password recovery and visitors entering email addresses into the recovery form.

**Impact:**

- Members do not receive a clear confirmation of their recovery request.
- The application returns them to sign-in prematurely.
- Automatically populating the email field creates unnecessary confusion.
- Members may incorrectly assume that the submitted email address has been verified as registered.
- Inconsistent messages may increase account enumeration risk if different recovery outcomes are exposed.
- The interface does not clearly explain the next step.
- The recovery experience appears incomplete and inconsistent with Swoovr branding.

A dedicated confirmation screen improves clarity while strengthening the consistency of account privacy protections.

### Notes or constraints

**Keycloak implementation**

Investigate the supported Keycloak reset-credentials flow and theme templates.

Where possible, implement the confirmation state through supported Keycloak customisation rather than introducing an unnecessary replacement authentication system.

If a dedicated application route is used, preserve secure authentication redirects and avoid leaking recovery information.

The original URL contains temporary authentication transaction parameters and should not be used as a permanent issue reference.

**Security requirements**

- Do not expose whether an account exists.
- Do not display the submitted email address on the confirmation page.
- Do not carry the recovery email into the sign-in form.
- Do not automatically authenticate after a reset request.
- Keep reset tokens single-use and time-limited.
- Maintain rate limiting and abuse protections.
- Preserve useful internal audit and diagnostic information without disclosing sensitive data.

**Design requirements**

- Use the established Swoovr authentication theme.
- Display a dedicated confirmation screen rather than a notification within Sign in.
- Provide clear Back to sign in and Contact support actions.
- Use the official support URL: `https://swoovr.com/support`.
- Keep authentication navigation simple and predictable.
- Ensure desktop, tablet and mobile layouts are properly designed.
- Remove development-oriented messaging from the confirmation page.

**Related issues**

- **#7:** Public landing page.
- **#8:** Unauthenticated account routing.
- **#9:** Sign-in design.
- **#10:** Registration design.
- **#11:** Registration validation and error presentation.
- **#12:** Forgot Password screen redesign.

These issues should share the same visual design system and authentication navigation conventions.

**Suggested implementation order**

1. Review the current Keycloak password recovery flow.
2. Identify why the submitted email is carried into Sign in.
3. Remove unintended email prepopulation.
4. Implement a dedicated reset-request confirmation state.
5. Apply generic, account-existence-neutral messaging.
6. Add Back to sign in and Contact support actions.
7. Apply the Swoovr authentication design system.
8. Validate recovery behaviour for registered and unregistered addresses.
9. Verify reset token handling, rate limiting and security controls.
10. Test desktop, tablet, mobile web and native app authentication flows.

### Acceptance criteria

- [ ] Password reset requests display a dedicated confirmation screen.
- [ ] The confirmation screen is separate from Sign in.
- [ ] The confirmation message does not confirm account existence.
- [ ] Registered and unregistered addresses receive equivalent public confirmation behaviour.
- [ ] The submitted email is not displayed on the confirmation page.
- [ ] The submitted email is not automatically transferred to the sign-in form.
- [ ] Back to sign in navigates to the correct authentication screen.
- [ ] Contact support navigates to `https://swoovr.com/support`.
- [ ] Password reset requests do not automatically authenticate members.
- [ ] Reset-request confirmation is distinct from successful password-change confirmation.
- [ ] The screen follows Swoovr's approved authentication design.
- [ ] Desktop, tablet and mobile layouts are responsive and professional.
- [ ] Development and internal diagnostic messaging are absent from the public interface.
- [ ] Existing Keycloak recovery security controls remain intact.
- [ ] Tests cover registered addresses, unregistered addresses, invalid inputs and recovery failures.

<a id="intake-14"></a>

## 14. Redesign Swoovr Me / Account page for a desktop-first experience and clearer onboarding flow

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: grouped settings and truthful account heading. Onboarding order and revision-persisted safety-dismissal proposals remain deferred. Real Edge with gates enabled and synthetic fixtures passed; full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero warnings/errors). See [member evidence and limitations](ISSUE-MEMBER-FIXES.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `/account` (`http://localhost:5081/account`)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#148](https://github.com/repasscloud/wanderconnect/issues/148)
- **Tracking Q:** Q106

### Historical intake investigation — pre-merge source snapshot

Q106: Signed-in screenshot shows dense account/safety/status/settings composition. Recent responsive work already widened account layout; verify current baseline before attributing all desktop problems to old width. OPEN: fixed phone-then-DOB order and revision-persisted safety dismissal need reconciliation with configured backend gates and existing safety notices; do not weaken them.

Source inspection: `src/Swoovr.Web.Client/Account/MemberAccountPanel.razor; src/Swoovr.Web/Components/Pages/Account.razor; docs/delivery/MEMBER-RESPONSIVE-LAYOUT.md`.

Duplicate review: Related #50 (standing), #121 (safety advice), #48; none covers this account/onboarding composition.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current Swoovr "Me / Account" page does not provide a clear, intuitive or engaging account management experience.

Although the page is visually consistent with Swoovr's dark theme, its layout feels more like an administrative settings dashboard than the account area of a dating, meeting and social discovery platform.

There is too much information presented simultaneously, several elements are positioned incorrectly, and the page lacks a logical visual hierarchy.

Specific observations:

1. **Too much information on the main account page.** Safety advice, account verification details, mobile number management, profile options, legal documents and other settings all compete for attention.

2. **No clear flow or hierarchy.** The member is presented with multiple cards and a long settings menu without an obvious starting point or meaningful grouping.

3. **The "Before you connect or meet" notice is permanently visible.** This information is useful, but it occupies substantial space and should not need to be read every time the member opens their account.

4. **The account setup requirements are unclear.** Members must provide a mobile number before they can access other areas of Swoovr, but this restriction is not clearly communicated when they sign in or attempt to navigate.

5. **Redundant account information is prominently displayed.** The card showing "Signed in with", "Email", "18+ confirmation" and "Member area" provides little practical value in this position.

6. **Legal and informational pages are mixed into account settings.** Terms of Use, Community Guidelines, Connection and Meeting Advice, and Privacy Notice are currently presented as menu items alongside member functionality.

7. **Coupon code redemption is in the wrong location.** A coupon code input should not be part of the primary account experience.

8. **The desktop interface appears to inherit the mobile layout.** The desktop experience does not make effective use of the available screen space or provide a navigation structure appropriate for desktop users.

9. **Desktop navigation and account navigation are not clearly separated.** The primary Explore, Connections, Chats and Me navigation is shared across platforms, but the account page needs its own desktop-appropriate navigation.

### What I expected

The Swoovr member experience should have a clearly defined onboarding sequence, followed by a dedicated, professionally designed account area.

Swoovr is launching primarily as a dating platform, with social discovery, friendships and additional meeting modes. The account experience should reflect that purpose rather than resembling an administrative application.

#### A. Mandatory account setup flow

When a member signs in for the first time, they should complete the required setup steps in this order:

1. **Add and verify mobile number**
   - Prompt the member to enter their mobile number.
   - Send and verify a six-digit OTP.
   - Explain why verification is required.
   - Prevent navigation to restricted member features until verification succeeds.

2. **Set date of birth**
   - Request the member's date of birth.
   - Validate that the member meets Swoovr's minimum age requirement of 18.
   - Explain any restrictions or consequences associated with an ineligible age.
   - Do not treat a simple 18+ confirmation as age or identity verification.

3. **Continue to Me / Account**
   - Once both mandatory steps are completed, direct the member to the account page.
   - Clearly show any remaining optional or required profile setup tasks.
   - Provide an obvious next action for completing their profile and becoming ready to connect.

The onboarding sequence should be explicit, with a progress indicator such as "Step 1 of 2" and "Step 2 of 2".

If a member has not completed mobile verification or date-of-birth setup, attempts to navigate elsewhere must display a clear explanation of what is required and provide a direct action to complete it.

The member should not simply encounter disabled navigation or an unexplained redirect.

#### B. Redesign the desktop account experience

The desktop layout should be designed independently of the mobile layout rather than simply expanding the mobile interface.

Suggested approach:

- Retain the primary desktop navigation: Explore, Connections, Chats and Me.
- Introduce dedicated account navigation, potentially using a collapsible sidebar or hamburger menu.
- Keep the main content area focused on the section currently selected.
- Avoid displaying every account setting simultaneously.
- Use the additional desktop width to create a balanced, spacious layout.
- Clearly distinguish between account management, profile configuration, safety, and membership functionality.

A persistent sidebar may be preferable to a hamburger menu on larger desktop screens, because settings can remain discoverable without requiring additional clicks. A collapsible sidebar would accommodate narrower desktop windows.

**Suggested desktop account sections:**

| Section | Purpose |
| --- | --- |
| Overview | Welcome, profile completion, account readiness and relevant actions |
| My profile | Profile details, personal information, photos and active modes |
| Discovery preferences | Who I'd like to meet and related preferences |
| Travel | Current location preferences, planned travel and visibility |
| Verification | Mobile, email, photo and ID verification status |
| Membership & billing | Membership tier, subscription, payments and coupon redemption |
| Notifications | Notification preferences |
| Privacy & safety | Privacy controls, blocking, safety options and account standing |
| Account settings | Account management, security, appearance and data controls |

This is a proposed information architecture, not a requirement to create these exact menu names. Existing features should be mapped into sensible groups without duplicating functionality.

The overview should be concise and action-oriented, rather than becoming another long settings page.

#### C. Make the "Before you connect or meet" notice dismissible

Retain the existing safety notice because its message is valuable.

However:

- Display it as an introductory notice when a member first reaches their account page after completing the required onboarding steps.
- Provide a clearly visible dismiss or "Got it" action.
- Once dismissed, do not show it automatically on subsequent visits.
- Persist acknowledgement server-side so it applies across devices and sessions.
- Keep the information available through the dedicated Safety area or Connection and Meeting Advice page.
- If the wording is materially updated in future, support a new notice revision so members can be shown important changes.

The notice should be visually polished and compact, without dominating the main page.

#### D. Remove redundant information from the account overview

The following information does not need to appear prominently on the account landing page:

- Signed in with: Password or passkey
- Email: Verified
- 18+ confirmation: Confirmed when you joined
- Member area: Allowed

Where appropriate, relocate relevant information to account security or verification settings.

Verification information should be presented meaningfully, using actionable status indicators where necessary.

For example, an unverified mobile number should be highlighted with a "Verify now" action rather than buried inside a generic information card.

#### E. Move legal and informational pages to a dedicated desktop footer

The following links should appear in a dedicated, consistently positioned footer on desktop:

- Terms of Use
- Community Guidelines
- Connection & Meeting Advice
- Privacy

These links must navigate to their respective pages.

Use **"Privacy"** rather than "Privacy Notice" for the visible footer label.

The footer should:

- Remain available throughout the signed-in desktop experience.
- Be visually separated from the account management navigation.
- Stay at the bottom of the page when content is short, without overlapping or hiding content on longer pages.
- Work consistently across appropriate desktop pages.

Avoid duplicating these links within the primary account settings menu unless a specific contextual reason exists.

#### F. Move coupon redemption to Membership & Billing

Coupon code entry should not appear in the primary account overview.

Move it into a dedicated membership or billing area where the member can:

- View their current membership tier.
- Review available membership options.
- Manage subscription and billing details, where applicable.
- Redeem eligible promotional or coupon codes.
- See clear confirmation of the benefit and any applicable conditions before redemption.

Coupon redemption should feel like an account benefit, not an unrelated form field.

#### G. Maintain distinct desktop and mobile navigation experiences

The four primary navigation destinations remain:

- Explore
- Connections
- Chats
- Me

On native iOS and Android applications, and on mobile web, these should remain accessible using the mobile bottom navigation pattern.

For desktop web:

- Preserve these destinations in the primary desktop navigation.
- Use a desktop-appropriate layout, such as the existing top navigation.
- Provide a separate, coherent account navigation system within Me.
- Do not directly reuse mobile page composition when it results in awkward desktop layouts.

Components and business logic may be shared, but layout and interaction should be responsive to the platform and viewport.

The overall experience should feel intentionally designed for desktop.

### Steps to reproduce

1. Launch the Swoovr member web application.
2. Sign in using a test member account.
3. Navigate to `http://localhost:5081/account`.
4. Observe the two-column layout, with several informational cards on the left and a long list of account links on the right.
5. Review the permanent "Before you connect or meet" card and the redundant account status information.
6. Scroll through the page and locate the mobile number section and coupon code functionality.
7. Review the positioning of legal and informational links.
8. Attempt to access Explore, Connections or Chats using an account that has not completed mandatory mobile verification.
9. Observe whether the restriction and required next step are clearly communicated.
10. Compare the desktop presentation with the intended mobile navigation experience.

### Evidence

Screenshot provided of the existing Swoovr Me / Account page, captured on desktop using Microsoft Edge at:

`http://localhost:5081/account`

Visible issues include:

- Excessive information density.
- Weak distinction between account setup and account management.
- Permanent safety introduction card.
- Redundant authentication and eligibility status card.
- Unstructured account navigation.
- Legal links mixed with functional account settings.
- Poor prioritisation of important member actions.

Additional screenshots should be captured when reviewing incomplete onboarding states and navigation restrictions.

### Impact

**Affected users:** All Swoovr members using the member web application, particularly desktop users and newly registered members.

**Impact:**

- New members may not understand the mandatory setup process.
- Mobile verification restrictions may appear confusing or arbitrary.
- Members may struggle to identify the next action needed to complete their account.
- Important account functionality becomes harder to discover.
- Excessive safety and administrative content competes with the core dating and discovery experience.
- The desktop application feels less polished and engaging than it should.
- Members may abandon onboarding or account setup due to unclear progression.

This is a high-priority improvement because the account experience is one of the first areas new members encounter.

### Notes or constraints

**Product direction**

Swoovr should feel like a modern dating and social discovery platform, not a business administration dashboard.

The experience should prioritise:

1. Getting the member set up.
2. Helping the member complete their profile.
3. Making them ready to discover and connect with others.
4. Keeping account, subscription, privacy and safety settings accessible without overwhelming them.

**Implementation considerations**

- The desktop account experience should have its own responsive information architecture.
- Shared components must not force desktop pages to imitate mobile layouts.
- Maintain consistency with Swoovr's existing branding, dark theme and overall design system.
- Preserve the existing mandatory mobile verification gate.
- Ensure age eligibility checks are enforced server-side.
- Keep mandatory onboarding states separate from optional profile completion.
- Persist safety notice acknowledgement in the database, not only browser storage.
- Keep navigation restrictions and feature availability enforced by the backend, not only by the UI.
- Do not remove existing features; relocate or restructure them appropriately.
- Make desktop account navigation keyboard-accessible and compatible with assistive technologies.
- Existing mobile apps and mobile web should retain a mobile-appropriate experience.
- Review the final design at multiple desktop and mobile viewport sizes.

**Suggested implementation order**

1. Review and document the current account navigation and onboarding behaviour.
2. Establish the mandatory mobile verification and date-of-birth setup flow.
3. Introduce clear incomplete-onboarding messaging and navigation handling.
4. Define the desktop account navigation hierarchy.
5. Redesign the desktop Me / Account overview.
6. Relocate redundant information, legal links and coupon redemption.
7. Implement dismissible, persisted safety guidance.
8. Validate desktop, mobile web and native mobile consistency.

**Acceptance criteria**

- [ ] First-time members follow the required mobile verification → date of birth → account flow.
- [ ] Incomplete onboarding restrictions are explained clearly with actionable guidance.
- [ ] The desktop account overview displays only relevant, prioritised information.
- [ ] Desktop account navigation is logically grouped and separate from primary navigation.
- [ ] Safety introduction appears once per applicable notice revision and can be dismissed.
- [ ] Safety advice remains accessible after dismissal.
- [ ] Redundant account status fields are removed from the main overview.
- [ ] Terms of Use, Community Guidelines, Connection & Meeting Advice and Privacy are accessible from the desktop footer.
- [ ] Coupon redemption is moved to Membership & Billing.
- [ ] Desktop and mobile layouts are intentionally designed for their respective form factors.
- [ ] Explore, Connections, Chats and Me remain consistently accessible when the member is eligible.
- [ ] Existing account management and safety functionality remains available.
- [ ] The redesigned page is responsive, accessible and consistent with Swoovr branding.

Additional reviewed evidence: [Signed-in account capture](https://github.com/repasscloud/wanderconnect/blob/main/docs/delivery/evidence/2026-11-01-issue-intake/Screenshot%202026-11-01%20at%2023.13.40.png).

<a id="intake-15"></a>

## 15. Redesign Profile and Modes page for responsive layouts, clearer controls and improved profile management

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: responsive profile/mode workspace, non-overlapping city labels, compact controls and scoped saves. Custom names and explicit about sharing preserved; visibility off/reload passed. Automatic initial-name mapping remains an unapproved product/data-mapping follow-up. Real Edge with gates enabled and synthetic fixtures passed; full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero warnings/errors). See [member evidence and limitations](ISSUE-MEMBER-FIXES.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `/me/profile` (Profile and modes)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#149](https://github.com/repasscloud/wanderconnect/issues/149)
- **Tracking Q:** Q107

### Historical intake investigation — pre-merge source snapshot

Q107: Screenshots support long narrow form, empty display name in one capture and home-city label overlap in another. Code uses checkboxes (styled like large controls), automatically enables visibility when enabling a mode, and also exposes a separate visibility checkbox: inability to turn visibility off is reported, not established. Check persistence and stale updates. Preserve customised names and explicit cross-mode about consent (D199).

Source inspection: `src/Swoovr.Web.Client/Profiles/ProfileEditor.razor; src/Swoovr.Web.Client/UI/CitySearch.razor; docs/delivery/MEMBER-RESPONSIVE-LAYOUT.md`.

Duplicate review: Related #37/#91 (mode privacy), #48; no exact profile-editor UX/label/initialisation duplicate.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

After completing mobile number verification and setting a date of birth, the member can access the "Profile and modes" page.

The page currently presents all profile information and connection modes in one extremely long, narrow column.

On desktop, the interface appears constrained to mobile dimensions, despite having substantial horizontal screen space available.

The layout does not feel purpose-built for desktop or mobile and lacks appropriate spacing, visual hierarchy and separation of functionality.

Several usability problems were identified.

**1. Desktop layout is unnecessarily narrow**

The profile editor occupies a small central column, leaving most of the desktop viewport unused.

The member must scroll through an excessively long page containing their profile details and all four connection modes.

The page appears to use a mobile-sized layout regardless of the available screen width.

**2. Registration details are not automatically populated**

Members are required to provide their first name during registration.

However, the Display Name field on the profile page is empty rather than being populated from their registration details.

This creates unnecessary duplicate data entry.

**3. Form fields lack sufficient spacing**

Several input fields, descriptions and controls appear too close together.

Labels, helper text and field boundaries need more consistent vertical spacing to improve readability.

**4. Some controls resemble buttons when they are actually settings**

The option labelled:

"SHOW MY MAIN ABOUT IN DATING WHEN THIS IS EMPTY"

(and equivalent options for other connection modes) visually resembles a button or text entry control.

Its purpose and current state are not immediately clear.

**5. Buttons are excessively large**

Several actions, particularly the mode activation and profile visibility buttons, occupy the full width of their containers and have excessive height.

These controls dominate the interface rather than supporting it.

**6. Home City autocomplete has a label rendering issue**

The Home City field correctly provides location suggestions when a member begins typing.

However, after selecting a city, the "HOME CITY" label remains displayed behind or overlapping the selected city text.

The selected value and field label visually interfere with each other.

**7. Connection mode activation uses buttons instead of switches**

Each connection mode currently presents a large action button:

- Use Dating
- Use Friends
- Use Activities
- Use Business

These are persistent on/off settings and should be represented by switches rather than large buttons.

**8. Mode profile visibility cannot be independently controlled**

When a member enables a connection mode, the corresponding "Show my profile in [Mode]" control becomes enabled.

However, the member does not appear able to switch profile visibility off independently.

Activating a mode and making the member discoverable in that mode should be separate decisions.

**9. Excessive capitalisation makes labels difficult to read**

For example:

"ABOUT ME IN ACTIVITIES (OPTIONAL, ONLY SHOWN IN ACTIVITIES)"

This is visually loud, difficult to scan and unnecessarily long.

The interface should use sentence case and concise contextual descriptions.

**10. Photos are poorly integrated into the profile management flow**

After adding text to the main About Me section and saving, the interface displays guidance referring to:

"Your photos: a main photo for every mode, and photos for each mode."

Photos are managed separately, but their relationship to individual connection modes is not clearly presented.

Members should be able to understand and manage each mode's profile content and associated photos without navigating through a confusing collection of unrelated sections.

**11. The introductory safety notice is repeated**

The "Before you connect or meet" notice appears at the top of this page, despite already appearing on the account overview.

This reinforces the issue of repeated introductory information taking space away from the actual task.

### What I expected

The Profile and Modes page should be redesigned around a logical, responsive profile management experience.

The design must accommodate desktop and mobile web appropriately, rather than relying on a single narrow layout for both.

#### A. Responsive desktop and mobile layouts

**Desktop**

- Use an appropriately sized content area that takes advantage of desktop screen width.
- Avoid an excessively narrow central column.
- Use a structured layout with clear section boundaries.
- Consider a two-column arrangement for profile details and contextual information.
- Keep individual connection modes in dedicated sections, tabs, or another clearly organised interface.
- Avoid displaying every mode's full editing form in one continuous page.
- Maintain comfortable spacing around forms and controls.

A suggested desktop structure:

1. Profile overview and completion status.
2. About You — shared profile information.
3. Connection Modes — selectable mode navigation.
4. Selected Mode — dedicated editor for its settings, introduction and photos.

For example, selecting Dating should display Dating settings and content, without requiring the member to scroll past Friends, Activities and Business.

**Mobile web**

- Use a properly responsive single-column layout.
- Ensure form fields, switches and actions are comfortably usable on a touch screen.
- Display mode navigation using mobile-appropriate tabs, cards or accordions.
- Avoid dense content and overly long pages.
- Retain consistency with the native iOS and Android experience where practical.

The application may share components and business logic, but desktop and mobile layouts should be intentionally designed for their respective form factors.

#### B. Automatically initialise Display Name

When creating the member's initial profile:

- Prepopulate Display Name using the first name provided at registration.
- Allow the member to change their display name independently of their registration details.
- Do not overwrite a previously customised display name on subsequent visits.
- Apply this initialisation to existing accounts where the display name is currently empty, using the registered first name where available.
- Avoid overwriting an existing profile value during migration or profile updates.

The member should not need to re-enter information already provided.

#### C. Improve form spacing and input presentation

All fields should have consistent:

- Label positioning.
- Input height.
- Horizontal and vertical padding.
- Spacing between fields.
- Helper text alignment.
- Error and validation message placement.

Labels must never overlap populated values.

Form sections should use consistent spacing, avoiding controls that touch or visually merge.

#### D. Fix Home City autocomplete presentation

The Home City autocomplete behaviour should remain, but its presentation must be corrected.

Expected behaviour:

1. Member enters a city name.
2. Matching city suggestions appear.
3. Member selects a suggestion.
4. The selected location displays correctly inside the input.
5. The field label remains readable and does not overlap the selected value.

If floating labels are used, they must correctly transition to their populated state.

The same behaviour should be verified for any similar autocomplete fields.

#### E. Replace mode activation buttons with switches

Replace "Use Dating", "Use Friends", "Use Activities" and "Use Business" buttons with clearly labelled on/off switches.

For example:

**Dating**

Use Dating: On / Off

Show my profile in Dating: On / Off

The mode activation control determines whether that mode is enabled for the member.

The visibility control determines whether the member's profile is discoverable in that mode.

These settings must be independently manageable.

Enabling a mode must not force profile visibility to remain enabled.

If the member disables a mode, its profile settings and content should be preserved for future reactivation rather than deleted.

#### F. Make mode visibility independently configurable

Each mode should support at least two distinct settings:

**Mode enabled**

Determines whether the member is actively using the mode.

**Profile visible**

Determines whether the member's profile can appear in discovery for that mode.

A member should be able to configure a mode and keep their profile hidden until they are ready.

A disabled mode should not produce a discoverable profile, regardless of its stored visibility preference.

Visibility controls should clearly communicate their effective state, without implying that a profile is discoverable when the mode itself is disabled.

Any mode-specific eligibility or verification requirements must continue to be enforced.

#### G. Replace confusing fallback controls

The existing:

"SHOW MY MAIN ABOUT IN [MODE] WHEN THIS IS EMPTY"

should be redesigned as a clearly labelled setting.

Suggested text:

**Use my main About Me when this is empty**

Helper text:

"If you haven't written a separate introduction for this mode, we'll show your main About Me instead."

Use a checkbox or switch, not a button-like text field.

The setting should be positioned near the mode-specific About Me editor.

The actual state must be visually obvious, and the fallback behaviour should be explained without excessive capitalisation.

#### H. Reduce button sizes and visual dominance

Use an appropriate visual hierarchy for actions.

- Primary Save actions should be clearly identifiable.
- Persistent settings should use switches or checkboxes.
- Secondary actions should use smaller buttons or text links.
- Avoid full-width buttons on desktop unless there is a specific usability reason.
- Maintain appropriate touch targets on mobile.
- Use consistent button sizing across the profile editor.

Buttons should represent actions, while switches should represent persistent boolean settings.

#### I. Improve wording and capitalisation

Use sentence case for field labels and supporting text.

For example, replace:

"ABOUT ME IN ACTIVITIES (OPTIONAL, ONLY SHOWN IN ACTIVITIES)"

with:

**About me in Activities**

"Optional. Only visible to people viewing your Activities profile."

Apply this approach consistently across Dating, Friends, Activities and Business.

Avoid unnecessary uppercase text and excessively long labels.

#### J. Organise photos within the relevant profile sections

Photo management should have a clear relationship to shared and mode-specific profiles.

Suggested structure:

**Main profile**

- Display name.
- Area and home city.
- Main About Me.
- Shared or main profile photo, where applicable.

**Dating profile**

- Dating enabled.
- Dating visibility.
- Dating intentions.
- Dating About Me.
- Dating photos.

**Friends profile**

- Friends enabled.
- Friends visibility.
- Friends intentions.
- Friends About Me.
- Friends photos.

**Activities profile**

- Activities enabled.
- Activities visibility.
- Activities intentions.
- Activities About Me.
- Activities photos.

**Business profile**

- Business enabled.
- Business visibility.
- Business intentions.
- Business About Me.
- Business photos.

This does not necessarily require duplicating photo storage or creating four separate upload systems.

The application should preserve existing photo ownership, visibility, moderation and sharing rules.

However, members should be able to understand which photos belong to which profile and manage them within the appropriate context.

Consider dedicated mode editor sections or tabs, with Profile Details and Photos grouped under the selected mode.

Avoid displaying every photo management interface for every mode simultaneously.

#### K. Remove repeated safety notices

The "Before you connect or meet" notice should follow the behaviour defined in the broader account redesign issue.

Once acknowledged, it should not repeatedly appear on each profile or account settings page.

The guidance should remain accessible through Safety and the dedicated Connection & Meeting Advice page.

### Steps to reproduce

1. Sign in to the Swoovr member web application using a test account.
2. Complete mobile number verification.
3. Set the required date of birth.
4. Navigate to Me → Profile and modes.
5. Observe that the desktop profile editor occupies a narrow, mobile-sized central column.
6. Review the Display Name field and confirm whether it has been initialised using the first name supplied at registration.
7. Review spacing between fields, descriptions and buttons.
8. Enter a location into Home City and select a city from autocomplete suggestions.
9. Observe the Home City label overlapping the selected location.
10. Enable Dating, Friends, Activities or Business.
11. Attempt to disable "Show my profile in [Mode]" independently.
12. Review the large activation controls and mode-specific About Me fallback controls.
13. Inspect the capitalisation and layout of labels and descriptions.
14. Enter and save Main About Me content.
15. Review the photo guidance and the relationship between main profile photos and mode-specific photos.
16. Repeat the layout review at desktop, tablet and mobile viewport sizes.

### Evidence

Attached screenshot: `15-profile-screen.jpeg`

The screenshot shows the complete Profile and Modes page in a desktop browser.

It illustrates the unusually narrow content column, excessive vertical page length, oversized controls, repetitive mode editing sections, repeated safety guidance and inconsistent information hierarchy.

**Additional evidence recommended:** A close-up screenshot of the Home City label overlapping a selected city, and a short recording demonstrating the profile visibility toggle behaviour.

### Impact

**Affected users:** All Swoovr members editing their profiles through the member web application.

Particularly affected are new members completing their initial profiles and desktop users.

**Impact:**

- The desktop experience appears unfinished or insufficiently adapted to desktop browsers.
- Excessive scrolling makes profile editing unnecessarily time-consuming.
- Members must re-enter details already collected during registration.
- Overlapping text reduces usability and perceived quality.
- Oversized controls and unclear settings make the interface harder to understand.
- Members may unintentionally expose their profiles when enabling a mode.
- Confusing fallback controls make it difficult to understand which About Me text is displayed.
- Poor separation between mode-specific content and photos complicates profile management.
- Repeated safety notices consume valuable editing space.

The distinction between enabling a mode and choosing to be visible within it is particularly important for member privacy and control.

### Evidence

Evidence withheld from publication: `15-redesign-profile-and-modes-page-for-responsive-layout.jpeg` (populated personal profile/intention data; fictional provenance unverified). See [evidence review](evidence/2026-11-01-issue-intake/README.md).

### Notes or constraints

**Product direction**

Swoovr is launching primarily as a dating and social discovery platform.

The profile experience should feel modern, approachable and polished, with a strong focus on helping members create attractive, complete profiles.

The application supports four connection modes:

- Dating
- Friends
- Activities
- Business

Mode-specific editing should scale cleanly as the platform evolves.

**Implementation considerations**

- Preserve existing member profile data.
- Preserve existing mode settings and intention selections.
- Maintain the maximum of five intention selections per mode.
- Do not automatically expose hidden profiles when updating mode settings.
- Persist mode activation and profile visibility independently.
- Preserve profile content when a mode is disabled.
- Retain existing photo moderation, storage and access rules.
- Ensure profile visibility is enforced by discovery services, not just by frontend controls.
- Do not automatically copy mode-specific About Me text into other modes.
- Respect existing behaviour for displaying Main About Me as a fallback.
- Avoid unnecessary database or API changes where the current model already supports the required behaviour.
- Keep shared responsive components reusable without compromising desktop presentation.
- Ensure form controls remain keyboard-accessible and suitable for screen readers.
- Validate the interface using realistic profile data and longer text values.

**Suggested implementation order**

1. Fix Home City autocomplete label overlap.
2. Correct Display Name initialisation.
3. Separate mode activation from profile visibility.
4. Replace activation buttons and ambiguous fallback controls with appropriate switches.
5. Improve field spacing, label text and button sizing.
6. Redesign desktop and mobile responsive layouts.
7. Restructure the mode editing workflow to avoid displaying all four complete forms simultaneously.
8. Integrate or clearly group photo management with the relevant profile sections.
9. Remove repeated introductory safety notices.
10. Verify responsive behaviour and persistence across all supported connection modes.

**Acceptance criteria**

- [ ] Desktop profile editor uses an appropriately sized, responsive content layout.
- [ ] Mobile web profile editor has a properly designed, touch-friendly layout.
- [ ] All four mode editors no longer need to be displayed as one continuous long form.
- [ ] Display Name is initialised using the registered first name when no display name exists.
- [ ] Existing customised display names are never overwritten automatically.
- [ ] Form fields have consistent spacing, labels and helper text.
- [ ] Home City autocomplete does not overlap field labels or selected values.
- [ ] Dating, Friends, Activities and Business activation controls use switches.
- [ ] Each mode has an independent profile visibility switch.
- [ ] Enabling a mode does not force the member's profile to become discoverable.
- [ ] Disabling a mode preserves its existing settings and content.
- [ ] Main About Me fallback is presented as an understandable setting with a clear on/off state.
- [ ] Buttons are appropriately sized for desktop and mobile contexts.
- [ ] Labels use sentence case rather than unnecessary uppercase text.
- [ ] Main profile information and mode-specific information are clearly separated.
- [ ] Photos are accessible within a clear shared or mode-specific profile management structure.
- [ ] Existing photo ownership, moderation and visibility rules remain intact.
- [ ] Introductory safety guidance does not repeatedly occupy profile editing pages after acknowledgement.
- [ ] Existing profile data and mode configurations remain intact after the redesign.

Safe supplementary evidence: [Blank profile capture](evidence/2026-11-01-issue-intake/15-profile-screen.jpeg).

<a id="intake-16"></a>

## 16. Redesign Your Photos page with shared photo library, independent mode galleries and membership limits

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Partial implementation merged: one mode-assignment gallery at a time with shared library/main preserved. Independent main photos and proposed photo limits remain deferred; API quotas and screening rules unchanged. Real Edge with gates enabled and synthetic fixtures passed; full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero warnings/errors). See [member evidence and limitations](ISSUE-MEMBER-FIXES.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `/me/photos` (Your photos)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#150](https://github.com/repasscloud/wanderconnect/issues/150)
- **Tracking Q:** Q108

### Historical intake investigation — pre-merge source snapshot

Q108: Code confirms shared main photo and optional per-mode assignments; screenshot shows a photo library and repetitive mode rows. OPEN: proposal for independent mode main photos changes CONFIRMED D175; 60 library / 8 Free / 16 Plus / 24 Premium limits are intake proposals pending reconciliation with current quotas/entitlements. Preserve existing assignments, moderation and downgrade data; no migration is authorised.

Source inspection: `src/Swoovr.Web.Client/Profiles/PhotosPanel.razor; docs/delivery/DECISIONS.md (D175, D203)`.

Duplicate review: Related #37/#73 (cross-mode policy), #85 (screening); new gallery/limits proposal is not covered in full.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current "Your photos" page has an awkward layout and an unintuitive photo management workflow.

Although the page uses more horizontal space than the Profile and Modes page, it still feels like a collection of administrative forms rather than a modern photo management experience for a dating and social discovery application.

Currently:

1. Members upload photos into a general collection.
2. One photo is designated as the main photo.
3. The same main photo automatically appears in Dating, Friends, Activities and Business.
4. Members assign additional photos to individual modes.
5. Each mode is displayed as a large, repetitive horizontal container.
6. The interface displays an outdated allowance of up to 30 uploaded photos.
7. There is no clear distinction between the shared photo library and each mode's photo gallery.
8. The interface does not clearly communicate membership-specific photo limits.

The overall presentation is excessively spacious in some areas, cramped in others, and makes poor use of available desktop screen space.

The photo upload control also uses the default browser file input, which looks inconsistent with the application's visual design.

### What I expected

The photo management experience should be redesigned around a **shared photo library**, with each connection mode having an independently configured photo gallery and main profile photo.

Members should upload their photos once and then decide where and how those photos appear.

There should be no mandatory global main photo shared automatically across all connection modes.

#### A. Shared photo library

Introduce a central photo library containing all photos uploaded by the member.

The library should:

- Display uploaded photos in a responsive thumbnail grid.
- Support uploading multiple photos at once.
- Allow members to add and remove photos.
- Display moderation status for each photo.
- Allow selection of existing photos for any connection mode.
- Avoid requiring the same image to be uploaded multiple times.
- Clearly indicate which photos are currently used in which modes.
- Support selecting a photo to preview it and manage its assignments.

**Proposed shared library limit: 60 photos per member, across all membership tiers.**

This is the total number of distinct photos the member may store, not the number visible in any particular mode.

A photo can be assigned to multiple modes without counting as a new upload or consuming additional shared-library storage.

The 60-photo limit gives members flexibility to maintain different sets of Dating, Friends, Activities and Business photos without unnecessarily duplicating files.

The shared library must remain private and must not automatically expose newly uploaded photos in discovery.

#### B. Independent photo galleries for each connection mode

Each mode must have its own photo gallery configuration.

Supported modes:

- Dating
- Friends
- Activities
- Business

Members should select photos from their shared library to assign to each mode independently.

For each mode, members should be able to:

- Select a main profile photo.
- Add additional photos from their shared library.
- Remove a photo from that mode without deleting it from the library.
- Replace the main profile photo.
- Reorder gallery photos.
- Use the same photo in multiple modes where desired.
- Keep different photos and different main photos for each mode.

For example, a member might use a casual portrait as their Dating main photo, a group activity photograph for Friends, and a professional headshot for Business.

There should be no requirement that all modes share the same main photo.

#### C. Membership-specific photo limits

The number of photos assigned to each individual mode must depend on the member's membership tier.

| Membership | Maximum photos per mode |
| --- | --: |
| Free | 8 |
| Plus | 16 |
| Premium | 24 |

These limits include the mode's main profile photo.

For example, a Free member can assign one main photo plus seven additional photos to Dating.

A Premium member can assign one main photo plus 23 additional photos to Business.

The limits apply independently to each mode.

The same library photo assigned to Dating and Friends counts once toward Dating's allowance and once toward Friends' allowance.

**Important:** The shared library limit and mode-specific limits are separate concepts.

The library limit controls how many distinct images a member may store. Mode limits control how many library images can be assigned to a particular mode.

Do not use the shared library limit as a substitute for membership entitlements.

#### D. Main profile photo selection

Remove the concept of a mandatory global main photo.

Instead:

- Each mode has its own main photo assignment.
- The member selects that photo from their shared library.
- The main photo is clearly distinguished from the additional gallery photos.
- Members can change the main photo at any time.
- Changing the main photo in one mode must not affect another mode.
- Removing a photo from one mode must not remove it from other modes.

The main photo must count toward the mode's membership allowance.

If a mode has no main photo, clearly indicate that the mode's photo setup is incomplete.

Apply existing profile readiness and discovery eligibility requirements rather than silently assigning a global fallback photo.

#### E. Redesign the desktop photo management interface

The desktop layout should be purpose-built for larger screens.

Suggested structure:

**Top section — Your photos**

- Page heading and concise description.
- Upload Photos action.
- Library usage indicator, for example "12 of 60 photos".
- Responsive photo thumbnail grid.

**Second section — Mode galleries**

Provide a tabbed or similarly structured interface for:

- Dating
- Friends
- Activities
- Business

Selecting a mode should display only that mode's photo management interface.

Within the selected mode, display:

- Main profile photo.
- Assigned gallery photos.
- Current allowance, for example "5 of 8 photos".
- Add from library.
- Change main photo.
- Reorder and remove actions.

Avoid displaying all four mode galleries as large, repetitive stacked containers.

The main objective is to make it easy for members to understand which photos are assigned to each mode without excessive scrolling.

#### F. Improve photo selection and management

Replace the existing tiny thumbnails and large empty horizontal containers with a proper photo selection interface.

Suggested interaction:

1. Member opens the Dating gallery.
2. Selects "Add from library".
3. A photo picker opens showing their available uploaded photos.
4. Member selects the photos to include.
5. Member chooses which assigned photo is the main Dating photo.
6. Member saves their changes.
7. The Dating gallery updates accordingly.

The photo picker should clearly show which images are already assigned to the selected mode.

Prevent duplicate assignments of the same image within a single mode.

Provide clear visual feedback when the membership limit is reached.

A member should not need to delete a photo from their library merely to remove it from a particular mode.

#### G. Improve upload experience

Replace the native-looking "Choose File" control with a styled photo upload interface consistent with Swoovr's design system.

Support:

- Clicking to browse for images.
- Drag-and-drop uploading on desktop.
- Multiple file selection.
- Upload progress and failure states.
- Clear file format and maximum size guidance.
- Existing server-side validation and moderation.

Keep uploaded photos in a pending or unavailable state until they meet the applicable moderation requirements.

A photo must not become publicly visible merely because the upload completed successfully.

#### H. Improve responsive behaviour

**Desktop**

- Use a responsive photo grid.
- Make sensible use of available horizontal space.
- Avoid oversized empty containers.
- Provide clearly visible image previews and contextual actions.
- Use appropriate modal or panel layouts for photo selection.
- Keep related actions near their corresponding photos.

**Mobile web and native mobile**

- Adapt the gallery into a touch-friendly grid.
- Support easy photo selection and reordering.
- Keep actions accessible without excessive scrolling.
- Use mobile-appropriate photo pickers and controls.

Do not simply compress the desktop interface into a narrow column.

#### I. Membership upgrades and downgrades

Photo limits must be enforced according to the member's active membership tier.

When upgrading:

- The new per-mode allowance becomes available when the membership entitlement is active.
- Existing photos and assignments remain unchanged.

When downgrading:

- Do not automatically delete photos or remove library assignments.
- Retain existing gallery configuration.
- Clearly identify when a mode exceeds the new membership allowance.
- Prevent adding further photos to an over-limit mode.
- Require the member to reduce their assigned photo count to the applicable limit.
- Ensure the externally visible gallery respects the active tier's allowance, using a deterministic and clearly communicated rule until the member resolves the excess.

The downgrade behaviour must be predictable and must not result in accidental data loss.

#### J. Deleting photographs

Differentiate between two actions:

**Remove from mode**

Removes an image assignment from the selected mode only.

The photo remains in the shared library and in any other modes where it is assigned.

**Delete from library**

Removes the photo from the member's library and all associated mode assignments.

This action should require confirmation and clearly explain its consequences.

A deleted photo must no longer appear in profile galleries or discovery once deletion is applied.

Existing moderation, retention, reporting and legal preservation rules must still be respected.

### Steps to reproduce

1. Sign in to the Swoovr member web application.
2. Navigate to Me → Photos.
3. Observe the current photo upload form and main photo area.
4. Upload and approve a test photo.
5. Observe that one photo is treated as the main photo across the connection modes.
6. Review the large stacked sections for Dating, Friends, Activities and Business.
7. Attempt to configure a different main photo independently for each mode.
8. Review how additional photos are assigned to individual modes.
9. Observe whether per-mode photo allowances reflect the active membership tier.
10. Compare the desktop presentation with the intended mobile photo management experience.

### Evidence

Current page: `/me/photos`

The screenshot provided shows the existing photo management layout, including the shared main photo, repeated mode sections, small image thumbnails and oversized empty gallery containers.

### Impact

**Affected users:** All Swoovr members managing profile photos, across Free, Plus and Premium membership tiers.

**Impact:**

- Members cannot intuitively manage separate visual identities across different modes.
- The current global main photo behaviour does not reflect the intended product design.
- Photo management requires excessive scrolling and unnecessary navigation.
- The page does not clearly communicate membership benefits.
- Members may misunderstand whether removing a photo affects other modes.
- The desktop interface feels unfinished and inconsistent with a modern dating platform.
- Confusing photo assignment behaviour may lead to unintended visibility of images across modes.

Photo management is a core feature of a dating and social discovery platform and should be prioritised accordingly.

### Notes or constraints

**Photo entitlement model**

- Shared library: **60 distinct photos per member**.
- Free: **8 photos per mode**, including main photo.
- Plus: **16 photos per mode**, including main photo.
- Premium: **24 photos per mode**, including main photo.
- Each mode has exactly one main photo when its photo setup is complete.
- A photo may be reused across multiple modes.
- A mode assignment does not duplicate the underlying image file.
- Shared library uploads do not automatically become visible in any mode.

**Technical considerations**

- Continue using the existing Cloudflare R2 image storage architecture.
- Preserve image moderation and approval workflows.
- Maintain existing image processing and optimised delivery behaviour.
- Store mode assignments separately from underlying image records.
- Enforce membership entitlements server-side.
- Prevent duplicate assignments within the same mode.
- Preserve mode-specific image ordering.
- Make main photo selection an explicit mode-level operation.
- Ensure photo visibility respects the member's active modes and discovery settings.
- Do not expose unapproved images.
- Avoid destructive changes to existing member images during migration.
- Review existing global main photo assignments and migrate them safely into the new mode-based model, without requiring duplicate uploads.
- Keep applicable abuse reporting, moderation and retention requirements intact.

**Suggested implementation order**

1. Review the existing photo storage and assignment model.
2. Establish the shared library and per-mode gallery structure.
3. Implement the membership-specific photo entitlement rules.
4. Replace the global main photo model with independent mode main photos.
5. Redesign the desktop photo library and gallery interface.
6. Implement a reusable photo selection and assignment experience.
7. Improve upload controls and responsive layouts.
8. Implement safe removal, deletion and downgrade handling.
9. Validate image moderation and discovery visibility.
10. Test all membership tiers and all four connection modes.

**Acceptance criteria**

- [ ] Members have a shared library supporting up to 60 distinct photos.
- [ ] Photos are uploaded once and may be assigned to multiple modes.
- [ ] Each mode has its own independently selected main photo.
- [ ] No mandatory global main photo is imposed.
- [ ] Free members can assign up to 8 photos per mode.
- [ ] Plus members can assign up to 16 photos per mode.
- [ ] Premium members can assign up to 24 photos per mode.
- [ ] Photo allowances include the main photo.
- [ ] Members can add existing library photos to a selected mode.
- [ ] Members can remove photos from individual modes without deleting them from the library.
- [ ] Members can reorder assigned photos.
- [ ] Members can replace a mode's main photo independently.
- [ ] Members can see their library usage and per-mode allowances.
- [ ] Uploads support multiple image selection and clear progress feedback.
- [ ] Desktop layout uses a responsive thumbnail grid and organised mode navigation.
- [ ] Mobile layout is touch-friendly and purpose-built.
- [ ] Existing moderation and image visibility rules remain enforced.
- [ ] Membership downgrades do not automatically delete stored photographs.
- [ ] Library deletion clearly distinguishes itself from removing a mode assignment.
- [ ] Existing photos and assignments are migrated without data loss.

Additional evidence: `16.png` reviewed locally and withheld under the [publication review](evidence/2026-11-01-issue-intake/README.md); no public attachment.

<a id="intake-17"></a>

## 17. Redesign "Who I'd Like to Meet" with responsive layouts, clearer discovery preferences and improved filtering controls

Implementation progress (2 November 2026): [PR #154](https://github.com/repasscloud/wanderconnect/pull/154). Implementation merged: per-mode unsaved filter drafts survive switching and failed reads; stale fields hidden while loading and controls disabled during save. Responsive workspace and selection counts added; Any/All, reciprocal exclusions and Plus rules preserved. Drafts remain local to this page. Real Edge with gates enabled and synthetic fixtures passed; full `./scripts/check.sh` passed (596 PostgreSQL cases, three smoke suites, zero warnings/errors). See [member evidence and limitations](ISSUE-MEMBER-FIXES.md).

- **Status:** Processed — new issue created; investigation remains OPEN
- **Area:** Member web
- **Page or URL:** `/me/matching` (confirmed in current route source; originally reported as `/me/who-id-like-to-meet`)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#151](https://github.com/repasscloud/wanderconnect/issues/151)
- **Tracking Q:** Q109

### Historical intake investigation — pre-merge source snapshot

Q109: Actual route is /me/matching, linked from Account. Screenshot supports long narrow combined identity/filter forms and separate saves. Current delivery explicitly retains 420px on matching/profile forms. Reorganise only after validating independent mode persistence, Any/All, five required/ten excluded, reciprocal exclusions and backend entitlements; no matching-semantic change is approved.

Source inspection: `src/Swoovr.Web/Components/Pages/Matching.razor; src/Swoovr.Web.Client/Profiles/MatchingPanel.razor; docs/delivery/MEMBER-RESPONSIVE-LAYOUT.md`.

Duplicate review: Related #44 is compatibility scoring/explanations, #48 accessibility; neither covers this settings composition.

Evidence and scope: screenshots reviewed locally; no runtime reproduction or token/SSO lifetime measurement performed. Original report below is retained as feedback, not a verified diagnosis or automatic amendment of CONFIRMED policy. See [publication review](evidence/2026-11-01-issue-intake/README.md).

### What happened

The current "Who I'd Like to Meet" page suffers from the same fundamental design problems identified in the Account, Profile and Photos pages.

On desktop, the entire interface is constrained to an extremely narrow, mobile-sized column, leaving most of the available screen space unused.

The page is also excessively long, with personal identity settings, privacy controls, dating preferences, discovery filters, exclusions, travel preferences and premium features all presented in one continuous form.

The result is a confusing settings experience that does not feel like a modern dating or social discovery application.

Specific observations:

**1. Desktop layout is effectively a stretched mobile experience**

The page occupies a narrow central column despite being displayed in a desktop browser.

Members must scroll extensively to access basic discovery settings.

There is no meaningful grouping or desktop navigation within the page.

**2. Personal identity and discovery preferences are mixed together**

The "About you" section includes:

- Gender.
- Pronouns.
- Sexual orientation.
- Gender and pronoun visibility.
- Sexual orientation visibility.
- Age visibility.
- Dating gender preferences.

These are not all the same type of setting.

Personal identity and profile visibility belong together, but discovery preferences should be managed separately.

**3. Poor organisation of discovery filters**

The Filters section combines:

- Connection mode selection.
- Age range.
- ID verification requirement.
- Required intentions.
- Excluded intentions.
- Visiting members.
- Distance.
- Compatibility sorting.

These settings have different purposes but are presented as one very long form.

**4. Intention selection is difficult to use**

The "Must have" and "Not for me" sections display long lists of vertically stacked checkboxes.

This requires excessive scrolling and makes it difficult to see which intentions have already been selected.

The same intentions appear in both sections.

There is no clear visual overview of positive and negative preferences.

**5. Filter labels and descriptions are unclear**

Examples include:

- "Must have (up to 5)"
- "Any of these"
- "All of these"
- "Not for me (up to 10, works both ways)"
- "Plus"

These labels are too terse or ambiguous.

In particular, "works both ways" does not adequately explain the consequences of excluding an intention.

**6. Personal identity fields have poor label presentation**

Labels such as:

"ORIENTATION (OPTIONAL, NEVER USED TO DECIDE WHO...)"

are truncated or excessively long.

The interface relies heavily on uppercase labels and small helper text, making the page difficult to scan.

**7. Checkboxes are used where better controls may exist**

Gender visibility, orientation visibility and age visibility are presented as checkboxes.

Although functionally acceptable, the interface would benefit from consistent, clearly labelled switches for persistent visibility preferences.

**8. Save behaviour is fragmented**

The page contains separate Save and Save filters buttons.

This may be technically appropriate for separate settings groups, but the boundaries between those groups are not sufficiently clear.

Members may not understand which changes are saved by each button.

**9. Premium filters are poorly distinguished**

Distance and compatibility options appear under a simple "Plus" heading, with a small explanation that these filters are part of Plus.

There is no clear distinction between available controls, locked benefits and upgrade opportunities.

**10. Travel discovery preferences are buried**

The "Visitors in travel" selector is placed deep inside the filter form.

This is a meaningful dating and discovery preference and deserves a clearer presentation.

### What I expected

The page should provide a structured, attractive and intuitive experience for configuring who a member wants to discover and connect with.

It should separate personal identity, profile visibility and discovery filtering into clear functional groups.

The desktop experience must use desktop-appropriate layouts, while mobile web should receive its own responsive presentation.

#### A. Redesign the desktop layout

Use the available desktop width intelligently.

Suggested structure:

**Discovery preferences**

Provide clear sections or tabs:

1. Who I am
2. Who I'd like to meet
3. Intentions & compatibility
4. Location & travel
5. Advanced filters

The exact section names may be refined during implementation.

The important requirement is to avoid one excessively long form containing every setting.

A desktop two-column layout could provide navigation or contextual information on one side and the selected settings form on the other.

Alternatively, use a tabbed layout with contained sections.

**Mobile web**

- Use an appropriately sized single-column layout.
- Group settings into collapsible sections or dedicated steps.
- Avoid unnecessarily long scrolling.
- Make controls comfortably touch-accessible.
- Keep navigation and save actions clear.

Do not simply constrain the desktop interface to a mobile-sized column.

#### B. Separate identity from discovery preferences

**Personal identity**

This should contain:

- Gender.
- Pronouns.
- Sexual orientation.
- Profile visibility for gender and pronouns.
- Profile visibility for sexual orientation.
- Profile visibility for age.

These settings describe the member and what they choose to display.

They should not be mixed with the filtering controls used to discover other members.

Sexual orientation must remain optional and must not be used to independently infer or override the member's dating preferences.

**Dating preferences**

This should contain the existing "Show me" options:

- Man.
- Woman.
- Non-binary.
- Another gender.

The member must be able to select more than one category.

Preserve the existing matching rules, including reciprocal preference requirements and the handling of members who prefer not to disclose their gender.

Provide a concise explanation of how matching works, with additional information available when requested.

Avoid displaying lengthy technical matching explanations directly inside the primary form.

#### C. Improve profile visibility controls

Replace the current visibility checkboxes with clearly labelled switches.

For example:

- Show my gender and pronouns.
- Show my sexual orientation.
- Show my age.

Each should display an obvious On or Off state.

Use appropriate helper text where a choice may have consequences for the member's public profile.

Ensure the actual profile presentation respects these choices.

Do not confuse hiding an attribute from other members with changing how internal eligibility or discovery rules operate.

#### D. Redesign intention filtering

The existing "Must have" and "Not for me" filters should be redesigned into a more usable intention selection interface.

**Required intentions**

Label:

"Looking for"

Supporting text:

"Choose up to 5 intentions you'd like to see in other members."

Allow members to select:

- Any selected intention.
- All selected intentions.

Use clearly selectable chips or another compact multi-selection interface rather than one checkbox per row.

Display the number selected, for example:

"3 of 5 selected"

**Excluded intentions**

Label:

"Not interested in"

Supporting text:

"Choose up to 10 intentions you don't want in potential matches."

Use the same compact selection pattern.

Display:

"4 of 10 selected"

Provide a clear explanation of reciprocal exclusions where applicable.

For example:

"Some exclusions work both ways. You may also be excluded from another member's results if your stated intentions conflict with their preferences."

The explanation must accurately reflect the existing matching rules.

**Conflict handling**

A member should not be able to require and exclude the same intention simultaneously.

If the member tries to select a conflicting intention, the interface should clearly explain the conflict and allow them to resolve it.

Do not silently change preferences.

#### E. Make discovery preferences mode-specific

The existing Mode selector should be improved so members can clearly configure discovery preferences independently for:

- Dating
- Friends
- Activities
- Business

Use tabs, a segmented selector or another appropriate control.

Changing the selected mode should update the visible filter options without requiring the member to navigate away.

Each mode should retain its own relevant filters and intention selections.

Do not automatically carry Dating preferences into Friends, Activities or Business.

Only display controls supported by the selected mode.

For example, Dating gender preferences should not automatically appear as a filtering requirement in Business.

The interface should accommodate currently enabled modes and preserve saved preferences when a mode is disabled.

#### F. Improve age range filtering

The current separate "Age from" and "To" fields are visually disconnected.

Use a clearly grouped age range control.

For example:

**Age range**

25 – 45

A dual-ended slider may be appropriate if it remains accessible and supports precise values.

Alternatively, use two well-labelled dropdowns or numeric controls.

Requirements:

- Clearly show the selected minimum and maximum.
- Prevent invalid ranges.
- Enforce adult-only age eligibility.
- Respect existing membership-specific filtering rules.
- Provide a clear indication when changes have been saved.

#### G. Improve ID verification filtering

The "ID Verified people only" option should be presented as a simple switch.

Label:

"Only show ID-verified members"

Supporting text:

"Only include members who have completed Swoovr's ID verification."

Do not imply that verification guarantees a member's identity, intentions or safety.

The filter must use the actual ID verification status, not email or mobile verification alone.

#### H. Give travel preferences their own section

Travel discovery preferences should be grouped separately from general demographic and intention filters.

Suggested heading:

**Travellers and visitors**

Provide a clear selection for:

- People currently visiting my area.
- People arriving in the future.
- Both.
- Neither.

Keep the existing underlying travel visibility and discovery rules.

Explain these choices using concise, member-friendly language.

Avoid ambiguous values such as "Here now and arriving soon" without clearly identifying what the selection means.

#### I. Improve premium filter presentation

Advanced discovery filters should have a dedicated section.

Existing Plus features include:

- Distance from home city.
- Compatibility-based sorting.

The interface should clearly distinguish between:

- Features available to the member.
- Features included with a higher membership tier.
- Features that are temporarily unavailable.

For members without access:

- Show a concise feature description.
- Clearly identify the required tier.
- Provide a contextual upgrade action where appropriate.
- Do not present a locked control as if it can be saved or applied.

For members with access, controls should behave normally.

The server must enforce membership entitlements regardless of frontend presentation.

#### J. Improve labels, helper text and typography

Remove excessive capitalisation and truncated labels.

Use sentence case consistently.

For example:

Instead of:

"ORIENTATION (OPTIONAL, NEVER USED TO DECIDE WHO...)"

Use:

**Sexual orientation**

"Optional. This does not determine who you see in Dating."

Replace overly technical or lengthy explanations with concise descriptions and contextual information controls.

Maintain readable text sizes, proper field spacing and adequate contrast.

#### K. Improve save behaviour

The page should have a clear and predictable save model.

If sections are saved independently:

- Each section should have its own clearly identified Save action.
- Show confirmation when changes are saved.
- Show unsaved changes where applicable.
- Warn before navigating away from unsaved changes.

If the redesigned page uses a unified save action, ensure it reliably saves all applicable settings.

Do not silently discard changes when switching modes or sections.

Preserve existing backend validation and preference persistence.

### Steps to reproduce

1. Sign in to the Swoovr member web application.
2. Navigate to Me → Who I'd like to meet.
3. Observe the narrow, vertically constrained desktop layout.
4. Review the About You section and the mixing of identity, profile visibility and Dating preferences.
5. Scroll to Filters.
6. Select Dating as the mode.
7. Review the age fields and ID verification option.
8. Review the vertically stacked required intention checkboxes.
9. Review the excluded intention checkboxes.
10. Observe how much scrolling is required to reach travel and premium filters.
11. Review the Plus controls and their explanations.
12. Change settings and observe the separate Save and Save filters actions.
13. Repeat the review at mobile and tablet viewport sizes.

### Evidence

Screenshot: `17.png`

The screenshot demonstrates:

- Excessively narrow desktop content.
- Poor use of available horizontal space.
- Long vertical forms.
- Mixed settings categories.
- Large numbers of individual checkboxes.
- Small and difficult-to-read descriptions.
- Weak visual distinction between basic and premium functionality.

### Impact

**Affected users:** All Swoovr members configuring discovery preferences.

**Impact:**

- Members may struggle to understand how Swoovr decides whom to show them.
- Required and excluded intentions are difficult to configure.
- Important discovery controls are buried deep within the page.
- Privacy and identity settings may be confused with discovery filters.
- The desktop interface feels unfinished and poorly adapted to larger screens.
- Members may overlook useful filters or misunderstand their effects.
- Premium features are not presented clearly.
- Confusing save behaviour may result in unintended preference changes or unsaved settings.

Because discovery preferences directly influence who members encounter, clarity and predictable behaviour are essential.

### Notes or constraints

**Product direction**

Swoovr supports multiple discovery modes with independent matching and intention preferences.

The initial product experience should prioritise Dating without assuming every mode requires the same filters.

Discovery must remain consistent with Swoovr's established reciprocal matching, identity, verification, locality and visibility rules.

**Technical considerations**

- Preserve existing saved preferences and mode-specific settings.
- Do not change matching algorithm semantics as part of a visual redesign without explicit review.
- Enforce required and excluded intention limits server-side.
- Maintain reciprocal exclusion rules where already implemented.
- Keep sexual orientation independent of gender-based discovery decisions.
- Respect gender disclosure preferences.
- Maintain the existing distinction between ID, photo, email and mobile verification.
- Respect travel visibility and traveller initiation restrictions.
- Enforce membership entitlements on the backend.
- Avoid exposing premium filters as active when a member lacks the required entitlement.
- Ensure changing a displayed filter does not unintentionally change unrelated mode preferences.
- Reuse existing responsive components where practical, without compromising desktop usability.
- Ensure all controls are accessible through keyboard and assistive technologies.
- Avoid applying filtering assumptions that have not been explicitly defined in the product rules.

**Suggested implementation order**

1. Review the current discovery preference model and matching behaviour.
2. Separate identity settings from discovery preferences.
3. Define a mode-specific filter navigation structure.
4. Redesign the desktop layout.
5. Replace long intention checkbox lists with compact multi-selection controls.
6. Improve age, visibility and verification controls.
7. Separate travel and premium filters into their appropriate sections.
8. Improve labels, descriptions and save feedback.
9. Implement responsive mobile layouts.
10. Validate filtering behaviour and persistence across all modes and membership tiers.

**Acceptance criteria**

- [ ] Desktop layout makes appropriate use of available viewport width.
- [ ] Mobile web uses a properly designed responsive layout.
- [ ] Identity, visibility and discovery settings are clearly separated.
- [ ] Dating gender preferences remain independently configurable.
- [ ] Gender, orientation and age visibility settings have clear on/off controls.
- [ ] Intention filters use a compact, understandable selection interface.
- [ ] Required intentions support existing Any/All matching behaviour.
- [ ] Required intentions remain limited to five selections.
- [ ] Excluded intentions remain limited to ten selections.
- [ ] Conflicting required and excluded intentions are handled clearly.
- [ ] Discovery preferences remain independent across modes.
- [ ] Age range controls prevent invalid values.
- [ ] ID verification filtering uses the correct verification level.
- [ ] Travel discovery preferences are grouped logically.
- [ ] Premium filters clearly communicate availability and membership requirements.
- [ ] Labels and helper text use readable sentence case.
- [ ] Save actions are clearly scoped and provide feedback.
- [ ] Switching modes or sections does not silently discard changes.
- [ ] Existing matching behaviour, preferences and privacy constraints remain intact.

Additional evidence: `17.png` reviewed locally and withheld under the [publication review](evidence/2026-11-01-issue-intake/README.md); no public attachment.

<a id="intake-18"></a>

## 18. Improve Admin account search with email and phone lookup, searchable account table and configurable filters

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/accounts`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#155](https://github.com/repasscloud/wanderconnect/issues/155)
- **Tracking Q:** [Q110](QUESTIONS.md#q110)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q110](QUESTIONS.md#q110) / [#155](https://github.com/repasscloud/wanderconnect/issues/155) — Entire item 18; registered-name search also depends on Q117.**

Source-supported: Accounts.razor uses an exact-email GET form and a basic result list. AdminEndpoints.cs normalises the verified email, left-joins profiles, returns at most 10 matches and audits a result count. It has no phone lookup or browse/filter contract. PR #152 merged the refusal/no-match correction; the owner confirmed lookup works on #137. This is an extension, not a recurrence of Q95.

Requirements/dependencies: CONFIRMED D38/D55 issuer/subject account identity and D185 least privilege remain. DEFAULT: reuse existing phone normalisation, account routes and pagination conventions. OPEN: demographic-filter purposes/field matrix and name-search authority (Q117); no broad support access is inferred.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

The Swoovr Admin Accounts page currently supports searching for member accounts using an exact email address.

While email searching now works, the overall account lookup and management experience is extremely basic and does not provide the functionality expected from a professional administrative application.

The current page contains:

- An "Exact email" input.
- A "Find" button.
- A basic text-based search result displaying the account type, status and creation date.
- A hyperlink to open the matching account.

The search result lacks meaningful structure, visual hierarchy and relevant account information.

There is also no obvious way to browse all member accounts or filter accounts using other useful attributes.

Specific problems include:

1. **Email-only search.** Administrators cannot locate accounts using a registered mobile phone number.
2. **Limited phone number handling.** There is no support for searching international phone numbers in common formats.
3. **No account listing.** Administrators cannot open a complete, paginated table of member accounts.
4. **No configurable filters.** Administrators cannot filter accounts by location, demographics, verification or membership state.
5. **Poor search result presentation.** A matching account appears as a basic text line rather than a properly formatted account summary.
6. **Limited administrative usability.** The page does not provide an efficient way to identify, compare and review accounts.
7. **Poor scalability.** The current presentation is unsuitable for managing a large member population.

### What I expected

The Admin Accounts page should become a central account lookup and management interface.

It should provide two primary capabilities:

1. **Quick account lookup** using an email address or mobile phone number.
2. **Browse all accounts** using a searchable, sortable, paginated Bootstrap table with configurable filtering.

The redesign should make effective use of the desktop viewport and follow Swoovr's existing Admin design system.

#### A. Support email address and mobile phone number searches

Replace the existing "Exact email" input with a more flexible search field.

Suggested label:

**Find an account**

Placeholder:

"Enter email address or mobile number"

Administrators should be able to search using either identifier.

**Email address**

- Support exact email lookup.
- Ignore leading and trailing whitespace.
- Handle case differences appropriately.
- Return the matching member account.
- Preserve existing account lookup permissions.

**Mobile phone number**

Support international phone number lookup, including common formatting variations.

For example, the following should resolve to the same Australian mobile number when applicable:

- `+61412345678`
- `61412345678`
- `0412345678` (when interpreted using the appropriate Australian country context)
- `+61 412 345 678`
- `+61 412-345-678`

Phone numbers should be normalised before searching.

Use the existing canonical phone number representation, preferably E.164, rather than relying on literal string comparisons.

Do not attempt to infer ambiguous international numbers without a known country context.

The lookup should use registered account identifiers, not arbitrary phone numbers mentioned in profiles or messages.

#### B. Improve quick search results

When an account is found, do not display it as a basic hyperlink followed by unformatted text.

Instead, display a clearly structured account result.

Suggested information:

| Field | Example |
| --- | --- |
| Display name | Fictional Sam |
| Email | fictional.sam@example.test |
| Mobile | +61 412 *** 678 |
| Account status | Active |
| Membership | Free |
| ID verification | Not verified |
| Home city | Adelaide |
| Created | 2 Nov 2026 |

Provide a clearly labelled action:

**View account**

The result should use a compact account summary card or similarly structured component consistent with Swoovr Admin styling.

The member's display name or account reference should be the primary identifier.

Avoid exposing unnecessary sensitive information in search results.

Account data visibility must respect administrator permissions.

#### C. Add a Browse all accounts action

Provide a clearly visible button on the Accounts page:

**Browse all accounts**

This should display a structured Bootstrap table containing member accounts.

The table may appear beneath the search interface or within a dedicated listing view.

The page should not require administrators to know a member's email address before they can access the account listing.

Suggested table columns:

| Column | Description |
| --- | --- |
| Member | Display name and account reference |
| Email | Registered email address |
| Mobile | Registered mobile number |
| Home city | Member's configured home city |
| Age | Calculated age, not date of birth |
| Gender | Member's selected gender |
| ID verification | Current verification state |
| Membership | Current membership tier |
| Membership status | Active, expired, cancelled or other supported state |
| Account status | Active, suspended, restricted or other supported state |
| Created | Account creation date |
| Actions | View account |

Membership tier, subscription status and account standing must remain distinct fields.

Do not combine them into an ambiguous single status.

Use existing data model values and do not invent unsupported states.

#### D. Allow administrators to choose visible table columns

The account table should support configurable column visibility.

Provide a **Columns** control that allows authorised administrators to select which fields appear.

For example:

- Email address.
- Mobile number.
- Home city.
- Age.
- Gender.
- ID verification state.
- Membership tier.
- Membership status.
- Account status.
- Account creation date.

Keep Member and Actions visible by default.

Column visibility preferences should be remembered for the administrator where practical.

Do not allow column preferences to bypass field-level authorisation.

Administrators should only be offered fields they have permission to view.

#### E. Add configurable account filtering

Provide a **Filters** control or filter panel.

Administrators should be able to filter accounts using relevant supported fields.

**Recommended filters**

| Field | Suggested filter |
| --- | --- |
| Email address | Exact or partial search |
| Home city | Searchable city selector |
| Phone number | Normalised lookup |
| Age | Minimum and maximum age |
| Gender | Multi-select |
| ID verification state | Multi-select |
| Membership tier | Free / Plus / Premium |
| Membership status | Supported subscription states |
| Account status | Supported account standing states |

Filters should support combinations.

For example, an authorised administrator may need to find:

- Members in Adelaide.
- Between 25 and 40 years old.
- With an active Plus subscription.
- Who have completed ID verification.

The table should display only accounts matching the selected criteria.

Provide:

- **Apply filters**
- **Clear filters**
- A visible indication of active filters.
- Total matching result count.

**Important:** Filter availability must follow administrator role permissions and authorised operational purposes.

Demographic filters such as age and gender should be restricted to roles with a legitimate need to use them. They should not automatically be available to every support or moderation user.

#### F. Use a proper Bootstrap data table

Use the existing Bootstrap-based Admin styling to create a professional account listing.

The table should support:

- Clear column headings.
- Consistent row heights.
- Appropriate spacing and padding.
- Sortable columns where supported.
- Server-side pagination.
- Server-side filtering.
- Configurable page sizes.
- Clear empty and loading states.
- Responsive desktop behaviour.
- Accessible keyboard navigation.

Suggested page sizes:

- 25 records.
- 50 records.
- 100 records.

Avoid fetching every account into the browser.

The design must remain usable with thousands or millions of member records.

For smaller viewports, provide appropriate horizontal scrolling or a responsive alternative rather than crushing every column into a narrow layout.

#### G. Separate quick lookup from account browsing

The two workflows should remain distinct.

**Quick lookup**

For administrators who already know a member's email address or mobile number.

Expected behaviour:

1. Enter identifier.
2. Click Find or press Enter.
3. Review matching account summary.
4. Select View account.

**Browse accounts**

For administrators investigating or reviewing groups of members.

Expected behaviour:

1. Select Browse all accounts.
2. View a paginated account table.
3. Choose visible columns.
4. Apply filters.
5. Sort results.
6. Open an account.

The quick lookup should not require loading the complete account table.

#### H. Improve account navigation

Selecting View account should open the existing Admin member account detail page.

The detail page should provide access to relevant information according to administrator permissions, including:

- Account overview.
- Contact information.
- Verification status.
- Membership and subscription information.
- Account standing.
- Relevant support and Trust and Safety functions.

When returning to the listing, preserve the administrator's previous search, filters, sorting and pagination where practical.

Do not require administrators to rebuild their search after reviewing an account.

#### I. Handle missing and incomplete data

Not every member will have every profile field populated.

The table must handle:

- Missing home city.
- Missing or undisclosed gender.
- Unavailable age information.
- Incomplete verification.
- Missing membership records.
- Incomplete onboarding.

Display a neutral value such as "Not provided" or "Not available" where appropriate.

Do not silently exclude members simply because optional profile fields are empty.

A member with incomplete onboarding should remain discoverable to an authorised administrator.

#### J. Maintain privacy, security and audit controls

The Accounts page contains personal information and must enforce existing administrative access controls.

Requirements:

- Authorise search and listing operations server-side.
- Apply field-level permissions to returned data.
- Restrict demographic filtering where appropriate.
- Avoid returning full dates of birth when age is sufficient.
- Mask phone numbers in summary results unless the administrator is authorised to view the complete number.
- Do not expose data through hidden columns, browser responses or client-side filtering.
- Rate-limit or otherwise protect bulk search and enumeration endpoints.
- Record appropriate administrative access and investigation activity.
- Prevent unauthorised bulk extraction of member information.
- Do not introduce unrestricted CSV exports or bulk download functionality as part of this issue.

The ability to browse all accounts must not imply unrestricted access to every member's sensitive information.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Navigate to `http://localhost:5082/accounts`.
3. Observe the "Exact email" search field.
4. Search for an existing member using their registered email address.
5. Observe the basic text-based search result.
6. Attempt to search using the member's registered mobile number.
7. Observe that mobile number searching is not supported.
8. Look for an option to browse all accounts.
9. Observe that no structured account listing is available.
10. Look for configurable filters and column visibility options.
11. Observe that these capabilities are not present.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Current Admin Accounts search results — `evidence/2026-11-01-issue-intake/18-accounts-screen-results-and-filter.png`.

The screenshot demonstrates:

- The current email-only search.
- Basic, unstructured account result presentation.
- Large unused desktop space.
- No visible account listing.
- No filtering controls.
- No configurable table columns.

### Impact

**Affected users:** Swoovr administrators, support personnel and authorised Trust and Safety staff.

**Impact:**

- Administrators cannot locate accounts using mobile numbers.
- Account investigations require knowing the exact registered email address.
- Support staff cannot efficiently browse or compare accounts.
- Locating accounts with shared characteristics requires manual investigation.
- The current interface will become increasingly inefficient as the member population grows.
- Basic search results provide insufficient context for efficient account identification.
- Administrative workflows require unnecessary additional navigation.

A structured Accounts interface is essential for efficient support, account management and Trust and Safety operations.

### Notes or constraints

**Design direction**

This is an administrative application, not a member-facing page.

Prioritise clear information hierarchy, efficient searching, readable tables and practical operational workflows.

Avoid card-based layouts for large account collections.

Use Bootstrap components and the existing Swoovr Admin design system.

**Phone number lookup**

- Use canonical international phone number normalisation.
- Support E.164 formatting.
- Support common spacing and punctuation variations.
- Support Australian national formatting where the country context is known.
- Do not use unreliable substring matching for exact phone lookup.
- Handle invalid or ambiguous phone number input clearly.

**Filtering and data protection**

- Use server-side search, filtering, sorting and pagination.
- Enforce role-based access controls.
- Apply field-level authorisation.
- Restrict sensitive demographic filters where required.
- Do not return sensitive fields merely because a column is hidden in the UI.
- Avoid unrestricted data export.
- Preserve existing privacy and audit requirements.

**Existing functionality**

- Preserve working email lookup.
- Reuse existing account detail routes.
- Reuse established membership and verification states.
- Do not alter member records merely to implement searching.
- Ensure account lookup works for members with incomplete profiles or onboarding.

**Related issues**

- **#2:** Admin account search fails to find existing users by email address.
- **#5:** Beta Enrollments table redesign.

The Accounts and Beta Enrollments pages should use consistent table, filtering and pagination patterns.

**Suggested implementation order**

1. Review existing Admin account search endpoints and data sources.
2. Add canonical mobile number lookup.
3. Redesign the quick-search results.
4. Introduce the Browse all accounts view.
5. Implement server-side pagination, sorting and filtering.
6. Add configurable column visibility.
7. Apply field-level permissions and relevant audit controls.
8. Improve responsive table styling and accessibility.
9. Test with large datasets and incomplete member records.
10. Verify consistency with the Beta Enrollments administration interface.

### Acceptance criteria

- [ ] Email address lookup continues to work.
- [ ] Mobile number lookup is supported.
- [ ] `+61412345678` and `61412345678` resolve to the same applicable phone number.
- [ ] Common phone formatting variations are handled correctly.
- [ ] Quick search results use a professional, structured account summary.
- [ ] Search results include a clearly labelled View account action.
- [ ] Browse all accounts is available to authorised administrators.
- [ ] Account listing uses a Bootstrap-based data table.
- [ ] Administrators can select which authorised columns are visible.
- [ ] Email, phone, home city, age, gender, ID verification and membership filtering are supported where authorised.
- [ ] Membership tier and membership status are displayed separately.
- [ ] Account status is distinguishable from membership status.
- [ ] Multiple filters can be combined.
- [ ] Server-side pagination and sorting are implemented.
- [ ] Page size can be configured.
- [ ] Total matching results are displayed.
- [ ] Incomplete account records are handled correctly.
- [ ] Table layouts remain usable across supported desktop and narrower viewports.
- [ ] Existing account details can be opened directly from search results and table rows.
- [ ] Search and filter state is preserved when returning from account details where practical.
- [ ] Sensitive fields and filters respect administrator permissions.
- [ ] Search endpoints enforce appropriate security and abuse protections.
- [ ] Automated tests cover email search, phone normalisation, combined filters, pagination and permissions.

<a id="intake-19"></a>

## 19. Redesign Admin member account detail page with complete profile inspection, role-based access and improved account management

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/accounts/{accountId}` (confirm actual route)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#156](https://github.com/repasscloud/wanderconnect/issues/156)
- **Tracking Q:** [Q111](QUESTIONS.md#q111)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q111](QUESTIONS.md#q111) / [#156](https://github.com/repasscloud/wanderconnect/issues/156) — Entire item 19; independent search, appeals, DOB/name and shared presentation work is cross-linked.**

Source-supported: Actual route is /accounts/{Id:guid}. Account.razor labels AgeStatus as Age and renders raw wire values, decision IDs, Plus grants and two staff-badge actions. ModerationEndpoints.SummaryAsync returns no numeric age/full DOB, but already supplies sanctions, entitlements, trips/review flags and an identity-on-file marker. D191 deliberately keeps decrypted verified identity separate. Existing case-scoped profile-at-report/evidence surfaces must be reused, not replaced by unrestricted chat inspection.

Requirements/dependencies: CONFIRMED D134 affiliation grants no permissions, D175 shared display name/main photo, D185/D191 restricted identity and D197 elevated access remain. DEFAULT: reuse existing capabilities and case links. OPEN: new sensitive-field inspection/preview scopes; per-mode main photos remain deferred under Q108.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

The current Swoovr Admin member account detail page provides limited information about a member and does not offer an effective way for authorised staff to investigate accounts, review profiles or resolve reports.

After locating a member through the Accounts search page and opening their record, administrators are presented with a collection of basic account attributes and administrative actions.

The page does not provide meaningful access to the member's complete profile configuration, connection modes, photographs, verification history, relevant reports or conversations.

Consequently, staff cannot easily determine what information a member has provided, what profile settings are enabled, what other members can see, or what activity may be relevant to an investigation.

The layout is also inconsistent, visually unpolished and resembles a collection of development forms rather than a production administrative interface.

**Specific problems identified**

1. **Insufficient account information.** The overview provides only a small number of account attributes, without access to the member's full profile or configuration.

2. **No profile inspection functionality.** Administrators cannot conveniently inspect completed profile fields, enabled connection modes, intentions, discovery visibility or member-facing profile presentation.

3. **No consolidated photo inspection.** There is no obvious way to review the member's uploaded photographs, moderation states or mode-specific photo assignments.

4. **No contextual conversation access.** There is no clear path from a member account to relevant reported conversations, message evidence or authorised conversation investigations.

5. **Poor presentation of account status.** Information appears as raw text such as `members · active` instead of properly labelled account and status fields.

6. **Incorrect age presentation.** The Age field displays `selfDeclaredAdult` rather than the member's actual calculated age.

7. **Date of birth is missing.** There is no clearly presented date-of-birth field for authorised staff.

8. **Raw internal values are displayed.** Values such as `notStarted`, `free` and `adelaide` are displayed without user-friendly formatting.

9. **Development identifiers are visible.** Headings include internal references such as `(D127)`, `(D129)`, `(D134)`, `(D149)`, `(D150)` and `(D151)`.

10. **Inconsistent form controls.** Input fields and buttons vary in size, spacing and presentation.

11. **Membership grant controls are poorly designed.** The "Plus for testers" section is presented as a separate development-oriented feature rather than an appropriate membership management function.

12. **Membership grant fields are too narrow.** The reason field truncates its placeholder text and does not provide adequate space for entering an explanation.

13. **Staff Badge management is confusing.** The interface provides actions such as "Set staff badge" even when a badge has already been assigned, without clearly displaying the current state.

14. **Administrative action history is poorly presented.** Records created through "Act without a report" appear as plain text separated by horizontal rules, with insufficient spacing and no proper table structure.

15. **No clear separation of sensitive information.** The page lacks an obvious permission-based structure distinguishing ordinary account information from protected identities, conversations and legal evidence.

### What I expected

The Admin member account detail page should become a comprehensive, professional account investigation and management interface.

Authorised staff should be able to inspect a member's account, understand their profile configuration, review relevant activity and perform permitted administrative actions without needing to navigate through disconnected tools.

The interface must respect Swoovr's established staff roles and permissions.

**This is not a request to give every administrator unrestricted access to member information or private conversations.**

The page should expose information and actions according to the staff member's specific permissions and the purpose of the investigation.

### A. Redesign the account overview

The top of the page should provide a clear, professionally formatted member summary.

Suggested information:

| Field | Expected presentation |
| --- | --- |
| Member | Display name |
| Account ID | Member reference |
| Email | Registered email address |
| Mobile | Registered mobile number |
| Account status | Active, Restricted, Suspended or other supported state |
| Age | Calculated age, e.g. 42 |
| Date of birth | Actual DOB, where authorised |
| Gender | Human-readable selected gender |
| Home city | Adelaide, South Australia, Australia |
| ID verification | Not started, Pending, Verified, Failed, etc. |
| Membership | Free, Plus or Premium |
| Membership status | Active, Expired, Cancelled, etc. |
| Staff affiliation | Enabled or Disabled |
| Created | Formatted account creation date |
| Last activity | Last recorded member activity, where available |
| Open cases | Number of relevant open cases |

Use actual supported values from the application's data model.

Do not fabricate missing information.

If a value has not been supplied, display an appropriate label such as "Not provided" or "Not available".

### B. Replace raw internal values with readable labels

The current interface displays values such as:

- `members · active`
- `selfDeclaredAdult`
- `notStarted · may start a check`
- `None on file`
- `free`
- `adelaide · moved 2 Nov 2026`

These are implementation-oriented representations rather than professionally formatted administrative information.

Display meaningful labels instead.

Examples:

| Current | Expected |
| --- | --- |
| `members · active` | Account type: Member / Status: Active |
| `selfDeclaredAdult` | Age assurance: Self-declared adult |
| `notStarted` | Not started |
| `may start a check` | Eligible to begin verification |
| `None on file` | No verified identity recorded |
| `free` | Free |
| `adelaide` | Adelaide |
| `moved 2 Nov 2026` | Home city last changed: 2 Nov 2026 |

**Important:** `selfDeclaredAdult` must not be presented as the member's age.

Age should be calculated from the stored date of birth where available.

Age assurance is a separate verification attribute and should remain labelled separately.

Use existing enums or domain values with appropriate display mappings rather than exposing raw enum identifiers.

### C. Display actual age and date of birth

The account overview should show the member's calculated age.

For example:

**Age: 42**

Where permitted, staff should also be able to see:

**Date of birth: 15 March 1984**

The example is illustrative only.

Age must be calculated from the member's stored date of birth using the appropriate date calculation.

Do not confuse:

- Declared age eligibility.
- Calculated age.
- Age assurance state.
- Verified date of birth.

These are separate concepts.

**Access requirements**

- Support and Admin may see calculated age where relevant to their authorised duties.
- Full DOB should be restricted to authorised age-assurance or investigation workflows.
- Legal officers may access DOB and protected identity information where specifically authorised.
- Root Admin does not automatically receive Legal-only identity access.

Do not display a complete DOB to every staff member by default.

### D. Introduce dedicated account detail sections

The page should not display every administrative function as one continuous list of cards.

Provide a structured interface with dedicated sections or tabs.

Suggested navigation:

1. Overview
2. Profile & Modes
3. Photos
4. Discovery & Preferences
5. Verification
6. Membership
7. Reports & Cases
8. Conversations
9. Account Actions
10. Activity & Audit
11. Legal & Evidence — restricted access

Not every section should appear to every role.

The navigation must be permission-aware.

A desktop sidebar, tabbed interface or similarly structured layout would be appropriate.

Avoid a single excessively long page.

### E. Provide complete profile inspection

Authorised administrators should be able to inspect the member's profile information.

This should include:

**Main profile**

- Display name.
- Main About Me.
- Home city.
- Area.
- Gender and pronouns.
- Sexual orientation, where access is authorised and relevant.
- Age visibility.
- Gender and pronoun visibility.
- Orientation visibility.
- Profile completion state.

**Connection modes**

- Dating enabled/disabled.
- Friends enabled/disabled.
- Activities enabled/disabled.
- Business enabled/disabled.
- Profile visibility for each mode.
- Selected intentions for each mode.
- Mode-specific About Me.
- Main About Me fallback setting.
- Other relevant mode configuration.

Clearly indicate whether a field is:

- Populated.
- Empty.
- Hidden from other members.
- Disabled.
- Not applicable.

Administrators should be able to distinguish between information the member has entered and information actually visible in discovery.

### F. Add member-facing profile preview

Provide a **View as member** or **Preview profile** function.

This should allow authorised staff to inspect how the member's profile appears in each enabled connection mode.

For example:

- Preview Dating profile.
- Preview Friends profile.
- Preview Activities profile.
- Preview Business profile.

The preview should reflect:

- The selected mode.
- Profile visibility settings.
- Main and additional photos.
- About Me text.
- Intentions.
- Visible identity information.

This is a read-only preview.

It must not impersonate the member, modify the account or create discovery activity.

Staff should be able to inspect a hidden or disabled mode's stored configuration when their investigation permissions allow it, but the preview must clearly indicate that the profile is not currently discoverable.

### G. Add photo inspection and moderation history

Provide a dedicated Photos section.

Authorised reviewers should be able to inspect:

- Uploaded photos.
- Moderation status.
- Upload timestamps.
- Shared photo library assignments.
- Main photo for each connection mode.
- Additional photos assigned to each mode.
- Relevant moderation history.
- Existing reports involving the photos.

The interface should clearly distinguish:

- Public profile photos.
- Mode-specific gallery photos.
- Private album media.
- Private-chat media.

Access to private or sensitive media must be independently authorised.

Ordinary account access must not automatically permit staff to reveal private images.

For authorised media review, preserve the existing blurred-preview, deliberate-reveal and auditing requirements.

### H. Add conversations and messaging investigation links

Provide a Conversations section allowing authorised staff to identify relevant conversation records associated with the member.

The section should not automatically expose all private message contents.

**Conversation metadata**

Where authorised, staff may be able to see:

- Conversation reference.
- Participants, subject to applicable identity masking.
- Creation date.
- Last activity timestamp.
- Conversation status.
- Whether a report or investigation is associated with the conversation.

**Message content**

Message contents require a separate, appropriately scoped access permission and investigation purpose.

Recommended behaviour:

- Support may access message evidence submitted with an authorised ordinary report, but not unrestricted unrelated chat history.
- Admin may inspect evidence relevant to authorised escalated cases, subject to the same investigation-scoping requirements.
- Legal officers may access preserved conversation evidence through the legal evidence workflow when explicitly authorised.
- Root Admin does not automatically receive Legal evidence access.

The interface should provide links to the relevant report, case or evidence viewer.

Do not introduce a general-purpose "read any member's chats" function.

Where broader content access is required for a legitimate investigation, it should use a separately authorised, audited process with a documented case or purpose.

### I. Apply role-based access controls

The account detail interface must enforce the existing four staff roles.

**Support**

Permitted:

- Review ordinary support cases.
- Inspect relevant account information.
- Review member-visible profile content relevant to a case.
- Review permitted case evidence.
- Dismiss eligible reports.
- Issue warnings.
- Apply or remove temporary restrictions up to 30 days.
- Handle authorised appeals.

Not permitted:

- Override Admin sanctions.
- Suspend or terminate accounts.
- Decide staff-affiliated cases.
- Access protected legal evidence.
- Retrieve protected identity information.
- Browse unrelated private conversations.

**Admin**

Includes the authorised Support capabilities, plus:

- Review escalated cases.
- Review staff-affiliated cases.
- Suspend accounts for up to 90 days.
- Permanently terminate accounts.
- Revoke public staff affiliation.
- Decide eligible appeals.
- Open provisional legal holds.

Opening a provisional legal hold must not grant access to preserved evidence.

Admin must not automatically gain Legal evidence or protected identity access.

**Legal / Security officer**

This is a separate specialist permission set.

Permitted according to the specific Legal grant:

- Manage legal holds.
- Access preserved evidence.
- Retrieve protected identities.
- Export authorised evidence.
- Record law-enforcement disclosures.
- Handle security incidents.

Legal does not automatically inherit Support or Admin moderation capabilities.

A Legal officer without Admin permission must not be able to issue ordinary sanctions simply because they can access preserved evidence.

**Root Admin (`IsRootAdmin`)**

One account only.

Includes Admin capabilities, plus:

- Appoint Legal officers.
- Revoke staff administrative access.
- Inspect sensitive-access logs.
- Manage security incidents.

Root Admin does not automatically receive Legal evidence permissions.

Legal evidence access requires an explicit Legal grant, even for Root Admin.

Mandatory MFA and the existing Root Admin authentication requirements must remain enforced.

### J. Enforce permissions in both UI and API

The UI must only display sections and actions available to the current staff member.

However, hiding a menu item is not sufficient.

All access checks must be enforced server-side.

Use capability-based permissions where appropriate rather than broad assumptions that Admin or Root can access all information.

Recommended permission areas include:

- Account summary.
- Profile inspection.
- Sensitive identity inspection.
- Public photo review.
- Private media investigation.
- Case-scoped conversation evidence.
- Legal evidence access.
- Membership management.
- Staff affiliation management.
- Account sanctions.
- Sensitive audit inspection.

Do not implement broad access simply because a staff member is authenticated.

### K. Redesign account status presentation

Replace the current raw text presentation with clearly labelled status indicators.

For example:

**Account status:** Active

**Membership:** Free

**ID verification:** Not started

Use Bootstrap status badges or equivalent components.

Appropriate colours may be used to distinguish statuses, such as:

- Active — success.
- Pending — neutral or informational.
- Restricted — warning.
- Suspended — warning or danger.
- Terminated — danger.

Do not rely on colour alone.

Display the actual status text.

Avoid combining unrelated fields into text such as `members · active`.

### L. Remove development identifiers

Remove internal references from user-facing headings, including:

- `(D127)`
- `(D129)`
- `(D134)`
- `(D149)`
- `(D150)`
- `(D151)`

Examples:

**Current:** ID verification (D129)

**Expected:** ID Verification

**Current:** Staff badge (D134)

**Expected:** Staff Badge

**Current:** Home city (D150)

**Expected:** Home City

These references belong in code, documentation or development tracking, not the application interface.

UAT must represent the production UI.

Do not maintain separate unfinished-looking UI components for UAT.

### M. Redesign membership grant management

Replace the development-oriented "Plus for testers" section with a proper membership administration section.

Suggested heading:

**Membership Management**

Display:

- Current membership tier.
- Membership status.
- Subscription or entitlement source.
- Current entitlement start date.
- Expiration date, where applicable.
- Relevant grant history.

For authorised manual grants, provide:

**Membership tier**

Free / Plus / Premium, subject to supported grant rules.

**Grant duration**

Number of days.

**Reason**

A properly sized multiline text area suitable for recording a meaningful explanation.

**Grant membership**

Primary action.

For example, an authorised administrator may grant Plus membership for 90 days with a documented reason.

The interface should clearly distinguish a complimentary administrative grant from a paid subscription.

Granting membership must not modify a Stripe subscription unless the existing business workflow explicitly requires it.

Use the existing entitlement system and audit requirements.

Avoid cramped inputs and truncated placeholder text.

### N. Redesign Staff Badge management

The Staff Badge section should display the actual current state.

Suggested presentation:

**Staff Badge**

Current state: Enabled / Disabled

Use a clearly labelled toggle or switch for authorised staff affiliation changes, subject to the established rules.

If the member already has an enabled badge, the control should reflect that state.

Do not continue displaying a generic "Set staff badge" action without indicating that the badge is already active.

Ensure:

- The current state is retrieved from authoritative account data.
- Changes require appropriate permissions.
- Relevant changes are confirmed where necessary.
- The previous and new states are audited.
- Public staff affiliation remains distinct from internal staff account permissions.

**Important:** A public Staff Badge must not grant administrative access.

Administrative roles and staff affiliation are separate concepts.

Support must not be able to decide staff-affiliated cases.

The existing rules for applying and revoking affiliation must remain authoritative. Do not introduce unrestricted badge changes through a simple frontend toggle.

### O. Improve administrative action controls

Sections such as:

- ID Verification.
- Age Assurance.
- Home City.
- Location and Arrival.
- Membership Management.
- Staff Badge.
- Account Sanctions.

should use consistent Bootstrap form controls and layout patterns.

Requirements:

- Consistent input heights.
- Consistent labels and helper text.
- Appropriate spacing between fields.
- Sensible button sizes.
- No touching or overlapping controls.
- Properly aligned actions.
- Clear validation messages.
- Consistent confirmation patterns.
- Uniform card and section styling.

Avoid placing numerous unrelated actions into large, repetitive blocks.

Sensitive actions should use dedicated confirmation dialogs or action panels where appropriate.

### P. Improve account sanctions and actions

The existing "Act without a report" section should be redesigned.

Suggested heading:

**Account Actions**

Provide a clear form for authorised actions such as:

- Warning.
- Temporary restriction.
- Other supported administrative sanctions.

Available actions must depend on the staff member's permissions.

Support must not be offered actions exceeding the 30-day restriction limit.

Admin may access additional permitted sanctions according to established policy.

Legal officers must not receive sanction capabilities merely because they have Legal access.

The form should include:

- Action type.
- Applicable duration.
- Restrictions, where supported.
- Structured reason.
- Optional detailed notes.
- Clear confirmation action.

A structured reason category should be used where practical.

Avoid relying exclusively on arbitrary free-text reasons.

### Q. Replace action history with a proper table

Currently, administrative action records appear as plain text separated by horizontal rules.

They are positioned too close to the Apply button and lack a useful information hierarchy.

Replace this with a dedicated table.

Suggested heading:

**Account Action History**

Suggested columns:

| Column | Description |
| --- | --- |
| Date | Action timestamp |
| Action | Warning, restriction, suspension, etc. |
| Reason | Structured reason category |
| Duration | Applicable period |
| Status | Active, expired, revoked, etc. |
| Staff member | Authorised actor or reference |
| Details | Open action record |

Requirements:

- Provide clear spacing between the action form and the history table.
- Use proper table headers and row separation.
- Support sorting where appropriate.
- Use readable status labels.
- Provide access to authorised action details.
- Avoid horizontal-rule-separated text blocks.
- Preserve historical actions rather than overwriting them.

If the member has many records, support pagination.

Do not expose restricted case information to roles without the required permissions.

### R. Improve page navigation and layout

The current page is excessively long and relies on vertically stacked administrative forms.

Redesign it for desktop administrative use.

Suggested structure:

**Header**

- Member display name.
- Member reference.
- Account status.
- Membership tier.
- Relevant quick actions.

**Primary account navigation**

- Overview.
- Profile & Modes.
- Photos.
- Verification.
- Membership.
- Reports & Cases.
- Conversations.
- Account Actions.
- Additional restricted sections where authorised.

**Main content**

Displays the selected section rather than every administrative form simultaneously.

**Contextual actions**

Present only the actions relevant to the selected section.

Use a suitable maximum content width without unnecessarily constraining the page to a narrow column.

Preserve consistent sidebar, header and navigation styling across the Admin application.

### S. Separate ordinary activity from protected evidence

Account activity and sensitive investigation evidence must not be treated as the same information.

**Ordinary account activity**

Examples include:

- Account creation.
- Profile updates.
- Verification state changes.
- Membership changes.
- Administrative warnings and restrictions.

These may be visible to appropriately authorised operational staff.

**Protected investigation evidence**

Examples include:

- Preserved private messages.
- Protected identity documents.
- Legal hold materials.
- Restricted media evidence.
- Law-enforcement disclosure records.

These require separate permissions and workflows.

Legal hold creation must not automatically grant access to the materials placed under hold.

All access to sensitive evidence must be explicitly authorised, appropriately scoped and audited.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Navigate to `http://localhost:5082/accounts`.
3. Search for an existing member account.
4. Open the member account detail page.
5. Observe the limited account summary.
6. Review raw values such as `members · active`, `selfDeclaredAdult`, `notStarted` and `free`.
7. Observe the absence of a clearly presented actual age and authorised DOB view.
8. Review internal development references in section headings.
9. Attempt to inspect the member's complete profile, enabled modes and profile visibility.
10. Attempt to inspect the member's photographs and mode assignments.
11. Look for links to relevant reports, cases and conversation evidence.
12. Review the Membership Management and Staff Badge controls.
13. Review the "Act without a report" form.
14. Create an authorised test action.
15. Observe the resulting history entries displayed as plain text with horizontal separators.
16. Compare the layout and available actions across Support, Admin, Legal and Root Admin roles.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Current Admin member account detail page — `evidence/2026-11-01-issue-intake/19-members-account-view-update.png`.

The screenshot demonstrates:

- Raw internal account values.
- Weak information hierarchy.
- Inconsistent form controls.
- Development identifiers in section headings.
- Limited profile inspection functionality.
- Poor membership management presentation.
- Confusing Staff Badge controls.
- Unstructured administrative action history.
- Excessively long account detail layout.

### Impact

**Affected users:** Swoovr Support, Admin, Legal/Security and Root Admin staff, according to their assigned permissions.

**Impact:**

- Staff cannot efficiently understand a member's account configuration.
- Investigating reports requires navigating disconnected administrative systems.
- Profile visibility and mode settings cannot be reviewed conveniently.
- Relevant photographs and conversation evidence are difficult to locate.
- Raw internal values make account information harder to interpret.
- Sensitive administrative actions are presented inconsistently.
- Membership grants and staff affiliation settings lack clear state presentation.
- Historical sanctions are difficult to review.
- Poor separation of sensitive information increases the risk of inappropriate access if new functionality is added without proper permissions.
- The page does not meet the expected standard for production administrative software.

This is a high-priority improvement because the member account detail page is central to support operations, account investigations and Trust and Safety workflows.

### Notes or constraints

**Authoritative staff role boundaries**

| Role | Permissions |
| --- | --- |
| Support | Ordinary cases, eligible appeals, warnings and restrictions up to 30 days. No admin sanction overrides or staff-affiliated case decisions |
| Admin | Support capabilities plus escalated and staff-affiliated cases, suspensions up to 90 days, permanent termination, eligible appeals and provisional legal holds |
| Legal | Separate specialist permissions for legal holds, preserved evidence, protected identities, evidence exports, law-enforcement disclosures and security incidents |
| Root Admin | Admin capabilities plus staff access administration, Legal appointments, sensitive-access logs and security incident controls |

Legal does not automatically inherit Admin or Support.

Root Admin does not automatically receive Legal evidence permissions.

Protected evidence access requires explicit authorisation.

**Technical requirements**

- Use server-side permission enforcement.
- Prefer granular capabilities over broad role-only checks.
- Keep Legal evidence access separate from ordinary moderation.
- Preserve existing audit requirements.
- Use human-readable mappings for enums and state values.
- Calculate age from DOB rather than presenting age-assurance state as age.
- Protect full DOB and other sensitive identity information.
- Do not expose private conversation contents through ordinary account lookup.
- Use case-scoped evidence access where appropriate.
- Preserve existing account data, sanctions, verification and membership records.
- Maintain existing business rules for staff affiliation.
- Use consistent Bootstrap components and responsive layouts.
- Remove development identifiers and placeholders from the UI.
- Do not expose sensitive data in frontend responses merely because the corresponding UI section is hidden.

**Related issues**

- **#18:** Admin account search, account listing and filtering.
- **#4:** Photo moderation and review workflow.
- **#5:** Beta Enrollments table redesign.
- **#3:** Admin session and authentication state handling.

The account detail page should use the same Admin design system and table conventions established by these related improvements.

**Suggested implementation order**

1. Review the current account detail data sources, API endpoints and permissions.
2. Define a role/capability matrix for each proposed section and action.
3. Redesign the account overview and status presentation.
4. Correct age, DOB and human-readable account information.
5. Remove development references and standardise form controls.
6. Introduce section-based account navigation.
7. Implement read-only profile and mode inspection.
8. Add member-facing profile previews.
9. Integrate authorised photo inspection and moderation history.
10. Add links to reports, cases and appropriately scoped conversation evidence.
11. Redesign membership grants and Staff Badge management.
12. Improve administrative sanctions and structured action history.
13. Implement restricted Legal and evidence navigation where permitted.
14. Validate backend permissions and audit logging.
15. Test each staff role independently.
16. Verify the complete interface using realistic accounts and investigation scenarios.

### Acceptance criteria

**Account overview**

- [ ] Member account overview uses a professional, structured layout.
- [ ] Raw enum values are replaced with human-readable labels.
- [ ] Account status uses clearly labelled status indicators.
- [ ] Membership tier and status are displayed separately.
- [ ] Age displays the calculated numerical age where DOB is available.
- [ ] Age assurance is displayed separately from age.
- [ ] Full DOB is available only to appropriately authorised staff.
- [ ] Internal development identifiers are removed from headings.
- [ ] Missing data is presented clearly without misleading placeholder values.

**Profile inspection**

- [ ] Authorised staff can inspect populated profile fields.
- [ ] Staff can see which connection modes are enabled.
- [ ] Staff can see mode-specific profile visibility.
- [ ] Staff can inspect selected intentions and About Me content.
- [ ] Hidden and empty fields are clearly distinguished.
- [ ] Read-only member-facing profile previews are available where authorised.

**Photos and conversations**

- [ ] Authorised reviewers can inspect relevant photos and moderation states.
- [ ] Mode-specific main and additional photo assignments are visible.
- [ ] Private media access requires appropriate permissions.
- [ ] Conversation metadata and relevant case links are available where authorised.
- [ ] Private message content is not exposed through unrestricted account browsing.
- [ ] Protected conversation evidence requires explicit investigation or Legal permissions.
- [ ] Sensitive access and photo reveal actions are audited.

**Administrative controls**

- [ ] Membership Management replaces "Plus for testers".
- [ ] Authorised staff can grant supported membership entitlements for a defined duration.
- [ ] Membership grant reasons use appropriately sized input controls.
- [ ] Staff Badge clearly displays Enabled or Disabled.
- [ ] Staff Badge changes follow existing permission rules.
- [ ] Form fields and buttons use consistent Bootstrap styling and spacing.
- [ ] Administrative actions are clearly grouped by purpose.
- [ ] Account Action History uses a structured table.
- [ ] Action history is visually separated from the action form.
- [ ] Existing action records remain accessible according to permissions.

**Security and role boundaries**

- [ ] Support cannot exceed its 30-day restriction authority.
- [ ] Support cannot override Admin sanctions or decide staff-affiliated cases.
- [ ] Admin can perform only its established additional actions.
- [ ] Legal does not automatically inherit Admin or Support capabilities.
- [ ] Root Admin does not automatically receive Legal evidence access.
- [ ] Provisional legal holds do not grant preserved-evidence access.
- [ ] Role-specific sections and actions are hidden when unauthorised.
- [ ] All protected operations are authorised server-side.
- [ ] Sensitive access is logged appropriately.
- [ ] Automated tests cover each role's allowed and denied operations.

**Design and usability**

- [ ] Desktop layout uses the available space effectively.
- [ ] Account information is organised into logical sections.
- [ ] The page no longer consists of one excessively long sequence of unrelated forms.
- [ ] Buttons and fields do not touch or overlap.
- [ ] Forms, labels and status indicators are visually consistent.
- [ ] Tables are readable, sortable and paginated where appropriate.
- [ ] Navigation remains consistent across the Admin application.
- [ ] UAT uses the same production-intended UI without development-only labels.

<a id="intake-20"></a>

## 20. Improve Beta Enrollments UI and introduce configurable Beta and Public Registration controls

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin / API / Member web
- **Page or URL:** `http://localhost:5082/beta`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#157](https://github.com/repasscloud/wanderconnect/issues/157); visual refinements reuse [#140](https://github.com/repasscloud/wanderconnect/issues/140)
- **Tracking Q:** [Q112](QUESTIONS.md#q112); existing [Q98](QUESTIONS.md#q98) for styling

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q112](QUESTIONS.md#q112) / [#157](https://github.com/repasscloud/wanderconnect/issues/157) — Item 20 A–F and registration criteria only. Visual refinements G–L reuse Q98/#140.**

Source-supported: AccountResolver reads static Beta:Enabled (default true) for application access. The generated member realm separately has registrationAllowed=true. D237 explicitly distinguishes Keycloak user creation from application registration/adoption; the beta access check is not evidence that Keycloak creation is closed. No persisted two-flag Root-managed policy was found in these sources. PR #153 merged guest registration navigation but did not add this policy.

Requirements/dependencies: CONFIRMED D62 invitation-only beta, D127 age/email/mobile gates and Keycloak ownership remain. OPEN: approve the proposed four-state model, Root-only mutation/view matrix, shared authority and supported provider enforcement/propagation contract before implementation. DEFAULT: fail closed on unavailable authority. No public launch or provider configuration change is authorised.

**Duplicate disposition — Q98 / [#140](https://github.com/repasscloud/wanderconnect/issues/140):** G–L and the Beta page checklist extend the existing table/layout issue. The source already uses `td { vertical-align: top; }`; child paragraph margins are a hypothesis for first-line misalignment, not a reproduced cause. Added expected acceptance for realistic email widths, consistent filters/Apply/Clear, first-line alignment and narrow/keyboard layouts while preserving existing data, provider labels and confirmed actions. No separate visual issue created.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

The Beta Enrollments page has been updated to use a table-based layout, which is an improvement over the previous card-based design.

However, several visual inconsistencies remain, and there is currently no clearly defined administrative mechanism to enable or disable private beta registration and public account registration.

Beta enrollment is intended to be a temporary access-control mechanism during Swoovr's pre-launch testing period.

Once Swoovr launches publicly, members should be able to register without requiring their email addresses to have been pre-authorised.

The registration system therefore needs independently configurable controls for beta enrollment and public registration.

**Problems identified**

1. **Email input is too narrow.** The "Email to pre-authorise" field is not wide enough for typical email addresses.

2. **Inconsistent filter controls.** Search, Status, Sort, Created from, Created to and Per page use different heights, widths and styling.

3. **Buttons are inconsistent with form controls.** Apply filters and Clear filters do not follow a consistent alignment or visual hierarchy.

4. **Table cell alignment is inconsistent.** Email Invitation and iOS Distribution content appears vertically lower than content in the other columns.

5. **No clear registration availability controls.** Administrators cannot readily determine whether beta enrollment or public registration is enabled.

6. **No defined registration mode management.** There is no obvious mechanism to transition from private beta to public registration, or temporarily disable all new registrations.

7. **Registration navigation does not reflect availability.** The member-facing Create account/Register action should disappear when all registration methods are disabled.

### What I expected

The Beta Enrollments page should retain its existing table-based layout while improving form consistency, spacing and usability.

Additionally, Swoovr should introduce two independent registration configuration flags:

- **Beta registration enabled**
- **Public registration enabled**

These flags must determine whether new accounts can be created and whether beta pre-authorisation is required.

### A. Introduce registration availability controls

Provide two configurable settings.

**Beta registration enabled**

Controls whether new registrations can proceed through the private beta enrollment process.

When enabled:

- Beta enrollment pre-authorisation is enforced for the private beta registration path.
- Eligible invited members can register.
- Accounts without the required beta eligibility cannot register through the private beta path, unless public registration is also enabled.
- Existing member sign-in remains unaffected.

**Public registration enabled**

Controls whether members may register without prior beta enrollment.

When enabled:

- New members can register without a beta invitation.
- Beta pre-authorisation is not required for public registration.
- Normal registration, age eligibility, verification and onboarding requirements remain enforced.

The controls must not affect existing accounts or revoke previously granted access.

### B. Define how both registration flags work together

The two flags should produce the following behaviour:

| Beta enabled | Public enabled | Registration behaviour |
| --- | --- | --- |
| On | Off | Private beta only. Registration requires valid beta eligibility |
| Off | Off | Registration closed. No new accounts can be created |
| Off | On | Public registration open. No beta enrollment required |
| On | On | Public registration takes precedence. Beta enrollment is not required |

For the fourth combination, the system should permit registration publicly while retaining beta enrollment functionality for existing beta participants or administrative tracking.

However, this configuration should not be necessary for normal operations.

The Admin interface should explain the effective registration state.

Suggested labels:

- **Private beta**
- **Registration closed**
- **Public registration**
- **Public registration with beta tracking**

Do not allow conflicting or ambiguous registration behaviour.

### C. Provide a dedicated Registration Settings area

Registration controls should not be buried among individual beta enrollment records.

Introduce a clearly separated **Registration Settings** section, preferably within an appropriate Admin configuration area, with a link or summary on the Beta Enrollments page.

Suggested layout:

**Registration Settings**

Beta registration

[On / Off]

"Require beta pre-authorisation when public registration is closed."

Public registration

[On / Off]

"Allow anyone meeting Swoovr's eligibility requirements to register."

Current registration mode:

**Private beta**

The controls should clearly display their current persisted values.

Only authorised administrators should be able to change these settings.

Recommended permission boundary:

- Support: View registration status only, where authorised.
- Admin: View status.
- Legal: No automatic access to registration configuration.
- Root Admin: View and modify registration availability.

This is a platform-wide operational control, not an ordinary member moderation setting.

### D. Ensure registration restrictions are enforced server-side

The configuration must be enforced by the registration backend and identity provider.

Do not rely solely on hiding the Create account button.

When registration is closed:

- New account creation must be rejected through all registration entry points.
- Direct access to Keycloak registration endpoints must not bypass the restriction.
- API-based or alternative registration flows must respect the same settings.
- Existing accounts must remain able to sign in.
- Existing member sessions must remain unaffected.
- Password recovery must continue to function.

When beta-only registration is enabled:

- Validate beta eligibility using the canonical registered email address.
- Apply the existing invitation and enrollment state rules.
- Prevent unauthorised registrations.
- Ensure beta registration decisions are made by authoritative backend services.

When public registration is enabled:

- Remove the beta enrollment requirement from normal account creation.
- Continue enforcing all ordinary eligibility and security requirements.

**Keycloak consideration:** Investigate how the existing Keycloak registration flow can consume the authoritative registration policy. Use supported authentication extensions or configuration mechanisms where required.

Do not implement a frontend-only restriction that can be bypassed through direct Keycloak URLs.

### E. Update member-facing registration navigation

The member web application must reflect the current registration availability.

**When registration is available**

Display the normal Create account/Register action on the unauthenticated landing and sign-in screens.

**When both registration flags are disabled**

- Hide the Create account/Register action.
- Keep Sign in available.
- Keep Forgot password available.
- Do not display broken registration links.

If someone accesses the registration page directly, display a professionally designed, Swoovr-branded message:

**Registration is currently closed**

"We're not accepting new registrations at the moment."

Provide a **Back to sign in** action.

Do not expose infrastructure configuration details or internal development messages.

The same policy should apply to mobile web, native iOS and Android authentication entry points.

### F. Make registration configuration persistent and auditable

Registration availability must be stored in an authoritative, shared configuration source.

The configuration must:

- Survive application restarts.
- Apply consistently across multiple API instances.
- Be available to the registration/identity-provider flow.
- Support safe changes without redeploying applications.
- Avoid inconsistent registration behaviour between instances.

Record configuration changes with:

- Administrator ID.
- Previous values.
- New values.
- Timestamp in UTC.
- Reason for change.

A short reason should be required when changing registration availability.

Consider a confirmation dialog when closing all registration or enabling unrestricted public registration.

**Fail-safe behaviour:** If the authoritative policy cannot be retrieved, registration must not silently default to open. Use a defined secure fallback and make configuration failures visible to authorised operations staff.

### G. Improve Email to pre-authorise field

The current email input is too narrow.

Increase it to a sensible width suitable for normal email addresses.

Expected behaviour:

- Use a standard Bootstrap email input.
- Provide enough width to display a typical email address.
- Apply consistent height and padding.
- Align the Add enrollment button appropriately.
- Validate email formatting.
- Display clear success and error feedback.

On desktop, the email input and Add enrollment action may appear horizontally.

On mobile, they may stack with appropriate spacing.

Do not allow controls to touch.

When beta registration is disabled, retain access to historical enrollment records, but clearly indicate that new pre-authorisations are not currently being enforced. Disabling beta registration should not automatically delete pending enrollments.

### H. Standardise all filtering controls

The following controls must use consistent Bootstrap styling:

- Search.
- Status.
- Sort.
- Created from.
- Created to.
- Per page.
- Apply filters.
- Clear filters.

Requirements:

- Consistent input heights.
- Consistent label typography.
- Consistent horizontal and vertical padding.
- Proper alignment within the filter row.
- Appropriate gaps between controls.
- Sensible widths according to the expected content.
- No overlapping or touching fields.
- Consistent focus and disabled states.

Dates should use the same date-picker styling and format.

Avoid mixing native-looking inputs with heavily customised controls unless their visual presentation is consistent.

### I. Improve filter layout and responsiveness

The filter interface should have a clear visual hierarchy.

Suggested desktop arrangement:

**Primary filters**

- Search.
- Status.
- Sort.

**Date and pagination filters**

- Created from.
- Created to.
- Per page.

**Actions**

- Apply filters.
- Clear filters.

The exact arrangement may vary with available screen width.

Use Bootstrap grid and spacing utilities to produce consistent alignment.

Avoid forcing all controls into a single row when it creates cramped fields.

On narrower screens, controls should wrap naturally while maintaining consistent spacing.

Apply filters should be the primary action.

Clear filters should be a secondary or outline action rather than an inconsistently positioned plain hyperlink.

### J. Correct table column vertical alignment

The existing table layout is generally acceptable and should be retained.

However, the content under the following columns appears vertically lower than adjacent cells:

- Email Invitation.
- iOS Distribution.

All table cells should align to the top.

Use consistent cell padding and vertical alignment.

For example, ensure Bootstrap table cells use the equivalent of:

`vertical-align: top;`

The first line of content in every column should begin at the same vertical position.

Secondary information may appear underneath, using consistent spacing.

Do not centre some columns vertically while top-aligning others.

### K. Improve table consistency

Retain the current columns:

- Member / Email.
- Enrollment.
- Email Invitation.
- iOS Distribution.
- Actions.

Improve presentation where necessary:

- Consistent cell padding.
- Top-aligned content.
- Clear primary and secondary text.
- Consistent date formatting.
- Appropriate status indicators.
- Readable reference identifiers.
- Consistent action links and buttons.

Do not allow internal diagnostic values to dominate the table.

Existing invitation and distribution information should remain accessible, but technical details may be moved into expanded enrollment details where appropriate.

### L. Maintain existing Beta Enrollments functionality

The redesign must preserve:

- Pre-authorisation by email.
- Enrollment status tracking.
- Existing beta invitation workflow.
- Email invitation status.
- iOS distribution tracking.
- Search, filtering, sorting and pagination.
- Enrollment management actions.
- Existing audit history.

Registration availability settings must not delete or modify historical beta enrollment data.

When public registration becomes available, beta enrollment records should remain accessible for reporting and historical purposes.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Navigate to `http://localhost:5082/beta`.
3. Observe the existing Beta Enrollments table.
4. Review the width of the Email to pre-authorise input.
5. Compare the heights and styles of Search, Status, Sort, Created from, Created to and Per page.
6. Compare the alignment of Apply filters and Clear filters.
7. Observe the vertical alignment of Email Invitation and iOS Distribution table cells.
8. Look for controls to enable or disable beta registration and public registration.
9. Confirm that no clearly defined registration availability management interface is present.
10. Review the current member-facing registration behaviour when registration availability changes or is intended to be closed.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Current Beta Enrollments page — `evidence/2026-11-01-issue-intake/20-beta-enrollments-page-visual-update.png`.

The screenshot demonstrates the current filter alignment, narrow email input and inconsistent table cell positioning.

The registration availability controls are a new requirement and are not illustrated in the screenshot.

### Impact

**Affected users:** Swoovr administrators, beta participants and prospective members.

**Impact:**

- Inconsistent form styling reduces administrative usability.
- Narrow fields make enrollment entry awkward.
- Misaligned table content reduces readability.
- There is no clearly controlled transition from private beta to public registration.
- Registration may remain incorrectly restricted after public launch.
- The platform lacks a straightforward way to temporarily close new registrations.
- Frontend-only registration restrictions could be bypassed if backend enforcement is not implemented.
- Multiple API and authentication instances may apply inconsistent policies without an authoritative shared configuration.

This is a high-priority improvement because registration availability is a platform-wide operational control required for beta testing, launch and ongoing operations.

### Notes or constraints

**Registration state model**

Implement two independent flags:

- `BetaRegistrationEnabled`
- `PublicRegistrationEnabled`

Recommended evaluation:

1. If public registration is enabled, eligible members can register without beta pre-authorisation.
2. Otherwise, if beta registration is enabled, a valid beta enrollment is required.
3. Otherwise, registration is closed.

The flags must not affect existing member sign-in.

**Security and permissions**

- Restrict configuration changes to Root Admin initially.
- Enforce registration policy server-side and through Keycloak.
- Audit every configuration change.
- Do not allow public registration to become enabled accidentally through configuration failure.
- Preserve existing email verification, mobile verification, age eligibility and onboarding requirements.
- Prevent registration bypasses through direct identity-provider endpoints.

**Infrastructure**

The flags may be stored in the application database or another authoritative shared configuration system.

They should not be implemented exclusively as static environment variables if administrators are expected to change them at runtime.

Ensure consistency across multiple Azure API instances and Keycloak.

A distributed cache may be used if appropriate, but stale values must not permit registration contrary to the authoritative policy.

**Related issues**

- **#5:** Beta Enrollments table redesign.
- **#7:** Public landing page and unauthenticated navigation.
- **#8:** Unauthenticated account routing.
- **#9:** Sign-in design.
- **#10:** Registration design.
- **#11:** Registration error presentation.
- **#18:** Admin account searching and listing.

These issues should use consistent Admin and member-facing design components where applicable.

**Suggested implementation order**

1. Review the current beta eligibility and registration architecture.
2. Define and persist the two registration configuration flags.
3. Implement authoritative registration policy evaluation.
4. Integrate enforcement into the existing Keycloak registration flow.
5. Add Root Admin registration configuration controls.
6. Update public landing, sign-in and registration navigation.
7. Implement the closed-registration screen.
8. Audit configuration changes.
9. Standardise Beta Enrollments form controls.
10. Correct table cell alignment.
11. Validate registration behaviour across all four flag combinations.
12. Test desktop, mobile web and native app entry points.

### Acceptance criteria

**Registration configuration**

- [ ] Beta registration can be independently enabled or disabled.
- [ ] Public registration can be independently enabled or disabled.
- [ ] Private beta requires valid beta eligibility when public registration is disabled.
- [ ] Public registration does not require beta enrollment.
- [ ] Disabling both registration methods prevents new account creation.
- [ ] Existing members can continue signing in when registration is closed.
- [ ] Forgot password remains available when registration is closed.
- [ ] Registration settings are persistent across application restarts.
- [ ] All API and identity-provider instances follow the same authoritative policy.
- [ ] Only appropriately authorised staff can modify registration availability.
- [ ] Configuration changes are audited.
- [ ] Registration remains closed on a policy retrieval failure rather than silently opening.

**Member-facing behaviour**

- [ ] Create account/Register is hidden when both registration methods are disabled.
- [ ] Registration remains accessible when either permitted registration method is enabled.
- [ ] Direct registration endpoints cannot bypass the configured policy.
- [ ] Closed registration displays a properly branded explanation.
- [ ] Existing authentication and onboarding requirements remain intact.
- [ ] Desktop, mobile web and native app registration entry points behave consistently.

**Beta Enrollments page**

- [ ] Email to pre-authorise input is appropriately sized.
- [ ] Search, Status, Sort, Created from, Created to and Per page use consistent control heights and styling.
- [ ] Apply filters and Clear filters have consistent alignment and spacing.
- [ ] Filter controls adapt correctly to narrower viewports.
- [ ] Table cells are consistently top-aligned.
- [ ] Email Invitation and iOS Distribution content aligns with other columns.
- [ ] Existing table functionality and enrollment records are preserved.
- [ ] Beta enrollment history remains accessible after beta registration is disabled.
- [ ] The page uses consistent Bootstrap components and follows the Swoovr Admin design system.

<a id="intake-21"></a>

## 21. Admin page refresh intermittently displays raw form submission error in Safari

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin
- **Page or URL:** `http://localhost:5082/media` (potentially other Admin routes)
- **Type:** Bug
- **Priority:** Medium
- **GitHub issue:** [#158](https://github.com/repasscloud/wanderconnect/issues/158)
- **Tracking Q:** [Q113](QUESTIONS.md#q113)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q113](QUESTIONS.md#q113) / [#158](https://github.com/repasscloud/wanderconnect/issues/158) — Entire item 21, including the additional approve-form report.**

Source-supported: User reports both missing form 'search' and a removed approve-{mediaId} form after refreshing /media. MediaReview.razor renders dynamic item-bound POST form names; successful decisions reload a queue in which that item may disappear. ContactHistory.razor contains the separate POST form 'search'; Accounts/Beta search uses GET. Program.cs registers static SSR and enables exception handling only outside Development. These facts support investigating stale POST replay but do not establish the request method, route or cause.

Requirements/dependencies: DEFAULT hypothesis: Safari refresh may replay an SSR POST whose named form no longer exists. OPEN cause: request/response trace is absent; not proven to be a session-expiry defect. Keep dynamic form identity/antiforgery and avoid navigation rewrites without evidence.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

When using the Swoovr Admin application in Safari, refreshing a page sometimes causes the application to display a raw error message instead of reloading the current page.

This occurred while signed in as an administrator on:

`http://localhost:5082/media`

The browser displays:

> Cannot submit the form 'search' because no form on the page currently has that name.

The normal Swoovr Admin interface disappears and is replaced by a plain text error response.

Once this happens, refreshing the page again does not resolve the problem.

To recover, I must manually change the browser URL back to `http://localhost:5082/` and navigate to the required page again.

**Observed behaviour:**

1. Administrator is authenticated and using the Admin application normally.
2. Administrator navigates to `/media`.
3. Refreshing the page intermittently causes an error.
4. The application displays a raw text response instead of the Admin interface.
5. Further refresh attempts continue displaying the error.
6. Navigating manually to `/` restores normal functionality.

The issue has been observed in Safari, which is the browser used for Admin testing.

### What I expected

Refreshing any Swoovr Admin page should reload that page successfully without losing the authenticated session or requiring navigation back to the application root.

Specifically:

- Refreshing `/media` should reload `/media`.
- The Admin layout should render normally.
- The authenticated administrator should remain signed in when the session is valid.
- Form state or interactive components should not prevent direct page loading.
- The application should not attempt to submit a form that is no longer present.
- Raw framework error messages should never be displayed to the user.

If a genuine error occurs, the application should display a properly styled Admin error page with a recovery action.

### Steps to reproduce

1. Open Safari.
2. Sign in to the Swoovr Admin application at `http://localhost:5082/`.
3. Navigate to `http://localhost:5082/media`.
4. Refresh the page using `Cmd + R`.
5. Repeat navigation and refresh attempts until the intermittent error appears.
6. Observe the raw error message:

   `Cannot submit the form 'search' because no form on the page currently has that name.`

7. Attempt to refresh again.
8. Observe that the error remains.
9. Navigate manually to `http://localhost:5082/`.
10. Confirm that the Admin application becomes accessible again.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Safari Admin refresh error — `evidence/2026-11-01-issue-intake/21-admin-safari-refresh-form-error.png`.

The screenshot shows Safari displaying a plain text error response, with the message indicating that the named form `search` could not be found.

### Impact

**Affected users:** Administrators using Safari, potentially other browsers.

**Impact:**

- Refreshing an Admin page can unexpectedly interrupt work.
- The current page becomes inaccessible until the administrator manually returns to `/`.
- Normal browser refresh behaviour is unreliable.
- The application exposes an internal framework error instead of a professional error screen.
- The issue creates unnecessary navigation and disrupts administrative workflows.

The problem is intermittent and currently has a straightforward workaround, so Medium priority is appropriate.

### Notes or constraints

**Investigation focus**

The error suggests a possible issue with ASP.NET Core / Blazor form handling.

Investigate:

- Whether a form named `search` is being submitted during navigation or refresh.
- Whether `FormName` and `EditForm` declarations are correctly configured.
- Whether duplicate or stale form references exist.
- Whether static SSR and interactive component behaviour are interacting incorrectly.
- Whether Safari is restoring form submission state during page reload.
- Whether enhanced form handling or navigation is involved.
- Whether a POST request is being replayed or routed incorrectly.

The exact cause has not been confirmed.

**Implementation requirements**

- Keep the fix focused on the underlying refresh/form-handling problem.
- Do not introduce unnecessary changes to the Admin navigation architecture.
- Preserve existing search and filtering functionality.
- Ensure direct navigation to Admin routes works correctly.
- Do not require administrators to return to `/` to recover from a refresh.
- Ensure unexpected errors use the standard Swoovr Admin error presentation.
- Avoid exposing raw framework exceptions or technical details to the browser.

**Related issue**

- **#3:** Admin session incorrectly reports expiration and displays inconsistent authentication pages.

The problems may be unrelated, but both involve unexpected interruptions during Admin navigation.

Note: Was on the media page, and I know a new photo needed to be reviewed, so I refreshed the page and it returned this error: `Cannot submit the form 'approve-5d8b6e7c-a15f-4447-87a8-102a1f35795c' because no form on the page currently has that name.`

### Acceptance criteria

- [ ] Refreshing `/media` in Safari reloads the page successfully.
- [ ] Repeated refreshes do not produce missing-form errors.
- [ ] Direct navigation to `/media` works without first visiting `/`.
- [ ] Existing Admin search and filtering forms continue working.
- [ ] Valid authenticated sessions remain intact during refresh.
- [ ] Other Admin routes are checked for the same issue.
- [ ] Safari behaviour is verified.
- [ ] Raw framework error messages are not displayed to users.
- [ ] Any unexpected failure displays a consistent Swoovr Admin error page.
- [ ] The underlying form submission issue is corrected rather than hidden.

<a id="intake-22"></a>

## 22. Redesign Admin Photo Review for high-volume moderation, member appeals and rejected-photo management

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin / Member web / API
- **Page or URL:** `http://localhost:5082/media`
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#159](https://github.com/repasscloud/wanderconnect/issues/159), [#160](https://github.com/repasscloud/wanderconnect/issues/160)
- **Tracking Q:** [Q114](QUESTIONS.md#q114), [Q115](QUESTIONS.md#q115)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q114](QUESTIONS.md#q114) / [#159](https://github.com/repasscloud/wanderconnect/issues/159) — Item 22 A–K/T, queue/history/interface acceptance; L–S split to Q115.**

Source-supported: MediaReview.razor renders inline list items and approve/reject/escalate details, separate reveal/conceal forms and a 30-day coded-count expander. MediaEndpoints review selects up to 100 uncertain items, with a separate pending-screening count, not a paginated workload query. Home.razor shows case/appeal/hold counts but no photo-review total. PR #152 merged factual purpose, audited reveals and stable decision codes. No server-side four-hour/versioned safety acknowledgement was found in the inspected review path.

Requirements/dependencies: CONFIRMED D178 screening/eligibility and restricted evidence remain. DEFAULT: reuse coded controls and existing lifecycle locks. OPEN: four-hour acknowledgement and governed private approval-note history (Q97). Support access is not implied: the current general media queue requires Admin. Retention and appeals are split to Q115.

**[Q115](QUESTIONS.md#q115) / [#160](https://github.com/repasscloud/wanderconnect/issues/160) — Item 22 L–S and rejected-photo/member-notification/appeal acceptance.**

Source-supported: MediaPipeline.RejectAsync records rejection and invokes DeleteObjectIfAllowedAsync; rejected objects can be deleted when preservation permits. MediaEndpoints review/view handles uncertain items; ordinary decisions accept uncertain/passed states, not a general rejected-photo override. Existing sanction appeals (D135/#50) do not themselves implement photo-specific appeals. The current review code does not establish member rejection notifications or durable photo-appeal history.

Requirements/dependencies: CONFIRMED D135 sanction appeal independence/30-day window must be reconciled explicitly for photo decisions, not silently generalised or weakened. CONFIRMED D178/D185/D191 evidence restrictions remain. OPEN: photo-specific eligibility/window/finality, reviewer capability and bounded retention; no blanket retention or ordinary approval of specialist-blocked media.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

The Swoovr Admin Photo Review page is unnecessarily large, visually cluttered and inefficient for staff who may need to review hundreds of photographs per hour.

The current interface displays extensive explanatory text, development references, oversized photo review sections and multiple expanded moderation forms.

Each photograph occupies a substantial amount of screen space, making it difficult to work through a queue efficiently.

The page also lacks a complete workflow for rejected photographs, member notifications, appeals and administrative reconsideration.

**Problems identified:**

1. Excessive explanatory text at the top of the page, including references to beta rules and internal development codes.
2. Important child sexual abuse material (CSAM) handling instructions are buried within ordinary page text.
3. "Coded reviews since..." appears as an expandable section rather than a dedicated review history interface.
4. Each photograph occupies an excessively large section of the page.
5. Photo previews and moderation actions are displayed inline instead of inside a focused review dialog.
6. Native-looking dropdowns are inconsistent with the Swoovr Admin design system.
7. Revealing and concealing a photograph use separate controls rather than one state-aware toggle.
8. Revealed photographs consume excessive screen space.
9. There is no prominent pending-photo count on the Admin dashboard or moderation navigation.
10. Rejection reasons are not clearly communicated to the affected member through notifications.
11. Members do not have a clear method to appeal a rejected photograph.
12. Administrators do not have an organised Rejected Photos queue for reconsideration or overriding previous decisions.

### What I expected

The Photo Review page should become a compact, professional and high-throughput moderation workspace.

The primary page should focus on identifying photographs awaiting review.

Detailed inspection and decision-making should take place inside an appropriately sized modal dialog or dedicated review panel.

The system must also support rejected-photo management, member notifications, appeals and authorised review overrides.

### A. Simplify the page header

Replace the existing lengthy introduction with a concise heading and description.

**Photo Review**

"Review photographs flagged for manual moderation."

Remove routine display of:

- Beta-related wording.
- Internal development identifiers such as `(D178)`.
- Unnecessary technical explanations.
- Repeated references to how the application is implemented.
- Excessive instructional text that does not help the reviewer perform the task.

Technical screening details should remain accessible in the review detail view where useful.

The page should not resemble development documentation.

Do not remove important safety requirements or warnings. Present them in the appropriate context instead.

### B. Introduce a dismissible CSAM safety warning

The existing warning about suspected child sexual abuse material is important and should be retained.

However, it should not occupy a permanent paragraph at the top of every Photo Review page.

Display a dedicated safety notice when an authorised reviewer first enters the media moderation workspace.

Suggested heading:

**Important safety guidance**

Suggested message:

"If you encounter suspected child sexual abuse material, stop reviewing the image immediately. Do not download, copy or share it. Use the specialist escalation process so the appropriate Trust and Safety or Legal personnel can handle it."

Provide a clear action:

**I understand**

**Behaviour**

- Display the notice when a reviewer enters Photo Review and no valid acknowledgement exists.
- Require acknowledgement before revealing potentially sensitive images.
- Store acknowledgement server-side, associated with the staff account and current notice version.
- Suppress repeated display for four hours after acknowledgement.
- Show the notice again after four hours when the reviewer next enters the moderation workspace.
- Re-display immediately if a materially revised safety notice is issued.
- Audit acknowledgement where appropriate.

A secure cookie may assist with presentation state, but should not be the sole authoritative record.

The notice must not replace the actual specialist escalation controls.

Keep the warning concise, accessible and suitable for professional staff.

### C. Replace oversized photo review blocks with a compact table

The main Photo Review page should display queued photographs in a structured Bootstrap table.

Suggested columns:

| Column | Description |
| --- | --- |
| Photo | Small blurred thumbnail or placeholder |
| Member | Display name or member reference |
| Uploaded | Upload date and time |
| Context | Profile, mode gallery or other applicable media type |
| Screening | Reason the photograph entered manual review |
| Status | Pending review, escalated, etc. |
| Actions | Review photo |

The table should:

- Use consistent row heights.
- Display concise information.
- Support pagination.
- Support sorting and filtering.
- Allow quick access to the selected photograph.
- Avoid showing full-sized images and approval forms directly in the table.

Do not automatically reveal potentially sensitive content in thumbnails.

A blurred thumbnail or appropriate placeholder is sufficient.

**Target:** A reviewer should be able to scan many queued photographs without scrolling through large individual cards.

### D. Open photo review in a modal dialog

Selecting **Review photo** should open a focused modal dialog.

Suggested desktop layout:

**Left side**

- Photo preview.
- Reveal/conceal control.
- Photo metadata.

**Right side**

- Screening result.
- Approval/rejection/escalation controls.
- Structured reason selection.
- Optional notes.
- Submit decision.

The modal should have a practical maximum width and height.

Large photographs should scale to fit the available preview area without expanding the entire page.

Use internal scrolling only where necessary.

The modal should remain usable on smaller desktop screens.

For mobile or tablet administrative access, use an appropriately adapted full-screen dialog or equivalent interface.

### E. Use one Reveal / Conceal toggle

Replace separate static Reveal photo and Conceal photo buttons with one state-aware control.

**When concealed:**

Button: **Reveal photo**

**When revealed:**

Button: **Conceal photo**

The button must update immediately to reflect the current state.

Expected behaviour:

1. Reviewer opens the photo review modal.
2. A blurred preview appears.
3. Reviewer selects Reveal photo.
4. Access is recorded.
5. The photograph becomes visible.
6. The button changes to Conceal photo.
7. Reviewer can conceal the image again without closing the modal.

Revealing a photograph must remain auditable.

Do not require a new free-text justification for routine assigned moderation.

Closing the modal should reset the preview to concealed.

### F. Limit photograph dimensions

Photographs must not expand the review workspace unnecessarily.

Use a fixed or responsive preview container with sensible maximum dimensions.

The image should:

- Preserve its aspect ratio.
- Fit within the preview area.
- Avoid overflowing the modal.
- Avoid stretching or distortion.
- Remain clearly visible for moderation.
- Support a deliberate larger inspection view where authorised and necessary.

Use `object-fit: contain` or equivalent behaviour.

Do not render a full-resolution image at its native dimensions inside the main page.

Authorised higher-resolution inspection should remain subject to the same access controls and auditing.

### G. Consolidate moderation actions

The current interface displays separate large sections for:

- Approve.
- Reject.
- Escalate for specialist review.

Replace these with a compact decision selector inside the review modal.

Suggested actions:

**Approve | Reject | Escalate**

Selecting an action should display only the fields relevant to that decision.

**Approve**

- Approval reason dropdown.
- Optional notes.
- Confirm approval.

**Reject**

- Rejection reason dropdown.
- Optional notes or required explanation where appropriate.
- Confirm rejection.

**Escalate**

- Safety concern dropdown.
- Optional or required notes based on concern type.
- Confirm escalation.

Only one decision form should be visible at a time.

Use the existing structured moderation reason codes established in issue #4.

Do not force reviewers to navigate through three large expanded forms for every photograph.

### H. Standardise all controls

The moderation interface should use consistent Bootstrap styling.

Requirements:

- Uniform dropdown heights.
- Consistent input borders.
- Consistent padding and spacing.
- Consistent button heights.
- Clear primary and secondary actions.
- Properly styled text areas.
- Readable labels.
- No overlapping controls.
- No unnecessary full-width buttons.

The current native-looking dropdowns are visually inconsistent and should be replaced or styled appropriately.

Do not mix default browser controls with unrelated custom components.

All controls should remain keyboard-accessible.

### I. Optimise high-volume review workflows

The system should support reviewers processing hundreds of photographs per hour.

After a decision:

1. Save the moderation result.
2. Update the queue.
3. Display a brief success confirmation.
4. Advance to the next available review item, where appropriate.

Provide a **Next photo** action if automatic advancement is not enabled.

Avoid returning the reviewer to the main page after every decision.

The workflow should minimise unnecessary clicks without removing essential controls.

**Additional requirements**

- Prevent duplicate decision submissions.
- Handle cases where another reviewer has already processed the item.
- Prevent conflicting concurrent moderation decisions.
- Preserve decision history.
- Ensure sensitive escalation actions remain deliberate.

Keyboard shortcuts may be considered for experienced reviewers, but must not allow accidental approval or rejection.

### J. Add moderation queue counts to the Admin dashboard

Pending photo reviews should be visible without requiring staff to open the Photo Review page.

On the Admin Overview page, display a moderation summary.

Suggested:

**Photo Reviews**

- Pending: 24
- Escalated: 3
- Appeals awaiting review: 5

Use actual backend counts.

Also consider displaying a count beside the Photos navigation item.

For example:

**Photos (24)**

Counts must reflect the current authorised queue and should update when decisions are completed.

Do not expose restricted specialist queue counts to staff who are not authorised to access them.

The dashboard should link directly to the relevant queue.

### K. Move coded review history to a dedicated area

The current "Coded reviews since..." expandable section should not remain part of the main review queue.

Create a separate area:

**Review History**

The review history should use a searchable, sortable and paginated table.

Suggested columns:

| Column | Description |
| --- | --- |
| Date | Decision timestamp |
| Photo | Media reference |
| Member | Member reference |
| Decision | Approved, Rejected, Escalated |
| Reason | Structured moderation reason |
| Reviewer | Staff member |
| Actions | View review details |

Provide appropriate filters for:

- Decision.
- Reason.
- Reviewer.
- Date range.
- Member reference.

The table must support thousands of records without loading everything into the browser.

Use server-side pagination and filtering.

Keep sensitive or restricted review details subject to existing permissions.

### L. Introduce a Rejected Photos queue

Rejected photographs should remain available for authorised reconsideration, subject to the applicable retention and evidence rules.

Create a dedicated section or tab:

**Rejected Photos**

This should display previously rejected photographs in a structured table.

Suggested columns:

- Photo reference.
- Member.
- Rejection date.
- Rejection reason.
- Original reviewer.
- Appeal status.
- Current moderation status.
- Review action.

The queue should support filtering by rejection reason, date and appeal status.

Rejected photographs must remain unavailable for member profile display unless a subsequent authorised decision changes their moderation state.

Do not make rejected photographs publicly visible merely because they appear in this queue.

### M. Allow authorised administrative reconsideration

Authorised staff should be able to reopen a rejected-photo decision for review.

Suggested actions:

**Uphold rejection**

The photograph remains rejected.

**Override rejection — Approve**

The photograph is approved following reconsideration.

**Escalate**

The matter is sent for specialist review.

An override must:

- Require appropriate permissions.
- Record the administrator performing the action.
- Record the original and new decisions.
- Record the reason for overriding.
- Preserve the original rejection history.
- Revalidate applicable moderation rules.
- Update the member's photo status.
- Notify the member of the outcome where appropriate.

Do not overwrite or erase the original decision.

For serious prohibited-content categories or evidence under legal hold, ordinary approval overrides must not bypass specialist restrictions or evidence-preservation requirements.

### N. Notify members when a photo is rejected

When a photograph is rejected, the member should receive a notification.

Suggested notification:

**Photo not approved**

"One of your photos couldn't be approved because it doesn't meet Swoovr's photo guidelines."

Display the applicable member-facing rejection reason.

For example:

**Reason:** Public profile photos cannot contain explicit nudity.

Provide actions:

- **View photo details**
- **Appeal decision**, where eligible.

Notifications should be accessible through the member's existing notification system.

The member should also be able to see the rejection status and explanation when managing their photos.

**Important:** Internal moderation codes, automated screening scores, sensitive investigative details and confidential evidence must not be included in member notifications.

Use clear member-facing reason descriptions mapped from the structured moderation codes.

### O. Introduce member photo rejection appeals

Members should have an appropriate way to request reconsideration of a rejected photograph.

Suggested flow:

1. Member receives a photo rejection notification.
2. Member opens the rejected photo details.
3. Member selects **Appeal decision**.
4. Member optionally provides an explanation.
5. The appeal is submitted.
6. The member receives confirmation.
7. The appeal enters the appropriate moderation appeals queue.

Suggested appeal form:

**Appeal photo decision**

"We'll review the photo and the reason it was rejected."

**Your explanation (optional)**

Multiline text field.

**Submit appeal**

The form should clearly indicate that an appeal does not guarantee approval.

The photograph must remain rejected while the appeal is pending.

### P. Route photo appeals into a dedicated review queue

Member photo appeals should appear in an authorised queue.

Suggested section:

**Photo Appeals**

Display:

- Photo reference.
- Member reference.
- Original rejection reason.
- Original reviewer.
- Member's appeal explanation.
- Appeal submitted date.
- Appeal status.
- Review action.

Appeal reviewers should be able to:

- Uphold the rejection.
- Approve the photo following review.
- Escalate for specialist handling.

Where practical, avoid assigning an appeal to the same staff member who made the original decision.

The system must respect existing staff permissions and appeal eligibility rules.

A Support reviewer must not override an Admin-level or specialist decision without the necessary authority.

### Q. Notify members of appeal outcomes

When an appeal is decided, the member should receive a notification.

**Appeal approved**

"Your photo appeal was approved. Your photo is now eligible to appear in your profile, subject to your photo settings."

**Appeal declined**

"We reviewed your appeal and confirmed the original photo decision."

Where appropriate, provide a concise member-facing explanation.

The notification should link to the relevant photo or appeal record.

Do not expose internal reviewer notes or sensitive investigative information.

### R. Prevent repeated or abusive appeals

Introduce reasonable controls to prevent duplicate or excessive appeal submissions.

Requirements:

- Do not create multiple active appeals for the same photo decision.
- Display the current appeal status.
- Preserve previous appeal history.
- Apply existing abuse-prevention controls.
- Avoid unrestricted repeated appeals after a final decision.

If further reconsideration is warranted, authorised staff should retain an appropriate administrative mechanism.

Do not permanently prevent specialist review of genuinely new safety information.

### S. Preserve moderation and evidence security

The redesigned interface must maintain appropriate safeguards.

**Routine moderation**

Support or Admin reviewers may process ordinary photo moderation according to their assigned permissions.

**Escalated cases**

Serious safety concerns must be transferred to the appropriate restricted Trust and Safety workflow.

**Protected evidence**

Legal holds, preserved evidence and protected identities must remain restricted to explicitly authorised personnel.

An Admin or Root Admin account must not receive protected Legal evidence access merely because it can moderate public photographs.

**CSAM concerns**

Suspected CSAM must use the existing specialist incident and evidence-handling workflow.

Do not allow routine photo approval, appeals or administrative overrides to bypass an active specialist restriction.

### T. Remove development-oriented presentation

The Photo Review page must not display internal implementation references or beta-specific wording as part of the normal reviewer interface.

Examples include:

- `(D178)`.
- "Current beta rules still apply".
- Development implementation notes.
- Unnecessary technical identifiers presented without context.

UAT must represent the intended production user interface.

Technical screening details may remain accessible within a dedicated diagnostic or review information panel for authorised staff.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Navigate to `http://localhost:5082/media`.
3. Review the lengthy explanatory text at the top of the page.
4. Observe the CSAM warning embedded within ordinary text.
5. Review the "Coded reviews since..." expandable section.
6. Locate a photograph requiring manual review.
7. Observe the amount of page space occupied by a single item.
8. Open its blurred preview.
9. Reveal the photograph.
10. Observe the separate reveal and conceal controls and oversized preview.
11. Expand the Approve, Reject and Escalate sections.
12. Review the inconsistent dropdown and form styling.
13. Submit a test rejection using an authorised test photograph.
14. Check whether the member receives a notification explaining the rejection.
15. Check whether the member can submit an appeal.
16. Check whether rejected photographs can be viewed in a dedicated Admin queue.
17. Check whether authorised administrators can reconsider or override a rejection.
18. Return to the Admin Overview and check whether pending photo-review counts are displayed.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Photo review — oversized moderation form — `evidence/2026-11-01-issue-intake/22-photo-review-01.png`.

Evidence reference (not opened or publication-reviewed in this run): Photo review — main queue layout — `evidence/2026-11-01-issue-intake/22-photo-review-02.png`.

Evidence reference (not opened or publication-reviewed in this run): Photo review — blurred preview and reveal controls — `evidence/2026-11-01-issue-intake/22-photo-review-03.png`.

Evidence reference (not opened or publication-reviewed in this run): Photo review — revealed image and review layout — `evidence/2026-11-01-issue-intake/22-photo-review-04.png`.

The four screenshots demonstrate excessive whitespace, large inline moderation controls, inconsistent field styling, lengthy page text and inefficient image reveal behaviour.

Processing note: references above use the current tracked filenames; no image was opened, copied, uploaded or staged.

### Impact

**Affected users:** Swoovr Support, Admin and authorised Trust and Safety reviewers, as well as members whose photographs are rejected.

**Operational impact**

- Routine photo reviews take unnecessarily long.
- Reviewers cannot efficiently scan large volumes of queued media.
- Oversized interfaces reduce the number of review items visible at once.
- Review actions require excessive scrolling and navigation.
- Pending workloads are not immediately visible from the Admin dashboard.
- Previous review decisions are difficult to search and analyse.
- Rejected photographs lack an organised reconsideration workflow.

**Member impact**

- Members may not receive clear rejection reasons.
- Members cannot easily understand why a photograph was rejected.
- There is no clear photo-specific appeal process.
- Incorrect rejections may remain unresolved.
- Members may repeatedly upload the same photo instead of appealing the original decision.

**Security and governance impact**

- Sensitive media must remain appropriately protected.
- Review access and reveal actions must remain auditable.
- Specialist restrictions must not be bypassed through ordinary administrative overrides.
- Review decisions and appeal outcomes must retain a complete history.

This is a high-priority improvement because photo moderation is expected to become a frequent, high-volume operational task.

### Notes or constraints

**Design direction**

The Photo Review page should behave like a professional moderation queue, not a collection of full-page forms.

Prioritise:

- Compact queue rows.
- Modal-based review.
- Fast decision-making.
- Consistent form controls.
- Clear queue counts.
- Structured review history.
- Member-facing transparency.
- Auditable reconsideration and appeals.

**Access and safety**

- Preserve blurred previews.
- Audit photo reveals.
- Retain the four-hour CSAM guidance acknowledgement, with notice-version tracking.
- Preserve specialist escalation and evidence-handling requirements.
- Respect existing Support, Admin, Legal and Root Admin boundaries.
- Do not expose restricted evidence through ordinary moderation views.
- Do not automatically approve images on appeal.
- Prevent ordinary overrides of active specialist restrictions.

**Member notifications**

- Use the existing Swoovr notification infrastructure.
- Store structured moderation outcomes.
- Map internal reason codes to appropriate member-facing explanations.
- Do not expose sensitive screening scores or confidential reviewer notes.
- Record appeal and notification status.

**Technical considerations**

- Use Bootstrap components consistent with the Admin design system.
- Use server-side pagination and filtering.
- Support concurrent reviewers safely.
- Prevent duplicate decisions.
- Preserve existing media moderation records.
- Maintain existing Cloudflare R2 image access controls.
- Avoid unrestricted image download capabilities.
- Reuse existing reports, appeals and case infrastructure where appropriate.
- Apply backend authorisation to all moderation and override actions.
- Ensure image approval restores eligibility without bypassing current profile visibility or account restrictions.

**Related issues**

- **#4:** Photo moderation workflow and structured decision reasons.
- **#19:** Member account inspection, photos and case access.
- **#3:** Admin authentication and session handling.

This issue extends #4 by addressing high-volume queue usability, rejected-photo management, member notifications and appeals.

**Suggested implementation order**

1. Review the existing media moderation and appeals data model.
2. Simplify the Photo Review page header.
3. Introduce the timed safety acknowledgement.
4. Replace inline photo review sections with a compact table.
5. Implement the modal-based review interface.
6. Consolidate Approve, Reject and Escalate into a single decision workflow.
7. Replace separate Reveal and Conceal buttons with one state-aware control.
8. Standardise form controls and preview dimensions.
9. Implement reviewer workflow progression and concurrency protection.
10. Add dashboard moderation queue counts.
11. Move coded review history to a dedicated paginated view.
12. Introduce the Rejected Photos queue.
13. Add authorised reconsideration and override handling.
14. Implement member photo rejection notifications.
15. Implement photo-specific member appeals.
16. Add appeal review and outcome notifications.
17. Validate permissions, audit logs and evidence restrictions.
18. Test performance with realistic high-volume review queues.

### Acceptance criteria

**Photo review interface**

- [ ] Page header is concise and free from development-oriented wording.
- [ ] CSAM guidance is presented as a dismissible safety notice.
- [ ] Safety acknowledgement remains valid for four hours.
- [ ] Safety notice revisions can require renewed acknowledgement.
- [ ] Main Photo Review page uses a compact, paginated table.
- [ ] Individual photographs no longer occupy large inline sections.
- [ ] Selecting Review photo opens a focused modal.
- [ ] Photos initially appear blurred where required.
- [ ] Reveal and Conceal use a single state-aware control.
- [ ] Revealing photographs remains audited.
- [ ] Image previews are appropriately constrained.
- [ ] Approve, Reject and Escalate use a consolidated decision interface.
- [ ] Only fields relevant to the selected decision are displayed.
- [ ] Dropdowns, inputs, buttons and text areas use consistent styling.
- [ ] Reviewers can efficiently advance to the next item.
- [ ] Concurrent decisions are handled safely.

**Dashboard and history**

- [ ] Admin Overview displays relevant pending photo-review counts.
- [ ] Photos navigation can display an authorised queue count.
- [ ] Coded review history is moved out of the main queue.
- [ ] Review history supports server-side pagination and filtering.
- [ ] Historical decisions remain accessible according to permissions.

**Rejected photographs**

- [ ] Rejected photographs appear in a dedicated queue.
- [ ] Rejection reason and original decision history are retained.
- [ ] Authorised staff can reconsider rejected photographs.
- [ ] Authorised staff can uphold, override or escalate decisions.
- [ ] Overrides require a recorded reason.
- [ ] Previous decisions are never overwritten or erased.
- [ ] Restricted specialist cases cannot be approved through ordinary override actions.

**Member notifications and appeals**

- [ ] Members receive a notification when a photograph is rejected.
- [ ] Notifications contain an appropriate member-facing rejection reason.
- [ ] Members can view the rejection details in their photo management area.
- [ ] Eligible rejected photographs provide an Appeal decision action.
- [ ] Members can submit an appeal with an optional explanation.
- [ ] Duplicate active appeals are prevented.
- [ ] Appeals enter an authorised review queue.
- [ ] Appeal reviewers can uphold, approve or escalate according to permissions.
- [ ] Members receive the outcome of their appeals.
- [ ] Internal moderation details are not disclosed through member notifications.
- [ ] Appeal and reconsideration history is preserved.

**Security and performance**

- [ ] Existing moderation permissions remain enforced.
- [ ] Sensitive media access remains restricted and audited.
- [ ] Specialist escalation and legal evidence protections remain intact.
- [ ] Pending queues support high-volume workloads.
- [ ] The interface remains usable with thousands of review records.
- [ ] Automated tests cover decisions, notifications, appeals, overrides and permissions.
- [ ] UAT presents the same production-intended interface without development labels.

<a id="intake-23"></a>

## 23. Prevent members from changing their date of birth after initial registration and introduce Admin-controlled DOB correction

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Member web / Admin / API / Mobile
- **Page or URL:** `http://localhost:5081/account`
- **Type:** Bug / Improvement
- **Priority:** High
- **GitHub issue:** [#161](https://github.com/repasscloud/wanderconnect/issues/161)
- **Tracking Q:** [Q116](QUESTIONS.md#q116)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q116](QUESTIONS.md#q116) / [#161](https://github.com/repasscloud/wanderconnect/issues/161) — Entire item 23; linked follow-up to #33 rather than a duplicate of verified-DOB mismatch handling.**

Source-supported: MemberAccountPanel keeps an editable DOB form when DobCheck is none, even for verified DOB (with explanatory text). AccountEndpoints /me/age rejects verified-date changes and open correction/review states, but permits validated unverified-date changes; underage entries lock the account. That is consistent with CONFIRMED D158, not proof of a backend bypass. D166 already provides a one-use provider-mismatch correction and preserves ID Verified during manual DOB review. #33 covers that mismatch, not universal first-save locking.

Requirements/dependencies: OPEN: universal locking supersedes CONFIRMED D158 allowing unverified changes; blanket re-verification/badge changes must reconcile D166. Historical DOB retention/access needs D191/D185 review. DEFAULT: 24-hour single-use grant is only a proposal. Onboarding order remains deferred. No existing issue is reopened or closed.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

The Swoovr member account page currently allows members to modify their date of birth after it has already been entered and saved.

This is incorrect behaviour.

A member's date of birth is an important identity and age eligibility attribute used throughout Swoovr, including:

- Determining whether the member meets the minimum age requirement of 18.
- Calculating the member's age.
- Supporting age-based discovery and matching.
- Maintaining accurate member identity information.
- Supporting ID verification and age assurance.
- Preventing deliberate misrepresentation of age.

Currently, a member can enter their date of birth, save it, and subsequently return to the account page to change it.

This creates an opportunity for members to manipulate their age, potentially bypass age-based restrictions or create inconsistencies between their account information and identity verification records.

The date of birth should be established during initial onboarding and become locked once successfully saved.

### What I expected

A member should be allowed to enter their date of birth once during initial account setup.

Before saving it, the application should clearly explain why accurate information is required and warn that the date of birth cannot subsequently be changed without assistance from Swoovr.

Once confirmed, the date of birth must become read-only.

Members who genuinely entered an incorrect date should be able to request a correction through Swoovr Support.

Only an appropriately authorised Admin should be able to temporarily unlock the date-of-birth field to permit a controlled correction.

### A. Require confirmation before initially saving DOB

During onboarding, when the member enters their date of birth for the first time, display a confirmation dialog before permanently saving the value.

Suggested dialog:

**Confirm your date of birth**

"Please make sure your date of birth is correct.

Swoovr uses your date of birth to confirm age eligibility, calculate your age and support identity verification.

Your date of birth is never displayed to other members, and you can choose whether to show your age on your profile.

For trust and transparency, your date of birth cannot be changed after confirmation without assistance from Swoovr Support."

Display the selected date of birth clearly.

Actions:

- **Go back and edit**
- **Confirm date of birth**

The member must explicitly confirm the date before it is saved.

Do not lock the field merely because a date has been entered but has not yet been confirmed.

### B. Lock the date of birth after confirmation

Once the member confirms and saves their DOB:

- Mark the DOB as confirmed and locked.
- Display the saved DOB as read-only in the member account area.
- Remove the ordinary DOB editing and Save controls.
- Do not permit modification through the standard profile or onboarding endpoints.
- Do not allow modification through mobile applications or alternative clients.
- Enforce the restriction server-side.

The lock must be authoritative and must not depend solely on a disabled input control.

A member must not be able to modify their DOB by manipulating API requests or frontend state.

### C. Improve the member-facing DOB presentation

After DOB confirmation, the member account page should display a concise, read-only section.

Suggested presentation:

**Date of birth**

19 August 1940

<Read-only — confirmed>

Supporting text:

"Your date of birth is private and cannot be changed without assistance from Swoovr Support."

Action:

**Request a correction**

This should direct the member to the existing support process.

Do not continue displaying an editable date picker and Save button after confirmation.

The member should be able to view their DOB but not modify it directly.

### D. Keep DOB private from other members

A member's date of birth must never be displayed publicly on their profile.

The application may calculate their current age for discovery and profile presentation.

Existing privacy preferences must remain available.

For example:

**Show my age**

On / Off

This controls whether the member's calculated age is visible to other members.

It must not affect:

- The stored DOB.
- Age eligibility.
- Internal age calculations.
- ID verification.
- Age-related safety checks.
- Applicable discovery eligibility rules.

Hiding age must not permit the member to bypass age-related requirements.

### E. Introduce Admin-controlled DOB unlocking

Add a controlled administrative action to the member account detail page.

Suggested section:

**Date of Birth**

Display:

- Current DOB.
- Calculated age.
- DOB confirmation status.
- Age assurance status.
- ID verification status.
- Previous correction history, where authorised.

For appropriately authorised Admin staff, provide an action:

**Authorise DOB correction**

This should not permanently unlock the member's DOB.

Instead, create a temporary, single-use permission allowing the member to submit one corrected date of birth.

The Admin must:

1. Open the member's account.
2. Select Authorise DOB correction.
3. Provide a structured correction reason.
4. Record the associated support case or investigation reference.
5. Confirm the authorisation.

The member should then receive a notification explaining that their DOB can be corrected.

### F. Make DOB correction authorisation temporary and single-use

An administrative unlock should have a limited validity period.

**Suggested default: 24 hours.**

Requirements:

- The authorisation applies only to the specified member.
- It may be used once.
- It automatically expires after 24 hours.
- It is consumed after a successful DOB correction.
- It cannot be transferred to another account.
- It cannot be reused through repeated API calls.
- It may be revoked by an authorised administrator before use.
- Expiration and consumption must be enforced by the API.

The DOB must return to the locked state immediately after the corrected value is confirmed.

If the authorisation expires unused, the existing DOB remains unchanged and locked.

### G. Allow the member to correct DOB after authorisation

When an Admin authorises a correction:

1. Notify the member that a DOB correction has been authorised.
2. Display a clear notification within their account.
3. Make the DOB field editable for that authorised correction only.
4. Display the existing DOB and allow the member to choose a replacement.
5. Require explicit confirmation before saving.
6. Validate the corrected DOB.
7. Save the new DOB.
8. Consume the correction authorisation.
9. Lock the field again.
10. Notify the member that the correction was completed.

Suggested notification:

**Date of birth correction available**

"Swoovr Support has authorised you to correct your date of birth. You can update it once before the authorisation expires."

Action:

**Correct date of birth**

Do not expose internal administrative notes or investigation details in the notification.

### H. Provide a support request option

Members who entered an incorrect DOB should be able to request assistance.

Add a link:

**Request a date of birth correction**

This should open the existing Swoovr support process, using the relevant account correction category.

The member should be able to explain the problem.

Support should receive a case containing the account reference and correction request.

Do not require members to send identity documents through ordinary email.

If supporting identity evidence is required, use the established secure identity verification or document submission workflow.

### I. Enforce staff role permissions

DOB correction must respect Swoovr's existing administrative permission boundaries.

**Support**

- May receive and review DOB correction requests.
- May inspect information necessary for authorised support duties.
- May escalate correction requests to Admin.
- Cannot independently unlock or change DOB.

**Admin**

- May review an eligible correction request.
- May authorise a temporary, single-use DOB correction.
- Must provide a reason and associated case reference.
- Cannot bypass age eligibility or verification requirements.

**Legal / Security officer**

- May access protected identity information only through the applicable Legal permissions and authorised purposes.
- Does not automatically inherit Admin DOB correction authority.

**Root Admin**

- Has Admin-level DOB correction authority.
- Must follow the same audit and correction requirements.
- Does not automatically receive Legal-only evidence access.

An Admin may authorise a correction, but ordinary correction should be performed by the member through the temporary authorised workflow.

Any exceptional staff-entered correction should require a separately defined, higher-assurance procedure rather than an unrestricted DOB edit field.

### J. Revalidate age eligibility after correction

Changing DOB can affect a member's eligibility and discovery behaviour.

After a successful correction, the system must:

- Recalculate age.
- Re-evaluate the minimum age requirement.
- Re-evaluate age-related account restrictions.
- Update age-based discovery eligibility.
- Update applicable age-range matching.
- Reassess the existing age assurance state.
- Check for conflicts with completed identity verification.

**If the corrected DOB indicates the member is under 18:**

- Immediately prevent access to adult-only functionality.
- Apply the established underage account handling workflow.
- Prevent discovery, messaging and other restricted member functionality.
- Escalate for appropriate Trust and Safety handling.

An administrative correction must never allow an underage member to remain active in an adult-only service.

### K. Reconcile DOB with ID verification

DOB is used as an identity attribute during ID verification.

Where a member has completed ID verification, a subsequent DOB correction must not silently preserve an inconsistent verified state.

Requirements:

- Compare the corrected DOB with the identity verification information available through the authorised verification process.
- Determine whether existing verification remains valid.
- Require re-verification where necessary.
- Do not automatically mark the corrected DOB as identity-verified.
- Preserve the historical verification record and appropriate audit trail.
- Restrict affected features until required verification is restored.

A completed ID verification should not be treated as permanently valid when its underlying identity attributes materially change.

### L. Maintain a complete DOB correction audit trail

Every administrative authorisation and successful DOB correction must be recorded.

Suggested audit information:

| Field | Description |
| --- | --- |
| Member ID | Account receiving the correction |
| Authorising Admin | Staff account granting permission |
| Case reference | Associated support or investigation case |
| Reason | Structured correction reason |
| Authorised at | UTC timestamp |
| Expires at | UTC timestamp |
| Used at | UTC timestamp, where applicable |
| Previous DOB | Restricted audit value |
| Corrected DOB | Restricted audit value |
| Verification impact | Whether re-verification was required |
| Result | Completed, expired, revoked or otherwise failed |

Full historical DOB values must be restricted to staff with an appropriate authorised need.

Do not expose historical DOB values in ordinary support listings, logs or notifications.

The audit trail should be tamper-resistant and subject to applicable retention rules.

### M. Protect DOB correction endpoints

The DOB correction workflow must be enforced by the API.

Requirements:

- Reject ordinary DOB updates after confirmation.
- Require a valid correction authorisation for subsequent changes.
- Verify the authorisation belongs to the authenticated member.
- Check expiry and single-use status.
- Consume the authorisation atomically with the successful update.
- Prevent concurrent requests from using the same authorisation twice.
- Validate the proposed DOB.
- Apply age eligibility checks.
- Audit the operation.
- Return clear, structured error responses.

Do not rely on frontend flags, hidden fields or disabled controls as the primary enforcement mechanism.

### N. Handle existing accounts

Existing members may already have a DOB stored without a separate confirmation or lock indicator.

The migration must not allow unrestricted DOB changes simply because the new confirmation system did not previously exist.

Recommended behaviour:

- Existing populated DOB values should be locked by default.
- Existing members should not be required to re-enter their DOB.
- Members without a DOB should complete the required initial entry and confirmation workflow.
- Previously verified accounts should preserve their verification history.
- Any existing DOB inconsistencies should follow the controlled correction process.

Do not overwrite existing DOB values during migration.

### Steps to reproduce

1. Sign in to the Swoovr member application.
2. Navigate to `http://localhost:5081/account`.
3. Locate the **Your age** section.
4. Observe the existing Date of birth field.
5. Change the date to a different value.
6. Click Save.
7. Observe that the application permits the DOB to be changed after its initial submission.
8. Refresh the account page.
9. Confirm whether the modified date persists.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Member account — editable date of birth — `evidence/2026-11-01-issue-intake/23-member-date-of-birth-editable.png`.

The screenshot shows a previously populated DOB field that remains editable, with a Save button available.

### Impact

**Affected users:** All Swoovr members, administrators and Trust and Safety personnel.

**Impact:**

- Members can change an important identity attribute without review.
- Age-based discovery can become unreliable.
- Members may misrepresent their age.
- Stored DOB may become inconsistent with completed ID verification.
- Age assurance and eligibility checks may be undermined.
- Trust and Safety investigations may rely on incorrect account information.
- Support staff lack a controlled process for legitimate corrections.

Because Swoovr is restricted to adults aged 18 and older, DOB integrity is an important account safety and eligibility requirement.

### Notes or constraints

**Core decision**

A member's DOB is a one-time, confirmed account attribute.

After initial confirmation, it must remain locked unless a specifically authorised correction workflow is completed.

**Recommended correction model**

- Initial DOB entry: Member.
- Initial DOB confirmation: Member.
- Subsequent DOB change: Not permitted by default.
- Correction request: Member through Support.
- Correction authorisation: Admin or Root Admin.
- Authorisation validity: 24 hours.
- Authorisation usage: Single-use.
- Corrected DOB entry: Member.
- Reverification: Required where applicable.

**Privacy**

- DOB is never public.
- Members can control the public visibility of their calculated age.
- Hiding age does not affect internal age eligibility.
- Historical DOB values require restricted access.

**Implementation considerations**

- Preserve the existing onboarding sequence.
- Implement server-side locking.
- Use transactional single-use correction authorisations.
- Maintain audit records.
- Recalculate age after correction.
- Re-evaluate age eligibility.
- Reconcile DOB changes with ID verification.
- Preserve existing Trust and Safety escalation requirements.
- Do not allow staff to bypass underage restrictions.
- Apply the same behaviour to web, mobile web, iOS and Android.
- Use the existing Swoovr support and notification systems.
- Maintain consistency with the Admin member account redesign.

**Related issues**

- **#19:** Admin member account details, role permissions and DOB presentation.
- **#10:** Registration and onboarding design.
- Existing member account redesign issue covering the Me / Account page.

This issue should be implemented alongside the existing age assurance and ID verification architecture, without weakening those controls.

### Acceptance criteria

**Initial DOB entry**

- [ ] Members can enter DOB during initial onboarding.
- [ ] DOB requires explicit confirmation before first save.
- [ ] Confirmation explains the importance of accuracy and the future correction process.
- [ ] DOB is validated for the minimum age requirement.
- [ ] DOB becomes locked immediately after confirmation.

**Member account**

- [ ] Confirmed DOB displays as read-only.
- [ ] Ordinary DOB editing and Save controls are removed after confirmation.
- [ ] Members can request a DOB correction through Support.
- [ ] DOB is never displayed publicly.
- [ ] Age visibility remains independently configurable.

**Administrative correction**

- [ ] Support can receive and escalate correction requests.
- [ ] Admin can authorise a temporary DOB correction.
- [ ] Legal does not automatically inherit correction authority.
- [ ] Root Admin follows the same correction requirements.
- [ ] Authorisations require a reason and case reference.
- [ ] Authorisations expire after 24 hours.
- [ ] Authorisations are single-use.
- [ ] Members receive notification when correction is authorised.
- [ ] Members must confirm the corrected DOB.
- [ ] DOB becomes locked again immediately after correction.
- [ ] Expired or revoked authorisations cannot be used.

**Age and verification**

- [ ] Age is recalculated after correction.
- [ ] Age-related discovery rules are re-evaluated.
- [ ] Underage corrections immediately trigger the appropriate restrictions.
- [ ] ID verification is reassessed where DOB changes.
- [ ] Required re-verification is enforced.
- [ ] Existing verification history is preserved.

**Security and auditing**

- [ ] API rejects unauthorised DOB changes.
- [ ] Frontend manipulation cannot bypass DOB locking.
- [ ] Correction authorisations are consumed atomically.
- [ ] Concurrent correction attempts cannot reuse authorisation.
- [ ] Administrative authorisations and corrections are audited.
- [ ] Historical DOB values are protected.
- [ ] Existing populated DOB values are locked during migration.
- [ ] Automated tests cover initial entry, locking, authorisation, expiry, correction, age eligibility and verification impacts.

<a id="intake-24"></a>

## 24. Require first and last name at registration, default profile display name to first name and standardise identity across Admin

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Member web / Admin / API / Mobile
- **Page or URL:** Keycloak registration (`http://localhost:8180/realms/swoovr-members-dev/`), member profile and Admin account management
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#162](https://github.com/repasscloud/wanderconnect/issues/162), [#163](https://github.com/repasscloud/wanderconnect/issues/163)
- **Tracking Q:** [Q117](QUESTIONS.md#q117), [Q118](QUESTIONS.md#q118)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q117](QUESTIONS.md#q117) / [#162](https://github.com/repasscloud/wanderconnect/issues/162) — Item 24 except independent mode-specific public-name overrides; links Q110 search, Q111 details and Q116 DOB.**

Source-supported: scripts/keycloak_realms.py generates required firstName/email but no lastName in the member user profile; register.ftl renders those dynamic fields. Account stores issuer/subject and accepted email, with no registered FirstName/LastName fields. MemberProfile has a separate nullable DisplayName; AccountResolver does not initialise it from provider names. D191 already stores encrypted verified names behind Legal-only access, which must not become ordinary Admin registered-name data.

Requirements/dependencies: OPEN: required surname, registered-name authority/field matrix, provider comparison and existing-account completion policy. Item 24's 'Core product decisions' are report proposals, not amendments to the confirmed register. DEFAULT: keep custom public names and declared/verified identity separate. Per-mode names are split to Q118; mandatory verification is not authorised.

**[Q118](QUESTIONS.md#q118) / [#163](https://github.com/repasscloud/wanderconnect/issues/163) — Item 24 D and mode-name portions of B/E/L/M and public-profile acceptance.**

Source-supported: MemberProfile owns DisplayName; MemberMode stores Enabled/Visible/About/ShareCoreAbout and intentions, with no per-mode name. CONFIRMED D175 explicitly shares display name across all modes. PR #154 preserved custom names and did not implement provider-name initialisation or per-mode overrides.

Requirements/dependencies: OPEN: approval to replace shared D175 names, fallback/edit semantics and required migrations/projections. DEFAULT: no automatic override creation or overwriting. Registered-name collection/initialisation is separate Q117; independent main photos remain deferred Q108.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

Swoovr currently collects a member's first name during account registration but does not require their last name.

The application also does not consistently distinguish between a member's registered identity and their public-facing profile name.

This creates inconsistencies between registration, member profiles, administrative account records and identity verification.

Swoovr needs to maintain two separate concepts:

1. **Registered identity:** The member's first and last name, used for account administration, identity verification and authorised support operations.
2. **Public profile identity:** The name or handle displayed to other members, which can be customised independently for each connection mode.

These identities must be linked to the same account without being treated as interchangeable.

### What I expected

Swoovr should require a first name and last name during registration.

The member's first name should automatically become their initial public display name.

Members should subsequently be able to customise their public display name, including independently for Dating, Friends, Activities and Business.

Throughout the Admin application, authorised staff should see the member's registered first and last name as their primary account identifier.

Public display names and mode-specific handles should remain available as supplementary information.

The registered name must also form part of the ID verification process.

### A. Require first and last name during registration

Update the Swoovr registration form to collect:

- First name — required.
- Last name — required.
- Email address — required.
- Existing age declaration and other applicable registration requirements.

Suggested form labels:

**First name**

**Last name**

Do not combine both into a single Full name input.

Use the existing Swoovr Keycloak authentication theme and follow the approved registration design from issue #10.

Requirements:

- Validate both names server-side.
- Trim unnecessary leading and trailing whitespace.
- Support Unicode characters, accents, hyphens, apostrophes and multi-part names.
- Do not impose English-only naming assumptions.
- Avoid unnecessarily restrictive character or length rules.
- Preserve the entered values after unrelated validation errors.
- Do not display first or last names publicly merely because they were collected at registration.

The registration page should retain appropriate responsive styling on desktop, tablet and mobile.

### B. Store registered identity separately from public display names

The member's registered identity should consist of:

- Registered first name.
- Registered last name.

These are account identity attributes.

Public display names are separate profile attributes.

Do not use one database field to represent both concepts.

Suggested conceptual model:

**Account identity**

- `FirstName`
- `LastName`

**Main member profile**

- `DisplayName`

**Mode-specific profiles**

- `DisplayNameOverride` or equivalent nullable field per mode.

Use the existing domain model where suitable rather than introducing duplicate identity storage.

If Keycloak is the authoritative source for registered names, ensure the member account model correctly synchronises or references those attributes.

### C. Default the public display name to the registered first name

When a new member account is created, initialise their main profile display name using the first name supplied during registration.

Example:

Registered identity:

- First name: Alexander
- Last name: Smith

Initial public display name:

**Alexander**

The member can subsequently change their display name.

This initialisation should occur only when the profile is first created or when an existing profile has no display name.

Do not overwrite a customised display name during subsequent sign-ins, profile updates or identity changes.

The registered first and last name must remain separate from the public display name.

### D. Support independent display names for each connection mode

Swoovr supports:

- Dating.
- Friends.
- Activities.
- Business.

Each mode should support an optional public display name override.

For example:

| Profile | Display name |
| --- | --- |
| Registered identity | Alexander Smith |
| Main profile | Alex |
| Dating | Alex |
| Friends | Alexander |
| Activities | Alex |
| Business | Alexander S. |

The registered identity must never automatically become public merely because a mode-specific display name is absent.

**Fallback behaviour**

1. Use the mode-specific display name if one is configured.
2. Otherwise, use the main profile display name.
3. If the main display name has never been customised, it defaults to the registered first name.

Members should not be forced to use their registered first name publicly.

A mode-specific display name should remain unchanged when the member updates another mode's display name.

Existing profile visibility and discovery settings must continue to apply.

### E. Clarify public profile presentation

When another member views a profile, they should see the selected public display name for that mode.

They may also see the member's calculated age if the member has enabled age visibility.

Example:

**Alex, 42**

Do not automatically expose the registered surname.

Do not use the registered full name as a fallback on public profiles.

A member's legal identity and public profile identity must remain separate.

Age visibility must continue to follow Swoovr's existing privacy settings and eligibility rules.

### F. Standardise registered names throughout Admin

The Admin application should use the member's registered first and last name as the primary human-readable account identifier.

This applies to authorised staff working in:

- Support.
- Admin.
- Legal / Security.
- Root Admin.

Examples of affected areas:

- Account search results.
- Account detail pages.
- Beta Enrollments.
- Reports and moderation cases.
- Appeals.
- Photo moderation.
- Account standing.
- Verification reviews.
- Membership management.
- Support lookup.
- Contact history.
- Authorised investigation views.
- Administrative action history.

For example:

**Alexander Smith**

Supporting information:

Display name: Alex

Account reference: SW-...

The public profile display name should not replace the registered identity in administrative records.

Where useful, show both the registered name and the relevant public profile name.

### G. Distinguish registered identity from verified identity

A first and last name entered during registration is a **self-declared identity** until appropriately verified.

The Admin application should clearly distinguish between:

- Registered name.
- ID verification status.
- Verified identity attributes, where available and authorised.

Do not label a registered name as legally verified solely because the member entered it during registration.

A member with no completed ID verification should still have their registered first and last name displayed in authorised Admin views, but the identity verification state must remain accurate.

### H. Include registered names in ID verification

When the member begins ID verification, use their registered identity details as part of the verification process.

Relevant attributes include:

- First name.
- Last name.
- Date of birth.

The verification workflow should compare the registered details against the identity information returned by the approved ID verification provider.

Where supported, account for legitimate name variations, including:

- Middle names.
- Hyphenated surnames.
- Multi-part surnames.
- Diacritics and transliteration.
- Common abbreviated or alternative given-name forms.

Do not automatically reject every non-exact textual match without appropriate provider rules or review.

Where a material identity mismatch occurs:

- Follow the established verification exception workflow.
- Do not silently overwrite registered identity information.
- Do not automatically grant verified status.
- Provide an appropriate correction or review pathway.

Protected identity documents and verification evidence must remain subject to the existing Legal and identity-access permission boundaries.

### I. Control changes to registered names

Members should not be able to bypass identity verification by freely replacing registered identity details after verification.

**Before ID verification**

Permit legitimate corrections to registered first and last names through a controlled account settings process, subject to validation and abuse protections.

**After successful ID verification**

Material changes to registered identity details should require an appropriate identity correction and reverification workflow.

Examples include:

- Legal name changes.
- Correction of registration errors.
- Changes following marriage or other legitimate circumstances.

A change to the registered name must not automatically change the member's public display names.

A public display name change must not alter registered identity attributes.

All security-relevant registered-name changes should be appropriately audited.

### J. Apply staff access permissions

Use the established Swoovr administrative role model.

**Support**

May view the registered first and last name where required for authorised account and support operations.

May view relevant public profile names and account references.

Does not automatically receive access to protected identity documents or Legal evidence.

**Admin**

May view registered account identity and relevant profile details for authorised administrative investigations.

May perform permitted account management and moderation actions.

Does not automatically receive protected Legal evidence access.

**Legal / Security officer**

May access registered identity and additional protected identity evidence only through applicable specialist permissions and authorised workflows.

Legal does not automatically inherit ordinary Support or Admin moderation authority.

**Root Admin**

May view registered account identity through Admin permissions.

Root Admin does not automatically receive Legal evidence access without an explicit Legal grant.

All protected identity information and evidence access must continue to be enforced server-side.

### K. Update account searching and filtering

Coordinate with issue #18.

Admin account search should support registered identity fields in addition to existing email and mobile lookup.

Suggested supported search identifiers:

- Registered first name.
- Registered last name.
- Full registered name.
- Email address.
- Mobile number.
- Member reference.

Where appropriate, allow searching by public display name as a separate search option.

Search results should clearly distinguish registered names from display names.

For example:

**Alexander Smith**

Display name: Alex

Email: alexander@example.test

Do not assume public handles are unique identifiers.

### L. Update existing member accounts

Existing member records may contain a first name but no registered last name.

Implement a safe migration and completion process.

Requirements:

- Preserve existing first names.
- Preserve existing public display names.
- Do not overwrite customised mode-specific names.
- Do not invent last names.
- Identify accounts missing required registered identity information.
- Prompt affected members to supply a last name through a controlled account completion process.
- Validate and save the supplied last name.
- Update Admin account presentation accordingly.

Members who already have a customised display name must not unexpectedly revert to their registered first name.

Where existing identity verification records are present, reconcile newly supplied names with the verification workflow rather than silently declaring them verified.

### M. Maintain consistent presentation across platforms

Apply these requirements to:

- Member web.
- Mobile web.
- Native iOS.
- Native Android.
- Admin web.
- API and identity-provider integrations.

The registration form should be properly designed for each viewport.

Profile editing should clearly distinguish:

**Registered name**

Used for account identity and verification.

**Display name**

Shown to other Swoovr members.

**Mode-specific display name**

Optional override for each connection mode.

Use concise labels and explanatory text.

Do not expose implementation terminology such as database property names or raw enum values in the member-facing interface.

### Steps to reproduce

1. Open the Swoovr registration flow.
2. Observe that First Name is required but Last Name is not collected.
3. Register using a test account.
4. Complete the required onboarding steps.
5. Open the member profile editor.
6. Observe how the initial display name is populated.
7. Review the available mode-specific profile name settings.
8. Open the member's record in the Admin application.
9. Observe whether the Admin interface displays the registered identity or public display name.
10. Review the ID verification process and its handling of registered names.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Current Swoovr registration screen — `evidence/2026-11-01-issue-intake/24-create-your-account-screen.png`.

The screenshot shows the existing registration form collecting First Name without a Last Name field.

### Impact

**Affected users:** All Swoovr members and authorised administrative staff.

**Impact:**

- Registered identity information is incomplete.
- Admin staff cannot consistently identify members by first and last name.
- Public display names may be confused with registered identity.
- Identity verification cannot reliably compare both registered name components.
- Members may be forced into inappropriate public naming conventions.
- Administrative reports and investigation records may inconsistently identify the same account.
- Existing profiles may require migration to support the revised identity structure.

Separating registered identity from public profile names improves account consistency, identity verification and administrative usability while preserving member control over public presentation.

### Notes or constraints

**Core product decisions**

1. First name is mandatory at registration.
2. Last name is mandatory at registration.
3. The registered first name becomes the initial main profile display name.
4. Members can change their public display name.
5. Each connection mode may have an independent display name override.
6. Registered first and last names are the primary names displayed throughout authorised Admin interfaces.
7. Registered names and DOB form part of ID verification.
8. Public profile names never automatically expose the registered surname.

**Technical considerations**

- Reuse existing identity and profile models where possible.
- Maintain a clear authoritative source for registered identity.
- Avoid duplicated or conflicting name data between Keycloak and member records.
- Do not automatically change public display names when registered identity changes.
- Preserve existing mode-specific profile information.
- Handle Unicode and international naming conventions.
- Maintain account privacy and administrative role boundaries.
- Enforce identity update restrictions server-side.
- Preserve applicable identity verification and audit requirements.
- Ensure existing accounts can complete missing identity information safely.

**Related issues**

- **#10:** Registration screen redesign.
- **#11:** Registration validation and error presentation.
- **#18:** Admin account search and filtering.
- **#19:** Admin member account detail and profile inspection.
- **#23:** Date-of-birth locking and identity correction.

These changes should be implemented consistently across registration, member profiles, identity verification and Admin account management.

**Suggested implementation order**

1. Review the existing Keycloak and member identity data model.
2. Add or confirm registered Last Name support.
3. Update registration validation and storage.
4. Initialise main Display Name from First Name.
5. Add or confirm independent mode-specific display name overrides.
6. Update member profile editing and fallback behaviour.
7. Standardise registered-name presentation throughout Admin.
8. Update account search to support registered names.
9. Integrate registered first and last names into ID verification.
10. Implement appropriate identity correction restrictions.
11. Migrate or complete existing member records safely.
12. Validate web, mobile and Admin behaviour.
13. Add automated tests for registration, display-name fallback, identity changes and verification.

### Acceptance criteria

**Registration**

- [ ] First Name is required.
- [ ] Last Name is required.
- [ ] Both fields support international names.
- [ ] Registration validates and persists both names.
- [ ] Existing registration and onboarding requirements remain intact.

**Public profiles**

- [ ] Main Display Name defaults to registered First Name.
- [ ] Members can customise their main Display Name.
- [ ] Each mode supports an optional display name override.
- [ ] Mode-specific names fall back to the main Display Name.
- [ ] Public profiles do not automatically expose registered surnames.
- [ ] Existing customised display names are preserved.
- [ ] Changing a public display name does not modify registered identity.

**Admin**

- [ ] Registered first and last names are displayed throughout authorised Admin interfaces.
- [ ] Public display names are shown as secondary information where relevant.
- [ ] Account search supports registered names.
- [ ] Admin account detail clearly distinguishes registered and public identity.
- [ ] Registered names are not incorrectly labelled as verified identities.
- [ ] Protected identity information remains permission-controlled.

**Identity verification**

- [ ] Registered first and last names are included in the verification workflow.
- [ ] DOB remains part of the age and identity verification process.
- [ ] Legitimate name variations are handled appropriately.
- [ ] Material identity mismatches follow the established review process.
- [ ] Changes to verified identity details trigger appropriate reassessment.
- [ ] Protected identity evidence remains restricted.

**Migration and security**

- [ ] Existing first names are preserved.
- [ ] Existing public display names are preserved.
- [ ] Missing last names can be collected securely.
- [ ] No fabricated surnames are introduced.
- [ ] Identity changes are validated and appropriately audited.
- [ ] API and identity-provider data remain consistent.
- [ ] Automated tests cover registered identity, public display names and mode-specific overrides.

<a id="intake-25"></a>

## 25. Standardise all Admin pages, remove unnecessary instructional text and enforce consistent responsive design

- **Status:** Processed — follow-up issues created; investigation / decisions OPEN
- **Area:** Admin
- **Page or URL:** All Swoovr Admin routes (`http://localhost:5082/`)
- **Type:** Improvement
- **Priority:** High
- **GitHub issue:** [#164](https://github.com/repasscloud/wanderconnect/issues/164), [#165](https://github.com/repasscloud/wanderconnect/issues/165)
- **Tracking Q:** [Q119](QUESTIONS.md#q119), [Q120](QUESTIONS.md#q120)

### Intake investigation refresh — 2 November 2026

Text/source investigation only; no screenshot opened and no runtime reproduction. The original report and unchecked acceptance below are preserved as user feedback, not confirmed implementation requirements.

**[Q119](QUESTIONS.md#q119) / [#164](https://github.com/repasscloud/wanderconnect/issues/164) — Item 25 A–I/M–O and shared presentation/security acceptance; J–L split to Q120.**

Source-supported: SupportLookup.razor contains the quoted #50/permission/logging paragraph. Account.razor exposes D-code headings; MediaReview.razor includes beta and implementation guidance. App.razor loads themes.css/admin.css, not a Bootstrap bundle; admin.css already provides shared controls, responsive layout and top-aligned td. The report's 'existing Bootstrap' premise is not established by the current shell. Functional beta/provider/safety distinctions remain operational facts.

Requirements/dependencies: CONFIRMED docs/design is read-only, Standard staff styling and D185/D197 audit/security remain. DEFAULT: reuse current CSS/components; framework adoption needs a deliberate dependency choice. OPEN: any removal of required disclosures requires owning policy review; legal-draft approval remains deferred. Device availability is separately Q120.

**[Q120](QUESTIONS.md#q120) / [#165](https://github.com/repasscloud/wanderconnect/issues/165) — Item 25 J–L and responsive availability criteria; conflicts with item 22 D recorded explicitly.**

Source-supported: Current Admin shell/CSS adapts layouts to narrow viewports; no route-level supported-device matrix is established by the inspected shell. The proposed table labels media/legal/root routes desktop-only, while item 22 proposes an adapted mobile/tablet review dialog. This conflict requires a route decision. A resized viewport or user-agent is not evidence of a trusted device and cannot enforce sensitive backend access.

Requirements/dependencies: OPEN: route classifications, meaning of desktop-only and device trust/enforcement requirements; the report table is a proposed starting point. DEFAULT: preserve existing role permissions and accessible reflow while policy is unresolved. No device deny rule or new authentication requirement is approved by this intake.

Related PRs: [#152](https://github.com/repasscloud/wanderconnect/pull/152), [#153](https://github.com/repasscloud/wanderconnect/pull/153) and [#154](https://github.com/repasscloud/wanderconnect/pull/154) are merged; no open PR was found at investigation time. Source links, duplicate assessment, split boundaries and validation gaps: [run record](ISSUE-INTAKE-2026-11-02.md).

### What happened

Throughout the Swoovr Admin application, pages contain excessive instructional, explanatory and development-oriented text.

Many screens display large paragraphs describing how the application works, which internal development decisions apply, or that administrative activity is being recorded.

This creates unnecessary visual clutter, wastes available screen space and makes the application feel more like development documentation than a professional administrative system.

The Admin application is an operational tool for Support, Admin, Legal/Security and Root Admin staff.

Its interface should prioritise information, actions and efficient workflows rather than repeatedly explaining routine functionality.

There are also inconsistencies in the appearance of dropdowns, inputs, buttons and other form controls.

**Problems identified:**

1. Excessive instructional and explanatory paragraphs appear at the top of many Admin pages.
2. Internal development references such as `(D127)`, `(D150)` and similar identifiers are visible in headings and descriptions.
3. Beta, UAT and development-oriented wording appears in normal application content.
4. Repeated explanations make pages feel unnecessarily complicated.
5. Routine notices stating that actions are recorded or logged appear throughout the interface.
6. Dropdowns, text fields, date pickers, buttons and other controls use inconsistent styling and dimensions.
7. Important information is sometimes presented as large blocks of text rather than structured contextual guidance.
8. The application does not consistently distinguish between operational pages suitable for mobile devices and sensitive administrative functions that should require desktop access.
9. Some pages waste significant space on instructional text rather than presenting the information or actions staff need.

The attached Support Lookup screenshot is one example, but **this issue applies to the entire Admin application**.

### What I expected

The Swoovr Admin application should use a consistent, professional, production-ready design across every page.

Information should be concise, relevant and presented only where it materially assists the user.

All administrative controls should follow one shared design system.

Sensitive or complex administrative workflows should be designed primarily for desktop use, with mobile and tablet access limited according to the operational requirements and security sensitivity of the function.

### A. Remove unnecessary explanatory text from Admin pages

Review every Admin route and identify instructional text that does not directly assist the staff member with their current task.

Remove unnecessary paragraphs describing:

- Routine application behaviour.
- Internal implementation details.
- Development decisions.
- Obvious functionality.
- General operational processes already understood by authorised staff.
- Repetitive statements about logging and monitoring.
- Internal development ticket references.

For example, the Support Lookup page currently displays:

"Support and admins (#50). Find the report behind a member's support reference (SW-XXXXXX). Admin-queue cases (staff-affiliated accounts, serious incidents) show to admins only. Every lookup is logged. Tell the member only what their own standing page says."

This is excessively detailed for a basic lookup screen.

Suggested replacement:

**Support Lookup**

"Find a report using its support reference."

The existing reference input and Find action should remain.

The longer explanation of permissions and operational procedures does not need to appear as a permanent paragraph.

Permission restrictions must continue to be enforced by the application.

**General principle:** If removing explanatory text does not make the interface harder to use or less safe, remove it.

### B. Keep important guidance where genuinely necessary

Not all informational text should be removed.

Some administrative workflows involve legal obligations, sensitive information or consequences that warrant clear guidance.

Examples include:

- Law Enforcement.
- Legal Holds.
- Protected identity access.
- Evidence preservation.
- Serious safety incidents.
- Destructive account actions.
- Sensitive data exports.

For these pages, retain concise, task-relevant instructions and warnings.

However, avoid displaying them as large paragraphs above the main content.

Use a structured contextual information panel.

**Suggested desktop layout**

**Main content area**

Contains the actual administrative form, case information or operational workflow.

**Right-side information panel**

Contains relevant guidance, requirements, warnings or procedural information.

The information panel should:

- Have a visually distinct but restrained container.
- Use a concise heading.
- Use short paragraphs or structured instructions.
- Avoid repeating information already visible in the main interface.
- Remain appropriately positioned alongside the relevant workflow.
- Support links to full internal procedures where necessary.

For example, the Law Enforcement page could display its operational controls on the left and relevant legal handling guidance on the right.

Do not use large full-width text blocks where contextual guidance would be more appropriate.

### C. Remove internal development references

Remove internal identifiers from all normal Admin page headings, labels and descriptions.

Examples:

- `(D127)`
- `(D129)`
- `(D134)`
- `(D149)`
- `(D150)`
- `(D151)`
- `(D178)`
- `(#50)` when referring to internal implementation tracking.

These identifiers are not useful to staff performing administrative operations.

They belong in:

- Source code.
- Development documentation.
- GitHub issues.
- Internal technical references.

They should not appear in the normal production interface.

Actual member references, support references, case IDs, audit IDs and other operational identifiers must remain available where required.

The restriction applies to internal development references, not legitimate business identifiers.

### D. Remove development and beta messaging

UAT must represent the intended production application.

Remove normal user-facing references such as:

- "In development".
- "Work in progress".
- "Current beta rules".
- "Draft implementation".
- Internal build numbers.
- Development decision codes.
- Unnecessary UAT-specific commentary.

Do not maintain a separate unfinished-looking interface for UAT.

The same production-intended UI components should be used across UAT and production.

Environment-specific configuration and integrations may differ, but the application should not require visual clean-up before production deployment.

**Exception:** Genuine operational statuses, such as an active Beta Enrollment program, should remain visible where they have a real business purpose.

Do not remove legitimate functional information merely because it contains the word "beta".

### E. Remove routine logging and monitoring notices

The Admin application currently displays messages such as:

- "This action is recorded."
- "Your access is recorded."
- "Every lookup is logged."
- "This activity will be logged."

Remove these routine statements from normal administrative pages.

Authorised Swoovr staff operate under established security, access-control and monitoring policies.

Repeatedly displaying these statements provides little operational value and creates unnecessary clutter.

**Important distinction**

Removing these notices must not disable, weaken or alter auditing.

The application must continue recording appropriate:

- Administrative access.
- Account changes.
- Moderation decisions.
- Sensitive data access.
- Photo reveals.
- Evidence access.
- Legal actions.
- Permission changes.
- Security-relevant activities.

Logging and monitoring must remain enforced independently of the interface.

**Exceptions**

Retain explicit acknowledgement or confirmation where required by law, policy or a specific high-risk workflow.

Examples include a required evidence-access acknowledgement or a serious safety handling warning.

Do not display routine logging explanations on every page, but do not remove mandatory security or legal disclosures.

### F. Standardise all form controls

All Admin pages must use a consistent component library and design system.

Review:

- Text inputs.
- Email inputs.
- Number inputs.
- Date pickers.
- Dropdowns.
- Searchable selectors.
- Checkboxes.
- Switches.
- Radio buttons.
- Text areas.
- Buttons.
- Filter controls.
- Modal dialogs.
- Confirmation dialogs.

Use the existing Bootstrap-based Admin styling consistently.

**Requirements**

- Uniform control heights for comparable fields.
- Consistent border radius.
- Consistent border styling.
- Consistent horizontal and vertical padding.
- Consistent label typography.
- Consistent spacing between fields.
- Consistent focus and validation states.
- Consistent disabled and read-only presentation.
- Consistent button sizing.
- Appropriate text-area dimensions.
- No overlapping or touching controls.

Where different dimensions are necessary, they should be intentional and defined through shared component variants.

Do not allow each page to implement unrelated input styling.

### G. Introduce reusable Admin components

Where appropriate, centralise common UI components.

Suggested shared components:

- Admin text input.
- Admin select.
- Admin date picker.
- Admin checkbox.
- Admin toggle.
- Admin text area.
- Admin primary button.
- Admin secondary button.
- Admin destructive button.
- Admin confirmation dialog.
- Admin information panel.
- Admin status badge.
- Admin data table.
- Admin filter toolbar.
- Admin empty state.
- Admin error state.

These should use common design tokens and Bootstrap styling.

Individual pages should not need to recreate form styling.

The goal is consistency and maintainability rather than introducing unnecessary abstraction.

### H. Standardise page headers

Every Admin page should follow a consistent heading structure.

Suggested format:

**Page title**

Optional one-line description, only if required.

Main content.

Avoid:

- Long introductory paragraphs.
- Internal development references.
- Repeated permission explanations.
- Unnecessary operational disclaimers.

If additional guidance is required, use a contextual panel or help link.

The page title should clearly describe the function.

Examples:

- Support Lookup.
- Photo Review.
- Beta Enrollments.
- Account Management.
- Law Enforcement.
- Legal Holds.
- Identifier Review.

Use consistent capitalisation and spacing.

### I. Establish desktop-first Admin design

Swoovr Admin is primarily an operational desktop application.

Desktop should be the primary design target.

Requirements:

- Use available desktop width effectively.
- Maintain consistent sidebar and header navigation.
- Use structured tables for data-heavy pages.
- Use modals or detail panels for focused workflows.
- Avoid unnecessarily narrow forms.
- Avoid excessive empty space and oversized cards.
- Keep contextual information separate from primary actions.
- Maintain consistent content margins and section spacing.

The application should feel like a professional administrative platform rather than a mobile layout displayed on a desktop browser.

### J. Define which Admin functions are available on mobile

Not every Admin function should be accessible from mobile or tablet.

Review all Admin routes and classify them according to their operational requirements.

**Suggested classification**

| Function | Desktop | Mobile / Tablet |
|---|---|---|
| Admin Overview | Full access | Supported |
| Support Lookup | Full access | Supported |
| Basic support cases | Full access | Supported where appropriate |
| Contact History | Full access | Read-only or limited |
| Account Search | Full access | Basic search |
| Account Detail | Full access | Limited summary |
| Beta Enrollments | Full access | Read-only or limited |
| Photo Moderation | Full access | Desktop only |
| Identifier Review | Full access | Desktop only |
| Serious Safety Incidents | Full access | Desktop only |
| Legal Holds | Full access | Desktop only |
| Law Enforcement | Full access | Desktop only |
| Protected Evidence Access | Full access | Desktop only |
| Evidence Export | Full access | Desktop only |
| Staff Permission Management | Full access | Desktop only |
| Root Admin Security Controls | Full access | Desktop only |

This is a proposed starting point.

Codex should review the actual workflows and determine appropriate availability.

**Important:** Desktop restrictions are an additional usability and operational safeguard, not a substitute for authentication, authorisation or device security.

### K. Provide a proper desktop-required screen

When a staff member attempts to access a desktop-only function from a mobile or tablet viewport, do not display a broken or compressed version of the page.

Instead, display a professionally designed message.

Suggested heading:

**Desktop required**

"This function is available in the desktop version of Swoovr Admin."

Provide an action:

**Return to Admin Overview**

Do not expose sensitive page content behind the restriction screen.

The message should use the standard Swoovr Admin design system.

Access limitations must not be bypassed merely by changing CSS or resizing a browser window.

For security-sensitive operations, any actual device or platform restrictions must be enforced through appropriate backend and access policies.

Viewport size alone must not be treated as a security boundary.

### L. Ensure useful mobile functionality remains available

Basic day-to-day administrative operations may remain accessible on mobile where appropriate.

Examples include:

- Checking workload counts.
- Looking up support references.
- Reviewing basic account information.
- Viewing authorised support cases.
- Performing low-risk support actions where permitted.

Mobile layouts should be specifically designed for those workflows.

Do not simply compress large desktop tables and forms into narrow columns.

Provide mobile-appropriate navigation, controls and spacing.

Tablet functionality should be assessed separately where the available screen size and workflow justify it.

### M. Maintain role-based permissions

The responsive design changes must not alter existing staff permissions.

**Support**

Ordinary support cases, eligible appeals, warnings and restrictions up to 30 days.

**Admin**

Support capabilities plus escalated and staff-affiliated cases, suspensions up to 90 days, termination, eligible appeals and provisional legal holds.

**Legal / Security officer**

Separate permissions for protected identities, legal holds, preserved evidence, disclosures and security incidents.

Legal does not automatically inherit Support or Admin permissions.

**Root Admin**

Admin capabilities plus staff-access and security administration.

Root Admin requires an explicit Legal grant for protected evidence access.

Hiding a page on mobile must not change the underlying role permissions.

All authorisation remains server-side.

### N. Standardise status and feedback messages

Replace verbose operational explanations with concise feedback.

Examples:

**Successful action**

"Changes saved."

**Failed action**

"Unable to save changes. Try again."

**No results**

"No matching records."

**Permission denied**

"You don't have permission to access this function."

Use consistent notification, validation and error components.

Avoid exposing internal API errors, development identifiers or implementation details.

Do not remove actionable information required to understand or recover from an error.

### O. Review every Admin route

This is an application-wide consistency issue.

Codex should audit all existing Admin pages rather than fixing only the Support Lookup screenshot.

For every route, review:

1. Page heading and description.
2. Unnecessary instructional text.
3. Internal development references.
4. Logging and monitoring notices.
5. Form control styling.
6. Button dimensions and spacing.
7. Table presentation.
8. Information panels.
9. Desktop layout.
10. Mobile and tablet availability.
11. Error and success feedback.
12. Accessibility.
13. Role-based visibility and permissions.

Document any route that requires an intentional exception.

### Steps to reproduce

1. Sign in to the Swoovr Admin application.
2. Navigate to `http://localhost:5082/`.
3. Open Support Lookup.
4. Observe the lengthy explanatory paragraph containing internal references and logging information.
5. Navigate through other Admin areas, including Accounts, Beta Enrollments, Photos and Law Enforcement.
6. Compare heading styles, descriptions, dropdowns, input fields and buttons.
7. Observe inconsistencies in control dimensions and spacing.
8. Identify pages displaying internal development codes or routine monitoring notices.
9. Review important legal and security pages containing large blocks of information.
10. Repeat the review at desktop, tablet and mobile viewport sizes.
11. Identify functions that should be restricted to desktop.

### Evidence

Evidence reference (not opened or publication-reviewed in this run): Admin Support Lookup showing excessive introductory text — `evidence/2026-11-01-issue-intake/25-remove-woke-text-each-admin-page.png`.

The screenshot demonstrates excessive explanatory text, internal issue references and routine logging statements.

This is one example of an application-wide problem.

### Impact

**Affected users:** All Swoovr Support, Admin, Legal/Security and Root Admin staff.

**Impact:**

- Unnecessary text reduces the amount of useful information visible.
- Pages feel cluttered and unfinished.
- Internal development references make UAT appear unlike production.
- Inconsistent controls reduce usability and increase maintenance overhead.
- Important operational guidance is difficult to distinguish from routine information.
- Excessive explanatory content slows administrative workflows.
- Complex workflows may be difficult or unsafe to operate on small screens.
- Inconsistent page designs reduce the overall quality of the Admin application.

A consistent, concise interface will improve efficiency, usability and maintainability across the entire administrative platform.

### Notes or constraints

**Primary design principles**

- Admin is an operational tool.
- Information should be concise and task-relevant.
- Do not display explanatory paragraphs unless they are genuinely useful.
- Keep essential legal and safety guidance.
- Remove internal development codes.
- Remove routine logging notices.
- Use a consistent Bootstrap-based design system.
- Design primarily for desktop.
- Explicitly classify mobile and tablet availability.
- Preserve all existing security, permissions and audit requirements.

**UAT and production**

UAT must use the same production-intended application UI and code.

Differences should be limited to appropriate environment configuration, integrations and test data.

Do not rely on a separate visual clean-up before production release.

**Monitoring and auditing**

Remove redundant UI notices such as "Your access is recorded".

Continue all existing administrative monitoring, auditing and security logging.

The absence of a notice must not imply that auditing is disabled.

Retain mandatory acknowledgements or notices where legally or operationally required.

**Important information**

Use contextual information panels where guidance materially assists the task.

Examples include Law Enforcement, Legal Holds and serious safety workflows.

Do not turn every page into a documentation screen.

**Desktop restrictions**

Codex should classify routes based on risk, complexity and operational suitability.

Desktop-only UI restrictions must not be mistaken for sufficient security controls.

**Related issues**

- **#3:** Admin authentication and session presentation.
- **#4:** Photo moderation controls.
- **#5:** Beta Enrollments layout.
- **#18:** Account search and table design.
- **#19:** Member account detail redesign.
- **#20:** Beta Enrollments styling and registration settings.
- **#21:** Admin refresh error handling.
- **#22:** High-volume photo moderation redesign.

This issue establishes the shared Admin presentation standards that should apply to those improvements.

**Suggested implementation order**

1. Audit all Admin routes.
2. Identify shared design inconsistencies.
3. Establish reusable Bootstrap components and design tokens.
4. Standardise page headings and content structure.
5. Remove unnecessary development and explanatory text.
6. Remove redundant monitoring notices.
7. Introduce reusable contextual information panels.
8. Standardise all form controls and button styling.
9. Classify Admin functions by desktop, tablet and mobile availability.
10. Implement desktop-required screens for unsupported workflows.
11. Validate role-based access controls remain unchanged.
12. Review responsive behaviour across supported routes.
13. Verify UAT and production use the same intended UI.
14. Add visual regression tests where practical.

### Acceptance criteria

**Content and presentation**

- [ ] Unnecessary introductory paragraphs are removed from Admin pages.
- [ ] Internal development identifiers are removed from normal UI content.
- [ ] Routine beta and development messaging is removed where not operationally relevant.
- [ ] Redundant "This action is recorded" and similar notices are removed.
- [ ] Essential safety and legal guidance remains available.
- [ ] Important contextual information uses structured panels where appropriate.
- [ ] Page headings use consistent styling and capitalisation.
- [ ] Feedback and validation messages are concise and consistent.

**Form controls**

- [ ] Text inputs use consistent styling and height.
- [ ] Dropdowns use consistent styling and height.
- [ ] Date pickers follow the shared design system.
- [ ] Checkboxes and switches are consistently presented.
- [ ] Text areas use appropriate dimensions and padding.
- [ ] Buttons have consistent sizing, spacing and visual hierarchy.
- [ ] No controls overlap or touch unnecessarily.
- [ ] Common UI elements use reusable components where appropriate.

**Responsive Admin experience**

- [ ] Desktop remains the primary Admin design target.
- [ ] Admin routes are classified for mobile and tablet suitability.
- [ ] Basic permitted operations have usable mobile layouts.
- [ ] Sensitive or complex workflows are restricted to desktop where required.
- [ ] Unsupported mobile workflows display a branded Desktop required screen.
- [ ] Protected content is not exposed through unavailable mobile views.
- [ ] Responsive presentation does not weaken backend authorisation.

**Security and operational integrity**

- [ ] Existing audit logging remains active.
- [ ] Mandatory legal and security acknowledgements are preserved.
- [ ] Support, Admin, Legal and Root Admin permission boundaries remain unchanged.
- [ ] Sensitive operations continue to enforce server-side authorisation.
- [ ] No existing administrative functionality is removed unintentionally.
- [ ] UAT uses the same production-intended UI and components.
- [ ] Automated or visual regression tests cover representative Admin pages.
