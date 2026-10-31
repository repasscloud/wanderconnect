#!/usr/bin/env python3
"""Validate nonsecret UAT service parameters locally, before an operator deploys."""
import argparse,ipaddress,json,re
from urllib.parse import urlsplit
from pathlib import Path

def validate(values):
    def required(name):
        value=values.get(name)
        if not value:raise ValueError('Missing parameter: '+name)
        return value
    images=required('images')
    if set(images)!={'api','web','admin','keycloak','screening','operator'}:raise ValueError('Exactly six reviewed images are required.')
    for image in images.values():
        if not isinstance(image,str) or not re.fullmatch(r'[a-z0-9][a-z0-9.-]*\.azurecr\.io/[a-z0-9/._-]+@sha256:[0-9a-f]{64}',image):raise ValueError('Images must be immutable Azure registry digests.')
    versions=required('cryptoVersions')
    keys={'founder','phone','ban','contact','contactLookup','evidence','identity','identityHmac','push','pushLookup'}
    if set(versions)!=keys or any(not isinstance(v,str) or not re.fullmatch('[0-9a-f]{32}',v) for v in versions.values()):raise ValueError('Every logical crypto key requires its exact physical vault version.')
    networks=required('trustedProxyNetworks').split(',')
    for value in networks:
        network=ipaddress.ip_network(value,strict=True)
        if network.prefixlen==0:raise ValueError('Trust-all proxy networks are forbidden.')
    for name in ['r2Endpoint','backupAccountUrl']:
        value=required(name);url=urlsplit(value)
        if url.scheme!='https' or not url.hostname or url.username or url.password or url.port or url.path or url.query or url.fragment:raise ValueError(name+' must be an exact HTTPS origin without credentials or a trailing slash.')
    if not re.fullmatch(r'[a-z0-9-]+\.[a-z0-9-]+\.azurecontainerapps\.io',required('environmentDefaultDomain')):raise ValueError('Use the foundation output default domain.')
    if not re.fullmatch(r'age1[0-9a-z]{58}',required('backupRecipient')):raise ValueError('An age public recipient is required; never supply the recovery identity.')
    if not re.fullmatch(r'[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]',required('r2Bucket')):raise ValueError('Invalid private R2 bucket name.')
    active=(['api','web','admin'] if values.get('deployServing') else [])+(['keycloak'] if values.get('deployIdentity') else [])
    certificates=values.get('certificates',{})
    for actor in active if values.get('bindPublicHostnames') else []:
        resource=certificates.get(actor,'')
        if not isinstance(resource,str) or not re.fullmatch(r'/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[a-zA-Z0-9._()-]+/providers/Microsoft.App/managedEnvironments/[a-z0-9-]+/certificates/[a-z0-9-]+',resource):raise ValueError('Active public hosts require explicit reviewed origin certificate bindings.')
    if values.get('enableSchedules') and not values.get('createJobs'):raise ValueError('Schedules require jobs.')
    if values.get('enableInvitations') and (not values.get('deployServing') or not values.get('bindPublicHostnames')):raise ValueError('Invitations require reviewed serving deployment.')
    return values

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('parameters',type=Path);args=p.parse_args()
    data=json.loads(args.parameters.read_text())
    values={k:v['value'] for k,v in data['parameters'].items()} if 'parameters' in data else data
    validate(values)
    print('PASS local UAT parameter shape; registry architecture, secret contents, proxy reachability and owner approvals remain operator checks. No cloud action.')
if __name__=='__main__':
    try:main()
    except (ValueError,KeyError,TypeError):raise SystemExit('UAT preflight refused invalid or missing parameters; no cloud action.')
