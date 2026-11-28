// Issue 544 (D431, D455): share one chosen location in a chat. API: point and label checks, only the two members, the chat's
// send rules (unanswered limit), delete for both (the place goes at once), report evidence, label screening, the audit and
// notifications never hold the place, a block ends it. Web: + → Share a location, denied permission, the device reading,
// pasted coordinates and links, preview before sending, the card with maps links (coordinates only, no map image), desktop
// and phone widths. Owned stack; every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, call, sql, matchReady } = require('../auth/local-tokens.cjs');
const audit = require('../accessibility/audit.cjs');
const output = process.env.SWOOVR_SCREENSHOTS;
const ADELAIDE = 2078025;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const names = ['me', 'ada', 'eve', 'blk'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('cloc-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, pid = {}, ids = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional CLOC ' + n, homeCityId: ADELAIDE } });
      assert.equal(saved.status, 200, n);
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT account_id FROM member_profiles WHERE public_id = '${pid[n]}'`);
      await call(tokens[n], '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    const connect = async (from, to) => {
      const ask = await call(tokens[from], '/api/requests', { method: 'POST', body: { to: pid[to], mode: 'dating' } });
      assert.equal(ask.status, 201, `${from}→${to}`);
      assert.equal((await call(tokens[to], `/api/requests/${ask.body.id}/accept`, { method: 'POST' })).status, 200);
      return (await call(tokens[from], '/api/conversations', { method: 'POST', body: { to: pid[to], mode: 'dating' } })).body.id;
    };
    const chat = await connect('ada', 'me'), blkChat = await connect('blk', 'me');
    const share = (who, conversation, body) => call(tokens[who], `/api/conversations/${conversation}/locations`, { method: 'POST', body });
    const listed = async (who, conversation, id) => (await call(tokens[who], `/api/conversations/${conversation}/messages`)).body.items.find(m => m.id === id);
    const place = { latitude: -34.928512345, longitude: 138.600712345, label: 'Fictional Café, front door' };

    // Refused: no point, out of range, Null Island, a long or two-line label; not a member of the chat.
    for (const bad of [{}, { latitude: -34.9 }, { latitude: 91, longitude: 1 }, { latitude: 1, longitude: -181 }, { latitude: 0, longitude: 0 }])
      assert.deepEqual(code(await share('me', chat, bad)), [400, 'invalid-location'], JSON.stringify(bad));
    pass();
    assert.deepEqual(code(await share('me', chat, { ...place, label: 'x'.repeat(81) })), [400, 'invalid-location'], 'label over 80'); pass();
    assert.deepEqual(code(await share('me', chat, { ...place, label: 'two\nlines' })), [400, 'invalid-location'], 'label on one line'); pass();
    assert.equal((await share('eve', chat, place)).status, 404, 'not in the chat'); pass();

    // Shared: a location message with the exact point (5 places) and its label, for both members.
    let r = await share('me', chat, place);
    assert.equal(r.status, 201); pass();
    const sent = r.body;
    assert.deepEqual([sent.kind, sent.text, sent.location?.latitude, sent.location?.longitude, sent.location?.label, sent.canDelete], ['location', place.label, -34.92851, 138.60071, place.label, true]); pass();
    // Unread before Ada opens the chat (reading the messages marks it read).
    assert.equal((await call(tokens.ada, '/api/me/notifications')).body.unreadChats, 1, 'delivered like a message'); pass();
    const theirs = await listed('ada', chat, sent.id);
    assert.deepEqual([theirs?.mine, theirs?.location?.latitude, theirs?.location?.longitude, theirs?.canDelete], [false, -34.92851, 138.60071, false]); pass();
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${sent.id}'`), 'geo:-34.92851,138.60071\n' + place.label, 'kept in the message text'); pass();
    // The label is screened like message text; a location without a label has nothing to screen.
    // Text screening may be off on this stack: a plain text message shows whether it runs, and the label must match it.
    const plain = (await call(tokens.me, `/api/conversations/${chat}/messages`, { method: 'POST', body: { text: 'Fictional see you there' } })).body;
    const screening = await sql(`SELECT count(*) FROM text_harm_intent WHERE source_id = '${plain.id}'`);
    assert.equal(await sql(`SELECT count(*) FROM text_harm_intent WHERE source_id = '${sent.id}'`), screening, 'the label is screened like message text (' + screening + ')'); pass();
    const bare = (await share('me', chat, { latitude: -34.93, longitude: 138.6 })).body;
    assert.deepEqual([bare.text, bare.location.label], ['', null]); pass();
    assert.equal(await sql(`SELECT count(*) FROM text_harm_intent WHERE source_id = '${bare.id}'`), '0'); pass();
    // Never the place in the audit, notifications or deliveries.
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'message.location' AND actor_account_id = '${ids.me}'`)) >= 2, 'audited'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'message.location' AND (detail LIKE '%138.6%' OR detail ILIKE '%Café%')`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM notifications n LEFT JOIN notification_deliveries d ON d.notification_id = n.id WHERE n.account_id = '${ids.ada}' AND (n::text LIKE '%138.6%' OR d::text LIKE '%138.6%' OR n::text ILIKE '%Café%')`), '0', 'no place in notifications'); pass();

    // Report: the message (with its place) is case evidence.
    const reported = await call(tokens.ada, '/api/reports', { method: 'POST', body: { subjectType: 'message', subjectId: sent.id, category: 'harassment', text: null, block: false } });
    assert.equal(reported.status, 201);
    const evidence = JSON.parse(await sql(`SELECT snapshot FROM case_evidence WHERE report_id = '${reported.body.id}' AND kind = 'message'`));
    assert.equal(evidence.messages.find(m => m.id === sent.id)?.kind, 'location', 'evidence: ' + JSON.stringify(evidence).slice(0, 300)); pass();

    // Delete for both: the place disappears for both at once; the text is kept 30 days for investigations (D167).
    assert.equal((await call(tokens.me, `/api/messages/${bare.id}`, { method: 'DELETE' })).status, 204); pass();
    const gone = await listed('ada', chat, bare.id);
    assert.deepEqual([gone?.deleted, gone?.location ?? null, gone?.text], [true, null, ''], 'no place once deleted'); pass();
    assert.equal(await sql(`SELECT body FROM messages WHERE id = '${bare.id}'`), 'geo:-34.93,138.6', 'kept for the evidence window'); pass();
    // Removed by Swoovr: no place either.
    const removed = (await share('me', chat, { latitude: -34.9, longitude: 138.61 })).body;
    await sql(`UPDATE messages SET moderation_removed_at = now() WHERE id = '${removed.id}'`);
    const hidden = await listed('ada', chat, removed.id);
    assert.deepEqual([hidden?.kind, hidden?.location ?? null], ['removed-by-moderation', null]); pass();

    // The unanswered limit counts locations, and a location from the other member is a reply.
    const limit = n => call(tokens.ada, '/api/me/notification-settings', { method: 'PUT', body: { unansweredLimit: n } });
    assert.equal((await limit(3)).status, 200);
    for (let i = 0; i < 3; i++) await share('me', chat, { latitude: -34.9 - i / 100, longitude: 138.6 });
    assert.deepEqual(code(await share('me', chat, place)), [409, 'unanswered-limit'], 'locations count'); pass();
    assert.equal((await share('ada', chat, { latitude: -34.95, longitude: 138.62 })).status, 201);
    assert.equal((await share('me', chat, place)).status, 201, 'their location is a reply'); pass();
    assert.equal((await limit(0)).status, 200);

    // A block closes the chat: nothing more can be shared.
    assert.equal((await share('blk', blkChat, place)).status, 201);
    assert.equal((await call(tokens.me, '/api/blocks', { method: 'POST', body: { profileId: pid.blk } })).status, 204);
    assert.equal((await share('blk', blkChat, place)).status, 404, 'blocked'); pass();

    // Web: + → Share a location.
    const signIn = async context => {
      const page = await context.newPage();
      await page.goto(web + '/account/sign-in?intro=done');
      await page.fill('#username', members.me.email); await page.fill('#password', members.me.password);
      await Promise.all([page.waitForURL(url => !url.pathname.startsWith('/account/sign-in')), page.click('#kc-login')]);
      return page;
    };
    const open = async page => {
      await page.goto(web + `/chats/${chat}`);
      await page.waitForSelector('[data-conversation=ready]');
      await page.click('[data-attach]');
      await page.click('[data-attach-location]');
      await page.waitForSelector('[data-location-form]');
    };
    // Permission denied: a clear message, nothing sent, Share stays off.
    let context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    let page = await signIn(context);
    await open(page);
    await page.click('[data-location-current]');
    await page.waitForSelector('[data-location-read=denied]');
    assert.ok(await page.isDisabled('[data-location-send]'), 'nothing to share yet'); pass();
    // Pasted: a maps link becomes the preview; a nonsense one is explained.
    await page.fill('[data-location-paste]', 'Fictional nowhere');
    await page.waitForSelector('[data-location-paste-invalid]'); pass();
    await page.fill('[data-location-paste]', 'https://www.google.com/maps/place/x/@-34.9211,138.5987,17z');
    await page.fill('[data-location-label]', 'Fictional Library steps');
    await page.waitForSelector('[data-location-preview]');
    assert.equal((await page.textContent('[data-location-preview-coordinates]')).trim(), '-34.9211, 138.5987'); pass();
    assert.equal(await page.locator('[data-location-form] img, [data-location-form] iframe').count(), 0, 'no map image in the form'); pass();
    assert.deepEqual(await page.evaluate(audit), [], 'page accessibility with the form open'); pass();
    if (output) await page.screenshot({ path: output + '/chat-location-form.png' });
    const requests = [];
    page.on('request', q => { if (!q.url().startsWith(web)) requests.push(q.url()); });
    await page.click('[data-location-send]');
    const card = page.locator('[data-message-kind=location]').filter({ has: page.locator('[data-location-title]', { hasText: 'Fictional Library steps' }) });
    await card.locator('[data-location-title]').waitFor();
    assert.equal((await card.locator('[data-location-title]').textContent()).trim(), 'Fictional Library steps'); pass();
    const links = await card.locator('[data-location-open]').evaluateAll(as => as.map(a => [a.dataset.locationOpen, a.href, a.target, a.rel]));
    assert.deepEqual(links.map(l => l[0]), ['apple', 'google', 'osm']); pass();
    assert.ok(links.every(([, href, target, rel]) => href.includes('-34.9211') && href.includes('138.5987') && !/Library/i.test(href) && target === '_blank' && rel.includes('noopener')), 'links: coordinates only ' + JSON.stringify(links)); pass();
    assert.equal(await page.locator('.chat-thread img.location-map, .location-card img, .location-card iframe').count(), 0, 'no map image in the chat'); pass();
    assert.deepEqual(requests.filter(u => /maps|openstreetmap|tile/i.test(u)), [], 'no map provider contacted'); pass();
    if (output) await page.screenshot({ path: output + '/chat-location-card.png' });
    // PR 631 review: a location from the other member is a reply, live: the locked composer opens without a reload.
    assert.equal((await call(tokens.ada, '/api/me/notification-settings', { method: 'PUT', body: { unansweredLimit: 3 } })).status, 200);
    for (let i = 0; i < 3; i++) await call(tokens.me, `/api/conversations/${chat}/messages`, { method: 'POST', body: { text: 'Fictional line ' + i } });
    await page.reload(); await page.waitForSelector('[data-conversation=ready]');
    await page.waitForSelector('[data-composer-state=awaiting-reply]'); pass();
    assert.equal((await share('ada', chat, { latitude: -34.97, longitude: 138.63 })).status, 201);
    await page.waitForSelector('[data-composer-state=open]', { timeout: 15000 }); pass();
    assert.equal((await call(tokens.ada, '/api/me/notification-settings', { method: 'PUT', body: { unansweredLimit: 0 } })).status, 200);
    await context.close();

    // The device reading (granted) at phone width.
    context = await browser.newContext({ viewport: { width: 390, height: 844 }, geolocation: { latitude: -34.9285, longitude: 138.6007, accuracy: 25 }, permissions: ['geolocation'] });
    page = await signIn(context);
    await open(page);
    await page.click('[data-location-current]');
    await page.waitForSelector('[data-location-read=ok]');
    assert.ok((await page.textContent('[data-location-preview-coordinates]')).includes('-34.9285, 138.6007'), 'the reading is the preview'); pass();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'no sideways scroll at 390 px'); pass();
    if (output) await page.screenshot({ path: output + '/chat-location-phone.png' });
    await page.click('[data-location-send]');
    const plainCard = page.locator('[data-message-kind=location]').filter({ has: page.locator('[data-location-coordinates]', { hasText: '-34.9285, 138.6007' }) });
    await plainCard.first().waitFor();
    assert.equal((await plainCard.first().locator('[data-location-title]').textContent()).trim(), 'Shared location', 'no label: a plain title'); pass();
    await context.close();

    console.log(`PASS: chat-location (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
