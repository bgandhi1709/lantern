targetScope = 'resourceGroup'

@description('Short environment name, used in every resource name.')
param environmentName string

param location string = resourceGroup().location

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

@description('Wire Security:Key from Key Vault into the app. Set true only after the secret exists in the vault (see infra/README.md).')
param wireSecurityKey bool = false

param tags object = {
  workload: 'lantern'
  env: environmentName
}

var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'

// The Key Vault name is global, so it carries a suffix derived from the resource group. It is
// stable: redeploying to the same group gives the same name.
var suffix = take(uniqueString(resourceGroup().id), 5)
var namePrefix = 'lantern-${environmentName}'

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    name: 'id-${namePrefix}'
    location: location
    tags: tags
  }
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
    appPrincipalId: identity.outputs.principalId
    tables: tables
    containers: containers
    queues: queues
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: 'kv-${namePrefix}-${suffix}'
    location: location
    tags: tags
    appPrincipalId: identity.outputs.principalId
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
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    image: containerImage
    targetPort: containerPort
    enableProbes: containerImage != placeholderImage
    firebaseProjectId: firebaseProjectId
    tableEndpoint: storage.outputs.tableEndpoint
    securityKeySecretUri: wireSecurityKey ? keyVault.outputs.securityKeySecretUri : ''
  }
}

output identityClientId string = identity.outputs.clientId
output storageAccountName string = storage.outputs.name
output keyVaultName string = keyVault.outputs.name
output containerAppName string = containerApp.outputs.appName
output containerAppFqdn string = containerApp.outputs.fqdn
