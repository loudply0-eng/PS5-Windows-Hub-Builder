# PS5 WINDOWS HUB — Secure Update Design

Status: the signed-update client, downloader, safe extractor, and separate updater helper are implemented and wired into the application. A real public signed release and an installed-client end-to-end update test are still outstanding, so operational automatic updating is not yet verified.

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

The WinForms preview exposes a wired update-check button and performs an automatic check shortly after startup, then every six hours while the app remains open. The client verifies the signed manifest with the embedded P-256 public key, enforces strict manifest/version/asset constraints, downloads only from approved HTTPS redirect hosts, verifies exact size and SHA-256, safely extracts the ZIP, and starts the separate updater helper. The helper waits for the app to exit, replaces the installation, runs a startup health check, retains the previous installation, and rolls back if the new build fails. CI covers signature/manifest validation, downloader redirects and interrupted transfers, safe ZIP extraction, successful updater replacement, rollback, and preservation of user data outside the installation directory. The GitHub-hosted Windows run 45796580738 passed. No signed GitHub Release, manifest, or detached signature has been published, and no real installed-client update has been demonstrated; therefore the feature is implemented but not yet proven operational in production.

Still outstanding: the maintainer must verify the matching encrypted private key locally, generate and sign the first release manifest offline, publish the exact ZIP plus `manifest.json` and `manifest.sig.base64` to a public GitHub Release, and test a real installed-client upgrade and rollback. Windows Authenticode signing is not implemented; the ECDSA signature authenticates update metadata but does not suppress SmartScreen/unknown-publisher warnings. The updater source is enabled, but without a published signed release it cannot yet complete an update.

## Verification addendum — 2026-11-26

Observed repository state on 2026-11-26 (GitHub API inspection):

- Repository metadata confirms the Builder repository is **public** and the Core repository remains **private**, matching the intended distribution arrangement.
- The application source sets `EnableSecureUpdates = true`, embeds a non-empty ECDSA P-256 public key, starts an update check shortly after launch, and schedules checks every six hours. The offline signer source contains the corresponding expected public key and fingerprint; the maintainer's actual local private-key file has not been validated in this inspection.
- The update client pins manifest and signature URLs to `github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/`. Because Builder is private, unauthenticated installations cannot retrieve those release assets. A public release endpoint is a prerequisite for general in-app updates.
- No signed GitHub Release or successful end-to-end installed-client update was verified during this inspection. The GitHub-hosted Windows CI run 45796580738 completed successfully. It validates the automated build/test suite, not a real signed-release update on an end-user installation.
- The inspected tracked file tree did not show a private signing-key file. This does not prove that local key material exists or that GitHub Actions secrets, repository history, or account security settings are clean; those require separate checks.

Therefore the updater implementation exists, but **automatic updating is not yet verified as operational for users**. The Builder release endpoint is already public; the remaining release gate is to create a signed versioned release locally with the matching offline private key, attach the exact update ZIP plus manifest and detached signature, include corresponding GPL source/notices, and complete clean-install plus upgrade/rollback tests. Keep Core private. Never put the private signing key or passphrase in GitHub, Actions secrets, Oracle, or ChatGPT.
