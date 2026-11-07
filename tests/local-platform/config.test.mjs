import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, stat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { initialize } from '../../scripts/local/platform.mjs';

test('initialization retains credentials and data identity across repeated runs', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'swoovr-launcher-'));
  await writeFile(join(dir, 'input.env'), 'SWOOVR_LOCAL_ADMIN_EMAIL=owner@example.test\nSWOOVR_LOCAL_ADMIN_PASSWORD="space and = sign"\n');
  await initialize(dir, join(dir, 'input.env'));
  const before = await readFile(join(dir, 'docker.env'), 'utf8');
  assert.match(before, /SWOOVR_LOCAL_ADMIN_PASSWORD='space and = sign'/);
  await initialize(dir, join(dir, 'input.env'));
  assert.equal(await readFile(join(dir, 'docker.env'), 'utf8'), before);
  assert.equal((await stat(join(dir, 'docker.env'))).mode & 0o077, 0);
});

test('environment files are parsed as data and interpolation is rejected', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'swoovr-launcher-'));
  await writeFile(join(dir, 'input.env'), 'SWOOVR_LOCAL_ADMIN_PASSWORD=$(touch /tmp/must-not-execute)\n');
  await assert.rejects(initialize(dir, join(dir, 'input.env')), /literal|unsupported/i);
});

test('nonfictional seed/admin addresses are refused', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'swoovr-launcher-'));
  await writeFile(join(dir, 'input.env'), 'SWOOVR_LOCAL_ADMIN_EMAIL=someone@gmail.com\n');
  await assert.rejects(initialize(dir, join(dir, 'input.env')), /example.test/);
});

test('Keycloak raw enrollment secret is encoded for the local TOTP generator', async () => {
  const { enrollmentSecret } = await import('../../scripts/local/platform.mjs');
  assert.equal(enrollmentSecret('12345678901234567890'), 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ');
});

test('bootstrap password containing an equals sign reaches Keycloak intact', async () => {
  const { localRealmApi } = await import('../../scripts/auth/provision.mjs');
  const api = await localRealmApi({
    read: async () => 'KC_BOOTSTRAP_ADMIN_PASSWORD=long=password=with=equals\n',
    provider: async (url, options) => {
      if (url.endsWith('/token')) {
        assert.equal(options.body.get('password'), 'long=password=with=equals');
        return { access_token: 'synthetic-management-token', expires_in: 60 };
      }
      return { realm: 'swoovr-dev' };
    },
  });
  assert.deepEqual(await api(''), { realm: 'swoovr-dev' });
});

test('rejected sign-in fails once instead of repeatedly attempting a password', async () => {
  const { login } = await import('../../scripts/local/platform.mjs');
  const previous = globalThis.fetch;
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return new Response('<form action="http://localhost:8180/login"><input name="password" type="password"></form>', { status: 200 });
  };
  try {
    await assert.rejects(login({ realm: 'swoovr-staff-dev', client: 'swoovr-admin-web', secret: 'synthetic',
      redirect: 'http://localhost:5082/signin-oidc', email: 'root-admin@example.test', password: 'synthetic', fixture: {} }), /credentials rejected/i);
    assert.equal(calls, 2);
  } finally { globalThis.fetch = previous; }
});

test('each nonroot staff authenticator has a distinct private fixture', async () => {
  const { staffFixtureName } = await import('../../scripts/local/platform.mjs');
  assert.equal(staffFixtureName('support'), 'staff-support.json');
  assert.equal(staffFixtureName('trust-safety'), 'staff-trust-safety.json');
  assert.notEqual(staffFixtureName('support'), 'admin.json');
  assert.throws(() => staffFixtureName('../admin'), /staff role/);
});
