// Applies the staff member's light/dark choice before first paint (Standard scheme only; per-browser preference).
(() => {
  const apply = () => {
    let theme = null;
    try { theme = localStorage.getItem('swoovr.admin.theme'); } catch { /* storage unavailable */ }
    if (theme !== 'light' && theme !== 'dark') theme = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    document.documentElement.dataset.theme = theme;
  };
  apply();
  window.swoovrApplyTheme = apply;
})();
