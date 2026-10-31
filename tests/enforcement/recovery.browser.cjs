// Actual local Keycloak/API/PostgreSQL and Edge UI. Fictional adults only; email ownership is admin-provisioned,
// not proof of a delivered verification link. Requires documented phone-off hosts; no native/device delivery claim.
const assert=require('node:assert/strict');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {web,createMember,memberToken,rootAdminToken,cli,sql,call,claims,elevate,matchReady}=require('../auth/local-tokens.cjs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});let context,checks=0;
 const equal=(actual,expected,label)=>{assert.equal(actual,expected,label);checks++;};
 try{
  const root=await rootAdminToken(browser),source=await createMember('recovery-source'),member=await createMember('recovery-member');
  await cli('beta','add',source.email,member.email);
  const sourceToken=await memberToken(browser,source),originalToken=await memberToken(browser,member);
  equal((await call(sourceToken,'/api/session')).status,200,'fictional source admitted');
  equal((await call(originalToken,'/api/session')).status,200,'legitimate member admitted');
  const sourceId=await sql(`SELECT id FROM accounts WHERE subject='${claims(sourceToken).sub}'`),id=await sql(`SELECT id FROM accounts WHERE subject='${claims(originalToken).sub}'`);
  equal((await call(originalToken,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Recovery',homeCityId:2078025}})).status,200,'profile prepared');
  assert.deepEqual(await matchReady(originalToken),[200,200]);checks++;
  await elevate(root,'termination','Fictional source for email recovery acceptance');
  equal((await call(root,`/api/admin/accounts/${sourceId}/sanctions`,{method:'POST',body:{action:'terminate',reason:'Fictional permanent termination for recovery acceptance'}})).status,200,'actual termination creates restriction');
  equal((await call(sourceToken,'/api/me/deletion',{method:'POST',body:{confirm:true,immediately:true,deleteFounderToken:false}})).status,200,'fictional terminated source asks for deletion');
  await cli('accounts','purge',sourceId);
  equal(await sql(`SELECT status FROM accounts WHERE id='${sourceId}'`),'Deleted','local provider/source deletion completed');
  const {localRealmApi}=await import('../../scripts/auth/provision.mjs');const realm=await localRealmApi({realm:'swoovr-members-dev'});
  const subject=claims(originalToken).sub;
  await realm('/users/'+subject,'PUT',{username:source.email,email:source.email,emailVerified:true});
  const changedToken=await memberToken(browser,{...member,email:source.email});
  const session=await call(changedToken,'/api/session');
  equal(session.status,200,'new-token session is usable');equal(session.body.emailChangeProblem,'identifier-restricted','neutral adoption warning');
  equal(session.body.memberArea,true,'existing beta/member access preserved');
  equal((await call(changedToken,'/api/me/profile')).status,200,'new-token profile access');
  equal((await call(changedToken,'/api/me/standing')).body.emailChangeProblem,'identifier-restricted','standing warning');
  equal(await sql(`SELECT email FROM accounts WHERE id='${id}'`),member.email,'accepted email retained');
  equal(await sql(`SELECT status FROM accounts WHERE id='${id}'`),'Active','legitimate account not banned');
  context=await browser.newContext({viewport:{width:1280,height:900}});const page=await context.newPage();
  await page.goto(web+'/account/sign-in?intro=done');await page.fill('#username',source.email);await page.fill('#password',member.password);
  await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);
  const warning=page.locator('[data-email-change-problem="identifier-restricted"]');await warning.waitFor();
  equal(await warning.locator('[data-email-support]').getAttribute('href'),'https://swoovr.com/support','approved support link');
  assert.ok((await warning.innerText()).includes('previously accepted email'));checks++;
  assert.ok(!(await warning.innerText()).includes(sourceId));checks++;
  const output=process.env.SWOOVR_SCREENSHOTS;
  if(output)await page.screenshot({path:output+'/email-recovery-account-desktop.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'narrow account does not overflow');
  if(output)await warning.screenshot({path:output+'/email-recovery-account-mobile.png'});
  await page.goto(web+'/me/standing');await warning.waitFor();
  equal(await page.locator('[data-standing-state]').getAttribute('data-standing-state'),'active','standing stays active');
  if(output)await page.screenshot({path:output+'/email-recovery-standing-mobile.png',fullPage:true});
  const bff=await context.request.get(web+'/bff/api/me/standing',{headers:{'X-Swoovr-Bff':'1','Swoovr-Version':'2026-10-28'}});equal(bff.status(),200,'BFF preserves usable standing');
  equal((await bff.json()).emailChangeProblem,'identifier-restricted','BFF transports neutral warning');
  // Correcting to the already accepted address needs no old token or additional identifier quota.
  await realm('/users/'+subject,'PUT',{username:member.email,email:member.email,emailVerified:true});
  await page.locator('a[href^="/account/reauthenticate"]').click();
  if(await page.locator('#username').isVisible())await page.fill('#username',member.email);
  await page.fill('#password',member.password);await Promise.all([page.waitForURL(web+'/account'),page.click('#kc-login')]);
  await page.waitForSelector('[data-account-panel="ready"]');
  equal(await page.locator('[data-email-change-problem]').count(),0,'fresh corrected sign-in clears warning');
  const corrected=await memberToken(browser,member);
  equal((await call(corrected,'/api/session')).body.emailChangeProblem,null,'corrected API session has no warning');
  equal(await sql(`SELECT count(*) FROM contact_histories WHERE account_id='${id}' AND contact_type='Email'`),'0','refused/corrected claim never changed contact history');
  console.log(`PASS: email change recovery (${checks} checks; fictional local provider administration, real API/BFF/Edge; no native UI or delivered email proof).`);
 }finally{if(context)await context.close();await browser.close();}
})().catch(error=>{console.error(error);process.exit(1);});
