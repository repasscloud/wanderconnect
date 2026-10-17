import { test } from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync, sign } from 'node:crypto';
import { validateIdToken, Transactions } from '../../scripts/auth/security.mjs';
const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const key = { ...publicKey.export({ format: 'jwk' }), kid: 'test', alg: 'RS256', use: 'sig' };
const now = 1000;
const base = { iss: 'http://localhost:8180/realms/datingapp-dev', aud: 'datingapp-investigation', azp: 'datingapp-investigation', sub: 'fictional', sid: 'session', nonce: 'nonce', iat: now, exp: now + 300, auth_time: now };
function jwt(claims = base, header = { alg: 'RS256', kid: 'test' }, signer = privateKey) {
  const input = [header, claims].map(x => Buffer.from(JSON.stringify(x)).toString('base64url')).join('.');
  return input + '.' + sign('RSA-SHA256', Buffer.from(input), signer).toString('base64url');
}
function validate(token, overrides = {}) { return validateIdToken(token, { keys: [key] }, { issuer: base.iss, client: base.aud, nonce: 'nonce', now, ...overrides }); }
test('valid signed ID token', () => assert.equal(validate(jwt()).sub, base.sub));
for (const [label, patch] of Object.entries({ issuer: { iss: 'elsewhere' }, audience: { aud: 'other' }, azp: { azp: 'other' }, nonce: { nonce: 'other' }, expired: { exp: now }, future: { iat: now + 30 }, futureAuth: { auth_time: now + 30 }, missingSubject: { sub: '' }, missingSession: { sid: '' }, timeType: { exp: '1300' } })) {
  test('reject ' + label, () => assert.throws(() => validate(jwt({ ...base, ...patch }))));
}
test('reject invalid signature', () => { const other = generateKeyPairSync('rsa', { modulusLength: 2048 }); assert.throws(() => validate(jwt(base, undefined, other.privateKey))); });
test('reject algorithm confusion and unknown key', () => { assert.throws(() => validate(jwt(base, { alg: 'HS256', kid: 'test' }))); assert.throws(() => validate(jwt(base, { alg: 'RS256', kid: 'missing' }))); });
test('reject duplicate signing key IDs', () => assert.throws(() => validateIdToken(jwt(), { keys: [key, key] }, { issuer: base.iss, client: base.aud, nonce: 'nonce', now })));
test('state binds browser and expires; valid transaction consumed once', () => {
  const store = new Transactions();
  const tx = store.start('browser', now);
  assert.throws(() => store.consume(tx.state, 'other', now));
  assert.equal(store.consume(tx.state, 'browser', now).nonce, tx.nonce);
  assert.throws(() => store.consume(tx.state, 'browser', now));
  const old = store.start('browser', now);
  assert.throws(() => store.consume(old.state, 'browser', now + 301));
});
test('refresh accepts omitted nonce only in refresh mode, rejects conflicting nonce', () => {
  const claims = { ...base }; delete claims.nonce;
  assert.throws(() => validate(jwt(claims)));
  assert.equal(validate(jwt(claims), { refresh: true }).sub, base.sub);
  assert.throws(() => validate(jwt({ ...base, nonce: 'other' }), { refresh: true }));
});
test('provider failure preserves only safe status and OAuth error code', async () => {
  const { createServer } = await import('node:http');
  const { request } = await import('../../scripts/auth/security.mjs');
  const server = createServer((req, res) => { res.writeHead(400); res.end(JSON.stringify({ error: 'invalid_grant', error_description: 'NEVER expose provider details' })); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try {
    await assert.rejects(request('http://127.0.0.1:' + server.address().port), error => error.status === 400 && error.oauthError === 'invalid_grant' && !error.message.includes('NEVER'));
  } finally { await new Promise(resolve => server.close(resolve)); }
});
test('stored fixture ID never silently rebinds after user or realm removal', async () => {
  const { validateFixture } = await import('../../scripts/auth/provision.mjs');
  assert.throws(() => validateFixture({ userId: 'old' }, []));
  assert.throws(() => validateFixture({ userId: 'old' }, [{ id: 'other' }]));
  assert.doesNotThrow(() => validateFixture({}, []));
  assert.doesNotThrow(() => validateFixture({ userId: 'same' }, [{ id: 'same' }]));
});
test('published signing-key rollover accepts new key and rejects removed old key', () => {
  const next = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const nextKey = { ...next.publicKey.export({ format: 'jwk' }), kid: 'next', alg: 'RS256', use: 'sig' };
  const options = { issuer: base.iss, client: base.aud, nonce: 'nonce', now };
  const nextToken = jwt(base, { alg: 'RS256', kid: 'next' }, next.privateKey);
  assert.equal(validateIdToken(nextToken, { keys: [key, nextKey] }, options).sub, base.sub);
  assert.equal(validateIdToken(jwt(), { keys: [key, nextKey] }, options).sub, base.sub);
  assert.throws(() => validateIdToken(jwt(), { keys: [nextKey] }, options));
  assert.throws(() => validateIdToken(nextToken, { keys: [key] }, options));
});
test('negative authentication epoch is rejected', () => assert.throws(() => validate(jwt({ ...base, auth_time: -1 }))));
