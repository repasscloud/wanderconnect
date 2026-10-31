const {expireFictionalReviews}=require('../retention/fixtures.cjs');
// Local acceptance for slice AL (#41 in part; policy §5, §14; D203): photos in chat through the media quarantine, a
// polite end without blocking, and conversation starters from shared intention chips. The recipient sees a photo only
// once it passed real screening (the owner's image-moderation-api); the sender sees its state; uncertain photos go to
// the admin review queue and blocked ones (the labelled Development-only override on an owned extra API on a random loopback port) become a
// Serious Safety Incident: sealed, held, escalated with ids only, never viewable. Delete for both, hide, report evidence
// with an admin case view, holds, `messages purge` and `accounts purge` (fail closed), the export and account deletion.
// Synthetic images only; every object the suite makes in the selected test store is deleted at the end. Fictional adults only.
const assert = require('node:assert/strict');
const fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os'),net=require('node:net'),{once}=require('node:events');
if(!process.env.SWOOVR_TEST_APP_DATABASE||!process.env.ConnectionStrings__Swoovr)throw Error('Chat photos require the owned runner.');
const { execFile, spawn } = require('node:child_process');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, admin, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady, mailTo, elevate } = require('../auth/local-tokens.cjs');
const { synthetic, withExif, upload } = require('../media/synthetic-images.cjs');

const ADELAIDE = 2078025;let EXTRA,extraProcess,extraFolder;
const output = process.env.SWOOVR_SCREENSHOTS;

