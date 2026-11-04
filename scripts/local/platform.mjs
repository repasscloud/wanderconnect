// Local-only launcher support. No dependencies; runs inside the Node tools container.
import { readFile, writeFile, mkdir, chmod } from 'node:fs/promises';
import { randomBytes, createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { localRealmApi } from '../auth/provision.mjs';
import { applyAll } from '../auth/realms.mjs';
import { totp } from '../auth/totp.mjs';

// Deliberately accept a small, literal dotenv subset; never source executable shell.
export function parseEnv(text) {
  const result = {};
  for (const line of text.split(/\r?\n/)) {
    if (!line.trim() || line.trimStart().startsWith('#')) continue;
    const match = /^(?:export )?([A-Z][A-Z0-9_]*)=(.*)$/.exec(line.trim());
    if (!match) throw Error('Unsupported dotenv syntax; use literal KEY=value lines.');
    let value = match[2].trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) value = value.slice(1, -1);
    if (/[\r\n'$`\\]/.test(value)) throw Error('Use literal dotenv values without quotes, interpolation, backslashes or shell substitutions.');
    result[match[1]] = value;
  }
  return result;
}
const optional = ['SWOOVR_DIDIT_API_KEY', 'SWOOVR_DIDIT_WORKFLOW_ID', 'SWOOVR_DIDIT_WEBHOOK_SECRET', 'SWOOVR_SCREENING_IMAGE'];
const secrets = ['KC_BOOTSTRAP_ADMIN_PASSWORD', 'SWOOVR_MEMBERS_WEB_SECRET', 'SWOOVR_VENUES_WEB_SECRET', 'SWOOVR_STAFF_WEB_SECRET', 'SWOOVR_DB_PASSWORD', 'SWOOVR_FOUNDER_SECRET_V1', 'SWOOVR_MEMBERS_API_ADMIN_SECRET', 'SWOOVR_LOCAL_ADMIN_PASSWORD', 'SWOOVR_LOCAL_MEMBER_PASSWORD'];
export async function initialize(dir = '.local', input = '.env') {
  await mkdir(dir, { recursive: true, mode: 0o700 });
  await chmod(dir, 0o700);
  const path = resolve(dir, 'docker.env');
  const existing = await readFile(path, 'utf8').catch(e => { if (e.code !== 'ENOENT') throw e; return ''; });
  const defaults = parseEnv(await readFile(input, 'utf8').catch(e => { if (e.code !== 'ENOENT') throw e; return ''; }));
  const values = parseEnv(existing);
  const wanted = [...secrets, ...optional, 'SWOOVR_LOCAL_ADMIN_EMAIL'];
  for (const key of wanted) if (!values[key] && defaults[key]) values[key] = defaults[key];
  for (const key of secrets) if (!values[key]) values[key] = randomBytes(32).toString('hex');
  values.SWOOVR_LOCAL_ADMIN_EMAIL ||= 'root-admin@example.test';
  if (!/^[a-z0-9.+_-]+@example\.test$/.test(values.SWOOVR_LOCAL_ADMIN_EMAIL)) throw Error('Local admin must use a fictional adult address at example.test.');
  for (const key of ['SWOOVR_DB_PASSWORD', 'SWOOVR_FOUNDER_SECRET_V1'])
    if (!/^[a-f0-9]{64}$/.test(values[key])) throw Error(key + ' must be 64 hexadecimal characters.');
  if (values.SWOOVR_LOCAL_ADMIN_PASSWORD.length < 12) throw Error('Local admin password must be at least 12 characters.');
  const output = '# Private managed localhost configuration. Retained on reset; edit credentials before first up.\n' +
    wanted.filter(k => values[k]).map(k => `${k}='${values[k]}'`).join('\n') + '\n';
  if (output !== existing) await writeFile(path, output, { mode: 0o600 });
  await chmod(path, 0o600);
  console.log('Private credentials ready in .local/platform/docker.env (values not printed).');
}
async function env() { return parseEnv(await readFile('.local/docker.env', 'utf8')); }
async function save(name, data) { await writeFile('.local/' + name, JSON.stringify(data, null, 2) + '\n', { mode: 0o600 }); }

// The existing realm tools read literal unquoted values. Supply an in-memory reader,
// preserving their fixed localhost target and local-realm checks.
async function realmApi(realm) {
  const values = await env();
  return localRealmApi({ realm, read: async () => Object.entries(values).map(([k,v]) => `${k}=${v}`).join('\n') });
}
async function ensureUser(realm, email, password, root = false, firstName = null) {
  const api = await realmApi(realm);
  const definition = await api('');
  if (definition.attributes?.['swoovr.environment'] !== 'local-development-only') throw Error('Not a local development realm.');
  const users = await api('/users?exact=true&username=' + encodeURIComponent(email));
  const owned = JSON.parse(await readFile('.local/identities.json', 'utf8').catch(e => { if (e.code !== 'ENOENT') throw e; return '{}'; }));
  const key = realm + ':' + email;
  if (users.length > 1 || users[0] && owned[key] !== users[0].id || !users.length && owned[key]) throw Error('Refusing an unmanaged or stale seed identity; inspect/reset the managed sandbox.');
  if (!users.length) await api('/users', 'POST', {
    username: email, email, firstName: firstName ?? (root ? 'Local Admin' : 'Fictional'), lastName: firstName ? 'Staff' : 'Adult',
    enabled: true, emailVerified: true, // Explicit LOCAL simulation only.
    // Staff fixtures have no terms step: the staff realm disables it.
    requiredActions: root ? ['CONFIGURE_TOTP'] : firstName ? [] : ['TERMS_AND_CONDITIONS'],
    credentials: [{ type: 'password', value: password, temporary: false }],
  });
  const created = (await api('/users?exact=true&username=' + encodeURIComponent(email)))[0];
  owned[key] = created.id;
  await save('identities.json', owned);
  return created;
}

// Keycloak's hidden totpSecret is the raw ASCII secret; the manual authenticator
// display uses base32. Keep the form's raw field unchanged and persist base32.
export function enrollmentSecret(raw) {
  let bits = 0, value = 0, encoded = '';
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  for (const byte of Buffer.from(raw, 'utf8')) {
    value = (value << 8) | byte; bits += 8;
    while (bits >= 5) { bits -= 5; encoded += alphabet[(value >>> bits) & 31]; }
    value &= (1 << bits) - 1;
  }
  if (bits) encoded += alphabet[(value << (5 - bits)) & 31];
  return encoded;
}
// Exercise real code+PKCE, password and TOTP flows, without a browser installation.
// Follow only the fixed local Keycloak origin; do not log cookies, HTML or tokens.
const decode = value => value.replaceAll('&amp;', '&').replaceAll('&#39;', "'").replaceAll('&quot;', '"');
export async function login({ realm, client, secret, redirect, email, password, fixture }) {
  const base = 'http://localhost:8180';
  const verifier = randomBytes(32).toString('base64url');
  const state = randomBytes(16).toString('hex');
  const cookies = new Map();
  let url = base + '/realms/' + realm + '/protocol/openid-connect/auth?' + new URLSearchParams({
    client_id: client, response_type: 'code', scope: 'openid profile email', redirect_uri: redirect,
    state, nonce: randomBytes(16).toString('hex'), code_challenge: createHash('sha256').update(verifier).digest('base64url'), code_challenge_method: 'S256',
  });
  let method = 'GET', body, passwordSubmitted = false, otpSubmitted = false, enrollmentSubmitted = false;
  for (let step = 0; step < 20; step++) {
    if (url.startsWith(redirect + '?')) {
      const result = new URL(url);
      if (result.searchParams.get('state') !== state || !result.searchParams.get('code')) throw Error('OAuth callback rejected.');
      const tokenResponse = await fetch(base + '/realms/' + realm + '/protocol/openid-connect/token', {
        method: 'POST', body: new URLSearchParams({ grant_type: 'authorization_code', client_id: client,
          client_secret: secret, code: result.searchParams.get('code'), redirect_uri: redirect, code_verifier: verifier }),
      });
      if (!tokenResponse.ok) throw Error('Local OAuth token exchange failed.');
      return (await tokenResponse.json()).access_token;
    }
    if (new URL(url).origin !== base) throw Error('Unexpected authentication destination.');
    const response = await fetch(url, { method, body, redirect: 'manual', signal: AbortSignal.timeout(15000), headers: {
      Cookie: [...cookies].map(([k,v]) => `${k}=${v}`).join('; '),
      ...(body ? { 'Content-Type': 'application/x-www-form-urlencoded' } : {}),
    } });
    for (const cookie of response.headers.getSetCookie()) {
      const pair = cookie.split(';')[0], at = pair.indexOf('='); cookies.set(pair.slice(0, at), pair.slice(at + 1));
    }
    if (response.status >= 300 && response.status < 400) {
      url = new URL(response.headers.get('location'), url).href; method = 'GET'; body = undefined; continue;
    }
    const html = await response.text();
    if (!response.ok) throw Error('Local sign-in HTTP ' + response.status);
    const form = /<form\b[^>]*action="([^"]+)"[^>]*>([\s\S]*?)<\/form>/.exec(html);
    if (!form) throw Error('Expected local authentication form.');
    const fields = new URLSearchParams();
    for (const input of form[2].matchAll(/<input\b[^>]*>/g)) {
      const name = /\bname="([^"]+)"/.exec(input[0])?.[1];
      if (name && /\btype="hidden"/.test(input[0])) fields.set(name, decode(/\bvalue="([^"]*)"/.exec(input[0])?.[1] ?? ''));
    }
    if (/name="password"/.test(html)) {
      if (passwordSubmitted) throw Error('Local credentials rejected; inspect saved credentials instead of retrying passwords.');
      passwordSubmitted = true;
      fields.set('username', email); fields.set('password', password); fields.set('credentialId', '');
    }
    else if (/name="userLabel"/.test(html)) {
      if (enrollmentSubmitted) throw Error('Local TOTP enrollment rejected.');
      enrollmentSubmitted = true;
      // Keycloak's enrollment page contains the secret in a hidden input.
      if (!fields.get('totpSecret')) throw Error('TOTP enrollment secret unavailable.');
      fixture.totpSecret = enrollmentSecret(fields.get('totpSecret'));
      fixture.lastOtpStep = Math.floor(Date.now()/30000);
      await save('admin.json', fixture);
      fields.set('totp', totp(fixture.totpSecret)); fields.set('userLabel', 'Managed localhost');
    } else if (/name="otp"/.test(html)) {
      if (otpSubmitted) throw Error('Local TOTP code rejected; inspect the saved factor and clock.');
      otpSubmitted = true;
      if (fixture.lastOtpStep > Math.floor(Date.now()/30000) + 1) throw Error('Local OTP counter is ahead of the clock.');
      if (!fixture.totpSecret) throw Error('Missing saved local TOTP secret; restore it rather than resetting existing credentials.');
      while (Math.floor(Date.now()/30000) <= (fixture.lastOtpStep ?? -1)) await new Promise(r => setTimeout(r, 500));
      fixture.lastOtpStep = Math.floor(Date.now()/30000);
      await save('admin.json', fixture);
      fields.set('otp', totp(fixture.totpSecret));
    } else if (/name="accept"/.test(html)) fields.set('accept', 'Accept');
    else throw Error('Unexpected local sign-in step; inspect account required actions.');
    url = new URL(decode(form[1]), url).href; method = 'POST'; body = fields;
  }
  throw Error('Local authentication did not complete (credentials or required actions).');
}
async function session(token) {
  const r = await fetch('http://127.0.0.1:5080/api/session', { headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(15000) });
  if (!r.ok) throw Error('API rejected real local session: ' + r.status);
  return r.json();
}
async function bootstrap() {
  const values = await env();
  const changes = await applyAll({ open: realmApi, secret: async () => values.SWOOVR_MEMBERS_API_ADMIN_SECRET });
  console.log('Local realm flows and service account configured: ' + Object.keys(changes).length + ' realms.');
  const root = await ensureUser('swoovr-staff-dev', values.SWOOVR_LOCAL_ADMIN_EMAIL, values.SWOOVR_LOCAL_ADMIN_PASSWORD, true);
  const fixture = JSON.parse(await readFile('.local/admin.json', 'utf8').catch(e => { if (e.code !== 'ENOENT') throw e; return '{}'; }));
  if (fixture.userId && fixture.userId !== root.id) throw Error('Stale managed admin fixture.');
  fixture.userId = root.id;
  await save('admin.json', fixture);
  const token = await login({ realm: 'swoovr-staff-dev', client: 'swoovr-admin-web', secret: values.SWOOVR_STAFF_WEB_SECRET,
    redirect: 'http://localhost:5082/signin-oidc', email: values.SWOOVR_LOCAL_ADMIN_EMAIL, password: values.SWOOVR_LOCAL_ADMIN_PASSWORD, fixture });
  await session(token); // Validated server-side evidence creates the application account before CLI grant.
  await writeFile('.local/admin-subject', root.id + '\n', { mode: 0o600 });
  const firstStart = await readFile('.local/initialized', 'utf8').then(() => false).catch(e => { if (e.code !== 'ENOENT') throw e; return true; });
  if (firstStart) for (const email of ['fictional.alex@example.test', 'fictional.sam@example.test'])
    await ensureUser('swoovr-members-dev', email, values.SWOOVR_LOCAL_MEMBER_PASSWORD);
  // D322: one fictional staff sign-in per non-root role. Keycloak only authenticates; the shell grants each role through
  // the API CLI after this first sign-in has created the application account. Keycloak names may not contain '&'.
  const roles = [];
  for (const [email, role, name] of [['fictional.trust@example.test', 'trust-safety', 'Trust and Safety'], ['fictional.support@example.test', 'support', 'Support']]) {
    const staff = await ensureUser('swoovr-staff-dev', email, values.SWOOVR_LOCAL_ADMIN_PASSWORD, false, name);
    await session(await login({ realm: 'swoovr-staff-dev', client: 'swoovr-admin-web', secret: values.SWOOVR_STAFF_WEB_SECRET,
      redirect: 'http://localhost:5082/signin-oidc', email, password: values.SWOOVR_LOCAL_ADMIN_PASSWORD, fixture: {} }));
    roles.push(staff.id + '\t' + role);
  }
  await writeFile('.local/staff-roles.tsv', roles.join('\n') + '\n', { mode: 0o600 });
  console.log('Managed fictional identities prepared; email ownership is simulated, member gates remain enabled.');
}
async function smoke() {
  const expected = [['http://127.0.0.1:5080/health/live', 'ok'], ['http://localhost:5081/account', 'Swoovr'], ['http://127.0.0.1:8025/api/v1/messages', 'messages']];
  for (const [url, fragment] of expected) {
    const r = await fetch(url, { signal: AbortSignal.timeout(15000) });
    if (!r.ok || !(await r.text()).includes(fragment)) throw Error('Smoke check failed: ' + url);
  }
  const challenge = await fetch('http://localhost:5082/', { redirect: 'manual', signal: AbortSignal.timeout(15000) });
  const destination = new URL(challenge.headers.get('location'));
  if (challenge.status !== 302 || destination.origin !== 'http://localhost:8180' || destination.searchParams.get('client_id') !== 'swoovr-admin-web') throw Error('Admin OIDC challenge failed.');
  const values = await env(), fixture = JSON.parse(await readFile('.local/admin.json', 'utf8'));
  const token = await login({ realm: 'swoovr-staff-dev', client: 'swoovr-admin-web', secret: values.SWOOVR_STAFF_WEB_SECRET,
    redirect: 'http://localhost:5082/signin-oidc', email: values.SWOOVR_LOCAL_ADMIN_EMAIL, password: values.SWOOVR_LOCAL_ADMIN_PASSWORD, fixture });
  const summary = await session(token);
  if (!summary.adminArea || !summary.multiFactor) throw Error('Admin grant/password+OTP evidence was not accepted.');
  console.log('PASS: API/web/admin/mail and real Keycloak password+OTP session with application admin grant.');
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    switch (process.argv[2]) {
      case 'init': await initialize(); break;
      case 'bootstrap': await bootstrap(); break;
      case 'smoke': await smoke(); break;
      case 'admin-code': console.log(totp(JSON.parse(await readFile('.local/admin.json', 'utf8')).totpSecret)); break;
      default: throw Error('Unknown local support command.');
    }
  } catch (e) { console.error('Local setup failed: ' + e.message); process.exitCode = 1; }
}
