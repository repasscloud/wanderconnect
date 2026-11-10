#!/usr/bin/env bash
# Package the Vultr UAT deployment bundle for one release: compose, Caddyfile, env templates, scripts,
# the Zammad provisioner and the release manifest (image digests). Contains no secrets.
# Usage (repository root): deploy/vultr-uat/bin/make-bundle.sh <release-manifest.env> <output-dir>
set -euo pipefail
manifest="${1:?release-manifest.env required}"
out="${2:?output directory required}"
root="$(cd "$(dirname "$0")/../../.." && pwd)"
python3 "$root/deploy/vultr-uat/bin/manifest-json.py" "$manifest" > /dev/null
commit="$(grep '^SWOOVR_COMMIT=' "$manifest" | cut -d= -f2)"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
bundle="$stage/swoovr-uat"
mkdir -p "$bundle/zammad" "$out"
cp -R "$root/deploy/vultr-uat/." "$bundle/"
{ cat "$manifest"; echo; cat "$root/deploy/vultr-uat/thirdparty.env"; } > "$bundle/release.env"
cp "$root/containers/zammad/provision.rb" "$bundle/zammad/provision.rb"
# Never ship filled private files even if someone created them in the checkout.
find "$bundle/env" -type f ! -name '*.example' -delete
rm -rf "$bundle/tls" "$bundle/release.env.previous" "$bundle/release.env.failed"
find "$bundle" -name __pycache__ -prune -exec rm -rf {} +
mkdir -p "$bundle/tls"
tar -C "$stage" --owner=0 --group=0 -czf "$out/swoovr-uat-bundle-$commit.tar.gz" swoovr-uat
echo "Wrote $out/swoovr-uat-bundle-$commit.tar.gz"
