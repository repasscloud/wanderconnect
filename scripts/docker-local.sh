#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
local_env=.local/docker.env
case "${1:-}" in
  init)
    # Generate an actual local-only bootstrap secret, never a committed example password.
    mkdir -p .local
    chmod 700 .local
    python3 - "$local_env" <<'PY'
import os
import secrets
import sys
keys = ['KC_BOOTSTRAP_ADMIN_PASSWORD', 'SWOOVR_MEMBERS_WEB_SECRET', 'SWOOVR_VENUES_WEB_SECRET', 'SWOOVR_STAFF_WEB_SECRET']
fd = os.open(sys.argv[1], os.O_RDWR | os.O_CREAT | os.O_APPEND, 0o600)
with os.fdopen(fd, 'r+') as env:
    env.seek(0)
    present = {line.split('=', 1)[0] for line in env.read().splitlines() if '=' in line}
    missing = [key for key in keys if key not in present]
    # Existing values are retained; only missing local secrets are appended.
    for key in missing:
        env.write(key + '=' + secrets.token_hex(32) + '\n')
print(f'Private .local/docker.env ready; {len(missing)} value(s) added, no secret printed.')
PY
    ;;
  realms)
    # Applies flow settings a realm import cannot express; safe to repeat.
    node scripts/auth/realms.mjs
    ;;
  auth-init|auth)
    # The harness remains a host-side investigation tool, not an application service.
    if [[ "$1" == auth-init ]]; then node scripts/auth/provision.mjs
    else node scripts/auth/harness.mjs; fi
    ;;
  up|down|status|smoke)
    if [[ ! -f "$local_env" ]]; then
      echo 'Run ./scripts/docker-local.sh init first.' >&2
      exit 1
    fi
    case "$1" in
      up) docker compose --env-file "$local_env" up --build -d --wait --wait-timeout 180
          node scripts/auth/realms.mjs
          python3 scripts/docker_smoke.py ;;
      down) docker compose --env-file "$local_env" down ;;
      status) docker compose --env-file "$local_env" ps ;;
      smoke) python3 scripts/docker_smoke.py ;;
    esac
    ;;
  *) echo 'Usage: ./scripts/docker-local.sh {init|up|realms|smoke|status|down|auth-init|auth}' >&2; exit 2 ;;
esac
