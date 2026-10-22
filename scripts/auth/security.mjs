// Investigation-only verification. Never imported by application hosts.
import { createPublicKey, randomBytes, verify } from 'node:crypto';
export const issuer = 'http://localhost:8180/realms/swoovr-dev';
export const client = 'swoovr-investigation';
export const origin = 'http://127.0.0.1:8280';
export const callback = origin + '/callback';
export const random = () => randomBytes(32).toString('base64url');
const requireValue = (condition) => { if (!condition) throw new Error('Evidence rejected'); };
export function validateIdToken(token, jwks, { issuer, client, nonce, refresh = false, now = Math.floor(Date.now() / 1000) }) {
  requireValue(typeof token === 'string' && token.length < 32768);
  const parts = token.split('.');
  requireValue(parts.length === 3 && parts.every(p => /^[A-Za-z0-9_-]+$/.test(p)));
  const header = JSON.parse(Buffer.from(parts[0], 'base64url'));
  requireValue(header.alg === 'RS256' && typeof header.kid === 'string' && !header.crit);
  const keys = jwks.keys.filter(k => k.kid === header.kid && k.kty === 'RSA' && k.use === 'sig' && k.alg === 'RS256');
  requireValue(keys.length === 1);
  const key = createPublicKey({ key: keys[0], format: 'jwk' });
  requireValue(key.asymmetricKeyDetails.modulusLength >= 2048);
  requireValue(verify('RSA-SHA256', Buffer.from(parts.slice(0, 2).join('.')), key, Buffer.from(parts[2], 'base64url')));
  const claims = JSON.parse(Buffer.from(parts[1], 'base64url'));
  requireValue(claims.iss === issuer);
  const audiences = Array.isArray(claims.aud) ? claims.aud : [claims.aud];
  requireValue(audiences.includes(client) && (claims.azp === undefined ? audiences.length === 1 : claims.azp === client));
  requireValue(typeof nonce === 'string' && nonce.length > 0 && ((refresh && claims.nonce === undefined) || claims.nonce === nonce));
  requireValue(Number.isInteger(claims.exp) && claims.exp > now);
  requireValue(Number.isInteger(claims.iat) && claims.iat <= now + 5 && claims.iat < claims.exp);
  requireValue(claims.nbf === undefined || (Number.isInteger(claims.nbf) && claims.nbf <= now));
  requireValue(Number.isInteger(claims.auth_time) && claims.auth_time >= 0 && claims.auth_time <= now + 5 && claims.auth_time <= claims.iat);
  requireValue(typeof claims.sub === 'string' && claims.sub.length > 0 && typeof claims.sid === 'string' && claims.sid.length > 0);
  return claims;
}
export class Transactions {
  #pending = new Map();
  start(browser, now = Math.floor(Date.now() / 1000)) {
    for (const [state, tx] of this.#pending) if (tx.expires <= now) this.#pending.delete(state);
    requireValue(this.#pending.size < 100);
    const tx = { state: random(), nonce: random(), verifier: random(), browser, created: now, expires: now + 300 };
    this.#pending.set(tx.state, tx);
    return tx;
  }
  consume(state, browser, now = Math.floor(Date.now() / 1000)) {
    const tx = this.#pending.get(state);
    requireValue(tx && tx.browser === browser && tx.expires > now);
    this.#pending.delete(state);
    return tx;
  }
}
export async function request(url, options = {}) {
  // All callers use fixed local URLs; never use a token's jku/x5u or arbitrary discovery URL.
  const response = await fetch(url, { ...options, redirect: 'error', signal: AbortSignal.timeout(10000) });
  const chunks = [];
  let length = 0;
  if (response.body) for await (const chunk of response.body) {
    length += chunk.length;
    requireValue(length < 2_000_000);
    chunks.push(chunk);
  }
  const content = Buffer.concat(chunks).toString('utf8');
  if (!response.ok) {
    let oauthError;
    try { const value = JSON.parse(content).error; if (['invalid_grant', 'invalid_request', 'unauthorized_client'].includes(value)) oauthError = value; } catch { /* No body details retained. */ }
    const error = new Error(`Local provider request rejected (${response.status})`);
    error.status = response.status; error.oauthError = oauthError;
    throw error;
  }
  return content ? JSON.parse(content) : null;
}
