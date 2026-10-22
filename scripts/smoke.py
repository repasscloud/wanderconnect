#!/usr/bin/env python3
"""Launch built portable hosts, assert HTTP responses, always clean up processes."""
import base64
import json
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
    with urllib.request.urlopen(api+'api/v1/status') as r: assert json.load(r)=={'service':'Swoovr.Api','stage':'foundation'}
    with urllib.request.urlopen(web) as r:
        html = r.read().decode()
        assert 'Swoovr' in html and 'not connected' in html
    check_web_assets(web)
    # Without a token, or with a malformed one, the session endpoint is 401 and never 500.
    # The Basic value is random per run: a wrong scheme, never a stored credential.
    for header in [None, 'Bearer not.a.token', 'Bearer abc', 'Basic ' + base64.b64encode(os.urandom(12)).decode()]:
        request = urllib.request.Request(api+'api/v1/session', headers={'Authorization': header} if header else {})
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
