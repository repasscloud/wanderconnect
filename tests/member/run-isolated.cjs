// Owned real-OIDC member harness. See docs/delivery/ISSUE-MEMBER-FIXES.md.
// Never takes ports, resets Docker or writes the shared private configuration.
const net=require('node:net');
const fs=require('node:fs/promises'),{execFile,spawn}=require('node:child_process'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const path=require('node:path');
const root=path.resolve(__dirname,'../..');
const privateConfig=process.env.SWOOVR_MEMBER_TEST_ENV;
if(!privateConfig||!path.isAbsolute(privateConfig))throw Error('Set SWOOVR_MEMBER_TEST_ENV to the existing managed local docker.env absolute path.');
const currentPublish=process.env.SWOOVR_MEMBER_TEST_PUBLISH;
if(!currentPublish)throw Error('Supply SWOOVR_MEMBER_TEST_PUBLISH; see evidence README.');
const run=(c,a,e={})=>new Promise((res,rej)=>execFile(c,a,{cwd:root,env:{...process.env,...e},maxBuffer:8e6},(err,out,stderr)=>err?rej(new Error(c+': '+stderr.slice(-1200))):res(out.trim())));
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 // Refuse occupied ports; never take over an existing host.
 for(const port of [15880,15881,11025,18025]){const probe=net.createServer();await new Promise((resolve,reject)=>{probe.once('error',reject);probe.listen(port,'127.0.0.1',resolve)});await new Promise(resolve=>probe.close(resolve));}
 const raw=await fs.readFile(privateConfig,'utf8');
 const config=Object.fromEntries(raw.split('\n').filter(l=>/^[A-Z0-9_]+=/.test(l)).map(l=>{const i=l.indexOf('=');return[l.slice(0,i),l.slice(i+1).replace(/^['"]|['"]$/g,'')]}));
 const db='swoovr_member_settings_'+crypto.randomBytes(8).toString('hex'),clientId='swoovr-member-settings-'+crypto.randomBytes(5).toString('hex'),secret=crypto.randomBytes(32).toString('hex');
 const email=clientId+'@example.test',password=crypto.randomBytes(24).toString('hex');
 const sql=q=>run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d',db,'-v','ON_ERROR_STOP=1','-tAc',q]);
 let userId,clientUuid,token,edge,ownedMedia,mailStarted=false;const children=[];
 const authenticate=async()=>{const response=await fetch('http://localhost:8180/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'local-bootstrap',password:config.KC_BOOTSTRAP_ADMIN_PASSWORD})});
 token=(await response.json()).access_token;if(!token)throw Error('Local management authentication failed');};
 await authenticate();
 const kc=async(path,method='GET',body)=>{const r=await fetch('http://localhost:8180/admin/realms/swoovr-members-dev'+path,{method,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});if(!r.ok)throw Error('Owned identity setup '+r.status+' '+path);return r.status===204||r.status===201?null:r.json()};
 const env={DOTNET_ENVIRONMENT:'Development',ASPNETCORE_ENVIRONMENT:'Development',ConnectionStrings__Swoovr:`Host=127.0.0.1;Port=5433;Database=${db};Username=swoovr;Password=${config.SWOOVR_DB_PASSWORD}`,Keycloak__BaseUrl:'http://localhost:8180',Keycloak__RequireHttpsMetadata:'false',Beta__Enabled:'true',Phone__Required:'true',Sms__Provider:'simulated',Email__Provider:'smtp',Email__Smtp__Host:'127.0.0.1',Email__Smtp__Port:'11025',Logging__LogLevel__Default:'Warning',Retention__Worker:'false',Media__Screening__Worker:'false',Founders__Secrets__1:crypto.randomBytes(32).toString('hex')};
 const start=async(dll,cwd,port,extra={})=>{const log=await fs.open('/tmp/'+clientId+'-'+port+'.log','w',0o600);const c=spawn('dotnet',[dll],{cwd,env:{...process.env,...env,...extra,ASPNETCORE_URLS:'http://localhost:'+port},stdio:['ignore',log.fd,log.fd]});await log.close();children.push(c);for(let i=0;i<150;i++){try{if((await fetch('http://localhost:'+port+(port===15880?'/health/live':'/'))).ok)return;}catch{}await sleep(100)}throw Error('host readiness '+port)};
 try{
 ownedMedia=await fs.mkdtemp(require('node:os').tmpdir()+'/swoovr-member-fixture-');await fs.chmod(ownedMedia,0o700);
 env.Media__Store='local';env.Media__Local__Path=ownedMedia;
 const mailImage=await run('docker',['inspect','swoovr-platform-mailpit-1','--format','{{.Config.Image}}']);
 await run('docker',['run','--rm','-d','--name',clientId,'-p','127.0.0.1:11025:1025','-p','127.0.0.1:18025:8025',mailImage]);mailStarted=true;
 await run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d','postgres','-c','CREATE DATABASE '+db]);
 await run('dotnet',['src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll','migrate'],env);
 const base=(await kc('/clients?clientId=swoovr-web'))[0],template=await kc('/clients/'+base.id);delete template.id;template.clientId=clientId;template.secret=secret;template.name='Owned member settings browser test';template.redirectUris=['http://localhost:15881/signin-oidc'];template.webOrigins=[];template.attributes={...template.attributes,'swoovr.owner':'member-settings-test'};
 for(const mapper of template.protocolMappers??[])delete mapper.id;
 await kc('/clients','POST',template);clientUuid=(await kc('/clients?clientId='+clientId))[0].id;
 await kc('/users','POST',{username:email,email,firstName:'Fictional',lastName:'Member',requiredActions:['TERMS_AND_CONDITIONS'],emailVerified:true,enabled:true,credentials:[{type:'password',value:password,temporary:false}]});userId=(await kc('/users?username='+email))[0].id;
 await run('dotnet',['src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll','beta','add',email],env);
 await run('dotnet',['src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll','geonames','import',root+'/tests/fixtures/geonames'],env);
 await start(root+'/src/Swoovr.Api/bin/Release/net10.0/Swoovr.Api.dll',root+'/src/Swoovr.Api',15880);
 const webEnv={Keycloak__Authority:'http://localhost:8180/realms/swoovr-members-dev',Keycloak__ClientId:clientId,Keycloak__ClientSecret:secret,Api__BaseUrl:'http://localhost:15880/'};
 await start(path.join(currentPublish,'Swoovr.Web.dll'),currentPublish,15881,webEnv);
 const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright');edge=await require(root+'/tests/auth/managed-edge.cjs').managedEdge(chromium);const context=await edge.browser.newContext({viewport:{width:1440,height:1000}}),page=await context.newPage();
 await page.goto('http://localhost:15881/account/sign-in?intro=done');
 await page.fill('#username',email);await page.fill('#password',password);await page.click('#kc-login');
 if(await page.locator('[name=accept]').count())await page.locator('[name=accept]').click();
 await page.waitForURL('http://localhost:15881/**');
 await require('./settings.browser.cjs')({page,context,sql,ownedMedia,root});
 await context.close();console.log('PASS: member settings with real OIDC, beta and phone gates enabled, disposable database.');
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
 if(userId)await clean('fictional member',()=>kc('/users/'+userId,'DELETE'));
 if(clientUuid)await clean('temporary client',()=>kc('/clients/'+clientUuid,'DELETE'));
 await clean('database',()=>run('docker',['exec','swoovr-platform-postgres-1','psql','-p','5433','-U','swoovr','-d','postgres','-c','DROP DATABASE IF EXISTS '+db+' WITH (FORCE)']));
 if(mailStarted)await clean('owned mail sink',()=>run('docker',['stop',clientId]));
 if(ownedMedia)await clean('synthetic media directory',()=>fs.rm(ownedMedia,{recursive:true,force:true}));
 if(errors.length)throw Error('Owned resource cleanup incomplete: '+errors.join('; '));
 console.log('CLEANUP: owned hosts, browser, database, temporary client and fictional member removed.');
 }
})().catch(e=>{console.error(e);process.exitCode=1});
