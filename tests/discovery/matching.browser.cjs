// Local acceptance for slice R (D137–D140, D151): date of birth required and never shown, age shown unless hidden,
// gender and reciprocal Dating "show me", excluded intentions that work both ways in Explore, profiles and requests,
// the viewer's own filters (age, required chips any/all, ID Verified only, visitor visibility), Plus filters only on
// Plus through the entitlement service, and the web pages in actual Edge. Same local stack as
// tests/discovery/market.browser.cjs. Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, GLENELG = 2070830, MELBOURNE = 2158177;
const everyone = ['man', 'woman', 'non-binary', 'another', 'unsaid'];
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = days => { const d = new Date(todayIn('Australia/Adelaide') + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cal', 'dex', 'eve', 'fay', 'gus', 'hal', 'ivy', 'tom', 'gle'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('match-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const homes = { tom: MELBOURNE, gle: GLENELG };
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), homeCityId: homes[n] ?? ADELAIDE } });
      assert.equal(saved.status, 200, n);
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    // Identities: ana a woman looking for men, bob a man looking for women, cal a man looking for men, dex non-binary
    // looking for everyone; eve has no date of birth; fay has no gender; the rest look for everyone.
    await matchReady(tokens.ana, { gender: 'woman', showMe: ['man'] });
    await matchReady(tokens.bob, { gender: 'man', showMe: ['woman'], dateOfBirth: '1990-06-01' });
    await matchReady(tokens.cal, { gender: 'man', showMe: ['man'] });
    await matchReady(tokens.dex, { gender: 'non-binary', showMe: everyone });
    await call(tokens.fay, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1992-02-02' } });
    for (const n of ['gus', 'hal', 'ivy', 'tom', 'gle']) await matchReady(tokens[n]);
    await call(tokens.eve, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'woman', showGender: true, showOrientation: false, showAge: true, datingShowMe: everyone } });
    const code = r => [r.status, r.body?.code];
    const explore = async (who, mode = 'friends') => { const r = await call(tokens[who], '/api/v1/explore?mode=' + mode); return r.status === 200 ? r.body.people : r; };
    const ids_ = people => people.map(p => p.id);
    const modes = (await call(tokens.ana, '/api/v1/modes')).body;
    const chip = (mode, i) => modes.find(m => m.id === mode).intentions[i].id;
    const [X, Y, Z] = [chip('friends', 0), chip('friends', 1), chip('friends', 2)];
    const setChips = (who, chips) => call(tokens[who], '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, intentions: chips } });
    const filters = (who, body, mode = 'friends') => call(tokens[who], `/api/v1/me/modes/${mode}/filters`, { method: 'PUT', body: { idVerifiedOnly: false, ...body } });

    // Options are data (D138).
    let r = await call(tokens.ana, '/api/v1/profile-options');
    assert.deepEqual(r.body.genders.map(g => g.id), everyone); assert.ok(r.body.orientations.length >= 5); pass();

    // Date of birth (D139): required, never an age alone, never shown.
    assert.deepEqual(code(await call(tokens.eve, '/api/v1/me/age', { method: 'PUT', body: { age: 30 } })), [400, 'date-of-birth-required']); pass();
    assert.deepEqual(code(await call(tokens.eve, '/api/v1/me/age', { method: 'PUT', body: {} })), [400, 'date-of-birth-required']); pass();
    assert.deepEqual(code(await call(tokens.eve, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '2999-01-01' } })), [400, 'invalid-age']); pass();
    assert.deepEqual(code(await call(tokens.eve, '/api/v1/explore?mode=friends')), [403, 'date-of-birth-required']); pass();
    assert.ok(!ids_(await explore('ana')).includes(pid.eve)); pass(); // out of discovery
    assert.equal((await call(tokens.ana, `/api/v1/profiles/${pid.eve}`)).status, 404); pass();
    let people = await explore('gus');
    const bobCard = people.find(p => p.id === pid.bob);
    assert.equal(typeof bobCard.age, 'number'); assert.equal(bobCard.gender, 'Man'); pass();
    assert.ok(!JSON.stringify(people).includes('1990-')); pass(); // never the date
    assert.ok(!JSON.stringify(await call(tokens.gus, `/api/v1/profiles/${pid.bob}`)).includes('1990-')); pass();
    // Hiding the age or gender hides them on cards, but the filters still use them.
    await call(tokens.bob, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'man', showGender: false, showOrientation: false, showAge: false, datingShowMe: ['woman'] } });
    people = await explore('gus');
    assert.deepEqual([people.find(p => p.id === pid.bob).age, people.find(p => p.id === pid.bob).gender], [null, null]); pass();
    await filters('gus', { minAge: 18, maxAge: 25 });
    assert.ok(!ids_(await explore('gus')).includes(pid.bob)); pass(); // bob is in his thirties, hidden age or not
    await filters('gus', {});
    assert.ok(ids_(await explore('gus')).includes(pid.bob)); pass();
    // After ID verification the date of birth can't change without support (D157; the verified status is simulated here).
    await sql(`UPDATE member_profiles SET age_status = 'Verified' WHERE account_id = '${ids.ivy}'`);
    assert.deepEqual(code(await call(tokens.ivy, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1980-01-01' } })), [409, 'date-of-birth-locked']); pass();
    assert.equal((await call(tokens.ivy, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1990-01-01' } })).status, 200); pass(); // the same date is fine

    // Dating needs a gender and a "show me"; both are reciprocal (D138).
    assert.deepEqual(code(await call(tokens.fay, '/api/v1/explore?mode=dating')), [403, 'dating-details-required']); pass();
    assert.ok(ids_(await explore('ana')).includes(pid.fay)); pass(); // Friends doesn't need them
    assert.ok(!ids_(await explore('gus', 'dating')).includes(pid.fay)); pass();
    people = ids_(await explore('ana', 'dating'));
    assert.ok(people.includes(pid.bob)); pass(); // woman→man and man→woman
    assert.ok(!people.includes(pid.cal) && !people.includes(pid.dex) && !people.includes(pid.gus)); pass(); // one-sided or not her choice
    assert.ok(!ids_(await explore('cal', 'dating')).includes(pid.ana)); pass();
    assert.ok(!ids_(await explore('dex', 'dating')).includes(pid.ana)); pass(); // dex includes women, ana doesn't include non-binary
    r = await call(tokens.ana, `/api/v1/profiles/${pid.cal}`);
    assert.equal(r.status, 200); assert.deepEqual(r.body.modes.map(m => m.id), ['friends']); pass(); // Dating isn't shared
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/requests', { method: 'POST', body: { to: pid.cal, mode: 'dating' } })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ana, '/api/v1/requests', { method: 'POST', body: { to: pid.cal, mode: 'friends' } })).status, 201); pass();
    assert.equal((await call(tokens.ana, '/api/v1/requests', { method: 'POST', body: { to: pid.bob, mode: 'dating' } })).status, 201); pass();
    // Identity validation; orientation never decides who anyone sees.
    assert.deepEqual(code(await call(tokens.gus, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'robot', showGender: true, showOrientation: false, showAge: true, datingShowMe: everyone } })), [400, 'invalid-profile']); pass();
    assert.deepEqual(code(await call(tokens.gus, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'man', pronouns: 'x'.repeat(41), showGender: true, showOrientation: false, showAge: true, datingShowMe: everyone } })), [400, 'invalid-profile']); pass();
    assert.deepEqual(code(await call(tokens.gus, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'man', showGender: true, showOrientation: false, showAge: true, datingShowMe: ['robot'] } })), [400, 'invalid-profile']); pass();
    await call(tokens.dex, '/api/v1/me/identity', { method: 'PUT', body: { gender: 'non-binary', orientation: 'gay', showGender: true, showOrientation: true, showAge: true, datingShowMe: everyone } });
    assert.ok(ids_(await explore('gus', 'dating')).includes(pid.dex)); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'identity.updated' AND (detail LIKE '%gay%' OR detail LIKE '%binary%')`), '0'); pass();

    // Required chips (any/all) narrow only the viewer's own results (D140).
    await setChips('gus', [X]); await setChips('hal', [X, Y]); await setChips('ivy', [Y]); await setChips('ana', [Z]);
    await filters('dex', { requiredChips: [X, Y], requiredMatch: 'any' });
    people = ids_(await explore('dex'));
    assert.ok(people.includes(pid.gus) && people.includes(pid.hal) && people.includes(pid.ivy) && !people.includes(pid.bob)); pass();
    await filters('dex', { requiredChips: [X, Y], requiredMatch: 'all' });
    people = ids_(await explore('dex'));
    assert.ok(people.includes(pid.hal) && !people.includes(pid.gus) && !people.includes(pid.ivy)); pass();
    assert.ok(ids_(await explore('gus')).includes(pid.dex)); pass(); // a required chip never hides the viewer from others
    await filters('dex', {});
    // ID Verified only.
    await sql(`UPDATE member_profiles SET id_status = 'Verified', id_checked_at = now() WHERE account_id = '${ids.hal}'`); // simulated for this local test
    await filters('dex', { idVerifiedOnly: true });
    people = ids_(await explore('dex'));
    assert.ok(people.includes(pid.hal) && !people.includes(pid.gus)); pass();
    await filters('dex', {});

    // Excluded chips work both ways in Explore, profiles and requests (D140).
    await filters('cal', { excludedChips: [Y] });
    assert.ok(!ids_(await explore('cal')).includes(pid.ivy)); pass(); // cal doesn't see someone holding Y
    assert.ok(!ids_(await explore('ivy')).includes(pid.cal)); pass(); // and ivy can't find cal
    assert.equal((await call(tokens.ivy, `/api/v1/profiles/${pid.cal}`)).status, 404); pass(); // their only shared mode was Friends... and Dating fails show me
    assert.deepEqual(code(await call(tokens.ivy, '/api/v1/requests', { method: 'POST', body: { to: pid.cal, mode: 'friends' } })), [404, 'not-available']); pass();
    assert.ok(ids_(await explore('cal')).includes(pid.gus)); pass(); // only Y excludes
    await filters('cal', {});
    assert.ok(ids_(await explore('ivy')).includes(pid.cal)); pass(); // removing it restores both ways

    // Filter validation.
    for (const [body, expected] of [[{ minAge: 40, maxAge: 30 }, 'invalid-filter'], [{ minAge: 17 }, 'invalid-filter'], [{ requiredChips: [chip('dating', 0)] }, 'invalid-filter'],
      [{ requiredChips: [X], excludedChips: [X] }, 'invalid-filter'], [{ requiredChips: modes.find(m => m.id === 'friends').intentions.slice(0, 6).map(i => i.id) }, 'invalid-filter'],
      [{ requiredMatch: '1' }, 'invalid-filter'], [{ visitors: 'everyone' }, 'invalid-filter']])
    { assert.deepEqual(code(await filters('ana', body)), [400, expected], JSON.stringify(body)); pass(); }
    assert.deepEqual(code(await filters('ana', {}, 'travel')), [404, 'unknown-mode']); pass();

    // Plus filters only with Plus from the entitlement service (D137, D151); a lapsed Plus never hides people.
    r = await call(tokens.hal, '/api/v1/me/modes/friends/filters');
    assert.equal(r.body.plusAvailable, false); pass();
    assert.deepEqual(code(await filters('hal', { maxDistanceKm: 5 })), [403, 'plus-required']); pass();
    assert.deepEqual(code(await filters('hal', { sort: 'compatibility' })), [403, 'plus-required']); pass();
    const grant = await call(root, `/api/v1/admin/accounts/${ids.hal}/entitlements`, { method: 'POST', body: { tier: 'plus', endsAt: new Date(Date.now() + 7 * 86400000).toISOString(), reason: 'Fictional tester' } });
    assert.equal(grant.status, 201);
    assert.equal((await call(tokens.hal, '/api/v1/me/modes/friends/filters')).body.plusAvailable, true); pass();
    assert.equal((await filters('hal', { maxDistanceKm: 5, sort: 'compatibility' })).status, 200); pass();
    people = ids_(await explore('hal'));
    assert.ok(!people.includes(pid.gle) && people.includes(pid.gus)); pass(); // Glenelg is over 5 km from Adelaide's centre
    assert.ok(people.indexOf(pid.ivy) < people.indexOf(pid.bob) || !people.includes(pid.bob)); pass(); // shares Y first
    await call(root, `/api/v1/admin/entitlements/${grant.body.id}/revoke`, { method: 'POST' });
    assert.ok(ids_(await explore('hal')).includes(pid.gle)); pass(); // the stored Plus filter stops counting

    // Visitor visibility (D137) in the Travel feed.
    const trip = await call(tokens.tom, '/api/v1/me/trips', { method: 'POST', body: { cityId: ADELAIDE, arrival: plus(3), departure: plus(6), visible: true } });
    assert.equal(trip.status, 201);
    const visitors = async who => (await call(tokens[who], '/api/v1/travel/visitors?mode=friends')).body.people.map(p => p.id);
    assert.ok((await visitors('gus')).includes(pid.tom)); pass(); // both by default
    await filters('gus', { visitors: 'current' });
    assert.ok(!(await visitors('gus')).includes(pid.tom)); pass(); // not arrived yet
    await filters('gus', { visitors: 'upcoming' });
    assert.ok((await visitors('gus')).includes(pid.tom)); pass();
    await filters('gus', { visitors: 'neither' });
    assert.equal((await visitors('gus')).length, 0); pass();
    await filters('gus', {});

    // BFF allow-list, then the pages in actual Edge.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const signIn = async (p, member) => {
      await p.goto(web + '/account/sign-in?intro=done');
      await p.fill('#username', member.email); await p.fill('#password', member.password);
      await Promise.all([p.waitForURL(web + '/account'), p.click('#kc-login')]);
    };
    await signIn(page, members.fay);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/v1/profile-options')).status(), 200); pass();
    assert.equal((await bff('api/v1/me/identity')).status(), 200); pass();
    assert.equal((await bff('api/v1/me/modes/friends/filters')).status(), 200); pass();
    await page.goto(web + '/explore');
    await page.waitForSelector('[data-explore-local], [data-explore-refused], .profile-grid, [data-mode]', { timeout: 20000 });
    await page.locator('[data-mode=dating], button:has-text("Dating")').first().click().catch(() => {});
    await page.waitForSelector('[data-explore-local=dating-details-required]', { timeout: 20000 }); pass();
    await page.goto(web + '/me/matching');
    await page.waitForSelector('[data-matching=ready]', { timeout: 20000 });
    await page.selectOption('#gender', 'woman');
    await page.check('[data-show-me-option=man]');
    await page.click('[data-identity-form] button[type=submit]');
    await page.waitForSelector('[data-identity-status=saved]'); pass();
    assert.equal(await sql(`SELECT gender_id || ':' || array_to_string(dating_show_me, ',') FROM member_identities WHERE account_id = '${ids.fay}'`), 'woman:man'); pass();
    await page.fill('#min-age', '25'); await page.fill('#max-age', '45');
    await page.click('[data-filter-form] button[type=submit]');
    await page.waitForSelector('[data-filter-status=saved]'); pass();
    assert.equal(await page.locator('[data-plus-filters]').getAttribute('data-plus-filters'), 'off'); pass();
    if (output) await page.screenshot({ path: output + '/matching-390.png', fullPage: true });
    assert.equal(await sql(`SELECT min_age || '-' || max_age FROM mode_filters WHERE account_id = '${ids.fay}' AND mode_id = 'dating'`), '25-45'); pass();
    await context.close();
    console.log(`PASS: matching acceptance, ${passed} checks (option lists as data, date of birth required and never shown, no age-only entry, out of discovery without one, age and gender hidden on cards but still filtered, date of birth locked after verification, Dating needs gender and show me, show me reciprocal in Explore/profiles/requests, identity validation, orientation never decides and never audited, required chips any/all narrow only the viewer, ID Verified only, excluded chips both ways in Explore/profiles/requests, filter validation, Plus distance and compatibility only on Plus and ignored after revocation, visitor visibility, BFF allow-list, web Dating notice and identity/filter forms in Edge).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
