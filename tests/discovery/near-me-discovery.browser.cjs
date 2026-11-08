// Local acceptance for #279 (D346, D348): Near me discovery and physical presence. A member from Melbourne (not an open
// market) is physically in Adelaide: with a fresh device reading they discover and contact Adelaide's people with no
// trip and no change to their home city. Near me lists the market's locals plus members appearing there by GPS
// ("Appear in nearby searches", checked live); GPS bands follow D348 (nearby needs fine, fresh readings, else unknown);
// blocks apply both ways; no coordinates or distances in any answer. Needs the local stack with the GeoNames import,
// the enrolled fictional root admin and hosts built from this checkout. Every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, memberToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    // loc: an Adelaide local; mel: from Melbourne, in Adelaide by GPS; app: from Melbourne, appearing in Adelaide;
    // hid: from Melbourne, in Adelaide but not appearing; blk: from Melbourne, appearing, who blocks mel.
    const names = ['loc', 'mel', 'app', 'hid', 'blk'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('nmd-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional NMD ' + n, homeCityId: n === 'loc' ? ADELAIDE : MELBOURNE } });
      assert.equal(saved.status, 200, n);
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const reading = (over = {}) => ({ latitude: -34.9285, longitude: 138.6007, accuracyMetres: 30, takenAt: new Date().toISOString(), ...over });
    const put = (who, body = reading()) => call(tokens[who], '/api/me/location', { method: 'PUT', body });
    const settings = (who, appearInNearby, showDistance) => call(tokens[who], '/api/me/location-settings', { method: 'PUT', body: { appearInNearby, showDistance } });
    const nearMe = who => call(tokens[who], '/api/explore?mode=friends&scope=nearMe');
    const ids_ = r => r.body.people.map(p => p.id);
    const card = (r, who) => r.body.people.find(p => p.id === pid[who]);
    const regate = who => sql(`UPDATE member_locations SET accepted_at = now() - interval '6 minutes' WHERE account_id = '${ids[who]}'`);

    // No reading yet: Near me asks for one; the active scope (home Melbourne) isn't open.
    assert.deepEqual(code(await nearMe('mel')), [409, 'location-required']); pass();
    assert.deepEqual(code(await call(tokens.mel, '/api/explore?mode=friends')), [403, 'market-not-open']); pass();
    assert.deepEqual(code(await call(tokens.mel, '/api/explore?mode=friends&scope=elsewhere')), [400, 'invalid-request']); pass();

    // In Adelaide by GPS: present, discovering locals and appearing members, not hidden ones.
    assert.equal((await put('mel')).body.presentIn, 'Adelaide'); pass();
    for (const who of ['app', 'blk']) { await settings(who, true, false); assert.equal((await put(who, reading({ latitude: -34.9285 + 5 / 111.32 }))).status, 200); }
    assert.equal((await put('hid')).status, 200);
    let r = await nearMe('mel');
    assert.deepEqual([r.status, r.body.market, r.body.scope], [200, 'Adelaide', 'nearMe']); pass();
    assert.ok(ids_(r).includes(pid.loc) && ids_(r).includes(pid.app)); pass(); // a local by home city, and a member appearing by GPS
    assert.ok(!ids_(r).includes(pid.hid)); pass(); // present, but not appearing
    assert.ok(!JSON.stringify(r.body).match(/latitude|longitude|distanceKm|-34\.9|138\.6/)); pass();

    // Contact with no trip; the home city and trips are untouched.
    assert.equal((await call(tokens.mel, `/api/profiles/${pid.loc}?mode=friends`)).status, 200); pass();
    r = await call(tokens.mel, '/api/requests', { method: 'POST', body: { to: pid.loc, mode: 'friends' } });
    assert.deepEqual([r.status, r.body.visiting ?? null], [201, null]); pass();
    assert.equal((await call(tokens.mel, '/api/me/profile')).body.homeCity.id, MELBOURNE); pass();
    assert.equal((await call(tokens.mel, '/api/me/trips')).body.items.length, 0); pass();
    assert.deepEqual(code(await call(tokens.mel, `/api/profiles/${pid.hid}?mode=friends`)), [404, 'unknown-mode']); pass(); // not appearing: not reachable by presence

    // GPS bands: hidden until both show their distance; ~5 km is within10Km by GPS; the same cell is nearby only when fine and fresh.
    assert.deepEqual(card(await nearMe('mel'), 'app').distance, { band: 'hidden', basis: null }); pass();
    await settings('mel', false, true); await settings('app', true, true);
    assert.deepEqual(card(await nearMe('mel'), 'app').distance, { band: 'within10Km', basis: 'gps' }); pass();
    assert.deepEqual(card(await nearMe('mel'), 'loc').distance.basis, null); pass(); // a local by home city: no GPS basis
    await regate('app');
    assert.equal((await put('app')).status, 200);
    assert.deepEqual(card(await nearMe('mel'), 'app').distance, { band: 'nearby', basis: 'gps' }); pass();
    await regate('app');
    assert.equal((await put('app', reading({ accuracyMetres: 900 }))).status, 200);
    assert.deepEqual(card(await nearMe('mel'), 'app').distance, { band: 'unknown', basis: null }); pass(); // coarse: never rounded to a band
    // Q154: the nearest visible band comes first; a member with a visible band sorts before those without one.
    await regate('app');
    assert.equal((await put('app')).status, 200);
    r = await nearMe('mel');
    assert.ok(ids_(r).indexOf(pid.app) < ids_(r).indexOf(pid.loc)); pass();

    // Appear off is checked live: gone from Near me at once.
    await settings('app', false, true);
    assert.ok(!ids_(await nearMe('mel')).includes(pid.app)); pass();

    // Blocks apply both ways.
    assert.ok(ids_(await nearMe('mel')).includes(pid.blk)); pass();
    assert.equal((await call(tokens.blk, '/api/blocks', { method: 'POST', body: { profileId: pid.mel } })).status, 204);
    assert.ok(!ids_(await nearMe('mel')).includes(pid.blk)); pass();

    // Two hours after the reading, presence ends: Near me asks again and new contact by presence stops.
    await sql(`UPDATE member_locations SET observed_at = now() - interval '121 minutes' WHERE account_id = '${ids.mel}'`);
    assert.deepEqual(code(await nearMe('mel')), [409, 'location-required']); pass();

    console.log(`PASS: Near me discovery (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
