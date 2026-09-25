param name string
param location string
param tags object

@description('Principal id of the app identity. It gets Key Vault Secrets User.')
param appPrincipalId string

@description('Name of the secret that holds Security:Key. Bicep never creates it: the value must not be in git or in deployment history.')
param securityKeySecretName string = 'security-key'

// Built-in role: Key Vault Secrets User.
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Security:Key cannot be recovered once lost, and rotating it orphans every registration.
    // Purge protection keeps a deleted vault recoverable for the retention period.
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, appPrincipalId, secretsUserRoleId)
  properties: {
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
  }
}

output name string = vault.name
output uri string = vault.properties.vaultUri
// Versionless, so a new secret version is picked up on the next revision.
output securityKeySecretUri string = '${vault.properties.vaultUri}secrets/${securityKeySecretName}'
