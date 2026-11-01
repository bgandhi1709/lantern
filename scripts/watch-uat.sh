#!/usr/bin/env bash
# Watches UAT while someone tests on a phone. Read-only. Needs `az login`.
#   scripts/watch-uat.sh queues      CloudAMQP queue depth and consumers, printed when they change
#   scripts/watch-uat.sh api         the API's console log, reconnecting when the app redeploys or scales
#   scripts/watch-uat.sh functions   the Functions app's console log, the same way
set -euo pipefail

ENV_NAME=${ENV_NAME:-uat}
RG=${RG:-rg-lantern-dev}
POLL_SECONDS=5

follow() {
  while true; do
    az containerapp logs show -n "$1" -g "$RG" --type console --follow --tail 0 || true
    sleep "$POLL_SECONDS"
  done
}

queues() {
  local url rest credentials location host vhost vh last="" now
  url=$(az keyvault secret show --vault-name "kv-lantern-${ENV_NAME}" -n amqp-url --query value -o tsv)
  rest=${url#*://}
  credentials=${rest%%@*}
  location=${rest#*@}
  host=${location%%/*}
  host=${host%%:*}
  vhost=/
  [[ $location == */* && -n ${location#*/} ]] && vhost=${location#*/}
  vh=$(jq -rn --arg v "$vhost" '$v | @uri')
  while true; do
    now=$(curl --silent --max-time 15 --config <(printf 'user = "%s"\n' "$credentials") "https://$host/api/queues/$vh" |
      jq -r '[.[] | "\(.name) ready=\(.messages_ready) unacked=\(.messages_unacknowledged) consumers=\(.consumers) published=\(.message_stats.publish // 0)"] | sort | join(" | ")' 2>/dev/null ||
      echo "api unreachable")
    if [ "$now" != "$last" ]; then
      echo "$(date +%H:%M:%S) $now"
      last=$now
    fi
    sleep "$POLL_SECONDS"
  done
}

case ${1:-} in
  queues) queues ;;
  api) follow "ca-lantern-${ENV_NAME}" ;;
  functions) follow "ca-lantern-${ENV_NAME}-functions" ;;
  *) echo "Usage: watch-uat.sh queues | api | functions" >&2; exit 2 ;;
esac
