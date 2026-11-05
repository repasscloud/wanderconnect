// Issue 217: on phones and tablets the Me / settings list is a popover side sheet opened from the header's menu button
// (the browser opens, closes and light-dismisses it; no script needed for that). This only closes it after a link in
// it is chosen, after Blazor enhanced navigation, and when the window widens to the desktop sidebar.
(function () {
  'use strict';
  const wide = window.matchMedia('(min-width: 900px)');
  function close() {
    document.querySelectorAll('[data-settings-menu]').forEach(menu => {
      try { if (menu.matches(':popover-open')) menu.hidePopover(); } catch { /* popover unsupported: the menu is a plain list */ }
    });
  }
  document.addEventListener('click', event => {
    if (event.target instanceof Element && event.target.closest('[data-settings-menu] a[href]')) close();
  });
  wide.addEventListener('change', close);
  const hook = () => window.Blazor?.addEventListener?.('enhancedload', close);
  if (window.Blazor) hook(); else window.addEventListener('load', hook, { once: true });
})();
