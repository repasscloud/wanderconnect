// Local acceptance for slice AF (#99, #22): the member-facing privacy notice draft. The public /privacy page is labelled
// DRAFT, covers the identity record, ban matching, 2 years after termination, contact history, unsent messages, evidence
// holds and the founder token, and links to the law-enforcement page; Me (the account page) and the data and deletion
// screen link to it, in Edge at phone width. The sign-up link lives in the Keycloak theme (checked in the file; the
// running Keycloak serves the main checkout's theme until this branch merges). Every person is fictional.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, cli, memberToken, call, sql, claims } = require('../auth/local-tokens.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let passed = 0; const pass = () => passed++;
  const output = process.env.SWOOVR_SCREENSHOTS;
  try {
    // Public, no sign-in.
    const response = await fetch(web + '/privacy');
    assert.equal(response.status, 200); pass();
    const html = await response.text();
    assert.ok(html.includes('DRAFT') && html.includes('not in force')); pass();
    for (const id of ['identity', 'bans', 'contact-history', 'messages', 'holds', 'founders', 'retention', 'law-enforcement', 'contact'])
      assert.ok(html.includes(`data-privacy-section="${id}"`), id);
    pass();
    for (const phrase of ['legal first and last name', '2 years', '30 days', 'founder recovery token', 'legal hold', 'privacy@swoovr.app', 'oaic.gov.au', 'href="/law-enforcement"'])
      assert.ok(html.includes(phrase), phrase);
    pass();
    assert.ok(!html.includes('24/7')); pass();
    // The sign-up page's link (theme file in this branch).
    const theme = await fs.readFile('containers/keycloak/themes/swoovr/login/register-commons.ftl', 'utf8');
    assert.ok(theme.includes('properties.swoovrPrivacyUrl') && theme.includes('privacyNoticeLink')); pass();
    assert.match(await fs.readFile('containers/keycloak/themes/swoovr/login/theme.properties', 'utf8'), /swoovrPrivacyUrl=http:\/\/localhost:5081\/privacy/); pass();

    // Signed in: Me and the data and deletion screen link to it.
    const member = await createMember('privacy');
    await cli('beta', 'add', member.email);
    const token = await memberToken(browser, member);
    await call(token, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Privacy', homeCityId: 2078025 } });
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', member.email); await page.fill('#password', member.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    await page.waitForSelector('[data-privacy-link]');
    assert.equal(await page.getAttribute('[data-privacy-link]', 'href'), '/privacy'); pass();
    await page.goto(web + '/account/data');
    await page.waitForSelector('[data-data-panel="none"] [data-privacy-link], [data-privacy-link]', { timeout: 20000 });
    assert.equal(await page.locator('[data-privacy-link]').first().getAttribute('href'), '/privacy'); pass();
    await page.locator('[data-privacy-link]').first().evaluate(a => a.click());
    await page.waitForSelector('[data-privacy-notice]');
    assert.ok((await page.locator('[data-draft]').innerText()).startsWith('DRAFT')); pass();
    if (output) await page.screenshot({ path: output + '/web-privacy.png', fullPage: true });
    await context.close();

    console.log(`PASS: privacy notice draft (${passed} checks).`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exit(1); });
