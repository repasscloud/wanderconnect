"""D362 deployment configuration checks: workflow triggers, gates, permissions, SSH, images and the secret inventory.
No network, GitHub or server access. YAML is parsed with Ruby's Psych when available (macOS and Ubuntu ship it)."""
import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
DEPLOY = ROOT / '.github/workflows/deploy-uat.yml'
MAINTENANCE = ROOT / '.github/workflows/uat-maintenance.yml'
SETUP = ROOT / 'scripts/uat/setup-github-uat.sh'
spec = importlib.util.spec_from_file_location('envtool', ROOT / 'deploy/vultr-uat/bin/envtool.py')
envtool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(envtool)


def load(path):
    if not shutil.which('ruby'):
        raise unittest.SkipTest('ruby is needed to parse YAML')
    out = subprocess.run(['ruby', '-ryaml', '-rjson', '-e', 'puts JSON.dump(YAML.load_file(ARGV[0]))', str(path)],
                         check=True, capture_output=True, text=True).stdout
    data = json.loads(out)
    data['on'] = data.pop('true', data.get('on'))  # YAML 1.1 reads the bare key `on` as true
    return data


def shell_array(name):
    text = SETUP.read_text()
    body = re.search(rf'^{name}=\((.*?)\)', text, re.S | re.M).group(1)
    return [word for word in body.split() if not word.startswith('#')]


class WorkflowChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.wf, cls.text = load(DEPLOY), DEPLOY.read_text()

    def test_triggers_only_on_version_tags(self):
        self.assertEqual(self.wf['on'], {'push': {'tags': ['v*.*.*']}})
        self.assertEqual(load(MAINTENANCE)['on'].keys(), {'workflow_dispatch'})

    def test_strict_tag_regex(self):
        regex = re.search(r'\[\[ ! "\$TAG" =~ (\S+) \]\]', self.text).group(1)
        for good in ['v1.0.0', 'v1.0.1', 'v1.2.0', 'v0.0.0', 'v10.20.300']:
            self.assertRegex(good, regex)
        for bad in ['1.0.0', 'v1.0', 'v1.0.0-rc1', 'v1.0.0+build', 'v01.0.0', 'v1.00.0', 'v1.0.00', 'v1.0.0.1',
                    'V1.0.0', 'v1.0.0 ', 'vX.Y.Z', 'v-1.0.0']:
            self.assertNotRegex(bad, regex, bad)
        # The server and the manifest validator use the same rule.
        server = (ROOT / 'deploy/vultr-uat/bin/swoovr').read_text()
        self.assertIn("TAG_RE='^v(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$'", server)

    def test_tag_must_be_on_main_and_match_the_commit(self):
        self.assertIn('git merge-base --is-ancestor "$GITHUB_SHA" refs/remotes/origin/main', self.text)
        self.assertIn('[[ "$tagged" == "$GITHUB_SHA" ]]', self.text)
        self.assertIn('scripts/release_version.py msbuild-args "$VERSION"', self.text)

    def test_mode_read_once_and_validated(self):
        jobs = self.wf['jobs']
        self.assertEqual(self.text.count('vars.DEPLOYMENT_MODE'), 1, 'DEPLOYMENT_MODE must be read exactly once')
        self.assertIn('mode: ${{ steps.mode.outputs.mode }}', self.text)
        self.assertIn('MODE: ${{ needs.validate.outputs.mode }}', self.text)
        step = next(s for s in jobs['validate']['steps'] if s.get('id') == 'mode')['run']
        self.assertIn('if [[ -z "$MODE" || "$MODE" == upgrade ]]; then', step)
        self.assertIn('elif [[ "$MODE" == refresh ]]; then', step)
        self.assertIn("echo \"::error::DEPLOYMENT_MODE must be upgrade or refresh, not '$MODE'.\"\n  exit 1", step.replace('            ', '  '))
        self.assertEqual(jobs['deploy']['needs'], ['validate', 'build'])
        self.assertNotIn('UAT_REFRESH_CONFIRM', self.text)
        # The host still refuses a refresh unless the workflow passes the matching tag-bound confirmation.
        self.assertIn('confirm=(--confirm-refresh "swoovr-uat:$TAG")', self.text)

    def test_concurrency_and_minimal_permissions(self):
        self.assertEqual(self.wf['permissions'], {})
        self.assertEqual(self.wf['concurrency'], {'group': 'swoovr-uat-deploy', 'cancel-in-progress': False})
        self.assertEqual(load(MAINTENANCE)['concurrency']['group'], 'swoovr-uat-deploy')
        jobs = self.wf['jobs']
        self.assertEqual(jobs['build']['permissions'], {'contents': 'read', 'packages': 'write'})
        self.assertEqual(jobs['deploy']['permissions'], {'contents': 'read'})
        self.assertEqual(jobs['deploy']['environment'], 'uat')
        for job in jobs.values():
            self.assertNotIn('write-all', json.dumps(job.get('permissions', {})))

    def test_actions_pinned_to_full_sha(self):
        for path in (DEPLOY, MAINTENANCE):
            for line in path.read_text().splitlines():
                if re.match(r'\s*(-\s+)?uses:', line):
                    self.assertRegex(line, r'uses:\s+[\w.-]+/[\w./-]+@[0-9a-f]{40}\s+#')

    def test_images_amd64_ghcr_commit_tag(self):
        build = self.wf['jobs']['build']
        self.assertEqual(build['strategy']['matrix']['component'], ['api', 'web', 'admin', 'operator', 'keycloak'])
        step = next(s for s in build['steps'] if s.get('id') == 'build')
        self.assertEqual(step['with']['platforms'], 'linux/amd64')
        self.assertEqual(step['with']['tags'], '${{ env.REGISTRY }}/swoovr-${{ matrix.component }}:${{ github.sha }}')
        self.assertEqual(self.wf['env']['REGISTRY'], 'ghcr.io/repasscloud')
        self.assertNotIn('arm64', self.text)
        self.assertNotIn(':latest', self.text)
        self.assertIn("if: steps.existing.outputs.exists != 'true'", self.text)  # never rebuild an existing commit tag
        self.assertIn('DOCKER_CONFIG="$anonymous" docker buildx imagetools inspect', self.text)

    def test_ssh_is_strict_root_and_secret_safe(self):
        for path in (DEPLOY, MAINTENANCE):
            text = path.read_text()
            self.assertIn('StrictHostKeyChecking yes', text)
            self.assertNotRegex(text, r'StrictHostKeyChecking[ =]no')
            self.assertNotRegex(text, r'UserKnownHostsFile[ =]/dev/null')
            self.assertIn('[[ "$SSH_USER" == root ]]', text)
            self.assertIn('rm -rf "$RUNNER_TEMP/ssh"', text)
            self.assertNotRegex(text, r'echo[^\n]*\$\{?(SSH_KEY|PV_)')
        # Provider values travel on stdin only, and artifacts never include them.
        self.assertIn('--provider-values - ', self.text)
        upload = [s for s in self.wf['jobs']['deploy']['steps'] if 'upload-artifact' in s.get('uses', '')][0]
        self.assertNotIn('provider', upload['with']['path'])
        self.assertNotIn('ssh', upload['with']['path'])


