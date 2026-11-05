// Signed-out colour mode toggle (Standard light / device / dark) and the policy contents list. Static SSR pages, so
// this binds by delegation and re-syncs after Blazor enhanced navigation. Signed-in members use Appearance instead.
(function () {
  'use strict';
  const wide = window.matchMedia('(min-width: 960px)');
  function signedOut() { return !document.documentElement.dataset.appearanceOwner; }
  function sync() {
    const state = window.SwoovrGuestAppearance;
    const mode = state?.preference?.mode ?? 'system';
    document.querySelectorAll('[data-mode-toggle]').forEach(group => {
      group.hidden = !state || !signedOut();
      group.querySelectorAll('[data-mode-choice]').forEach(button =>
        button.setAttribute('aria-pressed', String(button.dataset.modeChoice === mode)));
    });
    // Contents list: open beside the article on wide screens, collapsed behind "On this page" on narrow ones.
    document.querySelectorAll('[data-policy-toc]').forEach(details => { details.open = wide.matches; });
  }
  document.addEventListener('click', event => {
    const button = event.target instanceof Element ? event.target.closest('[data-mode-choice]') : null;
    const state = window.SwoovrGuestAppearance;
    if (!button || !state || !signedOut()) return;
    const api = window.SwoovrAppearance;
    api.saveGuestMode(state.storage, button.dataset.modeChoice);
    state.preference = api.signedOut(state.storage);
    if (state.storage === undefined) state.preference = { scheme: 'standard', mode: button.dataset.modeChoice };
    state.guestPreference = state.preference;
    state.apply();
    sync();
  });
  // Another tab changed the guest mode.
  window.addEventListener('storage', event => {
    const state = window.SwoovrGuestAppearance, api = window.SwoovrAppearance;
    if (!state || !signedOut() || (event.key !== api.guestModeKey && event.key !== null)) return;
    state.preference = api.signedOut(state.storage); state.apply(); sync();
  });
  wide.addEventListener('change', sync);
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', sync); else sync();
  // Blazor enhanced navigation patches the DOM without a reload.
  const hook = () => window.Blazor?.addEventListener?.('enhancedload', () => { window.SwoovrGuestAppearance?.apply(); sync(); });
  if (window.Blazor) hook(); else window.addEventListener('load', hook, { once: true });
})();
