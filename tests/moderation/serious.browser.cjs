// Local acceptance for slice Z (policy §9, owner Q77, D190) and #82: the Serious Safety Incident pathway. Serious
// categories in their own list; a serious report goes to the admin queue (never support), is escalated every time with
// no content, and places a provisional legal hold over both accounts and their conversation; police details (the
// reference kept sealed); messages chosen as evidence; reporting after a block, a disconnect and the other account's
// deletion; the reported person never told; general outcomes once closed; a block pushes "closed" to live chat clients
// (#82). Negative cases: strangers, other members' conversations and messages, staff, tampered or oversized bodies.
// Then the web Safety page, person page link and conversation block in Edge.
// Needs the local stack, the enrolled fictional root admin and hosts built from this checkout (API with
// Staff__LocalPasswords=true, Moderation__EscalationEmail and Accounts__ReauthSeconds=45). Every person is fictional.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, localEnv, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, mailTo, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
const RS = '\u001e';

/** A minimal SignalR JSON-protocol listener (as in the messaging suite), with keep-alive pings. */
async function listen(token) {
  const base = api + '/hubs/messages';
  const negotiate = await fetch(`${base}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Authorization: 'Bearer ' + token } });
  const { connectionToken } = await negotiate.json();
  const socket = new WebSocket(base.replace(/^http/, 'ws') + `?id=${encodeURIComponent(connectionToken)}&access_token=${encodeURIComponent(token)}`);
  const received = [];
  let handshake = false;
  socket.onmessage = async e => {
    const text = typeof e.data === 'string' ? e.data : Buffer.from(await e.data.arrayBuffer()).toString();
    for (const frame of text.split(RS).filter(Boolean)) {
      const message = JSON.parse(frame);
      if (!handshake && !('type' in message)) { handshake = true; continue; }
      if (message.type === 1) received.push({ target: message.target, payload: message.arguments[0] });
    }
  };
  await new Promise(resolve => { socket.onopen = resolve; setTimeout(resolve, 3000); });
  socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS);
  const ping = setInterval(() => { if (socket.readyState === 1) socket.send(JSON.stringify({ type: 6 }) + RS); else clearInterval(ping); }, 10000);
  for (let i = 0; i < 30 && !handshake; i++) await sleep(100);
  return { received, close: () => { clearInterval(ping); socket.close(); } };
}
const waitFor = async (predicate, ms = 5000) => { for (let i = 0; i < ms / 100 && !predicate(); i++) await sleep(100); return predicate(); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    const ESCALATION = (await localEnv('SWOOVR_ESCALATION_EMAIL')) || 'moderation-escalations@example.test';
    const root = await rootAdminToken(browser);
    const names = ['rep', 'sub', 'blk', 'del', 'dis', 'oth', 'web'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('sev-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      pid[n] = (await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Sev ' + n, homeCityId: ADELAIDE } })).body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      await call(tokens[n], '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const connect = async (a, b) => {
      const req = await call(tokens[a], '/api/requests', { method: 'POST', body: { to: pid[b], mode: 'friends' } });
      assert.equal((await call(tokens[b], `/api/requests/${req.body.id}/accept`, { method: 'POST' })).status, 200);
      return { request: req.body.id, conversation: (await call(tokens[a], '/api/conversations', { method: 'POST', body: { to: pid[b] } })).body.id };
    };
    const send = async (who, convo, text) => (await call(tokens[who], `/api/conversations/${convo}/messages`, { method: 'POST', body: { text } })).body.id;
    const withSub = await connect('rep', 'sub'), withBlk = await connect('rep', 'blk'), withDel = await connect('rep', 'del'), withDis = await connect('rep', 'dis');
    const threat = await send('sub', withSub.conversation, 'Fictional threatening message from Sub');
    const other1 = await send('sub', withSub.conversation, 'Fictional second message from Sub');
    await send('rep', withSub.conversation, 'Fictional reply from Rep');
    await send('blk', withBlk.conversation, 'Fictional message from Blk');
    await send('del', withDel.conversation, 'Fictional message from Del');
    const staff = { sup: await createStaff('sev-sup'), adm: await createStaff('sev-adm'), leg: await createStaff('sev-leg') };
    for (const [n, role] of [['sup', 'support'], ['adm', 'admin'], ['leg', 'legal']])
      assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff[n].email, role } })).status, 201);
    const st = {};
    for (const n of Object.keys(staff)) st[n] = await staffToken(browser, staff[n]);
    const report = (who, body) => call(tokens[who], '/api/reports', { method: 'POST', body: { text: null, block: false, ...body } });

    // Categories: the serious list is separate; routine reports no longer offer them.
    const serious = (await call(tokens.rep, '/api/report-categories?kind=serious')).body.map(c => c.id);
    for (const id of ['sexual-assault', 'violence', 'threats', 'stalking', 'coercion', 'child-exploitation', 'danger']) assert.ok(serious.includes(id), id);
    pass();
    const routine = (await call(tokens.rep, '/api/report-categories')).body.map(c => c.id);
    assert.ok(!routine.some(id => serious.includes(id)) && routine.includes('underage')); pass();
    assert.equal((await call(root, '/api/report-categories?kind=serious')).status, 403, 'staff aren\'t members'); pass();

    // Fictional owner-provided standalone meeting context, never another member's plan.
    const meetingContext=crypto.randomUUID(),foreignMeeting=crypto.randomUUID();
    for(const [id,owner] of [[meetingContext,ids.rep],[foreignMeeting,ids.sub]])await sql(`INSERT INTO meeting_safety_sessions(id,member_id,preferred_first_name,starts_at,final_check_in_at,time_zone_id,venue_or_area,share_place,member_note,private_feedback,status,created_at,updated_at) VALUES('${id}','${owner}','Fictional',now()-interval '1 hour',now()+interval '1 hour','UTC','Fictional meeting venue',false,'FICTIONAL PRIVATE CONTEXT NOTE','FICTIONAL PRIVATE CONTEXT FEEDBACK','Active',now(),now())`);

    // Validation and strangers.
    const base = { subjectType: 'conversation', subjectId: withSub.conversation, category: 'stalking' };
    assert.deepEqual(code(await report('rep', { ...base, police: 'maybe' })), [400, 'invalid-report']); pass();
    assert.deepEqual(code(await report('rep', { ...base, police: 'contacted', policeReference: 'x'.repeat(101) })), [400, 'invalid-report']); pass();
    assert.deepEqual(code(await report('rep', { ...base, messageIds: Array.from({ length: 21 }, () => crypto.randomUUID()) })), [400, 'invalid-report']); pass();
    assert.deepEqual(code(await report('rep', { ...base, messageIds: [crypto.randomUUID()] })), [400, 'invalid-report'], 'not from this conversation'); pass();
    assert.deepEqual(code(await report('oth', base)), [404, 'not-available'], 'a stranger can\'t report through someone else\'s conversation'); pass();
    assert.deepEqual(code(await report('oth', { subjectType: 'message', subjectId: threat, category: 'stalking' })), [404, 'not-available']); pass();
    assert.deepEqual(code(await report('sub', { subjectType: 'message', subjectId: threat, category: 'stalking' })), [404, 'not-available'], 'not one\'s own message'); pass();
    const tampered = await fetch(api + '/api/reports', { method: 'POST', headers: { Authorization: 'Bearer ' + tokens.rep, 'Content-Type': 'application/json' }, body: '{"subjectType":"conversation","messageIds":"all"}' });
    assert.equal(tampered.status, 400); pass();
    assert.deepEqual(code(await report('rep',{...base,meetingSafetySessionId:foreignMeeting})),[404,'not-available'],'another member meeting context refused');pass();
    assert.deepEqual(code(await report('rep',{...base,meetingSafetySessionId:crypto.randomUUID()})),[404,'not-available'],'unknown meeting context refused');pass();
    assert.equal(await sql(`SELECT count(*) FROM reports WHERE reporter_account_id = '${ids.rep}'`), '0', 'refusals filed nothing'); pass();

    // A serious report with police details and chosen messages.
    let r = await report('rep', { ...base, text: 'Fictional account of being followed', police: 'contacted', policeReference: 'FICT-POL-123', messageIds: [threat], meetingSafetySessionId:meetingContext });
    assert.equal(r.status, 201); assert.equal(r.body.serious, true); assert.equal(r.body.status, 'submitted'); pass();
    const subCase = await sql(`SELECT id FROM moderation_cases WHERE subject_account_id = '${ids.sub}' AND status IN ('Open', 'UnderReview')`);
    assert.equal(await sql(`SELECT queue FROM moderation_cases WHERE id = '${subCase}'`), 'Admin'); pass();
    assert.ok(!(await call(st.sup, '/api/admin/cases')).body.some(c => c.id === subCase)); pass();
    assert.equal((await call(st.sup, `/api/admin/cases/${subCase}`)).status, 404, 'support never sees serious cases'); pass();
    const detail = (await call(st.adm, `/api/admin/cases/${subCase}`)).body;
    assert.equal(detail.reports[0].police, 'contacted'); pass();
    const snapshot = JSON.parse(detail.evidence.find(e=>e.kind==='conversation').snapshot);
    assert.equal(snapshot.chosen, true); assert.deepEqual(snapshot.messages.map(m => m.id), [threat]); pass();
    assert.equal(await sql(`SELECT police_reference IS NOT NULL AND position(convert_to('FICT-POL', 'UTF8') in police_reference) = 0 FROM reports WHERE case_id = '${subCase}'`), 't', 'reference sealed'); pass();
    assert.ok(!JSON.stringify(detail).includes('FICT-POL'), 'admins don\'t see the reference'); pass();
    assert.equal(await sql(`SELECT meeting_safety_session_id FROM reports WHERE case_id='${subCase}'`),meetingContext,'own meeting linked to actual serious report');pass();
    const contextEvidence=detail.evidence.find(e=>e.kind==='meeting-safety');assert.ok(contextEvidence,'separate meeting-context evidence');pass();
    const contextSnapshot=JSON.parse(contextEvidence.snapshot);assert.equal(contextSnapshot.meetingSafetySessionId,meetingContext);assert.equal(contextSnapshot.venueOrArea,'Fictional meeting venue');pass();
    assert.ok(!contextEvidence.snapshot.includes('FICTIONAL PRIVATE CONTEXT NOTE')&&!contextEvidence.snapshot.includes('FICTIONAL PRIVATE CONTEXT FEEDBACK'),'private note/feedback excluded from incident snapshot');pass();
    // Escalated with no content.
    let mail = []; for (let i = 0; i < 20 && !mail.some(m => m.includes(subCase)); i++) { mail = await mailTo(ESCALATION); await sleep(250); }
    const escalation = mail.find(m => m.includes(subCase));
    assert.ok(escalation && escalation.includes('Serious Safety Incident') && escalation.includes('stalking')); pass();
    assert.ok(!mail.some(m => m.includes('Fictional account of being followed') || m.includes('FICT-POL') || m.includes('Fictional Sev'))); pass();
    // A provisional hold over both accounts and their conversation, captured at once.
    const hold = await sql(`SELECT id FROM legal_holds WHERE case_id = '${subCase}' AND source = 'serious-incident'`);
    assert.match(hold, /^[0-9a-f-]{36}$/); pass();
    assert.equal(await sql(`SELECT status FROM legal_holds WHERE id = '${hold}'`), 'Provisional'); pass();
    assert.equal(await sql(`SELECT string_agg(target_id::text, ',' ORDER BY target_id) FROM legal_hold_scopes WHERE hold_id = '${hold}' AND kind = 'Account'`),
      [ids.rep, ids.sub].sort().join(',')); pass();
    assert.equal(await sql(`SELECT target_id FROM legal_hold_scopes WHERE hold_id = '${hold}' AND kind = 'Conversation'`), withSub.conversation); pass();
    let held = (await call(st.leg, `/api/admin/holds/${hold}`)).body;
    assert.ok(held.items.filter(i => i.kind === 'message').length >= 3 && held.items.some(i => i.kind === 'report')); pass();
    // The reported person isn't told.
    assert.equal((await call(tokens.sub, '/api/me/notices')).body.length, 0); pass();
    // #49 (D207): the generic "Something new on Swoovr" email for a request may arrive; nothing about the report does.
    assert.equal((await mailTo(members.sub.email)).filter(t => !t.startsWith('You have a new request on Swoovr') && !t.startsWith('You have a new connection on Swoovr')).length, 0); pass();
    // Acknowledged, then another serious report escalates again and captures into the same hold.
    assert.equal((await call(st.adm, `/api/admin/cases/${subCase}/acknowledge`, { method: 'POST' })).status, 200);
    const itemsBefore = held.items.length;
    await send('sub', withSub.conversation, 'Fictional new message after the first report');
    r = await report('rep', { subjectType: 'message', subjectId: other1, category: 'threats', police: 'will-contact' });
    assert.equal(r.status, 201); pass();
    assert.equal((await call(st.adm, '/api/admin/cases?queue=admin')).body.find(c => c.id === subCase).awaitingAcknowledgement, true, 'escalated again'); pass();
    assert.equal(await sql(`SELECT count(*) FROM legal_holds WHERE case_id = '${subCase}'`), '1', 'one hold per case and pair'); pass();
    held = (await call(st.leg, `/api/admin/holds/${hold}`)).body;
    assert.ok(held.items.length > itemsBefore, 'captured again'); pass();

    // #82: a block pushes "closed" to both members' live chat clients.
    const repHub = await listen(tokens.rep), blkHub = await listen(tokens.blk);
    assert.equal((await call(tokens.rep, '/api/blocks', { method: 'POST', body: { profileId: pid.blk } })).status, 204);
    assert.ok(await waitFor(() => repHub.received.some(m => m.target === 'closed' && m.payload.conversationId === withBlk.conversation))); pass();
    assert.ok(await waitFor(() => blkHub.received.some(m => m.target === 'closed' && m.payload.conversationId === withBlk.conversation))); pass();
    // And from report-and-block.
    const withOth = await connect('oth', 'web');
    const othHub = await listen(tokens.oth);
    assert.equal((await report('web', { subjectType: 'conversation', subjectId: withOth.conversation, category: 'harassment', block: true })).status, 201);
    assert.ok(await waitFor(() => othHub.received.some(m => m.target === 'closed' && m.payload.conversationId === withOth.conversation))); pass();
    repHub.close(); blkHub.close(); othHub.close();

    // Reporting after a block, a disconnect and a deletion.
    assert.equal((await report('rep', { subjectType: 'conversation', subjectId: withBlk.conversation, category: 'coercion' })).status, 201, 'after a block'); pass();
    assert.equal((await call(tokens.rep, `/api/requests/${withDis.request}/disconnect`, { method: 'POST' })).status, 200);
    assert.equal((await report('rep', { subjectType: 'profile', subjectId: pid.dis, category: 'harassment' })).status, 201, 'after a disconnect (shared request)'); pass();
    tokens.del = await memberToken(browser, members.del);
    assert.equal((await call(tokens.del, '/api/me/deletion', { method: 'POST', body: { confirm: true, immediately: true, deleteFounderToken: false } })).status, 200);
    await cli('accounts', 'purge');
    assert.equal(await sql(`SELECT status FROM accounts WHERE id = '${ids.del}'`), 'Deleted');
    const people = (await call(tokens.rep, '/api/me/safety/people')).body;
    const state = id => people.find(p => p.subjectId === id)?.state;
    assert.equal(state(withDel.conversation), 'deleted'); assert.equal(people.find(p => p.subjectId === withDel.conversation).name, 'Deleted member'); pass();
    assert.equal(state(withBlk.conversation), 'blocked'); pass();
    assert.equal(state(withDis.conversation), 'disconnected'); pass();
    assert.equal(state(withSub.conversation), 'connected'); pass();
    assert.ok(!people.some(p => p.name.includes('Sev oth')), 'only people they dealt with'); pass();
    r = await report('rep', { subjectType: 'conversation', subjectId: withDel.conversation, category: 'sexual-assault', police: 'not-now' });
    assert.equal(r.status, 201, 'after the other account\'s deletion'); pass();
    assert.equal(await sql(`SELECT queue FROM moderation_cases WHERE subject_account_id = '${ids.del}'`), 'Admin'); pass();
    assert.match(await sql(`SELECT id FROM legal_holds h JOIN legal_hold_scopes s ON s.hold_id = h.id WHERE s.target_id = '${ids.del}' AND h.source = 'serious-incident' LIMIT 1`), /^[0-9a-f-]{36}$/); pass();
    assert.equal((await call(tokens.oth, '/api/me/safety/people')).body.some(p => p.subjectId === withDel.conversation), false, 'not someone else\'s'); pass();
    assert.equal((await call(root, '/api/me/safety/people')).status, 403); pass();

    // General outcomes once closed: action taken, or no breach found; never which action.
    assert.equal((await call(st.adm, `/api/admin/cases/${subCase}/actions`, { method: 'POST', body: { action: 'warn', reason: 'Fictional warning', hideFromDiscovery: false, noNewRequests: false, noMessages: false } })).status, 200);
    assert.equal((await call(st.adm, `/api/admin/cases/${subCase}/acknowledge`, { method: 'POST' })).status, 200);
    assert.equal((await call(st.adm, `/api/admin/cases/${subCase}/close`, { method: 'POST' })).status, 200);
    const disCase = await sql(`SELECT id FROM moderation_cases WHERE subject_account_id = '${ids.dis}'`);
    assert.equal((await call(st.sup, `/api/admin/cases/${disCase}/actions`, { method: 'POST', body: { action: 'dismiss', hideFromDiscovery: false, noNewRequests: false, noMessages: false } })).status, 200);
    assert.equal((await call(st.sup, `/api/admin/cases/${disCase}/close`, { method: 'POST' })).status, 200);
    const mine = (await call(tokens.rep, '/api/me/reports')).body;
    assert.ok(mine.filter(m => m.category === 'stalking' || m.category === 'threats').every(m => m.outcome === 'actionTaken' && m.status === 'closed')); pass();
    assert.equal(mine.find(m => m.category === 'harassment').outcome, 'noBreachFound'); pass();
    assert.equal(mine.find(m => m.category === 'coercion').outcome, null, 'open cases show no outcome'); pass();
    assert.ok(!JSON.stringify(mine).includes('warn'), 'never which action'); pass();

    // The web in Edge: the Safety page from a conversation, the person page link, block from a chat, outcomes.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.web.email); await page.fill('#password', members.web.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    const withRep = await connect('web', 'rep');
    await send('rep', withRep.conversation, 'Fictional message Web will report');
    await page.goto(web + `/people/${pid.rep}?mode=friends`);
    await page.waitForSelector('[data-serious-link]', { timeout: 20000 }); pass();
    await page.goto(web + `/chats/${withRep.conversation}`);
    await page.waitForSelector('[data-serious-link]', { timeout: 20000 });
    const href = await page.getAttribute('[data-serious-link]', 'href');
    assert.equal(href, `/me/safety?type=conversation&id=${withRep.conversation}`); pass();
    await page.goto(web + href);
    await page.waitForSelector('[data-safety-services]', { timeout: 20000 });
    assert.ok((await page.textContent('[data-safety-services]')).includes('000')); pass();
    await page.waitForSelector('[data-serious-category] option[value="stalking"]', { state: 'attached' });
    await page.selectOption('[data-serious-category]', 'stalking');
    // The phone-width page's sticky header can sit over a control scrolled to the top; click the elements directly.
    const tap = selector => page.locator(selector).first().evaluate(el => el.click());
    await tap('[data-police="will-contact"]');
    await page.waitForSelector('[data-choose-messages]');
    await tap('[data-choose]');
    if (output) await page.screenshot({ path: output + '/safety-report.png', fullPage: true });
    await tap('[data-serious-send]');
    await page.waitForSelector('[data-serious-done]', { timeout: 20000 }); pass();
    assert.equal(await sql(`SELECT count(*) FROM reports WHERE reporter_account_id = '${ids.web}' AND category_id = 'stalking' AND police = 'will-contact'`), '1'); pass();
    await page.goto(web + '/me/safety');
    await page.waitForSelector(`[data-safety-person="${withOth.conversation}"][data-safety-state="blocked"]`, { timeout: 20000 }); pass();
    // Block from the chat header: the window closes the chat.
    const withDis2 = await connect('web', 'dis');
    await page.goto(web + `/chats/${withDis2.conversation}`);
    await page.waitForSelector('[data-block-conversation]', { timeout: 20000 });
    await page.locator('[data-block-conversation]').evaluate(b => b.click());
    await page.waitForSelector('[data-block-confirm]');
    await page.locator('[data-block-confirm]').evaluate(b => b.click());
    await page.waitForSelector('[data-chat-status="closed"]'); pass();
    assert.equal(await sql(`SELECT closed_reason FROM conversations WHERE id = '${withDis2.conversation}'`), 'blocked'); pass();
    await page.goto(web + '/me/notices');
    await page.waitForSelector('[data-my-reports]', { timeout: 20000 });
    assert.ok(await page.locator('[data-report-outcome="none"]').count() >= 1); pass();
    await context.close();

    console.log(`PASS: serious safety incidents (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
