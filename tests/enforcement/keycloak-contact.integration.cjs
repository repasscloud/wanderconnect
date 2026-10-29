// Disposable Keycloak/PostgreSQL provider schema checks. No application login, real ownership or delivery claim.
// Raw SQL seeds only this owned provider fixture; production API code must never write the Keycloak database.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {execFile,spawn}=require('node:child_process');
const {once}=require('node:events');
const {localEnv,sql}=require('../auth/local-tokens.cjs');
const run=(cmd,args,options={})=>{const {captureCombined=false,...settings}=options;return new Promise((resolve,reject)=>execFile(cmd,args,{maxBuffer:8*1024*1024,...settings},(error,out,err)=>error?reject(new Error(cmd+' exited '+error.code+': '+err.slice(-1600))):resolve((captureCombined?out+err:out).trim())));};
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
 const javaHome=process.env.SWOOVR_KEYCLOAK_JAVA_HOME,libs=process.env.SWOOVR_KEYCLOAK_LIBS;
 if(!javaHome||!libs)throw new Error('Set explicit SWOOVR_KEYCLOAK_JAVA_HOME (21+) and SWOOVR_KEYCLOAK_LIBS (public26.8.1 libraries).');
 const id=crypto.randomUUID().replaceAll('-',''),database='swoovr_kc_retire_'+id,container='swoovr-kc-retire-'+id;
 const folder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-kc-retire-'));await fs.chmod(folder,0o700);
 const state=path.join(folder,'owned-state.json'),jar=path.join(folder,'swoovr-contact-policy.jar');
 let created=false,started=false,checks=0;
 const equal=(actual,expected,label)=>{assert.equal(actual,expected,label);checks++;};
 const query=q=>run('docker',['exec','swoovr-local-postgres-1','psql','-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-tAc',q]);
 try{
  const build=await run('python3',['scripts/check_keycloak_contact_policy.py','--java-home',javaHome,'--keycloak-libs',libs,'--output-jar',jar],{captureCombined:true});
  await fs.writeFile(path.join(folder,'build.log'),build,{mode:0o600});
  const image=process.env.SWOOVR_KEYCLOAK_TEST_IMAGE||await run('docker',['inspect','swoovr-local-keycloak-1','--format','{{.Image}}']);
  const networks=JSON.parse(await run('docker',['inspect','swoovr-local-postgres-1','--format','{{json .NetworkSettings.Networks}}']));
  const network=Object.keys(networks);assert.equal(network.length,1,'Use one unambiguous local fixture network.');
  await sql('CREATE DATABASE '+database);created=true;
  const bootstrap='Fictional-'+crypto.randomBytes(24).toString('hex');
  const env={...process.env,KC_DB:'postgres',KC_DB_URL:'jdbc:postgresql://swoovr-local-postgres-1:5432/'+database,KC_DB_USERNAME:'swoovr',KC_DB_PASSWORD:await localEnv('SWOOVR_DB_PASSWORD'),
   KC_BOOTSTRAP_ADMIN_USERNAME:'fictional-provider-root',KC_BOOTSTRAP_ADMIN_PASSWORD:bootstrap,KC_HOSTNAME_STRICT:'false'};
  const environment=['KC_DB','KC_DB_URL','KC_DB_USERNAME','KC_DB_PASSWORD','KC_BOOTSTRAP_ADMIN_USERNAME','KC_BOOTSTRAP_ADMIN_PASSWORD','KC_HOSTNAME_STRICT'];
  await run('docker',['run','-d','--name',container,'--network',network[0],'--cpus','2','--memory','1536m','-p','127.0.0.1::8080','--mount','type=bind,src='+jar+',dst=/opt/keycloak/providers/swoovr-contact-policy.jar,readonly',...environment.flatMap(name=>['-e',name]),image,'start-dev'],{env});started=true;
  const mapped=await run('docker',['port',container,'8080/tcp']);assert.match(mapped,/^127\.0\.0\.1:[0-9]+$/);const base='http://'+mapped;
  await fs.writeFile(state,JSON.stringify({container,database,base,jar}),{mode:0o600});
  console.log('OWNED FIXTURE: '+container+' / '+database+'; private logs '+folder);
  let ready=false;
  for(let n=0;n<180;n++){
   const running=await run('docker',['inspect',container,'--format','{{.State.Running}}']);if(running!=='true')throw new Error('Owned Keycloak exited before readiness; inspect private logs.');
   try{if((await fetch(base+'/realms/master/.well-known/openid-configuration',{signal:AbortSignal.timeout(1000)})).ok){ready=true;break;}}catch{}
   await sleep(1000);
  }
  assert.ok(ready,'Isolated provider startup timed out.');
  equal(await query("SELECT count(*) FROM information_schema.tables WHERE table_name='swoovr_email_retirement'"),'1','Actual Keycloak startup applies custom guard schema');
  equal(await query("SELECT count(*) FROM pg_trigger WHERE tgname='swoovr_retired_contact_guard'"),'1','Provider contact guard installed');
  const tokenResponse=await fetch(base+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:env.KC_BOOTSTRAP_ADMIN_USERNAME,password:bootstrap})});
  equal(tokenResponse.status,200,'Disposable bootstrap admin obtains a genuine provider token');const token=(await tokenResponse.json()).access_token;
  const request=async(route,method='GET',body)=>{
   const response=await fetch(base+route,{method,headers:{Authorization:'Bearer '+token,...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
   let value=null;try{value=await response.json();}catch{}return{status:response.status,body:value,location:response.headers.get('location')};
  };
  equal((await request('/admin/serverinfo')).body.systemInfo.version,'26.8.1','Actual running provider is pinned version');
  const realm='swoovr-contact-retirement-test';
  equal((await request('/admin/realms','POST',{realm,enabled:true,registrationEmailAsUsername:true,editUsernameAllowed:false,verifyEmail:true,resetPasswordAllowed:true,loginWithEmailAllowed:true,duplicateEmailsAllowed:false,attributes:{"swoovr.contact-policy":"enabled"}})).status,201,'Owned fictional realm');
  const root='/admin/realms/'+realm,realmId=(await request(root)).body.id;assert.match(realmId,/^[0-9a-f-]{36}$/);
  const clientSecret='Fictional-'+crypto.randomBytes(24).toString('hex'),clientId='swoovr-api-accounts';
  equal((await request(root+'/clients','POST',{clientId,secret:clientSecret,enabled:true,publicClient:false,serviceAccountsEnabled:true,fullScopeAllowed:false,standardFlowEnabled:false,directAccessGrantsEnabled:false})).status,201,'Restricted fixture service client');
  const client=(await request(root+'/clients?clientId='+clientId)).body[0];
  const serviceUser=(await request(root+'/clients/'+client.id+'/service-account-user')).body;
  const management=(await request(root+'/clients?clientId=realm-management')).body[0];
  const manage=(await request(root+'/clients/'+management.id+'/roles/manage-users')).body;
  const roleRoute=root+'/users/'+serviceUser.id+'/role-mappings/clients/'+management.id;
  equal((await request(roleRoute,'POST',[manage])).status,204,'Only explicit manage-users authorizes service operation');
  equal((await request(root+'/clients/'+client.id+'/scope-mappings/clients/'+management.id,'POST',[manage])).status,204,'Explicit client role scope replaces full-scope access');
  const serviceReply=await fetch(base+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'client_credentials',client_id:clientId,client_secret:clientSecret})});
  equal(serviceReply.status,200,'Real provider service-account token');const serviceToken=(await serviceReply.json()).access_token;
  const providerBase='/realms/'+realm+'/swoovr-contact-policy';
  const provider=async(route,method='GET',body,bearer=serviceToken,version)=>{
   const response=await fetch(base+providerBase+route,{method,headers:{...(bearer?{Authorization:'Bearer '+bearer}:{}),...(body?{'Content-Type':'application/json'}:{}),...(version!==undefined?{'Swoovr-Version':version}:{})},...(body?{body:JSON.stringify(body)}:{})});
   let value=null;try{value=await response.json();}catch{}return{status:response.status,body:value,version:response.headers.get('Swoovr-Version'),cache:response.headers.get('cache-control')};
  };
  equal((await provider('/capabilities','GET',null,null)).status,401,'No unauthenticated provider capability access');
  equal((await provider('/capabilities','GET',null,'fictional-invalid-token')).status,401,'Malformed provider bearer never grants access');
  equal((await provider('/capabilities','GET',null,null,'2026-02-30')).body.code,'invalid-api-version','Invalid date refuses before authentication');
  equal((await provider('/capabilities','GET',null,null,'0000-01-01')).body.code,'invalid-api-version','Year zero matches canonical DateOnly invalid policy');
  equal((await provider('/capabilities','GET',null,null,'2099-01-01')).body.code,'unsupported-api-version','Unsupported date refuses before authentication');
  const capabilities=await provider('/capabilities');equal(capabilities.status,200,'Actual custom provider endpoint is registered and authorized');
  const contract=await fs.readFile('src/Swoovr.Contracts/ApiContract.cs','utf8'),initial=contract.match(/const string Initial = "([^"]+)"/)[1];
  equal(capabilities.version,initial,'Provider protocol date generated from canonical contract');equal(capabilities.cache,'no-store','Restricted metadata is not cacheable');
  equal(capabilities.body.retireEmail,true,'Installed guard supports retirement');
  equal((await provider('/capabilities','GET',null,null,'')).body.code,'invalid-api-version','Explicit empty header is not treated as missing');
  equal((await provider('/capabilities','GET',null,null,initial+','+initial)).body.code,'invalid-api-version','Multiple combined date values refuse');
  await request(roleRoute,'DELETE',[manage]);equal((await provider('/capabilities')).status,403,'Withdrawn live service role refuses cached token');await request(roleRoute,'POST',[manage]);
  const email='fictional.retired.'+id+'@example.test',password='Fictional-'+crypto.randomBytes(20).toString('hex');
  const createdUser=await request(root+'/users','POST',{username:email,email,emailVerified:true,enabled:true,firstName:'Fictional Adult',lastName:'Provider Fixture',credentials:[{type:'password',value:password,temporary:false}]});
  equal(createdUser.status,201,'Fictional verified assertion is provisioned, not delivered ownership');const userId=createdUser.location.split('/').at(-1);assert.match(userId,/^[0-9a-f-]{36}$/);
  const before=await query(`SELECT count(*) FROM credential WHERE user_id='${userId}'`);equal(before,'1','Original password credential exists');
  // Disabled fictional OIDC metadata only; no external provider authentication or network request.
  const alias='fictional-linked';
  equal((await request(root+'/identity-provider/instances','POST',{alias,providerId:'oidc',enabled:false,trustEmail:false,storeToken:false,config:{clientId:'fictional-fixture',syncMode:'IMPORT',authorizationUrl:'https://identity.example.test/auth',tokenUrl:'https://identity.example.test/token'}})).status,201,'Disabled fictional linked-provider fixture');
  equal((await request(root+'/users/'+userId+'/federated-identity/'+alias,'POST',{identityProvider:alias,userId:crypto.randomUUID(),userName:'fictional.social@example.test'})).status,204,'Opaque linked credential associated with local provider user');
  const linkedBefore=(await request(root+'/users/'+userId+'/federated-identity')).body;
  equal(linkedBefore.length,1,'Direct and linked credentials coexist');

  const operation=crypto.randomUUID(),sink='retired.'+operation+'@swoovr.invalid';
  // Isolated protocol fixture only: product public clients remain PKCE and have no password grant.
  const fixtureClient='swoovr-contact-policy-fixture';
  equal((await request(root+'/clients','POST',{clientId:fixtureClient,publicClient:true,enabled:true,standardFlowEnabled:false,directAccessGrantsEnabled:true,fullScopeAllowed:false})).status,201,'Disposable protocol fixture client');
  const memberReply=await fetch(base+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:fixtureClient,username:email,password})});
  equal(memberReply.status,200,'Genuine fictional user token for provider authorization negative');const memberTokens=await memberReply.json();
  equal((await provider('/capabilities','GET',null,memberTokens.access_token)).status,403,'A normal provider user is not the service account');
  equal((await provider('/users/'+userId+'/email-binding','GET',null,memberTokens.access_token)).status,403,'A normal user cannot read restricted raw binding snapshots');
  equal((await request(root+'/users/'+userId+'/sessions')).body.length>0,true,'Source has actual provider session before retirement');
  await request(root+'/users/'+userId); // Prime ordinary provider cache before retirement.
  const snapshot=(await provider('/users/'+userId+'/email-binding')).body;
  const retireRoute='/users/'+userId+'/retire-email',retireBody={operationId:operation,expectedEmail:snapshot.email,expectedUsername:snapshot.username};
  equal((await provider(retireRoute,'POST',{...retireBody,expectedEmail:'fictional.changed@example.test'})).body.code,'binding-changed','Exact provider email compare-and-set refuses stale input');
  equal((await provider(retireRoute,'POST',{...retireBody,expectedUsername:'unrelated-later-login'})).body.code,'binding-changed','Exact username compare-and-set preserves a later binding');
  equal(await query(`SELECT count(*) FROM swoovr_email_retirement WHERE user_id='${userId}'`),'0','Refused compare-and-set leaves no marker');
  equal((await provider(retireRoute,'POST',retireBody,memberTokens.access_token)).status,403,'Normal user cannot retire its own binding');
  equal((await provider(retireRoute,'POST',retireBody)).status,200,'Actual restricted provider retirement commits');
  equal((await request(root+'/users/'+userId+'/sessions')).body.length,0,'Actual custom retirement revokes source provider sessions');
  const refresh=await fetch(base+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'refresh_token',client_id:fixtureClient,refresh_token:memberTokens.refresh_token})});
  equal(refresh.status,400,'Pre-retirement provider session cannot refresh');
  const retiredAt=await query(`SELECT retired_at FROM swoovr_email_retirement WHERE user_id='${userId}'`);
  equal((await provider(retireRoute,'POST',retireBody)).body.status,'retired','Same opaque operation retry is idempotent');
  equal(await query(`SELECT retired_at FROM swoovr_email_retirement WHERE user_id='${userId}'`),retiredAt,'Retry never rewrites original completion clock');
  equal((await provider(retireRoute+'/'+operation)).body.status,'retired','Provider completion is queryable without old contact');
  equal((await provider(retireRoute,'POST',{...retireBody,operationId:crypto.randomUUID(),expectedEmail:sink,expectedUsername:sink})).status,409,'Another operation cannot retire a later binding through an existing marker');
  const after=await request(root+'/users/'+userId);equal(after.body.email,sink,'Actual provider reads reserved sink');equal(after.body.emailVerified,false,'Sink is not represented as verified ownership');
  const restore=await request(root+'/users/'+userId,'PUT',{username:email,email,emailVerified:true});equal(restore.status>=400,true,'Standard provider update cannot restore retired mailbox/login');
  equal(await query(`SELECT email FROM user_entity WHERE id='${userId}'`),sink,'Refused update leaves binding safe');
  let blocked=false;try{await query(`UPDATE user_entity SET email_verified=true WHERE id='${userId}'`);}catch(error){blocked=error.message.includes('Swoovr retired contact policy refuses');}
  equal(blocked,true,'Independent SQL cannot mark sink as verified');
  equal((await request(root+'/users/'+userId,'PUT',{username:sink,email:sink,emailVerified:false,firstName:'Fictional Updated'})).status,204,'Unrelated profile text remains editable');
  equal(await query(`SELECT count(*) FROM credential WHERE user_id='${userId}'`),before,'Unrelated password credential retained');
  assert.deepEqual((await request(root+'/users/'+userId+'/federated-identity')).body,linkedBefore,'Unrelated linked credential IDs remain unchanged');checks++;
  const policy=await query(`SELECT row_to_json(p)::text FROM swoovr_email_retirement p WHERE user_id='${userId}'`);
  equal(policy.includes(email)||policy.includes(password),false,'Provider policy stores neither original contact nor credential');
  equal((await request(root+'/users?email='+encodeURIComponent(email)+'&exact=true')).body.length,0,'Retired mailbox no longer resolves the old provider subject');
  const newPassword='Fictional-New-'+crypto.randomBytes(20).toString('hex');
  const newOwner=await request(root+'/users','POST',{username:email,email,emailVerified:false,enabled:true,firstName:'Fictional New Adult',lastName:'Reassigned Fixture',credentials:[{type:'password',value:newPassword,temporary:false}]});
  equal(newOwner.status,201,'Independent new provider subject can use freed mailbox');
  const newOwnerId=newOwner.location.split('/').at(-1);assert.notEqual(newOwnerId,userId);checks++;
  // Explicit fictional verified-ownership assertion to test cache/subject separation, not delivered email evidence.
  equal((await request(root+'/users/'+newOwnerId,'PUT',{username:email,email,emailVerified:true})).status,204,'New-owner fictional verification fixture');
  const newLogin=await fetch(base+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:fixtureClient,username:email,password:newPassword})});
  equal(newLogin.status,200,'New owner authenticates through provider after old-alias cache eviction');
  const newToken=(await newLogin.json()).access_token;
  equal(JSON.parse(Buffer.from(newToken.split('.')[1],'base64url')).sub,newOwnerId,'Freed mailbox authenticates only its new subject, never original history');

  const control='fictional.control.'+id+'@example.test';
  const controlUser=await request(root+'/users','POST',{username:control,email:control,emailVerified:true,enabled:true,firstName:'Fictional Control Adult'});
  equal(controlUser.status,201,'Unretired positive control created');
  const controlId=controlUser.location.split('/').at(-1),changedControl='fictional.changed.'+id+'@example.test';
  equal((await request(root+'/users/'+controlId,'PUT',{username:changedControl,email:changedControl,emailVerified:false})).status,204,'Same standard provider update works without a retirement guard');
  await fs.writeFile(path.join(folder,'refused-response.json'),JSON.stringify({status:restore.status,body:restore.body}),{mode:0o600});
  const makeSource=async(label,unrelated=false,withCredential=false)=>{
   const contact='fictional.'+label+'.'+id+'@example.test';
   const created=await request(root+'/users','POST',{username:contact,email:contact,emailVerified:true,enabled:true,firstName:'Fictional Adult',lastName:'Owned Fixture',...(withCredential?{credentials:[{type:'password',value:password,temporary:false}]}:{})});
   equal(created.status,201,'Owned '+label+' source');const sourceId=created.location.split('/').at(-1);assert.match(sourceId,/^[0-9a-f-]{36}$/);
   if(unrelated){await query(`UPDATE user_entity SET username='unrelated-${sourceId}' WHERE id='${sourceId}'`);await request(root+'/clear-user-cache','POST');}
   const projected=(await request(root+'/users/'+sourceId)).body;
   const binding=await provider('/users/'+sourceId+'/email-binding');equal(binding.status,200,'Exact raw provider snapshot available');
   const view=binding.body;
   if(unrelated)await fs.writeFile(path.join(folder,'unrelated-snapshot-diagnostic.json'),JSON.stringify({projectedUsername:projected.username,rawUsername:view.username,rawDatabaseUsername:await query(`SELECT username FROM user_entity WHERE id='${sourceId}'`)}),{mode:0o600});
   return {id:sourceId,email:contact,view,route:'/users/'+sourceId+'/retire-email',body:{operationId:crypto.randomUUID(),expectedEmail:view.email,expectedUsername:view.username}};
  };
  const unrelated=await makeSource('unrelated-login',true);
  const unrelatedReply=await provider(unrelated.route,'POST',unrelated.body);
  equal(unrelatedReply.status,200,'Retire reviewed mailbox while preserving unrelated login; code='+(unrelatedReply.body?.code||'none')); 
  equal((await provider('/users/'+unrelated.id+'/email-binding')).body.username,unrelated.view.username,'Actual unrelated provider username preserved');
  equal(await query(`SELECT login_retired FROM swoovr_email_retirement WHERE user_id='${unrelated.id}'`),'f','Provider marker distinguishes unrelated login');
  const rollback=await makeSource('late-rollback',false,true);
  await query(`CREATE FUNCTION fixture_late_retirement_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.user_id='${rollback.id}' THEN RAISE EXCEPTION 'Fictional deferred provider failure'; END IF; RETURN NEW; END $$;
   CREATE CONSTRAINT TRIGGER fixture_late_retirement_failure AFTER INSERT ON swoovr_email_retirement DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fixture_late_retirement_failure();`);
  const failed=await provider(rollback.route,'POST',rollback.body);
  await fs.writeFile(path.join(folder,'late-failure-response.json'),JSON.stringify(failed),{mode:0o600});
  equal(failed.status>=400,true,'Late provider transaction failure is not a success response');
  equal(await query(`SELECT count(*) FROM swoovr_email_retirement WHERE user_id='${rollback.id}'`),'0','Commit-time failure rolls back provider marker');
  equal(await query(`SELECT email FROM user_entity WHERE id='${rollback.id}'`),rollback.email,'Commit-time failure keeps original binding');
  equal(await query(`SELECT count(*) FROM credential WHERE user_id='${rollback.id}'`),'1','Late failure preserves unrelated password record');
  equal((await provider(rollback.route+'/'+rollback.body.operationId)).status,404,'Missing provider receipt never falsely confirms completion');
  await query('DROP TRIGGER fixture_late_retirement_failure ON swoovr_email_retirement; DROP FUNCTION fixture_late_retirement_failure()');
  equal((await provider(rollback.route,'POST',rollback.body)).status,200,'Same opaque operation safely retries after provider recovery');
  equal((await provider(rollback.route+'/'+rollback.body.operationId)).status,200,'Separate status request proves committed retry state');
  const repeated=await makeSource('concurrent-same');
  const same=await Promise.all([1,2].map(()=>provider(repeated.route,'POST',repeated.body)));
  equal(same.every(result=>result.status===200),true,'Simultaneous same-operation requests converge');
  equal(await query(`SELECT count(*) FROM swoovr_email_retirement WHERE user_id='${repeated.id}'`),'1','Concurrent requests record one provider policy');
  const competing=await makeSource('concurrent-different');
  const different=await Promise.all([competing.body,{...competing.body,operationId:crypto.randomUUID()}].map(body=>provider(competing.route,'POST',body)));
  equal(different.map(result=>result.status).sort().join(','),'200,409','Only one independently requested retirement wins');

  // Independent stale database writer: read before retirement, wait on a separate owned advisory barrier,
  // then attempt the old email after provider commit. Never relax the production guard for the fixture.
  const staleSource=await makeSource('stale-writer'),space=1398230851,key=crypto.randomBytes(4).readInt32BE();
  const openSql=()=>{
   const child=spawn('docker',['exec','-i','swoovr-local-postgres-1','psql','-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-q','-A','-t','-X'],{stdio:['pipe','pipe','pipe']});
   const state={child,out:'',err:'',closed:once(child,'close')};child.stdout.on('data',chunk=>state.out+=chunk);child.stderr.on('data',chunk=>state.err+=chunk);
   state.wait=marker=>new Promise((resolve,reject)=>{
    const ready=()=>{if(state.out.includes(marker)){cleanup();resolve();}};
    const failed=()=>{cleanup();reject(new Error('Owned SQL connection ended before barrier.'));};
    const timer=setTimeout(()=>{cleanup();reject(new Error('Owned SQL barrier timed out.'));},15000);
    const cleanup=()=>{clearTimeout(timer);child.stdout.off('data',ready);child.off('close',failed);};
    child.stdout.on('data',ready);child.once('close',failed);ready();
   });return state;
  };
  const holder=openSql(),stale=openSql();
  try{
   holder.child.stdin.write(`SELECT pg_advisory_lock(${space},${key}); SELECT 'owned-barrier-ready';\n`);await holder.wait('owned-barrier-ready');
   stale.child.stdin.write(`BEGIN ISOLATION LEVEL READ COMMITTED;SELECT email FROM user_entity WHERE id='${staleSource.id}';SELECT 'owned-stale-read';SELECT pg_advisory_xact_lock(${space},${key});UPDATE user_entity SET email='${staleSource.email}',email_constraint='${staleSource.email}' WHERE id='${staleSource.id}';COMMIT;\n`);
   await stale.wait('owned-stale-read');
   equal((await provider(staleSource.route,'POST',staleSource.body)).status,200,'Provider retirement commits while independent stale reader waits');
   holder.child.stdin.end('\\q\n');await holder.closed;
   const [exit]=await stale.closed;
   equal(exit!==0&&stale.err.includes('Swoovr retired contact policy refuses'),true,'Delayed independent writer sees committed guard and refuses old mailbox');
   equal(await query(`SELECT email FROM user_entity WHERE id='${staleSource.id}'`),'retired.'+staleSource.body.operationId+'@swoovr.invalid','Stale rollback preserves retired binding');
  }finally{holder.child.stdin.end();stale.child.stdin.end();}
  await query('ALTER TABLE user_entity DISABLE TRIGGER swoovr_retired_contact_guard');
  equal((await provider('/capabilities')).status,503,'Disabled guard is unavailable, never reported configured');
  await query('ALTER TABLE user_entity ENABLE TRIGGER swoovr_retired_contact_guard');
  equal((await provider('/capabilities')).status,200,'Restored guard resumes capability probe');
  // Explicit operator-mistake fixture: refuse to confirm/reapply against a later inconsistent binding.
  await query('ALTER TABLE user_entity DISABLE TRIGGER swoovr_retired_contact_guard');
  await query(`UPDATE user_entity SET email='fictional.operator-mismatch.${id}@example.test',email_constraint='fictional.operator-mismatch.${id}@example.test' WHERE id='${userId}'`);
  await query('ALTER TABLE user_entity ENABLE TRIGGER swoovr_retired_contact_guard');
  equal((await provider(retireRoute+'/'+operation)).status,503,'Receipt alone cannot confirm inconsistent current provider binding');
  equal((await provider(retireRoute,'POST',retireBody)).status,503,'Same-operation retry never overwrites an inconsistent later binding');
  await query(`UPDATE user_entity SET email='${sink}',email_constraint='${sink}' WHERE id='${userId}'`);
  equal((await provider(retireRoute+'/'+operation)).status,200,'Explicit fixture repair restores truthful provider completion');

  console.log('PASS: '+checks+' isolated Keycloak/PostgreSQL schema-guard checks. Actual custom endpoint/CAS and guard; browser registration, ownership and delivery not proved.');
 }finally{
  let cleanupError;
  if(started){
   try{const logs=await run('docker',['logs',container],{captureCombined:true});await fs.writeFile(path.join(folder,'keycloak.log'),logs,{mode:0o600});}catch(error){cleanupError=error;}
   try{await run('docker',['rm','-f',container]);}catch(error){cleanupError??=error;}
  }
  if(created)try{await sql('DROP DATABASE '+database+' WITH (FORCE)');}catch(error){cleanupError??=error;}
  console.log('CLEANUP: owned Keycloak container/database removal attempted; private build/provider logs '+folder);
  if(cleanupError)throw cleanupError;
 }
})().catch(error=>{console.error(error);process.exitCode=1;});
