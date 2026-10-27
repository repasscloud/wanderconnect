// Local acceptance for slice X (Q45, D148, D153, D154, D174; details D181): mobile number verification with the labelled
// simulated sender (nothing is texted; the code lands in the local Mailpit sink). Codes: six digits, five minutes, five
// wrong tries, single use, a minute between sends, hourly limits per account and number, one member account per number,
// hashes only in the database, no number or code in the audit. When the hosts run with Phone__Required=true it also
// checks the registration gate (API and web). Numbers come from Australia's reserved fictional range (0491 570 006–159).
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, sleep, createMember, memberToken, cli, sql, call, claims, matchReady, mailTo } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const local = n => `0491 570 ${String(n).padStart(3, '0')}`;
const e164 = n => `+61491570${String(n).padStart(3, '0')}`;

/** The newest simulated text to a number, read from the local mail sink. */
async function latestCode(number) {
  for (let i = 0; i < 30; i++) {
    const mail = await mailTo(`sms-${number.slice(1)}@simulated.swoovr.invalid`);
    const code = mail[0]?.match(/Your Swoovr code is (\d{6})/)?.[1];
    if (code) return { code, text: mail[0], count: mail.length };
    await sleep(200);
  }
  throw new Error('no simulated text');
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    // Fictional numbers only; earlier runs' verified numbers are released first.
    await sql(`DELETE FROM member_phones WHERE number LIKE '+61491570%'`);
    await sql(`DELETE FROM phone_codes WHERE number LIKE '+61491570%'`);
    const names = ['ana', 'bob', 'cat'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('phone-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      await call(tokens[n], '/api/v1/me'); // the first call creates the account
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const gate = (await call(tokens.ana, '/api/v1/me')).body.phoneRequired === true;
    console.log(gate ? 'Gate on (Phone__Required=true): checking the registration gate too.' : 'Gate off: the registration gate is not checked in this run.');

    if (gate) {
      assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/profile')), [403, 'phone-verification-required']); pass();
      assert.deepEqual(code(await call(tokens.ana, '/api/v1/explore?mode=friends')), [403, 'phone-verification-required']); pass();
      for (const path of ['/api/v1/me', '/api/v1/me/phone', '/api/v1/me/notices', '/api/v1/me/deletion'])
      { assert.equal((await call(tokens.ana, path)).status, 200, path + ' stays open'); pass(); }
      assert.equal((await call(tokens.ana, '/api/v1/me/phone')).body.required, true); pass();
    }

    // The phone view and refusals.
    let r = await call(tokens.ana, '/api/v1/me/phone');
    assert.deepEqual([r.status, r.body.verified, r.body.sender, r.body.number], [200, false, 'simulated', null]); pass();
    for (const bad of ['12345', '0312345678', '+0491570100', 'call me', ''])
    { assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone', { method: 'POST', body: { number: bad } })), [400, 'invalid-phone'], bad); pass(); }
    assert.equal((await call(null, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } })).status, 401); pass();

    // Send, refuse wrong codes and the cooldown, then confirm.
    r = await call(tokens.ana, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } });
    assert.deepEqual([r.status, r.body.pendingNumber], [200, '+61••••••100']); pass();
    const first = await latestCode(e164(100));
    assert.match(first.text, /^SIMULATED: no text message was sent/, 'labelled simulated'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } })), [429, 'phone-code-cooldown']); pass();
    const wrong = first.code === '000000' ? '111111' : '000000';
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: wrong } })), [400, 'phone-code-wrong']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: '12ab56' } })), [400, 'phone-code-wrong']); pass();
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: first.code } })), [400, 'phone-code-expired'], "Ana's code is no use to Bob"); pass();
    r = await call(tokens.ana, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: '+61 491 570 100', code: first.code } });
    assert.deepEqual([r.status, r.body.verified, r.body.number, r.body.required], [200, true, '+61••••••100', false]); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: first.code } })), [400, 'phone-code-expired'], 'single use'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } })), [409, 'phone-already-verified']); pass();
    assert.equal((await call(tokens.ana, '/api/v1/me')).body.phoneVerified, true); pass();
    if (gate) { assert.notEqual((await call(tokens.ana, '/api/v1/me/profile')).body?.code, 'phone-verification-required', 'the gate opens'); pass(); }

    // What's stored: hashes only; the audit has ids and outcomes, never the number or the code.
    assert.equal(await sql(`SELECT number FROM member_phones WHERE account_id = '${ids.ana}'`), e164(100)); pass();
    assert.equal(await sql(`SELECT octet_length(code_hash) || ':' || octet_length(ip_hash) FROM phone_codes WHERE account_id = '${ids.ana}' LIMIT 1`), '32:32'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE actor_account_id = '${ids.ana}' AND action LIKE 'phone.%' AND (coalesce(detail,'') LIKE '%491570%' OR coalesce(detail,'') LIKE '%${first.code}%')`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM member_profiles WHERE account_id = '${ids.ana}'`) <= '1', true); pass();

    // One member account per number (D181); the refusal doesn't say whose.
    r = await call(tokens.bob, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } });
    assert.equal(r.status, 200); pass();
    const bobCode = (await latestCode(e164(100))).code;
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: bobCode } })), [409, 'phone-in-use']); pass();

    // D184: Ana changes number; verifying the new one releases the old one for someone else, and the log says "changed".
    await call(tokens.ana, '/api/v1/me/phone', { method: 'POST', body: { number: local(103) } });
    r = await call(tokens.ana, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(103), code: (await latestCode(e164(103))).code } });
    assert.deepEqual([r.status, r.body.number], [200, '+61••••••103']); pass();
    assert.equal(await sql(`SELECT detail FROM audit_entries WHERE actor_account_id = '${ids.ana}' AND action = 'phone.verified' ORDER BY id DESC LIMIT 1`), 'changed:simulated'); pass();
    await sleep(61000); // the minute between codes to the same number, for Bob
    await call(tokens.bob, '/api/v1/me/phone', { method: 'POST', body: { number: local(100) } });
    r = await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(100), code: (await latestCode(e164(100))).code } });
    assert.deepEqual([r.status, r.body.verified], [200, true], 'the released number can verify another account'); pass();

    // Expiry and five wrong tries.
    await call(tokens.bob, '/api/v1/me/phone', { method: 'POST', body: { number: local(101) } });
    const expiring = (await latestCode(e164(101))).code;
    await sql(`UPDATE phone_codes SET expires_at = now() - interval '1 second' WHERE account_id = '${ids.bob}' AND number = '${e164(101)}'`);
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(101), code: expiring } })), [400, 'phone-code-expired']); pass();
    await call(tokens.bob, '/api/v1/me/phone', { method: 'POST', body: { number: local(102) } });
    const right = (await latestCode(e164(102))).code;
    const nope = right === '000000' ? '111111' : '000000';
    for (let i = 1; i <= 4; i++) assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(102), code: nope } })), [400, 'phone-code-wrong']);
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(102), code: nope } })), [429, 'too-many-attempts']); pass();
    assert.deepEqual(code(await call(tokens.bob, '/api/v1/me/phone/confirm', { method: 'POST', body: { number: local(102), code: right } })), [429, 'too-many-attempts'], 'even the right code'); pass();

    // Hourly limit per account (seeded in SQL, as other suites do for limits).
    await sql(`INSERT INTO phone_codes (id, account_id, number, code_hash, ip_hash, created_at, expires_at, failed_attempts) SELECT gen_random_uuid(), '${ids.cat}', '+6149157020' || g, '\\x00', '\\x00', now() - interval '10 minutes', now() - interval '5 minutes', 0 FROM generate_series(0, 4) g`);
    assert.deepEqual(code(await call(tokens.cat, '/api/v1/me/phone', { method: 'POST', body: { number: local(110) } })), [429, 'phone-code-limit']); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE actor_account_id = '${ids.cat}' AND action = 'phone.code-limited'`), '1'); pass();

    // The web panel in Edge (through the BFF).
    await sql(`DELETE FROM phone_codes WHERE account_id = '${ids.cat}'`);
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.cat.email); await page.fill('#password', members.cat.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    if (gate) {
      await page.goto(web + '/connections');
      await page.waitForURL(/\/account\?phone=required/); pass();
    }
    await page.waitForSelector('[data-phone=none]', { timeout: 20000 });
    assert.equal(await page.locator('[data-phone-simulated]').count(), 1, 'test mode is said plainly'); pass();
    await page.fill('#phone-number', local(120));
    await page.click('[data-phone-send] button');
    await page.waitForSelector('[data-phone=pending]'); pass();
    await page.fill('#phone-code', (await latestCode(e164(120))).code);
    await page.click('[data-phone-confirm] button');
    await page.waitForSelector('[data-phone=verified]'); pass();
    assert.equal(await page.locator('[data-phone-number]').getAttribute('data-phone-number'), '+61••••••120'); pass();
    if (gate) {
      await page.goto(web + '/connections');
      assert.equal(new URL(page.url()).pathname, '/connections', 'the app opens after verifying'); pass();
    }
    if (process.env.SWOOVR_SCREENSHOTS) await page.screenshot({ path: process.env.SWOOVR_SCREENSHOTS + '/phone-390.png', fullPage: true });
    await context.close();

    console.log(`PASS: mobile number verification (${passed} checks${gate ? ', gate on' : ''}).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
