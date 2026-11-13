#!/usr/bin/env bash
# Configure GitHub for the automated UAT deployment (D362) with the GitHub CLI. Run it yourself, on your Mac; nothing
# here runs in CI. Values never appear on the command line, in output or in shell history.
#
#   scripts/uat/setup-github-uat.sh environments          create/update the `uat` environment (deployments only from
#                                                         tags v*) and set DEPLOYMENT_MODE=upgrade
#   scripts/uat/setup-github-uat.sh tag-protection        ruleset for tags v*: nobody but admins may move or delete them
#   scripts/uat/setup-github-uat.sh template [FILE]       write a 0600 FILE (default ~/swoovr-uat-backups/github-uat.env)
#                                                         listing every name, what it is and whether it is required
#   scripts/uat/setup-github-uat.sh values [FILE]         set every secret and variable: from FILE (KEY=value lines,
#                                                         must be mode 0600; blank values are skipped) or, for required
#                                                         names missing or blank in FILE, hidden prompts.
#                                                         UAT_SSH_PRIVATE_KEY is the PATH of the key file, not the key.
#   scripts/uat/setup-github-uat.sh host-key HOST         print the server's ed25519 host key and fingerprint; compare the
#                                                         fingerprint with the one you collected before saving it
#   scripts/uat/setup-github-uat.sh status                list which names are set (names only)
#
# Repository: $SWOOVR_GH_REPO, default the checkout's GitHub repository (gh repo view). Requires `gh auth login` with admin rights.
set -euo pipefail
REPO="${SWOOVR_GH_REPO:-$(gh repo view --json nameWithOwner -q .nameWithOwner)}"

# Environment secrets (`uat`). Keep in step with .github/workflows/deploy-uat.yml (tests/deploy checks this).
SECRETS=(
  UAT_SSH_HOST UAT_SSH_USER UAT_SSH_PRIVATE_KEY UAT_SSH_HOST_KEY
  UAT_ENTRA_CLIENT_SECRET UAT_DIDIT_API_KEY UAT_DIDIT_WEBHOOK_SECRET UAT_R2_ACCESS_KEY_ID UAT_R2_SECRET_ACCESS_KEY
  UAT_SCREENING_API_KEY UAT_MEDIA_PIPELINE_SERVICE_TOKEN UAT_MEDIA_PIPELINE_UPLOAD_SIGNING_KEY
  UAT_MODERATION_ESCALATION_EMAIL UAT_MODERATION_ESCALATION_BACKUP_EMAIL UAT_TWILIO_SMS_ACCOUNT_SID
  UAT_TWILIO_SMS_AUTH_TOKEN UAT_TWILIO_EMAIL_KEY_SID UAT_TWILIO_EMAIL_KEY_SECRET UAT_SENDGRID_API_KEY
  UAT_MAILERSEND_API_TOKEN UAT_ZAMMAD_IMAP_PASSWORD
)
# Environment variables (`uat`), not secret.
VARIABLES=(
  UAT_EXPECTED_HOSTNAME UAT_INITIAL_ADMIN_EMAIL UAT_ENTRA_TENANT_ID UAT_ENTRA_CLIENT_ID UAT_KEYCLOAK_ENABLED_REALMS
  UAT_DIDIT_WORKFLOW_ID UAT_R2_ENDPOINT UAT_R2_BUCKET UAT_SCREENING_BASE_URL UAT_TWILIO_SMS_FROM
  UAT_MAIL_FROM UAT_MAIL_FROM_NAME UAT_MAIL_HELPDESK_FROM_NAME UAT_API_MAIL_SERVICE UAT_KEYCLOAK_MAIL_SERVICE
  UAT_SMS_SERVICE UAT_SMTP_HOST UAT_SMTP_PORT UAT_SMTP_USERNAME UAT_ZAMMAD_SUPPORT_ADDRESS UAT_ZAMMAD_SUPPORT_NAME
  UAT_ZAMMAD_IMAP_HOST UAT_ZAMMAD_IMAP_PORT UAT_ZAMMAD_IMAP_SECURITY UAT_ZAMMAD_IMAP_USERNAME UAT_ZAMMAD_IMAP_FOLDER
  UAT_ZAMMAD_IMAP_KEEP_ON_SERVER UAT_ZAMMAD_MANAGE_MAIL
)
# Required for every deployment; the rest may stay unset (the host keeps its current value).
REQUIRED=(UAT_SSH_HOST UAT_SSH_USER UAT_SSH_PRIVATE_KEY UAT_SSH_HOST_KEY UAT_EXPECTED_HOSTNAME UAT_INITIAL_ADMIN_EMAIL
  UAT_ENTRA_TENANT_ID UAT_ENTRA_CLIENT_ID)
