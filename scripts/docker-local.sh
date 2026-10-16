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
try:
    fd = os.open(sys.argv[1], os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
except FileExistsError:
    print('Existing local environment retained.')
else:
    with os.fdopen(fd, 'w') as output:
        output.write('KC_BOOTSTRAP_ADMIN_PASSWORD=' + secrets.token_hex(32) + '\n')
    print('Created private .local/docker.env; no secret printed.')
PY
    ;;
  up|down|status|smoke)
    if [[ ! -f "$local_env" ]]; then
      echo 'Run ./scripts/docker-local.sh init first.' >&2
      exit 1
    fi
    case "$1" in
      up) docker compose --env-file "$local_env" up --build -d --wait --wait-timeout 180
          python3 scripts/docker_smoke.py ;;
      down) docker compose --env-file "$local_env" down ;;
      status) docker compose --env-file "$local_env" ps ;;
      smoke) python3 scripts/docker_smoke.py ;;
    esac
    ;;
  *) echo 'Usage: ./scripts/docker-local.sh {init|up|smoke|status|down}' >&2; exit 2 ;;
esac
