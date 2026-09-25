param name string
param location string
param tags object

// One identity for the app. It is user-assigned so its role assignments exist before the
// Container App does, and so the app can use it to read a Key Vault secret at first start.
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