class InventoryChecks(unittest.TestCase):
    def test_workflow_passes_exactly_the_envtool_mapping(self):
        text = DEPLOY.read_text()
        found = re.findall(r'PV_(UAT_\w+): \$\{\{ (secrets|vars)\.(UAT_\w+) \}\}', text)
        self.assertTrue(all(name == source for name, _, source in found), 'PV_<NAME> must read <NAME>')
        self.assertEqual(len(found), len({name for name, _, _ in found}))
        self.assertEqual({name for name, _, _ in found}, set(envtool.PROVIDER_VALUES))
        # Secrets come from secrets, non-secret values from vars, consistently with the setup script.
        secrets, variables = set(shell_array('SECRETS')), set(shell_array('VARIABLES'))
        for name, kind, _ in found:
            self.assertIn(name, secrets if kind == 'secrets' else variables, name)

    def test_setup_script_lists_every_secret_and_variable_once(self):
        text = DEPLOY.read_text() + MAINTENANCE.read_text()
        secrets_used = set(re.findall(r'secrets\.(UAT_\w+)', text))
        vars_used = set(re.findall(r'vars\.(UAT_\w+)', text))
        self.assertEqual(set(shell_array('SECRETS')), secrets_used)
        self.assertEqual(set(shell_array('VARIABLES')), vars_used)
        self.assertFalse(set(shell_array('SECRETS')) & set(shell_array('VARIABLES')))
        self.assertTrue(set(shell_array('REQUIRED')) <= secrets_used | vars_used)

    def test_template_lists_every_name_with_a_description(self):
        import os
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / 'uat.env'
            subprocess.run(['bash', str(SETUP), 'template', str(out)], check=True, capture_output=True,
                           env={**os.environ, 'SWOOVR_GH_REPO': 'example/example'})
            self.assertEqual(out.stat().st_mode & 0o777, 0o600)
            lines = out.read_text().splitlines()
            names = [line.split('=', 1)[0] for line in lines if line.startswith('UAT_')]
            self.assertEqual(names, shell_array('SECRETS') + shell_array('VARIABLES'))
            for i, line in enumerate(lines):
                if line.startswith('UAT_'):
                    self.assertRegex(lines[i - 1], r'^# \[(REQUIRED|optional) (secret|variable)\] \S', line)
                    required = line.split('=', 1)[0] in shell_array('REQUIRED')
                    self.assertEqual('[REQUIRED' in lines[i - 1], required, line)
            # Re-running never overwrites a filled-in file.
            rerun = subprocess.run(['bash', str(SETUP), 'template', str(out)], capture_output=True,
                                   env={**os.environ, 'SWOOVR_GH_REPO': 'example/example'})
            self.assertNotEqual(rerun.returncode, 0)

    def test_mapping_targets_existing_template_keys(self):
        templates = ROOT / 'deploy/vultr-uat/env'
        for name, targets in envtool.PROVIDER_VALUES.items():
            for file, key in targets:
                keys = [line.partition('=')[0] for line in (templates / (file + '.example')).read_text().splitlines()]
                self.assertIn(key, keys, f'{name} -> {file}:{key}')

    def test_no_generated_secret_is_supplied_from_github(self):
        generated = set()
        for template in (ROOT / 'deploy/vultr-uat/env').glob('*.example'):
            for line in template.read_text().splitlines():
                if '__GENERATE_' in line or '__RUN_' in line:
                    generated.add(line.partition('=')[0])
        mapped = {key for targets in envtool.PROVIDER_VALUES.values() for _, key in targets}
        self.assertFalse(generated & mapped, 'host-generated values must stay on the host')


class CaddyChecks(unittest.TestCase):
    def test_admin_paths_private_by_default(self):
        text = (ROOT / 'deploy/vultr-uat/Caddyfile').read_text()
        self.assertIn('path /admin /admin/* /realms/master /realms/master/*', text)
        self.assertIn('not remote_ip 172.30.0.0/24 {$SWOOVR_ADMIN_PRIVATE_NETWORKS}', text)
        self.assertIn('respond @private_admin 404', text)
        self.assertNotIn('auto_https off', text)
        for host in ['uat', 'api-uat', 'admin-uat', 'auth-uat', 'helpdesk-uat']:
            self.assertRegex(text, rf'(?m)^{host}\.swoovr\.app \{{')
        compose = (ROOT / 'deploy/vultr-uat/compose.yaml').read_text()
        self.assertIn('path: env/caddy-admin.env\n        required: false', compose)


if __name__ == '__main__':
    unittest.main()
