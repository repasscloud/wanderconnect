// Local development ONLY: applies the parts of the three Swoovr realms that a partial
// realm import cannot express (built-in flow requirements, execution configs, required
// actions). Idempotent; refuses anything that does not look like a local -dev realm.
import { localRealmApi } from './provision.mjs';

export const realms = ['swoovr-members-dev', 'swoovr-venues-dev', 'swoovr-staff-dev'];
// RFC 8176 authentication method references emitted by the AMR mapper (D31 evidence).
export const amrReferences = { 'auth-username-password-form': 'pwd', 'auth-otp-form': 'otp', 'webauthn-authenticator': 'hwk' };

export async function applyRealm(api, name) {
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
    const config = { alias: 'swoovr-amr-' + reference + '-' + providerId, config: { 'default.reference.value': reference } };
    if (!execution.authenticationConfig) {
      await api('/authentication/executions/' + execution.id + '/config', 'POST', config);
      changes.push('amr ' + reference + ' on ' + providerId);
      continue;
    }
    const current = await api('/authentication/config/' + execution.authenticationConfig);
    if (current.alias !== config.alias) throw new Error('Unmanaged authentication config on ' + providerId);
    if (current.config?.['default.reference.value'] !== reference) {
      await api('/authentication/config/' + current.id, 'PUT', { ...config, id: current.id });
      changes.push('amr ' + reference + ' on ' + providerId);
    }
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
