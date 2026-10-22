import { test } from 'node:test';
import assert from 'node:assert/strict';
import { applyRealm, applyAll, amrReferences, realms } from '../../scripts/auth/realms.mjs';

// In-memory stand-in for the Admin REST paths realms.mjs uses; no provider is contacted.
function fakeRealm(name, { terms = 'REQUIRED', environment = 'local-development-only', foreignConfig = false } = {}) {
  const state = {
    realm: { realm: name, attributes: { 'swoovr.environment': environment, 'swoovr.registrationTerms': terms } },
    registration: [{ id: 'terms', providerId: 'registration-terms-and-conditions', requirement: 'DISABLED' }],
    action: { alias: 'TERMS_AND_CONDITIONS', enabled: false, defaultAction: false, priority: 20 },
    browser: Object.keys(amrReferences).map((providerId, i) => ({ id: 'e' + i, providerId, authenticationConfig: foreignConfig && i === 0 ? 'foreign' : undefined })),
    configs: foreignConfig ? { foreign: { id: 'foreign', alias: 'someone-else', config: {} } } : {},
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
    assert.equal(state.configs[state.browser[i].authenticationConfig].config['default.reference.value'], reference);
  assert.equal(changes.length, 2 + Object.keys(amrReferences).length);
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
  const result = await applyAll({ open: async name => { opened.push(name); return fakeRealm(name, { terms: name === 'swoovr-members-dev' ? 'REQUIRED' : 'DISABLED' }).api; } });
  assert.deepEqual(opened, realms);
  assert.deepEqual(Object.keys(result), ['swoovr-members-dev', 'swoovr-venues-dev', 'swoovr-staff-dev']);
});
