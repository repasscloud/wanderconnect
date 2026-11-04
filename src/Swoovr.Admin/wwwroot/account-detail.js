// issue 190 account detail: confirmation dialogs for consequential actions. Each dialog holds a normal (non-enhanced) POST form,
// so the server re-renders the whole page after the action. Delegated, so it survives enhanced navigation.
(() => {
  document.addEventListener('click', event => {
    const opener = event.target.closest('[data-dialog-open]');
    if (opener) {
      const dialog = document.getElementById(opener.dataset.dialogOpen);
      if (dialog && typeof dialog.showModal === 'function') {
        event.preventDefault();
        dialog.showModal();
        dialog.querySelector('select, input:not([type=hidden]), textarea, button[type=submit]')?.focus();
      }
      return;
    }
    const closer = event.target.closest('[data-dialog-close]');
    if (closer) { closer.closest('dialog')?.close(); return; }
    // A click on the backdrop (the dialog element itself, outside its panel) closes it.
    if (event.target instanceof HTMLDialogElement && event.target.matches('.ad-dialog')) event.target.close();
  });
  // Forms with data-confirm ask once in a dialog before submitting; data-confirm-word requires typing that word.
  document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === '1') return;
    event.preventDefault();
    const word = form.dataset.confirmTyped || '';
    const dialog = document.createElement('dialog');
    dialog.className = 'ad-dialog';
    dialog.innerHTML = '<form method="dialog" class="ad-dialog-panel"><header><h2></h2><button type="button" class="ad-icon" data-dialog-close aria-label="Close">×</button></header>'
      + '<div class="ad-dialog-body"><p class="ui-help"></p></div>'
      + '<footer><button type="button" class="ui-button-ghost" data-dialog-close>Cancel</button><button type="submit" value="ok">Confirm</button></footer></form>';
    dialog.querySelector('h2').textContent = event.submitter?.textContent?.trim() || 'Confirm';
    dialog.querySelector('p').textContent = form.dataset.confirm;
    const ok = dialog.querySelector('button[type=submit]');
    if (word) {
      const label = document.createElement('label');
      label.className = 'ui-field';
      label.innerHTML = '<span></span><input autocomplete="off" />';
      label.querySelector('span').textContent = 'Type ' + word + ' to confirm';
      const input = label.querySelector('input');
      input.dataset.confirmWord = word;
      dialog.querySelector('.ad-dialog-body').append(label);
      ok.disabled = true;
    }
    const submitter = event.submitter;
    dialog.addEventListener('close', () => {
      if (dialog.returnValue === 'ok') { form.dataset.confirmed = '1'; form.requestSubmit(submitter ?? undefined); }
      dialog.remove();
    });
    document.body.append(dialog);
    dialog.showModal();
    (dialog.querySelector('input') ?? ok).focus();
  });
  // After a fresh sign-in (stale-session), ?resume=<dialog id> reopens the dialog the action came from, then drops the parameter.
  const resume = () => {
    const url = new URL(location.href);
    const id = url.searchParams.get('resume');
    if (!id) return;
    url.searchParams.delete('resume');
    history.replaceState(history.state, '', url.pathname + url.search + url.hash);
    const dialog = document.getElementById(id);
    if (dialog instanceof HTMLDialogElement && !dialog.open) {
      dialog.showModal();
      dialog.querySelector('select, input:not([type=hidden]), textarea, button[type=submit]')?.focus();
    }
  };
  document.addEventListener('DOMContentLoaded', () => { resume(); window.Blazor?.addEventListener('enhancedload', resume); }, { once: true });
  // A typed confirmation word (for example TERMINATE) enables the submit button only when it matches exactly.
  document.addEventListener('input', event => {
    const input = event.target.closest('[data-confirm-word]');
    if (!input) return;
    const submit = input.form?.querySelector('button[type=submit]');
    if (submit) submit.disabled = input.value.trim() !== input.dataset.confirmWord;
  });
})();
