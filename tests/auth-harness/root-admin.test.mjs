import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ensureRootAdmin, fixturePath, realm } from '../../scripts/auth/root-admin.mjs';

function fakeStaff({ environment = 'local-development-only', name = realm, users = [] } = {}) {
  const state = { users: [...users], writes: [] };
  const api = async (path, method = 'GET', body) => {
    if (path === '') return { realm: name, attributes: { 'swoovr.environment': environment } };
    if (path.startsWith('/users?exact=true&username=')) return state.users.filter(u => u.username === decodeURIComponent(path.split('=').pop()));
    if (path === '/users' && method === 'POST') { state.writes.push(body); state.users.push({ ...body, id: 'u' + state.users.length }); return; }
    throw new Error('Unexpected path ' + path);
  };
  return { api, state };
}
const memory = (initial) => {
  const files = { ...initial };
  return {
    files,
    read: async path => { if (!(path in files)) throw Object.assign(new Error('missing'), { code: 'ENOENT' }); return JSON.stringify(files[path]); },
    write: async (path, value) => { files[path] = structuredClone(value); },
  };
};

test('creates a fictional password-only root admin that must enrol TOTP', async () => {
  const { api, state } = fakeStaff(); const store = memory({});
  const result = await ensureRootAdmin({ api, read: store.read, write: store.write, password: () => 'synthetic-test-only' });
  assert.equal(result.created, true);
  const [user] = state.writes;
  assert.deepEqual(user.requiredActions, ['CONFIGURE_TOTP']);
  assert.equal(user.credentials[0].temporary, false);
  assert.equal(user.username, 'root-admin@example.test');
  assert.equal(store.files[fixturePath].fictional, true);
  assert.equal(store.files[fixturePath].emailOwnership, 'ADMIN_ASSERTED_SIMULATION_ONLY');
  assert.equal(store.files[fixturePath].userId, 'u0');
});

test('second run keeps the existing account and never rewrites its password', async () => {
  const { api, state } = fakeStaff(); const store = memory({});
  await ensureRootAdmin({ api, read: store.read, write: store.write, password: () => 'first' });
  const result = await ensureRootAdmin({ api, read: store.read, write: store.write, password: () => 'second' });
  assert.equal(result.created, false);
  assert.equal(state.writes.length, 1);
  assert.equal(store.files[fixturePath].password, 'first');
});

test('refuses non-local or non-staff realms before touching anything', async () => {
  for (const options of [{ environment: 'production' }, { name: 'swoovr-members-dev' }]) {
    const { api, state } = fakeStaff(options); const store = memory({});
    await assert.rejects(ensureRootAdmin({ api, read: store.read, write: store.write }), /refusing/);
    assert.deepEqual(state.writes, []); assert.deepEqual(store.files, {});
  }
});

test('refuses an existing account it did not create', async () => {
  const { api, state } = fakeStaff({ users: [{ id: 'someone', username: 'root-admin@example.test' }] }); const store = memory({});
  await assert.rejects(ensureRootAdmin({ api, read: store.read, write: store.write, password: () => 'x' }), /Unmanaged or stale/);
  assert.deepEqual(state.writes, []);
});

test('refuses a fixture it does not own', async () => {
  const { api } = fakeStaff(); const store = memory({ [fixturePath]: { owner: 'someone-else', fictional: true } });
  await assert.rejects(ensureRootAdmin({ api, read: store.read, write: store.write }), /Unmanaged root admin fixture/);
});
