// Issue 530 (D424, D427): Photo Verified. Off by default (the owned API has no switch): the member sees it isn't
// available. On an owned extra API with the switch on: a fresh four-character code each time, single use and bound to the
// account; one photo holding it goes through the upload checks into the Photo Identified queue (never a member link);
// Support approves against the public profile photos the member had (refused if those changed), which awards Photo
// Verified for 12 months and dates the matched photos; changing a matched photo ends it; rejection gives a member reason;
// three sends in 30 days; evidence deleted 30 days after the decision. Synthetic images and fictional adults only;
// labelled simulated screening and the Development-only disabled malware scanner on the extra API.
const assert = require('node:assert/strict');
const crypto = require('node:crypto'), fs = require('node:fs/promises'), path = require('node:path'), os = require('node:os'), net = require('node:net'), { once } = require('node:events');
const { spawn } = require('node:child_process');
if (!process.env.SWOOVR_TEST_APP_DATABASE || !process.env.SWOOVR_OWNED_RUN_MANIFEST || !process.env.Media__Local__Path) throw Error('Photo Verified requires the owned runner.');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, fillStaffOtp, sleep, createMember, createStaff, memberToken, staffToken, rootAdminToken, elevatedRootToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');
const { synthetic } = require('../media/synthetic-images.cjs');

