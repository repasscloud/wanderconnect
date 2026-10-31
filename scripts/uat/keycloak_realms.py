#!/usr/bin/env python3
"""Render DISABLED UAT realm preparations to a new, explicit outside-repository directory.

No network, env-file reads, secret expansion, import or provider activation occurs.
Canonical development JSON is read as public baseline data, never executed/imported.
"""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
from urllib.parse import urlsplit
import uuid

ROOT = Path(__file__).resolve().parents[2]
MEMBERS = 'https://uat.swoovr.app'
AUTH = 'https://auth.uat.swoovr.app'
RP = 'uat.swoovr.app'
ENTRA_ALIAS = 'entra'


def hosted_origin(value):
    parsed = urlsplit(value)
    host = parsed.hostname or ''
    labels = host.split('.')
    if (parsed.scheme != 'https' or parsed.username or parsed.password or parsed.port is not None
            or parsed.path or parsed.query or parsed.fragment or '*' in value
            or not host.endswith('.uat.swoovr.app') or host != host.lower()
            or not all(re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?', label) for label in labels)
            or value != 'https://' + host):
        raise ValueError('Portal origins must be exact HTTPS subdomains of uat.swoovr.app without ports, paths or wildcards.')
    return value


def execution(authenticator, requirement='REQUIRED', config=None):
    result = {'authenticator': authenticator, 'authenticatorFlow': False,
              'requirement': requirement, 'priority': 10, 'userSetupAllowed': False}
    if config:
        result['authenticatorConfig'] = config
    return result


def subflow(alias, requirement='REQUIRED'):
    return {'flowAlias': alias, 'authenticatorFlow': True, 'requirement': requirement,
            'priority': 10, 'userSetupAllowed': False}


def flow(alias, steps, *, top=True, provider='basic-flow'):
    for index, step in enumerate(steps):
        step['priority'] = (index + 1) * 10
    return {'alias': alias, 'description': 'Swoovr UAT preparation; activation requires isolated acceptance',
            'providerId': provider, 'topLevel': top, 'builtIn': False,
            'authenticationExecutions': steps}


def local_flows(value, member):
    """Explicit copies: do not rely on local-only applyRealm mutating built-in flows."""
    browser = 'swoovr-uat-browser'
    forms = 'swoovr-uat-forms'
    otp = 'swoovr-uat-optional-otp'
    value['browserFlow'] = browser
    value['authenticationFlows'] = [
        flow(browser, [execution('auth-cookie', 'ALTERNATIVE'), subflow(forms, 'ALTERNATIVE')]),
        flow(forms, [execution('auth-username-password-form', config='swoovr-amr-pwd'), subflow(otp, 'CONDITIONAL')], top=False),
        flow(otp, [execution('conditional-user-configured'), execution('auth-otp-form', config='swoovr-amr-otp')], top=False),
    ]
    value['authenticatorConfig'] = [
        {'alias': 'swoovr-amr-' + reference,
         'config': {'default.reference.value': reference, 'default.reference.maxAge': '43200'}}
        for reference in ('pwd', 'otp')
    ]
    if member:
        registration = 'swoovr-uat-registration'
        registration_forms = 'swoovr-uat-registration-forms'
        value['registrationFlow'] = registration
        value['authenticationFlows'] += [
            flow(registration, [{**subflow(registration_forms), 'authenticator': 'registration-page-form'}]),
            flow(registration_forms, [execution('registration-user-creation'), execution('registration-profile-action'),
                                     execution('registration-password-action'), execution('registration-terms-and-conditions')],
                 top=False, provider='form-flow'),
        ]
        value['requiredActions'] = [
            {'alias': 'TERMS_AND_CONDITIONS', 'name': 'Terms and Conditions',
             'providerId': 'TERMS_AND_CONDITIONS', 'enabled': True, 'defaultAction': True, 'priority': 10},
            {'alias': 'webauthn-register-passwordless', 'name': 'Register passkey',
             'providerId': 'webauthn-register-passwordless', 'enabled': True, 'defaultAction': False, 'priority': 20},
        ]


def staff_flows(value, tenant, client_id):
    browser = 'swoovr-uat-staff-entra-only'
    first = 'swoovr-uat-staff-first-broker'
    deny = 'swoovr-uat-staff-deny-local'
    value.update(browserFlow=browser, resetCredentialsFlow=deny, directGrantFlow=deny,
                 registrationFlow=deny, clientAuthenticationFlow='clients',
                 registrationAllowed=False, resetPasswordAllowed=False, loginWithEmailAllowed=False,
                 webAuthnPolicyPasswordlessPasskeysEnabled=False)
    value['authenticationFlows'] = [
        flow(browser, [execution('identity-provider-redirector', config='swoovr-uat-entra-redirect')]),
        # Do not merge/link an existing local identity merely because its email matches.
        flow(first, [execution('idp-create-user-if-unique')]),
        flow(deny, [execution('deny-access-authenticator')]),
    ]
    value['authenticatorConfig'] = [
        {'alias': 'swoovr-uat-entra-redirect', 'config': {'defaultProvider': ENTRA_ALIAS}}
    ]
    value['requiredActions'] = [
        {'alias': action, 'name': action, 'providerId': action,
         'enabled': False, 'defaultAction': False, 'priority': index * 10}
        for index, action in enumerate(('UPDATE_PASSWORD', 'CONFIGURE_TOTP', 'webauthn-register-passwordless'))
    ]
    authority = 'https://login.microsoftonline.com/' + tenant
    value['identityProviders'] = [{
        'alias': ENTRA_ALIAS, 'displayName': 'Sign in with Microsoft', 'providerId': 'oidc',
        'enabled': True, 'trustEmail': False, 'storeToken': False, 'addReadTokenRoleOnCreate': False,
        'authenticateByDefault': False, 'linkOnly': False, 'firstBrokerLoginFlowAlias': first,
        'config': {'clientId': client_id, 'clientSecret': '${SWOOVR_UAT_ENTRA_CLIENT_SECRET}',
                   'issuer': authority + '/v2.0', 'authorizationUrl': authority + '/oauth2/v2.0/authorize',
                   'tokenUrl': authority + '/oauth2/v2.0/token', 'jwksUrl': authority + '/discovery/v2.0/keys',
                   'useJwksUrl': 'true', 'validateSignature': 'true', 'disableUserInfo': 'true',
                   'clientAuthMethod': 'client_secret_post', 'defaultScope': 'openid profile email',
                   'syncMode': 'IMPORT'},
    }]


def render(admin_origin, venue_origin, tenant_id, client_id):
    admin_origin, venue_origin = hosted_origin(admin_origin), hosted_origin(venue_origin)
    if len({admin_origin, venue_origin, AUTH, MEMBERS}) != 4:
        raise ValueError('Admin, venue, authentication and member origins must be distinct.')
    tenant, client_id = str(uuid.UUID(tenant_id)), str(uuid.UUID(client_id))
    if tenant == str(uuid.UUID(int=0)) or client_id == str(uuid.UUID(int=0)):
        raise ValueError('Explicit nonzero Entra tenant/application UUIDs are required.')
    results = {}
    for kind in ('members', 'venues', 'staff'):
        name = 'swoovr-' + kind
        source = ROOT / 'containers/keycloak/realms' / (name + '-dev.json')
        value = copy.deepcopy(json.loads(source.read_text()))
        if (value.get('users') != [] or value.get('identityProviders') != [] or 'keys' in value
                or set(value.get('components', {})) != {'org.keycloak.userprofile.UserProfileProvider'}):
            raise ValueError('Canonical development baseline must contain no users or identity providers.')
        value.update(realm=name, enabled=False, sslRequired='all', smtpServer={}, users=[], identityProviders=[])
        value['attributes'] = {'swoovr.environment': 'uat-preparation-disabled',
                               'swoovr.registrationTerms': 'REQUIRED' if kind == 'members' else 'DISABLED'}
        for item in value['clients']:
            item['name'] = 'Swoovr UAT ' + item['clientId']
            if item['publicClient']:
                # D93's existing private-use scheme, pending verified hosted app links.
                continue
            origin = MEMBERS if kind == 'members' else admin_origin if kind == 'staff' else venue_origin
            item['secret'] = '${SWOOVR_UAT_' + kind.upper() + '_WEB_SECRET}'
            item['redirectUris'] = [origin + '/signin-oidc']
            item['attributes']['post.logout.redirect.uris'] = origin + '/signout-callback-oidc'
        if kind == 'members':
            value['webAuthnPolicyPasswordlessRpId'] = RP
        if kind == 'staff':
            staff_flows(value, tenant, client_id)
        else:
            local_flows(value, kind == 'members')
        results[name + '.json'] = json.dumps(value, indent=2, ensure_ascii=False) + '\n'
    manifest = {
        'schema': 'swoovr-keycloak-uat-preparation/1', 'keycloakVersion': '26.8.1',
        'realmsEnabled': False, 'memberOrigin': MEMBERS, 'authOriginDefault': AUTH,
        'adminOriginDefault': admin_origin, 'venueOriginDefault': venue_origin, 'passkeyRpId': RP,
        'entraBrokerCallback': AUTH + '/realms/swoovr-staff/broker/entra/endpoint',
        'runtimeSecretEnvironment': ['SWOOVR_UAT_MEMBERS_WEB_SECRET', 'SWOOVR_UAT_VENUES_WEB_SECRET',
                                     'SWOOVR_UAT_STAFF_WEB_SECRET', 'SWOOVR_UAT_ENTRA_CLIENT_SECRET'],
        'sha256': {name: hashlib.sha256(text.encode()).hexdigest() for name, text in results.items()},
        'activationGates': ['isolated pinned-version import/flow tests', 'MailerSend runtime secret/domain/DNS and real acceptance/delivery',
                            'root grant to an approved verified Entra subject and operator break-glass procedure', 'Entra tenant/app/Conditional Access/email evidence',
                            'separate members Apple/Google and email-link integration', 'host configuration/Q90'],
    }
    results['manifest.json'] = json.dumps(manifest, indent=2) + '\n'
    return results


def write_new_directory(output, files):
    raw = Path(output).expanduser()
    if not raw.is_absolute():
        raise ValueError('Output must be an explicit absolute outside-repository path.')
    target = raw.resolve()
    if raw.is_symlink() or target.exists() or not target.parent.is_dir():
        raise ValueError('Output must be a new directory with an existing parent; no overwrite/symlink output.')
    # Also reject the original checkout containing this .claude worktree, and other repos.
    for path in (target, *target.parents):
        if (path / '.git').exists() or path == ROOT:
            raise ValueError('Output must be outside every repository checkout.')
    if any(Path(name).name != name or name in ('.', '..') for name in files):
        raise ValueError('Output file names must be plain basenames.')
    target.mkdir(mode=0o700)
    try:
        for name, content in files.items():
            with (target / name).open('x', encoding='utf-8') as stream:
                os.chmod(target / name, 0o600)
                stream.write(content)
    except BaseException:
        for name in files:
            (target / name).unlink(missing_ok=True)
        target.rmdir()
        raise
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--admin-origin', required=True)
    parser.add_argument('--venue-origin', required=True)
    parser.add_argument('--entra-tenant-id', required=True, help='Actual single-tenant UUID; never common/organizations')
    parser.add_argument('--entra-client-id', required=True, help='Actual application UUID; not a secret')
    parser.add_argument('--output', required=True, help='New absolute directory outside every checkout')
    args = parser.parse_args()
    try:
        files = render(args.admin_origin, args.venue_origin, args.entra_tenant_id, args.entra_client_id)
        target = write_new_directory(args.output, files)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    print('Prepared three DISABLED UAT realms and manifest in ' + str(target) + '; no import or activation performed.')


if __name__ == '__main__':
    main()
