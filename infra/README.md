# Lantern infrastructure

Base infrastructure for Lantern, environment `uat`, deployed into the existing `rg-lantern-dev`
(Central India). One file, `main.bicep`; the environment is in `params/lantern.uat.bicepparam`.
It creates the Container App fully wired (Firebase project id, storage table endpoint, the
`security-key` secret from Key Vault, the `family-field-key` name and vault URI) but on a
placeholder image; the API release swaps in the real image and port (`.github/workflows/api.yml`),
nothing else.

## 1. Bootstrap, once

```bash
infra/bootstrap.sh rg-lantern-dev uat
```

Needs Owner or User Access Administrator on the resource group. Safe to run again. It creates the
identity `id-lantern-uat`, the Key Vault `kv-lantern-uat`, the roles the identity needs (Storage Table
Data Contributor and Storage Blob Data Contributor on the resource group, Key Vault Secrets User and Key Vault Crypto User on the vault),
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

Re-run `bootstrap.sh` before the first API release that uses Blob: the API identity needs the new role.

The deploying account needs Contributor only. It creates the storage account `lanternuat` with the tables
`parents` and `families`, the private `family` blob container (a Class space per Child and Class), the private `ncert` blob container (for `ncert-build`'s output — see
[`tools/ncert-build`](../tools/ncert-build/README.md)), a Log Analytics workspace, and the Container
Apps environment and app (0 to 1 replica, the identity attached). All names come from
`environmentName`. The template reads the identity and the Key Vault by name and wires the app's
settings: `Firebase__ProjectId` (a param, not a secret), `Storage__TableEndpoint` and `Storage__BlobEndpoint` (read from the
storage account), `Security__Key` (a Key Vault secret reference), `KeyVault__VaultUri` and
`KeyVault__FamilyKeyName` (plain values — the app only ever calls Key Vault's wrapKey/unwrapKey by
name, it never holds the key itself), and `AZURE_CLIENT_ID` (the identity's client id, needed since
`DefaultAzureCredential` can't otherwise tell which user-assigned identity to use). Add tables, never
rename one.

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

No secret is stored for Azure. The variables are `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and
`AZURE_SUBSCRIPTION_ID`.

## 4. Checks around the API release

The end-to-end tests run in Docker, not against UAT: the `e2e-docker` job in `.github/workflows/api.yml` brings up
`deploy/local` and runs them on every PR and push, and the release waits for it (decision D28). The gated `deploy`
job adds two cheap checks of its own. Before it swaps the image, a read-only preflight confirms `id-lantern-uat` still
has the Storage Table and Blob data roles from `bootstrap.sh`, and fails with the fix if not. After the swap, a smoke
check confirms `/health/live` answers 200 and `/v1/me` without a token answers 401.

If you set up the earlier cloud E2E, these are no longer used and can be deleted by hand: the `uat-e2e` GitHub
environment, the `env-uat-e2e` federated credential on the Entra app, its `Storage Table Data Contributor` role on
`lanternuat`, the Firebase `github-actions` OpenID Connect provider, the `FIREBASE_WEB_API_KEY` secret and the
`github-e2e-tests-lantern-ai-bg` service account.

## Not here yet

Queues (add when the API uses them), the API's read access to `ncert` (add with the read-path PR),
Foundry, a prod environment.
