#!/usr/bin/env python3
"""Generate and check the three local Keycloak realm imports (design D31-D41).

Local development ONLY: loopback redirect URIs, a mail sink and `-dev` realm names.
Client secrets are `${ENV}` placeholders resolved by Keycloak at import from
.local/docker.env; no secret is ever written here.
"""
import argparse
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root / 'containers/keycloak/realms'
web = 'http://localhost:5081'
api_audience = 'swoovr-api'

# CONFIRMED D37 (revised): 3 failures -> 15-minute temporary lock; never permanent.
brute_force = {
    'bruteForceProtected': True, 'permanentLockout': False, 'maxTemporaryLockouts': 0,
    'failureFactor': 3, 'waitIncrementSeconds': 900, 'maxFailureWaitSeconds': 900,
    'minimumQuickLoginWaitSeconds': 60, 'quickLoginCheckMilliSeconds': 1000,
    'maxDeltaTimeSeconds': 43200, 'bruteForceStrategy': 'MULTIPLE',
}

def attribute(name, display, required, extra=None):
    value = {'name': name, 'displayName': display,
             'permissions': {'view': ['admin', 'user'], 'edit': ['admin', 'user']},
             'validations': {}, 'multivalued': False}
    if required:
        value['required'] = {'roles': ['user']}
    value.update(extra or {})
    return value


def user_profile():
    attributes = [
        attribute('username', '${username}', False, {'validations': {'length': {'min': 3, 'max': 255}, 'username-prohibited-characters': {}, 'up-username-not-idn-homograph': {}}, 'permissions': {'view': ['admin', 'user'], 'edit': ['admin']}}),
        attribute('email', '${email}', True, {'validations': {'email': {}, 'length': {'max': 255}}}),
        attribute('firstName', '${firstName}', True, {'validations': {'length': {'max': 255}, 'person-name-prohibited-characters': {}}}),
    ]
    # Written by the built-in registration terms step (the 18+ checkbox, D40); undeclared attributes are dropped.
    attributes.append(attribute('terms_and_conditions', 'Adult attestation time', False, {'permissions': {'view': ['admin'], 'edit': ['admin']}}))
    return {'attributes': attributes, 'groups': [], 'unmanagedAttributePolicy': None}


def web_client(client_id, secret_env, path=''):
    return {
        'clientId': client_id, 'name': 'Swoovr web (local)', 'protocol': 'openid-connect', 'enabled': True,
        'publicClient': False, 'clientAuthenticatorType': 'client-secret', 'secret': '${' + secret_env + '}',
        'standardFlowEnabled': True, 'implicitFlowEnabled': False, 'directAccessGrantsEnabled': False,
        'serviceAccountsEnabled': False, 'frontchannelLogout': False,
        'redirectUris': [web + path + '/signin-oidc'], 'webOrigins': [],
        'attributes': {'pkce.code.challenge.method': 'S256', 'post.logout.redirect.uris': web + path + '/signout-callback-oidc',
                       'standard.token.exchange.enabled': 'false', 'oauth2.device.authorization.grant.enabled': 'false'},
        'fullScopeAllowed': False,
        'defaultClientScopes': ['basic', 'profile', 'email', 'acr', 'web-origins'], 'optionalClientScopes': [],
        'protocolMappers': [
            {'name': 'swoovr-amr', 'protocol': 'openid-connect', 'protocolMapper': 'oidc-amr-mapper',
             'config': {'id.token.claim': 'true', 'access.token.claim': 'true', 'introspection.token.claim': 'true'}},
            {'name': 'swoovr-identity-provider', 'protocol': 'openid-connect', 'protocolMapper': 'oidc-usersessionmodel-note-mapper',
             'config': {'user.session.note': 'identity_provider', 'claim.name': 'swoovr_idp', 'jsonType.label': 'String',
                        'id.token.claim': 'true', 'access.token.claim': 'true', 'introspection.token.claim': 'true'}},
            # D40: the registration checkbox records a timestamp; the API copies it once and owns age status.
            {'name': 'swoovr-adult-attested-at', 'protocol': 'openid-connect', 'protocolMapper': 'oidc-usermodel-attribute-mapper',
             'config': {'user.attribute': 'terms_and_conditions', 'claim.name': 'swoovr_adult_attested_at', 'jsonType.label': 'String',
                        'id.token.claim': 'true', 'access.token.claim': 'true', 'introspection.token.claim': 'true', 'multivalued': 'false'}},
            {'name': 'swoovr-api-audience', 'protocol': 'openid-connect', 'protocolMapper': 'oidc-audience-mapper',
             'config': {'included.custom.audience': api_audience, 'access.token.claim': 'true', 'id.token.claim': 'false', 'introspection.token.claim': 'true'}},
        ],
    }


