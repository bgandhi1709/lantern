#!/usr/bin/env bash
set -euo pipefail

url=${1:-${AMQP_URL:?usage: apply-topology.sh [amqp-url] [topology.json]}}
file=${2:-$(dirname "$0")/topology.json}
delay_ms=$((${RETRY_DELAY_SECONDS:?RETRY_DELAY_SECONDS is how long a failed action waits before it is tried again} * 1000))

scheme=${url%%://*}
rest=${url#*://}
credentials=${rest%%@*}
location=${rest#*@}
host=${location%%/*}
vhost=/
[[ $location == */* && -n ${location#*/} ]] && vhost=${location#*/}
host=${host%%:*}

if [[ $scheme == amqps ]]; then api=https://$host/api; else api=http://$host:15672/api; fi
vh=$(jq -rn --arg v "$vhost" '$v | @uri')

call() {
  curl --silent --show-error --fail --retry 30 --retry-delay 2 --retry-all-errors \
    --config <(printf 'user = "%s"\n' "$credentials") -H 'Content-Type: application/json' "$@" > /dev/null
}

while read -r item; do
  name=$(jq -r .name <<< "$item")
  call -X PUT "$api/exchanges/$vh/$name" -d "$(jq -c '{type, durable, arguments}' <<< "$item")"
done < <(jq -c '.exchanges[]' "$file")

recreate_if_changed() {
  local name=$1 wanted=$2 existing
  existing=$(curl --silent --config <(printf 'user = "%s"\n' "$credentials") -w '\n%{http_code}' "$api/queues/$vh/$name") || return 0
  [[ ${existing##*$'\n'} == 200 ]] || return 0
  existing=${existing%$'\n'*}
  [[ $(jq -cS ".arguments" <<< "$existing") == "$(jq -cS . <<< "$wanted")" ]] && return 0
  if [[ $(jq -r '.messages' <<< "$existing") != 0 ]]; then
    echo "Queue $name has different arguments and holds messages: drain it, then run this again." >&2
    exit 1
  fi
  call -X DELETE "$api/queues/$vh/$name"
}

while read -r item; do
  name=$(jq -r .name <<< "$item")
  arguments=$(jq -c --argjson delay "$delay_ms" 'walk(if . == "{{retryDelayMs}}" then $delay else . end)' <<< "$(jq -c .arguments <<< "$item")")
  recreate_if_changed "$name" "$arguments"
  call -X PUT "$api/queues/$vh/$name" -d "$(jq -c --argjson arguments "$arguments" '{durable, arguments: $arguments}' <<< "$item")"
done < <(jq -c '.queues[]' "$file")

while read -r item; do
  source=$(jq -r .source <<< "$item")
  destination=$(jq -r .destination <<< "$item")
  call -X POST "$api/bindings/$vh/e/$source/q/$destination" -d "$(jq -c '{routing_key}' <<< "$item")"
done < <(jq -c '.bindings[]' "$file")

echo "Topology applied to $host (vhost $vhost)."
