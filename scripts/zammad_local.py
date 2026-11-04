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


def parse_args(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['init', 'up', 'down', 'status', 'provision', 'reset'])
    parser.add_argument('--delete-local-data', action='store_true')
    result = parser.parse_args(args)
    if result.action == 'reset' and not result.delete_local_data:
        parser.error('reset requires --delete-local-data; this removes only the Zammad dataset')
    return result


def compose(*args):
    return subprocess.run(['docker', 'compose', '--project-name', 'swoovr-zammad',
        '--env-file', str(ENV), '-f', str(ROOT / 'containers/zammad/compose.upstream.yaml'),
        '-f', str(ROOT / 'containers/zammad/compose.local.yaml'), *args], check=True)


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
        compose('up', '-d', '--wait', '--wait-timeout', '180')
    elif args.action == 'provision':
        import json
        values = dict(line.split('=', 1) for line in ENV.read_text().splitlines())
        # Only the bootstrap program is copied; secret input stays off argv and logs.
        subprocess.run(['docker', 'cp', str(ROOT / 'containers/zammad/provision.rb'),
            'swoovr-zammad-zammad-railsserver-1:/tmp/swoovr-provision.rb'], check=True)
        result = subprocess.run(['docker', 'exec', '-i', 'swoovr-zammad-zammad-railsserver-1',
            'bundle', 'exec', 'rails', 'runner', '/tmp/swoovr-provision.rb'],
            input=json.dumps({'password': values['SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD'], 'webhook_secret': values['SWOOVR_ZAMMAD_WEBHOOK_SECRET']}),
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
    elif args.action == 'down':
        compose('down')
    elif args.action == 'status':
        compose('ps', '-a')
    elif args.action == 'reset':
        compose('down', '--volumes')


if __name__ == '__main__':
    main()
