using '../main.bicep'

param environmentName = 'uat'

param tables = {
  parents: 'parents'
  families: 'families'
  actions: 'actions'
}

// The Workspace container keeps its first name: renaming it would leave every existing Workspace behind.
param containers = {
  workspaces: 'family'
  ncert: 'ncert'
  ssc: 'ssc'
}

param keyVault = {
  securityKeySecret: 'security-key'
}

// A five-minute lock and 288 deliveries: a failing action retries for about a day before it is dead-lettered.
param actionsQueue = {
  name: 'workspace-events'
  lockDuration: 'PT5M'
  maxDeliveryCount: 288
  messageTimeToLive: 'P14D'
}

param apiSize = {
  cpu: '0.25'
  memory: '0.5Gi'
  minReplicas: 0
  maxReplicas: 1
}

param functionsSize = {
  cpu: '0.5'
  memory: '1Gi'
  minReplicas: 0
  maxReplicas: 1
}

// 30 days kept; 1 GB a day is far above pilot traffic and stops a runaway from costing more than a few rupees a day.
param telemetry = {
  retentionDays: 30
  dailyCapGb: 1
}

param childrenPerMinute = 30
param actionResendAfter = '00:05:00'

// Empty placeholder apps. The API release sets the real images and the API's port.
param containerImage = readEnvironmentVariable('CONTAINER_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param functionsImage = readEnvironmentVariable('FUNCTIONS_IMAGE', 'mcr.microsoft.com/k8se/quickstart:latest')
param containerPort = int(readEnvironmentVariable('CONTAINER_PORT', '80'))

// Not a secret; see main.bicep. The only Firebase project so far, used for uat and local dev too.
param firebaseProjectId = 'lantern-ai-bg1709'
