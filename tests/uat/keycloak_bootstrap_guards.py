#!/usr/bin/env python3
"""In-memory realm/bootstrap refusal contracts; no secret files or HTTP."""
import copy,importlib.util,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
m=module('uat_bootstrap','scripts/uat/keycloak_bootstrap.py');generator=module('uat_realms','scripts/uat/keycloak_realms.py')
rendered=generator.render('https://admin.uat.swoovr.app','https://venues.uat.swoovr.app','11111111-1111-4111-8111-111111111111','22222222-2222-4222-8222-222222222222')
env={'SWOOVR_UAT_'+kind+'_WEB_SECRET':'fictional-private-'+kind for kind in ['MEMBERS','VENUES','STAFF']};env['SWOOVR_UAT_ENTRA_CLIENT_SECRET']='fictional-private-entra'
for kind in ['members','venues','staff']:env['SWOOVR_UAT_REALM_'+kind.upper()]=rendered['swoovr-'+kind+'.json']
assert len(m.prepared_realms(env))==3;checks=1
for key,value in [('enabled',True),('users',[{'username':'fictional'}]),('realm','swoovr-members-dev'),('sslRequired','none')]:
    changed=copy.deepcopy(env);realm=json.loads(changed['SWOOVR_UAT_REALM_MEMBERS']);realm[key]=value;changed['SWOOVR_UAT_REALM_MEMBERS']=json.dumps(realm)
    try:m.prepared_realms(changed)
    except ValueError:checks+=1
    else:raise AssertionError('Unsafe realm accepted: '+key)
changed=copy.deepcopy(env);changed.pop('SWOOVR_UAT_MEMBERS_WEB_SECRET')
try:m.prepared_realms(changed)
except ValueError:checks+=1
else:raise AssertionError('Missing binding accepted.')
for base,development in [('http://auth.uat.swoovr.app',False),('https://user:secret@auth.uat.swoovr.app',False),('https://auth.uat.swoovr.app/path',False),('http://external.example.test',True)]:
    try:m.Transport(base,development)
    except ValueError:checks+=1
    else:raise AssertionError('Unsafe transport origin accepted.')
print(f'PASS {checks} disabled-realm/secret-binding/transport refusal checks; no HTTP request or real credential.')
