#!/usr/bin/env bash
# Run once with sudo: sudo scripts/raise-inotify-limit.sh
# Metro, the emulator and the editor each watch files, and the default of 128 inotify instances lets Metro miss edits and serve a
# stale bundle. This raises the limit now and keeps it across reboots.
set -euo pipefail

[ "$(id -u)" -eq 0 ] || { echo "Run it with sudo: sudo $0" >&2; exit 1; }

printf 'fs.inotify.max_user_instances=1024\nfs.inotify.max_user_watches=524288\n' >/etc/sysctl.d/90-lantern-inotify.conf
sysctl --system >/dev/null
echo "inotify instances now: $(cat /proc/sys/fs/inotify/max_user_instances)"
