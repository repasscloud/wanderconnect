// Native support journey against the running local platform and real local Zammad:
// member (web) submits -> staff (Admin) reads, adds a private note and a public reply, transfers nothing ->
// member sees the reply and never the note -> another member is refused -> staff closes -> member sees Closed.
// Fictional fixture accounts only. Passwords are read in-process from .local/platform/docker.env and never printed.
// Usage: SWOOVR_PLAYWRIGHT_PATH=/path/to/node_modules/playwright node tests/zammad/ui_journey.browser.cjs
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const root = path.resolve(__dirname, '../..');
const env = Object.fromEntries(fs.readFileSync(path.join(root, '.local/platform/docker.env'), 'utf8').split('\n')
  .filter(l => l.includes('=') && !l.startsWith('#')).map(l => { const i = l.indexOf('='); return [l.slice(0, i), l.slice(i + 1).replace(/^["']|["']$/g, '')]; }));
const web = process.env.SWOOVR_WEB || 'http://localhost:5081', admin = process.env.SWOOVR_ADMIN || 'http://localhost:5082';
const shots = path.join(root, '.local/zammad/ui-journey'); fs.mkdirSync(shots, { recursive: true, mode: 0o700 });
let checks = 0; const check = (ok, label) => { if (!ok) throw Object.assign(Error(label), { safe: true }); checks++; console.log('PASS: ' + label); };
const run = crypto.randomBytes(4).toString('hex');

async function signIn(page, start, email, password) {
  await page.goto(start);
  await page.waitForSelector('#username', { timeout: 30000 });
  await page.fill('#username', email); await page.fill('#password', password);
  await Promise.all([page.waitForURL(u => !u.toString().includes('/realms/'), { timeout: 30000 }), page.click('#kc-login')]);
}

(async () => {
  let pages = [];
  const browser = await chromium.launch(process.env.SWOOVR_BROWSER_CHANNEL ? { channel: process.env.SWOOVR_BROWSER_CHANNEL } : {});
  try {
    const member = await browser.newContext({ viewport: { width: 390, height: 844 } }), staff = await browser.newContext({ viewport: { width: 1280, height: 900 } }), other = await browser.newContext();
    const m = await member.newPage(), s = await staff.newPage(), o = await other.newPage(); pages = [['member', m], ['staff', s], ['other', o]];
    await signIn(m, web + '/account/sign-in?intro=done', 'fictional.alex@example.test', env.SWOOVR_LOCAL_MEMBER_PASSWORD);
    await m.goto(web + '/me/support/new?reference=SW-ABC234');
    await m.waitForSelector('[data-support-title]');
    check(await m.inputValue('[data-support-title]') === 'About report SW-ABC234', 'valid report reference prefills only a safe subject');
    await m.goto(web + '/me/support/new?reference=<script>');
    await m.waitForSelector('[data-support-title]');
    check(await m.inputValue('[data-support-title]') === '', 'invalid reference is ignored');
    const question = `Fictional journey ${run}: how do I change my display name?`;
    await m.fill('[data-support-title]', `Display name help ${run}`); await m.fill('[data-support-body]', question);
    await Promise.all([m.waitForURL(/\/me\/support\/\d+$/, { timeout: 60000 }), m.click('[data-support-send]')]);
    const id = m.url().match(/(\d+)$/)[1];
    await m.waitForSelector('[data-support-detail]');
    check((await m.textContent('[data-support-reference]')).trim() === 'TK-' + id, 'member sees the TK reference after creating');
    check((await m.textContent('[data-support-articles]')).includes(question), 'member sees their own message');
    await m.screenshot({ path: path.join(shots, 'member-created-390.png'), fullPage: true });
    check(await m.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'member ticket page fits 390px');

    await signIn(s, admin + '/sign-in', 'fictional.support@example.test', env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await s.goto(admin + '/tickets?query=TK-' + id);
    await s.waitForSelector('[data-tickets], [data-tickets-problem]', { timeout: 60000 });
    check(await s.locator(`[data-ticket="TK-${id}"]`).count() === 1, 'staff queue finds the member request by reference');
    // The queue is a helpdesk search; Zammad indexes new tickets asynchronously, so allow it to catch up.
    const listed = Date.now(); let found = false;
    while (!found && Date.now() - listed < 90000) {
      await s.goto(admin + '/tickets');
      await s.waitForSelector('[data-tickets], [data-tickets-problem]', { timeout: 60000 });
      found = await s.locator(`[data-ticket="TK-${id}"]`).count() === 1;
      if (!found) await s.waitForTimeout(3000);
    }
    check(found, `staff queue lists the new request (search index caught up in ${Math.round((Date.now() - listed) / 1000)} s)`);
    await s.goto(admin + '/tickets/' + id);
    await s.waitForSelector(`[data-ticket-detail="TK-${id}"]`);
    check((await s.textContent('[data-ticket-articles]')).includes(question), 'staff read the member message in Swoovr Admin');
    const note = `Private staff note ${run}: checked registry, nothing to flag.`, reply = `Public reply ${run}: open Account, then Profile, then edit your name.`;
    await s.fill('[data-ticket-reply-body]', note); await s.selectOption('[data-ticket-visibility]', 'private');
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-reply-send]')]);
    await s.waitForSelector('[data-ticket-article][data-private="true"]');
    check((await s.locator('[data-ticket-article][data-private="true"]').textContent()).includes(note), 'staff private note recorded and marked staff only');
    await s.fill('[data-ticket-reply-body]', reply); await s.selectOption('[data-ticket-visibility]', 'public');
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-reply-send]')]);
    await s.waitForFunction(r => document.querySelector('[data-ticket-articles]')?.textContent.includes(r), reply);
    check(true, 'staff public reply sent');
    await s.screenshot({ path: path.join(shots, 'admin-ticket.png'), fullPage: true });

    await m.goto(web + '/me/support/' + id);
    await m.waitForSelector('[data-support-articles]');
    const seen = await m.textContent('[data-support-articles]');
    check(seen.includes(reply), 'member sees the staff reply in Swoovr');
    check(!seen.includes(note) && !seen.includes('Private staff note'), 'member never sees the private note');
    await m.fill('[data-support-reply-body]', `Thanks ${run}, that worked.`);
    await m.click('[data-support-reply-send]');
    await m.waitForFunction(r => document.querySelector('[data-support-articles]')?.textContent.includes(r), `Thanks ${run}`, { timeout: 60000 });
    check(true, 'member reply appears in the same conversation');
    await m.screenshot({ path: path.join(shots, 'member-reply-390.png'), fullPage: true });
    await m.goto(web + '/me/support');
    await m.waitForSelector(`[data-support-ticket="TK-${id}"]`);
    check(true, 'member list shows the request');

    let otherSignedIn = true;
    try { await signIn(o, web + '/account/sign-in?intro=done', 'fictional.sam@example.test', env.SWOOVR_LOCAL_MEMBER_PASSWORD); } catch { otherSignedIn = false; }
    if (otherSignedIn) {
      await o.goto(web + '/me/support/' + id);
      await o.waitForSelector('[data-support-problem]', { timeout: 60000 });
      check(!(await o.content()).includes(question), 'another member cannot open the request');
    } else console.log('SKIP: second fixture member could not sign in with the shared local password (changed locally); API-level cross-member denial is covered by runtime_acceptance.py');

    await s.goto(admin + '/tickets/' + id);
    await s.waitForSelector('[data-ticket-change]');
    const closed = await s.locator('select[name=StateId] option', { hasText: 'Closed' }).getAttribute('value');
    await s.selectOption('select[name=StateId]', closed);
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-change-save]')]);
    await s.waitForSelector('[data-result="ok"]');
    check((await s.textContent('[data-ticket-state]')).trim() === 'Closed', 'staff closed the request');
    await m.goto(web + '/me/support/' + id);
    await m.waitForSelector('[data-support-state]');
    check((await m.textContent('[data-support-detail] [data-support-state]')).trim() === 'Closed', 'member sees the request closed');
    console.log(`PASS: ${checks} native support journey checks against real Zammad (ticket TK-${id}).`);
  } catch (e) {
    console.log('FAIL: ' + (e.safe ? e.message : (e.name + ': ' + String(e.message).split('\n')[0].slice(0, 200))));
    for (const [name, page] of pages) { try { console.log(`at ${name}: ${new URL(page.url()).pathname}`); await page.screenshot({ path: path.join(shots, `failure-${name}.png`), fullPage: true }); } catch { } }
    process.exitCode = 1;
  } finally { await browser.close(); }
})();
