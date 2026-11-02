#!/usr/bin/env bash
# Inspect an already-built disposable runtime; no service, network or volume access.
set -euo pipefail
if [[ $# -ne 1 ]]; then
    echo "Usage: $0 IMAGE" >&2
    exit 2
fi
docker run --rm --network none --read-only --cap-drop ALL --entrypoint /bin/sh "$1" -ec '
    package=libgssapi-krb5-2
    test "$(dpkg-query -W -f="\${Status}" "$package")" = "install ok installed"
    library=$(dpkg-query -L "$package" | sed -n "/\/libgssapi_krb5\.so\.2$/p")
    test -n "$library"
    test -r "$library"
    dependencies=$(ldd "$library")
    printf "%s\n" "$dependencies"
    if printf "%s\n" "$dependencies" | grep -q "not found"; then
        exit 1
    fi
    printf "PASS: GSS runtime library and native dependencies resolve\n"
'
