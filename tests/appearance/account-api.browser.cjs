// Actual owned API/Keycloak/Web BFF. No preference UI/device/auth-provider-delivery claim.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
const {chromium}=require(process.env.SWOOVR_PLAYWRIGHT_PATH||'playwright'),{managedEdge}=require('../auth/managed-edge.cjs');
const {api,web,call,createMember,memberToken,rootAdminToken,cli,sql,claims}=require('../auth/local-tokens.cjs');
(async()=>{const edge=await managedEdge(chromium);let checks=0;
 try{
  const root=await rootAdminToken(edge.browser),a=await createMember('appearance-first'),b=await createMember('appearance-second');await cli('beta','add',a.email,b.email);
  const ta=await memberToken(edge.browser,a),tb=await memberToken(edge.browser,b);await call(ta,'/api/me');await call(tb,'/api/me');
  const id=await sql(`SELECT id FROM accounts WHERE subject='${claims(ta).sub}'`);
  assert.equal((await call(null,'/api/me/appearance')).status,401);checks++;
  assert.equal((await call(root,'/api/me/appearance')).status,403);checks++;
  const owner=token=>crypto.createHash('sha256').update(claims(token).iss+'\0'+claims(token).sub).digest('hex');
  assert.equal((await call(ta,'/api/me/appearance')).status,204);assert.equal(await sql(`SELECT count(*) FROM member_appearances WHERE account_id='${id}'`),'0');checks++;
  const wrongOwner=await call(tb,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner(ta)},body:{preference:{scheme:'coral',mode:'dark'},expectedRevision:0,changeId:crypto.randomUUID()}});
  assert.equal(wrongOwner.status,409,'an old account edit cannot mutate the newly authenticated account');assert.equal(wrongOwner.body.code,'appearance-account-changed');checks++;
  assert.equal((await call(tb,'/api/me/appearance')).status,204,'mismatched owner created no preference');checks++;
  for(const body of [{preference:{scheme:'retired',mode:'system'},expectedRevision:0,changeId:crypto.randomUUID()},{preference:{scheme:'standard',mode:'auto'},expectedRevision:0,changeId:crypto.randomUUID()},{preference:{scheme:'standard',mode:'system'},expectedRevision:-1,changeId:crypto.randomUUID()},{preference:null,expectedRevision:0,changeId:crypto.randomUUID()}]){assert.equal((await call(ta,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner(ta)},body})).status,400);checks++;}
  const first={preference:{scheme:'eucalyptus',mode:'system'},expectedRevision:0,changeId:crypto.randomUUID()},saved=await call(ta,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner(ta)},body:first});assert.equal(saved.status,200);assert.deepEqual(saved.body,{preference:first.preference,revision:1});checks++;
  assert.deepEqual((await call(ta,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner(ta)},body:first})).body,saved.body);checks++;
  const writes=await Promise.all(['pride','coral'].map(scheme=>call(ta,'/api/me/appearance',{method:'PUT',headers:{'Swoovr-Appearance-Owner':owner(ta)},body:{preference:{scheme,mode:'dark'},expectedRevision:1,changeId:crypto.randomUUID()}})));
  assert.deepEqual(writes.map(r=>r.status).sort(),[200,409]);assert.equal(writes.find(r=>r.status===409).body.current.revision,2);checks++;
  assert.equal((await call(tb,'/api/me/appearance?accountId='+id)).status,204);checks++;
  await sql(`UPDATE member_appearances SET scheme='retired',mode='unknown' WHERE account_id='${id}'`);
  assert.deepEqual((await call(ta,'/api/me/appearance')).body,{preference:{scheme:'standard',mode:'system'},revision:2});assert.equal(await sql(`SELECT scheme FROM member_appearances WHERE account_id='${id}'`),'retired');checks++;
  const fresh=await fetch(api+'/api/me/appearance',{headers:{Authorization:'Bearer '+ta}});assert.match(fresh.headers.get('cache-control')??'',/no-store/);assert.ok(fresh.headers.get('swoovr-version'));checks++;
  const page=await edge.browser.newPage();await page.goto(web+'/account/sign-in');await page.fill('#username',a.email);await page.fill('#password',a.password);await page.click('#kc-login');await page.waitForURL(web+'/account');
  assert.equal((await page.request.get(web+'/bff/api/me/appearance')).status(),400);checks++;
  const relay=await page.request.get(web+'/bff/api/me/appearance',{headers:{'X-Swoovr-Bff':'1'}});assert.equal(relay.status(),200);assert.equal((await relay.json()).revision,2);checks++;
  const update=await page.request.put(web+'/bff/api/me/appearance',{headers:{'X-Swoovr-Bff':'1','Swoovr-Appearance-Owner':owner(ta)},data:{preference:{scheme:'union',mode:'light'},expectedRevision:2,changeId:crypto.randomUUID()}});assert.equal(update.status(),200);assert.equal((await update.json()).revision,3);checks++;
  assert.equal((await page.request.put(web+'/bff/api/me/appearance',{headers:{'X-Swoovr-Bff':'1','Swoovr-Appearance-Owner':owner(tb)},data:first})).status(),409);checks++;
  assert.ok(!(await page.content()).includes(ta),'access token never enters browser markup');checks++;
  if(process.env.SWOOVR_SCREENSHOTS){await fs.mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true});await page.goto(web+'/privacy');await page.setViewportSize({width:390,height:900});await page.locator('[data-privacy-section=appearance]').screenshot({path:path.join(process.env.SWOOVR_SCREENSHOTS,'appearance-privacy-390.png')});}
  console.log(`PASS appearance own API/BFF: ${checks} checks (actual local auth/HTTP/PostgreSQL; client UI deferred).`);
 }finally{await edge.close();}
})().catch(error=>{console.error(error);process.exitCode=1});
