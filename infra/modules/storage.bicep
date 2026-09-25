param name string
param location string
param tags object

@description('Principal id of the app identity. It gets data-plane roles on Table, Blob and Queue.')
param appPrincipalId string

param tables array
param containers array
param queues array

// Built-in roles: Storage Table Data Contributor, Storage Blob Data Contributor,
// Storage Queue Data Contributor.
var roleIds = [
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
]

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Every caller uses Entra. The API builds its TableClient with DefaultAzureCredential, so
    // shared keys stay off and no connection string exists to leak.
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource tableResources 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = [
  for table in tables: {
    parent: tableService
    name: table
  }
]

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource containerResources 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for container in containers: {
    parent: blobService
    name: container
    properties: {
      publicAccess: 'None'
    }
  }
]

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource queueResources 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = [
  for queue in queues: {
    parent: queueService
    name: queue
  }
]

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for roleId in roleIds: {
    scope: account
    name: guid(account.id, appPrincipalId, roleId)
    properties: {
      principalId: appPrincipalId
      principalType: 'ServicePrincipal'
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    }
  }
]

output name string = account.name
output tableEndpoint string = account.properties.primaryEndpoints.table
output blobEndpoint string = account.properties.primaryEndpoints.blob
output queueEndpoint string = account.properties.primaryEndpoints.queue
