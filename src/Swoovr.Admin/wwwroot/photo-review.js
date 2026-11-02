(() => {
  let returnId;
  function openReview() {
    const dialog = document.querySelector('[data-photo-dialog]');
    if (!dialog) { if (returnId) document.querySelector(`[data-review-open="${returnId}"]`)?.focus(); returnId = undefined; return; }
    if (typeof dialog.showModal === 'function' && !dialog.matches(':modal')) { dialog.removeAttribute('open'); dialog.showModal(); }
    dialog.querySelector('a,button,input,select,textarea')?.focus();
    if (dialog.dataset.ready) return;
    dialog.dataset.ready = 'true';
    dialog.addEventListener('close', () => { dialog.querySelectorAll('img').forEach(image => image.removeAttribute('src')); });
    dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.querySelector('[data-photo-close]')?.click(); });
  }
  document.addEventListener('click', event => {
    const dialog = event.target.closest?.('[data-photo-dialog]');
    if (dialog && event.target.closest('a[href]')) { returnId = dialog.querySelector('[data-reveal]')?.dataset.reveal; dialog.close(); }
  }, true);
  document.addEventListener('DOMContentLoaded', () => { openReview(); window.Blazor?.addEventListener('enhancedload', openReview); }, { once: true });
  openReview();
})();
