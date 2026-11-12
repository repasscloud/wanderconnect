#!/usr/bin/env python3
"""Idempotent UAT Keycloak reconciliation for the automated deployment (D362). Runs in the operator image.

Safe to repeat on every deployment. It never recreates, deletes or disables a realm, never touches a member or staff
user other than reading them, and never prints a secret. In order:

1. Admin credential: the master-realm service client `swoovr-deploy` (client credentials, secret generated on the
   host). If it is missing and the temporary bootstrap admin still works (fresh database), the temporary admin creates
   it. Its roles are limited to managing the three Swoovr realms plus reading master-realm users.
2. Staff realm: the `swoovr-zammad` public PKCE client (helpdesk SSO), and the `entra` identity broker synced to the
   configured tenant/application IDs and client secret.
3. Redirect URIs of the hosted web clients are verified against the UAT hostnames (refuses on drift, never rewrites).
4. The realms named in SWOOVR_KEYCLOAK_ENABLED_REALMS are enabled (only members and staff may be listed).
5. The temporary bootstrap admin is deleted once a personal master-realm admin with OTP exists; otherwise kept.
6. First administrator: reports whether SWOOVR_INITIAL_ADMIN_EMAIL belongs to exactly one enabled staff-realm user
   with a Keycloak-verified email and a linked Entra identity. Only then is its subject printed for the root grant.

Output: `SWOOVR_RECONCILE <key>=<value>` lines (states and IDs only).
"""
import os
import re
import sys
import urllib.parse
import uuid

from keycloak_bootstrap import Transport

AUTH = 'https://auth-uat.swoovr.app'
REALMS = ('swoovr-members', 'swoovr-venues', 'swoovr-staff')
ENABLEABLE = ('swoovr-members', 'swoovr-staff')  # No venue portal runs on UAT (D355); venues stays disabled.
DEPLOY_CLIENT = 'swoovr-deploy'
REALM_ROLES = ('view-realm', 'manage-realm', 'view-clients', 'manage-clients', 'view-users', 'manage-users',
               'view-identity-providers', 'manage-identity-providers')
MASTER_ROLES = ('view-users',)
WEB_CLIENTS = {('swoovr-members', 'swoovr-web'): 'https://uat.swoovr.app',
               ('swoovr-staff', 'swoovr-admin-web'): 'https://admin-uat.swoovr.app',
               ('swoovr-venues', 'swoovr-venue-web'): 'https://venues-uat.swoovr.app'}
HELPDESK = 'https://helpdesk-uat.swoovr.app'
BOOTSTRAP_ADMIN = 'swoovr-bootstrap'


def say(key, value):
    print(f'SWOOVR_RECONCILE {key}={value}', flush=True)


class Admin:
    """Admin REST calls with one bearer token; fixed diagnostics (path and status only, never bodies)."""

    def __init__(self, request, token):
        self.request, self.token = request, token

    def call(self, path, method='GET', data=None, expected=(200,)):
        code, body = self.request('/admin/realms' + path, method, data, self.token)
        if code not in expected:
            raise ValueError(f'Keycloak {method} /admin/realms{path.split("?")[0]} answered HTTP {code}.')
        return body

    def one(self, path, what):
        found = self.call(path)
        if not isinstance(found, list) or len(found) != 1:
            raise ValueError(f'Expected exactly one {what}.')
        return found[0]


def token(request, form):
    code, body = request('/realms/master/protocol/openid-connect/token', 'POST', form, form=True)
    return body['access_token'] if code == 200 and isinstance(body, dict) and body.get('access_token') else None


def bootstrap_token(request, env):
    password = env.get('KC_BOOTSTRAP_ADMIN_PASSWORD', '')
    if not password:
        return None
    return token(request, {'client_id': 'admin-cli', 'grant_type': 'password',
                           'username': env.get('KC_BOOTSTRAP_ADMIN_USERNAME', BOOTSTRAP_ADMIN), 'password': password})


def deploy_token(request, secret):
    return token(request, {'client_id': DEPLOY_CLIENT, 'grant_type': 'client_credentials', 'client_secret': secret})


