// Automated accessibility check over the main web pages (#48: accessibility as a release requirement). Dependency-free
// (no axe-core offline): in Edge, each page is checked for a document language and title, exactly one h1, a main landmark,
// labelled form controls, images with alt text, links and buttons with accessible names, unique ids, WCAG AA text
// contrast (4.5:1, or 3:1 for large text) against the nearest opaque background, and no sideways scrolling at 320 px.
// It is a floor, not an audit: screen-reader, keyboard-order and zoom reviews by a person are still needed.
// Public pages signed out; member pages as a fictional member. Needs hosts from this checkout.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { web, createMember, cli, memberToken, call, matchReady } = require('../auth/local-tokens.cjs');

const audit = () => {
  const problems = [];
  const visible = e => { const s = getComputedStyle(e); const r = e.getBoundingClientRect(); return s.display !== 'none' && s.visibility !== 'hidden' && (r.width > 0 || r.height > 0); };
  const name = e => (e.getAttribute('aria-label') || (e.getAttribute('aria-labelledby') || '').split(' ').map(id => document.getElementById(id)?.textContent ?? '').join(' ')
    || e.textContent || e.getAttribute('title') || e.querySelector('img[alt]')?.getAttribute('alt') || e.getAttribute('value') || '').trim();
  if (!document.documentElement.lang) problems.push('html has no lang');
  if (!document.title.trim()) problems.push('no title');
  const h1 = [...document.querySelectorAll('h1')].filter(visible).length;
  if (h1 !== 1) problems.push(`${h1} visible h1 elements`);
  if (!document.querySelector('main, [role=main]')) problems.push('no main landmark');
  for (const c of document.querySelectorAll('input:not([type=hidden]), select, textarea')) {
    if (!visible(c) && c.type !== 'checkbox') continue;
    const labelled = (c.id && document.querySelector(`label[for="${CSS.escape(c.id)}"]`)) || c.closest('label') || c.getAttribute('aria-label') || c.getAttribute('aria-labelledby') || c.getAttribute('title');
    if (!labelled) problems.push(`unlabelled ${c.tagName.toLowerCase()}${c.name ? ' ' + c.name : ''}${c.id ? '#' + c.id : ''}`);
  }
  for (const img of document.querySelectorAll('img')) if (!img.hasAttribute('alt')) problems.push(`img without alt ${img.getAttribute('src')?.slice(0, 40)}`);
  for (const e of document.querySelectorAll('a[href], button')) if (visible(e) && !name(e)) problems.push(`${e.tagName.toLowerCase()} without a name ${e.outerHTML.slice(0, 60)}`);
  const ids = {}; for (const e of document.querySelectorAll('[id]')) ids[e.id] = (ids[e.id] ?? 0) + 1;
  for (const [id, n] of Object.entries(ids)) if (n > 1) problems.push(`duplicate id ${id}`);
  const rgb = v => { const m = v.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 }; };
  const lum = c => { const f = x => { x /= 255; return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b); };
  const background = e => { for (let n = e; n; n = n.parentElement) { const s = getComputedStyle(n); if (s.backgroundImage !== 'none') return null; const c = rgb(s.backgroundColor); if (c && c.a >= 0.99) return c; } return { r: 255, g: 255, b: 255, a: 1 }; };
  const seen = new Set();
  for (const e of document.querySelectorAll('body *')) {
    if (!visible(e) || ![...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) continue;
    const s = getComputedStyle(e); const fg = rgb(s.color); const bg = background(e);
    if (!fg || !bg || fg.a < 0.99) continue;
    const ratio = (Math.max(lum(fg), lum(bg)) + 0.05) / (Math.min(lum(fg), lum(bg)) + 0.05);
    const size = parseFloat(s.fontSize), bold = Number(s.fontWeight) >= 700;
    const need = size >= 24 || (bold && size >= 18.66) ? 3 : 4.5;
    if (e.closest('[disabled], [aria-disabled=true]')) continue;
    if (ratio + 0.01 < need) { const key = `${s.color}/${size}`; if (!seen.has(key)) { seen.add(key); problems.push(`contrast ${ratio.toFixed(2)}:1 < ${need} "${e.textContent.trim().slice(0, 30)}"`); } }
  }
  if (document.documentElement.scrollWidth > window.innerWidth + 1) problems.push(`sideways scroll ${document.documentElement.scrollWidth}px at ${window.innerWidth}px`);
  return problems;
};

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
    for (const [path, ready] of [['/', 'main'], ['/safety', '[data-safety-page]'], ['/privacy', '[data-privacy-notice]'], ['/law-enforcement', '[data-law-enforcement-page]'], ['/settings/appearance', 'main']])
      await check(publicPage, path, ready);
    const member = await createMember('a11y');
    await cli('beta', 'add', member.email);
    const token = await memberToken(browser, member);
    await call(token, '/api/v1/me/profile', { method: 'PUT', body: { displayName: 'Fictional Access', homeCityId: 2078025 } });
    await matchReady(token);
    await call(token, '/api/v1/me/modes/friends', { method: 'PUT', body: { enabled: true, visible: true } });
    const page = await (await browser.newContext({ viewport: { width: 390, height: 844 } })).newPage();
    await page.goto(web + '/account/sign-in?intro=done');
    await page.fill('#username', member.email); await page.fill('#password', member.password);
    await Promise.all([page.waitForURL(web + '/account'), page.click('#kc-login')]);
    for (const [path, ready] of [['/account', 'main h1'], ['/me/profile', '[data-profile-editor="ready"]'], ['/me/photos', '[data-photos-panel="ready"]'],
      ['/explore?mode=friends', '.people-grid, [data-explore-local]'], ['/connections', 'main h1'], ['/chats', 'main h1'], ['/me/safety', '[data-safety-services]'],
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
