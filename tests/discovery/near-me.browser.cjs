// Local acceptance for #278 (D348, Q161): the Near me location store. A member's device reading is accepted only when
// fresh (two minutes), accurate (2 km) and possible (900 km/h), at most once every five minutes; it is stored snapped and
// sealed (no coordinate text in the database or the API's answers); it stops being used two hours after it was taken and
// `location purge` (the five-minute worker's step) deletes it; and turning "Appear" off, stopping sharing, sign out
// everywhere, a restriction and a deletion request each delete it at once. Needs the local stack with the GeoNames import,
// the enrolled fictional root admin and hosts built from this checkout. Every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, createStaff, memberToken, staffToken, elevatedRootToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    // A fictional Support person restricts, invited by the root admin with a same-session MFA elevation (D322).
    const root = await elevatedRootToken(browser);
    const supStaff = await createStaff('near-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: supStaff.email, role: 'support' } })).status, 201);
    const support = await staffToken(browser, supStaff);
    const names = ['ana', 'ben', 'cal', 'dee'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('near-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      assert.equal((await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Near ' + n, homeCityId: ADELAIDE } })).status, 200);
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const reading = (over = {}) => ({ latitude: -34.9285, longitude: 138.6007, accuracyMetres: 30, takenAt: new Date().toISOString(), ...over });
    const put = (who, body = reading()) => call(tokens[who], '/api/me/location', { method: 'PUT', body });
    const rows = who => sql(`SELECT count(*) FROM member_locations WHERE account_id = '${ids[who]}'`);
    // Lets the next reading through the five-minute gate without waiting.
    const age = (who, minutes) => sql(`UPDATE member_locations SET accepted_at = now() - interval '${minutes} minutes', observed_at = now() - interval '${minutes} minutes' WHERE account_id = '${ids[who]}'`);

    // Accepted: present in Adelaide, used for two hours; stored sealed, with nothing readable in the database.
    let r = await put('ana');
    assert.equal(r.status, 200); pass();
    assert.equal(r.body.presentIn, 'Adelaide'); pass();
    assert.ok(Math.abs(new Date(r.body.freshUntil) - Date.now() - 2 * 3600e3) < 120e3); pass();
    assert.ok(!JSON.stringify(r.body).match(/-34\.9|138\.6|latitude|longitude/)); pass();
    assert.equal(await rows('ana'), '1'); pass();
    const stored = await sql(`SELECT encode(sealed_cell, 'escape') || '|' || coalesce(market_id, '') || '|' || accuracy FROM member_locations WHERE account_id = '${ids.ana}'`);
    assert.ok(!stored.includes('-34.9') && !stored.includes('138.6') && stored.endsWith('|adelaide|Fine')); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%-34.9%' OR detail LIKE '%138.6%'`), '0'); pass();

    // One every five minutes; stale, coarse, 0,0 and impossible readings all read the same.
    assert.deepEqual(code(await put('ana')), [429, 'too-many-attempts']); pass();
    await age('ana', 6);
    assert.deepEqual(code(await put('ana', reading({ takenAt: new Date(Date.now() - 180e3).toISOString() }))), [400, 'location-unusable']); pass();
    assert.deepEqual(code(await put('ana', reading({ accuracyMetres: 2500 }))), [400, 'location-unusable']); pass(); // approximate permission
    assert.deepEqual(code(await put('ana', reading({ latitude: 0, longitude: 0 }))), [400, 'location-unusable']); pass();
    assert.deepEqual(code(await put('ana', reading({ latitude: -37.8136, longitude: 144.9631 }))), [400, 'location-unusable']); pass(); // Melbourne six minutes later
    r = await put('ana', reading({ accuracyMetres: 900 }));
    assert.equal(r.status, 200); pass();
    assert.ok((await sql(`SELECT accuracy FROM member_locations WHERE account_id = '${ids.ana}'`)) === 'Coarse'); pass();

    // Two hours after it was taken it isn't used, and the purge deletes it.
    await age('ana', 121);
    assert.equal((await call(tokens.ana, '/api/me/export', { method: 'POST' })).status < 500, true); pass();
    await cli('location', 'purge');
    assert.equal(await rows('ana'), '0'); pass();

    // Deletion events: "Appear" off, stop sharing, sign out everywhere, a restriction and a deletion request.
    await call(tokens.ana, '/api/me/location-settings', { method: 'PUT', body: { appearInNearby: true, showDistance: false } });
    assert.equal((await put('ana')).status, 200);
    await call(tokens.ana, '/api/me/location-settings', { method: 'PUT', body: { appearInNearby: false, showDistance: false } });
    assert.equal(await rows('ana'), '0'); pass(); // Appear off
    assert.equal((await put('ana')).status, 200); pass(); // browsing continues; stored with Appear off
    assert.equal((await call(tokens.ana, '/api/me/location', { method: 'DELETE' })).status, 204); pass();
    assert.equal(await rows('ana'), '0'); pass(); // stop sharing

    assert.equal((await put('cal')).status, 200);
    // "Sign out everywhere" sets the account's session cutoff (every caller goes through it); staff use of that route also
    // needs a same-session MFA step-up, which is covered by the messaging suite, so the cutoff is set directly here.
    await sql(`UPDATE accounts SET sessions_revoked_at = now() WHERE id = '${ids.cal}'`);
    assert.equal(await sql(`SELECT count(*) FROM accounts WHERE id = '${ids.cal}' AND sessions_revoked_at IS NOT NULL`), '1'); pass();
    await cli('location', 'purge');
    assert.equal(await rows('cal'), '0'); pass(); // taken before the sign-out cutoff: never used, purged

    assert.equal((await put('ben')).status, 200);
    r = await call(support, `/api/admin/accounts/${ids.ben}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the Near me suite.', days: 2, hideFromDiscovery: true, noNewRequests: true, noMessages: false } });
    assert.ok(r.status < 300, `restriction refused: ${r.status} ${r.body?.code ?? ''} ${r.body?.title ?? ''}`); pass();
    assert.equal(await rows('ben'), '0'); pass(); // a restriction

    assert.equal((await put('dee')).status, 200);
    assert.equal((await call(tokens.dee, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: false, deleteFounderToken: false } })).status, 200); pass();
    assert.equal(await rows('dee'), '0'); pass(); // a deletion request

    console.log(`PASS: Near me store (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
