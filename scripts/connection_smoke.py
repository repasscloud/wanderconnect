#!/usr/bin/env python3
"""Disposable fictional scenario HTTP contract, never authentication evidence."""
import concurrent.futures
from contextlib import closing
import json
import http.client
import urllib.parse
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
root=Path(__file__).resolve().parents[1]
prefix='/api/v1/prototype/conversations'
def call(base,path='',body=None,expected=200,raw=None):
    data=raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req=urllib.request.Request(base+prefix+path,data=data,headers={'Content-Type':'application/json'} if data is not None else {})
    try:
        with urllib.request.urlopen(req,timeout=5) as r:code,content,headers=r.status,r.read(),r.headers
    except urllib.error.HTTPError as r:code,content,headers=r.code,r.read(),r.headers
    assert code==expected,(path,code,expected)
    if expected in (200,201):
        value=json.loads(content)
        assert value['isSimulation'] is True and 'no-store' in headers['Cache-Control']
        return value

def run(mode,enabled,port,tests):
    env=dict(os.environ,ASPNETCORE_ENVIRONMENT=mode,ASPNETCORE_URLS=f'http://127.0.0.1:{port}',Prototype__Enabled=str(enabled).lower())
    with tempfile.TemporaryFile() as log:
        p=subprocess.Popen(['dotnet',str(root/'src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll')],cwd=root/'src/Swoovr.Api',env=env,stdout=log,stderr=log)
        base=f'http://127.0.0.1:{port}'
        try:
            for _ in range(100):
                try:
                    urllib.request.urlopen(base+'/health/live',timeout=2).close();break
                except OSError:time.sleep(.1)
            else:raise RuntimeError('Scenario host not ready')
            tests(base)
        finally:
            p.terminate()
            try:p.wait(timeout=5)
            except subprocess.TimeoutExpired:p.kill();p.wait()

def tests(base):
    made=0
    def create(travel='local'):
        nonlocal made
        made+=1
        return call(base,body={'travel':travel},expected=201)
    def act(s,actor,action,revision=None,expected=200):
        return call(base,'/'+s['id']+'/actions',{'actor':actor,'action':action,'revision':s['revision'] if revision is None else revision},expected)
    for raw in [b'{}',b'{"travel":"unknown"}',b'{"travel":"local","actor":"admin"}',b'{"travel":"local","travel":"arrived"}',b'null',b'{',b'[]']:
        call(base,raw=raw,expected=400)
    call(base,raw=b' '*17000,expected=413)
    parsed=urllib.parse.urlsplit(base)
    with closing(http.client.HTTPConnection(parsed.hostname,parsed.port,timeout=5)) as chunked:
        chunked.request('POST',prefix,body=iter([b' '*9000,b' '*9000]),headers={'Content-Type':'application/json'},encode_chunked=True)
        response=chunked.getresponse();assert response.status==413;response.read()
    call(base,'?role=admin',body={'travel':'local'},expected=400)
    s=create();assert s['state']=='None' and s['messages']==[]
    path='/'+s['id']
    for query in ['', '?actor=admin','?actor=alex&actor=jamie','?actor=alex&role=Admin']:
        call(base,path+query,expected=400)
    call(base,'/99999999-9999-9999-9999-999999999999?actor=alex',expected=404)
    for raw in [b'{}',b'{"actor":"alex","action":"request","revision":0,"text":"private"}',b'{"actor":"alex","action":"request","revision":0,"revision":0}',b'{"actor":"alex","action":"request","revision":-1}',b'{"actor":"alex","action":"request","revision":0.5}',b'{"actor":"other","action":"request","revision":0}']:
        call(base,path+'/actions',raw=raw,expected=400)
    act(s,'jamie','send',expected=409)
    pending=act(s,'alex','request');assert pending['state']=='Pending' and len(pending['messages'])==1
    act(pending,'alex','request',expected=409)
    act(pending,'alex','accept',expected=409)
    act(pending,'jamie','send',expected=409)
    act(pending,'jamie','accept',revision=0,expected=409)
    accepted=act(pending,'jamie','accept');assert accepted['state']=='Accepted'
    sent=act(accepted,'jamie','send');assert len(sent['messages'])==2
    blocked=act(sent,'alex','block');assert blocked['state']=='Unavailable' and blocked['messages']==[] and blocked['canRead'] is False
    for actor in ['alex','jamie']:
        view=call(base,path+'?actor='+actor);assert view['messages']==[] and view['canReport'] is True
        act(blocked,actor,'send',expected=409)
    closed=act(blocked,'jamie','close');assert closed['messages']==[]
    d=act(create(),'alex','request');d=act(d,'jamie','decline');assert d['state']=='Declined'
    act(d,'alex','request',expected=409);act(d,'jamie','request',expected=409)
    act(create('upcoming'),'alex','request',expected=409)
    arrived=act(create('arrived'),'alex','request');assert act(arrived,'jamie','accept')['state']=='Accepted'
    c=act(create(),'alex','request');c=act(c,'jamie','accept')
    def competing(_):
        req=urllib.request.Request(base+prefix+'/'+c['id']+'/actions',data=json.dumps({'actor':'alex','action':'send','revision':c['revision']}).encode(),headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req,timeout=5) as r:return r.status
        except urllib.error.HTTPError as r:return r.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:codes=list(pool.map(competing,range(8)))
    assert codes.count(200)==1 and codes.count(409)==7,codes
    view=call(base,'/'+c['id']+'?actor=jamie');assert len(view['messages'])==2
    while len(view['messages'])<64:view=act(view,'alex','send')
    act(view,'alex','send',expected=409)
    # Safety actions remain available after the message cap.
    assert act(view,'jamie','block')['messages']==[]
    while made<32:create()
    call(base,body={'travel':'local'},expected=429)

run('Development',False,5197,lambda b:call(b,body={'travel':'local'},expected=404))
run('Production',True,5198,lambda b:call(b,body={'travel':'local'},expected=404))
run('Development',True,5199,tests)
print('PASS: fictional conversation HTTP gates, lifecycle/current block projections, malformed/bounded input, within-process concurrent CAS and capacity.')