def ensure_deploy_client(admin, secret):
    """Create or update the master service client, then grant only the per-realm management roles."""
    wanted = {'clientId': DEPLOY_CLIENT, 'name': 'Swoovr UAT deployment automation', 'enabled': True,
              'protocol': 'openid-connect', 'publicClient': False, 'clientAuthenticatorType': 'client-secret',
              'secret': secret, 'serviceAccountsEnabled': True, 'standardFlowEnabled': False,
              'implicitFlowEnabled': False, 'directAccessGrantsEnabled': False, 'fullScopeAllowed': False,
              'redirectUris': [], 'webOrigins': [], 'attributes': {'oauth2.device.authorization.grant.enabled': 'false',
                                                                    'standard.token.exchange.enabled': 'false'}}
    existing = admin.call('/master/clients?clientId=' + DEPLOY_CLIENT)
    if existing:
        admin.call('/master/clients/' + existing[0]['id'], 'PUT', {**existing[0], **wanted}, (204,))
    else:
        admin.call('/master/clients', 'POST', wanted, (201,))
    client = admin.one('/master/clients?clientId=' + DEPLOY_CLIENT, 'deployment client')
    user = admin.call('/master/clients/' + client['id'] + '/service-account-user')
    grants = [(realm + '-realm', REALM_ROLES) for realm in REALMS] + [('master-realm', MASTER_ROLES)]
    for management_client, names in grants:
        target = admin.one('/master/clients?clientId=' + management_client, management_client)
        roles = [admin.call('/master/clients/' + target['id'] + '/roles/' + name) for name in names]
        admin.call('/master/users/' + user['id'] + '/role-mappings/clients/' + target['id'], 'POST', roles, (204,))
        admin.call('/master/clients/' + client['id'] + '/scope-mappings/clients/' + target['id'], 'POST', roles, (204,))


def ensure_zammad_client(admin):
    """Same definition as the former `bin/swoovr zammad-sso` and scripts/zammad_local.py sso_client()."""
    client = {'clientId': 'swoovr-zammad', 'name': 'Swoovr helpdesk', 'enabled': True, 'publicClient': True,
              'clientAuthenticatorType': 'none', 'standardFlowEnabled': True, 'directAccessGrantsEnabled': False,
              'implicitFlowEnabled': False, 'serviceAccountsEnabled': False, 'frontchannelLogout': False,
              'fullScopeAllowed': False, 'redirectUris': [HELPDESK + '/auth/openid_connect/callback'],
              'webOrigins': [HELPDESK],
              'attributes': {'pkce.code.challenge.method': 'S256', 'post.logout.redirect.uris': HELPDESK + '/*',
                             'standard.token.exchange.enabled': 'false',
                             'oauth2.device.authorization.grant.enabled': 'false'}}
    existing = admin.call('/swoovr-staff/clients?clientId=swoovr-zammad')
    if existing:
        merged = {**existing[0], **client, 'attributes': {**existing[0].get('attributes', {}), **client['attributes']}}
        admin.call('/swoovr-staff/clients/' + existing[0]['id'], 'PUT', merged, (204,))
        return 'updated'
    admin.call('/swoovr-staff/clients', 'POST', client, (201,))
    return 'created'


def entra_config(tenant, client_id):
    tenant, client_id = str(uuid.UUID(tenant)), str(uuid.UUID(client_id))
    if uuid.UUID(int=0) in (uuid.UUID(tenant), uuid.UUID(client_id)):
        raise ValueError('Entra tenant and client IDs must be real nonzero UUIDs.')
    authority = 'https://login.microsoftonline.com/' + tenant
    return {'clientId': client_id, 'issuer': authority + '/v2.0', 'authorizationUrl': authority + '/oauth2/v2.0/authorize',
            'tokenUrl': authority + '/oauth2/v2.0/token', 'jwksUrl': authority + '/discovery/v2.0/keys'}


def sync_entra(admin, env):
    """Keycloak side only: the Entra app registration itself is never changed from here."""
    secret = env.get('SWOOVR_UAT_ENTRA_CLIENT_SECRET', '')
    if len(secret.strip()) < 8:
        raise ValueError('The Entra client secret is missing.')
    wanted = entra_config(env.get('SWOOVR_ENTRA_TENANT_ID', ''), env.get('SWOOVR_ENTRA_CLIENT_ID', ''))
    current = admin.call('/swoovr-staff/identity-provider/instances/entra')
    config = dict(current.get('config', {}))
    drift = sorted(key for key, value in wanted.items() if config.get(key) != value)
    if current.get('trustEmail') is not False or current.get('storeToken') is not False or current.get('enabled') is not True:
        drift.append('flags')
    config.update(wanted)
    config['clientSecret'] = secret  # Keycloak never returns it, so it is always re-applied from the host value.
    admin.call('/swoovr-staff/identity-provider/instances/entra', 'PUT',
               {**current, 'enabled': True, 'trustEmail': False, 'storeToken': False, 'config': config}, (204,))
    return 'corrected:' + ','.join(drift) if drift else 'in-sync'


def verify_web_clients(admin):
    for (realm, client_id), origin in WEB_CLIENTS.items():
        client = admin.one(f'/{realm}/clients?clientId={client_id}', client_id)
        if client.get('redirectUris') != [origin + '/signin-oidc'] or \
                client.get('attributes', {}).get('post.logout.redirect.uris') != origin + '/signout-callback-oidc':
            raise ValueError(f'{client_id} callbacks do not match {origin}; fix the realm (no automatic rewrite).')


