#!/usr/bin/env bash
# Launch one cloud-session refine run per book that still has unrefined chapters, all in
# parallel. Idempotent: a book with nothing pending is skipped (see build_cloud_prompt.py).
#
#   scripts/cloud_refine_all.sh [book-id ...]
#
# With no arguments, discovers every book under ../lantern-data/bundles/. Writes a
# manifest (book, session_id, log path) to $LANTERN_DATA_DIR/logs/cloud-refine-manifest.tsv
# for cloud_refine_collect.sh to poll afterward.
set -euo pipefail
cd "$(dirname "$0")/.."
data_dir="${LANTERN_DATA_DIR:-$(cd ../../.. && pwd)/lantern-data}"
log_dir="$data_dir/logs/cloud-refine"
mkdir -p "$log_dir"
manifest="$data_dir/logs/cloud-refine-manifest.tsv"
: > "$manifest"

books=("$@")
if [ ${#books[@]} -eq 0 ]; then
  while IFS= read -r d; do books+=("$(basename "$d")"); done < <(find "$data_dir/bundles" -mindepth 1 -maxdepth 1 -type d | sort)
fi

echo "launching ${#books[@]} book(s) in parallel..."
pids=()
for book in "${books[@]}"; do
  (
    log="$log_dir/$book.log"
    if scripts/cloud_refine_book.sh "$book" > "$log" 2>&1; then
      session_id="$(grep -oE '^session_id=.*' "$log" | tail -1 | cut -d= -f2)"
      printf '%s\t%s\t%s\n' "$book" "${session_id:-none}" "$log" >> "$manifest"
    else
      printf '%s\t%s\t%s\n' "$book" "FAILED" "$log" >> "$manifest"
    fi
  ) &
  pids+=($!)
done

for pid in "${pids[@]}"; do wait "$pid" || true; done

echo "done launching. manifest: $manifest"
column -t "$manifest"
