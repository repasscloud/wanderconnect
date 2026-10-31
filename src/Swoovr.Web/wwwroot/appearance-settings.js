(function () {
  const api = window.SwoovrAppearance;
  const state = window.SwoovrGuestAppearance;
  let form;
  function update(message) {
    state.apply();
    if (!form) return;
    for (const input of form.querySelectorAll('input[type=radio]')) input.checked = state.preference[input.name] === input.value;
    const palette = window.SwoovrPalettes.find(p => p.id === state.preference.scheme);
    form.querySelector('[data-current-choice]').textContent = palette.name + ' · ' + ({system:'System',light:'Light',dark:'Dark'})[state.preference.mode];
    form.querySelector('[role=status]').textContent = message || (state.available ? 'Saved on this browser.' : 'Applied for this visit. Browser storage is unavailable.');
  }
  function connect() {
    if(window.SwoovrAccountAppearance)window.SwoovrAccountAppearance.start(document.documentElement.dataset.appearanceOwner);
    form = document.querySelector('[data-appearance-settings]');
    // Enhanced navigation can restore SSR root attributes while preserving the form.
    state.apply();
    if (!form) return;
    if (form.dataset.connected) { update(); return; }
    form.dataset.connected = 'true';
    for (const fieldset of form.querySelectorAll('fieldset')) fieldset.disabled = false;
    form.querySelector('[data-reset]').disabled = false;
    form.addEventListener('change', event => {
      if (!event.target.matches('input[type=radio]')) return;
      const data = new FormData(form);
      state.preference = api.normalize({ scheme: data.get('scheme'), mode: data.get('mode') });
      state.available = api.save(state.storage, state.preference);
      update();
    });
    form.querySelector('[data-reset]').addEventListener('click', () => {
      state.preference = { ...api.defaults };
      state.available = api.save(state.storage, state.preference);
      update(state.available ? 'Reset to Standard and System. Saved on this browser.' : 'Appearance reset for this visit. Browser storage is unavailable.');
    });
    update();
  }
  window.addEventListener('storage', event => {
    if (event.key !== api.key && event.key !== null) return;
    const latest = api.load(state.storage);
    state.guestPreference=latest.preference;
    if(state.activeAccount)return;
    state.preference = latest.preference; state.available = latest.available;
    update('Appearance updated from another tab.');
  });
  document.addEventListener('DOMContentLoaded', connect);
  // Blazor enhanced navigation replaces page content without a document reload.
  if (window.Blazor) window.Blazor.addEventListener('enhancedload', connect);
})();
