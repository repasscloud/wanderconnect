// Shared components and restyled screens (docs/design/reference/UI-STYLE.md), in actual Edge against Development hosts
// with Prototype__Enabled=true: equal profile tiles, tab bar active state and placement, filters sheet, fixed badge
// colours, chat bubble sides, computed fonts on every restyled screen in light and dark, no sideways scroll at 390px.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { assertSwoovrType } = require('../ui-fonts.cjs');
const base = process.env.SWOOVR_WEB_URL || 'http://127.0.0.1:5081';
const output = process.env.SWOOVR_BROWSER_OUTPUT || '.local/ui-browser';
const screens = ['/', '/settings/appearance', '/account', '/prototype/people', '/prototype/people/10000000-0000-0000-0000-000000000002', '/prototype/conversation', '/prototype/components', '/not-found'];
(async () => {
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  try {
    const context = await browser.newContext({ colorScheme: 'light', viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const look = async (scheme, mode) => {
      await page.goto(base + '/settings/appearance');
      await page.locator(`input[name=scheme][value=${scheme}]`).check(); await page.locator(`input[name=mode][value=${mode}]`).check();
      await page.waitForFunction(([s, m]) => document.documentElement.dataset.scheme === s && document.documentElement.dataset.theme === m, [scheme, mode]);
    };

    // Every screen: Swoovr type (Georgia only on data-font="quote") and no sideways scroll, light and dark.
    for (const mode of ['light', 'dark']) {
      await look('standard', mode);
      for (const path of screens) {
        await page.goto(base + path);
        await assertSwoovrType(page, `${path} ${mode}`); pass();
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${path} ${mode} scrolls sideways`); pass();
      }
    }
    await look('standard', 'light');

    // Tiles: photo and photo-free tiles are all exactly 170 × 270, two columns with a 10px gap at 390px.
    for (const path of ['/prototype/people', '/prototype/people?hideSparse=false', '/prototype/components']) {
      await page.goto(base + path);
      const tiles = await page.locator('.profile-tile').evaluateAll(els => els.map(e => { const r = e.getBoundingClientRect(); return { w: Math.round(r.width), h: Math.round(r.height), x: Math.round(r.x), kind: e.dataset.tile }; }));
      assert.ok(tiles.length >= 2, `${path}: tiles rendered`);
      assert.ok(tiles.some(t => t.kind === 'photo') && tiles.some(t => t.kind === 'quote'), `${path}: both tile kinds`); pass();
      for (const t of tiles) assert.deepEqual([t.w, t.h], [170, 270], `${path}: tile ${t.kind} is ${t.w}×${t.h}`);
      pass();
      const columns = [...new Set(tiles.map(t => t.x))].sort((a, b) => a - b);
      assert.equal(columns.length, 2, `${path}: two columns`); assert.equal(columns[1] - columns[0], 180, `${path}: 10px gap`); pass();
    }
    assert.equal(await page.locator('[data-tile=quote] [data-font=quote]').first().evaluate(e => getComputedStyle(e).fontFamily), 'Georgia, "Times New Roman", serif'); pass();
    // Large text keeps tiles equal to one another (rows share the tallest height).
    await page.goto(base + '/prototype/components'); await page.evaluate(() => document.documentElement.style.fontSize = '200%');
    const large = await page.locator('.profile-tile').evaluateAll(els => els.map(e => Math.round(e.getBoundingClientRect().height)));
    assert.equal(new Set(large).size, 1, `tiles differ at 200% text: ${large}`); pass();
    await page.evaluate(() => document.documentElement.style.fontSize = '');

    // Tab bar: active item per section (aria-current, bold), Connections opens requests and connections (slice L), fixed 80px bar at the bottom on phones.
    for (const [path, active] of [['/prototype/people', 'Explore'], ['/prototype/people/10000000-0000-0000-0000-000000000002', 'Explore'], ['/prototype/conversation', 'Chats'], ['/account', 'Me'], ['/settings/appearance', 'Me'], ['/connections', 'Connections'], ['/', null]]) {
      await page.goto(base + path);
      const tabs = await page.locator('.tab-bar [data-tab]').evaluateAll(els => els.map(e => ({ tab: e.dataset.tab, link: e.tagName === 'A', current: e.getAttribute('aria-current'), weight: getComputedStyle(e).fontWeight })));
      assert.deepEqual(tabs.map(t => t.tab), ['Explore', 'Connections', 'Chats', 'Me']);
      assert.deepEqual(tabs.filter(t => t.current === 'page').map(t => t.tab), active ? [active] : [], `${path}: active tab`);
      for (const t of tabs) assert.equal(t.weight, t.current ? '700' : '400', `${path}: ${t.tab} weight`);
      assert.equal(tabs.find(t => t.tab === 'Connections').link, true); pass();
    }
    const bar = await page.locator('.tab-bar').evaluate(e => { const r = e.getBoundingClientRect(); return { position: getComputedStyle(e).position, bottom: Math.round(r.bottom), height: Math.round(r.height) }; });
    assert.deepEqual(bar, { position: 'fixed', bottom: 844, height: 80 }); pass();

    // Filters: a closed sheet on phones that the Filters button opens; inline beside the grid on wide screens.
    await page.goto(base + '/prototype/people');
    assert.equal(await page.getByLabel('Hide sparse profiles', { exact: true }).isVisible(), false); pass();
    await page.getByRole('button', { name: 'Filters', exact: true }).click();
    assert.equal(await page.locator('#filters').evaluate(e => e.matches(':popover-open')), true); pass();
    await page.getByLabel('Hide sparse profiles', { exact: true }).check();
    await page.screenshot({ path: output + '/filters-sheet-390.png' });
    await page.getByRole('button', { name: 'Apply demo settings', exact: true }).click(); await page.waitForURL(/hideSparse=true/); pass();
    await page.setViewportSize({ width: 1280, height: 900 }); await page.goto(base + '/prototype/people');
    assert.equal(await page.getByLabel('Hide sparse profiles', { exact: true }).isVisible(), true);
    assert.equal(await page.getByRole('button', { name: 'Filters', exact: true }).isVisible(), false);
    assert.equal(await page.locator('.tab-bar').evaluate(e => getComputedStyle(e).position), 'static'); pass();
    await page.setViewportSize({ width: 390, height: 844 });

    // Badges keep their colours in every scheme and mode.
    const badgeColours = async () => { await page.goto(base + '/prototype/components'); return page.locator('[data-badges] .badge').evaluateAll(els => els.map(e => getComputedStyle(e.querySelector('.badge-disc, .badge-bird')).fill)); };
    const standard = await badgeColours();
    assert.deepEqual(standard, ['rgb(8, 124, 250)', 'rgb(0, 170, 160)', 'rgb(212, 175, 55)', 'rgb(224, 17, 95)', 'rgb(192, 192, 192)']); pass();
    await look('violet', 'dark'); assert.deepEqual(await badgeColours(), standard); pass();
    // Simulated checks on fictional people say so in their accessible name.
    await page.goto(base + '/prototype/people');
    assert.match(await page.locator('.profile-tile .badge').first().getAttribute('aria-label'), /simulated/); pass();

    // Chat (D56): my reply is right-aligned in the primary colour; theirs is the soft pill on the left.
    await page.goto(base + '/prototype/conversation');
    await page.getByRole('button', { name: 'Start fictional scenario', exact: true }).click(); await page.waitForURL(/actor=alex/);
    await page.getByRole('button', { name: 'Send sample request', exact: true }).click(); await page.waitForURL(/actor=alex/);
    await page.getByRole('link', { name: 'View as fictional Jamie', exact: true }).click(); await page.waitForURL(/actor=jamie/);
    await page.locator('.conversation-roles [aria-current=page]', { hasText: 'View as fictional Jamie' }).waitFor();
    assert.equal(await page.locator('.conversation-roles [aria-current=page]').count(), 1); pass();
    await page.getByRole('button', { name: 'Accept request', exact: true }).click(); await page.getByRole('heading', { name: 'Conversation accepted', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Send sample reply', exact: true }).click(); await page.locator('.bubble.mine').waitFor();
    const bubbles = await page.locator('.conversation-message').evaluateAll(els => els.map(e => ({ mine: e.classList.contains('mine'), right: Math.round(e.getBoundingClientRect().right), bg: getComputedStyle(e).backgroundColor })));
    const [primary, soft] = await page.evaluate(() => ['--primary', '--soft'].map(name => getComputedStyle(document.documentElement).getPropertyValue(name).trim()));
    const theirs = bubbles.find(b => !b.mine), mine = bubbles.find(b => b.mine);
    assert.ok(theirs && mine && mine.right > theirs.right, 'mine sits right of theirs');
    const toRgb = hex => `rgb(${[1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)).join(', ')})`;
    assert.equal(mine.bg, toRgb(primary)); assert.equal(theirs.bg, toRgb(soft), 'their bubble is the soft pill (D56)'); pass();
    await page.screenshot({ path: output + '/chat-violet-dark-390.png', fullPage: true });

    assert.deepEqual(errors, []);
    await context.close();
    console.log(`PASS: ${passed} UI checks in actual Edge: Swoovr type and no sideways scroll on 8 restyled screens light/dark, equal 170×270 photo and photo-free tiles (also equal at 200% text), tab bar active state and phone/desktop placement, filters sheet, fixed badge colours across schemes, chat bubble sides.`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
