#!/usr/bin/env python3
"""Local HTTP contract observations only: this does not validate tokens or log in."""
import json
import time
import urllib.error
import urllib.request


def fetch(url):
    for attempt in range(60):
        try:
            with urllib.request.urlopen(url, timeout=3) as response:
                return response.read().decode()
        except (urllib.error.URLError, TimeoutError):
            if attempt == 59:
                raise
            time.sleep(1)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


require(json.loads(fetch('http://localhost:5080/health/live')) == {'status': 'ok'},
        'API liveness mismatch')
status = json.loads(fetch('http://localhost:5080/api/v1/status'))
require(status == {'service': 'DatingApp.Api', 'stage': 'foundation'}, 'API status mismatch')
require('WanderConnect' in fetch('http://localhost:5081/'), 'Web shell missing')
issuer = 'http://localhost:8180/realms/datingapp-dev'
discovery = json.loads(fetch(issuer + '/.well-known/openid-configuration'))
require(discovery['issuer'] == issuer, 'Unexpected sandbox issuer')
# Compare exact known local URL before fetching; never follow an arbitrary discovered host.
require(discovery['jwks_uri'] == issuer + '/protocol/openid-connect/certs', 'Unexpected JWKS URL')
keys = json.loads(fetch(discovery['jwks_uri']))['keys']
require(any(key.get('use') == 'sig' and key.get('kid') for key in keys), 'No signing key')
require('S256' in discovery.get('code_challenge_methods_supported', []), 'PKCE S256 not advertised')
print('Docker HTTP smoke passed: API, web, realm issuer, JWKS, advertised PKCE S256.')
print('No login, token validation, verified-email or privileged-session evidence tested.')
