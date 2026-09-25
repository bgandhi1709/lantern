# Lantern infrastructure

Bicep for the Lantern API. Parameters for the `uat` environment are in `params/lantern.uat.bicepparam`.
It deploys into the existing `rg-lantern-dev` (Central India). The resource group is created outside this template.

## Two steps

1. **`bootstrap.sh`, once.** Creates what the template only reads.
2. **`main.bicep`, every time.** Creates the storage account, logs and the Container App.

### 1. Bootstrap (once per environment)

```bash
infra/bootstrap.sh rg-lantern-dev uat
```

Needs Owner or User Access Administrator on the resource group. It is safe to run again. It creates:

| Resource | Name (uat) | Notes |
|---|---|---|
| User-assigned managed identity | `id-lantern-uat` | The one identity the API uses for storage and Key Vault |
| Key Vault | `kv-lantern-uat` | RBAC, soft delete, purge protection |
| Secret | `security-key` | Random, base64, 32 bytes. Never overwritten on a re-run |
| Roles for the identity | | Storage Table, Blob and Queue Data Contributor on the resource group; Key Vault Secrets User on the vault |

The storage roles are on the resource group because the storage account does not exist yet. The
identity therefore reaches any storage account in that group.

**`security-key` derives the uid hash key and the field encryption key. Rotating it makes every
registration unreadable.** Save a copy (the script prints the command).

### 2. Deploy

```bash
az deployment group what-if -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
az deployment group create  -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
```

The deploying account needs Contributor only: the template makes no role assignments.

| Resource | Name (uat) | Why |
|---|---|---|
| Log Analytics workspace | `log-lantern-uat` | Console logs from the Container App |
| Storage account | `lanternuat` | Shared keys off, TLS 1.2, no public blob access |
| Table `parents` | | The API's only table (registration data) |
| Blob containers `raw`, `ncert` | | Private. Not used by the API yet; reserved for the NCERT build output |
| Queues `lantern-events`, `-poison`, `-parked` | | Not used by the API yet |
| Container Apps environment | `cae-lantern-uat` | Consumption only, no fixed charge |
| Container App | `ca-lantern-uat` | The API. 0.25 vCPU, 0.5 GiB, 0 to 1 replica |

Storage and table names only grow. A renamed table or container is a new, empty one.

## App settings

The API binds the sections `Firebase`, `Storage` and `Security` with no prefix. Settings that are
wrong or missing stop the app at start-up. The template looks up the identity and the vault by name,
so no ids are passed in.

| Setting | Source |
|---|---|
| `Firebase__ProjectId` | `firebaseProjectId` in the params file |
| `Storage__TableEndpoint` | The storage account, in Bicep |
| `Security__Key` | Key Vault secret `security-key`, read with the identity |
| `AZURE_CLIENT_ID` | Client id of the identity, looked up in Bicep |

Table, container, queue, storage account, identity and vault names are plain values in
`params/lantern.uat.bicepparam`. They are not secrets and not app settings. The API's default
table name, `parents`, must stay in the `tables` list. `Storage__ConnectionString` is never set in
Azure. It is for Azurite only.

Environment variables read by `params/lantern.uat.bicepparam`:

| Variable | Default | Meaning |
|---|---|---|
| `FIREBASE_PROJECT_ID` | `lantern-ai-bg1709` | Firebase project |
| `CONTAINER_IMAGE` | MCR quickstart placeholder | API image, for example `ghcr.io/bgandhi1709/lantern-api:sha-<sha>` |
| `CONTAINER_PORT` | `80` | `8080` for the .NET SDK container image |

Health probes on `/health/live` are on for any image except the placeholder.

## Not here yet

- A GitHub Actions deploy workflow and its Entra OIDC app registration.
- Building and publishing the API image (the API branch plans `dotnet publish -t:PublishContainer` to GHCR).
- Key Vault key for envelope encryption, Foundry, budget alerts, a prod environment, private networking.
