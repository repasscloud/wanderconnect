# UAT deployment through GitHub Actions: configuration

D362 (DEFAULT design on the owner's request of 12 November 2026). Pushing a `vX.Y.Z` tag from `main` builds the five Swoovr images for linux/amd64, publishes them publicly to GHCR, connects to the UAT VM as root over verified SSH, runs the selected mode (upgrade or refresh), provisions the stack, and checks it. This page covers the one-time GitHub configuration and the inventory of every value. The operator runbook is [deploy/vultr-uat/README.md](../../deploy/vultr-uat/README.md). The background investigation is [UAT-DEPLOYMENT-PREREQUISITES.md](UAT-DEPLOYMENT-PREREQUISITES.md).

**Status: implemented and tested locally; not yet run on GitHub or on the VM.** The workflows run only after the owner merges them and completes the prerequisites below.

## Workflows

| File | Trigger | Does |
| --- | --- | --- |
| [`.github/workflows/deploy-uat.yml`](../../.github/workflows/deploy-uat.yml) | push of a tag matching `v*.*.*`, then strictly validated as `vX.Y.Z` | validate → build → deploy |
| [`.github/workflows/uat-maintenance.yml`](../../.github/workflows/uat-maintenance.yml) | manual (Actions → UAT maintenance → Run workflow) | `status`, `history` or `admin-finalize` over SSH; never deploys |

Both share the concurrency group `swoovr-uat-deploy`: one UAT run at a time, never cancelled half-way. GitHub keeps at most one run *pending* per group, so pushing three tags quickly runs the first, and of the other two only the newest stays pending (the middle one is cancelled before it starts).

The superseded `images.yml` (Docker Hub, multi-arch, manual bundle download; D356/D359) is removed: there is one build and deployment system.

### What the deploy workflow checks before touching the server

1. The tag is exactly `vX.Y.Z` (digits, no leading zeros, no suffix). `v1.0.0-rc1`, `v01.0.0`, `v1.0` and `1.0.0` are refused.
2. The tag points at the commit being built, and that commit is reachable from `origin/main` (`git merge-base --is-ancestor`). A tag on another branch fails.
3. `X.Y` equals `Directory.Build.props` Major.Minor (`scripts/release_version.py msbuild-args`), and `scripts/check_version.py` passes. The images are compiled as version `X.Y.Z` (D320).
4. `DEPLOYMENT_MODE` is read **once** in `validate` and checked with an if/elif/else: empty or `upgrade` means upgrade, `refresh` means refresh, and anything else stops the run before any build or SSH. The checked value is passed to the deploy job as a job output; nothing reads the variable again.

## Upgrade or refresh

**CONFIRMED** (owner, 12 November 2026): the repository variable `DEPLOYMENT_MODE` alone selects the mode. There's no separate approval step or confirmation variable:

```sh
gh variable set DEPLOYMENT_MODE --body refresh    # the next tag push wipes and recreates UAT's application data
gh variable set DEPLOYMENT_MODE --body upgrade    # back to normal releases (the default when unset)
```

The variable is read when the run starts, not stored in the tag. So **set it back to `upgrade` straight after a refresh**, or the next tag refreshes again. The run's summary and the host history (`state/releases.jsonl`) record the mode of every deployment, and a refresh run shows a warning annotation.

What a refresh still guarantees on the VM:
- The workflow passes `--confirm-refresh swoovr-uat:<tag>` automatically, so `bin/swoovr` refuses a refresh started any other way.
- It takes and verifies a backup first (`pg_dump` of `swoovr`, `keycloak` and the Zammad database, checked with `pg_restore --list`, plus Zammad storage), and stops before deleting anything if that fails.
- It then removes only the `swoovr-uat` Compose project's containers and volumes (no pruning), empties the R2 photo bucket (`Media__R2__Bucket`; the bucket itself is kept), and bootstraps everything again.
- The host, its certificates and the server-held secrets stay as they are.

## Prerequisites (one time)

1. **Merge** the D362 change to `main` (the owner enables the two workflows by merging; they are listed in `ENABLED_WORKFLOWS` in `scripts/check_design.py`).
2. **GHCR package visibility.** The first run pushes `ghcr.io/repasscloud/swoovr-{api,web,admin,operator,keycloak}`. Public packages are already allowed in the repasscloud organisation (owner, 12 November 2026), but a **new package is created private**. After the first build, the deploy job stops at *Images are public* with a pointer. For each package: repasscloud → Packages → `swoovr-<image>` → **Package settings** → **Change visibility** → **Public**. Then re-run the failed job. It checks an **anonymous** pull of every digest on every run. Images are never rebuilt for a commit that already has them.
3. **Environment:** `scripts/uat/setup-github-uat.sh environments` creates the GitHub Environment `uat`, which holds the secrets and variables below. It limits it to workflow runs for `v*` tags, so a branch or pull-request run can't read the SSH key, and sets `DEPLOYMENT_MODE=upgrade`. You can also do this by hand: Settings → Environments → New environment `uat` → Deployment branches and tags → Selected → add tag rule `v*`.
4. **Tag protection (recommended):** `scripts/uat/setup-github-uat.sh tag-protection` creates a repository ruleset ("Release tags") for `v*`. Anyone with write access can still create a tag, but nobody except repository admins can move it to another commit or delete it. A deployed version then always means the same code. The same in the UI: Settings → Rules → Rulesets → New tag ruleset → target `v*` → *Restrict updates* and *Restrict deletions*, bypass: Repository admin.
5. **SSH from GitHub's runners.** No Vultr firewall group is in front of the VM (owner, 12 November 2026). The host accepts key-only SSH on port 22 from any address (nftables `swoovr_host` and UFW both allow it; root by key only, `PermitRootLogin without-password`, collected 12 November 2026). Nothing needs opening. If you add a Vultr firewall group later, it must allow port 22 from GitHub's runners.
6. **Deploy key:** create a dedicated key on your Mac, never reused elsewhere, with no passphrase (the workflow uses the key file directly): `ssh-keygen -t ed25519 -f ~/.ssh/swoovr-uat-deploy -N "" -C swoovr-uat-github-deploy`. Append the `.pub` line to `/root/.ssh/authorized_keys` on the VM (prefix `restrict` is fine; it still allows commands). This is one manual host change.
7. **Host key:** `scripts/uat/setup-github-uat.sh host-key <VM IPv4>` prints the ED25519 key and its fingerprint. It must equal the ED25519 fingerprint in `ssh/host-key-fingerprints.txt` of your 12 November collection. Only then store it as `UAT_SSH_HOST_KEY`.
8. **Values:** `scripts/uat/setup-github-uat.sh template` writes `~/swoovr-uat-backups/github-uat.env` (mode 0600; it never overwrites an existing file). It lists every secret and variable, each with a comment saying what it is and whether it is **REQUIRED** or optional. `UAT_SSH_USER=root`, `UAT_EXPECTED_HOSTNAME=uat-01` and `UAT_SSH_PRIVATE_KEY=~/.ssh/swoovr-uat-deploy` are already filled in. `UAT_SSH_PRIVATE_KEY` is the **path** to the key file, not the key. Fill in what you want GitHub to own, then run `scripts/uat/setup-github-uat.sh values ~/swoovr-uat-backups/github-uat.env`. Blank optional values are skipped, and the host keeps its current value. A blank required value is prompted for, with hidden input. Check with `scripts/uat/setup-github-uat.sh status`, then delete the file.
9. **Entra:** confirm the redirect URI and assignment (runbook "Microsoft Entra").

## Inventory

### 1. GitHub Environment secrets (`uat`)

| Secret | Configuration key on the host | Notes |
| --- | --- | --- |
| `UAT_SSH_HOST` | — | VM public IPv4 (or a host name) |
| `UAT_SSH_USER` | — | must be `root`; the workflow refuses anything else |
| `UAT_SSH_PRIVATE_KEY` | — | the dedicated deploy key (OpenSSH format) |
| `UAT_SSH_HOST_KEY` | — | one line `ssh-ed25519 AAAA…`; the workflow writes its own known_hosts and uses `StrictHostKeyChecking yes` |
| `UAT_ENTRA_CLIENT_SECRET` | `realms.env` `SWOOVR_UAT_ENTRA_CLIENT_SECRET` → Keycloak `entra` broker | re-applied by `keycloak-reconcile` on every deployment |
| `UAT_DIDIT_API_KEY` | `api.env` `Didit__ApiKey` | |
| `UAT_DIDIT_WEBHOOK_SECRET` | `api.env` `Didit__WebhookSecret` | same value as in the Didit console |
| `UAT_R2_ACCESS_KEY_ID` | `api.env` `Media__R2__AccessKeyId` | |
| `UAT_R2_SECRET_ACCESS_KEY` | `api.env` `Media__R2__SecretAccessKey` | |
| `UAT_SCREENING_API_KEY` | `api.env` `Media__Screening__ApiKey` | |
| `UAT_MEDIA_PIPELINE_SERVICE_TOKEN` | `api.env` `Media__Pipeline__ServiceToken` | must equal the processing Worker's `API_SERVICE_TOKEN` |
| `UAT_MEDIA_PIPELINE_UPLOAD_SIGNING_KEY` | `api.env` `Media__Pipeline__UploadSigningKey` | must equal the upload Worker's `UPLOAD_SIGNING_KEY` |
| `UAT_MODERATION_ESCALATION_EMAIL` | `api.env` `Moderation__EscalationEmail` | personal address, so kept secret |
| `UAT_MODERATION_ESCALATION_BACKUP_EMAIL` | `api.env` `Moderation__EscalationBackupEmail` | |
| `UAT_TWILIO_SMS_ACCOUNT_SID` | `api.env` `Sms__Twilio__AccountSid` | used when `UAT_SMS_SERVICE=twilio` |
| `UAT_TWILIO_SMS_AUTH_TOKEN` | `api.env` `Sms__Twilio__AuthToken` | |
| `UAT_TWILIO_EMAIL_KEY_SID` | `mail.env` `SWOOVR_MAIL_TWILIO_KEY_SID` | Twilio Email API (API and Keycloak, D360) |
| `UAT_TWILIO_EMAIL_KEY_SECRET` | `mail.env` `SWOOVR_MAIL_TWILIO_KEY_SECRET` | |
| `UAT_SENDGRID_API_KEY` | `mail.env` `SWOOVR_MAIL_SMTP_PASSWORD` | only with `UAT_ZAMMAD_MANAGE_MAIL=true` (or API email `smtp`) |
| `UAT_MAILERSEND_API_TOKEN` | `mail.env` `SWOOVR_MAIL_MAILERSEND_API_TOKEN` | only if a service is `mailersend` |
| `UAT_ZAMMAD_IMAP_PASSWORD` | `mail.env` `SWOOVR_ZAMMAD_IMAP_PASSWORD` | only with `UAT_ZAMMAD_MANAGE_MAIL=true` and an IMAP mailbox |

### 2. GitHub Environment variables (`uat`)

| Variable | Configuration key | Example / notes |
| --- | --- | --- |
| `UAT_EXPECTED_HOSTNAME` | — (checked before deploying) | `uat-01` |
| `UAT_INITIAL_ADMIN_EMAIL` | `automation.env` `SWOOVR_INITIAL_ADMIN_EMAIL` | the break-glass root admin's address, outside the Entra tenant (D364; password + authenticator, no Entra link) |
| `UAT_ENTRA_TENANT_ID` | realm templates + `automation.env` `SWOOVR_ENTRA_TENANT_ID` | Directory (tenant) ID |
| `UAT_ENTRA_CLIENT_ID` | realm templates + `automation.env` `SWOOVR_ENTRA_CLIENT_ID` | Application (client) ID |
| `UAT_KEYCLOAK_ENABLED_REALMS` | `automation.env` `SWOOVR_KEYCLOAK_ENABLED_REALMS` | `swoovr-members,swoovr-staff` (default); venues can never be listed |
| `UAT_DIDIT_WORKFLOW_ID` | `api.env` `Didit__WorkflowId` | |
| `UAT_R2_ENDPOINT`, `UAT_R2_BUCKET` | `api.env` `Media__R2__Endpoint`, `Media__R2__Bucket` | |
| `UAT_SCREENING_BASE_URL` | `api.env` `Media__Screening__BaseUrl` | |
| `UAT_TWILIO_SMS_FROM` | `api.env` `Sms__Twilio__From` | E.164 |
| `UAT_MAIL_FROM`, `UAT_MAIL_FROM_NAME`, `UAT_MAIL_HELPDESK_FROM_NAME` | `mail.env` `SWOOVR_MAIL_FROM`, `_FROM_NAME`, `_HELPDESK_FROM_NAME` | `hello@repasscloud.com`, `Swoovr`, `Swoovr Helpdesk` (D358) |
| `UAT_API_MAIL_SERVICE`, `UAT_KEYCLOAK_MAIL_SERVICE`, `UAT_SMS_SERVICE` | `mail.env` `SWOOVR_API_MAIL_SERVICE`, `SWOOVR_KEYCLOAK_MAIL_SERVICE`, `SWOOVR_SMS_SERVICE` | `twilio`, `twilio`, `mailpit` (D360) |
| `UAT_SMTP_HOST`, `UAT_SMTP_PORT`, `UAT_SMTP_USERNAME` | `mail.env` `SWOOVR_MAIL_SMTP_HOST`, `_PORT`, `_USERNAME` | `smtp.sendgrid.net`, `587`, `apikey` |
| `UAT_ZAMMAD_MANAGE_MAIL` | `mail.env` `SWOOVR_ZAMMAD_MANAGE_MAIL` | `false` (default): Zammad's mail (Microsoft 365) is configured in Zammad and never touched by deployments. `true`: provisioning manages it from the SendGrid/IMAP values below |
| `UAT_ZAMMAD_SUPPORT_ADDRESS`, `UAT_ZAMMAD_SUPPORT_NAME`, `UAT_ZAMMAD_IMAP_HOST`, `_PORT`, `_SECURITY`, `_USERNAME`, `_FOLDER`, `_KEEP_ON_SERVER` | `mail.env` `SWOOVR_ZAMMAD_*` | only used with `UAT_ZAMMAD_MANAGE_MAIL=true` |

An **unset or empty** secret or variable keeps the host's current value, so you only need to set what you want GitHub to own. To set a value to empty on the host, set it to `-`. The table in [`envtool.py`](../../deploy/vultr-uat/bin/envtool.py) (`PROVIDER_VALUES`) is the single mapping; `tests/deploy` checks that the workflow, this inventory's script and the env templates agree.

**Always required:** `UAT_SSH_HOST`, `UAT_SSH_USER`, `UAT_SSH_PRIVATE_KEY`, `UAT_SSH_HOST_KEY`, `UAT_EXPECTED_HOSTNAME`, `UAT_INITIAL_ADMIN_EMAIL`, `UAT_ENTRA_TENANT_ID`, `UAT_ENTRA_CLIENT_ID`. For a **refresh**, every provider value the host doesn't already hold must be set (the host still holds today's values, so none are strictly needed for the first refresh).