# Repository variables.
# Repository variable DEPLOYMENT_MODE (upgrade | refresh) is set by `environments` and changed by hand when needed.

# describe NAME: one line for the template. Keep in step with docs/delivery/UAT-GITHUB-DEPLOYMENT.md (Inventory).
describe() {
  case "$1" in
    UAT_SSH_HOST) echo 'VM public IPv4 (or host name).' ;;
    UAT_SSH_USER) echo 'Must be root.' ;;
    UAT_SSH_PRIVATE_KEY) echo 'PATH to the dedicated deploy private key (~ is fine), e.g. ~/.ssh/swoovr-uat-deploy.' ;;
    UAT_SSH_HOST_KEY) echo "One line 'ssh-ed25519 AAAA...' from 'host-key HOST', only after its fingerprint matched." ;;
    UAT_EXPECTED_HOSTNAME) echo 'Server hostname, checked before deploying: uat-01.' ;;
    UAT_INITIAL_ADMIN_EMAIL) echo 'Break-glass root admin address, outside the Entra tenant (D364).' ;;
    UAT_ENTRA_TENANT_ID) echo 'Entra Directory (tenant) ID.' ;;
    UAT_ENTRA_CLIENT_ID) echo 'Entra Application (client) ID.' ;;
    UAT_ENTRA_CLIENT_SECRET) echo 'Entra client secret for the Keycloak entra broker.' ;;
    UAT_DIDIT_API_KEY) echo 'Didit API key.' ;;
    UAT_DIDIT_WEBHOOK_SECRET) echo 'Didit webhook secret (same as in the Didit console).' ;;
    UAT_DIDIT_WORKFLOW_ID) echo 'Didit workflow ID.' ;;
    UAT_R2_ACCESS_KEY_ID) echo 'Cloudflare R2 access key ID.' ;;
    UAT_R2_SECRET_ACCESS_KEY) echo 'Cloudflare R2 secret access key.' ;;
    UAT_R2_ENDPOINT) echo 'R2 S3 endpoint URL.' ;;
    UAT_R2_BUCKET) echo 'R2 bucket name.' ;;
    UAT_SCREENING_API_KEY) echo 'Media screening API key.' ;;
    UAT_SCREENING_BASE_URL) echo 'Media screening base URL.' ;;
    UAT_MEDIA_PIPELINE_SERVICE_TOKEN) echo "Must equal the processing Worker's API_SERVICE_TOKEN." ;;
    UAT_MEDIA_PIPELINE_UPLOAD_SIGNING_KEY) echo "Must equal the upload Worker's UPLOAD_SIGNING_KEY." ;;
    UAT_MODERATION_ESCALATION_EMAIL) echo 'Moderation escalation address.' ;;
    UAT_MODERATION_ESCALATION_BACKUP_EMAIL) echo 'Moderation escalation backup address.' ;;
    UAT_TWILIO_SMS_ACCOUNT_SID) echo 'Twilio SMS account SID (used when UAT_SMS_SERVICE=twilio).' ;;
    UAT_TWILIO_SMS_AUTH_TOKEN) echo 'Twilio SMS auth token.' ;;
    UAT_TWILIO_SMS_FROM) echo 'Twilio SMS sender number, E.164.' ;;
    UAT_TWILIO_EMAIL_KEY_SID) echo 'Twilio Email API key SID (API and Keycloak mail, D360).' ;;
    UAT_TWILIO_EMAIL_KEY_SECRET) echo 'Twilio Email API key secret.' ;;
    UAT_SENDGRID_API_KEY) echo 'SendGrid SMTP password; only with UAT_ZAMMAD_MANAGE_MAIL=true or API mail smtp.' ;;
    UAT_MAILERSEND_API_TOKEN) echo 'MailerSend token; only if a mail service is mailersend.' ;;
    UAT_MAIL_FROM) echo 'Sender address, e.g. hello@repasscloud.com (D358).' ;;
    UAT_MAIL_FROM_NAME) echo 'Sender name, e.g. Swoovr.' ;;
    UAT_MAIL_HELPDESK_FROM_NAME) echo 'Helpdesk sender name, e.g. Swoovr Helpdesk.' ;;
    UAT_API_MAIL_SERVICE) echo 'API mail service, e.g. twilio (D360).' ;;
    UAT_KEYCLOAK_MAIL_SERVICE) echo 'Keycloak mail service, e.g. twilio (D360).' ;;
    UAT_SMS_SERVICE) echo 'SMS service, e.g. mailpit or twilio (D360).' ;;
    UAT_SMTP_HOST) echo 'SMTP host, e.g. smtp.sendgrid.net.' ;;
    UAT_SMTP_PORT) echo 'SMTP port, e.g. 587.' ;;
    UAT_SMTP_USERNAME) echo 'SMTP username, e.g. apikey.' ;;
    UAT_KEYCLOAK_ENABLED_REALMS) echo 'Default swoovr-members,swoovr-staff; venues can never be listed.' ;;
    UAT_ZAMMAD_MANAGE_MAIL) echo 'false (default): Zammad mail (Microsoft 365) is set up in Zammad, untouched by deploys. true: managed from the Zammad values below.' ;;
    UAT_ZAMMAD_IMAP_PASSWORD) echo 'Zammad IMAP password; only with UAT_ZAMMAD_MANAGE_MAIL=true.' ;;
    UAT_ZAMMAD_*) echo 'Only used with UAT_ZAMMAD_MANAGE_MAIL=true.' ;;
    *) echo "No description for $1" >&2; return 1 ;;
  esac
}

