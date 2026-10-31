#!/usr/bin/env bash
# One-time setup for one environment, run before the first deployment. Safe to run again.
# Creates what the Bicep only reads: the app identity, its role assignments, the Key Vault and
# the secret and key named in the bicepparam's keyVault. Needs Owner (or User Access Administrator) on the resource group.
#
# Usage: infra/bootstrap.sh [resource-group] [environment] [deploy-client-id]
# The deploy client id (the GitHub variable AZURE_CLIENT_ID) gets Key Vault Secrets User on the amqp-url secret alone, so
# the release can apply infra/topology.json to the broker. It still cannot read any other secret.
set -euo pipefail

RG="${1:-rg-lantern-dev}"
ENV_NAME="${2:-uat}"
DEPLOY_CLIENT_ID="${3:-}"
IDENTITY="id-lantern-${ENV_NAME}"
VAULT="kv-lantern-${ENV_NAME}"

# The secret name comes from the environment's bicepparam, so the app settings and this script never differ.
PARAMS_FILE="$(dirname "$0")/params/lantern.${ENV_NAME}.bicepparam"
source "$(dirname "$0")/kv-param.sh"
SECRET=$(key_vault_param securityKeySecret)
AMQP_SECRET=$(key_vault_param amqpUrlSecret)

SUB_ID=$(az account show --query id -o tsv)

# Every provider main.bicep deploys from. Registration is per subscription, once; the deploy identity cannot do it.
for ns in Microsoft.Storage Microsoft.App Microsoft.OperationalInsights Microsoft.Insights Microsoft.KeyVault Microsoft.ManagedIdentity; do
  echo "Provider ${ns}"
  az provider register --namespace "$ns" --wait -o none
done
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

# One identity for the API and the Functions app. The Storage roles are on the resource group so
# they cover the accounts the template creates later. Key Vault Secrets User is on the vault.
assign() { # role scope assignee-object-id assignee-type
  az role assignment create --role "$1" --scope "$2" --assignee-object-id "$3" \
    --assignee-principal-type "$4" -o none
}
echo "Roles for ${IDENTITY}"
assign "Storage Table Data Contributor" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
# Covers every blob container, so the API reads ncert, ssc and any later one with no further role (D82).
assign "Storage Blob Data Contributor" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
# The Functions host keeps its own state (leases) in the storage account, which needs Blob Data Owner.
assign "Storage Blob Data Owner" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
# Both apps export telemetry to Application Insights with this identity: the resource has no ingestion key.
assign "Monitoring Metrics Publisher" "$RG_ID" "$PRINCIPAL_ID" ServicePrincipal
assign "Key Vault Secrets User" "$VAULT_ID" "$PRINCIPAL_ID" ServicePrincipal

# The person running this creates the secret, so they need to write to the vault.
ME=$(az ad signed-in-user show --query id -o tsv)
assign "Key Vault Secrets Officer" "$VAULT_ID" "$ME" User

# So the Board content can be uploaded to the ncert blob container once it exists. On the resource
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

if az keyvault secret show --vault-name "$VAULT" -n "$AMQP_SECRET" -o none 2>/dev/null; then
  echo "Secret ${AMQP_SECRET} exists, left as is"
else
  echo "Creating placeholder ${AMQP_SECRET}; set the real URL as infra/README.md describes"
  az keyvault secret set --vault-name "$VAULT" -n "$AMQP_SECRET" --value placeholder --content-type placeholder -o none
fi

if [ -n "$DEPLOY_CLIENT_ID" ]; then
  echo "Read access to ${AMQP_SECRET} for the deploy identity"
  DEPLOY_OID=$(az ad sp show --id "$DEPLOY_CLIENT_ID" --query id -o tsv)
  assign "Key Vault Secrets User" "${VAULT_ID}/secrets/${AMQP_SECRET}" "$DEPLOY_OID" ServicePrincipal
fi

echo "Done. Next: az deployment group create -g ${RG} -f infra/main.bicep -p infra/params/lantern.${ENV_NAME}.bicepparam"
