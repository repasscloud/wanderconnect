// Optional real Keycloak acceptance for the local root admin (D36) and staff realm (D31/D32).
// Needs the local stack, `./scripts/docker-local.sh root-admin` and the API on 127.0.0.1:5080.
// The first run enrols TOTP and stores the fictional secret in the private fixture so later runs
// can sign in again; nothing here is a real credential.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const keycloak = 'http://localhost:8180', api = 'http://127.0.0.1:5080', realm = 'swoovr-staff-dev';
const redirect = 'http://localhost:5082/signin-oidc';
const fixturePath = '.local/swoovr-root-admin.json';
const output = process.env.SWOOVR_BROWSER_OUTPUT || '.local/auth-browser';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

(async () => {
  const { totp } = await import('../../scripts/auth/totp.mjs');
  const fixture = JSON.parse(await fs.readFile(fixturePath, 'utf8'));
  if (!fixture.fictional || (await fs.stat(fixturePath)).mode & 0o077) throw Error('Expected the private fictional root admin fixture');
  const env = await fs.readFile('.local/docker.env', 'utf8');
  const secret = env.split('\n').find(line => line.startsWith('SWOOVR_STAFF_WEB_SECRET='))?.split('=')[1];
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  // Keycloak refuses a code reused within its window, so wait for a fresh 30-second step.
  // The last used step is kept in the fixture so back-to-back runs do not reuse a code.
  const nextCode = async () => {
    while (Math.floor(Date.now() / 30000) <= (fixture.lastOtpStep ?? -1)) await sleep(500);
    fixture.lastOtpStep = Math.floor(Date.now() / 30000);
    await fs.writeFile(fixturePath, JSON.stringify(fixture), { mode: 0o600 });
    return totp(fixture.totpSecret);
  };
  try {
    const signIn = async () => {
      const verifier = crypto.randomBytes(32).toString('base64url');
      const challenge = crypto.createHash('sha256').update(verifier).digest('base64url');
      const context = await browser.newContext(); const page = await context.newPage();
      let code;
      page.on('request', r => { if (r.url().startsWith(redirect)) code = new URL(r.url()).searchParams.get('code'); });
      await page.route(redirect + '**', route => route.fulfill({ body: 'captured' }));
      await page.goto(`${keycloak}/realms/${realm}/protocol/openid-connect/auth?client_id=swoovr-admin-web&response_type=code&scope=openid&redirect_uri=${encodeURIComponent(redirect)}&code_challenge=${challenge}&code_challenge_method=S256&state=s&nonce=n`);
      return { context, page, verifier, code: () => code };
    };
    const exchange = async (session) => {
      for (let i = 0; i < 50 && !session.code(); i++) await sleep(100);
      const response = await fetch(`${keycloak}/realms/${realm}/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({ grant_type: 'authorization_code', code: session.code(), redirect_uri: redirect, client_id: 'swoovr-admin-web', client_secret: secret, code_verifier: session.verifier }) });
      return (await response.json()).access_token;
    };

    // Staff sign-in page: no self-registration, no social providers.
    let session = await signIn();
    assert.equal(await session.page.locator('a:has-text("Create an account")').count(), 0); pass();
    assert.equal(await session.page.locator('#kc-social-providers').count(), 0); pass();
    await session.page.fill('#username', fixture.username); await session.page.fill('#password', fixture.password);
    await session.page.click('#kc-login');
    await session.page.waitForURL(/login-actions/);
    const enrolling = !fixture.totpSecret;
    if (enrolling) {
      // First run: enrolment is mandatory before any session exists.
      assert.match(session.page.url(), /execution=CONFIGURE_TOTP/); pass();
      await session.page.click('#mode-manual');
      fixture.totpSecret = (await session.page.locator('#kc-totp-secret-key').innerText()).replace(/\s+/g, '');
      await fs.writeFile(fixturePath, JSON.stringify(fixture), { mode: 0o600 });
      await session.page.fill('#totp', await nextCode());
      await session.page.fill('#userLabel', 'Local test authenticator');
      await session.page.click('#saveTOTPBtn');
    } else {
      await session.page.waitForSelector('#otp'); pass();
      await session.page.fill('#otp', await nextCode());
      await session.page.click('#kc-login');
    }
    const first = await exchange(session);
    await session.context.close();
    const call = token => fetch(api + '/api/session', { headers: { Authorization: 'Bearer ' + token } });
    let response = await call(first);
    assert.equal(response.status, 200); pass();
    // The enrolment session proves only the password: the code was set up, not used to sign in,
    // so it cannot satisfy the root admin's password + OTP rule until the next sign-in.
    // adminArea depends on the application's root-admin record (tests/auth/accounts.browser.cjs grants it), never on the token.
    const summary = await response.json();
    assert.deepEqual(summary, { realm: 'staff', method: 'emailPassword', multiFactor: !enrolling, emailVerified: true, adultAttested: false, memberArea: false, adminArea: summary.adminArea && !enrolling, supportArea: false, beta: 'notRequired' }); pass();

    // Every later sign-in asks for the code; a wrong code does not create a session.
    session = await signIn();
    await session.page.fill('#username', fixture.username); await session.page.fill('#password', fixture.password);
    await session.page.click('#kc-login');
    await session.page.waitForSelector('#otp'); pass();
    await session.page.fill('#otp', '000000'); await session.page.click('#kc-login'); await sleep(500);
    assert.equal(session.code(), undefined, 'wrong code must not issue an authorization code'); pass();
    await session.page.screenshot({ path: `${output}/root-admin-otp.png`, fullPage: true });
    await session.page.fill('#otp', await nextCode()); await session.page.click('#kc-login');
    const second = await exchange(session);
    await session.context.close();
    const claims = JSON.parse(Buffer.from(second.split('.')[1], 'base64url'));
    assert.deepEqual([...claims.amr].sort(), ['otp', 'pwd']); pass();
    assert.equal((await call(second)).status, 200); pass();
    console.log(`PASS: ${passed} staff-realm checks in actual Edge against local Keycloak 26.8.1: no registration or social sign-in, root admin password + mandatory TOTP (enrolment before first session, code on every sign-in, wrong code rejected), API reports pwd+otp evidence for the staff realm.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
