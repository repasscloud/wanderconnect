#!/usr/bin/env bash
set -euo pipefail
BRAND_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_BIN="${PYTHON_BIN:-python3}"
exec "$PYTHON_BIN" "$BRAND_ROOT/scripts/make_contact_sheet.py" "$@"