template() {
  local file="${1:-$HOME/swoovr-uat-backups/github-uat.env}" name kind default
  [[ -e "$file" ]] && { echo "$file already exists; not overwriting." >&2; exit 1; }
  mkdir -p "$(dirname "$file")"
  ( umask 077
    { echo '# Swoovr UAT GitHub values (D362). Fill in, then: scripts/uat/setup-github-uat.sh values FILE'
      echo '# One KEY=value per line, no quotes, no comment after the value. A blank value is skipped: the server'
      echo "# keeps its current value. Use '-' to set it empty on the server. REQUIRED blanks are prompted for (hidden)."
      echo '# Delete this file once `status` shows nothing MISSING.'
      for name in "${SECRETS[@]}" "${VARIABLES[@]}"; do
        is_in "$name" "${SECRETS[@]}" && kind=secret || kind=variable
        is_in "$name" "${REQUIRED[@]}" && kind="REQUIRED $kind" || kind="optional $kind"
        case "$name" in
          UAT_SSH_USER) default=root ;; UAT_EXPECTED_HOSTNAME) default=uat-01 ;;
          UAT_SSH_PRIVATE_KEY) default='~/.ssh/swoovr-uat-deploy' ;; *) default= ;;
        esac
        echo; echo "# [$kind] $(describe "$name")"; echo "$name=$default"
      done
    } > "$file" )
  chmod 600 "$file"
  echo "Wrote $file (mode 0600). Fill it in, then run: $0 values $file"
}

is_in() { local item="$1"; shift; local x; for x in "$@"; do [[ "$x" == "$item" ]] && return 0; done; return 1; }

environments() {
  # Only workflow runs for a v* tag may use the environment (and so its secrets).
  gh api -X PUT "repos/$REPO/environments/uat" --input - > /dev/null <<'JSON'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
JSON
  if ! gh api "repos/$REPO/environments/uat/deployment-branch-policies" --jq '.branch_policies[] | select(.type == "tag") | .name' | grep -qxF 'v*'; then
    gh api -X POST "repos/$REPO/environments/uat/deployment-branch-policies" -f name='v*' -f type=tag > /dev/null
  fi
  gh variable set DEPLOYMENT_MODE --repo "$REPO" --body upgrade
  echo "Environment uat configured (only v* tags may deploy). DEPLOYMENT_MODE=upgrade."
}

tag_protection() {
  # Release tags are immutable: block moving (update) and deleting them for everyone except repository admins.
  if gh api "repos/$REPO/rulesets" --jq '.[].name' | grep -qxF 'Release tags'; then
    echo 'Ruleset "Release tags" already exists.'; return 0
  fi
  gh api -X POST "repos/$REPO/rulesets" --input - > /dev/null <<'JSON'
{"name": "Release tags", "target": "tag", "enforcement": "active",
 "conditions": {"ref_name": {"include": ["refs/tags/v*"], "exclude": []}},
 "rules": [{"type": "update"}, {"type": "deletion"}],
 "bypass_actors": [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}]}
