#!/usr/bin/env python3
"""Manage the isolated local Zammad stack. Secrets stay in ignored .local/zammad."""
import argparse
import os
from pathlib import Path
import secrets
import subprocess

ROOT = Path(__file__).resolve().parents[1]
ENV = ROOT / '.local/zammad/runtime.env'


def private_write(path, content):
    """Atomic replacement never follows an existing symlink or inherits public permissions."""
    import tempfile
    descriptor, temporary = tempfile.mkstemp(prefix='.private-', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def initialize(path=ENV):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    if path.is_symlink():
        raise SystemExit('Runtime environment must not be a symlink.')
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        if path.stat().st_mode & 0o077:
            raise SystemExit('Runtime environment must have permissions 0600.')
        return
    values = {
        'POSTGRES_PASS': secrets.token_hex(32),
        'POSTGRES_SUPERUSER_PASS': secrets.token_hex(32),
        'SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD': secrets.token_hex(32),
        'SWOOVR_ZAMMAD_WEBHOOK_SECRET': secrets.token_hex(32),
        'VERSION': '7.2.2-0000',
        'ZAMMAD_FQDN': 'localhost:8095',
        'ZAMMAD_HTTP_TYPE': 'http',
        'TZ': 'Australia/Adelaide',
    }
    with os.fdopen(descriptor, 'w') as stream:
        stream.write(''.join(f'{key}={value}\n' for key, value in values.items()))


def local_tls():
    """Throwaway self-signed "localhost" certificate for the local Keycloak forwarder, plus a CA bundle (the pinned image's
    system CAs + this certificate) that only Zammad's Rails container uses. Local only; never a real credential."""
    target = ENV.parent / 'tls'
    target.mkdir(mode=0o700, exist_ok=True)
    if not (target / 'bundle.pem').exists():
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '825', '-subj', '/CN=localhost',
            '-addext', 'subjectAltName=DNS:localhost,IP:127.0.0.1', '-keyout', str(target / 'key.pem'), '-out', str(target / 'cert.pem')],
            check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        image = 'ghcr.io/zammad/zammad@sha256:17dec4f83fc18bad7195d7d44df8dca35e04df882e6315385adf230ea759a614'
        system = subprocess.run(['docker', 'run', '--rm', '--entrypoint', 'cat', image, '/etc/ssl/certs/ca-certificates.crt'], capture_output=True, text=True, check=True).stdout
        private_write(target / 'bundle.pem', system + (target / 'cert.pem').read_text())
        # Readable by the containers' own user through the bind mount; the directory itself stays private.
        for name in ('key.pem', 'cert.pem', 'bundle.pem'): os.chmod(target / name, 0o644)


def parse_args(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['init', 'up', 'down', 'status', 'provision', 'runtime', 'reset'])
    parser.add_argument('--delete-local-data', action='store_true')
    parser.add_argument('--events', action='store_true', help='provision: activate the signed webhook and trigger (the API must have the matching secret)')
    result = parser.parse_args(args)
    if result.action == 'reset' and not result.delete_local_data:
        parser.error('reset requires --delete-local-data; this removes only the Zammad dataset')
    return result


def compose(*args):
    return subprocess.run(['docker', 'compose', '--project-name', 'swoovr-zammad',
        '--env-file', str(ENV), '-f', str(ROOT / 'containers/zammad/compose.upstream.yaml'),
        '-f', str(ROOT / 'containers/zammad/compose.local.yaml'), *args], check=True)


STAFF_REALM = 'swoovr-staff-dev'
KEYCLOAK = 'http://localhost:8180'
SSO_CLIENT = 'swoovr-zammad'


