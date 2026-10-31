#!/usr/bin/env python3
"""Owned local containers only; pinned UAT image/disabled-realm/SPI checks. No live provider or UAT deployment."""
import argparse, importlib.util, json, os, secrets, subprocess, tempfile, time, urllib.error, urllib.parse, urllib.request, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--image',required=True);parser.add_argument('--platform',default='linux/amd64',choices=['linux/amd64','linux/arm64']);args=parser.parse_args()
def docker(*values,check=True,timeout=30):
    return subprocess.run(['docker',*values],check=check,capture_output=True,text=True,timeout=timeout)
def request(base,path,method='GET',data=None,token=None,form=False):
    kind='' if data is None else 'application/x-www-form-urlencoded' if form else 'application/json'
    body='' if data is None else urllib.parse.urlencode(data) if form else json.dumps(data)
    wire='\n'.join([base+path,method,kind,token or '',body])+'\n'
    result=subprocess.run(['docker','exec','-i',probe,'java','--source','21','/probe/HttpProbe.java'],input=wire,capture_output=True,text=True,timeout=35)
    if result.returncode!=0:raise ConnectionError('Owned internal HTTP probe unavailable.')
    code,_,raw=result.stdout.partition('\n')
    value=json.loads(raw) if raw.lstrip().startswith(('{','[')) else None
    return int(code),value
