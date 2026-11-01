#!/usr/bin/env bash
# Shows, or removes, what UAT stores for Families. Needs `az login` with data access to the storage account.
#   scripts/uat-family.sh show                      row counts per table and blobs per Child folder (read-only)
#   scripts/uat-family.sh delete <familyId>         lists what it would delete and stops (a dry run)
#   UAT_CONFIRM=<familyId> scripts/uat-family.sh delete <familyId>
#                                                   deletes that Family's Parent profiles, Family rows, progress rows and Workspace blobs
# It never touches the Board content in ncert and ssc.
set -euo pipefail

ENV_NAME=${ENV_NAME:-uat}
ACCOUNT=${ACCOUNT:-lantern${ENV_NAME}}
AUTH=(--account-name "$ACCOUNT" --auth-mode login)
WORKSPACES=family

show() {
  local table
  for table in parents families progress actions; do
    printf '%s rows: ' "$table"
    az storage entity query "${AUTH[@]}" --table-name "$table" --query 'length(items)' -o tsv
  done
  echo "Workspace blobs per Family and Child:"
  az storage blob list "${AUTH[@]}" --container-name "$WORKSPACES" --query '[].name' -o tsv | awk -F/ '{print $1"/"$2}' | sort | uniq -c
}

uid_hashes() {
  az storage entity query "${AUTH[@]}" --table-name families --filter "PartitionKey eq '$1'" --select RowKey \
    --query "items[?starts_with(RowKey, 'parent_')].RowKey" -o tsv | sed 's/^parent_//'
}

remove() {
  local family=$1 apply=$2 hash row
  echo "Family $family:"
  for hash in $(uid_hashes "$family"); do
    echo "  parents: profile of $hash"
    [ "$apply" = yes ] && az storage entity delete "${AUTH[@]}" --table-name parents --partition-key "$hash" --row-key profile -o none
  done
  for row in $(az storage entity query "${AUTH[@]}" --table-name families --filter "PartitionKey eq '$family'" --select RowKey --query 'items[].RowKey' -o tsv); do
    echo "  families: $row"
    [ "$apply" = yes ] && az storage entity delete "${AUTH[@]}" --table-name families --partition-key "$family" --row-key "$row" -o none
  done
  for row in $(az storage entity query "${AUTH[@]}" --table-name progress --filter "PartitionKey eq '$family'" --select RowKey --query 'items[].RowKey' -o tsv); do
    echo "  progress: $row"
    [ "$apply" = yes ] && az storage entity delete "${AUTH[@]}" --table-name progress --partition-key "$family" --row-key "$row" -o none
  done
  echo "  blobs: $(az storage blob list "${AUTH[@]}" --container-name "$WORKSPACES" --prefix "$family/" --query 'length(@)' -o tsv) under $WORKSPACES/$family/"
  [ "$apply" = yes ] && az storage blob delete-batch "${AUTH[@]}" --source "$WORKSPACES" --pattern "$family/*" -o none
  return 0
}

case ${1:-} in
  show) show ;;
  delete)
    family=${2:?usage: uat-family.sh delete <familyId>}
    if [ "${UAT_CONFIRM:-}" = "$family" ]; then remove "$family" yes; else remove "$family" no; echo "Dry run. To delete: UAT_CONFIRM=$family $0 delete $family"; fi
    ;;
  *) echo "Usage: uat-family.sh show | delete <familyId>" >&2; exit 2 ;;
esac
