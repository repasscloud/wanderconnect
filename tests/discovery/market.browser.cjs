// Local acceptance for market-scoped discovery (Q24, D125, D146): Explore, person pages and requests reach only people
// whose home city is in the viewer's open market (or bots assigned to it); a declared trip lets locals find and write to
// visiting travellers, never the other way round; existing relationships survive a move; members outside every open
// market and members without a home city are told why; admins configure markets and bot assignments without a
// release; moving the home city into another market waits 30 days. Then the Explore and person pages in actual Edge.
// Same local stack as tests/travel/travel.browser.cjs (full GeoNames import) plus the enrolled fictional root admin.
// Every member is a fictional adult created for this run; the test market is removed at the end.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, GLENELG = 2070830, MELBOURNE = 2158177, SYDNEY = 2147714, DARWIN = 2073124, PALMERSTON = 6301965;
const TEST_MARKET = 'test-darwin';
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = days => { const d = new Date(todayIn('Australia/Adelaide') + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    assert.equal(await sql(`SELECT count(*) FROM geo_cities WHERE geo_name_id IN (${[ADELAIDE, GLENELG, MELBOURNE, SYDNEY, DARWIN, PALMERSTON]})`), '6',
      'GeoNames is not imported: run ./scripts/geonames.sh import');
    await sql(`DELETE FROM markets WHERE id = '${TEST_MARKET}'`); // a previous run that stopped early
    const started = await sql(`SELECT now()`); // audit rows from earlier runs share the test market id
    const root = await rootAdminToken(browser);
    const names = ['ada', 'gle', 'mel', 'syd', 'non', 'trv', 'mov', 'bo', 'stf', 'dar', 'pal'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('market-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const homes = { ada: ADELAIDE, gle: GLENELG, mel: MELBOURNE, syd: SYDNEY, non: null, trv: MELBOURNE, mov: ADELAIDE, bo: MELBOURNE, stf: MELBOURNE, dar: DARWIN, pal: PALMERSTON };
    const tokens = {}, pid = {}, ids = {};
    const profile = (n, home = homes[n], extra = {}) => call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), homeCityId: home, ...extra } });
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await profile(n);
      assert.equal(saved.status, 200, n);
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const code = r => [r.status, r.body?.code];
    const explore = async (who, mode = 'dating') => (await call(tokens[who], '/api/v1/explore?mode=' + mode)).body.people.map(p => p.id);
    const person = (who, whom) => call(tokens[who], `/api/v1/profiles/${pid[whom]}`);
    const send = (from, to, mode = 'dating', extra = {}) => call(tokens[from], '/api/v1/requests', { method: 'POST', body: { to: pid[to], mode, ...extra } });

    // The home market is the server's: worked out from the home city, never returned (D146).
    assert.equal(await sql(`SELECT home_market_id FROM member_profiles WHERE account_id = '${ids.gle}'`), 'adelaide'); pass(); // Glenelg is in Adelaide
    assert.equal(await sql(`SELECT coalesce(home_market_id, 'none') FROM member_profiles WHERE account_id = '${ids.mel}'`), 'none'); pass();
    const own = await call(tokens.ada, '/api/v1/me/profile');
    assert.ok(!JSON.stringify(own.body).includes('adelaide') && own.body.marketInterest === false); pass();

    // Explore is local (rules 8–12): same market only, named; never another market.
    let r = await call(tokens.ada, '/api/v1/explore?mode=dating');
    assert.equal(r.status, 200); assert.equal(r.body.market, 'Adelaide'); pass();
    assert.ok(r.body.people.some(p => p.id === pid.gle) && r.body.people.some(p => p.id === pid.mov)); pass();
    for (const other of ['mel', 'syd', 'non', 'trv', 'stf', 'dar', 'pal']) { assert.ok(!r.body.people.some(p => p.id === pid[other]), other); pass(); }
    // Outside every market, or no home city (rules 30–33): told why, nothing listed.
    assert.deepEqual(code(await call(tokens.mel, '/api/v1/explore?mode=dating')), [403, 'market-not-open']); pass();
    assert.match((await call(tokens.mel, '/api/v1/explore?mode=dating')).body.title, /isn't available in your area yet/); pass();
    assert.deepEqual(code(await call(tokens.non, '/api/v1/explore?mode=dating')), [403, 'home-city-required']); pass();
    assert.match((await call(tokens.non, '/api/v1/explore?mode=dating')).body.title, /Add your home city/); pass();
    assert.deepEqual(code(await call(tokens.mel, '/api/v1/explore?mode=travel')), [404, 'unknown-mode']); pass();
    // A tampered query (a market or city parameter) changes nothing (rule 12).
    assert.deepEqual(code(await call(tokens.mel, '/api/v1/explore?mode=dating&market=adelaide&homeCityId=2078025')), [403, 'market-not-open']); pass();

    // Profiles and requests across markets read as not available (rules 11–12), whatever the client sends.
    assert.equal((await person('ada', 'gle')).status, 200); pass();
    assert.deepEqual((await person('ada', 'gle')).body.modes.map(m => [m.id, m.canRequest]), [['dating', true], ['friends', true]]); pass();
    for (const [who, whom] of [['mel', 'ada'], ['ada', 'mel'], ['non', 'ada'], ['syd', 'mel'], ['dar', 'ada']]) {
      assert.equal((await person(who, whom)).status, 404, `${who} opens ${whom}`); pass();
      assert.deepEqual(code(await send(who, whom)), [404, 'not-available'], `${who} asks ${whom}`); pass();
    }
    assert.deepEqual(code(await call(tokens.mel, '/api/v1/requests', { method: 'POST', body: { to: pid.ada, mode: 'dating', homeCityId: ADELAIDE, market: 'adelaide' } })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ada, '/api/v1/profiles/00000000-0000-0000-0000-000000000000')).status, 404); pass(); // unknown id
    assert.deepEqual(code(await call(tokens.ada, '/api/v1/requests', { method: 'POST', body: { to: '00000000-0000-0000-0000-000000000000', mode: 'dating' } })), [404, 'not-available']); pass();
    // A block beats the market (D118): same market, still 404 both ways.
    await call(tokens.gle, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.ada } });
    assert.ok(!(await explore('ada')).includes(pid.gle)); pass();
    assert.equal((await person('ada', 'gle')).status, 404); pass();
    await call(tokens.gle, `/api/v1/blocks/${pid.ada}`, { method: 'DELETE' });

    // Staff get no market privileges (rules 14, 42); a staff member at home in Melbourne stays out of Adelaide.
    await call(root, `/api/v1/admin/accounts/${ids.stf}/staff-affiliation`, { method: 'PUT', body: { active: true } });
    assert.ok(!(await explore('ada')).includes(pid.stf)); pass();
    assert.equal((await person('ada', 'stf')).status, 404); pass();

    // Travel is the way to reach another market (rules 15–29): a declared trip lets Adelaide locals find and write to
    // the traveller; the traveller can't start contact with them before arrival.
    let trip = await call(tokens.trv, '/api/v1/me/trips', { method: 'POST', body: { cityId: ADELAIDE, arrival: plus(3), departure: plus(6), visible: true } });
    assert.equal(trip.status, 201); pass();
    assert.ok(!(await explore('ada')).includes(pid.trv)); pass(); // the feed is separate from Explore
    assert.ok((await call(tokens.ada, '/api/v1/travel/visitors?mode=dating')).body.people.some(p => p.id === pid.trv)); pass();
    r = await person('ada', 'trv');
    assert.equal(r.status, 200); assert.deepEqual(r.body.modes.map(m => [m.id, m.canRequest]), [['dating', true], ['friends', true]]); pass();
    assert.deepEqual(code(await send('trv', 'ada')), [404, 'not-available']); pass(); // no early initiation (D60)
    assert.equal((await person('trv', 'ada')).status, 404); pass(); // no feed or profiles of locals for travellers
    r = await send('ada', 'trv'); // from the profile: the visit is attached
    assert.equal(r.status, 201); assert.equal(r.body.visiting.tripId, trip.body.id); pass();
    const adaTrv = r.body.id;
    r = await person('trv', 'ada'); // a request between them opens the profile (rule 13)
    assert.equal(r.status, 200); pass();
    assert.deepEqual(r.body.modes.map(m => [m.id, m.connection, m.canRequest]), [['dating', 'received', false], ['friends', 'none', false]]); pass();
    assert.deepEqual(code(await send('trv', 'ada', 'friends')), [404, 'not-available']); pass();
    assert.equal((await call(tokens.trv, `/api/v1/requests/${adaTrv}/accept`, { method: 'POST' })).status, 200); pass();
    // Another member's trip id can't be borrowed: the trip must be the recipient's and one the sender can see.
    assert.deepEqual(code(await send('gle', 'mel', 'dating', { trip: trip.body.id })), [404, 'not-available']); pass();
    assert.deepEqual(code(await send('mel', 'ada', 'dating', { trip: trip.body.id })), [404, 'not-available']); pass();
    // Cancelling the trip ends the access but keeps the connection (rule 27).
    await call(tokens.trv, `/api/v1/me/trips/${trip.body.id}/cancel`, { method: 'POST' });
    assert.ok(!(await call(tokens.ada, '/api/v1/travel/visitors?mode=dating')).body.people.some(p => p.id === pid.trv)); pass();
    r = await person('ada', 'trv');
    assert.equal(r.status, 200); pass(); // connected
    assert.deepEqual(r.body.modes.map(m => [m.id, m.connection, m.canRequest]), [['dating', 'connected', true], ['friends', 'none', true]]); pass(); // a connected pair may add a mode
    assert.ok((await call(tokens.trv, '/api/v1/connections')).body.items.some(x => x.id === adaTrv)); pass();

    // Moving away keeps existing relationships (rules 13, 36, 46) and ends local discovery.
    r = await send('ada', 'mov'); assert.equal(r.status, 201); const adaMov = r.body.id;
    await call(tokens.mov, `/api/v1/requests/${adaMov}/accept`, { method: 'POST' });
    r = await send('gle', 'mov'); assert.equal(r.status, 201); const gleMov = r.body.id; // still pending when mov moves
    assert.equal((await profile('mov', MELBOURNE)).status, 200); pass(); // leaving every market is always allowed
    assert.ok(!(await explore('ada')).includes(pid.mov)); pass();
    assert.deepEqual(code(await call(tokens.mov, '/api/v1/explore?mode=dating')), [403, 'market-not-open']); pass();
    assert.equal((await person('ada', 'mov')).status, 200); pass();
    assert.equal((await person('gle', 'mov')).status, 200); pass(); // a pending request keeps the profile open
    assert.ok((await call(tokens.mov, '/api/v1/connections')).body.items.some(x => x.id === adaMov)); pass();
    assert.equal((await call(tokens.mov, `/api/v1/requests/${gleMov}/accept`, { method: 'POST' })).status, 200); pass(); // pending requests survive
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'home-market.changed' AND target_id = '${ids.mov}' ORDER BY at DESC LIMIT 1`), 'adelaide>none'); pass();

    // Members outside every market can ask to hear when theirs opens (rule 37); locals and the homeless can't.
    assert.equal((await call(tokens.mel, '/api/v1/me/market-interest', { method: 'PUT', body: { interested: true } })).status, 204); pass();
    assert.equal((await call(tokens.mel, '/api/v1/me/market-interest', { method: 'PUT', body: { interested: true } })).status, 204); pass(); // idempotent
    assert.equal((await call(tokens.mel, '/api/v1/me/profile')).body.marketInterest, true); pass();
    assert.equal(await sql(`SELECT geo_name_id FROM market_interests WHERE account_id = '${ids.mel}'`), String(MELBOURNE)); pass();
    assert.deepEqual(code(await call(tokens.ada, '/api/v1/me/market-interest', { method: 'PUT', body: { interested: true } })), [409, 'invalid-request']); pass();
    assert.deepEqual(code(await call(tokens.non, '/api/v1/me/market-interest', { method: 'PUT', body: { interested: true } })), [403, 'home-city-required']); pass();
    assert.equal((await call(tokens.mel, '/api/v1/me/market-interest', { method: 'PUT', body: '{"interested":' })).status, 400); pass(); // tampered body
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'market-interest.%' AND target_id = '${ids.mel}' AND detail IS NOT NULL`), '0'); pass();
    // Interest follows the home city and lapses once the home market is open.
    await call(tokens.syd, '/api/v1/me/market-interest', { method: 'PUT', body: { interested: true } });
    assert.equal((await profile('syd', MELBOURNE)).body.marketInterest, true); pass();
    assert.equal(await sql(`SELECT geo_name_id FROM market_interests WHERE account_id = '${ids.syd}'`), String(MELBOURNE)); pass();
    assert.equal((await profile('syd', ADELAIDE)).body.marketInterest, false); pass();
    assert.equal((await profile('syd', SYDNEY)).status, 200); pass(); // leaving every market is always allowed

    // Admin market registry (rules 1–6, 48): admin only; validated; no city in the audit.
    assert.notEqual((await call(tokens.ada, '/api/v1/admin/markets')).status, 200); pass();
    assert.notEqual((await call(tokens.ada, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { name: 'x', status: 'beta', areas: [{ centreCityId: DARWIN, radiusKm: 30 }], modes: ['dating'] } })).status, 200); pass();
    let markets = await call(root, '/api/v1/admin/markets');
    assert.equal(markets.status, 200);
    assert.deepEqual(markets.body.find(m => m.id === 'adelaide'), { id: 'adelaide', name: 'Adelaide', status: 'beta', areas: [{ centreCityId: ADELAIDE, radiusKm: 50 }],
      modes: ['activities', 'business', 'dating', 'friends'], homeMembers: markets.body.find(m => m.id === 'adelaide').homeMembers }); pass();
    const good = { name: 'Darwin (test)', status: 'planned', areas: [{ centreCityId: DARWIN, radiusKm: 30 }], modes: ['dating'] };
    for (const [id, body] of [['Bad Id', good], ['*', good], [TEST_MARKET, { ...good, status: 'open' }], [TEST_MARKET, { ...good, name: '' }],
      [TEST_MARKET, { ...good, name: 'n'.repeat(81) }], [TEST_MARKET, { ...good, areas: [] }], [TEST_MARKET, { ...good, areas: [{ centreCityId: DARWIN, radiusKm: 0 }] }],
      [TEST_MARKET, { ...good, areas: [{ centreCityId: DARWIN, radiusKm: 1001 }] }], [TEST_MARKET, { ...good, areas: [{ centreCityId: 1, radiusKm: 30 }] }],
      [TEST_MARKET, { ...good, areas: [{ centreCityId: DARWIN, radiusKm: 30 }, { centreCityId: DARWIN, radiusKm: 40 }] }],
      [TEST_MARKET, { ...good, areas: Array.from({ length: 21 }, (_, i) => ({ centreCityId: DARWIN + i, radiusKm: 1 })) }], [TEST_MARKET, { ...good, modes: ['travel'] }]]) {
      assert.equal((await call(root, `/api/v1/admin/markets/${encodeURIComponent(id)}`, { method: 'PUT', body })).status, 400, JSON.stringify(body).slice(0, 80)); pass();
    }
    assert.equal(await sql(`SELECT count(*) FROM markets WHERE id = '${TEST_MARKET}'`), '0'); pass(); // refusals changed nothing
    r = await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: good });
    assert.equal(r.status, 200); assert.equal(r.body.status, 'planned'); pass();
    // Homes are recomputed: dar's and pal's (Palmerston is about 15 km away); earlier runs' Darwin members count too.
    assert.ok(r.body.homeMembers >= 2); pass();
    assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE home_market_id = '${TEST_MARKET}' AND account_id IN ('${ids.dar}', '${ids.pal}')`), '2'); pass();
    // A planned market isn't open yet: dar is at home there but can't use Explore.
    assert.equal(await sql(`SELECT home_market_id FROM member_profiles WHERE account_id = '${ids.dar}'`), TEST_MARKET); pass();
    assert.deepEqual(code(await call(tokens.dar, '/api/v1/explore?mode=dating')), [403, 'market-not-open']); pass();
    // A wider region with two areas (rule 6), opened as a beta market offering Dating only.
    r = await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { ...good, status: 'beta', areas: [{ centreCityId: DARWIN, radiusKm: 10 }, { centreCityId: PALMERSTON, radiusKm: 10 }] } });
    assert.deepEqual([r.status, r.body.status, r.body.areas.length], [200, 'beta', 2]); pass();
    r = await call(tokens.dar, '/api/v1/explore?mode=dating');
    assert.equal(r.body.market, 'Darwin (test)'); assert.ok(r.body.people.some(p => p.id === pid.pal)); pass();
    assert.ok(!r.body.people.some(p => p.id === pid.ada)); pass();
    assert.ok(!(await explore('ada')).includes(pid.dar)); pass(); // markets never mix
    assert.deepEqual(code(await call(tokens.dar, '/api/v1/explore?mode=friends')), [403, 'market-not-open']); pass(); // a mode the market doesn't offer
    assert.equal((await send('dar', 'pal')).status, 201); pass();
    // Suspending stops new discovery but keeps requests (rule 47).
    await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { ...good, status: 'suspended' } });
    assert.deepEqual(code(await call(tokens.dar, '/api/v1/explore?mode=dating')), [403, 'market-not-open']); pass();
    assert.equal((await call(tokens.pal, '/api/v1/requests?box=received')).body.items.length, 1); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'market.updated' AND target_id = '${TEST_MARKET}' AND at >= '${started}'`), '3'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE (action LIKE 'market.%' OR action LIKE 'home-market.%') AND (detail LIKE '%${DARWIN}%' OR detail ILIKE '%darwin (%' OR detail ILIKE '%melbourne%')`), '0'); pass();

    // Home moves (rules 28, 34): into a different market once every 30 days; refusals change nothing and are audited.
    await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { ...good, status: 'beta' } });
    assert.equal((await profile('dar', ADELAIDE)).status, 200); pass(); // first move into a market
    assert.ok((await explore('ada')).includes(pid.dar)); pass();
    r = await profile('dar', DARWIN);
    assert.deepEqual(code(r), [403, 'home-market-locked']); pass();
    assert.equal((await call(tokens.dar, '/api/v1/me/profile')).body.homeCity.id, ADELAIDE); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'home-market.refused' AND target_id = '${ids.dar}'`), `adelaide>${TEST_MARKET}`); pass();
    assert.equal((await profile('dar', GLENELG)).status, 200); pass(); // within the same market is fine
    assert.equal((await profile('dar', null)).status, 200); pass(); // clearing it is fine
    assert.deepEqual(code(await profile('dar', DARWIN)), [403, 'home-market-locked']); pass(); // clearing doesn't reset the wait
    assert.equal((await profile('dar', ADELAIDE)).status, 200); pass(); // returning is fine
    await sql(`UPDATE member_profiles SET home_market_moved_at = now() - interval '31 days' WHERE account_id = '${ids.dar}'`);
    assert.equal((await profile('dar', DARWIN)).status, 200); pass();
    // The client can't set the market or the move time itself (extra fields are ignored).
    assert.equal((await profile('pal', PALMERSTON, { homeMarketId: 'adelaide', homeMarketMovedAt: null })).status, 200); pass();
    assert.equal(await sql(`SELECT home_market_id FROM member_profiles WHERE account_id = '${ids.pal}'`), TEST_MARKET); pass();

    // Official bots (rules 38–41): only in the markets an admin assigned, `*` meaning every open market.
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes: ['dating'] } });
    await call(tokens.bo, '/api/v1/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.ok(!(await explore('ada')).includes(pid.bo)); pass(); // no market assigned
    assert.deepEqual((await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-markets`)).body.markets, []); pass();
    for (const markets of [['nowhere'], ['adelaide', 'nowhere']]) {
      assert.deepEqual(code(await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-markets`, { method: 'PUT', body: { markets } })), [400, 'unknown-market']); pass();
    }
    assert.deepEqual(code(await call(root, `/api/v1/admin/accounts/${ids.ada}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } })), [404, 'not-a-bot']); pass();
    assert.notEqual((await call(tokens.bo, `/api/v1/admin/accounts/${ids.bo}/bot-markets`, { method: 'PUT', body: { markets: ['*'] } })).status, 200); pass();
    r = await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide', 'adelaide'] } });
    assert.deepEqual(r.body.markets, ['adelaide']); pass();
    assert.ok((await explore('ada')).includes(pid.bo)); pass();
    assert.ok(!(await explore('dar')).includes(pid.bo)); pass();
    assert.equal((await person('ada', 'bo')).status, 200); pass();
    assert.deepEqual(code(await send('bo', 'ada')), [403, 'bot-cannot-initiate']); pass();
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-markets`, { method: 'PUT', body: { markets: ['*'] } });
    assert.ok((await explore('ada')).includes(pid.bo) && (await explore('dar')).includes(pid.bo)); pass();
    await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { ...good, status: 'suspended' } });
    assert.ok((await explore('ada')).includes(pid.bo)); pass(); // still in the open market
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot`, { method: 'PUT', body: { bot: false } });
    assert.equal(await sql(`SELECT count(*) FROM bot_markets WHERE account_id = '${ids.bo}'`), '0'); pass(); // declassifying removes assignments
    assert.ok(!(await explore('ada')).includes(pid.bo)); pass(); // a Melbourne person again

    // Web: Explore names the market and lists locals; outside a market it explains and offers launch news; without a
    // home city it links to the profile; a person page offers requests only where the API allows them.
    const signIn = async member => {
      const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
      const page = await context.newPage();
      await page.goto(web + '/account/sign-in?intro=done');
      await page.fill('#username', member.email); await page.fill('#password', member.password);
      await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
      return { context, page };
    };
    let { context, page } = await signIn(members.ada);
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-mode=dating][data-explore-state=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-explore-market]').innerText(), 'People in Adelaide'); pass();
    const hrefs = await page.locator('.profile-tile h2 a').evaluateAll(els => els.map(e => e.getAttribute('href')));
    assert.ok(hrefs.includes('/people/' + pid.gle) && !hrefs.includes('/people/' + pid.mel) && !hrefs.includes('/people/' + pid.mov)); pass();
    if (output) await page.screenshot({ path: output + '/explore-market-390.png', fullPage: true });
    await page.goto(web + `/people/${pid.mel}`);
    await page.waitForSelector('[data-person=unavailable]', { timeout: 20000 }); pass();
    await context.close();

    ({ context, page } = await signIn(members.mel));
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-local=market-not-open][data-interest=on]', { timeout: 20000 }); pass();
    assert.match(await page.locator('[data-explore-local]').innerText(), /isn't available in your area yet/); pass();
    await page.click('[data-explore-local] button');
    await page.waitForSelector('[data-explore-local=market-not-open][data-interest=off]'); pass();
    assert.equal((await call(tokens.mel, '/api/v1/me/profile')).body.marketInterest, false); pass();
    await page.click('[data-explore-local] button');
    await page.waitForSelector('[data-interest=on]'); pass();
    if (output) await page.screenshot({ path: output + '/explore-not-open-390.png', fullPage: true });
    await context.close();

    ({ context, page } = await signIn(members.non));
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-local=home-city-required]', { timeout: 20000 }); pass();
    assert.equal(await page.locator('[data-explore-local] a').getAttribute('href'), '/me/profile'); pass();
    await context.close();

    ({ context, page } = await signIn(members.trv));
    const trip2 = await call(tokens.trv, '/api/v1/me/trips', { method: 'POST', body: { cityId: ADELAIDE, arrival: plus(2), departure: plus(4), visible: true } });
    assert.equal(trip2.status, 201);
    r = await send('gle', 'trv', 'friends'); assert.equal(r.status, 201); pass(); // gle writes to the visitor
    await page.goto(web + `/people/${pid.gle}`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-connect-mode=friends]').getAttribute('data-connection'), 'received'); pass();
    assert.equal(await page.locator('[data-connect-mode=dating] [data-request-unavailable]').count(), 1); pass();
    assert.equal(await page.locator('[data-connect-mode=dating] button').count(), 0); pass();
    if (output) await page.screenshot({ path: output + '/person-traveller-390.png', fullPage: true });
    await context.close();

    console.log(`PASS: market-scoped discovery acceptance, ${passed} checks (home market worked out by the server and never returned, Explore and profiles and requests local to one open market, home-city-required and market-not-open with launch interest, tampered parameters and bodies, other members' trips, unknown ids, blocks beat markets, staff without privileges, travellers found and written to by locals but never initiating, cancelled trips, relationships kept across moves, admin-only market registry with validation, regional areas, planned/beta/suspended, mode per market, id-only audit, the 30-day home move wait, bots only in assigned markets and removed on declassification, web Explore and person pages in Edge).`);
  } finally {
    await sql(`DELETE FROM markets WHERE id = '${TEST_MARKET}'`).catch(() => {});
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
