#!/usr/bin/env python3
"""Initial disabled UAT realm/service-client bootstrap. Never enables/overwrites realms."""
import json,os,re,urllib.request,urllib.parse,urllib.error

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None

class Transport:
    def __init__(self,base,development=False):
        url=urllib.parse.urlsplit(base)
        local=development and url.scheme=='http' and (url.hostname=='localhost' or re.fullmatch(r'swoovr-uat-image-[0-9a-f]{12}-kc',url.hostname or ''))
        if not(local or url.scheme=='https') or not url.hostname or url.username or url.password or url.path or url.query or url.fragment:raise ValueError('An exact verified Keycloak origin is required.')
        self.base=base;self.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect())
    def __call__(self,path,method='GET',data=None,token=None,form=False):
        if not path.startswith(('/admin/realms','/realms/master/protocol/openid-connect/token')):raise ValueError('Unsupported bootstrap endpoint.')
        encoded=None if data is None else (urllib.parse.urlencode(data) if form else json.dumps(data)).encode()
        headers={'Accept':'application/json'}
        if encoded is not None:headers['Content-Type']='application/x-www-form-urlencoded' if form else 'application/json'
        if token:headers['Authorization']='Bearer '+token
        request=urllib.request.Request(self.base+path,data=encoded,headers=headers,method=method)
        try:response=self.opener.open(request,timeout=15)
        except urllib.error.HTTPError as error:response=error
        with response:
            body=response.read(1048577)
            if len(body)>1048576:raise ValueError('Oversized bootstrap response.')
            result=json.loads(body) if body.strip() and body.lstrip().startswith((b'{',b'[')) else None
            return response.status,result

def prepared_realms(env):
    results=[]
    def expand(value):
        if isinstance(value,dict):return {k:expand(v) for k,v in value.items()}
        if isinstance(value,list):return [expand(v) for v in value]
        if isinstance(value,str) and value.startswith('${'):
            match=re.fullmatch(r'\$\{(SWOOVR_UAT_(?:MEMBERS_WEB_SECRET|VENUES_WEB_SECRET|STAFF_WEB_SECRET|ENTRA_CLIENT_SECRET))\}',value)
            if not match or not env.get(match[1],'').strip():raise ValueError('Missing or unsupported realm secret binding.')
            return env[match[1]]
        return value
    for kind in ['members','venues','staff']:
        raw=env['SWOOVR_UAT_REALM_'+kind.upper()]
        if len(raw)>131072:raise ValueError('Oversized realm input.')
        value=json.loads(raw)
        if value.get('realm')!='swoovr-'+kind or value.get('enabled') is not False or value.get('users')!=[] or value.get('sslRequired')!='all' or value.get('attributes',{}).get('swoovr.environment')!='uat-preparation-disabled':raise ValueError('Expected disabled userless UAT realm preparation.')
        results.append(expand(value))
    return results

def bootstrap(request,env):
    realms=prepared_realms(env)
    secret=env['SWOOVR_UAT_MEMBER_ADMIN_SECRET']
    if len(secret)<32:raise ValueError('Explicit service client secret required.')
    code,body=request('/realms/master/protocol/openid-connect/token','POST',{'client_id':'admin-cli','grant_type':'password','username':env.get('KC_BOOTSTRAP_ADMIN_USERNAME','swoovr-bootstrap'),'password':env['KC_BOOTSTRAP_ADMIN_PASSWORD']},form=True)
    if code!=200 or not isinstance(body,dict) or not body.get('access_token'):raise ValueError('Bootstrap authentication refused.')
    token=body['access_token']
    def api(path,method='GET',data=None,expected=200):
        code,value=request('/admin/realms'+path,method,data,token)
        if code!=expected:raise ValueError('Bootstrap request refused or uncertain; inspect partial state before retrying.')
        return value
    existing=api('')
    if not isinstance(existing,list) or any(r.get('realm') in {'swoovr-members','swoovr-venues','swoovr-staff'} for r in existing):raise ValueError('UAT realms already exist; refusing overwrite or activation.')
    for realm in realms:api('','POST',realm,201)
    base='/swoovr-members'
    api(base+'/clients','POST',{'clientId':'swoovr-api-accounts','name':'Swoovr API account administration','enabled':True,'protocol':'openid-connect','publicClient':False,'clientAuthenticatorType':'client-secret','secret':secret,'serviceAccountsEnabled':True,'standardFlowEnabled':False,'implicitFlowEnabled':False,'directAccessGrantsEnabled':False,'fullScopeAllowed':False,'redirectUris':[],'webOrigins':[]},201)
    def single(values):
        if not isinstance(values,list) or len(values)!=1:raise ValueError('Ambiguous bootstrap identity.')
        return values[0]
    client=single(api(base+'/clients?clientId=swoovr-api-accounts'));management=single(api(base+'/clients?clientId=realm-management'))
    user=api(base+'/clients/'+client['id']+'/service-account-user')
    roles=[api(base+'/clients/'+management['id']+'/roles/'+name) for name in ['view-users','manage-users']]
    api(base+'/users/'+user['id']+'/role-mappings/clients/'+management['id'],'POST',roles,204)
    api(base+'/clients/'+client['id']+'/scope-mappings/clients/'+management['id'],'POST',roles,204)
    print('Disabled UAT realms and restricted member service client created. No realm enabled; retire/protect temporary bootstrap credentials and complete hosted acceptance before activation.')
    return 0

def main():
    return bootstrap(Transport(os.environ['SWOOVR_KEYCLOAK_BASE'],os.environ.get('DOTNET_ENVIRONMENT')=='Development'),os.environ)
if __name__=='__main__':
    try:raise SystemExit(main())
    except (ValueError,KeyError,OSError):raise SystemExit('Keycloak bootstrap failed; no success or activation. Review partial state privately before retrying.')
