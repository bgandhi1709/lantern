using '../main.bicep'

param environmentName = 'uat'
// Created once by bootstrap.sh in this resource group; the template only reads them.
param identityName = 'id-lantern-uat'
param keyVaultName = 'kv-lantern-uat'

param firebaseProjectId = readEnvironmentVariable('FIREBASE_PROJECT_ID', 'lantern-ai-bg1709')

// Names are not secrets, so they live here in git. The storage account name is global: 3 to 24
// lowercase letters and digits. Once created, never change it: a new name is a new, empty account.
param storageAccountName = 'lanternuat'

// The API reads the table `parents` by default (Storage:ParentsTable in appsettings.json), so it
// must be in this list. Add tables; never rename or remove one.
param tables = [
  'parents'
]
param containers = [
  'raw'
  'ncert'
]
param queues = [
  'lantern-events'
  'lantern-events-poison'
  'lantern-events-parked'
]

// The API workflow sets these to the GHCR image and 8080 once an image exists.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))
