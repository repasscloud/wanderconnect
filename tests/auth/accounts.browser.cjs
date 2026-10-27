// Local acceptance for application accounts (D55): real Keycloak tokens from actual Edge
// against the API with its PostgreSQL database. Needs the local stack, the API on
// 127.0.0.1:5080 with ConnectionStrings__Swoovr, an enrolled fictional root admin
// (tests/auth/root-admin.browser.cjs) and a Debug build of the API for operator commands.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims, membersRealm, mailTo, sleep } = require('./local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const extras = [];
  try {
    const alice = await createMember('alice');
    const bob = await createMember('bob');
    await cli('beta', 'add', alice.email, bob.email);
    let aliceToken = await memberToken(browser, alice);

    // Session: account created from (issuer, subject); member area only.
    let session = await call(aliceToken, '/api/v1/session');
    assert.equal(session.status, 200); pass();
    assert.equal(session.body.memberArea, true); pass();
    assert.equal(session.body.adminArea, false); pass();
    assert.equal(session.body.supportArea, false); pass();
    const sub = claims(aliceToken).sub;
    assert.equal(await sql(`SELECT count(*) FROM accounts WHERE subject = '${sub}' AND realm = 'Members'`), '1'); pass();

    // Own account and age entry: a date of birth is required (D139, superseding the 23 October age-alone option).
    let me = await call(aliceToken, '/api/v1/me');
    assert.equal(me.status, 200); pass();
    assert.deepEqual({ ...me.body, beta: undefined }, { ageStatus: 'attested', age: null, dateOfBirth: null, declaredAge: null, badges: [], dateOfBirthVerified: false, accountState: 'active', dobCheck: 'none', phoneVerified: false, phoneRequired: false, beta: undefined }); pass();
    me = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1990-05-01', age: 20 } });
    assert.equal(me.status, 200); assert.equal(me.body.ageStatus, 'selfDeclaredAdult'); assert.equal(me.body.dateOfBirth, '1990-05-01'); assert.equal(me.body.declaredAge, null); pass();
    assert.equal(me.body.age, new Date().getUTCFullYear() - 1990 - (new Date().toISOString().slice(5, 10) < '05-01' ? 1 : 0)); pass();
    for (const body of [{ age: 30 }, {}, { age: -1 }, { age: 121 }]) {
      const bad = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body });
      assert.equal(bad.status, 400, JSON.stringify(body)); assert.equal(bad.body.code, 'date-of-birth-required'); pass();
    }
    for (const body of [{ dateOfBirth: '2999-01-01' }, { dateOfBirth: '1890-01-01' }]) {
      const bad = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body });
      assert.equal(bad.status, 400, JSON.stringify(body)); assert.equal(bad.body.code, 'invalid-age'); pass();
    }
    assert.equal((await call(aliceToken, '/api/v1/me')).body.dateOfBirth, '1990-05-01'); pass(); // refusals change nothing
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'age.updated' AND target_id = (SELECT id::text FROM accounts WHERE subject = '${sub}')`), '1'); pass();

    // A member cannot reach any admin route, whatever their email.
    for (const [method, path, body] of [['GET', '/api/v1/admin/invitations'], ['POST', '/api/v1/admin/invitations', { email: alice.email, role: 'admin' }],
      ['GET', '/api/v1/admin/accounts?email=' + encodeURIComponent(alice.email)], ['POST', '/api/v1/admin/accounts/00000000-0000-0000-0000-000000000001/adult-override']])
    { assert.equal((await call(aliceToken, path, { method, body })).status, 403, method + ' ' + path); pass(); }
    assert.equal((await call(null, '/api/v1/admin/invitations')).status, 401); pass();
    assert.equal((await call(null, '/api/v1/me')).status, 401); pass();

    // Root admin: the staff session alone is not enough; the application record grants it.
    let rootToken = await rootAdminToken(browser);
    session = await call(rootToken, '/api/v1/session');
    assert.equal(session.status, 200); assert.equal(session.body.realm, 'staff'); pass();
    const rootSub = claims(rootToken).sub;
    if (!session.body.adminArea) {
      assert.equal((await call(rootToken, '/api/v1/admin/invitations')).status, 403); pass();
      await cli('root-admin', 'grant', rootSub);
      session = await call(rootToken, '/api/v1/session');
    }
    assert.equal(session.body.adminArea, true); pass();
    assert.equal(session.body.memberArea, false); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'root-admin.granted'`) >= '1', true); pass();
    await assert.rejects(cli('root-admin', 'grant', sub), /staff-realm/); pass();

    // Invitations: a staff invitation for a member's email never reaches the member account (no merge by email).
    let created = await call(rootToken, '/api/v1/admin/invitations', { method: 'POST', body: { email: alice.email.toUpperCase(), role: 'support' } });
    assert.equal(created.status, 201); assert.equal(created.body.realm, 'staff'); assert.equal(created.body.state, 'pending'); assert.equal(created.body.email, alice.email); pass();
    aliceToken = await memberToken(browser, alice);
    session = await call(aliceToken, '/api/v1/session');
    assert.deepEqual([session.body.adminArea, session.body.supportArea, session.body.memberArea], [false, false, true]); pass();
    let list = await call(rootToken, '/api/v1/admin/invitations');
    assert.equal(list.body.find(i => i.id === created.body.id).state, 'pending'); pass();
    for (const body of [{ email: alice.email, role: 'member' }, { email: alice.email, role: 'venuePartner' }, { email: 'nope', role: 'admin' },
      { email: alice.email, role: 'admin', venueId: '33333333-3333-3333-3333-333333333333' }, { email: alice.email, role: '999' }])
    { assert.equal((await call(rootToken, '/api/v1/admin/invitations', { method: 'POST', body })).status, 400, JSON.stringify(body)); pass(); }
    const venue = await call(rootToken, '/api/v1/admin/invitations', { method: 'POST', body: { email: 'venue.' + alice.email, role: 'venuePartner', venueId: '33333333-3333-3333-3333-333333333333' } });
    assert.equal(venue.body.realm, 'venues'); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/invitations/' + created.body.id, { method: 'DELETE' })).status, 204); pass();
    list = await call(rootToken, '/api/v1/admin/invitations');
    assert.equal(list.body.find(i => i.id === created.body.id).state, 'revoked'); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/invitations/00000000-0000-0000-0000-000000000001', { method: 'DELETE' })).status, 404); pass();

    // Under-18 entry locks the account (D40); support/admin override lifts it.
    const bobToken = await memberToken(browser, bob);
    assert.equal((await call(bobToken, '/api/v1/me')).status, 200); pass();
    const seventeen = new Date(Date.now() - 17.5 * 365.25 * 86400000).toISOString().slice(0, 10);
    let locked = await call(bobToken, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: seventeen } });
    assert.equal(locked.status, 403); assert.equal(locked.body.code, 'account-locked'); pass();
    locked = await call(bobToken, '/api/v1/me');
    assert.equal(locked.status, 403); assert.equal(locked.body.code, 'account-locked'); pass();
    assert.equal((await call(bobToken, '/api/v1/session')).body.memberArea, false); pass();
    assert.equal((await call(bobToken, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1985-01-01' } })).status, 403, 'a locked account cannot re-enter an age'); pass();
    const found = await call(rootToken, '/api/v1/admin/accounts?email=' + encodeURIComponent(bob.email.toUpperCase()));
    assert.equal(found.status, 200); assert.equal(found.body.length, 1);
    assert.deepEqual([found.body[0].status, found.body[0].statusReason, found.body[0].ageStatus], ['locked', 'underage', 'underage']); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/accounts?email=not-an-email')).status, 400); pass();
    assert.equal((await call(rootToken, `/api/v1/admin/accounts/${found.body[0].id}/adult-override`, { method: 'POST' })).status, 204); pass();
    me = await call(bobToken, '/api/v1/me');
    assert.equal(me.status, 200); assert.equal(me.body.ageStatus, 'adultOverride'); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/accounts/00000000-0000-0000-0000-000000000001/adult-override', { method: 'POST' })).status, 404); pass();

    // The audit log never holds an email address.
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%@%' OR target_id LIKE '%@%'`), '0'); pass();

    // Private beta (D62): a verified member who is not enrolled signs in but cannot use the member area.
    const carol = await createMember('carol');
    let carolToken = await memberToken(browser, carol);
    session = await call(carolToken, '/api/v1/session');
    assert.deepEqual([session.status, session.body.memberArea, session.body.beta], [200, false, 'notEnrolled']); pass();
    me = await call(carolToken, '/api/v1/me');
    assert.deepEqual([me.status, me.body.code], [403, 'beta-not-enrolled']); pass();
    assert.equal((await call(carolToken, '/api/v1/me/age', { method: 'PUT', body: { age: 30 } })).status, 403); pass();
    // The web account page shows the neutral private-beta notice and the swoovr.com/beta link.
    const webContext = await browser.newContext(); const webPage = await webContext.newPage();
    await webPage.goto(web + '/account/sign-in?intro=done');
    await webPage.fill('#username', carol.email); await webPage.fill('#password', carol.password);
    await Promise.all([webPage.waitForURL(web + '/account'), webPage.click('#kc-login')]);
    await webPage.waitForSelector('[data-account-panel=ready]', { timeout: 20000 });
    assert.equal(await webPage.locator('[data-beta=notEnrolled]').count(), 1); pass();
    assert.equal(await webPage.locator('[data-beta] a').getAttribute('href'), 'https://swoovr.com/beta/'); pass();
    assert.match(await webPage.locator('[data-beta]').innerText(), /private beta[\s\S]*not on the list yet/); pass();
    assert.equal(await webPage.locator('[data-member-area=false]').count(), 1); pass();
    await webPage.screenshot({ path: '.local/auth-browser/beta-not-enrolled.png', fullPage: true });
    // Members cannot manage enrolments; admins can.
    for (const [method, path, body] of [['GET', '/api/v1/admin/beta'], ['POST', '/api/v1/admin/beta', { email: carol.email }], ['DELETE', '/api/v1/admin/beta/00000000-0000-0000-0000-000000000001']])
    { assert.equal((await call(aliceToken, path, { method, body })).status, 403, method + ' ' + path); pass(); }
    assert.equal((await call(rootToken, '/api/v1/admin/beta', { method: 'POST', body: { email: 'not-an-email' } })).status, 400); pass();
    const added = await call(rootToken, '/api/v1/admin/beta', { method: 'POST', body: { email: '  ' + carol.email.toUpperCase() } });
    assert.deepEqual([added.status, added.body.state, added.body.email], [201, 'pending', carol.email]); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/beta', { method: 'POST', body: { email: carol.email } })).status, 200, 'adding twice keeps one'); pass();
    session = await call(carolToken, '/api/v1/session');
    assert.deepEqual([session.body.memberArea, session.body.beta], [true, 'enrolled']); pass();
    assert.equal((await call(carolToken, '/api/v1/me')).status, 200); pass();
    await webPage.reload(); await webPage.waitForSelector('[data-account-panel=ready]', { timeout: 20000 });
    assert.equal(await webPage.locator('[data-beta]').count(), 0); pass();
    assert.equal(await webPage.locator('[data-member-area=true]').count(), 1); pass();
    await webContext.close();
    const enrolments = await call(rootToken, '/api/v1/admin/beta');
    assert.equal(enrolments.body.find(e => e.id === added.body.id).state, 'activated'); pass();
    assert.equal(await sql(`SELECT count(*) FROM beta_enrolments WHERE id = '${added.body.id}' AND account_id = (SELECT id FROM accounts WHERE subject = '${claims(carolToken).sub}')`), '1', 'bound to the account'); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/beta/' + added.body.id, { method: 'DELETE' })).status, 204); pass();
    session = await call(carolToken, '/api/v1/session');
    assert.deepEqual([session.body.memberArea, session.body.beta], [false, 'notEnrolled'], 'revoking ends access'); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/beta/00000000-0000-0000-0000-000000000001', { method: 'DELETE' })).status, 404); pass();
    // Staff are not gated by the beta.
    assert.equal((await call(rootToken, '/api/v1/session')).body.beta, 'notRequired'); pass();
    // The operator command loads a file of fictional addresses and reports counts only.
    const output = await cli('beta', 'add', '--file', 'containers/postgres/beta-enrolment.fictional.txt');
    assert.match(output, /Beta enrolments: \d+ added, \d+ already enrolled, 0 invalid\./); assert.doesNotMatch(output, /@/); pass();
    await assert.rejects(cli('beta', 'add', 'not-an-email'), /./); pass();
    assert.equal(await sql(`SELECT count(*) FROM beta_enrolments WHERE email = 'fictional.tester.one@example.test' AND revoked_at IS NULL`), '1'); pass();

    // Founding membership and recovery token (D63, D65).
    const dave = await createMember('dave'); const erin = await createMember('erin');
    await cli('beta', 'add', dave.email, erin.email);
    let daveToken = await memberToken(browser, dave); const erinToken = await memberToken(browser, erin);
    assert.equal((await call(daveToken, '/api/v1/me')).status, 200); assert.equal((await call(erinToken, '/api/v1/me')).status, 200);
    const daveId = (await call(rootToken, '/api/v1/admin/accounts?email=' + encodeURIComponent(dave.email))).body[0].id;
    assert.equal((await call(aliceToken, `/api/v1/admin/accounts/${daveId}/founder`, { method: 'POST' })).status, 403, 'members cannot grant'); pass();
    // D130: an admin exception still needs ID or Photo verification. Dave's ID status is set directly (simulated for
    // this local test only; tests/verification/didit.browser.cjs covers the real path).
    assert.equal((await call(rootToken, `/api/v1/admin/accounts/${daveId}/founder`, { method: 'POST' })).body.code, 'founder-not-eligible'); pass();
    await sql(`UPDATE member_profiles SET id_status = 'Verified', id_checked_at = now() WHERE account_id = '${daveId}'`);
    const placesBefore = Number(await sql('SELECT count(*) FROM founder_places'));
    const grant =await call(rootToken, `/api/v1/admin/accounts/${daveId}/founder`, { method: 'POST' });
    assert.equal(grant.status, 201); assert.equal(grant.body.emailSent, true); assert.equal(grant.body.place, placesBefore + 1); pass();
    assert.equal((await call(rootToken, `/api/v1/admin/accounts/${daveId}/founder`, { method: 'POST' })).body.code, 'already-founder'); pass();
    let mail = []; for (let i = 0; i < 20 && !mail.length; i++) { mail = await mailTo(dave.email); if (!mail.length) await sleep(250); }
    const firstCode = mail[0].match(/FOUNDER-V1-[0-9A-F]{64}/)?.[0];
    assert.ok(firstCode, 'founder code emailed through the local mail sink'); pass();
    me = await call(daveToken, '/api/v1/me');
    assert.deepEqual(me.body.badges.map(b => b.badge), ['founder', 'idVerified']); pass(); // the simulated ID status above
    assert.equal((await call(daveToken, '/api/v1/me/coupons', { method: 'POST', body: { code: firstCode.toLowerCase() } })).status, 200, 'own code is a no-op'); pass();
    // A shared or sold code fails for anyone else, and every refusal reads the same.
    let refused = await call(erinToken, '/api/v1/me/coupons', { method: 'POST', body: { code: firstCode } });
    assert.deepEqual([refused.status, refused.body.code], [400, 'coupon-invalid']); pass();
    for (const code of ['WELCOME10', 'FOUNDER-V1-' + '0'.repeat(64), 'FOUNDER-V9-' + firstCode.slice(11), 'FOUNDER-nonsense'])
    { refused = await call(erinToken, '/api/v1/me/coupons', { method: 'POST', body: { code } }); assert.deepEqual([refused.status, refused.body.code], [400, 'coupon-invalid'], code); pass(); }
    refused = await call(erinToken, '/api/v1/me/coupons', { method: 'POST', body: { code: firstCode } });
    assert.deepEqual([refused.status, refused.body.code], [429, 'too-many-attempts'], 'five failures an hour, then refused'); pass();
    assert.equal((await call(erinToken, '/api/v1/me')).body.badges.length, 0); pass();
    // Email change (owner answer): a new code for the new address; the old one stops working.
    const realmApi = await membersRealm();
    const daveUser = (await realmApi('/users?exact=true&email=' + encodeURIComponent(dave.email)))[0];
    const changedEmail = 'changed.' + dave.email;
    await realmApi('/users/' + daveUser.id, 'PUT', { ...daveUser, username: changedEmail, email: changedEmail, emailVerified: true });
    daveToken = await memberToken(browser, { email: changedEmail, password: dave.password });
    assert.equal((await call(daveToken, '/api/v1/me')).status, 200, 'beta access follows the account after an email change'); pass();
    mail = []; for (let i = 0; i < 20 && !mail.length; i++) { mail = await mailTo(changedEmail); if (!mail.length) await sleep(250); }
    const secondCode = mail[0]?.match(/FOUNDER-V1-[0-9A-F]{64}/)?.[0];
    assert.ok(secondCode && secondCode !== firstCode, 'new code for the new address'); pass();
    let tokens = (await call(rootToken, '/api/v1/admin/founder-tokens?place=' + grant.body.place)).body;
    assert.deepEqual(tokens.map(t => [t.status, t.invalidatedReason, t.secretVersion]), [['invalidated', 'email-changed', 1], ['valid', null, 1]]); pass();
    assert.equal(JSON.stringify(tokens).includes('FOUNDER-'), false, 'admins never see the code'); pass();
    // Deletion and return: the account row goes, the token row stays; a new account restores the same place.
    await sql(`DELETE FROM accounts WHERE id = '${daveId}'`);
    await realmApi('/users/' + daveUser.id, 'DELETE');
    const returning = { email: changedEmail, password: 'Correct-horse-' + Date.now() };
    await realmApi('/users', 'POST', { username: changedEmail, email: changedEmail, firstName: 'Fictional', enabled: true, emailVerified: true,
      credentials: [{ type: 'password', value: returning.password, temporary: false }] });
    await cli('beta', 'add', changedEmail);
    const returnToken = await memberToken(browser, returning);
    assert.notEqual(claims(returnToken).sub, daveUser.id); assert.deepEqual((await call(returnToken, '/api/v1/me')).body.badges, []); pass();
    refused = await call(returnToken, '/api/v1/me/coupons', { method: 'POST', body: { code: firstCode } });
    assert.equal(refused.status, 400, 'the invalidated code no longer restores'); pass();
    const restored = await call(returnToken, '/api/v1/me/coupons', { method: 'POST', body: { code: secondCode } });
    assert.equal(restored.status, 200); assert.deepEqual(restored.body.badges.map(b => b.badge), ['founder']); pass();
    assert.equal(Number(await sql('SELECT count(*) FROM founder_places')), placesBefore + 1, 'restoring never takes a new place'); pass();
    assert.equal(await sql(`SELECT founder_place FROM member_profiles WHERE account_id = (SELECT id FROM accounts WHERE subject = '${claims(returnToken).sub}')`), String(grant.body.place)); pass();
    // Admins can invalidate and delete tokens; the live account keeps its status.
    const live = tokens.find(t => t.status === 'valid');
    assert.equal((await call(rootToken, `/api/v1/admin/founder-tokens/${live.id}/invalidate`, { method: 'POST' })).status, 204); pass();
    assert.equal((await call(rootToken, '/api/v1/admin/founder-tokens/' + tokens[0].id, { method: 'DELETE' })).status, 204); pass();
    tokens = (await call(rootToken, '/api/v1/admin/founder-tokens?place=' + grant.body.place)).body;
    assert.deepEqual(tokens.map(t => [t.status, t.invalidatedReason]), [['invalidated', 'admin']]); pass();
    assert.deepEqual((await call(returnToken, '/api/v1/me')).body.badges.map(b => b.badge), ['founder']); pass();
    for (const [method, path] of [['GET', '/api/v1/admin/founder-tokens?place=1'], ['POST', `/api/v1/admin/founder-tokens/${live.id}/invalidate`], ['DELETE', `/api/v1/admin/founder-tokens/${live.id}`]])
    { assert.equal((await call(erinToken, path, { method })).status, 403, method + ' ' + path); pass(); }
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%FOUNDER%' OR target_id LIKE '%FOUNDER%' OR detail LIKE '%@%'`), '0', 'no code or address in the audit log'); pass();

    // The WebAssembly account page (D59) through the backend-for-frontend.
    const ui = await browser.newContext(); const page = await ui.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', returning.email); await page.fill('#password', returning.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.waitForSelector('[data-account-panel=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-badges=founder] [data-badge=founding]').count(), 1, 'founder badge drawn from the API'); pass();
    assert.ok(await page.evaluate(() => [...performance.getEntriesByType('resource')].some(r => r.name.includes('/_framework/') && r.name.endsWith('.wasm'))), 'runs as WebAssembly'); pass();
    await page.fill('#dob', '1990-05-01'); await page.click('[data-age-form] [type=submit]');
    await page.waitForSelector('[data-age-status=selfDeclaredAdult]');
    assert.match(await page.locator('[data-age-form] [role=status]').innerText(), /Saved\. Your age shows as \d+\./); pass();
    // D139: the date of birth is required; there's no age-only field, and an empty date can't be saved.
    assert.equal(await page.locator('#age').count(), 0); pass();
    await page.fill('#dob', '');
    assert.equal(await page.locator('[data-age-form] [type=submit]').isDisabled(), true); pass();
    assert.equal(JSON.parse(await sql(`SELECT json_build_array(date_of_birth, declared_age)::text FROM member_profiles WHERE account_id = (SELECT id FROM accounts WHERE subject = '${claims(returnToken).sub}')`))[0], '1990-05-01'); pass();
    await page.fill('#coupon', 'WELCOME10'); await page.click('[data-coupon-form] [type=submit]');
    await page.waitForSelector('[data-coupon-result=invalid]');
    assert.equal(await page.locator('[data-coupon-form] [role=status]').innerText(), "That code can't be used."); pass();
    await page.screenshot({ path: '.local/auth-browser/account-wasm.png', fullPage: true });
    // BFF boundary: header required, only the member's own routes, no token in the browser.
    const probe = await page.evaluate(async () => {
      const get = async (path, headers = {}) => (await fetch(path, { headers })).status;
      return [await get('/bff/api/v1/session'), await get('/bff/api/v1/session', { 'X-Swoovr-Bff': '1' }), await get('/bff/api/v1/admin/beta', { 'X-Swoovr-Bff': '1' }),
        await get('/bff/api/v1/prototype/people', { 'X-Swoovr-Bff': '1' }), await get('/bff/health/live', { 'X-Swoovr-Bff': '1' })];
    });
    assert.deepEqual(probe, [400, 200, 404, 404, 404]); pass();
    const visible = await page.evaluate(() => document.documentElement.outerHTML + JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + document.cookie);
    assert.doesNotMatch(visible, /eyJ[A-Za-z0-9_-]{10,}\./, 'no token in the page, storage or readable cookies'); pass();
    assert.equal((await fetch(web + '/bff/api/v1/session', { headers: { 'X-Swoovr-Bff': '1' } })).status, 401, 'no session cookie, no access'); pass();
    // Under-18 entry in the WebAssembly page locks the account and shows the locked notice.
    const young = await createMember('young'); await cli('beta', 'add', young.email);
    const youngPage = await (await browser.newContext()).newPage();
    await youngPage.goto(web + '/account/sign-in?intro=done');
    await youngPage.fill('#username', young.email); await youngPage.fill('#password', young.password); await youngPage.click('#kc-login');
    // First sign-in of an admin-created member stops at the 18+ terms action.
    await youngPage.waitForURL(u => u.href.includes('TERMS_AND_CONDITIONS') || u.href === web + '/account');
    if (youngPage.url().includes('TERMS_AND_CONDITIONS')) await youngPage.click('[name=accept]');
    await youngPage.waitForURL(web + '/account'); await youngPage.waitForSelector('[data-account-panel=ready]', { timeout: 20000 });
    await youngPage.fill('#dob', new Date(Date.now() - 16.5 * 365.25 * 86400000).toISOString().slice(0, 10)); await youngPage.click('[data-age-form] [type=submit]');
    await youngPage.waitForSelector('[data-account-locked=true]');
    assert.equal(await youngPage.locator('[data-age-form]').count(), 0); pass();
    await youngPage.reload(); await youngPage.waitForSelector('[data-account-panel=ready]', { timeout: 20000 });
    assert.equal(await youngPage.locator('[data-account-locked=true]').count(), 1, 'still locked after reload'); pass();
    assert.equal(await youngPage.locator('[data-member-area=false]').count(), 1); pass();
    await ui.close();

    for (const extra of extras) await extra();
    console.log(`PASS: ${passed} account checks against the API and PostgreSQL with real local Keycloak tokens: (issuer, subject) accounts, member area only for members, age entry (DOB, age, blank, invalid) and under-18 lock, admin routes refused to members and unauthenticated callers, root admin only from the application record, staff/venue invitations (validation, revoke) never granting a member account by matching email, adult override, no email in the audit log; private beta gate (not-enrolled API refusal and web notice with the swoovr.com/beta link, admin add/list/revoke, activation bound to the account, revocation ends access, staff not gated, file import by operator command); founding membership (admin grant, code emailed to the mail sink, founder badge, shared/sold/unknown codes refused alike, 5 failures an hour then 429, email change reissues and invalidates, delete-and-return restores the same place, admin invalidate/delete, codes never shown to admins or written to the audit log); the Interactive WebAssembly account page through the backend-for-frontend (founder badge, age entry and validation, coupon refusal, under-18 lock, header and route allow-list, no token in the browser).`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
