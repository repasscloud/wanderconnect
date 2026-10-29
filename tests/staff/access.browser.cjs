// Local acceptance for slice AC (internal policy §11, §15; procedure §5; #88): staff access controls. Time-limited elevated
// access with a recorded purpose (60 minutes, renewable, automatic expiry) for opening identities, exporting evidence and
// terminating; conflict-of-interest exclusions; bulk export approval by a second officer; the append-only, hash-chained
// sensitive-access log (views, queries, exports, refusals; never a query value or export token); revoking a staff member
// removes their roles and grants and ends their sessions. Negative cases: members, support, the wrong role, other staff's
// grants, expired and over-renewed grants, self-approval, unknown ids, tampered bodies, database edits of the log.
// Needs the local stack, the enrolled fictional root admin and hosts built from this checkout. Every person is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, admin, sleep, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;

(async () => {
  const edge = await require('../auth/managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    const names = ['x', 'y', 'b1', 'b2', 'b3', 'b4', 'b5', 'b6', 'history', 'replacement'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('acc-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Access ' + n, homeCityId: ADELAIDE } });
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'x' || n === 'y') await matchReady(tokens[n]);
    }
    const staff = { l1: await createStaff('acc-l1'), l2: await createStaff('acc-l2'), l3: await createStaff('acc-l3'), adm: await createStaff('acc-adm'), sup: await createStaff('acc-sup') };
    for (const [n, role] of [['l1', 'legal'], ['l2', 'legal'], ['l3', 'legal'], ['adm', 'admin'], ['sup', 'support']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {}, sid = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/session'); }
    for (const n of Object.keys(staff)) sid[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st[n]).sub}'`);
    const grant = (token, capability, purpose = 'Fictional purpose') => call(token, '/api/admin/access/grants', { method: 'POST', body: { capability, purpose } });
    const openIdentity = (token, id) => call(token, `/api/admin/accounts/${id}/identity`, { method: 'POST', body: { purpose: 'Fictional check' } });

    // Who may ask for what.
    assert.equal((await grant(tokens.x, 'identity-open')).status, 403, 'members never'); pass();
    assert.equal((await grant(st.sup, 'evidence-export')).status, 403, 'support never'); pass();
    assert.equal((await grant(st.l1, 'termination')).status, 403, 'legal doesn\'t terminate'); pass();
    assert.equal((await grant(st.adm, 'identity-open')).status, 403, 'admins don\'t open identities'); pass();
    assert.deepEqual(code(await grant(st.l1, 'everything')), [400, 'invalid-request']); pass();
    assert.deepEqual(code(await grant(st.l1, 'identity-open', ' ')), [400, 'purpose-required']); pass();
    assert.deepEqual(code(await grant(st.l1, 'identity-open', 'x'.repeat(501))), [400, 'purpose-required']); pass();
    const tampered = await fetch(api + '/api/admin/access/grants', { method: 'POST', headers: { Authorization: 'Bearer ' + st.l1, 'Content-Type': 'application/json' }, body: '{"capability":["identity-open"]}' });
    assert.equal(tampered.status, 400); pass();
    assert.equal((await call(null, '/api/admin/access/grants')).status, 401); pass();

    // Without access the protected actions are refused.
    assert.deepEqual(code(await openIdentity(st.l1, ids.x)), [403, 'elevated-access-required']); pass();
    // A grant: 60 minutes, purpose recorded, a new one supersedes the old.
    let r = await grant(st.l1, 'identity-open', 'Fictional police request REF-ACC-1');
    assert.equal(r.status, 201); assert.equal(r.body.active, true); pass();
    assert.ok(Math.abs(new Date(r.body.expiresAt) - Date.now() - 3600e3) < 120e3, 'sixty minutes'); pass();
    const first = r.body.id;
    r = await grant(st.l1, 'identity-open', 'Fictional second purpose');
    const second = r.body.id;
    assert.equal((await call(st.l1, '/api/admin/access/grants')).body.find(g => g.id === first).active, false, 'superseded'); pass();
    assert.deepEqual(code(await openIdentity(st.l1, ids.x)), [404, 'not-available'], 'now allowed; X has no record'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id = '${second}' AND detail LIKE '%Fictional%'`), '0', 'the purpose stays out of the audit log'); pass();
    // Renewing.
    assert.equal((await call(st.l2, `/api/admin/access/grants/${second}/renew`, { method: 'POST' })).status, 404, 'not someone else\'s'); pass();
    r = await call(st.l1, `/api/admin/access/grants/${second}/renew`, { method: 'POST' });
    assert.equal(r.status, 200); assert.equal(r.body.renewals, 1); pass();
    await sql(`UPDATE access_grants SET renewals = 8 WHERE id = '${second}'`);
    assert.deepEqual(code(await call(st.l1, `/api/admin/access/grants/${second}/renew`, { method: 'POST' })), [409, 'grant-renewal-limit']); pass();
    await sql(`UPDATE access_grants SET expires_at = now() - interval '1 minute' WHERE id = '${second}'`);
    assert.deepEqual(code(await call(st.l1, `/api/admin/access/grants/${second}/renew`, { method: 'POST' })), [409, 'grant-expired']); pass();
    assert.deepEqual(code(await openIdentity(st.l1, ids.x)), [403, 'elevated-access-required'], 'automatic expiry'); pass();
    r = await grant(st.l1, 'identity-open', 'Fictional third purpose');
    assert.equal((await call(st.l1, `/api/admin/access/grants/${r.body.id}/revoke`, { method: 'POST' })).body.active, false); pass();
    assert.deepEqual(code(await openIdentity(st.l1, ids.x)), [403, 'elevated-access-required'], 'ended early'); pass();

    // Termination needs access too.
    assert.deepEqual(code(await call(st.adm, `/api/admin/accounts/${ids.y}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'Fictional serious breach' } })), [403, 'elevated-access-required']); pass();
    await elevate(st.adm, 'termination', 'Fictional serious breach');
    assert.equal((await call(st.adm, `/api/admin/accounts/${ids.y}/sanctions`, { method: 'POST', body: { action: 'terminate', reason: 'Fictional serious breach' } })).status, 200); pass();

    // Conflicts of interest.
    assert.equal((await call(st.l1, '/api/admin/access/exclusions', { method: 'POST', body: { staffAccountId: '00000000-0000-0000-0000-000000000000', subjectAccountId: ids.x, reason: 'Fictional: a personal friend' } })).status, 200, 'a self-declaration'); pass();
    assert.equal((await call(st.sup, '/api/admin/access/exclusions', { method: 'POST', body: { staffAccountId: sid.adm, subjectAccountId: ids.x, reason: 'x' } })).status, 403, 'support can\'t exclude others'); pass();
    assert.deepEqual(code(await call(st.l2, '/api/admin/access/exclusions', { method: 'POST', body: { staffAccountId: ids.y, subjectAccountId: ids.x, reason: 'x' } })), [400, 'invalid-request'], 'a staff account'); pass();
    assert.deepEqual(code(await call(st.l2, '/api/admin/access/exclusions', { method: 'POST', body: { staffAccountId: sid.adm, subjectAccountId: ids.x, reason: '' } })), [400, 'purpose-required']); pass();
    assert.equal((await call(st.l2, '/api/admin/access/exclusions', { method: 'POST', body: { staffAccountId: sid.adm, subjectAccountId: ids.x, reason: 'Fictional: a relative' } })).status, 200, 'legal records others'); pass();
    await elevate(st.l1, 'identity-open', 'Fictional');
    assert.deepEqual(code(await openIdentity(st.l1, ids.x)), [403, 'conflict-of-interest']); pass();
    r = await call(st.l2, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional hold over X', accountIds: [ids.x] } });
    const xHold = r.body.id;
    assert.deepEqual(code(await call(st.l1, `/api/admin/holds/${xHold}`)), [403, 'conflict-of-interest']); pass();
    assert.equal((await call(st.l2, `/api/admin/holds/${xHold}`)).status, 200); pass();
    assert.deepEqual(code(await call(st.adm, `/api/admin/accounts/${ids.x}/sanctions`, { method: 'POST', body: { action: 'warn', reason: 'Fictional' } })), [403, 'conflict-of-interest']); pass();
    assert.ok((await call(st.l2, '/api/admin/access/exclusions')).body.filter(e => e.subjectAccountId === ids.x).length >= 2); pass();

    // Explicit personal associations survive final account deletion and a later link replacement.
    assert.equal((await call(root, `/api/admin/staff-links/${sid.l1}`, { method: 'PUT', body: { memberAccountId: ids.history } })).status, 204); pass();
    const historicalHold = await call(st.l2, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional historical conflict preservation', accountIds: [ids.history] } });
    assert.equal(historicalHold.status, 201); pass();
    const historyPath = `/api/admin/holds/${historicalHold.body.id}`;
    assert.deepEqual(code(await call(st.l1, historyPath)), [403, 'conflict-of-interest']); pass();
    tokens.history = await memberToken(browser, members.history);
    assert.equal((await call(tokens.history, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200); pass();
    await cli('accounts', 'purge', ids.history);
    assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id = '${ids.history}'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM staff_member_links WHERE member_account_id = '${ids.history}'`), '0'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${historicalHold.body.id}'`)) > 0); pass();
    assert.deepEqual(code(await call(st.l1, historyPath)), [403, 'conflict-of-interest'], 'retained copies remain excluded after provider deletion'); pass();
    assert.equal((await call(st.l2, historyPath)).status, 200, 'independent officer retains access'); pass();
    assert.equal((await call(root, `/api/admin/staff-links/${sid.l1}`, { method: 'PUT', body: { memberAccountId: ids.replacement } })).status, 204); pass();
    assert.deepEqual(code(await call(st.l1, historyPath)), [403, 'conflict-of-interest'], 'replacement never clears the former association'); pass();
    assert.deepEqual(code(await openIdentity(st.l1, ids.replacement)), [403, 'conflict-of-interest']); pass();
    await cli('staff', 'conflicts', 'reconcile');
    assert.deepEqual(code(await call(st.l1, historyPath)), [403, 'conflict-of-interest'], 'operator reconciliation retains the exclusion'); pass();
    assert.equal((await call(st.l3, historyPath + '/release', { method: 'POST', body: { reason: 'Fictional conflict test completed', overlapsChecked: true } })).status, 200); pass();
    assert.deepEqual(code(await call(st.l1, historyPath)), [403, 'conflict-of-interest'], 'release does not remove retained-copy conflicts'); pass();

    // Bulk exports need a second officer's approval.
    const bulk = ['b1', 'b2', 'b3', 'b4', 'b5', 'b6'].map(n => ids[n]);
    r = await call(st.l2, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional six-account hold', accountIds: bulk } });
    const bulkHold = r.body.id;
    await elevate(st.l2, 'evidence-export', 'Fictional bulk disclosure');
    r = await call(st.l2, `/api/admin/holds/${bulkHold}/exports`, { method: 'POST', body: { purpose: 'Fictional bulk disclosure' } });
    assert.equal(r.status, 409); assert.equal(r.body.code, 'bulk-export-approval-required'); assert.ok(r.body.approvalId); pass();
    const approvalId = r.body.approvalId;
    assert.deepEqual(code(await call(st.l2, `/api/admin/access/approvals/${approvalId}/approve`, { method: 'POST' })), [403, 'le-not-separated']); pass();
    assert.equal((await call(st.adm, `/api/admin/access/approvals/${approvalId}/approve`, { method: 'POST' })).status, 403, 'admins don\'t approve'); pass();
    assert.equal((await call(st.l1, `/api/admin/access/approvals/${crypto.randomUUID()}/approve`, { method: 'POST' })).status, 404); pass();
    assert.ok((await call(st.l1, '/api/admin/access/approvals')).body.some(a => a.id === approvalId)); pass();
    assert.equal((await call(st.l1, `/api/admin/access/approvals/${approvalId}/approve`, { method: 'POST' })).status, 200); pass();
    assert.equal((await call(st.l1, `/api/admin/access/approvals/${approvalId}/approve`, { method: 'POST' })).status, 409, 'once'); pass();
    r = await call(st.l2, `/api/admin/holds/${bulkHold}/exports`, { method: 'POST', body: { purpose: 'Fictional bulk disclosure', approvalId } });
    assert.equal(r.status, 201); pass();
    const exportToken = r.body.token;
    r = await call(st.l2, `/api/admin/holds/${bulkHold}/exports`, { method: 'POST', body: { purpose: 'Fictional bulk disclosure', approvalId } });
    assert.equal(r.status, 409, 'an approval is used once'); pass();
    assert.equal((await fetch(api + `/api/admin/evidence-exports/${exportToken}`, { headers: { Authorization: 'Bearer ' + st.l2 } })).status, 200); pass();

    // The sensitive-access log: refusals, views, exports, never values or tokens; append-only in the database.
    await call(st.adm, '/api/admin/contact-history?type=mobile&value=0491570159');
    assert.equal((await call(st.sup, '/api/admin/access/log')).status, 403); pass();
    r = await call(st.l2, '/api/admin/access/log?limit=1000');
    assert.equal(r.status, 200); assert.equal(r.body.intact, true); pass();
    const entries = r.body.entries;
    assert.ok(entries.some(e => e.actor === sid.l1 && e.resource === 'identity' && e.action === 'refused' && e.resourceId === ids.x), 'refused identity opening'); pass();
    assert.ok(entries.some(e => e.actor === sid.l1 && e.resource === 'holds' && e.action === 'refused' && e.resourceId === xHold), 'refused hold view'); pass();
    assert.ok(entries.some(e => e.actor === sid.l2 && e.resource === 'holds' && e.action === 'view' && e.resourceId === xHold), 'a hold view'); pass();
    assert.ok(entries.some(e => e.actor === sid.l2 && e.resource === 'evidence-export' && e.action === 'export' && e.resourceId === null), 'the download, without its token'); pass();
    assert.ok(entries.some(e => e.actor === sid.adm && e.resource === 'contact-history' && e.action === 'query'), 'the contact-history search'); pass();
    assert.equal(await sql(`SELECT count(*) FROM sensitive_access_log WHERE resource_id LIKE '%0491%' OR resource_id = '${exportToken}'`), '0', 'no values or tokens'); pass();
    for (const statement of [`UPDATE sensitive_access_log SET action = 'view' WHERE sequence = (SELECT max(sequence) FROM sensitive_access_log)`,
      'DELETE FROM sensitive_access_log WHERE sequence = (SELECT min(sequence) FROM sensitive_access_log)', 'TRUNCATE sensitive_access_log'])
      await assert.rejects(sql(statement), /append-only/);
    pass();

    // Revoking a staff member: roles and grants go, existing sessions are refused.
    assert.equal((await call(st.l1, `/api/admin/staff/${sid.adm}/revoke`, { method: 'POST', body: { reason: 'x' } })).status, 403, 'root admin only'); pass();
    assert.deepEqual(code(await call(root, `/api/admin/staff/${sid.adm}/revoke`, { method: 'POST', body: { reason: '' } })), [400, 'purpose-required']); pass();
    assert.equal((await call(root, `/api/admin/staff/${ids.x}/revoke`, { method: 'POST', body: { reason: 'x' } })).status, 404, 'not a member'); pass();
    assert.equal((await call(root, `/api/admin/staff/${crypto.randomUUID()}/revoke`, { method: 'POST', body: { reason: 'x' } })).status, 404); pass();
    assert.equal((await call(st.adm, '/api/admin/cases')).status, 200, 'working before'); pass();
    // Keep the root's ordinary last-seen save out of this ordering probe: it must reach the revocation writer.
    await sql(`UPDATE accounts SET last_seen_at=now() WHERE realm='Staff' AND subject='${claims(root).sub}'`);
    const holder=spawn('docker',['exec','-i','swoovr-local-postgres-1','psql','-U','swoovr','-d','swoovr','-v','ON_ERROR_STOP=1','-At'],{stdio:['pipe','pipe','pipe']});
    holder.stdin.on('error',()=>{});let holderOutput='',holderError='';
    holder.stdout.on('data',chunk=>{holderOutput+=chunk.toString();});holder.stderr.on('data',chunk=>{holderError+=chunk.toString().slice(0,1000);});
    const holderClosed=new Promise(resolve=>{holder.once('exit',resolve);holder.once('error',()=>resolve(-1));});
    let pending;
    try {
      holder.stdin.write("BEGIN; SELECT pg_advisory_xact_lock(1347568979,0); SELECT 'retirement-authority-held';\n");
      for(let attempt=0;attempt<100&&!holderOutput.includes('retirement-authority-held');attempt++){
        if(holder.exitCode!==null)throw new Error('Owned preservation probe exited before acquiring authority.');await sleep(20);
      }
      assert.ok(holderOutput.includes('retirement-authority-held'),'owned preservation authority is held');pass();
      let settled=false;
      pending=call(root, `/api/admin/staff/${sid.adm}/revoke`, { method: 'POST', body: { reason: 'Fictional: left the company' } });
      pending.then(()=>{settled=true;},()=>{settled=true;});
      // .NET Guid.ToByteArray uses little endian for its first three fields; lock objid is unsigned in pg_locks.
      const targetBytes=Buffer.from(sid.adm.replaceAll('-',''),'hex');targetBytes.subarray(0,4).reverse();targetBytes.subarray(4,6).reverse();targetBytes.subarray(6,8).reverse();
      const targetLock=crypto.createHash('sha256').update(targetBytes).digest().readUInt32BE(0);let waiting=false;
      for(let attempt=0;attempt<100;attempt++){
        waiting=Number(await sql(`SELECT count(*) FROM pg_locks p JOIN pg_locks a ON a.pid=p.pid
          WHERE p.locktype='advisory' AND p.database=(SELECT oid FROM pg_database WHERE datname='swoovr')
          AND p.classid=1347568979 AND p.objid=0 AND NOT p.granted
          AND a.locktype='advisory' AND a.classid=${0x41434354} AND a.objid=${targetLock} AND a.granted`))>0;
        if(waiting||settled)break;await sleep(20);
      }
      assert.ok(waiting&&!settled,'target staff lifecycle writer waits for the active provider-effect authority');pass();
      assert.ok(Number(await sql(`SELECT count(*) FROM role_grants WHERE account_id='${sid.adm}'`))>0
        &&Number(await sql(`SELECT count(*) FROM access_grants WHERE account_id='${sid.adm}' AND revoked_at IS NULL`))>0,
        'bulk roles/grants remain present until preservation authority is obtained');pass();
    } finally {
      holder.stdin.end('ROLLBACK;\n');
      if(await Promise.race([holderClosed,sleep(3000).then(()=>null)])===null){holder.kill('SIGKILL');await holderClosed;}
    }
    if(holderError)throw new Error('Owned preservation probe reported a PostgreSQL error.');
    r=await pending;
    assert.equal(r.status, 200); assert.ok(r.body.rolesRemoved >= 1 && r.body.grantsRevoked >= 1); pass();
    const firstCutoff=await sql(`SELECT extract(epoch from sessions_revoked_at) FROM accounts WHERE id='${sid.adm}'`);
    const simultaneous=await Promise.all([1,2].map(n=>call(root,`/api/admin/staff/${sid.adm}/revoke`,{method:'POST',body:{reason:'Fictional concurrent revocation '+n}})));
    assert.ok(simultaneous.every(result=>result.status===200),'independent root requests serialize revocation');pass();
    assert.ok(Number(await sql(`SELECT extract(epoch from sessions_revoked_at) FROM accounts WHERE id='${sid.adm}'`))>=Number(firstCutoff),'overlapping revocations never lower cutoff');pass();
    assert.equal((await call(st.adm, '/api/admin/cases')).status, 401, 'the session ended'); pass();
    assert.equal(await sql(`SELECT count(*) FROM role_grants WHERE account_id = '${sid.adm}'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM access_grants WHERE account_id = '${sid.adm}' AND revoked_at IS NULL`), '0'); pass();
    await sleep(31500); // Shared30s issuer-skew cutoff plus whole-second authentication evidence.
    const again = await staffToken(browser, staff.adm);
    assert.equal((await call(again, '/api/admin/cases')).status, 403, 'a new sign-in has no role'); pass();

    // The admin app in Edge: elevated access, renewing, the approvals list and the log.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();
    await page.goto(admin + '/');
    await page.fill('#username', staff.l1.email); await page.fill('#password', staff.l1.password); await page.click('#kc-login');
    await page.waitForSelector('[data-legal-home]', { timeout: 20000 });
    await page.goto(admin + '/access');
    await page.waitForSelector('[data-request-access]');
    await page.selectOption('select[name="capability"]', 'evidence-export');
    await page.fill('textarea[name="purpose"]', 'Fictional export from the admin app');
    await page.click('[data-request]');
    await page.waitForSelector('[data-result="ok"]');
    const live = page.locator('[data-grant-capability="evidence-export"][data-grant-active="yes"]');
    assert.ok(await live.count() >= 1); pass();
    await live.first().locator('[data-renew]').click();
    await page.waitForSelector('[data-result="ok"]'); pass();
    assert.equal(await page.getAttribute('[data-log-chain]', 'data-log-chain'), 'intact'); pass();
    if (output) await page.screenshot({ path: output + '/admin-access.png', fullPage: true });
    await context.close();

    console.log(`PASS: staff access controls (${passed} checks).`);
  } finally {
    await edge.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
