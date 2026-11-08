// Explicit finite browser acceptance against owned hosts/DB/clients/users. Never replaces an owner listener.
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os'),net=require('node:net'),http=require('node:http'),crypto=require('node:crypto');
const {spawn,execFile}=require('node:child_process');
const support=require('./owned-support.cjs');
const root=path.resolve(__dirname,'../..');
const suites=Object.freeze({'messaging-lifetime':'tests/messaging/lifetime.browser.cjs',members:'tests/auth/members.browser.cjs',onboarding:'tests/accounts/onboarding.browser.cjs','mode-names':'tests/profiles/mode-names.browser.cjs','mode-photos':'tests/media/mode-photos.browser.cjs','review-workflows':'tests/media/review-workflows.browser.cjs',accounts:'tests/auth/accounts.browser.cjs',didit:'tests/verification/didit.browser.cjs',phone:'tests/accounts/phone.browser.cjs',deletion:'tests/accounts/deletion.browser.cjs',
 // #257 location work (D345–D352): Explore modes and badges, distance filters and bands, Travel, the Near me store and discovery.
 modes:'tests/profiles/modes.browser.cjs',badges:'tests/profiles/badges.browser.cjs',matching:'tests/discovery/matching.browser.cjs',
 'location-privacy':'tests/discovery/location-privacy.browser.cjs','traveller-search':'tests/travel/traveller-search.browser.cjs',
 'near-me':'tests/discovery/near-me.browser.cjs','near-me-discovery':'tests/discovery/near-me-discovery.browser.cjs'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const command=(name,args,options={})=>new Promise((resolve,reject)=>execFile(name,args,{cwd:root,maxBuffer:8e6,...options},(error,stdout,stderr)=>error?reject(Object.assign(Error(name+' failed (private runner logs contain details).'),{privateOutput:stderr})):resolve(stdout.trim())));
function selectedSuites(value){const selected=value?.split(',');if(!selected?.length||new Set(selected).size!==selected.length||selected.some(key=>!Object.hasOwn(suites,key)))throw Error('Set SWOOVR_OWNED_SUITES to explicit unique names: '+Object.keys(suites).join(','));if(selected.includes('deletion')&&selected.length!==1)throw Error('Run deletion alone: its owned hosts use a shorter reauthentication window.');return selected;}
async function reserve(){const server=net.createServer();await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(0,'127.0.0.1',resolve);});return{server,port:server.address().port,close:()=>new Promise((resolve,reject)=>server.close(error=>error?reject(error):resolve()))};}
async function main(){
 const selected=selectedSuites(process.env.SWOOVR_OWNED_SUITES);
 const source=process.env.SWOOVR_OWNED_TEST_ENV;
 if(!source||!path.isAbsolute(source)||await fs.realpath(source)!==await fs.realpath(path.join(root,'.local/platform/docker.env')))throw Error('SWOOVR_OWNED_TEST_ENV must explicitly select this checkout\'s absolute managed .local/platform/docker.env.');
 const config=await support.privateEnv(source);
 for(const key of ['SWOOVR_DB_PASSWORD','KC_BOOTSTRAP_ADMIN_PASSWORD'])if(!config[key])throw Error('Managed local infrastructure is not initialized.');
 const configuration=process.env.SWOOVR_OWNED_CONFIGURATION||'Release';if(!['Debug','Release'].includes(configuration))throw Error('Use Debug or Release built artifacts.');
 const dll=name=>path.join(root,'src','Swoovr.'+name,'bin',configuration,'net10.0','Swoovr.'+name+'.dll');
 for(const name of ['Api','Web','Admin'])await fs.access(dll(name));
 const runId=crypto.randomUUID().replaceAll('-',''),folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-owned-browser-'));await fs.chmod(folder,0o700);
 const evidence=path.join(root,'.local/owned-browser-runs',runId);await fs.mkdir(evidence,{recursive:true,mode:0o700});
 const manifest={runId,database:'swoovr_registry_ui_'+runId,startedAt:Date.now(),envPath:path.join(folder,'environment.env'),rootFixture:path.join(folder,'root.json'),journal:path.join(folder,'users.jsonl'),pgContainer:'swoovr-platform-postgres-1',pgPort:5433,memberClient:'swoovr-owned-web-'+runId,staffClient:'swoovr-owned-admin-'+runId,apiDll:dll('Api')};
 const manifestPath=path.join(folder,'manifest.json'),children=[],reservations=[],clients=[];let createdDb=false,provider;
 const cleanEnv=Object.fromEntries(Object.entries(process.env).filter(([key])=>!key.includes('__')&&!/^SWOOVR_(?:TEST_|OWNED_RUN_|MEMBERS_|STAFF_|DIDIT_|R2_|FOUNDER_|DB_|LOCAL_ADMIN)/.test(key)));
 let env;
 const logError=async(label,error)=>{await fs.appendFile(path.join(evidence,'runner-errors.log'),label+': '+error.message+'\n'+(error.privateOutput||'')+'\n',{mode:0o600});};
 const pg=(database,query)=>command('docker',['exec',manifest.pgContainer,'psql','-p',String(manifest.pgPort),'-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-tAc',query]);
 const kc=await support.management(config);
 const request=async(realm,route,method='GET',body)=>(await kc(realm,route,method,body)).body;
 async function clone(realm,original,clientId,secret,base){
  clients.push({realm,clientId});
  const found=await request(realm,'/clients?clientId='+original);if(found.length!==1)throw Error('Expected one local OIDC template client.');
  const template=await request(realm,'/clients/'+found[0].id);delete template.id;
  Object.assign(template,{clientId,secret,name:'Owned browser fixture '+runId,rootUrl:base,baseUrl:base+'/',adminUrl:base+'/',redirectUris:[base+'/signin-oidc'],webOrigins:[base],attributes:{...template.attributes,'swoovr.owned-run':runId,'post.logout.redirect.uris':base+'/*'}});
  for(const mapper of template.protocolMappers||[])delete mapper.id;
  await request(realm,'/clients','POST',template);
  const actual=await request(realm,'/clients?clientId='+clientId);if(actual.length!==1||actual[0].attributes?.['swoovr.owned-run']!==runId)throw Error('Owned OIDC client read-back failed.');clients.at(-1).id=actual[0].id;
 }
 const stop=async entry=>{
  if(entry.stopped||!entry.child.pid)return;entry.stopped=true;
  if(entry.group){try{process.kill(-entry.child.pid,'SIGTERM');}catch(error){if(error.code!=='ESRCH')throw error;}}
  if(entry.child.exitCode!==null||entry.child.signalCode!==null)return;
  const done=new Promise(resolve=>entry.child.once('exit',resolve));
  const signal=value=>{try{entry.group?process.kill(-entry.child.pid,value):entry.child.kill(value);}catch(error){if(error.code!=='ESRCH')throw error;}};
  signal('SIGTERM');await Promise.race([done,sleep(4000)]);if(entry.child.exitCode===null&&entry.child.signalCode===null){signal('SIGKILL');await done;}
 };
 const spawnLogged=async(label,name,args,settings,{group=false,cwd=root}={})=>{
  const output=path.join(evidence,label+'.log'),handle=await fs.open(output,'w',0o600);
  const child=spawn(name,args,{cwd,env:settings,stdio:['ignore',handle.fd,handle.fd],detached:group});await handle.close();const entry={label,child,group,output};children.push(entry);child.on('error',error=>{entry.error=error;});return entry;
 };
 const start=async(name,reservation,extra={})=>{
  await reservation.close();const port=reservation.port;
  const entry=await spawnLogged(name.toLowerCase(),'dotnet',[dll(name)],{...env,...extra,ASPNETCORE_URLS:'http://localhost:'+port},{cwd:path.join(root,'src','Swoovr.'+name)});
  for(let n=0;n<300;n++){
   if(entry.error||entry.child.exitCode!==null||entry.child.signalCode!==null)throw Error('Owned '+name+' host exited before readiness.');
   try{const response=await fetch('http://localhost:'+port+(name==='Api'?'/health/live':name==='Admin'?'/signed-out':'/safety'),{signal:AbortSignal.timeout(1000)});if(response.ok){
    const listeners=(await command('lsof',['-tiTCP:'+port,'-sTCP:LISTEN'])).split('\n');if(listeners.some(id=>id!==String(entry.child.pid)))throw Error('Unexpected listener owns an allocated test port.');
    return entry;
   }}catch(error){if(error.message==='Unexpected listener owns an allocated test port.')throw error;}
   await sleep(100);
  }throw Error('Owned '+name+' host readiness timed out.');
 };
 let interrupted=false;const interrupt=()=>{interrupted=true;for(const entry of children)if(entry.child.exitCode===null)try{entry.group?process.kill(-entry.child.pid,'SIGTERM'):entry.child.kill('SIGTERM');}catch{}};
 process.once('SIGINT',interrupt);process.once('SIGTERM',interrupt);
 let failure;
 try{
  for(let n=0;n<3;n++)reservations.push(await reserve());
  manifest.api='http://127.0.0.1:'+reservations[0].port;manifest.web='http://localhost:'+reservations[1].port;manifest.admin='http://localhost:'+reservations[2].port;
  support.validateManifest(manifest,manifestPath);
  const memberSecret=crypto.randomBytes(32).toString('hex'),staffSecret=crypto.randomBytes(32).toString('hex');
  const synthetic={KC_BOOTSTRAP_ADMIN_PASSWORD:config.KC_BOOTSTRAP_ADMIN_PASSWORD,SWOOVR_DB_PASSWORD:config.SWOOVR_DB_PASSWORD,SWOOVR_MEMBERS_WEB_SECRET:memberSecret,SWOOVR_STAFF_WEB_SECRET:staffSecret,
   SWOOVR_MEMBERS_API_ADMIN_SECRET:config.SWOOVR_MEMBERS_API_ADMIN_SECRET||'',SWOOVR_FOUNDER_SECRET_V1:crypto.randomBytes(32).toString('hex'),SWOOVR_DIDIT_API_KEY:'synthetic-owned-only',SWOOVR_DIDIT_WEBHOOK_SECRET:crypto.randomBytes(32).toString('hex'),SWOOVR_DIDIT_WORKFLOW_ID:crypto.randomUUID()};
  await fs.writeFile(manifest.envPath,Object.entries(synthetic).map(([key,value])=>key+'='+JSON.stringify(value)).join('\n')+'\n',{mode:0o600});await fs.writeFile(manifest.journal,'',{mode:0o600});await fs.writeFile(manifestPath,JSON.stringify(manifest),{mode:0o600});
  await clone('swoovr-members-dev','swoovr-web',manifest.memberClient,memberSecret,manifest.web);await clone('swoovr-staff-dev','swoovr-admin-web',manifest.staffClient,staffSecret,manifest.admin);
  const media=path.join(folder,'media');await fs.mkdir(media,{mode:0o700});
  // Synthetic stand-in only; neither credentials nor requests reach Didit. Generated URLs are never navigated.
  const sessions=new Map();provider=http.createServer(async(req,res)=>{
   try{const body=await new Promise((resolve,reject)=>{let text='';req.on('data',part=>{text+=part;if(text.length>65536)reject(Error('Too large'));});req.on('end',()=>resolve(text));});
    res.setHeader('Content-Type','application/json');if(req.method==='POST'&&req.url==='/v3/session/'){
     const input=JSON.parse(body),id=crypto.randomUUID(),record={session_id:id,status:'Not Started',vendor_data:input.vendor_data,workflow_id:input.workflow_id,url:'https://mock.didit.me/'+id};sessions.set(id,record);res.end(JSON.stringify(record));
    }else{const id=/^\/v3\/session\/([0-9a-f-]{36})\/(?:decision|delete)\/$/.exec(req.url)?.[1];if(req.method==='DELETE'){sessions.delete(id);res.end('{}');}else if(id&&sessions.has(id))res.end(JSON.stringify(sessions.get(id)));else{res.statusCode=404;res.end('{}');}}
   }catch{res.statusCode=400;res.end('{}');}
  });await new Promise(resolve=>provider.listen(0,'127.0.0.1',resolve));
  env={...cleanEnv,TMPDIR:folder,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${manifest.database};Username=swoovr;Password=${config.SWOOVR_DB_PASSWORD}`,
   ...(selected.includes('deletion')?{Accounts__ReauthSeconds:'45',SWOOVR_REAUTH_SECONDS:'45'}:{}),
   Keycloak__BaseUrl:'http://localhost:8180',Keycloak__RequireHttpsMetadata:'false',Mfa__StaffClientId:manifest.staffClient,Staff__LocalPasswords:'true',Phone__Required:'false',Founders__Secrets__1:synthetic.SWOOVR_FOUNDER_SECRET_V1,
   MemberAdmin__ClientSecret:synthetic.SWOOVR_MEMBERS_API_ADMIN_SECRET,MemberAdmin__ContactRetirementEnabled:'false',MemberAdmin__ContactRetirementWorker:'false',
   Didit__ApiKey:'synthetic-owned-only',Didit__WorkflowId:synthetic.SWOOVR_DIDIT_WORKFLOW_ID,Didit__WebhookSecret:synthetic.SWOOVR_DIDIT_WEBHOOK_SECRET,Didit__BaseUrl:'http://127.0.0.1:'+provider.address().port+'/',Didit__Callbacks__Web:manifest.web+'/me/verification/return',
   Media__Store:'local',Media__Local__Path:media,Media__Screening__Simulate:'blocked',Media__Screening__RequireKnownContentCheck:'true',Media__Screening__Worker:'false',Media__Screening__BaseUrl:'http://127.0.0.1:1/',
   Email__Smtp__Host:'127.0.0.1',Email__Smtp__Port:'1025',Email__Provider:'smtp',Sms__Provider:'simulated',Push__Provider:'simulated',Retention__Worker:'false',MeetingSafety__Worker:'false',Notifications__Worker:'false',TextHarm__Enabled:'false',PolicyNotices__Enabled:'false',
   SWOOVR_OWNED_RUN_MANIFEST:manifestPath,SWOOVR_TEST_API_BASE:manifest.api,SWOOVR_TEST_APP_DATABASE:manifest.database,SWOOVR_TEST_WEB_BASE:manifest.web,SWOOVR_TEST_ADMIN_BASE:manifest.admin,SWOOVR_TEST_ENV_PATH:manifest.envPath,
   SWOOVR_TEST_PG_CONTAINER:manifest.pgContainer,SWOOVR_TEST_PG_PORT:String(manifest.pgPort),SWOOVR_TEST_ROOT_FIXTURE:manifest.rootFixture,SWOOVR_TEST_MEMBER_CLIENT:manifest.memberClient,SWOOVR_TEST_STAFF_CLIENT:manifest.staffClient,
   SWOOVR_OWNED_DIDIT_SIMULATED:'1',SWOOVR_BROWSER_OUTPUT:path.join(evidence,'browser'),SWOOVR_SCREENSHOTS:path.join(evidence,'browser'),SWOOVR_MEETING_OWNED_LOGS:evidence};
  await fs.mkdir(env.SWOOVR_BROWSER_OUTPUT,{mode:0o700});
  await pg('postgres','CREATE DATABASE '+manifest.database);createdDb=true;
  for(const args of [['migrate'],['geonames','import',path.join(root,'tests/fixtures/geonames')]])await command('dotnet',[dll('Api'),...args],{cwd:path.join(root,'src/Swoovr.Api'),env});
  await pg(manifest.database,'UPDATE registration_availability SET beta_enabled=true,public_enabled=false,revision=revision+1 WHERE id=1');
  const fixture={fictional:true,runId,username:'fictional.root.'+runId+'@example.test',password:'Owned-root-'+crypto.randomBytes(18).toString('hex'),totpSecret:Array.from(crypto.randomBytes(32),byte=>'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'[byte%32]).join('')};
  const staffRealm=await support.ownedRealm(manifest,'swoovr-staff-dev');
  await staffRealm('/users','POST',{username:fixture.username,email:fixture.username,firstName:'Fictional',lastName:'Root',enabled:true,emailVerified:true,requiredActions:[],credentials:[{type:'password',value:fixture.password,temporary:false},support.otpCredential(fixture.totpSecret)]});
  const users=await staffRealm('/users?exact=true&username='+encodeURIComponent(fixture.username));if(users.length!==1)throw Error('Owned MFA root lookup failed.');fixture.userId=users[0].id;
  await fs.writeFile(manifest.rootFixture,JSON.stringify(fixture),{mode:0o600});
  const rootAccount=crypto.randomUUID();await pg(manifest.database,`INSERT INTO accounts(id,issuer,subject,realm,email,status,is_root_admin,created_at,last_seen_at,delete_founder_token)VALUES('${rootAccount}','http://localhost:8180/realms/swoovr-staff-dev','${fixture.userId}','Staff','${fixture.username}','Active',false,now(),now(),false)`);
  await command('dotnet',[dll('Api'),'root-admin','grant',fixture.userId],{cwd:path.join(root,'src/Swoovr.Api'),env});
  await start('Api',reservations[0]);
  const portal={Api__BaseUrl:manifest.api+'/',Prototype__ApiBaseUrl:manifest.api+'/',Prototype__Enabled:'false',Keycloak__RequireHttpsMetadata:'false'};
  await start('Web',reservations[1],{...portal,Keycloak__Authority:'http://localhost:8180/realms/swoovr-members-dev',Keycloak__ClientId:manifest.memberClient,Keycloak__ClientSecret:memberSecret});
  await start('Admin',reservations[2],{...portal,Keycloak__Authority:'http://localhost:8180/realms/swoovr-staff-dev',Keycloak__ClientId:manifest.staffClient,Keycloak__ClientSecret:staffSecret});
  console.log('Owned loopback hosts ready; selected suites: '+selected.join(',')+'. Private evidence: '+evidence);
  for(const key of selected){
   if(interrupted)throw Error('Owned run interrupted.');
   // New metadata-only review fixtures create their member before beta enrolment; allow public registration in this disposable DB for that suite only.
   await pg(manifest.database,`UPDATE registration_availability SET beta_enabled=true,public_enabled=${key==='review-workflows'?'true':'false'},revision=revision+1 WHERE id=1`);
   const entry=await spawnLogged('suite-'+key,'node',[suites[key]],env,{group:true});
   if(entry.error)throw Error('Owned suite failed to spawn.');
   const code=await new Promise((resolve,reject)=>{entry.child.once('exit',resolve);entry.child.once('error',reject);});
   await stop(entry);
   if(entry.error||code!==0)throw Error('Owned suite '+key+' failed; inspect '+entry.output);
   const log=await fs.readFile(entry.output,'utf8');for(const line of log.split('\n'))if(/^PASS:/.test(line))console.log(line);
  }
  console.log('PASS: selected owned browser suites completed; Didit adapter uses a synthetic local stand-in.');
 }catch(error){failure=error;await logError('run',error);}
 finally{
  const errors=[];const clean=async(label,action)=>{try{await action();}catch(error){errors.push(label);await logError(label,error);}};
  for(const entry of children.reverse())await clean('owned process '+entry.label,()=>stop(entry));
  for(const reservation of reservations)if(reservation.server.listening)await clean('port reservation',()=>reservation.close());
  if(provider)await clean('synthetic provider',()=>new Promise(resolve=>provider.close(resolve)));
  await clean('owned users',async()=>{
   let entries=[];try{entries=(await fs.readFile(manifest.journal,'utf8')).split('\n').filter(Boolean).map(JSON.parse);}catch(error){if(error.code!=='ENOENT')throw error;}
   const ids=new Map();for(const entry of entries)if(entry.kind==='user')ids.set(entry.realm+':'+entry.id,entry);
   for(const entry of entries.filter(e=>e.kind==='expected')){
    const users=await request(entry.realm,'/users?exact=true&email='+encodeURIComponent(entry.email));
    for(const user of users){if(await support.ownsUser(manifest,entry.realm,user))ids.set(entry.realm+':'+user.id,{realm:entry.realm,id:user.id});
     else if(entry.realm==='swoovr-members-dev'&&entry.source==='browser-registration'){await support.claimRegistration(manifest,entry.email);ids.set(entry.realm+':'+user.id,{realm:entry.realm,id:user.id});}
     else throw Error('Refusing cleanup of unowned identity.');}
   }
   for(const entry of ids.values()){let user;try{user=await request(entry.realm,'/users/'+entry.id);}catch(error){if(error.status===404)continue;throw error;}
    if(!await support.ownsUser(manifest,entry.realm,user))throw Error('Refusing cleanup of unowned user id.');await request(entry.realm,'/users/'+entry.id,'DELETE');}
  });
  for(const client of clients.reverse())await clean('owned client',async()=>{const found=await request(client.realm,'/clients?clientId='+client.clientId);for(const value of found){if(value.attributes?.['swoovr.owned-run']!==runId)throw Error('Refusing cleanup of unowned client.');await request(client.realm,'/clients/'+value.id,'DELETE');}});
  if(createdDb)await clean('disposable database',()=>pg('postgres','DROP DATABASE '+manifest.database+' WITH (FORCE)'));
  await fs.writeFile(path.join(evidence,'summary.json'),JSON.stringify({runId,suites:selected,database:manifest.database,api:manifest.api,web:manifest.web,admin:manifest.admin,success:!failure,cleanupErrors:errors},null,2),{mode:0o600});
  if(!errors.length)await clean('private runtime directory',()=>fs.rm(folder,{recursive:true,force:true}));
  process.removeListener('SIGINT',interrupt);process.removeListener('SIGTERM',interrupt);
  if(errors.length)throw Error('Owned cleanup incomplete ('+errors.join(', ')+'); private evidence: '+evidence);
  console.log('CLEANUP: only owned processes, clients, fictional users, media and disposable database removed.');
 }
 if(failure)throw failure;
 console.log('Evidence: '+evidence);
}
module.exports={selectedSuites,suites};
if(require.main===module)main().catch(error=>{console.error(error.message);process.exitCode=1;});
