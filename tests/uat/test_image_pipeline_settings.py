"""UAT staging bindings stay API-only; all values are metadata, never credentials."""
import json
from pathlib import Path
import re
import unittest

ROOT=Path(__file__).resolve().parents[2]

class ImagePipelineSettings(unittest.TestCase):
    def test_two_runtime_secrets_are_restricted_to_api(self):
        records=json.loads((ROOT/'infra/uat/secret-bindings.json').read_text())
        selected=[r for r in records if r['env'].startswith('Media__Pipeline__')]
        self.assertEqual(selected,[
            {'name':'media-pipeline-service-token','env':'Media__Pipeline__ServiceToken','actors':['api']},
            {'name':'media-pipeline-upload-signing-key','env':'Media__Pipeline__UploadSigningKey','actors':['api']},
        ])
        self.assertTrue(all('value' not in r for r in selected))
    def test_staging_plain_settings_are_only_api_environment(self):
        source=(ROOT/'infra/uat/services.bicep').read_text()
        api=source.split('var apiEnv =',1)[1].split('var screeningStartup',1)[0]
        for name,value in [('Enabled','true'),('UploadOrigin','https://images-upload-uat.swoovr.app'),('AcceptedPolicyVersion','swoovr-images-2026-11-08-v1')]:
            self.assertRegex(api,rf"name: 'Media__Pipeline__{name}', value: '{re.escape(value)}'")
            self.assertEqual(source.count("name: 'Media__Pipeline__"+name+"'"),1)
        self.assertNotRegex(source,r"name: 'Media__Pipeline__(?:ServiceToken|UploadSigningKey)', value:")

if __name__=='__main__':unittest.main()
