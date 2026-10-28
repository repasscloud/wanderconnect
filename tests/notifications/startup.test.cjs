// Configuration rejection only: no credentials, database or provider request. Build the API first.
const test = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
const crypto = require('node:crypto');
const ecPublic = crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' }).publicKey.export({ type: 'spki', format: 'pem' });
const rsaPublic = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 }).publicKey.export({ type: 'spki', format: 'pem' });
const cwd = path.resolve(__dirname, '../../src/Swoovr.Api');
for (const [name, config, expected] of [
  ['production simulation', { Push__Provider: 'simulated' }, 'Simulated push is permitted only in Development'],
  ['unknown mode', { Push__Provider: 'typo' }, 'Push:Provider must be'],
  ['empty providers', { Push__Provider: 'providers' }, 'requires an explicitly enabled platform'],
  ['incomplete APNs', { Push__Provider: 'providers', Push__Apns__Enabled: 'true' }, 'Push:Apns:KeyId is required'],
  ['incomplete FCM', { Push__Provider: 'providers', Push__Fcm__Enabled: 'true' }, 'Push:Fcm:ProjectId is required'],
  ['public-only APNs key', { Push__Provider: 'providers', Push__Apns__Enabled: 'true', Push__Apns__KeyId: 'fictional', Push__Apns__TeamId: 'fictional', Push__Apns__PrivateKeyPem: ecPublic }, 'APNs requires a valid P-256 private key'],
  ['public-only FCM key', { Push__Provider: 'providers', Push__Fcm__Enabled: 'true', Push__Fcm__ProjectId: 'fictional', Push__Fcm__ClientEmail: 'fictional@example.test', Push__Fcm__PrivateKeyId: 'fictional', Push__Fcm__PrivateKeyPem: rsaPublic }, 'FCM requires a valid RSA private key'],
]) test(name + ' fails startup without fallback', () => {
  const result = spawnSync('dotnet', ['bin/Debug/net10.0/Swoovr.Api.dll'], {
    cwd, timeout: 10000, encoding: 'utf8', env: { PATH: process.env.PATH, HOME: process.env.HOME,
      ASPNETCORE_ENVIRONMENT: 'Production', DOTNET_ENVIRONMENT: 'Production', ASPNETCORE_URLS: 'http://127.0.0.1:5099', ...config },
  });
  assert.notEqual(result.status, 0);
  assert.ok((result.stdout + result.stderr).includes(expected), name + ' did not fail for the expected configuration error');
});
