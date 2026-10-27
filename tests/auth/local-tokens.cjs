// Shared helpers for local acceptance tests: fictional members created through the local
// Keycloak admin API, real tokens from the authorization-code + PKCE flow in actual Edge,
// and the API's operator commands. Local development only; nothing here is a real credential.
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const { execFile } = require('node:child_process');

const keycloak = 'http://localhost:8180', api = 'http://127.0.0.1:5080', web = 'http://localhost:5081', admin = 'http://localhost:5082';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const claims = token => JSON.parse(Buffer.from(token.split('.')[1], 'base64url'));

async function localEnv(key) {
  const env = await fs.readFile('.local/docker.env', 'utf8');
  return env.split('\n').find(line => line.startsWith(key + '='))?.split('=')[1];
}

/** A fictional adult member with a verified email; the 18+ attestation is accepted at first sign-in. */
async function createMember(label) {
  const { localRealmApi } = await import('../../scripts/auth/provision.mjs');
  const realmApi = await localRealmApi({ realm: 'swoovr-members-dev' });
  const email = `fictional.${label}.${Date.now()}.${crypto.randomBytes(3).toString('hex')}@example.test`;
  const password = 'Correct-horse-' + crypto.randomBytes(6).toString('hex');
  await realmApi('/users', 'POST', {
    username: email, email, firstName: 'Fictional', enabled: true,
    // Simulated for the local sandbox only; never evidence of real email ownership.
    emailVerified: true,
    credentials: [{ type: 'password', value: password, temporary: false }],
  });
  return { email, password };
}

/**
 * A fictional staff-realm person (local staff realm only, D136) with a verified email and a password. A support or
 * admin role still needs an invitation from an admin; this only creates the Keycloak user.
 */
async function createStaff(label) {
  const { localRealmApi } = await import('../../scripts/auth/provision.mjs');
  const realmApi = await localRealmApi({ realm: 'swoovr-staff-dev' });
  const email = `fictional.staff.${label}.${Date.now()}.${crypto.randomBytes(3).toString('hex')}@example.test`;
  const password = 'Correct-horse-' + crypto.randomBytes(6).toString('hex');
  await realmApi('/users', 'POST', { username: email, email, firstName: 'Fictional', lastName: 'Staff', enabled: true, emailVerified: true,
    credentials: [{ type: 'password', value: password, temporary: false }] });
  return { email, password };
}

