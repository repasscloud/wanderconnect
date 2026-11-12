"""Runs tests/deploy/cli_scenarios.sh in a throwaway Debian 13 container (bin/swoovr needs GNU tools, flock and root).
Skipped when Docker is unavailable or SWOOVR_SKIP_CONTAINER_TESTS is set. The repository is mounted read-only."""
import os
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
IMAGE = 'debian:trixie'


class CliScenarios(unittest.TestCase):
    def test_upgrade_refresh_and_safety_gates(self):
        if os.environ.get('SWOOVR_SKIP_CONTAINER_TESTS') or not shutil.which('docker') or \
                subprocess.run(['docker', 'info'], capture_output=True).returncode:
            self.skipTest('Docker unavailable')
        script = ('apt-get -qq update > /dev/null && apt-get -qq install -y --no-install-recommends python3 openssl '
                  'ca-certificates > /dev/null && bash /repo/tests/deploy/cli_scenarios.sh')
        result = subprocess.run(['docker', 'run', '--rm', '--network', 'bridge', '-v', f'{ROOT}:/repo:ro', IMAGE,
                                 'bash', '-c', script], capture_output=True, text=True, timeout=900)
        print(result.stdout[-4000:])
        self.assertEqual(result.returncode, 0, result.stdout[-4000:] + result.stderr[-2000:])
        self.assertIn('CLI SCENARIO CHECKS PASSED', result.stdout)


if __name__ == '__main__':
    unittest.main()
