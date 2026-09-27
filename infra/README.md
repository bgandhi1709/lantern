# Lantern infrastructure

Base infrastructure for Lantern, environment `uat`, deployed into the existing `rg-lantern-dev`
(Central India). One file, `main.bicep`; the environment is in `params/lantern.uat.bicepparam`.
It creates the Container App fully wired (Firebase project id, storage table endpoint, the
`security-key` secret from Key Vault) but on a placeholder image; the API release swaps in the
real image and port (`.github/workflows/api.yml`), nothing else.

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
`parents`, a Log Analytics workspace, and the Container Apps environment and app (0 to 1 replica, the
identity attached). All names come from `environmentName`. The template reads the identity and the
Key Vault by name and wires the app's settings: `Firebase__ProjectId` (a param, not a secret),
`Storage__TableEndpoint` (read from the storage account), `Security__Key` (a Key Vault secret
reference), and `AZURE_CLIENT_ID` (the identity's client id, needed since `DefaultAzureCredential`
can't otherwise tell which user-assigned identity to use). Add tables, never rename one.

The API release, not this template, sets the real image and port. Until then the params file uses a
placeholder image on port 80. Later deployments of this template read the running image and port
first (`keep-running-image.sh`), so they never put the placeholder back.

## 3. GitHub Actions

`.github/workflows/infra.yml` runs only when `infra/**` changes, so an API release never triggers it.
Every run builds, lints and runs `what-if`, then the deploy job waits for the reviewer on the `uat`
environment. Nothing is deployed until you approve, whether the run comes from a pull request, a merge
to `main` or a manual run. Reject the request to skip a release.

```bash
infra/github-setup.sh rg-lantern-dev uat   # once: Entra app with OIDC, Contributor on the group, variables
```

No secret is stored. The variables are `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`.

## Not here yet

Blob containers and queues (add when the API uses them), Foundry, a prod environment.
