#!/usr/bin/env python3
"""Guest support backend acceptance: a disposable Swoovr API (own PostgreSQL database) + real local Zammad + real Mailpit.
Acts as the future public website would (allowed Origin, JSON, Authorization: SwoovrGuest). Fictional .test addresses only.
Codes and grants stay in memory; nothing secret is printed. The owned database is dropped afterwards.
"""
import base64, hashlib, json, os, re, signal, subprocess, sys, time, urllib.error, urllib.parse, urllib.request, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/zammad'))
from webhook_acceptance import psql
PRIVATE = ROOT / '.local/zammad'
BASE = 'http://127.0.0.1:5097'
SITE = 'http://localhost:5099'
MAILPIT = 'http://127.0.0.1:8025/api/v1'
ZAMMAD = 'http://127.0.0.1:8095/api/v1/'


def call(path, method='GET', data=None, grant=None, origin=SITE, headers=None, raw=None):
    h = {**({'Origin': origin} if origin else {}), **(headers or {})}
    body = raw if raw is not None else (None if data is None else json.dumps(data).encode())
    if body is not None and 'Content-Type' not in h: h['Content-Type'] = 'application/json'
    if grant: h['Authorization'] = 'SwoovrGuest ' + grant
    req = urllib.request.Request(BASE + path, data=body, method=method, headers=h)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            text = r.read(); return r.status, (json.loads(text) if text else None), dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read()
        try: value = json.loads(text)
        except ValueError: value = None
        return e.code, value, dict(e.headers)


