# Lantern infrastructure

Bicep for the Lantern API, environment `uat`, deployed into the existing `rg-lantern-dev`
(Central India). One file, `main.bicep`; the environment is in `params/lantern.uat.bicepparam`.

## 1. Bootstrap, once

```bash
infra/bootstrap.sh rg-lantern-dev uat
```

Needs Owner or User Access Administrator on the resource group. Safe to run again. It creates the
identity `id-lantern-uat`, the Key Vault `kv-lantern-uat`, the roles the identity needs (Storage Table
Data Contributor on the resource group, Key Vault Secrets User on the vault) and the secret
`security-key`, only if it is missing.

**`security-key` derives the uid hash key and the field encryption key. Rotating it makes every
registration unreadable.** Save a copy (the script prints the command).

## 2. Deploy

```bash
az deployment group what-if -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
az deployment group create  -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
```

The deploying account needs Contributor only. It creates the storage account `lanternuat` with the table
`parents`, a Log Analytics workspace, and the Container Apps environment and app (0 to 1 replica).
All names come from `environmentName`. The template reads the identity and the vault by name.

App settings, named after the API's configuration sections: `Firebase__ProjectId`,
`Storage__TableEndpoint`, `Security__Key` (from Key Vault) and `AZURE_CLIENT_ID`. The table name is the
API's default, `parents`, so it must stay in `tables`; add tables, never rename one.

Environment variables read by the params file: `FIREBASE_PROJECT_ID`, `CONTAINER_IMAGE` (a placeholder
until an API image exists) and `CONTAINER_PORT` (`80` for the placeholder, `8080` for the .NET image).

## Not here yet

Blob containers and queues (add when the API uses them), a deploy workflow with OIDC, the API image
build, Foundry, budget alerts, a prod environment.
