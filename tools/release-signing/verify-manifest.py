#!/usr/bin/env python3
import argparse
import base64
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import urlparse

MAX_ASSET_BYTES = 1024 * 1024 * 1024
MAX_MANIFEST_BYTES = 64 * 1024
MAX_SIGNATURE_BYTES = 256
MAX_NOTES_BYTES = 16 * 1024
VERSION_RE = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\Z")
REQUIRED_FIELDS = {
    "version", "minimum_supported_version", "asset_name", "asset_url",
    "size_bytes", "sha256", "release_notes"
}


def reject_duplicate_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON property: {key}")
        result[key] = value
    return result


def parse_version(value):
    if not isinstance(value, str) or not VERSION_RE.fullmatch(value):
        raise ValueError(f"Invalid stable version: {value!r}")
    return tuple(int(part) for part in value.split("."))


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify a signed PS5 Windows Hub release manifest and its ZIP asset.")
    parser.add_argument("manifest", type=Path)
    parser.add_argument("asset", type=Path)
    parser.add_argument("public_key", type=Path)
    parser.add_argument("signature", type=Path)
    args = parser.parse_args()

    try:
        raw = args.manifest.read_bytes()
        if not raw or len(raw) > MAX_MANIFEST_BYTES:
            raise ValueError("Manifest is empty or exceeds 64 KiB.")
        document = json.loads(raw.decode("utf-8"), object_pairs_hook=reject_duplicate_keys)
        if not isinstance(document, dict) or set(document) != REQUIRED_FIELDS:
            raise ValueError("Manifest must contain exactly the seven required properties.")

        version = parse_version(document["version"])
        minimum = parse_version(document["minimum_supported_version"])
        if minimum > version:
            raise ValueError("Minimum supported version cannot exceed release version.")

        expected_name = f"PS5-Windows-Hub-{document['version']}.zip"
        if document["asset_name"] != expected_name or args.asset.name != expected_name or not args.asset.is_file():
            raise ValueError("Manifest and local asset filename do not match the release version.")

        parsed = urlparse(document["asset_url"])
        expected_path = f"/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v{document['version']}/{expected_name}"
        if (parsed.scheme != "https" or parsed.hostname != "github.com" or parsed.port not in (None, 443)
                or parsed.username or parsed.password or parsed.query or parsed.fragment
                or parsed.path != expected_path):
            raise ValueError("Manifest asset URL is not the exact versioned GitHub release URL.")

        size = args.asset.stat().st_size
        if type(document["size_bytes"]) is not int or document["size_bytes"] != size or size <= 0 or size > MAX_ASSET_BYTES:
            raise ValueError("Manifest asset size does not match the ZIP or exceeds 1 GiB.")

        if not isinstance(document["sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", document["sha256"]):
            raise ValueError("Manifest SHA-256 must be 64 lowercase hexadecimal characters.")
        digest = hashlib.sha256()
        with args.asset.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest() != document["sha256"]:
            raise ValueError("Manifest SHA-256 does not match the update ZIP.")

        if not isinstance(document["release_notes"], str) or len(document["release_notes"].encode("utf-8")) > MAX_NOTES_BYTES:
            raise ValueError("Release notes are invalid or exceed 16 KiB.")

        signature_text = args.signature.read_bytes()
        if not signature_text or len(signature_text) > MAX_SIGNATURE_BYTES:
            raise ValueError("Detached signature file is empty or too large.")
        signature_der = base64.b64decode(signature_text, validate=True)
        if len(signature_der) < 8 or len(signature_der) > 80:
            raise ValueError("ECDSA DER signature size is invalid.")

        key_info = subprocess.run(
            ["openssl", "pkey", "-pubin", "-in", str(args.public_key), "-text", "-noout"],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            check=True,
        ).stdout
        if "Public-Key: (256 bit)" not in key_info or "ASN1 OID: prime256v1" not in key_info:
            raise ValueError("The update verification key must be ECDSA P-256.")

        with tempfile.TemporaryDirectory(prefix="ps5-hub-signature-") as temporary:
            signature_path = Path(temporary) / "manifest.sig.der"
            signature_path.write_bytes(signature_der)
            subprocess.run(
                ["openssl", "dgst", "-sha256", "-verify", str(args.public_key),
                 "-signature", str(signature_path), str(args.manifest)],
                check=True
            )

        print("Manifest schema, asset size, SHA-256, exact URL, and detached signature: PASS")
        return 0
    except (OSError, UnicodeError, ValueError, json.JSONDecodeError, subprocess.CalledProcessError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
