#!/usr/bin/env python3
"""Published linux/amd64 hosts on an owned internal Docker network. No cloud/provider/real-member claim."""
import argparse, base64, json, os, re, secrets, subprocess, tempfile, time, urllib.parse, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--prefix',default='swoovr-uat-');p.add_argument('--tag',default='phase-zero-check');args=p.parse_args()
images={k:args.prefix+k+':'+args.tag for k in ['api','web','admin']}
uid=uuid.uuid4().hex[:12];network='swoovr-uat-image-'+uid;names={k:network+'-'+k for k in ['api','web','admin','pg','probe']};created=[];checks=0

def docker(*values,check=True,timeout=40):return subprocess.run(['docker',*values],check=check,capture_output=True,text=True,timeout=timeout)
def request(kind,path):
    wire='\n'.join(['http://'+names[kind]+':8080'+path,'GET','','',''])+'\n'
    r=subprocess.run(['docker','exec','-i',names['probe'],'java','--source','21','/probe/HttpProbe.java'],input=wire,capture_output=True,text=True,timeout=35)
    if r.returncode!=0:raise ConnectionError('Owned HTTP probe unavailable.')
    code,_,body=r.stdout.partition('\n');return int(code),body

def envfile(folder,name,values):
    p=folder/(name+'.env');p.write_text(''.join(k+'='+str(v)+'\n' for k,v in values.items()));os.chmod(p,0o600);return p

def job(name,arguments,env):
    container=network+'-'+name;created.append(container)
    r=docker('run','--platform','linux/amd64','--name',container,'--network',network,'--env-file',str(env),images['api'],*arguments,check=False,timeout=120)
    return r

