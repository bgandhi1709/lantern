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
# GitHub may put the owner and repository ids in the token subject, for example
# repo:owner@123/name@456. Ask GitHub for the exact prefix instead of guessing it.
PREFIX=$(gh api "repos/${REPO}/actions/oidc/customization/sub" --jq .sub_claim_prefix 2>/dev/null || true)
PREFIX="${PREFIX:-repo:${REPO}}"
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
  local body="{
    \"name\": \"$1\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"$2\",
    \"audiences\": [\"api://AzureADTokenExchange\"]}"
  if az ad app federated-credential list --id "$CLIENT_ID" --query "[?name=='$1'].name" -o tsv | grep -q .; then
    az ad app federated-credential update --id "$CLIENT_ID" --federated-credential-id "$1" --parameters "$body" -o none
  else
    az ad app federated-credential create --id "$CLIENT_ID" --parameters "$body" -o none
  fi
}
credential "pull-request" "${PREFIX}:pull_request"
credential "main" "${PREFIX}:ref:refs/heads/main"
credential "env-${ENV_NAME}" "${PREFIX}:environment:${ENV_NAME}"

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
  --input - >/dev/null <<JSON
{"reviewers":[{"type":"User","id":${ME}}]}
JSON
echo "Done."
