#!/usr/bin/env python3
"""Real Zammad trigger -> Swoovr API -> disposable PostgreSQL inbox acceptance."""
import hashlib
import hmac
import json
import os
from pathlib import Path
import signal
import subprocess
import time
import urllib.request
import urllib.error
import uuid
from provider_acceptance import request as provider_request

ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / '.local/zammad'

def psql(database, sql):
    return subprocess.run(['docker', 'exec', 'swoovr-platform-postgres-1', 'psql', '-p', '5433',
        '-U', 'swoovr', '-d', database, '-At', '-v', 'ON_ERROR_STOP=1', '-c', sql],
        check=True, capture_output=True, text=True).stdout.strip()

def main():
    config = json.loads((PRIVATE / 'provisioned.json').read_text())
    fixture = json.loads((PRIVATE / 'fixtures.json').read_text())
    values = dict(line.split('=', 1) for line in (PRIVATE / 'runtime.env').read_text().splitlines())
    platform = dict(line.split('=', 1) for line in (ROOT / '.local/platform/docker.env').read_text().splitlines() if '=' in line and not line.startswith('#'))
    database = 'swoovr_zammad_' + uuid.uuid4().hex[:12]
    psql('postgres', 'CREATE DATABASE ' + database)
    environment = os.environ.copy()
    environment.update({'ASPNETCORE_ENVIRONMENT': 'Development', 'ASPNETCORE_URLS': 'http://0.0.0.0:5095',
        'ConnectionStrings__Swoovr': 'Host=127.0.0.1;Port=5433;Database=' + database + ';Username=swoovr;Password=' + platform['SWOOVR_DB_PASSWORD'],
        'Ticketing__Zammad__WebhookSecret': values['SWOOVR_ZAMMAD_WEBHOOK_SECRET'], 'Retention__Worker': 'false'})
    log_path = PRIVATE / 'webhook-api.log'
    descriptor = os.open(log_path, os.O_WRONLY | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    os.fchmod(descriptor, 0o600)
    os.ftruncate(descriptor, 0)
    log = os.fdopen(descriptor, 'w')
    api = subprocess.Popen(['dotnet', str(ROOT / 'src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll')],
        cwd=ROOT / 'src/Swoovr.Api', env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    admin = config['provisioning_token']
    webhook_id = trigger_id = None
    try:
        for _ in range(90):
            if api.poll() is not None: raise RuntimeError('API failed; inspect private webhook-api.log')
            try:
                with urllib.request.urlopen('http://127.0.0.1:5095/health/live', timeout=1): break
            except (OSError, urllib.error.URLError): time.sleep(1)
        else: raise RuntimeError('API readiness timeout')
        code, webhooks = provider_request(admin, 'GET', 'webhooks')
        assert code == 200
        webhook = next(row for row in webhooks if row['name'] == 'Swoovr events')
        webhook_id = webhook['id']
        code, _ = provider_request(admin, 'PUT', f'webhooks/{webhook_id}', {'endpoint': 'http://host.docker.internal:5095/api/integrations/zammad/events', 'active': True})
        assert code == 200, 'enable local webhook'
        code, triggers = provider_request(admin, 'GET', 'triggers')
        assert code == 200
        trigger_id = next(row['id'] for row in triggers if row['name'] == 'Swoovr correspondence events')
        code, _ = provider_request(admin, 'PUT', f'triggers/{trigger_id}', {'active': True})
        assert code == 200
        code, ticket = provider_request(fixture['support']['token'], 'POST', 'tickets', {'title': 'Fictional webhook enquiry',
            'group_id': config['groups']['General Support'], 'customer_id': fixture['customer_a']['id'],
            'article': {'body': 'Ordinary fictional enquiry.', 'type': 'note', 'content_type': 'text/plain', 'internal': False, 'sender': 'Customer'}})
        assert code == 201
        for _ in range(60):
            count = int(psql(database, f'SELECT count(*) FROM ticketing_webhook_work WHERE provider_ticket_id={ticket["id"]}'))
            if count: break
            time.sleep(1)
        else: raise RuntimeError('real signed trigger delivery not persisted')
        print('PASS: real signed Zammad trigger persisted by Swoovr API before acknowledgment')
        # Replay and malformed signatures through the actual HTTP boundary.
        delivery = str(uuid.uuid4())
        body = json.dumps({'ticket': {'id': ticket['id'], 'updated_at': '2026-11-04T00:00:00Z'}, 'article': None}).encode()
        signature = 'sha1=' + hmac.new(values['SWOOVR_ZAMMAD_WEBHOOK_SECRET'].encode(), body, hashlib.sha1).hexdigest()
        def post(payload, sig):
            req = urllib.request.Request('http://127.0.0.1:5095/api/integrations/zammad/events', payload,
                headers={'Content-Type': 'application/json', 'X-Zammad-Delivery': delivery, 'X-Hub-Signature': sig})
            try:
                with urllib.request.urlopen(req, timeout=10) as response: return response.status
            except urllib.error.HTTPError as error: return error.code
        assert post(body, signature) == 204
        assert post(body, signature) == 204
        delivery = str(uuid.uuid4())
        assert post(body, signature) == 204
        digest = hashlib.sha256(body).hexdigest()
        assert int(psql(database, f"SELECT count(*) FROM ticketing_webhook_work WHERE body_digest='{digest}'")) == 1
        # Reuse a known delivery for the conflicting-body check.
        delivery = psql(database, f"SELECT delivery_id FROM ticketing_webhook_work WHERE body_digest='{digest}'")
        assert int(psql(database, f"SELECT count(*) FROM ticketing_webhook_work WHERE delivery_id='{delivery}'")) == 1
        assert post(body + b' ', signature) == 401
        changed = body + b' '
        changed_signature = 'sha1=' + hmac.new(values['SWOOVR_ZAMMAD_WEBHOOK_SECRET'].encode(), changed, hashlib.sha1).hexdigest()
        assert post(changed, changed_signature) == 409
        print('PASS: article-free events, exact replay deduplication, tamper denial, conflicting delivery denial')
    finally:
        if trigger_id: provider_request(admin, 'PUT', f'triggers/{trigger_id}', {'active': False})
        if webhook_id: provider_request(admin, 'PUT', f'webhooks/{webhook_id}', {'active': False, 'endpoint': 'http://swoovr-platform:5080/api/integrations/zammad/events'})
        if api.poll() is None:
            os.killpg(api.pid, signal.SIGTERM)
            try: api.wait(timeout=15)
            except subprocess.TimeoutExpired:
                os.killpg(api.pid, signal.SIGKILL); api.wait()
        log.close()
        psql('postgres', f'DROP DATABASE {database} WITH (FORCE)')

if __name__ == '__main__':
    main()
