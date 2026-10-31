#!/usr/bin/env python3
"""Owned amd64 screening image guard/health checks. No image scan, Shield request or real credentials."""
import json,os,secrets,subprocess,tempfile,time,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];IMAGE='swoovr-uat-screening:phase-zero-check';name='swoovr-uat-screening-'+uuid.uuid4().hex[:12];checks=0

def docker(*args,check=True,timeout=40):return subprocess.run(['docker',*args],capture_output=True,text=True,check=check,timeout=timeout)
source=(ROOT/'infra/uat/services.bicep').read_text();guard=source.split("var screeningStartup = '''\n",1)[1].split("\n'''",1)[0]
info=json.loads(docker('image','inspect',IMAGE).stdout)[0];assert info['Architecture']=='amd64' and info['Config']['User']=='appuser';checks+=1
try:
    result=docker('run','--rm','--platform','linux/amd64','--network','none','--entrypoint','/bin/sh',IMAGE,'-ec',guard,check=False)
    assert result.returncode!=0 and 'UAT screening credentials are required.' in result.stderr;checks+=1
    with tempfile.TemporaryDirectory(prefix='swoovr-screening-check-') as private:
        folder=Path(private);os.chmod(folder,0o700);values={k:'fictional-'+secrets.token_urlsafe(32) for k in ['API_KEY','ARACHNID_API_USERNAME','ARACHNID_API_PASSWORD']};env=folder/'runtime.env';env.write_text(''.join(k+'='+v+'\n' for k,v in values.items())+'ENABLE_DOCS=false\n');os.chmod(env,0o600)
        docker('run','-d','--platform','linux/amd64','--name',name,'--network','none','--memory','4g','--cpus','2','--env-file',str(env),'--entrypoint','/bin/sh',IMAGE,'-ec',guard)
        for _ in range(120):
            probe=docker('exec',name,'python','-c',"import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:8080/ready',timeout=3).read().decode())",check=False,timeout=10)
            if probe.returncode==0:break
            if docker('inspect','--format','{{.State.Running}}',name).stdout.strip()!='true':raise AssertionError('Owned screening image exited before readiness.')
            time.sleep(2)
        else:raise AssertionError('Owned screening readiness timed out.')
        ready=json.loads(probe.stdout);assert ready['ready'] is True and all(ready['engines'][k]=='ready' for k in ['nudenet','nsfw','arachnid']);checks+=1
        r=docker('exec',name,'python','-c',"import urllib.request;assert urllib.request.urlopen('http://127.0.0.1:8080/health',timeout=3).status==200");checks+=1
        # No scans. Arachnid readiness here means configured adapter only, not authenticated provider acceptance.
        logs=docker('logs',name);assert all(value not in logs.stdout+logs.stderr for value in values.values());checks+=1
        print(f'PASS {checks} actual amd64 screening artifact/nonroot/guard/local-model readiness/private-log checks. Fake runtime credentials, network none; no scan/Shield/provider or known-content verdict claim.')
finally:
    docker('rm','-f','-v',name,check=False);print('CLEANUP owned screening container/private fixtures; no source edits or MAIN host changes.')
