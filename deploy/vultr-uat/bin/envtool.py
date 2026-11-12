#!/usr/bin/env python3
"""Private env-file helper for the Vultr UAT host. Never prints secret values.

  init                 create env/*.env from env/*.env.example, generating every __GENERATE_*:<name>__ once
                       (the same <name> gets the same value in every file); existing env files are never touched
  check [--bootstrap]  refuse unfilled placeholders, wrong permissions or tag-based images in release.env
                       (--bootstrap allows the Zammad tokens that bootstrap itself fills)
  realms <directory>   load the three realm JSON templates from scripts/uat/keycloak_realms.py into env/realms.env
  provision-input      print the JSON that containers/zammad/provision.rb reads on stdin
  provision-output     read provision.rb stdout on stdin and store its tokens in env/api.env
  apply-provider FILE  write owner-supplied values from GitHub (a JSON object {GITHUB_NAME: value}, FILE or - for stdin)
                       into the env files named in PROVIDER_VALUES. Empty = keep the host value, "-" = set empty.
                       Prints names only (D362)
  zammad-tokens-reset  mark the four Zammad tokens in env/api.env as unfilled (refresh: Zammad's data is new)
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
    """KEY=value lines, values decoded like Compose's env files: a whole value in single quotes is literal (the form
    `check` requires for a `$`); a whole value in double quotes loses its quotes. Used for every value this tool passes on."""
    pairs = {}
    if path.exists():
        for line in path.read_text(encoding='utf-8').splitlines():
            if line and not line.startswith('#') and '=' in line:
                key, _, value = line.partition('=')
                pairs[key] = unquote(value)
    return pairs


def unquote(value):
    if len(value) >= 2 and value[0] == value[-1] and value[0] in ('"', "'"):
        return value[1:-1]
    return value


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

    created, added = [], []
    for template in sorted(ENV.glob('*.env.example')):
        target = ENV / template.name[:-len('.example')]
        text = PLACEHOLDER.sub(lambda match: generated(match[1], match[2]), template.read_text(encoding='utf-8'))
        if not target.exists():
            private_write(target, text)
            created.append(target.name)
            continue
        # D362: a newer release may add settings. Append keys the template declares but the file lacks, with the
        # template's (or a newly generated) value; existing values are never changed.
        present = set(read_pairs(target))
        missing = [line for line in text.splitlines()
                   if line and not line.startswith('#') and '=' in line and line.partition('=')[0] not in present]
        if missing:
            existing = target.read_text(encoding='utf-8')
            private_write(target, existing + ('' if existing.endswith('\n') else '\n') + '\n'.join(missing) + '\n')
            added.extend(f'{target.name}:{line.partition("=")[0]}' for line in missing)
    private_write(STORE, ''.join(f'{key}={value}\n' for key, value in sorted(store.items())))
    print('Created: ' + (', '.join(created) if created else 'nothing (all env files already exist)'))
    if added:
        print('Added new settings: ' + ', '.join(added))
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
        problems.extend('mail.env: ' + problem for problem in mail_problems(read_pairs(mail), read_pairs(ENV / 'api.env')))
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
    mail = read_pairs(ENV / 'mail.env')
    if zammad_manages_mail(mail):
        payload.update(notification_mail(mail))
    else:
        # Owner (12 November 2026): Zammad's mail (Microsoft 365) is configured in Zammad after setup; provisioning
        # must not overwrite its channels or sender.
        payload['manage_mail'] = False
    sys.stdout.write(json.dumps(payload))


def zammad_manages_mail(values):
    return values.get('SWOOVR_ZAMMAD_MANAGE_MAIL', 'false').strip().lower() == 'true'


def mail_problems(values, api=None):
    """env/mail.env (D358, D360): which service sends what, and the values the selected services need.
    api is env/api.env, for the Twilio SMS account when SWOOVR_SMS_SERVICE=twilio."""
    problems = []
    get = lambda key: values.get(key, '').strip()
    if not re.fullmatch(r'[^@\s<>"]+@[^@\s<>"]+\.[^@\s<>"]+', get('SWOOVR_MAIL_FROM')):
        problems.append('SWOOVR_MAIL_FROM must be a bare address (no name or <>)')
    for key in ['SWOOVR_MAIL_FROM_NAME', 'SWOOVR_MAIL_HELPDESK_FROM_NAME', 'SWOOVR_ZAMMAD_SUPPORT_NAME']:
        if any(ch in values.get(key, '') for ch in '<>"@'):
            problems.append(f'{key} must be a plain display name')
    api_service, keycloak_service, sms = get('SWOOVR_API_MAIL_SERVICE'), get('SWOOVR_KEYCLOAK_MAIL_SERVICE'), get('SWOOVR_SMS_SERVICE')
    if api_service not in ('twilio', 'smtp', 'mailpit'):
        problems.append('SWOOVR_API_MAIL_SERVICE must be twilio, smtp or mailpit')
    if keycloak_service not in ('twilio', 'mailersend'):
        problems.append('SWOOVR_KEYCLOAK_MAIL_SERVICE must be twilio or mailersend')
    if sms not in ('mailpit', 'twilio'):
        problems.append('SWOOVR_SMS_SERVICE must be mailpit or twilio')
    needed = set()
    if 'twilio' in (api_service, keycloak_service):
        needed |= {'SWOOVR_MAIL_TWILIO_KEY_SID', 'SWOOVR_MAIL_TWILIO_KEY_SECRET'}
    if keycloak_service == 'mailersend':
        needed.add('SWOOVR_MAIL_MAILERSEND_API_TOKEN')
    # Zammad sends through the SMTP relay only when this bundle manages its mail (otherwise configured in Zammad).
    if get('SWOOVR_ZAMMAD_MANAGE_MAIL') not in ('', 'true', 'false'):
        problems.append('SWOOVR_ZAMMAD_MANAGE_MAIL must be true or false')
    if zammad_manages_mail(values) or api_service == 'smtp':
        needed |= {'SWOOVR_MAIL_SMTP_HOST', 'SWOOVR_MAIL_SMTP_USERNAME', 'SWOOVR_MAIL_SMTP_PASSWORD'}
    for key in sorted(needed):
        if not get(key):
            problems.append(f'{key} is empty')
    for key in ['SWOOVR_MAIL_SMTP_PORT', 'SWOOVR_ZAMMAD_IMAP_PORT']:
        if not get(key).isdigit():
            problems.append(f'{key} must be a number only (no trailing comment)')
    if get('SWOOVR_ZAMMAD_IMAP_HOST'):
        if not re.fullmatch(r'[^@\s<>"]+@[^@\s<>"]+\.[^@\s<>"]+', get('SWOOVR_ZAMMAD_SUPPORT_ADDRESS')):
            problems.append('SWOOVR_ZAMMAD_SUPPORT_ADDRESS must be the support mailbox address when IMAP is set')
        for key in ['SWOOVR_ZAMMAD_IMAP_USERNAME', 'SWOOVR_ZAMMAD_IMAP_PASSWORD']:
            if not get(key):
                problems.append(f'{key} is empty while SWOOVR_ZAMMAD_IMAP_HOST is set')
        if get('SWOOVR_ZAMMAD_IMAP_SECURITY') not in ('ssl', 'starttls'):
            problems.append('SWOOVR_ZAMMAD_IMAP_SECURITY must be ssl or starttls')
        if get('SWOOVR_ZAMMAD_IMAP_KEEP_ON_SERVER') not in ('true', 'false'):
            problems.append('SWOOVR_ZAMMAD_IMAP_KEEP_ON_SERVER must be true or false')
    if sms == 'twilio' and api is not None:
        for key in ['Sms__Twilio__AccountSid', 'Sms__Twilio__AuthToken', 'Sms__Twilio__From']:
            if not api.get(key, '').strip():
                problems.append(f'SWOOVR_SMS_SERVICE=twilio needs {key} in env/api.env')
    return problems


def notification_mail(values):
    """Zammad: SMTP relay (SendGrid) from the shared sender, plus the optional IMAP support mailbox (D357, D358, D360)."""
    problems = mail_problems(values)
    if problems:
        raise SystemExit('mail.env: ' + '; '.join(problems))
    get = lambda key: values.get(key, '').strip()
    name = get('SWOOVR_MAIL_HELPDESK_FROM_NAME') or 'Swoovr Helpdesk'
    payload = {'notification_sender': f"{name} <{get('SWOOVR_MAIL_FROM')}>",
               'notification_smtp': {'host': get('SWOOVR_MAIL_SMTP_HOST'), 'port': int(get('SWOOVR_MAIL_SMTP_PORT')),
                                     'ssl': False, 'enable_starttls_auto': True, 'user': values['SWOOVR_MAIL_SMTP_USERNAME'],
                                     'password': values['SWOOVR_MAIL_SMTP_PASSWORD']}}
    if get('SWOOVR_ZAMMAD_IMAP_HOST'):
        payload['support_mailbox'] = {
            'address': get('SWOOVR_ZAMMAD_SUPPORT_ADDRESS'), 'name': get('SWOOVR_ZAMMAD_SUPPORT_NAME') or 'Swoovr Support',
            'imap': {'host': get('SWOOVR_ZAMMAD_IMAP_HOST'), 'port': int(get('SWOOVR_ZAMMAD_IMAP_PORT')),
                     'ssl': get('SWOOVR_ZAMMAD_IMAP_SECURITY'), 'user': values['SWOOVR_ZAMMAD_IMAP_USERNAME'],
                     'password': values['SWOOVR_ZAMMAD_IMAP_PASSWORD'], 'folder': get('SWOOVR_ZAMMAD_IMAP_FOLDER') or 'INBOX',
                     'keep_on_server': get('SWOOVR_ZAMMAD_IMAP_KEEP_ON_SERVER') == 'true'}}
    return payload


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


# D362: GitHub Environment secret/variable name -> [(env file, existing configuration key)]. One table, also read by
# tests/deploy to prove the workflow passes exactly these names. Generated values (passwords, data keys, client
# secrets, Zammad tokens) are never here: they are created and kept on the host.
PROVIDER_VALUES = {
    # Environment secrets
    'UAT_ENTRA_CLIENT_SECRET': [('realms.env', 'SWOOVR_UAT_ENTRA_CLIENT_SECRET')],
    'UAT_DIDIT_API_KEY': [('api.env', 'Didit__ApiKey')],
    'UAT_DIDIT_WEBHOOK_SECRET': [('api.env', 'Didit__WebhookSecret')],
    'UAT_R2_ACCESS_KEY_ID': [('api.env', 'Media__R2__AccessKeyId')],
    'UAT_R2_SECRET_ACCESS_KEY': [('api.env', 'Media__R2__SecretAccessKey')],
    'UAT_SCREENING_API_KEY': [('api.env', 'Media__Screening__ApiKey')],
    'UAT_MEDIA_PIPELINE_SERVICE_TOKEN': [('api.env', 'Media__Pipeline__ServiceToken')],
    'UAT_MEDIA_PIPELINE_UPLOAD_SIGNING_KEY': [('api.env', 'Media__Pipeline__UploadSigningKey')],
    'UAT_MODERATION_ESCALATION_EMAIL': [('api.env', 'Moderation__EscalationEmail')],
    'UAT_MODERATION_ESCALATION_BACKUP_EMAIL': [('api.env', 'Moderation__EscalationBackupEmail')],
    'UAT_TWILIO_SMS_ACCOUNT_SID': [('api.env', 'Sms__Twilio__AccountSid')],
    'UAT_TWILIO_SMS_AUTH_TOKEN': [('api.env', 'Sms__Twilio__AuthToken')],
    'UAT_TWILIO_EMAIL_KEY_SID': [('mail.env', 'SWOOVR_MAIL_TWILIO_KEY_SID')],
    'UAT_TWILIO_EMAIL_KEY_SECRET': [('mail.env', 'SWOOVR_MAIL_TWILIO_KEY_SECRET')],
    'UAT_SENDGRID_API_KEY': [('mail.env', 'SWOOVR_MAIL_SMTP_PASSWORD')],
    'UAT_MAILERSEND_API_TOKEN': [('mail.env', 'SWOOVR_MAIL_MAILERSEND_API_TOKEN')],
    'UAT_ZAMMAD_IMAP_PASSWORD': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_PASSWORD')],
    # Environment variables (not secret)
    'UAT_INITIAL_ADMIN_EMAIL': [('automation.env', 'SWOOVR_INITIAL_ADMIN_EMAIL')],
    'UAT_ENTRA_TENANT_ID': [('automation.env', 'SWOOVR_ENTRA_TENANT_ID')],
    'UAT_ENTRA_CLIENT_ID': [('automation.env', 'SWOOVR_ENTRA_CLIENT_ID')],
    'UAT_KEYCLOAK_ENABLED_REALMS': [('automation.env', 'SWOOVR_KEYCLOAK_ENABLED_REALMS')],
    'UAT_DIDIT_WORKFLOW_ID': [('api.env', 'Didit__WorkflowId')],
    'UAT_R2_ENDPOINT': [('api.env', 'Media__R2__Endpoint')],
    'UAT_R2_BUCKET': [('api.env', 'Media__R2__Bucket')],
    'UAT_SCREENING_BASE_URL': [('api.env', 'Media__Screening__BaseUrl')],
    'UAT_TWILIO_SMS_FROM': [('api.env', 'Sms__Twilio__From')],
    'UAT_MAIL_FROM': [('mail.env', 'SWOOVR_MAIL_FROM')],
    'UAT_MAIL_FROM_NAME': [('mail.env', 'SWOOVR_MAIL_FROM_NAME')],
    'UAT_MAIL_HELPDESK_FROM_NAME': [('mail.env', 'SWOOVR_MAIL_HELPDESK_FROM_NAME')],
    'UAT_API_MAIL_SERVICE': [('mail.env', 'SWOOVR_API_MAIL_SERVICE')],
    'UAT_KEYCLOAK_MAIL_SERVICE': [('mail.env', 'SWOOVR_KEYCLOAK_MAIL_SERVICE')],
    'UAT_SMS_SERVICE': [('mail.env', 'SWOOVR_SMS_SERVICE')],
    'UAT_SMTP_HOST': [('mail.env', 'SWOOVR_MAIL_SMTP_HOST')],
    'UAT_SMTP_PORT': [('mail.env', 'SWOOVR_MAIL_SMTP_PORT')],
    'UAT_SMTP_USERNAME': [('mail.env', 'SWOOVR_MAIL_SMTP_USERNAME')],
    'UAT_ZAMMAD_SUPPORT_ADDRESS': [('mail.env', 'SWOOVR_ZAMMAD_SUPPORT_ADDRESS')],
    'UAT_ZAMMAD_SUPPORT_NAME': [('mail.env', 'SWOOVR_ZAMMAD_SUPPORT_NAME')],
    'UAT_ZAMMAD_IMAP_HOST': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_HOST')],
    'UAT_ZAMMAD_IMAP_PORT': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_PORT')],
    'UAT_ZAMMAD_IMAP_SECURITY': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_SECURITY')],
    'UAT_ZAMMAD_IMAP_USERNAME': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_USERNAME')],
    'UAT_ZAMMAD_IMAP_FOLDER': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_FOLDER')],
    'UAT_ZAMMAD_IMAP_KEEP_ON_SERVER': [('mail.env', 'SWOOVR_ZAMMAD_IMAP_KEEP_ON_SERVER')],
    'UAT_ZAMMAD_MANAGE_MAIL': [('mail.env', 'SWOOVR_ZAMMAD_MANAGE_MAIL')],
}
ZAMMAD_TOKENS = ['Ticketing__Zammad__IdentityToken', 'Ticketing__Zammad__StaffDirectoryToken',
                 'Ticketing__Zammad__RetentionToken', 'Ticketing__Zammad__HomeCityCheckToken']


def encode(name, value):
    """A Compose env_file value: '$' would be interpolated, so such values are wrapped whole in single quotes."""
    if any(ch in value for ch in '\n\r\0'):
        raise SystemExit(f'{name}: line breaks are not allowed')
    if value[:1] in ('"', "'") or value[-1:] in ('"', "'"):
        raise SystemExit(f'{name}: may not start or end with a quote character')
    if '$' in value:
        if "'" in value:
            raise SystemExit(f'{name}: a value containing $ may not also contain a single quote')
        return "'" + value + "'"
    return value


def apply_provider(source='-'):
    with (sys.stdin if source == '-' else open(source, encoding='utf-8')) as stream:
        data = json.load(stream)
    if not isinstance(data, dict) or any(not isinstance(v, str) for v in data.values()):
        raise SystemExit('Provider values must be a JSON object of strings.')
    unknown = sorted(set(data) - set(PROVIDER_VALUES))
    if unknown:
        raise SystemExit('Unknown provider value names: ' + ', '.join(unknown))
    pending, changed, kept = {}, [], []
    for name in sorted(data):
        value = data[name].strip()
        if not value:
            kept.append(name)
            continue
        value = '' if value == '-' else value
        for file, key in PROVIDER_VALUES[name]:
            current = read_pairs(ENV / file).get(key)
            if current is None:
                raise SystemExit(f'{file} does not declare {key} (run envtool.py init)')
            if current != value:
                pending.setdefault(file, {})[key] = encode(name, value)
                changed.append(f'{file}:{key}')
    for file, values in pending.items():
        set_values(ENV / file, values)
    print(f'Provider values: {len(changed)} updated, {len(kept)} not supplied (host value kept).')
    for item in changed:
        print('  updated ' + item)


def zammad_tokens_reset():
    set_values(ENV / 'api.env', {key: '__RUN_bin/swoovr_zammad-provision__' for key in ZAMMAD_TOKENS})
    print('Zammad tokens in env/api.env marked unfilled; zammad-provision writes new ones.')


if __name__ == '__main__':
    command, *rest = sys.argv[1:] or ['help']
    actions = {'init': init, 'check': check, 'realms': realms, 'provision-input': provision_input,
               'provision-output': provision_output, 'apply-provider': apply_provider,
               'zammad-tokens-reset': zammad_tokens_reset}
    if command not in actions:
        print(__doc__)
        raise SystemExit(64)
    actions[command](*rest)
