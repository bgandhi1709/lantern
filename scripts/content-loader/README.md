# Content loader

A one-off move of a Board's content from its Blob container (`ncert` or `ssc`) into the `content` container of Cosmos DB
(ADR-0020, D136). The document kinds and the rules it checks are in
[`docs/architecture/contracts/README.md`](../../docs/architecture/contracts/README.md). It is a dry run unless given `--apply`.

## Set up

The machine has no `pip` or `venv`, so make the environment with `pip.pyz`:

```bash
mkdir -p /tmp/loader-pip && curl -sSL -o /tmp/loader-pip/pip.pyz https://bootstrap.pypa.io/pip/pip.pyz
python3 -m venv --without-pip /tmp/loader-venv
/tmp/loader-venv/bin/python /tmp/loader-pip/pip.pyz install -r scripts/content-loader/requirements.txt
```

## Run against UAT

1. Deploy the Bicep (the `api.yml` release does it, or `az deployment group create -g rg-lantern-dev -f infra/main.bicep -p infra/params/lantern.uat.bicepparam`).
   It creates the Cosmos DB account `cosmos-lantern-uat` on the free tier, which allows one account per subscription.
2. Give yourself the data role once. The release passes no `LOADER_PRINCIPAL_ID`, so grant it directly:
   `az cosmosdb sql role assignment create -g rg-lantern-dev -a cosmos-lantern-uat --role-definition-id 00000000-0000-0000-0000-000000000002 --scope / --principal-id $(az ad signed-in-user show --query id -o tsv)`
3. Check each Board without writing:
   `/tmp/loader-venv/bin/python scripts/content-loader/load_board.py --board ncert --account lanternuat`
4. Write it:
   `... --board ncert --account lanternuat --cosmos-endpoint https://cosmos-lantern-uat.documents.azure.com:443/ --apply`

It signs in with `az login`. It never uses an account key, and it never creates the database or container in Azure.

## Run locally

`docker compose -f deploy/local/docker-compose.yml up -d cosmos`, then add `--emulator` (it creates the database and container).
`--source-dir DIR` reads a local copy of a Board's container, and `--blob-connection-string` reads Azurite.

## Test

```bash
/tmp/loader-venv/bin/python -m pytest scripts/content-loader/tests
```

The rule tests need nothing running. `test_emulator.py` runs when the emulator is up on `localhost:8081` and is skipped otherwise.
