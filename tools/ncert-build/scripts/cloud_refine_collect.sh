#!/usr/bin/env bash
# Poll for cloud-refine-<book> branches from cloud_refine_all.sh, fetch each as it lands,
# place its output under lantern-data/refined/<book>/, and run ncert-build check.
#
#   scripts/cloud_refine_collect.sh [timeout_minutes]
#
# Reads the manifest cloud_refine_all.sh wrote. Safe to re-run: already-collected books
# (their refined/ JSON already on disk) are skipped.
set -uo pipefail
cd "$(dirname "$0")/.."
data_dir="${LANTERN_DATA_DIR:-$(cd ../../.. && pwd)/lantern-data}"
manifest="$data_dir/logs/cloud-refine-manifest.tsv"
repo_root="$(cd ../.. && pwd)"
timeout_min="${1:-60}"
deadline=$((SECONDS + timeout_min * 60))

mapfile -t books < <(cut -f1 "$manifest" | grep -v '^$')
pending=("${books[@]}")
echo "watching ${#pending[@]} book(s), timeout ${timeout_min}m"

while [ ${#pending[@]} -gt 0 ] && [ $SECONDS -lt $deadline ]; do
  next_pending=()
  for book in "${pending[@]}"; do
    branch="cloud-refine-$book"
    remote_sha="$(git -C "$repo_root" ls-remote origin "refs/heads/$branch" 2>/dev/null | cut -f1)"
    if [ -z "$remote_sha" ]; then
      next_pending+=("$book")
      continue
    fi
    git -C "$repo_root" fetch -q origin "$branch" 2>&1
    files="$(git -C "$repo_root" ls-tree -r --name-only "origin/$branch" -- "cloud-output/$book" 2>/dev/null)"
    if [ -z "$files" ]; then
      next_pending+=("$book")
      continue
    fi
    mkdir -p "$data_dir/refined/$book"
    ids=()
    while IFS= read -r f; do
      id="$(basename "$f" .json)"
      git -C "$repo_root" show "origin/$branch:$f" > "$data_dir/refined/$book/$id.json"
      ids+=("$id")
    done <<< "$files"
    echo "== $book: fetched ${#ids[@]} chapter(s), checking =="
    ( cd "$repo_root/tools/ncert-build" && .venv/bin/ncert-build check "${ids[@]}" 2>&1 )
  done
  pending=("${next_pending[@]}")
  [ ${#pending[@]} -eq 0 ] && break
  echo "-- still waiting on: ${pending[*]} ($(( (deadline - SECONDS) / 60 ))m left --"
  sleep 30
done

if [ ${#pending[@]} -gt 0 ]; then
  echo "TIMED OUT waiting on: ${pending[*]}"
  exit 1
fi
echo "all books collected"
