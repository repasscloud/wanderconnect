"""Pure generation/negative filesystem checks; no Keycloak, host or network use."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('uat_realms', ROOT / 'scripts/uat/keycloak_realms.py')
uat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(uat)
ARGS = ('https://admin-uat.swoovr.app', 'https://venues-uat.swoovr.app',
        '11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222')


class RealmPreparationChecks(unittest.TestCase):
    def realms(self):
        return {name: json.loads(value) for name, value in uat.render(*ARGS).items() if name != 'manifest.json'}

    def test_deterministic_with_matching_manifest(self):
        first = uat.render(*ARGS)
        self.assertEqual(first, uat.render(*ARGS))
        manifest = json.loads(first['manifest.json'])
        self.assertEqual(manifest['sha256'], {name: hashlib.sha256(content.encode()).hexdigest()
                         for name, content in first.items() if name != 'manifest.json'})

    def test_only_disabled_hosted_realms_without_users_or_keys(self):
        realms = self.realms()
        self.assertEqual(set(realms), {f'swoovr-{kind}.json' for kind in ('members', 'venues', 'staff')})
        for realm in realms.values():
            self.assertFalse(realm['enabled'])
            self.assertEqual(realm['users'], [])
            self.assertEqual(realm['sslRequired'], 'all')
            self.assertEqual(realm['smtpServer'], {})
            self.assertNotIn('keys', realm)
            self.assertEqual(set(realm['components']), {'org.keycloak.userprofile.UserProfileProvider'})
            self.assertNotIn('localhost', json.dumps(realm))
            self.assertNotIn('mailpit', json.dumps(realm))
            self.assertNotIn('-dev', realm['realm'])

    def test_exact_callbacks_and_no_password_grants_exchange_or_service_clients(self):
        for kind, origin in [('members', uat.MEMBERS), ('venues', ARGS[1]), ('staff', ARGS[0])]:
            realm = self.realms()[f'swoovr-{kind}.json']
            for client in realm['clients']:
                self.assertFalse(client['directAccessGrantsEnabled'])
                self.assertFalse(client['implicitFlowEnabled'])
                self.assertFalse(client['serviceAccountsEnabled'])
                self.assertEqual(client['attributes']['pkce.code.challenge.method'], 'S256')
                self.assertEqual(client['attributes']['standard.token.exchange.enabled'], 'false')
                if client['publicClient']:
                    self.assertEqual(kind, 'members')
                    self.assertNotIn('secret', client)
                    self.assertEqual(client['redirectUris'], ['com.swoovr.app:/oauth2redirect'])
                else:
                    self.assertEqual(client['redirectUris'], [origin + '/signin-oidc'])
                    self.assertEqual(client['attributes']['post.logout.redirect.uris'], origin + '/signout-callback-oidc')
                    self.assertEqual(client['secret'], '${SWOOVR_UAT_' + kind.upper() + '_WEB_SECRET}')

    def test_member_passkeys_and_explicit_adult_terms(self):
        member = self.realms()['swoovr-members.json']
        self.assertTrue(member['webAuthnPolicyPasswordlessPasskeysEnabled'])
        self.assertEqual(member['webAuthnPolicyPasswordlessRpId'], 'uat.swoovr.app')
        flows = {item['alias']: item for item in member['authenticationFlows']}
        registration = flows[member['registrationFlow']]['authenticationExecutions'][0]
        self.assertEqual(registration['authenticator'], 'registration-page-form')
        forms = flows[registration['flowAlias']]['authenticationExecutions']
        self.assertIn(('registration-terms-and-conditions', 'REQUIRED'),
                      [(step['authenticator'], step['requirement']) for step in forms])
        # Issue 332: every form action must exist in Keycloak 26 (its built-in "registration form" set); an unknown
        # one, such as the removed `registration-profile-action`, breaks the registration page with a server error.
        self.assertEqual([step['authenticator'] for step in forms],
                         ['registration-user-creation', 'registration-password-action', 'registration-terms-and-conditions'])
        self.assertTrue(next(action for action in member['requiredActions']
                             if action['alias'] == 'TERMS_AND_CONDITIONS')['defaultAction'])
        # Q126 A: change password / email are member-initiated Keycloak steps, enabled but never default.
        # Issue 332: VERIFY_EMAIL (verifyEmail=true sends nothing without it) and CONFIGURE_TOTP (Security & access).
        self.assertTrue(member['verifyEmail'])
        for alias in ('UPDATE_PASSWORD', 'UPDATE_EMAIL', 'VERIFY_EMAIL', 'CONFIGURE_TOTP'):
            action = next(action for action in member['requiredActions'] if action['alias'] == alias)
            self.assertTrue(action['enabled'])
            self.assertFalse(action['defaultAction'])

    def test_venue_and_staff_no_consumer_social_or_passkeys(self):
        for kind in ('venues', 'staff'):
            realm = self.realms()[f'swoovr-{kind}.json']
            self.assertFalse(realm['registrationAllowed'])
            self.assertFalse(realm['webAuthnPolicyPasswordlessPasskeysEnabled'])
            self.assertEqual([provider['alias'] for provider in realm['identityProviders']],
                             ['entra'] if kind == 'staff' else [])

    def test_staff_entra_plus_code_and_break_glass_password_no_auto_email_linking(self):
        staff = self.realms()['swoovr-staff.json']
        flows = {item['alias']: item for item in staff['authenticationFlows']}
        def steps(alias):
            return [(s.get('authenticator') or s.get('flowAlias'), s['requirement']) for s in flows[alias]['authenticationExecutions']]
        self.assertEqual(steps(staff['browserFlow']), [('auth-cookie', 'ALTERNATIVE'), ('swoovr-uat-staff-forms', 'ALTERNATIVE')])
        # D364: password always followed by a REQUIRED one-time code (enrolment when missing), never password alone.
        self.assertEqual(steps('swoovr-uat-staff-forms'), [('auth-username-password-form', 'REQUIRED'), ('swoovr-uat-staff-otp', 'REQUIRED')])
        self.assertEqual(steps('swoovr-uat-staff-otp'), [('auth-otp-form', 'REQUIRED')])
        self.assertFalse(staff['resetPasswordAllowed'])
        self.assertTrue(staff['verifyEmail'])
        for name in ('registrationFlow', 'resetCredentialsFlow', 'directGrantFlow'):
            self.assertEqual(flows[staff[name]]['authenticationExecutions'][0]['authenticator'], 'deny-access-authenticator')
        actions = {a['alias']: a['enabled'] for a in staff['requiredActions']}
        self.assertEqual(actions, {'VERIFY_EMAIL': True, 'UPDATE_PASSWORD': True, 'CONFIGURE_TOTP': True,
                                   'webauthn-register-passwordless': False})
        provider = staff['identityProviders'][0]
        self.assertFalse(provider['trustEmail'])
        self.assertFalse(provider['storeToken'])
        self.assertEqual(provider['config']['issuer'], f'https://login.microsoftonline.com/{ARGS[2]}/v2.0')
        self.assertEqual(provider['config']['clientSecret'], '${SWOOVR_UAT_ENTRA_CLIENT_SECRET}')
        broker = flows[provider['firstBrokerLoginFlowAlias']]['authenticationExecutions']
        self.assertEqual([step['authenticator'] for step in broker], ['idp-create-user-if-unique'])
        # Staff MFA after Microsoft: the Keycloak one-time code, recorded as `otp` in amr.
        self.assertEqual(steps(provider['postBrokerLoginFlowAlias']), [('auth-otp-form', 'REQUIRED')])
        references = {c['alias']: c['config']['default.reference.value'] for c in staff['authenticatorConfig']}
        for flow in staff['authenticationFlows']:
            for step in flow['authenticationExecutions']:
                if step.get('authenticator') in ('auth-otp-form', 'auth-username-password-form'):
                    expected = 'otp' if step['authenticator'] == 'auth-otp-form' else 'pwd'
                    self.assertEqual(references[step['authenticatorConfig']], expected)

    def test_staff_entra_app_roles_reach_the_admin_token_only_through_the_broker(self):
        staff = self.realms()['swoovr-staff.json']
        [mapper] = staff['identityProviderMappers']
        self.assertEqual((mapper['identityProviderAlias'], mapper['identityProviderMapper'], mapper['config']),
                         ('entra', 'oidc-user-attribute-idp-mapper',
                          {'claim': 'roles', 'user.attribute': 'swoovr_entra_roles', 'syncMode': 'FORCE'}))
        profile = json.loads(staff['components']['org.keycloak.userprofile.UserProfileProvider'][0]['config']['kc.user.profile.config'][0])
        [attribute] = [a for a in profile['attributes'] if a['name'] == 'swoovr_entra_roles']
        self.assertEqual(attribute['permissions'], {'view': ['admin'], 'edit': ['admin']})
        self.assertTrue(attribute['multivalued'])
        [admin] = [c for c in staff['clients'] if c['clientId'] == 'swoovr-admin-web']
        [claim] = [m for m in admin['protocolMappers'] if m['name'] == 'swoovr-entra-roles']
        self.assertEqual((claim['config']['claim.name'], claim['config']['multivalued'], claim['config']['access.token.claim']),
                         ('swoovr_entra_roles', 'true', 'true'))

    def test_reject_invalid_hosted_inputs(self):
        for origin in ['http://admin-uat.swoovr.app', 'https://localhost', 'https://admin.swoovr.app',
                       'https://admin-uat.swoovr.app/', 'https://admin-uat.swoovr.app:443',
                       'https://x@admin-uat.swoovr.app', 'https://*.uat.swoovr.app',
                       'https://admin-uat.swoovr.app?x=1', 'https://admin-uat.swoovr.app#x',
                       'https://evil.test', 'https://-bad-uat.swoovr.app', 'https://nested.admin-uat.swoovr.app']:
            with self.subTest(origin=origin), self.assertRaises(ValueError):
                uat.render(origin, *ARGS[1:])
        for tenant in ('common', 'organizations', '', '00000000-0000-0000-0000-000000000000'):
            with self.subTest(tenant=tenant), self.assertRaises(ValueError):
                uat.render(ARGS[0], ARGS[1], tenant, ARGS[3])
        with self.assertRaises(ValueError):
            uat.render(ARGS[1], ARGS[1], *ARGS[2:])

    def test_new_private_outside_output_and_no_overwrite(self):
        with tempfile.TemporaryDirectory() as private:
            output = Path(private) / 'review'
            files = uat.render(*ARGS)
            uat.write_new_directory(str(output), files)
            self.assertEqual(stat.S_IMODE(output.stat().st_mode), 0o700)
            for name, content in files.items():
                self.assertEqual((output / name).read_text(), content)
                self.assertEqual(stat.S_IMODE((output / name).stat().st_mode), 0o600)
            with self.assertRaises(ValueError):
                uat.write_new_directory(str(output), files)

    def test_reject_checkout_relative_symlink_and_traversal_output(self):
        files = uat.render(*ARGS)
        for output in (str(ROOT / 'must-not-create-uat'), 'relative-uat'):
            with self.subTest(output=output), self.assertRaises(ValueError):
                uat.write_new_directory(output, files)
        self.assertFalse((ROOT / 'must-not-create-uat').exists())
        with tempfile.TemporaryDirectory() as private:
            base = Path(private)
            (base / 'link').symlink_to(base / 'missing', target_is_directory=True)
            with self.assertRaises(ValueError):
                uat.write_new_directory(str(base / 'link'), files)
            with self.assertRaises(ValueError):
                uat.write_new_directory(str(base / 'traversal'), {'../escape': 'refused'})
            self.assertFalse((base / 'escape').exists())
            nested = base / 'unrelated-repo'
            nested.mkdir()
            (nested / '.git').mkdir()
            with self.assertRaises(ValueError):
                uat.write_new_directory(str(nested / 'output'), files)


if __name__ == '__main__':
    unittest.main()
