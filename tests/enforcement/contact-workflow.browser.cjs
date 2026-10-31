// Owned Keycloak/API/SSR integration only. Entrypoint is keycloak-contact.integration.cjs, never main's provider.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path'),net=require('node:net');
const {execFile,spawn}=require('node:child_process'),{once}=require('node:events');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const run=(command,args,options={})=>new Promise((resolve,reject)=>execFile(command,args,{maxBuffer:2*1024*1024,...options},(error,out,err)=>error?reject(new Error(command+' failed: '+err.slice(-1200))):resolve(out.trim())));
module.exports.run=async({base,container,bootstrapUser,bootstrapPassword,folder,providerQuery})=>{
 if(!/^swoovr-kc-retire-[0-9a-f]{32}$/.test(container)||!/^http:\/\/127\.0\.0\.1:[0-9]+$/.test(base))throw new Error('An owned provider context is required.');
 const provider=base.replace('127.0.0.1','localhost'),members='swoovr-members-dev',staffRealm='swoovr-staff-dev';
 const database='swoovr_contact_workflow_'+crypto.randomUUID().replaceAll('-',''),root=process.cwd(),apiDir=path.join(root,'src/Swoovr.Api');
 const {localEnv,sql}=require('../auth/local-tokens.cjs');
 const stamp=new Date().toISOString().replaceAll(/[-:.]/g,''),backup=path.join(root,'.local/log-backups','contact-workflow-'+stamp);await fs.mkdir(backup,{recursive:true,mode:0o700});
 const screenshots=process.env.SWOOVR_SCREENSHOTS;if(screenshots)await fs.mkdir(screenshots,{recursive:true});
 const equal=(actual,expected,message)=>{assert.equal(actual,expected,message);checks++;};let checks=0,created=false,apiHost,adminHost,restoreRoot,edge,context;
 const query=q=>run('docker',['exec','swoovr-local-postgres-1','psql','-U','swoovr','-d',database,'-v','ON_ERROR_STOP=1','-tAc',q]);
 const mapped=await run('docker',['port',container,'8080/tcp']);equal(base,'http://'+mapped,'Provider context matches its owned Docker mapping');
 const secrets={SWOOVR_MEMBERS_WEB_SECRET:'Fictional-'+crypto.randomBytes(24).toString('hex'),SWOOVR_STAFF_WEB_SECRET:'Fictional-'+crypto.randomBytes(24).toString('hex')};
 const serviceSecret='Fictional-'+crypto.randomBytes(24).toString('hex');
 const rootReply=await fetch(provider+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:bootstrapUser,password:bootstrapPassword})});
 equal(rootReply.status,200,'Owned fixture bootstrap authenticates against the browser hostname');const administrator=(await rootReply.json()).access_token;
 const management=async(route,method='GET',body)=>{
  const response=await fetch(provider+'/admin/'+route,{method,headers:{Authorization:'Bearer '+administrator,...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});
  assert.ok(response.ok,'Owned provider management request failed ('+response.status+').');return response.status===204||response.headers.get('content-length')==='0'?null:await response.json().catch(()=>null);
 };
 const {applyRealm,applyAccountsClient}=await import('../../scripts/auth/realms.mjs');
 for(const name of [members,staffRealm]){
  let source=await fs.readFile(path.join(root,'containers/keycloak/realms',name+'.json'),'utf8');
  source=source.replace(/\$\{([A-Z0-9_]+)\}/g,(_,key)=>{if(!secrets[key])throw new Error('Unexpected realm fixture secret placeholder.');return secrets[key];});
  const realm=JSON.parse(source);realm.smtpServer={host:'host.docker.internal',port:'1025',from:'no-reply@swoovr.example.test',fromDisplayName:'Swoovr local test',ssl:'false',starttls:'false',auth:'false'};
  if(name===members)realm.attributes['swoovr.contact-policy']='enabled';
  await management('realms','POST',realm);const api=(route='',method='GET',body)=>management('realms/'+name+route,method,body);
  await applyRealm(api,name);if(name===members)await applyAccountsClient(api,serviceSecret);
 }
 const makePerson=async(realm,label)=>{
  const email='fictional.'+label+'.'+crypto.randomBytes(8).toString('hex')+'@example.test',password='Fictional-'+crypto.randomBytes(20).toString('hex');
  // Existing source/staff fixture provisioning only. This does not prove real mailbox ownership.
  await management('realms/'+realm+'/users','POST',{username:email,email,firstName:'Fictional',lastName:'Adult',enabled:true,emailVerified:true,credentials:[{type:'password',value:password,temporary:false}]});return{email,password};
 };
 const source=await makePerson(members,'contact-source'),reviewer=await makePerson(staffRealm,'contact-reviewer'),support=await makePerson(staffRealm,'contact-support');
 const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');edge=await require('../auth/managed-edge.cjs').managedEdge(chromium);const browser=edge.browser;
 const token=async(realm,person,{register=false}={})=>{
  const redirect='http://localhost:'+(realm===members?'5081':'5082')+'/signin-oidc',clientId=realm===members?'swoovr-web':'swoovr-admin-web';
  const secret=secrets[realm===members?'SWOOVR_MEMBERS_WEB_SECRET':'SWOOVR_STAFF_WEB_SECRET'],verifier=crypto.randomBytes(32).toString('base64url');
  const state=crypto.randomBytes(16).toString('base64url'),nonce=crypto.randomBytes(16).toString('base64url');let code;
  const session=await browser.newContext();const page=await session.newPage();
  try{
   page.on('request',request=>{if(request.url().startsWith(redirect)){const url=new URL(request.url());assert.equal(url.searchParams.get('state'),state);code=url.searchParams.get('code');}});
   await page.route(redirect+'**',route=>route.fulfill({body:'owned authorization callback captured'}));
   const endpoint=register?'registrations':'auth';
   await page.goto(provider+'/realms/'+realm+'/protocol/openid-connect/'+endpoint+'?'+new URLSearchParams({client_id:clientId,response_type:'code',scope:'openid profile email',redirect_uri:redirect,code_challenge:crypto.createHash('sha256').update(verifier).digest('base64url'),code_challenge_method:'S256',state,nonce}));
   if(register){
    const after=Date.now();await page.fill('#email',person.email);await page.fill('#firstName','Fictional New Owner');await page.check('#termsAccepted');
    await page.click('#kc-register-form [type=submit]');let link;
    for(let attempt=0;attempt<80&&!link;attempt++){
     const inbox=await(await fetch('http://127.0.0.1:8025/api/v1/messages')).json();
     for(const message of inbox.messages||[])if(message.To.some(to=>to.Address===person.email)&&Date.parse(message.Created)>=after){
      const mail=await(await fetch('http://127.0.0.1:8025/api/v1/message/'+message.ID)).json();
      link=(mail.Text.match(/https?:\/\/\S+/g)||[]).find(value=>value.startsWith(provider+'/'));
     }
     if(!link)await sleep(250);
    }
    assert.ok(link,'Owned sandbox verification message was not caught; no delivery claim.');await page.goto(link);
    await page.fill('#password-new',person.password);await page.fill('#password-confirm',person.password);await page.click('[type=submit]');
   }else{
    await page.fill('#username',person.email);await page.fill('#password',person.password);await page.click('#kc-login');await page.waitForLoadState();
    if(page.url().includes('execution=TERMS_AND_CONDITIONS'))await page.click('[name=accept]');
   }
   for(let attempt=0;attempt<150&&!code;attempt++)await sleep(100);assert.ok(code,'Owned authorization code was not issued.');
   const response=await fetch(provider+'/realms/'+realm+'/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'authorization_code',code,redirect_uri:redirect,client_id:clientId,client_secret:secret,code_verifier:verifier})});
   equal(response.status,200,'Genuine owned authorization-code/PKCE exchange');const body=await response.json();const claims=JSON.parse(Buffer.from(body.access_token.split('.')[1],'base64url'));
   equal(claims.iss,provider+'/realms/'+realm,'Token issuer is the owned realm');return{access:body.access_token,claims};
  }finally{await session.close();}
 };
 const start=async(args,cwd,env,name,log=path.join(folder,name+'.log'))=>{const file=await fs.open(log,'w',0o600);const child=spawn('dotnet',args,{cwd,env,stdio:['ignore',file.fd,file.fd],detached:true});await file.close();return child;};
 const stop=async child=>{if(!child||child.exitCode!==null||child.signalCode!==null)return;const closed=once(child,'exit');process.kill(-child.pid,'SIGTERM');await Promise.race([closed,sleep(5000)]);if(child.exitCode===null&&child.signalCode===null){process.kill(-child.pid,'SIGKILL');await closed;}};
 const healthy=async(url,child)=>{for(let attempt=0;attempt<150;attempt++){if(child.exitCode!==null||child.signalCode!==null)throw new Error('Owned host exited before readiness.');try{if((await fetch(url)).ok)return;}catch{}await sleep(100);}throw new Error('Owned host readiness timeout.');};
 let env;
 try{
  const probe=net.createServer();await new Promise(resolve=>probe.listen(0,'127.0.0.1',resolve));const api='http://127.0.0.1:'+probe.address().port;await new Promise(resolve=>probe.close(resolve));
  await sql('CREATE DATABASE '+database);created=true;
  env={...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:api,
   ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${database};Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
   Keycloak__BaseUrl:provider,Keycloak__RequireHttpsMetadata:'false',Staff__LocalPasswords:'true',Beta__Enabled:'false',Phone__Required:'false',
   MemberAdmin__ClientSecret:serviceSecret,MemberAdmin__ContactRetirementEnabled:'true',MemberAdmin__ContactRetirementWorker:'false',
   Founders__Secrets__1:crypto.randomBytes(32).toString('hex'),Bans__HmacKeys__1:crypto.randomBytes(32).toString('base64'),
   Email__Smtp__Host:'127.0.0.1',Email__Smtp__Port:'1025',Sms__Provider:'simulated',
   Media__Store:'local',Media__Local__Path:path.join(folder,'workflow-media'),Media__Screening__Worker:'false',Retention__Worker:'false'};
  const apiDll=path.join(apiDir,'bin/Debug/net10.0/Swoovr.Api.dll');
  await run('dotnet',[apiDll,'migrate'],{cwd:apiDir,env});apiHost=await start([apiDll],apiDir,env,'workflow-api');await healthy(api+'/health/live',apiHost);
  const call=async(jwt,route,method='GET',body)=>{const response=await fetch(api+route,{method,headers:{...(jwt?{Authorization:'Bearer '+jwt}:{}),'Swoovr-Version':'2026-10-28',...(body?{'Content-Type':'application/json'}:{})},...(body?{body:JSON.stringify(body)}:{})});let value;try{value=await response.json();}catch{}return{status:response.status,body:value};};
  const sourceToken=await token(members,source),reviewToken=await token(staffRealm,reviewer),supportToken=await token(staffRealm,support);
  for(const identity of [sourceToken,reviewToken,supportToken])equal((await call(identity.access,'/api/session')).status,200,'Owned identity admitted');
  const sourceId=await query(`SELECT id FROM accounts WHERE subject='${sourceToken.claims.sub}'`),reviewerId=await query(`SELECT id FROM accounts WHERE subject='${reviewToken.claims.sub}'`),supportId=await query(`SELECT id FROM accounts WHERE subject='${supportToken.claims.sub}'`);
  // Explicit application-role fixtures; no invitation/hosted Entra or identity ownership claim.
  await query(`INSERT INTO role_grants(id,account_id,role,granted_at) VALUES('${crypto.randomUUID()}','${reviewerId}','Admin',now()),('${crypto.randomUUID()}','${supportId}','Support',now())`);
  equal((await call(sourceToken.access,'/api/me/age','PUT',{dateOfBirth:'1990-01-01'})).status,200,'Fictional source is an adult');
  equal((await call(sourceToken.access,'/api/me/profile','PUT',{displayName:'Fictional Original Retained Profile'})).status,200,'Original profile has its own content');
  equal((await call(reviewToken.access,'/api/admin/access/grants','POST',{capability:'termination',purpose:'Fictional owned contact workflow'})).status,201,'Separate termination capability');
  equal((await call(reviewToken.access,'/api/admin/accounts/'+sourceId+'/sanctions','POST',{action:'terminate',reason:'Fictional original safety case'})).status,200,'Source terminated through API');
  const entry=await query(`SELECT b.id FROM banned_identifiers b JOIN moderation_cases c ON c.id=b.case_id WHERE c.subject_account_id='${sourceId}' AND b.identifier_type='Email'`);
  equal((await call(sourceToken.access,'/api/admin/banned-identifiers/'+entry+'/retire-email','POST',{circumstancesReviewed:true,reason:'Fictional'})).status,403,'Member cannot retire bindings');
  equal((await call(supportToken.access,'/api/admin/banned-identifiers/'+entry+'/retire-email','POST',{circumstancesReviewed:true,reason:'Fictional'})).status,403,'Support cannot retire bindings');
  const mainRoot=path.dirname(await fs.realpath(path.join(root,'.local'))),allowed=new Set([path.join(root,'src/Swoovr.Admin'),path.join(mainRoot,'src/Swoovr.Admin')]);
  const listeners=[...new Set((await run('lsof',['-tiTCP:5082','-sTCP:LISTEN'])).split('\n'))],owners=new Set();
  for(const pid of listeners){const cwd=(await run('lsof',['-a','-p',pid,'-d','cwd','-Fn'])).split('\n').find(line=>line.startsWith('n'))?.slice(1);assert.ok(allowed.has(cwd),'Only owned main/worktree Admin may be replaced.');owners.add(path.dirname(path.dirname(cwd)));}
  equal(owners.size,1,'One positively identified Admin checkout');restoreRoot=[...owners][0];
  for(const pid of listeners)try{process.kill(Number(pid),'SIGTERM');}catch(error){if(error.code!=='ESRCH')throw error;}
  await sleep(1000);
  adminHost=await start([path.join(root,'src/Swoovr.Admin/bin/Debug/net10.0/Swoovr.Admin.dll')],path.join(root,'src/Swoovr.Admin'),{...env,ASPNETCORE_URLS:'http://localhost:5082',Keycloak__Authority:provider+'/realms/'+staffRealm,Keycloak__ClientSecret:secrets.SWOOVR_STAFF_WEB_SECRET,Api__BaseUrl:api+'/'},'workflow-admin');
  await healthy('http://localhost:5082/signed-out',adminHost);
  context=await browser.newContext({viewport:{width:390,height:844}});const page=await context.newPage();
  await page.goto('http://localhost:5082/registry/'+entry);await page.fill('#username',reviewer.email);await page.fill('#password',reviewer.password);await page.click('#kc-login');await page.waitForSelector('[data-registry-detail]');
  equal(await page.locator('[data-registry-retire-email] button').isDisabled(),true,'Initial retirement requires separate elevated access');
  await page.goto('http://localhost:5082/access');await page.selectOption('select[name=capability]','identifier-release');await page.fill('textarea[name=purpose]','Fictional current ownership and source review');await page.click('[data-request]');await page.waitForSelector('[data-result=ok]');
  const firstGrant=await query(`SELECT id FROM access_grants WHERE account_id='${reviewerId}' AND capability='identifier-release' AND revoked_at IS NULL`);
  equal((await call(reviewToken.access,'/api/admin/banned-identifiers/release','POST',{identifierIds:[entry],reason:'Fictional current ownership sufficiently reviewed'})).status,200,'Identifier release is separate');
  await page.goto('http://localhost:5082/registry/'+entry);await page.locator('[data-registry-retire-email] [name=emailBindingReviewed]').check();await page.locator('[data-registry-retire-email] textarea').fill('Fictional reviewed retirement; preserve original history and credentials');
  if(screenshots)await page.locator('[data-email-retirement]').screenshot({path:path.join(screenshots,'email-retirement-request-review.png')});
  await page.locator('[data-registry-retire-email] button').click();await page.waitForSelector('[data-result=ok]');
  equal(await page.locator('[data-email-retirement]').getAttribute('data-email-retirement-state'),'Pending','UI does not claim provider completion at preparation');
  const operation=await query(`SELECT id FROM email_retirement_operations WHERE registry_id='${entry}'`);
  equal(await query(`SELECT count(*) FROM accounts WHERE id='${sourceId}' AND email IS NULL AND email_retired_at IS NOT NULL`),'1','Prepared source suppresses delivery/adoption');
  equal((await call(sourceToken.access,'/api/session')).status,401,'Old original token refused while pending');
  equal((await call(reviewToken.access,'/api/admin/access/grants/'+firstGrant+'/revoke','POST')).status,200,'Human approval can be withdrawn before dispatch');
  await run('dotnet',[apiDll,'contacts','reconcile'],{cwd:apiDir,env});
  equal(await query(`SELECT state FROM email_retirement_operations WHERE id='${operation}'`),'NeedsReview','Revoked approval never authorizes a provider mutation');
  equal(await providerQuery(`SELECT count(*) FROM swoovr_email_retirement WHERE operation_id='${operation}'`),'0','No provider marker after revoked approval');
  await page.goto('http://localhost:5082/access');await page.selectOption('select[name=capability]','identifier-release');await page.fill('textarea[name=purpose]','Fictional fresh independent resume review');await page.click('[data-request]');await page.waitForSelector('[data-result=ok]');
  await page.goto('http://localhost:5082/registry/'+entry);await page.locator('[data-registry-review-email] [name=emailBindingReviewed]').check();await page.locator('[data-registry-review-email] textarea').fill('Fictional current binding and ownership re-reviewed for the same command');
  if(screenshots)await page.locator('[data-email-retirement]').screenshot({path:path.join(screenshots,'email-retirement-resume-review.png')});
  await page.locator('[data-registry-review-email] button').click();await page.waitForSelector('[data-result=ok]');
  equal(await query(`SELECT count(*) FROM email_retirement_reviews WHERE operation_id='${operation}'`),'1','Fresh approval recorded separately');
  equal(await query(`SELECT access_grant_id FROM email_retirement_operations WHERE id='${operation}'`),firstGrant,'Original request grant is unchanged');
  await run('dotnet',[apiDll,'contacts','reconcile'],{cwd:apiDir,env});
  equal(await query(`SELECT state FROM email_retirement_operations WHERE id='${operation}'`),'Completed','Real provider current-state confirmation completes API operation');
  equal(await providerQuery(`SELECT operation_id FROM swoovr_email_retirement WHERE user_id='${sourceToken.claims.sub}' AND realm_id=(SELECT id FROM realm WHERE name='${members}')`),operation,'Exactly the original opaque command confirmed');
  equal(await providerQuery(`SELECT count(*) FROM credential WHERE user_id='${sourceToken.claims.sub}' AND type='password'`),'1','Original password credential retained');
  await page.goto('http://localhost:5082/registry/'+entry);equal(await page.locator('[data-email-retirement]').getAttribute('data-email-retirement-state'),'Completed','SSR shows confirmed completion');
  if(screenshots)await page.locator('[data-email-retirement]').screenshot({path:path.join(screenshots,'email-retirement-completed.png')});
  equal((await call(sourceToken.access,'/api/session')).status,401,'Old original JWT remains refused after completion');
  const preview=await call(reviewToken.access,'/api/admin/access/grants','POST',{capability:'account-reinstatement',purpose:'Fictional separate source review'});equal(preview.status,201,'Reinstatement remains a separate capability');
  const restoration=await call(reviewToken.access,'/api/admin/accounts/'+sourceId+'/reinstatement');equal(restoration.body.blockedCode,'contact-recovery-required','Retired contact cannot accidentally restore original access');
  const newOwner={email:source.email,password:'Fictional-'+crypto.randomBytes(20).toString('hex')};const registered=await token(members,newOwner,{register:true});
  assert.notEqual(registered.claims.sub,sourceToken.claims.sub);checks++;
  const newToken=await token(members,newOwner);equal((await call(newToken.access,'/api/session')).status,200,'New owner registers using its own newly verified sandbox subject');
  const nextId=await query(`SELECT id FROM accounts WHERE subject='${newToken.claims.sub}'`);assert.notEqual(nextId,sourceId);checks++;
  equal(await query(`SELECT count(*) FROM sanctions WHERE account_id='${sourceId}' AND kind='Termination' AND lifted_at IS NULL`),'1','Original termination remains');
  const newProfile=await call(newToken.access,'/api/me/profile');equal(newProfile.status,200,'New owner has its own profile');assert.notEqual(newProfile.body.displayName,'Fictional Original Retained Profile');checks++;
  equal(await query(`SELECT count(*) FROM member_profiles WHERE account_id='${sourceId}' AND display_name='Fictional Original Retained Profile'`),'1','Original profile was not moved or erased');
  const again=await call(reviewToken.access,'/api/admin/banned-identifiers/'+entry+'/retire-email','POST',{circumstancesReviewed:true,reason:'Fictional completed retry'});equal(again.body.operationId,operation,'Completed request does not rotate operation id');
  console.log('PASS: owned Keycloak/API/admin contact workflow ('+checks+' checks; sandbox email-link consumption, not real ownership/delivery, hosted Entra or native UI).');
 }finally{
  let cleanupError;
  try{if(context)await context.close();if(edge)await edge.close();}catch(error){cleanupError=error;}
  try{await stop(adminHost);await stop(apiHost);}catch(error){cleanupError??=error;}
  if(restoreRoot){
   try{
    await fs.copyFile(path.join(root,'.local/logs/admin.log'),path.join(backup,'previous-shared-admin.log'));await fs.chmod(path.join(backup,'previous-shared-admin.log'),0o600);
    const restored=await start(['run','--project','src/Swoovr.Admin','--no-build','--no-launch-profile'],restoreRoot,{...process.env,DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://localhost:5082',Keycloak__Authority:'http://localhost:8180/realms/swoovr-staff-dev',Keycloak__ClientSecret:await localEnv('SWOOVR_STAFF_WEB_SECRET'),Keycloak__RequireHttpsMetadata:'false',Api__BaseUrl:'http://127.0.0.1:5080/'},'restored-admin',path.join(root,'.local/logs/admin.log'));
    restored.unref();await healthy('http://localhost:5082/signed-out',restored);
    await fs.copyFile(path.join(root,'.local/logs/admin.log'),path.join(backup,'restored-admin.log'));await fs.chmod(path.join(backup,'restored-admin.log'),0o600);
   }catch(error){cleanupError??=error;}
  }
  for(const name of ['workflow-api','workflow-admin','restored-admin'])try{await fs.copyFile(path.join(folder,name+'.log'),path.join(backup,name+'.log'));await fs.chmod(path.join(backup,name+'.log'),0o600);}catch(error){if(error.code!=='ENOENT')cleanupError??=error;}
  if(created)try{await sql('DROP DATABASE '+database+' WITH (FORCE)');}catch(error){cleanupError??=error;}
  console.log('CLEANUP: contact workflow owned hosts/database removed; original Admin restored when replaced. Private logs: '+backup);
  if(cleanupError)throw cleanupError;
 }
};
if(require.main===module)throw new Error('Run only through the owned Keycloak component runner.');
