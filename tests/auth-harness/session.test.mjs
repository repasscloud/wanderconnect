import { test } from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync, sign } from 'node:crypto';
import { request } from 'node:http';
import { createHarness } from '../../scripts/auth/harness.mjs';
import { issuer, client } from '../../scripts/auth/security.mjs';
const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const key = { ...publicKey.export({ format: 'jwk' }), kid: 'sessions', alg: 'RS256', use: 'sig' };
async function fixture(run) {
  let claims = {}, nonce, gate;
  let enteredResolve;
  let entered = new Promise(resolve => { enteredResolve = resolve; });
  let calls = 0;
  const server = createHarness(async (url, options) => {
    if (url.endsWith('/certs')) return { keys: [key] };
    calls++;
    if (gate) { enteredResolve(); await gate; }
    if (url.endsWith('/logout')) return null;
    const now = Math.floor(Date.now() / 1000);
    const input = [{ alg: 'RS256', kid: key.kid }, { iss: issuer, aud: client, sub: 'adult-fixture', sid: 'session', nonce, auth_time: now, iat: now, exp: now + 300, amr: ['datingapp:password'], ...claims }].map(x => Buffer.from(JSON.stringify(x)).toString('base64url')).join('.');
    return { id_token: input + '.' + sign('RSA-SHA256', Buffer.from(input), privateKey).toString('base64url'), refresh_token: 'PRIVATE_SYNTHETIC_REFRESH' };
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let cookie;
  const send = (path, options = {}) => new Promise((resolve, reject) => {
    const req = request('http://127.0.0.1:' + server.address().port + path, { method: options.method ?? 'GET', headers: { Host: '127.0.0.1:8280', Cookie: cookie ?? '', ...options.headers } }, res => {
      let body = ''; res.on('data', chunk => body += chunk); res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body }));
    }); req.setTimeout(1000, () => req.destroy(new Error('HTTP operation did not finish'))); req.on('error', reject); req.end(options.body);
  });
  try {
    const home = await send('/'); cookie = home.headers['set-cookie'][0].split(';')[0];
    const csrf = /name="csrf" value="([^"]+)"/.exec(home.body)[1];
    const post = (path, mode = '') => send(path, { method: 'POST', headers: { Origin: 'http://127.0.0.1:8280', 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ csrf, mode }).toString() });
    async function begin(mode = '') {
      const result = await post('/begin', mode);
      assert.equal(result.status, 303);
      const auth = new URL(result.headers.location); nonce = auth.searchParams.get('nonce');
      return '/callback?' + new URLSearchParams({ state: auth.searchParams.get('state'), iss: issuer, code: 'synthetic-code' });
    }
    await run({ send, post, begin, patch: value => { claims = value; }, calls: () => calls, pause: () => { let release; gate = new Promise(resolve => { release = resolve; }); return { entered, release: () => { gate = undefined; release(); } }; } });
  } finally { await new Promise(resolve => server.close(resolve)); }
}
test('forced reauthentication rejects an old authentication time', () => fixture(async f => {
  const callback = await f.begin('reauth');
  f.patch({ auth_time: Math.floor(Date.now() / 1000) - 60 });
  assert.equal((await f.send(callback)).status, 400);
  assert.equal((await f.post('/refresh')).status, 400);
}));
for (const patch of [{ sub: 'another-adult' }, { sid: 'another-session' }, { auth_time: 1 }]) {
  test('silent SSO rejects changed ' + Object.keys(patch)[0], () => fixture(async f => {
    assert.equal((await f.send(await f.begin())).status, 303);
    const callback = await f.begin('silent'); f.patch(patch);
    assert.equal((await f.send(callback)).status, 400);
  }));
}
test('reauthentication rejects a changed subject', () => fixture(async f => {
  assert.equal((await f.send(await f.begin())).status, 303);
  const callback = await f.begin('reauth'); f.patch({ sub: 'another-adult' });
  assert.equal((await f.send(callback)).status, 400);
}));
test('a newer begin supersedes earlier browser callbacks before token exchange', () => fixture(async f => {
  const old = await f.begin(); const current = await f.begin();
  assert.equal((await f.send(old)).status, 400);
  assert.equal(f.calls(), 0);
  assert.equal((await f.send(current)).status, 303);
}));
test('in-flight refresh rejects overlapping logout, refresh and begin', () => fixture(async f => {
  assert.equal((await f.send(await f.begin())).status, 303);
  const paused = f.pause(); const refresh = f.post('/refresh');
  await paused.entered;
  try {
    for (const path of ['/logout', '/refresh', '/begin']) assert.equal((await f.post(path)).status, 409);
  } finally { paused.release(); }
  assert.equal((await refresh).status, 303);
  assert.equal(f.calls(), 2);
}));
test('fresh reauthentication and unchanged silent SSO remain usable', () => fixture(async f => {
  const auth_time = Math.floor(Date.now() / 1000); f.patch({ auth_time });
  assert.equal((await f.send(await f.begin())).status, 303);
  assert.equal((await f.send(await f.begin('silent'))).status, 303);
  f.patch({ auth_time, sid: 'fresh-session' });
  assert.equal((await f.send(await f.begin('reauth'))).status, 303);
  assert.equal((await f.post('/refresh')).status, 303);
}));
test('logout invalidates a pending browser callback', () => fixture(async f => {
  assert.equal((await f.send(await f.begin())).status, 303);
  const pending = await f.begin();
  assert.equal((await f.post('/logout')).status, 303);
  const count = f.calls();
  assert.equal((await f.send(pending)).status, 400);
  assert.equal(f.calls(), count);
}));
test('logout cancels an initial pending login even without stored refresh', () => fixture(async f => {
  const pending = await f.begin();
  assert.equal((await f.post('/logout')).status, 303);
  assert.equal((await f.send(pending)).status, 400);
  assert.equal(f.calls(), 0);
}));
