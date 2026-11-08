import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('operator_config', ROOT/'scripts/image_pipeline_operator.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class OperatorTests(unittest.TestCase):
    def test_init_is_private_and_never_overwrites(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)/'operator'
            module.initialise(root)
            self.assertEqual(root.stat().st_mode & 0o777, 0o700)
            self.assertEqual((root/'operator.json').stat().st_mode & 0o777, 0o600)
            self.assertTrue(module.validate(root/'operator.json'))
            (root/'vm1.env').write_text('PRIVATE_VALUE=do-not-overwrite\n')
            with self.assertRaises(FileExistsError): module.initialise(root)
            self.assertEqual((root/'vm1.env').read_text(),'PRIVATE_VALUE=do-not-overwrite\n')
    def test_rejects_duplicate_hosts_credentials_in_urls_and_secret_perms(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)/'operator';module.initialise(root)
            p=root/'operator.json';data=json.loads(p.read_text())
            data.update(environment='uat',region='sydney',vm_ips=['203.0.113.10','203.0.113.10'],processor_url='https://user:password@processor.example.com',api_url='https://api.example.com')
            p.write_text(json.dumps(data));(root/'vm1.env').chmod(0o644)
            errors='\n'.join(module.validate(p))
            self.assertIn('distinct',errors);self.assertIn('credentials',errors);self.assertIn('permissions',errors)
            self.assertNotIn('password@',errors)
    def test_env_is_data_and_shell_interpolation_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'secrets.env';p.write_text('API_KEY=$(touch bad)\n')
            with self.assertRaises(ValueError):module.read_env(p)
            p.write_text('API_KEY=a=b=c\n')
            self.assertEqual(module.read_env(p)['API_KEY'],'a=b=c')
    def test_valid_private_inputs_are_local_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)/'operator';module.initialise(root)
            p=root/'operator.json';data=json.loads(p.read_text())
            data.update(environment='uat',region='sydney',vm_ips=['203.0.113.10','203.0.113.11'],processor_url='https://processor.example.com',api_url='https://api.example.com',cloudflare_account_id='a'*32,raw_retention_approved=True,licences_and_provider_terms_approved=True)
            p.write_text(json.dumps(data))
            for name in ['vm1.env','vm2.env']:
                (root/name).write_text('API_KEY=fictional-only-shared-key-1234567890\nARACHNID_API_USERNAME=fictional-user\nARACHNID_API_PASSWORD=fictional-password\n')
            (root/'workers.env').write_text('PROCESSOR_API_KEY=fictional-only-shared-key-1234567890\nAPI_SERVICE_TOKEN=fictional-service-token\nUPLOAD_SIGNING_KEY=fictional-upload-key\n')
            self.assertEqual(module.validate(p),[])

    def test_no_secret_values_in_errors(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)/'operator';module.initialise(root)
            (root/'vm1.env').write_text('API_KEY=SUPER_PRIVATE_SENTINEL\n')
            self.assertNotIn('SUPER_PRIVATE_SENTINEL','\n'.join(module.validate(root/'operator.json')))

if __name__=='__main__':unittest.main()
