// Automated accessibility check over the main web pages (#48: accessibility as a release requirement). Dependency-free
// (no axe-core offline): in Edge, each page is checked for a document language and title, exactly one h1, a main landmark,
// labelled form controls, images with alt text, links and buttons with accessible names, unique ids, WCAG AA text
// contrast (4.5:1, or 3:1 for large text) against the nearest opaque background, and no sideways scrolling at 320 px.
// It is a floor, not an audit: screen-reader, keyboard-order and zoom reviews by a person are still needed.
// Public pages signed out; member pages as a fictional member. Needs hosts from this checkout.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, cli, memberToken, call, matchReady } = require('../auth/local-tokens.cjs');

const audit = require('./audit.cjs');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const findings = {};
  let checked = 0;
  try {
    const check = async (page, path, ready) => {
      await page.goto(web + path);
      if (ready) await page.waitForSelector(ready, { timeout: 20000 });
      await page.waitForTimeout(300);
      for (const width of [390, 320]) {
        await page.setViewportSize({ width, height: 844 });
        const problems = await page.evaluate(audit);
        if (problems.length) findings[`${path} @${width}`] = problems;
      }
      checked++;
    };
    const publicPage = await (await browser.newContext()).newPage();
    for (const [path, ready] of [['/', 'main'], ['/safety', '[data-safety-page]'], ['/privacy', '[data-privacy-notice]'], ['/terms','[data-public-policy]'], ['/community-guidelines','[data-public-policy]'], ['/safety/advice','main h1'], ['/policies/fictional-unpublished','[data-approved-policy-unavailable]'], ['/law-enforcement', '[data-law-enforcement-page]'], ['/settings/appearance', 'main']])
      await check(publicPage, path, ready);
    const member = await createMember('a11y');
    await cli('beta', 'add', member.email);
    const token = await memberToken(browser, member);
    await call(token, '/api/me/profile', { method: 'PUT', body: { displayName: 'Fictional Access', homeCityId: 2078025 } });
    await matchReady(token);
    await call(token, '/api/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const page = await (await browser.newContext({ viewport: { width: 390, height: 844 } })).newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', member.email); await page.fill('#password', member.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    for (const [path, ready] of [['/account', 'main h1'], ['/account/policies','[data-policy-none]'], ['/me/meeting-safety','[data-meeting-plan-form]'], ['/me/profile', '[data-profile-editor="ready"]'], ['/me/photos', '[data-photos-panel="ready"]'],
      ['/explore?mode=friends', '.people-grid, [data-explore-empty], [data-explore-local]'], ['/connections', 'main h1'], ['/chats', 'main h1'], ['/me/safety', '[data-safety-services]'],
      ['/me/matching', 'main h1'], ['/travel', 'main h1'], ['/account/data', '[data-privacy-link]'], ['/me/notices', 'main h1'], ['/me/verification', 'main h1'],
      ['/me/notifications', '[data-notifications="ready"]'], ['/me/standing', '[data-standing="ready"]']])
      await check(page, path, ready);
    const report = Object.entries(findings).map(([where, list]) => `${where}\n  - ${list.join('\n  - ')}`).join('\n');
    if (process.env.SWOOVR_A11Y_REPORT) require('node:fs').writeFileSync(process.env.SWOOVR_A11Y_REPORT, report || 'no findings');
    assert.equal(Object.keys(findings).length, 0, `accessibility findings:\n${report}`);
    console.log(`PASS: accessibility floor on ${checked} pages at 390 and 320 px.`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error.message ?? error); process.exit(1); });
