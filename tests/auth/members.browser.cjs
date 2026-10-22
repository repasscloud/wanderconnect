// Optional real Keycloak acceptance for the members realm (design D31-D41).
// Needs the local stack (./scripts/docker-local.sh up), the API on 127.0.0.1:5080 and the
// web host on http://localhost:5081 configured for Keycloak. Accounts are fictional adults
// at example.test; every email is caught by the local mail sink, nothing is delivered.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { assertSwoovrType } = require('../ui-fonts.cjs');
const web = 'http://localhost:5081', api = 'http://127.0.0.1:5080', keycloak = 'http://localhost:8180', mailSink = 'http://localhost:8025';
const realm = 'swoovr-members-dev';
const output = process.env.SWOOVR_BROWSER_OUTPUT || '.local/auth-browser';
for (const url of [web, api, keycloak, mailSink]) if (!['localhost', '127.0.0.1'].includes(new URL(url).hostname)) throw Error('Auth checks require loopback hosts');
const b64 = value => Buffer.from(value).toString('base64url');
const claims = token => JSON.parse(Buffer.from(token.split('.')[1], 'base64url'));
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

async function mailLink(to, after) {
  for (let i = 0; i < 40; i++) {
    const list = await (await fetch(mailSink + '/api/v1/messages')).json();
    const message = list.messages.find(m => m.To.some(t => t.Address === to) && Date.parse(m.Created) >= after);
    if (message) return (await (await fetch(`${mailSink}/api/v1/message/${message.ID}`)).json()).Text.match(/https?:\/\/\S+/)[0];
    await sleep(250);
  }
  throw Error('Expected email was not caught by the mail sink');
}
async function mailCount(to) {
  const list = await (await fetch(mailSink + '/api/v1/messages')).json();
  return list.messages.filter(m => m.To.some(t => t.Address === to)).length;
}
async function secret() {
  const env = await fs.readFile('.local/docker.env', 'utf8');
  return env.split('\n').find(line => line.startsWith('SWOOVR_MEMBERS_WEB_SECRET='))?.split('=')[1];
}

