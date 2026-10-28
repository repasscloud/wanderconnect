// D211/D222: real local API and cookie BFF contract negotiation. Fictional adult only.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, call } = require('../auth/local-tokens.cjs');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0;
  try {
    const member = await createMember('contract');
    await cli('beta', 'add', member.email);
    const token = await memberToken(browser, member);
    assert.equal((await call(token, '/api/me')).status, 200); passed++;
    const before = (await call(token, '/api/me/profile')).body;
    for (const path of ['/api/status', '/api/me', '/api/admin/accounts']) {
      for (const [version, code] of [['bad', 'invalid-api-version'], ['2026-10-29', 'unsupported-api-version']]) {
        const response = await fetch('http://127.0.0.1:5080' + path, { headers: { 'Swoovr-Version': version } });
        assert.equal(response.status, 400); assert.equal((await response.json()).code, code); passed++;
      }
    }
    const refused = await fetch('http://127.0.0.1:5080/api/me/profile', { method: 'PUT', headers: {
      Authorization: 'Bearer ' + token, 'Swoovr-Version': '2099-01-01', 'Content-Type': 'application/json'
    }, body: JSON.stringify({ displayName: 'Must not persist', homeCityId: 2078025 }) });
    assert.equal(refused.status, 400); assert.deepEqual((await call(token, '/api/me/profile')).body, before); passed++;
    const context = await browser.newContext(); const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', member.email); await page.fill('#password', member.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    for (const version of [null, '2026-10-28']) {
      const response = await page.request.get(web + '/bff/api/me', { headers: { 'X-Swoovr-Bff': '1', ...(version ? { 'Swoovr-Version': version } : {}) } });
      assert.equal(response.status(), 200); assert.equal(response.headers()['swoovr-version'], '2026-10-28'); passed++;
    }
    for (const [version, code] of [['invalid', 'invalid-api-version'], ['2026-10-29', 'unsupported-api-version'], ['2026-10-28,2026-10-28', 'invalid-api-version']]) {
      const response = await page.request.get(web + '/bff/api/me', { headers: { 'X-Swoovr-Bff': '1', 'Swoovr-Version': version } });
      assert.equal(response.status(), 400); assert.equal((await response.json()).code, code); passed++;
    }
    for (const path of ['/bff/api/v1/me', '/bff/api/admin/accounts']) {
      assert.equal((await page.request.get(web + path, { headers: { 'X-Swoovr-Bff': '1' } })).status(), 404); passed++;
    }
    assert.equal((await page.request.get(web + '/bff/api/me')).status(), 400); passed++;
    assert.equal((await fetch('http://127.0.0.1:5080/api/v1/me')).status, 404); passed++;
    await context.close();
    console.log(`PASS: API versioning, ${passed} checks (stable paths, BFF header relay, fixed default, rejection before auth/mutation, legacy refusal, CSRF/admin boundaries).`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
