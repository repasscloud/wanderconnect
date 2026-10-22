// Local development ONLY: creates the fictional local root admin in the staff realm (D36).
// Password-only account with OTP enrolment required at first sign-in; Keycloak's conditional
// 2FA then asks for the code on every sign-in, and the API denies root-admin access unless
// the session carries both 'pwd' and 'otp'. Credentials live only in a private .local file.
import { open, mkdir, chmod } from 'node:fs/promises';
import { localRealmApi, privateRead } from './provision.mjs';
import { random } from './security.mjs';

export const realm = 'swoovr-staff-dev';
export const fixturePath = '.local/swoovr-root-admin.json';
const owner = 'swoovr-local-root-admin-v1';

export async function ensureRootAdmin({ api, read = privateRead, write = writePrivate, password = () => 'Root-' + random() } = {}) {
  const realmInfo = await api('');
  if (realmInfo.realm !== realm || realmInfo.attributes?.['swoovr.environment'] !== 'local-development-only')
    throw new Error('Expected the local staff realm; refusing to create a root admin');
  let fixture;
  try { fixture = JSON.parse(await read(fixturePath)); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  if (fixture && (fixture.owner !== owner || !fixture.fictional)) throw new Error('Unmanaged root admin fixture');
  if (!fixture) {
    fixture = { owner, fictional: true, username: 'root-admin@example.test', password: password(),
      emailOwnership: 'ADMIN_ASSERTED_SIMULATION_ONLY' };
    await write(fixturePath, fixture);
  }
  const users = await api('/users?exact=true&username=' + encodeURIComponent(fixture.username));
  if (users.length > 1 || (users[0] && fixture.userId && users[0].id !== fixture.userId) || (users[0] && !fixture.userId))
    throw new Error('Unmanaged or stale root admin; inspect the local realm before replacing the fixture');
  if (users.length) return { created: false, username: fixture.username };
  await api('/users', 'POST', {
    username: fixture.username, email: fixture.username, firstName: 'Root', enabled: true,
    // Simulated for the local sandbox only; never evidence of real email ownership.
    emailVerified: true, requiredActions: ['CONFIGURE_TOTP'],
    credentials: [{ type: 'password', value: fixture.password, temporary: false }],
  });
  fixture.userId = (await api('/users?exact=true&username=' + encodeURIComponent(fixture.username)))[0].id;
  await write(fixturePath, fixture);
  return { created: true, username: fixture.username };
}

async function writePrivate(path, value) {
  await mkdir('.local', { recursive: true, mode: 0o700 });
  await chmod('.local', 0o700);
  const file = await open(path, 'w', 0o600);
  try { await file.writeFile(JSON.stringify(value)); } finally { await file.close(); }
  await chmod(path, 0o600);
}

if (process.argv[1]?.endsWith('/root-admin.mjs')) ensureRootAdmin({ api: await localRealmApi({ realm }) })
  .then(result => console.log((result.created ? 'Created' : 'Kept') + ' local fictional root admin ' + result.username + '; credentials are in ' + fixturePath + ' (not printed). OTP enrolment is required at first sign-in.'))
  .catch(error => { console.error('Root admin setup failed: ' + error.message); process.exitCode = 1; });
