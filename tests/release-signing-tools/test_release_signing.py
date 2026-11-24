#!/usr/bin/env python3
import json
import os
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "tools" / "release-signing"
VERSION = "1.1.0"
ASSET_NAME = f"PS5-Windows-Hub-{VERSION}.zip"
ASSET_URL = (
    "https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/"
    f"v{VERSION}/{ASSET_NAME}"
)


def run(args, *, expect_success=True):
    result = subprocess.run(
        [str(arg) for arg in args],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        check=False,
    )
    if expect_success and result.returncode != 0:
        raise AssertionError(f"Command failed ({result.returncode}): {args}\n{result.stdout}")
    if not expect_success and result.returncode == 0:
        raise AssertionError(f"Command unexpectedly succeeded: {args}\n{result.stdout}")
    return result


def main():
    for script in (TOOLS / "generate-key.sh", TOOLS / "sign-manifest.sh", TOOLS / "publish-release.sh"):
        run(["bash", "-n", script])

    with tempfile.TemporaryDirectory(prefix="ps5-hub-release-signing-test-") as temporary:
        work = Path(temporary)
        private_key = work / "test-private.pem"
        public_key = work / "test-public.pem"
        asset = work / ASSET_NAME
        notes = work / "release-notes.txt"
        manifest = work / "manifest.json"
        signature = work / "manifest.sig.base64"

        run([
            "openssl", "genpkey", "-algorithm", "EC",
            "-pkeyopt", "ec_paramgen_curve:prime256v1",
            "-pkeyopt", "ec_param_enc:named_curve",
            "-out", private_key,
        ])
        run(["openssl", "pkey", "-in", private_key, "-pubout", "-out", public_key])

        with zipfile.ZipFile(asset, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("PS5-Windows-Hub.exe", b"test executable")
            archive.writestr("PS5-Windows-Hub.dll", b"test assembly")
            archive.writestr("PS5WindowsHub.Updater.exe", b"test updater")
            archive.writestr("relinker.exe", b"test relinker")
        notes.write_text("Security test release notes\n", encoding="utf-8")

        run([
            sys.executable, TOOLS / "create-manifest.py",
            "--version", VERSION,
            "--minimum-supported-version", "1.0.0",
            "--asset", asset,
            "--asset-url", ASSET_URL,
            "--release-notes", notes,
            "--output", manifest,
        ])

        document = json.loads(manifest.read_text(encoding="utf-8"))
        assert document["asset_name"] == ASSET_NAME
        assert document["size_bytes"] == asset.stat().st_size
        assert len(document["sha256"]) == 64

        run(["bash", TOOLS / "sign-manifest.sh", private_key, manifest, signature])
        run([sys.executable, TOOLS / "verify-manifest.py", manifest, asset, public_key, signature])

        tampered_asset = work / "tampered.zip"
        tampered_asset.write_bytes(asset.read_bytes() + b"changed")
        run(
            [sys.executable, TOOLS / "verify-manifest.py", manifest, tampered_asset, public_key, signature],
            expect_success=False,
        )

        tampered_manifest = work / "tampered-manifest.json"
        tampered_manifest.write_bytes(manifest.read_bytes() + b" ")
        run(
            [sys.executable, TOOLS / "verify-manifest.py", tampered_manifest, asset, public_key, signature],
            expect_success=False,
        )

        wrong_key = work / "wrong-public.pem"
        wrong_private = work / "wrong-private.pem"
        run([
            "openssl", "genpkey", "-algorithm", "EC",
            "-pkeyopt", "ec_paramgen_curve:prime256v1",
            "-pkeyopt", "ec_param_enc:named_curve",
            "-out", wrong_private,
        ])
        run(["openssl", "pkey", "-in", wrong_private, "-pubout", "-out", wrong_key])
        run(
            [sys.executable, TOOLS / "verify-manifest.py", manifest, asset, wrong_key, signature],
            expect_success=False,
        )

    print("Offline release signing tool tests: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
