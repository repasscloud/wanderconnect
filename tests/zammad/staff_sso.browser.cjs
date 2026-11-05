// Direct Zammad web access for staff (owner decision Q133/#204, 5 November 2026), against the running local platform,
// real local Keycloak (staff realm) and real local Zammad 7.2.2:
//   - the only way in is the central staff sign-in; no password form or reset;
//   - a Trust & Safety member lands in their own web identity, mirrored from Swoovr: ordinary queues and T&S work,
//     hand-off only into Trust & Safety — Restricted (worked through Swoovr Admin with elevation), nothing else;
//   - a restricted ticket stays invisible on the web even though Swoovr Admin may open it with elevation;
//   - recording a conflict of interest in Swoovr switches the web identity off and ends the live web session; clearing it
//     brings access back; a Keycloak staff user Swoovr doesn't manage can't get in at all.
// Fictional fixtures only. Passwords are read in-process from .local/platform/docker.env and never printed.
// Usage: SWOOVR_PLAYWRIGHT_PATH=/path/to/node_modules/playwright node tests/zammad/staff_sso.browser.cjs
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), { execFileSync } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const root = path.resolve(__dirname, '../..');
const env = Object.fromEntries(fs.readFileSync(path.join(root, '.local/platform/docker.env'), 'utf8').split('\n')
  .filter(l => l.includes('=') && !l.startsWith('#')).map(l => { const i = l.indexOf('='); return [l.slice(0, i), l.slice(i + 1).replace(/^["']|["']$/g, '')]; }));
const zammad = 'http://localhost:8095', kc = 'http://localhost:8180', realm = 'swoovr-staff-dev';
const shots = path.join(root, '.local/zammad/staff-sso'); fs.mkdirSync(shots, { recursive: true, mode: 0o700 });
let checks = 0; const check = (ok, label) => { if (!ok) throw Object.assign(Error(label), { safe: true }); checks++; console.log('PASS: ' + label); };
const sql = q => execFileSync('docker', ['exec', 'swoovr-platform-postgres-1', 'psql', '-p', '5433', '-U', 'swoovr', '-d', 'swoovr', '-At', '-v', 'ON_ERROR_STOP=1', '-c', q], { encoding: 'utf8' }).trim();
const rails = code => JSON.parse(execFileSync('docker', ['exec', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'runner', code], { encoding: 'utf8' }).split('\n').filter(l => l.startsWith('SWOOVR=')).pop().slice(7));
const sleep = ms => new Promise(r => setTimeout(r, ms));
async function until(probe, label, ms = 150000) { const start = Date.now(); while (Date.now() - start < ms) { if (await probe()) return; await sleep(3000); } throw Object.assign(Error('timed out: ' + label), { safe: true }); }

async function keycloak(pathname, method = 'GET', body) {
  const container = JSON.parse(execFileSync('docker', ['inspect', 'swoovr-platform-keycloak-1'], { encoding: 'utf8' }))[0];
  const kv = Object.fromEntries(container.Config.Env.map(e => [e.slice(0, e.indexOf('=')), e.slice(e.indexOf('=') + 1)]));
  const token = (await (await fetch(kc + '/realms/master/protocol/openid-connect/token', { method: 'POST', body: new URLSearchParams({ grant_type: 'password', client_id: 'admin-cli', username: kv.KC_BOOTSTRAP_ADMIN_USERNAME, password: kv.KC_BOOTSTRAP_ADMIN_PASSWORD }) })).json()).access_token;
  const r = await fetch(kc + '/admin/realms/' + realm + pathname, { method, headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) });
  if (!r.ok) throw Object.assign(Error('Keycloak admin ' + method + ' failed ' + r.status), { safe: true });
  return r.status === 204 || r.status === 201 ? null : r.json();
}

async function zammadSignIn(page, email, password) {
  await page.goto(zammad + '/#login');
  const button = page.locator('form[action*="openid_connect"] button, a[href*="openid_connect"]').first();
  await button.waitFor({ timeout: 60000 });
  if (!zammadSignIn.seen) {
    zammadSignIn.seen = true;
    await page.screenshot({ path: path.join(shots, 'zammad-login.png') });
    check(await page.locator('input[name="password"]').count() === 0, 'Zammad shows no password form: the central staff sign-in is the only way in');
    check(await page.locator('a[href*="password_reset"]').count() === 0, 'Zammad offers no password reset');
  }
  await Promise.all([page.waitForURL(u => u.toString().startsWith(kc), { timeout: 30000 }), button.click()]);
  await page.waitForSelector('#username', { timeout: 30000 });
  await page.fill('#username', email); await page.fill('#password', password);
  await Promise.all([page.waitForURL(u => u.toString().startsWith(zammad), { timeout: 60000 }), page.click('#kc-login')]);
}
const me = page => page.evaluate(async () => { const r = await fetch('/api/v1/users/me?expand=true', { credentials: 'same-origin' }); return { status: r.status, body: r.ok ? await r.json() : null }; });

(async () => {
  const browser = await chromium.launch(process.env.SWOOVR_BROWSER_CHANNEL ? { channel: process.env.SWOOVR_BROWSER_CHANNEL } : {});
  const trustEmail = 'fictional.trust@example.test';
  const staffId = sql(`SELECT id FROM accounts WHERE realm='Staff' AND email='${trustEmail}'`);
  const memberId = sql("SELECT id FROM accounts WHERE realm='Members' AND email='fictional.alex@example.test'");
  const run = crypto.randomBytes(4).toString('hex'); let stranger = null, restricted = null, excluded = false;
  try {
    const groups = rails('puts "SWOOVR=" + Group.pluck(:id, :name).to_h.to_json');
    // A fictional restricted request that Swoovr Admin could open with elevation, never the web sign-in.
    restricted = rails(`UserInfo.current_user_id = 1; t = Transaction.execute { Ticket.create!(title: 'Fictional restricted check ${run}', group: Group.find_by!(name: 'Trust & Safety — Restricted'), customer_id: User.where(active: true).joins(:roles).where(roles: {name: 'Customer'}).first.id, state: Ticket::State.find_by!(name: 'new'), priority: Ticket::Priority.find_by!(name: '2 normal')) }; puts "SWOOVR=" + t.id.to_s`);

    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } }); const page = await context.newPage();
    await zammadSignIn(page, trustEmail, env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await until(async () => (await me(page)).status === 200, 'Zammad session after central sign-in', 60000);
    const user = (await me(page)).body;
    await page.screenshot({ path: path.join(shots, 'zammad-signed-in.png') });
    check(user.login === 'swoovr-staff-web-' + staffId && user.roles.length === 1 && user.roles[0] === 'Agent', 'central sign-in lands in the Swoovr-managed web identity (Agent role only)');
    const named = Object.fromEntries(Object.entries(user.group_ids).map(([id, access]) => [groups[id], access.join(',')]));
    check(named['Trust & Safety'] === 'full' && named['Appeals'] === 'full' && named['General Support'] === 'full', 'Trust & Safety works its own queue, Appeals and the ordinary queues on the web');
    check(named['Trust & Safety — Restricted'] === 'create' && named['Legal & Law Enforcement'] === 'create', 'Restricted and Legal are hand-off only on the web (no read)');
    check(!named['Privacy & Data Protection'] && !named['Security'] && !named['Internal Operations'], 'no Privacy, Security or Internal Operations on the web');
    const peek = await page.evaluate(async id => (await fetch('/api/v1/tickets/' + id, { credentials: 'same-origin' })).status, restricted);
    check(peek === 403 || peek === 404, 'a restricted ticket is refused to the web sign-in (Swoovr Admin with elevation is the only way in)');
    const tokens = await page.evaluate(async () => { const r = await fetch('/api/v1/user_access_token', { credentials: 'same-origin' }); return r.ok ? (await r.json()).tokens.length : -1; });
    check(tokens <= 0, 'the web identity holds no API tokens');

    // Swoovr records a conflict of interest: the web sign-in is switched off and the live session ended.
    sql(`INSERT INTO staff_exclusions(staff_account_id,subject_account_id,reason,created_by,created_at) VALUES('${staffId}','${memberId}','Fictional SSO acceptance conflict ${run}','${staffId}',now())`); excluded = true;
    await until(async () => [401, 403].includes((await me(page)).status), 'web session ended after the conflict was recorded');
    check(sql(`SELECT count(*) FROM audit_entries WHERE action='ticketing.staff-sessions-ended' AND target_id='${staffId}' AND at > now() - interval '10 minutes'`) !== '0',
      'a conflict recorded in Swoovr switches off the web identity and ends its live Zammad session (audited)');
    const context2 = await browser.newContext(); const again = await context2.newPage();
    await zammadSignIn(again, trustEmail, env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await sleep(3000);
    check((await me(again)).status !== 200, 'signing in again is refused while the conflict stands');
    await context2.close();

    sql(`DELETE FROM staff_exclusions WHERE staff_account_id='${staffId}' AND reason='Fictional SSO acceptance conflict ${run}'`); excluded = false;
    await until(() => sql(`SELECT web_active FROM ticketing_staff WHERE account_id='${staffId}'`) === 't', 'web identity restored');
    const context3 = await browser.newContext(); const back = await context3.newPage();
    await zammadSignIn(back, trustEmail, env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await until(async () => (await me(back)).status === 200, 'session after the conflict was cleared', 60000);
    check(true, 'clearing the conflict in Swoovr restores web access');
    await context3.close();

    // A staff-realm person Swoovr doesn't manage gets no Zammad account at all.
    const email = `fictional.unmanaged.${run}@example.test`, password = 'Fictional-' + crypto.randomUUID();
    await keycloak('/users', 'POST', { username: email, email, firstName: 'Fictional', lastName: 'Adult', emailVerified: true, enabled: true, credentials: [{ type: 'password', value: password, temporary: false }] });
    stranger = (await keycloak('/users?exact=true&username=' + encodeURIComponent(email)))[0].id;
    const context4 = await browser.newContext(); const outsider = await context4.newPage();
    await zammadSignIn(outsider, email, password);
    await sleep(3000);
    check((await me(outsider)).status !== 200 && rails(`puts "SWOOVR=" + User.where(email: '${email}').count.to_s`) === 0, 'an unmanaged staff-realm identity is refused and no Zammad user is created');
    await context4.close();
    console.log(`PASS: ${checks} direct Zammad access checks (screenshots in private .local/zammad/staff-sso).`);
  } catch (error) {
    console.error('FAIL: ' + (error.safe ? error.message : error.name));
    process.exitCode = 1;
  } finally {
    if (excluded) try { sql(`DELETE FROM staff_exclusions WHERE staff_account_id='${staffId}' AND reason LIKE 'Fictional SSO acceptance conflict%'`); } catch {}
    if (stranger) try { await keycloak('/users/' + stranger, 'DELETE'); } catch {}
    if (restricted) try { rails(`Ticket.find(${restricted}).destroy!; puts "SWOOVR=1"`); } catch {}
    await browser.close();
  }
})();
