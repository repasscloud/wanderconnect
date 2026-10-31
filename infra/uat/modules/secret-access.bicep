// Secret-scoped data access, not vault-wide access to unrelated operator/recovery credentials.
param vaultName string
param principalId string
param secretNames array
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = { name: vaultName }
resource secrets 'Microsoft.KeyVault/vaults/secrets@2024-11-01' existing = [for name in secretNames: { parent: vault, name: name }]
resource grants 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (name,index) in secretNames: {
  scope: secrets[index]
  name: guid(secrets[index].id,principalId,'KeyVaultSecretsUser')
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions','4633458b-17de-408a-b874-0445c86b69e6')
  }
}]
