// Local development ONLY: applies the parts of the three Swoovr realms that a partial
// realm import cannot express (built-in flow requirements, execution configs, required
// actions). Idempotent; refuses anything that does not look like a local -dev realm.
import { readFile } from 'node:fs/promises';
import { localRealmApi } from './provision.mjs';

export const realms = ['swoovr-members-dev', 'swoovr-venues-dev', 'swoovr-staff-dev'];
// RFC 8176 authentication method references emitted by the AMR mapper (D31 evidence).
export const amrReferences = { 'auth-username-password-form': 'pwd', 'auth-otp-form': 'otp', 'webauthn-authenticator': 'hwk' };
// Keycloak only reports a reference while the authentication is younger than maxAge (default 0,
// i.e. the same second). Match the 12-hour SSO session; privileged freshness uses auth_time.
export const amrMaxAgeSeconds = '43200';

// The declarative user profile from the generated import (scripts/keycloak_realms.py), so field order and
// placeholders follow the theme without recreating the local volume.
export async function generatedProfile(name, read = path => readFile(path, 'utf8')) {
  const realm = JSON.parse(await read(new URL(`../../containers/keycloak/realms/${name}.json`, import.meta.url)));
  return JSON.parse(realm.components['org.keycloak.userprofile.UserProfileProvider'][0].config['kc.user.profile.config'][0]);
}
// Public clients from the generated import (the members app, no secret). The import is IGNORE_EXISTING, so a
// client added after the local volume was created is missing; confidential clients carry `${ENV}` secret
// placeholders that only an import resolves, so they still need the volume recreated.
export async function generatedPublicClients(name, read = path => readFile(path, 'utf8')) {
  const realm = JSON.parse(await read(new URL(`../../containers/keycloak/realms/${name}.json`, import.meta.url)));
  return realm.clients.filter(c => c.publicClient && !('secret' in c));
}
const profileShape = profile => JSON.stringify(profile.attributes.map(a => [a.name, a.annotations ?? {}, a.required ?? null, a.permissions]));

export async function applyRealm(api, name, profile = generatedProfile, publicClients = generatedPublicClients) {
  const realm = await api('');
  if (realm.realm !== name || realm.attributes?.['swoovr.environment'] !== 'local-development-only')
    throw new Error('Expected a local Swoovr development realm; refusing to configure ' + name);
  const changes = [];
  // D40: open registration requires the "I'm 18 or over" checkbox. The built-in terms
  // step records the attestation time only while the TERMS_AND_CONDITIONS action is enabled;
  // as a default action it also asks brokered (Apple/Google) accounts before first sign-in.
  const terms = realm.attributes['swoovr.registrationTerms'] === 'REQUIRED';
  const registration = await api('/authentication/flows/registration/executions');
  const termsStep = registration.find(e => e.providerId === 'registration-terms-and-conditions');
  if (!termsStep) throw new Error('Registration terms step is missing');
  const wanted = terms ? 'REQUIRED' : 'DISABLED';
  if (termsStep.requirement !== wanted) {
    await api('/authentication/flows/registration/executions', 'PUT', { id: termsStep.id, requirement: wanted });
    changes.push('registration terms ' + wanted);
  }
  const action = await api('/authentication/required-actions/TERMS_AND_CONDITIONS');
  if (action.enabled !== terms || action.defaultAction !== terms) {
    await api('/authentication/required-actions/TERMS_AND_CONDITIONS', 'PUT', { ...action, enabled: terms, defaultAction: terms });
    changes.push('terms action ' + (terms ? 'default' : 'disabled'));
  }
  const browser = await api('/authentication/flows/browser/executions');
  for (const [providerId, reference] of Object.entries(amrReferences)) {
    const execution = browser.find(e => e.providerId === providerId);
    if (!execution) throw new Error('Browser flow execution is missing: ' + providerId);
    const config = { alias: 'swoovr-amr-' + reference + '-' + providerId, config: { 'default.reference.value': reference, 'default.reference.maxAge': amrMaxAgeSeconds } };
    if (!execution.authenticationConfig) {
      await api('/authentication/executions/' + execution.id + '/config', 'POST', config);
      changes.push('amr ' + reference + ' on ' + providerId);
      continue;
    }
    const current = await api('/authentication/config/' + execution.authenticationConfig);
    if (current.alias !== config.alias) throw new Error('Unmanaged authentication config on ' + providerId);
    if (current.config?.['default.reference.value'] !== reference || current.config?.['default.reference.maxAge'] !== amrMaxAgeSeconds) {
      await api('/authentication/config/' + current.id, 'PUT', { ...config, id: current.id });
      changes.push('amr ' + reference + ' on ' + providerId);
    }
  }
  const wantedProfile = await profile(name);
  const currentProfile = await api('/users/profile');
  if (profileShape(currentProfile) !== profileShape(wantedProfile)) {
    await api('/users/profile', 'PUT', wantedProfile);
    changes.push('user profile');
  }
  for (const client of await publicClients(name)) {
    const existing = await api('/clients?clientId=' + encodeURIComponent(client.clientId));
    if (existing.length) continue; // Never overwrite a client someone may have changed in the console.
    await api('/clients', 'POST', client);
    changes.push('client ' + client.clientId);
  }
  // Confidential clients keep their secret; only their redirect and post-logout addresses follow the import (slice Q
  // moved the staff client to the separate admin app, D136).
  const generated = JSON.parse(await readFile(new URL(`../../containers/keycloak/realms/${name}.json`, import.meta.url), 'utf8'));
  for (const wanted of generated.clients.filter(c => 'secret' in c)) {
    const [existing] = await api('/clients?clientId=' + encodeURIComponent(wanted.clientId));
    const logout = wanted.attributes?.['post.logout.redirect.uris'];
    if (!existing?.id || JSON.stringify(existing.redirectUris) === JSON.stringify(wanted.redirectUris) && existing.attributes?.['post.logout.redirect.uris'] === logout) continue;
    await api('/clients/' + existing.id, 'PUT', { ...existing, redirectUris: wanted.redirectUris, attributes: { ...existing.attributes, 'post.logout.redirect.uris': logout } });
    changes.push('redirects ' + wanted.clientId);
  }
  return changes;
}

export async function applyAll({ open = realm => localRealmApi({ realm }) } = {}) {
  const result = {};
  for (const name of realms) result[name] = await applyRealm(await open(name), name);
  return result;
}

if (process.argv[1]?.endsWith('/realms.mjs')) applyAll().then(result => {
  for (const [name, changes] of Object.entries(result)) console.log(name + ': ' + (changes.length ? changes.join(', ') : 'already configured'));
}).catch(error => { console.error('Realm configuration failed: ' + error.message); process.exitCode = 1; });
