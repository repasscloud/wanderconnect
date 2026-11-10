#!/usr/bin/env python3
"""Private env-file helper for the Vultr UAT host. Never prints secret values.

  init                 create env/*.env from env/*.env.example, generating every __GENERATE_*:<name>__ once
                       (the same <name> gets the same value in every file); existing env files are never touched
  check [--bootstrap]  refuse unfilled placeholders, wrong permissions or tag-based images in release.env
                       (--bootstrap allows the Zammad tokens that bootstrap itself fills)
  realms <directory>   load the three realm JSON templates from scripts/uat/keycloak_realms.py into env/realms.env
  provision-input      print the JSON that containers/zammad/provision.rb reads on stdin
  provision-output     read provision.rb stdout on stdin and store its tokens in env/api.env
"""
import base64, json, os, re, secrets, string, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ENV = ROOT / 'env'
STORE = ENV / '.generated'
PLACEHOLDER = re.compile(r'__(GENERATE_PASSWORD|GENERATE_KEY32):([a-z0-9-]+)__')
UNFILLED = re.compile(r'__(?:GENERATE_|OWNER|RUN_)\S*?__')
ALPHABET = string.ascii_letters + string.digits


def private_write(path, text):
    path.parent.mkdir(mode=0o700, exist_ok=True)
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w', encoding='utf-8') as stream:
        stream.write(text)
    os.chmod(path, 0o600)


def read_pairs(path):
    pairs = {}
    if path.exists():
        for line in path.read_text(encoding='utf-8').splitlines():
            if line and not line.startswith('#') and '=' in line:
                key, _, value = line.partition('=')
                pairs[key] = value
    return pairs


def set_values(path, values):
    """Replace KEY=... lines in place; refuse keys the template does not declare."""
    lines = path.read_text(encoding='utf-8').splitlines()
    seen = set()
    for index, line in enumerate(lines):
        key = line.partition('=')[0]
        if key in values:
            lines[index] = key + '=' + values[key]
            seen.add(key)
    missing = set(values) - seen
    if missing:
        raise SystemExit(f'{path.name} does not declare: {", ".join(sorted(missing))}')
    private_write(path, '\n'.join(lines) + '\n')


def init():
    store = read_pairs(STORE)

    def generated(kind, name):
        if name not in store:
            # Passwords: 48 alphanumerics (meets the 32-1024 printable ASCII rule of database-bootstrap).
            store[name] = (''.join(secrets.choice(ALPHABET) for _ in range(48)) if kind == 'GENERATE_PASSWORD'
                           else base64.b64encode(secrets.token_bytes(32)).decode())
        return store[name]

    created = []
    for template in sorted(ENV.glob('*.env.example')):
        target = ENV / template.name[:-len('.example')]
        if target.exists():
            continue
        text = PLACEHOLDER.sub(lambda match: generated(match[1], match[2]), template.read_text(encoding='utf-8'))
        private_write(target, text)
        created.append(target.name)
    private_write(STORE, ''.join(f'{key}={value}\n' for key, value in sorted(store.items())))
    print('Created: ' + (', '.join(created) if created else 'nothing (all env files already exist)'))
    print('Fill every __OWNER:...__ value with a text editor; never paste values into a shell, issue or commit.')


def check(mode=None):
    later = '__RUN_bin/swoovr_zammad-provision__' if mode == '--bootstrap' else None
    problems = []
    for path in sorted(ENV.glob('*.env')):
        if path.stat().st_mode & 0o077:
            problems.append(f'{path.name}: permissions must be 0600')
        for number, line in enumerate(path.read_text(encoding='utf-8').splitlines(), 1):
            if UNFILLED.search(line) and not (later and line.endswith('=' + later)):
                problems.append(f'{path.name}:{number}: {line.partition("=")[0]} is not filled')
            key, _, value = line.partition('=')
            # Compose interpolates $ in unquoted env_file values, silently changing a secret.
            if not line.startswith('#') and '$' in value and not (value.startswith("'") and value.endswith("'")):
                problems.append(f'{path.name}:{number}: {key} contains $; wrap the whole value in single quotes')
    mail = ENV / 'mail.env'
    if not mail.exists():
        problems.append('mail.env is missing (envtool.py init)')
    elif not any(problem.startswith('mail.env:') for problem in problems):
        problems.extend('mail.env: ' + problem for problem in mail_problems(read_pairs(mail)))
    release = ROOT / 'release.env'
    if not release.exists():
        problems.append('release.env is missing')
    else:
        for key, value in read_pairs(release).items():
            if key.endswith('_IMAGE') and not re.fullmatch(r'[a-z0-9./_:-]+@sha256:[0-9a-f]{64}', value):
                problems.append(f'release.env: {key} must be an immutable repo@sha256 reference')
    for required in ['tls/postgres/server.crt', 'tls/postgres/server.key',
                     'tls/trust/pg-ca.crt', 'tls/trust/cloudflare-origin-ca.pem', 'tls/trust/origin-trust.pem']:
        if not (ROOT / required).is_file():
            problems.append(f'{required} is missing (bin/tls-setup)')
    if problems:
        print('\n'.join('FAIL ' + problem for problem in problems))
        raise SystemExit(1)
    print('PASS env files filled and private, release.env pinned by digest, TLS material present.')


