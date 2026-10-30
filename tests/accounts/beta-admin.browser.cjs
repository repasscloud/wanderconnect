// Actual owned API/Admin/Web; fictional adults. Staff-recorded iOS status is not Apple evidence.
const assert = require('node:assert/strict');
const {managedEdge}=require('../auth/managed-edge.cjs');
if(!process.env.SWOOVR_TEST_APP_DATABASE)throw Error('Use owned runner.');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, sql, createMember, createStaff, memberToken, staffToken, rootAdminToken, matchReady, call } = require('../auth/local-tokens.cjs');
const delay=ms=>new Promise(r=>setTimeout(r,ms));
async function listen(token){
 const base=api+'/hubs/messages',response=await fetch(base+'/negotiate?negotiateVersion=1',{method:'POST',headers:{Authorization:'Bearer '+token}});assert.equal(response.status,200);
 const {connectionToken}=await response.json(),socket=new WebSocket(base.replace(/^http/,'ws')+'?id='+encodeURIComponent(connectionToken)+'&access_token='+encodeURIComponent(token));
 const received=[];let handshake=false;socket.onmessage=async event=>{const value=typeof event.data==='string'?event.data:Buffer.from(await event.data.arrayBuffer()).toString();for(const frame of value.split('\u001e').filter(Boolean)){const item=JSON.parse(frame);if(!handshake&&!('type' in item)){handshake=true;continue;}if(item.type===1)received.push({target:item.target,payload:item.arguments[0]});}};
 await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});socket.send(JSON.stringify({protocol:'json',version:1})+'\u001e');
 for(let n=0;n<40&&!handshake;n++)await delay(50);assert.ok(handshake,'actual hub handshake');
 const ping=setInterval(()=>{if(socket.readyState===1)socket.send(JSON.stringify({type:6})+'\u001e');},10000);
 return {received,close:()=>{clearInterval(ping);socket.close();}};
}
(async () => {
  const edge=await managedEdge(chromium);const browser=edge.browser;
  let checks = 0, listener;
  try {
    const root = await rootAdminToken(browser);
    const operator = await createStaff('beta-ui-admin');
    const support = await createStaff('beta-ui-support');
    for (const [person, role] of [[operator, 'admin'], [support, 'support']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: person.email, role } })).status, 201);
    const admToken = await staffToken(browser, operator), supToken = await staffToken(browser, support);
    await call(admToken, '/api/session'); await call(supToken, '/api/session');
    const tester = await createMember('beta-ui-tester');
    assert.equal((await call(null, '/api/admin/beta')).status, 401); checks++;
    for (const method of ['GET', 'POST']) {
      const response = await call(supToken, '/api/admin/beta', { method, ...(method === 'POST' ? { body: { email: tester.email } } : {}) });
      assert.equal(response.status, 403); checks++;
    }
    assert.equal((await call(admToken, '/api/admin/beta', { method: 'POST', body: { email: 'invalid' } })).status, 400); checks++;
    const page = await browser.newPage();
    await page.goto(admin + '/sign-in');
    await page.fill('#username', operator.email); await page.fill('#password', operator.password); await page.click('#kc-login');
    await page.waitForURL(admin + '/**'); await page.goto(admin + '/beta');
    await page.waitForSelector('[data-beta-add]');
    await page.fill('[data-beta-add] input[name=email]', tester.email);
    await page.click('[data-beta-add] button');
    await page.waitForSelector('[data-beta-result="added"]');
    let row = page.locator('[data-beta-row]').filter({ hasText: tester.email });
    assert.equal(await row.count(), 1); checks++;
    assert.equal(await row.getAttribute('data-beta-state'), 'pending'); checks++;
    const id = await row.getAttribute('data-beta-row');
    assert.equal(await row.locator('[data-ios-record]').getAttribute('data-ios-record'),'not-recorded');checks++;
    const invalid=await call(admToken,`/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state:'verified',expectedRevision:0,confirm:true}});assert.equal(invalid.status,400);checks++;
    assert.equal((await call(supToken,`/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state:'invited',expectedRevision:0,confirm:true}})).status,403);checks++;
    const ios=row.locator('[data-beta-ios]');await ios.locator('select[name=iosState]').selectOption('invited');await ios.locator('input[name=confirmIos]').check();await ios.locator('button').click();
    await page.waitForSelector('[data-beta-result=ios-recorded]');row=page.locator(`[data-beta-row="${id}"]`);
    assert.equal(await row.locator('[data-ios-record]').getAttribute('data-ios-record'),'invited');checks++;
    assert.equal(await row.getAttribute('data-beta-state'),'pending','staff report never activates web beta');checks++;
    let listed=(await call(admToken,'/api/admin/beta')).body.find(e=>e.id===id);
    assert.equal(listed.iosDistributionSource,'staff-recorded-not-provider-verified');assert.equal(listed.iosDistributionRevision,1);checks++;
    const originalTime=listed.iosDistributionChangedAt;
    const same=await call(admToken,`/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state:'invited',expectedRevision:1,confirm:true}});
    assert.equal(same.status,200);assert.equal(same.body.iosDistributionChangedAt,originalTime);assert.equal(same.body.iosDistributionRevision,1);checks++;
    assert.equal((await call(admToken,`/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state:'accepted',expectedRevision:0,confirm:true}})).status,409);checks++;
    const raced=await Promise.all(['accepted','removed'].map(state=>call(admToken,`/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state,expectedRevision:1,confirm:true}})));
    assert.deepEqual(raced.map(r=>r.status).sort(),[200,409]);checks++;
    await page.reload();row=page.locator(`[data-beta-row="${id}"]`);assert.match(await row.innerText(),/not an Apple verification/);checks++;
    const forbidden=await page.request.post(admin+'/beta',{form:{_handler:'beta-add',email:'fictional.beta-forgery@example.test'}});assert.equal(forbidden.status(),400);checks++;
    assert.equal((await call(admToken,'/api/admin/beta')).body.some(e=>e.email==='fictional.beta-forgery@example.test'),false);checks++;
    const sensitive=await fetch(api+'/api/admin/beta',{headers:{Authorization:'Bearer '+admToken,'Swoovr-Version':'2026-10-28'}});assert.equal(sensitive.headers.get('cache-control'),'no-store');checks++;
    const folder=process.env.SWOOVR_SCREENSHOTS;if(folder){const fs=require('node:fs/promises'),path=require('node:path');await fs.mkdir(folder,{recursive:true});await page.setViewportSize({width:390,height:844});await page.locator('h1').evaluate(e=>e.scrollIntoView());await page.screenshot({path:path.join(folder,'beta-enrolments-390.png'),fullPage:true});}

    assert.equal((await call(supToken, '/api/admin/beta/' + id, { method: 'DELETE' })).status, 403); checks++;
    await page.fill('[data-beta-add] input[name=email]', tester.email.toUpperCase()); await page.click('[data-beta-add] button');
    await page.waitForSelector('[data-beta-result="added"]');
    assert.equal(await page.locator('[data-beta-row]').filter({ hasText: tester.email }).count(), 1); checks++;
    const member = await memberToken(browser, tester);
    await call(member, '/api/session');
    await page.reload(); row = page.locator(`[data-beta-row="${id}"]`);
    assert.equal(await row.getAttribute('data-beta-state'), 'activated'); checks++;
    const peer=await createMember('beta-ui-peer');assert.equal((await call(admToken,'/api/admin/beta',{method:'POST',body:{email:peer.email}})).status,201);const peerToken=await memberToken(browser,peer);
    let testerId;for(const [token,label]of[[member,'tester'],[peerToken,'peer']]){const profile=await call(token,'/api/me/profile',{method:'PUT',body:{displayName:'Fictional Beta '+label,homeCityId:2078025}});assert.equal(profile.status,200);if(label==='tester')testerId=profile.body.publicId;await matchReady(token);assert.equal((await call(token,'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true}})).status,200);}
    const requested=await call(peerToken,'/api/requests',{method:'POST',body:{to:testerId,mode:'friends'}});assert.equal(requested.status,201);assert.equal((await call(member,`/api/requests/${requested.body.id}/accept`,{method:'POST'})).status,200);
    const conversation=(await call(peerToken,'/api/conversations',{method:'POST',body:{to:testerId}})).body.id;listener=await listen(member);
    const before=await call(peerToken,`/api/conversations/${conversation}/messages`,{method:'POST',body:{text:'Fictional before beta revocation'}});assert.equal(before.status,201);
    for(let n=0;n<30&&!listener.received.some(e=>e.payload?.id===before.body.id);n++)await delay(50);assert.ok(listener.received.some(e=>e.payload?.id===before.body.id),'positive control receives actual live message');checks++;
    const testerAccount=(await sql(`SELECT id FROM accounts WHERE email='${tester.email}'`));const noticesBefore=await sql(`SELECT coalesce(sum(count),0) FROM notifications WHERE account_id='${testerAccount}' AND kind='Message'`);assert.ok(Number(noticesBefore)>0,'positive control creates message notification');checks++;
    await row.locator('input[name=confirmRevoke]').check(); await row.locator('[data-beta-revoke]').click();
    await page.waitForSelector('[data-beta-result="revoked"]');
    row = page.locator(`[data-beta-row="${id}"]`);
    assert.equal(await row.getAttribute('data-beta-state'), 'revoked'); checks++;
    assert.equal(await row.locator('[data-beta-revoke]').count(), 0); checks++;
    const after=await call(peerToken,`/api/conversations/${conversation}/messages`,{method:'POST',body:{text:'Fictional message while beta access removed'}});assert.equal(after.status,201);await delay(400);
    assert.equal(listener.received.some(e=>e.target==='message'&&e.payload?.id===after.body.id),false,'revoked beta listener receives no new message content');checks++;
    assert.equal(await sql(`SELECT coalesce(sum(count),0) FROM notifications WHERE account_id='${testerAccount}' AND kind='Message'`),noticesBefore,'revoked beta does not get new message notifications');checks++;

    assert.equal((await call(member, '/api/explore?mode=friends')).body.code, 'beta-not-enrolled'); checks++;
    assert.equal((await call(member, '/api/admin/beta')).status, 403); checks++;
    assert.equal((await call(member, '/api/me/deletion')).status,200,'revoked beta preserves account rights');checks++;
    assert.equal((await call(member, `/api/admin/beta/${id}/ios-distribution`,{method:'PUT',body:{state:'invited',expectedRevision:2,confirm:true}})).status,403);checks++;
    const memberPage=await browser.newPage();await memberPage.goto(web+'/account/sign-in?intro=done');await memberPage.fill('#username',tester.email);await memberPage.fill('#password',tester.password);await Promise.all([memberPage.waitForURL(web+'/account'),memberPage.click('#kc-login')]);
    assert.equal(await memberPage.locator('a[href="/account/data"]').count()>0,true,'revoked beta retains visible data link');checks++;await memberPage.goto(web+'/account/data');await memberPage.waitForSelector('[data-data-panel=none]');assert.equal(await memberPage.locator('[data-delete-form]').count(),1);checks++;
    await page.goto(admin + '/beta');
    assert.equal(await page.locator(`[data-beta-row="${id}"]`).getAttribute('data-beta-state'), 'revoked'); checks++;
    const deniedPage = await browser.newPage(); await deniedPage.goto(admin + '/beta');
    await deniedPage.fill('#username', support.email); await deniedPage.fill('#password', support.password); await deniedPage.click('#kc-login');
    await deniedPage.waitForURL(admin + '/**'); await deniedPage.goto(admin + '/beta');
    await deniedPage.waitForSelector('[data-beta-result]');
    assert.equal(await deniedPage.locator('[data-beta-add], [data-beta-row]').count(), 0); checks++;
    console.log(`PASS beta admin enrolment UI: ${checks} checks (fictional accounts; local API/browser only).`);
  } finally { listener?.close();await edge.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
