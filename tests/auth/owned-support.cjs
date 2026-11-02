// Shared guard and management boundary for the disposable managed-Docker browser runner.
const fs=require('node:fs/promises'),sync=require('node:fs'),path=require('node:path');
const uuid=/^[0-9a-f]{32}$/;
function validateManifest(value,filename){
 if(!path.isAbsolute(filename)||!value||!uuid.test(value.runId)||value.database!=='swoovr_registry_ui_'+value.runId)throw Error('Invalid owned runner manifest.');
 const folder=path.dirname(filename);
 for(const key of ['envPath','rootFixture','journal'])if(value[key]!==path.join(folder,{envPath:'environment.env',rootFixture:'root.json',journal:'users.jsonl'}[key]))throw Error('Owned runner private paths must share one directory.');
 const ports=[];for(const[key,host]of[['api','127.0.0.1'],['web','localhost'],['admin','localhost']]){
  const url=new URL(value[key]);if(url.protocol!=='http:'||url.hostname!==host||url.pathname!=='/'||url.search||url.hash||url.username||url.password||!/^\d+$/.test(url.port)||Number(url.port)<1024||Number(url.port)>65535)throw Error('Owned hosts must use distinct loopback ports.');ports.push(url.port);
 }if(new Set(ports).size!==3)throw Error('Owned hosts must have distinct ports.');
 if(value.pgContainer!=='swoovr-platform-postgres-1'||value.pgPort!==5433||!Number.isSafeInteger(value.startedAt))throw Error('Owned runner requires the managed local PostgreSQL listener.');
 if(value.memberClient!=='swoovr-owned-web-'+value.runId||value.staffClient!=='swoovr-owned-admin-'+value.runId)throw Error('Owned clients must match their run id.');
 const binaries=['Debug','Release'].map(configuration=>path.resolve(__dirname,'../../src/Swoovr.Api/bin',configuration,'net10.0/Swoovr.Api.dll'));
 if(!binaries.includes(value.apiDll))throw Error('Owned operator artifact must be this checkout\'s built API.');
 return value;
}
function loadManifest(filename){
 if(!filename)return null;
 if(!path.isAbsolute(filename)||(sync.statSync(filename).mode&0o077))throw Error('Owned manifest must be an absolute private file.');
 return validateManifest(JSON.parse(sync.readFileSync(filename,'utf8')),filename);
}
function parseEnv(raw){
 const result={};for(const line of raw.split(/\r?\n/)){const m=/^([A-Z][A-Z0-9_]*)=(.*)$/.exec(line);if(!m)continue;let value=m[2];if((value.startsWith('"')&&value.endsWith('"'))||(value.startsWith("'")&&value.endsWith("'")))value=value.slice(1,-1);result[m[1]]=value;}return result;
}
async function privateEnv(filename){if(!path.isAbsolute(filename)||(await fs.stat(filename)).mode&0o077)throw Error('Use an absolute private environment file.');return parseEnv(await fs.readFile(filename,'utf8'));}
async function management(config){
 let access,expires=0;
 async function authenticate(){const response=await fetch('http://localhost:8180/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'local-bootstrap',password:config.KC_BOOTSTRAP_ADMIN_PASSWORD}),signal:AbortSignal.timeout(15000)});const body=await response.json();if(!response.ok||!body.access_token)throw Error('Owned Keycloak management authentication failed.');access=body.access_token;expires=Date.now()+(body.expires_in-5)*1000;}
 return async(realm,route,method='GET',body)=>{
  if(!['swoovr-members-dev','swoovr-staff-dev'].includes(realm)||!route.startsWith('/')||route.includes('..'))throw Error('Owned management realm/path refused.');
  for(let attempt=0;attempt<2;attempt++){
   if(!access||Date.now()>=expires)await authenticate();
   const response=await fetch('http://localhost:8180/admin/realms/'+realm+route,{method,headers:{Authorization:'Bearer '+access,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(15000)});
   if(response.status===401&&attempt===0){expires=0;continue;}
   if(!response.ok){const error=Error('Owned Keycloak operation refused ('+response.status+').');error.status=response.status;throw error;}
   return{body:response.status===204||response.status===201?null:await response.json(),location:response.headers.get('location')};
  }
 };
}
async function journal(manifest,entry){await fs.appendFile(manifest.journal,JSON.stringify(entry)+'\n',{mode:0o600});}
async function ownsUser(manifest,realm,user){
 if(!user||!user.email?.endsWith('@example.test')||!user.email.includes(manifest.runId)||!user.username?.includes(manifest.runId)||!Number.isSafeInteger(user.createdTimestamp)||user.createdTimestamp<manifest.startedAt)return false;
 const entries=(await fs.readFile(manifest.journal,'utf8')).split('\n').filter(Boolean).map(JSON.parse);
 // The provider strips unmanaged attributes in these realms. Require an actual journalled creation Location ID,
 // a predeclared exact fictional address, its run-specific username, and the provider creation clock together.
 return entries.some(e=>e.kind==='user'&&e.realm===realm&&e.id===user.id)
  &&entries.some(e=>e.kind==='expected'&&e.realm===realm&&e.email===user.email&&(e.username??e.email)===user.username);
}
function fixtureEmail(manifest,label,staff=false){
 const safe=String(label).toLowerCase().replace(/[^a-z0-9-]/g,'').slice(0,6)||'adult';
 return `${staff?'fxs':'fx'}.${manifest.runId}.${safe}.${require('node:crypto').randomBytes(3).toString('hex')}@example.test`;
}
function otpCredential(secret){return{type:'otp',userLabel:'Owned test authenticator',secretData:JSON.stringify({value:secret}),credentialData:JSON.stringify({subType:'totp',digits:6,counter:0,period:30,algorithm:'HmacSHA1',secretEncoding:'BASE32'})};}
function cliError(stderr){return stderr?.includes('No single staff-realm account has that subject; sign in once first.')?'No single staff-realm account has that subject; sign in once first.':'Owned API operator command failed (inspect private runner log).';}
async function ownedRealm(manifest,realm){
 const config=await privateEnv(manifest.envPath),request=await management(config);
 return async(route,method='GET',body)=>{
  if(method==='POST'&&route==='/users'){
   if(!body?.email?.endsWith('@example.test')||!body.email.includes(manifest.runId)||!body.username?.includes(manifest.runId)||body.email.split('@')[0].length>64)throw Error('Owned fixtures require a run-bound fictional address within the email local-part limit.');
   body={...body,attributes:{...body.attributes,'swoovr.owned-run':[manifest.runId]}};
   await journal(manifest,{kind:'expected',realm,email:body.email,username:body.username});
   const result=await request(realm,route,method,body);const id=result.location?.split('/').at(-1);
   if(!id||!/^[0-9a-f-]{36}$/.test(id))throw Error('Owned user creation returned no id.');
   await journal(manifest,{kind:'user',realm,id,source:'creation-location'});
   const actual=(await request(realm,'/users/'+id)).body;
   if(!await ownsUser(manifest,realm,actual))throw Error('Owned user creation read-back failed.');
   return result.body;
  }
  if(method!=='GET'){
   const id=/^\/users\/([0-9a-f-]{36})(?:\/|$)/.exec(route)?.[1];if(!id)throw Error('Only owned user mutations are permitted.');
   const user=(await request(realm,'/users/'+id)).body;
   if(!await ownsUser(manifest,realm,user))throw Error('Unowned Keycloak user mutation refused.');
   if(method==='PUT'&&route==='/users/'+id){
    if(!body?.email?.endsWith('@example.test')||!body.email.includes(manifest.runId)||!body.username?.includes(manifest.runId)||body.email.split('@')[0].length>64)throw Error('Owned user update must retain its run-bound fictional identity.');
    await journal(manifest,{kind:'expected',realm,email:body.email,username:body.username});
    body={...body,attributes:{...body?.attributes,'swoovr.owned-run':[manifest.runId]}};
   }
  }
  return(await request(realm,route,method,body)).body;
 };
}
// Browser registration cannot set an unmanaged user-profile attribute. Journal its unique expected address first;
// claim only that exact newly created fictional identity and tag its actual provider ID before further operations.
async function claimRegistration(manifest,email){
 if(!email.endsWith('@example.test')||!email.includes(manifest.runId))throw Error('Registered fixture ownership refused.');
 const entries=(await fs.readFile(manifest.journal,'utf8')).split('\n').filter(Boolean).map(JSON.parse);
 if(!entries.some(e=>e.kind==='expected'&&e.realm==='swoovr-members-dev'&&e.email===email&&e.source==='browser-registration'))throw Error('Browser registration was not declared before creation.');
 const request=await management(await privateEnv(manifest.envPath));
 const users=(await request('swoovr-members-dev','/users?exact=true&email='+encodeURIComponent(email))).body;
 if(users.length!==1||users[0].createdTimestamp<manifest.startedAt||users[0].email!==email)throw Error('Registered fixture identity is not uniquely owned.');
 const user=users[0];if(user.username!==email)throw Error('Registered fixture username does not match its declared address.');
 await journal(manifest,{kind:'user',realm:'swoovr-members-dev',id:user.id,source:'expected-browser-registration'});
 await request('swoovr-members-dev','/users/'+user.id,'PUT',{...user,attributes:{...user.attributes,'swoovr.owned-run':[manifest.runId]}});
}
module.exports={validateManifest,loadManifest,parseEnv,privateEnv,management,journal,ownedRealm,claimRegistration,ownsUser,fixtureEmail,otpCredential,cliError};
