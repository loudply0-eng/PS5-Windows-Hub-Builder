#!/usr/bin/env bash
set -euo pipefail
umask 077

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <private-key.pem> <manifest.json> <manifest.sig.base64>" >&2
  exit 2
fi

PRIVATE_KEY="$1"
MANIFEST="$2"
SIGNATURE_OUT="$3"

[[ -f "$PRIVATE_KEY" ]] || { echo "Private key not found." >&2; exit 1; }
[[ -f "$MANIFEST" ]] || { echo "Manifest not found." >&2; exit 1; }
command -v openssl >/dev/null 2>&1 || { echo "OpenSSL is required." >&2; exit 1; }

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT
openssl dgst -sha256 -sign "$PRIVATE_KEY" -out "$tmp" "$MANIFEST"
base64 "$tmp" | tr -d '\r\n' > "$SIGNATURE_OUT"
chmod 600 "$SIGNATURE_OUT"

echo "Detached ECDSA P-256/SHA-256 signature written to: $SIGNATURE_OUT"
echo "The signature is base64-encoded DER (RFC 3279), matching the app verifier."
