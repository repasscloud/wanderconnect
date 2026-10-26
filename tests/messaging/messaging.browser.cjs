// Local acceptance for slice U (Q42, D143, D165): one conversation per connected pair, text only, sent through the API
// and pushed live through the SignalR hub (the app's path, with a bearer token) and the web host's relay (the browser's
// path, with the session cookie, same origin only). Delete for both within five minutes, hide for oneself, report a
// message or a conversation with evidence, blocks closing conversations for good, bots and travellers before arrival
// only replying, restricted and suspended members out, no moderator browsing, no message text in the audit, the
// purge job. Same local stack as tests/discovery/matching.browser.cjs; every member is a fictional adult.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = days => { const d = new Date(todayIn('Australia/Adelaide') + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const RS = '\u001e';

/** A minimal SignalR JSON-protocol listener over a raw WebSocket; collects pushes by target. */
async function listen(base, { token, headers = {} } = {}) {
  const auth = token ? { Authorization: 'Bearer ' + token } : {};
  const negotiate = await fetch(`${base}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { ...auth, ...headers } });
  if (negotiate.status !== 200) return { status: negotiate.status };
  const { connectionToken } = await negotiate.json();
  const url = base.replace(/^http/, 'ws') + `?id=${encodeURIComponent(connectionToken)}` + (token ? `&access_token=${encodeURIComponent(token)}` : '');
  const socket = new WebSocket(url, { headers });
  const received = [];
  let handshake = false, closed = false;
  socket.onmessage = async e => {
    const text = typeof e.data === 'string' ? e.data : Buffer.from(await e.data.arrayBuffer()).toString();
    for (const frame of text.split(RS).filter(Boolean)) {
      const message = JSON.parse(frame);
      if (!handshake && !('type' in message)) { handshake = !message.error; continue; }
      if (message.type === 1) received.push({ target: message.target, payload: message.arguments[0] });
      if (message.type === 7) closed = true;
    }
  };
  socket.onclose = () => { closed = true; };
  await new Promise(resolve => { socket.onopen = resolve; socket.onerror = resolve; setTimeout(resolve, 3000); });
  if (socket.readyState === 1) socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS);
  // Keep-alive, as SignalR clients do: the server drops a client it hasn't heard from in 30 seconds.
  const ping = setInterval(() => { if (socket.readyState === 1) socket.send(JSON.stringify({ type: 6 }) + RS); else clearInterval(ping); }, 10000);
  for (let i = 0; i < 30 && !handshake && !closed; i++) await sleep(100);
  // A server that aborts in OnConnectedAsync closes right after the handshake; give it a moment.
  await sleep(300);
  return { status: 200, received, get open() { return handshake && !closed && socket.readyState === 1; }, close: () => { clearInterval(ping); socket.close(); } };
}
const waitFor = async (predicate, ms = 5000) => { for (let i = 0; i < ms / 100 && !predicate(); i++) await sleep(100); return predicate(); };

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  const sockets = [];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat', 'dee', 'sus', 'res', 'lou', 'trv', 'bot'];
    const homes = { trv: MELBOURNE, bot: null };
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('msg-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Msg ' + n, homeCityId: n in homes ? homes[n] : ADELAIDE } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'bot') continue;
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/v1/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } });
    await call(root, `/api/v1/admin/accounts/${ids.bot}/bot-modes`, { method: 'PUT', body: { modes: ['friends'] } });
    await call(tokens.bot, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const connect = async (from, to, mode = 'friends', trip) => {
      const r = await call(tokens[from], '/api/v1/requests', { method: 'POST', body: { to: pid[to], mode, ...(trip ? { trip } : {}) } });
      assert.equal(r.status, 201, `${from}->${to}`);
      assert.equal((await call(tokens[to], `/api/v1/requests/${r.body.id}/accept`, { method: 'POST' })).status, 200);
    };
    for (const other of ['bob', 'dee', 'sus', 'res', 'bot']) await connect('ana', other);
    const start = (who, to) => call(tokens[who], '/api/v1/conversations', { method: 'POST', body: { to: pid[to] } });
    const send = (who, id, text) => call(tokens[who], `/api/v1/conversations/${id}/messages`, { method: 'POST', body: { text } });
    const list = async (who, id) => (await call(tokens[who], `/api/v1/conversations/${id}/messages`)).body?.items ?? [];

    // Starting: only between connected members; one conversation per pair, whoever starts it.
    assert.deepEqual(code(await start('ana', 'cat')), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: pid.ana } })), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: '00000000-0000-0000-0000-000000000000' } })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: ids.bob } })).status, 404); pass(); // an account id isn't a profile id
    let r = await call(tokens.ana, '/api/v1/conversations', { method: 'POST', body: { to: pid.bob, pairLow: ids.cat, id: '11111111-1111-1111-1111-111111111111' } });
    assert.deepEqual([r.status, r.body.person.id, r.body.canSend], [201, pid.bob, true]); pass();
    const ab = r.body.id;
    assert.notEqual(ab, '11111111-1111-1111-1111-111111111111'); pass();
    assert.deepEqual([(await start('ana', 'bob')).status, (await start('ana', 'bob')).body.id], [200, ab]); pass();
    assert.deepEqual([(await start('bob', 'ana')).status, (await start('bob', 'ana')).body.id], [200, ab]); pass();
    assert.equal(await sql(`SELECT count(*) FROM conversations WHERE id = '${ab}'`), '1'); pass();

    // Live pushes through the API hub (the app's path): both members, nobody else.
    const hub = `${api}/hubs/messages`;
    assert.equal((await listen(hub)).status, 401); pass();
    assert.equal((await listen(hub, { token: 'not-a-token' })).status, 401); pass();
    const bobHub = await listen(hub, { token: tokens.bob }), anaHub = await listen(hub, { token: tokens.ana }), catHub = await listen(hub, { token: tokens.cat });
    sockets.push(bobHub, anaHub, catHub);
    assert.ok(bobHub.open && anaHub.open && catHub.open); pass();

    // Sending: text only, 1–2,000 characters, no control characters.
    for (const bad of ['', '   ', 'x'.repeat(2001), 'bell\u0007'])
    { assert.deepEqual(code(await send('ana', ab, bad)), [400, 'invalid-message']); pass(); }
    assert.equal((await call(tokens.ana, `/api/v1/conversations/${ab}/messages`, { method: 'POST', body: { text: 7 } })).status, 400); pass();
    r = await call(tokens.ana, `/api/v1/conversations/${ab}/messages`, { method: 'POST', body: { text: '  Fictional hello Bob\nsecond line  ', sender: ids.bob, mine: false, sentAt: '2000-01-01T00:00:00Z' } });
    assert.deepEqual([r.status, r.body.mine, r.body.text, r.body.kind, r.body.canDelete], [201, true, 'Fictional hello Bob\nsecond line', 'text', true]); pass();
    assert.ok(new Date(r.body.sentAt).getFullYear() >= 2026); pass(); // the server's time, not the body's
    const hello = r.body.id;
    assert.ok(await waitFor(() => bobHub.received.some(x => x.target === 'message' && x.payload.id === hello && x.payload.mine === false))); pass();
    assert.ok(await waitFor(() => anaHub.received.some(x => x.target === 'message' && x.payload.id === hello && x.payload.mine === true))); pass();
    await sleep(500);
    assert.equal(catHub.received.length, 0); pass();
    assert.deepEqual((await list('bob', ab)).map(m => [m.id, m.mine, m.canDelete]), [[hello, false, false]]); pass();
    // Outsiders: another member, staff, no token.
    assert.deepEqual(code(await call(tokens.cat, `/api/v1/conversations/${ab}`)), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.cat, `/api/v1/conversations/${ab}/messages`)), [404, 'not-available']); pass();
    assert.deepEqual(code(await send('cat', ab, 'hi')), [404, 'not-available']); pass();
    const staff = await createStaff('msg-sup');
    assert.equal((await call(root, '/api/v1/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff);
    assert.ok([401, 403].includes((await call(sup, `/api/v1/conversations/${ab}/messages`)).status)); pass();
    assert.equal((await call(null, '/api/v1/conversations')).status, 401); pass();
    // Moderators have no route to conversations (rule 20).
    for (const path of ['/api/v1/admin/conversations', `/api/v1/admin/conversations/${ab}`, `/api/v1/admin/conversations/${ab}/messages`])
    { assert.equal((await call(root, path)).status, 404, path); pass(); }

    // Conversations list: newest first, the other person only.
    assert.deepEqual((await call(tokens.bob, '/api/v1/conversations')).body.map(c => [c.id, c.person.id]), [[ab, pid.ana]]); pass();

    // Delete for both (rule 8): the sender, within five minutes, once; both clients are told.
    r = await send('bob', ab, 'Fictional reply');
    const reply = r.body.id;
    assert.deepEqual(code(await call(tokens.ana, `/api/v1/messages/${reply}`, { method: 'DELETE' })), [404, 'not-available']); pass(); // not hers
    assert.equal((await call(tokens.bob, `/api/v1/messages/${reply}`, { method: 'DELETE' })).status, 204); pass();
    assert.ok(await waitFor(() => anaHub.received.some(x => x.target === 'deleted' && x.payload.messageId === reply))); pass();
    assert.ok(!(await list('ana', ab)).some(m => m.id === reply) && !(await list('bob', ab)).some(m => m.id === reply)); pass();
    assert.deepEqual(code(await call(tokens.bob, `/api/v1/messages/${reply}`, { method: 'DELETE' })), [404, 'not-available']); pass(); // once
    const late = (await send('bob', ab, 'Fictional late')).body.id;
    await sql(`UPDATE messages SET sent_at = now() - interval '6 minutes' WHERE id = '${late}'`);
    assert.deepEqual(code(await call(tokens.bob, `/api/v1/messages/${late}`, { method: 'DELETE' })), [409, 'delete-window-passed']); pass();
    assert.equal((await list('ana', ab)).find(m => m.id === late)?.canDelete, false); pass();

    // Hide for oneself (rule 9): a message, then the whole conversation; a new message brings it back with only new messages.
    assert.equal((await call(tokens.ana, `/api/v1/messages/${late}/hide`, { method: 'POST' })).status, 204); pass();
    assert.ok(!(await list('ana', ab)).some(m => m.id === late) && (await list('bob', ab)).some(m => m.id === late)); pass();
    assert.equal((await call(tokens.cat, `/api/v1/messages/${late}/hide`, { method: 'POST' })).status, 404); pass();
    assert.equal((await call(tokens.bob, `/api/v1/conversations/${ab}/hide`, { method: 'POST' })).status, 204); pass();
    assert.ok(!(await call(tokens.bob, '/api/v1/conversations')).body.some(c => c.id === ab)); pass();
    assert.equal((await list('bob', ab)).length, 0); pass();
    await sleep(50);
    const after = (await send('ana', ab, 'Fictional are you there')).body.id;
    assert.ok((await call(tokens.bob, '/api/v1/conversations')).body.some(c => c.id === ab)); pass();
    assert.deepEqual((await list('bob', ab)).map(m => m.id), [after]); pass();
    assert.ok((await list('ana', ab)).length >= 2); pass(); // ana's view is untouched

    // Paging: 50 at a time, newest first, with a cursor.
    // Seeded in SQL, so the 30-a-minute limit (checked below) doesn't throttle the rest of the suite.
    await sql(`INSERT INTO messages (id, conversation_id, sender_account_id, kind, body, sent_at)
      SELECT gen_random_uuid(), '${ab}', CASE WHEN i % 2 = 0 THEN '${ids.bob}'::uuid ELSE '${ids.ana}'::uuid END, 'Text', 'Fictional message ' || i, now() - interval '1 hour' + i * interval '1 second'
      FROM generate_series(0, 54) AS i`);
    await sql(`UPDATE conversations SET last_message_at = now() WHERE id = '${ab}'`);
    r = await call(tokens.ana, `/api/v1/conversations/${ab}/messages`);
    assert.deepEqual([r.body.items.length, r.body.more], [50, true]); pass();
    const older = await call(tokens.ana, `/api/v1/conversations/${ab}/messages?before=${encodeURIComponent(r.body.before)}`);
    assert.ok(older.body.items.length > 0 && older.body.items.every(m => new Date(m.sentAt) < new Date(r.body.before))); pass();

    // Reports (rules 10, 18–21): a message or the conversation, with the message and up to twenty before it as evidence.
    const reported = r.body.items.find(m => !m.mine).id;
    r = await call(tokens.ana, '/api/v1/reports', { method: 'POST', body: { subjectType: 'message', subjectId: reported, category: 'harassment', text: null, block: false } });
    assert.deepEqual([r.status, r.body.subjectType], [201, 'message']); pass();
    const evidence = JSON.parse(await sql(`SELECT e.snapshot FROM case_evidence e JOIN reports r ON r.id = e.report_id WHERE r.subject_ref = '${reported}'`));
    assert.equal(evidence.messages.length, 21); pass();
    assert.equal(evidence.messages.at(-1).id, reported); pass();
    assert.ok(evidence.messages.every(m => typeof m.text === "string" && typeof m.fromSubject === "boolean")); pass();
    const mine = r.body && (await list('ana', ab)).find(m => m.mine).id;
    assert.deepEqual(code(await call(tokens.ana, '/api/v1/reports', { method: 'POST', body: { subjectType: 'message', subjectId: mine, category: 'spam', block: false } })), [404, 'not-available']); pass(); // her own
    assert.deepEqual(code(await call(tokens.cat, '/api/v1/reports', { method: 'POST', body: { subjectType: 'message', subjectId: reported, category: 'spam', block: false } })), [404, 'not-available']); pass(); // not hers
    assert.deepEqual(code(await call(tokens.cat, '/api/v1/reports', { method: 'POST', body: { subjectType: 'conversation', subjectId: ab, category: 'spam', block: false } })), [404, 'not-available']); pass();
    r = await call(tokens.bob, '/api/v1/reports', { method: 'POST', body: { subjectType: 'conversation', subjectId: ab, category: 'spam', text: null, block: false } });
    assert.equal(r.status, 201); pass();
    // A message deleted for both is still evidence (D165).
    const regret = (await send('bob', ab, 'Fictional message to regret')).body.id;
    await call(tokens.bob, `/api/v1/messages/${regret}`, { method: 'DELETE' });
    assert.equal((await call(tokens.ana, '/api/v1/reports', { method: 'POST', body: { subjectType: 'message', subjectId: regret, category: 'harassment', block: false } })).status, 201); pass();
    assert.match(await sql(`SELECT e.snapshot FROM case_evidence e JOIN reports r ON r.id = e.report_id WHERE r.subject_ref = '${regret}'`), /"deleted":true,"text":"Fictional message to regret"/); pass();

    // 30 messages a minute per sender (D165).
    const burst = (await start('ana', 'res')).body.id;
    let limited = 0;
    for (let i = 0; i < 31; i++) if ((await send('res', burst, `Fictional burst ${i}`)).status === 429) limited++;
    assert.equal(limited, 1); pass();
    await sql(`UPDATE messages SET sent_at = sent_at - interval '2 minutes' WHERE sender_account_id = '${ids.res}'`);

    // Bots reply only in conversations members start (rule 11).
    const botConversation = (await start('bot', 'ana')).body.id;
    assert.deepEqual(code(await send('bot', botConversation, 'Fictional bot hello')), [403, 'bot-cannot-initiate']); pass();
    assert.equal((await send('ana', botConversation, 'Fictional hello bot')).status, 201); pass();
    assert.equal((await send('bot', botConversation, 'Fictional bot reply')).status, 201); pass();

    // A traveller connected through a local's request before arriving may reply but not write first (D60, D149).
    const trip = (await call(tokens.trv, '/api/v1/me/trips', { method: 'POST', body: { cityId: ADELAIDE, arrival: plus(0), departure: plus(3), visible: true, itineraryId: null } })).body;
    const visitRequest = await call(tokens.lou, '/api/v1/requests', { method: 'POST', body: { to: pid.trv, mode: 'friends', trip: trip.id } });
    assert.equal(visitRequest.status, 201);
    assert.equal((await call(tokens.trv, `/api/v1/requests/${visitRequest.body.id}/accept`, { method: 'POST' })).status, 200);
    const travel = (await start('trv', 'lou')).body.id;
    assert.deepEqual(code(await send('trv', travel, 'Fictional first')), [403, 'arrival-required']); pass();
    assert.equal((await send('lou', travel, 'Fictional welcome')).status, 201); pass();
    assert.equal((await send('trv', travel, 'Fictional thanks')).status, 201); pass();

    // Restricted and suspended members take no part (rule 15, D160).
    const resConversation = (await start('ana', 'res')).body.id, susConversation = (await start('ana', 'sus')).body.id;
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the messaging suite.', days: 2, hideFromDiscovery: true, noNewRequests: true, noMessages: true } })).status, 200);
    assert.equal((await call(root, `/api/v1/admin/accounts/${ids.sus}/sanctions`, { method: 'POST', body: { action: 'suspend', reason: 'Fictional suspension for the messaging suite.', days: 2 } })).status, 200);
    assert.deepEqual(code(await send('ana', resConversation, 'hi')), [404, 'not-available']); pass();
    assert.deepEqual(code(await send('ana', susConversation, 'hi')), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ana, `/api/v1/conversations/${susConversation}`)).body.canSend, false); pass();
    assert.deepEqual(code(await send('res', resConversation, 'hi')), [403, 'account-restricted']); pass();
    assert.deepEqual(code(await call(tokens.sus, '/api/v1/conversations')), [403, 'account-suspended']); pass();
    assert.equal((await listen(hub, { token: tokens.sus })).open, false); pass(); // the hub refuses barred members too

    // A block closes the conversation at once and for good (rules 12–14).
    const dd = (await start('ana', 'dee')).body.id;
    await send('ana', dd, 'Fictional before the block');
    assert.equal((await call(tokens.dee, '/api/v1/blocks', { method: 'POST', body: { profileId: pid.ana } })).status, 204); pass();
    assert.deepEqual(code(await send('ana', dd, 'hi')), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.ana, `/api/v1/conversations/${dd}/messages`)), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.dee, `/api/v1/conversations/${dd}/messages`)), [404, 'not-available']); pass();
    assert.ok(!(await call(tokens.ana, '/api/v1/conversations')).body.some(c => c.id === dd)); pass();
    assert.equal(await sql(`SELECT status FROM connection_requests WHERE pair_low = LEAST('${ids.ana}'::uuid, '${ids.dee}'::uuid) AND pair_high = GREATEST('${ids.ana}'::uuid, '${ids.dee}'::uuid)`), 'Ended'); pass();
    assert.equal((await call(tokens.dee, `/api/v1/blocks/${pid.ana}`, { method: 'DELETE' })).status, 204);
    assert.deepEqual(code(await send('ana', dd, 'hi')), [404, 'not-available']); pass(); // unblocking restores neither
    assert.deepEqual(code(await call(tokens.ana, `/api/v1/conversations/${dd}/messages`)), [404, 'not-available']); pass();
    assert.deepEqual(code(await start('ana', 'dee')), [404, 'not-available']); pass(); // no longer connected
    assert.ok(!(await call(tokens.ana, '/api/v1/connections')).body.items.some(i => i.person.id === pid.dee)); pass();
    await connect('ana', 'dee');
    r = await start('ana', 'dee');
    assert.ok(r.status === 201 && r.body.id !== dd); pass();
    assert.equal((await list('ana', r.body.id)).length, 0); pass();
    // The member can still report the closed conversation they were part of.
    assert.equal((await call(tokens.ana, '/api/v1/reports', { method: 'POST', body: { subjectType: 'conversation', subjectId: dd, category: 'harassment', block: false } })).status, 201); pass();

    // No message text in the audit, ever; ids only.
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE detail LIKE '%Fictional%'`), '0'); pass();
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE action IN ('conversation.started', 'message.deleted', 'conversation.closed')`)) >= 3); pass();

    // The purge job (D165): deleted text erased after 30 days.
    await sql(`UPDATE messages SET deleted_at = now() - interval '31 days' WHERE id = '${regret}'`);
    assert.match(await cli('messages', 'purge'), /deleted message texts erased/); pass();
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${regret}'`), ''); pass();
    assert.equal(await sql(`SELECT count(*) FROM messages WHERE id = '${reply}' AND body <> ''`), '1'); pass(); // deleted today: kept

    // Web: the relay pushes to the browser live; messages go through /bff/; other origins can't listen.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.ana.email); await page.fill('#password', members.ana.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/v1/conversations')).status(), 200); pass();
    assert.equal((await bff(`api/v1/conversations/${ab}/messages`)).status(), 200); pass();
    assert.equal((await bff('api/v1/admin/conversations')).status(), 404); pass();
    const cookie = (await context.cookies(web)).map(c => `${c.name}=${c.value}`).join('; ');
    const relay = `${web}/bff/hubs/messages`;
    // The same cookie from the page's own origin listens; from another origin it doesn't.
    const good = await listen(relay, { headers: { Cookie: cookie, Origin: web } });
    sockets.push(good);
    assert.equal(good.open, true, 'relay status ' + good.status); pass();
    await sleep(1500); // the relay opens its own API hub connection after the browser's handshake
    const relayed = (await send('bob', ab, 'Fictional through the relay')).body.id;
    assert.ok(await waitFor(() => good.received.some(x => x.target === 'message' && x.payload.id === relayed && x.payload.mine === false)),
      'relay socket open ' + good.open + ' got ' + JSON.stringify(good.received.map(x => x.target))); pass();
    const evil = await listen(relay, { headers: { Cookie: cookie, Origin: 'http://evil.example' } });
    assert.ok(evil.status !== 200 || !evil.open, 'cross-origin relay status ' + evil.status); pass();
    const noCookie = await listen(relay, { headers: { Origin: web } });
    assert.ok(noCookie.status !== 200 || !noCookie.open); pass();
    await page.goto(web + `/chats/${ab}`);
    await page.waitForSelector('[data-conversation="ready"][data-live="on"]');
    assert.equal(await page.locator('nav.tab-bar a[data-tab="Chats"]').getAttribute('aria-current'), 'page'); pass();
    const before = await page.locator('[data-message]').count();
    const live = (await send('bob', ab, 'Fictional live from Bob')).body.id;
    assert.equal(evil.received?.length ?? 0, 0); pass();
    await page.waitForSelector(`[data-message="${live}"]`, { timeout: 8000 });
    assert.equal(await page.locator(`[data-message="${live}"]`).getAttribute('data-mine'), 'false'); pass();
    assert.equal(await page.locator('[data-message]').count(), before + 1); pass();
    await page.fill('[data-message-input]', 'Fictional typed in Edge');
    await page.click('[data-send]');
    await page.waitForFunction(() => [...document.querySelectorAll('[data-mine="true"] p')].some(p => p.textContent === 'Fictional typed in Edge'));
    assert.ok((await list('bob', ab)).some(m => m.text === 'Fictional typed in Edge' && !m.mine)); pass();
    const typed = (await list('ana', ab)).find(m => m.text === 'Fictional typed in Edge').id;
    await page.click(`[data-delete="${typed}"]`);
    await page.waitForSelector(`[data-message="${typed}"]`, { state: 'detached' });
    assert.ok(!(await list('bob', ab)).some(m => m.id === typed)); pass();
    // Bob deletes his live message: it disappears from Ana's page without a reload.
    assert.equal((await call(tokens.bob, `/api/v1/messages/${live}`, { method: 'DELETE' })).status, 204);
    await page.waitForSelector(`[data-message="${live}"]`, { state: 'detached', timeout: 8000 });
    pass();
    await page.goto(web + '/chats');
    await page.waitForSelector('[data-chats="ready"]');
    assert.ok(await page.locator(`[data-chat="${ab}"]`).count() === 1); pass();
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= 390)); pass();
    // From a connected person's profile, "Message" opens the conversation.
    await page.goto(web + `/people/${pid.bob}`);
    await page.click('[data-message-person]');
    await page.waitForURL(web + `/chats/${ab}`);
    pass();
    await context.close();

    console.log(`PASS: messaging (${passed} checks).`);
  } finally {
    for (const s of sockets) s.close?.();
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
