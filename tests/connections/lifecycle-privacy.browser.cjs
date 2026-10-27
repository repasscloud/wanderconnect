// Local acceptance for slice W: cross-mode privacy (#37, owner answer Q67, D175) and the per-mode connection lifecycle
// (#38, Q68, D176). A profile opened from one mode shows only that mode and the modes the pair is connected in, with
// each mode's own about; Explore cards use the mode's about. Disconnecting ends one mode without blocking: the other
// member isn't told, the conversation stays open while another mode connects them and otherwise closes read-only (with
// a live "closed" push), and only the member who was disconnected waits 30 days. Negative cases: strangers, pending and
// settled requests, unknown ids, blocked pairs, restricted members, tampered and oversized bodies. Then the web pages in
// Edge. Same local stack as tests/messaging/messaging.browser.cjs; every member is a fictional adult.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, createMember, memberToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const RS = '\u001e';

/** A minimal SignalR JSON-protocol listener (as in the messaging suite), with keep-alive pings. */
async function listen(token) {
  const base = api + '/hubs/messages';
  const negotiate = await fetch(`${base}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Authorization: 'Bearer ' + token } });
  const { connectionToken } = await negotiate.json();
  const socket = new WebSocket(base.replace(/^http/, 'ws') + `?id=${encodeURIComponent(connectionToken)}&access_token=${encodeURIComponent(token)}`);
  const received = [];
  let handshake = false;
  socket.onmessage = async e => {
    const text = typeof e.data === 'string' ? e.data : Buffer.from(await e.data.arrayBuffer()).toString();
    for (const frame of text.split(RS).filter(Boolean)) {
      const message = JSON.parse(frame);
      if (!handshake && !('type' in message)) { handshake = true; continue; }
      if (message.type === 1) received.push({ target: message.target, payload: message.arguments[0] });
    }
  };
  await new Promise(resolve => { socket.onopen = resolve; setTimeout(resolve, 3000); });
  socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS);
  const ping = setInterval(() => { if (socket.readyState === 1) socket.send(JSON.stringify({ type: 6 }) + RS); else clearInterval(ping); }, 10000);
  for (let i = 0; i < 30 && !handshake; i++) await sleep(100);
  return { received, close: () => { clearInterval(ping); socket.close(); } };
}
const waitFor = async (predicate, ms = 5000) => { for (let i = 0; i < ms / 100 && !predicate(); i++) await sleep(100); return predicate(); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const sockets = [];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat', 'res'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('life-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      pid[n] = (await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Life ' + n, about: 'Core about ' + n, homeCityId: ADELAIDE } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await matchReady(tokens[n]);
      for (const mode of n === 'cat' ? ['friends'] : ['dating', 'friends', 'business'])
        assert.equal((await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } })).status, 200);
    }
    // Ana writes an about for Business only (D175).
    let r = await call(tokens.ana, '/api/v1/me/modes/business', { method: 'PUT', body: { enabled: true, visible: true, about: 'Fictional consultant, coffee meetings welcome' } });
    assert.equal(r.status, 200); assert.equal(r.body.modes.find(m => m.id === 'business').about, 'Fictional consultant, coffee meetings welcome'); pass();
    assert.equal(r.body.modes.find(m => m.id === 'dating').about, null, 'other modes keep the core about'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, about: 'x'.repeat(501) } })), [400, 'invalid-profile']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, about: 'bad\u0007bell' } })), [400, 'invalid-profile']); pass();
    const raw = await fetch(api + '/api/v1/me/modes/friends', { method: 'PUT', headers: { Authorization: 'Bearer ' + tokens.ana, 'Content-Type': 'application/json' }, body: '{"enabled":true,"about":' });
    assert.equal(raw.status, 400, 'a tampered body'); pass();

    // A profile seen from one mode (D175).
    const view = (who, target, mode) => call(tokens[who], `/api/v1/profiles/${pid[target]}` + (mode ? `?mode=${mode}` : ''));
    r = await view('bob', 'ana', 'business');
    assert.deepEqual(r.body.modes.map(m => m.id), ['business'], 'opened from Business: only Business'); pass();
    assert.equal(r.body.about, 'Fictional consultant, coffee meetings welcome'); pass();
    assert.equal(JSON.stringify(r.body).includes('dating'), false, 'nothing about Dating reaches a Business view'); pass();
    r = await view('bob', 'ana', 'dating');
    assert.deepEqual([r.body.modes.map(m => m.id), r.body.about], [['dating'], 'Core about ana']); pass();
    assert.equal(JSON.stringify(r.body).includes('consultant'), false, 'the Business about stays in Business'); pass();
    assert.equal((await view('bob', 'ana', 'activities')).status, 404, 'a mode they do not share reads as not found'); pass();
    assert.equal((await view('bob', 'ana', 'made-up')).status, 404); pass();
    assert.equal((await view('cat', 'ana', 'business')).status, 404, 'cat is not in Business'); pass();
    assert.deepEqual((await view('cat', 'ana', 'friends')).body.modes.map(m => m.id), ['friends']); pass();
    assert.deepEqual((await view('bob', 'ana')).body.modes.map(m => m.id), ['dating'], 'no mode given and not connected: the first shared mode only'); pass();
    const cards = mode => call(tokens.bob, '/api/v1/explore?mode=' + mode);
    assert.equal((await cards('business')).body.people.find(p => p.id === pid.ana).about, 'Fictional consultant, coffee meetings welcome'); pass();
    assert.equal((await cards('friends')).body.people.find(p => p.id === pid.ana).about, 'Core about ana', 'Explore uses the mode about'); pass();

    // Connected in Friends and Business; the Business view now also shows Friends.
    const connect = async (from, to, mode) => {
      const sent = await call(tokens[from], '/api/v1/requests', { method: 'POST', body: { to: pid[to], mode } });
      assert.equal(sent.status, 201, `${from}->${to} ${mode}`);
      assert.equal((await call(tokens[to], `/api/v1/requests/${sent.body.id}/accept`, { method: 'POST' })).status, 200);
      return sent.body.id;
    };
    const friendsLink = await connect('ana', 'bob', 'friends');
    const businessLink = await connect('bob', 'ana', 'business');
    r = await view('bob', 'ana', 'business');
    assert.deepEqual(r.body.modes.map(m => [m.id, m.connection]), [['friends', 'connected'], ['business', 'connected']]); pass();
    assert.equal(r.body.modes.find(m => m.id === 'friends').connectionId, friendsLink); pass();
    assert.equal(r.body.modes.some(m => m.id === 'dating'), false, 'still never Dating'); pass();
    const convo = (await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: pid.bob } })).body.id;
    assert.equal((await call(tokens.ana, `/api/v1/conversations/${convo}/messages`, { method: 'POST', body: { text: 'Fictional hello' } })).status, 201);

    // Negative cases for disconnect (D176).
    const disconnect = (who, id) => call(tokens[who], `/api/v1/requests/${id}/disconnect`, { method: 'POST' });
    assert.deepEqual(code(await disconnect('cat', friendsLink)), [404, 'not-available'], 'a stranger'); pass();
    assert.deepEqual(code(await disconnect('ana', '00000000-0000-4000-8000-000000000000')), [404, 'not-available'], 'unknown id'); pass();
    const pendingId = (await call(tokens.cat, '/api/v1/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })).body.id;
    assert.deepEqual(code(await disconnect('ana', pendingId)), [409, 'not-connected'], 'pending is declined, not disconnected'); pass();
    assert.equal((await call(tokens.ana, `/api/v1/requests/${pendingId}/decline`, { method: 'POST' })).status, 200);
    assert.deepEqual(code(await disconnect('ana', pendingId)), [409, 'not-connected'], 'a settled request'); pass();
    assert.equal((await call(null, `/api/v1/requests/${friendsLink}/disconnect`, { method: 'POST' })).status, 401); pass();

    // Ana disconnects Friends: Business still connects them, so the conversation stays open.
    const anaHub = await listen(tokens.ana), bobHub = await listen(tokens.bob);
    sockets.push(anaHub, bobHub);
    r = await disconnect('ana', friendsLink);
    assert.deepEqual([r.status, r.body.status], [200, 'disconnected']); pass();
    assert.equal(await sql(`SELECT status || '|' || ended_by FROM connection_requests WHERE id = '${friendsLink}'`), `Disconnected|${ids.ana}`); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'request.disconnected' AND target_id = '${friendsLink}'`), 'friends', 'the audit holds the mode only'); pass();
    assert.deepEqual(code(await disconnect('ana', friendsLink)), [409, 'not-connected'], 'twice'); pass();
    assert.equal((await call(tokens.bob, '/api/v1/conversations')).body.find(c => c.id === convo).canSend, true, 'Business still connects them'); pass();
    r = await view('bob', 'ana', 'business');
    assert.deepEqual(r.body.modes.map(m => [m.id, m.connection]), [['business', 'connected']], 'Friends left the view: not connected and not opened from it'); pass();
    assert.equal((await call(tokens.bob, '/api/v1/connections')).body.items.some(i => i.id === friendsLink), false, 'gone from Connections for Bob'); pass();
    assert.equal((await call(tokens.ana, '/api/v1/connections')).body.items.some(i => i.id === friendsLink), false, 'and for Ana'); pass();
    await sleep(500);
    assert.equal(bobHub.received.some(m => m.target === 'closed'), false, 'Bob is not told'); pass();
    // Only the member who was disconnected waits 30 days in that mode.
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })), [409, 'recently-disconnected']); pass();
    const again = await call(tokens.ana, '/api/v1/requests', { method: 'POST', body: { to: pid.bob, mode: 'friends' } });
    assert.equal(again.status, 201, 'the one who disconnected may ask again'); pass();
    assert.equal((await call(tokens.bob, `/api/v1/requests/${again.body.id}/decline`, { method: 'POST' })).status, 200);

    // Bob disconnects Business: no mode connects them, so the conversation closes read-only, pushed live to both.
    r = await disconnect('bob', businessLink);
    assert.equal(r.status, 200); pass();
    assert.ok(await waitFor(() => anaHub.received.some(m => m.target === 'closed' && m.payload.conversationId === convo)), 'closed pushed to Ana'); pass();
    assert.ok(await waitFor(() => bobHub.received.some(m => m.target === 'closed' && m.payload.conversationId === convo)), 'and to Bob'); pass();
    assert.equal(await sql(`SELECT closed_reason FROM conversations WHERE id = '${convo}'`), 'disconnected'); pass();
    for (const who of ['ana', 'bob']) {
      const listed = (await call(tokens[who], '/api/v1/conversations')).body.find(c => c.id === convo);
      assert.equal(listed?.canSend, false, who + ' still lists it, read-only'); pass();
      assert.deepEqual((await call(tokens[who], `/api/v1/conversations/${convo}/messages`)).body.items.map(m => m.text), ['Fictional hello']); pass();
      assert.equal((await call(tokens[who], `/api/v1/conversations/${convo}/messages`, { method: 'POST', body: { text: 'More?' } })).status, 404); pass();
    }
    assert.equal((await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: pid.bob } })).status, 404, 'no new chat without a connection'); pass();
    // Ana was disconnected from Business, so she waits there; Bob may ask again.
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/requests', { method: 'POST', body: { to: pid.bob, mode: 'business' } })), [409, 'recently-disconnected']); pass();

    // A block still hides the history for good (D143).
    const catLink = await connect('ana', 'cat', 'friends'); // Ana declined Cat above, so Cat waits 30 days; Ana asks
    assert.equal((await call(tokens.ana, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.cat } })).status, 204);
    assert.deepEqual(code(await disconnect('cat', catLink)), [404, 'not-available'], 'a blocked pair'); pass();
    assert.equal((await call(tokens.ana, `/api/v1/blocks/${pid.cat}`, { method: 'DELETE' })).status, 204);

    // Restricted members can't use it (D160).
    const resLink = await connect('res', 'bob', 'friends');
    assert.ok([200, 201].includes((await call(root, `/api/v1/admin/accounts/${ids.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional', days: 3 } })).status));
    assert.deepEqual(code(await disconnect('res', resLink)), [403, 'account-restricted']); pass();

    // The web pages in Edge: the person page from one mode, the mode about editor, and disconnect on Connections.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.bob.email); await page.fill('#password', members.bob.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    const bff = path => page.request.fetch(web + '/bff/' + path, { method: 'POST', headers: { 'X-Swoovr-Bff': '1' } });
    assert.equal((await bff(`api/v1/requests/${resLink}/disconnect`)).status(), 200, 'disconnect is on the BFF allow-list'); pass();
    await page.goto(web + `/people/${pid.ana}?mode=business`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-shared-modes]').getAttribute('data-shared-modes'), 'business'); pass();
    assert.match(await page.locator('.person-about').first().innerText(), /consultant/); pass();
    await page.goto(web + `/people/${pid.ana}?mode=dating`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-shared-modes]').getAttribute('data-shared-modes'), 'dating'); pass();
    assert.equal((await page.content()).includes('consultant'), false); pass();
    const link = await connect('ana', 'bob', 'dating');
    await page.goto(web + '/connections');
    await page.waitForSelector(`[data-request="${link}"] [data-connection-label]`, { timeout: 20000 });
    assert.equal(await page.locator(`[data-request="${link}"] [data-connection-label]`).innerText(), 'Match'); pass();
    await page.click(`[data-request="${link}"] [data-disconnect]`);
    await page.click(`[data-request="${link}"] [data-disconnect-confirm]`);
    await page.waitForSelector('[data-connections-status=disconnected]'); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE id = '${link}'`), 'Disconnected'); pass();
    await page.goto(web + '/me/profile');
    await page.waitForSelector('[data-mode-about=friends] textarea', { timeout: 20000 });
    await page.fill('[data-mode-about=friends] textarea', 'Fictional Friends-only about');
    await page.locator('[data-mode-about=friends] textarea').blur();
    for (let i = 0; i < 50 && await sql(`SELECT coalesce(about, '') FROM member_modes WHERE account_id = '${ids.bob}' AND mode_id = 'friends'`) !== 'Fictional Friends-only about'; i++) await sleep(100);
    assert.equal(await sql(`SELECT about FROM member_modes WHERE account_id = '${ids.bob}' AND mode_id = 'friends'`), 'Fictional Friends-only about'); pass();
    if (process.env.SWOOVR_SCREENSHOTS) await page.screenshot({ path: process.env.SWOOVR_SCREENSHOTS + '/profile-mode-about-390.png', fullPage: true });
    await context.close();

    console.log(`PASS: cross-mode privacy and connection lifecycle (${passed} checks).`);
  } finally {
    for (const s of sockets) s.close();
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
