#!/usr/bin/env bash
set -euo pipefail
umask 077

KEY_DIR="${1:-$HOME/.local/share/ps5-windows-hub-signing}"
PRIVATE_KEY="$KEY_DIR/update-signing-private.pem"
PUBLIC_KEY="$KEY_DIR/update-signing-public.pem"
PUBLIC_KEY_BASE64="$KEY_DIR/public-key-spki-base64.txt"

if ! command -v openssl >/dev/null 2>&1; then
  echo "OpenSSL is required." >&2
  exit 1
fi
if [[ -e "$PRIVATE_KEY" || -e "$PUBLIC_KEY" || -e "$PUBLIC_KEY_BASE64" ]]; then
  echo "A signing key already exists in: $KEY_DIR" >&2
  echo "Refusing to overwrite it. Back it up securely or choose a new directory." >&2
  exit 1
fi

mkdir -p "$KEY_DIR"
chmod 700 "$KEY_DIR"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:prime256v1 -pkeyopt ec_param_enc:named_curve -aes-256-cbc -out "$PRIVATE_KEY"
openssl pkey -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY"
openssl pkey -pubin -in "$PUBLIC_KEY" -outform DER | base64 | tr -d '\r\n' > "$PUBLIC_KEY_BASE64"
chmod 600 "$PRIVATE_KEY" "$PUBLIC_KEY" "$PUBLIC_KEY_BASE64"

echo "Signing key generated locally."
echo "Private key: $PRIVATE_KEY"
echo "Public key:  $PUBLIC_KEY"
echo "Public SPKI base64: $PUBLIC_KEY_BASE64"
echo
echo "IMPORTANT: Keep the private key only on this offline device and an encrypted offline backup."
echo "Only the public SPKI base64 value may be added to the Windows application."
