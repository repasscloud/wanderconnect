// Progressive enhancement only: no session, permission or form state is held here.
(() => {
  const narrow = matchMedia('(max-width: 800px)');
  let dispose;
  function initialize() {
    dispose?.();
    const sidebar = document.querySelector('#navigation-drawer .admin-sidebar');
    const home = document.querySelector('#sidebar-home');
    const drawer = document.querySelector('#navigation-drawer');
    const trigger = document.querySelector('[data-drawer-open]');
    if (!sidebar || !home || !drawer || !trigger) return;
    document.documentElement.classList.add('shell-ready');
    const close = () => drawer.close();
    const onClose = () => {
      trigger.setAttribute('aria-expanded', 'false');
      if (narrow.matches) trigger.focus();
    };
    const open = () => { drawer.showModal(); trigger.setAttribute('aria-expanded', 'true'); };
    const backdrop = event => { if (event.target === drawer) close(); };
    const resize = () => {
      if (drawer.open) close();
    };
    trigger.addEventListener('click', open);
    const closeButton = sidebar.querySelector('[data-drawer-close]');
    closeButton.addEventListener('click', close);
    drawer.addEventListener('close', onClose);
    drawer.addEventListener('click', backdrop);
    narrow.addEventListener('change', resize);
    resize();
    dispose = () => {
      narrow.removeEventListener('change', resize);
      drawer.removeEventListener('close', onClose);
      drawer.removeEventListener('click', backdrop);
      trigger.removeEventListener('click', open);
      closeButton.removeEventListener('click', close);
    };
  }
  // Leave the native top layer before enhanced navigation replaces the dialog's DOM.
  document.addEventListener('click', event => {
    if (event.target.closest?.('#navigation-drawer a[href]')) {
      const drawer = document.querySelector('#navigation-drawer');
      if (drawer?.open) drawer.close();
    }
  }, true);
  initialize();
  // The layout is replaced during enhanced SSR navigation as well as full-page posts.
  document.addEventListener('DOMContentLoaded', () => window.Blazor?.addEventListener('enhancedload', initialize), {once:true});
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') document.querySelector('.staff-menu[open]')?.removeAttribute('open');
  });
})();

// Copy-to-clipboard for reference buttons (data-copy). Progressive enhancement only.
document.addEventListener('click', async event => {
  const button = event.target.closest?.('button[data-copy]');
  if (!button) return;
  try {
    await navigator.clipboard.writeText(button.dataset.copy);
    button.setAttribute('data-copied', '');
    button.title = 'Copied';
    setTimeout(() => { button.removeAttribute('data-copied'); button.title = 'Copy'; }, 1500);
  } catch { button.title = 'Copy failed'; }
});
