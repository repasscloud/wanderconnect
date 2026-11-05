#!/usr/bin/env python3
"""Real local provider capability tests. Fictional adults only; persistent fixtures are isolated in Zammad."""
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import urllib.error
import uuid

ROOT = Path(__file__).resolve().parents[2]
PRIVATE = ROOT / '.local/zammad'

def request(token, method, path, data=None):
    req = urllib.request.Request('http://127.0.0.1:8095/api/v1/' + path,
        data=json.dumps(data).encode() if data is not None else None, method=method,
        headers={'Authorization': 'Token token=' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        # Provider errors can contain personal data; acceptance reports status only.
        return error.code, None


def main():
    config = json.loads((PRIVATE / 'provisioned.json').read_text())
    subprocess.run(['docker', 'cp', str(ROOT / 'containers/zammad/acceptance-fixtures.rb'),
        'swoovr-zammad-zammad-railsserver-1:/tmp/swoovr-acceptance.rb'], check=True)
    result = subprocess.run(['docker', 'exec', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec',
        'rails', 'runner', '/tmp/swoovr-acceptance.rb'], capture_output=True, text=True, check=True)
    prefix = 'SWOOVR_ACCEPTANCE_FIXTURES='
    fixtures = json.loads(next(line[len(prefix):] for line in result.stdout.splitlines() if line.startswith(prefix)))
    descriptor = os.open(PRIVATE / 'fixtures.json', os.O_CREAT | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    os.fchmod(descriptor, 0o600)
    os.ftruncate(descriptor, 0)
    with os.fdopen(descriptor, 'w') as stream:
        json.dump(fixtures, stream)
    passed = []
    def check(condition, name):
        if not condition:
            raise RuntimeError('FAIL: ' + name)
        passed.append(name)
        print('PASS:', name)
    admin = config['provisioning_token']
    support = fixtures['support']['token']
    customer = fixtures['customer_a']['token']
    other = fixtures['customer_b']['token']
    code, ticket = request(support, 'POST', 'tickets', {'title': 'Fictional support enquiry',
        'group_id': config['groups']['General Support'], 'customer_id': fixtures['customer_a']['id'],
        'swoovr_operation': str(uuid.uuid4()), 'article': {'body': 'Please help with this fictional enquiry.',
        'type': 'note', 'content_type': 'text/plain', 'internal': False, 'sender': 'Customer'}})
    check(code == 201, 'agent creates ticket with explicit customer and operation reference')
    ticket_id = ticket['id']
    code, _ = request(customer, 'GET', f'tickets/{ticket_id}')
    check(code == 200, 'owning customer reads ticket')
    code, _ = request(other, 'GET', f'tickets/{ticket_id}')
    check(code in (403, 404), 'other customer cannot read ticket')
    code, note = request(support, 'POST', 'ticket_articles', {'ticket_id': ticket_id, 'body': 'Private fictional staff note.',
        'content_type': 'text/plain', 'type': 'note', 'internal': True, 'sender': 'Agent',
        'attachments': [{'filename': 'private.txt', 'data': 'cHJpdmF0ZQ==', 'mime-type': 'text/plain'}]})
    check(code == 201, 'agent creates private note with attachment')
    code, articles = request(customer, 'GET', f'ticket_articles/by_ticket/{ticket_id}')
    check(code == 200 and all(not row['internal'] and row['id'] != note['id'] for row in articles), 'customer article list excludes private notes and attachments')
    code, _ = request(customer, 'GET', f'ticket_articles/{note["id"]}')
    check(code in (403, 404), 'customer direct private article access denied')
    attachment_id = note['attachments'][0]['id']
    code, _ = request(customer, 'GET', f'ticket_attachment/{ticket_id}/{note["id"]}/{attachment_id}')
    check(code in (403, 404), 'customer direct private attachment access denied')
    code, reply = request(support, 'POST', 'ticket_articles', {'ticket_id': ticket_id, 'body': 'Public fictional reply.',
        'content_type': 'text/plain', 'type': 'note', 'internal': False, 'sender': 'Agent'})
    check(code == 201, 'agent creates public reply')
    code, articles = request(customer, 'GET', f'ticket_articles/by_ticket/{ticket_id}')
    check(code == 200 and any(row['id'] == reply['id'] for row in articles), 'customer reads public reply')
    code, _ = request(support, 'PUT', f'tickets/{ticket_id}', {'group_id': config['groups']['Trust & Safety — Restricted']})
    check(code == 200, 'agent transfers to create-only restricted group')
    code, _ = request(support, 'GET', f'tickets/{ticket_id}')
    check(code in (403, 404), 'transfer immediately removes source agent access')
    code, _ = request(fixtures['restricted']['token'], 'GET', f'tickets/{ticket_id}')
    check(code == 200, 'restricted agent reads transferred ticket')
    code, _ = request(config['identity_token'], 'GET', f'tickets/{ticket_id}')
    check(code in (403, 404), 'identity lifecycle token cannot read ticket content')
    report = {'version': config['version'], 'checks': passed, 'ticket_id': ticket_id}
    (PRIVATE / 'provider-acceptance.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'PASS: {len(passed)} real Zammad capability checks.')

if __name__ == '__main__':
    main()
