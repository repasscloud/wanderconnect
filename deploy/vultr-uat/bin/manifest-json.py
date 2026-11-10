#!/usr/bin/env python3
"""Convert release-manifest.env (KEY=value lines) to JSON. Refuses tags: every image must be repo@sha256:<64 hex>."""
import json, re, sys

def main(path):
    values = {}
    for line in open(path, encoding='utf-8').read().splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        key, _, value = line.partition('=')
        if key.endswith('_IMAGE') and not re.fullmatch(r'[a-z0-9./_-]+@sha256:[0-9a-f]{64}', value):
            raise SystemExit(f'{key} is not an immutable digest reference.')
        values[key] = value
    if not re.fullmatch(r'[0-9a-f]{40}', values.get('SWOOVR_COMMIT', '')):
        raise SystemExit('SWOOVR_COMMIT must be a full 40-character commit SHA.')
    print(json.dumps(values, indent=2, sort_keys=True))

if __name__ == '__main__':
    main(sys.argv[1])
