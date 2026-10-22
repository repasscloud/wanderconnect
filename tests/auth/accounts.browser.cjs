// Local acceptance for application accounts (D55): real Keycloak tokens from actual Edge
// against the API with its PostgreSQL database. Needs the local stack, the API on
// 127.0.0.1:5080 with ConnectionStrings__Swoovr, an enrolled fictional root admin
// (tests/auth/root-admin.browser.cjs) and a Debug build of the API for operator commands.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, rootAdminToken, cli, sql, call, claims } = require('./local-tokens.cjs');

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

    // Own account and age entry (owner, 23 October 2026): DOB decides; age alone; both blank.
    let me = await call(aliceToken, '/api/v1/me');
    assert.equal(me.status, 200); pass();
    assert.deepEqual({ ...me.body, beta: undefined }, { ageStatus: 'attested', age: null, dateOfBirth: null, declaredAge: null, badges: [], beta: undefined }); pass();
    me = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth: '1990-05-01', age: 20 } });
    assert.equal(me.status, 200); assert.equal(me.body.ageStatus, 'selfDeclaredAdult'); assert.equal(me.body.dateOfBirth, '1990-05-01'); assert.equal(me.body.declaredAge, null); pass();
    assert.equal(me.body.age, new Date().getUTCFullYear() - 1990 - (new Date().toISOString().slice(5, 10) < '05-01' ? 1 : 0)); pass();
    me = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body: { age: 30 } });
    assert.deepEqual([me.body.age, me.body.dateOfBirth, me.body.declaredAge], [30, null, 30]); pass();
    me = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body: {} });
    assert.deepEqual([me.body.ageStatus, me.body.age], ['attested', null]); pass();
    for (const body of [{ dateOfBirth: '2999-01-01' }, { age: -1 }, { age: 121 }, { dateOfBirth: '1890-01-01' }]) {
      const bad = await call(aliceToken, '/api/v1/me/age', { method: 'PUT', body });
      assert.equal(bad.status, 400, JSON.stringify(body)); assert.equal(bad.body.code, 'invalid-age'); pass();
    }
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'age.updated' AND target_id = (SELECT id::text FROM accounts WHERE subject = '${sub}')`), '3'); pass();

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
    let locked = await call(bobToken, '/api/v1/me/age', { method: 'PUT', body: { age: 17 } });
    assert.equal(locked.status, 403); assert.equal(locked.body.code, 'account-locked'); pass();
    locked = await call(bobToken, '/api/v1/me');
    assert.equal(locked.status, 403); assert.equal(locked.body.code, 'account-locked'); pass();
    assert.equal((await call(bobToken, '/api/v1/session')).body.memberArea, false); pass();
    assert.equal((await call(bobToken, '/api/v1/me/age', { method: 'PUT', body: { age: 40 } })).status, 403, 'a locked account cannot re-enter an age'); pass();
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
    await webPage.reload();
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

    for (const extra of extras) await extra();
    console.log(`PASS: ${passed} account checks against the API and PostgreSQL with real local Keycloak tokens: (issuer, subject) accounts, member area only for members, age entry (DOB, age, blank, invalid) and under-18 lock, admin routes refused to members and unauthenticated callers, root admin only from the application record, staff/venue invitations (validation, revoke) never granting a member account by matching email, adult override, no email in the audit log; private beta gate (not-enrolled API refusal and web notice with the swoovr.com/beta link, admin add/list/revoke, activation bound to the account, revocation ends access, staff not gated, file import by operator command).`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
