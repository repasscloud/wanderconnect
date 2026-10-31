// Local acceptance for the app's Keycloak client (swoovr-mobile, public, code + PKCE): the same requests the MAUI app
// makes, from actual Edge against the local members realm and the API. Needs the local stack, the API on
// 127.0.0.1:5080 with its database, `./scripts/docker-local.sh realms` (creates the client) and a Debug API build.
// Negative tests from AUTHENTICATION.md: wrong redirect, missing PKCE, implicit and password grants, wrong verifier,
// code replay (which also drops the session's app tokens), refresh-token reuse, revoked session, wrong audience for privileged areas.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { chromium } = require(process.env.SWOOVR_PLAYWRIGHT_PATH || 'playwright');
const { keycloak, createMember, cli, call, claims, sleep } = require('./local-tokens.cjs');

const realm = `${keycloak}/realms/swoovr-members-dev`;
const clientId = 'swoovr-mobile', redirect = 'com.swoovr.app:/oauth2redirect';
const pkce = () => { const verifier = crypto.randomBytes(32).toString('base64url'); return { verifier, challenge: crypto.createHash('sha256').update(verifier).digest('base64url') }; };
const authorizeUrl = (params) => `${realm}/protocol/openid-connect/auth?` + new URLSearchParams({
  client_id: clientId, response_type: 'code', scope: 'openid profile email', redirect_uri: redirect, state: 'st', nonce: 'nn', ...params });
const token = form => fetch(`${realm}/protocol/openid-connect/token`, { method: 'POST', body: new URLSearchParams({ client_id: clientId, ...form }) });

/** Opens the authorize URL and returns the app redirect Keycloak sends (it never loads: the scheme is the app's). */
async function appRedirect(page, url, fill) {
  let location;
  const listener = r => { const l = r.headers()['location']; if (l?.startsWith('com.swoovr.app:')) location = l; };
  page.on('response', listener);
  await page.goto(url).catch(() => {});
  if (!location && fill) await fill(page);
  for (let i = 0; i < 100 && !location; i++) await sleep(100);
  page.off('response', listener);
  return location && new URL(location.replace('com.swoovr.app:', 'https://app.invalid'));
}

const signInForm = member => async page => {
  if (!(await page.$('#username'))) return;
  await page.fill('#username', member.email); await page.fill('#password', member.password); await page.click('#kc-login');
  await page.waitForLoadState();
  if (page.url().includes('execution=TERMS_AND_CONDITIONS')) await page.click('[name=accept]');
};

