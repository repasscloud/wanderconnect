import { test } from 'node:test';
import assert from 'node:assert/strict';
import { request } from 'node:http';
import { createHarness } from '../../scripts/auth/harness.mjs';
test('local HTTP boundary rejects host, CSRF, unbound/duplicate callback and replay without provider calls', async () => {
  let providerCalls = 0;
  const server = createHarness(async () => { providerCalls++; throw new Error('No provider allowed'); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    const url = 'http://127.0.0.1:' + server.address().port;
    const send = (path, options = {}) => new Promise((resolve, reject) => {
      const req = request(url + path, { method: options.method ?? 'GET', headers: { Host: '127.0.0.1:8280', ...options.headers } }, res => {
        let body = ''; res.on('data', chunk => body += chunk); res.on('end', () => resolve({ status: res.statusCode, headers: { get: name => Array.isArray(res.headers[name]) ? res.headers[name][0] : res.headers[name] }, text: async () => body }));
      });
      req.on('error', reject); req.end(options.body);
    });
    assert.equal((await fetch(url)).status, 400);
    assert.equal((await send('/callback?code=secret&state=bad')).status, 400);
    const home = await send('/');
    assert.equal(home.headers.get('cache-control'), 'no-store');
    assert.equal(home.headers.get('referrer-policy'), 'same-origin');
    const cookie = home.headers.get('set-cookie').split(';')[0];
    const html = await home.text();
    const csrf = /name="csrf" value="([^"]+)"/.exec(html)[1];
    const headers = { Cookie: cookie, Origin: 'http://127.0.0.1:8280', 'Content-Type': 'application/x-www-form-urlencoded' };
    assert.equal((await send('/begin', { method: 'POST', headers, body: 'csrf=wrong&mode=' })).status, 403);
    assert.equal((await send('/begin', { method: 'POST', headers: { ...headers, Origin: 'http://evil.invalid' }, body: `csrf=${csrf}&mode=` })).status, 403);
    const begin = await send('/begin', { method: 'POST', headers, body: `csrf=${csrf}&mode=` });
    assert.equal(begin.status, 303);
    const auth = new URL(begin.headers.get('location'));
    assert.equal(auth.searchParams.get('code_challenge_method'), 'S256');
    assert.equal(auth.searchParams.get('redirect_uri'), 'http://127.0.0.1:8280/callback');
    const state = auth.searchParams.get('state');
    const callback = `/callback?state=${state}&iss=${encodeURIComponent(auth.origin + '/realms/swoovr-dev')}&code=secret`;
    assert.equal((await send(callback + '&state=duplicate', { headers: { Cookie: cookie } })).status, 400);
    assert.equal(providerCalls, 0);
    assert.equal((await send(callback, { headers: { Cookie: cookie } })).status, 400);
    assert.equal(providerCalls, 1);
    assert.equal((await send(callback, { headers: { Cookie: cookie } })).status, 400);
    assert.equal(providerCalls, 1);
  } finally { await new Promise(resolve => server.close(resolve)); }
});
test('signed callback, refresh continuity and actual versus unavailable logout evidence', async () => {
  const { generateKeyPairSync, sign } = await import('node:crypto');
  const { issuer, client } = await import('../../scripts/auth/security.mjs');
  const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const key = { ...publicKey.export({ format: 'jwk' }), kid: 'integration', alg: 'RS256', use: 'sig' };
  let nonce, patch = {}, revoked = false, outage = false;
  const authTime = Math.floor(Date.now() / 1000) - 10;
  function token(refresh) {
    const claims = { iss: issuer, aud: client, azp: client, sub: 'fictional', sid: 'test-session', exp: Math.floor(Date.now() / 1000) + 300, iat: Math.floor(Date.now() / 1000), auth_time: authTime, amr: ['swoovr:password'], ...(refresh ? {} : { nonce }), ...patch };
    const input = [{ alg: 'RS256', kid: key.kid }, claims].map(x => Buffer.from(JSON.stringify(x)).toString('base64url')).join('.');
    return input + '.' + sign('RSA-SHA256', Buffer.from(input), privateKey).toString('base64url');
  }
  const server = createHarness(async (url, options) => {
    if (url.endsWith('/certs')) return { keys: [key] };
    if (url.endsWith('/logout')) { revoked = true; return null; }
    if (options.body.get('grant_type') === 'authorization_code') { revoked = false; return { id_token: token(false), refresh_token: 'PRIVATE_TEST_REFRESH' }; }
    if (revoked) {
      if (outage) throw new Error('network unavailable');
      throw Object.assign(new Error('rejected'), { status: 400, oauthError: 'invalid_grant' });
    }
    return { id_token: token(true), refresh_token: 'PRIVATE_TEST_REFRESH' };
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    const send = (path, options = {}) => new Promise((resolve, reject) => {
      const req = request('http://127.0.0.1:' + server.address().port + path, { method: options.method ?? 'GET', headers: { Host: '127.0.0.1:8280', ...options.headers } }, res => {
        let body = ''; res.on('data', chunk => body += chunk); res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body }));
      }); req.on('error', reject); req.end(options.body);
    });
    const home = await send('/');
    const cookie = home.headers['set-cookie'][0].split(';')[0];
    const csrf = /name="csrf" value="([^"]+)"/.exec(home.body)[1];
    const headers = { Cookie: cookie, Origin: 'http://127.0.0.1:8280', 'Content-Type': 'application/x-www-form-urlencoded' };
    const post = path => send(path, { method: 'POST', headers, body: `csrf=${csrf}&mode=` });
    async function login() {
      const begin = await post('/begin'); const auth = new URL(begin.headers.location); nonce = auth.searchParams.get('nonce');
      const result = await send('/callback?' + new URLSearchParams({ state: auth.searchParams.get('state'), iss: issuer, code: 'private-test-code' }), { headers: { Cookie: cookie } });
      assert.equal(result.status, 303);
      assert.equal(result.headers.location, '/');
    }
    await login();
    assert.equal((await post('/refresh')).status, 303);
    for (const conflicting of [{ sub: 'other' }, { sid: 'other' }, { auth_time: authTime - 1 }]) {
      patch = conflicting; assert.equal((await post('/refresh')).status, 400);
    }
    patch = {};
    assert.equal((await post('/logout')).status, 303);
    let report = (await send('/', { headers: { Cookie: cookie } })).body;
    assert.match(report, /<td>logout<\/td>(?:<td>n\/a<\/td>){6}<td>true<\/td>/);
    assert.equal(report.includes('PRIVATE_TEST_REFRESH'), false);
    assert.equal(report.includes('private-test-code'), false);
    await login(); outage = true;
    assert.equal((await post('/logout')).status, 303);
    report = (await send('/', { headers: { Cookie: cookie } })).body;
    assert.match(report, /<td>logout<\/td>(?:<td>n\/a<\/td>){6}<td>false<\/td>/);
  } finally { await new Promise(resolve => server.close(resolve)); }
});
