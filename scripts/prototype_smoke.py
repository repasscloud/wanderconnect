#!/usr/bin/env python3
"""Local fictional preview integration only; no provider or identity evidence."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
root = Path(__file__).resolve().parents[1]

def probe(base,path,expected=200):
    try:
        with urllib.request.urlopen(base+path,timeout=4) as r: code,body,headers=r.status,r.read(),r.headers
    except urllib.error.HTTPError as r: code,body,headers=r.code,r.read(),r.headers
    assert code == expected, (path,code,expected)
    return body,headers

def host(mode, enabled, port, run, kind="Api"):
    log=tempfile.TemporaryFile()
    env=dict(os.environ,ASPNETCORE_ENVIRONMENT=mode,ASPNETCORE_URLS=f'http://127.0.0.1:{port}',Prototype__Enabled=str(enabled).lower())
    process=subprocess.Popen(['dotnet',str(root/f'src/DatingApp.{kind}/bin/Release/net10.0/DatingApp.{kind}.dll')],cwd=root/f'src/DatingApp.{kind}',env=env,stdout=log,stderr=log)
    base=f'http://127.0.0.1:{port}'
    try:
        for _ in range(100):
            try: probe(base,'/health/live' if kind == 'Api' else '/');break
            except OSError: time.sleep(.1)
        else: raise RuntimeError('Prototype check host failed to start')
        run(base)
    finally:
        process.terminate()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.kill();process.wait()
        log.close()

prefix='/api/v1/prototype/people'
def disabled(base):probe(base,prefix,404)
def enabled(base):
    body,headers=probe(base,prefix)
    data=json.loads(body)
    assert data['isSimulation'] is True
    assert 'no-store' in headers['Cache-Control']
    names={p['name'] for p in data['people']}
    assert names == {'Jamie','Rowan','Casey'},names
    hidden='10000000-0000-0000-0000-000000000001'
    photo_free='10000000-0000-0000-0000-000000000002'
    sparse='10000000-0000-0000-0000-000000000003'
    for resource in ['', '/media', '/contact-eligibility']:
        probe(base,prefix+'/'+hidden+resource,404)
    assert json.loads(probe(base,prefix+'?requireActivity=true&match=all')[0])['people'] == []
    profile=json.loads(probe(base,prefix+'/'+photo_free)[0])
    assert profile['name']=='Jamie' and profile['hasDemoImage'] is False
    probe(base,prefix+'/'+photo_free+'/media',404)
    assert probe(base,prefix+'/'+sparse+'/media')[1]['Content-Type'].startswith('image/svg+xml')
    assert {p['name'] for p in json.loads(probe(base,prefix+'?hideSparse=true')[0])['people']} == {'Jamie','Casey'}
    probe(base,prefix+'/'+sparse+'?hideSparse=true',404)
    assert {p['name'] for p in json.loads(probe(base,prefix+'?incognito=true')[0])['people']} == {'Jamie','Rowan'}
    for travel,allowed in [('upcoming',False),('arrived',True)]:
        result=json.loads(probe(base,prefix+'?travel='+travel)[0])
        assert all(p['canContact']==allowed for p in result['people'])
    probe(base,prefix+'/'+photo_free+'/contact-eligibility?travel=upcoming',404)
    assert json.loads(probe(base,prefix+'/'+photo_free+'/contact-eligibility?travel=arrived')[0])['isSimulation'] is True
    probe(base,prefix+'/'+photo_free+'/contact-eligibility?incognito=true',404)
    for query in ['travel=unknown','incognito=maybe','hideSparse=true&hideSparse=false','role=Admin','userId=someone','match=unknown']:
        probe(base,prefix+'?'+query,400)
    assert not any(term in body.decode() for term in ['email','auth_time','ApplicableIntentions','Morgan','Quinn','Taylor'])

def web_disabled(base):
    body,_=probe(base,'/prototype/people')
    assert b'This local preview is off' in body and b'person-card-content' not in body

def web_invalid(base):
    for query in ['incognito=maybe','hideSparse=maybe','requireFriendship=maybe','requireActivity=maybe','incognito=true&incognito=false','travel=unknown']:
        body,_=probe(base,'/prototype/people?'+query)
        assert b'listed demo settings' in body and b'person-card-content' not in body
    for query in ['incognito=maybe','checkContact=maybe','disclose=maybe']:
        body,_=probe(base,'/prototype/people/10000000-0000-0000-0000-000000000002?'+query)
        assert b'Profile unavailable' in body

host('Development',False,5194,web_disabled,'Web')
host('Production',True,5195,web_disabled,'Web')
host('Development',True,5196,web_invalid,'Web')
host('Development',False,5191,disabled)
host('Production',True,5192,disabled)
host('Development',True,5193,enabled)
print('PASS: opt-in/default/production prototype gates, fictional filtered list/profile/media/contact, no hidden FWB leak, sparse/incognito/travel and malformed-input HTTP checks.')
