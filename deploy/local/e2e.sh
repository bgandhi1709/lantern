#!/usr/bin/env bash
# End-to-end check against the local Docker stack. Run after:
#   docker compose -f deploy/local/docker-compose.yml up --build -d
set -euo pipefail

dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
base=${LANTERN_URL:-https://local.lantern.api}
curl_api=(curl -s --cacert "$dir/certs/ca.crt" --resolve "local.lantern.api:${LANTERN_HTTPS_PORT:-443}:127.0.0.1")
failures=0

check() { # description expected actual
  if [[ $2 == "$3" ]]; then echo "ok    $1"; else echo "FAIL  $1 (expected $2, got $3)"; failures=$((failures + 1)); fi
}
status() { "${curl_api[@]}" -o /dev/null -w '%{http_code}' "$@"; }
auth() { echo "Authorization: Bearer $("$dir/token.sh" "$1")"; }
json() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

check "health" 200 "$(status "$base/health/live")"
check "me without a token" 401 "$(status "$base/v1/me")"

one=$("${curl_api[@]}" -H "$(auth dev-parent-1)" "$base/v1/me")
two=$("${curl_api[@]}" -H "$(auth dev-parent-2)" "$base/v1/me")
check "seeded family has 10 children" 10 "$(json 'len(d["children"])' <<<"$one")"
check "children are in classes 1 to 10" "1,2,3,4,5,6,7,8,9,10" "$(json '",".join(str(c["classLevel"]) for c in d["children"])' <<<"$one")"
check "both parents share the family" "$(json 'd["familyId"]' <<<"$one")" "$(json 'd["familyId"]' <<<"$two")"
check "the parents are different people" 1 "$(python3 -c "import json,sys; print(len({json.loads(a)['parent']['parentId'] for a in sys.argv[1:]}) - 1)" "$one" "$two")"

stranger="dev-new-$RANDOM$RANDOM"
check "a new caller is not registered" 404 "$(status -H "$(auth "$stranger")" "$base/v1/me")"
body='{"region":"Gujarat","language":"en","consent":{"accepted":true,"noticeVersion":"dev"},"children":[{"name":"Kid","classLevel":4,"birthYear":'$(($(date +%Y) - 9))'}]}'
register() { status -X POST -H "$(auth "$stranger")" -H "Content-Type: application/json" -d "$body" "$base/v1/register"; }
check "register" 201 "$(register)"
check "register again" 409 "$(register)"
mine=$("${curl_api[@]}" -H "$(auth "$stranger")" "$base/v1/me")
check "the new family is separate from the seeded one" 1 "$(python3 -c "import json,sys; print(int(json.loads(sys.argv[1])['familyId'] != json.loads(sys.argv[2])['familyId']))" "$mine" "$one")"
check "the new family holds only its own child" 1 "$(json 'len(d["children"])' <<<"$mine")"

echo
((failures == 0)) && echo "all checks passed" || { echo "$failures check(s) failed"; exit 1; }