def zammad(token, path, method='GET', data=None):
    req = urllib.request.Request(ZAMMAD + path, data=None if data is None else json.dumps(data).encode(), method=method,
                                 headers={'Authorization': 'Token token=' + token, 'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=60) as r: return json.loads(r.read())


def mails(address):
    q = urllib.parse.quote('to:"%s"' % address)
    with urllib.request.urlopen(MAILPIT + '/search?query=' + q, timeout=30) as r: return json.loads(r.read())['messages']


def code_for(address, before):
    for _ in range(60):
        found = mails(address)
        if len(found) > before:
            with urllib.request.urlopen(MAILPIT + '/message/' + found[0]['ID'], timeout=30) as r: text = json.loads(r.read())['Text']
            match = re.search(r'\b(\d{8})\b', text)
            return match.group(1), text
        time.sleep(.5)
    raise RuntimeError('No code email arrived in Mailpit')


def main():
    run = uuid.uuid4().hex[:10]
    database = 'swoovr_zammad_guest_' + run
    config = json.loads((PRIVATE / 'provisioned.json').read_text())
    staff_token = next(iter(json.loads((PRIVATE / 'api.json').read_text())['Ticketing']['Zammad']['Staff'].values()))['Token']
    platform = dict(l.split('=', 1) for l in (ROOT / '.local/platform/docker.env').read_text().splitlines() if '=' in l and not l.startswith('#'))
    password = platform['SWOOVR_DB_PASSWORD'].strip("'\"")
    checks = []
    def check(ok, name):
        if not ok: raise RuntimeError('FAIL: ' + name)
        checks.append(name); print('PASS:', name, flush=True)
    env = {**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development', 'ASPNETCORE_URLS': BASE,
           'ConnectionStrings__Swoovr': f'Host=127.0.0.1;Port=5433;Database={database};Username=swoovr;Password={password}',
           'Founders__Secrets__1': os.urandom(32).hex(), 'Keycloak__BaseUrl': 'http://localhost:8180', 'Keycloak__RequireHttpsMetadata': 'false',
           'Retention__Worker': 'false', 'Email__Provider': 'smtp', 'Email__Smtp__Host': '127.0.0.1', 'Email__Smtp__Port': '1025',
           'Ticketing__Zammad__BaseUrl': 'http://127.0.0.1:8095/', 'Ticketing__Zammad__IdentityToken': config['identity_token'],
           'Ticketing__Guest__AllowedOrigins__0': SITE, 'Ticketing__Guest__FrontendUrl': SITE + '/support'}
    log = open(PRIVATE / f'guest-api-{run}.log', 'w'); os.chmod(log.name, 0o600)
    api = None
    try:
        psql('postgres', 'CREATE DATABASE ' + database)
        api = subprocess.Popen(['dotnet', str(ROOT / 'src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll')], cwd=ROOT / 'src/Swoovr.Api', env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        for _ in range(240):
            if api.poll() is not None: raise RuntimeError('API stopped; inspect private guest api log')
            try:
                with urllib.request.urlopen(BASE + '/health/live', timeout=2) as r:
                    if r.status == 200: break
            except OSError: time.sleep(.25)
        else: raise RuntimeError('API readiness timed out')

        # Origin, CSRF and transport rules.
        s, cats, h = call('/api/support/guest/categories')
        check(s == 200 and h.get('Access-Control-Allow-Origin') == SITE and 'Access-Control-Allow-Credentials' not in h and {c['id'] for c in cats} >= {'account', 'recovery'} and len(cats) == 8,
              'categories answer the configured origin, without credentials, as a small public subset')
        s, _, h = call('/api/support/guest/codes', 'OPTIONS', headers={'Access-Control-Request-Method': 'POST'})
        check(s == 204 and 'Authorization' in h.get('Access-Control-Allow-Headers', ''), 'preflight from the allowed origin succeeds')
        s, _, _ = call('/api/support/guest/codes', 'OPTIONS', origin='https://evil.example', headers={'Access-Control-Request-Method': 'POST'})
        check(s == 403, 'preflight from another origin is refused')
        s, _, _ = call('/api/support/guest/codes', 'POST', {'email': 'x@example.test'}, origin='https://evil.example')
        check(s == 403, 'cross-origin code request is refused')
        s, _, _ = call('/api/support/guest/codes', 'POST', raw=b'email=x%40example.test', headers={'Content-Type': 'application/x-www-form-urlencoded'})
        check(s == 415, 'form posts (simple cross-site requests) are refused; JSON only')

        # Non-enumerating code requests and real Mailpit delivery.
        a, b, shared = f'guest-a-{run}@example.test', f'guest-b-{run}@example.test', 'fictional.alex@example.test'
        before = {x: len(mails(x)) for x in (a, b, shared)}
        s1, r1, _ = call('/api/support/guest/codes', 'POST', {'email': a})
        s2, r2, _ = call('/api/support/guest/codes', 'POST', {'email': shared})
        check(s1 == s2 == 202 and r1 == r2 == {'status': 'sent-if-allowed'}, 'new and member-owned addresses get the identical answer')
        s, _, _ = call('/api/support/guest/codes', 'POST', {'email': 'not-an-address'})
        check(s == 400, 'malformed address is a format error only')
        code_a, text = code_for(a, before[a])
        links = [u.rstrip(').,') for u in re.findall(r'https?://\S+', text)]
        check(code_a in text and links == [SITE + '/support'], 'email carries the code and only the configured page (no token in any link)')
        s, r, _ = call('/api/support/guest/codes', 'POST', {'email': a})
        time.sleep(2)
        check(s == 202 and r == {'status': 'sent-if-allowed'} and len(mails(a)) == before[a] + 1, 'a second code within a minute is silently not sent')

        # Exchange: POST only, single use, wrong codes fail generically.
        s, _, _ = call('/api/support/guest/grants', 'GET')
        check(s == 405, 'grant exchange is POST only (link scanners cannot consume codes)')
        wrong = '00000000' if code_a != '00000000' else '11111111'
        s, r, _ = call('/api/support/guest/grants', 'POST', {'email': a, 'code': wrong})
        check(s == 400 and r['code'] == 'guest-code-invalid', 'wrong code fails generically')
        s, r, _ = call('/api/support/guest/grants', 'POST', {'email': a, 'code': code_a})
        check(s == 200 and len(r['grant']) == 43, 'correct code exchanges once for a grant')
        grant_a = r['grant']
        s, r, _ = call('/api/support/guest/grants', 'POST', {'email': a, 'code': code_a})
        check(s == 400 and r['code'] == 'guest-code-invalid', 'replayed code fails')
        digest = hashlib.sha256(('swoovr:support-guest-grant:v1|' + grant_a).encode()).hexdigest()
        stored = psql(database, "SELECT count(*) FILTER (WHERE encode(token_hash,'hex') = '" + digest + "'), count(*) FILTER (WHERE position(convert_to('" + grant_a + "','UTF8') in token_hash) > 0) FROM ticketing_guest_grant")
        check(stored == '1|0', 'grant is stored only as its hash')

        # Guest A creates and follows up without any membership.
        s, r, _ = call('/api/support/guest/tickets', grant=grant_a)
        check(s == 200 and r['items'] == [], 'a new guest has no requests')
        op = str(uuid.uuid4())
        create = {'operationId': op, 'category': 'recovery', 'title': f'Cannot sign in {run}', 'body': f'Fictional guest {run}: my password reset email never arrives.'}
        s, t, _ = call('/api/support/guest/tickets', 'POST', create, grant=grant_a)
        check(s == 200 and t['reference'] == 'TK-%d' % t['id'], 'guest submits a request without a membership')
        s, again, _ = call('/api/support/guest/tickets', 'POST', create, grant=grant_a)
        check(s == 200 and again['id'] == t['id'], 'same operation key returns the same request (no duplicate)')
        s, r, _ = call('/api/support/guest/tickets', 'POST', {**create, 'body': 'changed'}, grant=grant_a)
        check(s == 409, 'same key with a different payload is refused')
        s, r, _ = call('/api/support/guest/tickets', 'POST', {**create, 'operationId': str(uuid.uuid4()), 'category': 'Legal & Law Enforcement'}, grant=grant_a)
        check(s == 400, 'internal queue names are not public categories')
        ticket = zammad(staff_token, 'tickets/%d' % t['id'])
        group = zammad(config['provisioning_token'], 'groups/%d' % ticket['group_id'])['name']
        customer = zammad(config['provisioning_token'], 'users/%d' % ticket['customer_id'])
        check(group == 'Account Recovery' and customer['login'].startswith('swoovr-guest-') and not customer.get('email') and customer['role_ids'] == [config['customer_role_id']],
              'routed to Account Recovery as a Customer-only guest with no helpdesk email')

        # Staff answer in the helpdesk: a public reply and a private note.
        note, reply = f'Private note {run}: check mail logs.', f'Public reply {run}: check your spam folder.'
        zammad(staff_token, 'ticket_articles', 'POST', {'ticket_id': t['id'], 'body': note, 'type': 'note', 'internal': True, 'content_type': 'text/plain'})
        zammad(staff_token, 'ticket_articles', 'POST', {'ticket_id': t['id'], 'body': reply, 'type': 'note', 'internal': False, 'content_type': 'text/plain'})
        s, d, _ = call('/api/support/guest/tickets/%d' % t['id'], grant=grant_a)
        bodies = ' '.join(x['body'] for x in d['articles'])
        check(s == 200 and reply in bodies and note not in bodies and not any(x['private'] for x in d['articles']), 'guest sees the public reply and never the private note')
        rop = str(uuid.uuid4())
        upload = {'filename': 'details.txt', 'contentType': 'text/plain', 'data': base64.b64encode(f'Fictional details {run}'.encode()).decode()}
        s, d, _ = call('/api/support/guest/tickets/%d/replies' % t['id'], 'POST', {'operationId': rop, 'body': f'Thanks {run}', 'attachments': [upload]}, grant=grant_a)
        check(s == 200 and any(f'Thanks {run}' in x['body'] for x in d['articles']), 'guest replies in the same conversation')
        s, d2, _ = call('/api/support/guest/tickets/%d/replies' % t['id'], 'POST', {'operationId': rop, 'body': f'Thanks {run}', 'attachments': [upload]}, grant=grant_a)
        check(s == 200 and sum(f'Thanks {run}' in x['body'] for x in d2['articles']) == 1, 'replayed reply key does not duplicate')
        art = next(x for x in d2['articles'] if x['attachments'])
        s, f, _ = call('/api/support/guest/tickets/%d/articles/%d/attachments/%d' % (t['id'], art['id'], art['attachments'][0]['id']), grant=grant_a)
        check(s == 200 and base64.b64decode(f['data']).decode() == f'Fictional details {run}', 'guest downloads their own text attachment')
        s, _, _ = call('/api/support/guest/tickets/%d/replies' % t['id'], 'POST', {'operationId': str(uuid.uuid4()), 'body': 'x', 'private': True}, grant=grant_a)
        check(s == 403, 'guests cannot write private notes')

        # Guest B is isolated from guest A.
        before[b] = len(mails(b))
        call('/api/support/guest/codes', 'POST', {'email': b})
        code_b, _ = code_for(b, before[b])
        grant_b = call('/api/support/guest/grants', 'POST', {'email': b, 'code': code_b})[1]['grant']
        s1 = call('/api/support/guest/tickets/%d' % t['id'], grant=grant_b)[0]
        s2 = call('/api/support/guest/tickets/%d/replies' % t['id'], 'POST', {'operationId': str(uuid.uuid4()), 'body': 'hijack'}, grant=grant_b)[0]
        s3, lb, _ = call('/api/support/guest/tickets', grant=grant_b)
        check(s1 == 404 and s2 == 404 and s3 == 200 and lb['items'] == [], "guest B cannot read, reply to or list guest A's request")
        s, _, _ = call('/api/support/guest/tickets/%d' % t['id'])
        check(s == 401, 'no grant, no access (ticket numbers are not credentials)')
        s, _, _ = call('/api/support/guest/tickets', headers={'Authorization': 'Bearer ' + grant_a})
        check(s == 401, 'grant only in its own scheme, never as a member bearer token')

        # Same address as a member: a separate guest, never merged.
        code_s, _ = code_for(shared, before[shared])
        grant_s = call('/api/support/guest/grants', 'POST', {'email': shared, 'code': code_s})[1]['grant']
        s, ts, _ = call('/api/support/guest/tickets', 'POST', {'operationId': str(uuid.uuid4()), 'category': 'account', 'title': f'Shared {run}', 'body': 'Fictional shared-address guest.'}, grant=grant_s)
        owner = zammad(config['provisioning_token'], 'users/%d' % zammad(staff_token, 'tickets/%d' % ts['id'])['customer_id'])
        members = [u for u in zammad(config['provisioning_token'], 'users/search?query=' + urllib.parse.quote('email:"%s"' % shared)) if u.get('email') == shared]
        check(s == 200 and owner['login'].startswith('swoovr-guest-') and all(m['id'] != owner['id'] for m in members), "a guest using a member's address gets its own customer; nothing is merged")

        # Revocation and expiry.
        s, _, _ = call('/api/support/guest/grants/current', 'DELETE', grant=grant_a)
        check(s == 204 and call('/api/support/guest/tickets', grant=grant_a)[0] == 401, 'revoked grant stops working immediately')
        psql(database, "UPDATE ticketing_guest_grant SET expires_at = now() - interval '1 second' WHERE revoked_at IS NULL")
        check(call('/api/support/guest/tickets', grant=grant_b)[0] == 401, 'expired grant stops working')
        print(f'PASS: {len(checks)} guest backend acceptance checks (real Zammad and Mailpit, disposable database).')
    finally:
        if api and api.poll() is None:
            os.killpg(api.pid, signal.SIGTERM)
            try: api.wait(timeout=15)
            except subprocess.TimeoutExpired: os.killpg(api.pid, signal.SIGKILL)
        log.close()
        try: psql('postgres', 'DROP DATABASE IF EXISTS ' + database + ' WITH (FORCE)')
        except Exception: pass


if __name__ == '__main__':
    main()