let EXTRA, extraProcess, extraFolder;
async function extraApi(env) {
  const manifest = JSON.parse(await fs.readFile(process.env.SWOOVR_OWNED_RUN_MANIFEST, 'utf8'));
  const probe = net.createServer(); await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
  EXTRA = 'http://127.0.0.1:' + probe.address().port; await new Promise(resolve => probe.close(resolve));
  extraFolder = await fs.mkdtemp(path.join(os.tmpdir(), 'swoovr-photo-verified-')); await fs.chmod(extraFolder, 0o700);
  const log = await fs.open(path.join(extraFolder, 'api.log'), 'w', 0o600);
  extraProcess = spawn('dotnet', [manifest.apiDll], { cwd: path.resolve('src/Swoovr.Api'), env: { ...process.env, ...env, ASPNETCORE_URLS: EXTRA }, stdio: ['ignore', log.fd, log.fd] }); await log.close();
  for (let n = 0; n < 150; n++) {
    if (extraProcess.exitCode !== null || extraProcess.signalCode !== null) throw Error('Owned Photo Verified API exited; inspect private log ' + extraFolder);
    try { if ((await fetch(EXTRA + '/health/live', { signal: AbortSignal.timeout(1000) })).ok) return; } catch {}
    await sleep(100);
  }
  throw Error('Owned Photo Verified API readiness timeout.');
}
async function stopExtra() {
  if (extraProcess && extraProcess.exitCode === null && extraProcess.signalCode === null) {
    const child = extraProcess, exited = once(child, 'exit'); child.kill('SIGTERM'); await Promise.race([exited, sleep(5000)]);
    if (child.exitCode === null && child.signalCode === null) { child.kill('SIGKILL'); await exited; }
  }
  extraProcess = null;
  if (extraFolder) { if (process.env.SWOOVR_SCREENSHOTS) await fs.copyFile(path.join(extraFolder, 'api.log'), path.join(process.env.SWOOVR_SCREENSHOTS, 'photo-verified-extra-api.log')).catch(() => {}); await fs.rm(extraFolder, { recursive: true, force: true }); extraFolder = null; }
}
const waitFor = async (predicate, ms = 60000) => { for (let i = 0; i < ms / 500; i++) { if (await predicate()) return true; await sleep(500); } return predicate(); };
const today = () => new Date().toISOString().slice(0, 10);
const output = process.env.SWOOVR_SCREENSHOTS;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const canvas = await browser.newPage();
  let passed = 0; const pass = () => passed++;
  try {
    const root = await rootAdminToken(browser);
    const names = ['pia', 'ned', 'rex'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('pv-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tk = {}, id = {}, pid = {};
    for (const n of names) {
      tk[n] = await memberToken(browser, members[n]);
      const saved = await call(tk[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Pv ' + n, homeCityId: 2078025 } });
      assert.equal(saved.status, 200); pid[n] = saved.body.publicId;
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      await call(tk[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tk[n]);
    }
    // Synthetic approved library photos (as the mode-photos suite does), placed on the profile.
    const jpeg = await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', seed: 530 }), hash = crypto.createHash('sha256').update(jpeg).digest('hex');
    const libraryPhoto = async account => {
      const media = crypto.randomUUID();
      await fs.mkdir(path.join(process.env.Media__Local__Path, 'photos'), { recursive: true });
      await fs.writeFile(path.join(process.env.Media__Local__Path, 'photos', media.replaceAll('-', '')), jpeg, { mode: 0o600 });
      await sql(`INSERT INTO media_items(id,account_id,status,object_key,sha256,upload_sha256,content_type,width,height,bytes,created_at,screen_attempts,known_content_checked,screener,screened_at) VALUES('${media}','${account}','Passed','photos/${media.replaceAll('-', '')}',decode('${hash}','hex'),decode('${hash}','hex'),'image/jpeg',640,480,${jpeg.length},now()-interval '2 hours',1,false,'synthetic-fixture',now())`);
      return media;
    };
    const place = (who, mediaIds) => call(tk[who], '/api/me/photos/placement', { method: 'PUT', body: { scope: 'friends', mediaIds, mainMediaId: mediaIds[0] ?? null } });
    const view = (base, who) => call(tk[who], '/api/me/verification/photo', { base });

    // ---- Off by default: an API without the switch says it isn't available and refuses a code. (The runner turns it on
    // for the owned hosts during this suite, so the screens can be checked below.)
    await extraApi({ Verification__PhotoVerified__Enabled: 'false' });
    const off = async route => { const res = await fetch(EXTRA + route.path, { method: route.method, headers: { Authorization: 'Bearer ' + tk.pia, 'Content-Type': 'application/json' }, body: route.body }); return { status: res.status, body: await res.json().catch(() => null) }; };
    let r = await off({ path: '/api/me/verification/photo', method: 'GET' });
    assert.deepEqual([r.status, r.body.available, r.body.state], [200, false, 'none']); pass();
    assert.equal((await off({ path: '/api/me/verification/photo/code', method: 'POST', body: JSON.stringify({ date: today() }) })).status, 503); pass();
    await stopExtra();

    await extraApi({ Media__Screening__Simulate: 'uncertain', Media__Screening__Worker: 'true', Attachments__Scanning__Enabled: 'false', Verification__PhotoVerified__Enabled: 'true' });
    const at = (who, route, options = {}) => fetch(EXTRA + route, { ...options, headers: { Authorization: 'Bearer ' + tk[who], ...(options.body && !(options.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}), ...(options.headers ?? {}) } })
      .then(async res => { const text = await res.text(); let body = null; try { body = text ? JSON.parse(text) : null; } catch { body = text; } return { status: res.status, body }; });
    const askCode = (who, date = today()) => at(who, '/api/me/verification/photo/code', { method: 'POST', body: JSON.stringify({ date }) });
    const send = async (who, code, seed) => {
      const form = new FormData();
      form.append('file', new Blob([await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', seed, label: 'FICTIONAL PAPER ' + code })], { type: 'image/jpeg' }), 'paper.jpg');
      form.append('code', code);
      return at(who, '/api/me/verification/photo', { method: 'POST', body: form });
    };

    // ---- A code needs a profile photo and today's date; it's four characters from the unambiguous alphabet.
    assert.deepEqual([(await askCode('pia')).status, (await askCode('pia')).body?.code], [409, 'photo-verification-needs-photo']); pass();
    const first = await libraryPhoto(id.pia), second = await libraryPhoto(id.pia);
    assert.equal((await place('pia', [first, second])).status, 200);
    assert.equal((await askCode('pia', '2020-01-01')).status, 400, 'not some other date'); pass();
    r = await askCode('pia');
    assert.equal(r.status, 200, JSON.stringify(r.body));
    assert.deepEqual([r.body.available, r.body.state, r.body.writtenDate, r.body.attemptsLeft], [true, 'issued', today(), 3]); pass();
    assert.match(r.body.code, /^[ACDEFHJKMNPQRTUVWXY3479]{4}$/); pass();
    const oldCode = r.body.code;
    const fresh = (await askCode('pia')).body.code;
    assert.equal((await send('pia', oldCode, 531)).body?.code, 'photo-verification-code', 'an old code is refused'); pass();
    assert.equal((await send('ned', fresh, 532)).body?.code, 'photo-verification-code', "someone else's code is refused"); pass();

    // ---- Sent: screened, then waiting for a person; never on a member link, and Pia can't ask for another code meanwhile.
    r = await send('pia', fresh, 533);
    assert.deepEqual([r.status, r.body.state, r.body.attemptsLeft], [200, 'submitted', 2]); pass();
    assert.equal((await send('pia', fresh, 534)).body?.code, 'photo-verification-code', 'single use'); pass();
    assert.equal((await askCode('pia')).body?.code, 'photo-verification-pending'); pass();
    const evidence = await sql(`SELECT media_id FROM photo_verification_attempts WHERE account_id = '${id.pia}' AND state = 'Submitted'`);
    assert.ok(await waitFor(async () => (await sql(`SELECT object_key FROM media_items WHERE id = '${evidence}'`)).startsWith('verification/')), 'kept in its own place'); pass();
    assert.equal(await sql(`SELECT purpose || '|' || status FROM media_items WHERE id = '${evidence}'`), 'verification|Uncertain'); pass();
    assert.ok(!(await call(tk.pia, '/api/me/photos')).body.library?.some?.(p => p.id === evidence), 'not in the library'); pass();

    // ---- Support reviews it: the evidence link, the code and date, and the profile photos she had.
    const staff = await createStaff('pv-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff); await call(sup, '/api/session');
    assert.equal((await call(sup, '/api/admin/media/safety-ack', { method: 'POST', body: { version: '2026-11-02', confirmed: true } })).status, 200);
    let queue = (await call(sup, '/api/admin/photo-verification')).body;
    let item = queue.find(i => i.accountId === id.pia);
    assert.deepEqual([item?.code, item?.writtenDate, item?.profilePhotos.map(p => p.id).sort(), item?.profilePhotosChanged], [fresh, today(), [first, second].sort(), false]); pass();
    assert.match(item.evidence, /\.verification\./); pass();
    assert.equal((await fetch(`${api}/${item.evidence}`, { headers: { Authorization: 'Bearer ' + sup } })).status, 200, 'the staff link serves it to its reviewer'); pass();
    assert.equal((await fetch(`${api}/${item.evidence}`)).status, 401, 'never without a staff session'); pass();
    assert.equal((await fetch(`${api}/${item.evidence}`, { headers: { Authorization: 'Bearer ' + tk.ned } })).status, 403, 'never to a member'); pass();
    const forged = item.evidence.replace('.verification.', '.member.');
    assert.notEqual((await fetch(`${api}/${forged}`)).status, 200, 'never as a member photo'); pass();
    // PR 608 review: never in the ordinary media review queue, and its decision endpoint refuses it.
    assert.ok(!(await call(sup, '/api/admin/media/review')).body.items.some(i => i.id === evidence), 'not in the ordinary queue'); pass();
    assert.equal((await call(sup, `/api/admin/media/${evidence}/decision`, { method: 'POST', body: { decision: 'approve', reasonCode: 'APPROVED_STANDARD' } })).status, 404); pass();
    assert.equal((await call(tk.pia, '/api/admin/photo-verification')).status, 403, "members can't see the queue"); pass();
    // PR 610 review: staff who haven't acknowledged the safety notice get no evidence links and can't decide.
    const fresh2 = await createStaff('pv-sup2');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: fresh2.email, role: 'support' } })).status, 201);
    const sup2 = await staffToken(browser, fresh2); await call(sup2, '/api/session');
    r = await call(sup2, '/api/admin/photo-verification');
    assert.deepEqual([r.status, r.body?.code], [403, 'media-safety-ack-required']); pass();
    r = await call(sup2, `/api/admin/photo-verification/${item.id}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'other' } });
    assert.deepEqual([r.status, r.body?.code], [403, 'media-safety-ack-required']); pass();

    // ---- A stale approval is refused: a profile photo changed after she sent it.
    assert.equal((await place('pia', [first])).status, 200);
    queue = (await call(sup, '/api/admin/photo-verification')).body; item = queue.find(i => i.accountId === id.pia);
    assert.equal(item.profilePhotosChanged, true); pass();
    assert.equal((await call(sup, `/api/admin/photo-verification/${item.id}/decision`, { method: 'POST', body: { decision: 'approve' } })).body?.code, 'photo-verification-stale'); pass();
    assert.equal((await call(sup, `/api/admin/photo-verification/${item.id}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'made-up' } })).status, 400); pass();
    assert.equal((await call(sup, `/api/admin/photo-verification/${item.id}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'profile-photos-changed' } })).status, 200); pass();
    // PR 608 review: rejected evidence stays for the 30 days (an ordinary retention run doesn't take it).
    await cli('retention', 'purge');
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id = '${evidence}' AND object_key IS NOT NULL`), '1', 'kept after a rejection'); pass();
    r = await view(undefined, 'pia');
    assert.deepEqual([r.body.state, r.body.reason, r.body.attemptsLeft], ['rejected', 'Your profile photos changed after you sent it. Send a new one.', 2]); pass();
    // D454: the decision reaches the member's notification centre, opening the verification page; no outcome in the text.
    const noticed = async () => (await call(tk.pia, '/api/me/notifications')).body.items.filter(n => n.kind === 'photoVerified');
    let notes = await noticed();
    assert.deepEqual([notes.length, notes[0]?.link, notes[0]?.title], [1, '/me/verification', 'There is an update to your Photo Verified request. Open Verification to see it.']); pass();

    // ---- Again, approved: Photo Verified for 12 months, the badge, and the date on the matched photo in the viewer.
    const again = (await askCode('pia')).body.code;
    assert.equal((await send('pia', again, 535)).body.state, 'submitted');
    item = (await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia);
    await waitFor(async () => !!(await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia));
    item = (await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia);
    assert.equal((await call(sup, `/api/admin/photo-verification/${item.id}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 200); pass();
    r = await view(undefined, 'pia');
    assert.equal(r.body.state, 'verified'); pass();
    notes = await noticed();
    assert.equal(notes.length, 2, 'one notification per decision'); pass();
    assert.equal(await sql(`SELECT count(*) FROM notification_deliveries d JOIN notifications n ON n.id = d.notification_id WHERE n.account_id = '${id.pia}' AND n.kind = 'PhotoVerified' AND d.status = 'send'`), '0', 'no push or email'); pass();
    const months = (new Date(r.body.verifiedUntil) - new Date(r.body.verifiedAt)) / 86400000;
    assert.ok(months >= 364 && months <= 366, '12 months'); pass();
    assert.equal(await sql(`SELECT photo_status || '|' || photo_manual || '|' || id_status FROM member_profiles WHERE account_id = '${id.pia}'`), 'Verified|false|NotStarted', 'never ID Verified'); pass();
    const shown = (await call(tk.ned, `/api/profiles/${pid.pia}?mode=friends`)).body;
    assert.ok(shown.badges.some(b => b.badge === 'photoVerified'), 'the badge shows'); pass();
    const friends = shown.modes.find(m => m.id === 'friends'), photos = [...(friends.photos ?? []), friends.mainPhoto, shown.photo].filter(Boolean);
    assert.ok(photos.some(p => p.id === first && p.photoVerifiedAt), 'the matched photo carries the date'); pass();

    // ---- Changing a matched public photo ends it.
    const third = await libraryPhoto(id.pia);
    assert.equal((await place('pia', [third])).status, 200);
    assert.equal(await sql(`SELECT photo_status FROM member_profiles WHERE account_id = '${id.pia}'`), 'Expired'); pass();
    assert.equal(await sql(`SELECT count(*) FROM photo_verification_matches WHERE account_id = '${id.pia}'`), '0'); pass();

    // ---- Three sends in 30 days.
    const third_code = (await askCode('pia')).body;
    assert.equal(third_code.attemptsLeft, 1);
    assert.equal((await send('pia', third_code.code, 536)).body.state, 'submitted');
    const last = (await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia)
      ?? (await waitFor(async () => !!(await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia)), (await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.pia));
    assert.equal((await call(sup, `/api/admin/photo-verification/${last.id}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'photo-unclear' } })).status, 200);
    assert.equal((await askCode('pia')).body?.code, 'photo-verification-limit'); pass();

    // ---- PR 610 review: evidence still being screened can't be decided. PR 608/610 review: revoking the badge takes the
    // photo dates with it.
    // PR 610 review: a historic `main` placement is never published, so it isn't a profile photo for this check.
    const legacy = await libraryPhoto(id.rex);
    await sql(`INSERT INTO media_placements(account_id, scope, position, media_id, is_main) VALUES('${id.rex}', 'main', 0, '${legacy}', false)`);
    assert.equal((await askCode('rex')).body?.code, 'photo-verification-needs-photo', 'a legacy main photo alone is not enough'); pass();
    const rexPhoto = await libraryPhoto(id.rex);
    assert.equal((await place('rex', [rexPhoto])).status, 200);
    assert.equal((await send('rex', (await askCode('rex')).body.code, 537)).body.state, 'submitted');
    const rexEvidence = await sql(`SELECT media_id FROM photo_verification_attempts WHERE account_id = '${id.rex}' AND state = 'Submitted'`);
    await waitFor(async () => !!(await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.rex));
    const rexItem = (await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.rex);
    assert.deepEqual(rexItem.profilePhotos.map(p => p.id), [rexPhoto], 'compared with the published photo only'); pass();
    await sql(`UPDATE media_items SET status = 'Pending' WHERE id = '${rexEvidence}'`);
    assert.equal((await call(sup, `/api/admin/photo-verification/${rexItem.id}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 409, 'not while screening'); pass();
    assert.equal((await call(sup, `/api/admin/photo-verification/${rexItem.id}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'other' } })).status, 409); pass();
    assert.equal(await sql(`SELECT state FROM photo_verification_attempts WHERE id = '${rexItem.id}'`), 'Submitted'); pass();
    await sql(`UPDATE media_items SET status = 'Uncertain' WHERE id = '${rexEvidence}'`);
    assert.equal((await call(sup, `/api/admin/photo-verification/${rexItem.id}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 200);
    const rexShown = async () => { const p = (await call(tk.pia, `/api/profiles/${pid.rex}?mode=friends`)).body, m = p.modes.find(x => x.id === 'friends');
      return [...(m.photos ?? []), m.mainPhoto, p.photo].filter(Boolean).filter(x => x.id === rexPhoto); };
    assert.ok((await rexShown()).some(p => p.photoVerifiedAt), 'dated while it stands'); pass();
    const elevated = await elevatedRootToken(browser);
    r = await call(elevated, `/api/admin/accounts/${id.rex}/verification-override`, { method: 'POST', body: { badge: 'photoVerified', apply: false, reasonCode: 'applied-in-error' } });
    assert.equal(r.status, 204, JSON.stringify(r.body));
    assert.ok((await rexShown()).length > 0 && (await rexShown()).every(p => !p.photoVerifiedAt), 'no date after a revoke'); pass();
    assert.equal(await sql(`SELECT count(*) FROM photo_verification_matches WHERE account_id = '${id.rex}'`), '0'); pass();

    // ---- PR 608/610 review: evidence that screening blocks closes its request with neutral wording (a sealed object
    // stands in for a known-content match: both take the same block path), so Rex can try again.
    assert.equal((await send('rex', (await askCode('rex')).body.code, 538)).body.state, 'submitted');
    const blocked = await sql(`SELECT media_id FROM photo_verification_attempts WHERE account_id = '${id.rex}' AND state = 'Submitted'`);
    await waitFor(async () => (await sql(`SELECT object_key FROM media_items WHERE id = '${blocked}'`)).startsWith('verification/'));
    const key = await sql(`SELECT object_key FROM media_items WHERE id = '${blocked}'`), sealed = 'evidence/' + key.split('/').pop();
    await fs.mkdir(path.join(process.env.Media__Local__Path, 'evidence'), { recursive: true });
    await fs.copyFile(path.join(process.env.Media__Local__Path, key), path.join(process.env.Media__Local__Path, sealed));
    await sql(`UPDATE media_items SET status = 'Pending', object_key = '${sealed}', next_screen_at = now() WHERE id = '${blocked}'`);
    assert.ok(await waitFor(async () => (await sql(`SELECT state FROM photo_verification_attempts WHERE media_id = '${blocked}'`)) === 'Rejected', 90000), 'closed by screening'); pass();
    assert.equal(await sql(`SELECT status FROM media_items WHERE id = '${blocked}'`), 'Blocked', 'still blocked (its escalation stands)'); pass();
    r = await call(tk.rex, '/api/me/verification/photo');
    assert.deepEqual([r.body.state, r.body.reason], ['rejected', "We couldn't award Photo Verified this time."]); pass();
    assert.equal((await askCode('rex')).status, 200, 'not stuck on "checking"'); pass();
    await sql(`UPDATE photo_verification_attempts SET decided_at = now() - interval '31 days' WHERE media_id = '${blocked}'`);
    await cli('retention', 'purge');
    assert.equal(await sql(`SELECT count(*) FROM photo_verification_attempts WHERE media_id = '${blocked}' AND evidence_deleted_at IS NULL`), '1', 'blocked evidence is left to safety retention'); pass();

    // ---- PR 610 review: the retention worker ends a photo-check award only while it's still the one due; a current one stays.
    await sql(`UPDATE member_profiles SET photo_status = 'Verified', photo_manual = false, photo_verified_until = now() + interval '1 day' WHERE account_id = '${id.rex}'`);
    await sql(`INSERT INTO photo_verification_matches(account_id, media_id, verified_at) VALUES('${id.rex}', '${rexPhoto}', now())`);
    await cli('retention', 'purge');
    assert.equal(await sql(`SELECT photo_status || '|' || count(m.media_id) FROM member_profiles p LEFT JOIN photo_verification_matches m ON m.account_id = p.account_id WHERE p.account_id = '${id.rex}' GROUP BY p.photo_status`), 'Verified|1', 'not due: kept'); pass();
    await sql(`UPDATE member_profiles SET photo_verified_until = now() - interval '1 minute' WHERE account_id = '${id.rex}'`);
    await cli('retention', 'purge');
    assert.equal(await sql(`SELECT photo_status || '|' || count(m.media_id) FROM member_profiles p LEFT JOIN photo_verification_matches m ON m.account_id = p.account_id WHERE p.account_id = '${id.rex}' GROUP BY p.photo_status`), 'Expired|0', 'due: ended'); pass();

    // ---- Retention: evidence 30 days after the decision is deleted; the decision record stays.
    await sql(`UPDATE photo_verification_attempts SET decided_at = now() - interval '31 days' WHERE account_id = '${id.pia}' AND decided_at IS NOT NULL`);
    await cli('retention', 'purge');
    assert.equal(await sql(`SELECT count(*) FROM photo_verification_attempts WHERE account_id = '${id.pia}' AND decided_at IS NOT NULL AND evidence_deleted_at IS NULL`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.pia}' AND purpose = 'verification' AND object_key IS NOT NULL`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM photo_verification_attempts WHERE account_id = '${id.pia}' AND state IN ('Approved', 'Rejected')`), '3', 'decision records kept'); pass();
    // ---- The screens (issue 530 part 2): the member's code on /me/verification; the admin Photo Identified tab.
    await libraryPhoto(id.ned).then(m => place('ned', [m]));
    const memberPage = await (await browser.newContext({ viewport: { width: 1280, height: 900 } })).newPage();
    await memberPage.goto(web + '/account/sign-in?intro=done'); await memberPage.fill('#username', members.ned.email); await memberPage.fill('#password', members.ned.password);
    await Promise.all([memberPage.waitForURL(u => !u.href.includes('/realms/')), memberPage.click('#kc-login')]);
    await memberPage.goto(web + '/me/verification');
    await memberPage.locator('[data-photo-check=none] [data-photo-check-start]').waitFor({ timeout: 20000 }); pass();
    assert.match(await memberPage.locator('#photo-check-heading + p').innerText(), /isn't an ID check/); pass();
    await memberPage.click('[data-photo-check-start]');
    await memberPage.locator('[data-photo-check=issued] [data-photo-check-code]').waitFor({ timeout: 10000 });
    const shownCode = (await memberPage.locator('.photo-check-value').innerText()).trim();
    assert.match(shownCode, /^[ACDEFHJKMNPQRTUVWXY3479]{4}$/); pass();
    assert.equal(await memberPage.locator('[data-photo-check-file]').getAttribute('accept'), 'image/jpeg,image/png,image/webp'); pass();
    // PR 610 review: the web BFF lets a camera-sized photo through to the verification API (a 1 MB body here). The owned API
    // has Photo Verified off, so it answers 503; the BFF's small-body limit would have answered 413 first.
    const bffStatus = await memberPage.evaluate(async () => {
      const form = new FormData(); form.append('file', new Blob([new Uint8Array(1024 * 1024)], { type: 'image/jpeg' }), 'paper.jpg'); form.append('code', 'XXXX');
      return (await fetch('/bff/api/me/verification/photo', { method: 'POST', body: form, headers: { 'X-Swoovr-Bff': '1' } })).status;
    });
    assert.notEqual(bffStatus, 413, 'not refused by the web for its size'); pass();
    for (const width of [1280, 390]) { await memberPage.setViewportSize({ width, height: 900 }); if (output) await memberPage.screenshot({ path: `${output}/photo-verified-code-${width}.png`, fullPage: true }); }
    // Sent through the extra API (the owned hosts have no malware scanner); the page then says it's being checked.
    assert.equal((await send('ned', shownCode, 540)).body.state, 'submitted');
    await memberPage.reload(); await memberPage.locator('[data-photo-check=submitted]').waitFor({ timeout: 20000 }); pass();
    await waitFor(async () => !!(await call(sup, '/api/admin/photo-verification')).body.find(i => i.accountId === id.ned));
    const adminPage = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
    // PR 610 review: a reviewer who hasn't acknowledged the safety notice sees it first, with no evidence, and acknowledges here.
    await adminPage.goto(admin + '/sign-in'); await adminPage.fill('#username', fresh2.email); await adminPage.fill('#password', fresh2.password); await adminPage.click('#kc-login');
    await fillStaffOtp(adminPage, fresh2); await adminPage.waitForURL(u => u.href.startsWith(admin) && !u.href.includes('/realms/'), { timeout: 30000 });
    await adminPage.goto(admin + '/media/photo-identified');
    await adminPage.locator('[data-photo-identified=ack]').waitFor({ timeout: 20000 }); pass();
    assert.equal(await adminPage.locator('[data-photo-identified-evidence]').count(), 0, 'no evidence before the acknowledgement'); pass();
    if (output) await adminPage.screenshot({ path: `${output}/photo-identified-ack-1440.png`, fullPage: true });
    await adminPage.locator('[data-photo-identified-ack-confirm]').check();
    await Promise.all([adminPage.waitForLoadState('load'), adminPage.click('[data-photo-identified-ack]')]);
    const card = adminPage.locator('[data-photo-identified-item]').filter({ hasText: 'Fictional Pv ned' });
    await card.waitFor({ timeout: 20000 });
    assert.equal((await card.locator('[data-photo-identified-code]').innerText()).trim(), shownCode); pass();
    assert.ok(await card.locator('[data-photo-identified-evidence]').evaluate(img => img.complete && img.naturalWidth > 0), 'the evidence loads for the reviewer'); pass();
    assert.equal(await card.locator('[data-photo-identified-profile]').count(), 1); pass();
    if (output) await adminPage.screenshot({ path: `${output}/photo-identified-1440.png`, fullPage: true });
    await card.locator('[data-photo-identified-approve]').check();
    await Promise.all([adminPage.waitForURL(u => u.href.includes('decided=approve'), { timeout: 20000 }), card.locator('[data-photo-identified-save]').click()]);
    assert.equal(await adminPage.locator('[data-photo-identified-item]').filter({ hasText: 'Fictional Pv ned' }).count(), 0); pass();
    await memberPage.reload(); await memberPage.locator('[data-photo-check=verified]').waitFor({ timeout: 20000 }); pass();
    if (output) await memberPage.screenshot({ path: `${output}/photo-verified-done-390.png`, fullPage: true });
    console.log(`PASS: Photo Verified (issue 530), ${passed} checks.`);
  } finally {
    await stopExtra();
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
