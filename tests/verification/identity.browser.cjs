// Local acceptance for slice AA (policy §2, §4, §8; owner Q78, D191; #87): the verified identity store and contact-change
// security. An approved ID check (a signed, fictional Didit webhook) keeps the minimum fields sealed in the restricted
// schema; staff see only "on file"; the `legal` role opens one record for a recorded purpose; the member's export has
// their own record; an ordinary deletion erases it, a terminated member's is kept two years and a later identical
// identity is flagged (never blocked); a held account's record is preserved as evidence. #87: a terminated member's
// number is blocked by the typed registry when it returns; changing number needs a fresh sign-in, tells the old number (never the new
// one) and ends the account's other sessions; an email change tells the old address; the admin contact-history screen.
// Needs the local stack, the enrolled fictional root admin, Didit settings in .local/docker.env and hosts built from this
// checkout started with Phone__Required=false and Accounts__ReauthSeconds=45. Every person and document is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, admin, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, membersRealm, mailTo, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const REAUTH_SECONDS = Number(process.env.SWOOVR_REAUTH_SECONDS ?? 45);
const canonical = v => Array.isArray(v) ? `[${v.map(canonical).join(',')}]`
  : v && typeof v === 'object' ? `{${Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canonical(v[k])).join(',')}}` : JSON.stringify(v);
// ACMA's fictional mobile numbers (0491 570 156–159), released from earlier runs' fictional accounts first.
const local = n => `0491 570 ${n}`, e164 = n => `+61491570${n}`;

async function latestText(number, pattern) {
  for (let i = 0; i < 40; i++) {
    const mail = await mailTo(`sms-${number.slice(1)}@simulated.swoovr.invalid`);
    const hit = mail.find(m => pattern.test(m));
    if (hit) return hit;
    await sleep(250);
  }
  return null;
}

