// D213/Q85 and D214/Q86: real local API/BFF/browser flow, fictional adult fixtures only.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, memberToken, cli, call, sql, claims, matchReady, mailTo } = require('../auth/local-tokens.cjs');
const byMe = "You've ended this conversation. Your connections with this member have been closed, and neither of you can send further messages here.";
const byOther = 'This conversation has ended. You can still view your previous messages, but replies are no longer available.';
const farewell = "Thanks for chatting. I don't think we're the right match, but I wish you all the best.";
const neutral = 'A conversation has ended on Swoovr.';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  if (output) await fs.mkdir(output, { recursive: true });
  try {
    const members = {}, tk = {}, id = {}, pid = {};
    for (const n of ['ana', 'ben', 'cam', 'dee']) members[n] = await createMember('end-' + n);
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    for (const n of Object.keys(members)) {
      tk[n] = await memberToken(browser, members[n]);
      const profile = await call(tk[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional End ' + n, homeCityId: 2078025 } });
      assert.equal(profile.status, 200); pid[n] = profile.body.publicId;
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      await matchReady(tk[n]);
      for (const mode of ['friends', 'dating']) assert.equal((await call(tk[n], '/api/me/modes/' + mode, { method: 'PUT', body: { enabled: true, visible: true } })).status, 200);
    }
    const connect = async (a, b, mode) => {
      const r = await call(tk[a], '/api/requests', { method: 'POST', body: { to: pid[b], mode } });
      assert.equal(r.status, 201, JSON.stringify(r.body));
      assert.equal((await call(tk[b], `/api/requests/${r.body.id}/accept`, { method: 'POST' })).status, 200);
    };
    const start = async (a, b) => (await call(tk[a], '/api/conversations', { method: 'POST', body: { to: pid[b] } })).body.id;
    const messages = async (n, c) => (await call(tk[n], `/api/conversations/${c}/messages`)).body.items;
    const end = (n, c, closing = null) => call(tk[n], `/api/conversations/${c}/end`, { method: 'POST', body: { closing } });
    for (const mode of ['friends', 'dating']) await connect('ana', 'ben', mode);
    const ab = await start('ana', 'ben');
    await call(tk.ana, `/api/conversations/${ab}/messages`, { method: 'POST', body: { text: 'Fictional history remains.' } });
    const results = await Promise.all(Array.from({ length: 4 }, () => end('ana', ab)));
    assert.deepEqual(results.map(r => r.status), [200, 200, 200, 200], 'idempotent concurrent ends'); pass();
    assert.ok(results.every(r => r.body.closed && !r.body.canSend)); pass();
    for (const [n, text] of [['ana', byMe], ['ben', byOther]]) {
      const list = await messages(n, ab), system = list.filter(m => m.kind === 'system');
      assert.equal(system.length, 1); assert.equal(system[0].text, text); assert.equal(system[0].mine, false); assert.equal(system[0].canDelete, false); pass();
      assert.ok(list.some(m => m.text === 'Fictional history remains.')); pass();
      for (const field of ['senderAccountId', 'initiatedBy', 'endedBy', 'closedReason']) assert.ok(!(field in system[0])); pass();
    }
    const category = (await call(tk.ben, '/api/report-categories')).body.find(c => c.id === 'other');
    const systemId = (await messages('ben', ab)).find(m => m.kind === 'system').id;
    for (const who of ['ana', 'ben']) {
      const report = await call(tk[who], '/api/reports', { method: 'POST', body: { subjectType: 'message', subjectId: systemId, category: category.id, text: 'Fictional system-event privacy check', block: false } });
      assert.equal(report.status, 404, 'system events cannot become an initiating-party oracle through report eligibility'); pass();
    }
    assert.equal(await sql(`SELECT count(*) FROM messages WHERE conversation_id='${ab}' AND kind='System'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action='conversation.ended' AND target_id='${ab}' AND detail='MemberEnded'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM connection_requests WHERE pair_low=least('${id.ana}'::uuid,'${id.ben}'::uuid) AND pair_high=greatest('${id.ana}'::uuid,'${id.ben}'::uuid) AND status='Accepted'`), '0'); pass();
    assert.equal((await end('ben', ab)).status, 200); pass();
    assert.equal((await end('cam', ab)).status, 404); pass();
    assert.equal((await end('ana', ab, 'free text')).status, 400); pass();
    assert.equal((await call(tk.ben, `/api/conversations/${ab}/messages`, { method: 'POST', body: { text: 'No late reply' } })).status, 404); pass();
    assert.equal((await call(tk.ben, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })).body.code, 'recently-disconnected'); pass();
    const notices = (await call(tk.ben, '/api/me/notifications')).body.items.filter(n => n.kind === 'conversationEnded');
    assert.equal(notices.length, 1); assert.equal(notices[0].title, neutral); assert.equal(notices[0].link, `/chats/${ab}`); pass();
    assert.equal((await call(tk.ana, '/api/me/notifications')).body.items.filter(n => n.kind === 'conversationEnded').length, 0); pass();
    // Simulate interruption after closure/centre commit but before dispatch's outcome (no device, no real send).
    const queued = await sql(`SELECT id FROM notifications WHERE dedupe_key='conversation-ended:${ab}' AND account_id='${id.ben}'`);
    await sql(`DELETE FROM notification_deliveries WHERE notification_id='${queued}'`);
    await cli('notifications', 'sweep'); await cli('notifications', 'sweep');
    assert.equal(await sql(`SELECT count(*) FROM notification_deliveries WHERE notification_id='${queued}' AND channel='push'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM notifications WHERE dedupe_key='conversation-ended:${ab}' AND account_id='${id.ben}'`), '1'); pass();
    tk.ben = await memberToken(browser, members.ben);
    const ticket = await call(tk.ben, '/api/me/export', { method: 'POST' }); assert.equal(ticket.status, 200);
    const exported = (await call(tk.ben, '/' + ticket.body.path)).body;
    const exportedSystem = exported.conversations.find(c => c.with === 'Fictional End ana').messages.filter(m => m.kind === 'system');
    assert.equal(exportedSystem.length, 1); assert.equal(exportedSystem[0].from, 'system'); assert.equal(exportedSystem[0].text, byOther); pass();
    const hidden = (await messages('ben', ab)).find(m => m.kind === 'text').id;
    assert.equal((await call(tk.ben, `/api/messages/${hidden}/hide`, { method: 'POST' })).status, 204); pass();
    assert.ok(!(await messages('ben', ab)).some(m => m.id === hidden)); assert.ok((await messages('ana', ab)).some(m => m.id === hidden)); pass();
    assert.equal((await call(tk.ben, `/api/conversations/${ab}/hide`, { method: 'POST' })).status, 204); pass();
    assert.ok(!(await call(tk.ben, '/api/conversations')).body.some(c => c.id === ab)); pass();
    // A valid reconnection after the restriction starts a new conversation and leaves the original closed.
    await sql(`UPDATE connection_requests SET ended_at=now()-interval '31 days' WHERE pair_low=least('${id.ana}'::uuid,'${id.ben}'::uuid) AND pair_high=greatest('${id.ana}'::uuid,'${id.ben}'::uuid) AND status='Disconnected'`);
    await connect('ben', 'ana', 'friends');
    assert.notEqual(await start('ben', 'ana'), ab); assert.equal((await call(tk.ana, `/api/conversations/${ab}`)).body.closed, true); pass();

    // One optional farewell followed by the system event; generic push even when private previews are off.
    await connect('ana', 'dee', 'friends'); const ad = await start('ana', 'dee');
    const device = (await call(tk.dee, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token: 'fictional-end-' + ad } })).body.id;
    await call(tk.dee, '/api/me/notification-settings', { method: 'PUT', body: { privatePreviews: false } });
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } }); const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done'); await page.fill('#username', members.dee.email); await page.fill('#password', members.dee.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.goto(web + `/chats/${ad}`); await page.waitForSelector('[data-live="on"]');
    assert.equal((await end('ana', ad, 'thanks')).status, 200); pass();
    const list = await messages('dee', ad);
    assert.deepEqual(list.slice(0, 2).map(m => [m.kind, m.text]), [['system', byOther], ['farewell', farewell]]); pass();
    await page.waitForSelector('[data-closed-conversation]');
    assert.equal(await page.locator('[data-composer]').count(), 0); assert.equal(await page.locator('[data-message-kind="system"]').count(), 1); pass();
    assert.equal(await page.locator('[data-report-conversation]').count(), 1); assert.equal(await page.locator('[data-block-conversation]').count(), 1); pass();
    assert.ok((await page.textContent('[data-closed-conversation]')).includes('Report and block options remain available.')); pass();
    if (output) await page.screenshot({ path: output + '/web-conversation-ended-recipient.png', fullPage: true });
    const pushes = await mailTo('push-' + device.replaceAll('-', '') + '@simulated.swoovr.invalid');
    const push = pushes.find(t => t.includes(neutral)); assert.ok(push); assert.ok(!push.includes(farewell) && !push.includes('Fictional End')); pass();
    // Recover ambiguous acceptance without repeating the first device; a new device still receives its one attempt.
    const endingNotice = await sql(`SELECT id FROM notifications WHERE dedupe_key='conversation-ended:${ad}' AND account_id='${id.dee}'`);
    const firstCount = (await mailTo('push-' + device.replaceAll('-', '') + '@simulated.swoovr.invalid')).length;
    await sql(`UPDATE notification_deliveries SET status='attempting' WHERE notification_id='${endingNotice}' AND device_id='${device}'`);
    await sql(`DELETE FROM notification_deliveries WHERE notification_id='${endingNotice}' AND channel='dispatch'`);
    const secondDevice = (await call(tk.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: 'fictional-second-' + ad } })).body.id;
    await cli('notifications', 'sweep'); await cli('notifications', 'sweep');
    assert.equal((await mailTo('push-' + device.replaceAll('-', '') + '@simulated.swoovr.invalid')).length, firstCount); pass();
    assert.equal(await sql(`SELECT status FROM notification_deliveries WHERE notification_id='${endingNotice}' AND device_id='${device}'`), 'unknown'); pass();
    assert.equal((await mailTo('push-' + secondDevice.replaceAll('-', '') + '@simulated.swoovr.invalid')).length, 1); pass();
    await page.goto(web + '/me/standing'); await page.waitForSelector('[data-standing="ready"]');
    assert.equal(await page.getAttribute('[data-support-web]', 'href'), 'https://swoovr.com/support'); assert.equal(await page.getAttribute('[data-support-email]', 'href'), 'mailto:support@swoovr.com'); pass();
    if (output) await page.screenshot({ path: output + '/web-standing-support.png', fullPage: true });
    await page.goto(web + '/privacy'); assert.ok((await page.textContent('[data-privacy-section="contact"]')).includes('support@swoovr.com')); pass();
    await context.close();

    // All connected modes muted: centre entry remains, no push.
    for (const mode of ['friends', 'dating']) await connect('cam', 'ben', mode);
    const bc = await start('cam', 'ben');
    const mutedDevice = (await call(tk.ben, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: 'fictional-muted-' + bc } })).body.id;
    await call(tk.ben, '/api/me/notification-settings', { method: 'PUT', body: { mutedModes: ['friends', 'dating'] } });
    assert.equal((await end('cam', bc)).status, 200); pass();
    assert.equal((await mailTo('push-' + mutedDevice.replaceAll('-', '') + '@simulated.swoovr.invalid')).length, 0); pass();
    assert.equal((await call(tk.ben, '/api/me/notifications')).body.items.filter(n => n.kind === 'conversationEnded' && n.link.endsWith(bc)).length, 1); pass();
    // Competing sends serialize with closure; accepted messages precede its system event, no late replies.
    await connect('cam', 'dee', 'friends'); const cd = await start('cam', 'dee');
    const competing = await Promise.all([
      ...Array.from({ length: 6 }, (_, i) => call(tk.cam, `/api/conversations/${cd}/messages`, { method: 'POST', body: { text: 'Fictional concurrent ' + i } })),
      end('cam', cd),
    ]);
    assert.equal(competing[6].status, 200); assert.ok(competing.slice(0, 6).every(r => [201, 404].includes(r.status))); pass();
    const concurrentHistory = await messages('dee', cd), closureEvent = concurrentHistory.find(m => m.kind === 'system');
    assert.ok(closureEvent); assert.equal(concurrentHistory[0].kind, 'system');
    assert.equal(concurrentHistory.filter(m => m.kind === 'text').length, competing.slice(0, 6).filter(r => r.status === 201).length); pass();
    assert.ok(concurrentHistory.filter(m => m.kind !== 'system').every(m => Date.parse(m.sentAt) <= Date.parse(closureEvent.sentAt))); pass();
    console.log(`PASS: conversation endings and support (${passed} checks).`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
