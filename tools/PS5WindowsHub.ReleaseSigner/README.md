# PS5 Windows Hub — One-file Offline Release Signer

The Windows signer is published as one self-contained EXE. The exact version 0.1.1 update ZIP is embedded inside that EXE, so no separate ZIP, manual file selection, or network connection is required.

## Run it

1. Download and extract the GitHub Actions artifact named `PS5-Windows-Hub-OneClick-Signer-0.1.1`.
2. Run `PS5-Windows-Hub-Release-Signer.exe` on the Windows PC that already contains the encrypted signing key.
3. The tool automatically detects:
   - The embedded update package `PS5-Windows-Hub-0.1.1.zip`.
   - Version `0.1.1` and minimum supported version `0.1.0`.
   - The local encrypted key at `%LOCALAPPDATA%\PS5WindowsHub\SigningKeys\ecdsa-p256-private-encrypted.pem`.
   - The output folder `Desktop\PS5WindowsHub-SignedRelease`.
4. Enter only the key passphrase and click **إنشاء ملفات التوقيع**.
5. The tool checks the embedded ZIP contents, verifies that the local key matches the application's trusted public key, hashes the exact embedded ZIP, signs the manifest, verifies the generated signature, saves both files, and opens the output folder.

The output contains exactly the public release metadata files:

- `manifest.json`
- `manifest.sig.base64`

## Security properties

- The encrypted private key never leaves the user's PC.
- The passphrase is used only in memory for the signing operation and is cleared afterward.
- The signer has no network client and does not upload the key, passphrase, manifest, signature, or update ZIP.
- The update ZIP is embedded as a resource in the signer EXE and is read directly for hashing. It is not extracted to a temporary folder.
- The signer checks the ECDSA P-256 key against the public trust anchor embedded in the application.
- The output signature is verified locally before the two output files are written.

## Release publication

The signer prepares the two signed metadata files only. Publishing GitHub Release `v0.1.1` and attaching the exact update ZIP, `manifest.json`, and `manifest.sig.base64` remains a separate release-publishing step. Do not edit or reformat `manifest.json` after signing.

The existing `0.1.0` preview has automatic updates disabled. Version `0.1.1` is the bootstrap build that enables the signed update path after its release assets are published.

Never share the encrypted private key or its passphrase.
