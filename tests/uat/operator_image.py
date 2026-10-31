#!/usr/bin/env python3
"""Actual linux/amd64 operator tooling, no network/provider/main effects."""
import json,subprocess
IMAGE='swoovr-uat-operator:phase-zero-check'
def docker(*values,input=None,check=True):return subprocess.run(['docker',*values],input=input,text=True,capture_output=True,check=check,timeout=120)
info=json.loads(docker('image','inspect',IMAGE).stdout)[0]
assert info['Architecture']=='amd64' and info['Config']['User']=='1654';checks=1
result=docker('run','--rm','--platform','linux/amd64','--network','none',IMAGE,'tools-check');assert 'PostgreSQL) 17.11' in result.stdout and 'PASS operator tooling imports' in result.stdout;checks+=1
for command in [['unknown'],['backup','api'],['backup-expiry'],['database-bootstrap']]:
    result=docker('run','--rm','--platform','linux/amd64','--network','none',IMAGE,*command,check=False)
    assert result.returncode!=0 and 'no success or fallback' in result.stderr and 'Traceback' not in result.stderr;checks+=1
script='''import os,subprocess,tempfile
from pathlib import Path
key=subprocess.run(['age-keygen'],capture_output=True,text=True,check=True)
identity=key.stdout
public=subprocess.run(['age-keygen','-y'],input=identity,capture_output=True,text=True,check=True).stdout.strip()
plain=b'fictional adult backup fixture; no identity or message content'
encrypted=subprocess.run(['age','-r',public],input=plain,capture_output=True,check=True).stdout
assert plain not in encrypted
with tempfile.TemporaryDirectory(prefix='swoovr-age-check-') as folder:
 os.chmod(folder,0o700);path=Path(folder)/'archive.age';path.write_bytes(encrypted);os.chmod(path,0o600)
 opened=subprocess.run(['age','-d','-i','-',str(path)],input=identity.encode(),capture_output=True,check=True).stdout
 assert opened==plain
 path.write_bytes(encrypted[:-1]+bytes([encrypted[-1]^1]))
 assert subprocess.run(['age','-d','-i','-',str(path)],input=identity.encode(),capture_output=True).returncode!=0
print('PASS actual age roundtrip/private stdin identity/tamper refusal (3).')
'''
r=docker('run','--rm','-i','--platform','linux/amd64','--network','none','--entrypoint','python',IMAGE,'-',input=script);assert 'PASS actual age' in r.stdout;checks+=3
print(f'PASS {checks} actual operator image/tool/refusal/encryption checks; no Azure storage, cloud TLS, provider or populated restore claim.')
