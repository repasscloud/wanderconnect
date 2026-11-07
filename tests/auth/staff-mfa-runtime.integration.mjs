// Opt-in: Compose-built local image, owned Keycloak container and disposable PostgreSQL database only.
// node tests/auth/staff-mfa-runtime.integration.mjs swoovr-keycloak-local:development
// No application/production deployment. Secrets, HTML and token bodies are never printed or persisted.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFile, writeFile, mkdtemp, rm } from 'node:fs/promises';
import { randomBytes, createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createServer } from 'node:net';
import { applyRealm, applyStaffMfa } from '../../scripts/auth/realms.mjs';
import { totp } from '../../scripts/auth/totp.mjs';
import { enrollmentSecret } from '../../scripts/local/platform.mjs';

const fetch=(url,options={})=>globalThis.fetch(url,{signal:AbortSignal.timeout(15000),...options});
const image = process.argv[2]; if (!image) throw Error('Pass the Compose-built local Keycloak image name');
const docker = (...args) => execFileSync('docker', args, { encoding:'utf8', stdio:['pipe','pipe','pipe'] });
const inspect = name => JSON.parse(docker('inspect',name))[0];
const tag=randomBytes(6).toString('hex'), container='swoovr-mfa-check-'+tag, database='swoovr_mfa_check_'+tag;
const directory=await mkdtemp(join(tmpdir(),'swoovr-mfa-check-'));
const pg=inspect('swoovr-platform-postgres-1'), pgEnv=Object.fromEntries(pg.Config.Env.map(v=>v.split(/=(.*)/s).slice(0,2)));
const networkOwner=pg.HostConfig.NetworkMode.startsWith("container:")?inspect(pg.HostConfig.NetworkMode.slice(10)):pg;
const [network, endpoint]=Object.entries(networkOwner.NetworkSettings.Networks)[0];
const bootstrap=randomBytes(32).toString('base64url'), password=randomBytes(24).toString('base64url'), clientSecret=randomBytes(32).toString('base64url');
const server=createServer(); await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port; await new Promise(r=>server.close(r));
const base='http://127.0.0.1:'+port, redirect='http://localhost:5082/signin-oidc';
let created=false,started=false, bearer, checks=0;
function check(ok,name){assert.ok(ok,name);checks++;}
async function api(realm,path,method='GET',body,retry=true){
 const r=await fetch(base+'/admin/realms/'+realm+path,{method,headers:{Authorization:'Bearer '+bearer,'Content-Type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});
 if(r.status===401&&retry){
  const renewed=await fetch(base+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'mfa-check',password:bootstrap})});
  if(!renewed.ok)throw Error('Isolated management renewal refused');bearer=(await renewed.json()).access_token;
  return api(realm,path,method,body,false);
 }
 if(!r.ok)throw Error('Isolated provider admin status '+r.status+' on '+method+' '+path);
 return r.status===204||r.headers.get('content-length')==='0'?undefined:r.json().catch(()=>undefined);
}
const decode=s=>s.replaceAll('&amp;','&').replaceAll('&quot;','"').replaceAll('&#39;',"'");
async function login(realm,email,fixture={}){
 const state=randomBytes(20).toString('hex'), verifier=randomBytes(32).toString('base64url');
 const query=new URLSearchParams({client_id:'swoovr-admin-web',redirect_uri:redirect,response_type:'code',scope:'openid email',state,
  code_challenge:createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256',prompt:'login',max_age:'0'});
 let url=base+'/realms/'+realm+'/protocol/openid-connect/auth?'+query,method='GET',body,cookies=fixture.cookies??new Map(),enrolled=false,otp=false;
 fixture.cookies=cookies;
 for(let step=0;step<25;step++){
  if(url.startsWith(redirect)){
   const returned=new URL(url);check(returned.searchParams.get('state')===state,'OIDC state retained');
   const response=await fetch(base+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'authorization_code',client_id:'swoovr-admin-web',client_secret:clientSecret,redirect_uri:redirect,code:returned.searchParams.get('code'),code_verifier:verifier})});
   check(response.ok,'code+PKCE completed');const tokens=await response.json();return { claims:JSON.parse(Buffer.from(tokens.access_token.split('.')[1],'base64url')),enrolled,otp,fixture };
  }
  if(new URL(url).origin!==base)throw Error('Unexpected isolated authentication origin');
  const r=await fetch(url,{method,body,redirect:'manual',headers:{Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; '),...(body?{'Content-Type':'application/x-www-form-urlencoded'}:{})}});
  for(const cookie of r.headers.getSetCookie()){const pair=cookie.split(';')[0],at=pair.indexOf('=');cookies.set(pair.slice(0,at),pair.slice(at+1));}
  if(r.status>=300&&r.status<400){url=new URL(r.headers.get('location'),url).href;method='GET';body=undefined;continue;}
  if(!r.ok)throw Error('Isolated sign-in status '+r.status+' step '+step);
  const html=await r.text(),form=/<form\b[^>]*action="([^"]+)"[^>]*>([\s\S]*?)<\/form>/.exec(html);
  if(!form)throw Error('Expected isolated native authentication form');
  const fields=new URLSearchParams();for(const match of form[2].matchAll(/<input\b[^>]*>/g)){
   const input=match[0],name=/\bname="([^"]+)"/.exec(input)?.[1];if(name&&/\btype="hidden"/.test(input))fields.set(name,decode(/\bvalue="([^"]*)"/.exec(input)?.[1]??''));
  }
  if(/name="password"/.test(form[2])){fields.set('username',email);fields.set('password',password);fields.set('credentialId','');}
  else if(/name="userLabel"/.test(form[2])){
   if(enrolled)throw Error('Native enrolment was refused');enrolled=true;
   fixture.secret=enrollmentSecret(fields.get('totpSecret'));fixture.step=Math.floor(Date.now()/30000);
   fields.set('totp',totp(fixture.secret));fields.set('userLabel','Owned MFA check');
  }
  else if(/name="otp"/.test(form[2])){
   if(otp)throw Error('Native OTP was refused');otp=true;if(!fixture.secret)throw Error('Missing owned fixture factor');
   while(Math.floor(Date.now()/30000)<=(fixture.step??-1))await new Promise(r=>setTimeout(r,250));fixture.step=Math.floor(Date.now()/30000);fields.set('otp',totp(fixture.secret));
  }
  else if(/name="accept"/.test(form[2]))fields.set('accept','Accept');
  else throw Error('Unexpected isolated native authentication step');
  url=new URL(decode(form[1]),url).href;method='POST';body=fields;
 }
 throw Error('Isolated authentication step limit');
}
try{
 docker('exec','swoovr-platform-postgres-1','psql','-p','5433','-U',pgEnv.POSTGRES_USER,'-d','postgres','-c','CREATE DATABASE '+database);created=true;
 const environment=[['KC_BOOTSTRAP_ADMIN_USERNAME','mfa-check'],['KC_BOOTSTRAP_ADMIN_PASSWORD',bootstrap],['KC_DB','postgres'],['KC_DB_URL','jdbc:postgresql://'+endpoint.IPAddress+':5433/'+database],['KC_DB_USERNAME',pgEnv.POSTGRES_USER],['KC_DB_PASSWORD',pgEnv.POSTGRES_PASSWORD],['JAVA_OPTS_APPEND','-Xms128m -Xmx384m']].map(([k,v])=>k+'='+v).join('\n');
 const envFile=join(directory,'runtime.env');await writeFile(envFile,environment,{mode:0o600});
 docker('run','-d','--name',container,'--label','swoovr.owned-mfa-check='+tag,'--memory','768m','--cpus','1','--network',network,'--env-file',envFile,'-p','127.0.0.1:'+port+':8080',image,'start-dev','--features=update-email','--log-level=INFO');started=true;
 for(let i=0;i<90;i++){try{if((await fetch(base+'/realms/master/.well-known/openid-configuration')).ok)break;}catch{}if(i===89)throw Error('Isolated provider did not become ready');await new Promise(r=>setTimeout(r,1000));}
 const token=await fetch(base+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'mfa-check',password:bootstrap})});check(token.ok,'isolated management authority');bearer=(await token.json()).access_token;
 for(const [realm,email,required] of [['swoovr-staff-dev','fictional.staff@example.test',true]]){
  const spec=JSON.parse(await readFile('containers/keycloak/realms/'+realm+'.json','utf8'));
  spec.sslRequired='none';spec.loginTheme='keycloak.v2';spec.smtpServer={};spec.users=[];spec.clients=spec.clients.filter(c=>c.clientId==='swoovr-admin-web'||c.clientId==='swoovr-web').slice(0,1);
  Object.assign(spec.clients[0],{clientId:'swoovr-admin-web',secret:clientSecret,redirectUris:[redirect]});
  spec.attributes['swoovr.registrationTerms']='DISABLED';spec.registrationAllowed=false;
  await api('', '', 'POST',spec);const open=(path,method,body)=>api(realm,path,method,body);
  await applyRealm(open,realm);await applyStaffMfa(open,realm);
  await api(realm,'/users','POST',{username:email,email,emailVerified:true,enabled:true,firstName:'Fictional',lastName:'Adult',requiredActions:[],credentials:[{type:'password',temporary:false,value:password}]});
  const first=await login(realm,email);console.log('Native first login completed for '+realm);check(first.enrolled===required,'staff without OTP must complete native enrolment');
  if(required){const second=await login(realm,email,first.fixture);check(second.otp&&second.claims.amr?.includes('otp'),'staff repeated login proves native OTP AMR');check(second.claims.sid===first.claims.sid&&second.claims.auth_time>first.claims.auth_time,'native forced reauthentication retains sid and advances signed auth_time');}

 }
 console.log('PASS: isolated Keycloak26.8.1 staff MFA runtime integration ('+checks+' checks; fictional verified-email assertion only; Compose-built image; no Entra/mail/device acceptance).');
}finally{
 if(started)docker('rm','-f',container);
 if(created)docker('exec','swoovr-platform-postgres-1','psql','-p','5433','-U',pgEnv.POSTGRES_USER,'-d','postgres','-c','DROP DATABASE '+database+' WITH (FORCE)');
 await rm(directory,{recursive:true,force:true});
}
