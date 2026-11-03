// Q118: real local tokens and owned API/database only. Fictional adults; no screenshots or external delivery.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, memberToken, cli, call, matchReady, sql, claims, web } = require('../auth/local-tokens.cjs');
if (!process.env.SWOOVR_TEST_API_BASE || !process.env.SWOOVR_TEST_APP_DATABASE) throw Error('Mode names acceptance requires the owned runner');
(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium);
  try {
    const [owner, viewer] = await Promise.all(['name-owner', 'name-viewer'].map(createMember));
    await cli('beta', 'add', owner.email, viewer.email);
    const a = await memberToken(edge.browser, owner), b = await memberToken(edge.browser, viewer);
    const modes = ['dating', 'friends', 'activities', 'business'];
    for (const [token, name] of [[a, 'Public Main'], [b, 'Public Viewer']]) {
      assert.equal((await call(token, '/api/me/profile', { method: 'PUT', body: { displayName: name, homeCityId: 2078025 } })).status, 200);
      await matchReady(token);
      for (const mode of modes) assert.equal((await call(token, '/api/me/modes/' + mode, { method: 'PUT', body: { enabled: true, visible: true } })).status, 200);
    }
    const id = (await call(a, '/api/me/profile')).body.publicId;
    const save = (mode, displayName) => call(a, '/api/me/modes/' + mode, { method: 'PUT', body: { enabled: true, visible: true, displayName } });
    for (const mode of modes) assert.equal((await save(mode, 'Alias ' + mode)).status, 200);
    for (const mode of modes) {
      const view = await call(b, `/api/profiles/${id}?mode=${mode}`);
      assert.equal(view.status, 200); assert.equal(view.body.displayName, 'Alias ' + mode);
      assert.deepEqual(view.body.modes.map(m => m.id), [mode]);
      for (const other of modes.filter(m => m !== mode)) assert.ok(!JSON.stringify(view.body).includes('Alias ' + other));
      assert.equal((await call(b, '/api/explore?mode=' + mode)).body.people.find(p => p.id === id).displayName, 'Alias ' + mode);
    }
    assert.equal((await save('dating', 'x'.repeat(41))).status, 400);
    assert.equal((await save('dating', 'Bad\nName')).status, 400);
    assert.equal((await call(a, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } })).body.modes.find(m => m.id === 'friends').displayName, 'Alias friends', 'old client omission retains alias');
    const before = await sql(`SELECT content_json FROM profile_versions WHERE account_id = (SELECT id FROM accounts WHERE subject = '${claims(a).sub}') AND mode_id = 'dating' ORDER BY captured_at DESC LIMIT 1`);
    assert.ok(before.includes('Alias dating'));
    await save('dating', '  ');
    assert.equal((await call(b, `/api/profiles/${id}?mode=dating`)).body.displayName, 'Public Main');
    assert.equal((await call(b, `/api/profiles/${id}?mode=friends`)).body.displayName, 'Alias friends');
    assert.ok((await sql(`SELECT count(*) FROM profile_versions WHERE account_id = (SELECT id FROM accounts WHERE subject = '${claims(a).sub}') AND content_json::text LIKE '%Alias dating%'`)) !== '0', 'historical versions retained');
    const request = await call(b, '/api/requests', { method: 'POST', body: { to: id, mode: 'friends' } });
    assert.equal(request.status, 201);
    assert.equal((await call(a, `/api/requests/${request.body.id}/accept`, { method: 'POST' })).status, 200);
    const chat = await call(b, '/api/conversations', { method: 'POST', body: { to: id, mode: 'friends' } });
    assert.equal(chat.status, 201); assert.equal(chat.body.person.displayName, 'Alias friends');
    await save('business', 'Business changed');
    assert.equal((await call(b, `/api/conversations/${chat.body.id}`)).body.person.displayName, 'Alias friends');
    await save('friends', 'Friend changed');
    assert.equal((await call(b, `/api/conversations/${chat.body.id}`)).body.person.displayName, 'Friend changed');
    // The actual WASM editor uses the same optional contract, through the authenticated BFF.
    const context = await edge.browser.newContext();
    try {
      const page = await context.newPage(); await page.goto(web + '/account/sign-in');
      await page.fill('#username', owner.email); await page.fill('#password', owner.password);
      await Promise.all([page.waitForURL(web + '/explore'), page.click('#kc-login')]);
      await page.goto(web + '/me/profile'); await page.waitForSelector('[data-profile-editor=ready]');
      await page.fill('#name-activities', 'Activity UI'); await page.locator('#name-activities').blur();
      await page.waitForFunction(() => document.querySelector('[data-mode-status]')?.textContent.includes('saved'));
      assert.equal((await call(a, '/api/me/profile')).body.modes.find(m => m.id === 'activities').displayName, 'Activity UI');
    } finally { await context.close(); }
    console.log('PASS: Q118 mode names, all-four privacy/fallback/validation, retained versions, fixed-origin chat, actual editor/BFF');
  } finally { await edge.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
