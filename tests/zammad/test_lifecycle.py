"""Local lifecycle must never silently destroy data or emit generated secrets."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]

class LifecycleTests(unittest.TestCase):
    def load(self):
        spec = importlib.util.spec_from_file_location('zammad_local', ROOT / 'scripts/zammad_local.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def test_secrets_are_private_and_initialization_is_idempotent(self):
        module = self.load()
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'runtime.env'
            module.initialize(path)
            first = path.read_bytes()
            module.initialize(path)
            self.assertEqual(first, path.read_bytes())
            self.assertEqual(path.stat().st_mode & 0o777, 0o600)
            values = dict(line.split('=', 1) for line in first.decode().splitlines())
            self.assertGreaterEqual(len(values['POSTGRES_PASS']), 48)
            self.assertNotEqual(values['POSTGRES_PASS'], values['POSTGRES_SUPERUSER_PASS'])

    def test_private_output_replaces_permissive_file_without_following_symlink(self):
        module = self.load()
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tokens.json'
            path.write_text('old')
            path.chmod(0o644)
            module.private_write(path, 'private')
            self.assertEqual(path.stat().st_mode & 0o777, 0o600)
            target = Path(directory) / 'unrelated'
            target.write_text('unchanged')
            path.unlink()
            path.symlink_to(target)
            module.private_write(path, 'private')
            self.assertEqual(target.read_text(), 'unchanged')
            self.assertFalse(path.is_symlink())

    def test_reset_requires_explicit_data_loss_acknowledgement(self):
        module = self.load()
        with self.assertRaises(SystemExit):
            module.parse_args(['reset'])
        self.assertEqual(module.parse_args(['reset', '--delete-local-data']).action, 'reset')

if __name__ == '__main__':
    unittest.main()
