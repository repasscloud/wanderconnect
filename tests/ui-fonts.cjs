// Computed type check shared by the browser suites (docs/design/reference/UI-STYLE.md checklist 1):
// every visible text element resolves to the Swoovr stack and is antialiased. The two exceptions in
// UI-STYLE.md opt in with data-font: "quote" (Georgia, photo-free tile quote) and "mono" (version line).
const assert = require('node:assert/strict');
const stack = '"Helvetica Neue", Helvetica, Arial, sans-serif';
const allowed = { quote: 'Georgia, "Times New Roman", serif', mono: 'ui-monospace, "SF Mono", Menlo, monospace' };
async function assertSwoovrType(page, label) {
  const found = await page.evaluate(() => {
    const result = [];
    for (const el of document.querySelectorAll('body *')) {
      const hasText = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) || ['INPUT', 'BUTTON', 'SELECT', 'TEXTAREA'].includes(el.tagName);
      if (!hasText || !el.getClientRects().length) continue;
      const s = getComputedStyle(el);
      if (s.visibility !== 'hidden') result.push([s.fontFamily, s.webkitFontSmoothing, el.tagName + (el.className ? '.' + el.className : ''), el.closest('[data-font]')?.dataset.font ?? null]);
    }
    return result;
  });
  assert.ok(found.length > 0, `${label}: no visible text found`);
  for (const [family, smoothing, element, exception] of found) {
    assert.equal(family, exception ? allowed[exception] : stack, `${label}: ${element} uses ${family}`);
    assert.equal(smoothing, 'antialiased', `${label}: ${element} is not antialiased`);
  }
  return found.length;
}
module.exports = { assertSwoovrType, stack };