def enable_realms(admin, wanted):
    states = []
    for realm in wanted:
        representation = admin.call('/' + realm)
        if representation.get('enabled') is True:
            states.append(realm + ':enabled')
            continue
        representation['enabled'] = True
        admin.call('/' + realm, 'PUT', representation, (204,))
        states.append(realm + ':now-enabled')
    return ','.join(states) or 'none'


def retire_bootstrap_admin(admin, request, env):
    """Delete the temporary master admin only when a personal admin with OTP and the admin role exists."""
    found = admin.call('/master/users?exact=true&username=' + BOOTSTRAP_ADMIN)
    if not found:
        return 'absent'
    people = [u for u in admin.call('/master/users?max=100')
              if u.get('username') != BOOTSTRAP_ADMIN and not u.get('username', '').startswith('service-account-')]
    for user in people:
        roles = admin.call('/master/users/' + user['id'] + '/role-mappings/realm')
        if user.get('enabled') and user.get('totp') and any(r.get('name') == 'admin' for r in roles or []):
            bootstrap = bootstrap_token(request, env)
            if not bootstrap:
                return 'present-unusable'
            Admin(request, bootstrap).call('/master/users/' + found[0]['id'], 'DELETE', expected=(204,))
            return 'deleted'
    return 'kept-no-personal-admin'


def admin_candidate(admin, email):
    """Verified identity only: never grants on an address match alone."""
    if not re.fullmatch(r'[^@\s]+@[^@\s]+\.[^@\s]+', email or ''):
        return 'not-configured', None
    users = [u for u in admin.call('/swoovr-staff/users?exact=true&email=' + urllib.parse.quote(email))
             if (u.get('email') or '').lower() == email.lower()]
    if not users:
        return 'pending-sign-in', None
    if len(users) > 1:
        return 'ambiguous', None
    user = users[0]
    if not user.get('enabled'):
        return 'disabled', None
    if user.get('emailVerified') is not True:
        return 'unverified-email', None
    links = admin.call('/swoovr-staff/users/' + user['id'] + '/federated-identity')
    if not any(link.get('identityProvider') == 'entra' for link in links or []):
        return 'not-linked', None
    return 'ready', user['id']


def wanted_realms(env):
    names = [name.strip() for name in env.get('SWOOVR_KEYCLOAK_ENABLED_REALMS', '').split(',') if name.strip()]
    if any(name not in ENABLEABLE for name in names):
        raise ValueError('SWOOVR_KEYCLOAK_ENABLED_REALMS may list only swoovr-members and swoovr-staff.')
    return names


def reconcile(request, env, admin_only=False):
    secret = env.get('SWOOVR_KC_DEPLOY_CLIENT_SECRET', '')
    if len(secret) < 32:
        raise ValueError('The deployment client secret is missing (env/automation.env).')
    access = deploy_token(request, secret)
    if not access:
        bootstrap = bootstrap_token(request, env)
        if not bootstrap:
            raise ValueError('No Keycloak deployment credential: swoovr-deploy is missing and the temporary admin is '
                             'gone. Run a refresh, or create swoovr-deploy by hand (deploy/vultr-uat/README.md).')
        ensure_deploy_client(Admin(request, bootstrap), secret)
        say('deploy-client', 'created-or-updated')
        access = deploy_token(request, secret)
        if not access:
            raise ValueError('The new deployment client cannot sign in.')
    else:
        say('deploy-client', 'ok')
    admin = Admin(request, access)
    for realm in REALMS:
        if admin.call('/' + realm).get('realm') != realm:
            raise ValueError(f'Realm {realm} is missing.')
    if not admin_only:
        say('zammad-client', ensure_zammad_client(admin))
        say('entra', sync_entra(admin, env))
        verify_web_clients(admin)
        say('web-clients', 'verified')
        say('realms', enable_realms(admin, wanted_realms(env)))
    # Also in admin-only mode (the maintenance workflow's admin-finalize), so the temporary admin goes once you have yours.
    say('bootstrap-admin', retire_bootstrap_admin(admin, request, env))
    state, subject = admin_candidate(admin, env.get('SWOOVR_INITIAL_ADMIN_EMAIL', ''))
    say('initial-admin', state)
    if subject:
        say('initial-admin-subject', subject)
    return 0


def main(args):
    env = os.environ
    request = Transport(env.get('SWOOVR_KEYCLOAK_BASE', AUTH), env.get('DOTNET_ENVIRONMENT') == 'Development')
    return reconcile(request, env, admin_only='--admin-only' in args)


if __name__ == '__main__':
    try:
        raise SystemExit(main(sys.argv[1:]))
    except (ValueError, KeyError, OSError) as error:
        # Messages above are fixed text, paths and status codes; never request or response bodies.
        raise SystemExit(f'Keycloak reconcile failed: {error}')
