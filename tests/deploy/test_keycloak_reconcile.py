"""D362 Keycloak reconciliation against an in-memory fake of the Admin REST API (no Keycloak, network or host)."""
import contextlib
import io
from pathlib import Path
import sys
import unittest
import urllib.parse

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/uat'))
import keycloak_reconcile as kr  # noqa: E402

TENANT, CLIENT = '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222'
DEPLOY_SECRET, BOOTSTRAP_PASSWORD, ENTRA_SECRET = 'd' * 48, 'b' * 48, 'entra-secret-value-xyz'


class FakeKeycloak:
    def __init__(self, deploy_client=False, bootstrap_admin=True):
        self.calls, self.deploy_client, self.bootstrap_admin = [], deploy_client, bootstrap_admin
        self.realms = {r: {'realm': r, 'enabled': False} for r in kr.REALMS}
        self.clients = {('swoovr-members', 'swoovr-web'): self.web('https://uat.swoovr.app'),
                        ('swoovr-staff', 'swoovr-admin-web'): self.web('https://admin-uat.swoovr.app'),
                        ('swoovr-venues', 'swoovr-venue-web'): self.web('https://venues-uat.swoovr.app')}
        self.idp = {'alias': 'entra', 'enabled': True, 'trustEmail': False, 'storeToken': False,
                    'config': {**kr.entra_config(TENANT, CLIENT), 'clientSecret': '**********'}}
        self.staff_users, self.links, self.master_people = [], {}, []
        self.deleted, self.credentials, self.invites, self.mail_works = [], {}, [], True

    @staticmethod
    def web(origin):
        return {'id': origin, 'redirectUris': [origin + '/signin-oidc'],
                'attributes': {'post.logout.redirect.uris': origin + '/signout-callback-oidc'}}

    def __call__(self, path, method='GET', data=None, token=None, form=False):
        self.calls.append((method, path, token))
        if path.endswith('/openid-connect/token'):
            if data['grant_type'] == 'client_credentials':
                ok = self.deploy_client and data['client_secret'] == DEPLOY_SECRET
                return (200, {'access_token': 'deploy'}) if ok else (401, {})
            ok = self.bootstrap_admin and data['password'] == BOOTSTRAP_PASSWORD
            return (200, {'access_token': 'bootstrap'}) if ok else (401, {})
        assert token in ('deploy', 'bootstrap'), 'admin calls need a token'
        url = urllib.parse.urlsplit(path)
        parts, query = url.path.split('/')[3:], dict(urllib.parse.parse_qsl(url.query))
        realm, rest = parts[0], parts[1:]
        if realm == 'master':
            return self.master(method, rest, query, data, token)
        if not rest:
            if method == 'PUT':
                self.realms[realm] = data
                return 204, None
            return 200, self.realms[realm]
        if rest[0] == 'clients' and len(rest) == 1:
            if method == 'POST':
                self.clients[(realm, data['clientId'])] = {**data, 'id': data['clientId']}
                return 201, None
            found = self.clients.get((realm, query.get('clientId')))
            return 200, [found] if found else []
        if rest[0] == 'clients' and method == 'PUT':
            self.clients[(realm, data['clientId'])] = data
            return 204, None
        if rest[:2] == ['identity-provider', 'instances']:
            if method == 'PUT':
                self.idp = data
                return 204, None
            return 200, self.idp
        if rest == ['users'] and method == 'POST':
            assert realm == 'swoovr-staff' and 'credentials' not in data, 'no credential is ever set by the deployment'
            self.staff_users.append({**data, 'id': f'u{len(self.staff_users)}'})
            return 201, None
        if rest == ['users']:
            return 200, [u for u in self.staff_users if u['email'] == query.get('email')]
        if rest[0] == 'users' and rest[2] == 'federated-identity':
            return 200, self.links.get(rest[1], [])
        if rest[0] == 'users' and rest[2] == 'credentials':
            return 200, [{'type': t} for t in self.credentials.get(rest[1], [])]
        if rest[0] == 'users' and rest[2] == 'execute-actions-email' and method == 'PUT':
            assert query.get('lifespan') == str(kr.INVITE_LIFESPAN) and data == kr.INVITE_ACTIONS
            if not self.mail_works:
                return 500, None
            self.invites.append(rest[1])
            return 204, None
        raise AssertionError(f'unexpected {method} {path}')

    def master(self, method, rest, query, data, token):
        if rest == ['clients'] and method == 'POST':
            assert token == 'bootstrap'
            self.deploy_client = True
            return 201, None
        if rest == ['clients']:
            name = query['clientId']
            if name == kr.DEPLOY_CLIENT:
                return 200, [{'id': 'deploy-id', 'clientId': name}] if self.deploy_client else []
            return 200, [{'id': name + '-id', 'clientId': name}]
        if rest[0] == 'clients' and method == 'PUT':
            return 204, None
        if rest[0] == 'clients' and rest[-1] == 'service-account-user':
            return 200, {'id': 'sa'}
        if rest[0] == 'clients' and rest[2] == 'roles':
            return 200, {'name': rest[3]}
        if 'role-mappings' in rest and rest[0] == 'users' and rest[-1] == 'realm':
            return 200, [{'name': 'admin'}]
        if 'role-mappings' in rest or 'scope-mappings' in rest:
            return 204, None
        if rest == ['users'] and query.get('username') == kr.BOOTSTRAP_ADMIN:
            return 200, [{'id': 'boot', 'username': kr.BOOTSTRAP_ADMIN}] if self.bootstrap_admin else []
        if rest == ['users']:
            boot = [{'id': 'boot', 'username': kr.BOOTSTRAP_ADMIN, 'enabled': True}] if self.bootstrap_admin else []
            return 200, boot + self.master_people
        if rest[0] == 'users' and method == 'DELETE':
            assert token == 'bootstrap'
            self.deleted.append(rest[1])
            self.bootstrap_admin = False
            return 204, None
        raise AssertionError(f'unexpected master {method} {rest}')


