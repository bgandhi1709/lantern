#!/usr/bin/env bash
# Puts the real ncert and ssc containers from the UAT storage account into the local Azurite, whole, so the
# local stack runs on the data UAT runs on. It empties the local Azurite first (docker compose down -v), because
# Families made on other data would point at the old masters.
#   deploy/local/load-uat-masters.sh            load, unless the real masters are already there
#   deploy/local/load-uat-masters.sh --force    empty the local Azurite and load again
# Needs `az login` with read access to the UAT storage account. The download is kept outside the repo, in
# LANTERN_MASTERS_DIR (default ~/Work/lantern-data/uat-masters), so a second load reuses it with --keep.
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
COMPOSE=$ROOT/deploy/local/docker-compose.yml
UAT_ACCOUNT=${LANTERN_UAT_ACCOUNT:-lanternuat}
DIR=${LANTERN_MASTERS_DIR:-$HOME/Work/lantern-data/uat-masters}
CONTAINERS=(ncert ssc)
LOCAL_CONNECTION="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
LOADED_MARKER=(ssc toc/ssc3-evs1.json)
MAX_CONNECTIONS=16
POLL_SECONDS=2
AZURITE_TIMEOUT_SECONDS=60

local_has() { az storage blob exists --connection-string "$LOCAL_CONNECTION" -c "$1" -n "$2" --query exists -o tsv 2>/dev/null; }

azurite_up() {
  docker compose -f "$COMPOSE" up -d azurite >/dev/null
  for _ in $(seq $((AZURITE_TIMEOUT_SECONDS / POLL_SECONDS))); do
    az storage container list --connection-string "$LOCAL_CONNECTION" -o none 2>/dev/null && return 0
    sleep "$POLL_SECONDS"
  done
  echo "Azurite did not answer" >&2
  exit 1
}

force=false
keep=false
for arg in "$@"; do
  case $arg in
    --force) force=true ;;
    --keep) keep=true ;;
    *) echo "Usage: load-uat-masters.sh [--force] [--keep]" >&2; exit 2 ;;
  esac
done

azurite_up
if [ "$force" = false ] && [ "$(local_has "${LOADED_MARKER[@]}")" = true ]; then
  echo "The real masters are already in the local Azurite."
  exit 0
fi

az account show >/dev/null 2>&1 || { echo "Run az login first." >&2; exit 1; }

echo "Emptying the local Azurite (docker compose down -v)"
docker compose -f "$COMPOSE" down -v --remove-orphans >/dev/null
azurite_up

for container in "${CONTAINERS[@]}"; do
  [ "$keep" = true ] && [ -d "$DIR/$container" ] || {
    rm -rf "${DIR:?}/$container"
    mkdir -p "$DIR/$container"
    echo "Downloading $container from $UAT_ACCOUNT"
    az storage blob download-batch --account-name "$UAT_ACCOUNT" --auth-mode login \
      -s "$container" -d "$DIR/$container" --max-connections "$MAX_CONNECTIONS" --no-progress -o none
  }
  echo "Uploading $container to the local Azurite"
  az storage container create --connection-string "$LOCAL_CONNECTION" -n "$container" -o none
  az storage blob upload-batch --connection-string "$LOCAL_CONNECTION" -d "$container" -s "$DIR/$container" \
    --overwrite --max-connections "$MAX_CONNECTIONS" --no-progress -o none
done

echo "Done. Start the stack with LANTERN_SEED_BOARD_MASTERS=false so the synthetic seeder leaves these alone."
