#!/usr/bin/env python3
"""Public synthetic metadata only; no secret/provider/cloud access."""
import copy,importlib.util,ipaddress
from pathlib import Path
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('preflight',root/'scripts/uat/preflight.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
values={'images':{k:'fictional.azurecr.io/'+k+'@sha256:'+'a'*64 for k in ['api','web','admin','keycloak','screening','operator']},'cryptoVersions':{k:'a'*32 for k in ['founder','phone','ban','contact','contactLookup','evidence','identity','identityHmac','push','pushLookup']},'trustedProxyNetworks':'10.42.0.0/23,127.0.0.1/32','r2Endpoint':'https://fictional.r2.cloudflarestorage.com','r2Bucket':'fictional-private','backupAccountUrl':'https://fictional.blob.core.windows.net','environmentDefaultDomain':'fictional.australiaeast.azurecontainerapps.io','backupRecipient':'age1'+'a'*58}
assert m.validate(values)==values;checks=1
for field,value in [('images',{}),('images',{'api':'latest'}),('cryptoVersions',{}),('trustedProxyNetworks','10.42.0.0/23;127.0.0.1/32'),('trustedProxyNetworks','0.0.0.0/0'),('trustedProxyNetworks','::/0'),('trustedProxyNetworks','10.42.0.1/23'),('r2Endpoint','https://secret@example.test'),('backupAccountUrl','https://fictional.blob.core.windows.net/'),('backupRecipient','AGE-SECRET-KEY-private'),('environmentDefaultDomain','localhost'),('r2Bucket','private/bucket'),('enableSchedules',True),('enableInvitations',True)]:
    changed=copy.deepcopy(values);changed[field]=value
    try:m.validate(changed)
    except (ValueError,TypeError):checks+=1
    else:raise AssertionError('Unsafe metadata accepted: '+field)
assert m.registration_admission_url(values)=='https://swoovr-uat-api.fictional.australiaeast.azurecontainerapps.io/api/registration/admission';checks+=1
custom=copy.deepcopy(values);custom['prefix']='fictional-test';assert m.registration_admission_url(custom)=='https://fictional-test-api.fictional.australiaeast.azurecontainerapps.io/api/registration/admission';checks+=1
for prefix in ['https://evil.test','user@api','api/path','../api','api:443','api?x','api#fragment','-api','api-','a'*60]:
    changed=copy.deepcopy(values);changed['prefix']=prefix
    try:m.validate(changed)
    except ValueError:checks+=1
    else:raise AssertionError('Unsafe registration authority prefix accepted.')
import json
bindings=json.loads((root/'infra/uat/secret-bindings.json').read_text())
m.validate_registration_bindings(bindings);checks+=1
for mutation in ['missing-api','missing-keycloak','different-secret','extra-reader']:
    changed=copy.deepcopy(bindings)
    if mutation=='missing-api':changed=[r for r in changed if r['env']!='Registration__ProviderToken']
    if mutation=='missing-keycloak':changed=[r for r in changed if r['env']!='SWOOVR_REGISTRATION_AUTHORITY_TOKEN']
    if mutation=='different-secret':next(r for r in changed if r['env']=='SWOOVR_REGISTRATION_AUTHORITY_TOKEN')['name']='different-token'
    if mutation=='extra-reader':next(r for r in changed if r['env']=='SWOOVR_REGISTRATION_AUTHORITY_TOKEN')['actors'].append('web')
    try:m.validate_registration_bindings(changed)
    except ValueError:checks+=1
    else:raise AssertionError('Unsafe registration authority binding accepted: '+mutation)
valid=copy.deepcopy(values);valid['deployServing']=True;assert m.validate(valid)==valid;checks+=1
valid['bindPublicHostnames']=True
try:m.validate(valid)
except ValueError:checks+=1
else:raise AssertionError('Public hostname binding without certificate accepted.')
valid['certificates']={k:'/subscriptions/11111111-1111-4111-8111-111111111111/resourceGroups/rg-fictional/providers/Microsoft.App/managedEnvironments/fictional/certificates/fictional' for k in ['api','web','admin']};assert m.validate(valid)==valid;checks+=1
print(f'PASS {checks} local parameter acceptance/refusal checks; no Azure action.')
