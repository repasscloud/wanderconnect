// Local investigation UI, intentionally separate from the seven application projects.
import { createServer } from 'node:http';
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { callback, client, issuer, origin, random, request, Transactions, validateIdToken } from './security.mjs';
const esc = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
export function createHarness(provider = request) {
  const transactions = new Transactions();
  const sessions = new Map();
  const headers = {
    'Cache-Control': 'no-store', 'Referrer-Policy': 'same-origin', 'X-Content-Type-Options': 'nosniff',
    'Content-Security-Policy': "default-src 'none'; form-action 'self' http://localhost:8180; frame-ancestors 'none'; base-uri 'none'"
  };
  const server = createServer(async (req, res) => {
    const send = (status, body = '', extra = {}) => { res.writeHead(status, { ...headers, 'Content-Type': 'text/html; charset=utf-8', ...extra }); res.end(body); };
    let lockedSession;
    try {
      if (req.headers.host !== '127.0.0.1:8280') return send(400, 'Invalid host');
      const url = new URL(req.url, origin);
      let id = /(?:^|;\s*)swoovr_investigation=([A-Za-z0-9_-]{43})(?:;|$)/.exec(req.headers.cookie ?? '')?.[1];
      const now = Math.floor(Date.now() / 1000);
      for (const [key, value] of sessions) if (value.expires <= now) sessions.delete(key);
      let session = sessions.get(id);
      if (url.pathname === '/callback' && req.method === 'GET') {
        if (!session) return send(400, 'Callback rejected');
        if (session.busy) return send(409, 'Session operation in progress');
        const allowed = new Set(['state', 'code', 'iss', 'session_state', 'error', 'error_description']);
        if ([...url.searchParams.keys()].some(k => !allowed.has(k) || url.searchParams.getAll(k).length !== 1)) return send(400, 'Callback rejected');
        const tx = transactions.consume(url.searchParams.get('state'), id);
        if (tx.generation !== session.generation) throw new Error('Superseded transaction');
        session.busy = true; lockedSession = session;
        if (url.searchParams.get('error') || url.searchParams.get('iss') !== issuer || !url.searchParams.get('code')) throw new Error('Callback rejected');
        const tokens = await provider(issuer + '/protocol/openid-connect/token', { method: 'POST', body: new URLSearchParams({ grant_type: 'authorization_code', client_id: client, redirect_uri: callback, code: url.searchParams.get('code'), code_verifier: tx.verifier }) });
        await observe(session, tokens, tx.nonce, tx.mode, tx.created);
        return send(303, '', { Location: '/' });
      }
      if (!session) {
        if (req.method !== 'GET' || url.pathname !== '/') return send(400, 'Session required');
        if (sessions.size >= 100) return send(503, 'Local session limit reached; restart harness');
        id = random();
        session = { csrf: random(), expires: now + 1800, observations: [] };
        sessions.set(id, session);
        // HTTP is allowed only on this loopback investigation host. Production needs Secure.
        res.setHeader('Set-Cookie', `swoovr_investigation=${id}; Path=/; HttpOnly; SameSite=Lax; Max-Age=1800`);
      }
      if (req.method === 'GET' && url.pathname === '/' && !url.search) {
        const form = (path, label, mode = '') => `<form method="post" action="${path}"><input type="hidden" name="csrf" value="${session.csrf}"><input type="hidden" name="mode" value="${mode}"><button>${label}</button></form>`;
        return send(200, `<h1>Local authentication investigation</h1><p>Fictional adult fixture only. Email status is administrator-asserted simulation. No application or privileged access is enabled.</p>${form('/begin', 'Browser login / SSO')}${form('/begin', 'Force password reauthentication', 'reauth')}${form('/begin', 'Silent SSO probe', 'silent')}${form('/refresh', 'Refresh stored session')}${form('/logout', 'End provider session')}<table border="1"><caption>Redacted observations; email ownership is simulated; privileged access always disabled</caption><thead><tr><th>Mode</th><th>Validated ID</th><th>Password AMR</th><th>Other AMR</th><th>Auth age (s)</th><th>Same auth time</th><th>Same session</th><th>Refresh rejected after logout</th></tr></thead><tbody>${session.observations.map(o => '<tr>' + [o.mode, o.validatedIdToken, o.amr?.includes('swoovr:password'), o.otherAmrPresent, o.authAgeSeconds, o.sameAuthTimeAsPrevious, o.sameSessionAsPrevious, o.refreshRejectedAfterLogout].map(v => '<td>' + esc(v ?? 'n/a') + '</td>').join('') + '</tr>').join('')}</tbody></table>`);
      }
      if (req.method !== 'POST' || !['/begin', '/refresh', '/logout'].includes(url.pathname) || url.search) return send(404, 'Not found');
      if (req.headers.origin !== origin || req.headers['content-type'] !== 'application/x-www-form-urlencoded') return send(403, 'Request rejected');
      let body = '';
      for await (const chunk of req) { body += chunk; if (body.length > 1024) return send(413, 'Request rejected'); }
      const form = new URLSearchParams(body);
      if (form.getAll('csrf').length !== 1 || form.get('csrf') !== session.csrf) return send(403, 'Request rejected');
      if (session.busy) return send(409, 'Session operation in progress');
      if (url.pathname === '/begin') {
        const mode = form.get('mode');
        if (!['', 'reauth', 'silent'].includes(mode)) return send(400, 'Request rejected');
        const tx = transactions.start(id); tx.mode = mode || 'browser';
        session.generation = (session.generation ?? 0) + 1; tx.generation = session.generation;
        const auth = new URL(issuer + '/protocol/openid-connect/auth');
        auth.search = new URLSearchParams({ client_id: client, redirect_uri: callback, response_type: 'code', scope: 'openid profile email', state: tx.state, nonce: tx.nonce, code_challenge_method: 'S256', code_challenge: createHash('sha256').update(tx.verifier).digest('base64url'), ...(mode === 'reauth' ? { prompt: 'login', max_age: '0' } : {}), ...(mode === 'silent' ? { prompt: 'none' } : {}) });
        return send(303, '', { Location: auth.href });
      }
      if (url.pathname === '/logout') {
        // Cancel pending login even when no provider session has been observed yet.
        session.generation = (session.generation ?? 0) + 1;
        if (!session.refresh) return send(303, '', { Location: '/' });
      }
      if (!session.refresh) return send(400, 'No validated session');
      session.busy = true; lockedSession = session;
      if (url.pathname === '/refresh') {
        const tokens = await provider(issuer + '/protocol/openid-connect/token', { method: 'POST', body: new URLSearchParams({ grant_type: 'refresh_token', client_id: client, refresh_token: session.refresh }) });
        await observe(session, tokens, session.nonce, 'refresh');
      } else {
        await provider(issuer + '/protocol/openid-connect/logout', { method: 'POST', body: new URLSearchParams({ client_id: client, refresh_token: session.refresh }) });
        // Prove refresh rejection after provider logout; never print or persist the token.
        let rejected = false;
        try { await provider(issuer + '/protocol/openid-connect/token', { method: 'POST', body: new URLSearchParams({ grant_type: 'refresh_token', client_id: client, refresh_token: session.refresh }) }); }
        catch (error) { rejected = error.status === 400 && error.oauthError === 'invalid_grant'; }
        session.observations.push({ mode: 'logout', refreshRejectedAfterLogout: rejected, privilegedAccessEnabled: false });
        delete session.refresh; delete session.claims; delete session.nonce;
      }
      return send(303, '', { Location: '/' });
    } catch {
      // Exceptions may contain URLs/codes/tokens; log no exception text or provider response body.
      send(400, 'Investigation rejected. No access granted. Return to the local harness and start a new transaction.');
    } finally {
      if (lockedSession) lockedSession.busy = false;
    }
  });
  async function observe(session, tokens, nonce, mode, started) {
    const jwks = await provider(issuer + '/protocol/openid-connect/certs');
    const claims = validateIdToken(tokens.id_token, jwks, { issuer, client, nonce, refresh: mode === 'refresh' });
    if ((mode === 'refresh' || (mode === 'silent' && session.claims)) && (claims.sub !== session.claims.sub || claims.sid !== session.claims.sid || claims.auth_time !== session.claims.auth_time)) throw new Error('Session changed');
    if (mode === 'reauth' && (claims.auth_time < started - 5 || (session.claims && claims.sub !== session.claims.sub))) throw new Error('Reauthentication evidence rejected');
    if (typeof tokens.refresh_token !== 'string') throw new Error('Missing refresh');
    const previous = session.claims;
    session.observations.push({ mode, validatedIdToken: true, amr: Array.isArray(claims.amr) ? claims.amr.filter(v => ['swoovr:password'].includes(v)) : [], otherAmrPresent: Array.isArray(claims.amr) && claims.amr.some(v => v !== 'swoovr:password'), brokerEvidencePresent: typeof claims.sandbox_broker === 'string', emailVerifiedClaim: claims.email_verified === true, emailOwnershipEvidence: 'ADMIN_ASSERTED_SIMULATION_ONLY', authAgeSeconds: Math.floor(Date.now() / 1000) - claims.auth_time, sameAuthTimeAsPrevious: previous ? previous.auth_time === claims.auth_time : null, sameSessionAsPrevious: previous ? previous.sid === claims.sid : null, privilegedAccessEnabled: false });
    session.observations = session.observations.slice(-20);
    session.refresh = tokens.refresh_token; session.claims = claims; session.nonce = nonce;
  }
  return server;
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  createHarness().listen(8280, '127.0.0.1', () => console.log('Local investigation at http://127.0.0.1:8280/; no tokens logged.'));
}
