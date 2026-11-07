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
  // Q126 A: members change their email through Keycloak's update-email step (Keycloak runs with
  // --features=update-email; the API then re-checks the address). Never a default action.
  if (terms) {
    const update = await api('/authentication/required-actions/UPDATE_EMAIL');
    if (!update.enabled || update.defaultAction) {
      await api('/authentication/required-actions/UPDATE_EMAIL', 'PUT', { ...update, enabled: true, defaultAction: false });
      changes.push('update-email enabled');
    }
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

// Q61 (D169): the API's service-account client for account deletion, members realm only, holding nothing but
// realm-management view-users and manage-users. Its secret comes from .local/docker.env (SWOOVR_MEMBERS_API_ADMIN_SECRET,
// created by `./scripts/docker-local.sh init`). It isn't in the generated import, which allows no service accounts for
// sign-in clients; hosted environments configure it the same way, outside these local scripts.
export const accountsClientId = 'swoovr-api-accounts';
export const accountsClientRoles = ['view-users', 'manage-users'];

export async function applyAccountsClient(api, secret) {
  if (!secret) return ['accounts client skipped (run ./scripts/docker-local.sh init)'];
  const changes = [];
  let [client] = await api('/clients?clientId=' + accountsClientId);
  const wanted = {
    clientId: accountsClientId, name: 'Swoovr API account deletion', enabled: true, protocol: 'openid-connect', publicClient: false,
    clientAuthenticatorType: 'client-secret', secret, serviceAccountsEnabled: true, standardFlowEnabled: false,
    implicitFlowEnabled: false, directAccessGrantsEnabled: false, redirectUris: [], webOrigins: [],
  };
  if (!client) {
    await api('/clients', 'POST', wanted);
    [client] = await api('/clients?clientId=' + accountsClientId);
    changes.push('client ' + accountsClientId);
  } else if ((await api('/clients/' + client.id + '/client-secret')).value !== secret) {
    await api('/clients/' + client.id, 'PUT', { ...client, secret });
    changes.push('secret ' + accountsClientId);
  }
  const user = await api('/clients/' + client.id + '/service-account-user');
  const [management] = await api('/clients?clientId=realm-management');
  const held = new Set((await api('/users/' + user.id + '/role-mappings/clients/' + management.id)).map(r => r.name));
  const missing = [];
  for (const name of accountsClientRoles) if (!held.has(name)) missing.push(await api('/clients/' + management.id + '/roles/' + name));
  if (missing.length) {
    await api('/users/' + user.id + '/role-mappings/clients/' + management.id, 'POST', missing);
    changes.push('roles ' + missing.map(r => r.name).join(' '));
  }
  return changes;
}

// #196/#193: staff browser OTP is required even when no credential exists, so Keycloak runs enrolment.
// Brokered staff also need the equivalent required post-broker flow in the configured Entra provider.
export async function applyStaffMfa(api, name) {
  if (name !== 'swoovr-staff-dev') return [];
  const executions = await api('/authentication/flows/browser/executions');
  const secondFactor = executions.find(e => e.authenticationFlow && e.displayName === 'Browser - Conditional 2FA');
  if (!secondFactor) throw new Error('Staff second-factor flow is missing; refusing to weaken authentication');
  const changes = [];
  // Keep native conditional dispatch: making this alternative-credential subflow REQUIRED executes it
  // before a user is identified on 26.8.1. The pinned provider condition queues native enrolment when needed.
  if (secondFactor.requirement !== 'CONDITIONAL') {
    await api('/authentication/flows/browser/executions', 'PUT', { id: secondFactor.id, requirement: 'CONDITIONAL' });
    changes.push('staff conditional second factor');
  }
  const start = executions.indexOf(secondFactor);
  for (const execution of executions.slice(start + 1)) {
    if (Number.isInteger(secondFactor.level) && execution.level <= secondFactor.level) break;
    if (execution.providerId?.startsWith('conditional-') && execution.requirement !== 'REQUIRED') {
      await api('/authentication/flows/browser/executions', 'PUT', { id: execution.id, requirement: 'REQUIRED' });
      changes.push('staff condition enabled ' + execution.providerId);
    }
  }
  const realm = await api('');
  if (realm.realm !== name || realm.attributes?.['swoovr.environment'] !== 'local-development-only') throw new Error('Expected local staff realm');
  if (realm.attributes['swoovr.mfa.required'] !== 'true') {
    await api('', 'PUT', { ...realm, attributes: { ...realm.attributes, 'swoovr.mfa.required': 'true' } });
    changes.push('mandatory staff enrolment policy');
  }
  const action = await api('/authentication/required-actions/CONFIGURE_TOTP');
  if (!action.enabled || action.defaultAction) {
    await api('/authentication/required-actions/CONFIGURE_TOTP', 'PUT', { ...action, enabled: true, defaultAction: false });
    changes.push('native staff OTP action');
  }
  return changes;
}

// #193: account-console credential deletion cannot bypass the API's enforced-MFA policy.
// Application-initiated native actions remain available through the Swoovr clients.
export async function applyMfaSelfServiceBoundary(api, name) {
  const changes = [];
  if (!['swoovr-members-dev', 'swoovr-staff-dev'].includes(name)) return changes;
  const realm = await api('');
  if (realm.realm !== name || realm.attributes?.['swoovr.environment'] !== 'local-development-only') throw new Error('Expected local realm for MFA configuration');
  const attributes = { ...realm.attributes };
  if (name === 'swoovr-staff-dev') attributes['swoovr.mfa.required'] = 'true';
  const listeners = [...new Set([...(realm.eventsListeners ?? ['jboss-logging']), 'swoovr-mfa-audit'])];
  if (JSON.stringify(attributes) !== JSON.stringify(realm.attributes) || JSON.stringify(listeners) !== JSON.stringify(realm.eventsListeners)) {
    await api('', 'PUT', { ...realm, attributes, eventsListeners: listeners }); changes.push('MFA policy and audit');
  }
  for (const id of ['account-console', 'account']) {
    const [client] = await api('/clients?clientId=' + id);
    if (client?.enabled !== false && client?.id) { await api('/clients/' + client.id, 'PUT', { ...client, enabled: false }); changes.push('managed MFA self-service ' + id); }
  }
  const recovery = await api('/authentication/required-actions/CONFIGURE_RECOVERY_AUTHN_CODES');
  if (!recovery.enabled || recovery.defaultAction) {
    await api('/authentication/required-actions/CONFIGURE_RECOVERY_AUTHN_CODES', 'PUT', { ...recovery, enabled: true, defaultAction: false }); changes.push('native recovery codes');
  }
  const executions = await api('/authentication/flows/browser/executions');
  const otp = executions.find(e => e.providerId === 'auth-otp-form');
  const recoveryForm = executions.find(e => e.providerId === 'auth-recovery-authn-code-form');
  if (!otp || !recoveryForm) throw new Error('Native recovery-code feature is unavailable; enable it before configuring MFA');
  for (const execution of [otp, recoveryForm]) {
    if (execution.requirement !== 'ALTERNATIVE') await api('/authentication/flows/browser/executions', 'PUT', { id: execution.id, requirement: 'ALTERNATIVE' });
  }
  const config = { alias: 'swoovr-amr-recovery-code', config: { 'default.reference.value': 'recovery-code', 'default.reference.maxAge': amrMaxAgeSeconds } };
  if (!recoveryForm.authenticationConfig) await api('/authentication/executions/' + recoveryForm.id + '/config', 'POST', config);
  else {
    const existing = await api('/authentication/config/' + recoveryForm.authenticationConfig);
    if (existing.alias !== config.alias) throw new Error('Unmanaged recovery-code AMR configuration');
    await api('/authentication/config/' + existing.id, 'PUT', { ...config, id: existing.id });
  }
  return changes;
}

async function localSecret(key) {
  const env = await readFile('.local/docker.env', 'utf8').catch(() => '');
  return env.split('\n').find(line => line.startsWith(key + '='))?.split('=')[1];
}

export async function applyAll({ open = realm => localRealmApi({ realm }), secret = () => localSecret('SWOOVR_MEMBERS_API_ADMIN_SECRET'), managedMfa = process.argv.includes('--managed-mfa') } = {}) {
  const result = {};
  for (const name of realms) {
    const api = await open(name);
    result[name] = [...await applyRealm(api, name), ...await applyStaffMfa(api, name)];
    if (managedMfa) result[name].push(...await applyMfaSelfServiceBoundary(api, name));
    if (name === 'swoovr-members-dev') result[name].push(...await applyAccountsClient(api, await secret()));
  }
  return result;
}

if (process.argv[1]?.endsWith('/realms.mjs')) applyAll().then(result => {
  for (const [name, changes] of Object.entries(result)) console.log(name + ': ' + (changes.length ? changes.join(', ') : 'already configured'));
}).catch(error => { console.error('Realm configuration failed: ' + error.message); process.exitCode = 1; });
