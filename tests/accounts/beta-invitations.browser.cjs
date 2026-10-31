// Owned API/Admin/local worker/Mailpit. Fictional adults only; no external email or Apple operation.
const assert=require('node:assert/strict'),fs=require('node:fs/promises'),path=require('node:path');
if(!process.env.SWOOVR_TEST_APP_DATABASE||process.env.Beta__Invitations__Enabled!=='true')throw Error('Use enabled owned beta-invitation runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright');
const {managedEdge}=require('../auth/managed-edge.cjs');
const {admin,call,createStaff,staffToken,rootAdminToken,mailTo,sql,sleep}=require('../auth/local-tokens.cjs');
(async()=>{
 const edge=await managedEdge(chromium);let checks=0;
 try{
  const root=await rootAdminToken(edge.browser),operator=await createStaff('beta-mail-admin');
  assert.equal((await call(root,'/api/admin/invitations',{method:'POST',body:{email:operator.email,role:'admin'}})).status,201);
  const token=await staffToken(edge.browser,operator);await call(token,'/api/session');
  const page=await edge.browser.newPage();await page.goto(admin+'/sign-in');await page.fill('#username',operator.email);await page.fill('#password',operator.password);await page.click('#kc-login');await page.waitForURL(admin+'/**');await page.goto(admin+'/beta');
  const email='fictional.beta-invitation.'+Date.now()+'@example.test';
  await page.fill('[data-beta-add] input[name=email]',email);await page.click('[data-beta-add] button');
  await page.waitForSelector('[data-beta-result=added]');assert.match(await page.locator('[data-beta-result]').innerText(),/queued; not sent or delivered/);checks++;
  let entry;for(let n=0;n<100;n++){entry=(await call(token,'/api/admin/beta')).body.find(e=>e.email===email);if(entry?.invitationState==='accepted')break;await sleep(250);}
  assert.equal(entry?.invitationState,'accepted');assert.equal(entry.invitationAttempts,1);assert.ok(entry.invitationCompletedAt);checks++;
  const mail=await mailTo(email);assert.equal(mail.length,1);assert.ok(mail[0].includes('http://localhost:5081/account'));assert.ok(!mail[0].includes('TestFlight'));checks++;
  await page.reload();const row=page.locator('[data-beta-row="'+entry.id+'"]');await row.waitFor();assert.match(await row.locator('[data-beta-invitation]').innerText(),/Provider accepted.*Acceptance is not delivery/);checks++;
  const repeated=await call(token,'/api/admin/beta',{method:'POST',body:{email:email.toUpperCase()}});assert.equal(repeated.status,200);assert.equal(repeated.body.id,entry.id);assert.equal(repeated.body.invitationAttempts,1);checks++;
  await sleep(16000);assert.equal((await mailTo(email)).length,1);checks++;
  assert.equal(await sql(`SELECT count(*) FROM beta_enrolments WHERE id='${entry.id}' AND account_id IS NULL AND activated_at IS NULL`),'1','email acceptance never grants account/Apple activation');checks++;
  if(process.env.SWOOVR_SCREENSHOTS){await fs.mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await page.setViewportSize({width:390,height:900});await page.screenshot({path:path.join(process.env.SWOOVR_SCREENSHOTS,'beta-invitation-email-390.png'),fullPage:true});}
  console.log(`PASS beta invitation queue/Admin/local Mailpit worker: ${checks} checks (no external acceptance/delivery evidence).`);
 }finally{await edge.close();}
})().catch(error=>{console.error(error);process.exitCode=1});
