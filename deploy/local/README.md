# Run the API locally over HTTPS

Serves the API at `https://local.lantern.api` in Docker, with an Azurite table and blob emulator beside it.
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

Table and Blob data is kept in the `lantern-local_azurite-data` volume. Reset it with
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
