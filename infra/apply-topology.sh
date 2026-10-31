#!/usr/bin/env bash
# Declares the exchanges, queues and bindings in topology.json on the broker named by an AMQP URL, through its
# management API. Safe to repeat; it cannot change the arguments of a queue that already exists.
# Usage: apply-topology.sh [amqp-url] [topology.json]   (the URL may be in AMQP_URL instead, so it stays out of the process list)
set -euo pipefail

url=${1:-${AMQP_URL:?usage: apply-topology.sh [amqp-url] [topology.json]}}
file=${2:-$(dirname "$0")/topology.json}

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

while read -r item; do
  name=$(jq -r .name <<< "$item")
  call -X PUT "$api/queues/$vh/$name" -d "$(jq -c '{durable, arguments}' <<< "$item")"
done < <(jq -c '.queues[]' "$file")

while read -r item; do
  source=$(jq -r .source <<< "$item")
  destination=$(jq -r .destination <<< "$item")
  call -X POST "$api/bindings/$vh/e/$source/q/$destination" -d "$(jq -c '{routing_key}' <<< "$item")"
done < <(jq -c '.bindings[]' "$file")

echo "Topology applied to $host (vhost $vhost)."
