// Local acceptance for slice V (Q43, D144; owner answers Q61–Q65, D169–D173): account deletion and data export.
// Deletion: a fresh sign-in, immediate deactivation (out of discovery, signed out everywhere, pending requests
// cancelled), cancel within 30 days, delete now, `accounts purge` (Keycloak user deleted, personal data removed, a
// tombstone left, history readable by the other member as "Deleted member"), founders (code emailed, token kept or
// deleted on request), terminated members (a persistent typed registry restriction, returning application registration refused).
// Export: once a day, a one-time 15-minute link, the member's own data only. Negative cases: staff, other members'
// links, stale sign-ins, tampered bodies, unknown links, restricted members still able to leave. The web page in Edge:
// download, delete, the pending-deletion screen and the sign-in-again step.
// Run against hosts started with Accounts__ReauthSeconds=45 so the stale-sign-in cases don't wait five minutes.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, createMember, memberToken, rootAdminToken, cli, sql, call, claims, matchReady, membersRealm, mailTo, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const REAUTH_SECONDS = Number(process.env.SWOOVR_REAUTH_SECONDS ?? 45);

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat', 'fou', 'ter', 'res', 'exp', 'webm'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('del-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    const fresh = async n => (tokens[n] = await memberToken(browser, members[n]));
    for (const n of names) {
      await fresh(n);
      pid[n] = (await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Del ' + n, homeCityId: ADELAIDE } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const staleExp = tokens.exp; // signed in now; stale once REAUTH_SECONDS have passed

    // The gate: signed out, staff and tampered bodies.
    assert.equal((await call(null, '/api/me/deletion')).status, 401); pass();
    assert.equal((await call(root, '/api/me/deletion')).status, 403, 'staff realm'); pass();
    assert.equal((await call(root, '/api/me/export', { method: 'POST' })).status, 403); pass();
    let r = await call(tokens.ana, '/api/me/deletion');
    assert.deepEqual([r.status, r.body.state, r.body.founder], [200, 'none', false]); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: false, immediately: true, deleteFounderToken: false } })), [400, 'confirmation-required']); pass();
    const raw = await fetch(api + '/api/me/deletion', { method: 'POST', headers: { Authorization: 'Bearer ' + tokens.ana, 'Content-Type': 'application/json' }, body: '{"confirm":"yes please"' });
    assert.equal(raw.status, 400, 'a tampered body is refused'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/deletion', { method: 'DELETE' })), [409, 'deletion-not-pending']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/deletion/now', { method: 'POST' })), [409, 'deletion-not-pending']); pass();
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.ana}'`), 'Active', 'refusals changed nothing'); pass();

    // Ana and Bob are connected and talking; Cat has a pending request to Ana.
    const req = await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 'friends' } });
    assert.equal((await call(tokens.bob, `/api/requests/${req.body.id}/accept`, { method: 'POST' })).status, 200);
    const pending = await call(tokens.cat, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } });
    assert.equal(pending.status, 201);
    const convo = (await call(tokens.ana, '/api/conversations', { method: 'POST', body: { to: pid.bob } })).body.id;
    assert.equal((await call(tokens.ana, `/api/conversations/${convo}/messages`, { method: 'POST', body: { text: 'Hello from a fictional Ana' } })).status, 201);
    assert.equal((await call(tokens.bob, `/api/conversations/${convo}/messages`, { method: 'POST', body: { text: 'Hi Ana, fictional Bob here' } })).status, 201);

    // Export (D172): a one-time link for the member who asked, once a day.
    const ticket = await call(tokens.ana, '/api/me/export', { method: 'POST' });
    assert.equal(ticket.status, 200); assert.match(ticket.body.path, /^api\/me\/export\/[A-Za-z0-9_-]{43}$/); pass();
    assert.ok(new Date(ticket.body.expiresAt) - Date.now() <= 15 * 60 * 1000 + 5000); pass();
    assert.deepEqual(code(await call(tokens.bob, '/' + ticket.body.path)), [404, 'export-unavailable'], "someone else's link"); pass();
    const download = await fetch(api + '/' + ticket.body.path, { headers: { Authorization: 'Bearer ' + tokens.ana } });
    assert.equal(download.status, 200); assert.match(download.headers.get('content-disposition') ?? '', /attachment; filename=swoovr-data-\d{4}-\d{2}-\d{2}\.json/); pass();
    const exported = JSON.parse(await download.text());
    assert.equal(exported.format, 'swoovr-data-export/1'); pass();
    assert.equal(exported.account.email, members.ana.email); assert.equal(exported.profile.displayName, 'Fictional Del ana'); pass();
    assert.equal(exported.profile.dateOfBirth, '1990-01-01'); assert.equal(exported.profile.homeCity.startsWith('Adelaide'), true); pass();
    assert.deepEqual(exported.modes.map(m => m.mode), ['friends']); pass();
    const chat = exported.conversations.find(c => c.with === 'Fictional Del bob');
    assert.deepEqual(chat.messages.map(m => [m.from, m.text]), [['you', 'Hello from a fictional Ana'], ['them', 'Hi Ana, fictional Bob here']]); pass();
    assert.deepEqual(exported.requests.map(q => [q.direction, q.with, q.status]).sort(), [['received', 'Fictional Del cat', 'pending'], ['sent', 'Fictional Del bob', 'accepted']]); pass();
    const text = JSON.stringify(exported);
    for (const other of ['bob', 'cat']) assert.equal(text.includes(members[other].email), false, 'no other member\'s email'); pass();
    assert.equal(text.includes(ids.bob) || text.includes(ids.cat), false, 'no other member\'s account id'); pass();
    assert.equal(text.includes('issuer') || text.includes(claims(tokens.ana).sub), false, 'no Keycloak identifiers'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/' + ticket.body.path)), [404, 'export-unavailable'], 'works once'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/export', { method: 'POST' })), [429, 'export-too-soon']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/export/' + 'A'.repeat(43))), [404, 'export-unavailable'], 'unknown link'); pass();
    assert.equal((await call(tokens.ana, '/api/me/export/short')).status, 404); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE actor_account_id = '${ids.ana}' AND action IN ('export.requested', 'export.downloaded')`), '2'); pass();
    assert.equal(await sql(`SELECT count(*) FROM data_exports WHERE account_id = '${ids.ana}'`), '1', 'one ticket row, hash only'); pass();

    // Request deletion with the 30-day recovery period (D170).
    await fresh('ana');
    const kc = await membersRealm();
    const anaUser = claims(tokens.ana).sub;
    assert.ok((await kc(`/users/${anaUser}/sessions`)).length >= 1, 'signed in before'); pass();
    r = await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: false, deleteFounderToken: false } });
    assert.deepEqual([r.status, r.body.state, r.body.signedOutEverywhere], [200, 'pending', true]); pass();
    const due = new Date(r.body.dueAt) - new Date(r.body.requestedAt);
    assert.equal(Math.round(due / 86400000), 30); pass();
    assert.equal((await kc(`/users/${anaUser}/sessions`)).length, 0, 'every Keycloak session ended'); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE id = '${pending.body.id}'`), 'Cancelled', 'pending requests cancelled'); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE id = '${req.body.id}'`), 'Accepted', 'connections wait'); pass();
    r = await call(tokens.ana, '/api/me');
    assert.deepEqual([r.status, r.body.accountState], [200, 'pendingDeletion']); pass();
    for (const [method, path, body] of [['GET', '/api/explore?mode=friends'], ['GET', '/api/conversations'], ['POST', '/api/requests', { to: pid.res, mode: 'friends' }], ['GET', '/api/me/profile']])
    { assert.deepEqual(code(await call(tokens.ana, path, { method, body })), [403, 'account-deletion-pending'], path); pass(); }
    assert.deepEqual(code(await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })), [409, 'deletion-already-pending']); pass();
    assert.equal((await call(tokens.bob, `/api/profiles/${pid.ana}`)).status, 404, 'out of discovery at once'); pass();
    assert.equal((await call(tokens.bob, '/api/explore?mode=friends')).body.people.some(p => p.id === pid.ana), false); pass();
    const bobView = (await call(tokens.bob, '/api/conversations')).body.find(c => c.id === convo);
    assert.equal(bobView.canSend, false, 'the conversation is not available while pending'); pass();
    assert.match(await sql(`SELECT string_agg(coalesce(detail, ''), ',') FROM audit_entries WHERE target_id = '${ids.ana}' AND action LIKE 'account.deletion%'`), /^30-days$/); pass();

    // Cancel (fresh sign-in) brings everything back.
    await fresh('ana');
    r = await call(tokens.ana, '/api/me/deletion', { method: 'DELETE' });
    assert.deepEqual([r.status, r.body.state, r.body.dueAt], [200, 'none', null]); pass();
    assert.equal((await call(tokens.ana, '/api/explore?mode=friends')).status, 200); pass();
    assert.equal((await call(tokens.bob, `/api/profiles/${pid.ana}`)).status, 200, 'back in discovery'); pass();

    // Request again, then delete now, then the purge job.
    await fresh('ana');
    assert.equal((await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: false, deleteFounderToken: false } })).status, 200);
    await fresh('ana');
    const anaLast = tokens.ana;
    r = await call(tokens.ana, '/api/me/deletion/now', { method: 'POST' });
    assert.equal(r.status, 200); assert.ok(new Date(r.body.dueAt) <= new Date()); pass();
    // Cat deletes at once (no recovery period).
    await fresh('cat');
    r = await call(tokens.cat, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } });
    assert.equal(r.status, 200); pass();

    // A founder: the code is emailed at the request; asking for the token to go too deletes it at final deletion.
    await sql(`UPDATE member_profiles SET id_status = 'Verified', id_checked_at = now() WHERE account_id = '${ids.fou}'`);
    const grant = await call(root, `/api/admin/accounts/${ids.fou}/founder`, { method: 'POST' });
    assert.equal(grant.status, 201); const place = grant.body.place;
    await fresh('fou');
    assert.equal((await call(tokens.fou, '/api/me/deletion')).body.founder, true); pass();
    const mailBefore = (await mailTo(members.fou.email)).length;
    r = await call(tokens.fou, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: false, deleteFounderToken: false } });
    assert.equal(r.body.founderCodeEmailed, true); pass();
    let mail = []; for (let i = 0; i < 20 && mail.length <= mailBefore; i++) { mail = await mailTo(members.fou.email); await sleep(250); }
    assert.match(mail[0], /FOUNDER-V1-[0-9A-F]{64}/, 'the founder code arrived before deletion'); pass();
    await fresh('fou');
    await call(tokens.fou, '/api/me/deletion', { method: 'DELETE' });
    await fresh('fou');
    r = await call(tokens.fou, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: true } });
    assert.deepEqual([r.status, r.body.deleteFounderToken, r.body.founderCodeEmailed], [200, true, false]); pass();

    // A terminated member may still delete; the typed contact restriction survives and blocks reuse (D217).
    await elevate(root, 'termination', 'Fictional termination for the deletion suite'); // #88
    const term = await call(root, `/api/admin/accounts/${ids.ter}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'Fictional test termination' } });
    assert.ok([200, 201].includes(term.status), 'terminated'); pass();
    await fresh('ter');
    assert.deepEqual(code(await call(tokens.ter, '/api/explore?mode=friends')), [403, 'account-suspended']); pass();
    r = await call(tokens.ter, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } });
    assert.equal(r.status, 200, 'a terminated member can leave'); pass();
    // A restricted member can open deletion and export too.
    assert.ok([200, 201].includes((await call(root, `/api/admin/accounts/${ids.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional', days: 3 } })).status));
    assert.equal((await call(tokens.res, '/api/me/deletion')).status, 200); pass();

    const holdsBefore = Number(await sql('SELECT count(*) FROM terminated_email_holds'));
    const purge = await cli('accounts', 'purge');
    assert.match(purge, /Accounts: \d+ deleted, 0 waiting for Keycloak/); pass();
    assert.doesNotMatch(purge, /@/); pass();
    for (const n of ['ana', 'cat', 'fou', 'ter']) {
      const row = (await sql(`SELECT status || '|' || coalesce(email, '') || '|' || (deleted_at IS NOT NULL AND email_accepted_at IS NULL) FROM accounts WHERE id = '${ids[n]}'`));
      assert.equal(row, 'Deleted||true', n + ' is a tombstone'); pass();
      assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id = '${ids[n]}'`), '0'); pass();
      assert.equal((await kc('/users?exact=true&email=' + encodeURIComponent(members[n].email))).length, 0, n + "'s Keycloak user deleted"); pass();
    }
    for (const table of ['member_modes', 'mode_filters', 'member_identities', 'trips', 'data_exports', 'beta_enrolments'])
    { assert.equal(await sql(`SELECT count(*) FROM ${table} WHERE account_id = '${ids.ana}'`), '0', table); pass(); }
    assert.equal(await sql(`SELECT count(*) FROM connection_requests WHERE '${ids.ana}' IN (from_account_id, to_account_id)`), '0', 'requests'); pass();
    assert.equal(await sql(`SELECT count(*) FROM messages WHERE sender_account_id = '${ids.ana}'`), '0', "Ana's messages are gone"); pass();
    assert.equal(await sql(`SELECT count(*) FROM messages WHERE sender_account_id = '${ids.bob}' AND conversation_id = '${convo}'`), '1', "Bob's stay"); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id = '${ids.ana}' AND action = 'account.deleted'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id IN ('${ids.ana}','${ids.cat}','${ids.fou}','${ids.ter}') AND detail LIKE '%@%'`), '0', 'no email in the audit'); pass();
    assert.equal(await sql(`SELECT count(*) FROM founder_tokens WHERE place = ${place}`), '0', 'the founder token went as asked'); pass();
    assert.equal(await sql(`SELECT count(*) FROM founder_places WHERE place = ${place}`), '1', 'the founding place is never reassigned'); pass();
    assert.equal(Number(await sql('SELECT count(*) FROM terminated_email_holds')), holdsBefore, 'registry provenance avoids recreating legacy holds'); pass();
    assert.equal(await sql(`SELECT count(*) FROM sanctions WHERE account_id = '${ids.ter}'`), '1', 'moderation records are kept'); pass();
    assert.equal(await sql(`SELECT count(*) FROM banned_identifiers b JOIN moderation_cases c ON b.case_id=c.id WHERE c.subject_account_id='${ids.ter}' AND b.identifier_type='Email' AND b.status='Active'`),'1','active email restriction survives deletion');pass();

    // Bob keeps the history, read-only, with a deleted member.
    const after = (await call(tokens.bob, '/api/conversations')).body.find(c => c.id === convo);
    assert.deepEqual([after.person.displayName, after.canSend], ['Deleted member', false]); pass();
    const history = (await call(tokens.bob, `/api/conversations/${convo}/messages`)).body.items;
    assert.deepEqual(history.map(m => m.text), ['Hi Ana, fictional Bob here']); pass();
    assert.equal((await call(tokens.bob, `/api/conversations/${convo}/messages`, { method: 'POST', body: { text: 'Still there?' } })).status, 404, 'nothing can be sent'); pass();

    // A token issued just before deletion is refused and never recreates the account.
    assert.equal((await call(anaLast, '/api/me')).status, 403); pass();
    assert.equal((await call(anaLast, '/api/me/deletion')).status, 403); pass();
    assert.equal(await sql(`SELECT count(*) FROM accounts WHERE subject = '${anaUser}'`), '1', 'no second account'); pass();
    assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id = '${ids.ana}'`), '0'); pass();
    // Purging again does nothing more.
    assert.match(await cli('accounts', 'purge'), /Accounts: 0 deleted, 0 waiting/); pass();

    // D217: provider authentication is possible, but banned email cannot create a new application member.
    const back = { email: members.ter.email, password: 'Correct-horse-' + crypto.randomBytes(6).toString('hex') };
    await kc('/users', 'POST', { username: back.email, email: back.email, firstName: 'Fictional', enabled: true, emailVerified: true, credentials: [{ type: 'password', value: back.password, temporary: false }] });
    await cli('beta', 'add', back.email);
    const backToken = await memberToken(browser, back);
    assert.deepEqual(code(await call(backToken, '/api/me')), [403,'identifier-restricted']);pass();
    assert.equal(await sql(`SELECT count(*) FROM accounts WHERE subject = '${claims(backToken).sub}'`),'0','no returning application mapping');pass();

    // Stale sign-ins (Accounts:ReauthSeconds on the test hosts).
    while (Date.now() / 1000 - claims(staleExp).auth_time < REAUTH_SECONDS + 5) await sleep(1000);
    assert.deepEqual(code(await call(staleExp, '/api/me/export', { method: 'POST' })), [403, 'stale-session']); pass();
    assert.deepEqual(code(await call(staleExp, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })), [403, 'stale-session']); pass();
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.exp}'`), 'Active'); pass();

    // The web page in Edge: download, delete, the pending screen, then sign in again to cancel.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, acceptDownloads: true });
    const page = await context.newPage();
    const signIn = async () => {
      await page.goto(web + '/account/sign-in?intro=done');
      await page.fill('#username', members.webm.email); await page.fill('#password', members.webm.password);
      await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    };
    await signIn();
    const signedInAt = Date.now();
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/me/deletion')).status(), 200, 'on the BFF allow-list'); pass();
    assert.equal((await bff('api/admin/accounts')).status(), 404, 'admin routes never are'); pass();
    await page.click('a[href="/account/data"]');
    await page.waitForSelector('[data-data-panel=none]', { timeout: 20000 }); pass();
    const [file] = await Promise.all([page.waitForEvent('download'), page.click('[data-export]')]);
    assert.match(file.suggestedFilename(), /^swoovr-data-\d{4}-\d{2}-\d{2}\.json$/); pass();
    const webExport = JSON.parse(require('node:fs').readFileSync(await file.path(), 'utf8'));
    assert.equal(webExport.profile.displayName, 'Fictional Del webm'); pass();
    await page.waitForSelector('[data-export-result=downloaded]');
    await page.click('[data-export]');
    await page.waitForSelector('[data-export-result=export-too-soon]'); pass();
    assert.equal(await page.locator('[data-delete-account]').isDisabled(), true, 'needs the confirmation tick'); pass();
    await page.check('[data-delete-confirm]');
    await page.click('[data-delete-account]');
    await page.waitForSelector('[data-data-panel=pending]', { timeout: 20000 }); pass();
    await page.goto(web + '/connections');
    await page.waitForURL(web + '/account/deletion'); pass();
    await page.waitForSelector('[data-pending-deletion]'); pass();
    while ((Date.now() - signedInAt) / 1000 < REAUTH_SECONDS + 5) await sleep(1000);
    await page.click('[data-cancel-deletion]');
    await page.waitForSelector('[data-reauth-required]'); pass();
    await page.click('[data-reauth-required] a');
    await page.waitForSelector('#username');
    await page.fill('#username', members.webm.email); await page.fill('#password', members.webm.password);
    await Promise.all([page.waitForURL(web + '/account/deletion'), page.click('#kc-login')]); pass();
    await page.waitForSelector('[data-pending-deletion]');
    await page.click('[data-cancel-deletion]');
    await page.waitForSelector('[data-data-panel=none]', { timeout: 20000 }); pass();
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.webm}'`), 'Active'); pass();
    await page.goto(web + '/connections');
    assert.equal(new URL(page.url()).pathname, '/connections', 'the app opens again'); pass();
    if (process.env.SWOOVR_SCREENSHOTS) await page.screenshot({ path: process.env.SWOOVR_SCREENSHOTS + '/data-390.png', fullPage: true });
    await context.close();

    // D170/D233: only this fixture's tombstone clock advances; Bob's own history must survive its expiry.
    await sql(`UPDATE accounts SET deleted_at=now()-interval '731 days' WHERE id='${ids.ana}'`);
    await cli('accounts','purge');
    assert.equal(await sql(`SELECT issuer FROM accounts WHERE id='${ids.ana}'`),'urn:swoovr:retired');pass();
    assert.equal(await sql(`SELECT count(*) FROM messages WHERE sender_account_id='${ids.bob}' AND conversation_id='${convo}'`),'1','tombstone expiry cannot cascade Bob’s content');pass();
    assert.deepEqual((await call(tokens.bob,`/api/conversations/${convo}/messages`)).body.items.map(m=>m.text),['Hi Ana, fictional Bob here']);pass();

    console.log(`PASS: account deletion and export (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
