# Lantern infrastructure

Bicep for the Lantern API. Parameters for the `uat` environment are in `params/lantern.uat.bicepparam`.
It deploys into the existing `rg-lantern-dev` (Central India). The resource group is created outside this template.

## What it creates

| Resource | Name (uat) | Why |
|---|---|---|
| User-assigned managed identity | `id-lantern-uat` | The API signs in to Azure with it (`DefaultAzureCredential`) |
| Log Analytics workspace | `log-lantern-uat` | Console logs from the Container App |
| Storage account | `stlanternuat55fkc` | Shared keys off, TLS 1.2, no public blob access |
| Table `parents` | | The API's only table (registration data) |
| Blob containers `raw`, `ncert` | | Private. Not used by the API yet; reserved for the NCERT build output |
| Queues `lantern-events`, `-poison`, `-parked` | | Not used by the API yet |
| Key Vault | `kv-lantern-uat-<suffix>` | Holds `Security:Key`. RBAC, soft delete, purge protection |
| Container Apps environment | `cae-lantern-uat` | Consumption only, no fixed charge |
| Container App | `ca-lantern-uat` | The API. 0.25 vCPU, 0.5 GiB, 0 to 1 replica |

The identity gets Storage Table, Blob and Queue Data Contributor on the account and
Key Vault Secrets User on the vault. `<suffix>` comes from the resource group id and is stable.

Storage, queue and container names only grow. A renamed table or container is a new, empty one.

## App settings

The API binds the sections `Firebase`, `Storage` and `Security` with no prefix. Settings that are
wrong or missing stop the app at start-up.

| Setting | Source |
|---|---|
| `Firebase__ProjectId` | `firebaseProjectId` parameter |
| `Storage__TableEndpoint` | Storage account output |
| `Security__Key` | Key Vault secret `security-key` (see below) |
| `AZURE_CLIENT_ID` | Identity client id |

Table, container, queue and storage account names are plain values in `params/lantern.uat.bicepparam`,
not secrets and not app settings. The API's default table name, `parents`, must stay in the `tables` list.
`Storage__ConnectionString` is never set in Azure. It is for Azurite only.

## Deploy

```bash
az deployment group what-if -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
az deployment group create  -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
```

Environment variables read by `params/lantern.uat.bicepparam`:

| Variable | Default | Meaning |
|---|---|---|
| `FIREBASE_PROJECT_ID` | `lantern-ai-bg1709` | Firebase project |
| `CONTAINER_IMAGE` | MCR quickstart placeholder | API image, for example `ghcr.io/bgandhi1709/lantern-api:sha-<sha>` |
| `CONTAINER_PORT` | `80` | `8080` for the .NET SDK container image |
| `WIRE_SECURITY_KEY` | `false` | `true` once the secret exists |

Health probes on `/health/live` are on for any image except the placeholder.

## Security key bootstrap

`Security:Key` (base64, at least 32 bytes) derives the uid hash key and the field encryption key.
It is not in Bicep or git. **Rotating it makes every existing registration unreadable. Back it up.**

A Container App secret that points at Key Vault fails the deployment if the secret is missing, so
this takes two deployments:

1. Deploy with `WIRE_SECURITY_KEY=false`. This creates everything.
2. Give yourself `Key Vault Secrets Officer` on the vault, then create the secret once:
   ```bash
   KV=$(az deployment group show -g rg-lantern-dev -n main --query properties.outputs.keyVaultName.value -o tsv)
   az keyvault secret set --vault-name "$KV" -n security-key --value "$(openssl rand -base64 32)"
   ```
3. Deploy again with `WIRE_SECURITY_KEY=true` and the real `CONTAINER_IMAGE` and `CONTAINER_PORT=8080`.

## Not here yet

- A GitHub Actions deploy workflow and its Entra OIDC app registration.
- Building and publishing the API image (the API branch plans `dotnet publish -t:PublishContainer` to GHCR).
- Key Vault key for envelope encryption, Foundry, budget alerts, a prod environment, private networking.
