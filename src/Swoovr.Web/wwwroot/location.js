// One device location reading for an explicit check (D149 confirm-arrival, D150 home check, D152: the browser's location
// permission on the web). Asked for only when the member taps the button; never watched or tracked. The API decides what
// the reading means and keeps only the outcome. Resolves to null when the member declines or the browser can't tell.
window.swoovrLocation = {
  read: () => new Promise(resolve => {
    if (!navigator.geolocation) { resolve(null); return; }
    navigator.geolocation.getCurrentPosition(
      p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracyMetres: p.coords.accuracy, takenAt: new Date(p.timestamp).toISOString() }),
      () => resolve(null),
      { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 });
  }),
};
