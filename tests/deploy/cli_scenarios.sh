#!/usr/bin/env bash
# End-to-end checks of deploy/vultr-uat/bin/swoovr with a fake `docker` (tests/deploy/fake-docker). Runs as root in a
# throwaway Debian container started by tests/deploy/test_cli_container.py; it never touches a real host or service.
set -euo pipefail
repo=/repo
work="$(mktemp -d)"
export FAKE_LOG="$work/docker.log" FAKE_STATE="$work/state" PATH="$work/fakebin:$PATH"
install -d "$work/fakebin" && install -m 755 "$repo/tests/deploy/fake-docker" "$work/fakebin/docker"
pass=0
ok() { echo "PASS $*"; pass=$((pass + 1)); }
bad() { echo "FAIL $*"; exit 1; }
host="$(hostname)"
: > "$FAKE_LOG"
mkdir -p /var/lib/docker  # df target checked by bin/swoovr

# A host as left by the earlier bundle: origin certificate, TLS material, Cloudflare ranges.
install -d -m 700 /etc/caddy/certs /opt/swoovr/swoovr-uat/env /opt/swoovr/swoovr-uat/tls/postgres /opt/swoovr/swoovr-uat/tls/trust
openssl req -x509 -newkey rsa:2048 -nodes -days 400 -subj /CN=uat -keyout /etc/caddy/certs/uat-origin.key \
  -out /etc/caddy/certs/uat-origin.pem -addext 'subjectAltName=DNS:uat.swoovr.app,DNS:api-uat.swoovr.app,DNS:admin-uat.swoovr.app,DNS:auth-uat.swoovr.app,DNS:venues-uat.swoovr.app,DNS:helpdesk-uat.swoovr.app' 2>/dev/null
chmod 600 /etc/caddy/certs/uat-origin.key
for f in tls/postgres/server.crt tls/postgres/server.key tls/trust/pg-ca.crt tls/trust/cloudflare-origin-ca.pem tls/trust/origin-trust.pem; do
  echo test > "/opt/swoovr/swoovr-uat/$f"
done
( umask 077; echo 'CLOUDFLARE_RANGES=173.245.48.0/20' > /opt/swoovr/swoovr-uat/env/caddy.env )

python3 "$repo/scripts/uat/keycloak_realms.py" --admin-origin https://admin-uat.swoovr.app --venue-origin https://venues-uat.swoovr.app \
  --entra-tenant-id 11111111-1111-4111-8111-111111111111 --entra-client-id 22222222-2222-4222-8222-222222222222 \
  --output "$work/realms" > /dev/null

