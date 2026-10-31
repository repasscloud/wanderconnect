param location string
param serverName string
param subnetId string
param privateDnsZoneId string
param administratorLogin string = 'swoovrbootstrap'
@secure()
param administratorPassword string
resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: serverName
  location: location
  sku: { name: 'Standard_B2ms', tier: 'Burstable' }
  properties: {
    version: '17'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    authConfig: { activeDirectoryAuth: 'Disabled', passwordAuth: 'Enabled' }
    network: { delegatedSubnetResourceId: subnetId, privateDnsZoneArmResourceId: privateDnsZoneId }
    storage: { storageSizeGB: 32, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: 30, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
  }
}
resource databases 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = [for name in ['swoovr','keycloak']: {
  parent: server
  name: name
  properties: { charset: 'UTF8', collation: 'en_US.utf8' }
}]
output host string = server.properties.fullyQualifiedDomainName
