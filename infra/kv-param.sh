#!/usr/bin/env bash
key_vault_param() {
  az bicep build-params --file "$PARAMS_FILE" --stdout \
    | python3 -c "import json, sys; print(json.loads(json.load(sys.stdin)['parametersJson'])['parameters']['keyVault']['value']['$1'])"
}
