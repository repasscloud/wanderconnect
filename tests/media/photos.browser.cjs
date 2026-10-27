// Local acceptance for slice AH (D201, D203; policy §5, §6, §10; procedure §4; #85, #76, #91): photos with media
// quarantine. Every upload is validated, stripped of EXIF/GPS, re-encoded, stored privately (R2), screened by the real
// image-moderation-api and shown only once it passes. Uncertain and blocked outcomes are driven by an extra API on
// 127.0.0.1:5090 started with the labelled Development-only override (Media__Screening__Simulate), and the fail-closed
// paths by an extra API pointed at an unreachable or nonsensical screener. Synthetic images only (solid colours,
// gradients, noise, text): never people, never explicit or abuse imagery. Every person is fictional. Objects created in
// the owner's R2 bucket are deleted at the end (removed photos, released holds, then `accounts purge`).
// Needs the local stack, image-moderation-api on :8080, hosts from this checkout started with
// `Phone__Required=false Accounts__ReauthSeconds=45 bash .local/run-hosts.sh` and the enrolled fictional root admin.
const assert = require('node:assert/strict');
const http = require('node:http');
const crypto = require('node:crypto');
const { execFile, spawn } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, mailTo } =
  require('../auth/local-tokens.cjs');
const { synthetic, withExif, jpegSize, upload } = require('./synthetic-images.cjs');

const ADELAIDE = 2078025, EXTRA = 'http://127.0.0.1:5090';
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

