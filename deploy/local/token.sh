#!/usr/bin/env bash
# Prints a Firebase ID token for a local Auth Emulator user, creating the user first if needed.
# Usage: token.sh <uid>   (dev-parent-1 and dev-parent-2 own the seeded Family)
set -euo pipefail

uid=${1:?usage: token.sh <uid>}
emulator=${AUTH_EMULATOR:-http://127.0.0.1:9099}
project=lantern-ai-bg1709
email="$uid@lantern.local"
password=local-password
json=(-H "Content-Type: application/json")

curl -s -o /dev/null -X POST "$emulator/identitytoolkit.googleapis.com/v1/projects/$project/accounts" \
  -H "Authorization: Bearer owner" "${json[@]}" \
  -d "{\"localId\":\"$uid\",\"email\":\"$email\",\"password\":\"$password\",\"displayName\":\"$uid\",\"emailVerified\":true}"

curl -sf -X POST "$emulator/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=local" "${json[@]}" \
  -d "{\"email\":\"$email\",\"password\":\"$password\",\"returnSecureToken\":true}" |
  sed -n 's/.*"idToken": *"\([^"]*\)".*/\1/p'
