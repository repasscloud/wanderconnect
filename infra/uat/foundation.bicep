// D283 DEFAULT: local compilation/review only. The owner creates/selects the subscription.
targetScope = 'resourceGroup'
param location string = 'australiaeast'
@minLength(3)
@maxLength(18)
param prefix string = 'swoovr-uat'
@description('Public operator IPv4 CIDRs permitted during private secret bootstrap; empty keeps public vault access disabled.')
param bootstrapOperatorCidrs array = []
var suffix = uniqueString(resourceGroup().id)
var names = {
  vault: 'kvswuat${suffix}'
  registry: 'crswuat${suffix}'
  environment: '${prefix}-environment'
  network: '${prefix}-network'
}
var actors = ['api', 'web', 'admin', 'keycloak', 'screening', 'migrate', 'operator', 'bootstrap', 'backup-publisher', 'backup-expiry', 'recovery']
resource network 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: names.network
  location: location
  properties: {
    addressSpace: { addressPrefixes: ['10.42.0.0/16'] }
    subnets: [
      { name: 'apps', properties: { addressPrefix: '10.42.0.0/23', delegations: [{ name: 'container-apps', properties: { serviceName: 'Microsoft.App/environments' } }] } }
      { name: 'postgres', properties: { addressPrefix: '10.42.2.0/27', delegations: [{ name: 'postgres', properties: { serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers' } }] } }
      { name: 'private-endpoints', properties: { addressPrefix: '10.42.2.32/27', privateEndpointNetworkPolicies: 'Disabled' } }
    ]
  }
}
resource postgresDns 'Microsoft.Network/privateDnsZones@2024-06-01' = { name: 'private.postgres.database.azure.com', location: 'global' }
resource vaultDns 'Microsoft.Network/privateDnsZones@2024-06-01' = { name: 'privatelink.vaultcore.azure.net', location: 'global' }
resource blobDns 'Microsoft.Network/privateDnsZones@2024-06-01' = { name: 'privatelink.blob.${az.environment().suffixes.storage}', location: 'global' }
resource postgresLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: postgresDns
  name: '${prefix}-link'
  location: 'global'
  properties: { registrationEnabled: false, virtualNetwork: { id: network.id } }
}
resource vaultLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: vaultDns
  name: '${prefix}-link'
  location: 'global'
  properties: { registrationEnabled: false, virtualNetwork: { id: network.id } }
}
resource blobLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: blobDns
  name: '${prefix}-link'
  location: 'global'
  properties: { registrationEnabled: false, virtualNetwork: { id: network.id } }
}
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: names.vault
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    enabledForTemplateDeployment: true
    publicNetworkAccess: empty(bootstrapOperatorCidrs) ? 'Disabled' : 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: [for cidr in bootstrapOperatorCidrs: { value: cidr }]
      virtualNetworkRules: []
    }
  }
}
resource vaultEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${prefix}-vault-private-endpoint'
  location: location
  properties: {
    subnet: { id: '${network.id}/subnets/private-endpoints' }
    privateLinkServiceConnections: [{ name: 'vault', properties: { privateLinkServiceId: vault.id, groupIds: ['vault'] } }]
  }
}
resource vaultZone 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: vaultEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'vault', properties: { privateDnsZoneId: vaultDns.id } }] }
}
resource registry 'Microsoft.ContainerRegistry/registries@2025-06-01-preview' = {
  name: names.registry
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false, anonymousPullEnabled: false, roleAssignmentMode: 'LegacyRegistryPermissions', publicNetworkAccess: 'Enabled' }
}
resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = [for actor in actors: {
  name: '${prefix}-${actor}'
  location: location
}]
resource pulls 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (actor,index) in actors: {
  scope: registry
  name: guid(registry.id, identities[index].id, 'AcrPull')
  properties: {
    principalId: identities[index].properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}]
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}
resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: names.environment
  location: location
  properties: {
    vnetConfiguration: { infrastructureSubnetId: '${network.id}/subnets/apps', internal: false }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
    appLogsConfiguration: { destination: 'azure-monitor' }
    zoneRedundant: false
  }
}
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: environment
  name: '${prefix}-logs'
  properties: {
    workspaceId: workspace.id
    logs: [{ category: 'ContainerAppConsoleLogs', enabled: true }, { category: 'ContainerAppSystemLogs', enabled: true }]
  }
}
module backups 'modules/backup-storage.bicep' = {
  name: '${prefix}-backup-storage'
  params: {
    location: location
    privateEndpointSubnetId: '${network.id}/subnets/private-endpoints'
    blobPrivateDnsZoneId: blobDns.id
    publisherPrincipalId: identities[8].properties.principalId
    expirerPrincipalId: identities[9].properties.principalId
    recoveryPrincipalId: identities[10].properties.principalId
  }
}
// API and identity captures share the dedicated publisher identity; expiry/recovery remain separate.
output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
output registryServer string = registry.properties.loginServer
output environmentId string = environment.id
output environmentDefaultDomain string = environment.properties.defaultDomain
output postgresSubnetId string = '${network.id}/subnets/postgres'
output postgresPrivateDnsZoneId string = postgresDns.id
output backupAccountUrl string = backups.outputs.accountUrl
output backupContainerId string = backups.outputs.containerResourceId
output identities array = [for (actor,index) in actors: { actor: actor, id: identities[index].id, clientId: identities[index].properties.clientId, principalId: identities[index].properties.principalId }]
