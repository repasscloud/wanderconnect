param location string
param name string
param environmentId string
param identityId string
param registryServer string
param image string
param environmentVariables array
param secretBindings array
param command array = []
param args array
param schedule string = ''
param timeoutSeconds int = 1800
var secretEnvironment = [for item in secretBindings: { name: item.env, secretRef: item.ref }]
resource job 'Microsoft.App/jobs@2025-07-01' = {
  name: name
  location: location
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: empty(schedule) ? 'Manual' : 'Schedule'
      replicaTimeout: timeoutSeconds
      replicaRetryLimit: 0
      manualTriggerConfig: empty(schedule) ? { parallelism: 1, replicaCompletionCount: 1 } : null
      scheduleTriggerConfig: empty(schedule) ? null : { parallelism: 1, replicaCompletionCount: 1, cronExpression: schedule }
      registries: [{ server: registryServer, identity: identityId }]
      secrets: [for item in secretBindings: { name: item.ref, keyVaultUrl: item.url, identity: identityId }]
    }
    template: {
      containers: [{ name: name, image: image, command: command, args: args, env: concat(environmentVariables,secretEnvironment), resources: { cpu: json('0.5'), memory: '1Gi' } }]
    }
  }
}
