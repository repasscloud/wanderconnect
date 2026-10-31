// D285 DEFAULT: bring an operator-approved origin certificate through a Key Vault reference.
// A proxied Cloudflare CNAME cannot satisfy ACA managed-certificate renewal requirements.
targetScope = 'resourceGroup'
param location string = 'australiaeast'
param prefix string = 'swoovr-uat'
param vaultName string = 'kvswuat${uniqueString(resourceGroup().id)}'
@minLength(32)
@maxLength(32)
param certificateSecretVersion string
var secretName = '${prefix}-edge-tls'
resource environment 'Microsoft.App/managedEnvironments@2025-07-01' existing = { name: '${prefix}-environment' }
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: '${prefix}-edge-certificate' }
module access 'modules/secret-access.bicep' = {
  name: '${prefix}-certificate-secret-access'
  params: { vaultName: vaultName, principalId: identity.properties.principalId, secretNames: [secretName] }
}
resource certificate 'Microsoft.App/managedEnvironments/certificates@2025-07-01' = {
  parent: environment
  name: '${prefix}-origin'
  location: location
  properties: {
    certificateKeyVaultProperties: {
      identity: identity.id
      keyVaultUrl: 'https://${vaultName}${az.environment().suffixes.keyvaultDns}/secrets/${secretName}/${certificateSecretVersion}'
    }
  }
  dependsOn: [access]
}
output certificateId string = certificate.id
