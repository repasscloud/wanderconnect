#!/usr/bin/env bash
# Managed localhost only. Executable from Bash or zsh; Bash 3.2+ and Docker Compose v2.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
repo=$PWD
export SWOOVR_LOCAL_ROOT="$repo"
export SWOOVR_LOCAL_UID="$(id -u)" SWOOVR_LOCAL_GID="$(id -g)"
state=$repo/.local/platform
project=swoovr-platform
tools_image=node:22-bookworm-slim@sha256:4b150b48b4116e2a22f2c39cc27025fbf4f153e2078fd3b14d159fdb5c7279da
usage() {
  cat <<'HELP'
Usage: ./scripts/local-platform.sh COMMAND [OPTIONS]
  init                 Generate missing private credentials (no services started).
  up [--reuse]         Build/start and migrate; seed members on first start; keep data.
  up --fresh --yes     Delete this managed installation's data, then build/start.
  down                 Stop/remove managed containers; keep all data/credentials.
  reset --yes          Stop/remove managed containers and ALL their data volumes.
  status               Show managed services and refresh container ID/PID records.
  smoke                Check portals and real Keycloak password+OTP admin access.
  admin-code           Print a current test admin TOTP code for manual sign-in.
  logs [SERVICE]       Follow managed logs (may contain private local information).
Options:
  --take-ports         On up only: stop Docker containers publishing required ports
                       and positively identified Swoovr hosts from THIS checkout.
                       Other host processes are never killed.
  --no-screening       On up only: skip photo screening (uploads cannot complete).
  --forget-credentials On reset --yes only: also remove managed credentials.

Private config: .local/platform/docker.env; initial defaults may come from .env.
Before first up set SWOOVR_LOCAL_ADMIN_EMAIL (must end @example.test),
SWOOVR_LOCAL_ADMIN_PASSWORD (12+ characters) and/or KC_BOOTSTRAP_ADMIN_PASSWORD.
Missing secrets are generated. Existing accounts/passwords are NEVER silently
rotated. Edit before first up, or deliberately reset --yes before changing them.
Use admin-code for the fictional admin's second factor; no real SMS/email sent.
No host .NET, Node, Python or browser installation is required.
HELP
}
command=${1:-help}; if [[ $# -gt 0 ]]; then shift; fi
fresh=false; reuse=false; take_ports=false; screening=true; yes=false; forget=false; service=
while [[ $# -gt 0 ]]; do
  case "$1" in
    --fresh) fresh=true ;; --reuse) reuse=true ;; --take-ports) take_ports=true ;;
    --no-screening) screening=false ;; --yes) yes=true ;; --forget-credentials) forget=true ;;
    *) if [[ "$command" == logs && -z "$service" && "$1" != -* ]]; then service=$1
       else echo "Unknown argument: $1" >&2; exit 2; fi ;;
  esac
  shift
done
case "$command" in help|-h|--help) usage; exit 0 ;; init|up|down|reset|status|smoke|admin-code|logs) ;; *) usage >&2; exit 2 ;; esac
if $fresh && $reuse; then echo "Choose only one of --fresh or --reuse." >&2; exit 2; fi
if { $reuse && [[ "$command" != up ]]; } || { $fresh && [[ "$command" != up ]]; } || { $forget && [[ "$command" != reset ]]; } \
  || { $take_ports && [[ "$command" != up ]]; }; then echo 'Option does not apply to this command.' >&2; exit 2; fi
if { $fresh || [[ "$command" == reset ]]; } && ! $yes; then echo 'Destructive reset requires --yes; no changes made.' >&2; exit 2; fi
command -v docker >/dev/null || { echo 'Install and start Docker Desktop/Engine with Compose v2.' >&2; exit 1; }
docker info >/dev/null 2>&1 || { echo 'Docker is not running. Start Docker Desktop/Engine.' >&2; exit 1; }
docker compose version >/dev/null
[[ ! -L "$state" ]] || { echo 'Refusing a symlinked managed state directory.' >&2; exit 1; }
mkdir -p "$state"
chmod 700 "$state"
if ! mkdir "$state/lock" 2>/dev/null; then
  echo "Another launcher holds $state/lock. Check its pid file before removing a stale lock." >&2; exit 1
