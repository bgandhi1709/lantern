targetScope = 'resourceGroup'

@description('Short environment name, used in every resource name.')
param environmentName string

param location string = resourceGroup().location

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

// Storage and Key Vault names are global, so they carry a suffix derived from the resource
// group. It is stable: redeploying to the same group gives the same names.
var suffix = take(uniqueString(resourceGroup().id), 5)
var namePrefix = 'lantern-${environmentName}'

// Add to these lists; never rename or remove. A renamed table, container or queue is a new,
// empty one.
var parentsTable = 'parents'
var tables = [
  parentsTable
]
var containers = [
  'raw'
  'ncert'
]
var queues = [
  'lantern-events'
  'lantern-events-poison'
  'lantern-events-parked'
]

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
    name: 'stlantern${environmentName}${suffix}'
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
    parentsTable: parentsTable
    securityKeySecretUri: wireSecurityKey ? keyVault.outputs.securityKeySecretUri : ''
  }
}

output identityClientId string = identity.outputs.clientId
output storageAccountName string = storage.outputs.name
output keyVaultName string = keyVault.outputs.name
output containerAppName string = containerApp.outputs.appName
output containerAppFqdn string = containerApp.outputs.fqdn
