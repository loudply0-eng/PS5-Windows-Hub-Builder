# PS5 Windows Hub — Offline Release Signer

This Windows GUI signs update metadata locally. It has no network client and does not upload the private key, passphrase, manifest, signature, or update ZIP.

## What it does

- Opens the encrypted ECDSA P-256 private key selected on this PC.
- Confirms that its public key exactly matches the trust anchor embedded in the Windows application.
- Calculates the update ZIP size and SHA-256.
- Creates the exact `manifest.json` bytes and signs them with ECDSA P-256/SHA-256.
- Verifies the signature locally before writing `manifest.json` and `manifest.sig.base64`.

The passphrase is used only in memory for the signing operation and the password field is cleared afterward. The private key is never copied into the repository or release output.

## First bootstrap release

The first update-enabled application build is planned as version `0.1.1`, with minimum supported version `0.1.1`.

1. Download and extract the `PS5-Windows-Hub-Preview` workflow artifact.
2. Use the ZIP named `PS5-Windows-Hub-0.1.1.zip` in this GUI.
3. Keep version `0.1.1` and minimum supported version `0.1.0` for the first bootstrap release.
4. Select the encrypted key file. The default path is:
   `%LOCALAPPDATA%\PS5WindowsHub\SigningKeys\ecdsa-p256-private-encrypted.pem`
5. Enter the key passphrase locally and select **Create signature files**.
6. Attach the generated `manifest.json` and `manifest.sig.base64` plus the exact ZIP to GitHub Release `v0.1.1`, along with the matching source archive and license from the build artifact.
7. Install the update-enabled `0.1.1` package once.

The existing `0.1.0` preview has automatic updates intentionally disabled, so it cannot bootstrap itself. After the first update-enabled package is installed and its signed release is published, later app updates are checked automatically at startup and every six hours while the app is open. The app verifies the signature and SHA-256, waits for an active conversion to finish, installs without a confirmation prompt, rolls back if the new build fails its health check, and restarts itself.

## Important

- Never share the passphrase or private key with anyone.
- Do not upload the private key to GitHub, GitHub Actions secrets, Oracle, or this repository.
- Never edit or reformat `manifest.json` after it has been signed.
- The release ZIP must be named exactly `PS5-Windows-Hub-{version}.zip`.
- The release tag and asset URL are pinned to this repository and version.
- The signer only prepares the signature files. Publishing the release is a separate maintainer action.
