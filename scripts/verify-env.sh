#!/usr/bin/env bash
# One clean slate for verifying the Parent app: local Docker, one emulator, one Metro, a fresh install.
#   scripts/verify-env.sh up      tear everything down, then start it all fresh
#   scripts/verify-env.sh down    stop it all and free the disk (run it after the PR is created)
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
APP=$ROOT/client/lantern-android
COMPOSE=$ROOT/deploy/local/docker-compose.yml
AVD=${LANTERN_AVD:-Small_Phone}
# Software rendering: the host GPU path (gpu mode host) left the emulator dead mid-verify.
EMU_GPU=${LANTERN_EMU_GPU:-swiftshader_indirect}
PACKAGE=com.lantern.app
METRO_PORT=8081
LOGS=${TMPDIR:-/tmp}/lantern-verify
export ANDROID_HOME=${ANDROID_HOME:-$HOME/Android/Sdk}
export PATH=$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$PATH
# shellcheck disable=SC1091
[ -s "$HOME/.config/nvm/nvm.sh" ] && . "$HOME/.config/nvm/nvm.sh" >/dev/null
[ -n "${JAVA_HOME:-}" ] || JAVA_HOME=$(ls -d "$HOME"/.local/jdk/jdk-17* 2>/dev/null | head -1 || true)
[ -z "$JAVA_HOME" ] || { export JAVA_HOME; export PATH=$JAVA_HOME/bin:$PATH; }

metro_pid() { ss -ltnp 2>/dev/null | grep ":$METRO_PORT " | grep -oE 'pid=[0-9]+' | head -1 | cut -d= -f2 || true; }

down() {
  local pid
  pid=$(metro_pid)
  [ -z "$pid" ] || kill "$pid" || true
  if adb get-state >/dev/null 2>&1; then
    adb uninstall "$PACKAGE" >/dev/null 2>&1 || true
    adb emu kill >/dev/null 2>&1 || true
  fi
  # A qemu left behind by a crashed emulator makes the next one start offline.
  sleep 3
  pkill -f "qemu.*-avd $AVD" 2>/dev/null || true
  rm -f "$HOME/.android/avd/$AVD.avd/"*.lock
  docker compose -f "$COMPOSE" down --remove-orphans
  docker image prune -f >/dev/null
  docker builder prune -f >/dev/null
  rm -rf "${TMPDIR:-/tmp}"/metro-cache "${TMPDIR:-/tmp}"/metro-file-map-* "$LOGS"
  docker system df
  df -h / | tail -1
}

wait_for() { # <description> <attempts> <command...>
  local what=$1 tries=$2
  shift 2
  for _ in $(seq "$tries"); do "$@" >/dev/null 2>&1 && return 0; sleep 5; done
  echo "Timed out waiting for $what" >&2
  exit 1
}

emulator_booted() { [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ]; }
api_live() { [ "$(curl -sk -o /dev/null -w '%{http_code}' --max-time 3 https://local.lantern.api/health/live)" = 200 ]; }

up() {
  down
  mkdir -p "$LOGS"

  docker compose -f "$COMPOSE" up --build -d
  wait_for "the local API" 60 api_live

  # setsid keeps the emulator alive when this shell or the agent's shell ends.
  setsid nohup emulator -avd "$AVD" -gpu "$EMU_GPU" -no-snapshot-save -no-audio >"$LOGS/emulator.log" 2>&1 &
  wait_for "the emulator to boot" 90 emulator_booted

  # The emulator keeps its data between runs, so a saved sign-in survives unless the app is removed first.
  adb uninstall "$PACKAGE" >/dev/null 2>&1 || true
  cd "$APP"
  npx expo run:android --no-bundler >"$LOGS/build.log" 2>&1
  CI=1 setsid nohup npx expo start --dev-client --port "$METRO_PORT" --clear >"$LOGS/metro.log" 2>&1 &
  wait_for "Metro" 24 grep -q "Waiting on" "$LOGS/metro.log"
  adb reverse "tcp:$METRO_PORT" "tcp:$METRO_PORT" >/dev/null
  # expo run:android already opened the app at a LAN address; a running app ignores the launch link.
  adb shell am force-stop "$PACKAGE"
  adb shell am start -a android.intent.action.VIEW \
    -d "lantern://expo-development-client/?url=http%3A%2F%2Flocalhost%3A$METRO_PORT" "$PACKAGE" >/dev/null
  echo "Ready: Docker, emulator, Metro (--clear) and a fresh install. Logs in $LOGS"
}

case ${1:-} in
  up) up ;;
  down) down ;;
  *) echo "Usage: verify-env.sh up | down" >&2; exit 2 ;;
esac
