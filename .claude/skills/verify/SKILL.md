---
name: verify
description: Build, run and drive the Lantern API locally over HTTPS at https://local.lantern.api. Use when verifying changes under apps/lantern-api or deploy/local.
---

# Verify the Lantern API

The runtime surface is HTTP. Tests are CI's job; do not run them here.

## Start

WSL or Linux, from the repo root:

```sh
deploy/local/setup.sh --no-windows      # once: certs, /etc/hosts entry, CA trust (needs Docker)
docker compose -f deploy/local/docker-compose.yml up --build -d
```

Windows: `deploy/local/setup.ps1`. Details are in `deploy/local/README.md`.

Docker on this WSL machine came from `apt install docker.io docker-compose-v2`, then
`systemctl enable --now docker`. WSL rewrites `/etc/hosts` on restart, so re-run `setup.sh` if
`getent hosts local.lantern.api` is empty.

Stop with `docker compose -f deploy/local/docker-compose.yml down -v`.

## Drive

- `curl https://local.lantern.api/health/live` returns 200 with no `--cacert` once the CA is trusted.
- `curl https://local.lantern.api/v1/me` with no token returns 401 with `WWW-Authenticate: Bearer`.
- Every route returns 401 without a token, including unknown ones.
- Windows reaches it through WSL localhost forwarding: `curl.exe -k --resolve local.lantern.api:443:127.0.0.1 https://local.lantern.api/health/live`.
- A missing `deploy/local/certs/local.lantern.api.key` makes the api container exit with `FileNotFoundException` in its logs.

## Gotchas

- `POST /v1/register` and `GET /v1/me` need a Google-signed Firebase ID token for project
  `lantern-ai-bg1709`. A locally forged token is always rejected, so the authenticated flow needs a
  real token from the user.
- Never use `pkill -f` with the DLL name; it matches the calling shell. Stop by listening port.
- The Windows PowerShell reachable from WSL may already be elevated. Use `-WhatIf` unless a real
  change is intended.
