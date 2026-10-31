// Local acceptance for slice AB (police-contact procedure §2–§14, D195): restricted law-enforcement case management.
// Cases with requests, requester details and independent verification, the legal assessment and APP 6.5 note, timers,
// separated approvals and break-glass review, implicated staff excluded, temporary police preservation holds, independent
// release after checking overlapping holds, redacted and corrected export packages, transfers with receipts, notification
// decisions, the closure checklist (holds outlive closure), seven-year retention, purges failing closed, holds blocking
// admin deletion, the transparency aggregate and the quarterly audit export, the public authority page and the admin app.
// Negative cases: members, support, admins, implicated staff, unknown ids, tampered bodies, closed cases, wrong realms.
// Needs the local stack, the enrolled fictional root admin and hosts built from this checkout (API with
// Staff__LocalPasswords=true, admin app on 5082). Every person, agency and officer is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const LE = '/api/admin/law-enforcement';
const AGENCY = 'Fictional SA Police Cybercrime Unit';
const OFFICER = 'Detective Fictional Officer';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('le-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      pid[n] = (await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional LE ' + n, homeCityId: ADELAIDE } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const req = await call(tokens.ana, '/api/requests', { method: 'POST', body: { to: pid.bob, mode: 'friends' } });
    assert.equal((await call(tokens.bob, `/api/requests/${req.body.id}/accept`, { method: 'POST' })).status, 200);
    const convo = (await call(tokens.ana, '/api/conversations', { method: 'POST', body: { to: pid.bob } })).body.id;
    await call(tokens.ana, `/api/conversations/${convo}/messages`, { method: 'POST', body: { text: 'Fictional message about a fictional meeting' } });

    // Staff: three security and legal officers (A owns cases, B approves, C will be implicated), support and an admin.
    const staff = { a: await createStaff('le-a'), b: await createStaff('le-b'), c: await createStaff('le-c'), sup: await createStaff('le-sup'), adm: await createStaff('le-adm') };
    for (const [n, role] of [['a', 'legal'], ['b', 'legal'], ['c', 'legal'], ['sup', 'support'], ['adm', 'admin']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) st[n] = await staffToken(browser, staff[n]);
    const sid = {};
    for (const n of Object.keys(staff)) await call(st[n], '/api/session');
    for (const n of Object.keys(staff)) sid[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st[n]).sub}'`);

    // Only `legal` reaches cases.
    for (const [who, token] of [['member', tokens.ana], ['support', st.sup], ['admin', st.adm]]) {
      assert.equal((await call(token, LE + '/cases')).status, 403, who);
      assert.equal((await call(token, LE + '/overview')).status, 403, who);
      assert.equal((await call(token, LE + '/transparency')).status, 403, who);
    }
    pass();
    assert.equal((await call(null, LE + '/cases')).status, 401); pass();
    assert.equal((await call(st.adm, LE + '/cases', { method: 'POST', body: {} })).status, 403); pass();

    // Validation and tampering.
    const base = { channel: 'email', urgency: 'urgent', agency: AGENCY, agencyCountry: 'AU', agencyReference: 'FICT-REF-1', officerName: OFFICER,
      officerEmail: 'fictional.officer@example.test', officerPhone: '0491 570 156', subjects: [ids.ana, ids.bob], privileged: false, confidential: false,
      request: { kind: 'disclosure', description: 'Fictional request: messages between the two subjects in October', from: null, until: null } };
    const open = body => call(st.a, LE + '/cases', { method: 'POST', body });
    assert.deepEqual(code(await open({ ...base, channel: 'carrier-pigeon' })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, urgency: 'whenever' })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, officerName: ' ' })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, request: null })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, request: { kind: 'disclosure', description: 'x'.repeat(2001) } })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, receivedAt: new Date(Date.now() + 864e5).toISOString() })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, subjects: [sid.sup] })), [400, 'le-case-invalid'], 'staff are not subjects'); pass();
    assert.deepEqual(code(await open({ ...base, subjects: [crypto.randomUUID()] })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await open({ ...base, agencyCountry: 'AUS' })), [400, 'le-case-invalid']); pass();
    const tampered = await fetch(api + LE + '/cases', { method: 'POST', headers: { Authorization: 'Bearer ' + st.a, 'Content-Type': 'application/json' }, body: '{"channel":"email","subjects":"everyone"}' });
    assert.equal(tampered.status, 400); pass();

    // A case: urgent, four-hour response timer.
    let r = await open(base);
    assert.equal(r.status, 201); pass();
    const caseId = r.body.id;
    assert.match(r.body.reference, /^LE-\d{4}-[0-9A-F]{6}$/); pass();
    assert.equal(r.body.timer, 'response-due'); assert.ok(Math.abs(new Date(r.body.due) - Date.now() - 4 * 3600e3) < 120e3); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id = '${caseId}' AND (detail LIKE '%Fictional%' OR detail LIKE '%FICT-REF%')`), '0', 'audit holds ids only'); pass();
    const step = (token, path, body, method = 'POST') => call(token, `${LE}/cases/${caseId}/${path}`, { method, body });
    // A critical case received two hours ago is already overdue.
    r = await open({ ...base, urgency: 'critical', subjects: [ids.cat], receivedAt: new Date(Date.now() - 2 * 3600e3).toISOString(), request: { kind: 'emergency-disclosure', description: 'Fictional imminent danger' } });
    const critical = r.body.id;
    assert.equal(r.body.timer, 'response-overdue'); pass();
    r = await call(st.a, LE + '/overview');
    assert.ok(r.body.open >= 2 && r.body.overdue >= 1); pass();
    r = await call(st.a, LE + '/cases');
    assert.ok(r.body.findIndex(c => c.id === critical) < r.body.findIndex(c => c.id === caseId), 'overdue first'); pass();
    assert.equal((await call(st.a, `${LE}/cases/${crypto.randomUUID()}`)).status, 404); pass();

    // A temporary police preservation hold needs independent verification first.
    const tempHold = body => call(st.a, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional police preservation request', accountIds: [ids.ana, ids.bob], leCaseId: caseId, kind: 'police-preservation', lawfulBasis: 'Verified request before formal process', ...body } });
    assert.deepEqual(code(await tempHold({})), [409, 'le-not-verified']); pass();
    assert.deepEqual(code(await step(st.a, 'verify', { source: 'email', evidence: 'Came from a government address' })), [400, 'le-case-invalid'], 'an email alone never verifies'); pass();
    assert.deepEqual(code(await step(st.a, 'verify', { source: 'agency-switchboard', evidence: '' })), [400, 'le-case-invalid']); pass();
    r = await step(st.a, 'verify', { source: 'agency-switchboard', evidence: 'Called the fictional published switchboard; the duty sergeant confirmed the officer and reference' });
    assert.equal(r.status, 200); assert.ok(r.body.verifiedAt); pass();
    assert.deepEqual(code(await tempHold({ reviewDue: new Date(Date.now() + 40 * 864e5).toISOString() })), [400, 'le-date-invalid']); pass();
    assert.deepEqual(code(await tempHold({ accountIds: [ids.cat] })), [400, 'hold-scope-invalid'], 'only the case\'s subjects'); pass();
    assert.deepEqual(code(await call(st.a, '/api/admin/holds', { method: 'POST', body: { reason: 'x', accountIds: [ids.ana], kind: 'police-preservation' } })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await tempHold({ kind: 'forever' })), [400, 'le-case-invalid']); pass();
    r = await tempHold({});
    assert.equal(r.status, 201); pass();
    const tempId = r.body.id;
    assert.ok(Math.abs(new Date(r.body.reviewDue) - Date.now() - 30 * 864e5) < 3600e3, 'reviewed within 30 days'); pass();
    r = await call(st.a, `/api/admin/holds/${tempId}`);
    assert.equal(r.body.kind, 'police-preservation'); assert.equal(r.body.leCaseId, caseId); assert.equal(r.body.requestingBody, AGENCY); pass();
    assert.deepEqual(code(await call(st.a, `/api/admin/holds/${tempId}/review`, { method: 'POST', body: { reviewDue: new Date(Date.now() + 45 * 864e5).toISOString() } })), [400, 'hold-dates-invalid'], 'extensions 30 days at a time'); pass();
    assert.equal((await call(st.a, `/api/admin/holds/${tempId}/review`, { method: 'POST', body: { reviewDue: new Date(Date.now() + 25 * 864e5).toISOString() } })).status, 200); pass();

    // Deciding: assessment, APP 6.5 note, separation of collector and approver.
    const requestId = (await call(st.a, `${LE}/cases/${caseId}`)).body.requests[0].id;
    const decide = (token, body, rid = requestId) => step(token, `requests/${rid}/decision`, body);
    assert.deepEqual(code(await decide(st.b, { outcome: 'disclosed', reason: 'Fictional warrant', breakGlass: false })), [409, 'le-not-assessed']); pass();
    assert.deepEqual(code(await step(st.a, 'assessment', { assessment: 'x', instrument: 'vibes' })), [400, 'le-case-invalid']); pass();
    assert.equal((await step(st.a, 'assessment', { assessment: 'Fictional: voluntary disclosure under APP 6.2(e); narrow to October messages', instrument: 'app-6.2e', mandatory: false })).status, 200); pass();
    assert.deepEqual(code(await decide(st.b, { outcome: 'disclosed', reason: 'Fictional basis', breakGlass: false })), [409, 'le-app65-required']); pass();
    assert.equal((await step(st.a, 'app65', { note: 'Fictional APP 6.5 note: reasonable belief based on the fictional report; extent: October messages; recipient: the fictional officer' })).status, 200); pass();
    assert.deepEqual(code(await decide(st.a, { outcome: 'disclosed', reason: 'Fictional basis', breakGlass: false })), [403, 'le-not-separated'], 'the owner doesn\'t approve their own case'); pass();
    assert.deepEqual(code(await decide(st.a, { outcome: 'disclosed', reason: 'On call', breakGlass: true })), [400, 'le-case-invalid'], 'break-glass only in an emergency'); pass();
    assert.deepEqual(code(await decide(st.b, { outcome: 'leaked', reason: 'x', breakGlass: false })), [400, 'le-case-invalid']); pass();
    r = await decide(st.b, { outcome: 'partly-disclosed', reason: 'Fictional: October messages only, narrowed', breakGlass: false });
    assert.equal(r.status, 200); assert.equal(r.body.requests[0].status, 'partly-disclosed'); pass();
    assert.deepEqual(code(await decide(st.b, { outcome: 'refused', reason: 'x', breakGlass: false })), [409, 'le-already-decided']); pass();
    // Break-glass on the critical case, reviewed afterwards by someone else.
    const criticalStep = (token, path, body) => call(token, `${LE}/cases/${critical}/${path}`, { method: 'POST', body });
    await criticalStep(st.a, 'verify', { source: 'agency-switchboard', evidence: 'Fictional accelerated callback through the published switchboard' });
    await criticalStep(st.a, 'assessment', { assessment: 'Fictional: serious threat to life', instrument: 'serious-threat', mandatory: false });
    const emergencyId = (await call(st.a, `${LE}/cases/${critical}`)).body.requests[0].id;
    r = await criticalStep(st.a, `requests/${emergencyId}/decision`, { outcome: 'disclosed', reason: 'Fictional on-call emergency approval', breakGlass: true });
    assert.equal(r.status, 200); assert.equal(r.body.requests[0].breakGlass, true); assert.ok(r.body.requests[0].retrospectiveDue); pass();
    assert.deepEqual(code(await criticalStep(st.a, `requests/${emergencyId}/retrospective`, { note: 'Self review' })), [403, 'le-not-separated']); pass();
    assert.ok((await call(st.a, LE + '/overview')).body.retrospectivesDue >= 1); pass();
    assert.equal((await criticalStep(st.b, `requests/${emergencyId}/retrospective`, { note: 'Fictional review: proportionate and minimal' })).status, 200); pass();

    // Implicated staff are excluded.
    assert.deepEqual(code(await step(st.a, 'people', { accountId: ids.cat, kind: 'implicated-staff' })), [400, 'le-case-invalid'], 'a member isn\'t staff'); pass();
    assert.deepEqual(code(await step(st.a, 'people', { accountId: sid.c, kind: 'subject' })), [400, 'le-case-invalid'], 'staff aren\'t subjects'); pass();
    assert.deepEqual(code(await step(st.a, 'people', { accountId: sid.a, kind: 'implicated-staff' })), [400, 'le-case-invalid'], 'reassign before naming the owner'); pass();
    assert.equal((await step(st.a, 'people', { accountId: sid.c, kind: 'implicated-staff' })).status, 200); pass();
    assert.equal((await call(st.c, `${LE}/cases/${caseId}`)).status, 403); pass();
    assert.equal((await call(st.c, `/api/admin/holds/${tempId}`)).status, 403, 'nor the case\'s holds'); pass();
    assert.ok(!(await call(st.c, LE + '/cases')).body.some(c => c.id === caseId), 'not listed'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'le.refused-excluded' AND target_id = '${caseId}'`) > '0', true); pass();
    assert.deepEqual(code(await step(st.a, 'owner', { ownerId: sid.c })), [400, 'le-case-invalid'], 'implicated staff can\'t own it'); pass();
    assert.deepEqual(code(await step(st.a, 'owner', { ownerId: sid.adm })), [400, 'le-case-invalid'], 'owners are legal'); pass();

    // Exports for the case: a redacted disclosure copy, the original untouched, and a traceable correction.
    const holdDetail = (await call(st.a, `/api/admin/holds/${tempId}`)).body;
    const message = holdDetail.items.find(i => i.kind === 'message');
    const exp = body => call(st.a, `/api/admin/holds/${tempId}/exports`, { method: 'POST', body: { purpose: 'Fictional disclosure for the case', leCaseId: caseId, ...body } });
    await elevate(st.a, 'evidence-export', 'Fictional disclosure for the case'); // #88
    assert.deepEqual(code(await exp({ leCaseId: critical })), [400, 'le-case-invalid'], 'the hold serves another case'); pass();
    assert.deepEqual(code(await exp({ redactions: [{ itemId: crypto.randomUUID(), fields: ['message.body'], reason: 'x' }] })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await exp({ redactions: [{ itemId: message.id, fields: [], reason: 'x' }] })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await exp({ correctionReason: 'No original named' })), [400, 'le-case-invalid']); pass();
    r = await exp({ redactions: [{ itemId: message.id, fields: ['message.body'], reason: 'Fictional: unrelated personal detail' }],
      disclosure: { agency: AGENCY, officer: OFFICER, reference: 'FICT-REF-1', legalBasis: 'app-6.2e', verifiedHow: 'Fictional switchboard callback' } });
    assert.equal(r.status, 201); pass();
    const download = async token => (await fetch(api + `/api/admin/evidence-exports/${token}`, { headers: { Authorization: 'Bearer ' + st.a } })).json();
    const pkg = await download(r.body.token);
    const sha = s => crypto.createHash('sha256').update(Buffer.from(s, 'utf8')).digest('hex');
    const redacted = pkg.items.find(i => i.id === message.id);
    assert.equal(redacted.redacted, true); assert.ok(!redacted.contentJson.includes('fictional meeting') && redacted.contentJson.includes('[REDACTED]')); pass();
    assert.equal(sha(redacted.contentJson), redacted.disclosedSha256); assert.notEqual(redacted.sha256, redacted.disclosedSha256); pass();
    assert.ok(pkg.items.every(i => sha(i.contentJson) === i.disclosedSha256), 'every disclosed hash checks'); pass();
    assert.equal(crypto.createHash('sha256').update(Buffer.concat(pkg.items.map(i => Buffer.from(i.disclosedSha256, 'hex')))).digest('hex'), pkg.manifest.packageSha256); pass();
    assert.equal(crypto.createHash('sha256').update(Buffer.concat(pkg.items.map(i => Buffer.from(i.sha256, 'hex')))).digest('hex'), pkg.manifest.originalPackageSha256); pass();
    assert.equal(pkg.case.id, caseId); assert.equal(pkg.hold.kind, 'police-preservation'); pass();
    const firstExport = pkg.id;
    r = await exp({ supplementsExportId: firstExport, correctionReason: 'Fictional: one message timestamp was mislabelled' });
    assert.equal(r.status, 201); pass();
    const correction = await download(r.body.token);
    assert.equal(correction.supplements.id, firstExport); assert.equal(correction.supplements.packageSha256, pkg.manifest.packageSha256); pass();
    assert.ok(correction.items.find(i => i.id === message.id).contentJson.includes('fictional meeting'), 'the original evidence is untouched'); pass();
    const pending = await exp({});
    assert.deepEqual(code(await exp({ supplementsExportId: (await sql(`SELECT id FROM evidence_exports WHERE downloaded_at IS NULL AND hold_id = '${tempId}' LIMIT 1`)), correctionReason: 'x' })), [400, 'le-case-invalid'], 'only downloaded packages are corrected'); pass();
    assert.equal(pending.status, 201);

    // Transfers and receipts.
    const pendingId = await sql(`SELECT id FROM evidence_exports WHERE downloaded_at IS NULL AND hold_id = '${tempId}' LIMIT 1`);
    assert.deepEqual(code(await step(st.a, 'transfers', { exportId: pendingId, recipient: OFFICER, method: 'secure-link' })), [400, 'le-case-invalid'], 'not downloaded'); pass();
    assert.deepEqual(code(await step(st.a, 'transfers', { exportId: firstExport, recipient: '', method: 'secure-link' })), [400, 'le-case-invalid']); pass();
    assert.deepEqual(code(await step(st.a, 'transfers', { exportId: firstExport, recipient: OFFICER, method: 'fax' })), [400, 'le-case-invalid']); pass();
    r = await step(st.a, 'transfers', { exportId: firstExport, recipient: OFFICER + ' (verified)', method: 'secure-link' });
    assert.equal(r.status, 200); assert.equal(r.body.transfers[0].packageSha256, pkg.manifest.packageSha256); pass();
    const transferId = r.body.transfers[0].id;
    assert.ok((await call(st.a, `/api/admin/holds/${tempId}`)).body.custody.some(c => c.action === 'transferred')); pass();

    // Closure: refused while anything is outstanding.
    r = await step(st.a, 'close', { note: 'Fictional closure', confirmObligations: true });
    assert.equal(r.status, 409); assert.equal(r.body.code, 'le-closure-incomplete'); assert.ok(r.body.missing.includes('transfer-receipts') && r.body.missing.includes('notification-decisions')); pass();
    assert.equal((await step(st.a, `transfers/${transferId}/receipt`, { reference: 'FICT-RECEIPT-1' })).status, 200); pass();
    assert.deepEqual(code(await step(st.a, `transfers/${transferId}/receipt`, { reference: 'again' })), [400, 'le-case-invalid'], 'once'); pass();
    // Notification decisions (§10).
    assert.deepEqual(code(await step(st.a, 'notifications', { accountId: ids.ana, decision: 'delay', reason: 'Fictional investigation' })), [400, 'le-date-invalid']); pass();
    assert.deepEqual(code(await step(st.a, 'notifications', { accountId: ids.ana, decision: 'delay', reason: 'x', reconsiderAt: new Date(Date.now() + 200 * 864e5).toISOString() })), [400, 'le-date-invalid']); pass();
    assert.deepEqual(code(await step(st.a, 'notifications', { accountId: ids.cat, decision: 'notified', reason: 'x' })), [400, 'le-case-invalid'], 'not a subject'); pass();
    assert.equal((await step(st.a, 'notifications', { accountId: ids.ana, decision: 'delay', reason: 'Fictional: notice would prejudice the investigation', reconsiderAt: new Date(Date.now() + 60 * 864e5).toISOString() })).status, 200); pass();
    r = await step(st.a, 'notifications', { accountId: ids.bob, decision: 'withhold', reason: 'Fictional: order prohibits notice', reconsiderAt: new Date(Date.now() + 90 * 864e5).toISOString() });
    assert.ok(r.body.closure.every(i => i.done), JSON.stringify(r.body.closure)); pass();
    assert.deepEqual(code(await step(st.a, 'close', { note: 'Fictional closure', confirmObligations: false })), [400, 'le-case-invalid']); pass();
    r = await step(st.a, 'close', { note: 'Fictional: disclosed, receipted, holds continue under review', confirmObligations: true });
    assert.equal(r.status, 200); assert.equal(r.body.status, 'closed'); pass();
    assert.ok(Math.abs(new Date(r.body.retainUntil) - new Date(r.body.closedAt) - 7 * 365.25 * 864e5) < 3 * 864e5, 'kept seven years'); pass();
    assert.equal(await sql(`SELECT status FROM legal_holds WHERE id = '${tempId}'`), 'Active', 'closing never releases a hold'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${tempId}'`)) > 0, 'nor destroys evidence'); pass();
    assert.deepEqual(code(await step(st.a, 'requests', { kind: 'other', description: 'late' })), [409, 'le-case-closed']); pass();

    // Holds block admin deletion; purges fail closed when hold status can't be read.
    await sql(`UPDATE member_profiles SET id_status = 'Verified' WHERE account_id = '${ids.bob}'`);
    r = await call(root, `/api/admin/accounts/${ids.bob}/founder`, { method: 'POST' });
    if (r.status === 201) {
      const token = (await call(root, `/api/admin/founder-tokens?place=${r.body.place}`)).body[0];
      assert.deepEqual(code(await call(root, `/api/admin/founder-tokens/${token.id}`, { method: 'DELETE' })), [409, 'held-by-legal-hold']); pass();
    } else console.log(`note: founder grant answered ${r.status} (${r.body?.code}); admin deletion check skipped`);
    tokens.ana = await memberToken(browser, members.ana);
    assert.equal((await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    process.env.Evidence__HoldCheckUnavailable = 'true';
    await assert.rejects(cli('accounts', 'purge'), /hold status unavailable/); pass();
    await assert.rejects(cli('messages', 'purge'), /hold status unavailable/); pass();
    delete process.env.Evidence__HoldCheckUnavailable;
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.ana}'`), 'PendingDeletion', 'nothing purged while hold status was unknown'); pass();
    await cli('accounts', 'purge');
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.ana}'`), 'Deleted'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${tempId}' AND account_id = '${ids.ana}'`)) > 0, 'held records preserved'); pass();

    // Independent release after checking overlapping holds (§7).
    r = await call(st.b, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional overlapping hold', accountIds: [ids.bob] } });
    const overlapping = r.body.id;
    assert.deepEqual(code(await call(st.a, `/api/admin/holds/${tempId}/release`, { method: 'POST', body: { reason: 'Fictional: formal process complete' } })), [403, 'hold-release-not-independent']); pass();
    assert.equal((await call(st.a, `/api/admin/holds/${tempId}`)).body.overlaps, 1); pass();
    assert.deepEqual(code(await call(st.b, `/api/admin/holds/${tempId}/release`, { method: 'POST', body: { reason: 'Fictional: formal process complete' } })), [409, 'hold-overlaps-unchecked']); pass();
    r = await call(st.b, `/api/admin/holds/${tempId}/release`, { method: 'POST', body: { reason: 'Fictional: formal process complete', overlapsChecked: true } });
    assert.equal(r.status, 200); assert.equal(r.body.releasedBy, sid.b); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${overlapping}'`)) > 0, 'the overlapping hold keeps its own evidence'); pass();
    assert.deepEqual(code(await call(st.b, `/api/admin/holds/${overlapping}/release`, { method: 'POST', body: { reason: 'x' } })), [403, 'hold-release-not-independent']); pass();
    assert.equal((await call(st.a, `/api/admin/holds/${overlapping}/release`, { method: 'POST', body: { reason: 'Fictional: no longer needed' } })).status, 200); pass();

    // Transparency (confidential cases left out, small counts suppressed) and the quarterly audit export (no case content).
    await open({ ...base, confidential: true, subjects: [ids.cat], request: { kind: 'preservation', description: 'Fictional confidential request' } });
    r = await call(st.a, LE + '/transparency?year=' + new Date().getUTCFullYear());
    assert.equal(r.status, 200); assert.equal(r.body.suppressedBelow, 5); pass();
    assert.ok(Object.values(r.body.requestKinds).every(v => v === null || v === 0 || v >= 5), 'no count from 1 to 4 is shown'); pass();
    const confidentialCount = Number(await sql(`SELECT count(*) FROM restricted.le_cases WHERE confidential AND date_part('year', received_at) = date_part('year', now())`));
    const plainCount = Number(await sql(`SELECT count(*) FROM restricted.le_cases WHERE NOT confidential AND date_part('year', received_at) = date_part('year', now())`));
    assert.ok(confidentialCount >= 1); assert.equal(r.body.cases, plainCount > 0 && plainCount < 5 ? null : plainCount, 'confidential cases are left out'); pass();
    assert.equal((await call(st.a, LE + '/transparency?year=2031')).body.cases, 0); pass();
    assert.deepEqual(code(await call(st.a, LE + '/audit-export?quarter=2026-Q9')), [400, 'le-case-invalid']); pass();
    const quarter = `${new Date().getUTCFullYear()}-Q${Math.floor(new Date().getUTCMonth() / 3) + 1}`;
    const auditResponse = await fetch(api + LE + '/audit-export?quarter=' + quarter, { headers: { Authorization: 'Bearer ' + st.a } });
    assert.equal(auditResponse.status, 200); pass();
    const auditText = await auditResponse.text();
    const audit = JSON.parse(auditText);
    assert.equal(audit.format, 'swoovr-le-audit/1'); assert.ok(audit.cases.some(c => c.id === caseId)); pass();
    for (const secret of [AGENCY, OFFICER, 'FICT-REF-1', 'APP 6.5 note', 'fictional meeting', ids.bob, 'switchboard;'])
      assert.ok(!auditText.includes(secret), 'no case content: ' + secret);
    pass();
    const closedRow = audit.cases.find(c => c.id === caseId);
    assert.equal(closedRow.requests[0].separated, true); assert.ok(closedRow.holds.every(h => h.independentRelease && h.custodyIntact)); pass();
    assert.equal((await call(st.sup, LE + '/audit-export?quarter=' + quarter)).status, 403); pass();

    // A backdated case clock cannot erase provenance for recent retained operational records.
    await sql(`UPDATE restricted.le_cases SET retain_until = now() - interval '1 day' WHERE id = '${caseId}'`);
    await cli('accounts','purge');
    assert.equal(await sql(`SELECT count(*) FROM restricted.le_cases WHERE id='${caseId}'`),'1','retained linked copies keep the police provenance clock');pass();
    await cli('retention','purge','--account',ids.ana);
    await cli('accounts', 'purge');
    assert.equal(await sql(`SELECT count(*) FROM restricted.le_cases WHERE id = '${caseId}'`), '1', 'recent export/receipt/custody records retain their police provenance'); pass();
    assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE id = '${tempId}'`), '1', 'the hold record and custody chain stay'); pass();

    // The public authority page.
    const pageText = await (await fetch(web + '/law-enforcement')).text();
    assert.ok(pageText.includes('lawenforcement@swoovr.app')); assert.ok(!/24\s*\/\s*7/.test(pageText), 'no 24/7 promise'); pass();

    // The admin app in Edge.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, acceptDownloads: true });
    const page = await context.newPage();
    await page.goto(admin + '/');
    await page.fill('#username', staff.b.email); await page.fill('#password', staff.b.password); await page.click('#kc-login');
    await page.waitForSelector('[data-legal-home]', { timeout: 20000 }); pass();
    await page.goto(admin + '/law-enforcement');
    await page.waitForSelector('[data-le-overview]');
    assert.equal(await page.locator(`[data-le-case="${critical}"]`).count(), 1); pass();
    await page.fill('input[name="agency"]', AGENCY); await page.fill('input[name="officer"]', OFFICER);
    await page.selectOption('select[name="urgency"]', 'standard');
    await page.fill('textarea[name="description"]', 'Fictional request opened in the admin app');
    await page.fill('textarea[name="subjects"]', ids.cat);
    await page.click('[data-open]');
    await page.waitForSelector('[data-result="ok"] [data-opened]'); pass();
    const uiCase = await page.getAttribute('[data-opened]', 'data-opened');
    await page.goto(admin + `/law-enforcement/${uiCase}`);
    await page.waitForSelector(`[data-le-detail="${uiCase}"]`);
    assert.equal(await page.getAttribute('[data-le-timer]', 'data-le-timer'), 'response-due'); pass();
    assert.equal(await page.getAttribute('[data-closure-item="notification-decisions"]', 'data-done'), 'no'); pass();
    await page.fill('textarea[name="evidence"]', 'Fictional switchboard callback from the admin app');
    await page.click('[data-verify]');
    await page.waitForSelector('[data-result="ok"]');
    assert.equal(await page.getAttribute('[data-le-verified]', 'data-le-verified'), 'yes'); pass();
    await page.selectOption('form:has([data-respond]) select[name="kind"]', 'acknowledgement');
    await page.click('[data-respond]');
    await page.waitForSelector('[data-le-timer="update-due"]'); pass();
    if (output) await page.screenshot({ path: output + '/admin-le-case.png', fullPage: true });
    await page.goto(admin + '/law-enforcement/transparency');
    await page.waitForSelector('[data-transparency]'); pass();
    const [file] = await Promise.all([page.waitForEvent('download'), page.click('[data-audit-export]')]);
    assert.equal(JSON.parse(await require('node:fs/promises').readFile(await file.path(), 'utf8')).format, 'swoovr-le-audit/1'); pass();
    await context.close();
    // The implicated officer sees nothing of the case in the admin app.
    const excluded = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const cPage = await excluded.newPage();
    await cPage.goto(admin + '/');
    await cPage.fill('#username', staff.c.email); await cPage.fill('#password', staff.c.password); await cPage.click('#kc-login');
    await cPage.waitForSelector('[data-legal-home]', { timeout: 20000 });
    await cPage.goto(admin + `/law-enforcement/${critical}`);
    await cPage.waitForSelector(`[data-le-detail="${critical}"]`); pass(); // C isn't implicated in the critical case
    await call(st.a, `${LE}/cases/${critical}/people`, { method: 'POST', body: { accountId: sid.c, kind: 'implicated-staff' } });
    await cPage.goto(admin + `/law-enforcement/${critical}`);
    await cPage.waitForSelector('[data-le-detail="missing"]'); pass();
    await excluded.close();

    console.log(`PASS: law-enforcement cases (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
