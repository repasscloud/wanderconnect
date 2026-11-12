#!/usr/bin/env python3
"""Validate a release manifest (KEY=value lines) and print it as JSON. Every *_IMAGE must be repo@sha256:<64 hex>.

  manifest-json.py FILE              any manifest (release.env or release-manifest.env)
  manifest-json.py FILE --release    also require a D362 release: SWOOVR_TAG vX.Y.Z, SWOOVR_VERSION X.Y.Z equal to the
                                     tag, a 40-hex SWOOVR_COMMIT and the five Swoovr images from ghcr.io/repasscloud
"""
import json
import re
import sys

TAG = re.compile(r'v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)')
COMPONENTS = ('api', 'web', 'admin', 'operator', 'keycloak')


def read(path):
    values = {}
    for line in open(path, encoding='utf-8').read().splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        key, _, value = line.partition('=')
        if key in values:
            raise SystemExit(f'{key} is defined twice.')
        if key.endswith('_IMAGE') and not re.fullmatch(r'[a-z0-9./_-]+@sha256:[0-9a-f]{64}', value):
            raise SystemExit(f'{key} is not an immutable digest reference.')
        values[key] = value
    if not re.fullmatch(r'[0-9a-f]{40}', values.get('SWOOVR_COMMIT', '')):
        raise SystemExit('SWOOVR_COMMIT must be a full 40-character commit SHA.')
    return values


def require_release(values):
    tag = values.get('SWOOVR_TAG', '')
    if not TAG.fullmatch(tag):
        raise SystemExit('SWOOVR_TAG must be vX.Y.Z (no prefix zeros, no suffix).')
    if values.get('SWOOVR_VERSION') != tag[1:]:
        raise SystemExit('SWOOVR_VERSION must equal the tag without its v.')
    for component in COMPONENTS:
        image = values.get(f'SWOOVR_{component.upper()}_IMAGE', '')
        if not re.fullmatch(rf'ghcr\.io/repasscloud/swoovr-{component}@sha256:[0-9a-f]{{64}}', image):
            raise SystemExit(f'SWOOVR_{component.upper()}_IMAGE must be ghcr.io/repasscloud/swoovr-{component}@sha256:<digest>.')


def main(argv):
    values = read(argv[0])
    if '--release' in argv[1:]:
        require_release(values)
    print(json.dumps(values, indent=2, sort_keys=True))


if __name__ == '__main__':
    main(sys.argv[1:])