suffix=uuid.uuid4().hex[:12];network='swoovr-uat-image-'+suffix;pg=network+'-pg';kc=network+'-kc';probe=network+'-probe';created=[];checks=0
spec=importlib.util.spec_from_file_location('uat',ROOT/'scripts/uat/keycloak_realms.py');uat=importlib.util.module_from_spec(spec);spec.loader.exec_module(uat)
with tempfile.TemporaryDirectory(prefix='swoovr-uat-keycloak-image-') as private:
    folder=Path(private);os.chmod(folder,0o700)
    try:
        image=json.loads(docker('image','inspect',args.image).stdout)[0];assert image['Os']+'/'+image['Architecture']==args.platform;checks+=1
        config=image['Config'];assert config['User']=='1000';checks+=1
        assert config['Entrypoint']==['/opt/keycloak/bin/swoovr-entrypoint.sh'];checks+=1
        for command,message in [(['start','--optimized'],'UAT MailerSend API token is required'),(['start-dev'],'refuses development startup')]:
            name=network+'-guard';created.append(name)
            result=docker('run','--platform',args.platform,'--name',name,'--network','none',args.image,*command,check=False,timeout=20)
            assert result.returncode!=0 and message in result.stderr;checks+=1;docker('rm','-f',name);created.remove(name)
        name=network+'-theme';created.append(name)
        properties=docker('run','--platform',args.platform,'--name',name,'--network','none','--entrypoint','/bin/bash',args.image,'-c','cat /opt/keycloak/themes/swoovr/login/theme.properties').stdout
        assert 'localhost:5081' not in properties;checks+=1
        for field,path in [('swoovrPrivacyUrl','privacy'),('swoovrTermsUrl','terms'),('swoovrCommunityUrl','community-guidelines')]:assert field+'=https://uat.swoovr.app/'+path in properties;checks+=1
        assert 'swoovrVersion=v0.1.0-uat' in properties;checks+=1
        docker('rm','-f',name);created.remove(name)
        docker('network','create','--internal',network)
        password=secrets.token_urlsafe(32);admin_password=secrets.token_urlsafe(32)
        env=folder/'runtime.env';values={'POSTGRES_USER':'swoovr_test','POSTGRES_PASSWORD':password,'POSTGRES_DB':'keycloak','KC_DB':'postgres','KC_DB_URL':'jdbc:postgresql://'+pg+':5432/keycloak','KC_DB_USERNAME':'swoovr_test','KC_DB_PASSWORD':password,'KC_HTTP_ENABLED':'true','KC_HOSTNAME_STRICT':'false','KC_BOOTSTRAP_ADMIN_USERNAME':'swoovr-image-admin','KC_BOOTSTRAP_ADMIN_PASSWORD':admin_password,'KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__API_TOKEN':'fictional-private-token','KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__FROM':'no-reply@example.test','SWOOVR_UAT_MEMBERS_WEB_SECRET':secrets.token_urlsafe(32),'SWOOVR_UAT_VENUES_WEB_SECRET':secrets.token_urlsafe(32),'SWOOVR_UAT_STAFF_WEB_SECRET':secrets.token_urlsafe(32),'SWOOVR_UAT_ENTRA_CLIENT_SECRET':secrets.token_urlsafe(32)}
        env.write_text(''.join(k+'='+v+'\n' for k,v in values.items()));os.chmod(env,0o600)
        created.append(pg);docker('run','-d','--name',pg,'--network',network,'--env-file',str(env),'postgres:17.11-alpine@sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb')
        for _ in range(60):
            if docker('exec',pg,'pg_isready','-U','swoovr_test',check=False).returncode==0:break
            time.sleep(1)
        else:raise AssertionError('Owned PostgreSQL did not become ready.')
        imports=folder/'imports';imports.mkdir(mode=0o755)
        rendered=uat.render('https://admin.uat.swoovr.app','https://venues.uat.swoovr.app','11111111-1111-4111-8111-111111111111','22222222-2222-4222-8222-222222222222')
        for name,value in rendered.items():
            if name!='manifest.json':p=imports/name;p.write_text(value);os.chmod(p,0o644) # Fictional public review config only, no expanded secrets.
        created.append(kc);docker('run','--platform',args.platform,'-d','--name',kc,'--network',network,'--env-file',str(env),'-v',str(imports)+':/opt/keycloak/data/import:ro',args.image,'start','--optimized','--import-realm')
        # Docker does not publish a port for an internal-only bridge. Keep egress closed and put the probe inside it.
        created.append(probe);docker('run','-d','--name',probe,'--network',network,'-v',str(ROOT/'tests/uat')+':/probe:ro','eclipse-temurin:21-jdk-noble@sha256:bd6ac06e2a2df928af21c75c4a8a0f2829a93377a65761d6a577bb32281e2ac3','sleep','600')
        base='http://'+kc+':8080'
        for _ in range(120):
            try:
                if request(base,'/realms/master/.well-known/openid-configuration')[0]==200:break
            except (urllib.error.URLError,TimeoutError,ConnectionError):pass
            if docker('inspect','--format','{{.State.Running}}',kc).stdout.strip()!='true':raise AssertionError('Owned Keycloak stopped before readiness.')
            time.sleep(1)
        else:raise AssertionError('Owned Keycloak readiness timed out.')
        print('PASS owned Keycloak image booted/imported on internal network; MAIN untouched.',flush=True)
        code,body=request(base,'/realms/master/protocol/openid-connect/token','POST',{'client_id':'admin-cli','grant_type':'password','username':'swoovr-image-admin','password':admin_password},form=True);assert code==200,'Owned bootstrap sign-in refused.';token=body['access_token'];checks+=1
        for kind in ['members','venues','staff']:
            code,value=request(base,'/admin/realms/swoovr-'+kind,token=token);assert code==200 and value['enabled'] is False;checks+=1
            assert value['smtpServer']=={} and value['loginTheme']=='swoovr';checks+=1
            code,clients=request(base,'/admin/realms/swoovr-'+kind+'/clients',token=token);assert code==200;checks+=1
            expected={'members':'swoovr-web','venues':'swoovr-venue-web','staff':'swoovr-admin-web'}[kind]
            selected=[client for client in clients if client['clientId']==expected];assert len(selected)==1;checks+=1
            origin='https://uat.swoovr.app' if kind=='members' else 'https://admin.uat.swoovr.app' if kind=='staff' else 'https://venues.uat.swoovr.app'
            assert selected[0]['redirectUris']==[origin+'/signin-oidc'];checks+=1
            assert selected[0]['attributes']['post.logout.redirect.uris']==origin+'/signout-callback-oidc';checks+=1
            assert selected[0]['directAccessGrantsEnabled'] is False and selected[0]['serviceAccountsEnabled'] is False;checks+=1
            if kind=='members':assert value['webAuthnPolicyPasswordlessRpId']=='uat.swoovr.app';checks+=1
        code,staff=request(base,'/admin/realms/swoovr-staff/authentication/flows',token=token);assert code==200 and any(f['alias']=='swoovr-uat-staff-entra-only' for f in staff);checks+=1
        code,executions=request(base,'/admin/realms/swoovr-staff/authentication/flows/swoovr-uat-staff-entra-only/executions',token=token);assert code==200 and len(executions)==1 and executions[0]['providerId']=='identity-provider-redirector' and executions[0]['requirement']=='REQUIRED';checks+=1
        code,providers=request(base,'/admin/realms/swoovr-staff/identity-provider/instances',token=token);assert code==200 and len(providers)==1 and providers[0]['alias']=='entra' and providers[0]['trustEmail'] is False;checks+=1
        code,users=request(base,'/admin/realms/master/users?username=swoovr-image-admin&exact=true',token=token);assert code==200 and len(users)==1;user=users[0];user['email']='fictional.adult@example.test';assert request(base,'/admin/realms/master/users/'+user['id'],'PUT',user,token)[0]==204;checks+=1
        # Real Keycloak template/SPI invocation; network has no external egress and fake credential cannot deliver.
        code,_=request(base,'/admin/realms/swoovr-members/testSMTPConnection','POST',{'host':'smtp.invalid','from':'ignored@example.test'},token);assert code==500,'Blocked provider acknowledgement must not be success.';checks+=1
        logs=docker('logs',kc).stdout+docker('logs',kc).stderr
        assert 'Email provider acknowledgement unavailable.' in logs and 'MailerSendEmailSender.send' in logs;checks+=1
        for secret in [values['KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__API_TOKEN'],admin_password,password]:assert secret not in logs;checks+=1
        print('PASS Keycloak image: '+str(checks)+' runtime/import/guard/private-log assertions; actual template reached selected HTTP SPI and failed closed without external egress. No real provider or Entra/UAT/browser acceptance.',flush=True)
    except Exception:
        if kc in created:
            result=docker('logs',kc,check=False);diagnostic=result.stdout+result.stderr
            for key,value in locals().get('values',{}).items():
                if any(part in key for part in ['PASSWORD','SECRET','TOKEN']):diagnostic=diagnostic.replace(value,'[redacted]')
            target=Path(tempfile.gettempdir())/('swoovr-keycloak-image-failure-'+suffix+'.log');target.write_text(diagnostic);os.chmod(target,0o600);print('Private failure diagnostic: '+str(target),flush=True)
        raise
    finally:
        for name in reversed(created):docker('rm','-f','-v',name,check=False)
        docker('network','rm',network,check=False)
        print('CLEANUP: owned Keycloak/PostgreSQL/network/private fixture removed; MAIN unchanged.',flush=True)
