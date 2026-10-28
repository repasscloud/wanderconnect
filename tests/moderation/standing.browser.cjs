// Local acceptance for slice AK (#50): account standing for the member (web, API) and the same view for staff, plus
// support references (SW-XXXXXX) that support looks up in the admin app. Never reporter identities, reports about the
// member, internal flags, holds or fraud signals; a blocked photo reads exactly like a rejected one; the page stays open
// while restricted. Negative cases: signed out, staff on member routes, members on staff routes, unknown and other
// realms' ids, admin-queue cases hidden from support. Same local stack as tests/moderation; every person is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, admin, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const output = process.env.SWOOVR_SCREENSHOTS;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'new'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('std-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Std ' + n, homeCityId: ADELAIDE } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'new') continue;
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const staff = { sup: await createStaff('std-sup'), adm: await createStaff('std-adm') };
    for (const [n, role] of [['sup', 'support'], ['adm', 'admin']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/session'); }
    const standing = async who => (await call(tokens[who], '/api/me/standing')).body;

    // ---- Who may read what.
    assert.equal((await call(null, '/api/me/standing')).status, 401); pass();
    assert.equal((await call(st.sup, '/api/me/standing')).status, 403); pass();
    assert.equal((await call(tokens.ana, `/api/admin/accounts/${ids.bob}/standing`)).status, 403); pass();
    assert.equal((await call(tokens.ana, '/api/admin/support/SW-ABCDEF')).status, 403); pass();
    assert.equal((await call(null, `/api/admin/accounts/${ids.bob}/standing`)).status, 401); pass();

    // ---- A new member: in good standing, with the date of birth to add.
    let s = await standing('new');
    assert.deepEqual([s.state, s.actions, s.inForce, s.decisions, s.reports], ['active', [{ code: 'date-of-birth', link: '/account' }], [], [], []]); pass();
    assert.deepEqual(s.verification, { emailVerified: true, phoneVerified: false, phoneRequired: false, ageStatus: s.verification.ageStatus, dateOfBirthSet: false,
      dateOfBirthVerified: false, dobCheck: 'none', idStatus: 'notStarted' }); pass();
    s = await standing('ana');
    assert.deepEqual(s.actions, []); pass();
    // An age flag asks for an ID check first (D127).
    await sql(`UPDATE member_profiles SET age_status = 'NeedsVerification' WHERE account_id = '${ids.new}'`);
    s = await standing('new');
    assert.deepEqual(s.actions.map(a => a.code), ['age-check', 'date-of-birth']); pass();
    assert.equal(s.verification.ageStatus, 'needsVerification'); pass();

    // ---- Ana reports Bob: her report gets a support reference; Bob sees nothing about it.
    const categories = (await call(tokens.ana, '/api/report-categories')).body;
    let r = await call(tokens.ana, '/api/reports', { method: 'POST', body: { subjectType: 'profile', subjectId: pid.bob, category: categories[0].id, text: 'Fictional note', block: false } });
    assert.equal(r.status, 201, JSON.stringify(r.body));
    const report = r.body.id;
    s = await standing('ana');
    assert.equal(s.reports.length, 1); pass();
    const ref = s.reports[0].reference;
    assert.match(ref, /^SW-[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$/); pass();
    assert.deepEqual([s.reports[0].id, s.reports[0].status, s.reports[0].outcome, s.reports[0].subjectType], [report, 'submitted', null, 'profile']); pass();
    assert.equal(await sql(`SELECT support_reference FROM reports WHERE id = '${report}'`), ref); pass();
    const bobView = JSON.stringify(await standing('bob'));
    for (const leak of [ids.ana, pid.ana, report, ref, 'Fictional Std ana', 'Fictional note'])
    { assert.ok(!bobView.includes(leak), 'Bob never sees ' + leak); pass(); }
    assert.deepEqual(JSON.parse(bobView).reports, []); pass();

    // ---- Support lookup: forgiving input, the case, ids only; unknown and malformed references.
    for (const typed of [ref, ref.toLowerCase(), ref.replace('-', ''), ' ' + ref.slice(3) + ' ']) {
      r = await call(st.sup, '/api/admin/support/' + encodeURIComponent(typed));
      assert.deepEqual([r.status, r.body.reference, r.body.reportId, r.body.reporterAccountId, r.body.subjectAccountId], [200, ref, report, ids.ana, ids.bob], typed); pass();
    }
    const caseId = r.body.caseId;
    assert.equal(r.body.queue, 'support'); pass();
    assert.ok(!JSON.stringify(r.body).includes('Fictional note')); pass(); // the note stays in the case
    for (const bad of ['SW-ZZZZZZ', 'SW-ABC23', 'SW-0OOOOO', "x' OR 1=1"])
    { assert.deepEqual(code(await call(st.sup, '/api/admin/support/' + encodeURIComponent(bad))), [404, 'unknown-reference'], bad); pass(); }
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'support.lookup' AND target_id = '${report}'`)) >= 4); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'support.lookup' AND coalesce(detail, '') LIKE '%SW-%'`), '0'); pass();

    // ---- Older reports get a reference the first time they're shown.
    await sql(`UPDATE reports SET support_reference = NULL WHERE id = '${report}'`);
    s = await standing('ana');
    const fresh = s.reports[0].reference;
    assert.ok(fresh !== ref && /^SW-/.test(fresh)); pass();
    assert.equal((await standing('ana')).reports[0].reference, fresh); pass(); // kept once given
    assert.deepEqual(code(await call(st.sup, '/api/admin/support/' + ref)), [404, 'unknown-reference']); pass();

    // ---- A serious incident goes to the admin queue: support can't find it by reference, admins can.
    const serious = (await call(tokens.bob, '/api/report-categories?kind=serious')).body[0].id;
    r = await call(tokens.bob, '/api/reports', { method: 'POST', body: { subjectType: 'profile', subjectId: pid.ana, category: serious, block: false, police: 'not-now' } });
    assert.equal(r.status, 201);
    const seriousRef = (await standing('bob')).reports[0].reference;
    assert.equal((await standing('bob')).reports[0].serious, true); pass();
    assert.deepEqual(code(await call(st.sup, '/api/admin/support/' + seriousRef)), [404, 'unknown-reference']); pass();
    r = await call(st.adm, '/api/admin/support/' + seriousRef);
    assert.deepEqual([r.status, r.body.queue, r.body.serious], [200, 'admin', true]); pass();
    const anaAfter = JSON.stringify(await standing('ana'));
    assert.ok(!anaAfter.includes(ids.bob) && !anaAfter.includes(seriousRef)); pass(); // Ana never learns she was reported
    assert.ok(!/hold|legal|case|flag|police/i.test(Object.keys(JSON.parse(anaAfter)).join(','))); pass();

    // ---- Sanctions: a warning, a restriction with an appeal, a lift; decisions newest first.
    r = await call(st.sup, `/api/admin/cases/${caseId}/actions`, { method: 'POST', body: { action: 'warn', reason: 'Fictional warning for the standing suite.' } });
    assert.ok(r.status < 300, JSON.stringify(r.body)); pass();
    r = await call(root, `/api/admin/accounts/${ids.bob}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the standing suite.', days: 3 } });
    assert.ok(r.status < 300); pass();
    s = await standing('bob');
    assert.equal(s.state, 'restricted'); pass();
    assert.equal(s.inForce.length, 1); pass(); // the warning limits nothing
    const restriction = s.inForce[0];
    assert.deepEqual([restriction.kind, restriction.reason, restriction.appealStatus], ['restriction', 'Fictional restriction for the standing suite.', null]); pass();
    assert.ok(new Date(restriction.endsAt) > new Date(Date.now() + 2.9 * 86400000) && new Date(restriction.appealBy) > new Date(Date.now() + 29 * 86400000)); pass();
    assert.deepEqual(s.decisions.map(d => [d.kind, d.state]).slice(0, 2), [['restriction', 'in-force'], ['warning', 'issued']]); pass();
    // Restricted members still reach their standing (D160), but nothing else.
    assert.deepEqual(code(await call(tokens.bob, '/api/explore?mode=friends')), [403, 'account-restricted']); pass();
    assert.equal((await call(tokens.bob, '/api/me/appeals', { method: 'POST', body: { sanctionId: restriction.id, text: 'Fictional appeal.' } })).status, 201);
    s = await standing('bob');
    assert.deepEqual([s.inForce[0].appealStatus, s.inForce[0].appealBy], ['pending', null]); pass();
    // Staff see exactly what Bob sees.
    r = await call(st.sup, `/api/admin/accounts/${ids.bob}/standing`);
    assert.deepEqual(r.body, s); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'standing.viewed' AND target_id = '${ids.bob}'`), '1'); pass();
    assert.deepEqual(code(await call(st.sup, '/api/admin/accounts/00000000-0000-0000-0000-000000000000/standing')), [404, 'not-available']); pass();
    const supId = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st.sup).sub}'`);
    assert.deepEqual(code(await call(st.adm, `/api/admin/accounts/${supId}/standing`)), [404, 'not-available']); pass(); // a staff account has no member standing
    assert.equal((await call(st.sup, '/api/admin/accounts/not-a-guid/standing')).status, 404); pass();
    await call(root, `/api/admin/sanctions/${restriction.id}/lift`, { method: 'POST', body: { reason: 'Fictional lift.' } });
    s = await standing('bob');
    assert.deepEqual([s.state, s.inForce.length, s.decisions[0].kind, s.decisions[0].state], ['active', 0, 'restriction', 'lifted']); pass();

    // ---- Ana's report shows the general outcome once the case is closed.
    await call(st.sup, `/api/admin/cases/${caseId}/acknowledge`, { method: 'POST' }); // urgent categories wait for acknowledgement
    assert.ok((await call(st.sup, `/api/admin/cases/${caseId}/close`, { method: 'POST' })).status < 300);
    s = await standing('ana');
    assert.deepEqual([s.reports[0].status, s.reports[0].outcome], ['closed', 'actionTaken']); pass();

    // ---- Refused photos: blocked and rejected read the same, dated by upload; nothing about holds or cases.
    const hash = () => '\\x' + crypto.randomBytes(32).toString('hex');
    const row = (status, reviewed) => `('${crypto.randomUUID()}', '${ids.ana}', '${status}', '${hash()}', '${hash()}', 'image/jpeg', 400, 400, 1000,
      '2026-10-26T00:00:00Z', 1, true, '2026-10-26T00:00:05Z', ${reviewed})`;
    await sql(`INSERT INTO media_items (id, account_id, status, sha256, upload_sha256, content_type, width, height, bytes, created_at, screen_attempts, known_content_checked,
      screened_at, reviewed_at) VALUES ${row('Blocked', 'NULL')}, ${row('Rejected', "'2026-10-27T09:00:00Z'")}`);
    s = await standing('ana');
    const photos = s.decisions.filter(d => d.kind === 'photo-not-approved');
    assert.equal(photos.length, 2); pass();
    assert.deepEqual(Object.keys(photos[0]), Object.keys(photos[1])); pass();
    assert.ok(photos[0].at === photos[1].at && photos[0].at.startsWith('2026-10-26T00:00:00')); pass(); // the upload time, never the decision time
    assert.ok(photos.every(p => p.state === null && p.sanctionId === null)); pass();
    await sql(`DELETE FROM media_items WHERE account_id = '${ids.ana}' AND object_key IS NULL AND status IN ('Blocked', 'Rejected')`);

    // ---- Web at phone width: Ana's standing with her reference; Bob, restricted again, reaches it from the restricted screen.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.ana.email); await page.fill('#password', members.ana.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    assert.ok(await page.isVisible('[data-standing-link]')); pass();
    await page.goto(web + '/me/standing');
    await page.waitForSelector('[data-standing="ready"]', { timeout: 20000 });
    assert.equal(await page.getAttribute('[data-standing-state]', 'data-standing-state'), 'active'); pass();
    assert.ok((await page.textContent(`[data-standing-report="${fresh}"]`)).includes('We took action on this report')); pass();
    if (output) await page.screenshot({ path: output + '/web-standing.png', fullPage: true });
    await context.close();
    r = await call(root, `/api/admin/accounts/${ids.bob}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional second restriction.', days: 1 } });
    const bobContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const bobPage = await bobContext.newPage();
    await bobPage.goto(web + '/account/sign-in?intro=done');
    await bobPage.fill('#username', members.bob.email); await bobPage.fill('#password', members.bob.password);
    await Promise.all([bobPage.waitForURL(web + '/account**'), bobPage.click('#kc-login')]);
    await bobPage.goto(web + '/explore');
    await bobPage.waitForURL('**/account/restricted**');
    await bobPage.$eval('[data-restricted-standing]', el => el.click());
    await bobPage.waitForSelector('[data-standing="ready"]', { timeout: 20000 });
    assert.equal(await bobPage.getAttribute('[data-standing-state]', 'data-standing-state'), 'restricted'); pass();
    assert.ok((await bobPage.textContent('[data-standing-in-force]')).includes('Fictional second restriction.')); pass();
    if (output) await bobPage.screenshot({ path: output + '/web-standing-restricted.png', fullPage: true });
    await bobContext.close();
    const second = (await call(st.sup, `/api/admin/accounts/${ids.bob}/standing`)).body.inForce[0].id;
    await call(root, `/api/admin/sanctions/${second}/lift`, { method: 'POST', body: { reason: 'Fictional lift.' } });

    // ---- Admin app: the member's view and the support lookup.
    const adminPage = await browser.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.sup.email); await adminPage.fill('#password', staff.sup.password); await adminPage.click('#kc-login');
    await adminPage.waitForURL(admin + '/**');
    await adminPage.goto(admin + '/support');
    await adminPage.fill('input[name=reference]', fresh.toLowerCase());
    await adminPage.click('[data-support-search]');
    await adminPage.waitForSelector(`[data-support-result="${fresh}"]`); pass();
    assert.equal(await adminPage.getAttribute('[data-support-case]', 'data-support-case'), caseId); pass();
    await adminPage.fill('input[name=reference]', seriousRef);
    await adminPage.click('[data-support-search]');
    await adminPage.waitForSelector('[data-result="unknown-reference"]'); pass(); // support can't see admin-queue cases
    await adminPage.goto(admin + `/accounts/${ids.ana}/standing`);
    await adminPage.waitForSelector('[data-standing="active"]'); pass();
    assert.ok((await adminPage.textContent(`[data-standing-report="${fresh}"]`)).includes(fresh)); pass();
    if (output) await adminPage.screenshot({ path: output + '/admin-standing.png', fullPage: true });

    console.log(`PASS: account standing (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
