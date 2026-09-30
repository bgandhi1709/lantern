using '../main.bicep'

param environmentName = 'uat'
param tables = [
  'parents'
  'families'
]

// Empty placeholder app. The API release sets the real image and port.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))

// Not a secret; see main.bicep. The only Firebase project so far, used for uat and local dev too.
param firebaseProjectId = 'lantern-ai-bg1709'
