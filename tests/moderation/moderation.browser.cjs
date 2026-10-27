// Local acceptance for slice Q (D131, D133–D136, D150, D151): member reports with consolidation, evidence, block in the
// same step and an abuse limit that never stops urgent reports; urgent escalation by email; support and admin queues
// and permissions; restrictions, suspensions and termination enforced server-side at once; automatic expiry; appeals
// decided by someone else; the staff-to-member conflict rule; the home-city override; complimentary Plus; then the
// separate admin app (Swoovr.Admin, port 5082) and the member report and notices pages in actual Edge.
// Needs the local stack, the enrolled fictional root admin, hosts built from this checkout (API with
// Staff__LocalPasswords=true and Moderation__EscalationEmail, web on 5081, admin on 5082) and the GeoNames import.
// Every person is fictional, created for this run; staff users exist only in the local staff realm (D136).
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, admin, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, fillRootAdmin, cli, sql, call, claims, mailTo, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    // D159: the escalation contact comes from .local/docker.env (the hosts read the same value); Mailpit catches it locally.
    const ESCALATION = (await localEnv('SWOOVR_ESCALATION_EMAIL')) || 'moderation-escalations@example.test';
    let root = await rootAdminToken(browser);
    const names = ['rep', 'sub', 'oth', 'stf', 'far', 'spm', 'own', 'urg', 'plus'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('mod-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), about: 'About ' + n, homeCityId: n === 'far' ? MELBOURNE : ADELAIDE } });
      assert.equal(saved.status, 200, n);
      await matchReady(tokens[n]);
      await call(tokens[n], '/api/v1/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    // Staff: two support people and a second admin, all fictional local password users invited by the root admin.
    const staff = { sup: await createStaff('sup'), sup2: await createStaff('sup2'), adm: await createStaff('adm') };
    for (const [n, role] of [['sup', 'support'], ['sup2', 'support'], ['adm', 'admin']])
      assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) st[n] = await staffToken(browser, staff[n]);
    assert.deepEqual([(await call(st.sup, '/api/v1/session')).body.supportArea, (await call(st.adm, '/api/v1/session')).body.adminArea], [true, true]); pass();
    const staffIds = Object.fromEntries(await Promise.all(Object.keys(st).map(async n => [n, await sql(`SELECT id FROM accounts WHERE subject = '${claims(st[n]).sub}'`)])));
    const code = r => [r.status, r.body?.code];
    const report = (who, subjectType, subjectId, category = 'harassment', extra = {}) =>
      call(tokens[who], '/api/v1/reports', { method: 'POST', body: { subjectType, subjectId, category, text: null, block: false, ...extra } });
    const explore = async who => (await call(tokens[who], '/api/v1/explore?mode=dating')).body.people?.map(p => p.id) ?? [];
    const caseOf = n => sql(`SELECT id FROM moderation_cases WHERE subject_account_id = '${ids[n]}' AND status IN ('Open', 'UnderReview')`);

    // Categories are data; members never see priorities.
    let r = await call(tokens.rep, '/api/v1/report-categories');
    // D190: threats moved to the Serious Safety Incident list; routine reports keep suspected underage as urgent.
    assert.equal(r.status, 200); assert.ok(r.body.length >= 9 && !r.body.some(c => c.id === 'threats') && r.body.some(c => c.id === 'underage')); pass();
    assert.ok(!JSON.stringify(r.body).includes('rgent')); pass();

    // A report, consolidated with a second reporter's; evidence preserved; the text never reaches the audit log.
    r = await report('rep', 'profile', pid.sub, 'harassment', { text: 'Fictional note: rude messages in person.' });
    assert.equal(r.status, 201); assert.equal(r.body.status, 'submitted'); pass();
    const subCase = await caseOf('sub');
    assert.match(subCase, /^[0-9a-f-]{36}$/); pass();
    assert.equal((await report('oth', 'profile', pid.sub, 'fake')).status, 201); pass();
    assert.equal(await sql(`SELECT count(*) FROM reports WHERE case_id = '${subCase}'`), '2'); pass();
    assert.equal(await sql(`SELECT count(*) FROM moderation_cases WHERE subject_account_id = '${ids.sub}'`), '1'); pass(); // duplicates consolidated
    assert.match(await sql(`SELECT snapshot FROM case_evidence WHERE case_id = '${subCase}' LIMIT 1`), /Fictional Sub/); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'report.%' AND detail LIKE '%rude%'`), '0'); pass();

    // Negative cases: self, unknown ids, other markets, another pair's request, unsupported or tampered input.
    assert.deepEqual(code(await report('rep', 'profile', pid.rep)), [404, 'not-available']); pass();
    assert.deepEqual(code(await report('rep', 'profile', crypto.randomUUID())), [404, 'not-available']); pass();
    assert.deepEqual(code(await report('far', 'profile', pid.rep)), [404, 'not-available']); pass(); // another market, no relationship
    assert.deepEqual(code(await report('rep', 'bot', pid.sub)), [404, 'not-available']); pass(); // not a bot
    assert.deepEqual(code(await report('rep', 'profile', pid.sub, 'nonsense')), [400, 'unknown-category']); pass();
    assert.deepEqual(code(await report('rep', 'photo', crypto.randomUUID())), [400, 'unsupported-subject']); pass(); // photos come later
    assert.deepEqual(code(await report('rep', 'message', crypto.randomUUID())), [404, 'not-available']); pass(); // messages since slice U
    assert.deepEqual(code(await report('rep', '999', pid.sub)), [400, 'invalid-report']); pass();
    assert.deepEqual(code(await report('rep', 'profile', pid.sub, 'harassment', { text: 'x'.repeat(2001) })), [400, 'invalid-report']); pass();
    const req = await call(tokens.rep, '/api/v1/requests', { method: 'POST', body: { to: pid.oth, mode: 'dating' } });
    assert.equal(req.status, 201);
    assert.deepEqual(code(await report('sub', 'request', req.body.id)), [404, 'not-available']); pass(); // not their request
    assert.equal((await report('oth', 'request', req.body.id, 'spam')).status, 201); pass();
    assert.equal(await sql(`SELECT count(*) FROM reports WHERE subject_account_id = '${ids.rep}' AND subject_type = 'Request'`), '1'); pass();
    // Report and block in one step: they disappear for each other and the pending request is cancelled.
    assert.equal((await report('oth', 'profile', pid.rep, 'harassment', { block: true })).status, 201); pass();
    assert.ok(!(await explore('rep')).includes(pid.oth) && !(await explore('oth')).includes(pid.rep)); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE id = '${req.body.id}'`), 'Cancelled'); pass();
    // The abuse limit: ten ordinary reports in ten minutes, then 429; urgent reports are always accepted.
    for (let i = 0; i < 10; i++) assert.equal((await report('spm', 'profile', pid.sub, 'spam')).status, 201);
    assert.deepEqual(code(await report('spm', 'profile', pid.sub, 'spam')), [429, 'too-many-requests']); pass();
    r = await report('spm', 'profile', pid.urg, 'underage');
    assert.equal(r.status, 201); pass();

    // Urgent: escalated by email at once (ids and category only) and open until acknowledged.
    const urgCase = await caseOf('urg');
    let mail = []; for (let i = 0; i < 20 && !mail.some(m => m.includes(urgCase)); i++) { mail = await mailTo(ESCALATION); await sleep(250); }
    assert.ok(mail.some(m => m.includes(urgCase) && m.includes('underage'))); pass();
    assert.ok(!mail.some(m => m.includes('Fictional Urg'))); pass();
    assert.equal((await call(st.sup, '/api/v1/admin/cases')).body.find(c => c.id === urgCase).awaitingAcknowledgement, true); pass();
    assert.deepEqual(code(await call(st.sup, `/api/v1/admin/cases/${urgCase}/close`, { method: 'POST' })), [409, 'invalid-request']); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${urgCase}/acknowledge`, { method: 'POST' })).status, 200); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${urgCase}/close`, { method: 'POST' })).status, 200); pass();

    // Staff-affiliated subjects go to the admin queue (D134): support can't see or act on them.
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.stf}/staff-affiliation`, { method: 'PUT', body: { active: true } })).status, 204);
    assert.equal((await report('rep', 'profile', pid.stf, 'harassment')).status, 201);
    const stfCase = await caseOf('stf');
    assert.equal(await sql(`SELECT queue FROM moderation_cases WHERE id = '${stfCase}'`), 'Admin'); pass();
    assert.ok(!(await call(st.sup, '/api/v1/admin/cases')).body.some(c => c.id === stfCase)); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${stfCase}`)).status, 404); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${stfCase}/actions`, { method: 'POST', body: { action: 'warn', reason: 'x' } })).status, 404); pass();
    assert.ok((await call(st.adm, '/api/v1/admin/cases?queue=admin')).body.some(c => c.id === stfCase)); pass();
    assert.deepEqual(code(await call(st.sup, `/api/v1/admin/accounts/${ids.stf}/sanctions`, { method: 'POST', body: { action: 'warn', reason: 'x' } })), [403, 'moderation-not-permitted']); pass();

    // Members and other members get nothing from staff routes.
    assert.equal((await call(tokens.rep, '/api/v1/admin/cases')).status, 403); pass();
    assert.equal((await call(tokens.rep, `/api/v1/admin/cases/${subCase}/actions`, { method: 'POST', body: { action: 'dismiss' } })).status, 403); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${crypto.randomUUID()}`)).status, 404); pass();

    // Support restricts (targeted: out of discovery and no new requests), within its limits.
    const act = (who, id, body) => call(st[who] ?? who, `/api/v1/admin/cases/${id}/actions`, { method: 'POST', body: { hideFromDiscovery: false, noNewRequests: false, noMessages: false, ...body } });
    assert.equal((await call(st.sup, `/api/v1/admin/cases/${subCase}/review`, { method: 'POST' })).body.case.status, 'underReview'); pass();
    assert.deepEqual(code(await act('sup', subCase, { action: 'suspend', reason: 'x', days: 3 })), [403, 'moderation-not-permitted']); pass();
    assert.deepEqual(code(await act('sup', subCase, { action: 'restrict', reason: 'x', days: 31, noNewRequests: true })), [400, 'invalid-request']); pass();
    assert.deepEqual(code(await act('sup', subCase, { action: 'restrict', reason: ' ', days: 7, noNewRequests: true })), [400, 'invalid-request']); pass();
    assert.deepEqual(code(await act('sup', subCase, { action: 'ban', reason: 'x' })), [400, 'invalid-request']); pass();
    const subReq = await call(tokens.rep, '/api/v1/requests', { method: 'POST', body: { to: pid.sub, mode: 'dating' } }); // before the restriction
    assert.equal(subReq.status, 201);
    r = await act('sup', subCase, { action: 'restrict', reason: 'Fictional reason: repeated rude behaviour reported.', days: 7, hideFromDiscovery: true, noNewRequests: true });
    assert.equal(r.status, 200); assert.equal(r.body.case.status, 'reviewed'); pass();
    assert.ok(!(await explore('rep')).includes(pid.sub)); pass(); // out of discovery at once
    // Q55 (D160): a restricted member can't do anything in the app until it's lifted.
    assert.deepEqual(code(await call(tokens.sub, '/api/v1/requests', { method: 'POST', body: { to: pid.urg, mode: 'dating' } })), [403, 'account-restricted']); pass();
    assert.deepEqual(code(await call(tokens.sub, `/api/v1/requests/${subReq.body.id}/accept`, { method: 'POST' })), [403, 'account-restricted']); pass();
    for (const path of ['/api/v1/explore?mode=dating', '/api/v1/me/profile', '/api/v1/connections', '/api/v1/me/trips', '/api/v1/me/verification'])
    { assert.deepEqual(code(await call(tokens.sub, path)), [403, 'account-restricted'], path); pass(); }
    assert.equal((await call(tokens.sub, '/api/v1/me')).body.accountState, 'restricted'); pass();
    // The web shows the restricted screen instead of member pages; the account page and notices stay open.
    {
      const restrictedContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
      const restrictedPage = await restrictedContext.newPage();
      await restrictedPage.goto(web + '/account/sign-in?intro=done');
      await restrictedPage.fill('#username', members.sub.email); await restrictedPage.fill('#password', members.sub.password);
      await Promise.all([restrictedPage.waitForURL(web + '/account'), restrictedPage.click('#kc-login')]);
      await restrictedPage.waitForSelector('[data-restricted-screen=restricted]', { timeout: 20000 }); pass(); // on the account page
      for (const path of ['/explore', '/connections', '/travel', `/people/${pid.rep}`, '/me/matching']) {
        await restrictedPage.goto(web + path);
        assert.ok(restrictedPage.url().startsWith(web + '/account/restricted'), path); pass();
      }
      assert.equal(await restrictedPage.locator('[data-restricted-screen]').getAttribute('data-restricted-screen'), 'restricted'); pass();
      if (output) await restrictedPage.screenshot({ path: output + '/restricted-390.png', fullPage: true });
      await restrictedPage.goto(web + '/me/notices');
      await restrictedPage.waitForSelector('[data-notice-kind=restriction]', { timeout: 20000 }); pass();
      await restrictedContext.close();
    }
    r = await call(tokens.sub, '/api/v1/me/notices');
    assert.equal(r.body[0].kind, 'restriction'); assert.equal(r.body[0].sanction.noNewRequests, true); assert.equal(r.body[0].sanction.canAppeal, true); pass();
    assert.ok((await mailTo(members.sub.email)).some(m => m.includes('new notice') && !m.includes('rude'))); pass(); // the email only says there's a notice
    // The reporter sees "reviewed", never the outcome.
    r = await call(tokens.rep, '/api/v1/me/reports');
    const mine = r.body.find(x => x.status === 'reviewed');
    assert.ok(mine); assert.ok(!/restrict|sanction|warn|suspend/i.test(JSON.stringify(r.body))); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'moderation.%' AND detail LIKE '%rude%'`), '0'); pass();

    // An admin suspends: no normal member functions, but the account, notices and appeals stay open.
    r = await act('adm', subCase, { action: 'suspend', reason: 'Fictional reason: suspension for review.', days: 14 });
    assert.equal(r.status, 200); pass();
    for (const path of ['/api/v1/explore?mode=dating', '/api/v1/me/profile', '/api/v1/connections', '/api/v1/me/reports'])
    { assert.deepEqual(code(await call(tokens.sub, path)), [403, 'account-suspended'], path); pass(); }
    assert.equal((await call(tokens.sub, '/api/v1/me')).status, 200); pass();
    assert.equal((await call(tokens.rep, `/api/v1/profiles/${pid.sub}`)).status, 404); pass(); // even for a connected member
    const suspension = (await call(tokens.sub, '/api/v1/me/notices')).body.find(n => n.kind === 'suspension').sanction;
    assert.equal(suspension.live, true); pass();

    // Appeals (D135): once, by the member, decided by someone other than the original decider.
    assert.equal((await call(tokens.rep, '/api/v1/me/appeals', { method: 'POST', body: { sanctionId: suspension.id } })).status, 404); pass(); // another member's
    r = await call(tokens.sub, '/api/v1/me/appeals', { method: 'POST', body: { sanctionId: suspension.id, text: 'Fictional appeal text.' } });
    assert.equal(r.status, 201); pass();
    const appealId = r.body.id;
    assert.deepEqual(code(await call(tokens.sub, '/api/v1/me/appeals', { method: 'POST', body: { sanctionId: suspension.id } })), [409, 'appeal-not-allowed']); pass();
    assert.ok(!(await call(st.sup, '/api/v1/admin/appeals')).body.some(a => a.id === appealId)); pass(); // an admin's action: not in support's list
    assert.deepEqual(code(await call(st.sup, `/api/v1/admin/appeals/${appealId}/decide`, { method: 'POST', body: { outcome: 'overturn' } })), [403, 'moderation-not-permitted']); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/appeals/${appealId}/decide`, { method: 'POST', body: { outcome: 'overturn' } })), [403, 'moderation-not-permitted']); pass(); // the original decider
    assert.equal((await call(root, `/api/v1/admin/appeals/${appealId}/decide`, { method: 'POST', body: { outcome: 'maybe' } })).status, 400); pass();
    assert.equal((await call(root, `/api/v1/admin/appeals/${appealId}/decide`, { method: 'POST', body: { outcome: 'overturn' } })).status, 200); pass();
    assert.deepEqual(code(await call(tokens.sub, '/api/v1/me/profile')), [403, 'account-restricted']); pass(); // the suspension is lifted at once; the earlier restriction still stands (Q55)
    assert.ok((await call(tokens.sub, '/api/v1/me/notices')).body.some(n => n.kind === 'appealOverturned')); pass();
    assert.equal((await call(root, `/api/v1/admin/appeals/${appealId}/decide`, { method: 'POST', body: { outcome: 'uphold' } })).status, 409); pass();

    // Lifting: support can't lift an admin's action; support lifts its own restriction, with a reason.
    const restriction = (await call(st.sup, `/api/v1/admin/accounts/${ids.sub}/summary`)).body.sanctions.find(s => s.kind === 'restriction' && s.live);
    assert.ok(restriction); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/sanctions/${restriction.id}/lift`, { method: 'POST', body: { reason: '' } })).status, 400); pass();
    assert.equal((await call(st.sup2, `/api/v1/admin/sanctions/${restriction.id}/lift`, { method: 'POST', body: { reason: 'Fictional: resolved.' } })).status, 200); pass();
    assert.ok((await explore('rep')).includes(pid.sub)); pass();
    assert.equal((await call(tokens.sub, '/api/v1/me/profile')).status, 200); pass(); // nothing left in force: back to normal
    const adminWarn = await call(st.adm, `/api/v1/admin/accounts/${ids.sub}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional admin restriction.', days: 2, noMessages: true } });
    assert.equal(adminWarn.status, 200);
    const adminRestriction = adminWarn.body.sanctions.find(s => s.live && s.createdByRole === 'admin');
    assert.deepEqual(code(await call(st.sup, `/api/v1/admin/sanctions/${adminRestriction.id}/lift`, { method: 'POST', body: { reason: 'x' } })), [403, 'moderation-not-permitted']); pass();
    // Expired sanctions lift by themselves.
    await sql(`UPDATE sanctions SET starts_at = now() - interval '3 days', ends_at = now() - interval '1 day' WHERE id = '${adminRestriction.id}'`);
    assert.equal((await call(st.sup, `/api/v1/admin/accounts/${ids.sub}/summary`)).body.sanctions.find(s => s.id === adminRestriction.id).live, false); pass();

    // No one decides about their own member account (D134): the second admin's personal account is "own".
    assert.equal((await call(root, `/api/v1/admin/staff-links/${staffIds.adm}`, { method: 'PUT', body: { memberAccountId: ids.own } })).status, 204); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/staff-links/${staffIds.adm}`, { method: 'PUT', body: { memberAccountId: ids.own } })).status, 403); pass();
    assert.equal((await call(root, `/api/v1/admin/staff-links/${staffIds.sup}`, { method: 'PUT', body: { memberAccountId: ids.own } })).status, 409); pass(); // one staff account per member
    assert.equal((await report('rep', 'profile', pid.own, 'spam')).status, 201);
    const ownCase = await caseOf('own');
    assert.deepEqual(code(await act('adm', ownCase, { action: 'warn', reason: 'x' })), [403, 'moderation-not-permitted']); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/cases/${ownCase}/close`, { method: 'POST' })), [403, 'moderation-not-permitted']); pass();
    assert.equal((await act('sup', ownCase, { action: 'dismiss' })).status, 200); pass();

    // Termination is permanent and admin-only.
    root = await rootAdminToken(browser); // a fresh sign-in for the sensitive action
    assert.deepEqual(code(await call(st.sup, `/api/v1/admin/accounts/${ids.far}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'x' } })), [403, 'moderation-not-permitted']); pass();
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.far}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'Fictional: termination test.' } })).status, 200); pass();
    r = await call(tokens.far, '/api/v1/me/profile');
    assert.deepEqual(code(r), [403, 'account-suspended']); assert.match(r.body.title, /closed/); pass();
    assert.equal((await call(tokens.far, '/api/v1/me/notices')).body[0].kind, 'termination'); pass();

    // D150: an admin lifts the 30-day home-city wait, audited without the city.
    await sql(`UPDATE member_profiles SET home_market_moved_to = 'adelaide', home_market_moved_at = now() WHERE account_id = '${ids.plus}'`);
    assert.equal((await call(st.sup, `/api/v1/admin/accounts/${ids.plus}/home-move-override`, { method: 'POST', body: { reason: 'Moved' } })).status, 403); pass();
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.plus}/home-move-override`, { method: 'POST', body: { reason: '' } })).status, 400); pass();
    r = await call(root, `/api/v1/admin/accounts/${ids.plus}/home-move-override`, { method: 'POST', body: { reason: 'Fictional: genuine move.' } });
    assert.equal(r.status, 200); assert.equal(r.body.homeMarketMovedAt, null); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'home-market.override' AND target_id = '${ids.plus}' AND detail IS NULL`), '1'); pass();

    // D151: complimentary Plus through the entitlement service; admins only; lapses and revocations fall back to Free.
    assert.deepEqual((await call(tokens.plus, '/api/v1/me/plan')).body, { tier: 'free', source: null, endsAt: null, complimentary: false }); pass();
    const in30 = new Date(Date.now() + 30 * 86400000).toISOString();
    for (const body of [{ tier: 'free', endsAt: in30, reason: 'x' }, { tier: 'plus', endsAt: new Date(Date.now() + 400 * 86400000).toISOString(), reason: 'x' },
      { tier: 'plus', endsAt: in30, reason: ' ' }, { tier: '1', endsAt: in30, reason: 'x' }])
    { assert.deepEqual(code(await call(root, `/api/v1/admin/accounts/${ids.plus}/entitlements`, { method: 'POST', body })), [400, 'invalid-entitlement'], JSON.stringify(body)); pass(); }
    assert.equal((await call(st.sup, `/api/v1/admin/accounts/${ids.plus}/entitlements`, { method: 'POST', body: { tier: 'plus', endsAt: in30, reason: 'Beta tester' } })).status, 403); pass();
    r = await call(root, `/api/v1/admin/accounts/${ids.plus}/entitlements`, { method: 'POST', body: { tier: 'plus', endsAt: in30, reason: 'Beta tester' } });
    assert.equal(r.status, 201); const grant = r.body.id; pass();
    r = await call(tokens.plus, '/api/v1/me/plan');
    assert.deepEqual([r.body.tier, r.body.source, r.body.complimentary], ['plus', 'adminGrant', true]); pass();
    assert.equal((await call(tokens.rep, '/api/v1/me/plan')).body.tier, 'free'); pass(); // nobody else
    assert.equal((await call(root, `/api/v1/admin/entitlements/${grant}`, { method: 'PUT', body: { endsAt: new Date(Date.now() + 60 * 86400000).toISOString() } })).status, 200); pass();
    assert.equal((await call(root, `/api/v1/admin/entitlements/${grant}/revoke`, { method: 'POST' })).status, 200); pass();
    assert.equal((await call(tokens.plus, '/api/v1/me/plan')).body.tier, 'free'); pass();
    assert.equal((await call(root, `/api/v1/admin/entitlements/${grant}/revoke`, { method: 'POST' })).status, 404); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'entitlement.%' AND target_id = '${ids.plus}'`), '3'); pass();

    // BFF: member moderation routes allowed, admin routes never.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const signIn = async (p, member) => {
      await p.goto(web + '/account/sign-in?intro=done');
      await p.fill('#username', member.email); await p.fill('#password', member.password);
      await Promise.all([p.waitForURL(web + '/account'), p.click('#kc-login')]);
    };
    await signIn(page, members.urg);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/v1/report-categories')).status(), 200); pass();
    assert.equal((await bff('api/v1/me/notices')).status(), 200); pass();
    assert.equal((await bff('api/v1/admin/cases')).status(), 404); pass();
    assert.equal((await bff(`api/v1/admin/accounts/${ids.urg}/entitlements`, { method: 'POST', data: {} })).status(), 404); pass();
    // Web: report from a person page.
    await page.goto(web + `/people/${pid.rep}`);
    await page.waitForSelector('[data-person=ready]', { timeout: 20000 });
    await page.click('[data-report-open]');
    await page.selectOption('#report-category', 'spam');
    await page.fill('#report-text', 'Fictional web report.');
    await page.click('[data-report-form] button[type=submit]');
    await page.waitForSelector('[data-report=sent]'); pass();
    assert.equal(await sql(`SELECT count(*) FROM reports WHERE reporter_account_id = '${ids.urg}' AND text = 'Fictional web report.'`), '1'); pass();
    if (output) await page.screenshot({ path: output + '/report-sent-390.png', fullPage: true });
    // Web: the suspended-then-overturned member's notices, with the appeal status.
    const subContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const subPage = await subContext.newPage();
    await signIn(subPage, members.sub);
    await subPage.goto(web + '/me/notices');
    await subPage.waitForSelector('[data-notices=ready]', { timeout: 20000 });
    assert.ok(await subPage.locator('[data-notice-kind=suspension]').count() >= 1); pass();
    assert.ok(await subPage.locator('[data-appeal-status=overturned]').count() >= 1); pass();
    assert.ok(await subPage.locator('[data-notice-kind=restriction] [data-appeal-open]').count() >= 1 || await subPage.locator('[data-notice-kind=restriction]').count() >= 1); pass();
    if (output) await subPage.screenshot({ path: output + '/notices-390.png', fullPage: true });

    // The admin app (Swoovr.Admin): signs staff in through the staff realm and works the queue server-side.
    const adminContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const adminPage = await adminContext.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.sup.email); await adminPage.fill('#password', staff.sup.password); await adminPage.click('#kc-login');
    await adminPage.waitForSelector('[data-dashboard]', { timeout: 20000 }); pass();
    const html = await adminPage.content();
    assert.ok(!html.includes('access_token') && !/eyJ[A-Za-z0-9_-]{20,}\./.test(html)); pass(); // no token in the page
    assert.ok((await adminContext.cookies()).every(c => c.name !== 'access_token' && c.value.length < 2000)); pass();
    await adminPage.click('a[href="/cases"]');
    await adminPage.waitForSelector('[data-cases]');
    assert.equal(await adminPage.locator(`[data-case="${stfCase}"]`).count(), 0); pass(); // support's queue has no staff cases
    if (output) await adminPage.screenshot({ path: output + '/admin-queue.png', fullPage: true });
    assert.equal((await report('rep', 'profile', pid.urg, 'inappropriate')).status, 201);
    const urgCase2 = await caseOf('urg');
    await adminPage.goto(admin + `/cases/${urgCase2}`);
    await adminPage.waitForSelector(`[data-case-detail="${urgCase2}"]`); pass();
    await adminPage.selectOption('select[name="ActForm.Action"]', 'warn');
    await adminPage.fill('textarea[name="ActForm.Reason"]', 'Fictional warning from the admin app.');
    await adminPage.click('[data-act]');
    await adminPage.waitForSelector('[data-result=ok]'); pass();
    assert.equal(await adminPage.locator('[data-case-status]').getAttribute('data-case-status'), 'reviewed'); pass();
    assert.ok((await call(tokens.urg, '/api/v1/me/notices')).body.some(n => n.kind === 'warning')); pass();
    if (output) await adminPage.screenshot({ path: output + '/admin-case.png', fullPage: true });
    await adminPage.goto(admin + `/accounts/${ids.plus}`);
    await adminPage.waitForSelector(`[data-account="${ids.plus}"]`);
    await adminPage.fill('form:has([data-op=grant]) input[name=reason]', 'Fictional beta tester');
    await adminPage.click('[data-op=grant]');
    await adminPage.waitForSelector('[data-result="403"]'); pass(); // support can't grant Plus
    await adminContext.close();
    // The root admin grants Plus from the account page.
    const rootContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const rootPage = await rootContext.newPage();
    await rootPage.goto(admin + `/accounts/${ids.plus}`);
    await fillRootAdmin(rootPage);
    await rootPage.waitForSelector(`[data-account="${ids.plus}"]`, { timeout: 20000 });
    await rootPage.fill('form:has([data-op=grant]) input[name=reason]', 'Fictional beta tester');
    await rootPage.click('[data-op=grant]');
    await rootPage.waitForSelector('[data-result=ok]'); pass();
    assert.equal(await rootPage.locator('[data-plan]').getAttribute('data-plan'), 'plus'); pass();
    assert.equal((await call(tokens.plus, '/api/v1/me/plan')).body.tier, 'plus'); pass();
    if (output) await rootPage.screenshot({ path: output + '/admin-account.png', fullPage: true });
    await rootContext.close(); await subContext.close(); await context.close();
    console.log(`PASS: reports and moderation acceptance, ${passed} checks (categories as data, reports consolidated into one case with evidence and no text in the audit, self/unknown/other-market/another pair's request/bot mismatch/unsupported/tampered refused, report and block in one step, abuse limit that never stops urgent reports, urgent escalation email with ids only and acknowledgement before closing, staff-affiliated cases admin-only, support limits (no suspend, 30 days, reason, scope), restriction out of discovery and no new requests at once, existing contact kept, notices and a notice-only email, reporter sees reviewed but not the outcome, suspension bars everything but account/notices/appeals, appeals once and by someone else, overturn lifts at once, support can't lift an admin's action, automatic expiry, own-account conflict via the staff link, admin-only termination, home-city override, complimentary Plus granted/extended/revoked with limits, BFF allow-list, web report and notices, admin app sign-in without tokens in the browser, queue, warn from a case, Plus grant refused for support and done by an admin).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
