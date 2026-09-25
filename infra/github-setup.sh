#!/usr/bin/env bash
# One-time setup so .github/workflows/infra.yml can sign in to Azure without a stored secret.
# Creates an Entra app with federated credentials for this repository, gives it Contributor on
# the resource group, and sets the GitHub variables and the `uat` environment (you approve deploys).
# Needs: az login (able to create app registrations, Owner on the resource group) and gh auth login.
#
# Usage: infra/github-setup.sh [resource-group] [environment]
set -euo pipefail

RG="${1:-rg-lantern-dev}"
ENV_NAME="${2:-uat}"
APP_NAME="github-lantern-infra-${ENV_NAME}"
REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "App registration ${APP_NAME}"
CLIENT_ID=$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)
if [ -z "$CLIENT_ID" ]; then
  CLIENT_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
fi
az ad sp show --id "$CLIENT_ID" -o none 2>/dev/null || az ad sp create --id "$CLIENT_ID" -o none
SP_ID=$(az ad sp show --id "$CLIENT_ID" --query id -o tsv)

# Pull requests (what-if), pushes to main (what-if) and the gated deploy each present a different
# subject, so each needs its own credential.
credential() { # name subject
  if az ad app federated-credential list --id "$CLIENT_ID" --query "[?name=='$1'].name" -o tsv | grep -q .; then
    return
  fi
  az ad app federated-credential create --id "$CLIENT_ID" --parameters "{
    \"name\": \"$1\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"$2\",
    \"audiences\": [\"api://AzureADTokenExchange\"]}" -o none
}
credential "pull-request" "repo:${REPO}:pull_request"
credential "main" "repo:${REPO}:ref:refs/heads/main"
credential "env-${ENV_NAME}" "repo:${REPO}:environment:${ENV_NAME}"

echo "Contributor on ${RG}"
az role assignment create --role Contributor --assignee-object-id "$SP_ID" \
  --assignee-principal-type ServicePrincipal \
  --scope "/subscriptions/${SUB_ID}/resourceGroups/${RG}" -o none

echo "GitHub variables and environment ${ENV_NAME}"
gh variable set AZURE_CLIENT_ID --body "$CLIENT_ID"
gh variable set AZURE_TENANT_ID --body "$TENANT_ID"
gh variable set AZURE_SUBSCRIPTION_ID --body "$SUB_ID"
ME=$(gh api user --jq .id)
gh api -X PUT "repos/${REPO}/environments/${ENV_NAME}" \
  --input - -o /dev/null <<JSON
{"reviewers":[{"type":"User","id":${ME}}],"deployment_branch_policy":{"protected_branches":false,"custom_branch_policies":true}}
JSON
gh api -X POST "repos/${REPO}/environments/${ENV_NAME}/deployment-branch-policies" \
  -f name=main -f type=branch >/dev/null 2>&1 || true
echo "Done."