### 3. Repository variables

| Variable | Values |
| --- | --- |
| `DEPLOYMENT_MODE` | `upgrade` (default when unset) or `refresh` |

### 4. Generated and kept on the server (never in GitHub)

Created once by `envtool.py init` into `env/.generated` and the env files, reused on every upgrade **and** refresh: the PostgreSQL superuser and the five role passwords, `KC_BOOTSTRAP_ADMIN_PASSWORD`, the registration authority token, the member-admin, member/venue/staff web client secrets, the Zammad database, bootstrap and webhook secrets, the new `swoovr-deploy` Keycloak client secret, and the ten data keys (founder, phone, bans, contact history and lookup, evidence, identity store and HMAC, push and lookup). Zammad's four API tokens are produced by `zammad-provision` on the host. The origin certificate (`/etc/caddy/certs`) and PostgreSQL TLS material (`tls/`) also stay on the host.

### 5. Derived by the workflow

Tag, version, Build number, commit SHA, image digests, build timestamp, run ID, the release manifest, and the disabled realm templates (generated from the tagged commit and `UAT_ENTRA_TENANT_ID`/`UAT_ENTRA_CLIENT_ID`). The member, admin, venue, auth and helpdesk origins are fixed (D355).

## `gh` commands

```sh
gh auth login                                              # an account with admin rights on this repository; run from the checkout
scripts/uat/setup-github-uat.sh environments
scripts/uat/setup-github-uat.sh tag-protection
scripts/uat/setup-github-uat.sh host-key <VM IPv4>         # compare the fingerprint, then run the printed command
scripts/uat/setup-github-uat.sh values ~/swoovr-uat-backups/github-uat.env
scripts/uat/setup-github-uat.sh status
```

