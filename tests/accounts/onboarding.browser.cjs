// D292: real owned Keycloak/API/BFF; no production data, no simulated provider acceptance.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {createMember,memberToken,cli,call,web,sql,claims}=require('../auth/local-tokens.cjs');
if(!process.env.SWOOVR_TEST_API_BASE||!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Requires owned runner');
(async()=>{
 const edge=await require('../auth/managed-edge.cjs').managedEdge(chromium);
 try{
  const member=await createMember('onboarding-adult');await cli('beta','add',member.email);
  const token=await memberToken(edge.browser,member);
  const me=await call(token,'/api/me');assert.equal(me.status,200);assert.equal(me.body.dateOfBirth,null);
  assert.equal((await call(token,'/api/me/safety-advice')).body.acknowledged,false);
  assert.equal((await call(token,'/api/me/safety-advice',{method:'POST',body:{revision:'2026-10-30.1',confirmed:true}})).status,409,'cannot dismiss before DOB setup');
  const context=await edge.browser.newContext();
  try{
   // Issue 254 (D342): sign-in lands on the onboarding screens (no tabs) and every page leads back there until setup is done.
   const page=await context.newPage();await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',member.email);await page.fill('#password',member.password);await Promise.all([page.waitForURL(web+'/welcome'),page.click('#kc-login')]);
   assert.equal(me.body.setupStep,'dateOfBirth');assert.equal(await page.locator('[data-tab]').count(),0,'no tabs during onboarding');
   for(const route of ['/account','/explore','/me/profile']){await page.goto(web+route);await page.waitForURL(web+'/welcome');}
   await page.waitForSelector('[data-onboarding-step=dob]');assert.match(await page.locator('.onboarding-eyebrow').innerText(),/Step 1 of 1/i);
   await page.fill('#onboarding-dob','1990-05-01');await page.check('[data-onboarding-dob-confirm]');await page.click('[data-onboarding-step=dob] button[type=submit]');
   await page.waitForSelector('[data-onboarding-step=done]');assert.equal((await call(token,'/api/me')).body.setupStep,null);
   await page.goto(web+'/account');await page.waitForSelector('[data-dob-locked=saved]');
   await page.waitForSelector('[data-advice-dismiss]');await page.locator('[data-advice-dismiss]').click();await page.waitForSelector('[data-advice-dismiss]',{state:'detached'});
   await page.reload();await page.waitForSelector('[data-account-panel=ready]');assert.equal(await page.locator('[data-advice-dismiss]').count(),0,'dismissal survives reload');
  }finally{await context.close();}
  const another=await memberToken(edge.browser,member),saved=(await call(another,'/api/me/safety-advice')).body;
  assert.equal(saved.acknowledged,true,'same account another session sees dismissal');
  assert.equal((await call(token,'/api/me/safety-advice',{method:'POST',body:{revision:'unknown',confirmed:true}})).status,400);
  await call(token,'/api/me/safety-advice',{method:'POST',body:{revision:saved.revision,confirmed:true}});
  assert.equal((await call(token,'/api/me/safety-advice')).body.acknowledgedAt,saved.acknowledgedAt,'retry retains original server time');
  await sql(`UPDATE accounts SET registered_names_confirmed_at=NULL WHERE subject='${claims(token).sub}'`);
  const recovery=await edge.browser.newContext();
  try{
   const page=await recovery.newPage();await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',member.email);await page.fill('#password',member.password);await Promise.all([page.waitForURL(web+'/welcome'),page.click('#kc-login')]);
   for(const route of ['/explore','/connections','/chats','/account']){await page.goto(web+route);await page.waitForURL(web+'/welcome');await page.waitForSelector('[data-onboarding-step=names]');}
   for(const open of ['/account/data','/me/support','/terms','/privacy','/community-guidelines']){await page.goto(web+open);assert.equal(new URL(page.url()).pathname,open,open+' stays reachable during setup');}
  }finally{await recovery.close();}
  console.log('PASS: real owned setup recovery, DOB-only progress, server advice dismissal across reload/session, unknown revision and retry. Phone-enabled path remains separate phone suite.');
 }finally{await edge.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