JSON
  echo 'Ruleset "Release tags" created: v* tags can be created, but not moved or deleted (admins can bypass).'
}

# in_file FILE NAME: succeeds and prints the value when FILE has a NAME= line (bash 3.2 compatible, macOS default).
in_file() { [[ -n "$1" ]] && grep -q "^$2=" "$1" && { grep "^$2=" "$1" | head -1 | cut -d= -f2-; }; }

values() {
  local file="${1:-}" name value line keyfile
  if [[ -n "$file" ]]; then
    [[ "$(stat -f %Lp "$file" 2>/dev/null || stat -c %a "$file")" == 600 ]] || { echo "$file must be mode 0600" >&2; exit 1; }
    while IFS= read -r line || [[ -n "$line" ]]; do
      [[ -z "$line" || "$line" == \#* || "$line" != *=* ]] && continue
      is_in "${line%%=*}" "${SECRETS[@]}" "${VARIABLES[@]}" || { echo "Unknown name in $file: ${line%%=*}" >&2; exit 1; }
    done < "$file"
  fi
  for name in "${SECRETS[@]}" "${VARIABLES[@]}"; do
    value="$(in_file "$file" "$name" || true)"
    if [[ -z "$value" ]] && is_in "$name" "${REQUIRED[@]}"; then
      if [[ "$name" == UAT_SSH_PRIVATE_KEY ]]; then read -r -p "Path to the deploy private key file: " value
      else read -r -s -p "$name: " value; echo; fi
    fi
    [[ -n "$value" ]] || { is_in "$name" "${REQUIRED[@]}" && { echo "$name is required" >&2; exit 1; }; continue; }
    if [[ "$name" == UAT_SSH_PRIVATE_KEY ]]; then
      keyfile="${value/#\~/$HOME}"
      [[ -f "$keyfile" ]] || { echo "UAT_SSH_PRIVATE_KEY: no key file at $keyfile" >&2; exit 1; }
      grep -q -- '-----BEGIN OPENSSH PRIVATE KEY-----' "$keyfile" || { echo "$keyfile is not an OpenSSH private key" >&2; exit 1; }
      value="$(cat "$keyfile")"
    fi
    if is_in "$name" "${SECRETS[@]}"; then printf '%s' "$value" | gh secret set "$name" --repo "$REPO" --env uat
    else printf '%s' "$value" | gh variable set "$name" --repo "$REPO" --env uat; fi
    echo "set $name"
  done
}

host_key() {
  local host="${1:?host}" line
  line="$(ssh-keyscan -T 10 -t ed25519 "$host" 2>/dev/null | awk '$2 == "ssh-ed25519" {print $2 " " $3; exit}')"
  [[ -n "$line" ]] || { echo "No ed25519 host key from $host" >&2; exit 1; }
  echo "Fingerprint: $(ssh-keygen -lf <(echo "$host $line") | awk '{print $2}')"
  echo "Compare it with ssh/host-key-fingerprints.txt from your server collection (ED25519 line). Only if equal:"
  echo "  printf '%s' '$line' | gh secret set UAT_SSH_HOST_KEY --repo $REPO --env uat"
}

status() {
  echo "Environment uat secrets set:"; gh secret list --repo "$REPO" --env uat --json name --jq '.[].name' | sed 's/^/  /'
  echo "Environment uat variables set:"; gh variable list --repo "$REPO" --env uat --json name --jq '.[].name' | sed 's/^/  /'
  echo "Repository variables:"; gh variable list --repo "$REPO" --json name,value --jq '.[] | select(.name == "DEPLOYMENT_MODE") | "  \(.name)=\(.value)"'
  local name missing=0
  for name in "${REQUIRED[@]}"; do
    if ! gh secret list --repo "$REPO" --env uat --json name --jq '.[].name' | grep -qx "$name" \
        && ! gh variable list --repo "$REPO" --env uat --json name --jq '.[].name' | grep -qx "$name"; then
      echo "MISSING $name"; missing=1
    fi
  done
  return "$missing"
}

case "${1:-}" in
  environments) environments ;;
  tag-protection) tag_protection ;;
  template) shift; template "$@" ;;
  values) shift; values "$@" ;;
  host-key) shift; host_key "$@" ;;
  status) status ;;
  *) sed -n '2,18p' "$0"; exit 64 ;;
esac