fi
printf '%s\n' "$$" > "$state/lock/pid"
cleanup() { rm -f "$state/lock/pid"; rmdir "$state/lock"; }
trap cleanup EXIT
# Explicit project/file/env prevents accidentally controlling legacy or hosted stacks.
dc() { docker compose -p "$project" -f "$repo/compose.local.yaml" --env-file "$state/docker.env" "$@"; }
node_local() {
  docker run --rm --user "$SWOOVR_LOCAL_UID:$SWOOVR_LOCAL_GID" --label com.swoovr.local-tool=true \
    --mount "type=bind,source=$repo,target=/work,readonly" \
    --mount "type=bind,source=$state,target=/work/.local" \
    -w /work "$tools_image" node scripts/local/platform.mjs "$@"
}
tool() { dc run --rm --no-deps tools node scripts/local/platform.mjs "$@"; }
record() {
  local ids
  ids=$(docker ps -aq --filter "label=com.docker.compose.project=$project")
  printf 'container_id\tname\tlinux_pid\tstarted_at\tstatus\n' > "$state/containers.tsv"
  if [[ -n "$ids" ]]; then
    # Deliberate word splitting of Docker-generated hexadecimal IDs only.
    docker inspect --format '{{.Id}}{{printf "\t"}}{{.Name}}{{printf "\t"}}{{.State.Pid}}{{printf "\t"}}{{.State.StartedAt}}{{printf "\t"}}{{.State.Status}}' $ids >> "$state/containers.tsv"
  fi
  chmod 600 "$state/containers.tsv"
}
failed() {
  trap - ERR INT TERM
  echo 'Startup failed. Stopping managed containers and retaining data for retry; inspect logs.' >&2
  record || true
  dc --profile "*" stop >/dev/null 2>&1 || true
  record || true
  exit 1
}
reset_data() {
  dc --profile "*" down --volumes --remove-orphans
  # Only these managed fixtures point into the now-deleted DB/realm; do not touch legacy .local.
  rm -f "$state/admin.json" "$state/identities.json" "$state/admin-subject" "$state/initialized" "$state/containers.tsv"
}
free_ports() {
  local port id owner ids= pid exe
  local running bindings
  running=$(docker ps -q)
  if [[ -n "$running" ]]; then
    # Match the HOST binding, not Docker's `publish` filter (which matches target
    # ports and could select an unrelated 60080->8080 mapping).
    bindings=$(docker inspect --format '{{range $port, $bindings := .NetworkSettings.Ports}}{{range $bindings}}{{$.Id}} {{.HostPort}}{{printf "\n"}}{{end}}{{end}}' $running)
    while read -r id port; do
      case "$port" in 5080|5081|5082|8180|8025|1025|5433|8080) ;; *) continue ;; esac
      owner=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$id")
      if [[ "$owner" != "$project" ]]; then
        if ! $take_ports; then echo "Docker container $id uses host port $port; rerun up with --take-ports." >&2; return 1; fi
        case " $ids " in *" $id "*) ;; *) ids="$ids $id" ;; esac
      fi
    done <<< "$bindings"
  fi
  if [[ -n "$ids" ]]; then
    printf '%s\n' "$(date -u +%FT%TZ) stopped Docker IDs:$ids" >> "$state/displaced.tsv"
    docker stop $ids
  fi
  if command -v lsof >/dev/null; then
    for port in 5080 5081 5082; do
      for pid in $(lsof -nP -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true); do
        exe=$(ps -p "$pid" -o comm= 2>/dev/null || true)
        case "$exe" in
          "$repo"/src/Swoovr.Api/bin/*/Swoovr.Api|"$repo"/src/Swoovr.Web/bin/*/Swoovr.Web|"$repo"/src/Swoovr.Admin/bin/*/Swoovr.Admin)
            if ! $take_ports; then echo "Local Swoovr PID $pid uses port $port; rerun with --take-ports." >&2; return 1; fi
            printf '%s\t%s\t%s\t%s\n' "$pid" "$(ps -p "$pid" -o lstart=)" "$exe" stopped >> "$state/displaced-hosts.tsv"
            echo "Stopping identified local Swoovr PID $pid on port $port."
            kill -TERM "$pid"
            ;;
          *)
            # Docker Desktop's forwarding process is not a container PID; never kill it.
            case "$exe" in *com.docker.backend*|*com.docker.vpnkit*) ;; *) echo "Unmanaged PID $pid on port $port; stop it yourself." >&2; return 1 ;; esac
            ;;
        esac
      done
    done
  fi
}
# A fixed localhost project must never be adopted/deleted from another checkout.
# Labels persist on volumes after `down`, so the guard also covers stopped data.
verify_owner() {
  local resource owner
  for resource in $(docker ps -aq --filter "label=com.docker.compose.project=$project"); do
    owner=$(docker inspect --format '{{index .Config.Labels "com.swoovr.local-root"}}' "$resource")
    [[ "$owner" == "$repo" ]] || { echo "Managed container $resource belongs to another/unlabelled checkout; refusing lifecycle changes." >&2; return 1; }
  done
  for resource in $(docker volume ls -q --filter "label=com.docker.compose.project=$project"); do
    owner=$(docker volume inspect --format '{{index .Labels "com.swoovr.local-root"}}' "$resource")
    [[ "$owner" == "$repo" ]] || { echo "Managed volume $resource belongs to another/unlabelled checkout; refusing lifecycle changes." >&2; return 1; }
  done
}
verify_owner
if [[ "$command" == init || "$command" == up ]]; then node_local init; fi
if [[ ! -f "$state/docker.env" ]]; then
  echo 'No managed installation yet. Run ./scripts/local-platform.sh init or up.' >&2; exit 1
fi
case "$command" in
  init) usage ;;
  up)
    trap failed ERR INT TERM
    if $fresh; then reset_data; fi
    free_ports
    # The default image is the existing owner-maintained screening service. If it
    # has never been built, build from the sibling checkout; no credentials copied.
    if $screening; then
      image=$(sed -n "s/^SWOOVR_SCREENING_IMAGE='\(.*\)'$/\1/p" "$state/docker.env")
      image=${image:-image-moderation-api-moderation-api:latest}
      if ! docker image inspect "$image" >/dev/null 2>&1; then
        if [[ "$image" == image-moderation-api-moderation-api:latest && -f ../image-moderation-api/Dockerfile ]]; then
          docker build -t "$image" ../image-moderation-api
        else
          docker pull "$image" || { echo 'Supply SWOOVR_SCREENING_IMAGE or sibling image-moderation-api; alternatively use --no-screening.' >&2; false; }
        fi
      fi
    fi
    dc build api web admin
    dc up -d --wait --wait-timeout 360 mailpit postgres keycloak
    record
    # New named volumes are root-owned: initialise only the managed app/portal
    # volume mountpoints. Serving apps still run as the image's non-root app user.
    dc run --rm --no-deps --user root --entrypoint /bin/bash api -c 'mkdir -p /app/.local; chown 1654:1654 /app/.local'
    for portal in web admin; do
      dc run --rm --no-deps --user root --entrypoint /bin/bash "$portal" -c 'mkdir -p /home/app/.aspnet/DataProtection-Keys; chown 1654:1654 /home/app/.aspnet/DataProtection-Keys'
    done
    dc run --rm --no-deps api migrate
    dc run --rm --no-deps api geonames import /fixtures/geonames
    # Reuse must preserve test deletions/revocations instead of re-enrolling users.
    if [[ ! -f "$state/initialized" ]]; then
      dc run --rm --no-deps api beta add fictional.alex@example.test fictional.sam@example.test
    fi
    if $screening; then dc --profile screening up -d --wait --wait-timeout 360 screening
    else dc --profile screening stop screening; fi
    dc up -d --wait api web admin
    record
    # Bounded readiness wait uses Node inside the shared namespace, not host tools.
    dc run --rm --no-deps tools node -e '
      (async()=>{for(let i=0;i<120;i++){try{const r=await fetch("http://127.0.0.1:5080/health/live",{signal:AbortSignal.timeout(2000)});if(r.ok)return;}catch{}await new Promise(r=>setTimeout(r,1000));}throw Error("API readiness timed out");})().catch(e=>{console.error(e.message);process.exit(1)});'
    tool bootstrap
    subject=$(cat "$state/admin-subject")
    [[ "$subject" =~ ^[a-f0-9-]{36}$ ]]
    dc exec -T api dotnet Swoovr.Api.dll root-admin grant "$subject"
    while IFS=$'\t' read -r staff_subject staff_role; do
      [[ "$staff_subject" =~ ^[a-f0-9-]{36}$ && "$staff_role" =~ ^(support|trust-safety)$ ]] || continue
      dc exec -T api dotnet Swoovr.Api.dll staff-role grant "$staff_subject" "$staff_role" </dev/null >/dev/null
    done < "$state/staff-roles.tsv"
    tool smoke
    date -u +%FT%TZ > "$state/initialized"
    record
    trap - ERR INT TERM
    cat <<'READY'
Ready: member http://localhost:5081/account | admin http://localhost:5082
Mail http://localhost:8025 | Keycloak http://localhost:8180/admin/
Private credentials: .local/platform/docker.env (not printed).
Fictional members: fictional.alex@example.test, fictional.sam@example.test
Use SWOOVR_LOCAL_MEMBER_PASSWORD; complete age, phone and profile gates in the UI.
Admin: SWOOVR_LOCAL_ADMIN_EMAIL / SWOOVR_LOCAL_ADMIN_PASSWORD.
Staff roles: fictional.trust@example.test (Trust & Safety), fictional.support@example.test (Support); SWOOVR_LOCAL_ADMIN_PASSWORD, no second factor.
Second factor: ./scripts/local-platform.sh admin-code
Docker IDs and Linux PIDs: .local/platform/containers.tsv
Stop/keep data: ./scripts/local-platform.sh down
READY
    ;;
  down) dc --profile "*" down --remove-orphans; record ;;
  reset) reset_data; if $forget; then rm -f "$state/docker.env"; fi; record ;;
  status) dc ps -a; record ;;
  smoke) tool smoke; record ;;
  admin-code) node_local admin-code ;;
  logs) if [[ -n "$service" ]]; then dc logs --follow --tail 100 "$service"; else dc logs --follow --tail 100; fi ;;
esac