def env(**extra):
    return {'SWOOVR_KC_DEPLOY_CLIENT_SECRET': DEPLOY_SECRET, 'KC_BOOTSTRAP_ADMIN_PASSWORD': BOOTSTRAP_PASSWORD,
            'SWOOVR_UAT_ENTRA_CLIENT_SECRET': ENTRA_SECRET, 'SWOOVR_ENTRA_TENANT_ID': TENANT,
            'SWOOVR_ENTRA_CLIENT_ID': CLIENT, 'SWOOVR_KEYCLOAK_ENABLED_REALMS': 'swoovr-members,swoovr-staff',
            'SWOOVR_INITIAL_ADMIN_EMAIL': 'first.admin@example.test', **extra}


def run(fake, values=None, admin_only=False, resend=False):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        kr.reconcile(fake, values or env(), admin_only, resend)
    text = out.getvalue()
    for secret in (DEPLOY_SECRET, BOOTSTRAP_PASSWORD, ENTRA_SECRET):
        assert secret not in text, 'a secret was printed'
    return dict(line.split(' ', 1)[1].split('=', 1) for line in text.splitlines())


class ReconcileChecks(unittest.TestCase):
    def test_fresh_keycloak_creates_deploy_client_and_enables_member_and_staff_only(self):
        fake = FakeKeycloak(deploy_client=False, bootstrap_admin=True)
        result = run(fake)
        self.assertEqual(result['deploy-client'], 'created-or-updated')
        self.assertTrue(fake.realms['swoovr-members']['enabled'] and fake.realms['swoovr-staff']['enabled'])
        self.assertFalse(fake.realms['swoovr-venues']['enabled'])
        self.assertEqual(result['zammad-client'], 'created')
        self.assertEqual(fake.idp['config']['clientSecret'], ENTRA_SECRET)
        self.assertEqual(result['bootstrap-admin'], 'kept-no-personal-admin')  # no personal admin with OTP yet
        self.assertEqual(result['root-account'], 'created-invite-sent')
        self.assertEqual(result['initial-admin'], 'invite-not-completed')

    def test_repeat_is_idempotent_and_never_needs_the_bootstrap_admin(self):
        fake = FakeKeycloak(deploy_client=True, bootstrap_admin=False)
        first, second = run(fake), run(fake)
        self.assertEqual(first['deploy-client'], 'ok')
        self.assertEqual(second['realms'], 'swoovr-members:enabled,swoovr-staff:enabled')
        self.assertEqual(second['zammad-client'], 'updated')
        self.assertEqual(second['entra'], 'in-sync')
        self.assertFalse(any(token == 'bootstrap' for _, _, token in fake.calls))

    def test_no_credential_at_all_fails_with_guidance(self):
        with self.assertRaisesRegex(ValueError, 'Run a refresh'):
            run(FakeKeycloak(deploy_client=False, bootstrap_admin=False))

    def test_entra_drift_is_corrected_on_the_keycloak_side(self):
        fake = FakeKeycloak(deploy_client=True)
        fake.idp['config']['clientId'] = '33333333-3333-4333-8333-333333333333'
        fake.idp['trustEmail'] = True
        self.assertTrue(run(fake)['entra'].startswith('corrected:'))
        self.assertEqual(fake.idp['config']['clientId'], CLIENT)
        self.assertIs(fake.idp['trustEmail'], False)

    def test_redirect_drift_refuses_without_rewriting(self):
        fake = FakeKeycloak(deploy_client=True)
        fake.clients[('swoovr-staff', 'swoovr-admin-web')]['redirectUris'] = ['https://evil.example/signin-oidc']
        with self.assertRaisesRegex(ValueError, 'swoovr-admin-web callbacks'):
            run(fake)
        self.assertEqual(fake.clients[('swoovr-staff', 'swoovr-admin-web')]['redirectUris'], ['https://evil.example/signin-oidc'])

    def test_venues_can_never_be_enabled(self):
        with self.assertRaisesRegex(ValueError, 'only swoovr-members and swoovr-staff'):
            run(FakeKeycloak(deploy_client=True), env(SWOOVR_KEYCLOAK_ENABLED_REALMS='swoovr-venues'))

    def test_bootstrap_admin_deleted_only_after_a_personal_admin_with_otp(self):
        fake = FakeKeycloak(deploy_client=True, bootstrap_admin=True)
        fake.master_people = [{'id': 'me', 'username': 'owner', 'enabled': True, 'totp': False}]
        self.assertEqual(run(fake)['bootstrap-admin'], 'kept-no-personal-admin')
        fake.master_people[0]['totp'] = True
        self.assertEqual(run(fake)['bootstrap-admin'], 'deleted')
        self.assertEqual(fake.deleted, ['boot'])

    def test_break_glass_account_created_once_with_invite_and_no_credentials(self):
        fake = FakeKeycloak(deploy_client=True, bootstrap_admin=False)
        result = run(fake, admin_only=True)
        self.assertEqual(result['root-account'], 'created-invite-sent')
        user = fake.staff_users[0]
        self.assertEqual((user['username'], user['emailVerified'], user['requiredActions']),
                         ('first.admin@example.test', False, kr.INVITE_ACTIONS))
        self.assertEqual(run(fake, admin_only=True)['root-account'], 'exists')  # never a second account or email
        self.assertEqual(len(fake.staff_users), 1)
        self.assertEqual(fake.invites, [user['id']])
        self.assertEqual(run(fake, admin_only=True, resend=True)['root-account'], 'invite-sent')
        self.assertEqual(fake.invites, [user['id'], user['id']])

    def test_invite_mail_failure_never_fails_the_deployment(self):
        fake = FakeKeycloak(deploy_client=True, bootstrap_admin=False)
        fake.mail_works = False
        self.assertEqual(run(fake)['root-account'], 'created-invite-email-failed')

    def test_initial_admin_needs_local_password_code_verified_email_and_no_entra_link(self):
        fake = FakeKeycloak(deploy_client=True, bootstrap_admin=False)
        user = {'id': 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', 'email': 'first.admin@example.test', 'enabled': True,
                'emailVerified': False, 'username': 'first.admin@example.test', 'requiredActions': ['UPDATE_PASSWORD']}
        fake.staff_users = [user]
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'invite-not-completed')
        user['requiredActions'] = []
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'unverified-email')
        user['emailVerified'] = True
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'missing-password-or-authenticator')
        fake.credentials[user['id']] = ['password']
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'missing-password-or-authenticator')
        fake.credentials[user['id']] = ['password', 'otp']
        result = run(fake, admin_only=True)
        self.assertEqual((result['initial-admin'], result['initial-admin-subject']), ('ready', user['id']))
        fake.links[user['id']] = [{'identityProvider': 'entra'}]
        result = run(fake, admin_only=True)
        self.assertEqual(result['initial-admin'], 'linked-to-identity-provider')
        self.assertNotIn('initial-admin-subject', result)
        fake.links.clear()
        fake.staff_users.append({**user, 'id': 'second'})
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'ambiguous')
        user['enabled'] = False
        fake.staff_users.pop()
        self.assertEqual(run(fake, admin_only=True)['initial-admin'], 'disabled')

    def test_missing_admin_email_is_reported_not_guessed(self):
        result = run(FakeKeycloak(deploy_client=True), env(SWOOVR_INITIAL_ADMIN_EMAIL=''), admin_only=True)
        self.assertEqual(result['initial-admin'], 'not-configured')


if __name__ == '__main__':
    unittest.main()
