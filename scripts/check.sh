#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 scripts/check_naming.py
python3 scripts/check_design.py --check
python3 scripts/keycloak_realms.py
node --test tests/auth-harness/*.test.mjs tests/appearance/*.test.cjs
dotnet restore Swoovr.Local.sln --ignore-failed-sources -p:NuGetAudit=false
dotnet build Swoovr.Local.sln --no-restore --configuration Release
# Persistence checks use the local PostgreSQL container when it is running (./scripts/docker-local.sh up);
# otherwise Swoovr.Checks prints a SKIP line. Each run uses its own throwaway database.
if [[ -z "${SWOOVR_TEST_DATABASE:-}" && -f .local/docker.env ]] && grep -q '^SWOOVR_DB_PASSWORD=' .local/docker.env \
  && (exec 3<>/dev/tcp/127.0.0.1/5433) 2>/dev/null; then
  SWOOVR_TEST_DATABASE="Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=$(grep '^SWOOVR_DB_PASSWORD=' .local/docker.env | cut -d= -f2)"
  export SWOOVR_TEST_DATABASE
fi
dotnet run --project tests/Swoovr.Checks --no-build --configuration Release
python3 scripts/smoke.py
python3 scripts/prototype_smoke.py
python3 scripts/connection_smoke.py
git diff --check
git diff --cached --check
