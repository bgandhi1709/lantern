# Lantern infrastructure

Base infrastructure for Lantern, environment `uat`, deployed into the existing `rg-lantern-dev`
(Central India). One file, `main.bicep`; the environment is in `params/lantern.uat.bicepparam`.
It creates the Container App fully wired (Firebase project id, storage table endpoint, the
`security-key` secret from Key Vault) but on a
placeholder image; the API release sets the real image and port (`.github/workflows/api.yml`),
nothing else.

## 1. Bootstrap, once

```bash
infra/bootstrap.sh rg-lantern-dev uat
```

Needs Owner or User Access Administrator on the resource group. Safe to run again. It creates the
identity `id-lantern-uat`, the Key Vault `kv-lantern-uat`, the roles the identity needs (Storage Table
Data Contributor, Storage Blob Data Contributor, Storage Blob Data Owner (the Functions host's own state) and Monitoring Metrics Publisher (telemetry export) on the resource group, Key Vault Secrets User on the vault),
a Storage Blob Data Contributor role on the resource group for whoever uploads the Board content, with a third argument (the deploy app's client id, the GitHub variable `AZURE_CLIENT_ID`) Key Vault Secrets User on the `amqp-url` secret alone for the deploy identity, the secret `security-key`, only if missing, and the secret `amqp-url` as a placeholder, only if missing.

**`security-key` derives the uid hash key. Rotating it makes every registration unreadable.**
Save a copy (the script prints the command). Lantern holds no Family key: each Family's personal fields are
locked on the phone with a key only the Parent can open (D66, [ADR-0010](../docs/adr/0010-family-key-held-by-the-parent.md)).
The old `family-field-key` in the vault is no longer used; it can be deleted once no Family on the old scheme remains.

### The broker URL (`amqp-url`)

Actions go through a CloudAMQP LavinMQ instance (RabbitMQ-compatible) on the free plan ([D119](../docs/ideation/decision-log.md), issue #173). Its AMQPS URL holds the broker password and is the one connection string in Azure, held as the Key Vault secret `amqp-url` (the name is `keyVault.amqpUrlSecret` in the bicepparam). Only CloudAMQP can issue it, so `bootstrap.sh` creates a placeholder (value and content type `placeholder`) and you replace it. Create a LavinMQ instance on the CloudAMQP free plan, copy its AMQPS URL, then paste it once at the hidden prompt (the URL goes to no file, no shell history and no GitHub secret):

```bash
read -rsp 'AMQPS URL: ' URL; echo
az keyvault secret set --vault-name kv-lantern-uat -n amqp-url --file <(printf '%s' "$URL"); unset URL
```

Setting it with no `--content-type` clears the placeholder flag. The release preflight reads the secret's content type through Azure Resource Manager, because the deploy identity has Contributor only and must not read secret values, and fails while the secret is missing or still the placeholder. It cannot see a wrong value, so paste the URL once and check it is one `amqps://user:password@host/vhost`.

**Rotate:** change the password or URL in CloudAMQP, run the same two commands, then restart both apps (`az containerapp revision restart` on `ca-lantern-uat` and `ca-lantern-uat-functions`). They read the secret when a revision starts.

## 2. Deploy

```bash
az deployment group what-if -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
az deployment group create  -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam
```

Re-run `bootstrap.sh` with the deploy client id before the first release that applies the broker topology (`infra/bootstrap.sh rg-lantern-dev uat <AZURE_CLIENT_ID>`), once the real `amqp-url` is set. The release preflight fails with the fix if a role is missing.

The deploying account needs Contributor only. It creates the storage account `lanternuat` with the tables
`parents`, `families`, `actions` (the action ledger) and `progress` (each Child's Chapter Attempts and Checks), the private `family` blob container (every Child's Workspace, a folder per Class; blob soft delete and versioning are off, so a deleted Child's files really go), the private `ncert` and `ssc` blob containers (each Board's Books, ready to read; layout in
[`docs/architecture/contracts`](../docs/architecture/contracts/README.md)), a Log Analytics workspace with a retention and a daily ingestion cap from the bicepparam, a workspace-based Application Insights `appi-lantern-uat` (no ingestion key: both apps sign in with the identity; its connection string reaches them as the secret ref `appinsights-connection-string`), and the Container
Apps environment with two apps (each 0 to 1 replica, the identity attached): the API `ca-lantern-uat`, and `ca-lantern-uat-functions`, which runs `Lantern.Functions` with no ingress and is woken by one KEDA `rabbitmq` rule per subscriber queue when a message arrives ([ADR-0018](../docs/adr/0018-actions-are-published-and-subscribed-on-cloudamqp.md)). All names come from
`environmentName`. The template reads the identity and the Key Vault by name and wires the app's
settings: `Firebase__ProjectId` (a param, not a secret), `Storage__TableEndpoint` and `Storage__BlobEndpoint` (read from the
storage account), `Broker__Url` (the `amqp-url` Key Vault secret ref), `Actions__Exchange` (read by both apps) and `Actions__CreateWorkspaceQueue`, `Actions__RemoveWorkspaceQueue` and `Actions__RemoveFamilyQueue` (the Functions app's triggers), `Security__Key` (a Key Vault secret reference), and `AZURE_CLIENT_ID` (the identity's client id, needed since
`DefaultAzureCredential` can't otherwise tell which user-assigned identity to use). Every name and tuning value (tables, the Workspace container, the Key Vault secret name, the exchange and queue names, each app's size and scale, the children rate limit, the action resend delay) is written once in `params/lantern.<env>.bicepparam`; the template passes each to the apps as an env var and `bootstrap.sh` reads the Key Vault secret name from the same file, so nothing environment-specific is hard-coded in the apps (D36). Add tables, never
rename one.

The release sets the real images and the API's port by deploying this template with them (`CONTAINER_IMAGE` for the API,
`FUNCTIONS_IMAGE` for the Functions app). Until then the params file uses a placeholder image on port 80 for both. A release passes the new
images and port itself; `infra-check` reads the running ones (`keep-running-image.sh`) so its what-if never shows the
placeholder coming back.

## 3. GitHub Actions

There is one workflow, `.github/workflows/api.yml`, and it runs when `apps/lantern-api/**` or `infra/**` changes.
The `infra-check` job lints the Bicep, runs `infra/preflight.sh` (providers registered, identity roles present) and
`what-if`, all read-only, on every PR. The `deploy` job waits for the reviewer on the `uat` environment, then applies
`infra/main.bicep` with the new API and Functions images and the API's port in one deployment, so an infra change and
an API release go out together and never collide. A release runs on a merge to `main`, a manual run on `main`, or a
PR labelled `deploy-uat` (to test a PR in UAT before merging); nothing is deployed until you approve. UAT is one
environment, so the last approved release wins. A manual run on any other branch cannot sign in to Azure: the app
registration trusts only the `pull_request`, `main` and `environment:uat` subjects (`github-setup.sh`, in `lantern-utilities`).

```bash
infra/github-setup.sh rg-lantern-dev uat   # once, from the lantern-utilities repository: Entra app with OIDC, Contributor on the group, variables
```

No secret is stored for Azure. The variables are `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and
`AZURE_SUBSCRIPTION_ID`.

## 4. Checks around the API release

The end-to-end tests run in Docker, not against UAT: the `e2e-docker` job in `.github/workflows/api.yml` brings up
`deploy/local` and runs them on every PR and push, and the release waits for it (decision D28). The gated `deploy`
job adds two cheap checks of its own. Before it deploys, a read-only preflight confirms `id-lantern-uat` still
has the Storage data roles from `bootstrap.sh`, and fails with the fix if not. After the deploy, a smoke
check confirms `/health/live` answers 200 and `/v1/me` without a token answers 401.

If you set up the earlier cloud E2E, these are no longer used and can be deleted by hand: the `uat-e2e` GitHub
environment, the `env-uat-e2e` federated credential on the Entra app, its `Storage Table Data Contributor` role on
`lanternuat`, the Firebase `github-actions` OpenID Connect provider, the `FIREBASE_WEB_API_KEY` secret and the
`github-e2e-tests-lantern-ai-bg` service account.

## The broker topology

`infra/topology.json` is the exchange, the queues and the bindings (ADR-0018). The `deploy` job runs `infra/apply-topology.sh` before the Bicep deployment, reading `amqp-url` with the deploy identity's one-secret role. It declares each entity through the broker's management API and is safe to repeat, but it cannot change the arguments of a queue that exists: to change one, delete the queue in the CloudAMQP console first (it must be empty), then release. To run it by hand: `read -rsp 'AMQPS URL: ' AMQP_URL; echo; export AMQP_URL; infra/apply-topology.sh`. It also takes the URL as its first argument and another topology file as its second.

A new subscriber adds its queue and binding to the file and its name to `actions` in the bicepparam (`new event` in `docs/architecture/recipes.md`).

## Removing the Service Bus namespace

ARM incremental deployments do not delete a resource the template dropped, so after the first release on CloudAMQP is verified (the smoke check passes, and a Child delete through UAT finishes with the Functions app waking from zero), delete the namespace by hand, then remove the `Microsoft.ServiceBus` provider registration only if nothing else in the subscription uses it:

```bash
az servicebus namespace delete -g rg-lantern-dev -n sb-lantern-uat
```

The `Azure Service Bus Data Owner` role of `id-lantern-uat` on the resource group is no longer used; remove it with `az role assignment delete --assignee <identity principal id> --role "Azure Service Bus Data Owner" --scope <resource group id>`.

## Catalogs

The API reads `ncert` and `ssc` with the resource-group Blob role from `bootstrap.sh` (D82); their names reach it as `Catalog__NcertContainer` and `Catalog__SscContainer` from `containers` in the bicepparam, and it will not start without them. `Lantern.Functions` reads the same two containers, with the same role and the same two settings, to copy each Child's Content slice out of its Board's container when the Child is registered or added (D88, ADR-0013), and it will not start without them either. Run the yearly upload in a maintenance window: a copy that sees the container change while it runs is not marked ready and retries. A catalog is kept in memory for the life of the process, so after the yearly upload restart the API (`az containerapp revision restart`, or any new release).

## Not here yet

A paid CloudAMQP plan, Foundry, a prod environment.

## Tracing one request

[ADR-0006](../docs/adr/0006-one-trace-in-application-insights.md): the API and the Function run share one `operation_Id` once #179 carries the trace through the broker; until then the trace stops at the publish. In Application Insights, Logs, take the id from Transaction search (or a request's `operation_Id`) and run:

```kusto
union requests, dependencies, traces, exceptions
| where operation_Id == "<operation id>"
| project timestamp, itemType, cloud_RoleName, name, target, resultCode, message, severityLevel
| order by timestamp asc
```

A resend after an API start has its own `operation_Id`; its `ResendAction` dependency carries the original request's trace in `customDimensions` (span links). Telemetry is off when `APPLICATIONINSIGHTS_CONNECTION_STRING` is unset, as in `deploy/local`.
