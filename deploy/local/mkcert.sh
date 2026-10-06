#!/bin/sh
# Runs inside an alpine container with openssl. Writes ca.crt, $HOST.crt and $HOST.key to $OUT.
set -eu

HOST=${HOST:-local.lantern.api}
OUT=${OUT:-/certs}
DAYS=${DAYS:-365}
# The Android emulator reaches the host machine at this address, and cannot resolve $HOST.
EMULATOR_IP=${EMULATOR_IP:-10.0.2.2}

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$OUT"

# The CA may only sign for $HOST and the emulator's host alias, and its key never leaves $work, so a leaked
# leaf key or a trusted ca.crt cannot be used against any other site.
openssl req -x509 -newkey rsa:2048 -nodes -days "$DAYS" \
  -keyout "$work/ca.key" -out "$OUT/ca.crt" \
  -subj "/CN=Lantern Local CA" \
  -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
  -addext "keyUsage=critical,keyCertSign,cRLSign" \
  -addext "nameConstraints=critical,permitted;DNS:$HOST,permitted;IP:$EMULATOR_IP/255.255.255.255"

openssl req -newkey rsa:2048 -nodes \
  -keyout "$OUT/$HOST.key" -out "$work/leaf.csr" -subj "/CN=$HOST"

cat > "$work/leaf.ext" <<EXT
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:$HOST,IP:$EMULATOR_IP
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid
EXT

openssl x509 -req -in "$work/leaf.csr" -CA "$OUT/ca.crt" -CAkey "$work/ca.key" \
  -set_serial "0x$(openssl rand -hex 16)" -days "$DAYS" -extfile "$work/leaf.ext" \
  -out "$OUT/$HOST.crt"

# Readable by the non-root user inside the api container.
chmod 644 "$OUT/$HOST.key" "$OUT/$HOST.crt" "$OUT/ca.crt"
echo "Wrote $OUT/ca.crt, $OUT/$HOST.crt, $OUT/$HOST.key"
