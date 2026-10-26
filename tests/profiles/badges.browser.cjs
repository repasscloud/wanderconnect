// Local acceptance for badges, staff and official bots (Q11, D108): other people's badges from server records in the
// configured order, staff in discovery with a badge they can't hide, per-bot mode authorisation through audited admin
// routes, bots only where authorised and switched on, no Photo/ID badge for a bot, admin verified never returned.
// Needs the same local stack as modes.browser.cjs plus the enrolled fictional root admin (root-admin.browser.cjs).
// Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const people = await Promise.all(['vic', 'sam', 'fay', 'bo'].map(n => createMember('badges-' + n)));
    const [vic, sam, fay, bo] = people;
    await cli('beta', 'add', ...people.map(p => p.email));
    const tokens = {};
    for (const [key, member] of Object.entries({ vic, sam, fay, bo })) tokens[key] = await memberToken(browser, member);
    const root = await rootAdminToken(browser);
    const account = async key => sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[key]).sub}'`);
    const pid = {};
    const setUp = async (key, name, modes) => {
      await call(tokens[key], '/api/v1/me/profile', { method: 'PUT', body: { displayName: name, homeCityId: 2078025 } }); // Adelaide (D125)
      await matchReady(tokens[key]);
      for (const mode of modes) await call(tokens[key], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      pid[key] = (await call(tokens[key], '/api/v1/me/profile')).body.publicId;
    };
    await setUp('vic', 'Fictional Vic', ['dating', 'friends']);
    await setUp('sam', 'Fictional Sam', ['dating']);
    await setUp('fay', 'Fictional Fay', ['dating']);
    await call(tokens.bo, '/api/v1/session'); // creates the account record
    const ids = { sam: await account('sam'), fay: await account('fay'), bo: await account('bo'), vic: await account('vic') };
    const explore = async (mode, who = 'vic') => (await call(tokens[who], '/api/v1/explore?mode=' + mode)).body.people;
    const card = async (key, mode = 'dating') => (await explore(mode)).find(p => p.id === pid[key]);

    // Admin routes are for admins: members and anonymous callers are refused.
    for (const [method, path, body] of [['PUT', `/api/v1/admin/accounts/${ids.sam}/staff-affiliation`, { active: true }],
      ['PUT', `/api/v1/admin/accounts/${ids.bo}/bot`, { bot: true }], ['PUT', `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { modes: ['dating'] }],
      ['GET', `/api/v1/admin/accounts/${ids.bo}/bot-modes`]]) {
      assert.equal((await call(tokens.vic, path, { method, body })).status, 403, path); pass();
      assert.equal((await call(null, path, { method, body })).status, 401, path); pass();
    }

    // Staff (rules 27–37): an ordinary member in discovery, with the ruby badge while the affiliation is active.
    assert.deepEqual((await card('sam')).badges, []); pass(); // staff were excluded from Explore before Q11; now anyone
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.sam}/staff-affiliation`, { method: 'PUT', body: { active: true } })).status, 204); pass();
    assert.deepEqual((await card('sam')).badges.map(b => b.badge), ['staff']); pass();
    assert.deepEqual((await call(tokens.sam, '/api/v1/me')).body.badges.map(b => b.badge), ['staff']); pass();
    assert.deepEqual([(await call(tokens.sam, '/api/v1/session')).body].map(s => [s.adminArea, s.supportArea]), [[false, false]]); pass(); // the badge grants no role (rule 31)
    assert.equal((await call(tokens.sam, '/api/v1/admin/beta')).status, 403); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'staff.affiliated' AND target_id = '${ids.sam}' AND detail IS NULL`), '1'); pass();
    // No member route hides a badge: a profile save carrying a made-up field changes nothing (rule 34, 63).
    await call(tokens.sam, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Sam', homeCityId: 2078025, badges: [], employee: false, hideBadges: true } });
    assert.deepEqual((await card('sam')).badges.map(b => b.badge), ['staff']); pass();

    // Several badges at once (rule 57) from records; admin verified never leaves the server (rules 15, 64).
    await sql(`UPDATE member_profiles SET founding_member = true, photo_status = 'Verified', photo_checked_at = '2026-10-17T00:00:00Z',
      id_status = 'Verified', id_checked_at = '2026-10-18T00:00:00Z', admin_verified = true, admin_verified_at = now() WHERE account_id = '${ids.fay}'`);
    let fayCard = await card('fay');
    assert.deepEqual(fayCard.badges.map(b => b.badge), ['founder', 'photoVerified', 'idVerified']); pass();
    assert.ok(!JSON.stringify(fayCard).toLowerCase().includes('admin')); pass();
    let fayProfile = await call(tokens.vic, `/api/v1/profiles/${pid.fay}`);
    assert.deepEqual(fayProfile.body.badges.map(b => [b.badge, b.checkedAt?.slice(0, 10) ?? null]),
      [['founder', null], ['photoVerified', '2026-10-17'], ['idVerified', '2026-10-18']]); pass();
    assert.deepEqual(Object.keys(fayProfile.body).sort(), ['about', 'age', 'area', 'badges', 'displayName', 'gender', 'id', 'modes', 'pronouns']); pass(); // age, gender and pronouns only as allowed (D138, D139)
    assert.ok(!JSON.stringify(fayProfile.body).toLowerCase().includes('admin')); pass();
    await sql(`UPDATE member_profiles SET id_status = 'Expired' WHERE account_id = '${ids.fay}'`); // rule 13: an expired check shows nothing
    assert.deepEqual((await card('fay')).badges.map(b => b.badge), ['founder', 'photoVerified']); pass();
    await sql(`UPDATE member_profiles SET id_status = 'Verified' WHERE account_id = '${ids.fay}'`);
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.fay}/staff-affiliation`, { method: 'PUT', body: { active: true } })).status, 204);
    assert.deepEqual((await card('fay')).badges.map(b => b.badge), ['founder', 'photoVerified', 'idVerified', 'staff']); pass();

    // The order is data (rule 61): changing badge_definitions reorders every response without a code change.
    try {
      await sql(`UPDATE badge_definitions SET sort_order = CASE badge WHEN 'Staff' THEN 0 WHEN 'Founder' THEN 9 ELSE sort_order END`);
      assert.deepEqual((await card('fay')).badges.map(b => b.badge), ['staff', 'photoVerified', 'idVerified', 'founder']); pass();
      assert.deepEqual((await call(tokens.fay, '/api/v1/me')).body.badges.map(b => b.badge), ['staff', 'photoVerified', 'idVerified', 'founder']); pass();
    } finally {
      await sql(`UPDATE badge_definitions SET sort_order = CASE badge WHEN 'Founder' THEN 1 WHEN 'PhotoVerified' THEN 2 WHEN 'IdVerified' THEN 3 WHEN 'Staff' THEN 4 WHEN 'Bot' THEN 5 END`);
    }
    assert.deepEqual((await card('fay')).badges.map(b => b.badge), ['founder', 'photoVerified', 'idVerified', 'staff']); pass();

    // Official bots (rules 45–59): classified by an admin, authorised per mode, only the bot badge.
    await call(tokens.bo, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Guide Bo' } });
    let bad = await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes: ['dating'] } });
    assert.deepEqual([bad.status, bad.body.code], [404, 'not-a-bot']); pass(); // not a bot yet
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.bo}/bot`, { method: 'PUT', body: { bot: true } })).status, 204); pass();
    // A bot has no home city; it appears in the markets an admin assigns (D125 rule 38, tests/discovery/market.browser.cjs).
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } })).status, 200); pass();
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.bo}/staff-affiliation`, { method: 'PUT', body: { active: true } })).status, 409); pass(); // a bot is never staff
    bad = await call(tokens.bo, '/api/v1/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.deepEqual([bad.status, bad.body.code], [403, 'bot-mode-not-authorised']); pass(); // nothing authorised yet
    for (const modes of [['travel'], ['DATING'], Array.from({ length: 50 }, () => 'dating').concat(['x'])]) {
      bad = await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes } });
      assert.deepEqual([bad.status, bad.body.code], [400, 'unknown-mode'], JSON.stringify(modes).slice(0, 40)); pass();
    }
    let authorised = await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes: ['dating', 'dating'] } });
    assert.deepEqual([authorised.status, authorised.body.modes], [200, ['dating']]); pass();
    assert.deepEqual((await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`)).body.modes, ['dating']); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'bot.modes-authorised' AND target_id = '${ids.bo}' ORDER BY id DESC LIMIT 1`), 'dating'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'bot.classified' AND target_id = '${ids.bo}'`), '1'); pass();
    assert.equal((await call(tokens.bo, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } })).body.code, 'bot-mode-not-authorised'); pass();
    assert.equal((await call(tokens.bo, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: false, visible: false } })).status, 200); pass(); // switching off is always allowed
    pid.bo = (await call(tokens.bo, '/api/v1/me/profile')).body.publicId;
    assert.ok(!(await explore('dating')).some(p => p.id === pid.bo)); pass(); // authorised but not switched on
    assert.equal((await call(tokens.bo, '/api/v1/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } })).status, 200); pass();
    // Even with verification records and a founder flag, a bot shows only its bot badge (rules 54, 59).
    await sql(`UPDATE member_profiles SET founding_member = true, photo_status = 'Verified', id_status = 'Verified' WHERE account_id = '${ids.bo}'`);
    assert.deepEqual((await card('bo')).badges, [{ badge: 'bot', checkedAt: null }]); pass();
    assert.deepEqual((await call(tokens.bo, '/api/v1/me')).body.badges.map(b => b.badge), ['bot']); pass();
    assert.deepEqual((await call(tokens.vic, `/api/v1/profiles/${pid.bo}`)).body.modes.map(m => m.id), ['dating']); pass();
    assert.ok(!(await explore('friends')).some(p => p.id === pid.bo)); pass();
    // Even if its own setting says on, a bot without the authorisation is not shown (an admin's withdrawal wins at once).
    await sql(`INSERT INTO member_modes (account_id, mode_id, enabled, visible, updated_at) VALUES ('${ids.bo}', 'business', true, true, now())
      ON CONFLICT (account_id, mode_id) DO UPDATE SET enabled = true, visible = true`);
    await call(tokens.vic, '/api/v1/me/modes/business', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.ok(!(await explore('business')).some(p => p.id === pid.bo)); pass();
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes: [] } });
    assert.ok(!(await explore('dating')).some(p => p.id === pid.bo)); pass();
    assert.equal((await call(tokens.vic, `/api/v1/profiles/${pid.bo}`)).status, 404); pass();
    await call(root, `/api/v1/admin/accounts/${ids.bo}/bot-modes`, { method: 'PUT', body: { modes: ['dating', 'friends'] } });
    assert.ok((await explore('dating')).some(p => p.id === pid.bo)); pass();

    // Revoking the staff affiliation removes only the badge (rule 44); the member stays in discovery.
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.sam}/staff-affiliation`, { method: 'PUT', body: { active: false } })).status, 204);
    assert.deepEqual((await card('sam')).badges, []); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'staff.revoked' AND target_id = '${ids.sam}'`), '1'); pass();
    await call(root, `/api/v1/admin/accounts/${ids.sam}/staff-affiliation`, { method: 'PUT', body: { active: true } });

    // Web: tiles and the profile page draw exactly the API's badges, in its order.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', vic.email); await page.fill('#password', vic.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-state=ready]', { timeout: 20000 });
    const tileBadges = key => page.locator(`.profile-tile:has(a[href="/people/${pid[key]}"]) [data-badge]`).evaluateAll(els => els.map(e => e.dataset.badge));
    assert.deepEqual(await tileBadges('fay'), ['founding', 'photo', 'id', 'staff']); pass();
    assert.deepEqual(await tileBadges('sam'), ['staff']); pass();
    assert.deepEqual(await tileBadges('bo'), ['bot']); pass();
    assert.equal(await page.locator(`.profile-tile:has(a[href="/people/${pid.bo}"]) [data-badge=bot]`).getAttribute('aria-label'), 'Swoovr guide (bot)'); pass();
    if (output) await page.screenshot({ path: output + '/explore-badges-390.png', fullPage: true });
    await page.goto(web + `/people/${pid.fay}`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('.person-name .badge-row').getAttribute('data-badges'), 'founder photoVerified idVerified staff'); pass();
    assert.deepEqual(await page.locator('.person-name .badge-date').allInnerTexts(), ['checked 17 Oct 2026', 'checked 18 Oct 2026']); pass();
    if (output) await page.screenshot({ path: output + '/person-badges-390.png', fullPage: true });
    await context.close();
    console.log(`PASS: badges, staff and bots acceptance, ${passed} checks (admin-only audited routes; staff in discovery with a badge that grants no role and can't be hidden; several badges from records, expiry hides, admin verified never returned; badge order from data; bots classified and authorised per mode, refused elsewhere, only the bot badge, withdrawal hides at once; web tiles and profile draw the API's badges in order).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