def realms(directory):
    values = {}
    for kind in ['members', 'venues', 'staff']:
        document = json.loads((Path(directory) / f'swoovr-{kind}.json').read_text(encoding='utf-8'))
        if document.get('enabled') is not False:
            raise SystemExit(f'swoovr-{kind}.json must be a disabled realm from scripts/uat/keycloak_realms.py')
        compact = json.dumps(document, separators=(',', ':'), ensure_ascii=False)
        if "'" in compact or '\n' in compact:
            raise SystemExit(f'swoovr-{kind}.json cannot be stored as a single-quoted env value')
        # Single quotes stop Compose interpolating the ${SWOOVR_UAT_...} placeholders keycloak-bootstrap expands.
        values['SWOOVR_UAT_REALM_' + kind.upper()] = "'" + compact + "'"
    set_values(ENV / 'realms.env', values)
    print('Stored three disabled realm templates in env/realms.env.')


def provision_input():
    values = read_pairs(ENV / 'zammad-provision.env')
    payload = {
        'password': values['SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD'],
        'webhook_secret': values['SWOOVR_ZAMMAD_WEBHOOK_SECRET'],
        'webhook_endpoint': 'https://api-uat.swoovr.app/api/integrations/zammad/events',
        'events_active': True,
        'sso': {'display_name': 'Swoovr staff sign-in', 'client_id': 'swoovr-zammad',
                'issuer': 'https://auth-uat.swoovr.app/realms/swoovr-staff'},
    }
    payload.update(notification_mail(read_pairs(ENV / 'mail.env')))
    sys.stdout.write(json.dumps(payload))


def mail_problems(values):
    """env/mail.env (D358): one sender and one MailerSend account for the API, Keycloak and Zammad."""
    problems = []
    sender = values.get('SWOOVR_MAIL_FROM', '')
    if not re.fullmatch(r'[^@\s<>"]+@[^@\s<>"]+\.[^@\s<>"]+', sender):
        problems.append('SWOOVR_MAIL_FROM must be a bare address on a MailerSend verified domain (no name or <>)')
    for key in ['SWOOVR_MAIL_FROM_NAME', 'SWOOVR_MAIL_HELPDESK_FROM_NAME']:
        if any(ch in values.get(key, '') for ch in '<>"@'):
            problems.append(f'{key} must be a plain display name')
    if not values.get('SWOOVR_MAIL_SMTP_PORT', '').isdigit():
        problems.append('SWOOVR_MAIL_SMTP_PORT must be a number only (no trailing comment)')
    for key in ['SWOOVR_MAIL_SMTP_HOST', 'SWOOVR_MAIL_SMTP_USERNAME', 'SWOOVR_MAIL_SMTP_PASSWORD', 'SWOOVR_MAIL_API_TOKEN']:
        if not values.get(key, '').strip():
            problems.append(f'{key} is empty')
    return problems


def notification_mail(values):
    """Zammad's staff notifications: MailerSend SMTP (D357) from the shared sender (D358)."""
    problems = mail_problems(values)
    if problems:
        raise SystemExit('mail.env: ' + '; '.join(problems))
    name = values.get('SWOOVR_MAIL_HELPDESK_FROM_NAME', '').strip() or 'Swoovr Helpdesk'
    return {'notification_sender': f"{name} <{values['SWOOVR_MAIL_FROM']}>",
            'notification_smtp': {'host': values['SWOOVR_MAIL_SMTP_HOST'].strip(),
                                  'port': int(values['SWOOVR_MAIL_SMTP_PORT']), 'ssl': False,
                                  'enable_starttls_auto': True, 'user': values['SWOOVR_MAIL_SMTP_USERNAME'],
                                  'password': values['SWOOVR_MAIL_SMTP_PASSWORD']}}


def provision_output():
    prefix = 'SWOOVR_PROVISION_RESULT='
    line = next((line for line in sys.stdin.read().splitlines() if line.startswith(prefix)), None)
    if line is None:
        raise SystemExit('Provisioning printed no result; inspect the private diagnostics file.')
    data = json.loads(line[len(prefix):])
    set_values(ENV / 'api.env', {
        'Ticketing__Zammad__IdentityToken': data['identity_token'],
        'Ticketing__Zammad__StaffDirectoryToken': data['staff_directory_token'],
        'Ticketing__Zammad__RetentionToken': data['retention_token'],
        'Ticketing__Zammad__HomeCityCheckToken': data['home_city_check_token'],
    })
    print('Provisioned Zammad ' + data['version'] + '; groups: ' + str(len(data['groups'])) + '. Tokens stored in env/api.env.')


if __name__ == '__main__':
    command, *rest = sys.argv[1:] or ['help']
    actions = {'init': init, 'check': check, 'realms': realms, 'provision-input': provision_input,
               'provision-output': provision_output}
    if command not in actions:
        print(__doc__)
        raise SystemExit(64)
    actions[command](*rest)
