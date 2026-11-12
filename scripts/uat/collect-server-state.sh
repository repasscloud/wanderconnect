#!/usr/bin/env bash
# Read-only inventory of the Swoovr UAT Vultr VM, for the deployment redesign (docs/delivery/UAT-DEPLOYMENT-PREREQUISITES.md).
#
# Run as root on the VM:   bash collect-server-state.sh [options]
#
# It never stops, starts or recreates containers, never changes firewall, SSH, certificates, volumes or application
# data, and never runs migrations, jobs or `bin/swoovr`. Database queries run inside the existing PostgreSQL
# containers over their local socket in a READ ONLY transaction and return names and counts only.
#
# Output (all 0600, under /root):
#   swoovr-uat-collect-<UTC timestamp>-public.tar.gz     no secret values: versions, inventories, redacted inspects,
#                                                       non-secret bundle files, firewall/SSH/systemd configuration
#   swoovr-uat-collect-<UTC timestamp>-sensitive.tar.gz  env/*.env (current values), SHA-256 of every secret file,
#                                                       Keycloak/Zammad admin usernames, SSH key fingerprints;
#                                                       optional TLS/origin key copies and logs (flags below)
#   swoovr-uat-collect-<UTC timestamp>.SHA256SUMS
#
# Options:
#   --bundle DIR          bundle directory (default /opt/swoovr/swoovr-uat)
#   --project NAME        Compose project (default swoovr-uat)
#   --include-tls         also copy tls/ (PostgreSQL CA + server key). Default: hashes only, the local backup has them.
#   --include-origin-key  also copy /etc/caddy/certs (Cloudflare Origin key). Default: hashes only.
#   --with-logs           also copy the last 300 log lines of each container into the sensitive archive.
#   --no-db               skip the read-only database metadata queries.
#   --no-du               skip volume size measurement (du can take minutes on Elasticsearch).
set -uo pipefail

BUNDLE=/opt/swoovr/swoovr-uat
PROJECT=swoovr-uat
INCLUDE_TLS=0 INCLUDE_ORIGIN_KEY=0 WITH_LOGS=0 WITH_DB=1 WITH_DU=1
while (($#)); do
  case "$1" in
    --bundle) BUNDLE="${2:?}"; shift ;;
    --project) PROJECT="${2:?}"; shift ;;
    --include-tls) INCLUDE_TLS=1 ;;
    --include-origin-key) INCLUDE_ORIGIN_KEY=1 ;;
    --with-logs) WITH_LOGS=1 ;;
    --no-db) WITH_DB=0 ;;
    --no-du) WITH_DU=0 ;;
    -h|--help) sed -n '2,27p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 64 ;;
  esac
  shift
done

[[ $EUID -eq 0 ]] || { echo 'Run as root.' >&2; exit 1; }
[[ -r /etc/os-release ]] && . /etc/os-release
[[ "${ID:-}" == debian ]] || echo "WARNING: expected Debian, found ${ID:-unknown}; continuing read-only." >&2

umask 077
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
NAME="swoovr-uat-collect-$STAMP"
WORK="$(mktemp -d "/root/.${NAME}.XXXXXX")"
PUB="$WORK/$NAME-public"
SEC="$WORK/$NAME-sensitive"
mkdir -p "$PUB" "$SEC"
chmod 0700 "$WORK" "$PUB" "$SEC"
trap 'rm -rf "$WORK"' EXIT
ERRORS="$PUB/errors.txt"; : > "$ERRORS"

# run <file> <command...>: capture stdout+stderr of a read-only command into a file; never abort the collection.
run() {
  local out="$1"; shift
  mkdir -p "$(dirname "$out")"
  # Shell functions (q, certinfo) cannot go through timeout(1); their own commands are bounded.
  if declare -F "$1" > /dev/null; then { printf '# %s\n' "$*"; "$@" 2>&1; } > "$out"
  else { printf '# %s\n' "$*"; timeout 300 "$@" 2>&1; } > "$out"; fi
  local rc=$?
  ((rc == 0)) || printf '%s (exit %s): %s\n' "${out#"$WORK"/}" "$rc" "$*" >> "$ERRORS"
}
have() { command -v "$1" > /dev/null 2>&1; }
say() { printf '== %s\n' "$*" >&2; }
# Container ID of a Compose service in this project (running or stopped), empty if none.
cid() { docker ps -aq --filter "label=com.docker.compose.project=$PROJECT" --filter "label=com.docker.compose.service=$1" | head -1; }

