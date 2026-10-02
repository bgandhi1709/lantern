#!/usr/bin/env bash
# The API release updates the image of each running app (the API and the Functions app). An infrastructure
# deployment must not put the placeholder back, so read the images and the API's port and pass them on.
# Nothing is set for an app that does not exist yet: the params file then uses the placeholder.
set -uo pipefail

APP="${APP_NAME:-ca-lantern-uat}"
FUNCTIONS_APP="${FUNCTIONS_APP_NAME:-ca-lantern-uat-functions}"
show() { # app query
  az resource show -g "${RESOURCE_GROUP}" -n "$1" --resource-type Microsoft.App/containerApps \
    --query "$2" -o tsv 2>/dev/null
}

IMAGE=$(show "$APP" 'properties.template.containers[0].image') || IMAGE=""
PORT=$(show "$APP" 'properties.configuration.ingress.targetPort') || PORT=""
FUNCTIONS_IMAGE=$(show "$FUNCTIONS_APP" 'properties.template.containers[0].image') || FUNCTIONS_IMAGE=""

if [ -n "$IMAGE" ] && [ -n "$PORT" ]; then
  echo "Keeping running image ${IMAGE} on port ${PORT}"
  echo "CONTAINER_IMAGE=${IMAGE}" >> "${GITHUB_ENV}"
  echo "CONTAINER_PORT=${PORT}" >> "${GITHUB_ENV}"
else
  echo "No running app yet; using the params file defaults"
fi

if [ -n "$FUNCTIONS_IMAGE" ]; then
  echo "Keeping running Functions image ${FUNCTIONS_IMAGE}"
  echo "FUNCTIONS_IMAGE=${FUNCTIONS_IMAGE}" >> "${GITHUB_ENV}"
fi
