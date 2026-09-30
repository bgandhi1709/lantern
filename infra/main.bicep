targetScope = 'resourceGroup'

@description('Environment name. Every name below is derived from it: uat gives lanternuat, id-lantern-uat and so on.')
param environmentName string

@description('Table names. The API reads `parents` by default, so it must be listed. Add only; a renamed table is a new, empty one.')
param tables array

@description('Empty placeholder app until the API image exists. The API release sets the image; see keep-running-image.sh.')
param containerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the image listens on: 80 for the placeholder, 8080 for the .NET SDK container image.')
param containerPort int = 80

@description('Firebase project the API accepts ID tokens from. Not a secret: it is in every token and every mobile app.')
param firebaseProjectId string

var location = resourceGroup().location
var name = 'lantern-${environmentName}'

// Created once by bootstrap.sh with its roles. The template only reads it.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-${name}'
}

// Created once by bootstrap.sh, which also writes the security-key secret. The template only reads it.
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
    // Callers sign in with a managed identity, so no key or connection string exists.
    allowSharedKeyAccess: false
  }

  resource tableService 'tableServices' = {
    name: 'default'

    resource table 'tables' = [for table in tables: {
      name: table
    }]
  }

  // Private container for the refined NCERT corpus (ncert-build's output). Read by nothing yet —
  // the API's read path is a later PR; this one only receives the upload.
  resource blobService 'blobServices' = {
    name: 'default'

    resource ncertContainer 'containers' = {
      name: 'ncert'
      properties: {
        publicAccess: 'None'
      }
    }
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
          env: [
            // The user-assigned identity is ambiguous to DefaultAzureCredential without this.
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'Firebase__ProjectId', value: firebaseProjectId }
            { name: 'Storage__TableEndpoint', value: storage.properties.primaryEndpoints.table }
            { name: 'Security__Key', secretRef: 'security-key' }
            // Not a secret: the app only ever calls Key Vault's wrapKey/unwrapKey with this name.
            // The key's own private material never leaves the vault.
            { name: 'KeyVault__VaultUri', value: vault.properties.vaultUri }
            { name: 'KeyVault__FamilyKeyName', value: 'family-field-key' }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
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
