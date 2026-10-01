# Run the API locally over HTTPS

Serves the API at `https://local.lantern.api` in Docker, with stand-ins beside it for everything that is not local: an Azurite table and blob emulator, the Firebase
Auth Emulator and a key file in place of Key Vault.
Works from WSL and from Windows.

## Setup (once)

Docker is required. Everything else is done by the script.

| Where you work | Run |
| --- | --- |
| WSL / Linux | `deploy/local/setup.sh` |
| Windows | `deploy/local/setup.ps1` from any PowerShell (it asks for elevation itself) |

On WSL, `setup.sh` also runs `setup.ps1`, so the Windows browser and tools work too. Pass
`--no-windows` to skip that. `--dry-run` (bash) and `-WhatIf` (PowerShell) print the changes and
make none. `--remove` / `-Remove` undoes everything.

The script:

1. generates a certificate for `local.lantern.api` in `deploy/local/certs/` (git-ignored), using an
   `alpine` container so the result is identical on every platform;
2. maps `local.lantern.api` to `127.0.0.1` in the hosts file;
3. trusts the local CA (Windows: Local Machine root store; Linux: `update-ca-certificates`).

The CA can only sign for `local.lantern.api`, and its private key is discarded after the
certificate is issued, so trusting it does not weaken TLS for any other site. The certificate lasts
365 days; run the script with `--force` / `-Force` to renew it.

## Run

```sh
docker compose -f deploy/local/docker-compose.yml up --build
```

Then open <https://local.lantern.api/health/live>. Without a token, `https://local.lantern.api/v1/me`
returns 401, which is correct. The Development settings apply (`Firebase:ProjectId` and a throwaway
`Security:Key`). Set `LANTERN_HTTPS_PORT` if port 443 is taken; the URL then needs that port.

## Sign in and test end to end

The Firebase Auth Emulator replaces Google sign-in and a PEM file in the `keys` volume replaces Key Vault
(same RSA-OAEP-256 wrapping). On start the API also writes a dev Family: two Parents (`dev-parent-1`,
`dev-parent-2`) and ten Children, one in each Class from 1 to 10, with a Class space for each.

This lives in `apps/lantern-api/Lantern.Api.Test.Integration.Host`, not in the API. `deploy/local/api.Dockerfile` runs the
API through that host (a `WebApplicationFactory` on Kestrel, so the stand-ins replace the API's own registrations); the
production `Dockerfile` never contains it. The E2E tests are in `apps/lantern-api/Lantern.Api.Test.Integration`.

```sh
deploy/local/token.sh dev-parent-1                          # an ID token; any uid works, new ones are created
curl --cacert deploy/local/certs/ca.crt --resolve local.lantern.api:443:127.0.0.1 \
  -H "Authorization: Bearer $(deploy/local/token.sh dev-parent-1)" https://local.lantern.api/v1/me

# The whole flow as tests: seeded Family, register, 409, isolation, tokens that must be refused
E2E_BASE_URL=https://local.lantern.api E2E_AUTH_EMULATOR=http://127.0.0.1:9099 \
  dotnet test --project apps/lantern-api/Lantern.Api.Test.Integration --filter-trait "Category=E2E"
```

Deleting a Child is finished by a worker in the container, so those E2E tests poll for up to a minute.

CI runs the same tests in its `e2e-docker` job on every PR and push, and the release waits for it, so what passes here
is what gates the release. They do not run against real UAT.

None of this can reach another environment: the stand-ins are not in the API or its production image. The emulator's
tokens are unsigned, so the local host relaxes signature checking; issuer, audience and expiry are still checked (the
tests prove a wrong one gets 401).

Table and Blob data is kept in the `lantern-local_azurite-data` volume and the Key Vault stand-in key in
`lantern-local_keys`. Reset both with
`docker compose -f deploy/local/docker-compose.yml down -v`.

## Notes

- **WSL rewrites `/etc/hosts` on restart.** Add `[network]` and `generateHosts=false` to
  `/etc/wsl.conf` to keep the entry. The Windows hosts file is not affected.
- **Firefox** uses its own certificate store. Set `security.enterprise_roots.enabled` to `true` in
  `about:config` to make it use the Windows one.
- **Android emulator:** the emulator does not read the PC's hosts file, so `local.lantern.api` will
  not resolve inside it. This needs its own solution when the app work starts.
- The Azurite account key in `docker-compose.yml` is Azurite's published development key, not a
  secret.
