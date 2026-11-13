# UAT operations SOP

Plain-language procedures for running the Swoovr UAT environment day to day. Run every terminal command from your checkout (`cd ~/Developer/wanderconnect-brief`): `gh` takes the repository from it. Written for the owner, who doesn't work on the back end. Every procedure says **what to run**, **what you should see** and **what to do if you see something else**.

Technical detail lives in the [server runbook](../../deploy/vultr-uat/README.md) and [GitHub deployment](UAT-GITHUB-DEPLOYMENT.md). Decisions: D362 (deployment), D363 (staff roles from Entra), D364 (break-glass root admin).

## Contents

1. [How the system fits together](#1-how-the-system-fits-together)
2. [Running a maintenance action (and the tag rule)](#2-running-a-maintenance-action-and-the-tag-rule)
3. [Deploying a new version](#3-deploying-a-new-version)
4. [The break-glass root admin](#4-the-break-glass-root-admin)
5. [Staff access and roles (Microsoft Entra)](#5-staff-access-and-roles-microsoft-entra)
6. [Email](#6-email)
7. [Settings: where each value lives](#7-settings-where-each-value-lives)
8. [Looking at the server directly](#8-looking-at-the-server-directly)
9. [Quick reference](#9-quick-reference)

## 1. How the system fits together

```
your Mac ──git tag vX.Y.Z──▶ GitHub ──"Deploy UAT" workflow──▶ builds 5 images (GHCR)
                                     │                          └─▶ SSH as root ──▶ server uat-01 (Vultr)
                                     └─"UAT maintenance" workflow (you start it by hand) ──▶ SSH ──▶ uat-01
```

- **Server `uat-01`** (Vultr, Debian) runs everything in Docker: the API, the member web app (`uat.swoovr.app`), Admin (`admin-uat.swoovr.app`), Keycloak sign-in (`auth-uat.swoovr.app`), the Zammad helpdesk (`helpdesk-uat.swoovr.app`), PostgreSQL and supporting services. Cloudflare sits in front.
- **The `swoovr` command on the server** (`/opt/swoovr/swoovr-uat/bin/swoovr`) does every server-side step. GitHub only ever runs that command over SSH.
- **Two GitHub workflows:**
  - **Deploy UAT** runs by itself when you push a version tag (section 3).
  - **UAT maintenance** runs only when you start it, for small jobs that don't deploy anything (section 2).
- **Keycloak** handles every sign-in. Staff sign in with Microsoft (Entra) or, for the root admin only, with email + password + code (sections 4–5).
- **Settings** live in two places: GitHub (secrets and variables) and env files on the server (section 7).

## 2. Running a maintenance action (and the tag rule)

The **UAT maintenance** workflow runs one small action on the server, then stops. It never deploys, migrates, wipes or rolls back anything.

| Action | What it does | When to use it |
| --- | --- | --- |
| `status` | Lists the containers and their health, checked through the web server | "Is everything up?" |
| `history` | Lists the versions deployed, newest last | "What's running, and since when?" |
| `admin-invite` | Emails the break-glass root admin its set-up link again | The set-up email didn't arrive or its link expired (section 4) |
| `admin-finalize` | Gives the root admin role to the break-glass account if it's ready | After the break-glass account has completed set-up and signed in once (section 4) |

### The tag rule (important)

The GitHub environment `uat` holds the SSH key to the server. It only lets runs **from a version tag** (`v*`) use it, so no branch or pull request can reach the server. A maintenance run must therefore **start from a tag**, normally the version currently deployed.

If you start it from `main` instead, it fails straight away with **"The deployment was rejected or didn't satisfy other protection rules."** Nothing ran; start it again from a tag.

**Which tag?** The latest deployed version: see the newest run of **Deploy UAT** in Actions, or `git tag --sort=-v:refname | head -1`. In the commands below it's `v1.0.2`; replace it with the current one.

### From the terminal

```bash
gh workflow run uat-maintenance.yml --ref v1.0.2 -f action=status
gh workflow run uat-maintenance.yml --ref v1.0.2 -f action=history
gh workflow run uat-maintenance.yml --ref v1.0.2 -f action=admin-invite
gh workflow run uat-maintenance.yml --ref v1.0.2 -f action=admin-finalize
```

Then follow it and read the result:

```bash
gh run watch $(gh run list --workflow uat-maintenance.yml --limit 1 --json databaseId -q '.[0].databaseId')
```

### From the GitHub website

1. **Actions** → **UAT maintenance** (left column) → **Run workflow** (right).
2. **Use workflow from:** open the drop-down, switch to the **Tags** tab, pick the deployed version (e.g. `v1.0.2`). Not `main`.
3. **action:** pick the action.
4. **Run workflow**, then open the run. The **summary** at the bottom of the run page lists the results (`SWOOVR_RESULT` lines).

### Reading the result

The summary shows lines such as `root-account=invite-sent` or `initial-admin=granted`. In logs, GitHub shows `***` wherever a secret's value would appear. Because the SSH user secret is `root`, the word "root" itself appears as `***` (for example `***-account=invite-sent`). That is cosmetic.

## 3. Deploying a new version

Pushing a tag `vX.Y.Z` that points at a commit on `main` deploys it. Only the last number comes from the tag: `Directory.Build.props` holds the first two (`1.0`), so `v1.0.3` needs no file change. Moving to `v1.1.0` or `v2.0.0` needs `python3 scripts/release_version.py bump minor` (or `major`) merged first.

**Upgrade or refresh.** The repository variable `DEPLOYMENT_MODE` decides, when the run starts:

| Mode | What happens | Use it when |
| --- | --- | --- |
| `upgrade` (normal) | Backup, then new versions of the apps; **all data kept** | Every ordinary release |
| `refresh` | Backup, then **everything is deleted and rebuilt**: databases, Keycloak users, Zammad data and its mail setup, every photo in the R2 bucket | Sign-in/realm changes that need a rebuild, or a deliberate clean start |

```bash
gh variable get DEPLOYMENT_MODE          # check first
gh variable set DEPLOYMENT_MODE --body upgrade   # or refresh

cd ~/Developer/wanderconnect-brief && git checkout main && git pull
git tag v1.0.3 origin/main && git push origin v1.0.3
gh run watch $(gh run list --workflow deploy-uat.yml --limit 1 --json databaseId -q '.[0].databaseId')
```

**After a refresh:**
- Set `DEPLOYMENT_MODE` back to `upgrade` once you're happy, or the next tag wipes everything again.
- Repeat section 4 (the break-glass account is recreated and gets a new set-up email).
- Repeat the Zammad mailbox setup ([runbook "Zammad mail"](../../deploy/vultr-uat/README.md#zammad-mail-microsoft-365)).
- Staff sign in again with Microsoft and set up their authenticator again.

**If a deployment fails**, the run names the stage. Data is never deleted before a verified backup. See the [runbook "Recovery"](../../deploy/vultr-uat/README.md#recovery); the usual fix is to correct the cause and push a new tag (tags can't be reused or moved).

## 4. The break-glass root admin

There is exactly **one root admin**, and it is deliberately **not** a Microsoft/Entra account (D364). It's the address in the GitHub variable `UAT_INITIAL_ADMIN_EMAIL`, with its own password and authenticator app, so it still works if Entra, the RePass Cloud tenant or Microsoft 365 is down. Use it for emergencies and for root-only tasks. Day-to-day work uses an Entra account with the Admin role (section 5).

### Setting it up (after every refresh, or the first time)

1. **Deploy** (section 3). The run summary shows `root-account=created-invite-sent` and `initial-admin=invite-not-completed`. Every deployment creates the account if it doesn't exist and Keycloak emails it a set-up link, **valid 12 hours**. It never creates a second account.
2. **Open the email** (from Swoovr, `support@swoovr.com`; check junk too). Verify the address, choose a password (at least 12 characters), and scan the QR code with an authenticator app (Microsoft Authenticator, 1Password, etc.). **Keep a backup of the authenticator offline**: this account exists for emergencies.
   - **Email missing or link expired?** Run **`admin-invite`** (section 2). It only resends the email; no redeploy is needed. The summary should show `root-account=invite-sent`. If it still doesn't arrive, see section 6.
3. **Sign in once** at https://admin-uat.swoovr.app using the **email + password form** (not "Sign in with Microsoft"), then the 6-digit code. "Your role can't do this" is expected now.
4. **Run `admin-finalize`** (section 2). The summary should show `initial-admin=granted`. In Admin, choose **Sign in again**; the full workspace appears.

### What the `initial-admin` result means

| Result | Meaning | Do |
| --- | --- | --- |
| `invite-not-completed` | The email's steps aren't finished | Complete the email (step 2); `admin-invite` to resend |
| `unverified-email` / `missing-password-or-authenticator` | Part of the set-up is missing | Same as above |
| `pending-first-sign-in` | Set-up done, but never signed in to Admin | Step 3, then `admin-finalize` |
| `granted` / `already-granted` | Done | Nothing |
| `linked-to-identity-provider` | Someone linked this account to Microsoft; it must stay local | Ask for help; nothing was changed |
| `held-by-another-account` | Another account already has root | Ask for help; nothing was changed |
| `pending-invite` | No account exists yet (address not set?) | Check `UAT_INITIAL_ADMIN_EMAIL`, then deploy |

**Changing the address:** `gh variable set UAT_INITIAL_ADMIN_EMAIL --env uat --body '<address>'`. The next deployment creates the new account and emails it, but root is never moved from an existing holder automatically: `admin-finalize` reports `held-by-another-account` until no root exists, which in practice means after a **refresh**.

## 5. Staff access and roles (Microsoft Entra)

Staff sign in with **Sign in with Microsoft**. Two things in Entra decide what happens (D363):

| In Entra | Effect |
| --- | --- |
| Member of the group **Swoovr UAT** (assigned to the app with Default Access) | May sign in at all |
| Member of **Swoovr UAT Support / Trust & Safety / Admin / Legal** (each assigned to the app with its app role) | Gets that role in Swoovr |

- **Adding someone:** add them to **Swoovr UAT**, then to the role group they need. Entra lets anyone in *any* assigned group sign in, so always add to Swoovr UAT first as a habit.
- **Changes apply at their next Microsoft sign-in.** Removing someone from a role group removes that role then.
- **Roles granted in Swoovr Admin** (Staff → a person → Internal roles) are separate: Entra never changes or removes them. Roles that came from Entra show as "(managed in Entra)" and can't be edited in Admin.
- **First sign-in for a staff member:** Microsoft, then verify their email by link, then set up an authenticator app (Keycloak shows a QR code). After that first set-up, **sign out and in once more** so the session counts as multi-factor.
- **Helpdesk** (`helpdesk-uat.swoovr.app` → **Swoovr staff sign-in**): only works for staff who already have a role; Swoovr creates their helpdesk access from their roles within a minute. Without a role you get 403.

## 6. Email

### Who sends what

| Sender | Service | From | Used for |
| --- | --- | --- | --- |
| Keycloak | Twilio Email API | `support@swoovr.com` | Set-up links, email verification, sign-in emails |
| Swoovr API | Twilio Email API | `support@swoovr.com` | Member and staff notifications |
| Zammad | Microsoft 365 mailbox (set up by you in Zammad) | the support mailbox | Helpdesk tickets |
| SMS | Mailpit (a catch-all on the server, nothing leaves) | — | Until Twilio SMS is verified |

The service names and sender are in `env/mail.env` on the server (`SWOOVR_KEYCLOAK_MAIL_SERVICE`, `SWOOVR_API_MAIL_SERVICE`, `SWOOVR_MAIL_FROM`).

### "An email didn't arrive": the procedure

1. **Check junk / "Other"** in the receiving mailbox.
2. **Test the sending path** from your Mac. This sends one plain email through exactly the same provider, sender and key that Keycloak uses:
   ```bash
   scripts/uat/mail-test.sh <server IPv4> <your address>
   ```
   - `ACCEPTED by Twilio …` → the server and Twilio are fine. Go to step 3.
   - `REFUSED by Twilio: HTTP 401/403` → the Twilio email key is wrong or revoked: update `UAT_TWILIO_EMAIL_KEY_SID` / `UAT_TWILIO_EMAIL_KEY_SECRET` (section 7) and deploy.
   - `REFUSED … 400` → the sender `support@swoovr.com` / domain isn't accepted by Twilio: check the sender domain in Twilio.
3. **If the test arrives but the real email didn't**, resend the real one:
   - root admin set-up email → `admin-invite` (section 2);
   - a staff member's verification email → they sign in again; Keycloak offers "resend".
4. **If the resent email still doesn't arrive but the test does**, the receiving provider is filtering that message's content (for example its sign-in link). Check Twilio's log for that message: Twilio Console → Email / Communications logs, or the SendGrid **Activity Feed** (swoovr.com signs mail through SendGrid account `u73501063`). Note the status (delivered, bounced, dropped, deferred) and reason.

**Note:** "accepted by Twilio" (HTTP 202) means Twilio took the message, not that it reached the inbox. The current Twilio key can send but cannot read delivery status (error 8021, `twilio/comms/emails.operations/read`). Giving the key that permission in the Twilio Console would let delivery status be checked from the server.

**swoovr.com DNS** (Cloudflare): mail is hosted in Microsoft 365 (MX), DKIM `s1`/`s2._domainkey` point at SendGrid so Twilio can sign as swoovr.com, SPF lists only Microsoft, DMARC is `p=none`. Use `dig @1.1.1.1 …` to check; some local resolvers return nothing.

## 7. Settings: where each value lives

| Kind | Where | How to change |
| --- | --- | --- |
| Deployment secrets and provider values (SSH, Entra, Didit, R2, Twilio…) | GitHub → Settings → Environments → **uat** | `scripts/uat/setup-github-uat.sh template` writes a private file listing every value with a description; fill it in, then `scripts/uat/setup-github-uat.sh values <file>`; check with `scripts/uat/setup-github-uat.sh status`; delete the file |
| A single variable | same | `gh variable set NAME --env uat --body '<value>'` |
| A single secret | same | `gh secret set NAME --env uat` (paste the value when asked) |
| Upgrade vs refresh | GitHub repository variable `DEPLOYMENT_MODE` | section 3 |
| Values generated on the server (database passwords, data keys, Zammad tokens) | `/opt/swoovr/swoovr-uat/env/*.env` on the server | Never in GitHub; they survive upgrades and refreshes |

A value left **empty** in GitHub keeps whatever the server already has. GitHub changes reach the server at the next deployment.

## 8. Looking at the server directly

Only when a procedure above says so. Everything here is read-only unless stated.

```bash
ssh -i ~/.ssh/swoovr-uat-deploy -o IdentitiesOnly=yes root@<server IPv4>
cd /opt/swoovr/swoovr-uat
./bin/swoovr status        # containers and health
./bin/swoovr history       # deployed versions
./bin/swoovr logs api      # recent logs of one service (api, web, admin, keycloak, zammad-railsserver, …)
exit
```

The full list of server commands is at the top of `deploy/vultr-uat/bin/swoovr` and in the [runbook "Day-2 commands"](../../deploy/vultr-uat/README.md#day-2-commands-on-the-host-exceptional).

## 9. Quick reference

| I want to… | Do |
| --- | --- |
| Deploy a normal release | Check `DEPLOYMENT_MODE=upgrade`, push tag `vX.Y.Z` on `main` (section 3) |
| Wipe and rebuild UAT | `DEPLOYMENT_MODE=refresh`, push a tag, then sections 4 and 5, then back to `upgrade` |
| Resend the root admin's set-up email | Maintenance `admin-invite` **from the tag** (section 2) |
| Give the root admin role | Maintenance `admin-finalize` **from the tag** after the account signed in once |
| Give someone a staff role | Add them to Swoovr UAT + the role group in Entra (section 5) |
| Check an email problem | `scripts/uat/mail-test.sh <server IPv4> <address>` (section 6) |
| See what's running | Maintenance `status` / `history` |
| Change a setting | Section 7, then deploy |
