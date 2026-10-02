#!/usr/bin/env bash
# One-time setup for one environment, run before the first deployment. Safe to run again.
# Creates what the Bicep only reads: the app identity, its role assignments, the Key Vault and
# the secret and key named in the bicepparam's keyVault. Needs Owner (or User Access Administrator) on the resource group.
#
# Usage: infra/bootstrap.sh [resource-group] [environment]
set -euo pipefail

RG="${1:-rg-lantern-dev}"
ENV_NAME="${2:-uat}"
IDENTITY="id-lantern-${ENV_NAME}"
VAULT="kv-lantern-${ENV_NAME}"

# The secret and key names come from the environment's bicepparam, so the app settings and this script never differ.
PARAMS_FILE="$(dirname "$0")/params/lantern.${ENV_NAME}.bicepparam"
key_vault_param() {
  az bicep build-params --file "$PARAMS_FILE" --stdout \
    | python3 -c "import json, sys; print(json.loads(json.load(sys.stdin)['parametersJson'])['parameters']['keyVault']['value']['$1'])"
}
SECRET=$(key_vault_param securityKeySecret)
FAMILY_KEY=$(key_vault_param familyKey)

SUB_ID=$(az account show --query id -o tsv)
RG_ID="/subscriptions/${SUB_ID}/resourceGroups/${RG}"
LOCATION=$(az group show -n "$RG" --query location -o tsv)

echo "Identity ${IDENTITY}"
az identity create -g "$RG" -n "$IDENTITY" -l "$LOCATION" -o none
PRINCIPAL_ID=$(az identity show -g "$RG" -n "$IDENTITY" --query principalId -o tsv)

echo "Key Vault ${VAULT}"
if ! az keyvault show -n "$VAULT" -g "$RG" -o none 2>/dev/null; then
  az keyvault create -g "$RG" -n "$VAULT" -l "$LOCATION" \
    --enable-rbac-authorization true --retention-days 7 --enable-purge-protection true -o none
fi
VAULT_ID=$(az keyvault show -n "$VAULT" -g "$RG" --query id -o tsv)

# One identity for the API and the Functions app. The Storage and Service Bus roles are on the resource group so
# they cover the accounts the template creates later. Key Vault Secrets User is on the vault.
assign() { # role scope assignee-object-id assignee-type
  az role assignment create --role "$1" --scope "$2" --assignee-object-id "$3" \
    --assignee-principal-type "$4" -o none
}
echo "Roles for ${IDENTITY}"
assign "Storage Table Data Contributor" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
assign "Storage Blob Data Contributor" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
# The Functions host keeps its own state (leases) in the storage account, which needs Blob Data Owner.
assign "Storage Blob Data Owner" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
# The API sends actions and the Functions app receives them.
assign "Azure Service Bus Data Sender" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
assign "Azure Service Bus Data Receiver" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
assign "Key Vault Secrets User" "$VAULT_ID" "$PRINCIPAL_ID" ServicePrincipal
# Wrap/unwrap only, not manage: the app never needs to create or delete the key itself.
assign "Key Vault Crypto User" "$VAULT_ID" "$PRINCIPAL_ID" ServicePrincipal

# The person running this creates the secret and the key, so they need to write to the vault.
ME=$(az ad signed-in-user show --query id -o tsv)
assign "Key Vault Secrets Officer" "$VAULT_ID" "$ME" User
assign "Key Vault Crypto Officer" "$VAULT_ID" "$ME" User
# Local development has no Key Vault emulator: dev machines call the real vault via `az login`.
assign "Key Vault Crypto User" "$VAULT_ID" "$ME" User

# So `ncert-build upload` can write to the ncert blob container once it exists. On the resource
# group like the Table role above, since the storage account isn't created yet either.
assign "Storage Blob Data Contributor" "$RG_ID" "$ME" User

# Owner grants no Storage data-plane access on its own (allowSharedKeyAccess is false), so without
# this the person running this script can't read/write the parents table either - same gap as Blob.
assign "Storage Table Data Contributor" "$RG_ID" "$ME" User

# Create the secret only if it is missing. Overwriting or rotating it makes every existing
# registration unreadable.
if az keyvault secret show --vault-name "$VAULT" -n "$SECRET" -o none 2>/dev/null; then
  echo "Secret ${SECRET} exists, left as is"
else
  echo "Creating secret ${SECRET} (role assignments can take a minute to apply)"
  for attempt in 1 2 3 4 5 6; do
    if az keyvault secret set --vault-name "$VAULT" -n "$SECRET" \
         --value "$(openssl rand -base64 32)" -o none 2>/dev/null; then
      break
    fi
    [ "$attempt" -eq 6 ] && { echo "Could not write the secret" >&2; exit 1; }
    sleep 20
  done
  echo "Save a copy: az keyvault secret show --vault-name ${VAULT} -n ${SECRET} --query value -o tsv"
  echo "Losing it loses access to all registrations."
fi

# Create the key only if it is missing. Its private material never leaves the vault — rotating it
# is safe (Key Vault keeps prior versions, so already-wrapped family keys keep unwrapping), but
# deleting it makes every family's data unreadable.
if az keyvault key show --vault-name "$VAULT" -n "$FAMILY_KEY" -o none 2>/dev/null; then
  echo "Key ${FAMILY_KEY} exists, left as is"
else
  echo "Creating key ${FAMILY_KEY} (role assignments can take a minute to apply)"
  for attempt in 1 2 3 4 5 6; do
    if az keyvault key create --vault-name "$VAULT" -n "$FAMILY_KEY" \
         --kty RSA --size 2048 --ops wrapKey unwrapKey -o none 2>/dev/null; then
      break
    fi
    [ "$attempt" -eq 6 ] && { echo "Could not create the key" >&2; exit 1; }
    sleep 20
  done
fi
echo "Done. Next: az deployment group create -g ${RG} -f infra/main.bicep -p infra/params/lantern.${ENV_NAME}.bicepparam"
