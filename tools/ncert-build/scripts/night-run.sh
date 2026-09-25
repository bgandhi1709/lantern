#!/usr/bin/env bash
# The long unattended build: text (with fractions), the remaining drafts, then figures class by
# class, each class followed by its bundles and an agreement report. Every step resumes where it
# stopped, so running this again after a reboot or a crash continues the work.
#
#   setsid nohup scripts/night-run.sh [--shutdown] > "$LANTERN_DATA_DIR/logs/night-$(date +%Y%m%d-%H%M).log" 2>&1 &
#
# --shutdown: Windows shuts down 5 minutes after the last class (cancel with `shutdown /a`).
# The GPU pauses above 84 °C until it's back at 76 °C (LANTERN_GPU_PAUSE_AT / _RESUME_AT), and
# only works 20:00-08:00 India time (LANTERN_RUN_WINDOW, "" for any hour); outside it the job waits.
set -u
cd "$(dirname "$0")/.."
export PYTHONUNBUFFERED=1
ncert=.venv/bin/ncert-build
grades="${LANTERN_GRADES:-1 2 3 4 5 6 7 8 9 10}"
shutdown=""
[ "${1:-}" = "--shutdown" ] && shutdown="--shutdown"

stamp() { echo; echo "=== $(date '+%F %T') $*"; }

stamp "text: re-extract (fractions) and re-segment what changed"
$ncert extract --grades 1-10
$ncert segment --grades 1-10

stamp "drafts still to do"
$ncert draft --grades 1-10 -v

for grade in $grades; do
  stamp "Class $grade: figures"
  $ncert pictures --grades "$grade" -v
  stamp "Class $grade: bundles and report"
  $ncert bundle --grades "$grade" --force
  $ncert review --grades "$grade" | sed -n '/^Class /p'
done

stamp "all classes: report and review sheet"
$ncert review --grades 1-10
stamp "done"
if [ -n "$shutdown" ]; then
  /mnt/c/Windows/System32/shutdown.exe /s /t 300 /c "Lantern night run finished"
  echo "Windows shuts down in 5 minutes (cancel: shutdown /a)"
fi
