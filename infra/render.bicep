targetScope = 'resourceGroup'

@description('Name prefix shared with main.bicep, for example lantern-uat.')
param name string

param location string = resourceGroup().location

@description('The Container Apps environment from main.bicep. It must list the GPU workload profile.')
param environmentId string

@description('Name of the GPU workload profile in that environment.')
param gpuProfileName string

@description('The user-assigned identity from bootstrap.sh. It pulls the image, reads and writes the story and video containers.')
param identityId string
param identityClientId string

@description('Blob endpoint of the Lantern storage account. Stories are read from and videos written to the render container there.')
param blobEndpoint string

param renderContainer string

@description('Shared-key storage account that holds only the model weights share. Container Apps can mount Azure Files only with an account key, which the main account forbids.')
param weightsStorageName string
param weightsShare string = 'flux-weights'

@description('Secret reference of the CloudAMQP URL in the environment: the job reads it from Key Vault.')
param amqpKeyVaultUrl string

param renderQueue string

@description('Private GHCR pull credential: the user name, and the Key Vault secret that holds a read:packages token. lantern-utilities is private, so its image is too.')
param registry { server: string, username: string }
param registryTokenKeyVaultUrl string

param image string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('CPU, memory of the single GPU replica. The T4 profile allows 8 vCPU and 56 GiB.')
param size { cpu: string, memory: string }

@description('Seconds a render may run before Azure stops it.')
param replicaTimeout int

@description('Most renders running at once. Each holds a whole T4.')
param maxExecutions int

@description('Messages waiting on the queue per execution started.')
param messagesPerExecution int = 1

var amqpSecret = {
  name: 'amqp-url'
  keyVaultUrl: amqpKeyVaultUrl
  identity: identityId
}

resource weights 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: weightsStorageName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
  }

  resource fileService 'fileServices' = {
    name: 'default'

    resource share 'shares' = {
      name: weightsShare
      properties: {
        shareQuota: 50
        enabledProtocols: 'SMB'
      }
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: last(split(environmentId, '/'))

  resource weightsMount 'storages' = {
    name: 'flux-weights'
    properties: {
      azureFile: {
        accountName: weights.name
        accountKey: weights.listKeys().keys[0].value
        shareName: weightsShare
        accessMode: 'ReadWrite'
      }
    }
  }
}

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: 'caj-${name}-render'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: gpuProfileName
    configuration: {
      triggerType: 'Event'
      replicaTimeout: replicaTimeout
      replicaRetryLimit: 0
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: maxExecutions
          pollingInterval: 30
          rules: [
            {
              name: renderQueue
              type: 'rabbitmq'
              metadata: {
                protocol: 'amqp'
                queueName: renderQueue
                mode: 'QueueLength'
                value: string(messagesPerExecution)
              }
              auth: [
                {
                  secretRef: amqpSecret.name
                  triggerParameter: 'host'
                }
              ]
            }
          ]
        }
      }
      secrets: [
        amqpSecret
        {
          name: 'registry-token'
          keyVaultUrl: registryTokenKeyVaultUrl
          identity: identityId
        }
      ]
      registries: [
        {
          server: registry.server
          username: registry.username
          passwordSecretRef: 'registry-token'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'lantern-render'
          image: image
          env: [
            { name: 'AZURE_CLIENT_ID', value: identityClientId }
            { name: 'Storage__BlobEndpoint', value: blobEndpoint }
            { name: 'Render__Container', value: renderContainer }
            { name: 'Broker__Url', secretRef: amqpSecret.name }
            { name: 'Render__Queue', value: renderQueue }
            { name: 'HF_HOME', value: '/weights/huggingface' }
            { name: 'FLUX_DTYPE', value: 'float16' }
          ]
          resources: {
            cpu: json(size.cpu)
            memory: size.memory
          }
          volumeMounts: [
            {
              volumeName: 'weights'
              mountPath: '/weights'
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'weights'
          storageType: 'AzureFile'
          storageName: environment::weightsMount.name
        }
      ]
    }
  }
}

output jobName string = job.name
