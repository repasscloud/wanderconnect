// issue 181: changing the Safety & Support country updates the page straight away; no "Show contacts" step. Delegated so it
// survives Blazor enhanced navigation. Without script the form's <noscript> button does the same.
(function () {
  'use strict';
  document.addEventListener('change', event => {
    const select = event.target instanceof HTMLSelectElement && event.target.matches('[data-country-select]') ? event.target : null;
    if (!select || !select.value || !select.form) return;
    select.form.requestSubmit();
  });
})();
