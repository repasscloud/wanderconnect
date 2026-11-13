// Issue #240 regression, restricted to disposable owned resources. Real Edge chat pages remain idle
// for 370 seconds, spanning the unchanged 300-second Keycloak token lifetime. Run with the explicit
// messaging-lifetime selector in tests/auth/run-owned.cjs; no owner users, clients or database are used.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const {chromium} = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const {managedEdge} = require('../auth/managed-edge.cjs');
const {web,admin,ownedRunId,createMember,memberToken,cli,sql,call,claims,matchReady,sleep,fillRootAdmin} = require('../auth/local-tokens.cjs');
assert(ownedRunId, 'Requires disposable owned runner');
const log = (event, extra={}) => console.log(JSON.stringify({at:new Date().toISOString(),event,...extra}));
(async()=>{
 const edge=await managedEdge(chromium), browser=edge.browser;
 try {
  const members=[await createMember('life-a'),await createMember('life-b')];
  await cli('beta','add',...members.map(m=>m.email));
  const tokens=[],ids=[],profiles=[];
  for(const m of members){
   const t=await memberToken(browser,m);tokens.push(t);
   const p=await call(t,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Lifetime',homeCityId:2078025}});
   assert.equal(p.status,200);profiles.push(p.body.publicId);
   await call(t,'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true}});
   await matchReady(t);
   ids.push(await sql(`SELECT id FROM accounts WHERE subject='${claims(t).sub}'`));
  }
  const request=await call(tokens[0],'/api/requests',{method:'POST',body:{to:profiles[1],mode:'friends'}});
  assert.equal(request.status,201);
  assert.equal((await call(tokens[1],`/api/requests/${request.body.id}/accept`,{method:'POST'})).status,200);
  const conversation=await call(tokens[0],'/api/conversations',{method:'POST',body:{to:profiles[1]}});
  assert.equal(conversation.status,201);const id=conversation.body.id;
  const contexts=[],pages=[],lifecycle=[[],[]];
  for(let i=0;i<2;i++){
   const context=await browser.newContext();contexts.push(context);const page=await context.newPage();pages.push(page);
   page.on('websocket',ws=>{if(!ws.url().includes('/bff/hubs/messages'))return;
    lifecycle[i].push({kind:'open',at:Date.now()});log('browser-socket-open',{browser:i});
    ws.on('framereceived',({payload})=>{
     // Capture only lifecycle metadata, never message frames, cookies or connection identifiers.
     if(typeof payload!=='string')return;
     for(const part of payload.split('\u001e').filter(Boolean)){
      const frame=JSON.parse(part);
      if(frame.type===7)log('browser-close-frame',{browser:i,allowReconnect:frame.allowReconnect===true});
     }
    });
    ws.on('close',()=>{lifecycle[i].push({kind:'close',at:Date.now()});log('browser-socket-close',{browser:i});});
   });
   await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',members[i].email);await page.fill('#password',members[i].password);
   await Promise.all([page.waitForURL(web+'/explore'),page.click('#kc-login')]);
   await page.goto(web+'/chats/'+id);await page.waitForSelector('[data-conversation="ready"][data-live="on"]');
  }
  async function exchange(sender,label){
   const text='Fictional lifetime '+label;
   await pages[sender].fill('[data-message-input]',text);await pages[sender].click('[data-send]');
   for(const page of pages)await page.waitForFunction(text=>[...document.querySelectorAll('[data-message] p')].some(p=>p.textContent===text),text,{timeout:15000});
   log('browser-send-and-receive',{sender,label});
  }
  // Use the real staff UI and its same-session fresh MFA elevation; never seed an authorization receipt.
  // Prepare it before the long observation, so a fixture authorization failure fails promptly.
  const staff=await browser.newPage();
  await staff.goto(admin+'/sign-in');await fillRootAdmin(staff);
  await staff.waitForURL(u=>u.origin===admin&&!u.pathname.includes('signin-oidc'));
  await staff.goto(admin+'/sign-in?elevate=true&returnUrl=/access');
  // Keycloak's same-session reauthentication form omits the username field.
  const fixturePath=process.env.SWOOVR_TEST_ROOT_FIXTURE;
  const fixture=JSON.parse(await fs.readFile(fixturePath,'utf8'));
  assert.equal(fixture.runId,ownedRunId);assert.equal(fixture.fictional,true);
  await staff.fill('#password',fixture.password);await staff.click('#kc-login');
  await staff.waitForSelector('#otp');
  while(Math.floor(Date.now()/30000)<=(fixture.lastOtpStep??-1))await sleep(500);
  fixture.lastOtpStep=Math.floor(Date.now()/30000);
  await fs.writeFile(fixturePath,JSON.stringify(fixture),{mode:0o600});
  const {totp}=await import('../../scripts/auth/totp.mjs');
  await staff.fill('#otp',totp(fixture.totpSecret));await staff.click('#kc-login');
  await staff.waitForURL(admin+'/access?mfa=verified');
  await staff.locator('[data-mfa-elevated]:not([data-mfa-elevated=none])').waitFor();
  log('staff-mfa-elevated');
  await exchange(0,'before-a');await exchange(1,'before-b');
  const started=Date.now();log('observation-start');
  // No navigation, reload or API polling: exercise actual idle browser reconnection.
  while(Date.now()-started<370000){await sleep(1000);if((Date.now()-started)%30000<1000)log('observation',{seconds:Math.floor((Date.now()-started)/1000)});}
  log('observation-finish',{seconds:Math.floor((Date.now()-started)/1000),lifecycle:lifecycle.map(x=>x.map(e=>({kind:e.kind,seconds:Math.round((e.at-started)/1000)})))});
  assert(lifecycle.every(x=>x.filter(e=>e.kind==='open').length>=2),'both real browsers reconnected across token expiry');
  await exchange(0,'after-a');await exchange(1,'after-b');
  for(const page of pages)assert.equal(await page.locator('[data-message]').count(),4,'history retained without duplicates');
  log('history-retained');
  await staff.goto(admin+`/accounts/${ids[0]}/access`);
  await staff.click('[data-dialog-open="sign-out"]');
  const before=lifecycle[0].filter(e=>e.kind==='close').length;
  const opened=lifecycle[0].filter(e=>e.kind==='open').length, revoked=Date.now();
  await staff.click('[data-confirm="sign-out"]');
  await staff.getByText('Signed out on every device.',{exact:true}).waitFor();
  for(let i=0;i<100&&lifecycle[0].filter(e=>e.kind==='close').length===before;i++)await sleep(100);
  assert(lifecycle[0].filter(e=>e.kind==='close').length>before,'retired browser socket closes promptly');
  log('revoked-browser-closed',{milliseconds:Date.now()-revoked});
  await pages[1].fill('[data-message-input]','Fictional after revocation');await pages[1].click('[data-send]');
  await pages[1].waitForFunction(()=>[...document.querySelectorAll('[data-message] p')].some(p=>p.textContent==='Fictional after revocation'));
  await sleep(3000);
  assert.equal(lifecycle[0].filter(e=>e.kind==='open').length,opened,'terminal revocation does not start a reconnect loop');
  log('revoked-connection-stayed-closed');
  assert.equal(await pages[0].getByText('Fictional after revocation',{exact:true}).count(),0,'revoked browser receives no push');
  const refused=await pages[0].request.post(web+`/bff/api/conversations/${id}/messages`,{headers:{'X-Swoovr-Bff':'1'},data:{text:'Fictional refused send'}});
  assert.equal(refused.status(),401);
  const deniedRead=await pages[0].request.get(web+`/bff/api/conversations/${id}/messages`,{headers:{'X-Swoovr-Bff':'1'}});
  assert.equal(deniedRead.status(),401);
  log('revoked-send-and-read-refused');
  console.log('PASS: issue 240 real browser lifetime, bidirectional delivery, history and prompt revocation.');
 }catch(error){console.error('SCENARIO FAILURE:',error);throw error;}
 finally{await edge.close().catch(error=>{
  // Edge's subprocesses can briefly retain the owned profile after the main engine exits.
  // The enclosing owned runner removes this whole private runtime directory after stopping hosts.
  if(error.code!=='ENOTEMPTY')throw error;
  log('browser-profile-cleanup-deferred-to-owned-runner');
 });}
})().catch(error=>{console.error(error);process.exitCode=1;});
