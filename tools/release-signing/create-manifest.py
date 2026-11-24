#!/usr/bin/env python3
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

MAX_ASSET_BYTES = 1024 * 1024 * 1024
MAX_RELEASE_NOTES_BYTES = 16 * 1024
VERSION_RE = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\Z")


def parse_version(value: str) -> tuple[int, int, int]:
    if not VERSION_RE.fullmatch(value):
        raise ValueError(f"Invalid stable version: {value}")
    return tuple(int(part) for part in value.split("."))


def main() -> int:
    parser = argparse.ArgumentParser(description="Create exact UTF-8 PS5 Windows Hub release manifest bytes.")
    parser.add_argument("--version", required=True)
    parser.add_argument("--minimum-supported-version", required=True)
    parser.add_argument("--asset", required=True, type=Path)
    parser.add_argument("--asset-url", required=True)
    parser.add_argument("--release-notes", required=True, type=Path)
    parser.add_argument("--output", default="manifest.json", type=Path)
    args = parser.parse_args()

    try:
        version = parse_version(args.version)
        minimum = parse_version(args.minimum_supported_version)
        if minimum > version:
            raise ValueError("Minimum supported version cannot exceed release version.")

        expected_name = f"PS5-Windows-Hub-{args.version}.zip"
        if args.asset.name != expected_name or not args.asset.is_file():
            raise ValueError(f"Asset must be an existing file named exactly {expected_name}.")

        parsed = urlparse(args.asset_url)
        expected_path = f"/releases/download/v{args.version}/{expected_name}"
        if (parsed.scheme != "https" or parsed.hostname != "github.com" or parsed.port not in (None, 443)
                or parsed.username or parsed.password or parsed.query or parsed.fragment
                or unquote(parsed.path) != f"/loudply0-eng/PS5-Windows-Hub-Builder{expected_path}"):
            raise ValueError("Asset URL must be the exact HTTPS GitHub release URL for this version.")

        size = args.asset.stat().st_size
        if size <= 0 or size > MAX_ASSET_BYTES:
            raise ValueError("Asset size is outside the 1 GiB limit.")

        notes_bytes = args.release_notes.read_bytes()
        if len(notes_bytes) > MAX_RELEASE_NOTES_BYTES:
            raise ValueError("Release notes exceed 16 KiB.")
        notes = notes_bytes.decode("utf-8")

        digest = hashlib.sha256()
        with args.asset.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)

        manifest = {
            "version": args.version,
            "minimum_supported_version": args.minimum_supported_version,
            "asset_name": expected_name,
            "asset_url": args.asset_url,
            "size_bytes": size,
            "sha256": digest.hexdigest(),
            "release_notes": notes,
        }
        encoded = json.dumps(manifest, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if len(encoded) > 64 * 1024:
            raise ValueError("Generated manifest exceeds 64 KiB.")

        args.output.write_bytes(encoded)
        print(f"Manifest written: {args.output}")
        print(f"Asset size: {size} bytes")
        print(f"SHA-256: {digest.hexdigest()}")
        print("Do not edit or reformat manifest.json after signing it.")
        return 0
    except (OSError, UnicodeError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
