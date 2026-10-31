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
print(f'PASS {checks} local parameter acceptance/refusal checks; no Azure action.')
