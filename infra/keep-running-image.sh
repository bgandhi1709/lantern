#!/usr/bin/env bash
# The API release updates the image of the running app. An infrastructure deployment must not
# put the placeholder back, so read the image and port of the running app and pass them on.
# Nothing is set when the app does not exist yet: the params file then uses the placeholder.
set -uo pipefail

APP="${APP_NAME:-ca-lantern-uat}"
show() {
  az resource show -g "${RESOURCE_GROUP}" -n "$APP" --resource-type Microsoft.App/containerApps \
    --query "$1" -o tsv 2>/dev/null
}

IMAGE=$(show 'properties.template.containers[0].image') || IMAGE=""
PORT=$(show 'properties.configuration.ingress.targetPort') || PORT=""

if [ -n "$IMAGE" ] && [ -n "$PORT" ]; then
  echo "Keeping running image ${IMAGE} on port ${PORT}"
  echo "CONTAINER_IMAGE=${IMAGE}" >> "${GITHUB_ENV}"
  echo "CONTAINER_PORT=${PORT}" >> "${GITHUB_ENV}"
else
  echo "No running app yet; using the params file defaults"
fi
