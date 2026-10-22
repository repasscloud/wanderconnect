// Computed type check shared by the browser suites (docs/design/reference/UI-STYLE.md checklist 1):
// every visible text element resolves to the Swoovr stack and is antialiased. Georgia is allowed only
// on elements that opt in with data-font="quote" (the photo-free profile tile quote).
const assert = require('node:assert/strict');
const stack = '"Helvetica Neue", Helvetica, Arial, sans-serif';
async function assertSwoovrType(page, label) {
  const found = await page.evaluate(() => {
    const result = [];
    for (const el of document.querySelectorAll('body *')) {
      const hasText = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) || ['INPUT', 'BUTTON', 'SELECT', 'TEXTAREA'].includes(el.tagName);
      if (!hasText || !el.getClientRects().length || el.closest('[data-font=quote]')) continue;
      const s = getComputedStyle(el);
      if (s.visibility !== 'hidden') result.push([s.fontFamily, s.webkitFontSmoothing, el.tagName + (el.className ? '.' + el.className : '')]);
    }
    return result;
  });
  assert.ok(found.length > 0, `${label}: no visible text found`);
  for (const [family, smoothing, element] of found) {
    assert.equal(family, stack, `${label}: ${element} uses ${family}`);
    assert.equal(smoothing, 'antialiased', `${label}: ${element} is not antialiased`);
  }
  return found.length;
}
module.exports = { assertSwoovrType, stack };