def realm(name, display, *, registration, clients, passkeys):
    value = {
        'realm': name, 'enabled': True, 'displayName': display, 'sslRequired': 'external',
        'registrationAllowed': registration, 'registrationEmailAsUsername': True, 'editUsernameAllowed': False,
        'verifyEmail': True, 'resetPasswordAllowed': True, 'loginWithEmailAllowed': True, 'duplicateEmailsAllowed': False,
        'rememberMe': False, 'loginTheme': 'swoovr', 'internationalizationEnabled': False,
        'passwordPolicy': 'length(12) and maxLength(128) and notUsername and notEmail',
        'ssoSessionIdleTimeout': 43200, 'ssoSessionMaxLifespan': 43200,
        'accessTokenLifespan': 300, 'revokeRefreshToken': True, 'refreshTokenMaxReuse': 0,
        'smtpServer': {'host': 'mailpit', 'port': '1025', 'from': 'no-reply@swoovr.app', 'fromDisplayName': 'Swoovr (local mail sink)', 'ssl': 'false', 'starttls': 'false', 'auth': 'false'},
        # Applied by scripts/auth/realms.mjs: built-in flows cannot be partially imported.
        'attributes': {'swoovr.environment': 'local-development-only', 'swoovr.registrationTerms': 'REQUIRED' if registration else 'DISABLED'},
        'clients': clients, 'users': [], 'identityProviders': [],
        'components': {'org.keycloak.userprofile.UserProfileProvider': [{
            'providerId': 'declarative-user-profile', 'subComponents': {},
            'config': {'kc.user.profile.config': [json.dumps(user_profile(), separators=(',', ':'))]}}]},
        **brute_force,
    }
    if passkeys:
        value.update({'webAuthnPolicyPasswordlessRpId': 'localhost', 'webAuthnPolicyPasswordlessRpEntityName': 'Swoovr', 'webAuthnPolicyPasswordlessResidentKey': 'required',
                      'webAuthnPolicyPasswordlessUserVerificationRequirement': 'required',
                      'webAuthnPolicyPasswordlessPasskeysEnabled': True})
    return value


realms = {
    'swoovr-members-dev': realm('swoovr-members-dev', 'Swoovr', registration=True, passkeys=True,
                                clients=[web_client('swoovr-web', 'SWOOVR_MEMBERS_WEB_SECRET')]),
    # Venue accounts are created by invitation; no self-registration, no social providers (D31).
    'swoovr-venues-dev': realm('swoovr-venues-dev', 'Swoovr venues', registration=False, passkeys=False,
                               clients=[web_client('swoovr-venue-web', 'SWOOVR_VENUES_WEB_SECRET', '/venues')]),
    # Employees broker from Entra ID once a tenant registration exists (D32); root admin needs OTP (D36).
    'swoovr-staff-dev': realm('swoovr-staff-dev', 'Swoovr staff', registration=False, passkeys=False,
                              clients=[web_client('swoovr-admin-web', 'SWOOVR_STAFF_WEB_SECRET', '/admin')]),
}
realms['swoovr-staff-dev']['resetPasswordAllowed'] = False  # Entra owns employee passwords; root admin is reset by master admin.


def check(name, value):
    assert value['identityProviders'] == [] or name == 'swoovr-members-dev', (name, 'social providers outside members realm')
    assert all(value[k] == v for k, v in brute_force.items()), (name, 'brute force')
    assert value['users'] == [], (name, 'no committed users')
    for client in value['clients']:
        assert client['secret'].startswith('${') and client['secret'].endswith('}'), (name, 'secret must be a placeholder')
        assert client['standardFlowEnabled'] and not client['implicitFlowEnabled'], (name, 'code flow only')
        assert not client['directAccessGrantsEnabled'] and not client['serviceAccountsEnabled'], (name, 'no password grant or service account')
        assert client['attributes']['pkce.code.challenge.method'] == 'S256', (name, 'PKCE')
        assert client['attributes']['standard.token.exchange.enabled'] == 'false', (name, 'D35: no token exchange')
        assert all(uri.startswith(web + '/') for uri in client['redirectUris']), (name, 'loopback redirect only')
        assert not client['fullScopeAllowed'], (name, 'no full scope')
    assert (value['attributes'].get('swoovr.registrationTerms') == 'REQUIRED') == value['registrationAllowed'], (name, 'D40: open registration needs the 18+ checkbox')


parser = argparse.ArgumentParser()
parser.add_argument('--write', action='store_true')
args = parser.parse_args()
for name, value in realms.items():
    check(name, value)
    text = json.dumps(value, indent=2, ensure_ascii=False) + '\n'
    path = out / f'{name}.json'
    if args.write:
        out.mkdir(parents=True, exist_ok=True)
        path.write_text(text)
    else:
        assert path.exists() and path.read_text() == text, f'{path.relative_to(root)} drifted; run --write'
print(f'PASS: {len(realms)} local Keycloak realm imports (temporary lockout, code+PKCE only, no token exchange, social only in members, 18+ registration checkbox).')