async function extraApi(env) {
  await stopExtra();
  const probe=net.createServer();await new Promise(resolve=>probe.listen(0,'127.0.0.1',resolve));EXTRA='http://127.0.0.1:'+probe.address().port;await new Promise(resolve=>probe.close(resolve));
  extraFolder=await fs.mkdtemp(path.join(os.tmpdir(),'swoovr-chat-extra-'));await fs.chmod(extraFolder,0o700);const log=await fs.open(path.join(extraFolder,'api.log'),'w',0o600);
  extraProcess=spawn('dotnet',['bin/Debug/net10.0/Swoovr.Api.dll'],{cwd:path.resolve('src/Swoovr.Api'),env:{...process.env,...env,ASPNETCORE_URLS:EXTRA},stdio:['ignore',log.fd,log.fd]});await log.close();
  for(let n=0;n<150;n++){if(extraProcess.exitCode!==null||extraProcess.signalCode!==null)throw Error('Owned chat-photo API exited; inspect private log '+extraFolder);try{if((await fetch(EXTRA+'/health/live',{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await sleep(100);}
  throw Error('Owned chat-photo API readiness timeout.');
}
async function stopExtra() {
  if(extraProcess&&extraProcess.exitCode===null&&extraProcess.signalCode===null){const child=extraProcess,exited=once(child,'exit');child.kill('SIGTERM');await Promise.race([exited,sleep(5000)]);if(child.exitCode===null&&child.signalCode===null){child.kill('SIGKILL');await exited;}}
  extraProcess=null;if(extraFolder){await fs.rm(extraFolder,{recursive:true,force:true});extraFolder=null;}
}
/** Purge only the runner-owned database/store; never import real provider credentials. */
async function purge(extra = {}) {
  const env={...process.env,...extra};
  return new Promise(resolve=>execFile('dotnet',['bin/Debug/net10.0/Swoovr.Api.dll','accounts','purge'],{cwd:path.resolve('src/Swoovr.Api'),env},
    (error,stdout,stderr)=>resolve({code:error?error.code??1:0,out:stdout+stderr})));
}
const waitFor = async (predicate, ms = 60000) => { for (let i = 0; i < ms / 500; i++) { if (await predicate()) return true; await sleep(500); } return predicate(); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const canvas = await browser.newPage();
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const accounts = [];
  let holdIds = [];
  const legal = {};
  try {
    const root = await rootAdminToken(browser);
    // Ana sends; Ben receives; Cam isn't connected; Dee turned photos off; Res gets restricted; Eve deletes her account; Bot is a bot.
    const names = ['ana', 'ben', 'cam', 'dee', 'res', 'eve', 'bot'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('att-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tk = {}, id = {}, pid = {};
    const modes = (await (async () => { const t = await memberToken(browser, members.ana); tk.ana = t; return (await call(t, '/api/modes')).body; })());
    const chips = Object.fromEntries(modes.map(m => [m.id, m.intentions.map(i => i.id)]));
    for (const n of names) {
      tk[n] ??= await memberToken(browser, members[n]);
      const saved = await call(tk[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Att ' + n, homeCityId: ADELAIDE } });
      pid[n] = saved.body.publicId;
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      accounts.push(id[n]);
      if (n === 'bot') continue;
      for (const mode of ['dating', 'friends', 'activities']) await call(tk[n], `/api/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tk[n]);
    }
    // Chips: Ana and Ben share friends[0] and dating[0]; friends[1] is only Ana's; activities[0] is shared but they aren't connected there.
    const pick = (who, mode, list) => call(tk[who], `/api/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true, intentions: list } });
    await pick('ana', 'friends', [chips.friends[0], chips.friends[1]]); await pick('ben', 'friends', [chips.friends[0], chips.friends[2]]);
    await pick('ana', 'dating', [chips.dating[0]]); await pick('ben', 'dating', [chips.dating[0]]);
    await pick('ana', 'activities', [chips.activities[0]]); await pick('ben', 'activities', [chips.activities[0]]);
    await call(root, `/api/admin/accounts/${id.bot}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/admin/accounts/${id.bot}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } });
    await call(root, `/api/admin/accounts/${id.bot}/bot-modes`, { method: 'PUT', body: { modes: ['friends'] } });
    await call(tk.bot, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const connect = async (from, to, mode = 'friends') => {
      const r = await call(tk[from], '/api/requests', { method: 'POST', body: { to: pid[to], mode } });
      assert.equal(r.status, 201, `${from}->${to} ${JSON.stringify(r.body)}`);
      assert.equal((await call(tk[to], `/api/requests/${r.body.id}/accept`, { method: 'POST' })).status, 200);
    };
    for (const other of ['ben', 'dee', 'res', 'eve', 'bot']) await connect('ana', other);
    const start = async (who, to) => (await call(tk[who], '/api/conversations', { method: 'POST', body: { to: pid[to] } })).body.id;
    const ab = await start('ana', 'ben'), ad = await start('ana', 'dee'), ar = await start('ana', 'res'), ae = await start('ana', 'eve'), abot = await start('ana', 'bot');
    const list = async (who, c) => (await call(tk[who], `/api/conversations/${c}/messages`)).body?.items ?? [];
    const staff = { adm: await createStaff('att-adm'), sup: await createStaff('att-sup'), leg: await createStaff('att-leg') };
    for (const [n, role] of [['adm', 'admin'], ['sup', 'support'], ['leg', 'legal']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) { st[n] = await staffToken(browser, staff[n]); await call(st[n], '/api/session'); }

    // ---- Starters: only chips both picked in a mode they're connected in, at most three.
    let r = await call(tk.ana, `/api/conversations/${ab}/starters`);
    const label = (mode, i) => modes.find(m => m.id === mode).intentions.find(x => x.id === chips[mode][i]).label;
    assert.deepEqual(r.body.map(s => s.chip), [label('friends', 0)]); pass(); // not friends[1] (only Ana's), not activities (not connected there)
    assert.ok(r.body[0].text.includes(`“${label('friends', 0)}”`)); pass();
    await connect('ben', 'ana', 'dating');
    r = await call(tk.ben, `/api/conversations/${ab}/starters`);
    assert.deepEqual(r.body.map(s => s.chip).sort(), [label('dating', 0), label('friends', 0)].sort()); pass();
    assert.deepEqual(code(await call(tk.cam, `/api/conversations/${ab}/starters`)), [404, 'not-available']); pass();

    // ---- Refusals before anything is stored.
    const jpeg = await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', width: 640, height: 480, seed: 41, label: 'SWOOVR CHAT TEST' });
    const send = (who, c, bytes = jpeg, opts = {}) => upload(`${api}/api/conversations/${c}/photos`, bytes, { token: tk[who], ...opts });
    const before = await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.ana}'`);
    assert.equal((await upload(`${api}/api/conversations/${ab}/photos`, jpeg, {})).status, 401); pass();
    assert.equal((await upload(`${api}/api/conversations/${ab}/photos`, jpeg, { token: st.sup })).status, 403); pass();
    assert.deepEqual(code(await send('cam', ab)), [404, 'not-available']); pass(); // someone else's conversation
    assert.deepEqual(code(await send('ana', '00000000-0000-0000-0000-000000000000')), [404, 'not-available']); pass();
    assert.deepEqual(code(await send('ana', ab, Buffer.from('GIF89a' + 'x'.repeat(400)), { type: 'image/gif', name: 'x.gif' })), [415, 'photo-unsupported']); pass();
    assert.deepEqual(code(await send('ana', ab, await synthetic(canvas, { kind: 'solid', width: 100, height: 100, type: 'image/png' }), { type: 'image/png', name: 'x.png' })), [400, 'photo-invalid']); pass();
    assert.deepEqual(code(await call(tk.ana, `/api/conversations/${ab}/photos`, { method: 'POST', body: { file: 'x' } })), [400, 'photo-invalid']); pass();
    assert.equal((await call(tk.dee, '/api/me/notification-settings', { method: 'PUT', body: { chatPhotos: false } })).status, 200);
    assert.deepEqual(code(await send('ana', ad)), [409, 'photos-off']); pass();
    assert.deepEqual(code(await send('bot', abot)), [403, 'bot-cannot-initiate']); pass(); // bots only reply
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.ana}'`), before); pass();

    // ---- A photo that passes: the sender sees it checking, the recipient only once approved.
    r = await send('ana', ab, withExif(jpeg, 6));
    assert.deepEqual([r.status, r.body.kind, r.body.mine, r.body.photoState, r.body.photo, r.body.text], [202, 'photo', true, 'checking', null, '']); pass();
    const first = r.body.id;
    const firstMedia = await sql(`SELECT media_id FROM messages WHERE id = '${first}'`);
    assert.equal(await sql(`SELECT purpose || '|' || conversation_id FROM media_items WHERE id = '${firstMedia}'`), `attachment|${ab}`); pass();
    const own = async () => (await list('ana', ab)).find(m => m.id === first);
    assert.ok(await waitFor(async () => (await own())?.photoState === 'approved'), 'screened and approved'); pass();
    const mine = await own();
    assert.ok(mine.photo?.link?.startsWith('api/media/')); pass();
    const seen = (await list('ben', ab)).find(m => m.id === first);
    assert.deepEqual([seen.kind, seen.mine, seen.photoState, seen.photo.id], ['photo', false, null, firstMedia]); pass(); // no state for the recipient
    assert.deepEqual(Object.keys(seen).sort(), ['canDelete', 'conversationId', 'deleted', 'id', 'kind', 'mine', 'photo', 'photoState', 'sentAt', 'text']); pass(); // no read receipts or presence
    let image = await fetch(`${api}/${seen.photo.link}`);
    assert.equal(image.status, 200); pass();
    const bytes = Buffer.from(await image.arrayBuffer());
    assert.ok(!bytes.includes(Buffer.from('SWOOVRGPS')) && !bytes.includes(Buffer.from('Exif'))); pass(); // stripped
    assert.equal((await fetch(`${api}/${seen.photo.link.replace(/.$/, c => c === 'A' ? 'B' : 'A')}`)).status, 404); pass(); // tampered link
    assert.ok(await waitFor(async () => (await call(tk.ben, '/api/me/notifications')).body.items.some(i => i.kind === 'message' && i.link === `/chats/${ab}`), 10000)); pass();
    // Never a profile photo.
    assert.ok(!(await call(tk.ana, '/api/me/photos')).body.photos.some(p => p.id === firstMedia)); pass();
    assert.deepEqual(code(await call(tk.ana, '/api/me/photos/placement', { method: 'PUT', body: { scope: 'main', mediaIds: [firstMedia] } })), [409, 'photo-not-approved']); pass();
    assert.deepEqual(code(await call(tk.ana, `/api/me/photos/${firstMedia}`, { method: 'DELETE' })), [404, 'unknown-photo']); pass();
    assert.deepEqual(code(await call(tk.cam, `/api/conversations/${ab}/messages`)), [404, 'not-available']); pass();

    // ---- Restricted members: can't send, and can't be sent to.
    await call(root, `/api/admin/accounts/${id.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the attachments suite.', days: 2 } });
    assert.deepEqual(code(await send('res', ar)), [403, 'account-restricted']); pass();
    assert.deepEqual(code(await send('ana', ar)), [404, 'not-available']); pass();

    // ---- Hide for oneself; delete for both (shown as deleted, object kept 30 days, then released and purged).
    r = await send('ana', ab, await synthetic(canvas, { kind: 'noise', type: 'image/jpeg', seed: 42 }));
    const second = r.body.id;
    assert.ok(await waitFor(async () => (await list('ben', ab)).some(m => m.id === second))); pass();
    assert.equal((await call(tk.ben, `/api/messages/${second}/hide`, { method: 'POST' })).status, 204);
    assert.ok(!(await list('ben', ab)).some(m => m.id === second) && (await list('ana', ab)).some(m => m.id === second)); pass();
    r = await send('ana', ab, await synthetic(canvas, { kind: 'text', type: 'image/jpeg', seed: 43 }));
    const third = r.body.id, thirdMedia = await sql(`SELECT media_id FROM messages WHERE id = '${third}'`);
    assert.ok(await waitFor(async () => (await list('ben', ab)).some(m => m.id === third))); pass();
    assert.equal((await call(tk.ana, `/api/messages/${third}`, { method: 'DELETE' })).status, 204);
    for (const who of ['ana', 'ben']) {
      const m = (await list(who, ab)).find(x => x.id === third);
      assert.deepEqual([m.deleted, m.photo, m.photoState], [true, null, null], who); pass();
    }
    assert.notEqual(await sql(`SELECT coalesce(object_key, '') FROM media_items WHERE id = '${thirdMedia}'`), ''); pass(); // kept for investigations
    await sql(`UPDATE messages SET deleted_at = now() - interval '31 days' WHERE id = '${third}'`);
    let out = await cli('messages', 'purge');
    assert.match(out, /[1-9]\d* deleted chat photos released/); pass();
    assert.equal(await sql(`SELECT status FROM media_items WHERE id = '${thirdMedia}'`), 'Removed'); pass();
    // Fail closed: with hold status unavailable nothing is deleted.
    let run = await purge({ Evidence__HoldCheckUnavailable: 'true' });
    assert.equal(run.code, 1); pass();
    assert.notEqual(await sql(`SELECT coalesce(object_key, '') FROM media_items WHERE id = '${thirdMedia}'`), ''); pass();
    run = await purge();
    assert.equal(run.code, 0, run.out); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id = '${thirdMedia}' AND object_key IS NOT NULL`), '0'); pass(); // object deleted (the removed row goes too)

    // ---- Report evidence: the photo is selectable like any message; an admin opens it for the case with a purpose.
    const cats = (await call(tk.ben, '/api/report-categories')).body;
    r = await call(tk.ben, '/api/reports', { method: 'POST', body: { subjectType: 'conversation', subjectId: ab, category: cats[cats.length - 1].id, block: false, messageIds: [first, second] } });
    assert.equal(r.status, 201, JSON.stringify(r.body));
    const caseId = await sql(`SELECT case_id FROM reports WHERE id = '${r.body.id}'`);
    const snapshot = await sql(`SELECT snapshot FROM case_evidence WHERE report_id = '${r.body.id}'`);
    const firstSha = await sql(`SELECT encode(sha256, 'hex') FROM media_items WHERE id = '${firstMedia}'`);
    assert.ok(snapshot.includes(`"photo":"${firstMedia}"`) && snapshot.includes(`"photoSha256":"${firstSha}"`)); pass();
    const caseView = (token, media, body) => call(token, `/api/admin/media/${media}/case-view`, { method: 'POST', body });
    assert.equal((await caseView(st.sup, firstMedia, { caseId, purpose: 'Fictional check' })).status, 403); pass();
    assert.equal((await caseView(tk.ben, firstMedia, { caseId, purpose: 'Fictional check' })).status, 403); pass();
    assert.deepEqual(code(await caseView(st.adm, firstMedia, { caseId })), [400, 'purpose-required']); pass();
    assert.deepEqual(code(await caseView(st.adm, firstMedia, { caseId: '00000000-0000-0000-0000-000000000000', purpose: 'Fictional check' })), [404, 'unknown-photo']); pass();
    assert.deepEqual(code(await caseView(st.adm, thirdMedia, { caseId, purpose: 'Fictional check' })), [404, 'unknown-photo']); pass(); // not in the evidence
    r = await caseView(st.adm, firstMedia, { caseId, purpose: 'Fictional check of a reported chat photo' });
    assert.equal(r.status, 200); pass();
    assert.match(r.body.link, /\.case\./); pass();
    assert.equal((await fetch(`${api}/${r.body.link}`)).status, 200); pass();
    assert.equal(await sql(`SELECT count(*) FROM media_review_views WHERE media_id = '${firstMedia}'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE target_id = '${firstMedia}' AND coalesce(detail, '') LIKE '%Fictional%'`), '0'); pass();
    const adminPage = await browser.newPage();
    await adminPage.goto(admin + '/');
    await adminPage.fill('#username', staff.adm.email); await adminPage.fill('#password', staff.adm.password); await adminPage.click('#kc-login');
    await adminPage.waitForURL(admin + '/**');
    await adminPage.goto(admin + `/cases/${caseId}`);
    await adminPage.waitForSelector('[data-case-photo-form]'); pass();
    await adminPage.selectOption('select[name="PhotoForm.MediaId"]', firstMedia);
    await adminPage.fill('input[name="PhotoForm.Purpose"]', 'Fictional admin app check');
    await adminPage.click('[data-case-photo-open]');
    await adminPage.waitForSelector('[data-case-photo]'); pass();

    // ---- Uncertain (labelled simulation on an extra API): the review queue, invisible until an admin approves.
    await extraApi({ Media__Screening__Simulate: 'uncertain', Media__Screening__Worker: 'true' });
    r = await upload(`${EXTRA}/api/conversations/${ab}/photos`, await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', seed: 44 }), { token: tk.ana });
    assert.equal(r.status, 202); pass();
    const unsure = r.body.id, unsureMedia = await sql(`SELECT media_id FROM messages WHERE id = '${unsure}'`);
    assert.ok(await waitFor(async () => (await list('ana', ab)).find(m => m.id === unsure)?.photoState === 'in-review')); pass();
    assert.ok(!(await list('ben', ab)).some(m => m.id === unsure)); pass();
    const queue = (await call(st.adm, '/api/admin/media/review')).body.items.find(i => i.id === unsureMedia);
    assert.deepEqual([queue?.purpose, queue?.accountId], ['attachment', id.ana]); pass();
    assert.equal((await call(st.adm, `/api/admin/media/${unsureMedia}/decision`, { method: 'POST', body: { decision: 'approve' } })).status, 200);
    assert.ok(await waitFor(async () => (await list('ben', ab)).some(m => m.id === unsure && m.photo))); pass();

    // ---- Blocked (labelled simulation): sealed, held, escalated with ids only; reads as not approved; nobody can view it.
    await extraApi({ Media__Screening__Simulate: 'blocked' });
    r = await upload(`${EXTRA}/api/conversations/${ab}/photos`, await synthetic(canvas, { kind: 'solid', type: 'image/jpeg', seed: 45, width: 400, height: 400 }), { token: tk.ana });
    const bad = r.body.id, badMedia = await sql(`SELECT media_id FROM messages WHERE id = '${bad}'`);
    assert.ok(await waitFor(async () => (await list('ana', ab)).find(m => m.id === bad)?.photoState === 'not-approved')); pass();
    // Await durable preservation completion, including accepted-alert markers, before stopping this worker.
    assert.ok(await waitFor(async () => await sql(`SELECT count(*) FROM media_items WHERE id = '${badMedia}' AND hold_id IS NOT NULL AND case_id IS NOT NULL AND preservation_completed_at IS NOT NULL`) === '1'), 'blocked chat photo hold linked');
    const escalationCase = await sql(`SELECT case_id FROM media_items WHERE id = '${badMedia}'`);
    const escalationAddress = await localEnv('SWOOVR_ESCALATION_EMAIL');
    assert.ok(await waitFor(async () => (await mailTo(escalationAddress)).some(t => t.includes(escalationCase))), 'blocked chat photo escalation completed');
    await stopExtra();
    assert.ok(!(await list('ben', ab)).some(m => m.id === bad)); pass();
    const [status, objectKey, blockedCase, holdId] = (await sql(`SELECT status, object_key, case_id, hold_id FROM media_items WHERE id = '${badMedia}'`)).split('|');
    holdIds.push(holdId);
    assert.deepEqual([status, objectKey.startsWith('evidence/')], ['Blocked', true]); pass();
    assert.equal(await sql(`SELECT queue || '|' || priority FROM moderation_cases WHERE id = '${blockedCase}'`), 'Admin|Urgent'); pass();
    assert.equal(await sql(`SELECT source FROM legal_holds WHERE id = '${holdId}'`), 'media-screening'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE hold_id = '${holdId}' AND kind = 'media-item'`)) >= 3, 'the hold captures the chat photos'); pass();
    const escalation = (await mailTo(await localEnv('SWOOVR_ESCALATION_EMAIL'))).find(t => t.includes(blockedCase));
    assert.ok(escalation && !escalation.includes(id.ana) && !escalation.includes('Fictional')); pass();
    assert.deepEqual(code(await call(st.adm, `/api/admin/media/${badMedia}/view`, { method: 'POST', body: { purpose: 'x' } })), [404, 'unknown-photo']); pass();
    assert.deepEqual(code(await caseView(st.adm, badMedia, { caseId: blockedCase, purpose: 'x' })), [404, 'unknown-photo']); pass();
    assert.ok((await call(tk.ana, '/api/me/standing')).body.decisions.some(d => d.kind === 'photo-not-approved')); pass();
    // Under the hold, a deleted chat photo is kept past 30 days.
    assert.equal((await call(tk.ana, `/api/messages/${unsure}`, { method: 'DELETE' })).status, 204);
    await sql(`UPDATE messages SET deleted_at = now() - interval '31 days' WHERE id = '${unsure}'`);
    out = await cli('messages', 'purge');
    assert.notEqual(await sql(`SELECT status FROM media_items WHERE id = '${unsureMedia}'`), 'Removed'); pass();
    assert.match(out, /[1-9]\d* kept under a legal hold/); pass();

    // ---- Daily limit (seeded in SQL, never through the screener).
    const sent = Number(await sql(`SELECT count(*) FROM media_items WHERE account_id = '${id.ana}' AND purpose = 'attachment' AND created_at > now() - interval '1 day'`));
    await sql(`INSERT INTO media_items (id, account_id, status, sha256, upload_sha256, content_type, width, height, bytes, created_at, screen_attempts, known_content_checked, purpose, removed_at)
      SELECT gen_random_uuid(), '${id.ana}', 'Removed', '\\x00', '\\x00', 'image/jpeg', 1, 1, 1, now(), 0, false, 'attachment', now() FROM generate_series(1, ${20 - sent})`);
    assert.deepEqual(code(await send('ana', ab)), [429, 'attachment-limit']); pass();
    await sql(`DELETE FROM media_items WHERE account_id = '${id.ana}' AND width = 1 AND object_key IS NULL`);

    // ---- Export: Ana's own chat photos with images (approved only); Ben's chats list them without Ana's images.
    tk.ana = await memberToken(browser, members.ana);
    r = await call(tk.ana, '/api/me/export', { method: 'POST' });
    const exported = await (await fetch(`${api}/${r.body.path}`, { headers: { Authorization: 'Bearer ' + tk.ana } })).json();
    const firstExport = exported.chatPhotos.find(p => p.id === firstMedia);
    assert.deepEqual([firstExport.state, firstExport.sha256, firstExport.conversation], ['approved', firstSha, ab]); pass();
    assert.ok(firstExport.dataBase64.length > 100); pass();
    assert.equal(exported.chatPhotos.find(p => p.id === badMedia).dataBase64, null); pass();
    assert.equal(exported.chatPhotos.find(p => p.id === badMedia).state, 'not-approved'); pass();
    assert.ok(!exported.photos.some(p => p.id === firstMedia)); pass(); // not a profile photo
    tk.ben = await memberToken(browser, members.ben);
    r = await call(tk.ben, '/api/me/export', { method: 'POST' });
    const benExport = await (await fetch(`${api}/${r.body.path}`, { headers: { Authorization: 'Bearer ' + tk.ben } })).json();
    const benChat = benExport.conversations.find(c => c.messages.some(m => m.photo === firstMedia));
    assert.ok(benChat && !benChat.messages.some(m => m.photo === badMedia) && benExport.chatPhotos.length === 0); pass();

    // ---- Web at phone width: Ben sees the photo; Ana sends one through the page and ends the chat with Eve politely.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const benPage = await context.newPage();
    await benPage.goto(web + '/account/sign-in?intro=done');
    await benPage.fill('#username', members.ben.email); await benPage.fill('#password', members.ben.password);
    await Promise.all([benPage.waitForURL(web + '/account'), benPage.click('#kc-login')]);
    await benPage.goto(web + `/chats/${ab}`);
    await benPage.waitForSelector(`[data-chat-photo="${firstMedia}"]`, { timeout: 20000 }); pass();
    assert.ok(await benPage.$eval(`[data-chat-photo="${firstMedia}"]`, img => img.complete && img.naturalWidth > 0)); pass();
    if (output) await benPage.screenshot({ path: output + '/web-chat-photo.png', fullPage: true });
    await context.close();
    const anaContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const anaPage = await anaContext.newPage();
    await anaPage.goto(web + '/account/sign-in?intro=done');
    await anaPage.fill('#username', members.ana.email); await anaPage.fill('#password', members.ana.password);
    await Promise.all([anaPage.waitForURL(web + '/account'), anaPage.click('#kc-login')]);
    await anaPage.goto(web + `/chats/${ae}`);
    await anaPage.waitForSelector('[data-conversation="ready"]', { timeout: 20000 });
    // Force approval/live-event before the older upload response to cover response/event reordering.
    await anaPage.route(`**/bff/api/conversations/${ae}/photos`, async route => {
      const response = await route.fetch();
      const pending = await response.json();
      assert.ok(await waitFor(async () => (await call(tk.ana, `/api/conversations/${ae}/messages`)).body.items
        .some(m => m.id === pending.id && m.photoState === 'approved')), 'photo approved before releasing upload response'); pass();
      await route.fulfill({ response });
    });
    await anaPage.setInputFiles('[data-chat-photo-input]', { name: 'fictional.jpg', mimeType: 'image/jpeg', buffer: await synthetic(canvas, { kind: 'gradient', type: 'image/jpeg', seed: 46 }) });
    await anaPage.waitForSelector('[data-photo-state="checking"], [data-chat-photo]', { timeout: 30000 }); pass();
    await anaPage.waitForSelector('[data-chat-photo]', { timeout: 60000 }); pass(); // approved and shown in place (live update)
    if (output) await anaPage.screenshot({ path: output + '/web-chat-photo-sent.png', fullPage: true });
    await anaPage.$eval('[data-end-open]', el => el.click());
    await anaPage.waitForSelector('[data-end-form]');
    await anaPage.$eval('[data-closing="none"]', el => el.click());
    if (output) await anaPage.screenshot({ path: output + '/web-chat-end.png', fullPage: true });
    await anaPage.$eval('[data-end-confirm]', el => el.click());
    await anaPage.waitForSelector('[data-chat-status="ended"]'); pass();
    await anaContext.close();
    assert.equal((await call(tk.eve, `/api/conversations/${ae}`)).body.canSend, false); pass();

    // ---- Ending politely (API): a fixed closing message, every mode disconnected, the chat read-only for both.
    assert.deepEqual(code(await call(tk.ana, `/api/conversations/${ab}/end`, { method: 'POST', body: { closing: 'you are awful' } })), [400, 'invalid-closing']); pass();
    assert.deepEqual(code(await call(tk.cam, `/api/conversations/${ab}/end`, { method: 'POST', body: { closing: null } })), [404, 'not-available']); pass();
    r = await call(tk.ben, `/api/conversations/${ab}/end`, { method: 'POST', body: { closing: 'thanks' } });
    assert.deepEqual([r.status, r.body.canSend], [200, false]); pass();
    const endedMessages = await list('ana', ab), last = endedMessages[0]; // newest first
    assert.deepEqual([last.kind, last.mine, last.text], ['system', false, 'This conversation has ended. You can still view your previous messages, but replies are no longer available.']); pass();
    assert.deepEqual([endedMessages[1].kind, endedMessages[1].text], ['farewell', "Thanks for chatting. I don't think we're the right match, but I wish you all the best."]); pass();
    assert.equal(await sql(`SELECT string_agg(status, ',' ORDER BY mode_id) FROM connection_requests WHERE pair_low = least('${id.ana}'::uuid, '${id.ben}'::uuid) AND pair_high = greatest('${id.ana}'::uuid, '${id.ben}'::uuid) AND status IN ('Accepted', 'Disconnected')`),
      'Disconnected,Disconnected'); pass();
    assert.deepEqual(code(await send('ana', ab)), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tk.ana, `/api/conversations/${ab}/messages`, { method: 'POST', body: { text: 'hi' } })), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tk.ana, '/api/requests', { method: 'POST', body: { to: pid.ben, mode: 'friends' } })), [409, 'recently-disconnected']); pass();
    assert.ok((await list('ana', ab)).some(m => m.id === first && m.photo)); pass(); // history stays readable
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'conversation.ended' AND target_id = '${ab}' AND detail = 'MemberEnded'`), '1'); pass();

    // ---- Account deletion: a hold over counterpart Ana preserves Eve’s photo independently, even though Eve is unnamed.
    await connect('eve', 'ana', 'dating');
    const ea = await start('eve', 'ana');
    r = await send('eve', ea, await synthetic(canvas, { kind: 'noise', type: 'image/jpeg', seed: 47 }));
    const eveMedia = await sql(`SELECT media_id FROM messages WHERE id = '${r.body.id}'`);
    assert.ok(await waitFor(async () => await sql(`SELECT status FROM media_items WHERE id = '${eveMedia}'`) === 'Passed')); pass();
    tk.eve = await memberToken(browser, members.eve);
    assert.equal((await call(tk.eve, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    run = await purge();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE id = '${eveMedia}' AND object_key IS NOT NULL`), '1','counterpart hold keeps actual bytes after owner deletion'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM evidence_items WHERE kind='media-item' AND record_id='${eveMedia}'`))>0,'counterpart hold captures media provenance');pass();
    assert.ok(!(await list('ana', ea)).some(m => m.kind === 'photo')); pass();



    // ---- Clean up the owner's bucket: release the hold (a legal officer who didn't place it), remove the suite's photos, purge.
    for (const hold of holdIds) {
      r = await call(st.leg, `/api/admin/holds/${hold}/release`, { method: 'POST', body: { reason: 'Fictional acceptance test finished', overlapsChecked: true } });
      assert.equal(r.status, 200, JSON.stringify(r.body));
    }
    const retainedHold=holdIds[0];
    // This fixture conversation is already closed; advance its ordinary deletion clock directly, without claiming a closed-chat unsend API.
    await sql(`UPDATE messages SET deleted_at=now()-interval '31 days' WHERE id='${first}'`);
    await cli('messages','purge');await purge();
    assert.equal(await sql(`SELECT status FROM media_items WHERE id='${firstMedia}'`),'Removed','ordinary photo expiry leaves retained review bytes');pass();
    r=await caseView(st.adm,firstMedia,{caseId,purpose:'Fictional retained review after ordinary deletion'});
    assert.equal(r.status,200);pass();
    const retainedBytes=await fetch(`${api}/${r.body.link}`);
    assert.equal(retainedBytes.status,200);pass();
    assert.equal(require('node:crypto').createHash('sha256').update(Buffer.from(await retainedBytes.arrayBuffer())).digest('hex'),firstSha,'actual retained review bytes unchanged');pass();
    const preserved=(token,media,body)=>call(token,`/api/admin/holds/${retainedHold}/media/${media}/link`,{method:'POST',body});
    assert.equal((await preserved(tk.ben,firstMedia,{purpose:'Fictional denied access'})).status,403);pass();
    assert.equal((await preserved(st.leg,firstMedia,{})).status,400);pass();
    await elevate(st.leg,'evidence-export','Fictional retained photo review');
    r=await preserved(st.leg,firstMedia,{purpose:'Fictional retained photo after hold release'});
    assert.equal(r.status,200);pass();assert.equal((await fetch(`${api}/${r.body.link}`)).status,200);pass();
    assert.equal((await preserved(st.leg,badMedia,{purpose:'Fictional blocked negative check'})).status,404);pass();
    await expireFictionalReviews(accounts);
    await sql(`UPDATE media_items SET status='Removed',removed_at=coalesce(removed_at,now()),next_screen_at=NULL
      WHERE account_id IN (${accounts.map(a=>`'${a}'`).join(',')}) AND status IN ('Pending','Passed','Uncertain')`);
    await purge();
    assert.equal(await sql(`SELECT count(*) FROM media_items WHERE account_id IN (${accounts.map(a=>`'${a}'`).join(',')}) AND object_key IS NOT NULL`),'0','expired fixture evidence and raw bytes disposed');pass();
    holdIds = [];
    console.log(`PASS: chat photos, endings and starters (${passed} checks).`);
  } finally {
    await stopExtra();
    if (accounts.length) {
      const list = accounts.map(a => `'${a}'`).join(',');
      await sql(`UPDATE media_items SET status = 'Removed', removed_at = coalesce(removed_at, now()), next_screen_at = NULL WHERE account_id IN (${list}) AND status IN ('Pending', 'Passed', 'Uncertain')`).catch(() => {});
      const run = await purge().catch(() => ({ out: '' }));
      const left = await sql(`SELECT count(*) FROM media_items WHERE account_id IN (${list}) AND object_key IS NOT NULL`).catch(() => '?');
      console.log(`Suite photo objects left in owned test store: ${left}${holdIds.length ? ' (fixture retention cleanup was interrupted)'  : ''}.`);
    }
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
