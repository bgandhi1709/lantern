# Lantern infrastructure

Base infrastructure for Lantern, environment `uat`, deployed into the existing `rg-lantern-dev`
(Central India). One file, `main.bicep`; the environment is in `params/lantern.uat.bicepparam`.
It creates the Container App fully wired (Firebase project id, storage table endpoint, the
`security-key` secret from Key Vault, the `family-field-key` name and vault URI) but on a
placeholder image; the API release sets the real image and port (`.github/workflows/api.yml`),
nothing else.

## 1. Bootstrap, once

```bash
infra/bootstrap.sh rg-lantern-dev uat
```

Needs Owner or User Access Administrator on the resource group. Safe to run again. It creates the
identity `id-lantern-uat`, the Key Vault `kv-lantern-uat`, the roles the identity needs (Storage Table
Data Contributor, Storage Blob Data Contributor, Storage Blob Data Owner (the Functions host's own state), Azure Service Bus Data Sender and Azure Service Bus Data Receiver on the resource group, Key Vault Secrets User and Key Vault Crypto User on the vault),
a Storage Blob Data Contributor role on the resource group for whoever runs it (so `ncert-build upload`
works once the container exists), the secret `security-key`, and the key `family-field-key`, each only
if missing. Whoever runs it also gets Key Vault Crypto User on the vault, since local development has
no Key Vault emulator and calls the real vault through `az login`.

**`security-key` derives the uid hash key. Rotating it makes every registration unreadable.**
Save a copy (the script prints the command). **`family-field-key` wraps each family's own
field-encryption key (issue #23) — its private material never leaves the vault.** Deleting it makes
every family's data unreadable; rotating it is safe, since Key Vault keeps prior versions and
already-wrapped keys keep unwrapping.

## 2. Deploy

```bash
az deployment group what-if -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
az deployment group create  -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
```

Re-run `bootstrap.sh` before the first release that uses Service Bus or the Functions app: the identity needs the new roles, and the release preflight fails with the fix if they are missing.

The deploying account needs Contributor only. It creates the storage account `lanternuat` with the tables
`parents`, `families` and `actions` (the action ledger), the private `family` blob container (every Child's Workspace, a folder per Class; blob soft delete and versioning are off, so a deleted Child's files really go), the private `ncert` blob container (for `ncert-build`'s output — see
[`tools/ncert-build`](../tools/ncert-build/README.md)), a Service Bus namespace `sb-lantern-uat` (Basic, no local keys) with the queue `workspace-events` (every Workspace event), a Log Analytics workspace, and the Container
Apps environment with two apps (each 0 to 1 replica, the identity attached): the API `ca-lantern-uat`, and `ca-lantern-uat-functions`, which runs `Lantern.Functions` with no ingress and is woken by a KEDA Service Bus rule when a message arrives ([ADR-0004](../docs/adr/0004-actions-through-service-bus-and-functions.md)). All names come from
`environmentName`. The template reads the identity and the Key Vault by name and wires the app's
settings: `Firebase__ProjectId` (a param, not a secret), `Storage__TableEndpoint` and `Storage__BlobEndpoint` (read from the
storage account), `ServiceBus__FullyQualifiedNamespace` (the Service Bus namespace the API sends to), `Actions__Queue` (the queue's name, read by both apps), `Security__Key` (a Key Vault secret reference), `KeyVault__VaultUri` and
`KeyVault__FamilyKeyName` (plain values — the app only ever calls Key Vault's wrapKey/unwrapKey by
name, it never holds the key itself), and `AZURE_CLIENT_ID` (the identity's client id, needed since
`DefaultAzureCredential` can't otherwise tell which user-assigned identity to use). Every name and tuning value (tables, the Workspace container, the Key Vault secret and key names, the queue and its retry settings, each app's size and scale, the children rate limit, the action resend delay) is written once in `params/lantern.<env>.bicepparam`; the template passes each to the apps as an env var and `bootstrap.sh` reads the Key Vault names from the same file, so nothing environment-specific is hard-coded in the apps (D36). Add tables, never
rename one.

The release sets the real images and the API's port by deploying this template with them (`CONTAINER_IMAGE` for the API,
`FUNCTIONS_IMAGE` for the Functions app). Until then the params file uses a placeholder image on port 80 for both. A release passes the new
images and port itself; `infra-check` reads the running ones (`keep-running-image.sh`) so its what-if never shows the
placeholder coming back.

## 3. GitHub Actions

There is one workflow, `.github/workflows/api.yml`, and it runs when `apps/lantern-api/**` or `infra/**` changes.
The `infra-check` job lints the Bicep and runs `what-if` (read-only). The `deploy` job waits for the reviewer on
the `uat` environment, then applies `infra/main.bicep` with the new API and Functions images and the API's port in one deployment, so an
infra change and an API release go out together and never collide. Nothing is deployed until you approve,
whether the run comes from a merge to `main` or a manual run.

```bash
infra/github-setup.sh rg-lantern-dev uat   # once: Entra app with OIDC, Contributor on the group, variables
```

No secret is stored for Azure. The variables are `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and
`AZURE_SUBSCRIPTION_ID`.

## 4. Checks around the API release

The end-to-end tests run in Docker, not against UAT: the `e2e-docker` job in `.github/workflows/api.yml` brings up
`deploy/local` and runs them on every PR and push, and the release waits for it (decision D28). The gated `deploy`
job adds two cheap checks of its own. Before it deploys, a read-only preflight confirms `id-lantern-uat` still
has the Storage and Service Bus data roles from `bootstrap.sh`, and fails with the fix if not. After the deploy, a smoke
check confirms `/health/live` answers 200 and `/v1/me` without a token answers 401.

If you set up the earlier cloud E2E, these are no longer used and can be deleted by hand: the `uat-e2e` GitHub
environment, the `env-uat-e2e` federated credential on the Entra app, its `Storage Table Data Contributor` role on
`lanternuat`, the Firebase `github-actions` OpenID Connect provider, the `FIREBASE_WEB_API_KEY` secret and the
`github-e2e-tests-lantern-ai-bg` service account.

## Not here yet

Service Bus topics (publish and subscribe need the Standard tier; add when an event gets a second consumer), the API's read access to `ncert` (add with the read-path PR),
Foundry, a prod environment.
