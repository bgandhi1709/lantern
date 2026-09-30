#!/usr/bin/env bash
# Refine every chapter of one book as a single Claude Code cloud session.
#
#   scripts/cloud_refine_book.sh <book-id>
#
# One session per book keeps the fixed cloud-session overhead (VM, git clone, commit,
# push) to once per book instead of once per chapter - see docs/ideation/decision-log.md.
# --cloud refuses to run without a real TTY, so this shells out through `script` to fake
# one, and refuses a description over ~128KiB (Linux's MAX_ARG_STRLEN), so a book's
# chapters are sent as several messages to the same session, not one. Output lands on a
# pushed branch named cloud-refine-<book>; fetch it, then run `ncert-build check` locally
# per chapter.
set -euo pipefail
cd "$(dirname "$0")/.."
tool_dir="$(pwd)"
repo_root="$(cd ../.. && pwd)"

book="${1:?usage: cloud_refine_book.sh <book-id>}"
msg_dir="$(mktemp -d)"
trap 'rm -rf "$msg_dir"' EXIT

python3 scripts/build_cloud_prompt.py "$book" "$msg_dir"
if [ ! -f "$msg_dir/00-open.txt" ]; then
  echo "$book: nothing to do, skipping"
  exit 0
fi

send_via_arg() {
  # $1: file with message text (must stay under ~128KiB) - --cloud's value must
  # immediately follow the flag, before any other flags, or the CLI parser drops it.
  # $2: extra claude args placed after the value.
  local file="$1" extra="$2" run_file
  run_file="$(mktemp)"
  cat > "$run_file" <<EOF
#!/usr/bin/env bash
cd "$repo_root"
claude --cloud "\$(cat "$file")" $extra
EOF
  chmod +x "$run_file"
  script -qec "$run_file" "$msg_dir/raw.log"
  rm -f "$run_file"
}

send_via_stdin() {
  # $1: file with message text, $2: extra claude args - --cloud's follow-up form reads
  # the message from stdin (unlike session creation, which needs it as an argument)
  local file="$1" extra="$2" run_file
  run_file="$(mktemp)"
  cat > "$run_file" <<EOF
#!/usr/bin/env bash
cd "$repo_root"
claude $extra < "$file"
EOF
  chmod +x "$run_file"
  script -qec "$run_file" "$msg_dir/raw.log"
  rm -f "$run_file"
}

echo "== opening session cloud-refine-$book =="
send_via_arg "$msg_dir/00-open.txt" "--model sonnet -n cloud-refine-$book"
session_id="$(grep -oE 'session_[A-Za-z0-9]+' "$msg_dir/raw.log" | head -1)"
if [ -z "$session_id" ]; then
  echo "could not find session id in output, aborting" >&2
  cat "$msg_dir/raw.log" >&2
  exit 1
fi
echo "session: $session_id"

for f in "$msg_dir"/[0-9][0-9]-continue.txt; do
  [ -e "$f" ] || continue
  echo "== sending $(basename "$f") =="
  send_via_stdin "$f" "-p --cloud $session_id --output-format json"
done

echo "== sending finish message =="
send_via_stdin "$msg_dir/99-finish.txt" "-p --cloud $session_id --output-format json"

echo "session_id=$session_id"
