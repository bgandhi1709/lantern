using '../main.bicep'

param environmentName = 'uat'
param tables = [
  'parents'
]

// Empty placeholder app. The API release sets the real image and port.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))
