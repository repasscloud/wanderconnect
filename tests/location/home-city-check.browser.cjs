// Local acceptance for #281 (D349): the scheduled Home City check. Accepted device readings (Near me and "Check my
// location matches") set that day's outcome in the Home City's time zone, with no coordinates kept; the monthly check
// skips sparse periods, suggests at 75% away across 3 of 4 parts with no home day in the last 7, excludes checked-in
// trip days from both counts, and records count-only results; "Don't suggest before" (Admin overview card, audited)
// holds a suggestion back; nothing restricts, warns or changes the Home City. With the helpdesk configured
// (SWOOVR_OWNED_ZAMMAD=1 in tests/auth/run-owned.cjs) it also checks the Zammad item: one internal note in "Home City
// checks" with counts only, invisible to the member until Support replies, no second item while one is open, none within
// 90 days of closure, and closed + 6 months retention. Periods are seeded as daily outcomes (the same rows readings
// write) so 30 days don't have to pass. Every person is a fictional adult created for this run.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { admin, createMember, createStaff, memberToken, staffToken, elevatedRootToken, fillStaffOtp, cli, sql, call, claims } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;
const zammad = !!process.env.Ticketing__Zammad__HomeCityCheckToken;
const output = process.env.SWOOVR_BROWSER_OUTPUT;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const root = await elevatedRootToken(browser);
    const supStaff = await createStaff('hcc-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: supStaff.email, role: 'support' } })).status, 201);
    const support = await staffToken(browser, supStaff);
    const names = ['now', 'spr', 'rec', 'trp', 'hom', 'dnb'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('hcc-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      assert.equal((await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Home ' + n, homeCityId: ADELAIDE } })).status, 200);
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const at = (lat, lon, over = {}) => ({ latitude: lat, longitude: lon, accuracyMetres: 30, takenAt: new Date().toISOString(), ...over });
    const city = () => at(-34.9285, 138.6007), hills = () => at(-34.9790, 138.7110), barossa = () => at(-34.5333, 138.9500), melbourne = () => at(-37.8136, 144.9631);
    const localToday = `(now() AT TIME ZONE 'Australia/Adelaide')::date`;
    const dayOf = who => sql(`SELECT coalesce(string_agg(outcome, ','), '') FROM home_city_days WHERE account_id = '${ids[who]}'`);

    // 1. Accepted readings set today's outcome; nothing positional is stored or audited.
    let r = await call(tokens.now, '/api/me/location', { method: 'PUT', body: hills() }); // ~11 km from the city's GeoNames point
    assert.equal(r.status, 200); pass();
    assert.equal(await dayOf('now'), 'Home'); pass();
    assert.equal(await sql(`SELECT home_city_check_from = ${localToday} FROM member_profiles WHERE account_id = '${ids.now}'`), 't'); pass(); // the period starts with the first reading
    assert.equal((await call(tokens.spr, '/api/me/home-check', { method: 'POST', body: barossa() })).body.outcome, 'inconsistent'); pass(); // ~50 km: outside the 25 km vicinity
    assert.equal(await dayOf('spr'), 'Away'); pass();
    assert.equal((await call(tokens.spr, '/api/me/home-check', { method: 'POST', body: city() })).body.outcome, 'consistent'); pass();
    assert.equal(await dayOf('spr'), 'Home'); pass(); // a home reading makes it a home day
    assert.equal((await call(tokens.spr, '/api/me/home-check', { method: 'POST', body: melbourne() })).body.outcome, 'inconsistent'); pass(); // this reading is away,
    assert.equal(await dayOf('spr'), 'Home'); pass(); // but the day stays a home day
    assert.equal(await sql(`SELECT count(*) FROM home_city_days WHERE account_id = '${ids.spr}'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM information_schema.columns WHERE table_name IN ('home_city_days', 'home_city_checks') AND column_name ~ '(lat|lon|accuracy|km|distance|place|city|destination|ip_)'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail ~ '(-3[47]\\.[0-9]|13[89]\\.[0-9]|144\\.9|Melbourne|Barossa)'`), '0'); pass();

    // 2. Seeded periods (days 1–30 ending before today in Adelaide), then the monthly job.
    const seed = async (who, outcomes, startOffset = 31) => {
      const start = `(${localToday} - ${startOffset})`;
      await sql(`DELETE FROM home_city_days WHERE account_id = '${ids[who]}'`);
      const values = Object.entries(outcomes).map(([d, o]) => `(${d}, '${o}')`).join(',');
      await sql(`INSERT INTO home_city_days (account_id, day, outcome, updated_at) SELECT '${ids[who]}', ${start} + (v.d - 1), v.o, now() FROM (VALUES ${values}) v(d, o)`);
      await sql(`UPDATE member_profiles SET home_city_check_from = ${start} WHERE account_id = '${ids[who]}'`);
    };
    const spread = [1, 3, 5, 8, 10, 12, 15, 17, 19, 22, 24, 25, 26, 27, 28, 29, 30];
    const days = f => Object.fromEntries(spread.map(d => [d, f(d)]));
    const latest = who => sql(`SELECT result || '|' || suppression || '|' || located_days || '|' || away_days || '|' || excluded_days || '|' || parts_covered || '|' || ticket_state
      FROM home_city_checks WHERE account_id = '${ids[who]}' ORDER BY checked_at DESC LIMIT 1`);
    await seed('spr', Object.fromEntries(spread.slice(0, 11).map(d => [d, 'Away'])));        // sparse: 11 days
    await seed('rec', days(d => d === 25 ? 'Home' : 'Away'));                                 // a home day in the last 7
    await seed('trp', days(d => d >= 15 ? 'Excluded' : 'Away'));                              // a checked-in trip from day 15
    await seed('hom', days(d => d === 1 || d === 8 ? 'Home' : 'Away'));                        // 15 of 17 away
    await seed('dnb', days(d => d === 1 || d === 8 ? 'Home' : 'Away'));

    // "Don't suggest before" on the Admin overview card (Support; audited with the reason).
    const adminContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const adminPage = await adminContext.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', supStaff.email); await adminPage.fill('#password', supStaff.password); await adminPage.click('#kc-login');
    await fillStaffOtp(adminPage, supStaff);
    await adminPage.waitForSelector('[data-dashboard], [data-holds-overview], main', { timeout: 20000 });
    await adminPage.goto(admin + `/accounts/${ids.dnb}`);
    await adminPage.waitForSelector('[data-home-city-check]', { timeout: 20000 });
    assert.equal(await adminPage.getAttribute('[data-home-city-latest]', 'data-home-city-latest'), 'none'); pass();
    const tomorrow = await sql(`SELECT to_char(${localToday} + 1, 'YYYY-MM-DD')`);
    await adminPage.click('[data-op="home-city-check"]');
    await adminPage.fill('#home-city-check input[name="until"]', tomorrow);
    await adminPage.selectOption('#home-city-check select[name="reason"]', 'Member is away for a while');
    await Promise.all([adminPage.waitForLoadState('networkidle'), adminPage.click('#home-city-check [data-confirm="home-city-check"]')]);
    await adminPage.waitForSelector(`[data-dont-suggest-before="${tomorrow}"]`, { timeout: 20000 });
    assert.equal(await sql(`SELECT home_city_dont_suggest_before::text FROM member_profiles WHERE account_id = '${ids.dnb}'`), tomorrow); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'home-city-check.dont-suggest-before' AND target_id = '${ids.dnb}' AND detail = '${tomorrow}: Member is away for a while'`), '1'); pass();
    if (output) await adminPage.screenshot({ path: output + '/admin-home-city-check.png', fullPage: true });
    // The route: members can't; bad dates and long reasons are refused.
    assert.equal((await call(tokens.dnb, `/api/admin/accounts/${ids.dnb}/home-city-check`, { method: 'PUT', body: { dontSuggestBefore: null } })).status, 403); pass();
    assert.deepEqual(code(await call(support, `/api/admin/accounts/${ids.dnb}/home-city-check`, { method: 'PUT', body: { dontSuggestBefore: '2020-01-01' } })), [400, 'invalid-request']); pass();
    assert.deepEqual(code(await call(support, `/api/admin/accounts/${ids.dnb}/home-city-check`, { method: 'PUT', body: { dontSuggestBefore: tomorrow, reason: 'x'.repeat(301) } })), [400, 'invalid-request']); pass();

    await cli('location', 'home-city-check').catch(() => {}); // exits 1 only when an item has failed for good
    assert.match(await latest('spr'), /^Skipped\|None\|11\|11\|0\|/); pass();                       // sparse readings skip
    assert.match(await latest('rec'), /^NoSuggestion\|None\|17\|16\|0\|4\|None$/); pass();          // a home day in the last 7 days prevents it
    assert.match(await latest('trp'), /^Skipped\|None\|6\|6\|11\|2\|None$/); pass();                // trip days count for neither side
    assert.match(await latest('dnb'), /^Suppressed\|DontSuggestBefore\|17\|15\|0\|4\|None$/); pass(); // held back by the date
    const hom = await latest('hom');
    assert.match(hom, zammad ? /^Suggested\|None\|17\|15\|0\|4\|Created$/ : /^Suggested\|None\|17\|15\|0\|4\|Pending$/); pass();
    if (!zammad) { assert.equal(await sql(`SELECT failure_code FROM home_city_checks WHERE account_id = '${ids.hom}'`), 'ticketing-unavailable'); pass(); }
    assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id IN ('${ids.spr}','${ids.rec}','${ids.hom}') AND home_city_check_from = ${localToday} - 1`), '3'); pass(); // the next period follows on
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'home-city-check.checked'`), '5'); pass();

    // Never a sanction, a warning, a notice or a Home City change.
    for (const n of ['hom', 'dnb', 'trp']) {
      assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids[n]}'`), 'Active');
      assert.equal(await sql(`SELECT count(*) FROM sanctions WHERE account_id = '${ids[n]}'`), '0');
      assert.equal(await sql(`SELECT count(*) FROM member_notices WHERE account_id = '${ids[n]}'`), '0');
      assert.equal(await sql(`SELECT home_geo_name_id FROM member_profiles WHERE account_id = '${ids[n]}'`), String(ADELAIDE));
    }
    pass();

    // The summary carries counts only; the card shows the latest result in observed days.
    r = await call(support, `/api/admin/accounts/${ids.hom}/summary`);
    assert.equal(r.status, 200);
    assert.deepEqual([r.body.homeCityCheck.latest.result, r.body.homeCityCheck.latest.observed, r.body.homeCityCheck.latest.locatedDays], ['suggested', 'away on 15 of 17 days with readings', 17]); pass();
    assert.ok(!JSON.stringify(r.body.homeCityCheck).match(/latitude|longitude|-34\.|138\./)); pass();
    await adminPage.goto(admin + `/accounts/${ids.hom}`);
    await adminPage.waitForSelector('[data-home-city-latest="suggested"]', { timeout: 20000 });
    assert.ok((await adminPage.locator('[data-home-city-latest]').innerText()).includes('away on 15 of 17 days with readings')); pass();

    if (zammad) {
      // 3. The Zammad item: an internal note in "Home City checks", counts only, hidden from the member until Support replies.
      const ticketId = Number(await sql(`SELECT ticket_id FROM home_city_checks WHERE account_id = '${ids.hom}'`));
      // The member list comes from Zammad's search index, which catches up a few seconds after a write.
      const listed = async () => (await call(tokens.hom, '/api/me/tickets')).body.items.some(t => t.id === ticketId);
      const eventually = async (want) => { for (let i = 0; i < 60 && await listed() !== want; i++) await new Promise(res => setTimeout(res, 1000)); return listed(); };      let detail = await call(support, `/api/admin/tickets/${ticketId}`);
      assert.equal(detail.status, 200); pass();
      const caps = (await call(support, '/api/admin/tickets/capabilities')).body;
      assert.equal(caps.groups.find(g => g.id === detail.body.staff.groupId)?.name, 'Home City checks'); pass();
      assert.equal(detail.body.staff.customerAccountId, ids.hom); pass();
      assert.equal(detail.body.articles.length, 1); pass();
      const note = detail.body.articles[0];
      assert.ok(note.private && note.body.includes('away on 15 of 17 days with readings') && note.body.includes('/accounts/' + ids.hom)
        && note.body.includes(await sql(`SELECT public_id FROM member_profiles WHERE account_id = '${ids.hom}'`)) && note.body.includes('Adelaide')); pass();
      assert.ok(!note.body.match(/-3[47]\.\d|13[89]\.\d|144\.9|latitude|longitude| km\b/)); pass();
      assert.equal(detail.body.ticket.title, 'Your Home City'); pass();
      assert.equal((await call(support, `/api/admin/tickets/${ticketId}/retention`)).body.category, 'HomeCityCheck'); pass();
      for (let i = 0; i < 60 && !(await call(support, `/api/admin/tickets?group=${detail.body.staff.groupId}`)).body?.items?.some(t => t.id === ticketId); i++) await new Promise(res => setTimeout(res, 1000));
      assert.equal(await listed(), false); pass();     // invisible in the list (the staff search above shows it is indexed)
      assert.equal((await call(tokens.hom, `/api/me/tickets/${ticketId}`)).status, 404); pass();                          // and in detail
      assert.equal(await sql(`SELECT count(*) FROM notifications WHERE account_id = '${ids.hom}'`), '0'); pass(); // the internal note notifies nobody

      // No second item while one is open.
      await seed('hom', days(d => d === 1 || d === 8 ? 'Home' : 'Away'), 30);
      await cli('location', 'home-city-check').catch(() => {});
      assert.match(await latest('hom'), /^Suppressed\|OpenItem\|/); pass();
      assert.equal(await sql(`SELECT count(*) FROM home_city_checks WHERE account_id = '${ids.hom}' AND ticket_id IS NOT NULL`), '1'); pass();

      // Support replies with the macro's text: the member now sees the item, with the reply and never the note.
      const module = (await call(support, '/api/admin/tickets/text-modules')).body.find(m => m.name === 'Home City checks · Suggest updating your Home City');
      assert.ok(module && module.body.includes('Me → Profile')); pass();
      assert.ok((await call(support, '/api/admin/tickets/macros')).body.some(m => m.name === 'Suggest updating your Home City')); pass();
      assert.equal((await call(support, `/api/admin/tickets/${ticketId}/replies`, { method: 'POST', body: { operationId: crypto.randomUUID(), body: module.body, private: false } })).status, 200); pass();
      assert.equal(await eventually(true), true); pass();
      const mine = await call(tokens.hom, `/api/me/tickets/${ticketId}`);
      assert.equal(mine.status, 200); pass();
      assert.deepEqual(mine.body.articles.map(a => a.body.includes('Me → Profile')), [true]); pass(); // only the reply
      assert.ok(!JSON.stringify(mine.body).includes('away on')); pass();

      // Closed: no new item within 90 days of closure.
      detail = await call(support, `/api/admin/tickets/${ticketId}`);
      const closed = caps.states.find(s => s.name === 'closed');
      assert.equal((await call(support, `/api/admin/tickets/${ticketId}`, { method: 'PUT', body: { operationId: crypto.randomUUID(), expectedUpdatedAt: detail.body.ticket.updatedAt, stateId: closed.id } })).status, 200); pass();
      await seed('hom', days(d => d === 1 || d === 8 ? 'Home' : 'Away'), 32);
      await cli('location', 'home-city-check').catch(() => {});
      assert.match(await latest('hom'), /^Suppressed\|Cooldown\|/); pass();
      assert.equal((await call(support, `/api/admin/tickets/${ticketId}/retention`)).body.retainUntil !== null, true); pass(); // closed + 6 months
    } else {
      console.log('NOTE: helpdesk not configured for this run (SWOOVR_OWNED_ZAMMAD unset); Zammad item steps not exercised.');
    }

    // 4. A Home City change restarts the period and drops the old city's days.
    assert.equal((await call(tokens.rec, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Home rec', homeCityId: MELBOURNE } })).status, 200);
    assert.equal(await sql(`SELECT count(*) FROM home_city_days WHERE account_id = '${ids.rec}'`), '0'); pass();
    assert.equal(await sql(`SELECT home_city_check_from IS NULL FROM member_profiles WHERE account_id = '${ids.rec}'`), 't'); pass();

    // 5. Retention: days older than 35 days and results older than 12 months go.
    await sql(`INSERT INTO home_city_days (account_id, day, outcome, updated_at) VALUES ('${ids.now}', ${localToday} - 40, 'Away', now())`);
    await sql(`UPDATE home_city_checks SET checked_at = now() - interval '13 months' WHERE account_id = '${ids.spr}'`);
    await cli('location', 'home-city-check').catch(() => {});
    assert.equal(await sql(`SELECT count(*) FROM home_city_days WHERE account_id = '${ids.now}' AND day < ${localToday} - 35`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM home_city_checks WHERE account_id = '${ids.spr}'`), '0'); pass();

    // Nothing positional anywhere this suite wrote.
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'home-c%' AND detail ~ '(-3[47]\\.[0-9]|13[89]\\.[0-9]|144\\.9)'`), '0'); pass();
    await adminContext.close();
    console.log(`PASS: scheduled Home City check (${passed} checks${zammad ? ', with the helpdesk' : ', helpdesk steps not run'}).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
