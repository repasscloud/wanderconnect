// Local acceptance for connection requests and blocks (D112, D117–D119): per-mode send/withdraw/accept/decline, at
// most one pending request per pair and mode, nothing connects on its own, bots never initiate, an audited rate limit,
// and blocks that hide both people from each other everywhere and read as 404, never "blocked". Then the Connections
// page and blocking from a profile in actual Edge. Same local stack as tests/profiles/modes.browser.cjs plus the
// enrolled fictional root admin. Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const names = ['ann', 'bob', 'cat', 'dan', 'eve', 'fin', 'gil', 'hal'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('requests-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, pid = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      // Everyone lives in Adelaide: Explore, profiles and requests are local to one market (D125).
      await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), homeCityId: 2078025 } });
      await matchReady(tokens[n]);
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = (await call(tokens[n], '/api/v1/me/profile')).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const send = (from, to, mode, extra = {}) => call(tokens[from], '/api/v1/requests', { method: 'POST', body: { to: pid[to], mode, ...extra } });
    const answer = (who, id, what) => call(tokens[who], `/api/v1/requests/${id}/${what}`, { method: 'POST' });
    const box = async (who, which) => (await call(tokens[who], `/api/v1/requests?box=${which}`)).body.items;
    const connections = async who => (await call(tokens[who], '/api/v1/connections')).body.items;
    const explore = async (who, mode = 'dating') => (await call(tokens[who], '/api/v1/explore?mode=' + mode)).body.people.map(p => p.id);
    const code = r => [r.status, r.body?.code];

    // Send: pending, in both boxes, nothing connected yet (Q17 rule 31).
    let r = await send('ann', 'bob', 'dating');
    assert.equal(r.status, 201); assert.deepEqual([r.body.mode, r.body.direction, r.body.status, r.body.person.id], ['dating', 'sent', 'pending', pid.bob]); pass();
    const annBob = r.body.id;
    assert.deepEqual(Object.keys(r.body.person).sort(), ['badges', 'displayName', 'id']); pass(); // no account id or email
    assert.ok((await box('bob', 'received')).some(x => x.id === annBob && x.direction === 'received' && x.person.id === pid.ann)); pass();
    assert.ok((await box('ann', 'sent')).some(x => x.id === annBob)); pass();
    assert.deepEqual(await connections('ann'), []); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'request.sent' AND target_id = '${annBob}'`), 'dating'); pass();
    let person = (await call(tokens.ann, `/api/v1/profiles/${pid.bob}`)).body;
    assert.deepEqual(person.modes.map(m => [m.id, m.connection, m.requestId]), [['dating', 'sent', annBob], ['friends', 'none', null]]); pass();
    assert.deepEqual((await call(tokens.bob, `/api/v1/profiles/${pid.ann}`)).body.modes.find(m => m.id === 'dating').connection, 'received'); pass();

    // One pending per pair and mode, whichever way; another mode is separate.
    assert.deepEqual(code(await send('ann', 'bob', 'dating')), [409, 'request-pending']); pass();
    assert.deepEqual(code(await send('bob', 'ann', 'dating')), [409, 'request-pending']); pass();
    r = await send('ann', 'bob', 'friends'); assert.equal(r.status, 201); pass();
    const annBobFriends = r.body.id;
    // Concurrent sends: the database keeps one.
    const race = await Promise.all([send('cat', 'eve', 'dating'), send('eve', 'cat', 'dating'), send('cat', 'eve', 'dating')]);
    assert.equal(race.filter(x => x.status === 201).length, 1); pass();
    assert.ok(race.filter(x => x.status !== 201).every(x => x.status === 409)); pass();
    const winner = race.findIndex(x => x.status === 201);
    await answer(['cat', 'eve', 'cat'][winner], race[winner].body.id, 'withdraw');

    // Only the recipient answers and only the sender withdraws; anyone else's ids are 404.
    for (const [who, what] of [['cat', 'accept'], ['cat', 'decline'], ['cat', 'withdraw'], ['ann', 'accept'], ['ann', 'decline'], ['bob', 'withdraw']]) {
      assert.deepEqual(code(await answer(who, annBob, what)), [404, 'not-available'], `${who} ${what}`); pass();
    }
    assert.equal((await answer('ann', '00000000-0000-0000-0000-000000000000', 'withdraw')).status, 404); pass();
    assert.equal((await call(tokens.ann, `/api/v1/requests/${annBob}/delete`, { method: 'POST' })).status, 404); pass(); // no such answer

    // Accept connects; asking again is refused; a settled request can't be answered again.
    r = await answer('bob', annBob, 'accept');
    assert.deepEqual([r.status, r.body.status, r.body.direction], [200, 'accepted', 'received']); pass();
    assert.ok((await connections('ann')).some(x => x.id === annBob) && (await connections('bob')).some(x => x.id === annBob)); pass();
    assert.deepEqual(code(await send('ann', 'bob', 'dating')), [409, 'already-connected']); pass();
    assert.deepEqual(code(await send('bob', 'ann', 'dating')), [409, 'already-connected']); pass();
    assert.deepEqual(code(await answer('bob', annBob, 'decline')), [409, 'request-settled']); pass();
    assert.equal((await call(tokens.ann, `/api/v1/profiles/${pid.bob}`)).body.modes.find(m => m.id === 'dating').connection, 'connected'); pass();
    // Decline: no connection, and the sender waits before asking again in that mode (D117).
    r = await answer('bob', annBobFriends, 'decline'); assert.equal(r.body.status, 'declined'); pass();
    assert.deepEqual(code(await send('ann', 'bob', 'friends')), [409, 'recently-declined']); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id IN ('${annBob}', '${annBobFriends}') AND action IN ('request.accepted', 'request.declined')`), '2'); pass();
    // Withdraw, then the same pair may ask again in that mode.
    r = await send('ann', 'cat', 'dating'); const annCat = r.body.id;
    assert.equal((await answer('ann', annCat, 'withdraw')).body.status, 'withdrawn'); pass();
    assert.ok(!(await box('cat', 'received')).some(x => x.id === annCat)); pass();

    // Modes: both must have it on and the person must show themselves in it; unknown modes and ids are refused.
    await call(tokens.ann, '/api/v1/me/modes/business', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.deepEqual(code(await send('ann', 'cat', 'business')), [404, 'not-available']); pass(); // cat has business off
    assert.deepEqual(code(await send('cat', 'ann', 'business')), [404, 'not-available']); pass(); // the sender has it off
    await call(tokens.cat, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: false } });
    assert.deepEqual(code(await send('ann', 'cat', 'friends')), [404, 'not-available']); pass(); // hidden
    await call(tokens.cat, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.deepEqual(code(await send('ann', 'cat', 'travel')), [404, 'unknown-mode']); pass();
    assert.deepEqual(code(await call(tokens.ann, '/api/v1/requests', { method: 'POST', body: { to: pid.ann, mode: 'dating' } })), [404, 'not-available']); pass(); // self
    assert.deepEqual(code(await call(tokens.ann, '/api/v1/requests', { method: 'POST', body: { to: '00000000-0000-0000-0000-000000000000', mode: 'dating' } })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ann, '/api/v1/requests', { method: 'POST', body: { to: 'not-a-uuid', mode: 'dating' } })).status, 400); pass();
    // A tampered body can't choose the sender, the status or the account behind the public id.
    r = await send('ann', 'cat', 'friends', { from: pid.bob, fromAccountId: ids.bob, status: 'accepted', toAccountId: ids.dan });
    assert.deepEqual([r.status, r.body.status, r.body.direction, r.body.person.id], [201, 'pending', 'sent', pid.cat]); pass();
    assert.equal(await sql(`SELECT from_account_id || ':' || to_account_id || ':' || status FROM connection_requests WHERE id = '${r.body.id}'`), `${ids.ann}:${ids.cat}:Pending`); pass();
    assert.equal((await call(tokens.ann, '/api/v1/requests?box=everything')).status, 400); pass();
    assert.equal((await call(tokens.ann, '/api/v1/requests?box=sent&page=0')).status, 400); pass();

    // Bots never initiate (Q11 rules 50–52) but may answer a member's request (rule 51).
    const root = await rootAdminToken(browser);
    await call(root, `/api/v1/admin/accounts/${ids.dan}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/v1/admin/accounts/${ids.dan}/bot-modes`, { method: 'PUT', body: { modes: ['dating'] } });
    await call(root, `/api/v1/admin/accounts/${ids.dan}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } }); // D125 rule 38
    assert.deepEqual(code(await send('dan', 'ann', 'dating')), [403, 'bot-cannot-initiate']); pass();
    assert.deepEqual(code(await send('dan', 'ann', 'friends')), [403, 'bot-cannot-initiate']); pass();
    r = await send('ann', 'dan', 'dating'); assert.equal(r.status, 201); pass();
    assert.equal((await answer('dan', r.body.id, 'accept')).body.status, 'accepted'); pass();
    assert.deepEqual(code(await send('ann', 'dan', 'friends')), [404, 'not-available']); pass(); // not authorised there

    // Blocks: both ways, everywhere, pending requests cancelled, always 404.
    const finEve = (await send('fin', 'eve', 'dating')).body.id;
    const eveFin = (await send('eve', 'fin', 'friends')).body.id;
    assert.ok((await explore('fin')).includes(pid.eve) && (await explore('eve')).includes(pid.fin)); pass();
    assert.equal((await call(tokens.eve, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.fin } })).status, 204); pass();
    assert.equal((await call(tokens.eve, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.fin } })).status, 204); pass(); // idempotent
    assert.equal(await sql(`SELECT string_agg(status, ',' ORDER BY status) FROM connection_requests WHERE id IN ('${finEve}', '${eveFin}')`), 'Cancelled,Cancelled'); pass();
    assert.ok(!(await explore('fin')).includes(pid.eve) && !(await explore('eve')).includes(pid.fin)); pass();
    assert.ok(!(await explore('fin', 'friends')).includes(pid.eve)); pass();
    for (const [who, other] of [['fin', 'eve'], ['eve', 'fin']]) {
      const profile = await call(tokens[who], `/api/v1/profiles/${pid[other]}`);
      assert.equal(profile.status, 404); assert.ok(!JSON.stringify(profile.body).toLowerCase().includes('block')); pass();
      const request = await send(who, other, 'activities');
      assert.deepEqual(code(request), [404, 'not-available']); assert.ok(!JSON.stringify(request.body).toLowerCase().includes('block')); pass();
      assert.deepEqual(code(await send(who, other, 'dating')), [404, 'not-available']); pass();
    }
    for (const [who, id, what] of [['eve', finEve, 'accept'], ['fin', finEve, 'withdraw'], ['fin', eveFin, 'decline']]) {
      assert.deepEqual(code(await answer(who, id, what)), [404, 'not-available']); pass();
    }
    for (const who of ['eve', 'fin']) for (const b of ['received', 'sent'])
      assert.ok(!(await box(who, b)).some(x => [finEve, eveFin].includes(x.id))); pass();
    assert.deepEqual((await call(tokens.eve, '/api/v1/blocks')).body.map(b => b.id), [pid.fin]); pass();
    assert.deepEqual((await call(tokens.fin, '/api/v1/blocks')).body, []); pass(); // fin is never told
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'block.added' AND actor_account_id = '${ids.eve}' AND target_id = '${ids.fin}' AND detail IS NULL`), '1'); pass();
    // Blocking back is allowed; one side unblocking keeps them apart while the other block stands.
    assert.equal((await call(tokens.fin, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.eve } })).status, 204); pass();
    assert.equal((await call(tokens.eve, `/api/v1/blocks/${pid.fin}`, { method: 'DELETE' })).status, 204); pass();
    assert.ok(!(await explore('eve')).includes(pid.fin)); pass();
    assert.equal((await call(tokens.fin, `/api/v1/blocks/${pid.eve}`, { method: 'DELETE' })).status, 204); pass();
    assert.ok((await explore('eve')).includes(pid.fin) && (await explore('fin')).includes(pid.eve)); pass();
    assert.equal((await call(tokens.fin, `/api/v1/blocks/${pid.eve}`, { method: 'DELETE' })).status, 404); pass(); // nothing to remove
    assert.equal((await call(tokens.fin, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.fin } })).status, 404); pass(); // self
    // A block ends an existing connection for both (Q42 rule 14, D143, since slice U); unblocking doesn't bring it back,
    // but the pair may connect again with a new request.
    await call(tokens.ann, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.bob } });
    assert.ok(!(await connections('ann')).some(x => x.id === annBob) && !(await connections('bob')).some(x => x.id === annBob)); pass();
    await call(tokens.ann, `/api/v1/blocks/${pid.bob}`, { method: 'DELETE' });
    assert.ok(!(await connections('bob')).some(x => x.id === annBob) && !(await connections('ann')).some(x => x.id === annBob)); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE id = '${annBob}'`), 'Ended'); pass();
    r = await call(tokens.ann, '/api/v1/requests', { method: 'POST', body: { to: pid.bob, mode: 'dating' } });
    assert.equal(r.status, 201); pass();
    assert.equal((await answer('bob', r.body.id, 'accept')).status, 200); pass();

    // Rate limit: 20 sends a day per sender, then 429, audited (D117).
    for (let i = 0; i < 20; i++) {
      r = await send('gil', 'hal', i % 2 ? 'friends' : 'dating');
      assert.equal(r.status, 201, `send ${i + 1}`);
      if (i < 19) await answer('gil', r.body.id, 'withdraw');
    }
    pass();
    assert.deepEqual(code(await send('gil', 'cat', 'dating')), [429, 'too-many-requests']); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'request.rate-limited' AND actor_account_id = '${ids.gil}'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'request.%' AND (detail LIKE '%@%' OR detail LIKE '%Fictional%')`), '0'); pass();

    // Through the backend-for-frontend: the new routes are allowed, others and bodies past 16 KB are not.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const signIn = async (p, member) => {
      await p.goto(web + '/account/sign-in?intro=done');
      await p.fill('#username', member.email); await p.fill('#password', member.password);
      await Promise.all([p.waitForURL(web + '/account'), p.click('#kc-login')]);
    };
    await signIn(page, members.ann);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/v1/requests?box=received')).status(), 200); pass();
    assert.equal((await bff('api/v1/connections')).status(), 200); pass();
    assert.equal((await bff('api/v1/blocks')).status(), 200); pass();
    assert.equal((await bff(`api/v1/requests/${annBob}`)).status(), 404); pass();
    assert.equal((await bff(`api/v1/requests/${annBob}/delete`, { method: 'POST' })).status(), 404); pass();
    assert.equal((await bff('api/v1/requests', { method: 'POST', data: { to: pid.hal, mode: 'dating', pad: 'x'.repeat(17 * 1024) } })).status(), 413); pass();

    // Web: send from a profile, answer on the other member's Connections page, then block from a profile.
    await page.goto(web + `/people/${pid.hal}`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-connect-mode=dating]').getAttribute('data-connection'), 'none'); pass();
    await page.click('[data-connect-mode=dating] button');
    await page.waitForSelector('[data-connect-mode=dating][data-connection=sent]'); pass();
    assert.equal(await page.locator('[data-connect-status]').innerText(), 'Request sent.'); pass();
    if (output) await page.screenshot({ path: output + '/person-request-390.png', fullPage: true });
    const halContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const halPage = await halContext.newPage();
    await signIn(halPage, members.hal);
    await halPage.click('.tab-bar [data-tab=Connections]');
    await halPage.waitForSelector('[data-connections=ready]', { timeout: 20000 });
    const incoming = halPage.locator(`[data-box=received] li:has(a[href="/people/${pid.ann}"])`);
    assert.equal(await incoming.getAttribute('data-request-mode'), 'dating'); pass();
    if (output) await halPage.screenshot({ path: output + '/connections-390.png', fullPage: true });
    await incoming.getByRole('button', { name: 'Accept' }).click();
    await halPage.waitForSelector('[data-connections-status=accept]'); pass();
    assert.equal(await halPage.locator(`[data-box=connected] a[href="/people/${pid.ann}"]`).count(), 1); pass();
    assert.equal(await halPage.locator(`[data-box=received] a[href="/people/${pid.ann}"]`).count(), 0); pass();
    await page.reload(); await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-connect-mode=dating]').getAttribute('data-connection'), 'connected'); pass();
    await page.getByRole('button', { name: 'Block Fictional Hal' }).click();
    await page.waitForSelector('[data-block=confirm]'); pass();
    await page.locator('[data-block=confirm]').getByRole('button', { name: 'Block', exact: true }).click();
    await page.waitForSelector('[data-person=blocked]'); pass();
    assert.ok(!(await explore('ann')).includes(pid.hal) && !(await explore('hal')).includes(pid.ann)); pass();
    await halPage.reload(); await halPage.waitForSelector('[data-connections=ready]', { timeout: 20000 });
    assert.equal(await halPage.locator(`a[href="/people/${pid.ann}"]`).count(), 0); pass(); // gone from Hal's lists too
    await page.goto(web + '/connections');
    await page.waitForSelector('[data-connections=ready]', { timeout: 20000 });
    await page.locator(`[data-blocked="${pid.hal}"]`).getByRole('button', { name: 'Unblock' }).click();
    await page.waitForSelector('[data-connections-status=unblocked]'); pass();
    assert.ok((await explore('ann')).includes(pid.hal)); pass();
    await halContext.close(); await context.close();
    console.log(`PASS: connection requests and blocks acceptance, ${passed} checks (send and both boxes, nothing connects on its own, one pending per pair and mode including a concurrent race, only the recipient answers and only the sender withdraws, accept/decline/withdraw and settled requests, 30-day wait after a decline, modes off/hidden/unknown, self and unknown ids, a tampered body, bots never initiate but may accept, blocks both ways in Explore/profiles/requests/lists with 404s that never say "blocked", blocking back and unblocking, connections hidden while blocked, 20-a-day audited rate limit, BFF allow-list and 16 KB limit, web send/accept/block/unblock in Edge).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
