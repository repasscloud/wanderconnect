#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 scripts/check_design.py --check
node --test tests/auth-harness/*.test.mjs
dotnet restore DatingApp.Local.sln --ignore-failed-sources -p:NuGetAudit=false
dotnet build DatingApp.Local.sln --no-restore --configuration Release
dotnet run --project tests/DatingApp.Checks --no-build --configuration Release
python3 scripts/smoke.py
git diff --check
git diff --cached --check
