using '../main.bicep'

param environmentName = 'dev'
param firebaseProjectId = readEnvironmentVariable('FIREBASE_PROJECT_ID', 'lantern-ai-bg1709')

// The API workflow sets these to the GHCR image and 8080 once an image exists.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))

// Set WIRE_SECURITY_KEY=true only after the security-key secret exists in Key Vault.
param wireSecurityKey = readEnvironmentVariable('WIRE_SECURITY_KEY', 'false') == 'true'
