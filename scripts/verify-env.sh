#!/usr/bin/env bash
# One clean slate for verifying the Parent app: local Docker, one emulator, one Metro, a fresh install.
#   scripts/verify-env.sh up      tear everything down, then start it all fresh
#   scripts/verify-env.sh down    stop it all and free the disk (run it after the PR is created)
#   scripts/verify-env.sh down --deep   also remove the base images (a re-pull costs about 2.5 GB)
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
APP=$ROOT/client/lantern-android
COMPOSE=$ROOT/deploy/local/docker-compose.yml
AVD=${LANTERN_AVD:-Lantern_34}
# Software rendering: the host GPU path (gpu mode host) left the emulator dead mid-verify.
EMU_GPU=${LANTERN_EMU_GPU:-swiftshader_indirect}
# PACKAGE and METRO_PORT are repeated in client/lantern-android/.maestro/*.yaml, which cannot read this file.
PACKAGE=com.lantern.app
METRO_PORT=8081
POLL_SECONDS=5
EMULATOR_EXIT_SECONDS=3
API_TIMEOUT_SECONDS=300
EMULATOR_BOOT_TIMEOUT_SECONDS=450
METRO_TIMEOUT_SECONDS=120
LOGS=${TMPDIR:-/tmp}/lantern-verify
export ANDROID_HOME=${ANDROID_HOME:-$HOME/Android/Sdk}
export PATH=$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$HOME/.local/maestro/bin:$PATH
# shellcheck disable=SC1091
[ -s "$HOME/.config/nvm/nvm.sh" ] && . "$HOME/.config/nvm/nvm.sh" >/dev/null
[ -n "${JAVA_HOME:-}" ] || JAVA_HOME=$(ls -d "$HOME"/.local/jdk/jdk-17* 2>/dev/null | head -1 || true)
[ -z "$JAVA_HOME" ] || { export JAVA_HOME; export PATH=$JAVA_HOME/bin:$PATH; }

metro_pid() { ss -ltnp 2>/dev/null | grep ":$METRO_PORT " | grep -oE 'pid=[0-9]+' | head -1 | cut -d= -f2 || true; }

down() {
  local deep=${1:-}
  local pid
  pid=$(metro_pid)
  [ -z "$pid" ] || kill "$pid" || true
  if adb get-state >/dev/null 2>&1; then
    adb uninstall "$PACKAGE" >/dev/null 2>&1 || true
    adb emu kill >/dev/null 2>&1 || true
  fi
  # A qemu left behind by a crashed emulator makes the next one start offline.
  sleep "$EMULATOR_EXIT_SECONDS"
  pkill -f "qemu.*-avd $AVD" 2>/dev/null || true
  rm -f "$HOME/.android/avd/$AVD.avd/"*.lock
  # --deep removes this stack's own images and the whole build cache; the images of other projects stay.
  local rmi=() builder_all=()
  [ "$deep" != --deep ] || { rmi=(--rmi all); builder_all=(-a); }
  docker compose -f "$COMPOSE" down --remove-orphans "${rmi[@]}"
  docker image prune -f >/dev/null
  docker builder prune -f "${builder_all[@]}" >/dev/null
  rm -rf "${TMPDIR:-/tmp}"/metro-cache "${TMPDIR:-/tmp}"/metro-file-map-* "$LOGS"
  docker system df
  df -h / | tail -1
}

wait_for() { # <description> <timeout seconds> <command...>
  local what=$1 tries=$(($2 / POLL_SECONDS))
  shift 2
  for _ in $(seq "$tries"); do "$@" >/dev/null 2>&1 && return 0; sleep "$POLL_SECONDS"; done
  echo "Timed out waiting for $what" >&2
  exit 1
}

emulator_booted() { [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ]; }
api_live() { [ "$(curl -sk -o /dev/null -w '%{http_code}' --max-time 3 https://local.lantern.api/health/live)" = 200 ]; }

up() {
  down
  mkdir -p "$LOGS"

  docker compose -f "$COMPOSE" up --build -d
  wait_for "the local API" "$API_TIMEOUT_SECONDS" api_live

  # -no-snapshot neither loads nor saves quick-boot state, which otherwise grows the AVD on every run.
  # setsid keeps the emulator alive when this shell or the agent's shell ends.
  setsid nohup emulator -avd "$AVD" -gpu "$EMU_GPU" -no-snapshot -no-audio >"$LOGS/emulator.log" 2>&1 &
  wait_for "the emulator to boot" "$EMULATOR_BOOT_TIMEOUT_SECONDS" emulator_booted

  # The emulator keeps its data between runs, so a saved sign-in survives unless the app is removed first.
  adb uninstall "$PACKAGE" >/dev/null 2>&1 || true
  cd "$APP"
  npx expo run:android --no-bundler >"$LOGS/build.log" 2>&1
  CI=1 setsid nohup npx expo start --dev-client --port "$METRO_PORT" --clear >"$LOGS/metro.log" 2>&1 &
  wait_for "Metro" "$METRO_TIMEOUT_SECONDS" grep -q "Waiting on" "$LOGS/metro.log"
  adb reverse "tcp:$METRO_PORT" "tcp:$METRO_PORT" >/dev/null
  # expo run:android already opened the app at a LAN address; a running app ignores the launch link.
  adb shell am force-stop "$PACKAGE"
  adb shell am start -a android.intent.action.VIEW \
    -d "lantern://expo-development-client/?url=http%3A%2F%2Flocalhost%3A$METRO_PORT" "$PACKAGE" >/dev/null
  echo "Ready: Docker, emulator, Metro (--clear) and a fresh install. Logs in $LOGS"
}

case ${1:-} in
  up) up ;;
  down) down "${2:-}" ;;
  *) echo "Usage: verify-env.sh up | down [--deep]" >&2; exit 2 ;;
esac
