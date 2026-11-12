#!/usr/bin/env bash
# Package the Vultr UAT deployment bundle for one release (D362): compose, Caddyfile, env templates, scripts, the Zammad
# provisioner, the disabled realm templates generated for this commit, the release manifest (image digests) and a
# SHA256SUMS file the host verifies before installing. Contains no secrets.
# Usage (repository root): deploy/vultr-uat/bin/make-bundle.sh <release-manifest.env> <realms-dir> <release-manifest.json> <output-dir>
set -euo pipefail
manifest="${1:?release-manifest.env required}"
realms="${2:?realm template directory required}"
manifest_json="${3:?release-manifest.json required}"
out="${4:?output directory required}"
root="$(cd "$(dirname "$0")/../../.." && pwd)"
python3 "$root/deploy/vultr-uat/bin/manifest-json.py" "$manifest" --release > /dev/null
tag="$(grep '^SWOOVR_TAG=' "$manifest" | cut -d= -f2)"
commit="$(grep '^SWOOVR_COMMIT=' "$manifest" | cut -d= -f2)"
for kind in members venues staff; do
  python3 -I -c 'import json,sys; d=json.load(open(sys.argv[1])); assert d["enabled"] is False and d["users"] == []' \
    "$realms/swoovr-$kind.json" || { echo "swoovr-$kind.json is not a disabled userless realm template" >&2; exit 1; }
done
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
bundle="$stage/swoovr-uat"
mkdir -p "$bundle/zammad" "$bundle/realms" "$out"
cp -R "$root/deploy/vultr-uat/." "$bundle/"
{ cat "$manifest"; echo; cat "$root/deploy/vultr-uat/thirdparty.env"; } > "$bundle/release.env"
python3 "$root/deploy/vultr-uat/bin/manifest-json.py" "$bundle/release.env" --release > /dev/null
cp "$manifest_json" "$bundle/release-manifest.json"
cp "$root/containers/zammad/provision.rb" "$bundle/zammad/provision.rb"
cp "$realms"/swoovr-{members,venues,staff}.json "$realms/manifest.json" "$bundle/realms/"
# Never ship filled private files or host state even if someone created them in the checkout.
find "$bundle/env" -type f ! -name '*.example' -delete
rm -rf "$bundle/tls" "$bundle/state" "$bundle/release.env.previous" "$bundle/release.env.failed"
find "$bundle" -name __pycache__ -prune -exec rm -rf {} +
( cd "$bundle" && find . -type f ! -name SHA256SUMS -print0 | LC_ALL=C sort -z | xargs -0 sha256sum > SHA256SUMS )
name="swoovr-uat-bundle-$tag-$commit.tar.gz"
tar -C "$stage" --owner=0 --group=0 --numeric-owner --sort=name -czf "$out/$name" swoovr-uat
( cd "$out" && sha256sum "$name" > "$name.sha256" )
echo "Wrote $out/$name"