# ------------------------------------------------------------------ operating system and hardware
say 'operating system'
run "$PUB/os/os-release.txt" cat /etc/os-release
run "$PUB/os/debian_version.txt" cat /etc/debian_version
run "$PUB/os/uname.txt" uname -a
run "$PUB/os/arch.txt" dpkg --print-architecture
run "$PUB/os/hostnamectl.txt" hostnamectl
run "$PUB/os/timedatectl.txt" timedatectl
run "$PUB/os/uptime.txt" uptime
run "$PUB/os/cpu.txt" lscpu
run "$PUB/os/memory.txt" free -h
run "$PUB/os/disks.txt" lsblk -o NAME,SIZE,TYPE,FSTYPE,MOUNTPOINT
run "$PUB/os/df.txt" df -hT
run "$PUB/os/sysctl-swoovr.txt" cat /etc/sysctl.d/90-swoovr.conf
run "$PUB/os/sysctl-live.txt" sysctl vm.max_map_count vm.overcommit_memory net.ipv4.ip_forward
run "$PUB/os/packages-relevant.txt" bash -c "dpkg-query -W -f='\${Package} \${Version}\n' | grep -E '^(docker|containerd|nftables|iptables|openssh|tailscale|unattended|chrony|python3 |jq |openssl )'"
run "$PUB/os/apt-sources.txt" bash -c 'cat /etc/apt/sources.list /etc/apt/sources.list.d/* 2>/dev/null'
run "$PUB/os/unattended-upgrades.txt" bash -c 'cat /etc/apt/apt.conf.d/20auto-upgrades /etc/apt/apt.conf.d/50unattended-upgrades 2>/dev/null'
run "$PUB/os/hosts.txt" cat /etc/hosts
run "$PUB/os/resolv.conf.txt" cat /etc/resolv.conf
run "$PUB/os/crontab-root.txt" crontab -l
run "$PUB/os/cron.d.txt" ls -la /etc/cron.d /etc/cron.daily

# ------------------------------------------------------------------ networking and firewall (read only)
say 'networking and firewall'
run "$PUB/network/ip-addr.txt" ip -br addr
run "$PUB/network/ip-link.txt" ip -br link
run "$PUB/network/ip-route.txt" ip route
run "$PUB/network/ip6-route.txt" ip -6 route
run "$PUB/network/listening.txt" ss -lntup
run "$PUB/network/nft-ruleset.txt" nft list ruleset
run "$PUB/network/iptables-save.txt" iptables-save
run "$PUB/network/ip6tables-save.txt" ip6tables-save
run "$PUB/network/etc-swoovr-listing.txt" ls -la /etc/swoovr
for f in /etc/swoovr/host.nft /etc/swoovr/cloudflare-ranges /etc/swoovr/edge-interface; do
  [[ -f "$f" ]] && run "$PUB/network/$(basename "$f").txt" cat "$f"
done
if have tailscale; then
  run "$PUB/network/tailscale-version.txt" tailscale version
  run "$PUB/network/tailscale-status.txt" tailscale status
else
  echo 'tailscale not installed' > "$PUB/network/tailscale-status.txt"
fi

