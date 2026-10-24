// Local acceptance for Travel (Q17, D107, D109–D111, D120–D122): GeoNames search, home cities and markets, trips with
// destination-local dates, the 14-day window, itineraries, overlap refusal, expiry and cancellation, paging and a rate
// limit, the asymmetric destination feed for locals, requests from it, and the Travel screen in actual Edge.
// Needs the local stack with the full GeoNames import (./scripts/geonames.sh download && ./scripts/geonames.sh import)
// and the enrolled fictional root admin. Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, sql, call, claims } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177, SYDNEY = 2147714, VICTOR_HARBOR = 2059470, GLENELG = 2070830;
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = (days, zone = 'Australia/Adelaide') => {
  const d = new Date(todayIn(zone) + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10);
};

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    assert.ok(Number(await sql(`SELECT count(*) FROM geo_cities WHERE geo_name_id IN (${[ADELAIDE, MELBOURNE, SYDNEY, VICTOR_HARBOR, GLENELG]})`)) === 5,
      'GeoNames is not imported: run ./scripts/geonames.sh import (or sample)');
    const names = ['loc', 'pal', 'trav', 'tia', 'far', 'blk', 'rex', 'nob'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('travel-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const homes = { loc: ADELAIDE, pal: GLENELG, trav: MELBOURNE, tia: SYDNEY, far: VICTOR_HARBOR, blk: MELBOURNE, rex: MELBOURNE, nob: null };
    const tokens = {}, pid = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), homeCityId: homes[n] } });
      assert.equal(saved.status, 200, n);
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const code = r => [r.status, r.body?.code];

    // GeoNames search (D111): ranked by population, "City, Region, Country", no coordinates, accents folded.
    let places = await call(tokens.loc, '/api/v1/places?q=adel');
    assert.equal(places.status, 200); assert.deepEqual(places.body[0], { id: ADELAIDE, name: 'Adelaide', region: 'South Australia', country: 'Australia', label: 'Adelaide, South Australia, Australia' }); pass();
    assert.ok(places.body.length <= 10 && places.body.every(p => Object.keys(p).sort().join() === 'country,id,label,name,region')); pass();
    places = await call(tokens.loc, '/api/v1/places?q=norwood');
    assert.ok(places.body.some(p => p.label === 'Norwood, South Australia, Australia') && places.body.some(p => p.label === 'Norwood, Massachusetts, United States')); pass();
    assert.equal(new Set(places.body.map(p => p.label)).size, places.body.length); pass(); // look-alike names are told apart
    assert.ok((await call(tokens.loc, '/api/v1/places?q=Z%C3%BCrich')).body.some(p => p.name === 'Zürich')); pass();
    assert.ok((await call(tokens.loc, '/api/v1/places?q=zurich')).body.some(p => p.name === 'Zürich')); pass();
    assert.ok((await call(tokens.loc, '/api/v1/places?q=parigi')).body.some(p => p.label.startsWith('Paris, Île-de-France'))); pass(); // alternate name
    for (const q of ['a', '', 'x'.repeat(61)]) { assert.equal((await call(tokens.loc, '/api/v1/places?q=' + q)).status, 400, q); pass(); }
    assert.deepEqual((await call(tokens.loc, '/api/v1/places?q=%25%25')).body, []); pass(); // LIKE wildcards are literal
    assert.equal((await call(null, '/api/v1/places?q=adel')).status, 401); pass();

    // Home city (D110): from GeoNames, private to the member.
    let mine = (await call(tokens.loc, '/api/v1/me/profile')).body;
    assert.deepEqual(mine.homeCity, { id: ADELAIDE, name: 'Adelaide', region: 'South Australia', country: 'Australia', label: 'Adelaide, South Australia, Australia' }); pass();
    assert.deepEqual(code(await call(tokens.nob, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Nob', homeCityId: 1 } })), [400, 'unknown-place']); pass();
    const locSeen = await call(tokens.trav, `/api/v1/profiles/${pid.loc}`);
    assert.ok(!JSON.stringify(locSeen.body).includes('homeCity') && !JSON.stringify(locSeen.body).includes('Adelaide, South')); pass();

    // Trips: destination-local dates, mandatory and ordered; window and lifecycle from the API.
    const trip = (who, body) => call(tokens[who], '/api/v1/me/trips', { method: 'POST', body: { visible: true, itineraryId: null, ...body } });
    const put = (who, id, body) => call(tokens[who], `/api/v1/me/trips/${id}`, { method: 'PUT', body });
    let r = await trip('trav', { cityId: ADELAIDE, arrival: plus(10), departure: plus(13) });
    assert.equal(r.status, 201); pass();
    const travSoon = r.body;
    assert.deepEqual([travSoon.state, travSoon.activeMarket, travSoon.opensOn, travSoon.city.label], ['visible', true, plus(-4), 'Adelaide, South Australia, Australia']); pass();
    assert.ok(!JSON.stringify(travSoon).match(/latitude|longitude|radius|distance/i)); pass();
    r = await trip('trav', { cityId: ADELAIDE, arrival: plus(20), departure: plus(22) });
    assert.deepEqual([r.body.state, r.body.opensOn], ['planned', plus(6)]); pass(); // beyond the window
    const travLater = r.body;
    assert.equal((await trip('trav', { cityId: ADELAIDE, arrival: plus(14), departure: plus(15) })).body.state, 'visible'); pass(); // exactly 14 days
    const fifteen = await trip('trav', { cityId: ADELAIDE, arrival: plus(15), departure: plus(16), visible: false });
    assert.deepEqual([fifteen.body.state, fifteen.body.visible], ['planned', false]); pass();
    for (const [what, body] of [['departure before arrival', { arrival: plus(5), departure: plus(4) }], ['already over', { arrival: plus(-5), departure: plus(-1) }],
      ['too far ahead', { arrival: plus(800), departure: plus(801) }], ['too long', { arrival: plus(1), departure: plus(400) }]]) {
      assert.deepEqual(code(await trip('trav', { cityId: ADELAIDE, ...body })), [400, 'invalid-trip-dates'], what); pass();
    }
    assert.equal((await trip('trav', { cityId: ADELAIDE, arrival: plus(-3), departure: plus(1) })).body.state, 'visible'); pass(); // already travelling
    assert.deepEqual(code(await trip('trav', { cityId: 1, arrival: plus(1), departure: plus(2) })), [400, 'unknown-place']); pass();
    assert.equal((await trip('trav', { cityId: ADELAIDE, arrival: 'soon', departure: plus(2) })).status, 400); pass();
    assert.equal((await call(tokens.trav, '/api/v1/me/trips', { method: 'POST', body: { cityId: ADELAIDE } })).status, 400); pass(); // dates are mandatory
    // Destination-local "today": a trip ending today in Honolulu is still current even when it's already tomorrow in Adelaide.
    const honolulu = (await call(tokens.trav, '/api/v1/places?q=honolulu')).body[0];
    if (honolulu && todayIn('Pacific/Honolulu') < todayIn('Australia/Adelaide')) {
      assert.equal((await trip('tia', { cityId: honolulu.id, arrival: todayIn('Pacific/Honolulu'), departure: todayIn('Pacific/Honolulu'), visible: false })).status, 201); pass();
    }

    // Overlap (D109): refused against another visible trip, the changeover day is fine, hidden trips never block.
    assert.deepEqual(code(await trip('trav', { cityId: SYDNEY, arrival: plus(12), departure: plus(16) })), [409, 'trip-overlaps']); pass();
    const hiddenOverlap = await trip('trav', { cityId: SYDNEY, arrival: plus(12), departure: plus(16), visible: false });
    assert.equal(hiddenOverlap.status, 201); pass();
    assert.deepEqual(code(await put('trav', hiddenOverlap.body.id, { cityId: SYDNEY, arrival: plus(12), departure: plus(16), visible: true, itineraryId: null })), [409, 'trip-overlaps']); pass();
    assert.equal((await put('trav', hiddenOverlap.body.id, { cityId: SYDNEY, arrival: plus(16), departure: plus(18), visible: true, itineraryId: null })).status, 200); pass(); // shares 16 with the 15–16 hidden trip only
    assert.equal((await trip('trav', { cityId: SYDNEY, arrival: plus(22), departure: plus(24) })).status, 201); pass(); // arrives the day the later Adelaide trip departs

    // Another member's trips and itineraries are not there.
    for (const [method, path] of [['PUT', `/api/v1/me/trips/${travSoon.id}`], ['POST', `/api/v1/me/trips/${travSoon.id}/cancel`], ['DELETE', `/api/v1/me/trips/${travSoon.id}`]]) {
      const res = await call(tokens.loc, path, { method, body: method === 'PUT' ? { cityId: ADELAIDE, arrival: plus(10), departure: plus(13), visible: false, itineraryId: null } : undefined });
      assert.equal(res.status, 404, method + ' ' + path); pass();
    }
    assert.equal(await sql(`SELECT visible FROM trips WHERE id = '${travSoon.id}'`), 't'); pass();
    // A tampered body can't pick the owner, the state or the lifecycle.
    r = await trip('rex', { cityId: ADELAIDE, arrival: plus(3), departure: plus(5), accountId: ids.loc, state: 'expired', cancelledAt: '2020-01-01', id: travSoon.id });
    assert.equal(r.status, 201); assert.notEqual(r.body.id, travSoon.id); pass();
    assert.equal(await sql(`SELECT account_id || ':' || (cancelled_at IS NULL) FROM trips WHERE id = '${r.body.id}'`), `${ids.rex}:true`); pass();
    const rexTrip = r.body;

    // Itineraries (D109): back-to-back stops; every stop opens once the first is within 14 days.
    const itinerary = (await call(tokens.tia, '/api/v1/me/itineraries', { method: 'POST' })).body.id;
    assert.equal((await trip('tia', { cityId: SYDNEY, arrival: plus(10), departure: plus(20), itineraryId: itinerary })).status, 201); pass();
    r = await trip('tia', { cityId: ADELAIDE, arrival: plus(25), departure: plus(28), itineraryId: itinerary });
    assert.deepEqual([r.status, r.body.state, r.body.opensOn], [201, 'visible', plus(-4)]); pass(); // 25 days away, open with the first stop
    const tiaAdelaide = r.body;
    assert.deepEqual(code(await trip('tia', { cityId: MELBOURNE, arrival: plus(43), departure: plus(45), itineraryId: itinerary })), [400, 'itinerary-not-back-to-back']); pass(); // gap of 15
    assert.deepEqual(code(await trip('tia', { cityId: MELBOURNE, arrival: plus(27), departure: plus(30), itineraryId: itinerary })), [400, 'itinerary-not-back-to-back']); pass(); // arrives before the last leaves
    assert.equal((await trip('tia', { cityId: MELBOURNE, arrival: plus(42), departure: plus(45), itineraryId: itinerary, visible: false })).status, 201); pass(); // gap of exactly 14
    const travItinerary = (await call(tokens.trav, '/api/v1/me/itineraries', { method: 'POST' })).body.id;
    assert.deepEqual(code(await trip('tia', { cityId: MELBOURNE, arrival: plus(50), departure: plus(52), itineraryId: travItinerary })), [404, 'not-available']); pass();
    const later = (await call(tokens.rex, '/api/v1/me/itineraries', { method: 'POST' })).body.id;
    r = await trip('rex', { cityId: MELBOURNE, arrival: plus(18), departure: plus(20), itineraryId: later, visible: false });
    r = await trip('rex', { cityId: ADELAIDE, arrival: plus(20), departure: plus(30), itineraryId: later });
    assert.deepEqual([r.body.state, r.body.opensOn], ['planned', plus(4)]); pass(); // first stop 18 days away: the whole itinerary waits
    await call(tokens.rex, `/api/v1/me/trips/${r.body.id}`, { method: 'DELETE' });

    // The destination feed (Q17 rules 22–28): locals see visiting travellers, city, country and exact dates only.
    const visitors = async (who, mode = 'dating') => call(tokens[who], `/api/v1/travel/visitors?mode=${mode}`);
    let feed = await visitors('loc');
    assert.equal(feed.status, 200); assert.equal(feed.body.market, 'Adelaide'); pass();
    const card = (f, who) => f.body.people.find(p => p.id === pid[who]);
    assert.deepEqual(card(feed, 'trav').visiting, { tripId: (await call(tokens.trav, '/api/v1/me/trips')).body.items.find(t => t.arrival === plus(-3)).id,
      city: 'Adelaide', country: 'Australia', arrival: plus(-3), departure: plus(1) }); pass(); // earliest visible trip, one card per traveller
    assert.equal(feed.body.people.filter(p => p.id === pid.trav).length, 1); pass();
    assert.deepEqual(Object.keys(card(feed, 'trav')).sort(), ['badges', 'displayName', 'id', 'intentions', 'visiting']); pass(); // no area, home city or coordinates
    assert.ok(!JSON.stringify(feed.body).match(/latitude|longitude|radius|distance|homeCity|area/i)); pass();
    assert.equal(card(feed, 'tia').visiting.tripId, tiaAdelaide.id); pass(); // through the itinerary
    assert.ok(card(feed, 'rex')); pass();
    assert.ok(!card(feed, 'pal') && !card(feed, 'loc')); pass(); // locals and the viewer are never "visiting"
    // Asymmetric: travellers and anyone outside an active market get no local feed (rule 23).
    for (const who of ['trav', 'far', 'nob']) { assert.deepEqual(code(await visitors(who)), [403, 'not-local'], who); pass(); }
    assert.deepEqual(code(await visitors('pal')), [200, undefined]); pass(); // Glenelg is in the Adelaide market
    assert.deepEqual(code(await visitors('loc', 'business')), [403, 'mode-not-enabled']); pass();
    assert.deepEqual(code(await visitors('loc', 'travel')), [404, 'unknown-mode']); pass();
    // Modes both people have on: Rex turns Friends off, so he's gone from Friends but not Dating.
    await call(tokens.rex, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: false, visible: false } });
    assert.ok(!card(await visitors('loc', 'friends'), 'rex') && card(await visitors('loc'), 'rex')); pass();
    // A hidden trip disappears at once and its details never show (rule 28); turning it back on restores it.
    await put('rex', rexTrip.id, { cityId: ADELAIDE, arrival: plus(3), departure: plus(5), visible: false, itineraryId: null });
    assert.ok(!card(await visitors('loc'), 'rex')); pass();
    assert.equal((await call(tokens.rex, '/api/v1/me/trips')).body.items.find(t => t.id === rexTrip.id).state, 'planned'); pass(); // kept, with its dates
    await put('rex', rexTrip.id, { cityId: ADELAIDE, arrival: plus(3), departure: plus(5), visible: true, itineraryId: null });
    assert.ok(card(await visitors('loc'), 'rex')); pass();
    // A trip outside any active market is saved but never shown; deactivating Adelaide closes the feed (D110).
    const melbourneTrip = (await call(tokens.tia, '/api/v1/me/trips')).body.items.find(t => t.city.id === SYDNEY);
    assert.equal(melbourneTrip.activeMarket, false); pass();
    try {
      await sql(`UPDATE markets SET active = false WHERE id = 'adelaide'`);
      assert.deepEqual(code(await visitors('loc')), [403, 'not-local']); pass();
      assert.equal((await call(tokens.rex, '/api/v1/me/trips')).body.items.find(t => t.id === rexTrip.id).activeMarket, false); pass();
      assert.equal((await trip('blk', { cityId: ADELAIDE, arrival: plus(2), departure: plus(4) })).status, 201); pass(); // still saved
    } finally {
      await sql(`UPDATE markets SET active = true WHERE id = 'adelaide'`);
    }
    assert.ok(card(await visitors('loc'), 'blk')); pass();
    // Blocks hide travellers too, both ways.
    await call(tokens.loc, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.blk } });
    assert.ok(!card(await visitors('loc'), 'blk')); pass();
    const blkTrip = (await call(tokens.blk, '/api/v1/me/trips')).body.items[0].id;
    assert.deepEqual(code(await call(tokens.loc, '/api/v1/requests', { method: 'POST', body: { to: pid.blk, mode: 'dating', trip: blkTrip } })), [404, 'not-available']); pass();
    // Expiry after departure, local time; cancellation keeps the history.
    await sql(`UPDATE trips SET arrival = DATE '${plus(-6)}', departure = DATE '${plus(-1)}' WHERE id = '${rexTrip.id}'`);
    assert.equal((await call(tokens.rex, '/api/v1/me/trips')).body.items.find(t => t.id === rexTrip.id).state, 'expired'); pass();
    assert.ok(!card(await visitors('loc'), 'rex')); pass();
    assert.equal((await call(tokens.trav, `/api/v1/me/trips/${travLater.id}/cancel`, { method: 'POST' })).status, 204); pass();
    assert.equal((await call(tokens.trav, '/api/v1/me/trips')).body.items.find(t => t.id === travLater.id).state, 'cancelled'); pass();
    assert.deepEqual(code(await put('trav', travLater.id, { cityId: ADELAIDE, arrival: plus(20), departure: plus(22), visible: true, itineraryId: null })), [409, 'trip-cancelled']); pass();
    assert.equal((await call(tokens.trav, `/api/v1/me/trips/${travLater.id}`, { method: 'DELETE' })).status, 204); pass();
    assert.ok(!(await call(tokens.trav, '/api/v1/me/trips')).body.items.some(t => t.id === travLater.id)); pass();

    // Requests from the feed (rules 29–31): the local asks, the traveller answers; nothing connects on its own.
    const travTrip = card(await visitors('loc'), 'trav').visiting.tripId;
    r = await call(tokens.loc, '/api/v1/requests', { method: 'POST', body: { to: pid.trav, mode: 'dating', trip: travTrip } });
    assert.equal(r.status, 201); assert.deepEqual([r.body.visiting.city, r.body.visiting.arrival], ['Adelaide', plus(-3)]); pass();
    const received = (await call(tokens.trav, '/api/v1/requests?box=received')).body.items.find(x => x.id === r.body.id);
    assert.deepEqual([received.direction, received.status, received.visiting.tripId], ['received', 'pending', travTrip]); pass();
    assert.equal((await call(tokens.trav, `/api/v1/requests/${r.body.id}/accept`, { method: 'POST' })).body.status, 'accepted'); pass();
    // The trip must be the recipient's, visible to this local now; outsiders can't use it.
    assert.deepEqual(code(await call(tokens.loc, '/api/v1/requests', { method: 'POST', body: { to: pid.tia, mode: 'dating', trip: travTrip } })), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.far, '/api/v1/requests', { method: 'POST', body: { to: pid.tia, mode: 'dating', trip: tiaAdelaide.id } })), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.loc, '/api/v1/requests', { method: 'POST', body: { to: pid.trav, mode: 'friends', trip: fifteen.body.id } })), [404, 'not-available']); pass(); // a hidden trip
    assert.equal((await call(tokens.pal, '/api/v1/requests', { method: 'POST', body: { to: pid.tia, mode: 'friends', trip: tiaAdelaide.id } })).status, 201); pass();

    // Unlimited trips, paged (50 a page); writes are rate limited and audited without places or dates.
    await sql(`INSERT INTO trips (id, account_id, geo_name_id, arrival, departure, visible, created_at, updated_at)
      SELECT gen_random_uuid(), '${ids.nob}', ${MELBOURNE}, DATE '${plus(30)}' + g, DATE '${plus(30)}' + g, false, now(), now() FROM generate_series(1, 55) g`);
    const page1 = (await call(tokens.nob, '/api/v1/me/trips')).body, page2 = (await call(tokens.nob, '/api/v1/me/trips?page=2')).body;
    assert.deepEqual([page1.items.length, page1.more, page2.items.length, page2.more], [50, true, 5, false]); pass();
    assert.equal((await call(tokens.nob, '/api/v1/me/trips?page=0')).status, 400); pass();
    let status;
    for (let i = 0; i < 40 && status !== 429; i++) status = (await trip('nob', { cityId: MELBOURNE, arrival: plus(100 + i), departure: plus(100 + i), visible: false })).status;
    assert.equal(status, 429); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'trip.created' AND actor_account_id = '${ids.nob}'`), '30'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'trip.%' AND (detail LIKE '%Adelaide%' OR detail LIKE '%-%-%' OR detail LIKE '%${ADELAIDE}%')`), '0'); pass();

    // Web: the Travel screen (separate from the mode control) for a local and a traveller.
    const contexts = [];
    const signIn = async (member) => {
      const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
      contexts.push(context);
      const page = await context.newPage();
      await page.goto(web + '/account/sign-in?intro=done');
      await page.fill('#username', member.email); await page.fill('#password', member.password);
      await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
      return page;
    };
    const page = await signIn(members.pal);
    assert.equal(await page.locator('.settings-list a[href="/travel"]').count(), 1); pass(); // entry from Me
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-state=ready]', { timeout: 20000 });
    assert.equal(await page.locator('.explore-modes').getByText('Travel').count(), 0); pass(); // never in the mode control
    await page.click('[data-travel-link]');
    await page.waitForSelector('[data-visitors=ready]', { timeout: 20000 });
    const tiaRow = page.locator(`[data-visitor="${pid.tia}"]`);
    assert.match(await tiaRow.innerText(), /Visiting Adelaide, Australia/); pass();
    assert.equal(await page.locator('h2#visitors-heading').innerText(), 'Visiting Adelaide'); pass();
    assert.ok((await page.locator('.geonames-credit').first().innerText()).includes('Place data © GeoNames (CC BY 4.0)')); pass();
    if (output) await page.screenshot({ path: output + '/travel-local-390.png', fullPage: true });
    await page.locator(`[data-visitor="${pid.rex}"], [data-visitor="${pid.trav}"]`).first().getByRole('button', { name: 'Send request' }).click();
    await page.waitForSelector('[data-visitor-status=sent]'); pass();
    // Home city on the profile page, from the same search.
    await page.goto(web + '/me/profile');
    await page.waitForSelector('[data-profile-editor=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-city-picked]').getAttribute('data-city-picked'), String(GLENELG)); pass();
    await page.click('[data-city-search=home-city] .link-button');
    await page.fill('#home-city', 'adel');
    await page.click(`[data-city="${ADELAIDE}"]`);
    await page.click('[data-core-form] button[type=submit]');
    await page.waitForSelector('[data-core-status=saved]');
    assert.equal((await call(tokens.pal, '/api/v1/me/profile')).body.homeCity.id, ADELAIDE); pass();
    assert.ok((await page.locator('[data-city-search=home-city] .geonames-credit').innerText()).includes('GeoNames')); pass();
    // A traveller adds a trip through the form and sees no local feed.
    const travPage = await signIn(members.trav);
    await travPage.goto(web + '/travel');
    await travPage.waitForSelector('[data-travel=ready]', { timeout: 20000 });
    await travPage.waitForSelector('[data-visitors=not-local]'); pass();
    await travPage.fill('#trip-city', 'glenel');
    await travPage.click(`[data-city="${GLENELG}"]`);
    await travPage.fill('#arrival', plus(40)); await travPage.fill('#departure', plus(42));
    await travPage.click('[data-trip-form] button[type=submit]');
    await travPage.waitForSelector('[data-trip-status=saved]'); pass();
    const added = (await call(tokens.trav, '/api/v1/me/trips')).body.items.find(t => t.city.id === GLENELG);
    assert.deepEqual([added.arrival, added.departure, added.state], [plus(40), plus(42), 'planned']); pass();
    await travPage.locator(`[data-trip="${added.id}"]`).getByLabel('Visible to locals').uncheck();
    await travPage.getByText('Hidden. The trip is kept.').waitFor();
    assert.equal((await call(tokens.trav, '/api/v1/me/trips')).body.items.find(t => t.id === added.id).visible, false); pass();
    if (output) await travPage.screenshot({ path: output + '/travel-traveller-390.png', fullPage: true });
    for (const path of ['api/v1/places?q=adel', 'api/v1/me/trips', 'api/v1/travel/visitors?mode=dating'])
      assert.equal((await travPage.request.get(web + '/bff/' + path, { headers: { 'X-Swoovr-Bff': '1' } })).status(), path.includes('visitors') ? 403 : 200, path);
    pass();
    for (const context of contexts) await context.close();
    console.log(`PASS: Travel acceptance, ${passed} checks (GeoNames search with labels, ranking and folding; private home city; trips with mandatory destination-local dates, window, lifecycle, overlap and the shared changeover day; itineraries back to back opening together; others' trips and itineraries 404; tampered bodies; destination feed for locals only with city/country/dates, one card per traveller, modes both ways, hidden/expired/cancelled/blocked/inactive-market suppression; requests from the feed accepted by the traveller; paging, rate limit and an audit without places or dates; the Travel screen and home city in Edge).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
