// Issue 448 (owner decision 27 November 2026, D457): optional profile details through the real API. The catalogue lists no
// sensitive category; a detail is saved unpublished by default and shown only in the modes the member published it in, to
// viewers who can see that mode (Dating-only details never elsewhere); a block hides everything; removal, the export, the
// audit (ids only) and the profile snapshot for moderation. Owned stack; every person is fictional.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { api, web, createMember, memberToken, cli, call, sql, claims, matchReady } = require('../auth/local-tokens.cjs');
const audit = require('../accessibility/audit.cjs');
const output = process.env.SWOOVR_SCREENSHOTS;
const ADELAIDE = 2078025;

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const code = r => [r.status, r.body?.code];
  try {
    const names = ['ana', 'ben', 'cat'];
    const members = Object.fromEntries(await Promise.all(names.map(async n => [n, await createMember('pdet-' + n)])));
    await cli('beta', 'add', ...Object.values(members).map(m => m.email));
    const tk = {}, pid = {}, id = {};
    for (const n of names) {
      tk[n] = await memberToken(browser, members[n]);
      const saved = await call(tk[n], '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Pdet ' + n, homeCityId: ADELAIDE } });
      assert.equal(saved.status, 200, n); pid[n] = saved.body.publicId;
      id[n] = await sql(`SELECT id FROM accounts WHERE subject = '${claims(tk[n]).sub}'`);
      for (const mode of ['dating', 'friends']) await call(tk[n], `/api/me/modes/${mode}`, { method: 'PUT', body: { enabled: true, visible: true } });
      await matchReady(tk[n]);
    }

    // The catalogue: no sensitive category, Dating-only details say so.
    const catalogue = (await call(tk.ana, '/api/profile-attributes')).body;
    const ids = catalogue.details.map(d => d.id);
    for (const hidden of ['religion', 'politics', 'ethnicity', 'weight', 'romantic-orientation', 'accessibility', 'nationality', 'causes', 'spirituality'])
      assert.ok(!ids.includes(hidden), 'not offered: ' + hidden);
    pass();
    assert.deepEqual(catalogue.details.find(d => d.id === 'relationship-status').modes, ['dating']); pass();
    assert.ok(catalogue.details.find(d => d.id === 'interests').options.some(o => o.id === 'hiking')); pass();

    const put = (who, detail, value, modes) => call(tk[who], `/api/me/profile-attributes/${detail}`, { method: 'PUT', body: { value, modes } });
    // Refused: sensitive, unknown, a bad value, a mode it can't appear in.
    assert.deepEqual(code(await put('ana', 'religion', 'x', ['dating'])), [404, 'unknown-profile-detail']); pass();
    assert.deepEqual(code(await put('ana', 'nope', 'x', [])), [404, 'unknown-profile-detail']); pass();
    assert.deepEqual(code(await put('ana', 'height', 400, [])), [400, 'invalid-profile-detail']); pass();
    assert.deepEqual(code(await put('ana', 'relationship-status', 'single', ['friends'])), [400, 'invalid-profile-detail']); pass();

    // Saved: unpublished, Dating only, both modes.
    let r = await put('ana', 'smoking', 'never', []);
    assert.equal(r.status, 200); pass();
    assert.equal((await put('ana', 'relationship-status', 'single', ['dating'])).status, 200);
    assert.equal((await put('ana', 'interests', ['travel', 'hiking'], ['dating', 'friends'])).status, 200);
    assert.equal((await put('ana', 'height', 172, ['friends'])).status, 200);
    const mine = (await call(tk.ana, '/api/me/profile-attributes')).body;
    assert.deepEqual(mine.map(v => [v.id, JSON.stringify(v.value), v.modes.join(',')]).sort(),
      [['height', '172', 'friends'], ['interests', '["hiking","travel"]', 'dating,friends'], ['relationship-status', '"single"', 'dating'], ['smoking', '"never"', '']]); pass();
    assert.equal(await sql(`SELECT count(*) FROM profile_attribute_values WHERE account_id = '${id.ben}'`), '0', 'only mine'); pass();

    // Ben sees Ana in Dating and Friends; Cat is connected nowhere but shares the market.
    const ask = await call(tk.ben, '/api/requests', { method: 'POST', body: { to: pid.ana, mode: 'friends' } });
    assert.equal((await call(tk.ana, `/api/requests/${ask.body.id}/accept`, { method: 'POST' })).status, 200);
    const view = async (who, mode) => (await call(tk[who], `/api/profiles/${pid.ana}?mode=${mode}`)).body;
    const detailsIn = (profile, mode) => profile.modes.find(m => m.id === mode)?.details ?? [];
    const friends = await view('ben', 'friends');
    assert.deepEqual(detailsIn(friends, 'friends').map(d => [d.id, d.values.join('|')]), [['height', '172 cm (5′8″)'], ['interests', 'Hiking|Travel']], 'Friends: only what is published there'); pass();
    const dating = await view('ben', 'dating');
    assert.deepEqual(detailsIn(dating, 'dating').map(d => d.id), ['relationship-status', 'interests'], 'Dating: its own'); pass();
    const raw = JSON.stringify([friends, dating]);
    assert.ok(!raw.includes('Never') && !raw.includes('smoking'), 'an unpublished detail never reaches another member'); pass();
    assert.ok(!raw.includes('"hiking"') && !raw.includes('"single"'), 'labels only, no option ids'); pass();
    // A block hides the whole profile, details included.
    assert.equal((await call(tk.cat, '/api/blocks', { method: 'POST', body: { profileId: pid.ana } })).status, 204);
    assert.equal((await call(tk.cat, `/api/profiles/${pid.ana}?mode=dating`)).status, 404); pass();

    // Publishing changes take effect at once; removing deletes it.
    assert.equal((await put('ana', 'height', 172, [])).status, 200);
    assert.ok(!detailsIn(await view('ben', 'friends'), 'friends').some(d => d.id === 'height'), 'unpublished again'); pass();
    assert.equal((await call(tk.ana, '/api/me/profile-attributes/height', { method: 'DELETE' })).status, 200);
    assert.equal(await sql(`SELECT count(*) FROM profile_attribute_values WHERE account_id = '${id.ana}' AND attribute_id = 'height'`), '0'); pass();

    // The audit has ids only; the moderation snapshot has what was shown; the export has everything saved.
    assert.ok(Number(await sql(`SELECT count(*) FROM audit_entries WHERE actor_account_id = '${id.ana}' AND action LIKE 'profile.detail.%'`)) >= 5); pass();
    assert.equal(await sql(`SELECT count(*) FROM audit_entries WHERE action LIKE 'profile.detail.%' AND (detail ILIKE '%single%' OR detail ILIKE '%never%' OR detail ILIKE '%hiking%')`), '0'); pass();
    const snapshot = await sql(`SELECT content_json FROM profile_versions WHERE account_id = '${id.ana}' AND mode_id = 'dating' ORDER BY captured_at DESC LIMIT 1`);
    assert.ok(snapshot.includes('relationship-status') && !snapshot.includes('smoking'), 'snapshot: published details only'); pass();
    const ticket = await call(tk.ana, '/api/me/export', { method: 'POST' });
    const exported = await (await fetch(api + '/' + ticket.body.path, { headers: { Authorization: 'Bearer ' + tk.ana } })).json();
    assert.deepEqual(exported.profileDetails.map(d => [d.detail, d.shownIn.join(',')]), [['interests', 'dating,friends'], ['relationship-status', 'dating'], ['smoking', '']], 'export'); pass();

    // Web (slice 2): the editor on /me/profile, then what Ben sees.
    const signIn = async (who, viewport) => {
      const context = await browser.newContext({ viewport });
      const page = await context.newPage();
      await page.goto(web + '/account/sign-in?intro=done'); await page.fill('#username', members[who].email); await page.fill('#password', members[who].password);
      await Promise.all([page.waitForURL(u => !u.href.includes('/realms/')), page.click('#kc-login')]);
      return { context, page };
    };
    let { context, page } = await signIn('ana', { width: 1280, height: 900 });
    await page.goto(web + '/me/profile');
    await page.waitForSelector('[data-profile-details=ready]', { timeout: 20000 });
    const categories = await page.locator('[data-detail-category]').evaluateAll(es => es.map(e => e.dataset.detailCategory));
    assert.ok(categories.includes('lifestyle') && !categories.includes('beliefs') && !categories.includes('accessibility'), 'no sensitive category offered: ' + categories); pass();
    assert.equal(await page.textContent('[data-detail-category=lifestyle] [data-detail-count]'), '1 of 7 answered', 'the owner sees their own progress'); pass();
    await page.click('[data-detail-toggle=lifestyle]');
    await page.selectOption('[data-detail-select=alcohol]', 'socially');
    await page.waitForSelector('[data-detail="alcohol"][data-detail-saved=yes] [data-detail-private=alcohol]');
    assert.equal(await sql(`SELECT value::text || '|' || array_to_string(modes, ',') FROM profile_attribute_values WHERE account_id = '${id.ana}' AND attribute_id = 'alcohol'`), '"socially"|', 'saved, shown to nobody'); pass();
    await page.check('[data-detail-mode="alcohol:dating"]');
    await page.waitForSelector('[data-detail="alcohol"]:not(:has([data-detail-private]))');
    assert.equal(await sql(`SELECT array_to_string(modes, ',') FROM profile_attribute_values WHERE account_id = '${id.ana}' AND attribute_id = 'alcohol'`), 'dating'); pass();
    await page.click('[data-detail-toggle=appearance]');
    await page.fill('[data-detail-number=height]', '183'); await page.press('[data-detail-number=height]', 'Tab');
    await page.waitForSelector('[data-detail-imperial=height]');
    assert.equal((await page.textContent('[data-detail-imperial=height]')).trim(), 'About 6′0″'); pass();
    await page.click('[data-detail-all=height]');
    await page.waitForFunction(() => document.querySelector('[data-detail-mode="height:friends"]')?.checked && document.querySelector('[data-detail-mode="height:dating"]')?.checked);
    pass();
    assert.deepEqual(await page.evaluate(audit), [], 'page accessibility'); pass();
    if (output) await page.screenshot({ path: output + '/profile-details-editor.png', fullPage: true });
    await context.close();
    ({ context, page } = await signIn('ben', { width: 390, height: 844 }));
    await page.goto(`${web}/people/${pid.ana}?mode=dating`);
    await page.waitForSelector('[data-person-more]', { timeout: 20000 });
    const shown = await page.locator('[data-person-detail]').evaluateAll(es => es.map(e => [e.dataset.personDetail, e.querySelector('dd').textContent]));
    assert.deepEqual(shown, [['height', '183 cm (6′0″)'], ['alcohol', 'Socially'], ['relationship-status', 'Single'], ['interests', 'Hiking, Travel']], 'published details, grouped, once each: ' + JSON.stringify(shown)); pass();
    assert.ok(!(await page.textContent('[data-person-more]')).includes('Never'), 'unpublished smoking never shown'); pass();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'no sideways scroll at 390 px'); pass();
    if (output) await page.screenshot({ path: output + '/profile-details-public.png', fullPage: true });
    await context.close();

    console.log(`PASS: profile-details (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