(async () => {
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const shot = (page, name) => page.screenshot({ path: `${output}/${name}.png`, fullPage: true });
  let passed = 0; const pass = () => passed++;
  try {
    const email = `fictional.adult.${Date.now()}@example.test`, password = 'Correct-horse-' + crypto.randomBytes(4).toString('hex');
    // Private beta (D62): the fictional address is pre-authorised before it registers.
    await require('./local-tokens.cjs').cli('beta', 'add', email);
    const context = await browser.newContext({ colorScheme: 'light', viewport: { width: 1280, height: 1000 } });
    const page = await context.newPage();

    // Launch splash (UI-STYLE.md): the first sign-in visit of a browser session plays it on our page,
    // then hands off to Keycloak; later visits in the same session go straight there.
    let started = Date.now();
    await page.goto(web + '/account/sign-in');
    assert.equal(await page.locator('#splash .word span').count(), 6); pass();
    assert.equal(await page.evaluate(() => document.documentElement.classList.contains('play')), true); pass();
    await assertSwoovrType(page, 'launch splash'); pass();
    await page.waitForURL(url => url.origin === keycloak, { timeout: 6000 });
    assert.ok(Date.now() - started >= 2000, 'the splash runs its full ~2.2 s before the hand-off'); pass();
    assert.equal((await context.cookies(web + '/account/sign-in')).some(c => c.name === 'swoovr.intro' && c.httpOnly && c.expires === -1), true, 'session-only intro cookie'); pass();
    const again = await page.request.get(web + '/account/sign-in', { maxRedirects: 0 });
    assert.equal(again.status(), 302, 'no replay later in the session'); pass();
    const calmIntro = await browser.newContext({ reducedMotion: 'reduce' }); const quiet = await calmIntro.newPage();
    started = Date.now();
    await quiet.goto(web + '/account/sign-in');
    assert.equal(await quiet.evaluate(() => document.documentElement.classList.contains('calm')), true); pass();
    await quiet.waitForURL(url => url.origin === keycloak, { timeout: 4000 });
    assert.ok(Date.now() - started < 2000, 'reduced motion: logo then a short cross-fade'); pass();
    await calmIntro.close();

    // Theme (D41): Standard only, device light/dark, claims line, no scheme picker.
    await page.goto(web + '/account/sign-in');
    await page.waitForURL(url => url.origin === keycloak);
    assert.match(await page.locator('.sw-claims').innerText(), /In development · Starting in Adelaide · Adults 18\+/); pass();
    assert.equal(await page.locator('[data-scheme], input[name=scheme]').count(), 0); pass();
    await assertSwoovrType(page, 'Keycloak sign-in'); pass();
    // The prototype's white sign-in screen is the Standard surface token (#FFFFFF light, #161616 dark).
    assert.equal(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), 'rgb(255, 255, 255)'); pass();
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.waitForFunction(() => document.documentElement.classList.contains('pf-v5-theme-dark'));
    assert.equal(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), 'rgb(22, 22, 22)'); pass();
    await assertSwoovrType(page, 'Keycloak sign-in dark'); pass();
    await shot(page, 'sign-in-dark');
    await page.emulateMedia({ colorScheme: 'light' });
    const calm = await browser.newContext({ reducedMotion: 'reduce' }); const still = await calm.newPage();
    await still.goto(web + '/account/sign-in'); await still.waitForURL(url => url.origin === keycloak);
    assert.equal(await still.evaluate(() => document.documentElement.classList.contains('sw-enter')), false, 'reduced motion: no stagger'); pass();
    await calm.close();

    // Registration (D40): blocked without the 18+ checkbox; email-first; then password.
    await page.goto(web + '/account/create');
    await page.waitForSelector('#kc-register-form');
    await assertSwoovrType(page, 'Keycloak registration'); pass();
    const entering = () => page.evaluate(() => document.documentElement.classList.contains('sw-enter'));
    assert.equal(await entering(), true, 'a new screen staggers in'); pass();
    await page.fill('#email', email); await page.fill('#firstName', 'Ana');
    // The page keeps "Create account" disabled until the box is ticked (prototype); the realm must still
    // refuse a submission that bypasses the page, so submit the form directly.
    assert.equal(await page.locator('#kc-register').isDisabled(), true); pass();
    await Promise.all([page.waitForNavigation(), page.evaluate(() => document.getElementById('kc-register-form').requestSubmit())]);
    assert.match(await page.locator('#input-error-terms-accepted').innerText(), /18 or over/); pass();
    assert.equal(await entering(), false, 'a re-render of the same screen does not animate again'); pass();
    assert.equal(await mailCount(email), 0, 'no account or email before the 18+ confirmation'); pass();
    await shot(page, 'register-blocked');
    started = Date.now();
    await page.check('#termsAccepted');
    assert.equal(await page.locator('#kc-register').isEnabled(), true); pass();
    await Promise.all([page.waitForNavigation(), page.click('#kc-register-form [type=submit]')]);
    await page.goto(await mailLink(email, started));
    await page.fill('#password-new', password); await page.fill('#password-confirm', password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('[type=submit]')]);
    assert.match(await page.locator('h1').innerText(), /You're in, Ana\./); pass();
    assert.equal(await page.locator('[data-adult-attested]').getAttribute('data-adult-attested'), 'true'); pass();
    assert.equal(await page.locator('[data-member-area]').getAttribute('data-member-area'), 'true'); pass();
    assert.equal(await page.locator('[data-session-method]').getAttribute('data-session-method'), 'unknown', 'email-first registration session has no method evidence'); pass();
    const cookies = await context.cookies(web);
    const session = cookies.find(c => c.name.startsWith('swoovr.member'));
    assert.ok(session?.httpOnly && session.sameSite === 'Lax'); pass();
    assert.ok(!cookies.some(c => /eyJ/.test(c.value)), 'no token in browser cookies'); pass();
    await shot(page, 'account-new');

    // Sign out is a POST with antiforgery; a cross-site GET does nothing.
    assert.ok([404, 405].includes((await page.request.get(web + '/account/sign-out', { maxRedirects: 0 })).status())); pass();
    await page.goto(web + '/account');
    assert.match(await page.locator('h1').innerText(), /You're in, Ana\./, 'GET must not sign out'); pass();
    await Promise.all([page.waitForURL(web + '/'), page.click('text=Sign out')]);
    await page.goto(web + '/account');
    assert.match(await page.locator('h1').innerText(), /Sign in to Swoovr\./); pass();

    // Password sign-in through the web app carries method evidence.
    await page.goto(web + '/account/sign-in');
    await page.fill('#username', email); await page.fill('#password', password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    assert.equal(await page.locator('[data-session-method]').getAttribute('data-session-method'), 'emailPassword'); pass();

    // API (D35): real token accepted; tampered, unsigned, cross-realm and missing tokens rejected.
    const verifier = crypto.randomBytes(32).toString('base64url');
    const challenge = crypto.createHash('sha256').update(verifier).digest('base64url');
    const redirect = web + '/signin-oidc';
    let code;
    const direct = await context.newPage();
    direct.on('request', r => { if (r.url().startsWith(redirect)) code = new URL(r.url()).searchParams.get('code'); });
    await direct.route(redirect + '**', route => route.fulfill({ body: 'captured' }));
    await direct.goto(`${keycloak}/realms/${realm}/protocol/openid-connect/auth?client_id=swoovr-web&response_type=code&scope=openid&redirect_uri=${encodeURIComponent(redirect)}&code_challenge=${challenge}&code_challenge_method=S256&state=t&nonce=t`);
    for (let i = 0; i < 50 && !code; i++) await sleep(100);
    const tokenResponse = await (await fetch(`${keycloak}/realms/${realm}/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({ grant_type: 'authorization_code', code, redirect_uri: redirect, client_id: 'swoovr-web', client_secret: await secret(), code_verifier: verifier }) })).json();
    await direct.close();
    const token = tokenResponse.access_token;
    const call = value => fetch(api + '/api/v1/session', { headers: value ? { Authorization: 'Bearer ' + value } : {} });
    const ok = await call(token);
    assert.equal(ok.status, 200); pass();
    const summary = await ok.json();
    assert.deepEqual(summary, { realm: 'members', method: 'emailPassword', multiFactor: false, emailVerified: true, adultAttested: true, memberArea: true, adminArea: false, supportArea: false, beta: 'enrolled' }, JSON.stringify({ summary, amr: claims(token).amr })); pass();
    const [header, payload, signature] = token.split('.');
    const forged = body => b64(JSON.stringify(body));
    const original = claims(token);
    for (const [name, value] of [
      ['missing', null],
      ['tampered method', `${header}.${forged({ ...original, amr: ['pwd', 'otp'] })}.${signature}`],
      ['moved to staff realm', `${header}.${forged({ ...original, iss: keycloak + '/realms/swoovr-staff-dev' })}.${signature}`],
      ['wrong audience', `${header}.${forged({ ...original, aud: 'account' })}.${signature}`],
      ['signature removed', `${header}.${payload}.`],
      ['alg none', `${b64(JSON.stringify({ alg: 'none', typ: 'JWT' }))}.${payload}.`],
      ['malformed', 'not.a.token'],
    ]) { assert.equal((await call(value)).status, 401, name); pass(); }

    // Lockout (D37): 3 failures lock for 15 minutes; a completed reset unlocks immediately.
    const attempt = async (value) => {
      const p = await browser.newPage();
      await p.goto(web + '/account/sign-in'); await p.fill('#username', email); await p.fill('#password', value);
      await Promise.all([p.waitForLoadState('load'), p.click('#kc-login')]); await sleep(400);
      const signedIn = p.url() === web + '/account'; await p.close(); return signedIn;
    };
    for (let i = 1; i <= 3; i++) assert.equal(await attempt('Wrong-password-' + i), false);
    assert.equal(await attempt(password), false, 'correct password refused while locked'); pass();
    const reset = await browser.newPage(); started = Date.now();
    await reset.goto(web + '/account/sign-in'); await reset.click('text=Forgot password?');
    await reset.fill('#username', email); await reset.click('[type=submit]');
    const newPassword = password + '-new';
    await reset.goto(await mailLink(email, started));
    await reset.fill('#password-new', newPassword); await reset.fill('#password-confirm', newPassword);
    await Promise.all([reset.waitForURL(url => url.origin === web), reset.click('[type=submit]')]);
    await reset.close();
    assert.equal(await attempt(newPassword), true, 'reset clears the lock immediately'); pass();

    // Passkey (D33): register with a virtual authenticator, then sign in without a password.
    const passkeyContext = await browser.newContext();
    const passkeyPage = await passkeyContext.newPage();
    const cdp = await passkeyContext.newCDPSession(passkeyPage);
    await cdp.send('WebAuthn.enable');
    // Presence is simulated, so Keycloak's passkey autofill may sign in before the button is pressed.
    const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', { options: { protocol: 'ctap2', transport: 'internal', hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true } });
    await passkeyPage.goto(web + '/account/sign-in');
    await passkeyPage.fill('#username', email); await passkeyPage.fill('#password', newPassword);
    await Promise.all([passkeyPage.waitForURL(web + '/account'), passkeyPage.click('#kc-login')]);
    await passkeyPage.route(redirect + '**', route => route.fulfill({ body: 'registered' }));
    await passkeyPage.goto(`${keycloak}/realms/${realm}/protocol/openid-connect/auth?client_id=swoovr-web&response_type=code&scope=openid&redirect_uri=${encodeURIComponent(redirect)}&code_challenge=${challenge}&code_challenge_method=S256&state=p&nonce=p&kc_action=webauthn-register-passwordless`);
    await Promise.all([passkeyPage.waitForURL(url => url.href.startsWith(redirect)), passkeyPage.click('#registerWebAuthn')]);
    const stored = (await cdp.send('WebAuthn.getCredentials', { authenticatorId })).credentials;
    assert.ok(stored.length === 1 && stored[0].isResidentCredential && stored[0].rpId === 'localhost', 'discoverable passkey stored for the local RP ID'); pass();
    await passkeyPage.unroute(redirect + '**');
    await passkeyContext.clearCookies();
    await passkeyPage.goto(web + '/account/sign-in');
    if (!passkeyPage.url().startsWith(web + '/account')) {
      await sleep(1500);
      if (passkeyPage.url().startsWith(keycloak)) await passkeyPage.click('#authenticateWebAuthnButton');
    }
    await passkeyPage.waitForURL(web + '/account');
    assert.match(await passkeyPage.locator('h1').innerText(), /You're in, Ana\./); pass();
    await shot(passkeyPage, 'account-passkey');
    await passkeyContext.close();

    // Invitation-only realms: no self-registration and no social providers (D31).
    for (const other of ['swoovr-venues-dev', 'swoovr-staff-dev']) {
      const p = await browser.newPage();
      await p.goto(`${keycloak}/realms/${other}/account`);
      assert.equal(await p.locator('a:has-text("Create an account")').count(), 0, other + ' registration link'); pass();
      assert.equal(await p.locator('#kc-social-providers').count(), 0, other + ' social providers'); pass();
      await p.close();
    }

    // 320px reflow on the account page.
    await page.setViewportSize({ width: 320, height: 800 });
    await page.goto(web + '/account');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= 320)); pass();
    await shot(page, 'account-320');
    await context.close();
    console.log(`PASS: ${passed} members-realm checks in actual Edge against local Keycloak 26.8.1: launch splash once per session (reduced-motion cross-fade) then Keycloak, prototype sign-in screens light/dark with Helvetica Neue computed on every text element and arrival stagger rules, 18+ blocking registration, email-first verification, server-side web session, password and passkey sign-in, API token validation negatives, 3-failure lockout cleared by reset, invitation-only venues/staff realms, 320px account page.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
