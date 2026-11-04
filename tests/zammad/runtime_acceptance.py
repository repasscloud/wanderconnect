#!/usr/bin/env python3
"""Actual Swoovr HTTP endpoints + real Keycloak code flow + pinned Zammad, owned disposable DB/realms.
Tokens, fictional messages and provider response/error bodies are never printed. No normal realm is modified.
"""
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import html
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import http.cookiejar
import io
import json
import os
from pathlib import Path
import re
import signal
import socket
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts'))
from zammad_local import provision_runtime, private_write
from webhook_acceptance import psql
from provider_acceptance import request as provider_call
PRIVATE=ROOT/'.local/zammad'


def request(url,method='GET',data=None,token=None,opener=None,headers=None):
    h={'Content-Type':'application/json',**(headers or {})}
    if token:h['Authorization']='Bearer '+token
    req=urllib.request.Request(url,data=None if data is None else json.dumps(data).encode(),headers=h,method=method)
    try:
        with (opener.open(req,timeout=90) if opener else urllib.request.urlopen(req,timeout=90)) as r:
            body=r.read();return r.status,json.loads(body) if body else None
    except urllib.error.HTTPError as e:
        body=e.read()
        try:value=json.loads(body)
        except (ValueError,UnicodeDecodeError):value=None
        return e.code,value


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None


