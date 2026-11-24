#!/usr/bin/env python3
import argparse
import base64
import re
import subprocess
import sys
import tempfile
from pathlib import Path

PLACEHOLDER = 'internal const string PublicKeyBase64 = "";'


def main() -> int:
    parser = argparse.ArgumentParser(description="Embed only the P-256 public update-verification key in the Windows app source.")
    parser.add_argument("--public-key-base64", required=True, type=Path)
    parser.add_argument("--source", default="app/PS5WindowsHub/UpdateTrust.cs", type=Path)
    args = parser.parse_args()

    try:
        encoded = args.public_key_base64.read_text(encoding="ascii").strip()
        if not re.fullmatch(r"[A-Za-z0-9+/]+={0,2}", encoded):
            raise ValueError("Public key file is not canonical base64.")
        der = base64.b64decode(encoded, validate=True)
        if len(der) < 80 or len(der) > 256:
            raise ValueError("P-256 SubjectPublicKeyInfo DER length is outside the expected range.")

        with tempfile.TemporaryDirectory(prefix="ps5-hub-public-key-") as temporary:
            der_path = Path(temporary) / "public-key.der"
            der_path.write_bytes(der)
            result = subprocess.run(
                ["openssl", "pkey", "-pubin", "-inform", "DER", "-in", str(der_path), "-text", "-noout"],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                check=True,
            )
        if "ASN1 OID: prime256v1" not in result.stdout or "Public-Key: (256 bit)" not in result.stdout:
            raise ValueError("The supplied public key is not an ECDSA P-256 key.")

        source = args.source.read_text(encoding="utf-8")
        occurrences = source.count(PLACEHOLDER)
        if occurrences != 1:
            raise ValueError("Expected exactly one empty public-key placeholder; refusing to overwrite an existing key.")
        replacement = f'internal const string PublicKeyBase64 = "{encoded}";'
        updated = source.replace(PLACEHOLDER, replacement)
        if updated == source:
            raise ValueError("The source public key was not changed.")

        args.source.write_text(updated, encoding="utf-8", newline="\n")
        print("Embedded the validated public P-256 key.")
        print("Only public key material was read. No private key was accessed.")
        print("Review and commit UpdateTrust.cs, then rebuild and run the full CI suite.")
        return 0
    except (OSError, UnicodeError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
