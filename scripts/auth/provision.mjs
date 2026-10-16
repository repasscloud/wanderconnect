// Explicit opt-in local sandbox mutation; never run against a configurable remote host.
import { chmod, mkdir, open, readFile, stat } from 'node:fs/promises';
import { client, callback, origin, random, request } from './security.mjs';
const base = 'http://localhost:8180';
const realmPath = '/admin/realms/datingapp-dev';
const flow = 'datingapp-investigation-browser-v1';
const owner = 'datingapp-local-investigation-v1';
async function privateRead(path) {
  if ((await stat(path)).mode & 0o077) throw new Error('Private local file permissions must be 600');
  return readFile(path, 'utf8');
}
export function validateFixture(fixture, users) {
  if (users.length > 1 || (users[0] && users[0].id !== fixture.userId) || (!users.length && fixture.userId)) throw new Error('Unmanaged or stale fixture; deliberately replace the private fixture after inspecting the disposable realm');
}
export async function provision() {
  const env = await privateRead('.local/docker.env');
  const password = env.split('\n').find(line => line.startsWith('KC_BOOTSTRAP_ADMIN_PASSWORD='))?.split('=')[1];
  if (!password) throw new Error('Initialize the local sandbox first');
  // Bootstrap admin-cli grant configures the sandbox only. It is not browser-login evidence.
  const admin = await request(base + '/realms/master/protocol/openid-connect/token', {
    method: 'POST', body: new URLSearchParams({ grant_type: 'password', client_id: 'admin-cli', username: 'local-bootstrap', password })
  });
  const api = (path, method = 'GET', body) => request(base + realmPath + path, {
    method, headers: { Authorization: 'Bearer ' + admin.access_token, 'Content-Type': 'application/json' },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  const realm = await api('');
  if (realm.realm !== 'datingapp-dev' || realm.registrationAllowed || Object.keys(realm.smtpServer ?? {}).length) {
    throw new Error('Expected isolated development realm; refuse configuration drift');
  }
  await mkdir('.local', { recursive: true, mode: 0o700 });
  await chmod('.local', 0o700);
  let fixture;
  try { fixture = JSON.parse(await privateRead('.local/auth-fixture.json')); }
  catch (error) {
    if (error.code !== 'ENOENT') throw error;
    fixture = { username: 'adult-fixture-' + random().slice(0, 12), password: random(), fictionalAdult: true, emailOwnership: 'ADMIN_ASSERTED_SIMULATION_ONLY', owner };
    const file = await open('.local/auth-fixture.json', 'wx', 0o600);
    try { await file.writeFile(JSON.stringify(fixture)); } finally { await file.close(); }
  }
  if (fixture.owner !== owner || !fixture.fictionalAdult) throw new Error('Unmanaged fixture');
  const flows = await api('/authentication/flows');
  const existingFlow = flows.find(f => f.alias === flow);
  if (existingFlow && existingFlow.description !== owner) throw new Error('Unmanaged browser flow');
  if (!existingFlow) {
    await api('/authentication/flows/browser/copy', 'POST', { newName: flow });
    const created = (await api('/authentication/flows')).find(f => f.alias === flow);
    await api('/authentication/flows/' + created.id, 'PUT', { ...created, description: owner });
  }
  const executions = await api('/authentication/flows/' + flow + '/executions');
  const passwordExecution = executions.find(e => e.providerId === 'auth-username-password-form');
  if (!passwordExecution) throw new Error('Expected password execution is missing');
  const config = { alias: flow + '-password-evidence', config: { 'default.reference.value': 'datingapp:password', 'default.reference.maxAge': '60' } };
  if (passwordExecution.authenticationConfig) {
    const current = await api('/authentication/config/' + passwordExecution.authenticationConfig);
    if (current.alias !== config.alias) throw new Error('Unmanaged authentication config');
    await api('/authentication/config/' + current.id, 'PUT', { ...config, id: current.id });
  } else await api('/authentication/executions/' + passwordExecution.id + '/config', 'POST', config);
  const flowId = (await api('/authentication/flows')).find(f => f.alias === flow).id;
  const clients = await api('/clients?clientId=' + client);
  if (clients.length > 1 || (clients[0] && clients[0].attributes?.['datingapp.owner'] !== owner)) throw new Error('Unmanaged client');
  const definition = {
    clientId: client, name: 'Local investigation ONLY', protocol: 'openid-connect', enabled: true,
    publicClient: true, standardFlowEnabled: true, implicitFlowEnabled: false, directAccessGrantsEnabled: false,
    serviceAccountsEnabled: false, redirectUris: [callback], webOrigins: [], fullScopeAllowed: false,
    defaultClientScopes: ['basic', 'profile', 'email'], optionalClientScopes: [],
    attributes: { 'datingapp.owner': owner, 'pkce.code.challenge.method': 'S256', 'post.logout.redirect.uris': origin + '/' },
    authenticationFlowBindingOverrides: { browser: flowId },
    protocolMappers: [
      { name: 'execution-amr', protocol: 'openid-connect', protocolMapper: 'oidc-amr-mapper', config: { 'id.token.claim': 'true', 'access.token.claim': 'true' } },
      { name: 'broker-observation', protocol: 'openid-connect', protocolMapper: 'oidc-usersessionmodel-note-mapper', config: { 'user.session.note': 'identity_provider', 'claim.name': 'sandbox_broker', 'jsonType.label': 'String', 'id.token.claim': 'true', 'access.token.claim': 'false' } }
    ]
  };
  if (clients[0]) await api('/clients/' + clients[0].id, 'PUT', { ...definition, id: clients[0].id });
  else await api('/clients', 'POST', definition);
  const configuredClient = (await api('/clients?clientId=' + client))[0];
  const scopes = await api('/client-scopes');
  for (const name of ['basic', 'profile', 'email']) {
    const scope = scopes.find(s => s.name === name);
    if (!scope) throw new Error('Required built-in client scope is missing');
    await api('/clients/' + configuredClient.id + '/default-client-scopes/' + scope.id, 'PUT');
  }
  const users = await api('/users?exact=true&username=' + encodeURIComponent(fixture.username));
  validateFixture(fixture, users);
  if (!users.length) await api('/users', 'POST', {
    username: fixture.username, email: fixture.username + '@example.invalid', firstName: 'Fictional', lastName: 'Adult', enabled: true,
    // Explicit simulation to permit browser investigation without SMTP; never ownership proof.
    emailVerified: true,
    credentials: [{ type: 'password', value: fixture.password, temporary: false }], requiredActions: []
  });
  if (!users.length) {
    fixture.userId = (await api('/users?exact=true&username=' + encodeURIComponent(fixture.username)))[0].id;
    const file = await open('.local/auth-fixture.json', 'w', 0o600);
    try { await file.writeFile(JSON.stringify(fixture)); } finally { await file.close(); }
  }
  const actual = (await api('/clients?clientId=' + client))[0];
  const appliedScopes = await api('/clients/' + actual.id + '/default-client-scopes');
  if (JSON.stringify(actual.redirectUris) !== JSON.stringify([callback]) || !actual.publicClient || actual.directAccessGrantsEnabled || actual.implicitFlowEnabled || actual.attributes?.['pkce.code.challenge.method'] !== 'S256' || !['basic', 'profile', 'email'].every(name => appliedScopes.some(scope => scope.name === name))) throw new Error('Client read-back mismatch');
  console.log('Configured local investigation client, copied browser flow and private fictional-adult fixture. Email status is administrator-asserted simulation only.');
}
if (process.argv[1]?.endsWith('/provision.mjs')) provision().catch(() => { console.error('Provisioning failed; inspect local configuration without logging credentials or response bodies.'); process.exitCode = 1; });
