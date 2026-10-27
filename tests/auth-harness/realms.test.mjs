import { test } from 'node:test';
import assert from 'node:assert/strict';
import { applyRealm, applyAll, applyAccountsClient, accountsClientRoles, amrReferences, amrMaxAgeSeconds, realms, generatedProfile, generatedPublicClients } from '../../scripts/auth/realms.mjs';

// In-memory stand-in for the Admin REST paths realms.mjs uses; no provider is contacted.
function fakeRealm(name, { terms = 'REQUIRED', environment = 'local-development-only', foreignConfig = false } = {}) {
  const state = {
    realm: { realm: name, attributes: { 'swoovr.environment': environment, 'swoovr.registrationTerms': terms } },
    registration: [{ id: 'terms', providerId: 'registration-terms-and-conditions', requirement: 'DISABLED' }],
    action: { alias: 'TERMS_AND_CONDITIONS', enabled: false, defaultAction: false, priority: 20 },
    browser: Object.keys(amrReferences).map((providerId, i) => ({ id: 'e' + i, providerId, authenticationConfig: foreignConfig && i === 0 ? 'foreign' : undefined })),
    configs: foreignConfig ? { foreign: { id: 'foreign', alias: 'someone-else', config: {} } } : {},
    // As first imported: email before first name, no placeholders.
    profile: { attributes: [{ name: 'username' }, { name: 'email' }, { name: 'firstName' }] },
    clients: [{ clientId: 'swoovr-web', publicClient: false }],
    writes: [],
  };
  const api = async (path, method = 'GET', body) => {
    if (method !== 'GET') state.writes.push(method + ' ' + path);
    if (path === '') return state.realm;
    if (path === '/authentication/flows/registration/executions') {
      if (method === 'PUT') { state.registration[0].requirement = body.requirement; return; }
      return state.registration;
    }
    if (path === '/authentication/required-actions/TERMS_AND_CONDITIONS') {
      if (method === 'PUT') { state.action = body; return; }
      return state.action;
    }
    if (path === '/authentication/flows/browser/executions') return state.browser;
    const clientQuery = path.match(/^\/clients\?clientId=(.+)$/);
    if (clientQuery) return state.clients.filter(c => c.clientId === decodeURIComponent(clientQuery[1]));
    if (path === '/clients' && method === 'POST') { state.clients.push(structuredClone(body)); return; }
    if (path === '/users/profile') { if (method === 'PUT') { state.profile = structuredClone(body); return; } return structuredClone(state.profile); }
    const execution = path.match(/^\/authentication\/executions\/(\w+)\/config$/);
    if (execution) {
      const id = 'c' + execution[1];
      state.configs[id] = { ...body, id };
      state.browser.find(e => e.id === execution[1]).authenticationConfig = id;
      return;
    }
    const config = path.match(/^\/authentication\/config\/(\w+)$/);
    if (config) { if (method === 'PUT') state.configs[config[1]] = body; return state.configs[config[1]]; }
    throw new Error('Unexpected path ' + path);
  };
  return { api, state };
}

test('members realm: 18+ checkbox required, terms action recorded by default, AMR references set', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev');
  const changes = await applyRealm(api, 'swoovr-members-dev');
  assert.equal(state.registration[0].requirement, 'REQUIRED');
  assert.equal(state.action.enabled, true);
  assert.equal(state.action.defaultAction, true);
  for (const [i, reference] of Object.values(amrReferences).entries())
  {
    const config = state.configs[state.browser[i].authenticationConfig].config;
    assert.equal(config['default.reference.value'], reference);
    assert.equal(config['default.reference.maxAge'], amrMaxAgeSeconds, 'AMR must outlive the authentication second');
  }
  assert.equal(changes.length, 4 + Object.keys(amrReferences).length);
});

test('members app client: created once from the import, public, PKCE, no secret, app redirect only', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev');
  assert.ok((await applyRealm(api, 'swoovr-members-dev')).includes('client swoovr-mobile'));
  const mobile = state.clients.find(c => c.clientId === 'swoovr-mobile');
  assert.equal(mobile.publicClient, true);
  assert.equal('secret' in mobile, false);
  assert.equal(mobile.attributes['pkce.code.challenge.method'], 'S256');
  assert.equal(mobile.directAccessGrantsEnabled, false);
  assert.deepEqual(mobile.redirectUris, ['com.swoovr.app:/oauth2redirect']);
  assert.equal(mobile.protocolMappers.find(m => m.protocolMapper === 'oidc-audience-mapper').config['included.custom.audience'], 'swoovr-api');
  // A console edit is never overwritten.
  mobile.redirectUris = ['changed'];
  assert.ok(!(await applyRealm(api, 'swoovr-members-dev')).some(c => c.startsWith('client')));
  assert.deepEqual(mobile.redirectUris, ['changed']);
});

test('only the members realm has a public client', async () => {
  assert.deepEqual((await generatedPublicClients('swoovr-members-dev')).map(c => c.clientId), ['swoovr-mobile']);
  for (const name of ['swoovr-venues-dev', 'swoovr-staff-dev']) assert.deepEqual(await generatedPublicClients(name), []);
});

