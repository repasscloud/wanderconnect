// Local acceptance for slice S (D141): the routes the app's Explore, Connections, Chats and Me screens call, with the
// app's own token (swoovr-mobile, code + PKCE in actual Edge), and every refusal those screens now explain in the web's
// words: no date of birth, no Dating details, restricted, suspended, the age check, the beta gate. Negative cases: other
// members' ids, another market, blocked pairs, bots, staff tokens, unknown and settled ids, tampered bodies. Then the web
// badge artwork, drawn from the same BadgeArtwork as the app, and the shared tab bar. Same local stack as
// tests/discovery/matching.browser.cjs; every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, createStaff, appToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;

(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cal', 'dex', 'eve', 'res', 'sus', 'chk', 'out', 'bot', 'blk'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('app-' + n)])));
    await cli('beta', 'add', ...names.filter(n => n !== 'out').map(n => members[n].email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await appToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional App ' + n, homeCityId: n === 'bot' ? null : n === 'cal' ? MELBOURNE : ADELAIDE } });
      if (n !== 'out') assert.equal(saved.status, 200, n);
      pid[n] = saved.body?.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'out' || n === 'bot') continue;
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      if (n === 'dex') continue; // no date of birth
      if (n === 'eve') { await call(tokens.eve, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1991-03-03' } }); continue; } // no Dating details
      await matchReady(tokens[n]);
    }

    // The app's own client: a public client, the same API, the same answers as the web's.
    assert.equal(claims(tokens.ana).azp, 'swoovr-mobile'); pass();
    let r = await call(tokens.ana, '/api/me');
    assert.deepEqual([r.status, r.body.accountState, 'dateOfBirth' in r.body], [200, 'active', true]); pass();

    // Every route the app's screens call answers the app's token (Explore, person, Connections, Me pages).
    for (const path of ['/api/session', '/api/modes', '/api/me/profile', '/api/explore?mode=friends', `/api/profiles/${pid.bob}`,
      '/api/requests?box=received', '/api/requests?box=sent', '/api/connections', '/api/blocks', '/api/report-categories', '/api/me/reports',
      '/api/me/notices', '/api/me/verification', '/api/profile-options', '/api/me/identity', '/api/me/modes/friends/filters', '/api/me/trips',
      '/api/travel/visitors?mode=friends', '/api/places?q=adel', '/api/me/plan'])
    { assert.equal((await call(tokens.ana, path)).status, 200, path); pass(); }

    // Explore: people in the same market with badges, age and gender as allowed, never a date of birth.
    let people = (await call(tokens.ana, '/api/explore?mode=friends')).body;
    assert.equal(people.market, 'Adelaide'); pass();
    assert.ok(people.people.some(p => p.id === pid.bob) && !people.people.some(p => [pid.cal, pid.dex, pid.out].includes(p.id))); pass();
    assert.ok(people.people.every(p => !('dateOfBirth' in p) && Array.isArray(p.badges))); pass();

    // The refusals the app now explains (MemberText), each with the code the screen keys on.
    assert.deepEqual(code(await call(tokens.dex, '/api/explore?mode=friends')), [403, 'date-of-birth-required']); pass();
    assert.deepEqual(code(await call(tokens.eve, '/api/explore?mode=dating')), [403, 'dating-details-required']); pass();
    assert.equal((await call(tokens.eve, '/api/explore?mode=friends')).status, 200); pass(); // Friends doesn't need them
    assert.deepEqual(code(await call(tokens.cal, '/api/explore?mode=friends')), [403, 'market-not-open']); pass();
    assert.deepEqual(code(await call(tokens.out, '/api/explore?mode=friends')), [403, 'beta-not-enrolled']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/explore?mode=travel')), [404, 'unknown-mode']); pass(); // Travel is not a mode (D146: 404)

    // Age assurance (D127): barred everywhere but the account and verification.
    assert.equal((await call(root, `/api/admin/accounts/${ids.chk}/age-assurance`, { method: 'PUT', body: { needsVerification: true } })).status, 200); pass();
    for (const path of ['/api/explore?mode=friends', '/api/connections', `/api/profiles/${pid.bob}`, '/api/me/trips'])
    { assert.deepEqual(code(await call(tokens.chk, path)), [403, 'age-verification-required'], path); pass(); }
    assert.deepEqual([(await call(tokens.chk, '/api/me')).body.ageStatus, (await call(tokens.chk, '/api/me/verification')).status], ['needsVerification', 200]); pass();

    // Restricted and suspended (D160): every member route barred; /me says the state; notices stay open.
    const sanction = (who, body) => call(root, `/api/admin/accounts/${ids[who]}/sanctions`, { method: 'POST', body: { hideFromDiscovery: false, noNewRequests: false, noMessages: false, ...body } });
    assert.equal((await sanction('res', { action: 'restrict', reason: 'Fictional restriction for the app suite.', days: 2, noNewRequests: true })).status, 200); pass();
    assert.equal((await sanction('sus', { action: 'suspend', reason: 'Fictional suspension for the app suite.', days: 2 })).status, 200); pass();
    for (const [who, expected] of [['res', 'account-restricted'], ['sus', 'account-suspended']]) {
      for (const path of ['/api/explore?mode=friends', `/api/profiles/${pid.ana}`, '/api/connections', '/api/requests?box=received', '/api/me/trips', '/api/me/identity'])
      { assert.deepEqual(code(await call(tokens[who], path)), [403, expected], who + path); pass(); }
      assert.deepEqual(code(await call(tokens[who], '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })), [403, expected]); pass();
      assert.equal((await call(tokens[who], '/api/me/notices')).status, 200); pass();
      assert.equal((await call(tokens[who], '/api/me')).body.accountState, who === 'res' ? 'restricted' : 'suspended'); pass();
    }
    // A barred member is out of others' discovery and profiles.
    people = (await call(tokens.ana, '/api/explore?mode=friends')).body.people.map(p => p.id);
    assert.ok(!people.includes(pid.res) && !people.includes(pid.sus)); pass();
    assert.deepEqual(code(await call(tokens.ana, `/api/profiles/${pid.res}`)), [404, 'unknown-mode']); pass();

    // Person pages: other markets, unknown and malformed ids, no date of birth all read the same (the profile route's 404 unknown-mode).
    for (const id of [pid.cal, pid.dex, '00000000-0000-0000-0000-000000000000', ids.bob /* an account id, not a public id */])
    { assert.deepEqual(code(await call(tokens.ana, `/api/profiles/${id}`)), [404, 'unknown-mode'], id); pass(); }
    assert.equal((await call(tokens.ana, '/api/profiles/not-a-guid')).status, 404); pass();
    r = await call(tokens.ana, `/api/profiles/${pid.bob}?mode=friends`); // D175: seen from one mode
    assert.ok(r.body.modes.some(m => m.id === 'friends' && m.connection === 'none' && m.canRequest) && !('dateOfBirth' in r.body)); pass();

    // Requests from the person page and Connections: tampered fields ignored, unknown modes refused, other markets 404.
    r = await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 'friends', from: pid.eve, status: 'accepted', direction: 'received' } });
    assert.deepEqual([r.status, r.body.status, r.body.direction, r.body.person.id], [201, 'pending', 'sent', pid.bob]); pass();
    const request = r.body.id;
    assert.deepEqual(code(await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 'friends' } })), [409, 'request-pending']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 'travel' } })), [404, 'unknown-mode']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.cal, mode: 'friends' } })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 7 } })).status, 400); pass();
    // Another member's request: eve can't accept or withdraw it; the sender can't accept their own.
    for (const [who, answer] of [['eve', 'accept'], ['eve', 'withdraw'], ['ana', 'accept']])
    { assert.ok([403, 404].includes((await call(tokens[who], `/api/requests/${request}/${answer}`, { method: 'POST' })).status), who + answer); pass(); }
    assert.equal((await call(tokens.bob, `/api/requests?box=received`)).body.items.filter(i => i.id === request).length, 1); pass();
    r = await call(tokens.bob, `/api/requests/${request}/accept`, { method: 'POST' });
    assert.deepEqual([r.status, r.body.status], [200, 'accepted']); pass();
    assert.deepEqual(code(await call(tokens.bob, `/api/requests/${request}/decline`, { method: 'POST' })), [409, 'request-settled']); pass(); // settled
    assert.ok((await call(tokens.ana, '/api/connections')).body.items.some(i => i.id === request && i.person.id === pid.bob)); pass();
    assert.equal((await call(tokens.ana, `/api/profiles/${pid.bob}`)).body.modes.find(m => m.id === 'friends').connection, 'connected'); pass();

    // Bots: shown with only the bot badge where an admin authorised them; never initiate.
    assert.equal((await call(root, `/api/admin/accounts/${ids.bot}/bot`, { method: 'PUT', body: { bot: true } })).status, 204); pass();
    await call(root, `/api/admin/accounts/${ids.bot}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } });
    await call(root, `/api/admin/accounts/${ids.bot}/bot-modes`, { method: 'PUT', body: { modes: ['friends'] } });
    await call(tokens.bot, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const botCard = (await call(tokens.ana, '/api/explore?mode=friends')).body.people.find(p => p.id === pid.bot);
    assert.deepEqual(botCard?.badges, [{ badge: 'bot', checkedAt: null }]); pass();
    assert.deepEqual(code(await call(tokens.bot, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })), [403, 'bot-cannot-initiate']); pass();
    assert.equal((await call(tokens.ana, '/api/reports', { method: 'POST', body: { subjectType: 'bot', subjectId: pid.bot, category: 'spam', text: null, block: false } })).status, 201); pass();
    assert.equal((await call(tokens.ana, '/api/reports', { method: 'POST', body: { subjectType: 1, subjectId: pid.bob, category: 'spam', block: false } })).status, 400); pass();

    // Blocking from the person page: gone both ways at once; Connections lists it; unblocking restores visibility.
    assert.equal((await call(tokens.ana, '/api/blocks', { method: 'POST', body: { profileId: pid.blk } })).status, 204); pass();
    assert.deepEqual(code(await call(tokens.ana, `/api/profiles/${pid.blk}`)), [404, 'unknown-mode']); pass();
    assert.deepEqual(code(await call(tokens.blk, `/api/profiles/${pid.ana}`)), [404, 'unknown-mode']); pass();
    assert.ok(!(await call(tokens.blk, '/api/explore?mode=friends')).body.people.some(p => p.id === pid.ana)); pass();
    assert.deepEqual(code(await call(tokens.blk, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })), [404, 'not-available']); pass();
    assert.deepEqual((await call(tokens.ana, '/api/blocks')).body.map(b => b.id), [pid.blk]); pass();
    assert.ok(!(await call(tokens.blk, '/api/blocks')).body.some(b => b.id === pid.ana)); pass(); // the blocked side isn't told
    assert.equal((await call(tokens.blk, `/api/blocks/${pid.ana}`, { method: 'DELETE' })).status, 404); pass(); // only the blocker unblocks
    assert.equal((await call(tokens.ana, `/api/blocks/${pid.blk}`, { method: 'DELETE' })).status, 204); pass();
    assert.equal((await call(tokens.ana, `/api/profiles/${pid.blk}`)).status, 200); pass();

    // Staff tokens never reach member screens (a staff realm token isn't a member).
    const staff = await createStaff('app-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff);
    for (const path of ['/api/explore?mode=friends', `/api/profiles/${pid.ana}`, '/api/connections'])
    { assert.ok([401, 403].includes((await call(sup, path)).status), path); pass(); }
    // A member token never reaches staff routes.
    assert.equal((await call(tokens.ana, `/api/admin/accounts/${ids.bob}/summary`)).status, 403); pass();
    // No token at all.
    assert.equal((await call(null, '/api/explore?mode=friends')).status, 401); pass();

    // Audit: ids only, never a date of birth or text from these steps.
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%1990-01-01%' OR detail LIKE '%1991-03-03%' OR detail LIKE '%Fictional restriction%'`), '0'); pass();

    // Web: the badges are drawn from the shared artwork (five shapes, fixed colours) and the tab bar is the app's.
    const page = await (await browser.newContext({ viewport: { width: 390, height: 844 } })).newPage();
    await page.goto(web + '/prototype/components');
    const drawn = await page.locator('[data-badges] .badge').evaluateAll(els => els.map(e => ({ kind: e.dataset.badge, paths: e.querySelectorAll('path').length,
      rects: e.querySelectorAll('rect, circle').length, fill: getComputedStyle(e.querySelector('.badge-disc, .badge-bird')).fill, transform: e.querySelector('path').getAttribute('transform') })));
    assert.deepEqual(drawn.map(d => [d.kind, d.paths, d.rects]), [['photo', 5, 0], ['id', 5, 0], ['founding', 1, 0], ['staff', 1, 0], ['bot', 1, 0]]); pass();
    assert.deepEqual(drawn.map(d => d.fill), ['rgb(8, 124, 250)', 'rgb(0, 170, 160)', 'rgb(212, 175, 55)', 'rgb(224, 17, 95)', 'rgb(192, 192, 192)']); pass();
    assert.ok(drawn.slice(2).every(d => d.transform?.startsWith('translate(145')) && drawn[0].transform === null); pass();
    assert.deepEqual(await page.locator('nav.tab-bar [data-tab]').evaluateAll(els => els.map(e => e.dataset.tab)), ['Explore', 'Connections', 'Chats', 'Me']); pass();
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= 390)); pass();

    console.log(`PASS: app parity (${passed} checks).`);
  } finally {
    await edge.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
