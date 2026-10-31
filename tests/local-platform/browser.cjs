// Optional actual-browser acceptance against the managed Docker stack.
// Needs installed Edge and Playwright; neither is required by the launcher.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { managedEdge } = require('../auth/managed-edge.cjs');
(async () => {
  const { parseEnv } = await import('../../scripts/local/platform.mjs');
  const { totp } = await import('../../scripts/auth/totp.mjs');
  const env = parseEnv(await fs.readFile('.local/platform/docker.env', 'utf8'));
  const fixture = JSON.parse(await fs.readFile('.local/platform/admin.json', 'utf8'));
  const edge = await managedEdge(chromium);
  try {
    const staff = await edge.browser.newContext();
    const page = await staff.newPage();
    await page.goto('http://localhost:5082/');
    await page.fill('#username', env.SWOOVR_LOCAL_ADMIN_EMAIL);
    await page.fill('#password', env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await page.click('#kc-login');
    await page.waitForSelector('#otp');
    while (Math.floor(Date.now()/30000) <= (fixture.lastOtpStep ?? -1)) await new Promise(r => setTimeout(r, 500));
    fixture.lastOtpStep = Math.floor(Date.now()/30000);
    await fs.writeFile('.local/platform/admin.json', JSON.stringify(fixture, null, 2) + '\n', { mode: 0o600 });
    await page.fill('#otp', totp(fixture.totpSecret));
    await page.click('#kc-login');
    await page.waitForURL('http://localhost:5082/', { timeout: 30000 });
    assert.equal(await page.locator('[data-dashboard]').count(), 1, 'admin API data loaded');
    assert.equal(await page.locator('[data-refused]').count(), 0);
    assert.equal((await staff.cookies()).some(c => c.name === 'swoovr.admin' && c.httpOnly), true);
    await staff.close();
    const members = await edge.browser.newContext();
    const member = await members.newPage();
    await member.goto('http://localhost:5081/account/sign-in?intro=done');
    await member.waitForSelector('#username');
    await member.fill('#username', 'fictional.alex@example.test');
    await member.fill('#password', env.SWOOVR_LOCAL_MEMBER_PASSWORD);
    await member.click('#kc-login');
    await member.waitForLoadState();
    if (await member.locator('[name=accept]').count()) await member.locator('[name=accept]').click();
    await member.waitForURL(url => url.origin === 'http://localhost:5081', { timeout: 30000 });
    await member.goto('http://localhost:5081/account');
    assert.equal((await members.cookies()).some(c => c.name === 'swoovr.member' && c.httpOnly), true);
    assert.match(await member.locator('body').innerText(), /account|mobile|profile/i);
    const forwarded = await members.request.get('http://localhost:5081/bff/api/session', { headers: { 'X-Swoovr-Bff': '1' } });
    assert.equal(forwarded.status(), 200);
    const summary = await forwarded.json();
    assert.equal(summary.realm, 'members');
    assert.equal(summary.emailVerified, true);
    assert.equal(summary.adultAttested, true);
    await members.close();
    console.log('PASS: actual Edge member and staff OIDC callbacks, HttpOnly portal sessions and staff password+OTP sign-in against Docker hosts.');
  } finally { await edge.close(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
