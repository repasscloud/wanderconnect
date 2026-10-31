// D283 DEFAULT staged preparation. Foundation and reviewed Key Vault inputs precede this deployment.
targetScope = 'resourceGroup'
param location string = 'australiaeast'
param prefix string = 'swoovr-uat'
param vaultName string = 'kvswuat${uniqueString(resourceGroup().id)}'
param environmentDefaultDomain string
param registryName string = 'crswuat${uniqueString(resourceGroup().id)}'
param postgresName string = 'pgswuat${uniqueString(resourceGroup().id)}'
param postgresAdmin string = 'swoovrbootstrap'
@description('Immutable registry digests, including separately prepared screening/operator images.')
param images object
@description('Physical Key Vault versions for every logical crypto key1. Never substitute latest for these bindings.')
param cryptoVersions object
@description('Exact ingress subnet/loopback plus approved proxy CIDRs; no0-prefix. Deployment preflight validates it.')
param trustedProxyNetworks string
param r2Endpoint string
param r2Bucket string
param backupAccountUrl string
param backupRecipient string
param grantSecretAccess bool = false
param createJobs bool = false
param deployIdentity bool = false
param deployServing bool = false
param enableSchedules bool = false
param enableInvitations bool = false
param bindPublicHostnames bool = false
param certificates object = {}
var actors = ['api','web','admin','keycloak','screening','migrate','operator','bootstrap','backup-publisher','backup-expiry','recovery']
var records = loadJsonContent('secret-bindings.json')
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = { name: vaultName }
resource registry 'Microsoft.ContainerRegistry/registries@2025-06-01-preview' existing = { name: registryName }
resource network 'Microsoft.Network/virtualNetworks@2024-05-01' existing = { name: '${prefix}-network' }
resource dns 'Microsoft.Network/privateDnsZones@2024-06-01' existing = { name: 'private.postgres.database.azure.com' }
resource environment 'Microsoft.App/managedEnvironments@2025-07-01' existing = { name: '${prefix}-environment' }
resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [for actor in actors: { name: '${prefix}-${actor}' }]
var bindings = [for item in records: {
  name: item.name
  ref: item.name
  env: item.env
  actors: item.actors
  url: 'https://${vaultName}${az.environment().suffixes.keyvaultDns}/secrets/${prefix}-${item.name}${contains(item,'vkey') ? '/${cryptoVersions[item.vkey]}' : ''}'
}]
module database 'modules/postgres.bicep' = {
  name: '${prefix}-postgres'
  params: { location: location, serverName: postgresName, subnetId: '${network.id}/subnets/postgres', privateDnsZoneId: dns.id, administratorLogin: postgresAdmin, administratorPassword: vault.getSecret('${prefix}-pg-admin-pass') }
}
module access 'modules/secret-access.bicep' = [for (actor,index) in actors: if(grantSecretAccess || createJobs || deployIdentity || deployServing) {
  name: '${prefix}-${actor}-secrets'
  params: { vaultName: vaultName, principalId: identities[index].properties.principalId, secretNames: union([],map(filter(records,r=>contains(r.actors,actor)),r=> '${prefix}-${r.name}')) }
}]
var apiHost = '${prefix}-api.${environmentDefaultDomain}'
var authHost = '${prefix}-keycloak.${environmentDefaultDomain}'
var screenHost = '${prefix}-screening.internal.${environmentDefaultDomain}'
var common = [
  { name: 'DOTNET_ENVIRONMENT', value: 'Uat' }
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Uat' }
  { name: 'Staff__LocalPasswords', value: 'false' }
  { name: 'Prototype__Enabled', value: 'false' }
  { name: 'DetailedErrors', value: 'false' }
  { name: 'Keycloak__RequireHttpsMetadata', value: 'true' }
  { name: 'Hosting__TrustedProxyNetworks', value: trustedProxyNetworks }
]
var apiEnv = concat(common,[
  { name: 'Hosting__PublicOrigin', value: 'https://api.uat.swoovr.app' }
  { name: 'AllowedHosts', value: 'api.uat.swoovr.app;${apiHost}' }
  { name: 'Keycloak__BaseUrl', value: 'https://auth.uat.swoovr.app' }
  { name: 'Keycloak__MembersRealm', value: 'swoovr-members' }
  { name: 'Keycloak__VenuesRealm', value: 'swoovr-venues' }
  { name: 'Keycloak__StaffRealm', value: 'swoovr-staff' }
  { name: 'Keycloak__StaffSsoAlias', value: 'entra' }
  { name: 'MemberAdmin__ClientId', value: 'swoovr-api-accounts' }
  { name: 'MemberAdmin__ContactRetirementEnabled', value: 'true' }
  { name: 'Phone__Required', value: 'true' }
  { name: 'Retention__Worker', value: 'true' }
  { name: 'Media__Screening__Worker', value: 'true' }
  { name: 'Media__Screening__RequireKnownContentCheck', value: 'true' }
  { name: 'Media__Store', value: 'r2' }
  { name: 'Media__R2__Endpoint', value: r2Endpoint }
  { name: 'Media__R2__Bucket', value: r2Bucket }
  { name: 'Media__Screening__BaseUrl', value: 'https://${screenHost}/' }
  { name: 'Email__Provider', value: 'mailersend' }
  { name: 'Sms__Provider', value: 'twilio' }
  { name: 'Push__Provider', value: 'disabled' }
  { name: 'Didit__TestMode', value: 'false' }
  { name: 'Beta__Enabled', value: 'true' }
  { name: 'Beta__Invitations__Enabled', value: string(enableInvitations) }
  { name: 'Beta__Invitations__PublicBaseUrl', value: 'https://uat.swoovr.app/' }
  { name: 'MeetingSafety__Enabled', value: 'true' }
  { name: 'MeetingSafety__Worker', value: 'true' }
  { name: 'MeetingSafety__PublicBaseUrl', value: 'https://uat.swoovr.app/' }
  { name: 'TextHarm__Enabled', value: 'true' }
  { name: 'TextHarm__Worker', value: 'true' }
  { name: 'Founders__Places', value: '1000' }
])
var screeningStartup = '''
python -c 'import os,sys; names=("API_KEY","ARACHNID_API_USERNAME","ARACHNID_API_PASSWORD"); sys.exit("UAT screening credentials are required.") if any(not os.environ.get(k,"").strip() for k in names) else None'
exec /usr/local/bin/entrypoint
'''
var specs = [
  { actor: 'api', host: 'api.uat.swoovr.app', cpu: '0.5', memory: '1Gi', env: apiEnv, internal: false, live: '/health/live', ready: '/health/ready', probe: 8080 }
  { actor: 'web', host: 'uat.swoovr.app', cpu: '0.5', memory: '1Gi', env: concat(common,[{ name: 'Hosting__PublicOrigin', value: 'https://uat.swoovr.app' },{ name: 'AllowedHosts', value: 'uat.swoovr.app;${prefix}-web.${environmentDefaultDomain}' },{ name: 'Api__BaseUrl', value: 'https://${apiHost}/' },{ name: 'Keycloak__Authority', value: 'https://auth.uat.swoovr.app/realms/swoovr-members' },{ name: 'Keycloak__ClientId', value: 'swoovr-web' }]), internal: false, live: '/health/live', ready: '/health/ready', probe: 8080 }
  { actor: 'admin', host: 'admin.uat.swoovr.app', cpu: '0.25', memory: '0.5Gi', env: concat(common,[{ name: 'Hosting__PublicOrigin', value: 'https://admin.uat.swoovr.app' },{ name: 'AllowedHosts', value: 'admin.uat.swoovr.app;${prefix}-admin.${environmentDefaultDomain}' },{ name: 'Api__BaseUrl', value: 'https://${apiHost}/' },{ name: 'Keycloak__Authority', value: 'https://auth.uat.swoovr.app/realms/swoovr-staff' },{ name: 'Keycloak__ClientId', value: 'swoovr-admin-web' }]), internal: false, live: '/health/live', ready: '/health/ready', probe: 8080 }
  { actor: 'screening', host: screenHost, cpu: '2', memory: '4Gi', env: [{ name: 'ENABLE_DOCS', value: 'false' },{ name: 'LOG_LEVEL', value: 'INFO' }], internal: true, live: '/health', ready: '/ready', probe: 8080 }
]
module apps 'modules/container-app.bicep' = [for spec in specs: if(deployServing) {
  name: '${prefix}-${spec.actor}-app'
  params: { location: location, name: '${prefix}-${spec.actor}', environmentId: environment.id, identityId: identities[indexOf(actors,spec.actor)].id, registryServer: registry.properties.loginServer, image: images[spec.actor], hostname: spec.host, internalIngress: spec.internal, certificateId: bindPublicHostnames ? (certificates[?spec.actor] ?? '') : '', cpu: spec.cpu, memory: spec.memory, environmentVariables: spec.env, secretBindings: filter(bindings,b=>contains(b.actors,spec.actor)), livenessPath: spec.live, readinessPath: spec.ready, probePort: spec.probe, command: spec.actor == 'screening' ? ['/bin/sh','-ec'] : [], args: spec.actor == 'screening' ? [screeningStartup] : [] }
  dependsOn: [access]
}]
module identityApp 'modules/container-app.bicep' = if(deployIdentity) {
  name: '${prefix}-keycloak-app'
  params: {
    location: location
    name: '${prefix}-keycloak'
    environmentId: environment.id
    identityId: identities[3].id
    registryServer: registry.properties.loginServer
    image: images.keycloak
    hostname: 'auth.uat.swoovr.app'
    certificateId: bindPublicHostnames ? (certificates.?keycloak ?? '') : ''
    cpu: '1'
    memory: '2Gi'
    probePort: 9000
    livenessPath: '/health/live'
    readinessPath: '/health/ready'
    environmentVariables: [
      { name: 'KC_DB', value: 'postgres' }
      { name: 'KC_DB_URL', value: 'jdbc:postgresql://${database.outputs.host}:5432/keycloak?sslmode=verify-full&sslrootcert=/etc/pki/tls/certs/ca-bundle.crt' }
      { name: 'KC_DB_USERNAME', value: 'swoovr_keycloak' }
      { name: 'KC_HOSTNAME', value: 'https://auth.uat.swoovr.app' }
      { name: 'KC_HTTP_ENABLED', value: 'true' }
      { name: 'KC_PROXY_HEADERS', value: 'xforwarded' }
      { name: 'KC_PROXY_TRUSTED_ADDRESSES', value: trustedProxyNetworks }
      { name: 'KC_BOOTSTRAP_ADMIN_USERNAME', value: 'swoovr-bootstrap' }
      { name: 'KC_LOG_LEVEL', value: 'INFO' }
    ]
    secretBindings: filter(bindings,b=>contains(b.actors,'keycloak'))
  }
  dependsOn: [access]
}
var jobSpecs = [
  { name: 'places-import', actor: 'migrate', image: images.operator, args: ['places-import'], command: [], cron: '' }
  { name: 'migrate', actor: 'migrate', image: images.api, args: ['migrate'], command: [], cron: '' }
  { name: 'root-admin', actor: 'operator', image: images.api, args: ['root-admin','grant'], command: [], cron: '' }
  { name: 'accounts-purge', actor: 'operator', image: images.api, args: ['accounts','purge'], command: [], cron: '' }
  { name: 'founders', actor: 'operator', image: images.api, args: ['founders','allocate'], command: [], cron: enableSchedules ? '0 2 * * *' : '' }
  { name: 'db-bootstrap', actor: 'bootstrap', image: images.operator, args: ['database-bootstrap'], command: [], cron: '' }
  { name: 'keycloak-bootstrap', actor: 'bootstrap', image: images.operator, args: ['keycloak-bootstrap'], command: [], cron: '' }
  { name: 'backup-api', actor: 'backup-publisher', image: images.operator, args: ['backup','api'], command: [], cron: enableSchedules ? '15 2 * * *' : '' }
  { name: 'backup-keycloak', actor: 'backup-publisher', image: images.operator, args: ['backup','keycloak'], command: [], cron: enableSchedules ? '45 2 * * *' : '' }
  { name: 'backup-expiry', actor: 'backup-expiry', image: images.operator, args: ['backup-expiry'], command: [], cron: enableSchedules ? '15 4 * * *' : '' }
  { name: 'recovery', actor: 'recovery', image: images.operator, args: ['recovery-help'], command: [], cron: '' }
]
var operatorEnv = concat(common,[
  { name: 'Hosting__PublicOrigin', value: 'https://api.uat.swoovr.app' }
  { name: 'MemberAdmin__ClientId', value: 'swoovr-api-accounts' }
  { name: 'Keycloak__BaseUrl', value: 'https://auth.uat.swoovr.app' }
  { name: 'Keycloak__MembersRealm', value: 'swoovr-members' }
  { name: 'Media__R2__Endpoint', value: r2Endpoint }
  { name: 'Media__R2__Bucket', value: r2Bucket }
  { name: 'PGHOST', value: database.outputs.host }
  { name: 'PGPORT', value: '5432' }
  { name: 'PGSSLMODE', value: 'verify-full' }
  { name: 'PGSSLROOTCERT', value: '/etc/ssl/certs/ca-certificates.crt' }
  { name: 'SWOOVR_PG_ADMIN', value: postgresAdmin }
  { name: 'SWOOVR_KEYCLOAK_BASE', value: 'https://${authHost}' }
  { name: 'SWOOVR_BACKUP_ACCOUNT', value: backupAccountUrl }
  { name: 'SWOOVR_BACKUP_CONTAINER', value: 'swoovr-backups' }
  { name: 'SWOOVR_BACKUP_RECIPIENT', value: backupRecipient }
])
module jobs 'modules/job.bicep' = [for job in jobSpecs: if(createJobs) {
  name: '${prefix}-${job.name}-job'
  params: { location: location, name: '${prefix}-${job.name}', environmentId: environment.id, identityId: identities[indexOf(actors,job.actor)].id, registryServer: registry.properties.loginServer, image: job.image, environmentVariables: concat(operatorEnv,[
    { name: 'AZURE_CLIENT_ID', value: identities[indexOf(actors,job.actor)].properties.clientId }
    { name: 'Email__Provider', value: job.actor == 'operator' ? 'mailersend' : 'disabled' }
    { name: 'Media__Store', value: contains(['operator','backup-publisher','recovery'],job.actor) ? 'r2' : 'none' }
    { name: 'PGUSER', value: contains(['bootstrap','recovery'],job.actor) ? postgresAdmin : 'swoovr_backup' }
    { name: 'PGDATABASE', value: job.name == 'backup-keycloak' ? 'keycloak' : contains(['bootstrap','recovery'],job.actor) ? 'postgres' : 'swoovr' }
  ]), secretBindings: filter(bindings,b=>contains(b.actors,job.actor)), args: job.args, command: job.command, schedule: job.cron }
  dependsOn: [access]
}]
output postgresHost string = database.outputs.host
output authDefaultHost string = authHost
output apiDefaultHost string = apiHost
