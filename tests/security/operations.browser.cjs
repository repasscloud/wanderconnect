// Local acceptance for slice AD (internal policy §12; #89): security operations that need no host. Incidents with
// severity levels, the 30-day data-breach assessment and closing only after it; alerts with ids only to the escalation
// contacts for unusual identity openings and bulk exports; a critical incident for a broken custody chain; `keys rotate`
// re-sealing contact history, evidence, police references and the identity store to a new key version while older ones
// stay readable, and back. Negative cases: members, support, admins, unknown ids, bad bodies, unconfigured versions.
// Needs the local stack, the enrolled fictional root admin, the escalation address in .local/docker.env and hosts built
// from this checkout. Every person is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, admin, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, mailTo, elevate } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const KEY_FILE = '.local/test-rotation-keys.json';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const root = await rootAdminToken(browser);
    const names = ['s1', 's2', 's3', 's4', 's5', 's6'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('sec-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Security ' + n, homeCityId: ADELAIDE } });
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const staff = { leg: await createStaff('sec-leg'), adm: await createStaff('sec-adm'), sup: await createStaff('sec-sup') };
    for (const [n, role] of [['leg', 'legal'], ['adm', 'admin'], ['sup', 'support']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {}, sid = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/session'); sid[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st[n]).sub}'`); }
    const escalation = await localEnv('SWOOVR_ESCALATION_EMAIL');

    // Who sees incidents.
    for (const [who, token] of [['member', tokens.s1], ['support', st.sup], ['admin', st.adm]]) assert.equal((await call(token, '/api/admin/security/incidents')).status, 403, who);
    pass();
    assert.equal((await call(null, '/api/admin/security/incidents')).status, 401); pass();
    assert.equal((await call(root, '/api/admin/security/incidents')).status, 200, 'the root admin'); pass();

    // A manual incident: severity, the 30-day assessment, closing only after it.
    const incidents = '/api/admin/security/incidents';
    assert.deepEqual(code(await call(st.leg, incidents, { method: 'POST', body: { severity: 'sev9', summary: 'x' } })), [400, 'invalid-request']); pass();
    assert.deepEqual(code(await call(st.leg, incidents, { method: 'POST', body: { severity: 'sev2', summary: ' ' } })), [400, 'invalid-request']); pass();
    let r = await call(st.leg, incidents, { method: 'POST', body: { severity: 'sev2', summary: 'Fictional: a staff laptop was lost' } });
    assert.equal(r.status, 201); assert.equal(r.body.severity, 'sev2'); assert.ok(r.body.severityMeaning.startsWith('High')); pass();
    const manual = r.body.id;
    assert.ok(Math.abs(new Date(r.body.assessmentDue) - Date.now() - 30 * 864e5) < 3600e3, 'assess within 30 days'); pass();
    assert.equal((await call(st.leg, `${incidents}/${manual}/close`, { method: 'POST' })).status, 409, 'not before the assessment'); pass();
    assert.deepEqual(code(await call(st.leg, `${incidents}/${manual}/assess`, { method: 'POST', body: { reason: 'x' } })), [400, 'invalid-request']); pass();
    assert.equal((await call(st.leg, `${incidents}/${manual}/contain`, { method: 'POST' })).body.status, 'contained'); pass();
    r = await call(st.leg, `${incidents}/${manual}/assess`, { method: 'POST', body: { notifiable: false, reason: 'Fictional: the disk was encrypted; no likely serious harm' } });
    assert.equal(r.body.notifiable, false); pass();
    assert.equal((await call(st.leg, `${incidents}/${manual}/close`, { method: 'POST' })).body.status, 'closed'); pass();
    assert.equal((await call(st.leg, `${incidents}/${manual}/close`, { method: 'POST' })).status, 409); pass();
    assert.equal((await call(st.leg, `${incidents}/${crypto.randomUUID()}/contain`, { method: 'POST' })).status, 404); pass();
    assert.equal((await call(st.adm, `${incidents}/${manual}/assess`, { method: 'POST', body: { notifiable: true, reason: 'x' } })).status, 403); pass();

    // Unusual identity openings (or probing): the fourth within an hour alerts, with ids only.
    await elevate(st.leg, 'identity-open', 'Fictional review of several accounts');
    // The log entry is written as each response finishes, so pause between attempts.
    for (const n of ['s1', 's2', 's3', 's4', 's5']) { await call(st.leg, `/api/admin/accounts/${ids[n]}/identity`, { method: 'POST', body: { purpose: 'Fictional' } }); await sleep(200); }
    r = await call(st.leg, incidents);
    const unusual = r.body.find(i => i.kind === 'unusual-identity-access' && i.subjectStaffId === sid.leg);
    assert.ok(unusual); assert.equal(unusual.severity, 'sev3'); pass();
    await call(st.leg, `/api/admin/accounts/${ids.s6}/identity`, { method: 'POST', body: { purpose: 'Fictional' } });
    assert.equal((await call(st.leg, incidents)).body.filter(i => i.kind === 'unusual-identity-access' && i.subjectStaffId === sid.leg).length, 1, 'once an hour'); pass();
    if (escalation) {
      let mail = [];
      for (let i = 0; i < 20 && !mail.some(m => m.includes(unusual.id)); i++) { await sleep(250); mail = await mailTo(escalation); }
      const alert = mail.find(m => m.includes(unusual.id));
      assert.ok(alert, 'the escalation contact was emailed'); assert.ok(!Object.values(ids).some(id => alert.includes(id)), 'no member ids'); pass();
    } else console.log('note: SWOOVR_ESCALATION_EMAIL not set; email check skipped');

    // A bulk export alerts too.
    r = await call(st.leg, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional six-account hold', accountIds: names.map(n => ids[n]) } });
    const bulkHold = r.body.id;
    await elevate(st.leg, 'evidence-export', 'Fictional');
    assert.equal((await call(st.leg, `/api/admin/holds/${bulkHold}/exports`, { method: 'POST', body: { purpose: 'Fictional bulk' } })).body.code, 'bulk-export-approval-required');
    assert.ok((await call(st.leg, incidents)).body.some(i => i.kind === 'bulk-export' && i.summary.includes(bulkHold))); pass();

    // A broken custody chain is critical.
    r = await call(st.leg, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional hold to tamper with', accountIds: [ids.s6] } });
    const tamperedHold = r.body.id;
    await sql(`UPDATE custody_events SET detail = 'tampered' WHERE hold_id = '${tamperedHold}' AND action = 'placed'`);
    r = await call(st.leg, `/api/admin/holds/${tamperedHold}`);
    assert.equal(r.body.chainIntact, false); pass();
    assert.ok((await call(st.leg, incidents)).body.some(i => i.kind === 'custody-chain-broken' && i.severity === 'sev1' && i.summary.includes(tamperedHold))); pass();

    // Key rotation: every set to a new version and back; values stay readable throughout.
    let keys;
    try { keys = JSON.parse(await fs.readFile(KEY_FILE, 'utf8')); }
    catch { keys = { evidence: crypto.randomBytes(32).toString('base64'), contact: crypto.randomBytes(32).toString('base64'), identity: crypto.randomBytes(32).toString('base64') }; await fs.writeFile(KEY_FILE, JSON.stringify(keys), { mode: 0o600 }); }
    await assert.rejects(cli('keys', 'rotate', 'evidence', '--to', '2'), /isn't configured/); pass();
    await assert.rejects(cli('keys', 'rotate', 'nothing'), /Usage/); pass();
    Object.assign(process.env, { Evidence__Keys__2: keys.evidence, ContactHistory__Keys__2: keys.contact, IdentityStore__Keys__2: keys.identity });
    try {
      const status = await cli('keys', 'status');
      assert.match(status, /Keys evidence: configured \[1, 2\]/); pass();
      const out = await cli('keys', 'rotate', 'all');
      assert.match(out, /Keys evidence: \d+ re-sealed to version 2/); assert.match(out, /Keys contact-history: \d+ re-sealed to version 2/); pass();
      assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE get_byte(sealed, 0) <> 2`), '0', 'every evidence item on version 2'); pass();
      assert.equal(await sql(`SELECT count(*) FROM contact_histories WHERE get_byte(encrypted_value, 0) <> 2`), '0'); pass();
      assert.equal(await sql(`SELECT count(*) FROM restricted.verified_identities WHERE get_byte(sealed, 0) <> 2`), '0'); pass();
      assert.equal(await sql(`SELECT count(*) FROM reports WHERE police_reference IS NOT NULL AND get_byte(police_reference, 0) <> 2`), '0'); pass();
      assert.match(await cli('keys', 'rotate', 'evidence'), /0 re-sealed to version 2/, 'idempotent'); pass();
      assert.match(await cli('keys', 'status'), /Keys evidence: configured \[1, 2\]; values v2=\d+/); pass();
    } finally {
      // Back to version 1 (the hosts only know version 1), even if a check above failed.
      await cli('keys', 'rotate', 'all', '--to', '1');
      for (const k of ['Evidence__Keys__2', 'ContactHistory__Keys__2', 'IdentityStore__Keys__2']) delete process.env[k];
    }
    assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE get_byte(sealed, 0) <> 1`), '0', 'back on version 1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM contact_histories WHERE get_byte(encrypted_value, 0) <> 1`), '0'); pass();
    // Hashes are of the plain content, so an export after two re-seals still checks.
    const plainHold = (await call(st.leg, '/api/admin/holds', { method: 'POST', body: { reason: 'Fictional small hold', accountIds: [ids.s1] } })).body.id;
    r = await call(st.leg, `/api/admin/holds/${plainHold}/exports`, { method: 'POST', body: { purpose: 'Fictional check after rotation' } });
    const pkg = await (await fetch(api + `/api/admin/evidence-exports/${r.body.token}`, { headers: { Authorization: 'Bearer ' + st.leg } })).json();
    const sha = s => crypto.createHash('sha256').update(Buffer.from(s, 'utf8')).digest('hex');
    assert.ok(pkg.items.length > 0 && pkg.items.every(i => sha(i.contentJson) === i.sha256)); pass();

    // The admin app in Edge.
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();
    await page.goto(admin + '/');
    await page.fill('#username', staff.leg.email); await page.fill('#password', staff.leg.password); await page.click('#kc-login');
    await page.waitForSelector('[data-legal-home]', { timeout: 20000 });
    await page.goto(admin + '/security');
    await page.waitForSelector(`[data-incident="${unusual.id}"]`); pass();
    await page.fill('textarea[name="summary"]', 'Fictional incident from the admin app');
    await page.click('[data-open]');
    await page.waitForSelector('[data-result="ok"]'); pass();
    if (output) await page.screenshot({ path: output + '/admin-security.png', fullPage: true });
    await context.close();

    console.log(`PASS: security operations (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