/** Signs in through the real Keycloak page and exchanges the code for an access token. */
async function codeToken(browser, { realm, clientId, secretKey, redirect, fill }) {
  const verifier = crypto.randomBytes(32).toString('base64url');
  const challenge = crypto.createHash('sha256').update(verifier).digest('base64url');
  const context = await browser.newContext();
  const page = await context.newPage();
  let code;
  page.on('request', r => { if (r.url().startsWith(redirect)) code = new URL(r.url()).searchParams.get('code'); });
  await page.route(redirect + '**', route => route.fulfill({ body: 'captured' }));
  await page.goto(`${keycloak}/realms/${realm}/protocol/openid-connect/auth?client_id=${clientId}&response_type=code&scope=openid&redirect_uri=${encodeURIComponent(redirect)}&code_challenge=${challenge}&code_challenge_method=S256&state=s&nonce=n`);
  await fill(page);
  for (let i = 0; i < 100 && !code; i++) await sleep(100);
  await context.close();
  if (!code) throw new Error('No authorization code for ' + realm);
  const response = await fetch(`${keycloak}/realms/${realm}/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({
    grant_type: 'authorization_code', code, redirect_uri: redirect, client_id: clientId, client_secret: await localEnv(secretKey), code_verifier: verifier }) });
  const token = (await response.json()).access_token;
  if (!token) throw new Error('Token exchange failed for ' + realm);
  return token;
}

const memberToken = (browser, member) => codeToken(browser, {
  realm: 'swoovr-members-dev', clientId: 'swoovr-web', secretKey: 'SWOOVR_MEMBERS_WEB_SECRET', redirect: web + '/signin-oidc',
  fill: async page => {
    await page.fill('#username', member.email); await page.fill('#password', member.password); await page.click('#kc-login');
    // Admin-created users still get the realm's 18+ terms action on first sign-in; accepting it records the attestation.
    await page.waitForLoadState();
    if (page.url().includes('execution=TERMS_AND_CONDITIONS')) await page.click('[name=accept]');
  },
});

/**
 * A member token through the app's own client (swoovr-mobile: public, code + PKCE, com.swoovr.app redirect), the token
 * the MAUI app sends as its bearer. The redirect never loads (the scheme is the app's), so the code is read from
 * Keycloak's Location header.
 */
async function appToken(browser, member) {
  const realm = `${keycloak}/realms/swoovr-members-dev`, redirect = 'com.swoovr.app:/oauth2redirect';
  const verifier = crypto.randomBytes(32).toString('base64url');
  const challenge = crypto.createHash('sha256').update(verifier).digest('base64url');
  const context = await browser.newContext();
  const page = await context.newPage();
  let code;
  page.on('response', r => { const l = r.headers()['location']; if (l?.startsWith('com.swoovr.app:')) code = new URL(l.replace('com.swoovr.app:', 'https://app.invalid')).searchParams.get('code'); });
  await page.goto(`${realm}/protocol/openid-connect/auth?` + new URLSearchParams({ client_id: 'swoovr-mobile', response_type: 'code', scope: 'openid profile email',
    redirect_uri: redirect, state: 's', nonce: 'n', code_challenge: challenge, code_challenge_method: 'S256' })).catch(() => {});
  if (!code && await page.$('#username')) {
    await page.fill('#username', member.email); await page.fill('#password', member.password);
    await page.click('#kc-login').catch(() => {});
    await page.waitForLoadState().catch(() => {});
    if (page.url().includes('execution=TERMS_AND_CONDITIONS')) await page.click('[name=accept]').catch(() => {});
  }
  for (let i = 0; i < 100 && !code; i++) await sleep(100);
  await context.close();
  if (!code) throw new Error('No app authorization code');
  const response = await fetch(`${realm}/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({
    grant_type: 'authorization_code', code, redirect_uri: redirect, client_id: 'swoovr-mobile', code_verifier: verifier }) });
  const token = (await response.json()).access_token;
  if (!token) throw new Error('App token exchange failed');
  return token;
}

/** A staff-realm token through the admin app's client (password only; OTP is optional for staff other than the root admin). */
const staffToken = (browser, staff) => codeToken(browser, {
  realm: 'swoovr-staff-dev', clientId: 'swoovr-admin-web', secretKey: 'SWOOVR_STAFF_WEB_SECRET', redirect: admin + '/signin-oidc',
  fill: async page => { await page.fill('#username', staff.email); await page.fill('#password', staff.password); await page.click('#kc-login'); },
});

/** The local root admin (password + OTP). Needs `./scripts/docker-local.sh root-admin` and an enrolled TOTP. */
async function rootAdminToken(browser) {
  const { totp } = await import('../../scripts/auth/totp.mjs');
  const fixturePath = '.local/swoovr-root-admin.json';
  const fixture = JSON.parse(await fs.readFile(fixturePath, 'utf8'));
  if (!fixture.fictional || !fixture.totpSecret) throw new Error('Run tests/auth/root-admin.browser.cjs once to enrol the fictional root admin');
  return codeToken(browser, {
    realm: 'swoovr-staff-dev', clientId: 'swoovr-admin-web', secretKey: 'SWOOVR_STAFF_WEB_SECRET', redirect: admin + '/signin-oidc',
    fill: async page => fillRootAdmin(page, fixture, fixturePath, totp),
  });
}