stage_release() { # stage_release TAG COMMIT -> release dir
  local tag="$1" commit="$2" m="$work/m-$1-$2" dir
  mkdir -p "$m"
  { echo "SWOOVR_TAG=$tag"; echo "SWOOVR_VERSION=${tag#v}"; echo "SWOOVR_COMMIT=$commit"
    for c in api web admin operator keycloak; do echo "SWOOVR_$(tr a-z A-Z <<< "$c")_IMAGE=ghcr.io/repasscloud/swoovr-$c@sha256:$(printf '%064d' 1)"; done
  } > "$m/release-manifest.env"
  echo '{"tag":"'"$tag"'"}' > "$m/release-manifest.json"
  ( cd "$repo" && deploy/vultr-uat/bin/make-bundle.sh "$m/release-manifest.env" "$work/realms" "$m/release-manifest.json" "$m/dist" ) > /dev/null
  dir="/opt/swoovr/releases/$tag-${commit:0:12}"; install -d "$dir"; tar -C "$dir" -xzf "$m"/dist/*.tar.gz
  echo "$dir/swoovr-uat"
}

provider="$work/provider.json"
python3 - "$repo/deploy/vultr-uat/bin/envtool.py" > "$provider" <<'PY'
import importlib.util, json, sys
spec = importlib.util.spec_from_file_location('envtool', sys.argv[1]); envtool = importlib.util.module_from_spec(spec); spec.loader.exec_module(envtool)
special = {'UAT_API_MAIL_SERVICE': 'twilio', 'UAT_KEYCLOAK_MAIL_SERVICE': 'twilio', 'UAT_SMS_SERVICE': 'mailpit',
           'UAT_SMTP_PORT': '587', 'UAT_ZAMMAD_IMAP_PORT': '993', 'UAT_ZAMMAD_IMAP_SECURITY': 'ssl',
           'UAT_ZAMMAD_IMAP_KEEP_ON_SERVER': 'true', 'UAT_ZAMMAD_IMAP_FOLDER': 'INBOX', 'UAT_ZAMMAD_IMAP_HOST': '-',
           'UAT_MAIL_FROM': 'hello@example.test', 'UAT_MAIL_FROM_NAME': 'Swoovr', 'UAT_MAIL_HELPDESK_FROM_NAME': 'Swoovr Helpdesk',
           'UAT_ZAMMAD_SUPPORT_NAME': 'Swoovr Support', 'UAT_INITIAL_ADMIN_EMAIL': 'first.admin@example.test',
           'UAT_ENTRA_TENANT_ID': '11111111-1111-4111-8111-111111111111', 'UAT_ENTRA_CLIENT_ID': '22222222-2222-4222-8222-222222222222',
           'UAT_KEYCLOAK_ENABLED_REALMS': 'swoovr-members,swoovr-staff', 'UAT_R2_ENDPOINT': 'https://r2.example.test',
           'UAT_SCREENING_BASE_URL': 'https://screening.example.test', 'UAT_ZAMMAD_SUPPORT_ADDRESS': '-', 'UAT_ZAMMAD_IMAP_USERNAME': '-',
           'UAT_ZAMMAD_IMAP_PASSWORD': '-', 'UAT_MAILERSEND_API_TOKEN': '-', 'UAT_ZAMMAD_MANAGE_MAIL': 'false'}
print(json.dumps({name: special.get(name, 'fixture-value-' + name.lower()) for name in envtool.PROVIDER_VALUES}))
PY
deploy() { # deploy RELEASE_DIR MODE [extra...] -> exit code
  local dir="$1" mode="$2"; shift 2
  set +e
  "$dir/bin/swoovr" deploy --release-dir "$dir" --mode "$mode" --provider-values - --run-id 1 --expect-hostname "$host" "$@" \
    < "$provider" > "$work/out.log" 2>&1
  local code=$?; set -e; return "$code"
}
count() { grep -c -- "$1" "$FAKE_LOG" || true; }
live=/opt/swoovr/swoovr-uat
c1=1111111111111111111111111111111111111111; c2=2222222222222222222222222222222222222222

r1="$(stage_release v1.0.3 "$c1")"
r2="$(stage_release v1.0.4 "$c2")"

# 1. Invalid mode and an unconfirmed refresh stop before anything is installed or deleted.
deploy "$r1" reset && bad 'invalid mode accepted'; grep -q 'mode must be upgrade or refresh' "$work/out.log" && ok 'invalid mode refused' || bad 'invalid mode refused'
deploy "$r1" refresh && bad 'refresh without confirmation accepted'
grep -q 'refresh needs --confirm-refresh swoovr-uat:v1.0.3' "$work/out.log" && ok 'refresh refused without the tag-bound confirmation' || bad 'refresh refused without the tag-bound confirmation'
deploy "$r1" refresh --confirm-refresh swoovr-uat:v9.9.9 && bad 'refresh with another tag confirmation accepted'; ok 'confirmation for another tag refused'
[[ "$(count 'compose.* down')" == 0 && ! -e "$live/compose.yaml" ]] && ok 'nothing installed or deleted by refused runs' || bad 'nothing installed or deleted by refused runs'

# 2. Upgrade on a host without data refuses (the first deployment must be a refresh).
deploy "$r1" upgrade && bad 'upgrade without data accepted'; grep -q 'first deployment must be a refresh' "$work/out.log" && ok 'upgrade without data refused' || bad 'upgrade without data refused'
[[ "$(count 'compose.* down')" == 0 ]] && ok 'refused upgrade deleted nothing' || bad 'refused upgrade deleted nothing'

# 3. First refresh: no data to back up, full bootstrap, admin pending, history recorded.
FAKE_VERSION=1.0.3 deploy "$r1" refresh --confirm-refresh swoovr-uat:v1.0.3 || { cat "$work/out.log"; bad 'first refresh failed'; }
grep -q 'SWOOVR_RESULT deployed=v1.0.3' "$work/out.log" && ok 'first refresh deployed' || bad 'first refresh deployed'
grep -q 'SWOOVR_RESULT initial-admin=pending-sign-in' "$work/out.log" && ok 'initial admin reported pending, not granted' || bad 'initial admin reported pending, not granted'
ls /opt/swoovr/backups/*-pre-refresh-v1.0.3/NOTHING-TO-BACK-UP > /dev/null && ok 'empty host recorded as nothing to back up' || bad 'empty host recorded as nothing to back up'
grep -q '^Ticketing__Zammad__RetentionToken=t3$' "$live/env/api.env" && ok 'Zammad tokens provisioned' || bad 'Zammad tokens provisioned'
grep -q "^SWOOVR_UAT_REALM_STAFF='{" "$live/env/realms.env" && ok 'realm templates loaded from the release' || bad 'realm templates loaded from the release'
[[ "$(stat -c %a "$live/env/api.env")" == 600 ]] && ok 'env files stay 0600' || bad 'env files stay 0600'
grep -q 'fixture-value-uat_didit_api_key' "$live/env/api.env" && ! grep -q 'fixture-value' "$work/out.log" && ok 'provider values applied, never printed' || bad 'provider values applied, never printed'
for job in db-bootstrap migrate places-import keycloak-bootstrap keycloak-reconcile; do
  grep -q "run --rm -T $job" "$FAKE_LOG" || bad "bootstrap skipped $job"
done; ok 'bootstrap ran db-bootstrap, migrate, places-import, keycloak-bootstrap, keycloak-reconcile'

# 4. Upgrade: backup first, then migrate, never down/volume removal; data keys unchanged.
keys_before="$(grep -E '^(Evidence__Keys__1|IdentityStore__HmacKey|Phone__CodeKey)=' "$live/env/api.env" | sha256sum)"
: > "$FAKE_LOG"
FAKE_VERSION=1.0.4 FAKE_ADMIN_STATE=ready deploy "$r2" upgrade || { cat "$work/out.log"; bad 'upgrade failed'; }
[[ "$(count 'compose.* down')" == 0 && "$(count 'volume rm')" == 0 && "$(count prune)" == 0 && "$(count r2-empty)" == 0 ]] && ok 'upgrade never removes containers, volumes, photos or prunes' || bad 'upgrade never removes containers, volumes, photos or prunes'
dump_line="$(grep -n 'pg_dump' "$FAKE_LOG" | head -1 | cut -d: -f1)"; migrate_line="$(grep -n 'run --rm -T migrate' "$FAKE_LOG" | head -1 | cut -d: -f1)"
(( dump_line < migrate_line )) && ok 'pre-upgrade backup taken before migrations' || bad 'pre-upgrade backup taken before migrations'
ls /opt/swoovr/backups/*-pre-upgrade-v1.0.4/swoovr.dump /opt/swoovr/backups/*-pre-upgrade-v1.0.4/SHA256SUMS > /dev/null && ok 'pre-upgrade backup has dumps and checksums' || bad 'pre-upgrade backup has dumps and checksums'
[[ "$keys_before" == "$(grep -E '^(Evidence__Keys__1|IdentityStore__HmacKey|Phone__CodeKey)=' "$live/env/api.env" | sha256sum)" ]] && ok 'data keys preserved by upgrade' || bad 'data keys preserved by upgrade'
grep -q 'SWOOVR_RESULT initial-admin=granted' "$work/out.log" && ok 'verified admin granted in stage 2' || bad 'verified admin granted in stage 2'
cmp -s "$live/release.env.previous" "$r1/release.env" && ok 'previous release manifest kept for rollback' || bad 'previous release manifest kept for rollback'
[[ "$(grep -c '"result": "success"' "$live/state/releases.jsonl")" == 2 ]] && grep -q '"failed_stage": "preflight"' "$live/state/releases.jsonl" && tail -1 "$live/state/releases.jsonl" | grep -q '"previous": {"commit": "'"$c1"'", "tag": "v1.0.3"}' "$live/state/releases.jsonl" && ok 'history records both releases and the previous one' || bad 'history records both releases and the previous one'

# 5. Tags are immutable: the same tag from another commit is refused.
r3="$(stage_release v1.0.3 3333333333333333333333333333333333333333)"
deploy "$r3" upgrade && bad 'reused tag accepted'; grep -q 'tags are immutable' "$work/out.log" && ok 'reused tag from another commit refused' || bad 'reused tag from another commit refused'

# 6. Refresh with data: a verified backup before anything is deleted; a failing backup deletes nothing.
: > "$FAKE_LOG"
r4="$(stage_release v1.0.5 4444444444444444444444444444444444444444)"
FAKE_FAIL_PGDUMP=1 deploy "$r4" refresh --confirm-refresh swoovr-uat:v1.0.5 && bad 'refresh continued after a failed backup'
[[ "$(count 'compose.* down')" == 0 ]] && ok 'failed pre-refresh backup: nothing deleted' || bad 'failed pre-refresh backup: nothing deleted'
tail -1 "$live/state/releases.jsonl" | grep -q '"failed_stage": "pre-refresh-backup"' && ok 'failure recorded with its stage' || bad 'failure recorded with its stage'
: > "$FAKE_LOG"
FAKE_VERSION=1.0.5 deploy "$r4" refresh --confirm-refresh swoovr-uat:v1.0.5 || { cat "$work/out.log"; bad 'refresh failed'; }
dump_line="$(grep -n 'pg_dump' "$FAKE_LOG" | head -1 | cut -d: -f1)"; down_line="$(grep -n 'compose.* down' "$FAKE_LOG" | head -1 | cut -d: -f1)"
(( dump_line < down_line )) && ok 'pre-refresh backup taken before down --volumes' || bad 'pre-refresh backup taken before down --volumes'
r2_line="$(grep -n 'run --rm -T r2-empty --bucket fixture-value-uat_r2_bucket' "$FAKE_LOG" | head -1 | cut -d: -f1)"
[[ -n "$r2_line" ]] && (( r2_line > down_line )) && ok 'refresh empties the configured R2 bucket after the reset' || bad 'refresh empties the configured R2 bucket after the reset'
grep 'compose.* down' "$FAKE_LOG" | grep -q -- '--project-name swoovr-uat .*down --volumes --remove-orphans' && ok 'refresh removes only the swoovr-uat project' || bad 'refresh removes only the swoovr-uat project'
[[ -s /etc/caddy/certs/uat-origin.key && -f "$live/tls/postgres/server.key" ]] && ok 'certificates and TLS material survive refresh' || bad 'certificates and TLS material survive refresh'
[[ "$keys_before" == "$(grep -E '^(Evidence__Keys__1|IdentityStore__HmacKey|Phone__CodeKey)=' "$live/env/api.env" | sha256sum)" ]] && ok 'server-held secrets survive refresh' || bad 'server-held secrets survive refresh'

# 7. A failing migration stops the upgrade and is recorded; data is left in place.
: > "$FAKE_LOG"
r5="$(stage_release v1.0.6 5555555555555555555555555555555555555555)"
FAKE_FAIL_JOB=migrate deploy "$r5" upgrade && bad 'upgrade continued after a failed migration'
tail -1 "$live/state/releases.jsonl" | grep -q '"failed_stage": "migrate"' && ok 'failed migration recorded at stage migrate' || bad 'failed migration recorded at stage migrate'
[[ "$(count 'compose.* down')" == 0 ]] && ok 'failed migration deleted nothing' || bad 'failed migration deleted nothing'

echo "ALL $pass CLI SCENARIO CHECKS PASSED"