with tempfile.TemporaryDirectory(prefix='swoovr-uat-app-images-') as private:
    folder=Path(private);os.chmod(folder,0o700)
    try:
        for kind,image in images.items():
            data=json.loads(docker('image','inspect',image).stdout)[0];assert data['Architecture']=='amd64' and data['Os']=='linux';checks+=1
            c=data['Config'];assert c['User']=='1654' and c['WorkingDir']=='/app';checks+=1
            assert c['Entrypoint']==['dotnet','Swoovr.'+kind.capitalize()+'.dll'];checks+=1
            name=network+'-'+kind+'-guard';created.append(name)
            run=docker('run','--platform','linux/amd64','--name',name,'--network','none','-e','Staff__LocalPasswords=true',image,check=False,timeout=30)
            assert run.returncode!=0 and 'development-only:Staff:LocalPasswords' in run.stderr and 'Now listening on' not in run.stdout;checks+=1
            docker('rm','-f',name);created.remove(name)
        docker('network','create','--internal',network)
        subnet=json.loads(docker('network','inspect',network).stdout)[0]['IPAM']['Config'][0]['Subnet']
        password=secrets.token_urlsafe(32);pg_env=envfile(folder,'pg',{'POSTGRES_USER':'swoovr_test','POSTGRES_PASSWORD':password,'POSTGRES_DB':'swoovr_image'})
        created.append(names['pg']);docker('run','-d','--name',names['pg'],'--network',network,'--env-file',str(pg_env),'postgres:17.11-alpine@sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb')
        for _ in range(60):
            if docker('exec',names['pg'],'pg_isready','-U','swoovr_test',check=False).returncode==0:break
            time.sleep(1)
        else:raise AssertionError('Owned database unavailable.')
        key=lambda:base64.b64encode(secrets.token_bytes(32)).decode()
        common={'DOTNET_ENVIRONMENT':'Uat','ASPNETCORE_ENVIRONMENT':'Uat','Hosting__TrustedProxyNetworks':subnet,'Keycloak__RequireHttpsMetadata':'true'}
        api={**common,'Hosting__PublicOrigin':'https://api.uat.swoovr.app','AllowedHosts':'api.uat.swoovr.app;'+names['api'],'ConnectionStrings__Swoovr':'Host='+names['pg']+';Database=swoovr_image;Username=swoovr_test;Password='+password+';SSL Mode=Disable','Keycloak__BaseUrl':'https://auth.uat.swoovr.app','Keycloak__MembersRealm':'swoovr-members','Keycloak__VenuesRealm':'swoovr-venues','Keycloak__StaffRealm':'swoovr-staff','Keycloak__StaffSsoAlias':'entra','MemberAdmin__ClientId':'swoovr-api-accounts','MemberAdmin__ClientSecret':secrets.token_urlsafe(32),'Phone__Required':'true','Retention__Worker':'true','Media__Screening__Worker':'true','Email__Provider':'mailersend','Email__MailerSend__ApiToken':'fictional-private-email-token','Email__From':'Swoovr <no-reply@example.test>','Sms__Provider':'twilio','Sms__Twilio__AccountSid':'AC'+'a'*32,'Sms__Twilio__AuthToken':'fictional-private-sms-token','Sms__Twilio__From':'+15005550006','Push__Provider':'disabled','Media__Store':'r2','Media__R2__Endpoint':'https://objects.example.test','Media__R2__Bucket':'fictional-private','Media__R2__AccessKeyId':'fictional-private-access','Media__R2__SecretAccessKey':secrets.token_urlsafe(32),'Media__Screening__BaseUrl':'https://screening.example.test','Media__Screening__RequireKnownContentCheck':'true','Didit__ApiKey':'fictional-private-didit','Didit__WorkflowId':'fictional-workflow','Didit__WebhookSecret':secrets.token_urlsafe(32),'Didit__BaseUrl':'https://didit.example.test','Moderation__EscalationEmail':'safety@example.test','Moderation__EscalationBackupEmail':'backup@example.test','Founders__Secrets__1':secrets.token_urlsafe(40),'Phone__CodeKey':secrets.token_urlsafe(40)}
        for field in ['Bans__HmacKeys__1','ContactHistory__Keys__1','ContactHistory__LookupKey','Evidence__Keys__1','IdentityStore__Keys__1','IdentityStore__HmacKey','Push__TokenKeys__1','Push__TokenLookupKey']:api[field]=key()
        api_env=envfile(folder,'api',api)
        minimal=envfile(folder,'migrate-minimal',{**common,'Hosting__PublicOrigin':'https://api.uat.swoovr.app','ConnectionStrings__Swoovr':api['ConnectionStrings__Swoovr'],'Email__Provider':'disabled','Push__Provider':'disabled','Media__Store':'none'})
        r=job('migrate-minimal',['migrate'],minimal);assert r.returncode==0,'Least-privilege migration must not require unrelated provider/crypto secrets.';checks+=1
        r=job('migrate',['migrate'],api_env);assert r.returncode==0,'Published migrate command failed.';checks+=1
        for name,command in [('purge',['accounts','purge']),('founders',['founders','allocate'])]:
            r=job(name,command,api_env);assert r.returncode==0,'Published empty-database operator command failed: '+name;checks+=1
        r=job('root-refusal',['root-admin','grant'],api_env);assert r.returncode!=0,'Missing root subject must be refused.';checks+=1
        print('PASS published image architecture/nonroot/startup guards and owned migration/operator commands.',flush=True)
        for kind in ['api','web','admin']:
            if kind=='api':env=api_env
            else:
                host='uat.swoovr.app' if kind=='web' else 'admin.uat.swoovr.app';realm='members' if kind=='web' else 'staff'
                env=envfile(folder,kind,{**common,'Hosting__PublicOrigin':'https://'+host,'AllowedHosts':host+';'+names[kind],'Api__BaseUrl':'https://unconfigured-api.example.test/','Keycloak__Authority':'https://auth.uat.swoovr.app/realms/swoovr-'+realm,'Keycloak__ClientId':'swoovr-web' if kind=='web' else 'swoovr-admin-web','Keycloak__ClientSecret':secrets.token_urlsafe(32)})
            created.append(names[kind]);docker('run','--platform','linux/amd64','-d','--name',names[kind],'--network',network,'--env-file',str(env),images[kind])
        created.append(names['probe']);docker('run','-d','--name',names['probe'],'--network',network,'-v',str(ROOT/'tests/uat')+':/probe:ro','eclipse-temurin:21-jdk-noble@sha256:bd6ac06e2a2df928af21c75c4a8a0f2829a93377a65761d6a577bb32281e2ac3','sleep','900')
        for kind in ['api','web','admin']:
            for _ in range(60):
                try:
                    if request(kind,'/health/live')[0]==200:break
                except (ConnectionError,TimeoutError):pass
                if docker('inspect','--format','{{.State.Running}}',names[kind]).stdout.strip()!='true':raise AssertionError('Published '+kind+' stopped before liveness.')
                time.sleep(1)
            else:raise AssertionError('Published '+kind+' liveness timed out.')
            checks+=1
        assert request('api','/health/ready')[0]==200;checks+=1
        for kind in ['web','admin']:assert request(kind,'/health/ready')[0]==503;checks+=1 # Explicit unreachable fake HTTPS API, never optimistic readiness.
        code,html=request('web','/');assert code==200 and 'Swoovr' in html and 'blazor.web' in html;checks+=1
        code,html=request('admin','/signed-out');assert code==200 and 'Swoovr' in html;checks+=1
        copy=network+'-assets';created.append(copy);docker('create','--platform','linux/amd64','--name',copy,images['web'])
        manifest=folder/'assets.json';docker('cp',copy+':/app/Swoovr.Web.staticwebassets.endpoints.json',str(manifest));data=json.loads(manifest.read_text())
        routes=[e['Route'] for e in data['Endpoints']];wasm=next(r for r in routes if r.endswith('.wasm'))
        code,body=request('web','/'+wasm);assert code==200 and body.startswith('\x00asm');checks+=1
        styles=next(r for r in routes if r.endswith('.css'));code,body=request('web','/'+styles);assert code==200 and len(body)>100;checks+=1
        for kind in ['api','web','admin']:
            logs=docker('logs',names[kind]).stdout+docker('logs',names[kind]).stderr
            for value in [password,api['Email__MailerSend__ApiToken'],api['Sms__Twilio__AuthToken'],api['Didit__ApiKey']]:assert value not in logs;checks+=1
        print('PASS published hosts: '+str(checks)+' architecture/guard/operator/liveness/readiness/static-WASM/private-log assertions. Synthetic proxy/config/database only; no TLS/provider/authenticated UAT journey.',flush=True)
    except Exception:
        for kind in ['api','web','admin']:
            if names[kind] in created:
                result=docker('logs',names[kind],check=False);text=result.stdout+result.stderr
                for value in locals().get('api',{}).values():
                    if len(str(value))>12:text=text.replace(str(value),'[redacted]')
                target=Path(tempfile.gettempdir())/('swoovr-app-image-failure-'+uid+'-'+kind+'.log');target.write_text(text);os.chmod(target,0o600);print('Private failure diagnostic: '+str(target),flush=True)
        raise
    finally:
        for name in reversed(created):docker('rm','-f','-v',name,check=False)
        docker('network','rm',network,check=False)
        print('CLEANUP: owned published-host/database/probe containers/network/private fixtures removed; MAIN untouched.',flush=True)
