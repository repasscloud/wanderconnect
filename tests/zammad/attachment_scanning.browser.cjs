// D336 (#221) acceptance against the running local platform, real local Zammad and the real pinned ClamAV:
// 1. A member sends a clean file and the EICAR test file: the text arrives at once; the clean file reaches Zammad only after
//    scanning and is downloadable; EICAR never reaches Zammad and shows as blocked (staff see the detection name).
// 2. With the scanner stopped, a reply's file stays in quarantine (health 503, nothing in Zammad) and is released after restart.
// 3. A file that arrived by email (never through quarantine) is scanned on download from Admin: EICAR is refused.
// Fictional fixture accounts only. Passwords are read in-process from .local/platform/docker.env and never printed.
// Usage: SWOOVR_PLAYWRIGHT_PATH=/path/to/node_modules/playwright SWOOVR_BROWSER_CHANNEL=chrome node tests/zammad/attachment_scanning.browser.cjs
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), { execFileSync } = require('node:child_process');
const rails = code => JSON.parse(execFileSync('docker', ['exec', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'runner', code], { encoding: 'utf8' }).split('\n').filter(l => l.startsWith('SWOOVR=')).pop().slice(7));
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const root = path.resolve(__dirname, '../..');
const env = Object.fromEntries(fs.readFileSync(path.join(root, '.local/platform/docker.env'), 'utf8').split('\n')
  .filter(l => l.includes('=') && !l.startsWith('#')).map(l => { const i = l.indexOf('='); return [l.slice(0, i), l.slice(i + 1).replace(/^["']|["']$/g, '')]; }));
const web = 'http://localhost:5081', admin = 'http://localhost:5082', api = 'http://localhost:5080';
const shots = path.join(root, '.local/zammad/attachment-scanning'); fs.mkdirSync(shots, { recursive: true, mode: 0o700 });
let checks = 0; const check = (ok, label) => { if (!ok) throw Object.assign(Error(label), { safe: true }); checks++; console.log('PASS: ' + label); };
const run = crypto.randomBytes(4).toString('hex');
// The standard harmless antivirus test string, built at runtime so this file itself never matches a scanner.
const eicar = ['X5O!P%@AP[4\\PZX54(P^)7CC)7}$', 'EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'].join('');
const zammadFiles = id => rails(`puts "SWOOVR=" + Ticket.find(${Number(id)}).articles.flat_map { |a| a.attachments.map(&:filename) }.to_json`);
const clamav = action => execFileSync('docker', [action, 'swoovr-platform-clamav-1'], { stdio: 'ignore' });
const health = async () => (await fetch(api + '/health/attachments')).status;

async function signIn(page, start, email, password) {
  await page.goto(start);
  await page.waitForSelector('#username', { timeout: 30000 });
  await page.fill('#username', email); await page.fill('#password', password);
  await Promise.all([page.waitForURL(u => !u.toString().includes('/realms/'), { timeout: 30000 }), page.click('#kc-login')]);
}

(async () => {
  const browser = await chromium.launch(process.env.SWOOVR_BROWSER_CHANNEL ? { channel: process.env.SWOOVR_BROWSER_CHANNEL } : {});
  let restart = false;
  try {
    check(await health() === 200, 'scanner healthy before the run');
    const m = await (await browser.newContext({ viewport: { width: 390, height: 844 }, acceptDownloads: true })).newPage();
    const s = await (await browser.newContext({ viewport: { width: 1280, height: 900 } })).newPage();
    await signIn(m, web + '/account/sign-in?intro=done', 'fictional.alex@example.test', env.SWOOVR_LOCAL_MEMBER_PASSWORD);

    // 1. Clean file + EICAR with the first message.
    const cleanText = `Fictional clean file ${run}.`;
    await m.goto(web + '/me/support/new');
    await m.waitForSelector('[data-support-pick]', { state: 'attached' });
    await m.fill('[data-support-title]', `Scanning ${run}`); await m.fill('[data-support-body]', `Fictional scanning check ${run}.`);
    await m.setInputFiles('[data-support-pick]', [{ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from(cleanText) }, { name: 'eicar.txt', mimeType: 'text/plain', buffer: Buffer.from(eicar) }]);
    await m.waitForSelector('[data-support-upload]');
    await Promise.all([m.waitForURL(/\/me\/support\/\d+$/, { timeout: 120000 }), m.click('[data-support-send]')]);
    const id = m.url().match(/(\d+)$/)[1];
    await m.waitForSelector('[data-support-detail]');
    check((await m.textContent('[data-support-articles]')).includes(`Fictional scanning check ${run}.`), 'the message text arrives at once');
    await m.waitForSelector('[data-support-attachment]', { timeout: 120000 });
    await m.waitForSelector('[data-support-held="blocked"]', { timeout: 60000 });
    check((await m.textContent('[data-support-held="blocked"]')).includes('eicar.txt') && (await m.textContent('[data-support-held="blocked"]')).includes('Blocked by the malware check'), 'EICAR shows as blocked to the member');
    check((await m.locator('[data-support-attachment]').allTextContents()).join(' ').includes('notes.txt'), 'the clean file is released into the conversation without a reload');
    const files = zammadFiles(id);
    check(files.includes('notes.txt') && !files.includes('eicar.txt'), 'Zammad holds the clean file and never the EICAR file');
    const download = m.waitForEvent('download');
    await m.locator('[data-support-attachment] button').first().click();
    const saved = await (await download).path();
    check(fs.readFileSync(saved, 'utf8') === cleanText, 'the member downloads the clean file intact');
    await m.screenshot({ path: path.join(shots, 'member-scanned-390.png'), fullPage: true });

    await signIn(s, admin + '/sign-in', 'fictional.support@example.test', env.SWOOVR_LOCAL_ADMIN_PASSWORD);
    await s.goto(admin + '/tickets/' + id);
    await s.waitForSelector(`[data-ticket-detail="TK-${id}"]`, { timeout: 60000 });
    const blocked = await s.textContent('[data-ticket-held="blocked"]');
    check(blocked.includes('eicar.txt') && /Eicar/i.test(blocked), 'staff see the blocked file with its detection name');
    const staffFile = await s.request.get(admin + (await s.locator('[data-ticket-attachment] a').first().getAttribute('href')));
    check(staffFile.status() === 200 && (await staffFile.text()) === cleanText, 'staff download the released clean file');
    await s.screenshot({ path: path.join(shots, 'staff-scanned-1280.png'), fullPage: true });

    // 2. Scanner outage: the file waits, nothing is released, then it goes through after the restart.
    clamav('stop'); restart = true;
    check(await health() === 503, 'health reports the stopped scanner (503)');
    await m.goto(web + '/me/support/' + id);
    await m.waitForSelector('[data-support-reply-body]');
    await m.fill('[data-support-reply-body]', `Fictional reply during outage ${run}.`);
    await m.setInputFiles('[data-support-pick]', [{ name: 'outage.txt', mimeType: 'text/plain', buffer: Buffer.from(`Outage file ${run}.`) }]);
    await m.waitForSelector('[data-support-upload]');
    await m.click('[data-support-reply-send]');
    await m.waitForSelector('[data-support-held="checking"]', { timeout: 60000 });
    check((await m.textContent('[data-support-held="checking"]')).includes('outage.txt'), 'during the outage the file shows as being checked');
    await new Promise(r => setTimeout(r, 20000));
    check(!zammadFiles(id).includes('outage.txt'), 'nothing is released while the scanner is down');
    check((await fetch(api + '/health/ready')).status === 200, 'the API stays ready during the scanner outage');
    clamav('start'); restart = false;
    const back = Date.now(); while (await health() !== 200 && Date.now() - back < 180000) await new Promise(r => setTimeout(r, 3000));
    check(await health() === 200, 'scanner healthy again after the restart');
    // The page's CSP forbids evaluated predicates, so poll the rendered files.
    const released = Date.now();
    while (!(await m.locator('[data-support-attachment]').allTextContents()).join(' ').includes('outage.txt') && Date.now() - released < 240000) await m.waitForTimeout(2000);
    check((await m.locator('[data-support-attachment]').allTextContents()).join(' ').includes('outage.txt'), 'the member sees the file released after the outage without a reload');
    check(zammadFiles(id).includes('outage.txt'), 'the waiting file is scanned and released after the outage');

    // 3. An emailed EICAR attachment (never quarantined) is refused on download from Admin.
    const subject = `Emailed file ${run}`, boundary = 'b' + run;
    execFileSync('docker', ['exec', '-i', 'swoovr-zammad-zammad-railsserver-1', 'bundle', 'exec', 'rails', 'r', 'Channel::Driver::MailStdin.new'], { input:
      `From: fictional.mailer.${run}@example.test\r\nTo: support@swoovr.test\r\nSubject: ${subject}\r\nMessage-ID: <${crypto.randomUUID()}@example.test>\r\nDate: ${new Date().toUTCString()}\r\nMIME-Version: 1.0\r\n` +
      `Content-Type: multipart/mixed; boundary="${boundary}"\r\n\r\n--${boundary}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nFictional emailed message ${run}.\r\n` +
      `--${boundary}\r\nContent-Type: text/plain; name="mailed.txt"\r\nContent-Disposition: attachment; filename="mailed.txt"\r\n\r\n${eicar}\r\n--${boundary}--\r\n`, stdio: ['pipe', 'ignore', 'ignore'] });
    const mailed = rails(`t = Ticket.where(title: ${JSON.stringify(subject)}).last; a = t.articles.first; puts "SWOOVR=" + {id: t.id, article: a.id, attachment: a.attachments.find { |x| x.filename == "mailed.txt" }&.id}.to_json`);
    check(mailed.attachment, 'the emailed attachment is in Zammad (it never passed Swoovr)');
    const refused = await s.request.get(`${admin}/tickets/${mailed.id}/articles/${mailed.article}/attachments/${mailed.attachment}`);
    const refusal = await refused.text();
    check(refused.status() === 409 && refusal.includes("malware check didn't clear it") && !refusal.includes('EICAR-STANDARD'), 'Admin refuses the emailed EICAR file after scanning it on download');
    console.log(`PASS attachment scanning: ${checks} live checks (real ClamAV, local Zammad; EICAR test string only).`);
  } catch (error) {
    console.error('FAIL: ' + (error.safe ? error.message : error.name + ': ' + error.message));
    process.exitCode = 1;
  } finally {
    if (restart) try { clamav('start'); } catch { }
    await browser.close();
  }
})();
