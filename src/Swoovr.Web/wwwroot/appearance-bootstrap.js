// Synchronous external bootstrap runs before styles to preserve the requested mode.
(function () {
  const api = window.SwoovrAppearance;
  let storage;
  try { storage = window.localStorage; } catch { /* Private/blocked storage. */ }
  const initial = api.load(storage);
  const media = window.matchMedia('(prefers-color-scheme: dark)');
  const state = { preference: initial.preference, guestPreference: initial.preference, activeAccount: null, available: initial.available, media, storage };
  function apply() {
    const resolved = api.resolve(state.preference, media.matches);
    document.documentElement.dataset.scheme = resolved.scheme;
    document.documentElement.dataset.theme = resolved.mode;
    document.documentElement.dataset.requestedMode = state.preference.mode;
  }
  state.apply = apply;
  window.SwoovrGuestAppearance = state;
  if(window.SwoovrAccountAppearance)window.SwoovrAccountAppearance.start(document.documentElement.dataset.appearanceOwner);
  apply();
  media.addEventListener('change', apply);
  window.addEventListener('pagehide', () => media.removeEventListener('change', apply), { once: true });
  window.addEventListener('pageshow', event => { if (event.persisted) { media.addEventListener('change', apply); apply(); } });
})();
