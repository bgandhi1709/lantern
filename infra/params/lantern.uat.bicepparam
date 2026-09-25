using '../main.bicep'

param environmentName = 'uat'
param firebaseProjectId = readEnvironmentVariable('FIREBASE_PROJECT_ID', 'lantern-ai-bg1709')
param tables = [
  'parents'
]

// The API workflow sets these to the GHCR image and 8080 once an image exists.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))
