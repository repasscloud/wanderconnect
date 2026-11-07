// Issue 185 (D341): Security & access → Active sessions and Linked sign-ins against the managed local platform
// (./scripts/local-platform.sh up). Fictional member Alex on three browser contexts. A disabled fictional Google identity
// provider and link are created only when absent and removed again. Takes about five minutes: it waits for a signed-out
// device's web session to end when its access token next refreshes. Usage: node tests/auth/sign-ins.browser.cjs [screenshot-dir]
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const repo = path.resolve(__dirname, '../..');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { managedEdge } = require('./managed-edge.cjs');
const env = Object.fromEntries(fs.readFileSync(process.env.SWOOVR_PLATFORM_ENV || path.join(repo, '.local/platform/docker.env'), 'utf8').split('\n').filter(l => /^[A-Z_]+=/.test(l))
  .map(l => [l.slice(0, l.indexOf('=')), l.slice(l.indexOf('=') + 1).replace(/^'(.*)'$/, '$1')]));
const web = 'http://localhost:5081', kc = 'http://localhost:8180', realm = 'swoovr-members-dev';
const email = 'fictional.alex@example.test', out = process.argv[2] || path.join(repo, '.local/auth-browser');
fs.mkdirSync(out, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
let passes = 0; const pass = name => { passes++; console.log('PASS', name); };

async function admin(path, method = 'GET', body) {
  const t = await (await fetch(`${kc}/realms/master/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({
    grant_type: 'password', client_id: 'admin-cli', username: 'local-bootstrap', password: env.KC_BOOTSTRAP_ADMIN_PASSWORD }) })).json();
  const r = await fetch(`${kc}/admin/realms/${realm}${path}`, { method, headers: { authorization: 'Bearer ' + t.access_token, 'content-type': 'application/json' }, body: body && JSON.stringify(body) });
  if (!r.ok && r.status !== 404 && r.status !== 409) throw Error(`${method} ${path} -> ${r.status}`);
  return r.status === 200 ? r.json() : r.status;
}
async function signIn(context) {
  const page = await context.newPage();
  await page.goto(web + '/account/sign-in');
  await page.fill('#username', email); await page.fill('#password', env.SWOOVR_LOCAL_MEMBER_PASSWORD);
  await Promise.all([page.waitForURL(u => u.href.startsWith(web) && !u.href.includes('signin-oidc')), page.click('#kc-login')]);
  return page;
}
const ready = page => page.waitForSelector('[data-sign-ins=ready]', { timeout: 30000 });

(async () => {
  const [user] = await admin('/users?exact=true&email=' + encodeURIComponent(email));
  assert(user?.id, 'fictional Alex exists');
  await admin(`/users/${user.id}/logout`, 'POST');
  let createdIdp = false;
  const edge = await managedEdge(chromium), browser = edge.browser;
  try {
    const deviceA = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const deviceB = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const deviceC = await browser.newContext();
    const a = await signIn(deviceA), b = await signIn(deviceB), c = await signIn(deviceC);
    await a.goto(web + '/account#security'); await ready(a);
    let rows = a.locator('[data-session]');
    assert.equal(await rows.count(), 3, 'three sessions listed'); pass('three real Keycloak sessions listed');
    assert.equal(await a.locator('[data-session-current=true]').count(), 1); pass('exactly one marked This device');
    assert.equal(await a.locator('[data-session-current=true] [data-session-end]').count(), 0); pass('no Sign out on this device');
    assert.equal(await a.locator('[data-session-device=web]').count(), 3); pass('device label from client (web)');
    assert.match(await a.locator('[data-session-current=true]').innerText(), /Web browser\s*This device[\s\S]*Signed in .* · Active now/); pass('row wording');
    assert.equal(await a.locator('[data-linked-none]').count(), 1); pass('no linked sign-ins shown when none');
    await a.screenshot({ path: out + '/185-desktop-sessions.png', fullPage: true });

    // Per-device sign-out (device C is the last-active other session; find it by checking it disappears).
    const before = (await admin(`/users/${user.id}/sessions`)).map(s => s.id);
    await a.locator('[data-session-end]').first().click();
    await a.waitForSelector('[data-sign-ins-result=ended]');
    assert.equal(await a.locator('[data-session]').count(), 2); pass('one other session signed out in the list');
    const after = (await admin(`/users/${user.id}/sessions`)).map(s => s.id);
    assert.equal(after.length, 2); assert.ok(after.every(id => before.includes(id))); pass('Keycloak really ended exactly one session');

    // Phone-width rendering of device B, then sign out of all other devices from A.
    await b.goto(web + '/account#security'); await ready(b);
    await b.locator('#security').screenshot({ path: out + '/185-phone-security.png' });
    await a.click('[data-sessions-end-others]');
    await a.waitForSelector('[data-sign-ins-result=ended-others]');
    assert.equal(await a.locator('[data-session]').count(), 1); pass('only this device left in the list');
    assert.equal((await admin(`/users/${user.id}/sessions`)).length, 1); pass('Keycloak keeps only this session');
    await a.locator('#security').screenshot({ path: out + '/185-desktop-after-others.png' });

    // Linked sign-in: a disabled fictional Google provider and a fictional link (removed again in finally).
    if ((await admin('/identity-provider/instances/google')) === 404) {
      await admin('/identity-provider/instances', 'POST', { alias: 'google', providerId: 'google', enabled: false, config: { clientId: 'fictional-client', clientSecret: 'fictional-secret' } });
      createdIdp = true;
    }
    await admin(`/users/${user.id}/federated-identity/google`, 'POST', { identityProvider: 'google', userId: 'fictional-google-user', userName: email });
    await a.reload(); await ready(a);
    assert.equal(await a.locator('[data-linked-provider=google]').count(), 1); pass('linked Google listed');
    assert.match(await a.locator('[data-linked-provider=google]').innerText(), /^Google/); pass('provider label only, no provider user name');
    assert.doesNotMatch(await a.locator('#security').innerText(), /fictional-google-user/); pass('provider user id never shown');
    await a.click('[data-linked-remove]');
    assert.equal(await a.locator('[data-linked-confirm]').count(), 1); pass('remove asks to confirm');
    await a.locator('#security').screenshot({ path: out + '/185-desktop-linked-confirm.png' });
    await a.click('[data-linked-remove-confirm]');
    await a.waitForSelector('[data-sign-ins-result=removed]');
    assert.equal(await a.locator('[data-linked-provider]').count(), 0); pass('linked sign-in removed in the list');
    assert.equal((await admin(`/users/${user.id}/federated-identity`)).length, 0); pass('Keycloak link really removed');

    // Device B: its Keycloak session is gone; its next token refresh fails and the web session ends (<= 5 minutes).
    const started = Date.now();
    for (;;) {
      await b.goto(web + '/account');
      if (/\/account\/sign-in|realms\/swoovr-members-dev/.test(b.url()) || await b.locator('a[href*="/account/sign-in"]').count() > 0
        && await b.locator('[data-account-panel]').count() === 0) break;
      if (Date.now() - started > 6 * 60_000) throw Error('device B still signed in after 6 minutes');
      await sleep(15000);
    }
    pass(`device B signed out on the web after ${Math.round((Date.now() - started) / 1000)} s`);
  } finally {
    await admin(`/users/${user.id}/federated-identity/google`, 'DELETE').catch(() => {});
    if (createdIdp) await admin('/identity-provider/instances/google', 'DELETE').catch(e => console.error('cleanup', e.message));
    await admin(`/users/${user.id}/logout`, 'POST').catch(() => {});
    await edge.close();
  }
  console.log(`ALL PASS (${passes})`);
})().catch(e => { console.error('FAIL', e); process.exit(1); });
