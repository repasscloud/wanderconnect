// Owned-stack browser evidence. Synthetic response projection checks are deliberately labelled;
// existing matching/market/travel suites remain the real API eligibility/filter evidence.
const assert = require('node:assert/strict');
const { managedEdge } = require('../auth/managed-edge.cjs');
if (!process.env.SWOOVR_TEST_APP_DATABASE) throw Error('Use owned runner.');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, call, matchReady } = require('../auth/local-tokens.cjs');
(async () => {
  const edge = await managedEdge(chromium); const browser = edge.browser;
  let checks = 0;
  try {
    const viewer = await createMember('pool-guidance'); await cli('beta', 'add', viewer.email);
    const token = await memberToken(browser, viewer);
    assert.equal((await call(token, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Guidance', homeCityId: 2078025 } })).status, 200);
    await matchReady(token);
    await call(token, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const peer = await createMember('pool-guidance-peer'); await cli('beta', 'add', peer.email); const peerToken = await memberToken(browser, peer);
    assert.equal((await call(peerToken, '/api/me/profile', { method:'PUT', body:{displayName:'Fictional Guidance Peer',homeCityId:2078025} })).status,200); await matchReady(peerToken);
    assert.equal((await call(peerToken,'/api/me/modes/friends',{method:'PUT',body:{enabled:true,visible:true}})).status,200);
    const page = await browser.newPage(); await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', viewer.email); await page.fill('#password', viewer.password); await page.click('#kc-login');
    await page.waitForURL(web + '/account**');
    let state = 'empty', intercepted = 0; const methods = [];
    page.on('request', request => { if (request.url().includes('/bff/')) methods.push(request.method()); });
    await page.route('**/bff/api/explore?**', async route => {
      intercepted++;
      const response = await route.fetch(); // Preserve contract headers from the running local stack.
      if (state === 'transport') return route.abort('failed');
      if (state === 'failure' || state === 'limited') return route.fulfill({ response, status: state === 'limited' ? 429 : 503, contentType: 'application/problem+json', body: JSON.stringify({ code: 'service-unavailable' }) });
      assert.equal(response.status(), 200, 'Fixture must be eligible before synthetic result projection');
      const body = await response.json(); if (state === 'short') { assert.ok(body.people.length > 0); body.people = body.people.slice(0,1); } else body.people = [];
      return route.fulfill({ response, body: JSON.stringify(body) });
    });
    await page.goto(web + '/explore'); await page.waitForSelector('[data-explore-empty]');
    assert.ok(intercepted > 0, 'Synthetic Explore interception must run'); checks++;
    assert.equal(await page.textContent('[data-explore-empty]'), 'No profiles match this view right now.'); checks++;
    assert.equal(await page.getAttribute('[data-discovery-filters]', 'href'), '/me/matching'); checks++;
    assert.equal(await page.getAttribute('[data-discovery-travel]', 'href'), '/travel'); checks++;
    assert.match(await page.textContent('[data-discovery-guidance]'), /both members' preferences applied/); checks++;
    assert.equal(methods.filter(method => method !== 'GET').length, 0, 'Guidance must not write filters/modes/travel'); checks++;
    state = 'short'; await page.reload(); await page.waitForSelector('.people-grid'); assert.equal(await page.locator('[data-discovery-guidance]').count(),1); checks++;
    assert.equal(await page.locator('[data-explore-empty]').count(),0); checks++;
    await page.setViewportSize({width:390,height:844});
    if(process.env.SWOOVR_SCREENSHOTS) { const fs=require('node:fs/promises'); await fs.mkdir(process.env.SWOOVR_SCREENSHOTS,{recursive:true}); await page.screenshot({path:require('node:path').join(process.env.SWOOVR_SCREENSHOTS,'discovery-guidance.png'),fullPage:true}); }
    const previousInterceptions = intercepted;
    state = 'failure'; await page.reload(); await page.waitForSelector('[data-explore-unavailable]');
    assert.equal(await page.locator('[data-explore-empty], [data-discovery-guidance]').count(), 0); checks++;
    assert.ok(intercepted > previousInterceptions); checks++;
    for (const failure of ['limited', 'transport']) { state = failure; await page.reload(); await page.waitForSelector('[data-explore-unavailable]'); assert.equal(await page.locator('[data-explore-empty], [data-discovery-guidance]').count(), 0); checks++; }
    console.log(`PASS discovery guidance: ${checks} synthetic projection checks; no activity/density/provider claims.`);
  } finally { await edge.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