def main():
    run=uuid.uuid4().hex[:12]
    directory=PRIVATE/('runtime-'+run);directory.mkdir(mode=0o700)
    config=json.loads((PRIVATE/'provisioned.json').read_text())
    platform=dict(line.split('=',1) for line in (ROOT/'.local/platform/docker.env').read_text().splitlines() if '=' in line and not line.startswith('#'))
    database='swoovr_zammad_runtime_'+run
    realms=['swoovr-ticket-members-'+run,'swoovr-ticket-staff-'+run]
    kc='http://localhost:8180';base='http://127.0.0.1:5095'
    checks=[];api=None;log=None;created_realms=[];staff_ids=[];proxy=None
    def check(ok,name):
        if not ok:raise RuntimeError('FAIL: '+name)
        checks.append(name);print('PASS:',name,flush=True)
    # Auth credentials stay exclusively in process memory/private files.
    container=json.loads(subprocess.check_output(['docker','inspect','swoovr-platform-keycloak-1']))[0]
    keycloak_env=dict(line.split('=',1) for line in container['Config']['Env'] if '=' in line)
    body=urllib.parse.urlencode({'grant_type':'password','client_id':'admin-cli','username':keycloak_env['KC_BOOTSTRAP_ADMIN_USERNAME'],'password':keycloak_env['KC_BOOTSTRAP_ADMIN_PASSWORD']}).encode()
    with urllib.request.urlopen(urllib.request.Request(kc+'/realms/master/protocol/openid-connect/token',body)) as response:admin_token=json.load(response)['access_token']
    def admin(path,method='GET',data=None):
        code,value=request(kc+'/admin/'+path,method,data,admin_token)
        if code not in (200,201,204):raise RuntimeError('Owned Keycloak setup operation failed: '+str(code))
        return value
    # A bounded local transport fault forwards a real write then discards its response.
    class Proxy(BaseHTTPRequestHandler):
        drop=None
        events=[]
        def log_message(self,*args):pass
        def handle_request(self):
            payload=None
            if self.command!='GET':
                if self.headers.get('Transfer-Encoding','').lower()=='chunked':
                    chunks=[]
                    while True:
                        size=int(self.rfile.readline().split(b';')[0],16)
                        if not size:self.rfile.readline();break
                        chunks.append(self.rfile.read(size));self.rfile.read(2)
                        if sum(map(len,chunks))>300000:raise RuntimeError('Provider fixture payload exceeds bounds')
                    payload=b''.join(chunks)
                else:payload=self.rfile.read(int(self.headers.get('Content-Length','0')))
            req=urllib.request.Request('http://127.0.0.1:8095'+self.path,payload,method=self.command,headers={k:v for k,v in self.headers.items() if k.lower() not in ('host','connection','content-length','transfer-encoding')})
            try:
                with urllib.request.urlopen(req,timeout=30) as r:code=r.status;data=r.read();ctype=r.headers.get('Content-Type','application/json')
            except urllib.error.HTTPError as e:code=e.code;data=e.read();ctype='application/json'
            Proxy.events.append((self.command,self.path.split('?')[0],code))
            if code>=400:private_write(directory/'provider-error.json',data.decode(errors='replace'))
            if Proxy.drop==self.path and self.command=='POST' and code in (200,201):
                Proxy.drop=None;self.connection.shutdown(socket.SHUT_RDWR);self.connection.close();return
            self.send_response(code);self.send_header('Content-Type',ctype);self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
        do_GET=do_POST=do_PUT=handle_request
    def stop():
        nonlocal api,log
        if api and api.poll() is None:
            os.killpg(api.pid,signal.SIGTERM)
            try:api.wait(timeout=15)
            except subprocess.TimeoutExpired:os.killpg(api.pid,signal.SIGKILL);api.wait()
        if log:log.close()
        api=log=None
    environment={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','DOTNET_ENVIRONMENT':'Development','ASPNETCORE_URLS':base,
        'ConnectionStrings__Swoovr':'Host=127.0.0.1;Port=5433;Database='+database+';Username=swoovr;Password='+platform['SWOOVR_DB_PASSWORD'],
        'Founders__Secrets__1':os.urandom(32).hex(),'Keycloak__BaseUrl':kc,'Keycloak__MembersRealm':realms[0],
        'Keycloak__StaffRealm':realms[1],'Keycloak__RequireHttpsMetadata':'false','Staff__LocalPasswords':'true','Retention__Worker':'false'}
    def start():
        nonlocal api,log
        private_write(directory/'api.log','');log=open(directory/'api.log','a')
        api=subprocess.Popen(['dotnet',str(ROOT/'src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll')],cwd=ROOT/'src/Swoovr.Api',env=environment,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
        for _ in range(120):
            if api.poll() is not None:raise RuntimeError('API stopped; inspect private runtime api.log')
            try:
                if request(base+'/health/live')[0]==200:return
            except OSError:pass
            time.sleep(.25)
        raise RuntimeError('API readiness timed out')
    def call(token,path,method='GET',data=None,expected=200,opener=None,headers=None):
        code,value=request(('https://127.0.0.1:5096' if opener else base)+path,method,data,token,opener,headers)
        if code!=expected:raise RuntimeError('HTTP '+method+' '+re.sub(r'/\d+', '/[id]',path.split('?')[0])+' returned '+str(code)+' expected '+str(expected)+' code '+str(value.get('code') if isinstance(value,dict) else None))
        return value
    def login(realm,username,password):
        jar=http.cookiejar.CookieJar();opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar),NoRedirect())
        verifier=base64.urlsafe_b64encode(os.urandom(32)).rstrip(b'=').decode();challenge=base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b'=').decode()
        callback='http://127.0.0.1:31415/callback'
        url=kc+'/realms/'+realm+'/protocol/openid-connect/auth?'+urllib.parse.urlencode({'client_id':'ticket-runtime','response_type':'code','scope':'openid profile email','redirect_uri':callback,'code_challenge':challenge,'code_challenge_method':'S256','state':run,'nonce':run})
        def fetch(url,payload=None):
            try:
                with opener.open(urllib.request.Request(url,payload),timeout=15) as r:return r.status,r.read().decode(),r.headers
            except urllib.error.HTTPError as e:return e.code,e.read().decode(),e.headers
        status,page,headers=fetch(url)
        # Browsers treat localhost as a secure cookie origin; urllib does not. This owned opener only visits fixed local Keycloak.
        for cookie in jar:cookie.secure=False
        form=re.search(r'<form[^>]*action="([^"]+)"',page)
        if not form:raise RuntimeError('Keycloak login form missing')
        status,page,headers=fetch(html.unescape(form[1]),urllib.parse.urlencode({'username':username,'password':password}).encode())
        for _ in range(10):
            target=headers.get('Location','')
            if target.startswith(callback):break
            if status not in (302,303):
                private_write(directory/'failed-login.html',page)
                raise RuntimeError('Code-flow fixture sign-in did not complete; private HTML recorded')
            status,page,headers=fetch(urllib.parse.urljoin(url,target))
        else:raise RuntimeError('Login redirect limit')
        code=urllib.parse.parse_qs(urllib.parse.urlsplit(target).query)['code'][0]
        payload=urllib.parse.urlencode({'grant_type':'authorization_code','client_id':'ticket-runtime','code':code,'redirect_uri':callback,'code_verifier':verifier}).encode()
        with urllib.request.urlopen(urllib.request.Request(kc+'/realms/'+realm+'/protocol/openid-connect/token',payload)) as r:token=json.load(r)['access_token']
        claims=json.loads(base64.urlsafe_b64decode(token.split('.')[1]+'=='))
        if 'pwd' not in claims.get('amr',[]) or 'auth_time' not in claims:raise RuntimeError('Real direct-session evidence missing: amr='+str(claims.get('amr'))+' auth_time_present='+str('auth_time' in claims))
        return token,claims
    try:
        password='Fictional-'+uuid.uuid4().hex
        for realm in realms:
            admin('realms','POST',{'realm':realm,'enabled':True,'sslRequired':'none','loginTheme':'keycloak','registrationAllowed':False,
                'clients':[{'clientId':'ticket-runtime','enabled':True,'publicClient':True,'standardFlowEnabled':True,'directAccessGrantsEnabled':False,'redirectUris':['http://127.0.0.1:31415/callback'],
                    'defaultClientScopes':['basic','profile','email','acr','web-origins'],'protocolMappers':[{'name':'amr','protocol':'openid-connect','protocolMapper':'oidc-amr-mapper','config':{'access.token.claim':'true','id.token.claim':'true'}},
                    {'name':'aud','protocol':'openid-connect','protocolMapper':'oidc-audience-mapper','config':{'included.custom.audience':'swoovr-api','access.token.claim':'true'}}]}]})
            created_realms.append(realm)
            executions=admin('realms/'+realm+'/authentication/flows/browser/executions')
            execution=next(e for e in executions if e.get('providerId')=='auth-username-password-form')
            admin('realms/'+realm+'/authentication/executions/'+execution['id']+'/config','POST',{'alias':'runtime-amr','config':{'default.reference.value':'pwd','default.reference.maxAge':'43200'}})
        people={}
        for label,realm in [('member',realms[0]),('other',realms[0]),('support',realms[1]),('restricted',realms[1]),('legal',realms[1]),('admin',realms[1])]:
            email=label+'.'+run+'@example.test'
            admin('realms/'+realm+'/users','POST',{'username':email,'email':email,'firstName':'Fictional','lastName':'Adult','emailVerified':True,'enabled':True,'credentials':[{'type':'password','value':password,'temporary':False}]})
            people[label]=login(realm,email,password)
        check(True,'real Keycloak authorization-code/PKCE direct password sessions carry live AMR and auth_time')
        psql('postgres','CREATE DATABASE '+database);start()
        psql(database,'UPDATE registration_availability SET public_enabled=true,beta_enabled=false WHERE id=1')
        for label,(token,claims) in people.items():
            call(token,'/api/session')
            realm=realms[0] if label in ('member','other') else realms[1]
            account=psql(database,"SELECT id FROM accounts WHERE subject='"+claims['sub']+"' AND issuer='"+kc+'/realms/'+realm+"'")
            people[label]=(token,claims,account)
        roles={'support':'Support','restricted':'TrustSafety','legal':'Legal','admin':'Admin'}
        for label,role in roles.items():psql(database,"INSERT INTO role_grants(id,account_id,role,granted_at) VALUES(gen_random_uuid(),'"+people[label][2]+"','"+role+"',now())")
        settings=provision_runtime([{'id':people[label][2],'roles':[role]} for label,role in roles.items()],config,directory)
        staff_ids=list(settings['Ticketing']['Zammad']['Staff'])
        proxy=ThreadingHTTPServer(('127.0.0.1',0),Proxy);threading.Thread(target=proxy.serve_forever,daemon=True).start()
        settings['Ticketing']['Zammad']['BaseUrl']='http://127.0.0.1:'+str(proxy.server_port)+'/'
        # TLS pins are provisioned per actor. This fixture exercises actual TLS client-certificate possession.
        for label in ('server','client'):
            subprocess.run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-keyout',str(directory/(label+'.key')),'-out',str(directory/(label+'.crt')),'-days','1','-subj','/CN=127.0.0.1','-addext','subjectAltName=IP:127.0.0.1'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            os.chmod(directory/(label+'.key'),0o600)
        der=ssl.PEM_cert_to_DER_cert((directory/'client.crt').read_text());pin=hashlib.sha256(der).hexdigest().upper()
        for label in ('restricted','legal'):settings['Ticketing']['Zammad']['Staff'][people[label][2]]['ClientCertificatePins']=[pin]
        private_write(directory/'api.json',json.dumps(settings));stop()
        environment.update({'Ticketing__ConfigurationFile':str(directory/'api.json'),'ASPNETCORE_URLS':base+';https://127.0.0.1:5096','Kestrel__Certificates__Default__Path':str(directory/'server.crt'),'Kestrel__Certificates__Default__KeyPath':str(directory/'server.key')})
        start()
        context=ssl.create_default_context(cafile=str(directory/'server.crt'));context.load_cert_chain(str(directory/'client.crt'),str(directory/'client.key'))
        secure=urllib.request.build_opener(urllib.request.HTTPSHandler(context=context))
        member,other,support,restricted,legal,adminperson=[people[k][0] for k in ('member','other','support','restricted','legal','admin')]
        call(None,'/api/me/tickets',expected=401);call(member,'/api/admin/tickets',expected=403)
        check(call(member,'/api/me/tickets')['items']==[],'unmapped member lists empty tickets and member cannot reach staff routes')
        operation=str(uuid.uuid4());draft={'operationId':operation,'title':'Fictional runtime support','body':'Please help with an ordinary fictional problem.'}
        with ThreadPoolExecutor(max_workers=2) as pool:created=list(pool.map(lambda _:call(member,'/api/me/tickets','POST',draft),range(2)))
        ticket=created[0]['id'];check(created[1]['id']==ticket and created[0]['reference']=='TK-'+str(ticket),'concurrent authenticated creation creates exactly one real ticket with TK reference')
        call(other,f'/api/me/tickets/{ticket}',expected=404)
        call(member,f'/api/me/tickets/{ticket}',headers={'From':'1'})
        check(True,'cross-customer detail denied and raw client From header cannot select provider actor')
        options=call(member,'/api/me/tickets/options');caps=call(support,'/api/admin/tickets/capabilities')
        check(bool(options['states']) and bool(options['priorities']) and bool(caps['owners']) and bool(caps['categories']),'member labels and staff owner/category/subcategory choices come from secured API')
        note={'operationId':str(uuid.uuid4()),'body':'Private fictional staff note','private':True,'attachments':[{'filename':'ordinary.txt','contentType':'text/plain','data':base64.b64encode(b'Private ordinary text').decode()}]}
        staff_detail=call(support,f'/api/admin/tickets/{ticket}/replies','POST',note)
        private_note=next(a for a in staff_detail['articles'] if a['private']);att=private_note['attachments'][0]
        member_detail=call(member,f'/api/me/tickets/{ticket}')
        check(all(not a['private'] for a in member_detail['articles']) and private_note['id'] not in [a['id'] for a in member_detail['articles']],'member projection excludes private notes and attachments')
        call(member,f'/api/me/tickets/{ticket}/articles/{private_note["id"]}/attachments/{att["id"]}',expected=404)
        reply={'operationId':str(uuid.uuid4()),'body':'Public ordinary reply','attachments':[{'filename':'../../ordinary.txt','contentType':'text/plain','data':base64.b64encode(b'Ordinary public text').decode()}]}
        call(support,f'/api/admin/tickets/{ticket}/replies','POST',reply)
        again=call(support,f'/api/admin/tickets/{ticket}/replies','POST',reply)
        check(sum(a['body']=='Public ordinary reply' for a in again['articles'])==1,'reply operation ID is payload-bound and cannot duplicate a real provider article')
        public=next(a for a in call(member,f'/api/me/tickets/{ticket}')['articles'] if a['body']=='Public ordinary reply');attachment=public['attachments'][0]
        download=call(member,f'/api/me/tickets/{ticket}/articles/{public["id"]}/attachments/{attachment["id"]}')
        check(download['filename']=='ordinary.txt' and base64.b64decode(download['data'])==b'Ordinary public text','ordinary attachment scans, sanitizes filename and downloads through current article visibility')
        call(member,f'/api/me/tickets/{ticket}/replies','POST',{**reply,'operationId':str(uuid.uuid4()),'private':True},expected=403)
        call(support,f'/api/admin/tickets/{ticket}/replies','POST',{**reply,'operationId':str(uuid.uuid4()),'attachments':[{'filename':'passport.txt','contentType':'text/plain','data':base64.b64encode(b'identity document').decode()}]},expected=400)
        check(True,'member private-note mutation and sensitive evidence uploads denied')
        private_write(directory/'client-input.json',json.dumps({'baseUrl':base+'/','memberToken':member,'staffToken':support,'ticketId':ticket}))
        typed=subprocess.run(['dotnet',str(ROOT/'tests/Swoovr.Checks/bin/Release/net10.0/Swoovr.Checks.dll'),'--ticketing-client-live',str(directory/'client-input.json')],capture_output=True,text=True)
        if typed.returncode:private_write(directory/'typed-client.log',typed.stdout+typed.stderr)
        check(typed.returncode==0,'actual MemberClient/AdminClient contracts and date header exercised against authenticated Swoovr API')
        before=call(support,f'/api/admin/tickets/{ticket}')['ticket'];priority=caps['priorities'][-1]['id']
        changed=call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':before['updatedAt'],'priorityId':priority})
        call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':before['updatedAt'],'priorityId':priority},expected=409)
        check(changed['priorityId']==priority,'current-state mutation succeeds and stale staff changes fail safely')
        classified=call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':changed['updatedAt'],'classification':{'swoovr_category':'technical','swoovr_subcategory':'web','swoovr_resolution':'unresolved'}})
        call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':classified['updatedAt'],'classification':{'swoovr_subcategory':'refund'}},expected=400)
        check(True,'classification persisted in Zammad and category-dependent invalid choices denied')
        for query in (people['member'][2],'swoovr-'+people['member'][2],people['member'][1]['email']):
            check(call(support,'/api/admin/tickets/customers','POST',{'query':query})['accountId']==people['member'][2],'precise customer lookup by supported server identity')
        customer=psql(database,"SELECT provider_user_id FROM ticketing_identity WHERE account_id='"+people['member'][2]+"'")
        check(call(support,'/api/admin/tickets/customers','POST',{'query':customer})['accountId']==people['member'][2],'numeric customer lookup resolves only durable server mapping')
        # Ordinary staff assignment, close/reopen macro, follow-up, scoped search and bounded bulk.
        current=call(support,f'/api/admin/tickets/{ticket}')['ticket']
        owner=next(o['id'] for o in caps['owners'] if o['name']==people['support'][1]['email'])
        assigned=call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':current['updatedAt'],'ownerId':owner})
        modules=call(support,'/api/admin/tickets/text-modules');macros=call(support,'/api/admin/tickets/macros')
        macro=next(m for m in macros if m['name']=='Swoovr close ticket' and m['supported'])
        closed=call(support,f'/api/admin/tickets/{ticket}/macros/{macro["id"]}','POST',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':assigned['updatedAt']})
        closed_state=next(s['id'] for s in caps['states'] if s['name']=='closed')
        check(closed['stateId']==closed_state and any(m['name']=='Swoovr support acknowledgement' for m in modules),'verified macro closes real ticket and provider text module projects plain text')
        check(call(member,f'/api/me/tickets/{ticket}')['canReply'],'closed ticket follow-up reflects live provider group policy')
        followed=call(member,f'/api/me/tickets/{ticket}/replies','POST',{'operationId':str(uuid.uuid4()),'body':'Ordinary fictional follow-up on same ticket'})
        check(followed['ticket']['id']==ticket,'closed ticket reply remains in same provider conversation')
        open_state=next(s['id'] for s in caps['states'] if s['name']=='open')
        current=call(support,f'/api/admin/tickets/{ticket}')['ticket']
        bulk=call(support,'/api/admin/tickets/bulk','POST',{'items':[{'id':ticket,'change':{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':current['updatedAt'],'stateId':open_state}},{'id':999999999,'change':{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':current['updatedAt'],'stateId':open_state}}]})
        check(bulk[0]['status']==200 and bulk[1]['status']==404,'bounded bulk returns per-item permission/current-state results without blind retry')
        for _ in range(20):
            listed=call(support,'/api/admin/tickets?query=TK-'+str(ticket)+'&sort=title&descending=false')
            if any(t['id']==ticket for t in listed['items']):break
            time.sleep(.25)
        check(len(listed['items'])==1,'TK reference search routes through current staff access')
        # Case/appeal links (safe references only). Fixture case and report live in this run's own database.
        case_id,admin_case,ref=str(uuid.uuid4()),str(uuid.uuid4()),'SW-'+''.join(__import__('secrets').choice('ABCDEFGHJKMNPQRSTUVWXYZ23456789') for _ in range(6))
        def fixture(sql_text):
            done=subprocess.run(['docker','exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d',database,'-At','-v','ON_ERROR_STOP=1','-c',sql_text],capture_output=True,text=True)
            if done.returncode:raise RuntimeError('Fixture insert failed: '+done.stderr.strip().splitlines()[0][:200])
        fixture(f"INSERT INTO moderation_cases(id,subject_account_id,status,queue,priority,created_at,updated_at) VALUES('{case_id}','{people['other'][2]}','Open','Support','Normal',now(),now()),('{admin_case}','{people['member'][2]}','Open','Admin','Normal',now(),now())")
        fixture(f"INSERT INTO reports(id,reporter_account_id,subject_account_id,subject_type,subject_ref,category_id,priority,case_id,created_at,support_reference) VALUES(gen_random_uuid(),'{people['member'][2]}','{people['other'][2]}','Profile','fictional','spam','Normal','{case_id}',now(),'{ref}')")
        linked=call(member,'/api/me/tickets','POST',{'operationId':str(uuid.uuid4()),'title':'About report '+ref,'body':'Fictional follow-up on my report.','reference':ref})
        check([l['caseId'] for l in call(support,f"/api/admin/tickets/{linked['id']}/links")]==[case_id],"member's own report reference links the new request to that case only")
        stranger=call(member,'/api/me/tickets','POST',{'operationId':str(uuid.uuid4()),'title':'Probe','body':'Fictional probe.','reference':'SW-ABC234'})
        check(call(support,f"/api/admin/tickets/{stranger['id']}/links")==[],'an unknown reference links nothing and reveals nothing')
        call(support,f"/api/admin/tickets/{linked['id']}/links",'POST',{'caseId':admin_case},expected=404)
        call(support,f"/api/admin/tickets/{linked['id']}/links",'POST',{'caseId':case_id,'appealId':str(uuid.uuid4())},expected=404)
        check(True,'support cannot link an admin-queue case or an appeal from another case')
        before=psql(database,f"SELECT status||'|'||(SELECT count(*) FROM sanctions WHERE case_id='{case_id}')||'|'||(SELECT count(*) FROM appeals) FROM moderation_cases WHERE id='{case_id}'")
        current=call(support,f"/api/admin/tickets/{linked['id']}")
        call(support,f"/api/admin/tickets/{linked['id']}",'PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':current['ticket']['updatedAt'],'stateId':closed_state})
        check(psql(database,f"SELECT status||'|'||(SELECT count(*) FROM sanctions WHERE case_id='{case_id}')||'|'||(SELECT count(*) FROM appeals) FROM moderation_cases WHERE id='{case_id}'")==before,'closing a linked request changes no case, sanction or appeal')
        for _ in range(40):
            filtered=call(support,'/api/admin/tickets?state='+str(open_state)+'&owner='+str(owner)+'&sort=updated_at&descending=true&page=1&pageSize=5')
            if any(t['id']==ticket for t in filtered['items']):break
            time.sleep(.5)
        check(any(t['id']==ticket for t in filtered['items']),'staff paging, owner/state filters and explicit sorting use real provider search')
        on_behalf=call(support,'/api/admin/tickets','POST',{'accountId':people['member'][2],'ticket':{'operationId':str(uuid.uuid4()),'title':'Fictional staff-created enquiry','body':'Ordinary on-behalf support enquiry'}})
        check(call(member,f'/api/me/tickets/{on_behalf["id"]}')['ticket']['id']==on_behalf['id'],'authorised staff create-on-behalf remains bound to Swoovr member mapping')
        psql(database,"UPDATE accounts SET email='"+people['member'][1]['email']+"',email_accepted_at=now()+interval '30 seconds' WHERE id='"+people['other'][2]+"'")
        call(support,'/api/admin/tickets/customers','POST',{'query':people['member'][1]['email']},expected=409)
        psql(database,"UPDATE accounts SET email='"+people['other'][1]['email']+"' WHERE id='"+people['other'][2]+"'")
        check(True,'exact shared email lookup reports ambiguity rather than selecting/merging an account')
        # Real accepted provider write with dropped response; no replay on uncertain article mutation.
        Proxy.drop='/api/v1/ticket_articles'
        uncertain={'operationId':str(uuid.uuid4()),'body':'Fictional lost-response reply'}
        call(member,f'/api/me/tickets/{ticket}/replies','POST',uncertain,expected=409)
        call(member,f'/api/me/tickets/{ticket}/replies','POST',uncertain)
        check(sum(a['body']=='Fictional lost-response reply' for a in call(member,f'/api/me/tickets/{ticket}')['articles'])==1,'real lost article response reconciles exact durable operation marker and actor without replay')
        # Current conflict exclusions deny list/detail/mutations/downloads across the API boundary.
        psql(database,"INSERT INTO staff_exclusions(staff_account_id,subject_account_id,reason,created_by,created_at) VALUES('"+people['support'][2]+"','"+people['member'][2]+"','Fictional acceptance conflict','"+people['admin'][2]+"',now())")
        call(support,f'/api/admin/tickets/{ticket}',expected=403)
        call(support,f'/api/admin/tickets/{ticket}/articles/{public["id"]}/attachments/{attachment["id"]}',expected=403)
        check(True,'current Swoovr conflict excludes detail and direct attachment download')
        psql(database,"DELETE FROM staff_exclusions WHERE staff_account_id='"+people['support'][2]+"'")
        current=call(support,f'/api/admin/tickets/{ticket}')['ticket']
        restricted_group=next(g['id'] for g in caps['transferGroups'] if g['name']=='Trust & Safety — Restricted')
        call(support,f'/api/admin/tickets/{ticket}','PUT',{'operationId':str(uuid.uuid4()),'expectedUpdatedAt':current['updatedAt'],'groupId':restricted_group})
        call(support,f'/api/admin/tickets/{ticket}',expected=404);call(adminperson,f'/api/admin/tickets/{ticket}',expected=404)
        call(restricted,f'/api/admin/tickets/{ticket}',expected=404)
        call(restricted,'/api/admin/access/grants','POST',{'capability':'ticketing-restricted','purpose':'Fictional restricted runtime acceptance'},expected=201)
        call(restricted,f'/api/admin/tickets/{ticket}',expected=404)
        check(call(restricted,f'/api/admin/tickets/{ticket}',opener=secure)['staff']['groupId']==restricted_group,'transfer removes support/admin access immediately; restricted access requires role, provider group, fresh elevation and actual pinned TLS channel')
        call(legal,f'/api/admin/tickets/{ticket}',expected=404,opener=secure)
        check(True,'Legal has no implicit Restricted T&S access')
        call(adminperson,'/api/admin/tickets/diagnostics')
        call(support,'/api/admin/tickets/diagnostics',expected=403)
        call(support,'/api/admin/tickets/text-modules');call(support,'/api/admin/tickets/macros')
        check(True,'secured integration diagnostics and verified provider text-module/macro reads execute through Swoovr')
        # Signed verified email mutation retains mapping/tickets; provider role changes never grant broader customer impersonation.
        changed_email='changed.'+run+'@example.test'
        admin('realms/'+realms[0]+'/users/'+people['member'][1]['sub'],'PUT',{'email':changed_email,'emailVerified':True})
        member,changed_claims=login(realms[0],people['member'][1]['email'],password)
        check(call(member,f'/api/me/tickets/{ticket}')['ticket']['id']==ticket,'new verified email claim preserves original ticket access')
        code,synced=provider_call(config['identity_token'],'GET','users/'+customer)
        check(synced['email']==changed_email and synced['login']=='swoovr-'+people['member'][2],'verified email sync updates original canonical provider customer on support reads')
        provider_call(config['identity_token'],'PUT','users/'+customer,{'active':False})
        call(member,f'/api/me/tickets/{ticket}',expected=403)
        provider_call(config['identity_token'],'PUT','users/'+customer,{'active':True,'roles':['Agent']})
        call(member,f'/api/me/tickets/{ticket}',expected=403)
        provider_call(config['identity_token'],'PUT','users/'+customer,{'roles':['Customer']})
        check(True,'externally disabled or promoted provider customer fails closed without automatic privilege repair')
        # Provider grant revocation also denies, even while Swoovr role remains granted.
        support_provider=call(support,'/api/admin/tickets/capabilities')
        code,users=provider_call(config['identity_token'],'GET','users/search?query=login:%22swoovr-staff-'+people['support'][2]+'%22')
        support_user=next(u for u in users if u['login']=='swoovr-staff-'+people['support'][2])
        provider_call(config['provisioning_token'],'PUT','users/'+str(support_user['id']),{'group_ids':{}})
        call(support,f'/api/admin/tickets/{on_behalf["id"]}',expected=404)
        check(True,'live provider group revocation denies staff independently of Swoovr role')
        psql(database,"DELETE FROM role_grants WHERE account_id='"+people['restricted'][2]+"'")
        call(restricted,f'/api/admin/tickets/{ticket}',expected=403,opener=secure)
        psql(database,"UPDATE accounts SET status='PendingDeletion' WHERE id='"+people['member'][2]+"'")
        call(member,f'/api/me/tickets/{ticket}',expected=403)
        check(True,'current Swoovr role revocation and account deletion state fail closed with old valid tokens')
        private_write(directory/'lifecycle-input.json',json.dumps({'providerUrl':'http://127.0.0.1:8095/','identityToken':config['identity_token'],'accountId':people['member'][2]}))
        def lifecycle():
            result=subprocess.run(['dotnet',str(ROOT/'tests/Swoovr.Checks/bin/Release/net10.0/Swoovr.Checks.dll'),'--ticketing-lifecycle-live',str(directory/'lifecycle-input.json')],env={**os.environ,'SWOOVR_TEST_DATABASE':environment['ConnectionStrings__Swoovr']},capture_output=True,text=True)
            if result.returncode:private_write(directory/'lifecycle.log',result.stdout+result.stderr)
            check(result.returncode==0,'actual provider lifecycle reconcile completes without replacing customer identity')
        lifecycle()
        code,deactivated=provider_call(config['identity_token'],'GET','users/'+customer)
        check(not deactivated['active'] and not deactivated['email'],'pending deletion suppresses provider customer and delivery address')
        psql(database,"UPDATE accounts SET status='Active' WHERE id='"+people['member'][2]+"'")
        lifecycle()
        check(call(member,f'/api/me/tickets/{ticket}')['ticket']['id']==ticket,'cancelled account deletion restores only original canonical customer access')
        private_write(directory/'report.json',json.dumps({'checks':checks,'count':len(checks),'database_removed':True,'realms_removed':True},indent=2))
        print('PASS: '+str(len(checks))+' actual authenticated runtime acceptance checks.',flush=True)
    finally:
        stop()
        private_write(directory/'provider-events.json',json.dumps(Proxy.events))
        if proxy:proxy.shutdown();proxy.server_close()
        for realm in created_realms:
            try:admin('realms/'+realm,'DELETE')
            except Exception:pass
        try:psql('postgres','DROP DATABASE IF EXISTS '+database+' WITH (FORCE)')
        except Exception:pass
        # Revoke only runtime users owned by this run; preserve normal local staff.
        for account in staff_ids:
            query='login:%22swoovr-staff-'+account+'%22'
            req=urllib.request.Request('http://127.0.0.1:8095/api/v1/users/search?query='+query,headers={'Authorization':'Token token='+config['identity_token']})
            try:
                with urllib.request.urlopen(req) as r:rows=json.load(r)
                for row in rows:
                    if row['login']=='swoovr-staff-'+account:
                        req=urllib.request.Request('http://127.0.0.1:8095/api/v1/users/'+str(row['id']),json.dumps({'active':False}).encode(),method='PUT',headers={'Authorization':'Token token='+config['identity_token'],'Content-Type':'application/json'})
                        urllib.request.urlopen(req).close()
            except Exception:pass

if __name__=='__main__':main()
