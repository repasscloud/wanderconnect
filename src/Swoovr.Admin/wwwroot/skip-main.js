// Shared member/staff keyboard bypass. No data collection, history write or navigation.
(function () {
  'use strict';
  document.addEventListener('click', function (event) {
    const link = event.target instanceof Element ? event.target.closest('a[data-skip-main]') : null;
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    const main = document.getElementById('main-content');
    if (!main) return;
    event.preventDefault();
    event.stopPropagation();
    main.focus();
  }, true);
})();
