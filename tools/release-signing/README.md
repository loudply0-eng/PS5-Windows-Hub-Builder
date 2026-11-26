# PS5 Windows Hub — Offline Release Signing

## Production signing key

The repository already contains the production ECDSA P-256 **public** trust anchor in `app/PS5WindowsHub/UpdateTrust.cs`. The matching encrypted private key is kept only on the maintainer's Windows PC:

`%LOCALAPPDATA%\PS5WindowsHub\SigningKeys\ecdsa-p256-private-encrypted.pem`

Do not run `generate-key.sh`, regenerate this key, replace the embedded public key, or upload the private key to GitHub, GitHub Actions, Oracle, this repository, or any artifact. A different key would not match the trust anchor already embedded in the application.

## First bootstrap release

The update-enabled candidate is version `0.1.1`. The first manifest must use:

- `version`: `0.1.1`
- `minimum_supported_version`: `0.1.0`

The minimum must remain `0.1.0` so an existing `0.1.0` preview can accept the first update. Setting it to `0.1.1` would cause the old app to reject the manifest before downloading it.

## Sign with the Windows GUI

Use the standalone `PS5-Windows-Hub-Release-Signer.exe` from the successful Windows workflow artifact. It works offline and has no release-upload function.

1. Select the encrypted key at the path above.
2. Select the exact `PS5-Windows-Hub-0.1.1.zip` built by CI.
3. Set version to `0.1.1` and minimum supported version to `0.1.0`.
4. Enter the key passphrase locally and create the signature files.
5. The signer verifies that the private key matches the embedded P-256 public key, calculates the ZIP size and SHA-256, creates `manifest.json` and `manifest.sig.base64`, and verifies the signature locally.

The passphrase is cleared from the input and its character buffer is zeroed after signing. The GUI does not upload or transmit the private key.

## Publish the first release

Create GitHub Release `v0.1.1` and attach these public files from the matching CI artifact:

- `PS5-Windows-Hub-0.1.1.zip`
- `manifest.json`
- `manifest.sig.base64`
- `AnyPS5-v0.1.5-source.zip`
- `LICENSE-AnyPS5.txt`

The manifest must reference the exact versioned asset URL. Do not edit or reformat `manifest.json` after signing it. Verify that the latest-release URLs serve both manifest files and that the app can fetch and verify them before treating the release as live.

The `0.1.0` preview cannot bootstrap itself because its update feature is disabled. Install the signed `0.1.1` package once. After that first bootstrap installation and release publication, later updates are checked at startup and every six hours while the app is open, downloaded and verified automatically, installed without a confirmation prompt, and followed by an automatic restart. The updater keeps the previous installation for rollback and leaves user data outside the replacement directory.

## Legacy command-line helpers

The scripts in this folder (`create-manifest.py`, `sign-manifest.sh`, and `publish-release.sh`) remain for maintainers who intentionally use an offline shell workflow. They are not needed for the Windows GUI signing path. Never use `generate-key.sh` with the existing production trust anchor.
