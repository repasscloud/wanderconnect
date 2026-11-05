// Member web layout acceptance on a throwaway owned database, real BFF + Keycloak sessions.
// Fictional adults only; baseline mode captures geometry before layout assertions are enabled.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { managedEdge } = require('../auth/managed-edge.cjs');
const { web, createMember, memberToken, cli, call, matchReady } = require('../auth/local-tokens.cjs');
if (!process.env.SWOOVR_TEST_APP_DATABASE) throw Error('Use the owned meeting runner.');
const baseline = process.env.SWOOVR_RESPONSIVE_BASELINE === '1';
const output = process.env.SWOOVR_RESPONSIVE_OUTPUT;
const widths = [375, 768, 1024, 1440];
(async () => {
  const edge = await managedEdge(chromium);
  let checks = 0;
  const measurements = [];
  const reflowProblems = [];
  try {
    if (output) await fs.mkdir(output, { recursive: true });
    const browser = edge.browser;
    const members = [];
    for (const name of ['Alex', 'Rowan', 'Morgan', 'Jamie', 'Taylor', 'Casey']) {
      const member = await createMember('responsive-' + name.toLowerCase());
      await cli('beta', 'add', member.email);
      const token = await memberToken(browser, member);
      assert.equal((await call(token, '/api/me/profile', { method: 'PUT', body: {
        displayName: 'Fictional ' + name, homeCityId: 2078025,
        about: 'A fictional adult who enjoys coastal walks, conversation and finding new places for coffee.'
      } })).status, 200);
      await matchReady(token);
      assert.equal((await call(token, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true, shareCoreAbout: true } })).status, 200);
      members.push({ ...member, token, id: (await call(token, '/api/me/profile')).body.publicId });
    }
    const [viewer, peer] = members;
    // Real connections and chat data; existing domain/API authorization stays in force.
    const request = await call(viewer.token, '/api/requests', { method: 'POST', body: { to: peer.id, mode: 'friends' } });
    assert.equal(request.status, 201);
    const accepted = await call(peer.token, '/api/requests/' + request.body.id + '/accept', { method: 'POST' });
    assert.equal(accepted.status, 200);
    const chat = await call(viewer.token, '/api/conversations', { method: 'POST', body: { to: peer.id } });
    assert.equal(chat.status, 201);
    const chatId = chat.body.id;
    assert.equal((await call(peer.token, '/api/conversations/' + chatId + '/messages', { method: 'POST', body: { text: 'Would you like to go for a coastal walk this weekend?' } })).status, 201);
    const context = await browser.newContext();
    const page = await context.newPage();
    const guest = await browser.newPage();
    const capture = async (target, route, ready, label, modes = ['light', 'dark']) => {
      await target.goto(web + route);
      await target.waitForSelector(ready, { timeout: 30000 });
      for (const theme of modes) {
        await target.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
        for (const width of widths) {
          await target.setViewportSize({ width, height: 900 });
          await target.evaluate(() => window.scrollTo(0, 0));
          await target.waitForTimeout(150);
          const size = await target.evaluate(() => {
            const content = document.querySelector('main > div');
            const tile = document.querySelector('.profile-tile');
            return { content: content?.getBoundingClientRect().width, tile: tile?.getBoundingClientRect().width,
              overflow: document.documentElement.scrollWidth > innerWidth + 1 };
          });
          measurements.push({ label, theme, width, ...size });
          if (!baseline) {
            assert.equal(size.overflow, false, `${label} ${theme} ${width}: horizontal overflow`); checks++;
            if (width >= 1024 && ['explore', 'connections', 'account', 'profile', 'chats'].includes(label)) {
              assert.ok(size.content > 650, `${label} must use desktop space: ${size.content}`); checks++;
            }
            if (size.tile) { assert.ok(size.tile <= 170 && size.tile >= 150, 'Reference tiles stay bounded, narrow phones shrink slightly'); checks++; }
          }
          if (output && ['home', 'explore', 'profile', 'connections', 'chat', 'account', 'appearance', 'safety'].includes(label)
            && ((theme === 'light' && [375, 768, 1440].includes(width)) || (theme === 'dark' && width === 1440)))
            await target.screenshot({ path: path.join(output, `${label}-${width}-${theme}.png`), fullPage: true });
        }
      }
    };
    await capture(guest, '/', 'main h1', 'home');
    for (const [route, label] of [['/explore', 'guest-explore'], ['/connections', 'guest-connections'], ['/chats', 'guest-chats'], ['/account', 'guest-account'], ['/people/' + peer.id, 'guest-profile']]) {
      await capture(guest, route, 'main h1', label);
      assert.equal((await guest.locator('main').innerText()).includes('Fictional Rowan'), false, 'Guest must not see private profile content');
    }
    await capture(guest, '/settings/appearance', '.scheme-choice', 'appearance');
    await capture(guest, '/safety', '[data-safety-page]', 'safety');
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', viewer.email); await page.fill('#password', viewer.password);
    await Promise.all([page.waitForURL(web + '/explore'), page.click('#kc-login')]);
    const routes = [
      ['/explore', '.people-grid', 'explore'], ['/people/' + peer.id + '?mode=friends', '[data-connect-modes]', 'profile'],
      ['/connections', '[data-connections="ready"]', 'connections'], ['/chats', '[data-chat-list]', 'chats'],
      ['/chats/' + chatId, '[data-conversation="ready"]', 'chat'], ['/account', '[data-account-panel="ready"]', 'account'],
      ['/me/profile', '[data-profile-editor="ready"]', 'editor'], ['/me/photos', '[data-photos-panel="ready"]', 'photos'],
      ['/me/matching', 'main h1', 'matching'], ['/travel', 'main h1', 'travel'],
      ['/me/safety', '[data-safety-services]', 'member-safety'], ['/me/meeting-safety', '[data-meeting-plan-form]', 'meeting-safety'],
      ['/me/notifications', '[data-notifications="ready"]', 'notifications'], ['/me/standing', '[data-standing="ready"]', 'standing'],
      ['/me/standing/history', '[data-standing="ready"]', 'standing-history'], ['/me/safety/people', '[data-safety-people-page="ready"]', 'safety-people'],
      ['/account/data', '[data-privacy-link]', 'account-data'], ['/me/notices', 'main h1', 'notices'],
      ['/me/verification', 'main h1', 'verification'], ['/privacy', '[data-privacy-notice]', 'privacy'],
      ['/terms', '[data-public-policy]', 'terms'], ['/community-guidelines', '[data-public-policy]', 'community'],
      ['/safety/advice', 'main h1', 'advice'], ['/not-found-fictional', 'main h1', 'not-found']
    ];
    if (process.env.SWOOVR_RESPONSIVE_QUICK !== '1') for (const [route, ready, label] of routes) await capture(page, route, ready, label);
    if (!baseline) {
      for (const [route, ready] of routes.slice(0, 12)) {
        await page.goto(web + route); await page.waitForSelector(ready);
        for (const width of [320, 768, 1440]) {
          await page.setViewportSize({ width, height: 900 });
          // Root-relative text doubles too; this tests actual reflow rather than a screenshot transform.
          await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
          await page.waitForTimeout(150);
          const overflow = await page.evaluate(() => [...document.querySelectorAll('main *, header *')].filter(e => { const r = e.getBoundingClientRect(); return r.right > innerWidth + 1 || r.left < -1; }).map(e => ({ tag: e.tagName, class: e.className?.baseVal ?? e.className, width: e.getBoundingClientRect().width, text: e.textContent.slice(0, 80) })));
          if (!await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)) {
            reflowProblems.push({ route, width, overflow });
            if (output) await page.screenshot({ path: path.join(output, `reflow-${route.split('/')[1]}-${width}.png`), fullPage: false });
          }
          checks++;
          if (output && width === 320 && ['/explore', '/chats/' + chatId, '/me/profile'].includes(route))
            await page.screenshot({ path: path.join(output, `text-200-${route.split('/')[1]}-320.png`), fullPage: false });
          await page.evaluate(() => { document.documentElement.style.fontSize = ''; });
        }
      }
      if (output) await fs.writeFile(path.join(output, 'reflow.json'), JSON.stringify(reflowProblems, null, 2) + '\n');
      await guest.goto(web + '/settings/appearance');
      for (const scheme of ['standard', 'lounge', 'pride', 'trans', 'union', 'violet', 'coral', 'eucalyptus']) {
        await guest.locator(`input[name="scheme"][value="${scheme}"]`).check();
        for (const mode of ['light', 'dark']) {
          await guest.locator(`input[name="mode"][value="${mode}"]`).check();
          assert.equal(await guest.locator('html').getAttribute('data-scheme'), scheme);
          assert.equal(await guest.locator('html').getAttribute('data-theme'), mode); checks += 2;
        }
      }
      // Labelled synthetic transport/result states exercise presentation only, after actual eligibility.
      let scenario = 'empty', release;
      await page.route('**/bff/api/explore?**', async route => {
        if (scenario === 'loading') await new Promise(resolve => { release = resolve; });
        const response = await route.fetch();
        assert.equal(response.status(), 200);
        if (scenario === 'error') return route.fulfill({ response, status: 503, body: JSON.stringify({ code: 'service-unavailable' }) });
        const body = await response.json(); body.people = [];
        return route.fulfill({ response, body: JSON.stringify(body) });
      });
      for (scenario of ['empty', 'error', 'loading']) {
        await page.goto(web + '/explore', { waitUntil: 'domcontentloaded' });
        await page.waitForSelector(scenario === 'empty' ? '[data-explore-empty]' : scenario === 'error' ? '[data-explore-unavailable]' : '[data-explore="loading"] [role="status"]');
        for (const width of widths) {
          await page.setViewportSize({ width, height: 900 });
          assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), scenario + ' layout'); checks++;
        }
        if (output) await page.screenshot({ path: path.join(output, `synthetic-${scenario}-1440.png`), fullPage: false });
        if (scenario === 'loading') {
          for (let i = 0; i < 100 && !release; i++) await page.waitForTimeout(50);
          assert.equal(typeof release, 'function'); release(); await page.waitForSelector('[data-explore-empty]'); }
      }
      await page.unroute('**/bff/api/explore?**');
      await page.goto(web + '/explore'); await page.waitForSelector('.people-grid');
      for (const width of widths) {
        await page.setViewportSize({ width, height: 900 });
        await page.locator('.tab').first().focus(); await page.keyboard.press('Tab');
        assert.ok(await page.evaluate(() => {
          const e = document.activeElement, r = e.getBoundingClientRect();
          return e.matches('.tab') && getComputedStyle(e).outlineStyle !== 'none' && r.left >= 0 && r.right <= innerWidth && r.top >= 0 && r.bottom <= innerHeight;
        }), 'Keyboard navigation remains visible at ' + width); checks++;
      }
      await page.locator('.profile-tile a').first().focus(); await page.keyboard.press('Tab');
      assert.ok(await page.evaluate(() => document.activeElement.matches('.profile-tile a') && getComputedStyle(document.activeElement.closest('.profile-tile')).outlineStyle !== 'none')); checks++;
      await page.goto(web + '/explore'); await page.waitForSelector('.people-grid');
      await page.setViewportSize({ width: 375, height: 900 });
      await page.keyboard.press('Tab');
      assert.equal(await page.evaluate(() => document.activeElement.classList.contains('skip-link')), true); checks++;
      await page.keyboard.press('Enter');
      assert.equal(await page.evaluate(() => document.activeElement.id), 'main-content'); checks++;
      await page.goto(web + '/chats/' + chatId); await page.waitForSelector('[data-composer]');
      await page.locator('[data-message-input]').fill('A fictional reply from the responsive browser test.');
      await page.locator('[data-send]').click();
      await page.waitForSelector('.bubble.mine'); checks++;
    }
    if (output) {
      await fs.writeFile(path.join(output, 'measurements.json'), JSON.stringify(measurements, null, 2) + '\n');
      await fs.writeFile(path.join(output, 'reflow.json'), JSON.stringify(reflowProblems, null, 2) + '\n');
    }
    assert.equal(reflowProblems.length, 0, `200% text overflow: ${reflowProblems.map(p => p.route + '@' + p.width).join(', ')}`);
    console.log(`${baseline ? 'BASELINE' : 'PASS'} member responsive: ${checks} assertions, ${measurements.length} route/theme/width observations.`);
  } finally { await edge.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
