// Local acceptance for application accounts (D55): real Keycloak tokens from actual Edge
// against the API with its PostgreSQL database. Needs the local stack, the API on
// 127.0.0.1:5080 with ConnectionStrings__Swoovr, an enrolled fictional root admin
// (tests/auth/root-admin.browser.cjs) and a Debug build of the API for operator commands.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { createMember, memberToken, rootAdminToken, cli, sql, call, claims } = require('./local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const extras = [];
  try {
    const alice = await createMember('alice');
    const bob = await createMember('bob');
    const enrol = async member => { if (process.env.SWOOVR_BETA_ENROL !== '0') await cli('beta', 'add', member.email).catch(() => {}); };
    await enrol(alice); await enrol(bob);
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

    for (const extra of extras) await extra();
    console.log(`PASS: ${passed} account checks against the API and PostgreSQL with real local Keycloak tokens: (issuer, subject) accounts, member area only for members, age entry (DOB, age, blank, invalid) and under-18 lock, admin routes refused to members and unauthenticated callers, root admin only from the application record, staff/venue invitations (validation, revoke) never granting a member account by matching email, adult override, no email in the audit log.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
