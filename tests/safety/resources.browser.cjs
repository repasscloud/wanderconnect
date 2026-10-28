// Local acceptance for slice AI (#103, D204): country-aware Safety & Support. The public read route and its country
// resolution (manual wins; Cloudflare's header never trusted on a Development host; the AU fallback; unknown and
// unsupported countries get neutral guidance, never another country's numbers); nothing stored about the caller and no
// profile change; admins edit packs with revisions and a launch-readiness gate (members, support and legal refused;
// tampered bodies); the public /safety page signed out at phone width with a session-scoped choice; Me → Safety signed
// in; the admin app's Safety resources page. Test data lives in the user-assigned code ZZ, never offered to members.
// Needs the local stack, hosts from this checkout and the enrolled fictional root admin. Every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims } = require('../auth/local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  const pack = async (query = '', headers = {}) => {
    const response = await fetch(api + '/api/v1/safety/resources' + query, { headers });
    return { status: response.status, headers: response.headers, body: await response.json() };
  };
  try {
    // ---- The public read: no sign-in, country resolution, never another country's numbers.
    let r = await pack('?country=AU');
    assert.equal(r.status, 200); pass();
    assert.deepEqual([r.body.country, r.body.source, r.body.readiness, r.body.stale], ['AU', 'manual', 'ready', false]); pass();
    assert.equal(r.body.resources[0].category, 'emergency'); assert.equal(r.body.resources[0].phone, '000'); pass();
    for (const phone of ['131444', '1800737732', '131114']) assert.ok(r.body.resources.some(x => x.phone === phone), phone);
    assert.ok(r.body.resources.some(x => x.websiteUrl === 'https://www.esafety.gov.au/report') && r.body.resources.some(x => x.websiteUrl === 'https://www.accce.gov.au/report')); pass();
    assert.ok(r.body.resources.every(x => !x.websiteUrl || x.websiteUrl.startsWith('https://'))); pass();
    assert.deepEqual(r.body.countries.map(c => c.code).sort(), ['AU', 'GB', 'NZ']); pass();
    assert.match(r.headers.get('cache-control'), /private/); pass();
    r = await pack('');
    assert.deepEqual([r.body.country, r.body.source], ['AU', 'fallback'], 'Development falls back to AU'); pass();
    r = await pack('', { 'CF-IPCountry': 'NZ' });
    assert.deepEqual([r.body.country, r.body.source], ['AU', 'fallback'], 'a client-supplied CF-IPCountry is never trusted here'); pass();
    r = await pack('?country=nz');
    assert.deepEqual([r.body.country, r.body.readiness, r.body.stale], ['NZ', 'not-ready', true]); pass();
    assert.equal(r.body.resources[0].phone, '111'); assert.ok(!JSON.stringify(r.body).includes('"000"')); pass();
    r = await pack('?country=GB');
    assert.deepEqual([r.body.readiness, r.body.stale], ['demo', true]); pass();
    for (const unsupported of ['FR', 'US']) {
      r = await pack('?country=' + unsupported);
      assert.deepEqual([r.body.country, r.body.readiness, r.body.resources.length], [unsupported, 'unsupported', 0]);
      assert.ok(!JSON.stringify(r.body).includes('"000"') && !JSON.stringify(r.body).includes('"phone"'), 'no numbers borrowed');
    }
    pass();
    r = await pack('?country=XX');
    assert.deepEqual([r.body.country, r.body.readiness, r.body.resources.length], [null, 'unknown', 0]); pass();
    for (const bad of ['AUS', '1', '%3Cx%3E', 'A'])
      assert.deepEqual(code(await (async () => { const x = await fetch(api + '/api/v1/safety/resources?country=' + bad); return { status: x.status, body: await x.json() }; })()), [400, 'unknown-country'], bad);
    pass();

    // ---- Nothing about the caller is stored; the member's profile, home city and modes don't change.
    const member = await createMember('safety');
    await cli('beta', 'add', member.email);
    const token = await memberToken(browser, member);
    await call(token, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Safety', homeCityId: 2078025 } });
    const before = JSON.stringify((await call(token, '/api/v1/me/profile')).body);
    const audits = await sql('SELECT count(*) FROM audit_entries');
    for (const c of ['NZ', 'GB', 'FR']) assert.equal((await call(token, '/api/v1/safety/resources?country=' + c)).status, 200);
    assert.equal(JSON.stringify((await call(token, '/api/v1/me/profile')).body), before, 'profile unchanged'); pass();
    assert.equal(await sql('SELECT count(*) FROM audit_entries'), audits, 'nothing recorded about reads'); pass();
    assert.equal(await sql(`SELECT count(*) FROM information_schema.columns WHERE table_name LIKE 'safety%' AND (column_name ~ '(^|_)ip(_|$)' OR column_name LIKE '%address%')`), '0', 'no IP column'); pass();

    // ---- Admin editing: admins only, validated, revisions, the readiness gate.
    const root = await rootAdminToken(browser);
    const staff = { adm: await createStaff('safety-adm'), sup: await createStaff('safety-sup'), leg: await createStaff('safety-leg') };
    for (const [n, role] of [['adm', 'admin'], ['sup', 'support'], ['leg', 'legal']])
      assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/v1/session'); }
    const admId = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st.adm).sub}'`);
    assert.equal((await call(null, '/api/v1/admin/safety/countries')).status, 401); pass();
    for (const who of [token, st.sup, st.leg]) assert.equal((await call(who, '/api/v1/admin/safety/countries')).status, 403);
    pass();
    const countries = (await call(st.adm, '/api/v1/admin/safety/countries')).body;
    assert.deepEqual(countries.find(c => c.code === 'AU').launchProblems, []); pass();
    assert.ok(countries.find(c => c.code === 'NZ').launchProblems.includes('unverified-resources')); pass();
    r = await call(st.adm, '/api/v1/admin/safety/countries/NZ', { method: 'PUT', body: { name: 'New Zealand', enabled: true, readiness: 'ready' } });
    assert.deepEqual(code(r), [409, 'country-not-ready']); assert.ok(r.body.problems.includes('unverified-resources')); pass();
    assert.equal((await pack('?country=NZ')).body.readiness, 'not-ready', 'a refused change leaves NZ as it was'); pass();
    // The suite's own test country starts empty on every run (earlier runs' contacts would block the launch check).
    await sql(`DELETE FROM safety_resources WHERE country_code = 'ZZ'`);
    const country = (body = {}) => call(st.adm, '/api/v1/admin/safety/countries/ZZ', { method: 'PUT', body: { name: 'Fictional test country', enabled: false, readiness: 'not-ready', ...body } });
    assert.equal((await country()).status, 200); pass();
    assert.deepEqual(code(await country({ readiness: 'launched' })), [400, 'invalid-resource']); pass();
    assert.deepEqual(code(await call(st.adm, '/api/v1/admin/safety/countries/ZZZ', { method: 'PUT', body: { name: 'x', enabled: false, readiness: 'demo' } })), [400, 'unknown-country']); pass();
    const resource = (body = {}) => ({ country: 'ZZ', language: 'en', category: 'emergency', name: 'Fictional emergency line', description: 'Fictional test contact.',
      phone: '999', phoneDisplay: '999', sourceUrl: 'https://example.org/source', order: 10, enabled: true, lastVerifiedOn: '2026-10-28', reviewDueOn: '2027-01-26', ...body });
    for (const [bad, why] of [[{ websiteUrl: 'http://example.org/' }, 'http'], [{ sourceUrl: 'javascript:alert(1)' }, 'script'], [{ phone: 'nine' }, 'not dialable'],
      [{ category: 'magic' }, 'category'], [{ name: '' }, 'name'], [{ phone: null, websiteUrl: null }, 'no contact'], [{ reviewDueOn: '2026-01-01' }, 'review before verified']])
      assert.equal((await call(st.adm, '/api/v1/admin/safety/resources', { method: 'POST', body: resource(bad) })).status, 400, why);
    pass();
    assert.deepEqual(code(await call(st.adm, '/api/v1/admin/safety/resources', { method: 'POST', body: resource({ country: 'QQ' }) })), [400, 'unknown-country']); pass();
    const raw = await fetch(api + '/api/v1/admin/safety/resources', { method: 'POST', headers: { Authorization: 'Bearer ' + st.adm, 'Content-Type': 'application/json' }, body: '{"country":"ZZ","order":"first"}' });
    assert.equal(raw.status, 400, 'tampered body'); pass();
    assert.equal((await call(st.sup, '/api/v1/admin/safety/resources', { method: 'POST', body: resource() })).status, 403); pass();
    r = await call(st.adm, '/api/v1/admin/safety/resources', { method: 'POST', body: resource() });
    assert.equal(r.status, 201); const created = r.body.id; pass();
    assert.equal((await country({ readiness: 'ready', lastReviewedOn: '2026-10-28', reviewDueOn: '2027-01-26' })).status, 200, 'verified emergency number: launch-ready'); pass();
    assert.equal((await pack('?country=ZZ')).body.readiness, 'unsupported', 'a hidden country is never served'); pass();
    r = await call(st.adm, `/api/v1/admin/safety/resources/${created}`, { method: 'PUT', body: resource({ lastVerifiedOn: null, reviewDueOn: null }) });
    assert.equal(r.status, 200); pass();
    assert.equal((await call(st.adm, '/api/v1/admin/safety/countries')).body.find(c => c.code === 'ZZ').readiness, 'not-ready', 'unverifying drops a launched pack'); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/safety/resources/${created}`, { method: 'PUT', body: resource({ country: 'AU' }) })), [400, 'invalid-resource'], 'a contact stays in its country'); pass();
    assert.equal((await call(st.adm, `/api/v1/admin/safety/resources/00000000-0000-0000-0000-000000000000`, { method: 'PUT', body: resource() })).status, 404); pass();
    const revisions = (await call(st.adm, '/api/v1/admin/safety/revisions?country=ZZ')).body.filter(v => v.actor === admId);
    for (const action of ['country.updated', 'resource.created', 'resource.updated', 'country.not-ready']) assert.ok(revisions.some(v => v.action === action), action);
    assert.ok(revisions.every(v => v.snapshot.length > 2)); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'safety.%' AND actor_account_id = '${admId}'`)) >= 4); pass();

    // ---- Web, signed out, phone width: /safety with the fallback, a session-scoped choice, neutral guidance.
    const phone = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await phone.newPage();
    await page.goto(web + '/safety');
    await page.waitForSelector('[data-safety-page="AU"]'); pass();
    assert.equal(await page.getAttribute('[data-country-hint]', 'data-country-hint'), 'fallback'); pass();
    assert.ok((await page.textContent('#danger-heading')).includes('000')); pass();
    assert.equal(await page.getAttribute('[data-call="000"]', 'href'), 'tel:000', 'a deliberate tap, nothing dials by itself'); pass();
    assert.ok(await page.locator('[data-swoovr-actions]').count() === 1 && (await page.textContent('[data-swoovr-actions]')).includes('serious safety incident'), "Swoovr's own actions apart"); pass();
    assert.equal(await page.locator('label[for="safety-country"]').count(), 1, 'the selector is labelled'); pass();
    await page.selectOption('[data-country-select]', 'NZ');
    await Promise.all([page.waitForURL(/country=NZ/), page.click('[data-country-form] button')]);
    await page.waitForSelector('[data-safety-page="NZ"]');
    assert.equal(await page.getAttribute('[data-country-hint]', 'data-country-hint'), 'manual'); pass();
    assert.ok(await page.locator('[data-safety-warning]').count() === 1, 'not-ready packs warn'); pass();
    await page.goto(web + '/safety');
    await page.waitForSelector('[data-safety-page="NZ"]', { timeout: 10000 }); pass();
    if (output) await page.screenshot({ path: output + '/web-safety-nz.png', fullPage: true });
    await page.goto(web + '/safety?country=FR');
    await page.waitForSelector('[data-safety-neutral]');
    assert.ok(!(await page.textContent('[data-safety-services]')).match(/\d{3}/), 'no numbers for an unsupported country'); pass();
    await page.goto(web + '/me/safety');
    assert.equal(await page.locator('[data-public-safety]').count(), 1, 'Me → Safety signed out points to the public page'); pass();
    await phone.close();

    // ---- Web, signed in: Me → Safety keeps Z's behaviour with the API pack and a session choice.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const me = await context.newPage();
    await me.goto(web + '/account/sign-in?intro=done');
    await me.fill('#username', member.email); await me.fill('#password', member.password);
    await Promise.all([me.waitForURL(web + '/account'), me.click('#kc-login')]);
    await me.goto(web + '/me/safety');
    await me.waitForSelector('[data-safety-services="AU"]', { timeout: 20000 });
    assert.ok((await me.textContent('[data-safety-services]')).includes('000')); pass();
    await me.selectOption('[data-country-select]', 'GB');
    await me.waitForSelector('[data-safety-services="GB"]');
    assert.ok((await me.textContent('[data-safety-warning]')).startsWith('Demonstration only')); pass();
    await me.reload();
    await me.waitForSelector('[data-safety-services="GB"]', { timeout: 20000 }); pass();
    assert.equal(JSON.stringify((await call(token, '/api/v1/me/profile')).body), before, 'choosing a country changes nothing on the profile'); pass();
    if (output) await me.screenshot({ path: output + '/web-me-safety.png', fullPage: true });
    await context.close();

    // ---- Admin app: the list with launch problems and a country's editor.
    const adminPage = await browser.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.adm.email); await adminPage.fill('#password', staff.adm.password); await adminPage.click('#kc-login');
    await adminPage.waitForURL(admin + '/**');
    await adminPage.goto(admin + '/safety/NZ');
    await adminPage.waitForSelector('[data-launch-problems="NZ"]'); pass();
    assert.ok(await adminPage.locator('[data-country-edit="NZ"]').count() === 1 && await adminPage.locator('[data-safety-resource]').count() >= 3); pass();
    await adminPage.selectOption('[data-country-edit="NZ"] [data-readiness-select]', 'ready');
    await adminPage.click('[data-save-country]');
    await adminPage.waitForSelector('[data-result="country-not-ready"]'); pass();
    if (output) await adminPage.screenshot({ path: output + '/admin-safety.png', fullPage: true });

    console.log(`PASS: country-aware safety resources (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
