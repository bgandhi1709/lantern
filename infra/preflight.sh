#!/usr/bin/env bash
# Read-only checks for what infra/bootstrap.sh sets up once and the deployment cannot: the resource providers the
# template uses and the identity's roles. Runs on every PR (infra-check) and before every release, so a gap fails
# the PR with its fix instead of the release after merge.
#
# Usage: infra/preflight.sh [resource-group] [environment]
set -euo pipefail

RG="${1:-rg-lantern-dev}"
ENV_NAME="${2:-uat}"
IDENTITY="id-lantern-${ENV_NAME}"
FIX="Run infra/bootstrap.sh ${RG} ${ENV_NAME} with an Owner login."
failed=0

# Every provider main.bicep deploys a resource from.
for ns in Microsoft.Storage Microsoft.ServiceBus Microsoft.App Microsoft.OperationalInsights Microsoft.KeyVault Microsoft.ManagedIdentity; do
  state=$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo unknown)
  case "$state" in
    Registered) ;;
    unknown) echo "::warning::Could not read the registration of ${ns}; the deploying identity may lack subscription read." ;;
    *) echo "::error::The subscription has not registered ${ns} (${state}). ${FIX}"; failed=1 ;;
  esac
done

principal=$(az identity show -g "$RG" -n "$IDENTITY" --query principalId -o tsv)
roles=$(az role assignment list --assignee-object-id "$principal" --all --query "[].roleDefinitionName" -o tsv)
for role in "Storage Table Data Contributor" "Storage Blob Data Contributor" "Storage Blob Data Owner" \
            "Azure Service Bus Data Sender" "Azure Service Bus Data Receiver" "Key Vault Secrets User" "Key Vault Crypto User"; do
  grep -qxF "$role" <<<"$roles" || { echo "::error::${IDENTITY} lacks '${role}'. ${FIX}"; failed=1; }
done

exit "$failed"
