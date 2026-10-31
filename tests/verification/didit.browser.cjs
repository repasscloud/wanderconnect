// Local acceptance for slice P (D126–D130, D147, D152): ID verification through Didit's hosted flow in test mode, the
// webhook on the API, the return re-read, admin outcomes, the age-assurance gate and the founder allocation job.
// Real Didit test-mode session creation happens at most twice a run (members "ana" and "web"); no verification is
// completed at Didit (that needs a person with an ID). Every other state change arrives as a webhook this test signs
// with the destination secret from .local/docker.env, exactly as Didit signs them; forged, stale, replayed,
// mis-addressed and oversized deliveries are refused. Needs the local stack, the enrolled fictional root admin, hosts
// built from this checkout with the Didit settings (see docs/delivery/LOCAL-DEVELOPMENT.md) and the GeoNames import.
// Every member is a fictional adult created for this run.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, sleep, createMember, memberToken, rootAdminToken, cli, sql, call, claims, localEnv, membersRealm, matchReady } = require('../auth/local-tokens.cjs');

const ADELAIDE = 2078025;
// Python's json.dumps(sort_keys=True, separators=(",", ":"), ensure_ascii=False) for the integer/string/null/object
// payloads below, which is what Didit's X-Signature-V2 covers.
const canonical = v => Array.isArray(v) ? `[${v.map(canonical).join(',')}]`
  : v && typeof v === 'object' ? `{${Object.keys(v).sort().map(k => JSON.stringify(k) + ':' + canonical(v[k])).join(',')}}` : JSON.stringify(v);

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  const secret = await localEnv('SWOOVR_DIDIT_WEBHOOK_SECRET');
  const workflow = await localEnv('SWOOVR_DIDIT_WORKFLOW_ID');
  assert.ok(secret && workflow, 'Didit settings missing from .local/docker.env');
  // The founder job (an operator command) issues tokens and mails them to the local mail sink.
  Object.assign(process.env, { Founders__Secrets__1: await localEnv('SWOOVR_FOUNDER_SECRET_V1'), Email__Smtp__Host: '127.0.0.1', Email__Smtp__Port: '1025' });
  const hook = async (payload, { sign = 'v2', headers = {}, raw } = {}) => {
    const body = raw ?? JSON.stringify(payload);
    const key = Buffer.from(secret, 'utf8');
    const signature = sign === 'v2' ? { 'X-Signature-V2': crypto.createHmac('sha256', key).update(canonical(payload)).digest('hex') }
      : sign === 'raw' ? { 'X-Signature': crypto.createHmac('sha256', key).update(body).digest('hex') }
      : sign === 'forged' ? { 'X-Signature-V2': crypto.createHmac('sha256', 'not-the-secret').update(canonical(payload)).digest('hex') } : {};
    const response = await fetch(api + '/api/webhooks/didit', { method: 'POST', body,
      headers: { 'Content-Type': 'application/json', 'X-Timestamp': String(Math.floor(Date.now() / 1000)), ...signature, ...headers } });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  };
  const event = (session, status, vendor, extra = {}) => ({ event_id: crypto.randomUUID(), session_id: session, status, webhook_type: 'status.updated',
    created_at: Math.floor(Date.now() / 1000), timestamp: Math.floor(Date.now() / 1000), workflow_id: workflow, vendor_data: vendor, ...extra });
  const approved = (age, dateOfBirth) => ({ decision: { id_verifications: [{ node_id: 'feature_ocr', status: 'Approved', age, document_type: 'Passport', ...(dateOfBirth ? { date_of_birth: dateOfBirth } : {}) }] } });
  try {
    const root = await rootAdminToken(browser);
    const names = ['ana', 'bel', 'cat', 'dee', 'eve', 'nev', 'web', 'bot'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('verify-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tokens = {}, ids = {}, pid = {};
    for (const n of names) {
      tokens[n] = await memberToken(browser, members[n]);
      const saved = await call(tokens[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional ' + n[0].toUpperCase() + n.slice(1), homeCityId: ADELAIDE } });
      assert.equal(saved.status, 200, n);
      await matchReady(tokens[n]);
      await call(tokens[n], '/api/me/modes/dating', { method: 'PUT', body: { enabled: true, visible: true } });
      pid[n] = saved.body.publicId;
      ids[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tokens[n]).sub}'`);
    }
    const code = r => [r.status, r.body?.code];
    const view = n => call(tokens[n], '/api/me/verification');
    const start = (n, client = 'web') => call(tokens[n], '/api/me/verification', { method: 'POST', body: { client } });
    const fakeAttempt = async (n, state = 'Created') => { // a session row as if Didit had created it (no provider call)
      const session = crypto.randomUUID();
      await sql(`INSERT INTO id_verification_attempts (id, account_id, session_id, workflow_id, state, provider_status, started, self_service, created_at, updated_at)
        VALUES ('${crypto.randomUUID()}', '${ids[n]}', '${session}', '${workflow}', '${state}', 'Not Started', false, true, now(), now())`);
      await sql(`UPDATE member_profiles SET can_request_id_verification = false WHERE account_id = '${ids[n]}'`);
      return session;
    };

    // Before any check: optional, free, labelled test mode (D147), nothing shown publicly.
    let r = await view('ana');
    assert.equal(r.status, 200); pass();
    assert.deepEqual([r.body.idStatus, r.body.attemptState, r.body.canStart, r.body.available, r.body.testMode, r.body.url], ['notStarted', null, true, true, true, null]); pass();
    assert.deepEqual(code(await start('ana', 'evil')), [400, 'invalid-request']); pass(); // the return address is never the client's
    assert.deepEqual(code(await call(tokens.ana, '/api/me/verification', { method: 'POST', body: { client: 'web', callback: 'https://evil.example/' } })).slice(0, 1), [200]);
    pass(); // an injected callback is ignored (the configured web return address is used)

    // Real Didit test-mode session for ana (created by the call above): the hosted link, the session stored against the account.
    r = await view('ana');
    assert.equal(r.body.attemptState, 'created'); assert.match(r.body.url, /^https:\/\/[a-z.]*didit\.me\//); pass();
    const anaSession = await sql(`SELECT session_id FROM id_verification_attempts WHERE account_id = '${ids.ana}'`);
    assert.match(anaSession, /^[0-9a-f-]{36}$/); pass();
    assert.equal(await sql(`SELECT can_request_id_verification FROM member_profiles WHERE account_id = '${ids.ana}'`), 'f'); pass();
    assert.equal((await start('ana')).body.url, r.body.url); pass(); // starting again shows the open session, no second one
    assert.equal(await sql(`SELECT count(*) FROM id_verification_attempts WHERE account_id = '${ids.ana}'`), '1'); pass();
    // The return re-read asks Didit: the session hasn't been started, so nothing changes.
    assert.equal((await view('ana')).body.attemptState, 'created'); pass();
    // Did Didit's own webhook reach this API through the quick tunnel? Recorded, never asserted (depends on the tunnel).
    await sleep(8000);
    const liveEvents = await sql(`SELECT count(*) FROM didit_webhook_events WHERE session_id = '${anaSession}'`);
    console.log(`INFO: live Didit webhook deliveries for the new session so far: ${liveEvents}`);

    // Webhook authentication (D152): signature first, then the signed timestamp, then the event id.
    const stateOf = n => sql(`SELECT state FROM id_verification_attempts WHERE account_id = '${ids[n]}' ORDER BY created_at DESC LIMIT 1`);
    r = await hook(event(anaSession, 'Approved', ids.ana, approved(30)), { sign: 'forged' });
    assert.equal(r.status, 401); assert.equal(await stateOf('ana'), 'Created'); pass();
    assert.equal((await hook(event(anaSession, 'Approved', ids.ana, approved(30)), { sign: 'none' })).status, 401); pass();
    const simple = event(anaSession, 'Approved', ids.ana);
    const simpleSig = crypto.createHmac('sha256', secret).update(`${simple.timestamp}:${simple.session_id}:${simple.status}:${simple.webhook_type}`).digest('hex');
    assert.equal((await hook(simple, { sign: 'none', headers: { 'X-Signature-Simple': simpleSig } })).status, 401); pass(); // the simple signature doesn't cover vendor_data
    const stale = event(anaSession, 'Approved', ids.ana, { timestamp: Math.floor(Date.now() / 1000) - 600, ...approved(30) });
    assert.equal((await hook(stale)).status, 400); assert.equal(await stateOf('ana'), 'Created'); pass(); // a fresh X-Timestamp header doesn't help
    const signedThenTampered = event(anaSession, 'Approved', ids.ana, approved(30));
    const goodSig = crypto.createHmac('sha256', secret).update(canonical(signedThenTampered)).digest('hex');
    assert.equal((await hook(null, { sign: 'none', raw: JSON.stringify({ ...signedThenTampered, vendor_data: ids.bel }), headers: { 'X-Signature-V2': goodSig } })).status, 401); pass();
    assert.equal((await hook(null, { sign: 'none', raw: '{"a":"' + 'x'.repeat(1024 * 1024) + '"}', headers: { 'X-Signature-V2': 'ab' } })).status, 413); pass();
    r = await hook(event(anaSession, 'Approved', ids.bel, approved(30))); // signed, but for another member's account
    assert.deepEqual([r.status, r.body.outcome], [200, 'vendor-mismatch']); assert.equal(await stateOf('ana'), 'Created'); pass();
    r = await hook(event(anaSession, 'Approved', ids.ana, { ...approved(30), workflow_id: crypto.randomUUID() }));
    assert.deepEqual([r.status, r.body.outcome], [200, 'workflow-mismatch']); pass();
    r = await hook(event(crypto.randomUUID(), 'Approved', ids.ana, approved(30)));
    assert.deepEqual([r.status, r.body.outcome], [200, 'unknown-session']); pass();
    r = await hook({ ...event(anaSession, 'ACTIVE', ids.ana), webhook_type: 'user.status.updated' });
    assert.deepEqual([r.status, r.body.outcome], [200, 'ignored']); pass();
    assert.equal((await hook({ ...event(anaSession, 'Approved', ids.ana), event_id: 'not-a-uuid' })).status, 400); pass();

    // Accepted deliveries: In Progress, then Approved (adult) by raw-body signature; ID Verified shows; replays change nothing.
    r = await hook(event(anaSession, 'In Progress', ids.ana));
    assert.deepEqual([r.status, r.body.outcome], [200, 'applied']); assert.equal(await stateOf('ana'), 'InProgress'); pass();
    assert.deepEqual([(await view('ana')).body.idStatus, (await view('ana')).body.attemptState], ['pending', 'inProgress']); pass();
    const approval = event(anaSession, 'Approved', ids.ana, approved(30, '1985-05-05')); // a different date from ana's profile (1990-01-01)
    assert.equal((await hook(approval, { sign: 'raw' })).body.outcome, 'applied'); pass();
    r = await view('ana');
    assert.deepEqual([r.body.idStatus, r.body.attemptState, r.body.url, r.body.canStart, r.body.refusal], ['verified', 'approved', null, false, 'alreadyVerified']); pass();
    assert.equal((await call(tokens.ana, '/api/me')).body.ageStatus, 'verified'); pass();
    // Q53 (D158) and Q58 (D166): the document's date differs, so the date of birth isn't verified and the member gets one
    // correction; ID Verified stays.
    let anaMe = (await call(tokens.ana, '/api/me')).body;
    assert.deepEqual([anaMe.dateOfBirthVerified, anaMe.dobCheck], [false, 'correctionNeeded']); pass();
    assert.equal(await sql(`SELECT date_of_birth_verified::text FROM member_profiles WHERE account_id = '${ids.ana}'`), 'false'); pass();
    assert.deepEqual(code(await call(tokens.ana, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1985-05-05' } })), [409, 'dob-correction-required']); pass();
    assert.deepEqual(code(await call(tokens.bel, '/api/me/verification/date-of-birth', { method: 'POST', body: { dateOfBirth: '1985-05-05' } })), [409, 'dob-correction-not-open']); pass(); // nothing open for bel
    for (const bad of [{}, { dateOfBirth: '2999-01-01' }, { dateOfBirth: 'nope' }])
    { assert.equal((await call(tokens.ana, '/api/me/verification/date-of-birth', { method: 'POST', body: bad })).status, 400, JSON.stringify(bad)); pass(); }
    assert.equal(await sql(`SELECT dob_check FROM member_profiles WHERE account_id = '${ids.ana}'`), 'CorrectionNeeded'); pass(); // refused tries don't use it
    // The correction: the API reads Didit's decision again. This real test-mode session was never completed, so Didit has no
    // date to compare and the date goes to an admin's review (a matching date is verified; see the checks and the owner's run).
    r = await call(tokens.ana, '/api/me/verification/date-of-birth', { method: 'POST', body: { dateOfBirth: '1985-05-05', dateOfBirthVerified: true, dobCheck: 'none' } });
    assert.deepEqual([r.status, r.body.dateOfBirth, r.body.dateOfBirthVerified, r.body.dobCheck], [200, '1985-05-05', false, 'manualReview']); pass();
    assert.ok((await call(tokens.ana, '/api/me')).body.badges.some(b => b.badge === 'idVerified')); pass(); // ID Verified kept
    assert.deepEqual(code(await call(tokens.ana, '/api/me/verification/date-of-birth', { method: 'POST', body: { dateOfBirth: '1985-05-05' } })), [409, 'dob-correction-not-open']); pass(); // once
    assert.deepEqual(code(await call(tokens.ana, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1990-01-01' } })), [409, 'dob-under-review']); pass();
    // The admin queue: admins only, ids and reasons, never a date.
    let reviews = await call(root, '/api/admin/dob-reviews');
    const anaReview = reviews.body.find(x => x.accountId === ids.ana);
    assert.deepEqual([reviews.status, anaReview?.reason, anaReview?.resolvedAt], [200, 'still-different', null]); pass();
    assert.ok(!/\d{4}-\d{2}-\d{2}"(?!T)/.test(JSON.stringify(anaReview)) && !JSON.stringify(anaReview).includes('1985')); pass();
    assert.equal((await call(tokens.ana, '/api/admin/dob-reviews')).status, 403); pass();
    assert.equal((await call(root, `/api/admin/accounts/${ids.ana}/summary`)).body.dobCheck, 'manualReview'); pass();
    for (const body of [{ outcome: 'verified' }, { outcome: 'maybe', reason: 'x' }, { outcome: '0', reason: 'x' }])
    { assert.equal((await call(root, `/api/admin/dob-reviews/${anaReview.id}/resolve`, { method: 'POST', body })).status, 400, JSON.stringify(body)); pass(); }
    assert.equal((await call(root, `/api/admin/dob-reviews/${crypto.randomUUID()}/resolve`, { method: 'POST', body: { outcome: 'verified', reason: 'x' } })).status, 404); pass();
    r = await call(root, `/api/admin/dob-reviews/${anaReview.id}/resolve`, { method: 'POST', body: { outcome: 'verified', reason: 'Fictional: checked against the document in Didit.' } });
    assert.deepEqual([r.status, r.body.outcome], [200, 'verified']); pass();
    anaMe = (await call(tokens.ana, '/api/me')).body;
    assert.deepEqual([anaMe.dateOfBirthVerified, anaMe.dobCheck], [true, 'none']); pass();
    assert.equal((await call(root, `/api/admin/dob-reviews/${anaReview.id}/resolve`, { method: 'POST', body: { outcome: 'notVerified', reason: 'x' } })).status, 409); pass();
    assert.ok(!(await call(root, '/api/admin/dob-reviews')).body.some(x => x.id === anaReview.id)); pass(); // the open queue
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'dob.%' AND detail ~ '[0-9]{4}-[0-9]{2}'`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM information_schema.columns WHERE table_name = 'dob_reviews' AND data_type = 'date'`), '0'); pass();
    assert.ok((await call(tokens.ana, '/api/me')).body.badges.some(b => b.badge === 'idVerified')); pass();
    assert.ok((await call(tokens.bel, `/api/profiles/${pid.ana}`)).body.badges.some(b => b.badge === 'idVerified')); pass(); // others see the badge
    assert.deepEqual((await hook(approval)).body, { duplicate: true }); pass(); // the same event id again
    r = await hook(event(anaSession, 'Declined', ids.ana)); // a later, contradictory status for a settled attempt
    assert.equal(r.status, 200); assert.equal((await view('ana')).body.idStatus, 'verified'); pass();
    assert.equal((await view('ana')).body.attemptState, 'approved'); pass(); // and the return re-read (Didit says Not Started) can't move it back
    assert.deepEqual(code(await start('ana')), [409, 'already-verified']); pass();
    // Only the outcome is stored: no document type, age or date of birth anywhere in the attempt or the audit.
    assert.equal(await sql(`SELECT adult::text || '/' || provider_status FROM id_verification_attempts WHERE account_id = '${ids.ana}'`), 'true/Approved'); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE (action LIKE 'verification.%' OR action LIKE 'age.%') AND (detail LIKE '%Passport%' OR detail LIKE '%30%' OR detail LIKE '%${anaSession}%' OR target_id = '${anaSession}')`), '0'); pass();
    assert.equal(await sql(`SELECT count(*) FROM information_schema.columns WHERE table_name IN ('id_verification_attempts', 'didit_webhook_events') AND column_name ~ '(document|birth|dob|selfie|image|face|name|body)'`), '0'); pass();
    assert.notEqual(await sql(`SELECT founder_eligible_at FROM member_profiles WHERE account_id = '${ids.ana}'`), ''); pass(); // D130 eligibility time recorded

    // One self-service attempt (D129): a declined check uses it; an admin may allow another.
    const belSession = await fakeAttempt('bel');
    await hook(event(belSession, 'In Progress', ids.bel));
    assert.equal((await hook(event(belSession, 'Declined', ids.bel))).body.outcome, 'applied'); pass();
    r = await view('bel');
    assert.deepEqual([r.body.idStatus, r.body.attemptState, r.body.canStart, r.body.refusal], ['rejected', 'declined', false, 'attemptUsed']); pass();
    assert.deepEqual(code(await start('bel')), [409, 'verification-attempt-used']); pass();
    assert.ok(!(await call(tokens.ana, `/api/profiles/${pid.bel}`)).body.badges.some(b => b.badge === 'idVerified')); pass();
    // An expired session that was never started isn't an attempt: the member gets it back (D129, D155).
    const catSession = await fakeAttempt('cat');
    await hook(event(catSession, 'Expired', ids.cat));
    r = await view('cat');
    assert.deepEqual([r.body.attemptState, r.body.canStart], ['expired', true]); pass();
    // ...but an abandoned real attempt (started, then stopped) uses it.
    const deeSession = await fakeAttempt('dee');
    await hook(event(deeSession, 'In Progress', ids.dee)); await hook(event(deeSession, 'Abandoned', ids.dee));
    assert.deepEqual([(await view('dee')).body.attemptState, (await view('dee')).body.canStart], ['abandoned', false]); pass();

    // An approved document showing someone under 18 locks the account (D127).
    const eveSession = await fakeAttempt('eve');
    await hook(event(eveSession, 'Approved', ids.eve, approved(16)));
    assert.deepEqual(code(await call(tokens.eve, '/api/me')), [403, 'account-locked']); pass();
    assert.equal(await sql(`SELECT status || '/' || status_reason FROM accounts WHERE id = '${ids.eve}'`), 'Locked/underage'); pass();
    assert.equal(await sql(`SELECT id_status || '/' || age_status FROM member_profiles WHERE account_id = '${ids.eve}'`), 'Rejected/Underage'); pass();
    assert.ok(!(await call(tokens.ana, '/api/explore?mode=dating')).body.people.some(p => p.id === pid.eve)); pass();

    // Admin outcomes (D129): staff only, recent sign-in, audited; members and other members' ids get nothing.
    assert.equal((await call(tokens.ana, `/api/admin/accounts/${ids.bel}/verification`)).status, 403); pass();
    assert.equal((await call(tokens.ana, `/api/admin/accounts/${ids.bel}/verification/allow-again`, { method: 'POST' })).status, 403); pass();
    assert.equal((await call(tokens.ana, `/api/admin/accounts/${ids.nev}/age-assurance`, { method: 'PUT', body: { needsVerification: true } })).status, 403); pass();
    r = await call(root, `/api/admin/accounts/${ids.bel}/verification`);
    assert.equal(r.status, 200); assert.deepEqual(r.body.attempts.map(a => [a.state, a.started, a.selfService]), [['declined', true, true]]); pass();
    assert.ok(!/url|didit\.me|birth|document/i.test(JSON.stringify(r.body))); pass();
    assert.equal((await call(root, `/api/admin/accounts/${crypto.randomUUID()}/verification`)).status, 404); pass();
    assert.equal((await call(root, `/api/admin/accounts/${ids.bel}/verification/delete`, { method: 'POST' })).status, 404); pass();
    r = await call(root, `/api/admin/accounts/${ids.bel}/verification/allow-again`, { method: 'POST' });
    assert.equal(r.status, 200); assert.equal(r.body.canRequestIdVerification, true); pass();
    assert.deepEqual([(await view('bel')).body.canStart, (await view('bel')).body.idStatus], [true, 'rejected']); pass();
    assert.deepEqual(code(await call(root, `/api/admin/accounts/${ids.ana}/verification/allow-again`, { method: 'POST' })), [409, 'invalid-request']); pass(); // already verified
    r = await call(root, `/api/admin/accounts/${ids.ana}/verification/revoke`, { method: 'POST' });
    assert.equal(r.status, 200); assert.equal(r.body.idStatus, 'rejected'); pass();
    assert.ok(!(await call(tokens.bel, `/api/profiles/${pid.ana}`)).body.badges.some(b => b.badge === 'idVerified')); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action IN ('verification.allow-again', 'verification.revoke') AND target_id IN ('${ids.bel}', '${ids.ana}')`), '2'); pass();

    // Age assurance (D127): a flagged account can use only its account and ID verification.
    r = await call(root, `/api/admin/accounts/${ids.nev}/age-assurance`, { method: 'PUT', body: { needsVerification: true } });
    assert.deepEqual([r.status, r.body.ageStatus], [200, 'needsVerification']); pass();
    for (const path of ['/api/explore?mode=dating', '/api/me/profile', '/api/connections', '/api/me/trips', '/api/modes', `/api/profiles/${pid.ana}`]) {
      assert.deepEqual(code(await call(tokens.nev, path)), [403, 'age-verification-required'], path); pass();
    }
    assert.deepEqual(code(await call(tokens.nev, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'dating' } })), [403, 'age-verification-required']); pass();
    assert.equal((await call(tokens.nev, '/api/me')).body.ageStatus, 'needsVerification'); pass();
    r = await view('nev');
    assert.deepEqual([r.status, r.body.ageCheckRequired, r.body.canStart], [200, true, true]); pass();
    assert.ok(!(await call(tokens.ana, '/api/explore?mode=dating')).body.people.some(p => p.id === pid.nev)); pass();
    assert.equal((await call(tokens.ana, `/api/profiles/${pid.nev}`)).status, 404); pass();
    // The flag follows the verified email to a new account: delete the Keycloak user and register again with the same address.
    const realm = await membersRealm();
    const [kcUser] = await realm(`/users?email=${encodeURIComponent(members.nev.email)}&exact=true`);
    await realm(`/users/${kcUser.id}`, 'DELETE');
    await realm('/users', 'POST', { username: members.nev.email, email: members.nev.email, firstName: 'Fictional', enabled: true, emailVerified: true,
      credentials: [{ type: 'password', value: members.nev.password, temporary: false }] });
    await cli('beta', 'revoke', members.nev.email); await cli('beta', 'add', members.nev.email); // the old enrolment is bound to the old account
    const again = await memberToken(browser, members.nev);
    assert.notEqual(claims(again).sub, claims(tokens.nev).sub); pass();
    assert.equal((await call(again, '/api/me')).body.ageStatus, 'needsVerification'); pass();
    assert.deepEqual(code(await call(again, '/api/explore?mode=dating')), [403, 'age-verification-required']); pass();
    // An authorised resolution (adult override, support or admin) lifts it.
    const nevAgain = await sql(`SELECT id FROM accounts WHERE subject = '${claims(again).sub}'`);
    assert.equal((await call(root, `/api/admin/accounts/${nevAgain}/adult-override`, { method: 'POST' })).status, 204); pass();
    assert.equal((await call(again, '/api/me/profile')).status, 200); pass();

    // Bots never get an ID check (Q11 rule 54).
    await call(root, `/api/admin/accounts/${ids.bot}/bot`, { method: 'PUT', body: { bot: true } });
    assert.deepEqual(code(await start('bot')), [403, 'not-available']); pass();

    // Founders (D130): admin exceptions still need verification; the job allocates in eligibility order, never twice.
    assert.deepEqual(code(await call(root, `/api/admin/accounts/${ids.cat}/founder`, { method: 'POST' })), [409, 'founder-not-eligible']); pass();
    const restored = await fakeAttempt('dee'); // a second, admin-allowed attempt for dee ends approved
    await sql(`UPDATE id_verification_attempts SET self_service = false WHERE session_id = '${restored}'`);
    await sql(`UPDATE id_verification_attempts SET state = 'Abandoned' WHERE account_id = '${ids.dee}' AND session_id <> '${restored}'`);
    await hook(event(restored, 'Approved', ids.dee, approved(36, '1990-01-01'))); // the same date as dee's profile
    assert.equal((await view('dee')).body.idStatus, 'verified'); pass();
    let deeMe = (await call(tokens.dee, '/api/me')).body;
    assert.deepEqual([deeMe.dateOfBirthVerified, deeMe.dateOfBirth], [true, '1990-01-01']); pass(); // matched: marked verified
    assert.deepEqual(code(await call(tokens.dee, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1991-01-01' } })), [409, 'date-of-birth-locked']); pass();
    assert.equal((await call(tokens.dee, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1990-01-01' } })).status, 200); pass(); // the same date is fine
    assert.deepEqual(code(await call(tokens.ana, '/api/me/age', { method: 'PUT', body: { dateOfBirth: '1990-02-01' } })), [409, 'date-of-birth-locked']); pass(); // verified by review now
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'dob.%' AND (detail LIKE '%19%' OR detail ~ '[0-9]{4}-[0-9]{2}')`), '0'); pass(); // never a date
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action = 'dob.verified' AND target_id = '${ids.dee}'`), '1'); pass();
    assert.equal((await call(root, `/api/admin/accounts/${ids.dee}/summary`)).body.dateOfBirthVerified, true); pass();
    const placesBefore = Number(await sql(`SELECT count(*) FROM founder_places`));
    const run = await cli('founders', 'allocate');
    assert.match(run, /Founders: \d+ newly eligible, \d+ places allocated, \d+ skipped, \d+ left\./); pass();
    const placesAfter = Number(await sql(`SELECT count(*) FROM founder_places`));
    assert.ok(placesAfter <= 1000 && placesAfter >= placesBefore); pass();
    const deePlace = await sql(`SELECT founder_place FROM member_profiles WHERE account_id = '${ids.dee}'`);
    if (placesBefore < 1000) { assert.match(deePlace, /^\d+$/); pass(); }
    assert.equal(await sql(`SELECT (founder_eligible_at <= founding_since AND founder_entitlement_from IS NOT NULL)::text FROM member_profiles WHERE account_id = '${ids.dee}'`), 'true'); pass();
    assert.equal(await sql(`SELECT coalesce(founder_place::text, 'none') FROM member_profiles WHERE account_id = '${ids.ana}'`), 'none'); pass(); // revoked before the run
    await cli('founders', 'allocate');
    assert.equal(await sql(`SELECT founder_place FROM member_profiles WHERE account_id = '${ids.dee}'`), deePlace); pass(); // never a second place
    await assert.rejects(sql(`INSERT INTO founder_places (place, granted_at) VALUES (1001, now())`)); pass(); // the database refuses place 1,001
    await assert.rejects(sql(`UPDATE member_profiles SET founder_place = ${deePlace} WHERE account_id = '${ids.bel}'`)); pass(); // one account per place

    // The webhook isn't on the BFF; the member route is. Then the page in actual Edge.
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const signIn = async (p, member) => {
      await p.goto(web + '/account/sign-in?intro=done');
      await p.fill('#username', member.email); await p.fill('#password', member.password);
      await Promise.all([p.waitForURL(web + '/account'), p.click('#kc-login')]);
    };
    await signIn(page, members.web);
    const bff = (path, options = {}) => page.request.fetch(web + '/bff/' + path, { ...options, headers: { 'X-Swoovr-Bff': '1', ...(options.headers ?? {}) } });
    assert.equal((await bff('api/webhooks/didit', { method: 'POST', data: {} })).status(), 404); pass();
    assert.equal((await bff(`api/admin/accounts/${ids.web}/verification`)).status(), 404); pass();
    assert.equal((await bff('api/me/verification')).status(), 200); pass();
    await page.click('a[href="/me/verification"]');
    await page.waitForSelector('[data-verification=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-verification-mode=test]').count(), 1); pass(); // never presented as live
    assert.equal(await page.locator('[data-id-status]').getAttribute('data-id-status'), 'notStarted'); pass();
    if (output) await page.screenshot({ path: output + '/verification-start-390.png', fullPage: true });
    await page.click('[data-verification-start]'); // the second real Didit test-mode session of this run
    await page.waitForSelector('[data-verification-link]', { timeout: 20000 });
    const link = page.locator('[data-verification-link]');
    assert.match(await link.getAttribute('href'), /^https:\/\/[a-z.]*didit\.me\//); pass();
    assert.equal(await link.getAttribute('target'), '_blank'); pass(); // opens outside the app, in a new browser tab
    assert.equal(await page.locator('[data-attempt-state]').getAttribute('data-attempt-state'), 'created'); pass();
    if (output) await page.screenshot({ path: output + '/verification-open-390.png', fullPage: true });
    // Coming back: the return page re-reads through the API; a forged status in the address changes nothing.
    await page.goto(web + `/me/verification/return?verificationSessionId=${crypto.randomUUID()}&status=Approved`);
    await page.waitForSelector('[data-verification=ready]', { timeout: 20000 });
    assert.equal(await page.locator('[data-id-status]').getAttribute('data-id-status'), 'pending'); pass();
    const webSession = await sql(`SELECT session_id FROM id_verification_attempts WHERE account_id = '${ids.web}'`);
    await hook(event(webSession, 'In Review', ids.web));
    await page.click('[data-verification-refresh]');
    await page.waitForSelector('[data-attempt-state=inReview]'); pass();
    // Q58 (D166): approved with a different date of birth, the one-time prompt pops up; the correction goes to review
    // here because this real test-mode session holds no date to compare.
    await hook(event(webSession, 'Approved', ids.web, approved(30, '1970-03-03')));
    await page.goto(web + '/me/verification');
    await page.waitForSelector('[data-dob-correction=open]', { timeout: 20000 }); pass();
    assert.equal(await page.locator('[role=dialog][aria-modal=true]').count(), 1); pass();
    assert.ok(!(await page.locator('[data-dob-correction=open]').innerText()).includes('1970')); pass(); // the document's date is never shown
    if (output) await page.screenshot({ path: output + '/dob-correction-390.png', fullPage: true });
    await page.fill('#dob-correction', '1970-03-03');
    await page.click('[data-dob-correction-form] button[type=submit]');
    await page.waitForFunction(() => document.querySelector('[data-dob-review=open]') || document.querySelector('[data-dob-correction-status]:not([data-dob-correction-status=""])'), null, { timeout: 20000 });
    assert.equal(await page.locator('[data-dob-review=open]').count(), 1, 'correction status ' + await page.locator('[data-dob-correction-status]').getAttribute('data-dob-correction-status').catch(() => 'none')); pass();
    assert.equal(await page.locator('[data-dob-correction=open]').count(), 0); pass();
    assert.equal((await call(root, '/api/admin/dob-reviews')).body.filter(x => x.accountId === ids.web).length, 1); pass();
    await page.goto(web + '/account');
    await page.waitForSelector('[data-dob-locked=manualReview]', { timeout: 20000 }); pass();
    // A flagged member sees why on the account page.
    const nevContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const nevPage = await nevContext.newPage();
    await call(root, `/api/admin/accounts/${ids.cat}/age-assurance`, { method: 'PUT', body: { needsVerification: true } });
    await signIn(nevPage, members.cat);
    await nevPage.waitForSelector('[data-age-check-required=true]', { timeout: 20000 }); pass();
    if (output) await nevPage.screenshot({ path: output + '/age-check-390.png', fullPage: true });
    await nevContext.close(); await context.close();
    console.log(`PASS: Didit ID verification acceptance, ${passed} checks (test-mode label, real Didit session creation bound to the account, one open session, configured return address only, webhook signature before timestamp before dedupe, forged/unsigned/simple-only/tampered/stale/oversized/replayed deliveries, wrong vendor_data, workflow and unknown sessions, forward-only states, re-read on return, outcome-only storage, one self-service attempt, unused sessions returned, under-18 lock, admin allow-again/revoke audited, age-assurance gate everywhere and across a re-registration, adult override, bots refused, founder job order/limit/no second place, BFF allow-list, web start/return/refresh in Edge).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
