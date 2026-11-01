// Owned local integration/preview harness. See docs/delivery/evidence/2026-11-01-admin-redesign/README.md.
// Never takes ports, resets Docker or writes the shared private configuration.
const net=require('node:net');
const fs=require('node:fs/promises'),{execFile,spawn}=require('node:child_process'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const path=require('node:path');
const root=path.resolve(__dirname,'../..');
const privateConfig=process.env.SWOOVR_ADMIN_TEST_ENV;
if(!privateConfig||!path.isAbsolute(privateConfig))throw Error('Set SWOOVR_ADMIN_TEST_ENV to the existing managed local docker.env absolute path.');
const currentPublish=process.env.SWOOVR_ADMIN_TEST_PUBLISH;
const baselinePublish=process.env.SWOOVR_ADMIN_BASELINE_PUBLISH||currentPublish;
if(!currentPublish)throw Error('Supply SWOOVR_ADMIN_TEST_PUBLISH; see evidence README.');
const run=(c,a,e={})=>new Promise((res,rej)=>execFile(c,a,{cwd:root,env:{...process.env,...e},maxBuffer:8e6},(err,out,stderr)=>err?rej(new Error(c+': '+stderr.slice(-1200))):res(out.trim())));
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 // Refuse occupied ports; never take over an existing host.
 for(const port of [15880,15882,15883]){const probe=net.createServer();await new Promise((resolve,reject)=>{probe.once('error',reject);probe.listen(port,'127.0.0.1',resolve)});await new Promise(resolve=>probe.close(resolve));}
 const raw=await fs.readFile(privateConfig,'utf8');
 const config=Object.fromEntries(raw.split('\n').filter(l=>/^[A-Z0-9_]+=/.test(l)).map(l=>{const i=l.indexOf('=');return[l.slice(0,i),l.slice(i+1).replace(/^['"]|['"]$/g,'')]}));
 const db='swoovr_admin_design_'+crypto.randomBytes(8).toString('hex'),clientId='swoovr-admin-design-'+crypto.randomBytes(5).toString('hex'),secret=crypto.randomBytes(32).toString('hex');
 const email=clientId+'@example.test',password=crypto.randomBytes(24).toString('hex');
 const sql=q=>run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d',db,'-v','ON_ERROR_STOP=1','-tAc',q]);
 let userId,clientUuid,token,edge;const children=[];
 const authenticate=async()=>{const response=await fetch('http://localhost:8180/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'local-bootstrap',password:config.KC_BOOTSTRAP_ADMIN_PASSWORD})});
 token=(await response.json()).access_token;if(!token)throw Error('Local management authentication failed');};
 await authenticate();
 const kc=async(path,method='GET',body)=>{const r=await fetch('http://localhost:8180/admin/realms/swoovr-staff-dev'+path,{method,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});if(!r.ok)throw Error('Owned identity setup '+r.status+' '+path);return r.status===204||r.status===201?null:r.json()};
 const env={DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${db};Username=swoovr;Password=${config.SWOOVR_DB_PASSWORD}`,Keycloak__BaseUrl:'http://localhost:8180',Keycloak__RequireHttpsMetadata:'false',Staff__LocalPasswords:'true',Beta__Enabled:'false',Phone__Required:'false',Retention__Worker:'false',Media__Screening__Worker:'false',Founders__Secrets__1:crypto.randomBytes(32).toString('hex')};
 const start=async(dll,cwd,port,extra={})=>{const log=await fs.open('/tmp/'+clientId+'-'+port+'.log','w',0o600);const c=spawn('dotnet',[dll],{cwd,env:{...process.env,...env,...extra,ASPNETCORE_URLS:'http://localhost:'+port},stdio:['ignore',log.fd,log.fd]});await log.close();children.push(c);for(let i=0;i<150;i++){try{if((await fetch('http://localhost:'+port+(port===15880?'/health/live':'/signed-out'))).ok)return;}catch{}await sleep(100)}throw Error('host readiness '+port)};
 try{
 await run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d','postgres','-c','CREATE DATABASE '+db]);
 await run('dotnet',['src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll','migrate'],env);
 const base=(await kc('/clients?clientId=swoovr-admin-web'))[0],template=await kc('/clients/'+base.id);delete template.id;template.clientId=clientId;template.secret=secret;template.name='Owned admin design browser test';template.redirectUris=['http://localhost:15882/signin-oidc','http://localhost:15883/signin-oidc'];template.webOrigins=[];template.attributes={...template.attributes,'swoovr.owner':'admin-design-test'};
 if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='sessions') template.attributes['access.token.lifespan']='5';
 for(const mapper of template.protocolMappers??[])delete mapper.id;
 await kc('/clients','POST',template);clientUuid=(await kc('/clients?clientId='+clientId))[0].id;
 await kc('/users','POST',{username:email,email,firstName:'Fictional',lastName:'Staff',emailVerified:true,enabled:true,credentials:[{type:'password',value:password,temporary:false}]});userId=(await kc('/users?username='+email))[0].id;
 await start(root+'/src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll',root+'/src/Swoovr.Api',15880);
 const adminEnv={Keycloak__Authority:'http://localhost:8180/realms/swoovr-staff-dev',Keycloak__ClientId:clientId,Keycloak__ClientSecret:secret,Api__BaseUrl:'http://localhost:15880/'};
 await start(path.join(baselinePublish,'Swoovr.Admin.dll'),baselinePublish,15883,adminEnv);
 await start(path.join(currentPublish,'Swoovr.Admin.dll'),currentPublish,15882,adminEnv);
 const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright');edge=await require(root+'/tests/auth/managed-edge.cjs').managedEdge(chromium);const context=await edge.browser.newContext({viewport:{width:1440,height:1000}}),page=await context.newPage();
 const login=async(port)=>{await page.goto('http://localhost:'+port+'/cases');if(await page.locator('#username').count()){await page.fill('#username',email);await page.fill('#password',password);await page.click('#kc-login')}await page.waitForURL('http://localhost:'+port+'/cases');};
 await login(15882);
 if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='accounts') await require('./accounts.browser.cjs').refused(page);
 if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='beta') await require('./beta.browser.cjs').refused(page);
 // Local fixture authority only in the disposable database. Production permissions are untouched.
 const staffId=await sql(`SELECT id FROM accounts WHERE subject='${userId}'`);assert(staffId);
 await sql(`INSERT INTO role_grants (id,account_id,role,granted_at) VALUES ('${crypto.randomUUID()}','${staffId}','Admin',now())`);
 // Dense fictional case queue; no real member, evidence or private content is copied.
 for(let i=0;i<14;i++){const id=crypto.randomUUID(),caseId=crypto.randomUUID();await sql(`INSERT INTO accounts (id,issuer,subject,realm,email,status,is_root_admin,created_at,last_seen_at,delete_founder_token) VALUES ('${id}','http://localhost:8180/realms/swoovr-members-dev','design-fictional-${i}','Members','fictional.design.${i}@example.test','Active',false,now(),now(),false); INSERT INTO moderation_cases (id,subject_account_id,status,queue,priority,created_at,updated_at) VALUES ('${caseId}','${id}','Open','Support','${i%4===0?'Urgent':'Normal'}',now(),now())`)}
 if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='beta'){
  const member=await sql("SELECT id FROM accounts WHERE subject='design-fictional-0'");
  await sql(`INSERT INTO beta_enrolments(id,email,created_at,revoked_at,activated_at,account_id) SELECT gen_random_uuid(),CASE WHEN n=1 THEN 'fictional.design.0@example.test' ELSE 'fictional.paging.'||lpad(n::text,4,'0')||'@example.test' END,now()-n*interval '1 minute',CASE WHEN n=2 THEN now() END,CASE WHEN n=1 THEN now() END,CASE WHEN n=1 THEN '${member}'::uuid END FROM generate_series(1,1005) n`);
 }
 const out=process.env.SWOOVR_ADMIN_SCREENSHOTS||root+'/.local/admin-design-evidence';await fs.mkdir(out,{recursive:true});
 if(process.env.SWOOVR_ADMIN_BASELINE_PUBLISH){await login(15883);await page.reload();await page.screenshot({path:out+'/before-queue-1440.png',fullPage:true});
 await page.goto('http://localhost:15883/access');await page.screenshot({path:out+'/before-access-1440.png',fullPage:true});}
 await login(15882);await page.reload();await page.screenshot({path:out+'/after-queue-1440.png',fullPage:true});
 if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='accounts') await require('./accounts.browser.cjs').found(page);
 else if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='sessions') await require('./sessions.browser.cjs')({page,kc,userId});
 else if(process.env.SWOOVR_ADMIN_TEST_SCENARIO==='beta') await require('./beta.browser.cjs')({page,base:'http://localhost:15882'});
 else await require(root+'/tests/admin/shell.browser.cjs')({page,base:'http://localhost:15882',out});
 if(process.env.SWOOVR_ADMIN_PREVIEW==='1'){
  const previewDir=await fs.mkdtemp(require('node:os').tmpdir()+'/swoovr-admin-preview-');await fs.chmod(previewDir,0o700);
  const fixture=path.join(previewDir,'fictional-staff.json');await fs.writeFile(fixture,JSON.stringify({url:'http://localhost:15882',email,password,fictional:true}),{mode:0o600});
  console.log('Preview ready: http://localhost:15882 ; fictional sign-in details in '+fixture+' . Ctrl+C cleans up owned resources.');
  await new Promise(resolve=>{process.once('SIGINT',resolve);process.once('SIGTERM',resolve)});await fs.rm(previewDir,{recursive:true,force:true});
 }
 await context.close();console.log('PASS: real Keycloak fictional staff session + disposable PostgreSQL/API + SSR browser redesign checks.');
 }finally{
 // An expired management session or failed browser shutdown must not skip other owned resources.
 const errors=[];
 const clean=async(label,action)=>{try{await action()}catch(error){errors.push(label+': '+error.message)}};
 await clean('browser',async()=>{if(edge)await edge.close()});
 for(const c of children.reverse())await clean('host '+c.pid,async()=>{
  if(c.exitCode!==null||c.signalCode!==null)return;
  const exited=new Promise(resolve=>c.once('exit',resolve));c.kill('SIGTERM');
  let timer;await Promise.race([exited,new Promise(resolve=>{timer=setTimeout(resolve,4000)})]);clearTimeout(timer);
  if(c.exitCode===null&&c.signalCode===null){c.kill('SIGKILL');await exited;}
 });
 await clean('management refresh',authenticate);
 if(userId)await clean('fictional staff',()=>kc('/users/'+userId,'DELETE'));
 if(clientUuid)await clean('temporary client',()=>kc('/clients/'+clientUuid,'DELETE'));
 await clean('database',()=>run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d','postgres','-c','DROP DATABASE IF EXISTS '+db+' WITH (FORCE)']));
 if(errors.length)throw Error('Owned resource cleanup incomplete: '+errors.join('; '));
 console.log('CLEANUP: owned hosts, browser, database, temporary client and fictional staff removed.');
 }
})().catch(e=>{console.error(e);process.exitCode=1});