test('user profile follows the generated import: first name before email, prototype placeholders', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev');
  const changes = await applyRealm(api, 'swoovr-members-dev');
  assert.ok(changes.includes('user profile'));
  assert.deepEqual(state.profile, await generatedProfile('swoovr-members-dev'));
  const names = state.profile.attributes.map(a => a.name);
  assert.ok(names.indexOf('firstName') < names.indexOf('email'));
  assert.equal(state.profile.attributes.find(a => a.name === 'email').annotations.inputTypePlaceholder, '${swoovrPlaceholder.username}');
  assert.deepEqual(await applyRealm(api, 'swoovr-members-dev'), []);
});

test('second run changes nothing (idempotent)', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev');
  await applyRealm(api, 'swoovr-members-dev');
  const before = state.writes.length;
  assert.deepEqual(await applyRealm(api, 'swoovr-members-dev'), []);
  assert.equal(state.writes.length, before);
});

test('invitation-only realms keep the terms step and default action disabled', async () => {
  const { api, state } = fakeRealm('swoovr-staff-dev', { terms: 'DISABLED' });
  state.action = { ...state.action, enabled: true, defaultAction: true };
  await applyRealm(api, 'swoovr-staff-dev');
  assert.equal(state.registration[0].requirement, 'DISABLED');
  assert.equal(state.action.enabled, false);
  assert.equal(state.action.defaultAction, false);
});

test('refuses realms that are not local Swoovr development realms', async () => {
  for (const [name, options] of [['swoovr-members', {}], ['swoovr-members-dev', { environment: 'production' }]]) {
    const { api, state } = fakeRealm(name, options);
    await assert.rejects(applyRealm(api, 'swoovr-members-dev'), /refusing/);
    assert.deepEqual(state.writes, []);
  }
});

test('never overwrites an authentication config it does not own', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev', { foreignConfig: true });
  await assert.rejects(applyRealm(api, 'swoovr-members-dev'), /Unmanaged authentication config/);
  assert.equal(state.configs.foreign.alias, 'someone-else');
});

test('applyAll covers exactly the three realms from D31', async () => {
  const opened = [];
  const result = await applyAll({ open: async name => { opened.push(name); return fakeRealm(name, { terms: name === 'swoovr-members-dev' ? 'REQUIRED' : 'DISABLED' }).api; }, secret: async () => undefined });
  assert.deepEqual(opened, realms);
  assert.deepEqual(Object.keys(result), ['swoovr-members-dev', 'swoovr-venues-dev', 'swoovr-staff-dev']);
});

test('an owned config with the old zero max age is updated', async () => {
  const { api, state } = fakeRealm('swoovr-members-dev');
  await applyRealm(api, 'swoovr-members-dev');
  const id = state.browser[0].authenticationConfig;
  state.configs[id].config = { 'default.reference.value': 'pwd' };
  assert.deepEqual(await applyRealm(api, 'swoovr-members-dev'), ['amr pwd on auth-username-password-form']);
  assert.equal(state.configs[id].config['default.reference.maxAge'], amrMaxAgeSeconds);
});

test('the deletion client (D169): created once with only view-users and manage-users, secret kept in step', async () => {
  const state = { clients: [], secret: null, mappings: [], writes: [] };
  const roles = { 'view-users': { id: 'r1', name: 'view-users' }, 'manage-users': { id: 'r2', name: 'manage-users' }, 'realm-admin': { id: 'r3', name: 'realm-admin' } };
  const api = async (path, method = 'GET', body) => {
    if (method !== 'GET') state.writes.push(method + ' ' + path);
    if (path === '/clients?clientId=swoovr-api-accounts') return state.clients;
    if (path === '/clients?clientId=realm-management') return [{ id: 'rm', clientId: 'realm-management' }];
    if (path === '/clients' && method === 'POST') { state.clients.push({ ...structuredClone(body), id: 'acc' }); state.secret = body.secret; return; }
    if (path === '/clients/acc/client-secret') return { value: state.secret };
    if (path === '/clients/acc' && method === 'PUT') { state.secret = body.secret; return; }
    if (path === '/clients/acc/service-account-user') return { id: 'sa' };
    if (path === '/users/sa/role-mappings/clients/rm') { if (method === 'POST') { state.mappings.push(...body); return; } return state.mappings; }
    const role = path.match(/^\/clients\/rm\/roles\/(.+)$/);
    if (role) return roles[role[1]];
    throw new Error('Unexpected path ' + path);
  };
  assert.deepEqual(await applyAccountsClient(api, undefined), ['accounts client skipped (run ./scripts/docker-local.sh init)']);
  assert.deepEqual(state.writes, []);
  assert.deepEqual(await applyAccountsClient(api, 's1'), ['client swoovr-api-accounts', 'roles view-users manage-users']);
  const [client] = state.clients;
  assert.equal(client.serviceAccountsEnabled, true);
  assert.equal(client.publicClient, false);
  assert.equal(client.standardFlowEnabled || client.directAccessGrantsEnabled || client.implicitFlowEnabled, false, 'no sign-in flows');
  assert.deepEqual(state.mappings.map(r => r.name), accountsClientRoles);
  assert.deepEqual(await applyAccountsClient(api, 's1'), [], 'idempotent');
  assert.deepEqual(await applyAccountsClient(api, 's2'), ['secret swoovr-api-accounts']);
  assert.equal(state.secret, 's2');
});