(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const secret = await localEnv('SWOOVR_DIDIT_WEBHOOK_SECRET'), workflow = await localEnv('SWOOVR_DIDIT_WORKFLOW_ID');
    assert.ok(secret && workflow, 'Didit settings missing from .local/docker.env');
    const hook = async payload => {
      const response = await fetch(api + '/api/webhooks/didit', { method: 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json',
        'X-Timestamp': String(Math.floor(Date.now() / 1000)), 'X-Signature-V2': crypto.createHmac('sha256', Buffer.from(secret, 'utf8')).update(canonical(payload)).digest('hex') } });
      return response.status;
    };
    await sql(`DELETE FROM member_phones WHERE number IN ('${e164(156)}', '${e164(157)}', '${e164(158)}'); DELETE FROM phone_codes WHERE number IN ('${e164(156)}', '${e164(157)}', '${e164(158)}')`);
    // Legacy hash-only markers have no source provenance. Never erase unrelated rows as fixture cleanup.
    // Current termination uses the typed registry; recycle only positively matched fictional numbers through its reviewed API.
    const root = await rootAdminToken(browser);
    // Reserved fictional number is deliberately recycled between suite runs through the reviewed API, not SQL history deletion.
    await elevate(root,'identifier-release','Fictional reserved-number fixture review');
    const oldNumbers=await call(root,'/api/admin/banned-identifiers/lookup',{method:'POST',body:{type:'mobile',value:e164(158),purpose:'Review reserved fictional number from previous acceptance runs'}});
    assert.equal(oldNumbers.status,200);
    const oldIds=oldNumbers.body.filter(b=>b.status==='active').map(b=>b.id);
    if(oldIds.length)assert.equal((await call(root,'/api/admin/banned-identifiers/release',{method:'POST',body:{identifierIds:oldIds,reason:'Reserved fictional test number recycled for another acceptance run; original account sanctions remain.'}})).status,200);

    const names = ['ana', 'ter', 'twin', 'ret', 'chg', 'eml', 'held'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('idn-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Idn ' + n, homeCityId: ADELAIDE } });
      await matchReady(tokens[n]);
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    // An approved, fictional document check for a member (a signed webhook, as Didit sends it).
    const approve = async (n, person) => {
      const session = crypto.randomUUID();
      await sql(`INSERT INTO id_verification_attempts (id, account_id, session_id, workflow_id, state, provider_status, started, self_service, created_at, updated_at)
        VALUES ('${crypto.randomUUID()}', '${ids[n]}', '${session}', '${workflow}', 'InProgress', 'In Progress', true, true, now(), now())`);
      const status = await hook({ event_id: crypto.randomUUID(), session_id: session, status: 'Approved', webhook_type: 'status.updated', created_at: Math.floor(Date.now() / 1000),
        timestamp: Math.floor(Date.now() / 1000), workflow_id: workflow, vendor_data: ids[n],
        decision: { id_verifications: [{ node_id: 'feature_ocr', status: 'Approved', age: 36, document_type: 'Passport', document_number: 'FICT-DOC-' + n,
          address: '1 Fictional Street', issuing_state: 'AUS', expiration_date: '2032-06-30', verification_method: 'document', ...person }] } });
      assert.equal(status, 200, n);
      return session;
    };
    const ANA = { first_name: 'Fictional', last_name: 'Annabel', date_of_birth: '1990-01-01' };
    const TERRA = { first_name: 'Fictiönal', last_name: 'Terra-Example', date_of_birth: '1990-01-01' };

    // An approval keeps the minimum fields, sealed, in the restricted schema; never the document number or address.
    await approve('ana', ANA);
    assert.equal(await sql(`SELECT issuing_country || '/' || decision || '/' || (match_hmac IS NOT NULL)::text FROM restricted.verified_identities WHERE account_id = '${ids.ana}'`), 'AUS/Approved/true'); pass();
    assert.equal(await sql(`SELECT count(*) FROM restricted.verified_identities WHERE account_id = '${ids.ana}' AND (position(convert_to('Annabel', 'UTF8') in sealed) > 0 OR position(convert_to('FICT-DOC', 'UTF8') in sealed) > 0)`), '0', 'sealed'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%Annabel%' OR detail LIKE '%FICT-DOC%' OR detail LIKE '%1990-01-01%'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'identity.recorded' AND target_id = '${ids.ana}'`), '1'); pass();

    // Staff see only that it's on file; only `legal` opens it, for a recorded purpose.
    const staff = { sup: await createStaff('idn-sup'), adm: await createStaff('idn-adm'), leg: await createStaff('idn-leg') };
    for (const [n, role] of [['sup', 'support'], ['adm', 'admin'], ['leg', 'legal']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) st[n] = await staffToken(browser, staff[n]);
    let r = await call(st.adm, `/api/admin/accounts/${ids.ana}/summary`);
    assert.equal(r.status, 200); assert.ok(r.body.identityOnFileSince); assert.equal(r.body.identityIssuingCountry, 'AUS'); pass();
    assert.ok(!JSON.stringify(r.body).includes('Annabel')); pass();
    const open = (token, id, purpose) => call(token, `/api/admin/accounts/${id}/identity`, { method: 'POST', body: { purpose } });
    // #88: opening needs time-limited access with a recorded purpose.
    if (!(await call(st.leg, '/api/admin/access/grants')).body.some(g => g.active && g.capability === 'identity-open'))
      assert.deepEqual(code(await open(st.leg, ids.ana, 'Fictional check')), [403, 'elevated-access-required']);
    pass();
    await elevate(st.leg, 'identity-open', 'Fictional police request REF-IDN-1');
    for (const [who, token] of [['admin', st.adm], ['support', st.sup], ['member', tokens.ana], ['root admin without the role', root]])
      assert.equal((await open(token, ids.ana, 'x')).status, 403, who);
    pass();
    assert.equal((await open(null, ids.ana, 'x')).status, 401); pass();
    assert.deepEqual(code(await open(st.leg, ids.ana, ' ')), [400, 'purpose-required']); pass();
    assert.deepEqual(code(await open(st.leg, ids.ana, 'x'.repeat(501))), [400, 'purpose-required']); pass();
    assert.deepEqual(code(await open(st.leg, crypto.randomUUID(), 'Fictional check')), [404, 'not-available']); pass();
    assert.deepEqual(code(await open(st.leg, ids.chg, 'Fictional check')), [404, 'not-available'], 'no record on file'); pass();
    r = await open(st.leg, ids.ana, 'Fictional police request REF-IDN-1');
    assert.equal(r.status, 200);
    assert.deepEqual([r.body.firstName, r.body.lastName, r.body.dateOfBirth, r.body.documentType, r.body.issuingCountry, r.body.expiryDate],
      ['Fictional', 'Annabel', '1990-01-01', 'Passport', 'AUS', '2032-06-30']); pass();
    assert.ok(!JSON.stringify(r.body).includes('FICT-DOC') && !JSON.stringify(r.body).includes('Fictional Street'), 'never the number or address'); pass();
    assert.equal(await sql(`SELECT purpose FROM restricted.identity_accesses WHERE account_id = '${ids.ana}' ORDER BY id DESC LIMIT 1`), 'Fictional police request REF-IDN-1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'identity.decrypted' AND target_id = '${ids.ana}' AND detail IS NULL`), '1'); pass();

    // The member's own export includes their record.
    tokens.ana = await memberToken(browser, members.ana);
    r = await call(tokens.ana, '/api/me/export', { method: 'POST' });
    assert.equal(r.status, 200);
    const exported = await call(tokens.ana, '/' + r.body.path);
    assert.equal(exported.status, 200); assert.equal(exported.body.verifiedIdentity.lastName, 'Annabel'); pass();

    // A legal hold preserves the record as evidence.
    await approve('held', { first_name: 'Fictional', last_name: 'Heldon', date_of_birth: '1990-01-01' });
    r = await call(st.leg, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional hold for the identity test', accountIds: [ids.held] } });
    assert.equal(r.status, 201);
    assert.ok((await call(st.leg, `/api/admin/holds/${r.body.id}`)).body.items.some(i => i.kind === 'identity-record')); pass();

    // An ordinary deletion erases the record.
    assert.equal((await call(tokens.ana, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    // A terminated member: identity and number kept for ban enforcement (2 years), flagged when they return.
    await approve('ter', TERRA);
    tokens.ter = await memberToken(browser, members.ter);
    assert.equal((await call(tokens.ter, '/api/me/phone', { method: 'POST', body: { number: local(158) } })).status, 200);
    const terCode = (await latestText(e164(158), /Your Swoovr code is \d{6}/)).match(/(\d{6})/)[1];
    assert.equal((await call(tokens.ter, '/api/me/phone/confirm', { method: 'POST', body: { number: local(158), code: terCode } })).status, 200);
    await elevate(root, 'termination', 'Fictional termination for the identity suite');
    assert.equal((await call(root, `/api/admin/accounts/${ids.ter}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'Fictional termination', hideFromDiscovery: false, noNewRequests: false, noMessages: false } })).status, 200);
    tokens.ter = await memberToken(browser, members.ter);
    assert.equal((await call(tokens.ter, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    assert.match(await cli('accounts', 'purge'), /0 waiting for Keycloak/);
    assert.equal(await sql(`SELECT count(*) FROM restricted.verified_identities WHERE account_id = '${ids.ana}'`), '0', 'ordinary deletion erased it'); pass();
    assert.equal(await sql(`SELECT round(extract(epoch from retain_until - now()) / 86400) FROM restricted.verified_identities WHERE account_id = '${ids.ter}'`), '730', 'terminated: kept two years'); pass();
    assert.equal(await sql(`SELECT count(*) FROM banned_identifiers b JOIN moderation_cases c ON b.case_id=c.id WHERE c.subject_account_id='${ids.ter}' AND b.identifier_type='Mobile' AND b.status='Active' AND b.ban_expires_at IS NULL`),'1','typed mobile restriction survives deletion');pass();
    // The same identity (written differently) verifying another account is flagged, not blocked.
    await approve('twin', { ...TERRA, first_name: 'FICTIONAL', last_name: 'terra example' });
    assert.equal(await sql(`SELECT system_flag || '/' || queue FROM moderation_cases WHERE subject_account_id = '${ids.twin}'`), 'returning-terminated-identity/Admin'); pass();
    assert.equal((await call(tokens.twin, '/api/me')).status, 200, 'never blocked'); pass();
    // D217: attaching a restricted mobile is refused without banning the legitimate account.
    assert.deepEqual(code(await call(tokens.ret, '/api/me/phone', { method: 'POST', body: { number: local(158) } })),[403,'identifier-restricted']);pass();
    assert.equal(await sql(`SELECT count(*) FROM member_phones WHERE account_id='${ids.ret}'`),'0','refused attachment changes no contact');pass();
    assert.equal((await call(tokens.ret,'/api/me')).status,200,'otherwise legitimate account remains usable');pass();

    // #87: changing number needs a fresh sign-in, tells the old number and ends the account's other sessions.
    assert.equal((await call(tokens.chg, '/api/me/phone', { method: 'POST', body: { number: local(156) } })).status, 200, 'the first number needs no fresh sign-in');
    const firstCode = (await latestText(e164(156), /Your Swoovr code is \d{6}/)).match(/(\d{6})/)[1];
    assert.equal((await call(tokens.chg, '/api/me/phone/confirm', { method: 'POST', body: { number: local(156), code: firstCode } })).status, 200);
    await memberToken(browser, members.chg); // a second, separate Keycloak session (another device)
    const realm = await membersRealm();
    const subject = claims(tokens.chg).sub;
    assert.ok((await realm(`/users/${subject}/sessions`)).length >= 2); pass();
    await sleep(Math.max(0, (claims(tokens.chg).auth_time + REAUTH_SECONDS + 2) * 1000 - Date.now())); // the sign-in goes stale
    assert.deepEqual(code(await call(tokens.chg, '/api/me/phone', { method: 'POST', body: { number: local(157) } })), [403, 'stale-session']); pass();
    tokens.chg = await memberToken(browser, members.chg);
    assert.equal((await call(tokens.chg, '/api/me/phone', { method: 'POST', body: { number: local(157) } })).status, 200); pass();
    const newCode = (await latestText(e164(157), /Your Swoovr code is \d{6}/)).match(/(\d{6})/)[1];
    r = await call(tokens.chg, '/api/me/phone/confirm', { method: 'POST', body: { number: local(157), code: newCode } });
    assert.equal(r.status, 200); pass();
    const notice = await latestText(e164(156), /number on your Swoovr account was just changed/);
    assert.ok(notice && !notice.includes('157') && !notice.includes('0491'), 'the old number hears, without the new one'); pass();
    const sessions = await realm(`/users/${subject}/sessions`);
    assert.deepEqual(sessions.map(s => s.id), [claims(tokens.chg).sid], 'only the session that made the change remains'); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE action = 'phone.change-secured' AND target_id = '${ids.chg}'`), 'sessions-ended'); pass();

    // #87: an email change tells the old address, never the new one.
    const oldEmail = members.eml.email, newEmail = oldEmail.replace('fictional.', 'fictional.changed.');
    const emlSubject = claims(tokens.eml).sub;
    const user = await realm(`/users/${emlSubject}`);
    await realm(`/users/${emlSubject}`, 'PUT', { ...user, email: newEmail, username: user.username, emailVerified: true });
    members.eml.email = newEmail;
    tokens.eml = await memberToken(browser, members.eml); // the realm uses the email as the username
    assert.equal((await call(tokens.eml, '/api/me')).status, 200);
    let old = []; for (let i = 0; i < 20 && !old.some(m => m.includes('was just changed')); i++) { old = await mailTo(oldEmail); await sleep(250); }
    assert.ok(old.some(m => m.includes('was just changed') && !m.includes(newEmail) && !m.includes('changed.')), 'the old address hears, without the new one'); pass();

    // Admin app: the contact-history screen (admins) and the identity page (legal) in Edge.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();
    await page.goto(admin + '/');
    await page.fill('#username', staff.adm.email); await page.fill('#password', staff.adm.password); await page.click('#kc-login');
    await page.waitForSelector('[data-dashboard]', { timeout: 20000 });
    await page.goto(admin + '/contact-history');
    await page.selectOption('select[name="type"]', 'mobile'); await page.fill('input[name="value"]', local(156));
    await page.click('[data-contact-search]');
    await page.waitForSelector('[data-contact-previous]');
    assert.ok((await page.textContent('[data-contact-previous]')).includes(ids.chg)); pass();
    assert.ok(!(await page.content()).includes('491570156'), 'never the value'); pass();
    await page.goto(admin + `/accounts/${ids.twin}`);
    await page.waitForSelector('[data-identity-on-file="on-file"]', { timeout: 20000 }); pass();
    await context.close();
    const legalContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const legalPage = await legalContext.newPage();
    await legalPage.goto(admin + '/');
    await legalPage.fill('#username', staff.leg.email); await legalPage.fill('#password', staff.leg.password); await legalPage.click('#kc-login');
    await legalPage.waitForSelector('[data-legal-home]', { timeout: 20000 });
    await legalPage.goto(admin + `/identity?account=${ids.twin}`);
    await legalPage.fill('textarea[name="purpose"]', 'Fictional ban-enforcement review of a flagged case');
    await legalPage.click('[data-identity-open]');
    await legalPage.waitForSelector('[data-identity-name]');
    assert.ok((await legalPage.textContent('[data-identity-name]')).includes('terra example')); pass();
    await legalContext.close();

    console.log(`PASS: verified identity store and contact-change security (${passed} checks).`);
  } finally {
    await edge.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
