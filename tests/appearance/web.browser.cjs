// Optional real browser acceptance; run against the built local web host.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const palettes = require('../../src/Swoovr.Web/wwwroot/appearance-palettes.js');
const { assertSwoovrType } = require('../ui-fonts.cjs');
const base = process.env.SWOOVR_WEB_URL || 'http://127.0.0.1:5081';
if (new URL(base).hostname !== '127.0.0.1') throw Error('Browser checks require a loopback host');
const output = process.env.SWOOVR_BROWSER_OUTPUT || '.local/appearance-browser';
(async () => {
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const context = await browser.newContext({ colorScheme: 'light', viewport: { width: 1280, height: 1000 } });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    for (const path of ['/', '/settings/appearance', '/account']) { await page.goto(base + path); await assertSwoovrType(page, path); }
    await page.goto(base + '/settings/appearance');
    await page.getByRole('radio', { name: 'Eucalyptus', exact: true }).check();
    const schemeInput = id => page.locator(`input[name=scheme][value=${id}]`);
    const modeInput = mode => page.locator(`input[name=mode][value=${mode}]`);
    const expectState = async (scheme, requested, resolved) => {
      await page.waitForFunction(([scheme,requested,resolved]) => document.documentElement.dataset.scheme === scheme && document.documentElement.dataset.requestedMode === requested && document.documentElement.dataset.theme === resolved, [scheme,requested,resolved]);
      assert.equal(await schemeInput(scheme).isChecked(), true);
      assert.equal(await modeInput(requested).isChecked(), true);
    };
    const toRgb = hex => `rgb(${[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)).join(', ')})`;
    for (const p of palettes) for (const mode of ['system','light','dark']) {
      await schemeInput(p.id).check(); await modeInput(mode).check();
      await expectState(p.id,mode,mode==='system'?'light':mode);
      const actual = await page.evaluate(() => ({ bg: getComputedStyle(document.body).backgroundColor, primary: getComputedStyle(document.querySelector('.sample-profile .primary-button')).backgroundColor, onPrimary: getComputedStyle(document.querySelector('.sample-profile .primary-button')).color }));
      const expected = p[mode==='system'?'light':mode];
      assert.equal(actual.bg,toRgb(expected.bg)); assert.equal(actual.primary,toRgb(expected.primary)); assert.equal(actual.onPrimary,toRgb(expected.onPrimary));
    }
    await modeInput('system').check();
    await page.emulateMedia({colorScheme:'dark'}); await expectState('eucalyptus','system','dark');
    await page.reload(); await expectState('eucalyptus','system','dark');
    await modeInput('light').check(); await page.emulateMedia({colorScheme:'dark'}); await expectState('eucalyptus','light','light');
    await page.screenshot({path:output+'/eucalyptus-light-desktop.png',fullPage:true});
    const other = await context.newPage(); await other.goto(base+'/settings/appearance');
    await other.getByRole('radio',{name:'Union Jack',exact:true}).check();
    await expectState('union','light','light');
    await page.getByRole('button',{name:'Reset appearance',exact:true}).click();
    await expectState('standard','system','dark');
    // Native radio keyboard navigation remains available.
    await modeInput('system').focus(); await page.keyboard.press('ArrowRight');
    await expectState('standard','light','light');
    await schemeInput('union').check(); await modeInput('dark').check();
    await page.evaluate(() => { window.appearanceNavigationCount=0; Blazor.addEventListener('enhancedload', () => window.appearanceNavigationCount++); });
    await page.getByRole('link',{name:'Swoovr',exact:true}).click();
    await page.waitForFunction(() => window.appearanceNavigationCount >= 1);
    assert.equal(await page.evaluate(() => document.documentElement.dataset.scheme),'union','navigation must retain selected scheme on Home');
    assert.equal(await page.evaluate(() => document.documentElement.dataset.theme),'dark','navigation must retain mode on Home');
    await page.getByRole('link',{name:'Choose your appearance',exact:true}).click();
    await page.waitForFunction(() => window.appearanceNavigationCount >= 2);
    await expectState('union','dark','dark');
    await modeInput('light').check();
    await schemeInput('pride').check(); await expectState('pride','light','light');
    await page.setViewportSize({width:320,height:900});
    await page.evaluate(() => document.documentElement.style.fontSize='200%');
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),true,'320px large text must not overflow');
    await page.screenshot({path:output+'/pride-light-320-large-text.png',fullPage:true});
    await page.evaluate(() => document.documentElement.style.fontSize='100%');
    await modeInput('dark').check();
    await page.screenshot({path:output+'/pride-dark-320.png',fullPage:true});
    await assertSwoovrType(page, 'appearance dark 320px');
    await page.evaluate(() => localStorage.setItem('swoovr.appearance.guest.v1','{broken'));
    await page.reload(); await expectState('standard','system','dark');
    const denied = await browser.newContext();
    await denied.addInitScript(() => Object.defineProperty(window,'localStorage',{get(){throw new Error('test storage denial');}}));
    const blocked = await denied.newPage(); await blocked.goto(base+'/settings/appearance');
    await blocked.getByRole('radio',{name:'Coral',exact:true}).check();
    assert.match(await blocked.getByRole('status').innerText(),/storage is unavailable/);
    assert.equal(await blocked.evaluate(() => document.documentElement.dataset.scheme),'coral');
    await denied.close();
    assert.deepEqual(errors,[]);
    await context.close();
    console.log('PASS: actual Edge Helvetica Neue computed on every text element (home, appearance, account; light and dark), 24 choices/16 computed palettes, system/explicit mode, reload, cross-tab, reset, keyboard, enhanced navigation, 320px 200% text, malformed storage; injected storage denial retained in-memory choice. Screenshots saved locally.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode=1; });
