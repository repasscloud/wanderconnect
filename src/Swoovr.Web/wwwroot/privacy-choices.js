// Issue 184 (D318): "Your Privacy Choices". Same storage key and values as the swoovr.app launch site, so a choice made
// there carries over: 'granted' or 'denied' in localStorage 'swoovr_privacy_v1' (per browser; not tied to an account).
// Google Analytics loads only after an explicit allow, never under Global Privacy Control, with advertising signals off and
// clean page addresses (no query, fragment or ids). Choices older than 13 months are asked again.
(function () {
  'use strict';
  const key = 'swoovr_privacy_v1', atKey = 'swoovr_privacy_v1_at', maxAgeMs = 395 * 24 * 3600 * 1000;
  const gpc = navigator.globalPrivacyControl === true;
  let loaded = false, lastPath = null, opener = null;
  const read = k => { try { return localStorage.getItem(k); } catch { return null; } };
  const write = (k, v) => { try { localStorage.setItem(k, v); } catch { /* storage blocked: the choice lasts for this page */ } };
  const dialog = () => document.querySelector('[data-privacy-dialog]');
  const gaId = () => dialog()?.dataset.ga || '';
  function choice() {
    const value = read(key), at = Number(read(atKey));
    if (value !== 'granted' && value !== 'denied') return null;
    if (!at) { write(atKey, String(Date.now())); return value; }   // carried over from swoovr.app
    return Date.now() - at > maxAgeMs ? null : value;
  }
  function save(value) { write(key, value); write(atKey, String(Date.now())); }
  // Page addresses only: ids (GUIDs, long numbers) become ":id" so no member or conversation can be identified.
  const cleanPath = () => location.pathname.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, ':id').replace(/\/\d{4,}(?=\/|$)/g, '/:id');
  function pageView() {
    if (!loaded || cleanPath() === lastPath) return;
    lastPath = cleanPath();
    window.gtag('event', 'page_view', { page_location: location.origin + lastPath, page_title: document.title.replace(/^.*— /, ''), page_referrer: '' });
  }
  function load() {
    const id = gaId();
    if (loaded || !id || gpc) return;
    loaded = true;
    window['ga-disable-' + id] = false;
    window.dataLayer = window.dataLayer || [];
    window.gtag = function () { window.dataLayer.push(arguments); };
    window.gtag('consent', 'default', { analytics_storage: 'granted', ad_storage: 'denied', ad_user_data: 'denied', ad_personalization: 'denied' });
    window.gtag('js', new Date());
    window.gtag('config', id, { send_page_view: false, page_location: location.origin + cleanPath(), page_referrer: '', allow_google_signals: false, allow_ad_personalization_signals: false });
    pageView();
    const script = document.createElement('script'); script.async = true;
    script.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(id);
    document.head.appendChild(script);
  }
  function clearAnalyticsCookies() {
    const domains = ['', location.hostname, '.' + location.hostname, '.' + location.hostname.split('.').slice(-2).join('.')];
    for (const cookie of document.cookie.split(';')) {
      const name = cookie.split('=')[0].trim();
      if (/^_ga(?:_|$)|^_gid$|^_gat/.test(name)) for (const domain of domains)
        document.cookie = name + '=; Max-Age=0; Path=/; SameSite=Lax' + (domain ? '; Domain=' + domain : '');
    }
  }
  function sync() {
    const d = dialog(); if (!d) return;
    const box = d.querySelector('[data-privacy-analytics]');
    if (box) { box.checked = !gpc && choice() === 'granted'; box.disabled = gpc; }
    d.querySelector('[data-privacy-gpc]')?.toggleAttribute('hidden', !gpc);
    d.querySelector('[data-privacy-accept]')?.toggleAttribute('disabled', gpc);
    // The floating control stays clear of a conversation's message box; the footer link remains.
    document.querySelectorAll('[data-privacy-pill]').forEach(p => p.toggleAttribute('hidden', /^\/chats\/./.test(location.pathname)));
  }
  function open(modal) {
    const d = dialog(); if (!d) return;
    sync();
    if (d.open) return;
    opener = document.activeElement;
    modal ? d.showModal() : d.show();
    (d.querySelector('[data-privacy-analytics]:not(:disabled)') || d.querySelector('[data-privacy-reject], [data-privacy-close]'))?.focus({ preventScroll: true });
  }
  function close(message) {
    const d = dialog(); if (!d) return;
    if (message) { const status = d.querySelector('[data-privacy-status]'); if (status) status.textContent = message; }
    if (d.open) d.close();
    if (opener instanceof HTMLElement && opener.isConnected) opener.focus({ preventScroll: true });
    opener = null;
  }
  function apply(value) {
    const was = choice();
    save(value);
    if (value === 'granted') load();
    else {
      if (gaId()) window['ga-disable-' + gaId()] = true;
      clearAnalyticsCookies();
      if (loaded || was === 'granted') { location.reload(); return; } // unload tags that already ran
    }
    close(value === 'granted' ? 'Analytics allowed.' : 'Optional analytics is off.');
  }
  document.addEventListener('click', event => {
    const t = event.target instanceof Element ? event.target : null;
    if (!t) return;
    if (t.closest('[data-privacy-open]')) { event.preventDefault(); open(true); return; }
    if (t.closest('[data-privacy-accept]')) { apply('granted'); return; }
    if (t.closest('[data-privacy-reject]')) { apply('denied'); return; }
    if (t.closest('[data-privacy-save]')) { apply(dialog()?.querySelector('[data-privacy-analytics]')?.checked && !gpc ? 'granted' : 'denied'); return; }
    if (t.closest('[data-privacy-close]')) { event.preventDefault(); close(); }
  });
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && dialog()?.open) { event.preventDefault(); close(); } });
  window.addEventListener('storage', event => { if (event.key === key) location.reload(); });
  function start() {
    sync();
    const current = choice();
    if (gpc) { if (read(key) !== 'denied') save('denied'); clearAnalyticsCookies(); return; }
    if (current === 'granted') load();
    else if (current === null && gaId()) open(false);   // ask once, without blocking the page, only where analytics exists
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
  const hook = () => window.Blazor?.addEventListener?.('enhancedload', () => { sync(); pageView(); });
  if (window.Blazor) hook(); else window.addEventListener('load', hook, { once: true });
})();
