param location string
param name string
param environmentId string
param identityId string
param registryServer string
param image string
param hostname string
param internalIngress bool = false
param certificateId string = ''
param cpu string = '0.5'
param memory string = '1Gi'
param environmentVariables array
param secretBindings array
param livenessPath string = '/health/live'
param readinessPath string = '/health/ready'
param probePort int = 8080
param command array = []
param args array = []
var secretEnvironment = [for item in secretBindings: { name: item.env, secretRef: item.ref }]
resource app 'Microsoft.App/containerApps@2025-07-01' = {
  name: name
  location: location
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identityId}': {} } }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: identityId }]
      secrets: [for item in secretBindings: { name: item.ref, keyVaultUrl: item.url, identity: identityId }]
      ingress: {
        external: !internalIngress
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        customDomains: empty(certificateId) ? [] : [{ name: hostname, bindingType: 'SniEnabled', certificateId: certificateId }]
      }
    }
    template: {
      containers: [{
        name: name
        image: image
        command: command
        args: args
        env: concat(environmentVariables,secretEnvironment)
        resources: { cpu: json(cpu), memory: memory }
        probes: [for probe in [
          { type: 'Startup', path: readinessPath, period: 30, failures: 10 }
          { type: 'Liveness', path: livenessPath, period: 30, failures: 3 }
          { type: 'Readiness', path: readinessPath, period: 10, failures: 3 }
        ]: {
          type: probe.type
          httpGet: { path: probe.path, port: probePort, scheme: 'HTTP', httpHeaders: [{ name: 'Host', value: hostname }, { name: 'X-Forwarded-Proto', value: 'https' }] }
          initialDelaySeconds: 10
          periodSeconds: probe.period
          failureThreshold: probe.failures
          timeoutSeconds: 5
          successThreshold: 1
        }]
      }]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}
output defaultHost string = app.properties.configuration.ingress.fqdn
