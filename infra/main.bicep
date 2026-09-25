targetScope = 'resourceGroup'

@description('Environment name. Every name below is derived from it: uat gives lanternuat, id-lantern-uat and so on.')
param environmentName string

@description('Table names. The API reads `parents` by default, so it must be listed. Add only; a renamed table is a new, empty one.')
param tables array

@description('Firebase project id. Public: the API pins token issuer and audience to it.')
param firebaseProjectId string

@description('Placeholder until the first API image exists.')
param containerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the image listens on: 80 for the placeholder, 8080 for the .NET SDK container image.')
param containerPort int = 80

var location = resourceGroup().location
var name = 'lantern-${environmentName}'

// Created once by bootstrap.sh with the roles the app needs. The template only reads them.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-${name}'
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: 'kv-${name}'
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'lantern${environmentName}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // The API signs in with its managed identity, so no key or connection string exists.
    allowSharedKeyAccess: false
  }

  resource tableService 'tableServices' = {
    name: 'default'

    resource table 'tables' = [for table in tables: {
      name: table
    }]
  }
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: 'cae-${name}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: 'ca-${name}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: {
        external: true
        targetPort: containerPort
      }
      secrets: [
        {
          name: 'security-key'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/security-key'
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'lantern-api'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          // Names follow the API's configuration sections, with no prefix.
          env: [
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'Firebase__ProjectId', value: firebaseProjectId }
            { name: 'Storage__TableEndpoint', value: storage.properties.primaryEndpoints.table }
            { name: 'Security__Key', secretRef: 'security-key' }
          ]
        }
      ]
      // Zero when idle, so an unused pilot costs nothing. One replica at most.
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