(async () => {
  const edge = await require('./managed-edge.cjs').managedEdge(chromium), browser = edge.browser;
  let passed = 0; const pass = () => passed++;
  try {
    const alice = await createMember('mobile');
    const outsider = await createMember('mobile-outsider');
    await cli('beta', 'add', alice.email);
    const context = await browser.newContext();
    const page = await context.newPage();

    // Unregistered redirect: Keycloak shows its own error page and never redirects anywhere.
    const { challenge: c0 } = pkce();
    let response = await page.goto(authorizeUrl({ redirect_uri: 'com.evil.app:/oauth2redirect', code_challenge: c0, code_challenge_method: 'S256' }));
    assert.equal(response.status(), 400); assert.match(await page.content(), /redirect_uri/i); pass();
    // PKCE is required, and only S256.
    let back = await appRedirect(page, authorizeUrl({}));
    assert.equal(back.searchParams.get('error'), 'invalid_request'); assert.equal(back.searchParams.get('code'), null); pass();
    back = await appRedirect(page, authorizeUrl({ code_challenge: c0, code_challenge_method: 'plain' }));
    assert.equal(back.searchParams.get('error'), 'invalid_request'); pass();
    // No implicit flow and no password grant for the app.
    back = await appRedirect(page, authorizeUrl({ response_type: 'token', code_challenge: c0, code_challenge_method: 'S256' }));
    assert.ok(back.searchParams.get('error') || new URLSearchParams(back.hash.slice(1)).get('error')); assert.equal(new URLSearchParams(back.hash.slice(1)).get('access_token'), null); pass();
    response = await token({ grant_type: 'password', username: alice.email, password: alice.password, scope: 'openid' });
    assert.ok([400, 401].includes(response.status)); assert.equal((await response.json()).error, 'unauthorized_client'); pass();

    // Sign in through the real page; the code comes back to the app's scheme with the state.
    const first = pkce();
    back = await appRedirect(page, authorizeUrl({ code_challenge: first.challenge, code_challenge_method: 'S256' }), signInForm(alice));
    assert.equal(back.pathname, '/oauth2redirect'); assert.equal(back.searchParams.get('state'), 'st'); pass();
    const code = back.searchParams.get('code'); assert.ok(code); pass();
    // Without a verifier, or with someone else's, the code is refused.
    response = await token({ grant_type: 'authorization_code', code, redirect_uri: redirect });
    assert.equal(response.status, 400); pass();
    response = await token({ grant_type: 'authorization_code', code, redirect_uri: redirect, code_verifier: pkce().verifier });
    assert.equal(response.status, 400); pass();

    // Fresh attempt (the browser session signs straight in): redeemed once, no client secret.
    const second = pkce();
    back = await appRedirect(page, authorizeUrl({ code_challenge: second.challenge, code_challenge_method: 'S256' }), signInForm(alice));
    const code2 = back.searchParams.get('code');
    response = await token({ grant_type: 'authorization_code', code: code2, redirect_uri: redirect, code_verifier: second.verifier });
    assert.equal(response.status, 200); pass();
    let tokens = await response.json();
    assert.equal(tokens.token_type, 'Bearer'); assert.ok(tokens.refresh_token && tokens.id_token); pass();
    response = await token({ grant_type: 'authorization_code', code: code2, redirect_uri: redirect, code_verifier: second.verifier });
    assert.equal(response.status, 400); pass(); // replay
    const access = claims(tokens.access_token), id = claims(tokens.id_token);
    assert.equal(access.iss, realm); assert.equal(access.azp, clientId); pass();
    assert.ok([].concat(access.aud).includes('swoovr-api')); pass();
    assert.equal(id.aud, clientId); assert.equal(id.nonce, 'nn'); pass();
    assert.ok(access.swoovr_adult_attested_at); assert.deepEqual(access.amr, ['pwd']); pass();

    // The API treats the app's token like the web's: member area yes, staff areas no.
    let session = await call(tokens.access_token, '/api/session');
    assert.equal(session.status, 200); assert.equal(session.body.memberArea, true); assert.equal(session.body.beta, 'enrolled'); pass();
    assert.equal(session.body.adminArea, false); assert.equal(session.body.supportArea, false); pass();
    assert.equal((await call(tokens.access_token, '/api/me')).status, 200); pass();
    assert.equal((await call(tokens.access_token, '/api/admin/beta')).status, 403); pass();
    assert.equal((await call(tokens.id_token, '/api/session')).status, 401); pass(); // ID token: wrong audience
    const tampered = tokens.access_token.slice(0, -4) + (tokens.access_token.endsWith('AAAA') ? 'BBBB' : 'AAAA');
    assert.equal((await call(tampered, '/api/session')).status, 401); pass();

    // The replayed code above made Keycloak drop the app from that session, so its refresh token is dead too.
    response = await token({ grant_type: 'refresh_token', refresh_token: tokens.refresh_token });
    assert.equal(response.status, 400); assert.equal((await response.json()).error, 'invalid_grant'); pass();

    // Refresh rotates; the old refresh token is refused afterwards.
    const fourth = pkce();
    back = await appRedirect(page, authorizeUrl({ code_challenge: fourth.challenge, code_challenge_method: 'S256' }), signInForm(alice));
    tokens = await (await token({ grant_type: 'authorization_code', code: back.searchParams.get('code'), redirect_uri: redirect, code_verifier: fourth.verifier })).json();
    response = await token({ grant_type: 'refresh_token', refresh_token: tokens.refresh_token });
    assert.equal(response.status, 200); pass();
    const rotated = await response.json();
    assert.notEqual(rotated.refresh_token, tokens.refresh_token); pass();
    response = await token({ grant_type: 'refresh_token', refresh_token: tokens.refresh_token });
    assert.equal(response.status, 400); pass();

    // Signing out ends the Keycloak session: the browser end-session page returns to the app, then refresh fails.
    const endSession = `${realm}/protocol/openid-connect/logout?` + new URLSearchParams({
      client_id: clientId, id_token_hint: tokens.id_token, post_logout_redirect_uri: 'com.swoovr.app:/signedout' });
    back = await appRedirect(page, endSession);
    assert.equal(back?.pathname, '/signedout'); pass();
    response = await token({ grant_type: 'refresh_token', refresh_token: rotated.refresh_token });
    assert.equal(response.status, 400); pass();
    // An unregistered return address is refused.
    response = await page.goto(endSession.replace(encodeURIComponent('com.swoovr.app:/signedout'), encodeURIComponent('https://evil.example/')));
    assert.equal(response.status(), 400); pass();

    // Not on the beta list: signed in, but the member routes stay closed (D62), same as on the web.
    const outsiderContext = await browser.newContext();
    const third = pkce();
    back = await appRedirect(await outsiderContext.newPage(), authorizeUrl({ code_challenge: third.challenge, code_challenge_method: 'S256' }), signInForm(outsider));
    response = await token({ grant_type: 'authorization_code', code: back.searchParams.get('code'), redirect_uri: redirect, code_verifier: third.verifier });
    const outsiderToken = (await response.json()).access_token;
    session = await call(outsiderToken, '/api/session');
    assert.equal(session.body.beta, 'notEnrolled'); assert.equal(session.body.memberArea, false); pass();
    const me = await call(outsiderToken, '/api/me');
    assert.equal(me.status, 403); assert.equal(me.body.code, 'beta-not-enrolled'); pass();
    await outsiderContext.close();
    await context.close();
    console.log(`PASS: mobile client acceptance, ${passed} checks (public PKCE client, redirect allow-list, no implicit/password grant, verifier and replay, API audience and areas, rotation, end session, beta gate).`);
  } finally {
    await edge.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
