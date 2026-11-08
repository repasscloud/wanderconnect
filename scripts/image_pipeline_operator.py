#!/usr/bin/env python3
"""Prepare/check PRIVATE operator inputs for #294. Never provisions or sources env files."""
import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import sys
from urllib.parse import urlsplit

VM_KEYS = ('API_KEY', 'ARACHNID_API_USERNAME', 'ARACHNID_API_PASSWORD')
WORKER_KEYS = ('PROCESSOR_API_KEY', 'API_SERVICE_TOKEN', 'UPLOAD_SIGNING_KEY')
PLACEHOLDER = re.compile(r'CHANGE_ME|REPLACE_ME|example\.(?:test|invalid)|^$', re.I)

def initialise(directory):
    directory=Path(directory)
    directory.mkdir(mode=0o700,parents=True,exist_ok=False)
    files={
        'operator.json':json.dumps({'environment':'REPLACE_ME','region':'REPLACE_ME','vm_ips':['REPLACE_ME','REPLACE_ME'],
            'processor_url':'https://processor.example.invalid','api_url':'https://api.example.invalid',
            'cloudflare_account_id':'REPLACE_ME','raw_retention_approved':False,'licences_and_provider_terms_approved':False,
            'vm_env_files':['vm1.env','vm2.env'],'worker_env_file':'workers.env'},indent=2)+'\n',
        'vm1.env':'\n'.join(k+'=REPLACE_ME' for k in VM_KEYS)+'\n',
        'vm2.env':'\n'.join(k+'=REPLACE_ME' for k in VM_KEYS)+'\n',
        'workers.env':'\n'.join(k+'=REPLACE_ME' for k in WORKER_KEYS)+'\n',
        '.gitignore':'*\n',
    }
    for name,value in files.items():
        fd=os.open(directory/name,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
        with os.fdopen(fd,'w') as out:out.write(value)

def read_env(path):
    values={}
    for line in Path(path).read_text().splitlines():
        if not line.strip() or line.lstrip().startswith('#'):continue
        key,separator,value=line.partition('=')
        if not separator or not re.fullmatch('[A-Z][A-Z0-9_]*',key) or key in values:
            raise ValueError('invalid or duplicate environment key')
        if any(c in value for c in ('$','`','\x00')) or value[:1] in ('"',"'"):
            raise ValueError('environment values must be literal unquoted data; shell interpolation is forbidden')
        values[key]=value
    return values

def validate(path):
    path=Path(path);errors=[]
    def private(p):
        if p.is_symlink():errors.append(p.name+': symbolic secret/config file is not accepted')
        elif not p.is_file():errors.append(p.name+': missing private file')
        elif p.stat().st_mode & 0o077:errors.append(p.name+': permissions must exclude group/other access (chmod 600)')
    private(path)
    try:data=json.loads(path.read_text())
    except (OSError,ValueError):return errors+['operator config must be readable JSON']
    if not isinstance(data,dict):return errors+['operator config must be an object']
    if data.get('environment') not in ('development','uat','production'):errors.append('environment: choose development, uat or production')
    if not isinstance(data.get('region'),str) or PLACEHOLDER.search(data.get('region','')):errors.append('region: provide the selected Vultr region')
    ips=data.get('vm_ips')
    if not isinstance(ips,list) or len(ips)!=2 or len(set(str(x) for x in ips))!=2:errors.append('vm_ips: provide two distinct VM IP addresses')
    else:
        for i,address in enumerate(ips):
            try:
                ip=ipaddress.ip_address(address)
                if ip.is_loopback or ip.is_unspecified or ip.is_multicast:raise ValueError()
            except (ValueError,TypeError):errors.append('vm_ips: invalid address for VM '+str(i+1))
    for key in ('processor_url','api_url'):
        value=data.get(key,'')
        try:
            url=urlsplit(value)
            if url.username or url.password:errors.append(key+': credentials must not be included in URLs')
            if url.scheme!='https' or not url.hostname or url.query or url.fragment or url.path not in ('','/') or PLACEHOLDER.search(url.hostname or ''):
                errors.append(key+': provide an HTTPS origin without path/query/fragment')
        except (ValueError,TypeError):errors.append(key+': invalid HTTPS origin')
    if not re.fullmatch('[0-9a-f]{32}',str(data.get('cloudflare_account_id',''))):errors.append('cloudflare_account_id: provide the 32-character account ID')
    for key in ('raw_retention_approved','licences_and_provider_terms_approved'):
        if data.get(key) is not True:errors.append(key+': release approval is unresolved')
    envs=data.get('vm_env_files',[])
    if not isinstance(envs,list) or len(envs)!=2:errors.append('vm_env_files: exactly two private files required');envs=[]
    records=[]
    for name,required in [(n,VM_KEYS) for n in envs]+[(data.get('worker_env_file','workers.env'),WORKER_KEYS)]:
        if not isinstance(name,str):errors.append('invalid env file reference');continue
        p=path.parent/name;private(p)
        try:values=read_env(p)
        except (OSError,ValueError):errors.append(p.name+': missing or invalid literal environment file');continue
        for key in required:
            if key not in values or PLACEHOLDER.search(values.get(key,'')):errors.append(p.name+': missing runtime secret '+key)
        if 'POSTGRES_PASSWORD' in values or 'DATABASE_URL' in values:errors.append(p.name+': database credentials do not belong on processor/Worker')
        records.append(values)
    if len(records)==3 and records[0].get('API_KEY')!=records[1].get('API_KEY'):errors.append('VM inbound API_KEY must match both backends for the shared load-balancer identity')
    if len(records)==3 and records[0].get('API_KEY')!=records[2].get('PROCESSOR_API_KEY'):errors.append('Worker PROCESSOR_API_KEY must match the processor inbound API_KEY')
    return errors

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    sub=parser.add_subparsers(dest='command',required=True)
    sub.add_parser('init').add_argument('--directory',type=Path,required=True)
    sub.add_parser('check').add_argument('--config',type=Path,required=True)
    args=parser.parse_args()
    if args.command=='init':
        try:initialise(args.directory)
        except (OSError,ValueError):print('Refused: destination exists or cannot be created; no existing files overwritten.',file=sys.stderr);return 2
        print('Created private operator templates. Fill them locally; no resources created and no secrets displayed.');return 0
    errors=validate(args.config)
    if errors:
        for error in errors:print('OPEN: '+error)
        return 2
    print('PASS: local operator inputs only. Hosted connectivity, provider acceptance, sizing and cutover are NOT verified.')
    return 0

if __name__=='__main__':raise SystemExit(main())