/** Fills the staff realm's password and OTP pages for the fictional root admin (also used to sign in to the admin app). */
async function fillRootAdmin(page, fixture, fixturePath, totp) {
  if (!fixture) {
    ({ totp } = await import('../../scripts/auth/totp.mjs'));
    fixturePath = '.local/swoovr-root-admin.json';
    fixture = JSON.parse(await fs.readFile(fixturePath, 'utf8'));
  }
  await page.fill('#username', fixture.username); await page.fill('#password', fixture.password); await page.click('#kc-login');
  await page.waitForSelector('#otp');
  // Keycloak refuses a code reused within its window, so wait for a fresh 30-second step.
  while (Math.floor(Date.now() / 30000) <= (fixture.lastOtpStep ?? -1)) await sleep(500);
  fixture.lastOtpStep = Math.floor(Date.now() / 30000);
  await fs.writeFile(fixturePath, JSON.stringify(fixture), { mode: 0o600 });
  await page.fill('#otp', totp(fixture.totpSecret)); await page.click('#kc-login');
}

/**
 * Completes what discovery needs since slice R (D138, D139): a fictional date of birth and, for Dating, a gender with a
 * "show me" that includes everyone ("prefer not to say" is never a show-me value, D161), so suites written before R keep
 * matching. Pass options to vary them.
 */
async function matchReady(token, { dateOfBirth = '1990-01-01', gender = 'woman', showMe = ['man', 'woman', 'non-binary', 'another'] } = {}) {
  const age = await call(token, '/api/v1/me/age', { method: 'PUT', body: { dateOfBirth } });
  const identity = await call(token, '/api/v1/me/identity', { method: 'PUT', body: { gender, showGender: true, showOrientation: false, showAge: true, datingShowMe: showMe } });
  return [age.status, identity.status];
}

/** Runs an API operator command with the local database connection. */
async function cli(...args) {
  const connection = `Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`;
  const dll = 'src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll';
  // `accounts purge` deletes Keycloak users through the API's deletion client (D169) and Didit sessions (D173), and keys
  // terminated-email holds from the founder secret as the hosts do (D171).
  const env = { ...process.env, ConnectionStrings__Swoovr: connection, DOTNET_ENVIRONMENT: 'Development',
    Founders__Secrets__1: process.env.Founders__Secrets__1 ?? await localEnv('SWOOVR_FOUNDER_SECRET_V1') ?? '',
    MemberAdmin__ClientSecret: await localEnv('SWOOVR_MEMBERS_API_ADMIN_SECRET') ?? '',
    Didit__ApiKey: await localEnv('SWOOVR_DIDIT_API_KEY') ?? '', Didit__WorkflowId: await localEnv('SWOOVR_DIDIT_WORKFLOW_ID') ?? '' };
  return new Promise((resolve, reject) => execFile('dotnet', [dll, ...args], { env,
  }, (error, stdout, stderr) => error ? reject(new Error(stderr || error.message)) : resolve(stdout)));
}

/** Runs SQL in the local database container and returns trimmed rows. */
function sql(query) {
  return new Promise((resolve, reject) => execFile('docker', ['exec', 'swoovr-local-postgres-1', 'psql', '-U', 'swoovr', '-d', 'swoovr', '-tAc', query],
    (error, stdout, stderr) => error ? reject(new Error(stderr)) : resolve(stdout.trim())));
}

/** Calls the API with a bearer token. */
async function call(token, path, { method = 'GET', body } = {}) {
  const response = await fetch(api + path, { method, headers: {
    ...(token ? { Authorization: 'Bearer ' + token } : {}), ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null };
}

/** The local members realm's admin API (management only, never sign-in evidence). */
async function membersRealm() {
  const { localRealmApi } = await import('../../scripts/auth/provision.mjs');
  return localRealmApi({ realm: 'swoovr-members-dev' });
}

/** Text bodies of the local mail sink's messages to one address, newest first. */
async function mailTo(address) {
  const list = await (await fetch('http://127.0.0.1:8025/api/v1/search?query=' + encodeURIComponent(`to:"${address}"`))).json();
  return Promise.all((list.messages ?? []).map(async m => (await (await fetch(`http://127.0.0.1:8025/api/v1/message/${m.ID}`)).json()).Text));
}

module.exports = { keycloak, api, web, admin, sleep, claims, localEnv, createMember, createStaff, memberToken, appToken, staffToken, rootAdminToken, fillRootAdmin,
  cli, sql, call, membersRealm, mailTo, matchReady };
