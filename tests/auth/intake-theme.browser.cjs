// Fully owned Keycloak + mail sink: never touches the installed realms, accounts or retained screenshots.
const fs=require('node:fs/promises'),os=require('node:os'),path=require('node:path'),crypto=require('node:crypto'),net=require('node:net'),assert=require('node:assert/strict');
const {execFile}=require('node:child_process');
const run=(command,args,env={})=>new Promise((resolve,reject)=>execFile(command,args,{env:{...process.env,...env},maxBuffer:2e6},(error,out)=>error?reject(Error(command+' failed (details kept private)')):resolve(out.trim())));
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 for(const port of [18180,18025]){const probe=net.createServer();await new Promise((resolve,reject)=>{probe.once('error',reject);probe.listen(port,'127.0.0.1',resolve)});await new Promise(resolve=>probe.close(resolve));}
 const root=path.resolve(__dirname,'../..'),dir=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-intake-theme-'));await fs.chmod(dir,0o700);
 const id='swoovr-theme-'+crypto.randomBytes(5).toString('hex'),network=id+'-net',keycloak=id+'-kc',mail=id+'-mail';
 const bootstrap=crypto.randomBytes(24).toString('hex'),password=crypto.randomBytes(24).toString('hex');
 let edge,page,networkCreated=false;const containers=[];
 try{
  const realm=JSON.parse(await fs.readFile(path.join(root,'containers/keycloak/realms/swoovr-members-dev.json'),'utf8'));
  realm.sslRequired='none';realm.clients=[{clientId:'theme-browser',publicClient:true,protocol:'openid-connect',standardFlowEnabled:true,redirectUris:['http://localhost:15881/*']}];
  realm.users=[{username:'fictional.theme@example.test',email:'fictional.theme@example.test',firstName:'Fictional',lastName:'Adult',emailVerified:true,enabled:true,credentials:[{type:'password',value:password,temporary:false}]}];
  realm.users.push({...realm.users[0],username:'fictional.disabled@example.test',email:'fictional.disabled@example.test',enabled:false});
  await fs.writeFile(path.join(dir,'realm.json'),JSON.stringify(realm),{mode:0o600});
  const image=await run('docker',['inspect','swoovr-platform-keycloak-1','--format','{{.Config.Image}}']);
  const mailImage=await run('docker',['inspect','swoovr-platform-mailpit-1','--format','{{.Config.Image}}']);
  await run('docker',['network','create',network]);networkCreated=true;
  await run('docker',['run','--rm','-d','--name',mail,'--network',network,'--network-alias','mailpit','-p','127.0.0.1:18025:8025',mailImage]);containers.push(mail);
  await run('docker',['run','--rm','-d','--name',keycloak,'--network',network,'-p','127.0.0.1:18180:8080','--env','KC_BOOTSTRAP_ADMIN_USERNAME=local-bootstrap','--env','KC_BOOTSTRAP_ADMIN_PASSWORD','-v',path.join(dir,'realm.json')+':/opt/keycloak/data/import/realm.json:ro','-v',path.join(root,'containers/keycloak/themes/swoovr')+':/opt/keycloak/themes/swoovr:ro',image,'start-dev','--import-realm','--hostname-strict=false','--spi-theme--cache-themes=false','--spi-theme--cache-templates=false','--spi-theme--static-max-age=-1'],{KC_BOOTSTRAP_ADMIN_PASSWORD:bootstrap});containers.push(keycloak);
  const base='http://localhost:18180',realmPath='/realms/swoovr-members-dev';let ready=false;
  for(let i=0;i<450;i++){try{if((await fetch(base+realmPath+'/.well-known/openid-configuration')).ok){ready=true;break}}catch{}await sleep(200)}assert(ready,'owned Keycloak ready');
  const grant=await fetch(base+'/realms/master/protocol/openid-connect/token',{method:'POST',body:new URLSearchParams({grant_type:'password',client_id:'admin-cli',username:'local-bootstrap',password:bootstrap})});const token=(await grant.json()).access_token;assert(token,'owned management session');
  const api=async(p,method='GET',body)=>{const r=await fetch(base+'/admin'+realmPath+p,{method,headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});assert(r.ok,'owned provider configuration '+r.status);return [201,204].includes(r.status)?null:r.json()};
  await (await import('../../scripts/auth/realms.mjs')).applyRealm(api,'swoovr-members-dev');
  const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'/Applications/ChatGPT.app/Contents/Resources/cua_node/lib/node_modules/playwright');edge=await require('./managed-edge.cjs').managedEdge(chromium);
  const context=await edge.browser.newContext({reducedMotion:'reduce'}),failures=[];page=await context.newPage();
  const check=async(name,fn)=>{try{await fn()}catch(error){failures.push(name+': '+error.message)}};
  const inspect=async(stage)=>{
   for(const scheme of ['light','dark']){await page.emulateMedia({colorScheme:scheme});for(const width of [375,768,1440]){await page.setViewportSize({width,height:1000});await check(stage+' '+scheme+' reflow '+width,async()=>assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)));await page.evaluate(async()=>{await document.fonts.ready;scrollTo(0,0);await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))});await page.waitForTimeout(200);if(width!==768)await page.screenshot({animations:'disabled',path:'/tmp/swoovr-intake-auth-'+stage+'-'+width+'-'+scheme+'.png',fullPage:true});}}
  };
  const auth=()=>base+realmPath+'/protocol/openid-connect/auth?'+new URLSearchParams({client_id:'theme-browser',redirect_uri:'http://localhost:15881/account',response_type:'code',scope:'openid',state:crypto.randomBytes(16).toString('hex')});
  await page.goto(auth());await page.waitForSelector('#kc-form-login');
  for(const width of [375,768,1440]){await page.setViewportSize({width,height:1000});await check('sign-in reflow '+width,async()=>assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)));const box=await page.locator('#kc-form-login').boundingBox();assert(box.width<=421,'constrained sign-in form');}
  await inspect('sign-in');
  await check('desktop frame',async()=>assert(await page.locator('.pf-v5-c-login__main').evaluate(el=>getComputedStyle(el).minHeight==='0px'),'desktop form is not forced to phone-height layout'));
  await page.getByRole('link',{name:'Create an account',exact:false}).click();await page.waitForSelector('#kc-register-form');
  await page.fill('#firstName','Fictional Adult');await page.fill('#email','fictional.theme@example.test');if(await page.locator('#password').count()){await page.fill('#password',password);await page.fill('#password-confirm',password);}assert(await page.locator('#termsAccepted').count(),'adult attestation retained');await page.check('#termsAccepted');await page.click('#kc-register-form button[type=submit]');
  await page.waitForSelector('[aria-invalid=true]');
  await check('registration error focus',async()=>assert(await page.locator('[aria-invalid=true]').first().evaluate(el=>el===document.activeElement),'first invalid registration field receives focus'));
  await check('registration error description',async()=>assert(await page.locator('[aria-invalid=true]').first().getAttribute('aria-describedby'),'invalid field links to its error'));
  await inspect('registration-error');
  const confirmations=[],durations=[];
  for(const email of ['fictional.theme@example.test','fictional.absent@example.test','fictional.disabled@example.test']){
   await page.goto(auth());await page.getByRole('link',{name:'Forgot password?',exact:false}).click();await page.waitForSelector('#kc-reset-password-form');
   await check('email keyboard',async()=>assert.equal(await page.locator('#username').getAttribute('type'),'email'));
   await inspect('reset-request');
   await page.fill('#username',email);const started=Date.now();await page.click('#kc-reset-password-form button[type=submit]');await page.waitForLoadState();durations.push(Date.now()-started);
   await check('dedicated reset confirmation',async()=>{assert.equal(await page.locator('#kc-form-login').count(),0);assert.equal(await page.locator('[data-reset-confirmation]').count(),1);});
   // Issue 252: "Back to sign in" and "Try another email" are buttons (54px, full width), not text links.
   await check('reset actions are buttons',async()=>{for(const hook of ['[data-reset-back]','[data-reset-try-another]']){const box=await page.locator(hook).boundingBox(),form=await page.locator('[data-reset-confirmation]').boundingBox();assert(box&&box.height>=54&&Math.abs(box.width-form.width)<2,hook+' is a full-width 54px button');assert.equal(await page.locator(hook).evaluate(e=>getComputedStyle(e).textDecorationLine),'none',hook+' has no link underline');}});
   await inspect('reset-confirmation');
   confirmations.push((await page.locator('body').innerText()).replace(/\s+/g,' ').trim());
  }
  await check('neutral reset response',async()=>assert(confirmations.every(text=>text===confirmations[0]),'existing, nonexistent and disabled addresses show the same acknowledgement'));
  const mailbox=await (await fetch('http://localhost:18025/api/v1/messages')).json();
  await check('local email effects',async()=>assert.equal(mailbox.total,1,'only the enabled existing fixture receives a reset message in the owned sink'));
  console.log('Reset request samples (ms; one per enabled/absent/disabled fixture, not a timing-security proof):',durations.join(', '));
  await context.close();assert.equal(failures.length,0,failures.join('\n'));
  console.log('PASS: actual isolated Keycloak/Edge sign-in and registration/reset UI, duplicate-email focus, light/dark reflow and neutral enabled/absent/disabled reset confirmation with local mail effects.');
 }catch(error){
  if(page&&!page.isClosed()){console.error('UI input inventory:',JSON.stringify(await page.locator('input').evaluateAll(els=>els.map(e=>({id:e.id,name:e.name,type:e.type})))));await page.screenshot({path:'/tmp/swoovr-intake-theme-failure.png',fullPage:true});}
  throw error;
 }finally{
  const errors=[];if(edge)try{await edge.close()}catch{errors.push('browser')}
  for(const name of containers.reverse())try{await run('docker',['stop','--time','5',name])}catch{errors.push(name)}
  if(networkCreated)try{await run('docker',['network','rm',network])}catch{errors.push('network')}
  await fs.rm(dir,{recursive:true,force:true});assert.equal(errors.length,0,'owned cleanup: '+errors.join(', '));console.log('CLEANUP: owned provider, mail sink, network, browser and private fixture removed.');
 }
})().catch(error=>{console.error(error.message);process.exitCode=1});
