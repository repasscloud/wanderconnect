#!/usr/bin/env python3
"""Nonsecret job-template override contracts; no Azure access or execution."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];tool=ROOT/'scripts/uat/job-template.py';checks=0
original={'containers':[{'name':'swoovr-uat-root-admin','image':'fictional@sha256:'+'a'*64,'args':['root-admin','grant'],'env':[{'name':'ConnectionStrings__Swoovr','secretRef':'db-operator'}]}]}
r=subprocess.run([sys.executable,str(tool),'--container','swoovr-uat-root-admin','--','root-admin','grant','fictional-subject'],input=json.dumps(original),capture_output=True,text=True,check=True);value=json.loads(r.stdout);assert value['containers'][0]['args']==['root-admin','grant','fictional-subject'];checks+=1
assert value['containers'][0]['env']==original['containers'][0]['env'];checks+=1
for data,container,args in [({},'missing',['x']),(original,'missing',['x']),(original,'swoovr-uat-root-admin',[])]:
 r=subprocess.run([sys.executable,str(tool),'--container',container,'--',*args],input=json.dumps(data),capture_output=True,text=True);assert r.returncode!=0 and not r.stdout;checks+=1
r=subprocess.run([sys.executable,str(tool),'--container','swoovr-uat-root-admin','--command','/bin/bash','--','-ec','printf safe'],input=json.dumps(original),capture_output=True,text=True,check=True);v=json.loads(r.stdout);assert v['containers'][0]['command']==['/bin/bash'] and v['containers'][0]['args']==['-ec','printf safe'];checks+=1
print(f'PASS {checks} nonsecret job-template preparation checks; secret references preserved, no cloud action.')
