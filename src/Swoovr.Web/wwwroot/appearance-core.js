// Guest preference only. This module never reads account identity or changes access.
(function (root) {
  'use strict';
  const palettes = typeof module === 'object' && module.exports ? require('./appearance-palettes.js') : root.SwoovrPalettes;
  const ids = new Set(palettes.map(p => p.id));
  const modes = new Set(['system', 'light', 'dark']);
  const defaults = Object.freeze({ scheme: 'standard', mode: 'system' });
  const key = 'swoovr.appearance.guest.v1';
  function normalize(value) {
    return value && ids.has(value.scheme) && modes.has(value.mode)
      ? { scheme: value.scheme, mode: value.mode } : { ...defaults };
  }
  function resolve(value, systemDark) {
    const preference = normalize(value);
    return { scheme: preference.scheme, mode: preference.mode === 'system' ? systemDark ? 'dark' : 'light' : preference.mode };
  }
  function load(storage) {
    try {
      const raw = storage.getItem(key);
      let value;
      try { value = JSON.parse(raw); } catch { /* Invalid data falls back without a write. */ }
      return { preference: normalize(value?.version === 2 ? value : migrateV1(value)), available: true };
    } catch { return { preference: { ...defaults }, available: false }; }
  }
  // Schema v1 held the six retired schemes; only Electric and Coral have successors.
  function migrateV1(value) {
    if (value?.version !== 1) return null;
    const scheme = { electric: 'violet', coral: 'coral' }[value.scheme] ?? defaults.scheme;
    return { scheme, mode: value.mode };
  }
  function save(storage, value) {
    try { storage.setItem(key, JSON.stringify({ version: 2, ...normalize(value) })); return true; }
    catch { return false; }
  }
  const api = { defaults, key, normalize, resolve, load, save };
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.SwoovrAppearance = api;
})(globalThis);
