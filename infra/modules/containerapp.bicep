param environmentName string
param appName string
param location string
param tags object

param workspaceName string

@description('Resource id of the app identity.')
param identityId string

@description('Client id of the app identity. DefaultAzureCredential needs it to pick the user-assigned identity.')
param identityClientId string

param image string

@description('Port the container listens on: 80 for the placeholder, 8080 for the .NET image.')
param targetPort int

@description('Run liveness and startup probes on /health/live. Off for the placeholder image, which has no such route.')
param enableProbes bool

@description('Firebase project id. Public: the API pins token issuer and audience to it.')
param firebaseProjectId string

param tableEndpoint string

@description('Key Vault secret URI for Security:Key. The secret must exist: bootstrap.sh creates it.')
param securityKeySecretUri string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

// Names follow the API's configuration sections (Firebase, Storage, Security), with no prefix.
// A wrong name fails the options validation and the app refuses to start. The table name is not
// set: the API's default, `parents`, is created in the storage module from the .bicepparam list.
var baseEnv = [
  {
    name: 'AZURE_CLIENT_ID'
    value: identityClientId
  }
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'Firebase__ProjectId'
    value: firebaseProjectId
  }
  {
    name: 'Storage__TableEndpoint'
    value: tableEndpoint
  }
  {
    name: 'Security__Key'
    secretRef: 'security-key'
  }
]

var probes = enableProbes
  ? [
      {
        type: 'Startup'
        httpGet: {
          path: '/health/live'
          port: targetPort
        }
        initialDelaySeconds: 3
        periodSeconds: 3
        failureThreshold: 20
      }
      {
        type: 'Liveness'
        httpGet: {
          path: '/health/live'
          port: targetPort
        }
        periodSeconds: 30
        failureThreshold: 3
      }
    ]
  : []

// No workload profiles: a consumption-only environment has no fixed charge.
resource managedEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
      secrets: [
        {
          name: 'security-key'
          keyVaultUrl: securityKeySecretUri
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'lantern-api'
          image: image
          // The smallest allowed size.
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: baseEnv
          probes: probes
        }
      ]
      scale: {
        // Zero when idle, so an unused pilot costs nothing. At most one replica because the
        // planned SignalR hub is in-process: a second replica would not see the first one's
        // connections.
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

output environmentName string = managedEnvironment.name
output appName string = app.name
output fqdn string = app.properties.configuration.ingress.fqdn
