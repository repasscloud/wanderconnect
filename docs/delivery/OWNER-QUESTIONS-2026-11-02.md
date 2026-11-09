# Owner questions — 2 November 2026

**Current UAT pointer (10 November 2026):** [D355](D355-UAT-HOSTNAMES-2026-11-10.md) supersedes historical nested hostnames and paid edge certificate assumptions. UAT hosting is OPEN, including Vultr; production remains Azure. Earlier records below preserve their original wording; Azure infrastructure is an optional implementation.


Questions for every open issue that needs something from the owner, written after the full issue relabel on 2 November 2026. Each question has an id (`124.1` = issue #124, question 1), options where they make sense, and a **Suggested** answer: the builder's recommendation, not a decision. Nothing in this file is CONFIRMED until the owner answers on the issue.

## How to answer

- Answer on the GitHub issue, one line per question: `124.1 A`, `150.3 Free 8 / Plus 16 / Premium 24`, or `161.1 B because …`. "Suggested" (or `124.* suggested`) accepts the suggestion.
- Then add the `decided` label (and `owner-update` if you added new information). Claude records the answers in DECISIONS.md and QUESTIONS.md, removes the labels, and closes or updates the issue with the recording PR.
- Skip any question you're not ready for; it stays open.

| Part | Issues | What's needed |
| --- | --- | --- |
| [1. Decisions](#part-1--decisions-decision-needed) | #124, #148, #150, #155, #156, #157, #159, #160, #161, #162, #163, #165 | A product or policy decision before building |
| [2. Your feedback, clarified](#part-2--your-recent-feedback-clarified) | #141, #158 | Short follow-ups to comments you left |
| [3. Things only you can do](#part-3--things-only-you-can-do-owner-action) | #57, #119, #127 | Actions outside the code |
| [4. Check and close](#part-4--check-and-close-needs-verification) | #33, #103, #138, #139, #142–#151 | Try the merged fix; say "works" or what's wrong |

---

## Part 1 — Decisions (`decision-needed`)

### #124 — Q90 UAT hosting topology, Azure region and subscription ownership

Your 2 November comment confirms UAT is `https://uat.swoovr.app` and local is `http://localhost:<port>`. The rest of the hosting proposal (D221) is still unapproved. Nothing will be provisioned or deployed by these answers alone.

- **124.1 Azure region.** A: Australia East (Sydney). B: Australia Southeast (Melbourne). C: other. *Suggested: A — matches the Sydney processing described in the legal-approved privacy notice (D288).*
- **124.2 Hosting for Api, Web and Admin.** A: Azure Container Apps, one API replica to start (SignalR has no backplane yet). B: Azure App Service. *Suggested: A.*
- **124.3 Keycloak.** Its own Container App, state in Azure Database for PostgreSQL Flexible Server, with separate databases and logins for the app and Keycloak, and realms named without `-dev`. Approve? Yes / No (say what instead).
- **124.4 Hostnames.** Confirm `uat.swoovr.app` (members), `admin.uat.swoovr.app`, `api.uat.swoovr.app` and `auth.uat.swoovr.app` (Keycloak). Yes / list changes.
- **124.5 Edge certificate.** Cloudflare's free Universal SSL doesn't cover second-level names like `api.uat.swoovr.app` (EDGE-UAT.md). A: approve a paid Cloudflare edge certificate that covers them (Advanced Certificate Manager or equivalent) plus a Cloudflare origin certificate in Key Vault. B: use first-level names instead (e.g. `uat-api.swoovr.app`, `uat-admin.swoovr.app`, `uat-auth.swoovr.app`), which free Universal SSL covers. *Suggested: B if you don't want another paid add-on; otherwise A.*
- **124.6 Cloudflare Tunnel.** A: no tunnel; Cloudflare proxies to the HTTPS origins, which accept only Cloudflare traffic. B: keep a dedicated Cloudflare Tunnel for UAT. *Suggested: A.*
- **124.7 Backups.** Encrypted Azure Blob storage: daily kept 30 days, weekly kept 4 weeks, monthly kept 12 months, covering the app and Keycloak databases, with one restore drill before testers join. Approve? Yes / amend.
- **124.8 Naming and tags.** Resource group `rg-swoovr-uat-aue`, prefix `swoovr-uat`, tags `product=Swoovr`, `environment=uat`, `owner=RePassCloud`. Approve? Yes / amend.
- **124.9 Subscription.** Who creates or selects the Azure subscription, what is it called, and who is the billing owner?
- **124.10 Deployment rights.** A: you run every deployment yourself from the runbook. B: Claude may deploy to UAT when you say so in a specific session, using credentials you provide. *Suggested: A until UAT has run once.*

### #148 — Q106 Me / Account page and onboarding (the remaining part)

The layout part is merged (PR #154; see #148 in Part 4). These are the open product questions.

- **148.1 Onboarding order.** A: 1 verify mobile, 2 date of birth, shown as "Step 1 of 2 / Step 2 of 2" (as in your report). B: date of birth first, then mobile. *Suggested: A.*
- **148.2 When the mobile gate is switched off** (`Phone:Required` off, as in some local setups), is onboarding just the date-of-birth step? Yes / No.
- **148.3 Before onboarding is finished**, what do Explore, Connections and Chats do? A: the tabs stay visible; opening one shows "Finish setting up your account" with a button straight to the missing step. B: the tabs are hidden until setup is done. *Suggested: A.*
- **148.4 "Before you connect or meet" notice.** A: shown once after onboarding, with "Got it"; dismissal is stored on the server (all devices), and it shows again only when its wording changes (a new revision). B: always shown. *Suggested: A.*
- **148.5 After it's dismissed**, where does the safety advice stay reachable? A: a "Safety & advice" entry in the account navigation plus the footer. B: footer only.
- **148.6 Coupon codes.** Move coupon redemption into a "Membership & billing" section? Yes / No.
- **148.7 Desktop account navigation.** A: a left sidebar (collapsible on narrow windows) with sections Profile, Photos, Who I'd like to meet, Verification, Privacy & safety, Notifications, Appearance, Membership & billing, Account. B: tabs across the top. *Suggested: A.*
- **148.8 Desktop footer links.** Terms, Community Guidelines, Connection & meeting advice, Privacy, and Support (`https://swoovr.com/support`, from your #141 comment). Anything to add or remove?

### #150 — Q108 Photos: a separate main photo per mode and tier limits

Today: one main photo shared by every mode (CONFIRMED D175), up to 6 extra photos per mode, up to 30 photos kept, the same on every tier.

- **150.1 Main photo.** A: each mode has its own main photo; no shared main (your report). B: keep one shared main photo (D175 as is). C: one default main photo, which a mode can optionally override. *Suggested: A or C. Separate main photos also help keep Business separate from Dating. A replaces D175's shared main photo.*
- **150.2 Library size.** Keep up to 60 photos per member on every tier (today 30)? Yes / other number.
- **150.3 Photos per mode, including the main photo.** Free 8 / Plus 16 / Premium 24 (your report; today 1 main + 6 on every tier)? Yes / other numbers.
- **150.4 Downgrades.** When a member drops to a tier with a lower limit: A: nothing is deleted; photos beyond the new limit (the last in their order) stop showing in that mode until they upgrade or reorder. B: the member must pick which photos to keep showing before the downgrade takes effect. *Suggested: A.*
- **150.5 A mode with no main photo.** A: the mode can't be shown in discovery until it has one, and the page says "add a main photo". B: shown with a placeholder. *Suggested: A.*
- **150.6 Moving existing members over.** The current shared main photo becomes the main photo of every mode the member has enabled. Approve? Yes / No.

### #155 — Q110 Admin account search by phone and an account browser

Exact-email search works (#137). Not decided: who may use phone search and the browser, and which personal fields they can filter and see.

- **155.1 Phone search** (exact registered mobile number) — which roles? A: Support, Admin and Root Admin. B: Admin and Root Admin only. *Suggested: A.*
- **155.2 Phone number in results.** A: masked (`+61 4•• ••• 123`), with the full number only after the existing elevated-access step with a recorded purpose. B: always shown in full to those roles. *Suggested: A.*
- **155.3 Account browser** (a paged list of all accounts) — which roles? A: Admin and Root Admin. B: Support as well. *Suggested: A.*
- **155.4 Filters allowed in the browser.** Tick the ones you want: account state; standing / sanction; membership tier; email / mobile / ID verification state; home market; created date; last active date; age band; gender; enabled modes. Leave out any that staff shouldn't be able to filter people by. *Suggested: all except gender.*
- **155.5 Export from the browser.** A: none; the existing approved evidence export stays the only bulk export. B: CSV export for Root Admin. *Suggested: A.*
- **155.6 Search by registered name** (depends on #162). Once registered first and last names exist, can Support search by them? Yes / No / Admin only.

### #156 — Q111 Admin account details and profile inspection

- **156.1 Age and date of birth.** A: every staff role sees calculated age and "DOB verified / not verified"; the full date of birth only after elevated access with a recorded purpose. B: Admin and Root Admin always see the full date of birth. *Suggested: A.*
- **156.2 "View profile as members see it".** A read-only preview of each mode (no profile visit recorded, no impersonation) — which roles? A: Support, Admin, Root Admin. B: Admin and Root Admin. *Suggested: A.*
- **156.3 Photos members can't see** (unassigned, pending or rejected). A: Admin only, through the existing blurred, audited reveal. B: Support as well. *Suggested: A.*
- **156.4 Conversations.** Confirm staff can only read a conversation through a report or case (the existing case evidence), never by browsing an account's chats. Yes / No.
- **156.5 Role boundaries.** Confirm: Legal gets no moderation actions just by being Legal, and Root Admin gets no Legal access (legal holds, law enforcement, identity) just by being Root. Yes / No.

### #157 — Q112 Beta, public and closed registration

- **157.1 Two switches, four states.** Approve: Beta off + Public off = **closed** (no new accounts); Beta on + Public off = **invite-only beta** (today); Beta off + Public on = **public**; Beta on + Public on = **public, with beta enrolments still tracked**. Yes / amend.
- **157.2a Who can change the registration setting.** A: Root Admin only, with a reason and a confirmation step, audited. B: Admin as well. *Suggested: A.*
- **157.2b Who can see the current registration state** in Admin. A: all staff. B: Admin and Root Admin only. *Suggested: A.*
- **157.3 What "closed" covers.** Confirm that closing registration also hides "Create account" in Keycloak and refuses new accounts there (including a first-time Apple or Google sign-in), not just in the app. Yes / No.
- **157.4 If the setting can't be read**, new registrations are refused (fail closed); existing members can still sign in and reset passwords. Confirm? Yes / No.
- **157.5 UAT starting state.** Invite-only beta (Beta on, Public off)? Yes / other.
- **157.6 Message when registration is closed** to someone who isn't invited. A: "Swoovr isn't open for new accounts yet" with a link to `https://swoovr.com/beta`. B: your wording.

### #159 — Q114 Photo-review queue

- **159.1 CSAM / safety warning acknowledgement.** A: required when a reviewer enters Photo Review, stored on the server per staff member and notice version, valid 4 hours (your report). B: once per notice version. C: every visit. *Suggested: A.*
- **159.2 Who reviews photos.** A: Admin only (today). B: Support as well. *Suggested: A.*
- **159.3 Photo-review counts on the Admin overview.** A: shown only to staff who can review photos. B: shown to all staff. *Suggested: A.*
- **159.4 Notes on approvals.** A: an optional private note on an approval, kept as long as other moderation records in the retention schedule. B: no free-text notes on approvals; reason codes only. *Suggested: B (rejections keep the "Other" note).*
- **159.5 Tablet and phone.** A: photo review works on a tablet in a full-screen dialog and is hidden on phones. B: desktop only. C: works everywhere. (Pairs with 165.3.) *Suggested: A.*

### #160 — Q115 Photo rejection notices and appeals

- **160.1 Telling members.** On rejection, an in-app notice ("This photo wasn't approved: <plain reason>"), and optionally email. A: in-app only. B: in-app plus email. *Suggested: A.*
- **160.2 Appeals.** A: one appeal per rejected photo within 30 days, decided by a different staff member, final (the same rules as D135 sanction appeals). B: no photo appeals; the member uploads a different photo. *Suggested: A.*
- **160.3 Never appealable and no detailed reason.** Photos blocked for suspected child sexual abuse material, known illegal content or specialist escalation. Confirm? Yes / No.
- **160.4 Keeping the rejected file.** A: kept privately for the 30-day appeal window, then deleted unless a legal hold applies. B: deleted at rejection (an appeal can't then look at the image again). *Suggested: A.*
- **160.5 Who decides appeals.** A: an Admin who didn't make the original decision. B: Root Admin only. *Suggested: A.*
- **160.6 Reconsidering without an appeal.** Can an Admin overturn a rejection on their own initiative (recorded with a reason, re-screened first)? Yes / No.

### #161 — Q116 Locking the date of birth

Today (CONFIRMED D158) a member can change an **unverified** date of birth any time; a verified one is locked.

- **161.1 Lock for everyone.** A: lock the date of birth once the member first confirms it, verified or not (your report; replaces D158's "unverified can change"). B: keep D158. *Suggested: A.*
- **161.2 Confirmation.** A "Is this right? You can't change it later without support" dialog before the first save. Yes / No.
- **161.3 Existing members** who already have a date of birth: locked straight away, with no window to change it. Yes / No.
- **161.4 Corrections.** The member asks support (`https://swoovr.com/support`); an Admin or Root Admin grants one correction, valid 24 hours, used once, audited. Support can't grant. Approve? Yes / change the hours or roles.
- **161.5 ID-verified members who correct their date.** A: the new date is checked against the verified identity record (D191); a match stays verified, a mismatch goes to the date-of-birth review queue, and the ID Verified badge stays meanwhile (as D166 does). B: the date becomes unverified and the badge is removed until a new Didit check. *Suggested: A.*
- **161.6 Previous dates.** A: keep the previous value encrypted, Legal-only, for the retention schedule's period. B: don't keep it; the audit records only that a change happened. *Suggested: A — useful if someone lies about their age.*

### #162 — Q117 Registered first and last names

- **162.1 Last name.** A: required at registration. B: optional. *Suggested: A.*
- **162.2 People with one name.** A: an "I only have one name" option that leaves the last name empty. B: no exception. *Suggested: A.*
- **162.3 Apple / Google sign-up.** Use the names the provider sends, and ask the member to confirm them or fill in anything missing on their first sign-in. Yes / No.
- **162.4 Existing accounts without a last name.** A: asked at next sign-in and required before continuing. B: asked, but they can skip it. *Suggested: A.*
- **162.5 First public display name.** Set to the first name only, only when the profile has no display name yet, and never overwriting a name the member chose. Yes / No.
- **162.6 Staff view.** Registered first + last name is the main identifier in Admin for: A: Support, Admin and Root Admin. B: Admin and Root Admin (Support sees display name and reference). *Suggested: A.*
- **162.7 Changing registered names.** A: the member can edit them in account settings (audited). B: only through support. *Suggested: A.*
- **162.8 Comparing with ID verification.** A: keep registered names and verified identity completely separate. B: show staff a "registered name matches ID: yes/no" flag (no names revealed). *Suggested: B.*

### #163 — Q118 Per-mode display names

- **163.1 Allow per-mode display names?** A: yes, an optional name per mode, falling back to the main display name (replaces D175's shared display name). B: no, keep one display name. *Suggested: A, mainly for Business.*
- **163.2 Which modes.** A: all four. B: Business only. *Suggested: A.*
- **163.3 Chat header.** Two people have one conversation across modes (D143). Which name shows at the top? A: the name for the mode the conversation started in. B: the main display name. *Suggested: A.*
- **163.4 Never fall back to registered names.** Confirm an empty mode name never shows the registered first or last name (only the main display name). Yes / No.

### #165 — Q120 Admin on desktop, tablet and phone

Your report's table: Overview, Support lookup, basic cases, contact history (limited), account search (basic), account detail (summary) and beta enrolments (limited) work on phone and tablet. Photo moderation, identifier review, serious incidents, legal holds, law enforcement, protected evidence, evidence export, staff permissions and Root security are desktop only.

- **165.1 The table.** Approve as written? Yes / list the rows to change.
- **165.2 What "desktop only" means.** A: a layout rule; on small screens the page shows "Open this on a desktop" instead of the tools. It is not a security control, because screen size can't be trusted. B: a security rule requiring a managed or trusted device (needs Entra conditional access once hosted). *Suggested: A now; B later with Entra.*
- **165.3 Tablets.** Count as A: desktop (full access). B: phone (limited). *Suggested: A, which also settles 159.5.*
- **165.4 What a phone shows on a desktop-only page.** "This page needs a larger screen" plus a link back to the overview. Yes / your wording.

---

## Part 2 — Your recent feedback, clarified

### #141 — Q99 Landing page and what guests can see (your 2 November comment)

Your instructions will be built as written: guests can only open `/`, `/terms`, `/community-guidelines`, `/privacy`, `/safety/advice` and a guest version of `/safety` (country resources only, no "On Swoovr", "I'm meeting someone" or "Report a serious safety incident" section). Every other route redirects to `/`. Support links go to `https://swoovr.com/support`.

- **141.1 Where guests sign in.** With the top-right links gone: A: Sign in and Create account buttons on `/` only. B: a Sign in button on `/` only; Create account is reached from the sign-in page. *Suggested: A.*
- **141.2 "(draft)".** Legal approval is recorded (D288). Confirm the Terms, Community Guidelines, Privacy notice and safety advice are now the operative versions, so "(draft)" can come off the web, the app and the Keycloak sign-up page. Yes / No.
- **141.3 "Private beta".** Remove it everywhere, including the Keycloak pages and the "you're not in the beta yet" message? Or keep it in that one message? A: remove everywhere. B: keep it only in the not-invited message.
- **141.4 Redirect behaviour.** A guest opening e.g. `/me/photos` goes to `/` with no message. After they sign in, A: always land on Explore. B: return to the page they first asked for. *Suggested: A (no route hints).*
- **141.5 Guest `/safety` country picker.** Keep the country dropdown (defaulting from the visitor's country)? Yes / No.
- **141.6 Appearance for guests.** Guests always see Standard black and white, with no theme picker (personal schemes are for signed-in members). Confirm? Yes / No.
- **141.7 The app.** Same rule in the iOS/Android app: before sign-in only the welcome screen and those public pages. Yes / No.

### #158 — Q113 Safari Admin refresh error

Thanks for the Safari version (27.2). To reproduce it:

- **158.1 Does it still happen** on today's main? Yes / No / haven't tried.
- **158.2 Which page and action.** Which Admin page were you on, and what did you click last before refreshing (e.g. "Search" on Accounts, "Approve" on Photos)?
- **158.3 How you refreshed.** Cmd+R / the reload button / reopening the tab? Did Safari ask "resend form"?
- **158.4 Timing.** Had the page been open a long time (over an hour) before the refresh?

---

## Part 3 — Things only you can do (`owner-action`)

### #57 — External reviews before an unrestricted external beta

Legal review is done (D288). The application-security and app-store-policy reviews are still open.

- **57.1a Security reviewer.** Who will do the application-security review / penetration test? (A name, a firm, or "not chosen yet".)
- **57.1b Security review timing.** A: before UAT testers join. B: before the unrestricted external beta.
- **57.2 App-store review.** Will you review against the current App Store and Google Play rules yourself, or engage someone?
- **57.3 Gate.** Confirm invited UAT testers may use the app before these two reviews, and only the *unrestricted* external beta waits for them. Yes / No.

### #119 — Online Dating Code organisational items

Templates are ready (D220, D264). These need people and agreements.

- **119.1 Trust and Safety Leader.** Who is it (a name or role; "me for now" is fine)?
- **119.2 Police.** A: invite each Australian police agency to an MOU. B: adopt the ANZPAA information-sharing protocol. C: decide later.
- **119.3 Safety by Design self-assessment.** When will you complete the draft (SAFETY-BY-DESIGN-SELF-ASSESSMENT-DRAFT.md)?
- **119.4 Industry forums.** Any you plan to join, or "later"?

### #127 — Push notifications on real devices

- **127.1 Accounts.** Do the Apple Developer account (for `com.swoovr.app`) and the Firebase project exist yet? Yes / No / when.
- **127.2 Devices.** Which physical iPhone (model, iOS version) and Android phone (model, Android version) will you test on?
- **127.3 Credentials.** When the APNs `.p8` key and the Firebase service-account file are in `.local/` (referenced from `.local/docker.env`, as D210 says), tell the issue they're ready. Never paste keys into an issue.

---

## Part 4 — Check and close (`needs-verification`)

The fix for each of these is merged into main. Try it on your local stack (member web :5081, Admin :5082) and reply **"works"** (Claude closes it with the next PR) or describe what's still wrong. Issues that also have a Part 1 question stay open for the remaining part.

| Issue | What to check |
| --- | --- |
| #33 — DOB differs from the verified ID | An approved ID check with a different date of birth shows a one-time correction prompt, and a mismatch lands in Admin's date-of-birth review (D166). |
| #103 — Country Safety & Support resources | `/safety` shows resources for your country, the country dropdown changes them, and Admin can edit resources. |
| #138 — Q96 Admin session expiry | Admin stays signed in while you move between pages for a long period, and the signed-out page is branded. |
| #139 — Q97 Photo moderation | Previews are blurred until you reveal them by mouse or keyboard; approve/reject use reason codes, and "Other" needs a note. |
| #142 — Q100 Guest `/account` | Signed out, `/account` redirects instead of showing account content. (#141 widens this to every member route.) |
| #143 — Q101 Sign-in design | The sign-in page matches the approved design on desktop and phone width. |
| #144 — Q102 Registration screen | The Create account page is laid out properly on desktop and phone. |
| #145 — Q103 Email already registered | Registering with an existing email shows a clear error next to the field, with focus on it. |
| #146 — Q104 Forgot password screen | The reset page matches the sign-in design. |
| #147 — Q105 Reset confirmation | After "send reset link" you see a confirmation page with no password form, and the same text whether or not the email exists. |
| #148 — Q106 Account page layout | Settings are grouped and the heading is right. (Onboarding questions: Part 1.) |
| #149 — Q107 Profile and modes | The profile/mode page fits desktop and phone, city labels don't overlap, and turning visibility off sticks after reload. |
| #150 — Q108 Photos page | One mode's gallery shows at a time; the shared library and main photo still work. (Per-mode main photos: Part 1.) |
| #151 — Q109 Who I'd like to meet | Unsaved filters survive switching modes; Save is disabled while saving. |

Also: **#137 — Q95 Admin email search.** You confirmed it works; it closes with the next recording PR. No action needed.

## Overnight follow-ups — 3 November 2026

These new Q records arose while implementing the owner answers. Answer using the GitHub issue ids below, as in the earlier sections; the builder continues independent work.

### #170 — Q121 Android Firebase package

- **170.1 Package choice.** A: register current `com.swoovr.app` in the existing Firebase project and replace the ignored local configuration. B: approve `com.swoovr.app.uat` for the test build and review callbacks/signing. *Suggested: A.*
- **170.2 Corrected file.** Confirm `.local/firebase/google-services.json` is ready after A. Never paste credentials.

### #172 — Q122 Safety industry forums (later)

- **172.1 Timing.** A: research a concise shortlist before public-beta readiness; owner chooses memberships later. B: defer until after invited UAT. *Suggested: A.* No joining, payment or outreach.

### #173 — Q123 Trusted-contact safety pages

- **173.1 Guest exception.** A: explicitly allow existing capability-token/code-protected trusted-contact meeting-safety pages, keeping all expiry/consent/rate/privacy checks. B: keep an absolute six-page guest rule and explicitly remove/disable that sharing journey. *Suggested: A.* The current strict implementation follows D290 while this is OPEN.
