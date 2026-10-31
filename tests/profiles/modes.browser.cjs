// Local acceptance for connection modes (D92): the API with PostgreSQL and real local Keycloak tokens, then the
// Interactive WebAssembly profile, Explore and person pages through the backend-for-frontend in actual Edge.
// Needs the local stack, the API on 127.0.0.1:5080 with its database, the web host on localhost:5081 and a Debug API
// build for operator commands. Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const [ana, ben, cai, dee, eli] = await Promise.all(['ana', 'ben', 'cai', 'dee', 'eli'].map(n => createMember('modes-' + n)));
    await cli('beta', 'add', ana.email, ben.email, cai.email, eli.email); // dee is not on the beta
    const tokens = {};
    for (const [key, member] of Object.entries({ ana, ben, cai, dee, eli })) tokens[key] = await memberToken(browser, member);

    // Catalogue: four stable ids in order, labels from data, ten intention chips each (Q16), no Travel.
    let modes = await call(tokens.ana, '/api/modes');
    assert.equal(modes.status, 200);
    assert.deepEqual(modes.body.map(m => [m.id, m.label]), [['dating', 'Dating'], ['friends', 'Friends'], ['activities', 'Activities'], ['business', 'Business']]); pass();
    assert.ok(modes.body.every(m => m.intentions.length === 10 && m.maxIntentions === 5)); pass();
    assert.deepEqual([modes.body[0].intentions[0], modes.body[3].intentions[9]], [
      { id: '00000016-0001-4000-8000-000000000001', label: 'Long-term relationship' }, { id: '00000016-0004-4000-8000-000000000010', label: 'Knowledge sharing' }]); pass();
    const chip = (mode, n) => modes.body.find(m => m.id === mode).intentions[n - 1].id;
    assert.equal((await call(null, '/api/modes')).status, 401); pass();
    assert.equal((await call(tokens.dee, '/api/modes')).body.code, 'beta-not-enrolled'); pass();

    // Own profile starts empty with every mode off.
    let mine = await call(tokens.ana, '/api/me/profile');
    assert.equal(mine.status, 200); assert.equal(mine.body.displayName, null);
    assert.deepEqual(mine.body.modes.map(m => [m.id, m.enabled, m.visible]), [['dating', false, false], ['friends', false, false], ['activities', false, false], ['business', false, false]]); pass();

    // Core profile: trimmed, limits and control characters refused, nothing stored in the audit log.
    mine = await call(tokens.ana, '/api/me/profile', { method: 'PUT', body: { displayName: '  Fictional Ana ', area: 'Adelaide CBD', about: 'Fictional adult.\nLikes walks.', homeCityId: 2078025 } });
    await matchReady(tokens.ana);
    assert.equal(mine.status, 200); assert.equal(mine.body.displayName, 'Fictional Ana'); pass();
    for (const body of [{ displayName: 'x'.repeat(41) }, { area: 'y'.repeat(61) }, { about: 'z'.repeat(501) }, { displayName: 'Ana\u0007' }, { area: 'Adel\naide' }]) {
      const bad = await call(tokens.ana, '/api/me/profile', { method: 'PUT', body });
      assert.deepEqual([bad.status, bad.body.code], [400, 'invalid-profile']); pass();
    }
    assert.equal((await call(tokens.ana, '/api/me/profile')).body.displayName, 'Fictional Ana'); pass(); // refusals changed nothing
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'profile.updated' AND detail IS NOT NULL`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%Fictional Ana%' OR detail LIKE '%Adelaide CBD%'`), '0'); pass();

    // Modes: several at once, each independent; unknown modes, Travel and made-up intentions refused.
    mine = await call(tokens.ana, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true, intentions: [] } });
    mine = await call(tokens.ana, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: false } });
    assert.deepEqual(mine.body.modes.map(m => [m.id, m.enabled, m.visible]), [['dating', true, true], ['friends', true, false], ['activities', false, false], ['business', false, false]]); pass();
    mine = await call(tokens.ana, '/api/me/modes/friends', { method: 'PUT', body: { enabled: false, visible: false } });
    assert.deepEqual(mine.body.modes.find(m => m.id === 'dating'), { id: 'dating', enabled: true, visible: true, intentions: [], about: null, shareCoreAbout: false }); pass(); // turning one off leaves the other
    for (const id of ['travel', 'connect', 'DATING']) {
      const bad = await call(tokens.ana, `/api/me/modes/${id}`, { method: 'PUT', body: { enabled: true, visible: true } });
      assert.deepEqual([bad.status, bad.body?.code], [404, 'unknown-mode'], id); pass();
    }
    let bad = await call(tokens.ana, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true, intentions: ['00000000-0000-0000-0000-000000000001'] } });
    assert.deepEqual([bad.status, bad.body.code], [400, 'invalid-mode']); pass();
    assert.equal((await call(tokens.dee, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } })).status, 403); pass();

    // Intentions (Q16): up to five per mode, only the mode's own chips, independent per mode, kept while a mode is off.
    const save = (who, mode, body) => call(tokens[who], `/api/me/modes/${mode}`, { method: 'PUT', body });
    const datingFive = [1, 2, 3, 7, 10].map(n => chip('dating', n));
    mine = await save('ana', 'dating', { enabled: true, visible: true, intentions: datingFive });
    assert.deepEqual(mine.body.modes.find(m => m.id === 'dating').intentions, datingFive); pass();
    bad = await save('ana', 'dating', { enabled: true, visible: true, intentions: [...datingFive, chip('dating', 4)] });
    assert.deepEqual([bad.status, bad.body.code], [400, 'too-many-intentions']); pass(); // six, past the client's limit
    bad = await save('ana', 'dating', { enabled: true, visible: true, intentions: Array.from({ length: 400 }, (_, i) => `00000016-0001-4000-8000-${String(i).padStart(12, '0')}`) });
    assert.equal(bad.status, 400); pass(); // a tampered body far past the limit
    bad = await save('ana', 'dating', { enabled: true, visible: true, intentions: [chip('friends', 1)] });
    assert.deepEqual([bad.status, bad.body.code], [400, 'invalid-mode']); pass(); // another mode's chip
    mine = await save('ana', 'dating', { enabled: true, visible: true, intentions: [...datingFive, datingFive[0], datingFive[1]] });
    assert.equal(mine.status, 200); assert.equal(mine.body.modes.find(m => m.id === 'dating').intentions.length, 5); pass(); // duplicates count once
    assert.deepEqual((await call(tokens.ana, '/api/me/profile')).body.modes.find(m => m.id === 'dating').intentions, datingFive); pass(); // refusals changed nothing
    mine = await save('ana', 'friends', { enabled: false, visible: false, intentions: [chip('friends', 1), chip('friends', 9)] });
    assert.deepEqual(mine.body.modes.find(m => m.id === 'friends').intentions, [chip('friends', 1), chip('friends', 9)]); pass(); // chosen while off
    assert.deepEqual(mine.body.modes.find(m => m.id === 'dating').intentions, datingFive); pass(); // other mode untouched
    assert.match(await sql(`SELECT detail FROM audit_entries WHERE action = 'mode.updated' AND target_id = 'friends' ORDER BY id DESC LIMIT 1`), /^off:hidden:intentions:2$/); pass();

    // Ben: dating on and visible. Cai: dating on but hidden. Eli: dating visible but no display name.
    await call(tokens.ben, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Ben', area: 'Glenelg', homeCityId: 2078025 } });
    await matchReady(tokens.ben);
    await call(tokens.ben, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
    await call(tokens.ben, '/api/me/modes/business', { method: 'PUT', body: { enabled: true, visible: true } });
    await call(tokens.cai, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Cai', homeCityId: 2078025 } });
    await matchReady(tokens.cai);
    await call(tokens.cai, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: false } });
    await call(tokens.eli, '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });

    // Explore needs the mode on for the viewer; shows only visible, named, eligible people; never the viewer.
    // Matched by public id: earlier runs leave their own fictional members with the same names.
    const pid = {};
    for (const key of Object.keys(tokens)) if (key !== 'dee') pid[key] = (await call(tokens[key], '/api/me/profile')).body.publicId;
    const ids = r => r.body.people.map(p => p.id);
    let explore = await call(tokens.ana, '/api/explore?mode=dating');
    assert.equal(explore.status, 200); assert.ok(ids(explore).includes(pid.ben)); pass();
    assert.ok(!ids(explore).includes(pid.cai)); pass(); // hidden
    assert.ok(!ids(explore).includes(pid.ana)); pass(); // self
    assert.ok(!ids(explore).includes(pid.eli) && explore.body.people.every(p => p.displayName)); pass(); // eli has no name yet
    const benCard = explore.body.people.find(p => p.id === pid.ben);
    assert.deepEqual(Object.keys(benCard).sort(), ['about', 'age', 'area', 'badges', 'displayName', 'distanceBand', 'gender', 'id', 'intentions', 'photo', 'pronouns']); pass(); // D203: the main photo (null without one) // #90: a rounded band, never a distance // age, gender and pronouns only as allowed (D138, D139); never the date of birth, email or account id
    assert.ok(!JSON.stringify(benCard).includes('1990-') && !('dateOfBirth' in benCard)); pass();
    assert.deepEqual(benCard.badges, []); pass(); // an ordinary member has no public badge (Q11)
    // Intentions show on tiles in the owner's order and never exclude anyone (Q16 rule 10): Ben picked none at first.
    await save('ben', 'dating', { enabled: true, visible: true, intentions: [chip('dating', 4), chip('dating', 2)] });
    let benTile = (await call(tokens.ana, '/api/explore?mode=dating')).body.people.find(p => p.id === pid.ben);
    assert.deepEqual(benTile.intentions, ['Short-term dating', 'Something casual / no strings']); pass();
    // D113: a chip retired after Ben picked it stays stored but hidden, doesn't block a save and can't be added afresh.
    const retired = chip('dating', 4); // Ana doesn't hold this one
    try {
      await sql(`UPDATE mode_intentions SET available = false WHERE id = '${retired}'`);
      benTile = (await call(tokens.ana, '/api/explore?mode=dating')).body.people.find(p => p.id === pid.ben);
      assert.deepEqual(benTile.intentions, ['Short-term dating']); pass();
      assert.deepEqual((await call(tokens.ben, '/api/me/profile')).body.modes.find(m => m.id === 'dating').intentions, [chip('dating', 2)]); pass();
      assert.ok(!(await call(tokens.ben, '/api/modes')).body[0].intentions.some(i => i.id === retired)); pass();
      const benFive = [1, 2, 3, 5, 6].map(n => chip('dating', n));
      assert.equal((await save('ben', 'dating', { enabled: true, visible: true, intentions: benFive })).status, 200); pass(); // five plus the hidden one
      assert.equal(await sql(`SELECT count(*) FROM member_mode_intentions i JOIN accounts a ON a.id = i.account_id WHERE a.subject = '${claims(tokens.ben).sub}' AND i.intention_id = '${retired}'`), '1'); pass();
      bad = await save('ana', 'dating', { enabled: true, visible: true, intentions: [retired] });
      assert.deepEqual([bad.status, bad.body.code], [400, 'invalid-mode']); pass(); // adding a retired chip is refused
      await save('ben', 'dating', { enabled: true, visible: true, intentions: [chip('dating', 2)] });
    } finally {
      await sql(`UPDATE mode_intentions SET available = true WHERE id = '${retired}'`);
    }
    benTile = (await call(tokens.ana, '/api/explore?mode=dating')).body.people.find(p => p.id === pid.ben);
    assert.deepEqual(benTile.intentions, ['Short-term dating', 'Something casual / no strings']); pass(); // reactivated, the kept choice shows again
    const benAccount = await sql(`SELECT a.id FROM accounts a WHERE a.subject = '${claims(tokens.ben).sub}'`);
    assert.notEqual(benCard.id, benAccount); pass(); // the public id is not the account id
    bad = await call(tokens.ana, '/api/explore?mode=business');
    assert.deepEqual([bad.status, bad.body.code], [403, 'mode-not-enabled']); pass();
    for (const q of ['?mode=travel', '?mode=', '']) { bad = await call(tokens.ana, '/api/explore' + q); assert.equal(bad.status, 404, q); pass(); }
    assert.ok(!ids(await call(tokens.ben, '/api/explore?mode=dating')).includes(pid.cai)); pass();

    // Person profile: only in modes both have on; otherwise not found, the same for unknown ids.
    let person = await call(tokens.ana, `/api/profiles/${benCard.id}`);
    assert.equal(person.status, 200); assert.deepEqual(person.body.modes.map(m => m.id), ['dating']); pass(); // ana has business off
    const caiId = await sql(`SELECT p.public_id FROM member_profiles p JOIN accounts a ON a.id = p.account_id WHERE a.subject = '${claims(tokens.cai).sub}'`);
    assert.equal((await call(tokens.ana, `/api/profiles/${caiId}`)).status, 404); pass();
    assert.equal((await call(tokens.ana, '/api/profiles/00000000-0000-0000-0000-000000000000')).status, 404); pass();
    const anaId = (await call(tokens.ana, '/api/me/profile')).body.publicId;
    assert.equal((await call(tokens.ana, `/api/profiles/${anaId}`)).status, 404); pass(); // own profile is not "someone else"

    // Account state still wins: a revoked beta place, a bot flag or a lock hides Ben everywhere.
    await cli('beta', 'revoke', ben.email);
    assert.ok(!ids(await call(tokens.ana, '/api/explore?mode=dating')).includes(pid.ben)); pass();
    assert.equal((await call(tokens.ana, `/api/profiles/${benCard.id}`)).status, 404); pass();
    await cli('beta', 'add', ben.email);
    await call(tokens.ben, '/api/session'); // binds the new enrolment
    assert.ok(ids(await call(tokens.ana, '/api/explore?mode=dating')).includes(pid.ben)); pass();
    await sql(`UPDATE member_profiles SET bot = true WHERE public_id = '${benCard.id}'`);
    assert.ok(!ids(await call(tokens.ana, '/api/explore?mode=dating')).includes(pid.ben)); pass(); // Q11: a bot with no authorised modes stays out (badges.browser.cjs covers authorised bots)
    await sql(`UPDATE member_profiles SET bot = false WHERE public_id = '${benCard.id}'`);
    await call(tokens.ben, '/api/me/age', { method: 'PUT', body: { dateOfBirth: new Date(Date.now() - 16.5 * 365.25 * 86400000).toISOString().slice(0, 10) } }); // fictional under-18 date locks the account (D40)
    assert.ok(!ids(await call(tokens.ana, '/api/explore?mode=dating')).includes(pid.ben)); pass();
    assert.equal((await call(tokens.ben, '/api/explore?mode=dating')).body.code, 'account-locked'); pass();

    // A second visible person for the pages (Ben is now locked).
    await call(tokens.eli, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Eli', area: 'Norwood', about: 'Fictional adult who reads a lot.', homeCityId: 2078025 } });
    await matchReady(tokens.eli);
    await save('eli', 'dating', { enabled: true, visible: true, intentions: [chip('dating', 6), chip('dating', 1)] });

    // Web: the profile page, Explore and a person page in WebAssembly, through /bff/ only.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const apiCalls = [];
    page.on('request', r => { if (r.url().includes(':5080')) apiCalls.push(r.url()); });
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', ana.email); await page.fill('#password', ana.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.goto(web + '/me/profile');
    await page.waitForSelector('[data-profile-editor=ready]', { timeout: 20000 });
    assert.equal(await page.inputValue('#display-name'), 'Fictional Ana'); pass();
    assert.deepEqual(await page.locator('[data-mode-card]').evaluateAll(els => els.map(e => [e.dataset.modeCard, e.dataset.modeEnabled])),
      [['dating', 'true'], ['friends', 'false'], ['activities', 'false'], ['business', 'false']]); pass();
    assert.equal(await page.locator('[data-chip-picker]').count(), 4); pass();
    assert.equal(await page.locator('[data-chip-picker=dating] [data-chip-count]').innerText(), '5 of 5'); pass();
    assert.equal(await page.locator('[data-chip-picker=friends] [data-chip-count]').innerText(), '2 of 5'); pass(); // kept while Friends is off
    assert.equal(await page.locator('[data-chip-picker=dating] button[aria-pressed=false]:not([disabled])').count(), 0); pass(); // full: the rest are disabled
    await page.click(`[data-chip-picker=dating] [data-chip="${chip('dating', 10)}"]`);
    await page.waitForSelector('[data-chip-picker=dating] [data-chip-count="4"]'); pass();
    await page.click(`[data-chip-picker=dating] [data-chip="${chip('dating', 9)}"]`);
    await page.waitForSelector('[data-chip-picker=dating] [data-chip-count="5"]');
    assert.deepEqual((await call(tokens.ana, '/api/me/profile')).body.modes.find(m => m.id === 'dating').intentions, [1, 2, 3, 7, 9].map(n => chip('dating', n))); pass();
    assert.equal(await page.locator('[data-chip-picker=business] [data-chip-count]').innerText(), '0 of 5'); pass();
    await page.getByLabel('Use Activities').check();
    await page.waitForSelector('[data-mode-card=activities][data-mode-enabled=true][data-mode-visible=true]');
    assert.equal((await call(tokens.ana, '/api/me/profile')).body.modes.find(m => m.id === 'activities').enabled, true); pass();
    await page.getByLabel('Show my profile in Activities').uncheck();
    await page.waitForSelector('[data-mode-card=activities][data-mode-visible=false]'); pass();
    await page.fill('#area', 'Adelaide'); await page.click('[data-core-form] button[type=submit]');
    await page.waitForSelector('[data-core-status=saved]'); pass();
    // The server refuses what the browser's maxlength would stop; remove it to send an over-long name.
    await page.$eval('#display-name', el => el.removeAttribute('maxlength'));
    await page.fill('#display-name', 'n'.repeat(41)); await page.click('[data-core-form] button[type=submit]');
    await page.waitForSelector('[data-core-status=invalid]'); pass();
    if (output) await page.screenshot({ path: output + '/profile-390.png', fullPage: true });

    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-state=ready]', { timeout: 20000 });
    assert.deepEqual(await page.locator('.explore-modes [data-mode]').evaluateAll(els => els.map(e => e.textContent)), ['Dating', 'Friends', 'Activities', 'Business']); pass();
    assert.equal(await page.locator('.explore-modes [aria-pressed=true]').getAttribute('data-mode'), 'dating'); pass();
    assert.equal(await page.locator('.explore-modes').getByText('Travel').count(), 0); pass();
    const hrefs = await page.locator('.profile-tile h2 a').evaluateAll(els => els.map(e => e.getAttribute('href').split('?')[0])); // links carry ?mode= (D175)
    assert.ok(hrefs.includes('/people/' + pid.eli) && !hrefs.includes('/people/' + pid.ben) && !hrefs.includes('/people/' + pid.cai)); pass();
    const tiles = await page.locator('.profile-tile').evaluateAll(els => els.map(e => [Math.round(e.getBoundingClientRect().width), Math.round(e.getBoundingClientRect().height)]));
    assert.ok(tiles.every(([w, h]) => w === tiles[0][0] && h === tiles[0][1])); pass(); // equal tiles (UI-STYLE.md)
    assert.equal(await page.locator(`.profile-tile:has(a[href^="/people/${pid.eli}"]) .tile-intentions`).innerText(), 'Long-term relationship · Open to exploring'); pass();
    if (output) await page.screenshot({ path: output + '/explore-390.png', fullPage: true });
    await page.click('.explore-modes [data-mode=business]');
    await page.waitForSelector('[data-explore-mode=business][data-explore-state=off]'); pass();
    assert.equal(await page.locator('[data-explore-state=off] a').getAttribute('href'), '/me/profile'); pass();
    await page.click('.explore-modes [data-mode=dating]');
    await page.waitForSelector('[data-explore-mode=dating][data-explore-state=ready]');
    await page.click(`.profile-tile a[href^="/people/${pid.eli}"]`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    assert.equal(await page.locator('h1').innerText(), 'Fictional Eli'); pass();
    assert.equal(await page.locator('[data-shared-modes]').getAttribute('data-shared-modes'), 'dating'); pass();
    assert.deepEqual(await page.locator('[data-shared-modes] .chip.is-on').allInnerTexts(), ['Long-term relationship', 'Open to exploring']); pass();
    await page.goto(web + `/people/${caiId}`);
    await page.waitForSelector('[data-person=unavailable]', { timeout: 20000 }); pass();
    assert.deepEqual(apiCalls, []); pass(); // the browser never calls the API directly
    assert.equal((await page.request.get(web + '/bff/api/explore?mode=dating', { headers: { 'X-Swoovr-Bff': '1' } })).status(), 200); pass();
    assert.equal((await page.request.get(web + '/bff/api/admin/beta', { headers: { 'X-Swoovr-Bff': '1' } })).status(), 404); pass();
    assert.equal((await page.request.get(web + '/bff/api/me/modes/../../admin/beta', { headers: { 'X-Swoovr-Bff': '1' } })).status(), 404); pass();
    assert.equal((await page.request.get(web + '/bff/api/explore?mode=dating')).status(), 400); pass(); // header required

    // Signed out, the pages say so and load nothing.
    const guest = await (await browser.newContext()).newPage();
    await guest.goto(web + '/explore');
    assert.match(await guest.locator('main').innerText(), /Sign in to see who's on Swoovr/); pass();
    await context.close();
    console.log(`PASS: connection modes acceptance, ${passed} checks (catalogue from data, forty intention chips with the five limit, retired chips kept and hidden, chips on tiles and profiles, own profile and text limits, several independent modes, unknown/Travel modes and made-up intentions refused, Explore both-ways visibility, public ids, account state and beta always win, unauthorised bots excluded, WebAssembly profile/Explore/person pages through /bff/ only).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
