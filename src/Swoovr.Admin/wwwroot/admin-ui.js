// Admin productivity layer (progressive enhancement only; every page works without it).
// ⌘K / Ctrl+K jump palette · / focus search · ? shortcuts · g + letter go to page · ⌘/Ctrl+Enter submit · Esc close.
(() => {
  const GO = { o: '/', q: '/cases', p: '/appeals', m: '/media', d: '/dob-reviews', a: '/accounts', b: '/beta', u: '/support', c: '/contact-history',
    r: '/registry', h: '/holds', l: '/law-enforcement', x: '/disclosures', e: '/access', s: '/security', y: '/safety', k: '/beta-scorecard', n: '/code-reports' };
  const navigate = url => (window.Blazor?.navigateTo ? window.Blazor.navigateTo(url) : (location.href = url));
  const typing = el => el?.closest?.('input,textarea,select,[contenteditable]');
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  /* Theme */
  document.addEventListener('click', event => {
    if (!event.target.closest?.('[data-theme-toggle]')) return;
    const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem('swoovr.admin.theme', next); } catch { /* storage unavailable */ }
  });
  document.addEventListener('DOMContentLoaded', () => window.Blazor?.addEventListener('enhancedload', () => window.swoovrApplyTheme?.()), { once: true });

  /* Palette */
  let palette;
  function pages() {
    return [...document.querySelectorAll('#sidebar-home .admin-sidebar a.nav-link')].map(a => {
      const group = a.closest('.nav-group')?.querySelector('.nav-heading')?.textContent?.trim() ?? '';
      return { label: a.textContent.trim(), href: a.getAttribute('href'), group };
    });
  }
  function openPalette() {
    if (palette?.open) return;
    palette = document.createElement('dialog');
    palette.className = 'ui-palette';
    palette.setAttribute('aria-label', 'Jump to');
    palette.innerHTML = '<input type="search" placeholder="Jump to a page or search accounts…" aria-label="Jump to" autocomplete="off"><ul role="listbox"></ul>';
    document.body.append(palette);
    const input = palette.querySelector('input'), list = palette.querySelector('ul');
    const draw = () => {
      const q = input.value.trim(), lq = q.toLowerCase();
      const items = pages().filter(p => !lq || p.label.toLowerCase().includes(lq) || p.group.toLowerCase().includes(lq));
      if (q.length > 1) items.push({ label: `Search accounts for “${q}”`, href: '/accounts?q=' + encodeURIComponent(q), group: 'Accounts' });
      list.innerHTML = items.map((p, i) => `<li role="option" aria-selected="${i === 0}" data-href="${esc(p.href)}">${esc(p.label)}<small>${esc(p.group)}</small></li>`).join('');
    };
    const choose = li => { if (!li) return; palette.close(); navigate(li.dataset.href); };
    input.addEventListener('input', draw);
    input.addEventListener('keydown', e => {
      const items = [...list.children]; let i = items.findIndex(li => li.getAttribute('aria-selected') === 'true');
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault(); items[i]?.setAttribute('aria-selected', 'false');
        i = Math.max(0, Math.min(items.length - 1, i + (e.key === 'ArrowDown' ? 1 : -1)));
        items[i]?.setAttribute('aria-selected', 'true'); items[i]?.scrollIntoView({ block: 'nearest' });
      } else if (e.key === 'Enter') { e.preventDefault(); choose(items[Math.max(0, i)]); }
    });
    list.addEventListener('click', e => choose(e.target.closest('li')));
    palette.addEventListener('click', e => { if (e.target === palette) palette.close(); });
    palette.addEventListener('close', () => palette.remove());
    draw(); palette.showModal(); input.focus();
  }
  document.addEventListener('click', event => { if (event.target.closest?.('[data-palette-open]')) openPalette(); });

  /* Shortcut sheet */
  function openShortcuts() {
    if (document.querySelector('.ui-shortcuts')) return;
    const d = document.createElement('dialog'); d.className = 'ui-shortcuts'; d.setAttribute('aria-label', 'Keyboard shortcuts');
    const rows = [['⌘K', 'Jump to a page or search accounts'], ['/', 'Focus the page search'], ['g then a letter', 'Go to: q queue · a accounts · d date of birth · m media · b beta · h holds · l law enforcement · s security · e elevated access · o overview'],
      ['⌘ Enter', 'Submit the form you are in'], ['Esc', 'Close'], ['?', 'Show this list']];
    d.innerHTML = `<header><h2>Keyboard shortcuts</h2><button type="button" class="ui-icon-button" aria-label="Close" data-close>×</button></header><dl>${rows.map(([k, t]) => `<dt><kbd>${k}</kbd></dt><dd>${t}</dd>`).join('')}</dl>`;
    document.body.append(d);
    d.addEventListener('click', e => { if (e.target === d || e.target.closest('[data-close]')) d.close(); });
    d.addEventListener('close', () => d.remove());
    d.showModal();
  }

  /* Keys */
  let pendingG = false, gTimer;
  document.addEventListener('keydown', event => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); openPalette(); return; }
    if ((event.metaKey || event.ctrlKey) && event.key === 'Enter') {
      const form = document.activeElement?.closest?.('form') ?? document.querySelector('dialog[open] form[method=post], details[open] form[method=post]');
      if (form && !form.closest('.ui-palette')) { event.preventDefault(); form.requestSubmit(form.querySelector('button[type=submit],button:not([type])') ?? undefined); }
      return;
    }
    if (event.metaKey || event.ctrlKey || event.altKey || typing(event.target) || document.querySelector('dialog[open]')) return;
    if (event.key === '/') {
      const field = document.querySelector('main input[type=search], main input[name=q], main input[name=reference], main input[name=value]');
      if (field) { event.preventDefault(); field.focus(); field.select?.(); }
      return;
    }
    if (event.key === '?') { event.preventDefault(); openShortcuts(); return; }
    if (pendingG) { pendingG = false; clearTimeout(gTimer); const href = GO[event.key.toLowerCase()]; if (href) { event.preventDefault(); navigate(href); } return; }
    if (event.key === 'g') { pendingG = true; gTimer = setTimeout(() => (pendingG = false), 1200); }
  });
})();

// Accounts search: show which kind of search the text will run (mobile, email or name) while typing.
document.addEventListener('input', event => {
  const input = event.target.closest?.('[data-kind-input]'); if (!input) return;
  const v = input.value.trim();
  const kind = !v ? '' : v.includes('@') ? 'email' : /^[\d\s+().-]+$/.test(v) && /\d/.test(v) ? 'phone' : 'name';
  const hints = { phone: 'Spaces removed · + optional', email: 'Lower-cased before search', name: 'First or last name · use % as a wildcard', '': 'Type to detect the search type' };
  document.querySelectorAll('[data-kind-chips] [data-kind]').forEach(c => c.toggleAttribute('aria-current', c.dataset.kind === kind));
  const hint = document.querySelector('[data-kind-hint]'); if (hint) hint.textContent = hints[kind];
});

// CSV export of a visible table (data-export-csv="<table id>"); exports only what this staff member already sees.
document.addEventListener('click', event => {
  const button = event.target.closest?.('[data-export-csv]'); if (!button) return;
  const table = document.getElementById(button.dataset.exportCsv); if (!table) return;
  const cell = text => `"${text.replace(/\s+/g, ' ').trim().replace(/"/g, '""')}"`;
  const csv = [...table.rows].map(row => [...row.cells].map(c => cell(c.innerText)).join(',')).join('\r\n');
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
  link.download = button.dataset.exportName || 'export.csv';
  link.click(); setTimeout(() => URL.revokeObjectURL(link.href), 1000);
});
