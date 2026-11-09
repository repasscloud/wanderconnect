#!/usr/bin/env python3
"""Run the actual built API on owned loopback ports, no DB/providers or member data.

Complements PostgreSQL store checks: proves route registration/version middleware and
service authentication fail closed. This is not a member-upload or publication test.
"""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll'

def main():
    if not DLL.exists():raise SystemExit('Build Swoovr.Local.sln Release first.')
    count=0
    for enabled in (False,True):
        with socket.socket() as listener:
            listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
        secret=secrets.token_urlsafe(40)
        env=os.environ.copy()
        # No external configuration or accidental connection to a managed DB.
        for name in list(env):
            if name.startswith(('ConnectionStrings__','Media__Pipeline__')):env.pop(name)
        env.update(ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS=f'http://127.0.0.1:{port}',
            ConnectionStrings__Swoovr='',Media__Pipeline__Enabled=str(enabled).lower(),
            Media__Pipeline__ServiceToken=secret,Media__Pipeline__UploadSigningKey=secrets.token_urlsafe(40),
            Media__Pipeline__UploadOrigin='https://upload.example.test',
            Media__Pipeline__AcceptedPolicyVersion='swoovr-images-2026-11-08-v1')
        with tempfile.TemporaryFile() as log:
            process=subprocess.Popen(['dotnet',str(DLL)],cwd=ROOT/'src/Swoovr.Api',env=env,stdout=log,stderr=subprocess.STDOUT)
            def request(path,body=None,token=None):
                headers={'Swoovr-Version':'2026-10-28'}
                if token:headers['Authorization']='Bearer '+token
                if body is not None:headers['Content-Type']='application/json'
                req=urllib.request.Request(f'http://127.0.0.1:{port}'+path,data=body,headers=headers)
                try:
                    with urllib.request.urlopen(req,timeout=3) as response:return response.status,response.read()
                except urllib.error.HTTPError as response:return response.code,response.read()
            try:
                for _ in range(100):
                    if process.poll() is not None:raise AssertionError('API failed startup; inspect controlled local configuration.')
                    try:
                        if request('/health/live')[0]==200:break
                    except (OSError,TimeoutError):time.sleep(.1)
                else:raise AssertionError('API readiness timeout')
                if not enabled:
                    status,body=request('/api/media/pipeline/readiness');assert status==503,(status,body.decode()[:500]);count+=1
                    assert request('/api/media/pipeline/claim',b'{}',secret)[0]==404;count+=1
                    assert request('/api/me/photos/uploads',b'{"purpose":"profile"}')[0] in (401,403);count+=1
                else:
                    assert request('/api/media/pipeline/readiness')[0]==401;count+=1
                    assert request('/api/media/pipeline/readiness',token='wrong')[0]==401;count+=1
                    status,body=request('/api/media/pipeline/readiness',token=secret)
                    result=json.loads(body);assert status==503 and result.get('mode')=='staging-only' and result.get('deliverable') is False and result.get('reason')=='persistence-unavailable',(status,result);count+=1
                    assert result['ready'] is False;count+=1
                    assert request('/api/media/pipeline/claim',b'{}','wrong')[0]==401;count+=1
                    assert request('/api/media/pipeline/claim',b'{}',secret)[0]==503;count+=1
                    assert request('/api/media/pipeline/access',b'{}',secret)[0]==503;count+=1
                    assert request('/api/me/photos/uploads',b'{"purpose":"profile"}')[0] in (401,403);count+=1
            finally:
                process.terminate()
                try:process.wait(timeout=10)
                except subprocess.TimeoutExpired:process.kill();process.wait()
    print(f'PASS: {count} actual API HTTP transport/authentication checks; no DB, member sign-in, external provider or delivery acceptance claimed.')

if __name__=='__main__':main()
