#!/usr/bin/env python3
"""Launch built portable hosts, assert HTTP responses, always clean up processes."""
import base64
import json
import http.client
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from web_smoke import check_web_assets

root = Path(__file__).resolve().parents[1]
processes = []
def start(name, port):
    log = tempfile.TemporaryFile()
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=f'http://127.0.0.1:{port}')
    process = subprocess.Popen(['dotnet',str(root/f'src/Swoovr.{name}/bin/Release/net10.0/Swoovr.{name}.dll')],cwd=root/f'src/Swoovr.{name}',env=env,stdout=log,stderr=log)
    processes.append((process,log))
    url = f'http://127.0.0.1:{port}/'
    for _ in range(100):
        if process.poll() is not None:
            log.seek(0);raise RuntimeError(log.read().decode())
        try:
            urllib.request.urlopen(url + ('health/live' if name=='Api' else ''),timeout=1).close()
            return url
        except (OSError,urllib.error.URLError):time.sleep(.1)
    raise RuntimeError(f'{name} did not start')
try:
    api = start('Api',5187)
    web = start('Web',5188)
    with urllib.request.urlopen(api+'health/live') as r: assert json.load(r)=={'status':'ok'}
    with urllib.request.urlopen(api+'api/status') as r: assert json.load(r)=={'service':'Swoovr.Api','stage':'foundation'}
    # D211/D222: one fixed default, strict explicit dates; errors do not echo attacker input.
    for version in [None, '2026-10-28']:
        request = urllib.request.Request(api+'api/status', headers={} if version is None else {'Swoovr-Version': version})
        with urllib.request.urlopen(request) as r:
            assert r.headers['Swoovr-Version'] == '2026-10-28'
            assert 'Swoovr-Version' in r.headers.get('Vary', '')
    for version, code in [('v1', 'invalid-api-version'), ('2026-02-30', 'invalid-api-version'),
                          ('2026-1-28', 'invalid-api-version'), ('2026-10-28, 2026-10-28', 'invalid-api-version'),
                          ('2026-10-29', 'unsupported-api-version'), ('2020-01-01', 'unsupported-api-version')]:
        request = urllib.request.Request(api+'api/status', headers={'Swoovr-Version': version})
        try: urllib.request.urlopen(request); raise AssertionError('invalid version accepted')
        except urllib.error.HTTPError as error:
            assert error.code == 400
            assert error.headers.get('Cache-Control') == 'no-store'
            assert error.headers.get_content_type() == 'application/problem+json'
            body = error.read().decode()
            assert version not in body
            assert json.loads(body) == {'code': code, 'supportedVersions': ['2026-10-28']}
    duplicate = http.client.HTTPConnection('127.0.0.1', 5187)
    duplicate.putrequest('GET', '/api/status')
    duplicate.putheader('Swoovr-Version', '2026-10-28')
    duplicate.putheader('Swoovr-Version', '2026-10-28')
    duplicate.endheaders()
    response = duplicate.getresponse()
    assert response.status == 400 and json.load(response)['code'] == 'invalid-api-version'
    duplicate.close()
    try: urllib.request.urlopen(api+'api/v1/status'); raise AssertionError('legacy path still supported')
    except urllib.error.HTTPError as error: assert error.code == 404
    # Health and SignalR protocol paths have no date contract.
    with urllib.request.urlopen(urllib.request.Request(api+'health/live', headers={'Swoovr-Version': 'invalid'})) as r:
        assert r.status == 200
    with urllib.request.urlopen(web) as r:
        html = r.read().decode()
        assert 'Swoovr' in html and 'not connected' in html
    check_web_assets(web)
    # Without a token, or with a malformed one, the session endpoint is 401 and never 500.
    # The Basic value is random per run: a wrong scheme, never a stored credential.
    for header in [None, 'Bearer not.a.token', 'Bearer abc', 'Basic ' + base64.b64encode(os.urandom(12)).decode()]:
        request = urllib.request.Request(api+'api/session', headers={'Authorization': header} if header else {})
        try: urllib.request.urlopen(request); raise AssertionError('session must require a valid token')
        except urllib.error.HTTPError as error: assert error.code == 401, (header, error.code)
    # An unconfigured web host keeps sign-in off and never redirects to a provider.
    with urllib.request.urlopen(web+'account') as r: assert "Sign-in isn't configured" in r.read().decode()
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *args): return None
    try: urllib.request.build_opener(NoRedirect).open(web+'account/sign-in'); raise AssertionError('expected redirect')
    except urllib.error.HTTPError as error: assert error.code == 302 and error.headers['Location'] == '/account', error.headers['Location']
    print('PASS: appearance route and linked runtime scripts/styles.')
    print('PASS: session endpoint 401 for missing/malformed tokens; unconfigured web sign-in stays off.')
    print('PASS: API liveness/status JSON and Blazor foundation HTTP response.')
finally:
    for p,log in processes:
        p.terminate()
        try:p.wait(timeout=5)
        except subprocess.TimeoutExpired:p.kill();p.wait()
        log.close()
