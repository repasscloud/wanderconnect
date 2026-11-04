// Issue 190 / issue 191 (D322, D323): account detail end to end against the managed local platform
// (./scripts/local-platform.sh up). Signs in as the fictional root admin (password + TOTP), the fictional Trust & Safety
// and Support staff, and the managed fictional member alex; opens the date of birth with a purpose; locks alex and checks
// that the live member session is refused by the API and shown the locked screen with the same reference; removes the
// lock; grants Plus, applies Photo Verified and warns the test-created fictional account; checks role limits in the UI.
// Run: NODE_PATH=<dir with playwright> node tests/admin/account-detail.platform.cjs   (channel msedge)
// The test-created account id below is local data; set SWOOVR_TEST_ACCOUNT to another fictional member if needed.
const { chromium } = require('playwright');
const path = require('path');
const repo = path.resolve(__dirname, '../..');
const { execSync } = require('child_process');
const fs = require('fs'), assert = require('assert');
const env = Object.fromEntries(fs.readFileSync(process.env.SWOOVR_PLATFORM_ENV ?? path.join(repo, '.local/platform/docker.env'),'utf8').split('\n').filter(l=>l.includes('=')).map(l=>{const i=l.indexOf('=');return [l.slice(0,i), l.slice(i+1).replace(/^["']|["']$/g,'')];}));
const sam = 'efb6e17f-8b8c-4e2c-8891-536da9dfe8f0', test = process.env.SWOOVR_TEST_ACCOUNT ?? '31e8d61a-4de6-42a2-8a7c-9479f7af80b8', A = 'http://localhost:5082';
let n = 0; const ok = (c, m) => { assert.ok(c, m); n++; console.log('PASS', m); };
async function staff(b, email, otp) {
  const p = await b.newPage({ viewport: { width: 1440, height: 1000 } });
  p.on('response', r => { if (r.status() === 404) console.log('404', r.url()); });
  await p.goto(A + '/accounts/' + sam);
  await p.fill('#username', email); await p.fill('#password', env.SWOOVR_LOCAL_ADMIN_PASSWORD); await p.click('[type=submit]');
  await p.waitForLoadState('networkidle');
  for (let i = 0; otp && i < 3 && await p.locator('#otp').count(); i++) {
    if (i) await p.waitForTimeout(31000);
    const code = execSync('./scripts/local-platform.sh admin-code 2>/dev/null', { cwd: repo }).toString().trim().split('\n').pop();
    await p.fill('#otp', code); await p.click('[type=submit]'); await p.waitForLoadState('networkidle'); }
  if (!p.url().startsWith(A)) throw Error('sign-in did not complete for ' + email + ': ' + p.url());
  return p;
}
async function dialog(p, open, fill) { await p.click(`[data-dialog-open="${open}"]`); const d = p.locator(`dialog#${open}`); await d.waitFor(); if (fill) await fill(d); await Promise.all([p.waitForLoadState('load'), d.locator('button[type=submit]').click()]); await p.waitForLoadState('networkidle'); }
async function confirm(p) { const d = p.locator('dialog.ad-dialog[open]'); await d.waitFor(); await Promise.all([p.waitForNavigation(), d.locator('button[value=ok]').click()]); await p.waitForLoadState('networkidle'); }
(async () => {
  const b = await chromium.launch({ channel: 'msedge' });
  const admin = await staff(b, env.SWOOVR_LOCAL_ADMIN_EMAIL, true);
  // Overview: date of birth (issue 190 §2)
  await admin.goto(`${A}/accounts/${sam}`);
  await dialog(admin, 'dob-open', async d => { await d.locator('textarea[name=detail]').fill('Fictional support check'); });
  ok(await admin.locator('[data-profile-dob]').count() === 1, 'date of birth opens after purpose (' + (await admin.locator('[data-result]').getAttribute('data-result')) + ')');
  // Access: sign the managed fictional member in, lock them, they see the locked screen, unlock (issue 190 §10)
  const m = await b.newPage();
  await m.goto('http://localhost:5081/account/sign-in?intro=done'); await m.waitForLoadState('networkidle');
  await m.fill('#username', 'fictional.alex@example.test'); await m.fill('#password', env.SWOOVR_LOCAL_MEMBER_PASSWORD);
  await Promise.all([m.waitForNavigation(), m.click('#kc-login')]); await m.waitForLoadState('networkidle');
  for (let i = 0; i < 3 && m.url().includes('8180'); i++) { const accept = m.locator('input[name=accept], button:has-text("Accept"), #kc-accept'); if (await accept.count()) { await Promise.all([m.waitForNavigation(), accept.first().click()]); await m.waitForLoadState('networkidle'); } else break; }
  ok(m.url().startsWith('http://localhost:5081'), 'member signed in: ' + m.url().slice(0, 50));
  const pw = env.SWOOVR_DB_PASSWORD;
  const alex = execSync(`docker exec -e PGPASSWORD='${pw}' swoovr-platform-postgres-1 psql -h 127.0.0.1 -p 5433 -U swoovr -d swoovr -tAc "select id from accounts where email='fictional.alex@example.test'"`).toString().trim();
  ok(/^[0-9a-f-]{36}$/.test(alex), 'member account ' + alex);
  await admin.goto(`${A}/accounts/${alex}/access`);
  if (await admin.locator('[data-lock-state=locked]').count()) await dialog(admin, 'unlock', async d => { await d.locator('select[name=reasonCode]').selectOption('applied-in-error'); });
  await admin.selectOption('form:has([data-op=lock]) select[name=reasonCode]', 'trust-safety-investigation');
  await admin.click('[data-op=lock]'); await confirm(admin);
  ok(await admin.locator('[data-lock-state=locked]').count() === 1, 'sign-in locked');
  const ref = (await admin.locator('[data-lock-reference]').textContent()).trim();
  ok(/^SW-[A-Z2-9]{6}$/.test(ref), 'public reference ' + ref);
  ok((await admin.locator('[data-account-state=locked]').count()) === 1, 'header shows Sign-in locked');
  const api = await m.request.fetch('http://localhost:5081/bff/api/me/profile', { headers: { 'X-Swoovr-Bff': '1' } });
  const body = await api.json().catch(() => ({}));
  ok(api.status() === 403 && body.code === 'account-locked' && body.reference === ref, 'existing session refused by the API: ' + api.status() + ' ' + body.code);
  await m.goto('http://localhost:5081/me'); await m.waitForLoadState('networkidle');
  ok(m.url().includes('/account/restricted?state=locked'), 'member redirected to locked screen: ' + m.url().slice(21));
  ok((await m.locator('[data-locked-reference]').getAttribute('data-locked-reference')) === ref, 'member sees the same reference');
  
  await dialog(admin, 'unlock', async d => { await d.locator('select[name=reasonCode]').selectOption('investigation-closed'); });
  ok(await admin.locator('[data-lock-state=unlocked]').count() === 1, 'lock removed');
  ok((await admin.locator('[data-locks] tr[data-active=false]').count()) >= 1, 'lock history kept');
  const after = await m.request.fetch('http://localhost:5081/bff/api/me/profile', { headers: { 'X-Swoovr-Bff': '1' } });
  ok(after.status() !== 403 || (await after.json()).code !== 'account-locked', 'member API no longer locked: ' + after.status());
  // Membership on the test account (§8)
  await admin.goto(`${A}/accounts/${test}/membership`);
  await admin.selectOption('form:has([data-op=grant]) select[name=reasonCode]', 'beta-tester');
  await Promise.all([admin.waitForLoadState('networkidle'), admin.click('[data-op=grant]')]); await admin.waitForTimeout(800);
  ok((await admin.locator('[data-result]').getAttribute('data-result')) === 'ok', 'complimentary Plus granted');
  // Verification override (§3)
  await admin.goto(`${A}/accounts/${test}/verification`);
  await dialog(admin, 'override-photoVerified', async d => { await d.locator('select[name=reasonCode]').selectOption('staff-selfie-check'); });
  ok((await admin.locator('[data-result]').getAttribute('data-result')) === 'ok', 'Photo Verified applied (' + await admin.locator('.notice').first().textContent() + ')');
  ok((await admin.locator('[data-verification-history] tbody tr').count()) >= 1, 'override history row');
  // Enforcement warn (§12)
  await admin.goto(`${A}/accounts/${test}/enforcement?action=warn`);
  await admin.click('[data-op=sanction]'); await confirm(admin);
  ok((await admin.locator('[data-result]').getAttribute('data-result')) === 'ok', 'warning issued');
  ok((await admin.locator('[data-sanction]').count()) >= 1, 'enforcement history row');
  await admin.goto(`${A}/accounts/${test}/audit`);
  ok((await admin.locator('[data-audit] tbody tr').count()) >= 3, 'audit shows actions');
  
  // Trust & Safety role boundaries
  const ts = await staff(b, 'fictional.trust@example.test', false);
  await ts.goto(`${A}/accounts/${test}/enforcement`);
  ok(await ts.locator('a[data-enforce-action=suspend]').count() === 1, 'T&S may suspend');
  ok(await ts.locator('span[data-enforce-action=terminate][aria-disabled=true]').count() === 1, 'T&S cannot terminate');
  await ts.goto(`${A}/accounts/${test}/enforcement?action=restrict`);
  const maxTs = await ts.locator('select[name=days] option').last().getAttribute('value');
  ok(maxTs === '90', 'T&S restricts up to 90 days');
  
  const sup = await staff(b, 'fictional.support@example.test', false);
  await sup.goto(`${A}/accounts/${test}/enforcement?action=restrict`);
  ok((await sup.locator('select[name=days] option').last().getAttribute('value')) === '30', 'Support restricts up to 30 days');
  ok(await sup.locator('span[data-enforce-action=suspend][aria-disabled=true]').count() === 1, 'Support cannot suspend');
  await sup.goto(`${A}/accounts/${test}/access`);
  ok(await sup.locator('[data-op=lock]').count() === 0, 'Support cannot lock');
  console.log('TOTAL', n);
  await b.close();
})().catch(e => { console.error('FAIL', e.message); process.exit(1); });
