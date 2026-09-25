param workspaceName string
param location string
param tags object

// Container Apps ships the app's console logs here. The API has no Application Insights SDK
// yet, so there is no App Insights resource.
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

output workspaceName string = workspace.name
