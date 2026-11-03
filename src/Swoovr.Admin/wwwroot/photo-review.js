(() => {
  let returnId;
  function openReview() {
    const dialog = document.querySelector('[data-photo-dialog],[data-review-dialog]');
    if (!dialog) { if (returnId) document.querySelector(`[data-review-open="${returnId}"]`)?.focus(); returnId = undefined; return; }
    if (typeof dialog.showModal === 'function' && !dialog.matches(':modal')) { dialog.removeAttribute('open'); dialog.showModal(); }
    dialog.querySelector('a,button,input,select,textarea')?.focus();
    if (dialog.dataset.ready) return;
    dialog.dataset.ready = 'true';
    dialog.addEventListener('close', () => { dialog.querySelectorAll('img').forEach(image => image.removeAttribute('src')); });
    dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.querySelector('[data-photo-close]')?.click(); });
  }
  document.addEventListener('click', event => {
    const dialog = event.target.closest?.('[data-photo-dialog],[data-review-dialog]');
    if (dialog && event.target.closest('a[href]')) { returnId = dialog.querySelector('[data-reveal]')?.dataset.reveal; dialog.close(); }
  }, true);
  document.addEventListener('DOMContentLoaded', () => { openReview(); window.Blazor?.addEventListener('enhancedload', openReview); }, { once: true });
  openReview();
})();

// Media review keyboard flow (progressive enhancement; the server re-validates every decision).
// V reveal/conceal · A/R/E choose decision (first reason auto-selected, focus moves to Reason)
// ↑/↓ in Reason change reason · Enter in Reason saves · ←/→ previous/next item.
(() => {
  const dialog = () => document.querySelector('[data-photo-dialog],[data-review-dialog]');
  const select = () => dialog()?.querySelector('[data-photo-decision]');
  let master;
  function snapshot(sel) {
    if (sel.dataset.snap) return;
    sel.dataset.snap = '1';
    master = [...sel.querySelectorAll('option[data-decision]')].map(o => ({ value: o.value, text: o.textContent, decision: o.dataset.decision, notes: o.dataset.notes === 'required' }));
  }
  function syncNotes(sel) {
    const d = dialog(); if (!d) return;
    const opt = master?.find(o => o.value === sel.value);
    const field = d.querySelector('[data-notes-field]'); const area = field?.querySelector('textarea');
    if (!field) return;
    const show = !!opt && opt.decision !== 'approve';
    field.hidden = !show && !(area && area.value.trim());
    const hint = field.querySelector('[data-notes-hint]'); if (hint) hint.textContent = opt?.notes ? '(required)' : '(optional)';
    d.querySelectorAll('[data-decide]').forEach(b => b.setAttribute('aria-pressed', String(!!opt && b.dataset.decide === opt.decision)));
  }
  function choose(decision) {
    const sel = select(); if (!sel) return;
    snapshot(sel);
    const items = master.filter(o => o.decision === decision);
    sel.replaceChildren(...items.map(o => { const el = document.createElement('option'); el.value = o.value; el.textContent = o.text; el.dataset.decision = o.decision; if (o.notes) el.dataset.notes = 'required'; return el; }));
    sel.value = items[0]?.value ?? '';
    syncNotes(sel);
    sel.focus();
  }
  function step(dir) {
    const link = dialog()?.querySelector(dir > 0 ? '[data-review-next]' : '[data-review-prev]');
    link?.click();
  }
  document.addEventListener('click', event => {
    const decide = event.target.closest?.('[data-photo-dialog] [data-decide]');
    if (decide) { event.preventDefault(); choose(decide.dataset.decide); return; }
    const row = event.target.closest?.('tr[data-row-link]');
    if (row && !event.target.closest('a,button,input,select,textarea,summary')) row.querySelector('[data-review-open],[data-row-target]')?.click();
  });
  document.addEventListener('change', event => {
    if (event.target.matches?.('[data-photo-decision]')) { snapshot(event.target); syncNotes(event.target); }
  });
  document.addEventListener('keydown', event => {
    const d = dialog(); if (!d || event.metaKey || event.ctrlKey || event.altKey) return;
    const target = event.target;
    if (target.matches?.('[data-photo-decision]')) {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const options = [...target.options].filter(o => o.value);
        const i = options.findIndex(o => o.value === target.value);
        const next = options[Math.min(options.length - 1, Math.max(0, i + (event.key === 'ArrowDown' ? 1 : -1)))];
        if (next) { target.value = next.value; snapshot(target); syncNotes(target); }
      } else if (event.key === 'Enter') {
        event.preventDefault();
        const notes = d.querySelector('[data-notes-field]:not([hidden]) textarea');
        const needs = master?.find(o => o.value === target.value)?.notes; if (notes && needs && !notes.value.trim()) { notes.focus(); return; }
        const form = target.form; form?.requestSubmit(form.querySelector('[data-photo-decide]'));
      }
      return;
    }
    if (target.closest?.('input,textarea,select,[contenteditable]')) return;
    const key = event.key.toLowerCase();
    if (key === 'v') { d.querySelector('[data-reveal]')?.click(); event.preventDefault(); }
    else if (key === 'a' || key === 'r' || key === 'e') { choose({ a: 'approve', r: 'reject', e: 'escalate' }[key]); event.preventDefault(); }
    else if (event.key === 'ArrowRight') step(1);
    else if (event.key === 'ArrowLeft') step(-1);
  });
  // Re-apply server-rendered state after enhanced navigation re-renders the dialog.
  const restore = () => { const sel = select(); if (sel) { delete sel.dataset.snap; snapshot(sel); if (sel.value) syncNotes(sel); } };
  document.addEventListener('DOMContentLoaded', () => { restore(); window.Blazor?.addEventListener('enhancedload', restore); }, { once: true });
  restore();
})();

// Date of birth review keys: M matches ID · U leave unverified · D compare in Didit · ⌘/Ctrl+Enter save and next.
(() => {
  document.addEventListener('keydown', event => {
    const form = document.querySelector('[data-review-dialog] [data-dob-form]'); if (!form) return;
    if ((event.metaKey || event.ctrlKey) && event.key === 'Enter') { event.preventDefault(); form.requestSubmit(form.querySelector('[data-dob-save]')); return; }
    if (event.metaKey || event.ctrlKey || event.altKey || event.target.closest?.('input,textarea,select,[contenteditable]')) return;
    const key = event.key.toLowerCase();
    if (key === 'm' || key === 'u') {
      event.preventDefault();
      const radio = form.querySelector(`[data-resolve="${key === 'm' ? 'verified' : 'notVerified'}"]`);
      if (radio) { radio.checked = true; const area = form.querySelector('textarea[name=reason]'); area?.focus(); area?.setSelectionRange(area.value.length, area.value.length); }
    } else if (key === 'd') {
      const link = document.querySelector('[data-review-dialog] [data-didit-open]'); if (link) { event.preventDefault(); link.click(); }
    }
  });
})();
