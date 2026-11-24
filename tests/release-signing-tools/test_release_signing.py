#!/usr/bin/env python3
import base64
import json
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
    converted = []
    bash_command = str(args[0]).lower() in ("bash", "bash.exe")
    if bash_command:
        # Python is a native Windows process in this job. Resolving "bash"
        # through Windows PATH can select the WSL shim in System32 instead
        # of the MSYS2 shell that is running this test.
        bash_path = subprocess.run(
            ["cygpath", "-w", "/usr/bin/bash"],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            check=True,
        )
        converted.append(bash_path.stdout.strip())

    for arg in (args[1:] if bash_command else args):
        if isinstance(arg, Path) and bash_command:
            path_result = subprocess.run(
                ["cygpath", "-u", str(arg)],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                check=True,
            )
            converted.append(path_result.stdout.strip())
        else:
            converted.append(str(arg))
    result = subprocess.run(
        converted,
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

        public_der = subprocess.run(
            ["openssl", "pkey", "-pubin", "-in", str(public_key), "-outform", "DER"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=True,
        ).stdout
        public_key_b64 = base64.b64encode(public_der).decode("ascii")
        public_key_b64_file = work / "public-key-spki-base64.txt"
        public_key_b64_file.write_text(public_key_b64, encoding="ascii")
        trust_source = work / "UpdateTrust.cs"
        trust_source.write_text(
            (ROOT / "app" / "PS5WindowsHub" / "UpdateTrust.cs").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        run([
            sys.executable, TOOLS / "embed-public-key.py",
            "--public-key-base64", public_key_b64_file,
            "--source", trust_source,
        ])
        assert f'PublicKeyBase64 = "{public_key_b64}";' in trust_source.read_text(encoding="utf-8")

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

        encoded_url_manifest = work / "encoded-url-manifest.json"
        encoded_url = ASSET_URL.replace("/releases/download/", "/releases%2Fdownload/")
        run([
            sys.executable, TOOLS / "create-manifest.py",
            "--version", VERSION,
            "--minimum-supported-version", "1.0.0",
            "--asset", asset,
            "--asset-url", encoded_url,
            "--release-notes", notes,
            "--output", encoded_url_manifest,
        ], expect_success=False)

        run(["bash", TOOLS / "sign-manifest.sh", private_key, manifest, signature])
        run([sys.executable, TOOLS / "verify-manifest.py", manifest, asset, public_key, signature])

        tampered_directory = work / "tampered"
        tampered_directory.mkdir()
        tampered_asset = tampered_directory / ASSET_NAME
        changed_bytes = bytearray(asset.read_bytes())
        changed_bytes[0] ^= 1
        tampered_asset.write_bytes(changed_bytes)
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

        wrong_curve_private = work / "wrong-curve-private.pem"
        wrong_curve_public = work / "wrong-curve-public.pem"
        run([
            "openssl", "genpkey", "-algorithm", "EC",
            "-pkeyopt", "ec_paramgen_curve:secp384r1",
            "-out", wrong_curve_private,
        ])
        run(["openssl", "pkey", "-in", wrong_curve_private, "-pubout", "-out", wrong_curve_public])
        run(
            [sys.executable, TOOLS / "verify-manifest.py", manifest, asset, wrong_curve_public, signature],
            expect_success=False,
        )

    print("Offline release signing tool tests: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