# ------------------------------------------------------------------ SSH (effective configuration; keys as fingerprints only)
say 'ssh'
run "$PUB/ssh/sshd-effective.txt" sshd -T
run "$PUB/ssh/sshd_config.d-listing.txt" ls -la /etc/ssh/sshd_config.d
run "$PUB/ssh/sshd_config.txt" bash -c "grep -vE '^\s*(#|$)' /etc/ssh/sshd_config /etc/ssh/sshd_config.d/*.conf 2>/dev/null"
run "$PUB/ssh/host-key-fingerprints.txt" bash -c 'for k in /etc/ssh/ssh_host_*_key.pub; do ssh-keygen -lf "$k"; done'
for home in /root /home/*; do
  keys="$home/.ssh/authorized_keys"
  [[ -f "$keys" ]] || continue
  # Fingerprint, type and comment only: no key material.
  run "$SEC/ssh/authorized_keys-fingerprints-$(basename "$home").txt" ssh-keygen -lf "$keys"
  run "$PUB/ssh/authorized_keys-stat-$(basename "$home").txt" stat -c '%A %U:%G %s %y %n' "$keys"
done

# ------------------------------------------------------------------ systemd
say 'systemd'
run "$PUB/systemd/failed.txt" systemctl --failed --no-pager
run "$PUB/systemd/units-running.txt" systemctl list-units --type=service --state=running --no-pager
run "$PUB/systemd/unit-files-enabled.txt" systemctl list-unit-files --state=enabled --no-pager
for unit in swoovr-host-firewall swoovr-edge-firewall docker containerd ssh tailscaled unattended-upgrades; do
  run "$PUB/systemd/$unit.status.txt" systemctl status "$unit" --no-pager
  run "$PUB/systemd/$unit.cat.txt" systemctl cat "$unit"
done

# ------------------------------------------------------------------ Docker engine
say 'docker engine'
run "$PUB/docker/version.txt" docker version
run "$PUB/docker/compose-version.txt" docker compose version
run "$PUB/docker/info.txt" docker info
run "$PUB/docker/daemon.json.txt" cat /etc/docker/daemon.json
run "$PUB/docker/system-df.txt" docker system df -v
run "$PUB/docker/compose-ls.txt" docker compose ls -a
# Registry logins: registry names and credential helper only, never the auth values.
if [[ -f /root/.docker/config.json ]]; then
  run "$PUB/docker/registry-logins.txt" python3 -I -c 'import json;c=json.load(open("/root/.docker/config.json"));print("auths:",sorted(c.get("auths",{})));print("credsStore:",c.get("credsStore"));print("credHelpers:",sorted(c.get("credHelpers",{})))'
else
  echo 'no /root/.docker/config.json' > "$PUB/docker/registry-logins.txt"
fi

# ------------------------------------------------------------------ containers, images, networks, volumes
say 'containers'
run "$PUB/docker/ps-all.txt" docker ps -a --no-trunc --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.State}}\t{{.CreatedAt}}\t{{.Ports}}'
run "$PUB/docker/images-digests.txt" docker images --digests --no-trunc
# Redacted inspect: environment variable NAMES only (values removed), everything else kept.
REDACT='[.[] | .Config.Env |= ((. // []) | map(split("=")[0]))]'
ids="$(docker ps -aq)"
if [[ -n "$ids" ]]; then
  # shellcheck disable=SC2086
  if have jq; then docker inspect $ids 2>>"$ERRORS" | jq "$REDACT" > "$PUB/docker/containers-inspect-redacted.json"
  else docker inspect $ids 2>>"$ERRORS" | python3 -I -c 'import json,sys;d=json.load(sys.stdin);[c["Config"].__setitem__("Env",[e.split("=",1)[0] for e in (c["Config"].get("Env") or [])]) for c in d];json.dump(d,sys.stdout,indent=2)' > "$PUB/docker/containers-inspect-redacted.json"; fi
  # One-line summary per container: service, image ref, image ID, restart policy, health, mounts, networks.
  # shellcheck disable=SC2086
  run "$PUB/docker/containers-summary.txt" docker inspect -f '{{.Name}} | service={{index .Config.Labels "com.docker.compose.service"}} | image={{.Config.Image}} | imageId={{.Image}} | restart={{.HostConfig.RestartPolicy.Name}} | state={{.State.Status}} | health={{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} | started={{.State.StartedAt}} | mounts={{range .Mounts}}{{.Type}}:{{if .Name}}{{.Name}}{{else}}{{.Source}}{{end}}->{{.Destination}}({{if .RW}}rw{{else}}ro{{end}}) {{end}}| nets={{range $k,$v := .NetworkSettings.Networks}}{{$k}}={{$v.IPAddress}} {{end}}' $ids
fi
# Image IDs -> repo digests, to confirm the running digests equal release.env.
run "$PUB/docker/image-repodigests.txt" bash -c 'docker images -q --no-trunc | sort -u | xargs -r docker image inspect -f "{{.Id}} {{json .RepoDigests}} {{json .RepoTags}} {{.Architecture}} {{.Created}}"'
run "$PUB/docker/networks.txt" docker network ls --no-trunc
run "$PUB/docker/networks-inspect.json" bash -c 'docker network ls -q | xargs -r docker network inspect'
run "$PUB/docker/volumes.txt" docker volume ls
run "$PUB/docker/volumes-inspect.json" bash -c 'docker volume ls -q | xargs -r docker volume inspect'
if ((WITH_DU)); then
  run "$PUB/docker/volume-sizes.txt" bash -c 'for v in $(docker volume ls -q); do m=$(docker volume inspect -f "{{.Mountpoint}}" "$v"); printf "%s\t%s\n" "$(du -sh "$m" 2>/dev/null | cut -f1)" "$v"; done'
fi
# Zammad's local backup volume: file names, sizes and dates only.
run "$PUB/docker/zammad-backup-volume-listing.txt" bash -c "m=\$(docker volume inspect -f '{{.Mountpoint}}' ${PROJECT}_zammad-backup 2>/dev/null) && ls -la \"\$m\""

# ------------------------------------------------------------------ the deployment bundle
say 'bundle'
if [[ -d "$BUNDLE" ]]; then
  run "$PUB/bundle/tree-stat.txt" find "$BUNDLE" "$(dirname "$BUNDLE")" -maxdepth 4 -printf '%M %u:%g %s %TY-%Tm-%TdT%TH:%TM %p\n'
  # Non-secret files, copied with permissions.
  ( cd "$BUNDLE" && for f in compose.yaml Caddyfile release.env release.env.previous release.env.failed thirdparty.env README.md \
      VULTR-VM.md .gitignore next.env bin zammad postgres env/*.env.example; do
      [[ -e "$f" ]] && cp -a --parents "$f" "$PUB/bundle/" 2>>"$ERRORS"
    done )
  find "$PUB/bundle" -name '__pycache__' -prune -exec rm -rf {} + 2>/dev/null
  run "$PUB/bundle/files-sha256.txt" bash -c "cd '$BUNDLE' && find . -path ./env -prune -o -path ./tls -prune -o -type f -print0 | sort -z | xargs -0 sha256sum"
  # env/: names of variables and placeholder state only in public; current files in sensitive (tokens changed at bootstrap).
  run "$PUB/bundle/env-keys.txt" python3 -I - "$BUNDLE/env" <<'PY'
import pathlib, re, sys
root = pathlib.Path(sys.argv[1]); unfilled = re.compile(r'__(?:GENERATE_|OWNER|RUN_)\S*?__')
for path in sorted(root.glob('*.env')):
    print(f'## {path.name} mode={oct(path.stat().st_mode & 0o777)}')
    for line in path.read_text(encoding='utf-8').splitlines():
        if line and not line.startswith('#') and '=' in line:
            key, _, value = line.partition('=')
            state = 'UNFILLED' if unfilled.search(value) else ('EMPTY' if not value.strip() else 'set')
            print(f'{key}\t{state}')
PY
  mkdir -p "$SEC/bundle"
  ( cd "$BUNDLE" && cp -a --parents env "$SEC/bundle/" 2>>"$ERRORS" )
  find "$SEC/bundle/env" -name '*.example' -delete 2>/dev/null
  ((INCLUDE_TLS)) && ( cd "$BUNDLE" && cp -a --parents tls "$SEC/bundle/" 2>>"$ERRORS" )
  # Realm templates the runbook may have left on the host (nonsecret, but tenant/client IDs).
  for d in /opt/swoovr/swoovr-uat-realms /root/swoovr-uat-realms; do
    [[ -d "$d" ]] && cp -a --parents "$d" "$SEC/" 2>>"$ERRORS"
  done
  run "$PUB/bundle/tls-stat.txt" find "$BUNDLE/tls" -printf '%M %u:%g %U:%G %s %p\n'
else
  echo "Bundle directory $BUNDLE not found" >> "$ERRORS"
fi

# ------------------------------------------------------------------ certificates (public facts; private keys hashed, not copied by default)
say 'certificates'
certinfo() { openssl x509 -in "$1" -noout -subject -issuer -serial -startdate -enddate -fingerprint -sha256 -ext subjectAltName 2>&1; }
run "$PUB/certs/origin-certs-stat.txt" find /etc/caddy/certs -printf '%M %u:%g %s %p\n'
[[ -f /etc/caddy/certs/uat-origin.pem ]] && run "$PUB/certs/uat-origin.pem.txt" certinfo /etc/caddy/certs/uat-origin.pem
for f in "$BUNDLE/tls/trust/pg-ca.crt" "$BUNDLE/tls/postgres/server.crt"; do
  [[ -f "$f" ]] && run "$PUB/certs/$(basename "$f").txt" certinfo "$f"
done
run "$PUB/certs/key-matches.txt" bash -c '
  m() { [[ -f "$1" && -f "$2" ]] || { echo "missing $1 or $2"; return; }
        [[ "$(openssl x509 -in "$1" -noout -pubkey | sha256sum)" == "$(openssl pkey -in "$2" -pubout 2>/dev/null | sha256sum)" ]] && echo "MATCH $1" || echo "MISMATCH $1"; }
  m /etc/caddy/certs/uat-origin.pem /etc/caddy/certs/uat-origin.key
  m '"$BUNDLE"'/tls/trust/pg-ca.crt '"$BUNDLE"'/tls/postgres/ca.key
  m '"$BUNDLE"'/tls/postgres/server.crt '"$BUNDLE"'/tls/postgres/server.key
  openssl verify -CAfile '"$BUNDLE"'/tls/trust/pg-ca.crt '"$BUNDLE"'/tls/postgres/server.crt'
run "$PUB/certs/cloudflare-origin-roots.txt" bash -c "awk '/BEGIN CERT/{n++;f=\"$WORK/.cfroot.\"n} {print > f}' '$BUNDLE/tls/trust/cloudflare-origin-ca.pem' && for f in '$WORK'/.cfroot.*; do openssl x509 -in \"\$f\" -noout -subject -enddate -fingerprint -sha256; rm -f \"\$f\"; done"
((INCLUDE_ORIGIN_KEY)) && cp -a --parents /etc/caddy/certs "$SEC/" 2>>"$ERRORS"
# SHA-256 of every secret-bearing file, so they can be compared with the local backup without copying them again.
run "$SEC/secret-files-sha256.txt" bash -c "sha256sum /etc/caddy/certs/* '$BUNDLE'/tls/postgres/* '$BUNDLE'/tls/trust/* '$BUNDLE'/env/*.env '$BUNDLE'/env/.generated 2>/dev/null"

# ------------------------------------------------------------------ health through Caddy (same probe as `bin/swoovr status`; read only)
say 'health'
caddy="$(cid caddy)"
mkdir -p "$PUB/health"
if [[ -n "$caddy" ]]; then
  for host in uat api-uat admin-uat auth-uat helpdesk-uat; do
    path=/health/live; [[ $host == auth-uat ]] && path=/realms/swoovr-members; [[ $host == helpdesk-uat ]] && path=/
    code="$(docker exec "$caddy" wget -q -S -O /dev/null --no-check-certificate "https://$host.swoovr.app$path" 2>&1 | awk '/HTTP\//{print $2}' | tail -1)"
    printf '%-26s %-22s %s\n' "$host.swoovr.app" "$path" "${code:-unreachable}"
  done > "$PUB/health/through-caddy.txt" 2>&1 || true
  ready="$(docker exec "$caddy" wget -q -O - --no-check-certificate https://api-uat.swoovr.app/health/ready 2>&1)"
  printf 'api /health/ready: %s\n' "$ready" >> "$PUB/health/through-caddy.txt"
  run "$PUB/health/caddy-version.txt" docker exec "$caddy" caddy version
fi

# ------------------------------------------------------------------ read-only database metadata (names and counts only)
q() { # q <service> <database> <sql> -> stdout; read-only transaction, local socket, superuser from the container's env
  local id; id="$(cid "$1")"; [[ -n "$id" ]] || { echo "no $1 container"; return 1; }
  docker exec -e PGOPTIONS='-c default_transaction_read_only=on' "$id" sh -c 'psql -X -q -A -F " | " -U "$POSTGRES_USER" -d "$1" -c "$2"' sh "$2" "$3"
}
if ((WITH_DB)); then
  say 'database metadata (read only)'
  D="$PUB/databases"; mkdir -p "$D"
  run "$D/swoovr-postgres-version.txt" q swoovr-postgres postgres 'SELECT version();'
  run "$D/swoovr-postgres-databases.txt" q swoovr-postgres postgres "SELECT datname, pg_get_userbyid(datdba) AS owner, pg_size_pretty(pg_database_size(datname)) AS size FROM pg_database ORDER BY 1;"
  run "$D/swoovr-postgres-roles.txt" q swoovr-postgres postgres "SELECT rolname, rolsuper, rolcreaterole, rolcreatedb, rolcanlogin FROM pg_roles WHERE rolname NOT LIKE 'pg\_%' ORDER BY 1;"
  run "$D/swoovr-migrations.txt" q swoovr-postgres swoovr 'SELECT "MigrationId", "ProductVersion" FROM __ef_migrations_history ORDER BY 1;'
  run "$D/swoovr-table-rows.txt" q swoovr-postgres swoovr "SELECT schemaname, relname, n_live_tup FROM pg_stat_user_tables WHERE n_live_tup > 0 ORDER BY 1,2;"
  run "$D/swoovr-accounts.txt" q swoovr-postgres swoovr 'SELECT realm, status, count(*) AS accounts, count(*) FILTER (WHERE is_root_admin) AS root_admins FROM accounts GROUP BY 1,2 ORDER BY 1,2;'
  run "$D/keycloak-realms.txt" q swoovr-postgres keycloak "SELECT r.name, r.enabled, r.ssl_required, (SELECT count(*) FROM user_entity u WHERE u.realm_id = r.id) AS users, (SELECT count(*) FROM client c WHERE c.realm_id = r.id) AS clients FROM realm r ORDER BY 1;"
  run "$D/keycloak-clients.txt" q swoovr-postgres keycloak "SELECT r.name AS realm, c.client_id, c.enabled, c.public_client, c.service_accounts_enabled, (SELECT string_agg(value, ' ') FROM redirect_uris ru WHERE ru.client_id = c.id) AS redirects FROM client c JOIN realm r ON r.id = c.realm_id WHERE r.name <> 'master' AND c.client_id LIKE 'swoovr%' ORDER BY 1,2;"
  run "$D/keycloak-identity-providers.txt" q swoovr-postgres keycloak "SELECT r.name AS realm, i.provider_alias, i.provider_id, i.enabled, i.trust_email, i.store_token FROM identity_provider i JOIN realm r ON r.id = i.realm_id ORDER BY 1,2;"
  run "$D/keycloak-migration.txt" q swoovr-postgres keycloak 'SELECT id, version, update_time FROM migration_model ORDER BY update_time DESC LIMIT 3;'
  # Usernames of Keycloak master-realm admins and of staff users: sensitive (personal identifiers).
  run "$SEC/databases/keycloak-master-users.txt" q swoovr-postgres keycloak "SELECT u.username, u.enabled, u.email_verified, (SELECT string_agg(type, ',') FROM credential cr WHERE cr.user_id = u.id) AS credentials FROM user_entity u JOIN realm r ON r.id = u.realm_id WHERE r.name = 'master' ORDER BY 1;"
  run "$SEC/databases/keycloak-staff-users.txt" q swoovr-postgres keycloak "SELECT u.id AS subject, u.username, u.enabled, u.email_verified FROM user_entity u JOIN realm r ON r.id = u.realm_id WHERE r.name = 'swoovr-staff' AND u.service_account_client_link IS NULL ORDER BY 2;"
  run "$D/zammad-postgres-databases.txt" q zammad-postgresql postgres "SELECT datname, pg_get_userbyid(datdba) AS owner, pg_size_pretty(pg_database_size(datname)) AS size FROM pg_database ORDER BY 1;"
  zdb="$(docker exec "$(cid zammad-postgresql)" sh -c 'echo "$ZAMMAD_DB"' 2>/dev/null)"
  if [[ -n "$zdb" ]]; then
    run "$D/zammad-counts.txt" q zammad-postgresql "$zdb" "SELECT (SELECT count(*) FROM tickets) AS tickets, (SELECT count(*) FROM users) AS users, (SELECT count(*) FROM groups) AS groups, (SELECT count(*) FROM channels WHERE active) AS active_channels, (SELECT count(*) FROM tokens WHERE action = 'api') AS api_tokens;"
    run "$D/zammad-groups.txt" q zammad-postgresql "$zdb" 'SELECT name, active FROM groups ORDER BY 1;'
    run "$D/zammad-channels.txt" q zammad-postgresql "$zdb" 'SELECT id, area, active, group_id FROM channels ORDER BY 1;'
    run "$D/zammad-settings.txt" q zammad-postgresql "$zdb" "SELECT name, CASE WHEN name LIKE '%credentials%' THEN '(present)' ELSE state_current::text END FROM settings WHERE name IN ('system_init_done','fqdn','http_type','auth_openid_connect','auth_openid_connect_credentials','user_show_password_login','notification_sender','api_token_access','es_url') ORDER BY 1;"
    run "$SEC/databases/zammad-agent-logins.txt" q zammad-postgresql "$zdb" "SELECT login, active FROM users WHERE login LIKE 'swoovr-%' OR id IN (SELECT user_id FROM roles_users ru JOIN roles r ON r.id = ru.role_id WHERE r.name IN ('Admin','Agent')) ORDER BY 1;"
  fi
  rails="$(cid zammad-railsserver)"
  [[ -n "$rails" ]] && run "$D/zammad-elasticsearch.txt" docker exec "$rails" sh -c 'curl -s "http://zammad-elasticsearch:9200/_cluster/health?pretty"; curl -s "http://zammad-elasticsearch:9200/_cat/indices?v"'
  [[ -n "$rails" ]] && run "$D/zammad-version.txt" docker exec "$rails" sh -c 'cat /opt/zammad/VERSION 2>/dev/null || true'
fi

# ------------------------------------------------------------------ optional logs (may contain addresses and tokens in URLs)
if ((WITH_LOGS)); then
  say 'logs'
  mkdir -p "$SEC/logs"
  for id in $(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT"); do
    n="$(docker inspect -f '{{.Name}}' "$id" | tr -d /)"
    timeout 60 docker logs --tail 300 "$id" > "$SEC/logs/$n.log" 2>&1
  done
fi

# ------------------------------------------------------------------ inventory and archives
say 'archives'
cat > "$PUB/README.txt" <<EOF
Swoovr UAT server inventory collected $STAMP on $(hostname) by scripts/uat/collect-server-state.sh.
Bundle: $BUNDLE  Project: $PROJECT  include-tls=$INCLUDE_TLS include-origin-key=$INCLUDE_ORIGIN_KEY logs=$WITH_LOGS db=$WITH_DB
Nothing on the host was changed. Commands that failed are listed in errors.txt.
EOF
( cd "$WORK" && find "$NAME-public" -type f -printf '%M %s %p\n' | sort -k3 ) > "$PUB/INVENTORY.txt"
( cd "$WORK" && find "$NAME-sensitive" -type f -printf '%M %s %p\n' | sort -k3 ) > "$SEC/INVENTORY.txt"
chmod -R go-rwx "$PUB" "$SEC"
tar -C "$WORK" --numeric-owner -czf "/root/$NAME-public.tar.gz" "$NAME-public"
tar -C "$WORK" --numeric-owner -czf "/root/$NAME-sensitive.tar.gz" "$NAME-sensitive"
chmod 0600 "/root/$NAME-public.tar.gz" "/root/$NAME-sensitive.tar.gz"
( cd /root && sha256sum "$NAME-public.tar.gz" "$NAME-sensitive.tar.gz" > "$NAME.SHA256SUMS" && chmod 0600 "$NAME.SHA256SUMS" )
echo
echo "Wrote (0600):"
ls -l "/root/$NAME-public.tar.gz" "/root/$NAME-sensitive.tar.gz" "/root/$NAME.SHA256SUMS"
echo "Commands that failed: $(wc -l < "$ERRORS") (see errors.txt in the public archive)."
