// Local acceptance for slice T (D142, D149, D150, D153, D164): Traveller Search only through one's own live, visible
// trip within 14 days of arrival; browsing before arrival but no requests; confirm-arrival from a fresh device location
// checked by the API; contact after arrival; a changed or cancelled trip ending it; support confirming an arrival after
// review; home-location checks that flag for an admin's review and never punish; no coordinates, IP addresses or places
// stored or audited. Negative cases: other members' trips and ids, other markets, blocked pairs, restricted members,
// bots, staff, unknown and malformed ids, tampered bodies. Ends with the web Travel page in Edge with a granted
// geolocation. Same local stack as tests/discovery/market.browser.cjs (GeoNames full import); every member is a
// fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, GLENELG = 2070830, MELBOURNE = 2158177, DARWIN = 2073124;
const TEST_MARKET = 'test-darwin-t';
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = days => { const d = new Date(todayIn('Australia/Adelaide') + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const here = (extra = {}) => ({ latitude: -34.9285, longitude: 138.6007, accuracyMetres: 30, takenAt: new Date().toISOString(), ...extra });
const melbourne = (extra = {}) => here({ latitude: -37.8136, longitude: 144.9631, ...extra });

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  await sql(`DELETE FROM markets WHERE id = '${TEST_MARKET}'`); // a previous run that stopped early
  try {
    const root = await rootAdminToken(browser);
    // Locals in Adelaide (lou, lia, res restricted, blk who'll block, exc who excludes a chip), a Darwin local (dar),
    // travellers from Melbourne (tra arrives today, trb in 10 days, trc in 20, trd hidden, tre cancelled, trg no date
    // of birth, trh for the rate limit), an Adelaide local with an Adelaide "trip" (adl) and a bot.
    const names = ['lou', 'lia', 'res', 'blk', 'exc', 'dar', 'tra', 'trb', 'trc', 'trd', 'tre', 'trg', 'trh', 'adl', 'bot'];
    const homes = { dar: DARWIN, tra: MELBOURNE, trb: MELBOURNE, trc: MELBOURNE, trd: MELBOURNE, tre: MELBOURNE, trg: MELBOURNE, trh: MELBOURNE, adl: GLENELG, bot: null };
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('ts-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional TS ' + n, homeCityId: n in homes ? homes[n] : ADELAIDE } });
      assert.equal(saved.status, 200, n);
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'bot') continue;
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      if (n !== 'trg') await matchReady(tokens[n]);
    }
    // A second open market (Darwin), so "another market" is real.
    assert.equal((await call(root, `/api/v1/admin/markets/${TEST_MARKET}`, { method: 'PUT', body: { name: 'Darwin (test)', status: 'beta', areas: [{ centreCityId: DARWIN, radiusKm: 30 }], modes: ['dating', 'friends'] } })).status, 200); pass();
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } });
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot-modes`, { method: 'PUT', body: { modes: ['friends'] } });
    await call(tokens.bot, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });

    const trip = async (who, city, arrival, departure, visible = true) => {
      const r = await call(tokens[who], '/api/v1/me/trips', { method: 'POST', body: { cityId: city, arrival, departure, visible, itineraryId: null } });
      assert.equal(r.status, 201, who); return r.body;
    };
    const trips = {
      tra: await trip('tra', ADELAIDE, plus(0), plus(4)), trb: await trip('trb', GLENELG, plus(10), plus(12)), trc: await trip('trc', ADELAIDE, plus(20), plus(22)),
      trd: await trip('trd', ADELAIDE, plus(0), plus(3), false), tre: await trip('tre', ADELAIDE, plus(0), plus(3)), trg: await trip('trg', ADELAIDE, plus(0), plus(3)),
      trh: await trip('trh', ADELAIDE, plus(0), plus(3)), adl: await trip('adl', ADELAIDE, plus(0), plus(3)),
    };
    assert.equal((await call(tokens.tre, `/api/v1/me/trips/${trips.tre.id}/cancel`, { method: 'POST' })).status, 204);
    const search = (who, tripId, mode = 'friends') => call(tokens[who], `/api/v1/travel/search?trip=${tripId}&mode=${mode}`);
    const ids_ = r => r.body.people.map(p => p.id);

    // Trip views say where search and arrival stand.
    let r = await call(tokens.tra, '/api/v1/me/trips');
    let view = r.body.items.find(t => t.id === trips.tra.id);
    assert.deepEqual([view.arrivalState, view.arrivedAt, view.searchOpen, view.searchOpensOn], ['upcoming', null, true, plus(-14)]); pass();
    view = (await call(tokens.trc, '/api/v1/me/trips')).body.items[0];
    assert.deepEqual([view.searchOpen, view.searchOpensOn], [false, plus(6)]); pass();
    assert.equal((await call(tokens.adl, '/api/v1/me/trips')).body.items[0].searchOpen, false); pass(); // a local's own market

    // Traveller Search: only through one's own live, visible trip, from 14 days before arrival (Q41 rules 1–3, 8).
    r = await search('tra', trips.tra.id);
    assert.deepEqual([r.status, r.body.market, r.body.tripId, r.body.arrivalState, r.body.canRequest], [200, 'Adelaide', trips.tra.id, 'upcoming', false]); pass();
    assert.ok(ids_(r).includes(pid.lou) && ids_(r).includes(pid.lia) && ids_(r).includes(pid.bot)); pass();
    assert.ok(!ids_(r).includes(pid.dar) && !ids_(r).includes(pid.trb) && !ids_(r).includes(pid.tra)); pass(); // no other market, no other travellers
    assert.ok(r.body.people.every(p => !('dateOfBirth' in p) && !('homeCityId' in p))); pass();
    assert.equal((await search('trb', trips.trb.id)).status, 200); pass(); // 10 days ahead, a city in the market's area
    assert.deepEqual(code(await search('trc', trips.trc.id)), [403, 'search-not-open']); pass(); // 20 days ahead
    assert.deepEqual(code(await search('trd', trips.trd.id)), [403, 'search-not-open']); pass(); // hidden from locals
    assert.deepEqual(code(await search('tre', trips.tre.id)), [403, 'trip-not-live']); pass(); // cancelled
    assert.deepEqual(code(await search('adl', trips.adl.id)), [403, 'search-not-open']); pass(); // locals use Explore
    assert.deepEqual(code(await search('trg', trips.trg.id)), [403, 'date-of-birth-required']); pass();
    assert.deepEqual(code(await search('tra', trips.tra.id, 'dating')).slice(0, 1), [200]); pass();
    // Another member's trip, an unknown trip, no trip, a bad id, an unknown mode or Travel as a mode.
    assert.deepEqual(code(await search('trb', trips.tra.id)), [404, 'not-available']); pass();
    assert.deepEqual(code(await search('tra', '00000000-0000-0000-0000-000000000000')), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.tra, '/api/v1/travel/search?mode=friends')), [404, 'not-available']); pass();
    assert.equal((await call(tokens.tra, '/api/v1/travel/search?trip=nope&mode=friends')).status, 400); pass();
    assert.deepEqual(code(await search('tra', trips.tra.id, 'travel')), [404, 'unknown-mode']); pass();
    // A market can't be chosen: extra query parameters change nothing.
    r = await call(tokens.tra, `/api/v1/travel/search?trip=${trips.tra.id}&mode=friends&market=${TEST_MARKET}&cityId=${DARWIN}`);
    assert.ok(r.body.market === 'Adelaide' && !ids_(r).includes(pid.dar)); pass();

    // Before arrival: profiles open, requests don't (Q41 rule 4, D149 rule 2).
    r = await call(tokens.tra, `/api/v1/profiles/${pid.lou}`);
    assert.equal(r.status, 200); pass();
    assert.ok(r.body.modes.every(m => m.canRequest === false && m.arrivalRequired === true)); pass();
    assert.deepEqual(code(await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.lou, mode: 'friends' } })), [403, 'arrival-required']); pass();
    assert.deepEqual(code(await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.lou, mode: 'friends', trip: trips.tra.id } })), [403, 'arrival-required']); pass();
    assert.deepEqual(code(await call(tokens.tra, `/api/v1/profiles/${pid.dar}`)), [404, 'unknown-mode']); pass(); // another market
    assert.deepEqual(code(await call(tokens.trc, `/api/v1/profiles/${pid.lou}`)), [404, 'unknown-mode']); pass(); // search not open yet
    assert.deepEqual(code(await call(tokens.trc, '/api/v1/requests', { method: 'POST', body: { to: pid.lou, mode: 'friends' } })), [404, 'not-available']); pass();
    // Locals may still write to an incoming traveller (Q41 rule 5).
    r = await call(tokens.lia, '/api/v1/requests', { method: 'POST', body: { to: pid.trb, mode: 'dating' } });
    assert.equal(r.status, 201); pass();

    // Confirm-arrival (D149): the API decides; client flags never count.
    const arrive = (who, tripId, body) => call(tokens[who], `/api/v1/me/trips/${tripId}/arrival`, { method: 'POST', body });
    assert.deepEqual(code(await arrive('trb', trips.trb.id, here())), [403, 'arrival-too-early']); pass();
    assert.deepEqual(code(await arrive('tra', trips.tra.id, undefined)), [400, 'location-unusable']); pass();
    for (const bad of [here({ takenAt: new Date(Date.now() - 180000).toISOString() }), here({ takenAt: new Date(Date.now() + 60000).toISOString() }),
      here({ accuracyMetres: 5000 }), here({ accuracyMetres: 0 }), here({ latitude: 95 }), here({ latitude: 0, longitude: 0 })])
    { assert.deepEqual(code(await arrive('trh', trips.trh.id, bad)), [400, 'location-unusable']); pass(); } // trh's checks also feed the rate limit below
    r = await arrive('tra', trips.tra.id, melbourne({ arrived: true, arrivalState: 'arrived', marketId: 'adelaide' }));
    assert.deepEqual(code(r), [403, 'location-outside-market']); pass();
    view = (await call(tokens.tra, '/api/v1/me/trips')).body.items.find(t => t.id === trips.tra.id);
    assert.deepEqual([view.arrivalState, view.arrivedAt], ['unconfirmed', null]); pass();
    assert.equal((await search('tra', trips.tra.id)).body.canRequest, false); pass();
    assert.deepEqual(code(await arrive('trb', trips.tra.id, here())), [404, 'not-available']); pass(); // another member's trip
    assert.deepEqual(code(await arrive('tre', trips.tre.id, here())), [409, 'trip-not-live']); pass(); // cancelled
    assert.equal((await arrive('tra', 'not-a-guid', here())).status, 404); pass();
    // Not in the trip's market: a reading in Darwin (another open market) doesn't count for Adelaide.
    assert.deepEqual(code(await arrive('tra', trips.tra.id, here({ latitude: -12.4634, longitude: 130.8456 }))), [403, 'location-outside-market']); pass();
    r = await arrive('tra', trips.tra.id, here());
    assert.deepEqual([r.status, r.body.arrivalState, typeof r.body.arrivedAt], [200, 'arrived', 'string']); pass();
    assert.ok(!JSON.stringify(r.body).includes('-34.92') && !JSON.stringify(r.body).includes('138.60')); pass();
    assert.equal(await sql(`SELECT arrival_market_id || ':' || arrival_method FROM trips WHERE id = '${trips.tra.id}'`), 'adelaide:DeviceLocation'); pass();
    // A failed re-check never takes back an arrival.
    assert.deepEqual(code(await arrive('tra', trips.tra.id, melbourne())), [403, 'location-outside-market']); pass();
    assert.equal((await call(tokens.tra, '/api/v1/me/trips')).body.items.find(t => t.id === trips.tra.id).arrivalState, 'arrived'); pass();

    // After arrival: contact under the normal rules (Q41 rule 6), once, not per request.
    r = await search('tra', trips.tra.id);
    assert.deepEqual([r.body.arrivalState, r.body.canRequest], ['arrived', true]); pass();
    assert.ok((await call(tokens.tra, `/api/v1/profiles/${pid.lou}`)).body.modes.every(m => m.canRequest && !m.arrivalRequired)); pass();
    r = await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.lou, mode: 'friends', trip: trips.trb.id /* someone else's trip: ignored */ } });
    assert.deepEqual([r.status, r.body.visiting?.tripId], [201, trips.tra.id]); pass();
    const louRequest = r.body.id;
    r = await call(tokens.lou, '/api/v1/requests?box=received');
    assert.ok(r.body.items.some(i => i.id === louRequest && i.visiting?.city === 'Adelaide')); pass();
    assert.equal((await call(tokens.lou, `/api/v1/requests/${louRequest}/accept`, { method: 'POST' })).status, 200); pass();
    assert.deepEqual(code(await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.dar, mode: 'friends' } })), [404, 'not-available']); pass(); // still not Darwin
    assert.equal((await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.bot, mode: 'friends' } })).status, 201); pass(); // a bot assigned to Adelaide
    assert.deepEqual(code(await call(tokens.bot, '/api/v1/requests', { method: 'POST', body: { to: pid.tra, mode: 'friends' } })), [403, 'bot-cannot-initiate']); pass();

    // Safety rules still apply (rule 7): blocks, restrictions and excluded chips.
    assert.equal((await call(tokens.blk, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.tra } })).status, 204);
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the travel suite.', days: 2, hideFromDiscovery: true, noNewRequests: true, noMessages: false } })).status, 200);
    const modes = (await call(tokens.exc, '/api/v1/modes')).body;
    const chip = modes.find(m => m.id === 'friends').intentions[0].id;
    await call(tokens.tra, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, intentions: [chip] } });
    assert.equal((await call(tokens.exc, '/api/v1/me/modes/friends/filters', { method: 'PUT', body: { idVerifiedOnly: false, excludedChips: [chip] } })).status, 200);
    r = await search('tra', trips.tra.id);
    assert.ok(!ids_(r).includes(pid.blk) && !ids_(r).includes(pid.res) && !ids_(r).includes(pid.exc) && ids_(r).includes(pid.lia)); pass();
    for (const who of ['blk', 'res', 'exc'])
    { assert.deepEqual(code(await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid[who], mode: 'friends' } })), [404, 'not-available'], who); pass(); }
    assert.deepEqual(code(await search('res', trips.tra.id)), [403, 'account-restricted']); pass();

    // Rule 9: new dates need a new arrival; the connection stays.
    r = await call(tokens.tra, `/api/v1/me/trips/${trips.tra.id}`, { method: 'PUT', body: { cityId: ADELAIDE, arrival: plus(0), departure: plus(6), visible: true, itineraryId: null } });
    assert.deepEqual([r.status, r.body.arrivalState], [200, 'upcoming']); pass();
    assert.deepEqual(code(await call(tokens.tra, '/api/v1/requests', { method: 'POST', body: { to: pid.lia, mode: 'friends' } })), [403, 'arrival-required']); pass();
    assert.equal((await call(tokens.tra, `/api/v1/me/trips/${trips.tra.id}`, { method: 'PUT', body: { cityId: ADELAIDE, arrival: plus(0), departure: plus(6), visible: false, itineraryId: null } })).body.arrivalState, 'upcoming'); pass();
    assert.equal((await arrive('tra', trips.tra.id, here())).body.arrivalState, 'arrived'); pass();
    r = await call(tokens.tra, `/api/v1/me/trips/${trips.tra.id}`, { method: 'PUT', body: { cityId: ADELAIDE, arrival: plus(0), departure: plus(6), visible: true, itineraryId: null } });
    assert.equal(r.body.arrivalState, 'arrived'); pass(); // visibility alone keeps it
    // Rule 12 and Q41 rule 9: cancelling ends search and contact; connections stay.
    assert.equal((await call(tokens.tra, `/api/v1/me/trips/${trips.tra.id}/cancel`, { method: 'POST' })).status, 204);
    assert.deepEqual(code(await search('tra', trips.tra.id)), [403, 'trip-not-live']); pass();
    assert.deepEqual(code(await call(tokens.tra, `/api/v1/profiles/${pid.lia}`)), [404, 'unknown-mode']); pass();
    assert.equal((await call(tokens.tra, `/api/v1/profiles/${pid.lou}`)).status, 200); pass(); // connected
    assert.ok((await call(tokens.tra, '/api/v1/connections')).body.items.some(i => i.id === louRequest)); pass();

    // The rate limit: ten location checks an hour per member, refused ones included (D164); trh has used six.
    for (let i = 0; i < 4; i++) await arrive('trh', trips.trh.id, melbourne());
    assert.deepEqual(code(await arrive('trh', trips.trh.id, here())), [429, 'too-many-attempts']); pass();

    // Support confirms an arrival after a review (rule 7); members can't.
    const staff = await createStaff('ts-sup');
    assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff);
    assert.equal((await call(tokens.trd, `/api/v1/admin/trips/${trips.trd.id}/arrival`, { method: 'POST', body: { reason: 'x' } })).status, 403); pass();
    assert.equal((await call(sup, `/api/v1/admin/trips/${trips.trd.id}/arrival`, { method: 'POST', body: { reason: ' ' } })).status, 400); pass();
    assert.deepEqual(code(await call(sup, `/api/v1/admin/trips/${trips.trb.id}/arrival`, { method: 'POST', body: { reason: 'Fictional review.' } })), [403, 'arrival-too-early']); pass();
    assert.deepEqual(code(await call(sup, `/api/v1/admin/trips/${trips.tre.id}/arrival`, { method: 'POST', body: { reason: 'Fictional review.' } })), [409, 'trip-not-live']); pass();
    assert.equal((await call(sup, '/api/v1/admin/trips/00000000-0000-0000-0000-000000000000/arrival', { method: 'POST', body: { reason: 'Fictional review.' } })).status, 404); pass();
    assert.equal((await call(sup, `/api/v1/admin/trips/${trips.trd.id}/arrival`, { method: 'POST', body: { reason: 'Fictional review: location unavailable.' } })).status, 204); pass();
    r = await call(sup, `/api/v1/admin/accounts/${ids.trd}/trips`);
    assert.deepEqual([r.status, r.body[0].arrivalState, r.body[0].arrivalMethod, r.body[0].market, 'city' in r.body[0]], [200, 'arrived', 'supportReview', 'adelaide', false]); pass();
    assert.equal((await call(tokens.trd, '/api/v1/admin/accounts/' + ids.trd + '/trips')).status, 403); pass();

    // Home-location checks (D150): consistent inside, inconsistent outside, flagged for review after three days, never punished.
    const home = (who, body, headers = {}) => fetch('http://127.0.0.1:5080/api/v1/me/home-check', { method: 'POST',
      headers: { Authorization: 'Bearer ' + tokens[who], 'Content-Type': 'application/json', ...headers }, body: JSON.stringify(body) })
      .then(async res => ({ status: res.status, body: await res.json().catch(() => null) }));
    assert.deepEqual([(await home('lou', here())).body.outcome], ['consistent']); pass();
    assert.deepEqual(code(await home('lou', here({ accuracyMetres: 9000 }))), [400, 'location-unusable']); pass();
    assert.deepEqual((await home('trb', here())).body.outcome, 'not-checked'); pass(); // Melbourne is in no market
    assert.deepEqual((await home('lia', melbourne(), { 'cf-iplatitude': '-37.8', 'cf-iplongitude': '144.9' })).body.outcome, 'inconsistent'); pass();
    assert.equal(await sql(`SELECT count(*) FROM home_location_checks WHERE account_id = '${ids.lia}' AND method = 'ApproximateIp'`), '0'); pass(); // headers ignored locally
    await sql(`INSERT INTO home_location_checks (id, account_id, checked_at, method, home_market_id, outcome) VALUES
      (gen_random_uuid(), '${ids.lia}', now() - interval '2 days', 'Device', 'adelaide', 'Inconsistent'), (gen_random_uuid(), '${ids.lia}', now() - interval '1 day', 'Device', 'adelaide', 'Inconsistent')`);
    assert.equal((await call(sup, `/api/v1/admin/accounts/${ids.lia}/summary`)).body.homeReviewFlaggedAt, null); pass();
    assert.equal((await home('lia', melbourne())).body.outcome, 'inconsistent'); pass();
    r = await call(sup, `/api/v1/admin/accounts/${ids.lia}/summary`);
    assert.ok(r.body.homeReviewFlaggedAt && r.body.inconsistentHomeChecks === 4 && r.body.status === 'active'); pass();
    assert.equal((await call(tokens.lia, '/api/v1/explore?mode=friends')).status, 200); pass(); // never suspended or hidden
    assert.equal((await call(tokens.lou, `/api/v1/profiles/${pid.lia}`)).status, 200); pass();
    assert.equal((await call(sup, `/api/v1/admin/accounts/${ids.lia}/home-review/clear`, { method: 'POST', body: { reason: 'x' } })).status, 403); pass(); // admins only
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.lia}/home-review/clear`, { method: 'POST', body: { reason: '' } })).status, 400); pass();
    r = await call(root, `/api/v1/admin/accounts/${ids.lia}/home-review/clear`, { method: 'POST', body: { reason: 'Fictional: moved recently.' } });
    assert.deepEqual([r.status, r.body.homeReviewFlaggedAt, r.body.inconsistentHomeChecks], [200, null, 0]); pass();
    // A member with a confirmed arrival elsewhere is travelling: their home check isn't counted.
    const darTrip = await trip('dar', ADELAIDE, plus(0), plus(2));
    assert.equal((await arrive('dar', darTrip.id, here())).body.arrivalState, 'arrived'); pass();
    assert.equal((await home('dar', here())).body.outcome, 'not-checked'); pass();

    // Nothing about where anyone is reaches the audit log or the schema.
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail ~ '(-3[47]\\.[0-9]|13[08]\\.[0-9]|144\\.9|Melbourne|Adelaide, South)'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM information_schema.columns WHERE table_name IN ('trips', 'home_location_checks') AND (column_name ~ '(lat|lon|accuracy|ip_|address|city)')`), '0'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE action IN ('trip.arrived', 'trip.arrival-refused', 'trip.arrival-reviewed', 'home-check.recorded', 'home-review.flagged', 'home-review.cleared')`)) >= 6); pass();

    // Web: the Travel page in Edge with the browser's location permission (D152).
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, geolocation: { latitude: -34.9285, longitude: 138.6007, accuracy: 20 }, permissions: ['geolocation'] });
    const page = await context.newPage();
    const webTrip = await trip('trb', ADELAIDE, plus(0), plus(2));
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.trb.email); await page.fill('#password', members.trb.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff(`api/v1/travel/search?trip=${webTrip.id}&mode=friends`)).status(), 200); pass();
    assert.equal((await bff('api/v1/admin/accounts/' + ids.trb + '/trips')).status(), 404); pass(); // no admin routes on the BFF
    await page.goto(web + '/travel');
    await page.waitForSelector(`[data-confirm-arrival="${webTrip.id}"]`);
    assert.ok(await page.locator(`[data-search-trip="${webTrip.id}"]`).isVisible()); pass();
    await page.click(`[data-confirm-arrival="${webTrip.id}"]`);
    await page.waitForSelector('[data-trip-status="arrived"]');
    assert.match(await page.locator('[data-trip-status]').innerText(), /You've arrived/); pass();
    assert.equal(await page.locator(`[data-trip="${webTrip.id}"] [data-arrival]`).getAttribute('data-arrival'), 'arrived'); pass();
    await page.click(`[data-search-trip="${webTrip.id}"]`);
    await page.waitForSelector('[data-search="ready"]');
    assert.equal(await page.locator('[data-arrival]').first().getAttribute('data-arrival'), 'arrived'); pass();
    assert.match(await page.locator('h1').innerText(), /People in Adelaide/); pass();
    assert.ok(await page.locator(`a[href="/people/${pid.lou}"]`).count() >= 1); pass();
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= 390)); pass();
    // Another member's trip in the address bar: refused like any other unknown trip.
    await page.goto(web + `/travel/search/${trips.trc.id}`);
    await page.waitForSelector('[data-search="refused"]');
    assert.equal(await page.locator('[data-search-refused]').getAttribute('data-search-refused'), 'not-available'); pass();
    await context.close();

    console.log(`PASS: Traveller Search and arrival (${passed} checks).`);
  } finally {
    await sql(`DELETE FROM markets WHERE id = '${TEST_MARKET}'`);
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
