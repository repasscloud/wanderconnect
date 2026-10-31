// Local acceptance for slice AJ (#49; policy §10; D135): the notification centre, settings and deliveries. Lock screens
// (the labelled simulated push sender, written to Mailpit) and emails stay private by default; a member's switches,
// muted modes and quiet hours stop pushes and emails but never the centre; messages are never emailed; unread messages
// from one conversation collapse; blocked, restricted, suspended and deleted people never cause a notification and drop
// out of the centre; a restricted member sees account notices only; bots can't start anything; staff and other members
// get nothing. Travel reminders come from `notifications sweep`. Same local stack as tests/messaging; fictional adults.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, createMember, createStaff, memberToken, staffToken, rootAdminToken, cli, sql, call, claims, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025, MELBOURNE = 2158177;
const output = process.env.SWOOVR_SCREENSHOTS;
const todayIn = zone => new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
const plus = (days, zone = 'Australia/Melbourne') => { const d = new Date(todayIn(zone) + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const hhmm = (offsetMinutes) => {
  const now = new Date(new Date().toLocaleString('en-US', { timeZone: 'Australia/Adelaide' }));
  const t = ((now.getHours() * 60 + now.getMinutes() + offsetMinutes) % 1440 + 1440) % 1440;
  return String(Math.floor(t / 60)).padStart(2, '0') + ':' + String(t % 60).padStart(2, '0');
};
const waitFor = async (predicate, ms = 6000) => { for (let i = 0; i < ms / 200; i++) { if (await predicate()) return true; await sleep(200); } return predicate(); };

/** Subject and text of the local mail sink's messages to one address, newest first. */
async function mail(address) {
  const list = await (await fetch('http://127.0.0.1:8025/api/v1/search?query=' + encodeURIComponent(`to:"${address}"`))).json();
  return Promise.all((list.messages ?? []).map(async m => {
    const full = await (await fetch(`http://127.0.0.1:8025/api/v1/message/${m.ID}`)).json();
    return { subject: full.Subject, text: full.Text };
  }));
}
const pushBox = id => `push-${id.replace(/-/g, '')}@simulated.swoovr.invalid`;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bob', 'cat', 'dee', 'res', 'eve', 'bot'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('ntf-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Ntf ' + n, homeCityId: ADELAIDE } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
      if (n === 'bot') continue;
      for (const mode of ['dating', 'friends']) await call(tokens[n], `/api/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tokens[n]);
    }
    await call(root, `/api/admin/accounts/${ids.bot}/bot`, { method: 'PUT', body: { bot: true } });
    await call(root, `/api/admin/accounts/${ids.bot}/bot-markets`, { method: 'PUT', body: { markets: ['adelaide'] } });
    await call(root, `/api/admin/accounts/${ids.bot}/bot-modes`, { method: 'PUT', body: { modes: ['friends'] } });
    await call(tokens.bot, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const request = (from, to, mode = 'dating') => call(tokens[from], '/api/requests', { method: 'POST', body: { to: pid[to], mode } });
    const centre = async who => (await call(tokens[who], '/api/me/notifications')).body;
    const deliveries = (who, kind) => sql(`SELECT d.channel || ':' || d.status FROM notification_deliveries d JOIN notifications n ON n.id = d.notification_id
      WHERE n.account_id = '${ids[who]}' AND n.kind = '${kind}' ORDER BY d.id DESC LIMIT 2`).then(t => t.split('\n').filter(Boolean).sort());
    const settings = (who, body) => call(tokens[who], '/api/me/notification-settings', { method: 'PUT', body });

    // ---- Signed out, staff and defaults.
    assert.equal((await call(null, '/api/me/notifications')).status, 401); pass();
    assert.equal((await call('not-a-token', '/api/me/notification-settings')).status, 401); pass();
    const staff = await createStaff('ntf-sup');
    assert.equal((await call(root, '/api/admin/invitations', { method: 'POST', body: { email: staff.email, role: 'support' } })).status, 201);
    const sup = await staffToken(browser, staff); await call(sup, '/api/session');
    for (const path of ['/api/me/notifications', '/api/me/notification-settings', '/api/me/devices'])
    { assert.equal((await call(sup, path)).status, 403, path); pass(); }
    let r = await call(tokens.ana, '/api/me/notification-settings');
    assert.deepEqual(r.body, { messages: true, requests: true, connections: true, travel: true, push: true, email: true, privatePreviews: true,
      quietFrom: null, quietUntil: null, timeZone: 'Australia/Adelaide', mutedModes: [], chatPhotos: true, devices: 0 }); pass();
    assert.deepEqual(await centre('ana'), { items: [], unread: 0 }); pass();

    // ---- Settings validation and tampered bodies.
    for (const bad of [{ quietFrom: '22:00' }, { quietUntil: '07:00' }, { quietFrom: '25:00', quietUntil: '07:00' }, { quietFrom: '9pm', quietUntil: '07:00' },
      { timeZone: 'Mars/Base' }, { timeZone: 'x'.repeat(65) }, { mutedModes: ['nonsense'] }, { mutedModes: ['dating', 'dating'] }])
    { assert.deepEqual(code(await settings('ana', bad)), [400, 'invalid-settings'], JSON.stringify(bad)); pass(); }
    assert.equal((await settings('ana', { push: 'yes' })).status, 400); pass();
    r = await settings('ana', { accountId: ids.bob, email: true, mutedModes: [] });
    assert.equal(r.status, 200); pass();
    assert.equal(await sql(`SELECT count(*) FROM notification_settings WHERE account_id = '${ids.bob}'`), '0'); pass(); // only the caller's own row

    // ---- Devices: platform and token checks, one device per token across accounts, the limit, others' ids.
    for (const bad of [{ platform: 'web', token: 'simulated-a' }, { platform: 'ios' }, { platform: 'ios', token: 'has space' }, { platform: 'ios', token: 'x'.repeat(513) }])
    { assert.deepEqual(code(await call(tokens.ana, '/api/me/devices', { method: 'POST', body: bad })), [400, 'invalid-device']); pass(); }
    const token = 'simulated-ntf-' + Date.now();
    r = await call(tokens.ana, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token } });
    assert.deepEqual([r.status, r.body.platform, r.body.simulated], [201, 'ios', true]); pass();
    const device = r.body.id;
    r = await call(tokens.ana, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token } });
    assert.deepEqual([r.status, r.body.id], [200, device]); pass();
    // The same phone signed in by Bob stops getting Ana's notifications.
    r = await call(tokens.bob, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token } });
    assert.equal(r.status, 201); pass();
    assert.equal((await call(tokens.ana, '/api/me/devices')).body.length, 0); pass();
    assert.deepEqual(code(await call(tokens.ana, `/api/me/devices/${r.body.id}`, { method: 'DELETE' })), [404, 'not-available']); pass(); // Bob's device
    assert.equal((await call(tokens.bob, `/api/me/devices/${r.body.id}`, { method: 'DELETE' })).status, 204); pass();
    assert.deepEqual(code(await call(tokens.bob, `/api/me/devices/${r.body.id}`, { method: 'DELETE' })), [404, 'not-available']); pass(); // already gone
    const installationId = require('node:crypto').randomUUID();
    const installed = await call(tokens.ana, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token: token + '-install-old', installationId } });
    assert.equal(installed.status, 201); pass();
    const rotated = await call(tokens.ana, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token: token + '-install-new', installationId } });
    assert.equal(rotated.body.id, installed.body.id); assert.equal(rotated.status, 200); pass();
    assert.equal(await sql(`SELECT count(*) FROM push_devices WHERE id='${installed.body.id}' AND token_cipher IS NOT NULL AND token_key_version IS NOT NULL AND token NOT LIKE 'simulated-%'`), '1'); pass();
    assert.equal((await call(tokens.bob, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token: token + '-other', installationId } })).status, 404); pass();
    assert.equal((await call(tokens.ana, `/api/me/devices/${installed.body.id}`, { method: 'DELETE' })).status, 204); pass();
    const anaDevice = (await call(tokens.ana, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-ana' } })).body.id;
    const bobDevice = (await call(tokens.bob, '/api/me/devices', { method: 'POST', body: { platform: 'ios', token: token + '-bob' } })).body.id;
    for (let i = 0; i < 9; i++) await call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: `${token}-dee-${i}` } });
    assert.equal((await call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-dee-9' } })).status, 201); pass();
    assert.deepEqual(code(await call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-dee-10' } })), [409, 'device-limit']); pass();
    const inactive = (await call(tokens.dee, '/api/me/devices')).body[0];
    await sql(`UPDATE push_devices SET invalidated_at=now() WHERE id='${inactive.id}'`);
    assert.equal((await call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-dee-replacement' } })).status, 201); pass();
    assert.deepEqual(code(await call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-dee-0' } })), [409, 'device-limit']); pass();
    const capacitySlot = (await call(tokens.dee, '/api/me/devices')).body[0];
    await call(tokens.dee, `/api/me/devices/${capacitySlot.id}`, { method: 'DELETE' });
    const concurrentDevices = await Promise.all(Array.from({ length: 4 }, (_, i) => call(tokens.dee, '/api/me/devices', { method: 'POST', body: { platform: 'android', token: token + '-concurrent-' + i } })));
    assert.equal(concurrentDevices.filter(r => r.status === 201).length, 1); assert.equal(concurrentDevices.filter(r => r.status === 409).length, 3); pass();
    assert.equal((await call(tokens.dee, '/api/me/devices')).body.length, 10); pass();
    for (const d of (await call(tokens.dee, '/api/me/devices')).body) await call(tokens.dee, `/api/me/devices/${d.id}`, { method: 'DELETE' });

    // ---- A request: in the centre with the name and mode; private on the lock screen and in the email.
    r = await request('bob', 'ana');
    assert.equal(r.status, 201); pass();
    const bobRequest = r.body.id;
    let c = await centre('ana');
    assert.deepEqual(c.items.map(i => [i.kind, i.title, i.link, i.read]), [['request', 'Fictional Ntf bob sent you a Dating request', '/connections', false]]); pass();
    assert.equal(c.unread, 1); pass();
    assert.ok(await waitFor(async () => (await mail(pushBox(anaDevice))).length === 1)); pass();
    let [push] = await mail(pushBox(anaDevice));
    assert.equal(push.subject, 'SIMULATED PUSH (not delivered)'); pass();
    assert.ok(push.text.startsWith('SIMULATED: no push notification was sent.') && push.text.includes('Title: Swoovr') && push.text.includes('Body: You have a new request.')); pass();
    assert.ok(!push.text.includes('bob') && !push.text.includes('Dating') && !push.text.includes(token)); pass(); // no name, no mode, no device token
    assert.ok(await waitFor(async () => (await mail(members.ana.email)).length === 1)); pass();
    let [email] = await mail(members.ana.email);
    assert.equal(email.subject, 'Something new on Swoovr'); pass();
    assert.ok(email.text.includes('You have a new request on Swoovr') && !email.text.includes('bob') && !email.text.includes('Dating')); pass();
    assert.deepEqual(await deliveries('ana', 'Request'), ['email:sent', 'push:simulated']); pass();
    // Ids only in the centre's storage: no names or text.
    assert.equal(await sql(`SELECT count(*) FROM notifications WHERE account_id = '${ids.ana}' AND actor_account_id = '${ids.bob}' AND mode_id = 'dating' AND subject_id = '${bobRequest}'`), '1'); pass();

    // ---- Accepting tells the sender (a link to the person in that mode); declining tells nobody.
    assert.equal((await call(tokens.ana, `/api/requests/${bobRequest}/accept`, { method: 'POST' })).status, 200);
    c = await centre('bob');
    assert.deepEqual(c.items.map(i => [i.kind, i.title, i.link]), [['connection', 'Fictional Ntf ana accepted your Dating request', `/people/${pid.ana}?mode=dating`]]); pass();
    r = await request('cat', 'bob', 'friends');
    assert.equal((await call(tokens.bob, `/api/requests/${r.body.id}/decline`, { method: 'POST' })).status, 200);
    assert.equal((await centre('cat')).items.length, 0); pass();

    // ---- Messages: never emailed; unread ones collapse with one push; reading the chat reads them.
    const chat = (await call(tokens.bob, '/api/conversations', { method: 'POST', body: { to: pid.ana } })).body.id;
    const say = (who, text) => call(tokens[who], `/api/conversations/${chat}/messages`, { method: 'POST', body: { text } });
    const emailsBefore = (await mail(members.ana.email)).length;
    assert.equal((await say('bob', 'Fictional secret hello')).status, 201);
    assert.equal((await say('bob', 'Fictional second line')).status, 201);
    c = await centre('ana');
    assert.deepEqual(c.items[0], { ...c.items[0], kind: 'message', title: '2 new messages from Fictional Ntf bob', link: `/chats/${chat}`, count: 2, read: false }); pass();
    assert.ok(await waitFor(async () => (await mail(pushBox(anaDevice))).length === 2)); pass();
    await sleep(800);
    assert.equal((await mail(pushBox(anaDevice))).length, 2); pass(); // the second message didn't push again
    [push] = await mail(pushBox(anaDevice));
    assert.ok(push.text.includes('Body: You have a new message.') && !push.text.includes('secret')); pass();
    assert.equal((await mail(members.ana.email)).length, emailsBefore); pass(); // messages are never emailed
    assert.equal(await sql(`SELECT count(*) FROM notification_deliveries d JOIN notifications n ON n.id = d.notification_id WHERE n.account_id = '${ids.ana}' AND n.kind = 'Message' AND d.channel = 'email'`), '0'); pass();
    await call(tokens.ana, `/api/conversations/${chat}/messages`);
    c = await centre('ana');
    assert.equal(c.items.find(i => i.kind === 'message').read, true); pass();
    assert.equal(c.unread, 0); pass();

    // ---- Private previews off: the lock screen names the person and shows the start of the message.
    assert.equal((await settings('ana', { privatePreviews: false })).status, 200);
    assert.equal((await say('bob', 'Fictional full preview')).status, 201);
    assert.ok(await waitFor(async () => (await mail(pushBox(anaDevice))).length === 3)); pass();
    [push] = await mail(pushBox(anaDevice));
    assert.ok(push.text.includes('Title: Fictional Ntf bob') && push.text.includes('Body: Fictional full preview')); pass();
    r = await request('dee', 'ana', 'friends');
    assert.ok(await waitFor(async () => (await mail(pushBox(anaDevice))).length === 4)); pass();
    [push] = await mail(pushBox(anaDevice));
    assert.ok(push.text.includes('Body: Fictional Ntf dee sent you a Friends request.')); pass();
    [email] = await mail(members.ana.email);
    assert.ok(!email.text.includes('dee') && !email.text.includes('Friends')); pass(); // emails stay private whatever the preview setting
    const deeRequest = r.body.id;

    // ---- Muted modes, switched-off kinds and quiet hours stop pushes and emails, never the centre.
    await call(tokens.ana, `/api/conversations/${chat}/messages`);
    assert.equal((await settings('ana', { mutedModes: ['dating'] })).status, 200);
    assert.equal((await say('bob', 'Fictional muted')).status, 201); // Bob and Ana are connected in Dating only
    assert.equal((await centre('ana')).items.find(i => i.kind === 'message').read, false); pass();
    assert.deepEqual(await sql(`SELECT d.status FROM notification_deliveries d JOIN notifications n ON n.id = d.notification_id WHERE n.account_id = '${ids.ana}' AND n.kind = 'Message' ORDER BY d.id DESC LIMIT 1`), 'off'); pass();
    await sleep(500);
    assert.equal((await mail(pushBox(anaDevice))).length, 4); pass();
    // Explicit provider rejection can retry, but a newly muted mode must be rechecked at dispatch.
    const retryNotice = await sql(`SELECT id FROM notifications WHERE account_id='${ids.ana}' AND kind='Request' AND mode_id='dating' ORDER BY created_at LIMIT 1`);
    await sql(`UPDATE notification_deliveries SET status='retry', attempts=1, next_attempt_at=now()-interval '1 minute' WHERE notification_id='${retryNotice}' AND channel='push' AND device_id='${anaDevice}'`);
    await cli('notifications', 'sweep');
    assert.equal((await mail(pushBox(anaDevice))).length, 4); pass();
    assert.equal(await sql(`SELECT status FROM notification_deliveries WHERE notification_id='${retryNotice}' AND device_id='${anaDevice}'`), 'suppressed'); pass();
    r = await request('cat', 'ana', 'dating');
    assert.deepEqual(await deliveries('ana', 'Request'), ['email:off', 'push:off']); pass();

    assert.equal((await settings('ana', { requests: false })).status, 200);
    assert.equal((await call(tokens.ana, `/api/requests/${deeRequest}/decline`, { method: 'POST' })).status, 200);
    r = await request('dee', 'ana', 'dating');
    assert.deepEqual(await deliveries('ana', 'Request'), ['email:off', 'push:off']); pass();
    assert.ok((await centre('ana')).items.some(i => i.title === 'Fictional Ntf dee sent you a Dating request')); pass();
    assert.equal((await call(tokens.dee, `/api/requests/${r.body.id}/withdraw`, { method: 'POST' })).status, 200);
    assert.equal((await settings('ana', { quietFrom: hhmm(-60), quietUntil: hhmm(60), timeZone: 'Australia/Adelaide' })).body.quietFrom, hhmm(-60)); pass();
    r = await request('ana', 'dee', 'dating');
    assert.equal(r.status, 201);
    assert.equal((await call(tokens.dee, `/api/requests/${r.body.id}/accept`, { method: 'POST' })).status, 200);
    assert.deepEqual(await deliveries('ana', 'Connection'), ['email:quiet', 'push:quiet']); pass();
    assert.ok((await centre('ana')).items.some(i => i.title === 'Fictional Ntf dee accepted your Dating request')); pass();
    await settings('ana', {}); // back to the defaults

    // ---- Blocks: the blocked person's items leave the centre, whoever blocked whom, and they can't cause new ones.
    assert.ok((await centre('ana')).items.some(i => i.title.includes('Ntf cat'))); pass();
    assert.equal((await call(tokens.ana, '/api/blocks', { method: 'POST', body: { profileId: pid.cat } })).status < 300, true);
    assert.ok(!(await centre('ana')).items.some(i => i.title.includes('Ntf cat'))); pass();
    r = await request('eve', 'ana', 'friends');
    assert.equal(r.status, 201);
    assert.ok((await centre('ana')).items.some(i => i.title.includes('Ntf eve'))); pass();
    assert.equal((await call(tokens.eve, '/api/blocks', { method: 'POST', body: { profileId: pid.ana } })).status < 300, true);
    assert.ok(!(await centre('ana')).items.some(i => i.title.includes('Ntf eve'))); pass();
    await call(tokens.eve, `/api/blocks/${pid.ana}`, { method: 'DELETE' });

    // ---- Restrictions: a restricted person's items disappear; a restricted member sees only account notices.
    r = await request('res', 'ana', 'friends');
    assert.equal(r.status, 201);
    assert.ok((await centre('ana')).items.some(i => i.title.includes('Ntf res'))); pass();
    r = await call(root, `/api/admin/accounts/${ids.res}/sanctions`, { method: 'POST', body: { action: 'restrict', reason: 'Fictional restriction for the notifications suite.', days: 2 } });
    assert.ok(r.status < 300); pass();
    assert.ok(!(await centre('ana')).items.some(i => i.title.includes('Ntf res'))); pass();
    c = await centre('res');
    assert.deepEqual(c.items.map(i => [i.kind, i.title, i.link]), [['account', "There's a notice about your account", '/me/notices']]); pass();
    assert.deepEqual(code(await call(tokens.res, '/api/me/notification-settings')), [403, 'account-restricted']); pass();
    assert.deepEqual(code(await request('res', 'bob', 'friends')), [403, 'account-restricted']); pass();
    const notice = c.items[0].id;
    assert.equal((await call(tokens.res, `/api/me/notifications/${notice}/read`, { method: 'POST' })).status, 204); pass();
    assert.equal((await centre('res')).unread, 0); pass();

    // ---- Bots can't start anything; other members' and unknown ids read as not available.
    assert.deepEqual(code(await call(tokens.bot, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } })), [403, 'bot-cannot-initiate']); pass();
    assert.equal(await sql(`SELECT count(*) FROM notifications WHERE actor_account_id = '${ids.bot}'`), '0'); pass();
    assert.deepEqual(code(await call(tokens.ana, `/api/me/notifications/${notice}/read`, { method: 'POST' })), [404, 'not-available']); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/notifications/00000000-0000-0000-0000-000000000000/read', { method: 'POST' })), [404, 'not-available']); pass();
    assert.equal((await call(tokens.ana, '/api/me/notifications/not-a-guid/read', { method: 'POST' })).status, 404); pass();

    // ---- Travel reminders: once per trip, the day before, from the sweep; a cancelled trip's reminder goes.
    r = await call(tokens.ana, '/api/me/trips', { method: 'POST', body: { cityId: MELBOURNE, arrival: plus(1), departure: plus(4), visible: false, itineraryId: null } });
    assert.equal(r.status, 201, JSON.stringify(r.body));
    const trip = r.body.id;
    const far = (await call(tokens.ana, '/api/me/trips', { method: 'POST', body: { cityId: MELBOURNE, arrival: plus(9), departure: plus(10), visible: false, itineraryId: null } })).body.id;
    await cli('notifications', 'sweep');
    await cli('notifications', 'sweep');
    c = await centre('ana');
    assert.deepEqual(c.items.filter(i => i.kind === 'travel').map(i => [i.title, i.link]), [['Your trip to Melbourne starts tomorrow', '/travel']]); pass();
    assert.equal(await sql(`SELECT count(*) FROM notifications WHERE account_id = '${ids.ana}' AND kind = 'Travel'`), '1'); pass();
    assert.equal(await sql(`SELECT count(*) FROM notifications WHERE dedupe_key = 'trip-start:${far}'`), '0'); pass();
    assert.deepEqual(await deliveries('ana', 'Travel'), ['email:sent', 'push:simulated']); pass();
    await call(tokens.ana, `/api/me/trips/${trip}/cancel`, { method: 'POST' });
    assert.ok(!(await centre('ana')).items.some(i => i.kind === 'travel')); pass();
    await call(tokens.ana, `/api/me/trips/${far}/cancel`, { method: 'POST' });

    // ---- Read all; audit with ids only.
    assert.equal((await call(tokens.ana, '/api/me/notifications/read', { method: 'POST' })).status, 204);
    assert.equal((await centre('ana')).unread, 0); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action IN ('notifications.settings', 'device.registered', 'device.removed') AND (coalesce(detail, '') LIKE '%simulated%' OR coalesce(detail, '') LIKE '%:%')`), '0'); pass();

    // ---- Web at phone width: the centre, the settings form and the BFF allow-list.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', members.bob.email); await page.fill('#password', members.bob.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    assert.ok(await page.isVisible('[data-notifications-link]')); pass();
    await page.goto(web + '/me/notifications');
    await page.waitForSelector('[data-notifications="ready"]', { timeout: 20000 });
    assert.ok((await page.textContent('[data-notification-list]')).includes('Fictional Ntf ana accepted your Dating request')); pass();
    assert.ok((await page.textContent('[data-notification-privacy]')).includes('never show names')); pass();
    await page.$eval('[data-setting="email"]', el => el.click());
    await page.$eval('[data-mute="business"]', el => el.click());
    await page.fill('[data-setting="quiet-from"]', '22:00'); await page.fill('[data-setting="quiet-until"]', '07:00');
    await page.$eval('[data-save-settings]', el => el.click());
    await page.waitForSelector('[data-settings-status="saved"]');
    r = await call(tokens.bob, '/api/me/notification-settings');
    assert.deepEqual([r.body.email, r.body.mutedModes, r.body.quietFrom, r.body.quietUntil], [false, ['business'], '22:00', '07:00']); pass();
    if (output) await page.screenshot({ path: output + '/web-notifications.png', fullPage: true });
    const bff = async path => page.evaluate(async p => (await fetch('/bff/' + p, { headers: { 'X-Swoovr-Bff': '1' } })).status, path);
    assert.equal(await bff('api/me/notifications'), 200); pass();
    assert.equal(await bff('api/me/devices'), 404); pass(); // devices are the app's, not the web's
    assert.equal(await page.evaluate(async () => (await fetch('/bff/api/me/notifications')).status), 400); pass(); // no BFF header
    await context.close();

    console.log(`PASS: notifications (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
