# PS5 WINDOWS HUB — Secure Update Design

Status: the signed-update client, downloader, safe extractor, and separate updater helper are implemented and wired into the application. The first public signed bootstrap release, v0.1.1, was published on 2026-11-27. Its ECDSA P-256 signature and exact update ZIP size/SHA-256 were verified by the release workflow against the application's embedded trust anchor. A real installed-client update/rollback test on an end-user Windows installation is still outstanding.

## Trust model

- Publish versioned releases from the public Builder repository.
- Embed only an ECDSA P-256 public verification key in the application.
- Keep the matching private signing key offline on the maintainer's device. Never store it in GitHub, GitHub Actions, Oracle, or this repository.
- Sign the exact UTF-8 bytes of the release manifest using ECDSA P-256 with SHA-256. The verifier accepts the standard DER signature format emitted by OpenSSL. Distribute a detached base64 signature beside the manifest. The app embeds a P-256 public trust anchor. The matching encrypted private key must remain offline; its availability and successful local use by the maintainer still need to be verified. Signing tools are in `tools/release-signing/`.
- The signed manifest must contain version, minimum supported version, exact asset filename, byte length, SHA-256 digest, release URL, and release notes.
- Reject invalid signatures, malformed or oversized manifests, unexpected asset names, mismatched lengths or digests, downgrade attempts, and unsupported versions.

## Installation transaction

1. Download the manifest and detached signature over HTTPS.
2. Verify the signature with the public key embedded in the installed application before trusting manifest fields.
3. Download only the exact asset named by the verified manifest into a unique staging directory.
4. Verify exact byte length and SHA-256 before extraction.
5. Extract with path-traversal protections; reject rooted paths, parent-directory traversal, links, duplicate paths, and files outside staging.
6. Launch a separately packaged updater helper from the currently installed, trusted application.
7. The helper waits for the application to exit, backs up current application files, installs verified staged files, and starts a health-check launch.
8. If the new application fails its health check within a defined timeout, restore the backup and relaunch the previous version.
9. Keep user data, settings, and game-library paths outside the replaceable application directory.

## Release pipeline

- Build a self-contained Windows x64 application and updater helper.
- Produce a release asset and SHA-256 digest.
- Generate the manifest and detached signature only on the maintainer's offline signing device.
- Upload the signed manifest, signature, and release asset to GitHub Releases.
- Never put private signing material in workflow secrets or build logs.

## Required tests before enabling updates

- Accept valid signatures; reject altered manifests and signatures made by another key.
- Reject truncated, oversized, malformed, and unsupported manifests.
- Reject wrong asset names, lengths, digests, and downgrade attempts.
- Reject archive traversal and link entries.
- Confirm interrupted downloads leave the installed version untouched.
- Confirm failed replacement or failed health checks restore the previous version.
- Confirm successful updates preserve user settings and data.
- Test first install and upgrades on clean Windows x64 systems.

## Current implementation status

The WinForms application exposes a wired update-check button and performs an automatic check shortly after startup, then every six hours while the app remains open. The client verifies the signed manifest with the embedded P-256 public key, enforces strict manifest/version/asset constraints, downloads only from approved HTTPS redirect hosts, verifies exact size and SHA-256, safely extracts the ZIP, and starts the separate updater helper. The helper waits for the app to exit, replaces the installation, runs a startup health check, retains the previous installation, and rolls back if the new build fails. CI covers signature/manifest validation, downloader redirects and interrupted transfers, safe ZIP extraction, successful updater replacement, rollback, and preservation of user data outside the installation directory.

The first signed bootstrap release, **v0.1.1**, is now public: https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/tag/v0.1.1. It contains `PS5-Windows-Hub-0.1.1.zip` (170,217,925 bytes; SHA-256 `74f38e5991bdb1e45d22896487bf2f2970da79c497f29d1e7fd5060b5ffff9c0`), `manifest.json`, `manifest.sig.base64`, the standalone installer, checksums, and the corresponding upstream source archives and licenses. The manifest and signature are also checked in under `release/v0.1.1/`. Release workflow run 46061296869 completed successfully: it downloaded the exact CI artifact that had been signed, verified the manifest signature against the application's embedded P-256 trust anchor, matched the ZIP byte length and SHA-256, and published the release assets.

The existing 0.1.0 preview has automatic updates intentionally disabled. It cannot bootstrap itself. Users on 0.1.0 must install `PS5-Windows-Hub-Setup-0.1.1.exe` once manually. After 0.1.1 is installed, the client can retrieve the public `releases/latest/download/manifest.json` and detached signature endpoints; later signed releases can then be applied automatically. A real installed-client check and a subsequent version-to-version update/rollback on an end-user Windows installation have not yet been demonstrated.

Still outstanding: install the 0.1.1 bootstrap installer on a clean Windows x64 machine, confirm the installed client retrieves and accepts the published `latest` metadata, then exercise a later signed update (for example 0.1.1 → 0.1.2) and rollback on a real installation. The encrypted private key was successfully used locally to produce the v0.1.1 signature and remains offline. Windows Authenticode signing is not implemented; the ECDSA signature authenticates update metadata but does not suppress SmartScreen/unknown-publisher warnings.

## Verification addendum — 2026-11-26

Observed repository state on 2026-11-26 (GitHub API inspection):

- Repository metadata confirms the Builder repository is **public** and the Core repository remains **private**, matching the intended distribution arrangement.
- The application source sets `EnableSecureUpdates = true`, embeds a non-empty ECDSA P-256 public key, starts an update check shortly after launch, and schedules checks every six hours. The offline signer source contains the corresponding expected public key and fingerprint; the maintainer's actual local private-key file has not been validated in this inspection.
- The update client pins manifest and signature URLs to `github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/`. The Builder repository is public, and `v0.1.1` is now the latest published release.
- The v0.1.1 release was verified in GitHub Actions run 46061296869. The exact previously signed CI payload was used; the manifest signature, embedded public-key fingerprint, file size, and SHA-256 passed before publication. This verifies the release assets and metadata, not yet a real installed-client update on an end-user Windows installation.
- The inspected tracked file tree did not show a private signing-key file. This does not prove that local key material exists or that GitHub Actions secrets, repository history, or account security settings are clean; those require separate checks.

Therefore the first signed-release gate is complete, but **end-user automatic updating is not yet fully verified** until the 0.1.1 bootstrap installer is installed and a later version-to-version update/rollback is exercised on a real Windows installation. Keep Core private. Never put the private signing key or passphrase in GitHub, Actions secrets, Oracle, or ChatGPT.
