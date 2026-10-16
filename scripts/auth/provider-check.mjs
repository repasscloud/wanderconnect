// Real local-provider negative checks. No fixture credentials or user tokens are used.
import assert from 'node:assert/strict';
import { callback, client, issuer } from './security.mjs';
const auth = issuer + '/protocol/openid-connect/auth';
async function check(parameters, invalidRedirect = false) {
  const query = new URLSearchParams({ client_id: client, redirect_uri: callback, response_type: 'code', scope: 'openid', state: 'negative-probe', ...parameters });
  const response = await fetch(auth + '?' + query, { redirect: 'manual', signal: AbortSignal.timeout(10000) });
  const location = response.headers.get('location');
  if (invalidRedirect) {
    assert.equal(response.status, 400);
    assert.equal(location, null);
  } else {
    assert.equal(response.status, 302);
    const target = new URL(location);
    assert.equal(target.origin + target.pathname, callback);
    assert.equal(target.searchParams.get('error'), 'invalid_request');
    assert.equal(target.searchParams.has('code'), false);
  }
}
await check({ redirect_uri: 'https://example.invalid/callback', code_challenge: 'a'.repeat(43), code_challenge_method: 'S256' }, true);
await check({});
await check({ code_challenge: 'a'.repeat(43), code_challenge_method: 'plain' });
const grant = await fetch(issuer + '/protocol/openid-connect/token', { method: 'POST', redirect: 'error', signal: AbortSignal.timeout(10000), body: new URLSearchParams({ grant_type: 'password', client_id: client, username: 'nonexistent', password: 'not-a-credential' }) });
assert.equal(grant.status, 400);
assert.equal((await grant.json()).error, 'unauthorized_client');
console.log('PASS: provider rejects non-allowlisted redirect, missing PKCE, plain PKCE and application password grant. No browser authentication inferred.');
