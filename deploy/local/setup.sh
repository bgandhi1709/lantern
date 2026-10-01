#!/usr/bin/env bash
# Local HTTPS for local.lantern.api on WSL and Linux. On WSL it also runs setup.ps1 for Windows.
set -euo pipefail

HOST=local.lantern.api
DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
CERTS=$DIR/certs
CA_TARGET=/usr/local/share/ca-certificates/lantern-local-ca.crt

remove=0 force=0 windows=1 dry=0

usage() {
  cat <<USAGE
Usage: setup.sh [--remove] [--force] [--no-windows] [--dry-run]

  (none)         create certificates, add the hosts entry, trust the CA
  --remove       undo the hosts entry and the CA trust
  --force        regenerate the certificates
  --no-windows   on WSL, skip the Windows hosts entry and trust store
  --dry-run      print what would change and change nothing
USAGE
}

for arg in "$@"; do
  case $arg in
    --remove) remove=1 ;;
    --force) force=1 ;;
    --no-windows) windows=0 ;;
    --dry-run) dry=1 ;;
    -h | --help) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done

die() { echo "error: $*" >&2; exit 1; }
is_wsl() { grep -qi microsoft /proc/version 2>/dev/null; }
[[ $(uname -s) == Linux ]] || die "this script supports Linux and WSL; on Windows run setup.ps1"

act() {
  if ((dry)); then printf '+ %s\n' "$*"; return; fi
  "$@"
}

as_root() {
  if ((EUID == 0)); then act "$@"; else act sudo "$@"; fi
}

generate_certs() {
  if [[ -f $CERTS/ca.crt && -f $CERTS/$HOST.crt && -f $CERTS/$HOST.key ]] && ((!force)); then
    echo "Certificates already exist (use --force to regenerate)."
    return
  fi

  local docker=docker mount=$DIR
  if ! command -v docker >/dev/null; then
    command -v docker.exe >/dev/null || die "Docker is required to generate the certificates"
    docker=docker.exe
    mount=$(wslpath -w "$DIR")
  fi

  act mkdir -p "$CERTS"
  act "$docker" run --rm -v "$mount:/work" -e "HOST=$HOST" -e OUT=/work/certs alpine:3 \
    sh -c 'apk add --no-cache openssl >/dev/null && sh /work/mkcert.sh'
}

has_hosts_entry() {
  grep -Eq "^[[:space:]]*[^#[:space:]]+[[:space:]]+([^#]*[[:space:]])?$HOST([[:space:]]|$)" /etc/hosts
}

add_hosts_entry() {
  if has_hosts_entry; then echo "/etc/hosts already maps $HOST."; return; fi
  if ((dry)); then echo "+ append '127.0.0.1 $HOST' to /etc/hosts"; return; fi
  echo "127.0.0.1 $HOST" | as_root tee -a /etc/hosts >/dev/null
}

remove_hosts_entry() {
  as_root sed -i "/[[:space:]]$HOST\([[:space:]]\|\$\)/d" /etc/hosts
}

trust_ca() {
  if ! command -v update-ca-certificates >/dev/null; then
    echo "warning: update-ca-certificates not found; trust $CERTS/ca.crt manually" >&2
    return
  fi
  as_root install -m 644 "$CERTS/ca.crt" "$CA_TARGET"
  as_root update-ca-certificates
}

untrust_ca() {
  command -v update-ca-certificates >/dev/null || return 0
  as_root rm -f "$CA_TARGET"
  as_root update-ca-certificates --fresh
}

windows_side() {
  if ! is_wsl || ((!windows)); then return 0; fi
  local args=(-NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$DIR/setup.ps1")")
  if ((remove)); then args+=(-Remove); fi
  if ((dry)); then args+=(-WhatIf); fi
  act powershell.exe "${args[@]}"
}

if ((remove)); then
  remove_hosts_entry
  untrust_ca
  windows_side
  if ((!dry)); then echo "Removed."; fi
  exit 0
fi

generate_certs
add_hosts_entry
trust_ca
windows_side

if is_wsl && ! grep -q 'generateHosts *= *false' /etc/wsl.conf 2>/dev/null; then
  echo "note: WSL rewrites /etc/hosts on restart. To keep the entry, add this to /etc/wsl.conf:"
  echo "      [network]"
  echo "      generateHosts=false"
fi

echo
echo "Start it:  docker compose -f deploy/local/docker-compose.yml up --build"
echo "Then open: https://$HOST/health/live"
