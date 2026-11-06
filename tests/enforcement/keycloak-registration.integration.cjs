// Entirely disposable provider/runtime acceptance. Synthetic HTTP authority and OIDC issuer, not the real API/social provider.
// No installed realm, owner credential, service port or database is read or modified.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),os=require('node:os'),path=require('node:path');
const {execFile}=require('node:child_process');
const run=(command,args,env={})=>new Promise((resolve,reject)=>execFile(command,args,{env:{...process.env,...env},maxBuffer:4e6},(error,out)=>error?reject(new Error(command+' exited '+error.code)):resolve(out.trim())));
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const decode=s=>s.replaceAll('&amp;','&').replaceAll('&#x3d;','=').replaceAll('&#x2f;','/');
(async()=>{
 const suffix=crypto.randomBytes(8).toString('hex'),prefix='swoovr-registration-check-'+suffix,network=prefix,pg=prefix+'-pg',authority=prefix+'-authority',kc=prefix+'-kc',extract=prefix+'-extract';
 const folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-registration-check-'));await fs.chmod(folder,0o700);
 const token=crypto.randomBytes(32).toString('hex'),password='Fictional-'+crypto.randomBytes(20).toString('hex');
 const containers=[];let networkCreated=false,checks=0;
 const check=(ok,label)=>{assert.ok(ok,label);checks++;};
 try{
  await run('docker',['network','create',network]);networkCreated=true;
  await run('docker',['create','--name',extract,'swoovr-registration-provider:source-check']);containers.push(extract);
  await run('docker',['cp',extract+':/output/swoovr-contact-policy.jar',path.join(folder,'provider.jar')]);await fs.chmod(path.join(folder,'provider.jar'),0o644);
  await fs.writeFile(path.join(folder,'authority.cjs'),String.raw`
const http=require('node:http'),crypto=require('node:crypto');
const {privateKey,publicKey}=crypto.generateKeyPairSync('rsa',{modulusLength:2048});const jwk=publicKey.export({format:'jwk'});jwk.kid='synthetic';jwk.use='sig';jwk.alg='RS256';
let policy={betaEnabled:false,publicEnabled:false,revision:1,unavailable:false};const nonces=new Map(),admissions=[];
const json=(res,status,value)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(value));};
http.createServer(async(req,res)=>{const u=new URL(req.url,'http://authority:9000');let raw='';for await(const chunk of req)raw+=chunk;
 if(u.pathname==='/control'){if(req.headers['x-control']!==process.env.CONTROL)return json(res,403,{});policy={...policy,...JSON.parse(raw)};return json(res,200,{...policy,admissions});}
 if(u.pathname==='/api/registration')return json(res,policy.unavailable?503:200,policy);
 if(u.pathname==='/api/registration/admission'){if(req.headers['x-swoovr-registration']!==process.env.CONTROL)return json(res,401,{});const email=JSON.parse(raw).email;admissions.push(email);return json(res,policy.unavailable?503:200,{allowed:policy.publicEnabled||policy.betaEnabled&&email==='invited@example.test',revision:policy.revision});}
 if(u.pathname==='/oidc/auth'){const code=crypto.randomBytes(16).toString('hex');nonces.set(code,u.searchParams.get('nonce'));const target=new URL(u.searchParams.get('redirect_uri'));target.searchParams.set('state',u.searchParams.get('state'));target.searchParams.set('code',code);res.writeHead(302,{Location:target.href});return res.end();}
 if(u.pathname==='/oidc/token'){const p=new URLSearchParams(raw),nonce=nonces.get(p.get('code'));const now=Math.floor(Date.now()/1000);const enc=v=>Buffer.from(JSON.stringify(v)).toString('base64url');const content=enc({alg:'RS256',kid:'synthetic'})+'.'+enc({iss:'http://authority:9000/oidc',aud:'synthetic-client',sub:'synthetic-new-user',iat:now,exp:now+300,nonce,email:'broker-new@example.test',email_verified:true,given_name:'Fictional',family_name:'Broker'});return json(res,200,{access_token:'synthetic-access',token_type:'Bearer',expires_in:300,id_token:content+'.'+crypto.sign('RSA-SHA256',Buffer.from(content),privateKey).toString('base64url')});}
 if(u.pathname==='/oidc/userinfo')return json(res,200,{sub:'synthetic-new-user',email:'broker-new@example.test',email_verified:true,given_name:'Fictional',family_name:'Broker'});
 if(u.pathname==='/oidc/jwks')return json(res,200,{keys:[jwk]});
 return json(res,404,{});
}).listen(9000,'0.0.0.0');
`,{mode:0o644});
  await run('docker',['run','-d','--name',authority,'--network',network,'--network-alias','authority','-p','127.0.0.1::9000','-e','CONTROL','-v',path.join(folder,'authority.cjs')+':/fixture.cjs:ro','node:22-bookworm-slim','node','/fixture.cjs'],{CONTROL:token});containers.push(authority);
  await run('docker',['run','-d','--name',pg,'--network',network,'--network-alias','postgres','-e','POSTGRES_PASSWORD','-e','POSTGRES_DB=fixture','postgres:17.11-alpine'],{POSTGRES_PASSWORD:password});containers.push(pg);
  const baseImage='quay.io/keycloak/keycloak@sha256:79c623a22abacf3fe6c00a5fb30f5b7843d717b234033b80995891c06116d244';
  await run('docker',['run','-d','--name',kc,'--network',network,'-p','127.0.0.1::8080','-e','KC_BOOTSTRAP_ADMIN_USERNAME=fixture-admin','-e','KC_BOOTSTRAP_ADMIN_PASSWORD','-e','KC_DB=postgres','-e','KC_DB_URL=jdbc:postgresql://postgres:5432/fixture','-e','KC_DB_USERNAME=postgres','-e','KC_DB_PASSWORD','-e','SWOOVR_REGISTRATION_AUTHORITY_URL=http://authority:9000/api/registration/admission','-e','SWOOVR_REGISTRATION_AUTHORITY_TOKEN','-v',path.join(folder,'provider.jar')+':/opt/keycloak/providers/swoovr-contact-policy.jar:ro','-v',path.resolve('containers/keycloak/themes/swoovr')+':/opt/keycloak/themes/swoovr:ro',baseImage,'start-dev','--http-port=8080','--hostname-strict=false'],{KC_BOOTSTRAP_ADMIN_PASSWORD:password,KC_DB_PASSWORD:password,SWOOVR_REGISTRATION_AUTHORITY_TOKEN:token});containers.push(kc);
  const port=async(name,internal)=>(await run('docker',['port',name,internal])).split(':').at(-1);
  const base='http://127.0.0.1:'+await port(kc,'8080/tcp'),mock='http://127.0.0.1:'+await port(authority,'9000/tcp');
  let ready=false;for(let i=0;i<180;i++){try{if((await fetch(base+'/realms/master',{signal:AbortSignal.timeout(1000)})).ok){ready=true;break;}}catch{}await pause(500);}check(ready,'disposable pinned Keycloak starts');
  const login=await fetch(base+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'fixture-admin',password})});const access=(await login.json()).access_token;check(!!access,'synthetic master bootstrap authentication');
  const admin=async(route,method='GET',body)=>{const r=await fetch(base+'/admin/'+route,{method,headers:{Authorization:'Bearer '+access,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});check(r.ok,'owned fixture admin operation '+method+' '+route.split('?')[0]);return r.status===204||r.status===201?null:r.json();};
  const realm='swoovr-members-fixture';await admin('realms','POST',{realm,enabled:true,sslRequired:'none',registrationAllowed:true,registrationEmailAsUsername:true,resetPasswordAllowed:true,verifyEmail:false,loginTheme:'swoovr',attributes:{'swoovr.environment':'local-development-only'},clients:[{clientId:'fixture',enabled:true,publicClient:true,standardFlowEnabled:true,redirectUris:['http://127.0.0.1:31415/callback']}],users:[{username:'existing@example.test',email:'existing@example.test',emailVerified:true,firstName:'Fictional',lastName:'Existing',enabled:true,credentials:[{type:'password',value:password,temporary:false}]}]});
  const rpath='realms/'+realm;
  const forms=await admin(rpath+'/authentication/form-action-providers'),authenticators=await admin(rpath+'/authentication/authenticator-providers');
  check(forms.some(p=>p.id==='registration-user-creation'&&p.displayName==='Swoovr guarded registration user creation'),'runtime selects guarded direct creation factory');
  check(authenticators.some(p=>p.id==='idp-create-user-if-unique'&&p.displayName==='Swoovr guarded broker user creation'),'runtime selects guarded broker creation factory');
  check(authenticators.some(p=>p.id==='auth-username-password-form'&&p.displayName==='Swoovr registration-aware username password form'),'runtime selects registration-aware login factory');
  const control=async(flags)=>{const r=await fetch(mock+'/control',{method:'POST',headers:{'X-Control':token},body:JSON.stringify(flags)});assert(r.ok);return r.json();};
  const authUrl=(registration=false,extra={})=>base+'/realms/'+realm+'/protocol/openid-connect/'+(registration?'registrations':'auth')+'?'+new URLSearchParams({client_id:'fixture',response_type:'code',scope:'openid',redirect_uri:'http://127.0.0.1:31415/callback',state:crypto.randomBytes(8).toString('hex'),...extra});
  const jar=new Map();
  async function browserRequest(url,options={}){const key=new URL(url).origin;const cookies=jar.get(key)||new Map();const r=await fetch(url,{...options,redirect:'manual',headers:{...(options.headers||{}),Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; ')}});for(const cookie of r.headers.getSetCookie()){const pair=cookie.split(';')[0],i=pair.indexOf('=');cookies.set(pair.slice(0,i),pair.slice(i+1));}jar.set(key,cookies);return r;}
  async function follow(url){let r;for(let i=0;i<15;i++){if(url.startsWith('http://127.0.0.1:31415/callback'))return{callback:true,url};r=await browserRequest(url);if(r.status>=300&&r.status<400){url=new URL(r.headers.get('location'),url).href;continue;}return{response:r,html:await r.text(),url};}throw Error('Fixture redirect limit');}
  for(const [betaEnabled,publicEnabled]of[[false,false],[true,false],[false,true],[true,true]]){
   await control({betaEnabled,publicEnabled,unavailable:false,revision:2});jar.clear();const page=await follow(authUrl());check(page.response.status===200,'signin rendered');check(page.html.includes('id="kc-registration"')===(betaEnabled||publicEnabled),'Create account link follows persisted four-state policy');check(page.html.includes('login-actions/reset-credentials'),'password recovery remains visible');
  }
  async function registration(email,expected,refusal='Registration is closed'){jar.clear();const page=await follow(authUrl(true));const action=decode(page.html.match(/<form[^>]+id="kc-register-form"[^>]+action="([^"]+)"/)?.[1]||page.html.match(/<form[^>]+action="([^"]+)"[^>]+id="kc-register-form"/)?.[1]||'');check(!!action,'registration endpoint form found');const response=await browserRequest(action,{method:'POST',body:new URLSearchParams({username:email,email,firstName:'Fictional',lastName:'Example',password,'password-confirm':password})});const html=await response.text();if(!expected){check(html.includes(refusal),'refused direct submission names the state (D335): '+refusal);if(refusal==='Registration is closed')check(!/beta|invite/i.test(html.match(/<h2>[^<]*<\/h2>\s*<p>[^<]*<\/p>/)?.[0]||''),'closed refusal never mentions beta or invitations');}const users=await admin(rpath+'/users?exact=true&username='+encodeURIComponent(email));check(users.length===(expected?1:0),'direct creation transaction matches admission');}
  await control({betaEnabled:false,publicEnabled:false,unavailable:false});await registration('closed@example.test',false);
  await control({betaEnabled:true,publicEnabled:false});await registration('uninvited@example.test',false,'Swoovr is currently invite-only');await registration('invited@example.test',true);
  await control({betaEnabled:false,publicEnabled:true});await registration('public@example.test',true);
  await control({unavailable:true});jar.clear();let page=await follow(authUrl());check(!page.html.includes('id="kc-registration"'),'authority unavailable hides Create account');await registration('unavailable@example.test',false);
  // A successful existing-password login never becomes a registration attempt when creation is closed/unavailable.
  jar.clear();page=await follow(authUrl());const action=decode(page.html.match(/<form[^>]+id="kc-form-login"[^>]+action="([^"]+)"/)?.[1]||'');const signed=await browserRequest(action,{method:'POST',body:new URLSearchParams({username:'existing@example.test',password})});check(signed.status===302&&signed.headers.get('location').startsWith('http://127.0.0.1:31415/callback'),'existing sign-in succeeds while registration authority unavailable');
  await control({betaEnabled:false,publicEnabled:false,unavailable:false});
  await admin(rpath+'/identity-provider/instances','POST',{alias:'synthetic',providerId:'oidc',enabled:true,trustEmail:true,firstBrokerLoginFlowAlias:'first broker login',config:{clientId:'synthetic-client',clientSecret:'synthetic-only',authorizationUrl:mock+'/oidc/auth',tokenUrl:'http://authority:9000/oidc/token',userInfoUrl:'http://authority:9000/oidc/userinfo',issuer:'http://authority:9000/oidc',validateSignature:'true',useJwksUrl:'true',jwksUrl:'http://authority:9000/oidc/jwks',defaultScope:'openid profile email'}});
  jar.clear();const broker=await follow(authUrl(false,{kc_idp_hint:'synthetic'}));check(broker.html?.includes('Registration is closed'),'synthetic broker first-login creation refused by guard with the neutral closed wording (D335)');check((await admin(rpath+'/users?exact=true&email=broker-new%40example.test')).length===0,'broker refusal leaves no provider account');check((await control({})).admissions.includes('broker-new@example.test'),'broker creation actually consulted synthetic authority');
  console.log('PASS disposable Keycloak26.8.1 registration: '+checks+' assertions; selected three guarded factories, four-state link rendering, direct/broker precreation refusal, unavailable failclosed and existing signin/reset visibility. Synthetic authority/OIDC only; no installed/API/social acceptance.');
 }finally{
  for(const name of containers.reverse()){try{if(name===kc)await fs.writeFile(path.join(folder,'keycloak.log'),await run('docker',['logs',name]),{mode:0o600});}catch{}await run('docker',['rm','-f','-v',name]);}
  if(networkCreated)await run('docker',['network','rm',network]);
  console.log('Owned provider resources removed; private source/runtime logs: '+folder);
 }
})().catch(error=>{console.error(error.message);process.exitCode=1;});
