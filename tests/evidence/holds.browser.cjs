// Local acceptance for slice Y (policy §3, §11, §13; owner Q76, D189): legal holds and evidence preservation.
// The `legal` role (security / legal officer) appointed only by the root admin; holds with reason, scope, period, review
// and release conditions; capture into the sealed evidence store with a hash-chained custody log; an admin's
// provisional hold from a case (only the case's people); deletion still completing while held records stay preserved;
// `messages purge` keeping held text; one-time exports whose hashes check out; the disclosure register; release retaining required
// evidence pending its independent schedule. Negative cases: members, support, admins reading holds, unknown ids, tampered bodies, staff accounts in
// scope, other members' conversations, released holds. Then the admin app's hold pages in Edge.
// Needs the local stack, the enrolled fictional root admin and hosts built from this checkout (API with
// Staff__LocalPasswords=true and Accounts__ReauthSeconds=45, admin app on 5082). Every person is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, admin, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;

(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat', 'dan'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('hold-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      pid[n] = (await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Hold ' + n, homeCityId: ADELAIDE } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    // Ana and Bob talk (one message deleted for both); Cat talks with Bob and, separately, with Dan.
    const connect = async (a, b) => {
      const req = await call(tokens[a], '/api/requests', { method: 'POST', body: { to: pid[b], mode: 'friends' } });
      assert.equal((await call(tokens[b], `/api/requests/${req.body.id}/accept`, { method: 'POST' })).status, 200);
      return (await call(tokens[a], '/api/conversations', { method: 'POST', body: { to: pid[b] } })).body.id;
    };
    const send = async (who, convo, text) => (await call(tokens[who], `/api/conversations/${convo}/messages`, { method: 'POST', body: { text } })).body.id;
    const anaBob = await connect('ana', 'bob'), catBob = await connect('cat', 'bob'), catDan = await connect('cat', 'dan');
    await send('ana', anaBob, 'Fictional threat from Ana for the hold test');
    const deletedByBob = await send('bob', anaBob, 'Fictional message Bob deletes');
    assert.equal((await call(tokens.bob, `/api/messages/${deletedByBob}`, { method: 'DELETE' })).status, 204);
    const catDeleted = await send('cat', catDan, 'Fictional unheld message Cat deletes');
    assert.equal((await call(tokens.cat, `/api/messages/${catDeleted}`, { method: 'DELETE' })).status, 204);

    // Staff: support, an admin and the security / legal officer (only the root admin may appoint one).
    const staff = { sup: await createStaff('hold-sup'), adm: await createStaff('hold-adm'), leg: await createStaff('hold-leg') };
    for (const [n, role] of [['sup', 'support'], ['adm', 'admin']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of ['sup', 'adm']) st[n] = await staffToken(browser, staff[n]);
    assert.equal((await call(st.adm, '/api/admin/invitations', { method: 'POST', body: { email: staff.leg.email, role: 'legal' } })).status, 403); pass();
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.leg.email, role: 'legal' } })).status, 201); pass();
    st.leg = await staffToken(browser, staff.leg);
    // D195 (procedure §7): release needs a second, independent officer.
    staff.leg2 = await createStaff('hold-leg2');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.leg2.email, role: 'legal' } })).status, 201);
    st.leg2 = await staffToken(browser, staff.leg2);
    const session = (await call(st.leg, '/api/session')).body;
    assert.deepEqual([session.adminArea, session.supportArea, session.memberArea], [false, false, false]); pass();
    assert.equal((await call(st.leg, '/api/admin/cases')).status, 403, 'legal has no moderation queue'); pass();
    const legId = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st.leg).sub}'`);
    assert.equal(await sql(`SELECT role FROM role_grants WHERE account_id = '${legId}'`), 'Legal'); pass();

    // Nobody else reads holds.
    const hold = body => call(st.leg, '/api/admin/holds', { method: 'POST', body });
    for (const [who, token] of [['member', tokens.cat], ['support', st.sup], ['admin', st.adm]])
      assert.equal((await call(token, '/api/admin/holds')).status, 403, who);
    pass();
    assert.equal((await call(tokens.cat, '/api/admin/holds/overview')).status, 403); pass();
    assert.equal((await call(st.sup, '/api/admin/holds/overview')).status, 403); pass();
    assert.equal((await call(null, '/api/admin/holds')).status, 401); pass();
    assert.equal((await call(st.sup, '/api/admin/holds', { method: 'POST', body: { reason: 'x', accountIds: [ids.ana] } })).status, 403); pass();
    assert.deepEqual(code(await call(st.adm, '/api/admin/holds', { method: 'POST', body: { reason: 'x', accountIds: [ids.ana] } })), [400, 'hold-case-required']); pass();

    // Validation.
    assert.deepEqual(code(await hold({ reason: ' ', accountIds: [ids.ana] })), [400, 'hold-reason-required']); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [] })), [400, 'hold-scope-invalid']); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [crypto.randomUUID()] })), [400, 'hold-scope-invalid']); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [legId] })), [400, 'hold-scope-invalid'], 'staff accounts are not members'); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [ids.ana], conversationIds: [catBob] })), [400, 'hold-scope-invalid'], 'not Ana\'s conversation'); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [ids.ana], reviewDue: new Date(Date.now() + 400 * 864e5).toISOString() })), [400, 'hold-dates-invalid']); pass();
    assert.deepEqual(code(await hold({ reason: 'x', accountIds: [ids.ana], from: '2026-10-20T00:00:00Z', until: '2026-10-01T00:00:00Z' })), [400, 'hold-dates-invalid']); pass();
    const tampered = await fetch(api + '/api/admin/holds', { method: 'POST', headers: { Authorization: 'Bearer ' + st.leg, 'Content-Type': 'application/json' }, body: '{"reason":"x","accountIds":"everyone"}' });
    assert.equal(tampered.status, 400); pass();
    assert.equal(await sql(`SELECT count(*) FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id = h.id WHERE s.target_id = '${ids.ana}'`), '0', 'refusals placed nothing'); pass();

    // D269 acceptance source fixture: explicit synthetic approved receipt, not legal approval or a public catalogue entry.
    if(process.env.SWOOVR_TEST_APP_DATABASE){
      await sql(`INSERT INTO policy_acceptances(account_id,version,content_sha256,accepted_at) VALUES('${ids.ana}','fictional-held-approved',repeat('a',64),now())`);
    }
    // Legal places a hold over Ana and her conversation with Bob: captured at once, sealed, chained.
    const REASON = 'Fictional police preservation request REF-HOLD-1';
    let r = await hold({ reason: REASON, accountIds: [ids.ana], conversationIds: [anaBob], releaseConditions: 'When the fictional investigation closes' });
    assert.equal(r.status, 201); assert.equal(r.body.status, 'active'); assert.ok(r.body.items > 5); pass();
    const holdId = r.body.id;
    r = await call(st.leg, `/api/admin/holds/${holdId}`);
    assert.equal(r.status, 200); pass();
    const kinds = new Set(r.body.items.map(i => i.kind));
    for (const k of ['account', 'profile', 'mode', 'conversation', 'message', 'request', 'audit']) assert.ok(kinds.has(k), k);
    pass();
    assert.equal(r.body.items.filter(i => i.kind === 'message').length, 2, 'both messages, the deleted one included'); pass();
    assert.ok(!r.body.items.some(i => i.recordId === catBob), 'Cat and Bob\'s conversation is out of scope'); pass();
    assert.deepEqual(r.body.custody.map(c => c.action).slice(0, 3), ['placed', 'captured', 'viewed']); pass();
    assert.equal(r.body.chainIntact, true); pass();
    assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}' AND (position(convert_to('Fictional threat', 'UTF8') in sealed) > 0)`), '0', 'never stored in clear'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%REF-HOLD%' OR detail LIKE '%Fictional threat%'`), '0', 'no reason or content in the audit log'); pass();
    assert.equal(await sql(`SELECT count(*) FROM custody_events WHERE detail LIKE '%REF-HOLD%'`), '0'); pass();
    assert.equal((await call(st.leg, `/api/admin/holds/${crypto.randomUUID()}`)).status, 404); pass();
    assert.equal((await call(st.adm, `/api/admin/holds/${holdId}`)).status, 403, 'an admin can\'t read a hold'); pass();

    // An admin's provisional hold from a case: only the case's people.
    assert.equal((await call(tokens.bob, '/api/reports', { method: 'POST', body: { subjectType: 'profile', subjectId: pid.ana, category: 'harassment', text: null, block: false } })).status, 201);
    const caseId = await sql(`SELECT id FROM moderation_cases WHERE subject_account_id = '${ids.ana}' AND status IN ('Open', 'UnderReview')`);
    assert.deepEqual(code(await call(st.adm, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional serious report', accountIds: [ids.ana, ids.dan], caseId } })), [400, 'hold-scope-invalid']); pass();
    assert.deepEqual(code(await call(st.adm, '/api/admin/holds', { method: 'POST', body: { reason: 'x', accountIds: [ids.ana], caseId: crypto.randomUUID() } })), [400, 'hold-case-required']); pass();
    r = await call(st.adm, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional serious report', accountIds: [ids.ana, ids.bob], caseId } });
    assert.equal(r.status, 201); assert.equal(r.body.status, 'provisional'); pass();
    const provisional = r.body.id;
    assert.ok(Math.abs(new Date(r.body.reviewDue) - Date.now() - 30 * 864e5) < 3600e3, 'reviewed within 30 days'); pass();
    r = await call(st.adm, '/api/admin/holds/overview');
    assert.equal(r.status, 200); assert.ok(r.body.live >= 2); pass();
    assert.equal((await call(st.adm, `/api/admin/holds/${provisional}/confirm`, { method: 'POST' })).status, 403); pass();
    r = await call(st.leg, `/api/admin/holds/${provisional}/confirm`, { method: 'POST' });
    assert.equal(r.status, 200); assert.equal(r.body.status, 'active'); pass();
    assert.deepEqual(code(await call(st.leg, `/api/admin/holds/${provisional}/confirm`, { method: 'POST' })), [409, 'hold-not-provisional']); pass();
    assert.deepEqual(code(await call(st.leg, `/api/admin/holds/${holdId}/review`, { method: 'POST', body: { reviewDue: new Date(Date.now() + 400 * 864e5).toISOString() } })), [400, 'hold-dates-invalid']); pass();
    r = await call(st.leg, `/api/admin/holds/${holdId}/review`, { method: 'POST', body: { reviewDue: new Date(Date.now() + 120 * 864e5).toISOString() } });
    assert.equal(r.status, 200); assert.ok(r.body.custody.some(c => c.action === 'reviewed')); pass();

    // Ana deletes her account: deletion completes, held records stay in the evidence store.
    const before = Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}'`));
    tokens.ana = await memberToken(browser, members.ana);
    assert.equal((await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    assert.match(await cli('accounts', 'purge'), /Accounts: [1-9]\d* deleted/);
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.ana}'`), 'Deleted'); pass();
    if(process.env.SWOOVR_TEST_APP_DATABASE){
      assert.equal(await sql(`SELECT count(*) FROM policy_acceptances WHERE account_id='${ids.ana}'`),'0','held final deletion erases ordinary acceptance after real provider deletion');pass();
      assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id='${holdId}' AND kind='policy-acceptance'`),'1','active hold retains independent encrypted acceptance proof');pass();
    }

    assert.equal(await sql(`SELECT count(*) FROM messages WHERE sender_account_id = '${ids.ana}'`), '0', 'her live messages are gone'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}'`)) > before, 'the pending-deletion state was captured too'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}' AND kind = 'message'`)) >= 2, 'her messages stay preserved'); pass();

    // `messages purge`: deleted text past 30 days stays while held; unheld text (Cat and Dan, nobody held) goes.
    await sql(`UPDATE messages SET deleted_at = now() - interval '40 days' WHERE id IN ('${deletedByBob}', '${catDeleted}')`);
    assert.match(await cli('messages', 'purge'), /kept under a legal hold/);
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${deletedByBob}'`), 'Fictional message Bob deletes'); pass();
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${catDeleted}'`), ''); pass();

    // Exports: purpose required, disclosures complete, one-time, the officer's own, hashes that check out.
    const exp = body => call(st.leg, `/api/admin/holds/${holdId}/exports`, { method: 'POST', body });
    // #88: exporting needs time-limited access with a recorded purpose.
    assert.deepEqual(code(await exp({ purpose: 'Fictional' })), [403, 'elevated-access-required']); pass();
    await elevate(st.leg, 'evidence-export', 'Fictional response to REF-HOLD-1');
    assert.deepEqual(code(await exp({ purpose: '' })), [400, 'disclosure-invalid']); pass();
    assert.deepEqual(code(await exp({ purpose: 'x', disclosure: { agency: 'SA Police (fictional)', officer: 'Fictional Officer', reference: 'REF-HOLD-1', legalBasis: 'because', verifiedHow: 'x' } })), [400, 'disclosure-invalid']); pass();
    assert.equal((await call(st.adm, `/api/admin/holds/${holdId}/exports`, { method: 'POST', body: { purpose: 'x' } })).status, 403); pass();
    r = await exp({ purpose: 'Fictional response to REF-HOLD-1', disclosure: { agency: 'SA Police (fictional)', officer: 'Fictional Officer', reference: 'REF-HOLD-1', legalBasis: 'warrant', verifiedHow: 'Called the published station number' } });
    assert.equal(r.status, 201); pass();
    const token = r.body.token;
    assert.equal((await call(st.adm, `/api/admin/evidence-exports/${token}`)).status, 403); pass();
    const download = await fetch(api + `/api/admin/evidence-exports/${token}`, { headers: { Authorization: 'Bearer ' + st.leg } });
    assert.equal(download.status, 200); pass();
    const pkg = await download.json();
    assert.equal(pkg.format, 'swoovr-evidence/1'); assert.equal(pkg.disclosure.legalBasis, 'warrant'); pass();
    const sha = s => crypto.createHash('sha256').update(s).digest();
    assert.ok(pkg.items.every(i => sha(Buffer.from(i.contentJson, 'utf8')).toString('hex') === i.sha256), 'every item hash checks'); pass();
    assert.equal(crypto.createHash('sha256').update(Buffer.concat(pkg.items.map(i => Buffer.from(i.sha256, 'hex')))).digest('hex'), pkg.manifest.packageSha256); pass();
    assert.ok(pkg.items.some(i => i.kind === 'message' && i.contentJson.includes('Fictional threat from Ana')), 'the deleted member\'s message is in the package'); pass();
    assert.equal(pkg.custody.intact, true); assert.ok(pkg.custody.events.some(e => e.action === 'disclosed') && pkg.custody.events.some(e => e.action === 'exported')); pass();
    assert.deepEqual(code(await call(st.leg, `/api/admin/evidence-exports/${token}`)), [404, 'export-unavailable'], 'used once'); pass();
    assert.deepEqual(code(await call(st.leg, '/api/admin/evidence-exports/not-a-token!')), [404, 'export-unavailable']); pass();
    r = await call(st.leg, '/api/admin/disclosures');
    assert.ok(r.body.some(d => d.reference === 'REF-HOLD-1' && d.holdId === holdId)); pass();
    assert.equal((await call(st.adm, '/api/admin/disclosures')).status, 403); pass();

    // Release resumes the independent retention schedule; required evidence and custody remain. D195: by an officer
    // who neither placed nor confirmed it, after checking the overlapping (provisional, now confirmed) hold on Ana.
    assert.deepEqual(code(await call(st.leg2, `/api/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: '' } })), [400, 'hold-reason-required']); pass();
    assert.equal((await call(st.adm, `/api/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: 'x' } })).status, 403); pass();
    assert.deepEqual(code(await call(st.leg, `/api/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: 'Fictional investigation closed' } })), [403, 'hold-release-not-independent']); pass();
    assert.deepEqual(code(await call(st.leg2, `/api/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: 'Fictional investigation closed' } })), [409, 'hold-overlaps-unchecked']); pass();
    r = await call(st.leg2, `/api/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: 'Fictional investigation closed', overlapsChecked: true } });
    assert.equal(r.status, 200); assert.equal(r.body.status, 'released'); assert.ok(r.body.items.length > 0, 'release preserves required copies'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}'`)) > 0); pass();
    assert.deepEqual(code(await call(st.leg, `/api/admin/holds/${holdId}/capture`, { method: 'POST' })), [409, 'hold-released']); pass();
    assert.equal((await exp({ purpose: 'Fictional retained evidence review' })).status, 201); pass();
    assert.ok((await call(st.leg, '/api/admin/holds?status=released')).body.some(h => h.id === holdId)); pass();
    r = await call(st.leg, `/api/admin/holds/${holdId}`);
    assert.ok(r.body.custody.some(c => c.action === 'released') && r.body.custody.some(c => c.action === 'release-refused')); pass();
    // Bob's deleted message is still under the other (confirmed) hold, which names Bob.
    assert.match(await cli('messages', 'purge'), /kept under a legal hold/);
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${deletedByBob}'`), 'Fictional message Bob deletes'); pass();

    // The admin app in Edge: the officer's hold pages, placing a hold and a one-time download.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, acceptDownloads: true });
    const page = await context.newPage();
    await page.goto(admin + '/');
    await page.fill('#username', staff.leg.email); await page.fill('#password', staff.leg.password); await page.click('#kc-login');
    await page.waitForSelector('[data-legal-home]', { timeout: 20000 }); pass();
    // Shared fictional runs exceed the200-row due-review window. Make only this fixture due first.
    await sql(`UPDATE legal_holds SET review_due=now()-interval '1 day' WHERE id='${provisional}'`);
    await page.goto(admin + '/holds');
    await page.waitForSelector('[data-holds]');
    assert.ok(await page.locator(`[data-hold="${provisional}"]`).count() === 1); pass();
    await page.fill('textarea[name="reason"]', 'Fictional hold placed in the admin app');
    await page.fill('textarea[name="accounts"]', ids.dan);
    await page.click('[data-place]');
    await page.waitForSelector('[data-result="ok"]'); pass();
    const danHold = await sql(`SELECT h.id FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id = h.id WHERE s.target_id = '${ids.dan}' LIMIT 1`);
    await page.goto(admin + `/holds/${danHold}`);
    await page.waitForSelector(`[data-hold-detail="${danHold}"]`);
    assert.equal(await page.getAttribute('[data-chain]', 'data-chain'), 'intact'); pass();
    if (output) await page.screenshot({ path: output + '/admin-hold.png', fullPage: true });
    await page.fill('textarea[name="purpose"]', 'Fictional internal review');
    await page.click('[data-export]');
    await page.waitForSelector('[data-export-link] a');
    const [file] = await Promise.all([page.waitForEvent('download'), page.click('[data-export-link] a')]);
    const saved = JSON.parse(await require('node:fs/promises').readFile(await file.path(), 'utf8'));
    assert.equal(saved.format, 'swoovr-evidence/1'); assert.equal(saved.hold.id, danHold); pass();
    await page.goto(admin + `/holds/${holdId}`);
    await page.waitForSelector(`[data-hold-detail="${holdId}"]`);
    assert.ok((await page.locator('body').innerText()).includes('retention')); pass();
    if (output) await page.screenshot({ path: output + '/admin-released-retained-hold.png', fullPage: true });
    const disposition={reason:'Fictional independent review confirms no qualifying review or police obligation.',confirmed:true};
    assert.equal((await call(st.sup, `/api/admin/holds/${danHold}/retention-disposition`, {method:'POST',body:disposition})).status,403);pass();
    assert.equal((await call(st.leg2, `/api/admin/holds/${danHold}/release`, {method:'POST',body:{reason:'Fictional unlinked preservation completed',overlapsChecked:true}})).status,200);pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id='${danHold}'`))>0,'release alone never infers no review');pass();
    assert.equal((await call(st.leg, `/api/admin/holds/${danHold}/retention-disposition`,{method:'POST',body:disposition})).status,409,'placer cannot self-authorise disposal');pass();
    assert.equal((await call(st.leg2, `/api/admin/holds/${holdId}/retention-disposition`,{method:'POST',body:disposition})).status,409,'linked review material cannot bypass schedule');pass();
    assert.equal((await call(st.leg2, `/api/admin/holds/${danHold}/retention-disposition`,{method:'POST',body:disposition})).status,200);pass();
    assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id='${danHold}'`),'0','independent positive disposition permits otherwise unneeded copies to expire');pass();
    assert.equal((await call(st.leg2, `/api/admin/holds/${danHold}/retention-disposition`,{method:'POST',body:disposition})).status,200,'disposition is idempotent');pass();
    await page.goto(admin+`/holds/${danHold}`);
    await page.waitForSelector('[data-retention-state="disposed"]');pass();
    if(output) await page.screenshot({path:output+'/admin-disposed-hold.png',fullPage:true});
    await context.close();
    // An admin's case page places a provisional hold.
    const adminContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const adminPage = await adminContext.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.adm.email); await adminPage.fill('#password', staff.adm.password); await adminPage.click('#kc-login');
    await adminPage.waitForSelector('[data-holds-overview]', { timeout: 20000 }); pass();
    await adminPage.goto(admin + `/cases/${caseId}`);
    await adminPage.fill('input[name="PreserveForm.Reason"]', 'Fictional preservation from the case page');
    await adminPage.click('[data-preserve]');
    await adminPage.waitForSelector('[data-result="ok"]'); pass();
    await adminPage.goto(admin + '/holds');
    await adminPage.waitForSelector('[data-holds="refused"]'); pass(); // admins don't read holds
    await adminContext.close();

    console.log(`PASS: legal holds and evidence (${passed} checks).`);
  } finally {
    await edge.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
