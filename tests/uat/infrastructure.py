#!/usr/bin/env python3
"""Compile UAT templates and check deployment security contracts without Azure access."""
import argparse,json,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--bicep',required=True);args=p.parse_args()
checks=0
def require(value):
    global checks
    assert value,'Infrastructure contract failed at assertion '+str(checks+1)
    checks+=1
with tempfile.TemporaryDirectory(prefix='swoovr-infra-') as private:
    templates={}
    for source in sorted((ROOT/'infra/uat').rglob('*.bicep')):
        target=Path(private)/(source.stem+'.json')
        result=subprocess.run([args.bicep,'build',str(source),'--outfile',str(target)],capture_output=True,text=True)
        require(result.returncode==0 and not result.stderr.strip())
        templates[source.stem]=json.loads(target.read_text())
    foundation=templates['foundation'];services=templates['services'];app=templates['container-app'];job=templates['job']
    resource=lambda doc,kind: next(r for r in doc['resources'] if r['type']==kind)
    environment=resource(foundation,'Microsoft.App/managedEnvironments')
    require(environment['identity']['type']=='UserAssigned')
    certificate=resource(templates['edge'],'Microsoft.App/managedEnvironments/certificates')['properties']
    require(set(certificate)=={'certificateKeyVaultProperties'})
    require(set(certificate['certificateKeyVaultProperties'])=={'identity','keyVaultUrl'})
    require('certificateSecretVersion' in certificate['certificateKeyVaultProperties']['keyVaultUrl'])
    vault=resource(foundation,'Microsoft.KeyVault/vaults')['properties']
    for name in ['enableRbacAuthorization','enableSoftDelete','enablePurgeProtection','enabledForTemplateDeployment']:require(vault[name] is True)
    require(vault['networkAcls']['defaultAction']=='Deny' and vault['softDeleteRetentionInDays']==90)
    require('Disabled' in vault['publicNetworkAccess'] and foundation['parameters']['bootstrapOperatorCidrs']['defaultValue']==[])
    registry=resource(foundation,'Microsoft.ContainerRegistry/registries')['properties']
    require(registry['adminUserEnabled'] is False and registry['anonymousPullEnabled'] is False)
    require(registry['roleAssignmentMode']=='LegacyRegistryPermissions')
    pg=resource(templates['postgres'],'Microsoft.DBforPostgreSQL/flexibleServers')['properties']
    require(pg['version']=='17' and pg['backup']['backupRetentionDays']==30)
    require(set(pg['network'])=={'delegatedSubnetResourceId','privateDnsZoneArmResourceId'})
    require(templates['postgres']['parameters']['administratorPassword']['type'].lower()=='securestring')
    cfg=resource(app,'Microsoft.App/containerApps')['properties']
    require(cfg['configuration']['ingress']['allowInsecure'] is False)
    require(cfg['template']['scale']=={'minReplicas':1,'maxReplicas':1})
    require(cfg['configuration']['activeRevisionsMode']=='Single')
    require('keyVaultUrl' in json.dumps(cfg['configuration']['copy']))
    probes=cfg['template']['containers'][0]['copy'][0]['input']
    require(probes['timeoutSeconds']==5 and probes['successThreshold']==1)
    j=resource(job,'Microsoft.App/jobs')['properties']['configuration']
    require(j['replicaRetryLimit']==0)
    require('replicaCompletionCount' in j['manualTriggerConfig'] and 'parallelism' in j['scheduleTriggerConfig'])
    for flag in ['grantSecretAccess','createJobs','deployIdentity','deployServing','enableSchedules','enableInvitations','bindPublicHostnames']:require(services['parameters'][flag]['defaultValue'] is False)
    records=json.loads((ROOT/'infra/uat/secret-bindings.json').read_text())
    for actor in foundation['variables']['actors']:
        chosen=[r for r in records if actor in r['actors']]
        require(len({r['env'] for r in chosen})==len(chosen))
        require(len({r['name'] for r in chosen})==len(chosen))
    require(next(r for r in records if r['name']=='age-recovery')['actors']==['recovery'])
    require(len([r for r in records if 'vkey' in r])==10)
    require(all('value' not in r for r in records))
    require('cryptoVersions' in json.dumps(services['variables']['copy']))
    # Bicep folds concat into an ARM expression; inspect literal components in the compiled contract.
    wire=json.dumps(services)
    for key in ['Phone__Required','Media__Screening__RequireKnownContentCheck','Retention__Worker','Sms__Provider','Email__Provider']:require(key in wire)
    require('UAT screening credentials are required.' in services['variables']['screeningStartup'])
    require('root-admin' in json.dumps(services['variables']['jobSpecs']) and 'grant' in json.dumps(services['variables']['jobSpecs']))
    require('primaryEndpoints.blob' not in json.dumps(templates['backup-storage']['outputs']['accountUrl']))
print(f'PASS {checks} compiled infrastructure/security assertions; no Azure authentication, provisioning or hosted validation.')
