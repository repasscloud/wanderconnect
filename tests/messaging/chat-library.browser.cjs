// Issue 543 (D456): the chat + menu. Image from library: only the member's own approved, standard library photos (never an
// album, albums-only or explicit photo, someone else's, or a removed one); the chat gets its own copy through the same checks
// as an upload (a new attachment item, screened, with every chat photo rule), so deleting the library photo never touches the
// chat photo. Send a photo and Take a photo use the same upload (the camera input asks for capture). Owned stack; synthetic
// images and fictional adults only.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises'), path = require('node:path'), os = require('node:os'), net = require('node:net'), crypto = require('node:crypto'), { once } = require('node:events');
const { spawn } = require('node:child_process');
if (!process.env.SWOOVR_TEST_APP_DATABASE || !process.env.Media__Local__Path || !process.env.SWOOVR_OWNED_RUN_MANIFEST) throw Error('Chat library photos require the owned runner.');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, sleep, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');
const { synthetic } = require('../media/synthetic-images.cjs');
const output = process.env.SWOOVR_SCREENSHOTS;
let EXTRA, extraProcess, extraFolder;
// A second owned API with the labelled Development-only screening simulation ("uncertain": every photo waits for a person),
// as the explicit-chat suite does; the owned runner's own API simulates "blocked" and has no ClamAV on the host.
async function extraApi(env) {
  const manifest = JSON.parse(await fs.readFile(process.env.SWOOVR_OWNED_RUN_MANIFEST, 'utf8'));
  const probe = net.createServer(); await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
  EXTRA = 'http://127.0.0.1:' + probe.address().port; await new Promise(resolve => probe.close(resolve));
  extraFolder = await fs.mkdtemp(path.join(os.tmpdir(), 'swoovr-chat-library-')); await fs.chmod(extraFolder, 0o700);
  const log = await fs.open(path.join(extraFolder, 'api.log'), 'w', 0o600);
  extraProcess = spawn('dotnet', [manifest.apiDll], { cwd: path.resolve('src/Swoovr.Api'), env: { ...process.env, ...env, ASPNETCORE_URLS: EXTRA }, stdio: ['ignore', log.fd, log.fd] }); await log.close();
  for (let n = 0; n < 150; n++) {
    if (extraProcess.exitCode !== null || extraProcess.signalCode !== null) throw Error('Owned chat-library API exited; inspect private log ' + extraFolder);
    try { if ((await fetch(EXTRA + '/health/live', { signal: AbortSignal.timeout(1000) })).ok) return; } catch {}
    await sleep(100);
  }
  throw Error('Owned chat-library API readiness timeout.');
}
async function stopExtra() {
  if (extraProcess && extraProcess.exitCode === null && extraProcess.signalCode === null) {
    const child = extraProcess, exited = once(child, 'exit'); child.kill('SIGTERM'); await Promise.race([exited, sleep(5000)]);
    if (child.exitCode === null && child.signalCode === null) { child.kill('SIGKILL'); await exited; }
  }
  extraProcess = null; if (extraFolder) { await fs.rm(extraFolder, { recursive: true, force: true }); extraFolder = null; }
}
const waitFor = async (predicate, ms = 90000) => { for (let i = 0; i < ms / 500; i++) { if (await predicate()) return true; await sleep(500); } return predicate(); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const canvas = await browser.newPage();
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const names = ['snd', 'rcv', 'oth'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('clib-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tk = {}, id = {}, pid = {};
    for (const n of names) {
      tk[n] = await memberToken(browser, members[n]);
      const saved = await call(tk[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Clib ' + n, homeCityId: 2078025 } });
      assert.equal(saved.status, 200); pid[n] = saved.body.publicId;
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      await call(tk[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tk[n]);
    }
    const ask = await call(tk.snd, '/api/requests', { method: 'POST', body: { to: pid.rcv, mode: 'friends' } });
    assert.equal((await call(tk.rcv, `/api/requests/${ask.body.id}/accept`, { method: 'POST' })).status, 200);
    const chat = (await call(tk.snd, '/api/conversations', { method: 'POST', body: { to: pid.rcv, mode: 'friends' } })).body.id;

    // Fixture photos in the owned store (as the mode-photos and photo-verified suites do).
    const jpeg = await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', seed: 543, width: 640, height: 480 }), hash = crypto.createHash('sha256').update(jpeg).digest('hex');
    const fixture = async (account, extra = {}) => {
      const media = crypto.randomUUID();
      await fs.mkdir(path.join(process.env.Media__Local__Path, 'photos'), { recursive: true });
      await fs.writeFile(path.join(process.env.Media__Local__Path, 'photos', media.replaceAll('-', '')), jpeg, { mode: 0o600 });
      const cols = Object.keys(extra).map(k => ',' + k).join(''), vals = Object.values(extra).map(v => ',' + v).join('');
      await sql(`INSERT INTO media_items(id,account_id,status,object_key,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,known_content_checked,screener,screened_at${cols}) VALUES('${media}','${account}','Passed','photos/${media.replaceAll('-', '')}',decode('${hash}','hex'),decode('${hash}','hex'),'image/jpeg',640,480,${jpeg.length},now()-interval '2 hours',1,false,'synthetic-fixture',now()${vals})`);
      return media;
    };
    const library = await fixture(id.snd);
    const albumOnly = await fixture(id.snd, { album_only: 'true', content_rating: "'Explicit'" });
    const removed = await fixture(id.snd, { removed_at: 'now()' });
    const theirs = await fixture(id.oth);
    const root = await rootAdminToken(browser);
    const staff = await createStaff('clib-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff); await call(sup, '/api/session');
    assert.equal((await call(sup, '/api/admin/media/safety-ack', { method: 'POST', body: { version: '2026-11-02', confirmed: true } })).status, 200);
    await extraApi({ Media__Screening__Simulate: 'uncertain', Media__Screening__Worker: 'true', Attachments__Scanning__Enabled: 'false' });
    const send = async (who, conversation, photoId) => {
      const res = await fetch(`${EXTRA}/api/conversations/${conversation}/photos/library`, { method: 'POST', headers: { Authorization: 'Bearer ' + tk[who], 'Content-Type': 'application/json' }, body: JSON.stringify({ photoId }) });
      return { status: res.status, body: await res.json().catch(() => null) };
    };

    // Refused: not mine, albums-only/explicit, removed, unknown, nothing chosen, not in the chat.
    for (const [what, photo] of [['someone else\'s', theirs], ['albums-only', albumOnly], ['removed', removed], ['unknown', crypto.randomUUID()], ['none', null]])
      assert.deepEqual(code(await send('snd', chat, photo)), [404, 'photo-not-found'], what);
    pass();
    assert.equal((await send('oth', chat, theirs)).status, 404, 'not in the chat'); pass();
    // The recipient's Photos in chat off applies as for any photo.
    assert.equal((await call(tk.rcv, '/api/me/notification-settings', { method: 'PUT', body: { chatPhotos: false } })).status, 200);
    assert.deepEqual(code(await send('snd', chat, library)), [409, 'photos-off']); pass();
    assert.equal((await call(tk.rcv, '/api/me/notification-settings', { method: 'PUT', body: { chatPhotos: true } })).status, 200);

    // Sent: a new chat attachment (its own copy), checking first, then approved and shown to the recipient.
    let r = await send('snd', chat, library);
    assert.deepEqual([r.status, r.body.kind, r.body.photoState], [202, 'photo', 'checking']); pass();
    const message = r.body.id, copy = await sql(`SELECT media_id FROM messages WHERE id = '${message}'`);
    assert.notEqual(copy, library, 'its own copy'); pass();
    assert.equal(await sql(`SELECT purpose || '|' || conversation_id FROM media_items WHERE id = '${copy}'`), `attachment|${chat}`); pass();
    const listed = async who => (await call(tk[who], `/api/conversations/${chat}/messages`)).body.items.find(m => m.id === message);
    assert.ok(await waitFor(async () => (await listed('snd'))?.photoState === 'in-review'), 'screened like an upload (simulated: waits for a person)'); pass();
    assert.equal(await listed('rcv'), undefined, 'the recipient sees nothing before it passes'); pass();
    assert.equal((await call(sup, `/api/admin/media/${copy}/decision`, { method: 'POST', body: { decision: 'approve', reasonCode: 'APPROVED_FALSE_POSITIVE' } })).status, 200);
    assert.ok(await waitFor(async () => (await listed('snd'))?.photoState === 'approved'), 'approved'); pass();
    assert.ok((await listed('rcv'))?.photo?.link, 'the recipient sees it'); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.snd}' AND purpose = 'attachment'`), '1', 'counts towards the daily photo limit'); pass();
    // Deleting the library photo leaves the chat photo.
    assert.equal((await call(tk.snd, `/api/me/photos/${library}`, { method: 'DELETE' })).status < 300, true);
    assert.equal(await sql(`SELECT removed_at IS NULL FROM media_items WHERE id = '${copy}'`), 't', 'the chat copy stays'); pass();

    // Web: the + menu, the library picker (eligible photos only) and the camera input.
    const second = await fixture(id.snd);
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.snd.email); await page.fill('#password', members.snd.password);
    await Promise.all([page.waitForURL(url => !url.pathname.startsWith('/account/sign-in')), page.click('#kc-login')]);
    await page.goto(web + `/chats/${chat}`);
    await page.waitForSelector('[data-conversation=ready]');
    await page.click('[data-attach]');
    assert.deepEqual(await page.locator('[data-attach-menu-open] .chat-menu-item').evaluateAll(es => es.map(e => e.textContent.trim())),
      ['Image from library', 'Send a photo', 'Take a photo', 'Plan a meet-up', 'Share a location']); pass();
    assert.equal(await page.getAttribute('[data-chat-camera-input]', 'capture'), 'environment', 'the camera on phones'); pass();
    assert.equal(await page.getAttribute('[data-chat-camera-input]', 'accept'), 'image/jpeg,image/png,image/webp'); pass();
    await page.click('[data-attach-library]');
    await page.waitForSelector('[data-library-picker] [data-library-photo]');
    assert.deepEqual(await page.locator('[data-library-photo]').evaluateAll(es => es.map(e => e.dataset.libraryPhoto)), [second], 'only eligible library photos'); pass();
    assert.ok(await page.isDisabled('[data-library-send]'), 'nothing chosen yet'); pass();
    await page.click(`[data-library-photo="${second}"]`);
    if (output) await page.screenshot({ path: output + '/chat-library-picker.png' });
    // The runner's own API has no ClamAV on the host, so its upload check can't run here (screening and delivery are covered
    // above on the second API). The picker must send the chosen photo to the library route and show the API's answer.
    const [posted] = await Promise.all([page.waitForRequest(q => q.url().includes(`/conversations/${chat}/photos/library`) && q.method() === 'POST'), page.click('[data-library-send]')]);
    assert.deepEqual(JSON.parse(posted.postData()), { photoId: second }, 'the chosen photo'); pass();
    const answer = await posted.response();
    if (answer.status() === 202) await page.waitForSelector('[data-library-picker]', { state: 'detached' });
    else assert.ok((await page.locator('[data-library-status]').textContent()).trim().length > 0, `the refusal is explained (${answer.status()})`);
    pass();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'no sideways scroll at 390 px'); pass();
    await context.close();

    console.log(`PASS: chat-library (${passed} checks).`);
  } finally {
    await stopExtra();
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
