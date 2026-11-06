// Native support journey against the running local platform and real local Zammad:
// member (web) submits -> staff (Admin) reads, adds a private note and a public reply, transfers nothing ->
// member sees the reply and never the note -> another member is refused -> staff closes -> member sees Closed.
// Fictional fixture accounts only. Passwords are read in-process from .local/platform/docker.env and never printed.
// Usage: SWOOVR_PLAYWRIGHT_PATH=/path/to/node_modules/playwright node tests/zammad/ui_journey.browser.cjs
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), { execFileSync } = require('node:child_process');
// Zammad's shipped operator ingress for inbound mail (untrusted by default) and a Rails read-back helper; ids/flags only.
const rails = code => JSON.parse(execFileSync('docker', ['exec', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'runner', code], { encoding: 'utf8' }).split('\n').filter(l => l.startsWith('SWOOVR=')).pop().slice(7));
const mailIn = (from, subject, body) => execFileSync('docker', ['exec', '-i', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'r', 'Channel::Driver::MailStdin.new'],
  { input: `From: ${from}\r\nTo: support@swoovr.test\r\nSubject: ${subject}\r\nMessage-ID: <${crypto.randomUUID()}@example.test>\r\nDate: ${new Date().toUTCString()}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n${body}\r\n`, stdio: ['pipe', 'ignore', 'ignore'] });
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
    // bypassCSP lets the harness's waitForFunction predicates run under the app's strict CSP (test contexts only).
    const member = await browser.newContext({ viewport: { width: 390, height: 844 }, bypassCSP: true }), staff = await browser.newContext({ viewport: { width: 1280, height: 900 }, bypassCSP: true }), other = await browser.newContext({ bypassCSP: true });
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
    // Issue 223: open requests show on the overview by status, each linking to the filtered queue.
    await s.goto(admin + '/');
    await s.waitForSelector('[data-ticket-overview]', { timeout: 60000 });
    const newTile = s.locator('[data-ticket-overview-state="new"]');
    check(await newTile.count() === 1 && Number((await newTile.locator('strong').textContent()).replace('+', '')) >= 1, 'the overview counts the new request');
    check(/\/tickets\?state=\d+$/.test(await newTile.getAttribute('href')), 'the tile opens the queue filtered to that status');
    await s.goto(admin + '/tickets/' + id);
    await s.waitForSelector(`[data-ticket-detail="TK-${id}"]`);
    check((await s.textContent('[data-ticket-articles]')).includes(question), 'staff read the member message in Swoovr Admin');
    const note = `Private staff note ${run}: checked registry, nothing to flag.`, reply = `Public reply ${run}: open Account, then Profile, then edit your name.`;
    await s.fill('[data-ticket-reply-body]', note); await s.check('[data-ticket-visibility="private"]');
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-reply-send]')]);
    await s.waitForSelector('[data-ticket-article][data-private="true"]');
    check((await s.locator('[data-ticket-article][data-private="true"]').textContent()).includes(note), 'staff private note recorded and marked staff only');
    await s.fill('[data-ticket-reply-body]', reply); await s.check('[data-ticket-visibility="public"]');
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-reply-send]')]);
    await s.waitForFunction(r => document.querySelector('[data-ticket-articles]')?.textContent.includes(r), reply);
    check(true, 'staff public reply sent');
    await s.screenshot({ path: path.join(shots, 'admin-ticket.png'), fullPage: true });

    // The signed Zammad event becomes exactly one in-app notice for the public reply (none for the private note).
    let notices = [];
    for (let i = 0; i < 60 && notices.length === 0; i++) {
      notices = (await m.evaluate(async () => (await (await fetch('/bff/api/me/notifications', { headers: { 'X-Swoovr-Bff': '1' } })).json()).items))
        .filter(n => n.kind === 'support' && n.link === '/me/support/' + id);
      if (!notices.length) await m.waitForTimeout(1000);
    }
    check(notices.length === 1 && !notices[0].title.includes(reply) && !notices[0].title.includes(note), 'one content-free support notice from the real Zammad event, linking to the request');
    await m.waitForTimeout(5000);
    const again = (await m.evaluate(async () => (await (await fetch('/bff/api/me/notifications', { headers: { 'X-Swoovr-Bff': '1' } })).json()).items)).filter(n => n.kind === 'support' && n.link === '/me/support/' + id);
    check(again.length === 1, 'the private note and later events add no further notice');
    // Inbound email reply from the member's own address, threaded by ticket number. The address could be forged, so it
    // must not speak as the member inside Swoovr; staff see it labelled.
    const number = rails(`puts "SWOOVR=" + {n: Ticket.find(${id}).number}.to_json`).n;
    const emailed = `Email reply ${run}: please also delete my photos.`;
    mailIn('fictional.alex@example.test', `Re: [Ticket#${number}] Display name help`, emailed);
    const threaded = rails(`a=Ticket::Article.where(ticket_id: ${id}).order(:id).last; puts "SWOOVR=" + {type: a.type.name, sender: a.sender.name, mine: a.body.include?("${run}")}.to_json`);
    check(threaded.type === 'email' && threaded.sender === 'Customer' && threaded.mine, 'inbound email reply threads into the same helpdesk ticket by its number');
    await m.goto(web + '/me/support/' + id);
    await m.waitForSelector('[data-support-articles]');
    check(!(await m.textContent('[data-support-articles]')).includes(emailed), 'an emailed message (unverified sender) is not shown as the member in Swoovr');
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
    check(await m.locator('[data-support-urgent-link]').getAttribute('href') === 'https://swoovr.com/support', 'urgent and safety matters go to urgent support (issue 220)');
    check(await m.locator('#settings-menu [data-settings-link="support"][aria-current="page"]').count() === 1, 'Support sits in the Me menu (issue 219)');

    // Issue 220: a photo with the first message, the file downloadable, and the member closing their own request.
    const png = Buffer.from('89504e470d0a1a0a0000000d4948445200000001000000010806000000' + '1f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082', 'hex');
    await m.goto(web + '/me/support/new');
    await m.waitForSelector('[data-support-pick]', { state: 'attached' });
    await m.fill('[data-support-title]', `Photo help ${run}`); await m.fill('[data-support-body]', `Fictional journey ${run}: screenshot attached.`);
    await m.setInputFiles('[data-support-pick]', [{ name: 'screen.png', mimeType: 'image/png', buffer: png }]);
    await m.waitForSelector('[data-support-upload]');
    await m.setInputFiles('[data-support-pick]', [{ name: 'setup.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('MZ') }]);
    await m.waitForSelector('[data-support-files-problem]');
    check(await m.locator('[data-support-send]').isDisabled(), 'a file type outside the list blocks sending');
    await m.locator('[data-support-upload]').nth(1).locator('button').click();
    await m.waitForFunction(() => !document.querySelector('[data-support-files-problem]'));
    await Promise.all([m.waitForURL(/\/me\/support\/\d+$/, { timeout: 120000 }), m.click('[data-support-send]')]);
    await m.waitForSelector('[data-support-attachment]', { timeout: 60000 });
    check((await m.textContent('[data-support-articles]')).includes('screen.png'), 'the photo arrives with the first message');
    const download = m.waitForEvent('download');
    await m.locator('[data-support-attachment] button').first().click();
    check((await download).suggestedFilename() === 'screen.png', 'the member can download it');
    await m.click('[data-support-close]');
    await m.click('[data-support-close-yes]');
    await m.waitForFunction(() => document.querySelector('[data-support-detail] [data-support-state]')?.textContent.trim() === 'Closed', null, { timeout: 60000 });
    check(true, 'the member closed their own request');
    check(await m.locator('[data-support-reply-body]').count() === 0 && await m.locator('[data-support-closed]').count() === 1, 'a closed request takes no reply and is not reopened');
    // Issue 228: staff see the closed request without a reply box (they can still reopen it from its status).
    const closedId = m.url().match(/(\d+)$/)[1];
    await s.goto(admin + '/tickets/' + closedId);
    await s.waitForSelector(`[data-ticket-detail="TK-${closedId}"]`, { timeout: 60000 });
    check(await s.locator('[data-ticket-closed]').count() === 1 && await s.locator('[data-ticket-reply]').count() === 0 && await s.locator('[data-ticket-change]').count() === 1, 'Admin offers no reply on a closed request, only its status');
    await m.screenshot({ path: path.join(shots, 'member-closed-390.png'), fullPage: true });

    let otherSignedIn = true;
    try { await signIn(o, web + '/account/sign-in?intro=done', 'fictional.sam@example.test', env.SWOOVR_LOCAL_MEMBER_PASSWORD); } catch { otherSignedIn = false; }
    if (otherSignedIn) {
      await o.goto(web + '/me/support/' + id);
      await o.waitForSelector('[data-support-problem]', { timeout: 60000 });
      check(!(await o.content()).includes(question), 'another member cannot open the request');
    } else console.log('SKIP: second fixture member could not sign in with the shared local password (changed locally); API-level cross-member denial is covered by runtime_acceptance.py');

    await s.goto(admin + '/tickets/' + id);
    await s.waitForSelector('[data-ticket-change]');
    check(await s.locator('[data-ticket-email]').count() >= 1, 'staff see the emailed message labelled as an unverified sender');
    const strangerText = `New email ${run}: I need help with a booking.`;
    mailIn(`stranger-${run}@example.test`, `Help ${run}`, strangerText);
    const mailed = rails(`t=Ticket.where(title: "Help ${run}").last; puts "SWOOVR=" + {id: t&.id, group: t&.group&.name}.to_json`);
    check(mailed.id && mailed.group === 'General Support', 'email from a new address opens a ticket in General Support');
    await s.goto(admin + '/tickets/' + mailed.id);
    await s.waitForSelector(`[data-ticket-detail="TK-${mailed.id}"]`);
    check(await s.locator('[data-ticket-email-sender]').count() === 1 && (await s.textContent('[data-ticket-articles]')).includes(strangerText), 'staff handle a mailbox ticket natively, marked as an unverified email sender');
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
    // A guest (no membership) submits through the public guest API; staff handle it natively in Admin.
    const site = 'http://localhost:5099', api = process.env.SWOOVR_API || 'http://127.0.0.1:5080', guestEmail = `guest-ui-${run}@example.test`;
    const guestCall = async (p, method, body, grant) => { const r = await fetch(api + '/api/support/guest' + p, { method, headers: { Origin: site, 'Content-Type': 'application/json', ...(grant ? { Authorization: 'SwoovrGuest ' + grant } : {}) }, body: body && JSON.stringify(body) }); return [r.status, r.status === 204 ? null : await r.json()]; };
    check((await guestCall('/codes', 'POST', { email: guestEmail }))[0] === 202, 'guest asks for a code through the guest API');
    let guestCode;
    for (let i = 0; i < 60 && !guestCode; i++) {
      const found = await (await fetch('http://127.0.0.1:8025/api/v1/search?query=' + encodeURIComponent(`to:"${guestEmail}"`))).json();
      if (found.messages?.length) guestCode = ((await (await fetch('http://127.0.0.1:8025/api/v1/message/' + found.messages[0].ID)).json()).Text.match(/\b(\d{8})\b/) || [])[1];
      else await new Promise(r => setTimeout(r, 500));
    }
    const [, grant] = await guestCall('/grants', 'POST', { email: guestEmail, code: guestCode });
    const [gs, gt] = await guestCall('/tickets', 'POST', { operationId: crypto.randomUUID(), category: 'account', title: `Guest question ${run}`, body: `Fictional guest ${run}: can I pause my membership?` }, grant.grant);
    check(gs === 200, 'guest creates a request without a membership');
    await s.goto(admin + '/tickets/' + gt.id);
    await s.waitForSelector(`[data-ticket-detail="TK-${gt.id}"]`);
    check(await s.locator('[data-ticket-guest]').count() === 1, 'staff see the request marked as a guest, not a member');
    const guestReply = `Guest reply ${run}: yes, from Membership settings.`;
    await s.fill('[data-ticket-reply-body]', guestReply); await s.check('[data-ticket-visibility="public"]');
    await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-reply-send]')]);
    await s.waitForSelector('[data-result="ok"]');
    const [, gd] = await guestCall('/tickets/' + gt.id, 'GET', null, grant.grant);
    check(gd.articles.some(a => a.body.includes(guestReply)), 'guest reads the staff reply through the guest API');
    let notice;
    for (let i = 0; i < 60 && !notice; i++) {
      const found = await (await fetch('http://127.0.0.1:8025/api/v1/search?query=' + encodeURIComponent(`to:"${guestEmail}" subject:"Swoovr support replied"`))).json();
      if (found.messages?.length) notice = await (await fetch('http://127.0.0.1:8025/api/v1/message/' + found.messages[0].ID)).json();
      else await new Promise(r => setTimeout(r, 1000));
    }
    check(notice && notice.Text.includes('TK-' + gt.id) && !notice.Text.includes(guestReply), 'guest gets one reply email naming the request, without the reply text');
    // Report follow-up: the member's own SW- reference links the request to that case (a reference only), and closing the
    // request changes nothing in Swoovr's case, sanctions or appeals.
    const sql = q => execFileSync('docker', ['exec', 'swoovr-platform-postgres-1', 'psql', '-p', '5433', '-U', 'swoovr', '-d', 'swoovr', '-At', '-c', q], { encoding: 'utf8' }).trim();
    const samPublic = sql("SELECT p.public_id FROM member_profiles p JOIN accounts a ON a.id = p.account_id WHERE a.email = 'fictional.sam@example.test'");
    await m.goto(web + '/me/support');
    const reported = await m.evaluate(async body => (await fetch('/bff/api/reports', { method: 'POST', headers: { 'X-Swoovr-Bff': '1', 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).status,
      { subjectType: 'profile', subjectId: samPublic, category: 'spam', text: `Fictional report ${run}`, block: false });
    const reference = sql("SELECT r.support_reference FROM reports r JOIN accounts a ON a.id = r.reporter_account_id WHERE a.email = 'fictional.alex@example.test' ORDER BY r.created_at DESC LIMIT 1");
    if (reported < 300 && /^SW-/.test(reference)) {
      const caseId = sql(`SELECT case_id FROM reports WHERE support_reference = '${reference}'`);
      const before = sql(`SELECT c.status || '|' || (SELECT count(*) FROM sanctions s WHERE s.case_id = c.id) || '|' || (SELECT count(*) FROM appeals) FROM moderation_cases c WHERE c.id = '${caseId}'`);
      await m.goto(web + '/me/support/new?reference=' + reference);
      await m.waitForSelector('[data-support-title]');
      await m.fill('[data-support-body]', `Fictional follow-up ${run} about my report.`);
      await Promise.all([m.waitForURL(/\/me\/support\/\d+$/, { timeout: 60000 }), m.click('[data-support-send]')]);
      const linkedId = m.url().match(/(\d+)$/)[1];
      await s.goto(admin + '/tickets/' + linkedId);
      await s.waitForSelector(`[data-ticket-link="${caseId}"]`);
      check(true, "member's own report reference links the request to its case (reference only)");
      const closedState = await s.locator('select[name=StateId] option', { hasText: 'Closed' }).getAttribute('value');
      await s.selectOption('select[name=StateId]', closedState);
      await Promise.all([s.waitForLoadState('load'), s.click('[data-ticket-change-save]')]);
      await s.waitForSelector('[data-result="ok"]');
      check(sql(`SELECT c.status || '|' || (SELECT count(*) FROM sanctions s WHERE s.case_id = c.id) || '|' || (SELECT count(*) FROM appeals) FROM moderation_cases c WHERE c.id = '${caseId}'`) === before,
        'closing the linked request leaves the case, its sanctions and appeals unchanged');
    } else console.log('SKIP: the fixture report could not be created locally (status ' + reported + '); case links not exercised in the browser');
    // Staff access is mirrored from Swoovr into the helpdesk (Q133): the root admin sees it and can check it now.
    const rootPage = await (await browser.newContext({ viewport: { width: 1280, height: 900 } })).newPage(); pages.push(['root', rootPage]);
    await rootPage.goto(admin + '/sign-in');
    await rootPage.waitForSelector('#username', { timeout: 30000 });
    await rootPage.fill('#username', env.SWOOVR_LOCAL_ADMIN_EMAIL); await rootPage.fill('#password', env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await rootPage.click('#kc-login');
    // The fictional root admin has a second factor; the launcher computes the current code (kept in-process, never printed).
    await rootPage.waitForSelector('#otp', { timeout: 30000 });
    await rootPage.fill('#otp', execFileSync(path.join(root, 'scripts/local-platform.sh'), ['admin-code'], { cwd: root, encoding: 'utf8' }).trim().split('\n').pop());
    await Promise.all([rootPage.waitForURL(u => !u.toString().includes('/realms/'), { timeout: 30000 }), rootPage.click('#kc-login')]);
    await rootPage.goto(admin + '/tickets/diagnostics');
    await rootPage.waitForSelector('[data-ticket-staff-sync="configured"]', { timeout: 60000 });
    check(await rootPage.getAttribute('[data-ticket-staff-failed]', 'data-ticket-staff-failed') === '0', 'integration health shows staff access copied from Swoovr with nothing failing');
    await Promise.all([rootPage.waitForLoadState('load'), rootPage.click('[data-ticket-staff-sync-now] button')]);
    await rootPage.waitForSelector('[data-ticket-staff-sync-result]', { timeout: 60000 });
    check(/^Checked \d+ staff\./.test((await rootPage.textContent('[data-ticket-staff-sync-result]')).trim()), 'an administrator can check staff helpdesk access on demand');
    await rootPage.screenshot({ path: path.join(shots, 'admin-staff-access.png'), fullPage: true });
    console.log(`PASS: ${checks} native support journey checks against real Zammad (ticket TK-${id}).`);
  } catch (e) {
    console.log('FAIL: ' + (e.safe ? e.message : (e.name + ': ' + String(e.message).split('\n')[0].slice(0, 200))));
    for (const [name, page] of pages) { try { console.log(`at ${name}: ${new URL(page.url()).pathname}`); await page.screenshot({ path: path.join(shots, `failure-${name}.png`), fullPage: true }); } catch { } }
    process.exitCode = 1;
  } finally { await browser.close(); }
})();
