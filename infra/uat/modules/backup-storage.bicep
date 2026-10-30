// D261 DEFAULT under D221/D216. Preparation only: no resources are provisioned by repository checks.
param location string = 'australiaeast'
@minLength(3)
@maxLength(24)
param accountName string = 'swuatbak${uniqueString(resourceGroup().id)}'
param containerName string = 'swoovr-backups'
@description('Dedicated private-endpoint subnet; parent UAT topology supplies it.')
param privateEndpointSubnetId string
@description('Private DNS zone for privatelink.blob.core.windows.net, linked by the parent topology.')
param blobPrivateDnsZoneId string
param publisherPrincipalId string
param expirerPrincipalId string
param recoveryPrincipalId string

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_GRS' }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowCrossTenantReplication: false
    publicNetworkAccess: 'Disabled'
    networkAcls: { defaultAction: 'Deny', bypass: 'None' }
    encryption: { keySource: 'Microsoft.Storage', services: { blob: { enabled: true, keyType: 'Account' } } }
  }
}
resource service 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    isVersioningEnabled: false
    deleteRetentionPolicy: { enabled: false }
    containerDeleteRetentionPolicy: { enabled: false }
  }
}
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: service
  name: containerName
  properties: { publicAccess: 'None' }
}
var tiers = [
  { name: 'daily', days: 30 }
  { name: 'weekly', days: 28 }
  // The job enforces exact12-calendar-month metadata expiry.366 days is the storage-service backstop, not365.
  { name: 'monthly', days: 366 }
]
resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    policy: {
      rules: [for tier in tiers: {
        enabled: true
        name: 'swoovr-${tier.name}-expiry'
        type: 'Lifecycle'
        definition: {
          filters: {
            blobTypes: [ 'blockBlob' ]
            prefixMatch: [ '${containerName}/api/${tier.name}/', '${containerName}/keycloak/${tier.name}/' ]
          }
          actions: {
            baseBlob: { delete: { daysAfterCreationGreaterThan: tier.days } }
            snapshot: { delete: { daysAfterCreationGreaterThan: tier.days } }
            version: { delete: { daysAfterCreationGreaterThan: tier.days } }
          }
        }
      }]
    }
  }
}
resource endpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'swoovr-uat-backups-private-endpoint'
  location: location
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [{
      name: 'blob'
      properties: { privateLinkServiceId: storage.id, groupIds: [ 'blob' ] }
    }]
  }
}
resource zoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: endpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'blob', properties: { privateDnsZoneId: blobPrivateDnsZoneId } }] }
}
resource publisherRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'swoovr-backup-publisher')
  properties: {
    roleName: 'Swoovr UAT backup publisher ${uniqueString(resourceGroup().id)}'
    description: 'Read/write encrypted generations; no deletion and no recovery-key access.'
    type: 'CustomRole'
    assignableScopes: [ resourceGroup().id ]
    permissions: [{
      actions: [ 'Microsoft.Storage/storageAccounts/blobServices/containers/read' ]
      dataActions: [
        'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read'
        'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write'
      ]
    }]
  }
}
resource expirerRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'swoovr-backup-expirer')
  properties: {
    roleName: 'Swoovr UAT backup expirer ${uniqueString(resourceGroup().id)}'
    description: 'Read/delete expired encrypted generations; cannot publish/replace archives or read recovery keys.'
    type: 'CustomRole'
    assignableScopes: [ resourceGroup().id ]
    permissions: [{
      actions: [ 'Microsoft.Storage/storageAccounts/blobServices/containers/read' ]
      dataActions: [
        'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read'
        'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/delete'
      ]
    }]
  }
}
resource publisherGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: container
  name: guid(container.id, publisherPrincipalId, publisherRole.id)
  properties: { principalId: publisherPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: publisherRole.id }
}
resource expirerGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: container
  name: guid(container.id, expirerPrincipalId, expirerRole.id)
  properties: { principalId: expirerPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: expirerRole.id }
}
resource recoveryGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: container
  name: guid(container.id, recoveryPrincipalId, 'swoovr-recovery-read')
  properties: {
    principalId: recoveryPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1')
  }
}
output accountUrl string = storage.properties.primaryEndpoints.blob
output containerResourceId string = container.id
