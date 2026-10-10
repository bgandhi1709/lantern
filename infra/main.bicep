targetScope = 'resourceGroup'

@description('Environment name. Every name below is derived from it: uat gives lanternuat, id-lantern-uat and so on.')
param environmentName string

@description('Table names, passed to both apps. Add only; a renamed table is a new, empty one.')
param tables {
  parents: string
  families: string
  actions: string
}

@description('Blob containers. `workspaces` holds every Child\'s Workspace and is passed to both apps; `ncert` and `ssc` hold each Board\'s refined corpus.')
param containers {
  workspaces: string
  ncert: string
  ssc: string
}

@description('The Key Vault secret bootstrap.sh creates: the key that hashes each uid. Lantern holds no Family key (D66).')
param keyVault {
  securityKeySecret: string
}

@description('The one queue for every Workspace event; the dispatcher picks the handler by type. The lock is the retry delay; deliveries times lock is how long a failing event retries.')
param actionsQueue {
  name: string
  lockDuration: string
  maxDeliveryCount: int
  messageTimeToLive: string
}

@description('Size and scale of each Container App. cpu is a string because Bicep has no decimals.')
param apiSize {
  cpu: string
  memory: string
  minReplicas: int
  maxReplicas: int
}

@description('See apiSize.')
param functionsSize {
  cpu: string
  memory: string
  minReplicas: int
  maxReplicas: int
}

@description('Children endpoint calls per caller per minute.')
param childrenPerMinute int

@description('How long an action may go unsent before the API resends it on start, as a .NET TimeSpan (hh:mm:ss).')
param actionResendAfter string

@description('Telemetry in Log Analytics: days kept, and the most it may ingest in a day (GB), the guardrail on cost.')
param telemetry {
  retentionDays: int
  dailyCapGb: int
}

@description('Empty placeholder app until the API image exists. The API release sets the image; see keep-running-image.sh.')
param containerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Empty placeholder app until the Functions image exists. The API release sets the image; see keep-running-image.sh.')
param functionsImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the image listens on: 80 for the placeholder, 8080 for the .NET SDK container image.')
param containerPort int = 80

@description('Firebase project the API accepts ID tokens from. Not a secret: it is in every token and every mobile app.')
param firebaseProjectId string

var location = resourceGroup().location
var name = 'lantern-${environmentName}'

// Not a secret (it only names the resource), kept as a secret ref like security-key.
var insightsSecret = { name: 'appinsights-connection-string', value: insights.properties.ConnectionString }

// Names both apps read, each from the resource that owns it.
var sharedEnv = [
  { name: 'Storage__ParentsTable', value: tables.parents }
  { name: 'Storage__FamiliesTable', value: tables.families }
  { name: 'Storage__ActionsTable', value: tables.actions }
  { name: 'Storage__WorkspaceContainer', value: containers.workspaces }
]

// Created once by bootstrap.sh with its roles. The template only reads it.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'id-${name}'
}

// Created once by bootstrap.sh, which also writes the secret and the key named in keyVault. The template only reads it.
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
    retentionInDays: telemetry.retentionDays
    workspaceCapping: {
      dailyQuotaGb: telemetry.dailyCapGb
    }
  }
}