async function extraApi(env) {
  await stopExtra();
  await new Promise((resolve, reject) => {
    const child = spawn('bash', ['.local/run-hosts.sh'], { env: { Phone__Required: 'false', Accounts__ReauthSeconds: '45', ...process.env, SWOOVR_EXTRA_API_PORT: '5090', ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let out = ''; child.stdout.on('data', d => out += d);
    child.on('exit', code => code === 0 ? resolve() : reject(new Error('extra api: ' + out)));
  });
}
async function stopExtra() {
  await new Promise(resolve => execFile('bash', ['-c', 'lsof -ti tcp:5090 -sTCP:LISTEN | xargs kill 2>/dev/null; true'], () => resolve()));
  for (let i = 0; i < 30; i++) { try { await fetch(EXTRA + '/health/live'); await sleep(500); } catch { return; } }
}
/** `accounts purge` with its exit code and output (the helper throws on exit 1, which a waiting deletion elsewhere can cause). */
async function purge(extra = {}) {
  const env = { ...process.env, ConnectionStrings__Swoovr: `Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=${await localEnv('SWOOVR_DB_PASSWORD')}`,
    DOTNET_ENVIRONMENT: 'Development', Founders__Secrets__1: await localEnv('SWOOVR_FOUNDER_SECRET_V1'), MemberAdmin__ClientSecret: await localEnv('SWOOVR_MEMBERS_API_ADMIN_SECRET') ?? '',
    Media__R2__Endpoint: await localEnv('SWOOVR_R2_ENDPOINT'), Media__R2__Bucket: await localEnv('SWOOVR_R2_BUCKET'),
    Media__R2__AccessKeyId: await localEnv('SWOOVR_R2_ACCESS_KEY_ID'), Media__R2__SecretAccessKey: await localEnv('SWOOVR_R2_SECRET_ACCESS_KEY'), ...extra };
  return new Promise(resolve => execFile('dotnet', ['src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll', 'accounts', 'purge'], { env },
    (error, stdout, stderr) => resolve({ code: error ? error.code ?? 1 : 0, out: stdout + stderr })));
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const canvas = await browser.newPage();
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  const stub = http.createServer((req, res) => {
    if (stub.mode === 'not-ready' && req.url === '/ready') { res.writeHead(503); return res.end('{}'); }
    if (req.url === '/ready') { res.writeHead(200, { 'Content-Type': 'application/json' }); return res.end('{"ready":true}'); }
    req.resume(); req.on('end', () => { res.writeHead(200, { 'Content-Type': 'application/json' }); res.end('{"status":"completed","results":{"nudenet":{"status":"completed","detections":[{"class":"SOMETHING_NEW","score":0.1}]},"nsfw":{"status":"completed","is_nsfw":false},"arachnid":{"status":"disabled"}}}'); });
  });
  await new Promise(resolve => stub.listen(5099, '127.0.0.1', resolve));
  const accounts = [];
  try {
    // People: Ana uploads; Ben views in Friends; Cam is blocked by Ana; Dee is restricted; Eve deletes her account.
    const names = ['ana', 'ben', 'cam', 'dee', 'eve'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('photo-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tk = {}, id = {}, pub = {};
    for (const n of names) {
      tk[n] = await memberToken(browser, members[n]);
      await call(tk[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Photo ' + n, homeCityId: ADELAIDE } });
      await matchReady(tk[n]);
      await call(tk[n], '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      pub[n] = (await call(tk[n], '/api/v1/me/profile')).body.publicId;
      accounts.push(id[n]);
    }
    await call(tk.ana, '/api/v1/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
    assert.ok((await call(tk.ana, '/api/v1/blocks', { method: 'POST', body: { profileId: pub.cam } })).status < 300); pass();
    const root = await rootAdminToken(browser);
    const staff = { adm: await createStaff('photo-adm'), sup: await createStaff('photo-sup'), leg: await createStaff('photo-leg') };
    for (const [n, role] of [['adm', 'admin'], ['sup', 'support'], ['leg', 'legal']])
      assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {}, sid = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/v1/session'); sid[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(st[n]).sub}'`); }
    const up = (who, bytes, opts = {}) => upload((opts.base ?? api) + '/api/v1/me/photos', bytes, { token: tk[who], ...opts });
    const mine = who => call(tk[who], '/api/v1/me/photos');
    const settle = async (who, photoId, from = 'checking') => {
      for (let i = 0; i < 40; i++) { const p = (await mine(who)).body.photos.find(x => x.id === photoId); if (p && p.state !== from) return p; await sleep(750); }
      throw new Error('photo did not settle');
    };
    const jpeg = async (seed, kind = 'gradient', width = 640, height = 480) => synthetic(canvas, { kind, seed, width, height, type: 'image/jpeg' });

    // ---- Refusals before anything is stored.
    assert.equal((await upload(api + '/api/v1/me/photos', await jpeg(1))).status, 401); pass();
    assert.equal((await upload(api + '/api/v1/me/photos', await jpeg(1), { token: root })).status, 403, 'staff realm'); pass();
    assert.deepEqual(code(await up('ana', Buffer.from('GIF89a' + 'x'.repeat(400)), { type: 'image/gif', name: 'x.gif' })), [415, 'photo-unsupported']); pass();
    assert.deepEqual(code(await up('ana', await synthetic(canvas, { width: 120, height: 120 }), { type: 'image/png' })), [400, 'photo-invalid']); pass();
    assert.deepEqual(code(await up('ana', Buffer.alloc(11 * 1024 * 1024, 1))), [413, 'photo-too-big']); pass();
    const broken = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), crypto.randomBytes(500)]);
    assert.deepEqual(code(await up('ana', broken, { type: 'image/png' })), [400, 'photo-invalid']); pass();
    const raw = await fetch(api + '/api/v1/me/photos', { method: 'POST', headers: { Authorization: 'Bearer ' + tk.ana, 'Content-Type': 'application/json' }, body: '{"file":"x"}' });
    assert.equal(raw.status, 400, 'not a form'); pass();
    assert.deepEqual(code(await up('ana', await jpeg(2), { replaces: crypto.randomUUID() })), [404, 'unknown-photo']); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.ana}'`), '0', 'nothing stored by a refusal'); pass();

    // ---- Passed (real screener): EXIF and GPS stripped, re-encoded, turned upright, hashed, private object, main photo.
    const original = withExif(await jpeg(3), 6);
    assert.ok(original.includes(Buffer.from('SWOOVRGPS'))); pass();
    let r = await up('ana', original);
    assert.equal(r.status, 202); assert.equal(r.body.state, 'checking'); assert.equal(r.body.link, null); pass();
    const first = r.body.id;
    let photo = await settle('ana', first);
    assert.equal(photo.state, 'approved', JSON.stringify(photo)); pass();
    const row = (await sql(`SELECT status, object_key, screener, known_content_checked, encode(sha256, 'hex'), encode(upload_sha256, 'hex') FROM media_items WHERE id = '${first}'`)).split('|');
    assert.deepEqual(row.slice(0, 4), ['Passed', 'photos/' + first.replaceAll('-', ''), 'image-moderation-api', 'f']); pass();
    assert.equal(row[5], sha(original), 'upload hash kept'); pass();
    let image = await fetch(web + '/media/' + photo.link.split('/').pop());
    const served = Buffer.from(await image.arrayBuffer());
    assert.equal(image.status, 200); assert.equal(image.headers.get('content-type'), 'image/jpeg'); pass();
    assert.ok(!served.includes(Buffer.from('Exif')) && !served.includes(Buffer.from('SWOOVRGPS')), 'metadata gone'); pass();
    assert.deepEqual(jpegSize(served), { width: 480, height: 640 }, 'orientation applied'); pass();
    assert.equal(sha(served), row[4], 'stored hash is of the served bytes'); pass();
    let own = (await mine('ana')).body;
    assert.equal(own.main, first); assert.deepEqual(own.photos[0].scopes, ['main']); pass();
    // PNG, noise and text images pass too (synthetic only).
    const extraIds = [];
    for (const [i, kind] of ['solid', 'noise', 'text'].entries()) {
      r = await up('ana', await synthetic(canvas, { kind, seed: 10 + i, type: 'image/png' }), { type: 'image/png', name: 'x.png' });
      assert.equal(r.status, 202); extraIds.push(r.body.id);
    }
    for (const x of extraIds) assert.equal((await settle('ana', x)).state, 'approved');
    pass();

    // ---- Placement: one main, up to six per mode, own approved photos only.
    const place = (who, scope, mediaIds) => call(tk[who], '/api/v1/me/photos/placement', { method: 'PUT', body: { scope, mediaIds } });
    assert.equal((await place('ana', 'friends', [extraIds[0], extraIds[1]])).status, 200); pass();
    assert.equal((await place('ana', 'dating', [extraIds[2]])).status, 200); pass();
    assert.deepEqual(code(await place('ana', 'main', [first, extraIds[0]])), [400, 'invalid-placement']); pass();
    assert.deepEqual(code(await place('ana', 'nonsense', [first])), [400, 'invalid-placement']); pass();
    assert.deepEqual(code(await place('ana', 'friends', [first, first])), [400, 'invalid-placement']); pass();
    assert.deepEqual(code(await place('ana', 'friends', Array.from({ length: 7 }, () => crypto.randomUUID()))), [400, 'invalid-placement']); pass();
    assert.deepEqual(code(await place('ben', 'friends', [first])), [409, 'photo-not-approved'], "someone else's photo"); pass();
    assert.deepEqual(code(await place('ana', 'friends', [crypto.randomUUID()])), [409, 'photo-not-approved']); pass();
    const tampered = await fetch(api + '/api/v1/me/photos/placement', { method: 'PUT', headers: { Authorization: 'Bearer ' + tk.ana, 'Content-Type': 'application/json' }, body: '{"scope":"main","mediaIds":"all"}' });
    assert.equal(tampered.status, 400); pass();
    assert.deepEqual(code(await call(tk.ben, `/api/v1/me/photos/${first}`, { method: 'DELETE' })), [404, 'unknown-photo'], "can't delete someone else's"); pass();

    // ---- What others see: Ben (Friends) gets the main photo and Friends photos, never the Dating one; Cam (blocked) nothing.
    const explore = await call(tk.ben, '/api/v1/explore?mode=friends');
    const card = explore.body.people.find(p => p.id === pub.ana);
    assert.equal(card?.photo?.id, first, 'card shows the main photo'); pass();
    assert.equal((await fetch(web + '/media/' + card.photo.link.split('/').pop())).status, 200); pass();
    let person = (await call(tk.ben, `/api/v1/profiles/${pub.ana}?mode=friends`)).body;
    assert.equal(person.photo.id, first); pass();
    const friendsPhotos = person.modes.find(m => m.id === 'friends').photos.map(p => p.id);
    assert.deepEqual(friendsPhotos, [extraIds[0], extraIds[1]]); pass();
    assert.ok(!JSON.stringify(person).includes(extraIds[2]), 'a Dating photo never shows in Friends'); pass();
    assert.equal((await call(tk.cam, `/api/v1/profiles/${pub.ana}?mode=friends`)).status, 404, 'blocked: no profile, no links'); pass();
    assert.ok(!JSON.stringify((await call(tk.cam, '/api/v1/explore?mode=friends')).body).includes(first)); pass();
    // Links: tampered, wrong variant, unknown or random all read as not found; the web host only passes member links.
    const token = card.photo.link.split('/').pop();
    assert.equal((await fetch(api + '/api/v1/media/' + token.slice(0, -2) + 'AA')).status, 404); pass();
    assert.equal((await fetch(api + '/api/v1/media/' + token.replace('.member.', '.review.'))).status, 404); pass();
    assert.equal((await fetch(api + '/api/v1/media/nonsense')).status, 404); pass();
    assert.equal((await fetch(web + '/media/' + token.replace('.member.', '.review.'))).status, 404); pass();
    // Profile versions carry photo ids and hashes (#91).
    const core = await sql(`SELECT content_json FROM profile_versions WHERE account_id = '${id.ana}' AND mode_id = 'core' ORDER BY captured_at DESC LIMIT 1`);
    assert.ok(core.includes(first) && core.includes(row[4]), 'core version names the main photo and hash'); pass();
    const friendsVersion = await sql(`SELECT content_json FROM profile_versions WHERE account_id = '${id.ana}' AND mode_id = 'friends' ORDER BY captured_at DESC LIMIT 1`);
    assert.ok(friendsVersion.includes(extraIds[0]) && !friendsVersion.includes(extraIds[2])); pass();

    // ---- Replacement is screened again and takes over the original's places once it passes.
    r = await up('ana', await jpeg(20), { replaces: first });
    assert.equal(r.status, 202); assert.equal(r.body.replaces, first); pass();
    assert.equal((await settle('ana', r.body.id)).state, 'approved'); pass();
    own = (await mine('ana')).body;
    assert.equal(own.main, r.body.id); assert.ok(!own.photos.some(p => p.id === first)); pass();
    assert.equal((await fetch(web + '/media/' + token)).status, 404, "the replaced photo's link stops working"); pass();
    for (let i = 0; i < 20 && await sql(`SELECT coalesce(object_key, 'none') FROM media_items WHERE id = '${first}'`) !== 'none'; i++) await sleep(500);
    assert.equal(await sql(`SELECT status || '|' || coalesce(object_key, 'none') FROM media_items WHERE id = '${first}'`), 'Removed|none', 'replaced object deleted'); pass();
    const mainId = r.body.id;

    // ---- Limits and barred members.
    await sql(`INSERT INTO media_items (id, account_id, status, sha256, upload_sha256, content_type, width, height, bytes, created_at, screen_attempts, known_content_checked)
      SELECT gen_random_uuid(), '${id.ben}', 'Removed', ''::bytea, ''::bytea, 'image/jpeg', 1, 1, 1, now(), 0, false FROM generate_series(1, 20)`);
    assert.deepEqual(code(await up('ben', await jpeg(30))), [429, 'upload-limit']); pass();
    await sql(`UPDATE media_items SET created_at = now() - interval '2 hours', status = 'Rejected' WHERE account_id = '${id.ben}'`);
    await sql(`INSERT INTO media_items (id, account_id, status, sha256, upload_sha256, content_type, width, height, bytes, created_at, screen_attempts, known_content_checked)
      SELECT gen_random_uuid(), '${id.ben}', 'Rejected', ''::bytea, ''::bytea, 'image/jpeg', 1, 1, 1, now() - interval '2 hours', 0, false FROM generate_series(1, 10)`);
    assert.deepEqual(code(await up('ben', await jpeg(31))), [409, 'photo-limit']); pass();
    await sql(`DELETE FROM media_items WHERE account_id = '${id.ben}' AND object_key IS NULL`);
    assert.equal((await call(st.adm, `/api/v1/admin/accounts/${id.dee}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction', days: 2 } })).status, 200);
    assert.equal((await up('dee', await jpeg(32))).status, 403, 'restricted members are barred'); pass();

    // ---- Staff routes: admins only.
    assert.equal((await call(null, '/api/v1/admin/media/review')).status, 401); pass();
    assert.equal((await call(tk.ana, '/api/v1/admin/media/review')).status, 403, 'member'); pass();
    assert.equal((await call(st.sup, '/api/v1/admin/media/review')).status, 403, 'support'); pass();
    assert.equal((await call(st.leg, '/api/v1/admin/media/review')).status, 403, 'legal'); pass();
    assert.equal((await call(st.adm, '/api/v1/admin/media/review')).status, 200); pass();

    // ---- Uncertain (simulated, labelled, Development only): restricted review, never visible meanwhile.
    await extraApi({ Media__Screening__Simulate: 'uncertain', Media__Screening__Worker: 'true' });
    r = await up('ana', await jpeg(40), { base: EXTRA });
    assert.equal(r.status, 202, JSON.stringify(r.body));
    const unsure = r.body.id;
    photo = await settle('ana', unsure);
    assert.equal(photo.state, 'in-review'); assert.equal(photo.link, null); pass();
    assert.equal(await sql(`SELECT screener FROM media_items WHERE id = '${unsure}'`), 'simulated', 'the override is labelled'); pass();
    assert.deepEqual(code(await place('ana', 'friends', [unsure])), [409, 'photo-not-approved']); pass();
    assert.ok(!JSON.stringify((await call(tk.ben, `/api/v1/profiles/${pub.ana}?mode=friends`)).body).includes(unsure), 'not visible to others'); pass();
    let queue = (await call(st.adm, '/api/v1/admin/media/review')).body;
    assert.ok(queue.items.some(i => i.id === unsure && i.reason === 'nudity-signal')); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/media/${unsure}/view`, { method: 'POST', body: { purpose: '' } })), [400, 'purpose-required']); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/media/${unsure}/view`, { method: 'POST', body: { purpose: 'x' } })).status, 403); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/media/${crypto.randomUUID()}/view`, { method: 'POST', body: { purpose: 'x' } })), [404, 'unknown-photo']); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/media/${mainId}/view`, { method: 'POST', body: { purpose: 'x' } })), [404, 'unknown-photo'], 'approved photos are not for casual viewing'); pass();
    // A conflict of interest keeps the reviewer away.
    await call(st.adm, '/api/v1/admin/access/exclusions', { method: 'POST', body: { staffAccountId: '00000000-0000-0000-0000-000000000000', subjectAccountId: id.ana, reason: 'Fictional: knows this member' } });
    const conflicted = await call(st.adm, `/api/v1/admin/media/${unsure}/view`, { method: 'POST', body: { purpose: 'Fictional review' } });
    assert.deepEqual(code(conflicted), [403, 'conflict-of-interest']); pass();
    await sql(`DELETE FROM staff_exclusions WHERE staff_account_id = '${sid.adm}'`);
    const view = await call(st.adm, `/api/v1/admin/media/${unsure}/view`, { method: 'POST', body: { purpose: 'Fictional review of a screening flag' } });
    assert.equal(view.status, 200); pass();
    assert.equal((await fetch(api + '/' + view.body.link)).status, 200, 'the review link opens'); pass();
    assert.equal((await fetch(web + '/media/' + view.body.link.split('/').pop())).status, 404, 'the member site never passes review links'); pass();
    assert.equal(await sql(`SELECT purpose FROM media_review_views WHERE media_id = '${unsure}' ORDER BY id DESC LIMIT 1`), 'Fictional review of a screening flag'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM sensitive_access_log WHERE resource = 'media' AND resource_id = '${unsure}' AND action = 'view'`)) >= 1, 'view logged'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id = '${unsure}' AND detail LIKE '%Fictional%'`), '0', 'the purpose stays out of the audit'); pass();
    assert.deepEqual(code(await call(st.adm, `/api/v1/admin/media/${unsure}/decision`, { method: 'POST', body: { decision: 'publish' } })), [400, 'invalid-decision']); pass();
    assert.equal((await call(st.sup, `/api/v1/admin/media/${unsure}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 403); pass();
    // Admin app: blurred until revealed, then approved there.
    const adminPage = await browser.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.adm.email); await adminPage.fill('#password', staff.adm.password); await adminPage.click('#kc-login');
    await adminPage.waitForURL(admin + '/**');
    await adminPage.goto(admin + '/media');
    await adminPage.waitForSelector(`[data-media-item="${unsure}"]`); pass();
    await adminPage.fill(`[data-purpose="${unsure}"]`, 'Fictional review in the admin app');
    await adminPage.click(`[data-view="${unsure}"]`);
    await adminPage.waitForSelector(`[data-media-image="${unsure}"]`);
    await adminPage.waitForFunction(id => document.querySelector(`[data-media-image="${id}"]`).naturalWidth > 0, unsure);
    assert.match(await adminPage.$eval(`[data-media-image="${unsure}"]`, e => getComputedStyle(e).filter), /blur/, 'blurred by default'); pass();
    await adminPage.click(`label[for="reveal-${unsure}"]`);
    assert.equal(await adminPage.$eval(`[data-media-image="${unsure}"]`, e => getComputedStyle(e).filter), 'none'); pass();
    if (output) await adminPage.screenshot({ path: output + '/admin-photo-review.png', fullPage: true });
    await adminPage.click(`[data-approve="${unsure}"]`);
    await adminPage.waitForSelector('[data-result="ok"]'); pass();
    assert.equal((await mine('ana')).body.photos.find(p => p.id === unsure).state, 'approved'); pass();
    // A second uncertain photo is rejected: off the profile, object deleted, reads as "not approved".
    r = await up('ana', await jpeg(41), { base: EXTRA });
    const refused = r.body.id;
    await settle('ana', refused);
    assert.equal((await call(st.adm, `/api/v1/admin/media/${refused}/decision`, { method: 'POST', body: { decision: 'reject', reason: 'Fictional: explicit content' } })).status, 200); pass();
    assert.equal((await mine('ana')).body.photos.find(p => p.id === refused).state, 'not-approved'); pass();
    assert.equal(await sql(`SELECT coalesce(object_key, 'none') FROM media_items WHERE id = '${refused}'`), 'none'); pass();

    // ---- Blocked (simulated known-content match): sealed evidence, Serious Safety Incident, provisional hold, ids-only escalation.
    await extraApi({ Media__Screening__Simulate: 'blocked' });
    r = await up('ana', await jpeg(50), { base: EXTRA });
    const blocked = r.body.id;
    photo = await settle('ana', blocked);
    assert.equal(photo.state, 'not-approved', 'reads exactly like a refusal'); assert.equal(photo.link, null); pass();
    const [status, objectKey, caseId, holdId] = (await sql(`SELECT status, object_key, case_id, hold_id FROM media_items WHERE id = '${blocked}'`)).split('|');
    assert.equal(status, 'Blocked'); assert.equal(objectKey, 'evidence/' + blocked.replaceAll('-', '')); pass();
    assert.equal(await sql(`SELECT queue || '|' || priority || '|' || coalesce(system_flag, '') FROM moderation_cases WHERE id = '${caseId}'`), 'Admin|Urgent|media-known-content'); pass();
    assert.equal(await sql(`SELECT source || '|' || status FROM legal_holds WHERE id = '${holdId}'`), 'media-screening|Provisional'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}' AND kind = 'media-item'`)) >= 1, 'media items captured'); pass();
    assert.equal(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}' AND sealed::text LIKE '%photos/%'`), '0', 'sealed, nothing in clear'); pass();
    const escalation = (await mailTo(await localEnv('SWOOVR_ESCALATION_EMAIL')))[0] ?? '';
    assert.ok(escalation.includes(caseId) && escalation.includes('media-known-content'), 'escalated with the case id'); pass();
    assert.ok(!escalation.includes(blocked) && !escalation.includes('evidence/') && !escalation.includes(id.ana), 'ids of the case only, no media or member'); pass();
    // Nobody can look at it: not in the review queue, no view, no decision, no link, not in the export.
    queue = (await call(st.adm, '/api/v1/admin/media/review')).body;
    assert.ok(!queue.items.some(i => i.id === blocked)); pass();
    for (const who of ['adm', 'leg', 'sup'])
      assert.ok([403, 404].includes((await call(st[who], `/api/v1/admin/media/${blocked}/view`, { method: 'POST', body: { purpose: 'Fictional' } })).status), who);
    pass();
    assert.equal((await call(st.adm, `/api/v1/admin/media/${blocked}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 404); pass();
    // The member removing it hides it from their list; it stays sealed evidence.
    assert.equal((await call(tk.ana, `/api/v1/me/photos/${blocked}`, { method: 'DELETE' })).status, 200);
    assert.equal(await sql(`SELECT status || '|' || object_key FROM media_items WHERE id = '${blocked}'`), `Blocked|${objectKey}`); pass();

    // ---- Fail closed: unreachable, not ready, or an answer Swoovr doesn't understand stays pending and invisible.
    for (const [mode, base, reason] of [['down', 'http://127.0.0.1:5098', 'unreachable'], ['not-ready', 'http://127.0.0.1:5099', 'not-ready'], ['odd', 'http://127.0.0.1:5099', 'unknown-label']]) {
      stub.mode = mode;
      await extraApi({ Media__Screening__BaseUrl: base });
      r = await up('ana', await jpeg(60 + reason.length), { base: EXTRA });
      for (let i = 0; i < 20 && await sql(`SELECT coalesce(last_screen_error, '') FROM media_items WHERE id = '${r.body.id}'`) === ''; i++) await sleep(500);
      assert.equal(await sql(`SELECT status || '|' || last_screen_error FROM media_items WHERE id = '${r.body.id}'`), `Pending|${reason}`, mode);
      assert.equal((await mine('ana')).body.photos.find(p => p.id === r.body.id).state, 'checking');
      assert.deepEqual(code(await place('ana', 'friends', [r.body.id])), [409, 'photo-not-approved']);
      pass();
    }
    await stopExtra();
    // The override is refused outside Development: the host won't start.
    const production = await new Promise(resolve => execFile('dotnet', ['src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll'], { timeout: 30000,
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production', ASPNETCORE_URLS: 'http://127.0.0.1:5097', Media__Screening__Simulate: 'blocked' } },
      (error, stdout, stderr) => resolve({ code: error?.code, out: stdout + stderr })));
    assert.ok(production.code !== 0 && production.out.includes('Development only'), 'Simulate refused outside Development'); pass();

    // ---- The member's export includes their photos (approved ones with the image; refusals all look the same, without it).
    tk.ana = await memberToken(browser, members.ana);
    const ticket = await call(tk.ana, '/api/v1/me/export', { method: 'POST' });
    assert.ok(ticket.status < 300, JSON.stringify(ticket.body));
    const exported = await (await fetch(api + '/' + ticket.body.path, { headers: { Authorization: 'Bearer ' + tk.ana } })).json();
    const mainExport = exported.photos.find(p => p.id === mainId);
    assert.ok(mainExport.dataBase64 && sha(Buffer.from(mainExport.dataBase64, 'base64')) === mainExport.sha256); pass();
    const refusedExport = exported.photos.find(p => p.id === refused);
    assert.equal(refusedExport.state, 'not-approved'); assert.equal(refusedExport.dataBase64, null); pass();
    assert.ok(!exported.photos.some(p => p.id === blocked) || exported.photos.find(p => p.id === blocked).dataBase64 === null); pass();

    // ---- Web screens: Eve uploads on /me/photos at phone width; Ben sees Ana's photos on her person page.
    const phone = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const evePage = await phone.newPage();
    await evePage.goto(web + '/account/sign-in?intro=done');
    await evePage.fill('#username', members.eve.email); await evePage.fill('#password', members.eve.password);
    await Promise.all([evePage.waitForURL(web + '/account'), evePage.click('#kc-login')]);
    await evePage.locator('[data-photos-link]').first().evaluate(a => a.click());
    await evePage.waitForSelector('[data-photos-panel="ready"]');
    await evePage.setInputFiles('[data-photo-input]', { name: 'synthetic.png', mimeType: 'image/png', buffer: await synthetic(canvas, { kind: 'gradient', seed: 70 }) });
    await evePage.waitForSelector('[data-photo-state="approved"]', { timeout: 45000 }); pass();
    await evePage.waitForFunction(() => document.querySelector('[data-photo-image]')?.naturalWidth > 0);
    assert.equal(await evePage.getAttribute('[data-photo-state="approved"]', 'data-photo-main'), 'yes'); pass();
    await evePage.locator('[data-mode-photo^="friends:"]').first().evaluate(e => e.click());
    await evePage.waitForSelector('[data-mode-photos="friends"][data-mode-photo-count="1"]'); pass();
    if (output) await evePage.screenshot({ path: output + '/web-photos.png', fullPage: true });
    const benPage = await phone.newPage();
    await phone.clearCookies();
    await benPage.goto(web + '/account/sign-in?intro=done');
    await benPage.fill('#username', members.ben.email); await benPage.fill('#password', members.ben.password);
    await Promise.all([benPage.waitForURL(web + '/account'), benPage.click('#kc-login')]);
    await benPage.goto(`${web}/people/${pub.ana}?mode=friends`);
    await benPage.waitForSelector('[data-person-photo="main"]');
    await benPage.waitForFunction(() => document.querySelector('[data-person-photo="main"]').naturalWidth > 0); pass();
    assert.equal(await benPage.locator('[data-person-photo="friends"]').count(), 2); pass();
    assert.equal(await benPage.locator('[data-person-photo="dating"]').count(), 0); pass();
    if (output) await benPage.screenshot({ path: output + '/web-person-photos.png', fullPage: true });
    await benPage.goto(web + '/explore?mode=friends');
    await benPage.waitForSelector('[data-tile-photo]'); pass();
    await phone.close();

    // ---- Deletion: Eve deletes her account now; `accounts purge` removes her photo object (nothing holds it).
    const eveObject = await sql(`SELECT object_key FROM media_items WHERE account_id = '${id.eve}' AND status = 'Passed' LIMIT 1`);
    assert.ok(eveObject.startsWith('photos/')); pass();
    tk.eve = await memberToken(browser, members.eve);
    r = await call(tk.eve, '/api/v1/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } });
    assert.ok(r.status < 300, JSON.stringify(r.body));
    // Fail closed first: with hold status unavailable nothing is deleted.
    let run = await purge({ Evidence__HoldCheckUnavailable: 'true' });
    assert.equal(run.code, 1); assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.eve}' AND object_key IS NOT NULL`), '1'); pass();
    run = await purge();
    assert.match(run.out, /Photos: \d+ objects deleted/); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.eve}' AND object_key IS NOT NULL`), '0', 'deleted account: objects gone'); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_placements WHERE account_id = '${id.eve}'`), '0'); pass();

    // ---- Clean up the owner's bucket: everyone removes their photos; the hold over Ana is released by a legal officer
    // (not the placer, overlaps checked); then `accounts purge` deletes every remaining object, the sealed one included.
    for (const n of ['ana', 'ben', 'cam', 'dee'])
      for (const p of (await mine(n)).body?.photos ?? []) await call(tk[n], `/api/v1/me/photos/${p.id}`, { method: 'DELETE' });
    assert.equal(await sql(`SELECT object_key FROM media_items WHERE id = '${blocked}'`), objectKey, 'held: the sealed object stays'); pass();
    assert.notEqual(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.ana}' AND status = 'Removed' AND object_key IS NOT NULL`), '0', 'held: removed objects wait'); pass();
    r = await call(st.leg, `/api/v1/admin/holds/${holdId}/release`, { method: 'POST', body: { reason: 'Fictional acceptance test finished', overlapsChecked: true } });
    assert.equal(r.status, 200, JSON.stringify(r.body)); pass();
    run = await purge();
    assert.match(run.out, /Photos: \d+ objects deleted/); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id IN (${accounts.map(a => `'${a}'`).join(',')}) AND object_key IS NOT NULL
      AND status NOT IN ('Pending')`), '0', 'no objects left in the bucket'); pass();

    console.log(`PASS: photos and media quarantine (${passed} checks).`);
  } finally {
    await stopExtra();
    stub.close();
    // Pending fail-closed uploads: remove them so the hosts' sweep never publishes them, and delete their objects.
    if (accounts.length) {
      await sql(`UPDATE media_items SET status = 'Removed', removed_at = now(), next_screen_at = NULL WHERE account_id IN (${accounts.map(a => `'${a}'`).join(',')}) AND status = 'Pending'`).catch(() => {});
      await purge().catch(() => {});
    }
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