`values` reads `KEY=value` lines from the 0600 file (any names from the two tables above), pipes each value to `gh secret set … --env uat` or `gh variable set … --env uat` on stdin, and prints only names. Names not in the file are skipped, except the required ones, which it prompts for (hidden input, or a file path for the private key). The same by hand, one value at a time:

```sh
gh secret set UAT_SSH_PRIVATE_KEY --env uat < ~/.ssh/swoovr-uat-deploy
printf '%s' root | gh secret set UAT_SSH_USER --env uat
gh secret set UAT_SSH_HOST --env uat            # prompts, hidden
gh variable set UAT_EXPECTED_HOSTNAME --env uat --body uat-01
gh variable set UAT_INITIAL_ADMIN_EMAIL --env uat --body '<address>'
gh variable set DEPLOYMENT_MODE --body upgrade
```

## Releasing

```sh
git checkout main
git pull --ff-only
python3 scripts/release_version.py next          # e.g. 1.0.1 (next Build on the committed Major.Minor)
git tag -a v1.0.1 -m "Swoovr 1.0.1"              # or: python3 scripts/release_version.py allocate
git merge-base --is-ancestor v1.0.1 origin/main && echo "on main"
git push origin v1.0.1                            # starts Deploy UAT
gh run watch "$(gh run list --workflow deploy-uat.yml -L 1 --json databaseId -q '.[0].databaseId')"
```

`X.Y` must match `Directory.Build.props` (use `release_version.py bump minor|major` in a reviewed PR first). A tag is immutable: never move, delete-and-recreate or reuse it. The workflow reuses an image that already exists for the commit and never overwrites it, and the VM refuses a tag it has already deployed from a different commit. To redeploy the same release, re-run the workflow run (Actions → the run → Re-run all jobs). A re-run reads `DEPLOYMENT_MODE` again: check it first.
