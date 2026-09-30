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
Data Contributor on the resource group, Key Vault Secrets User and Key Vault Crypto User on the vault),
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

The deploying account needs Contributor only. It creates the storage account `lanternuat` with the tables
`parents` and `families`, the private `ncert` blob container (for `ncert-build`'s output — see
[`tools/ncert-build`](../tools/ncert-build/README.md)), a Log Analytics workspace, and the Container
Apps environment and app (0 to 1 replica, the identity attached). All names come from
`environmentName`. The template reads the identity and the Key Vault by name and wires the app's
settings: `Firebase__ProjectId` (a param, not a secret), `Storage__TableEndpoint` (read from the
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
`AZURE_SUBSCRIPTION_ID`. The script also grants the same Entra app `Storage Table Data Contributor`,
scoped to just the `lanternuat` storage account, so the API's E2E job (`.github/workflows/api.yml`,
job `e2e-uat`) can delete the rows it creates after each run — needs the storage account already
deployed, so re-run the script once after the first infra deploy if it printed "not deployed yet".

## 4. E2E tests against the real UAT API

`e2e-uat` runs after every successful `deploy`, registering one real family through a real
Firebase-issued ID token and deleting it again afterward (`apps/lantern-api/Lantern.Api.Tests/E2E`).
Sign-in is Google-only, so it can't do a scripted login. Instead of a service account with a
downloaded key, it uses the GitHub Actions job's own OIDC identity, the same way `azure/login`
already does: the runner mints a short-lived token for the job, and Firebase exchanges it directly
for a real ID token via a custom OpenID Connect sign-in provider — no key exists anywhere.

The job runs in the `uat-e2e` environment, which has no required reviewers: `uat` holds the approval
gate and `deploy` already passed it. `infra/github-setup.sh` creates the environment and its
federated credential.

**One-time Firebase setup (console, not scriptable from here):**
1. GCP Console → the `lantern-ai-bg1709` project → **Identity Platform** → Enable (free tier, does
   not touch existing Google sign-in users).
2. Firebase Console → Authentication → Sign-in method → Add new provider → **OpenID Connect**.
   - Name it `github-actions` (the provider ID becomes `oidc.github-actions` — must match
     `FirebaseOidcProviderId` in `RegisteredFamilyFixture.cs`).
   - Issuer: `https://token.actions.githubusercontent.com`
   - Client ID: `lantern-e2e` (must match `GitHubOidcAudience` in the same file — this is the
     `aud` the job requests on its own OIDC token, not a secret).
   - Response type: ID token only — no client secret needed, since the fixture calls
     `accounts:signInWithIdp` directly with the token rather than doing a redirect/code exchange.
3. `gh secret set FIREBASE_WEB_API_KEY --body "<apiKey from: firebase apps:sdkconfig WEB --project lantern-ai-bg1709>"`
   — the project's web API key, not secret by Firebase's own design, just kept out of source.

No GitHub secret is needed for Firebase, same as Azure. The `github-e2e-tests-lantern-ai-bg`
service account created during an earlier iteration of this setup is unused by this approach and
can be deleted.

## Not here yet

Queues (add when the API uses them), the API's read access to `ncert` (add with the read-path PR),
Foundry, a prod environment.
