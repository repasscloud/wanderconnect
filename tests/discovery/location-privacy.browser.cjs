// Local acceptance for slice AE (internal policy §6, §10; #90, #91): location and mode privacy. A Plus member tries to
// pinpoint someone's home city by sweeping the distance filter a kilometre at a time: filters snap to bands, so the sweep
// learns nothing finer than the band, cards show a rounded band, and filter changes and searches stop at the hourly
// limits. The core "about" crosses into a mode only with consent. Profile versions: moderators see what was visible when
// a report was made (not later edits), and a legal hold preserves them. Negative cases: members on admin routes,
// tampered bodies, non-Plus distance, limits reached. Needs the local stack with the GeoNames import, the enrolled
// fictional root admin and hosts built from this checkout. Every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    // A suburb 6–9 km from Adelaide's centre, inside the Adelaide market.
    const near = await sql(`SELECT geo_name_id FROM geo_cities WHERE country_code = 'AU' AND geo_name_id <> ${ADELAIDE}
      AND sqrt(power((latitude + 34.92866) * 111.2, 2) + power((longitude - 138.59863) * 111.2 * cos(radians(34.93)), 2)) BETWEEN 6.2 AND 8.8
      ORDER BY population DESC LIMIT 1`);
    assert.ok(near, 'a GeoNames suburb 6–9 km from Adelaide');
    const names = ['plus', 'tgt', 'free'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('loc-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      pid[n] = (await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Loc ' + n, homeCityId: n === 'tgt' ? Number(near) : ADELAIDE,
        about: n === 'tgt' ? 'Fictional core about for the target' : null } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const in30 = new Date(Date.now() + 30 * 864e5).toISOString();
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.plus}/entitlements`, { method: 'POST', body: { tier: 'plus', endsAt: in30, reason: 'Fictional Plus for the location test' } })).status, 201);
    const filter = (who, km) => call(tokens[who], '/api/v1/me/modes/friends/filters', { method: 'PUT', body: { idVerifiedOnly: false, maxDistanceKm: km } });
    const explore = who => call(tokens[who], '/api/v1/explore?mode=friends');
    const target = r => r.body.people.find(p => p.id === pid.tgt);

    // A rounded band on the card, never a distance.
    let r = await explore('plus');
    assert.equal(r.status, 200); pass();
    assert.equal(target(r)?.distanceBand, '5–10 km'); pass();
    assert.ok(!JSON.stringify(r.body).match(/"(latitude|longitude|distanceKm)"/i), 'no coordinates or distances'); pass();
    assert.deepEqual(code(await filter('free', 10)), [403, 'plus-required']); pass();

    // The pinpointing attempt: 1 km at a time. Every filter snaps to its band and the answers only change at band edges.
    const presence = [];
    for (const km of [1, 2, 3, 4, 5, 6, 7, 8, 9]) {
      r = await filter('plus', km);
      assert.equal(r.status, 200, `filter ${km}`);
      assert.equal(r.body.maxDistanceKm, km <= 5 ? 5 : 10, `${km} km snaps to its band`);
      presence.push(!!target(await explore('plus')));
    }
    pass();
    assert.deepEqual(presence, [false, false, false, false, false, true, true, true, true], 'only "within 5" or "within 10" can be learnt'); pass();
    // The tenth filter change in the hour is the last; the eleventh is refused.
    assert.equal((await filter('plus', 25)).status, 200); pass();
    assert.deepEqual(code(await filter('plus', 50)), [429, 'search-limit']); pass();
    // Searches stop at 60 an hour (seeded in SQL, as the API is rate limited by design).
    await sql(`INSERT INTO search_events (account_id, kind, at) SELECT '${ids.plus}', 'search', now() FROM generate_series(1, 60)`);
    assert.deepEqual(code(await explore('plus')), [429, 'search-limit']); pass();
    assert.equal(await sql(`SELECT count(*) FROM search_events WHERE account_id = '${ids.plus}' AND kind = 'filter'`), '10', 'refused changes aren\'t counted'); pass();
    // Events older than a day go with `accounts purge`.
    await sql(`UPDATE search_events SET at = now() - interval '2 days' WHERE account_id = '${ids.plus}'`);
    await cli('accounts', 'purge');
    assert.equal(await sql(`SELECT count(*) FROM search_events WHERE account_id = '${ids.plus}'`), '0'); pass();

    // Consent before the core about crosses into a mode.
    const card = async () => target(await explore('free'));
    const person = async () => (await call(tokens.free, `/api/v1/profiles/${pid.tgt}?mode=friends`)).body;
    assert.equal((await card()).about, null, 'not shown without consent'); pass();
    assert.equal((await person()).modes[0].about, null); pass();
    const tampered = await fetch(api + '/api/v1/me/modes/friends', { method: 'PUT', headers: { Authorization: 'Bearer ' + tokens.tgt, 'Content-Type': 'application/json' }, body: '{"enabled":true,"visible":true,"shareCoreAbout":"yes"}' });
    assert.equal(tampered.status, 400); pass();
    r = await call(tokens.tgt, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, shareCoreAbout: true } });
    assert.equal(r.body.modes.find(m => m.id === 'friends').shareCoreAbout, true); pass();
    assert.equal((await card()).about, 'Fictional core about for the target', 'shown once agreed'); pass();
    r = await call(tokens.tgt, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.equal(r.body.modes.find(m => m.id === 'friends').shareCoreAbout, true, 'other saves keep the answer'); pass();
    await call(tokens.tgt, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, about: 'Fictional friends-only about' } });
    assert.equal((await person()).modes[0].about, 'Fictional friends-only about', 'the mode about wins'); pass();

    // Profile versions: the free member reports the target; then the target edits. Moderators see the earlier version.
    const versions = Number(await sql(`SELECT count(*) FROM profile_versions WHERE account_id = '${ids.tgt}'`));
    assert.ok(versions >= 3); pass();
    assert.equal(await sql(`SELECT count(*) FROM profile_versions WHERE account_id = '${ids.tgt}' AND (content_json LIKE '%1990%' OR content_json LIKE '%${near}%' OR content_json ILIKE '%showMe%')`), '0', 'no date of birth, home city or show-me'); pass();
    assert.equal((await call(tokens.free, '/api/v1/reports', { method: 'POST', body: { subjectType: 'profile', subjectId: pid.tgt, category: 'harassment', text: null, block: false } })).status, 201);
    const caseId = await sql(`SELECT id FROM moderation_cases WHERE subject_account_id = '${ids.tgt}' ORDER BY created_at DESC LIMIT 1`);
    await new Promise(resolve => setTimeout(resolve, 50));
    await call(tokens.tgt, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, about: 'Fictional edited after the report' } });
    r = await call(root, `/api/v1/admin/cases/${caseId}/profile-at-report`);
    assert.equal(r.status, 200); pass();
    const friends = r.body.versions.find(v => v.mode === 'friends');
    assert.ok(friends.contentJson.includes('Fictional friends-only about') && !friends.contentJson.includes('edited after'), 'the version visible at the report'); pass();
    assert.ok(r.body.versions.some(v => v.mode === 'core')); pass();
    assert.equal((await call(tokens.free, `/api/v1/admin/cases/${caseId}/profile-at-report`)).status, 403, 'members never'); pass();
    // A legal hold preserves the versions.
    const leg = await createStaff('loc-leg');
    assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: leg.email, role: 'legal' } })).status, 201);
    const legToken = await staffToken(browser, leg);
    r = await call(legToken, '/api/v1/admin/holds', { method: 'POST', body: { reason: 'Fictional hold for profile versions', accountIds: [ids.tgt] } });
    r = await call(legToken, `/api/v1/admin/holds/${r.body.id}`);
    assert.ok(r.body.items.filter(i => i.kind === 'profile-version').length >= versions, 'every version preserved'); pass();

    // Web: the consent box on the profile editor, and the admin app's case page.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.free.email); await page.fill('#password', members.free.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.goto(web + '/me/profile');
    const box = page.locator('[data-share-core="friends"]');
    await box.waitFor({ timeout: 20000 });
    assert.equal(await box.isChecked(), false, 'never assumed'); pass();
    await box.evaluate(b => b.click());
    await page.waitForFunction(() => document.querySelector('[data-share-core="friends"]')?.checked === true);
    for (let i = 0; i < 20 && await sql(`SELECT share_core_about FROM member_modes WHERE account_id = '${ids.free}' AND mode_id = 'friends'`) !== 't'; i++) await new Promise(res => setTimeout(res, 250));
    assert.equal(await sql(`SELECT share_core_about FROM member_modes WHERE account_id = '${ids.free}' AND mode_id = 'friends'`), 't'); pass();
    if (output) await page.screenshot({ path: output + '/web-mode-consent.png', fullPage: true });
    await context.close();
    const adminContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const adminPage = await adminContext.newPage();
    const adm = await createStaff('loc-adm');
    assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: adm.email, role: 'admin' } })).status, 201);
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', adm.email); await adminPage.fill('#password', adm.password); await adminPage.click('#kc-login');
    await adminPage.waitForSelector('[data-dashboard], [data-holds-overview]', { timeout: 20000 });
    await adminPage.goto(admin + `/cases/${caseId}`);
    await adminPage.waitForSelector('[data-profile-at-report]');
    assert.ok((await adminPage.locator('[data-profile-version="friends"] pre').innerText()).includes('friends-only about')); pass();
    await adminContext.close();

    console.log(`PASS: location and mode privacy (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
