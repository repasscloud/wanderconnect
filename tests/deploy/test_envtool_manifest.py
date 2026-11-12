"""D362 host env tool and release manifest checks, on a temporary copy of the bundle (no host, no Docker)."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
BUNDLE = ROOT / 'deploy/vultr-uat'
DIGEST = 'sha256:' + 'a' * 64
COMMIT = 'b' * 40


def manifest(**override):
    values = {'SWOOVR_TAG': 'v1.0.3', 'SWOOVR_VERSION': '1.0.3', 'SWOOVR_COMMIT': COMMIT}
    for component in ['api', 'web', 'admin', 'operator', 'keycloak']:
        values[f'SWOOVR_{component.upper()}_IMAGE'] = f'ghcr.io/repasscloud/swoovr-{component}@{DIGEST}'
    values.update(override)
    return ''.join(f'{k}={v}\n' for k, v in values.items() if v is not None)


class Manifest(unittest.TestCase):
    def check(self, text):
        with tempfile.NamedTemporaryFile('w', suffix='.env', delete=False) as handle:
            handle.write(text)
        try:
            return subprocess.run([sys.executable, str(BUNDLE / 'bin/manifest-json.py'), handle.name, '--release'],
                                  capture_output=True, text=True)
        finally:
            Path(handle.name).unlink()

    def test_valid_release(self):
        result = self.check(manifest())
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)['SWOOVR_TAG'], 'v1.0.3')

    def test_rejects_bad_releases(self):
        for override in [{'SWOOVR_TAG': 'v1.0.3-rc1'}, {'SWOOVR_TAG': 'v01.0.3'}, {'SWOOVR_VERSION': '1.0.4'},
                         {'SWOOVR_COMMIT': 'abc'}, {'SWOOVR_API_IMAGE': 'ghcr.io/repasscloud/swoovr-api:latest'},
                         {'SWOOVR_WEB_IMAGE': f'docker.io/repasscloud/swoovr-web@{DIGEST}'}, {'SWOOVR_ADMIN_IMAGE': None}]:
            self.assertNotEqual(self.check(manifest(**override)).returncode, 0, override)
        self.assertNotEqual(self.check(manifest() + 'SWOOVR_TAG=v9.9.9\n').returncode, 0, 'duplicate keys')


class EnvTool(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        shutil.copytree(BUNDLE, self.dir / 'b', ignore=shutil.ignore_patterns('tls', '__pycache__', 'state'))
        for env in (self.dir / 'b/env').glob('*.env'):
            env.unlink()
        self.tool = self.dir / 'b/bin/envtool.py'
        self.env = self.dir / 'b/env'

    def tearDown(self):
        shutil.rmtree(self.dir)

    def run_tool(self, *args, data=None):
        return subprocess.run([sys.executable, str(self.tool), *args], input=data, capture_output=True, text=True)

    def value(self, file, key):
        for line in (self.env / file).read_text().splitlines():
            if line.startswith(key + '='):
                return line.partition('=')[2]
        raise KeyError(key)

    def test_init_generates_once_and_keeps_existing_files(self):
        self.assertEqual(self.run_tool('init').returncode, 0)
        secret = self.value('automation.env', 'SWOOVR_KC_DEPLOY_CLIENT_SECRET')
        self.assertEqual(len(secret), 48)
        self.assertEqual(oct((self.env / 'automation.env').stat().st_mode & 0o777), '0o600')
        (self.env / 'automation.env').unlink()
        self.run_tool('init')  # recreates only the missing file, with the same stored value
        self.assertEqual(self.value('automation.env', 'SWOOVR_KC_DEPLOY_CLIENT_SECRET'), secret)
        self.assertEqual(self.value('api.env', 'ConnectionStrings__Swoovr').count(
            self.value('bootstrap.env', 'SWOOVR_API_DB_PASSWORD')), 1)

    def test_apply_provider_sets_keeps_clears_and_quotes(self):
        self.run_tool('init')
        before = self.value('api.env', 'Didit__ApiKey')
        data = {'UAT_DIDIT_WORKFLOW_ID': 'wf-1', 'UAT_SENDGRID_API_KEY': 'SG.pa$word', 'UAT_DIDIT_API_KEY': '',
                'UAT_ZAMMAD_IMAP_HOST': '-', 'UAT_INITIAL_ADMIN_EMAIL': 'first.admin@example.test'}
        result = self.run_tool('apply-provider', '-', data=json.dumps(data))
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(self.value('api.env', 'Didit__WorkflowId'), 'wf-1')
        self.assertEqual(self.value('api.env', 'Didit__ApiKey'), before)  # empty keeps the host value
        self.assertEqual(self.value('mail.env', 'SWOOVR_MAIL_SMTP_PASSWORD'), "'SG.pa$word'")
        self.assertEqual(self.value('mail.env', 'SWOOVR_ZAMMAD_IMAP_HOST'), '')
        self.assertNotIn('pa$word', result.stdout)
        self.assertIn('updated mail.env:SWOOVR_MAIL_SMTP_PASSWORD', result.stdout)

    def test_apply_provider_refuses_unknown_names_and_unsafe_values(self):
        self.run_tool('init')
        for data in [{'UAT_SOMETHING_ELSE': 'x'}, {'UAT_DIDIT_API_KEY': 'a\nb'}, {'UAT_DIDIT_API_KEY': "'x"},
                     {'UAT_SENDGRID_API_KEY': "a$b'c"}, {'UAT_DIDIT_API_KEY': 3}]:
            result = self.run_tool('apply-provider', '-', data=json.dumps(data))
            self.assertNotEqual(result.returncode, 0, data)
            self.assertNotIn('a\nb', result.stdout + result.stderr)

    def test_zammad_mail_left_to_zammad_by_default(self):
        self.run_tool('init')
        (self.env / 'zammad-provision.env').write_text('SWOOVR_ZAMMAD_BOOTSTRAP_PASSWORD=p\nSWOOVR_ZAMMAD_WEBHOOK_SECRET=w\n')
        payload = json.loads(self.run_tool('provision-input').stdout)
        self.assertIs(payload['manage_mail'], False)
        self.assertNotIn('notification_smtp', payload)
        self.assertNotIn('support_mailbox', payload)

    def test_init_appends_new_template_keys_without_changing_values(self):
        self.run_tool('init')
        mail = self.env / 'mail.env'
        kept = [line for line in mail.read_text().splitlines() if not line.startswith('SWOOVR_ZAMMAD_MANAGE_MAIL=')]
        mail.write_text('\n'.join(kept + ['SWOOVR_MAIL_FROM=kept@example.test']) + '\n')
        result = self.run_tool('init')
        self.assertIn('mail.env:SWOOVR_ZAMMAD_MANAGE_MAIL', result.stdout)
        self.assertEqual(self.value('mail.env', 'SWOOVR_ZAMMAD_MANAGE_MAIL'), 'false')
        self.assertIn('SWOOVR_MAIL_FROM=kept@example.test', mail.read_text())
        self.assertNotIn('Added new settings', self.run_tool('init').stdout)  # idempotent

    def test_zammad_tokens_reset_and_realm_templates(self):
        self.run_tool('init')
        self.assertEqual(self.run_tool('zammad-tokens-reset').returncode, 0)
        self.assertEqual(self.value('api.env', 'Ticketing__Zammad__RetentionToken'), '__RUN_bin/swoovr_zammad-provision__')
        realms = self.dir / 'realms'
        render = subprocess.run([sys.executable, str(ROOT / 'scripts/uat/keycloak_realms.py'), '--admin-origin',
                                 'https://admin-uat.swoovr.app', '--venue-origin', 'https://venues-uat.swoovr.app',
                                 '--entra-tenant-id', '11111111-1111-4111-8111-111111111111', '--entra-client-id',
                                 '22222222-2222-4222-8222-222222222222', '--output', str(realms)], capture_output=True, text=True)
        self.assertEqual(render.returncode, 0, render.stderr)
        self.assertEqual(self.run_tool('realms', str(realms)).returncode, 0)
        self.assertTrue(self.value('realms.env', 'SWOOVR_UAT_REALM_STAFF').startswith("'{"))


if __name__ == '__main__':
    unittest.main()