def keycloak_admin():
    """Local bootstrap admin from the running Keycloak container's environment (never printed)."""
    import json, urllib.parse, urllib.request
    container = json.loads(subprocess.check_output(['docker', 'inspect', 'swoovr-platform-keycloak-1']))[0]
    env = dict(line.split('=', 1) for line in container['Config']['Env'] if '=' in line)
    body = urllib.parse.urlencode({'grant_type': 'password', 'client_id': 'admin-cli', 'username': env['KC_BOOTSTRAP_ADMIN_USERNAME'], 'password': env['KC_BOOTSTRAP_ADMIN_PASSWORD']}).encode()
    with urllib.request.urlopen(urllib.request.Request(KEYCLOAK + '/realms/master/protocol/openid-connect/token', body), timeout=15) as response:
        token = json.load(response)['access_token']
    def call(path, method='GET', data=None):
        request = urllib.request.Request(KEYCLOAK + '/admin/realms/' + path, None if data is None else json.dumps(data).encode(), method=method,
            headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
        with urllib.request.urlopen(request, timeout=15) as response:
            raw = response.read()
            return json.loads(raw) if raw else None
    return call


def sso_client(realm=STAFF_REALM):
    """The staff realm's public PKCE client for Zammad's web sign-in (idempotent). Redirects only to the local Zammad."""
    call = keycloak_admin()
    # Same definition as the realm import (scripts/keycloak_realms.py), applied to an already-imported realm.
    client = {'clientId': SSO_CLIENT, 'name': 'Swoovr helpdesk (local)', 'enabled': True, 'publicClient': True, 'clientAuthenticatorType': 'none',
        'standardFlowEnabled': True, 'directAccessGrantsEnabled': False, 'implicitFlowEnabled': False, 'serviceAccountsEnabled': False,
        'frontchannelLogout': False, 'fullScopeAllowed': False,
        'redirectUris': ['http://localhost:8095/auth/openid_connect/callback'], 'webOrigins': ['http://localhost:8095'],
        'attributes': {'pkce.code.challenge.method': 'S256', 'post.logout.redirect.uris': 'http://localhost:8095/*',
                       'standard.token.exchange.enabled': 'false', 'oauth2.device.authorization.grant.enabled': 'false'}}
    existing = call(f'{realm}/clients?clientId={SSO_CLIENT}')
    if existing: call(f'{realm}/clients/{existing[0]["id"]}', 'PUT', {**existing[0], **client})
    else: call(f'{realm}/clients', 'POST', client)
    return {'display_name': 'Swoovr staff sign-in', 'client_id': SSO_CLIENT, 'issuer': f'{KEYCLOAK}/realms/{realm}'}


def api_settings(config, output=None):
    """Private API configuration: helpdesk credentials only. Staff tokens are issued by the API's own staff sync (Q133)."""
    import json
    target = output or ENV.parent
    zammad = {'BaseUrl': 'http://swoovr-zammad:8080/', 'IdentityToken': config['identity_token'], 'StaffDirectoryToken': config['staff_directory_token']}
    # D337 (#224): the retention identity (ticket metadata + data privacy deletion).
    if config.get('retention_token'): zammad['RetentionToken'] = config['retention_token']
    settings = {'Ticketing': {'Zammad': zammad}}
    private_write(target / 'api.json', json.dumps(settings))
    lines = ['Ticketing__Zammad__' + key + '=' + value for key, value in zammad.items()]
    secret = dict(line.split('=', 1) for line in ENV.read_text().splitlines() if '=' in line).get('SWOOVR_ZAMMAD_WEBHOOK_SECRET')
    if secret: lines.append('Ticketing__Zammad__WebhookSecret=' + secret)
    # Local guest support: a placeholder website origin for the disposable local harness (the real website is separate).
    lines.extend(['Ticketing__Guest__AllowedOrigins__0=http://localhost:5099', 'Ticketing__Guest__FrontendUrl=http://localhost:5099/support'])
    # Local only: a short missed-event sweep so reconciliation can be observed (default 10 minutes).
    lines.append('Ticketing__Events__ReconcileMinutes=2')
    # Keep private lines this script doesn't manage (for example the owner's local push settings), after its own.
    managed = {line.split('=', 1)[0] for line in lines}
    existing = (target / 'api.env').read_text().splitlines() if (target / 'api.env').exists() else []
    kept = [line for line in existing if line.split('=', 1)[0] not in managed and not line.startswith('Ticketing__')]
    private_write(target / 'api.env', '\n'.join(lines + kept) + '\n')
    return settings


def main():
    args = parse_args()
    initialize()
    if args.action == 'init':
        print('Private runtime configuration initialized.')
    elif args.action == 'up':
        exists = subprocess.run(['docker', 'network', 'inspect', 'swoovr-ticketing'],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
        if not exists:
            subprocess.run(['docker', 'network', 'create', 'swoovr-ticketing'], check=True)
        # Connect only the known platform anchor; the original launcher/issuers remain intact.
        anchor = subprocess.run(['docker', 'inspect', 'swoovr-platform-mailpit-1'],
            capture_output=True, text=True)
        if anchor.returncode == 0:
            import json
            networks = json.loads(anchor.stdout)[0]['NetworkSettings']['Networks']
            if 'swoovr-ticketing' not in networks:
                subprocess.run(['docker', 'network', 'connect', '--alias', 'swoovr-platform',
                    'swoovr-ticketing', 'swoovr-platform-mailpit-1'], check=True)
        local_tls()
        compose('up', '-d', '--wait', '--wait-timeout', '180')
    elif args.action == 'provision':
        import json
        values = dict(line.split('=', 1) for line in ENV.read_text().splitlines())
        # Only the bootstrap program is copied; secret input stays off argv and logs.
        subprocess.run(['docker', 'cp', str(ROOT / 'containers/zammad/provision.rb'),
            'swoovr-zammad-zammad-railsserver-1:/tmp/swoovr-provision.rb'], check=True)
        result = subprocess.run(['docker', 'exec', '-i', 'swoovr-zammad-zammad-railsserver-1',
            'bundle', 'exec', 'rails', 'runner', '/tmp/swoovr-provision.rb'],
            input=json.dumps({'password': values['SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD'], 'webhook_secret': values['SWOOVR_ZAMMAD_WEBHOOK_SECRET'], 'events_active': args.events,
                'sso': sso_client()}),
            capture_output=True, text=True)
        if result.returncode:
            # Rails errors can echo input attributes. Keep complete diagnostics private.
            diagnostic = ENV.parent / 'provision-error.log'
            private_write(diagnostic, result.stdout + result.stderr)
            raise SystemExit('Provisioning failed; inspect private .local/zammad/provision-error.log.')
        prefix = 'SWOOVR_PROVISION_RESULT='
        line = next(line for line in result.stdout.splitlines() if line.startswith(prefix))
        data = json.loads(line[len(prefix):])
        target = ENV.parent / 'provisioned.json'
        private_write(target, json.dumps(data))
        print('Provisioned Zammad ' + data['version'] + '; groups: ' + str(len(data['groups'])))
    elif args.action == 'runtime':
        import json
        api_settings(json.loads((ENV.parent / 'provisioned.json').read_text()))
        print('Wrote private API configuration. Staff are provisioned in Zammad by the API itself.')
    elif args.action == 'down':
        compose('down')
    elif args.action == 'status':
        compose('ps', '-a')
    elif args.action == 'reset':
        compose('down', '--volumes')


if __name__ == '__main__':
    main()