// Workspace-based, so telemetry lands in the one workspace above. Ingestion signs in with the managed identity
// (Monitoring Metrics Publisher, granted by bootstrap.sh), so no key can send telemetry.
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    DisableLocalAuth: true
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

    resource table 'tables' = [for table in items(tables): {
      name: table.value
    }]
  }

  // Private containers for each Board's refined corpus (ncert-build's output). Read by nothing yet;
  // the API's read path is a later PR. Access comes from the resource-group roles in bootstrap.sh.
  resource blobService 'blobServices' = {
    name: 'default'

    properties: {
      // A deleted Child's files must really go (ADR-0003), so no soft-deleted or versioned copy is kept.
      deleteRetentionPolicy: {
        enabled: false
      }
      containerDeleteRetentionPolicy: {
        enabled: false
      }
      isVersioningEnabled: false
    }

    // One Workspace per Child, with a folder per Class: {familyId}/{childId}/{class}/.
    resource workspaceContainer 'containers' = {
      name: containers.workspaces
      properties: {
        publicAccess: 'None'
      }
    }

    resource ncertContainer 'containers' = {
      name: containers.ncert
      properties: {
        publicAccess: 'None'
      }
    }

    resource sscContainer 'containers' = {
      name: containers.ssc
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

// Basic is queues only, which is all the one consumer of each action needs; topics (pub-sub) need Standard (ADR-0004).
// Callers sign in with a managed identity, so no key or connection string exists.
resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: 'sb-${name}'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }

  // One queue for every action type; the message's type picks the handler. A message that is not settled comes back
  // when its lock expires, so the lock is the retry delay and a failing action is dead-lettered only after
  // maxDeliveryCount locks.
  resource queue 'queues' = {
    name: actionsQueue.name
    properties: {
      lockDuration: actionsQueue.lockDuration
      maxDeliveryCount: actionsQueue.maxDeliveryCount
      defaultMessageTimeToLive: actionsQueue.messageTimeToLive
      deadLetteringOnMessageExpiration: true
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
          keyVaultUrl: '${vault.properties.vaultUri}secrets/${keyVault.securityKeySecret}'
          identity: identity.id
        }
        insightsSecret
      ]
    }
    template: {
      containers: [
        {
          name: 'lantern-api'
          image: containerImage
          env: concat(sharedEnv, [
            // The user-assigned identity is ambiguous to DefaultAzureCredential without this.
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'Firebase__ProjectId', value: firebaseProjectId }
            { name: 'Storage__TableEndpoint', value: storage.properties.primaryEndpoints.table }
            { name: 'Storage__BlobEndpoint', value: storage.properties.primaryEndpoints.blob }
            { name: 'ServiceBus__FullyQualifiedNamespace', value: '${serviceBus.name}.servicebus.windows.net' }
            { name: 'Actions__Queue', value: serviceBus::queue.name }
            { name: 'Security__Key', secretRef: 'security-key' }
            { name: 'RateLimits__ChildrenPerMinute', value: string(childrenPerMinute) }
            { name: 'Actions__ResendAfter', value: actionResendAfter }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: insightsSecret.name }
            { name: 'OTEL_SERVICE_NAME', value: 'lantern-api' }
          ])
          resources: {
            cpu: json(apiSize.cpu)
            memory: apiSize.memory
          }
        }
      ]
      // Zero when idle, so an unused pilot costs nothing.
      scale: {
        minReplicas: apiSize.minReplicas
        maxReplicas: apiSize.maxReplicas
      }
    }
  }
}

// Lantern.Functions: finishes the actions the API puts on the queue. No ingress; a message on the queue wakes it, and
// it goes back to zero when the queue is empty.
resource functionsApp 'Microsoft.App/containerApps@2025-01-01' = {
  name: 'ca-${name}-functions'
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
      secrets: [insightsSecret]
    }
    template: {
      containers: [
        {
          name: 'lantern-functions'
          image: functionsImage
          env: concat(sharedEnv, [
            // The user-assigned identity is ambiguous to DefaultAzureCredential without this.
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'Storage__TableEndpoint', value: storage.properties.primaryEndpoints.table }
            { name: 'Storage__BlobEndpoint', value: storage.properties.primaryEndpoints.blob }
            // The Functions host keeps its own state in the storage account.
            { name: 'AzureWebJobsStorage__accountName', value: storage.name }
            { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
            { name: 'AzureWebJobsStorage__clientId', value: identity.properties.clientId }
            { name: 'ServiceBus__fullyQualifiedNamespace', value: '${serviceBus.name}.servicebus.windows.net' }
            { name: 'ServiceBus__credential', value: 'managedidentity' }
            { name: 'ServiceBus__clientId', value: identity.properties.clientId }
            { name: 'Actions__Queue', value: serviceBus::queue.name }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: insightsSecret.name }
            { name: 'OTEL_SERVICE_NAME', value: 'lantern-functions' }
          ])
          // The Functions host and the .NET worker are two processes.
          resources: {
            cpu: json(functionsSize.cpu)
            memory: functionsSize.memory
          }
        }
      ]
      scale: {
        minReplicas: functionsSize.minReplicas
        maxReplicas: functionsSize.maxReplicas
        rules: [
          {
            name: 'workspace-events'
            custom: {
              type: 'azure-servicebus'
              metadata: {
                queueName: serviceBus::queue.name
                namespace: serviceBus.name
                messageCount: '1'
              }
              identity: identity.id
            }
          }
        ]
      }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
