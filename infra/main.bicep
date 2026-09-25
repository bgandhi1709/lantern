targetScope = 'resourceGroup'

@description('Short environment name, used in every resource name.')
param environmentName string

param location string = resourceGroup().location

@description('Name of the user-assigned identity the app runs as. Created once by bootstrap.sh, not by this template.')
param identityName string

@description('Name of the Key Vault that holds the security-key secret. Created once by bootstrap.sh, in this resource group.')
param keyVaultName string

@description('Storage account name. Global, 3 to 24 lowercase letters and digits. Set in the .bicepparam file.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Table names. The API reads `parents` by default, so it must be listed. Add only; a renamed table is a new, empty one.')
param tables array

param containers array

param queues array

@description('Firebase project id. Public, not a secret: the API validates Firebase tokens against it.')
param firebaseProjectId string

@description('Image the container app runs. The placeholder lets infrastructure be provisioned before the first API image exists.')
param containerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Port the image listens on: 80 for the placeholder, 8080 for the .NET SDK container image.')
param containerPort int = 80

param tags object = {
  workload: 'lantern'
  env: environmentName
}

var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'

var namePrefix = 'lantern-${environmentName}'

// Created once by bootstrap.sh, together with its role assignments. The template only reads it,
// so the deploying account needs no role-assignment rights.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    workspaceName: 'log-${namePrefix}'
    location: location
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: storageAccountName
    location: location
    tags: tags
    tables: tables
    containers: containers
    queues: queues
  }
}

module containerApp 'modules/containerapp.bicep' = {
  name: 'containerapp'
  params: {
    environmentName: 'cae-${namePrefix}'
    appName: 'ca-${namePrefix}'
    location: location
    tags: tags
    workspaceName: monitoring.outputs.workspaceName
    identityId: identity.id
    identityClientId: identity.properties.clientId
    image: containerImage
    targetPort: containerPort
    enableProbes: containerImage != placeholderImage
    firebaseProjectId: firebaseProjectId
    tableEndpoint: storage.outputs.tableEndpoint
    // Versionless, so a new secret version is picked up on the next revision.
    securityKeySecretUri: '${vault.properties.vaultUri}secrets/security-key'
  }
}

output identityClientId string = identity.properties.clientId
output storageAccountName string = storage.outputs.name
output containerAppName string = containerApp.outputs.appName
output containerAppFqdn string = containerApp.outputs.fqdn
