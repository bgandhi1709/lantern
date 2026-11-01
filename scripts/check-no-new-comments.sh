#!/usr/bin/env bash
# Fails when the staged diff adds a comment line to code. The reason for a choice goes in an ADR or the decision log,
# and a name or a small function carries the rest. Lint directives are exempt.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

added=$(git diff --cached -U0 --diff-filter=ACMR -- '*.cs' '*.ts' '*.tsx' '*.bicep' '*.bicepparam' |
  awk '
    /^\+\+\+ b\// { file = substr($0, 7) }
    /^\+[[:space:]]*(\/\/|\/\*)/ && !/eslint-|@ts-|prettier-ignore|#region|#pragma|auto-generated/ { print file ": " substr($0, 2) }
  ')

if [ -n "$added" ]; then
  echo "This commit adds comments:" >&2
  echo "$added" >&2
  echo "Rename or extract instead. A reason goes in the ADR or docs/ideation/decision-log.md." >&2
  exit 1
fi
