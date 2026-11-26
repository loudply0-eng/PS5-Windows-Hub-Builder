# PS5 WINDOWS HUB — Secure Update Design

Status: partial implementation. This document does not claim that automatic updates are complete or enabled.

## Trust model

- Publish versioned releases from the public Builder repository.
- Embed only an ECDSA P-256 public verification key in the application.
- Keep the matching private signing key offline on the maintainer's device. Never store it in GitHub, GitHub Actions, Oracle, or this repository.
- Sign the exact UTF-8 bytes of the release manifest using ECDSA P-256 with SHA-256. The verifier accepts the standard DER signature format emitted by OpenSSL. Distribute a detached base64 signature beside the manifest. Offline key-generation and signing scripts are in `tools/release-signing/`; no production key has been generated or embedded.
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

The WinForms preview still has a disabled update button. The repository now contains a partial signed-manifest verifier (P-256 key enforcement, exact-byte signature verification, strict JSON property checks, version downgrade checks), a downloader that validates every HTTPS redirect against an explicit GitHub asset-host allow-list and checks exact length and SHA-256, and a bounded ZIP extractor that rejects traversal, rooted paths, duplicate/case-colliding paths, Windows device/invalid filenames, file/directory collisions, links, special files, excessive entry counts, and expanded payloads over 1 GiB. A separate updater helper now stages a replacement beside the installation, waits for the app to exit, swaps directories, runs a noninteractive startup health check, and restores the previous directory if the check fails. The app now has a wired update coordinator: it fetches the pinned latest-release manifest and detached signature, verifies the exact manifest bytes, downloads and verifies the asset, safely extracts it, and hands it to the external updater helper. The UI handler is connected but remains compile-time disabled because the production public key and signed release assets do not exist yet. A Windows CI security-test harness covers P-256 signature verification, strict manifest validation, pinned metadata endpoints, downloader redirects/hash verification/interrupted transfers, and safe ZIP extraction. The workflow also includes integration tests for mutex coordination, a successful directory swap, and a failed health check that must restore the previous application; the latest expanded test suite still needs a successful CI run.

Still not implemented: production key generation and secure backup by the maintainer, embedding the production public key, Authenticode publisher verification, persistent user-data preservation tests, and clean-install/upgrade end-to-end tests against a real signed GitHub Release. Offline manifest generation, detached-signature verification, and public release publishing scripts are now included. The downloader itself only stages a verified ZIP; the coordinator then extracts it and launches the separate updater helper. The feature remains disabled until the maintainer provisions the production public key, publishes a signed release, and the remaining end-to-end tests pass.

## Verification addendum — 2026-11-26

Observed repository state on 2026-11-26 (GitHub API inspection):

- The Builder repository is currently **private**, and the Core repository is also private. This differs from the intended distribution arrangement described in the README (public Builder, private Core).
- The application source at the inspected Builder base commit sets `EnableSecureUpdates = true`, embeds a non-empty ECDSA P-256 public key, starts an update check shortly after launch, and schedules checks every six hours. The offline signer source contains the same expected public key and fingerprint.
- The update client pins manifest and signature URLs to `github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/latest/download/`. Because Builder is private, unauthenticated installations cannot retrieve those release assets. A public release endpoint is a prerequisite for general in-app updates.
- No signed GitHub Release or successful end-to-end installed-client update was verified during this inspection. The Windows CI job is currently failing; job logs were not retrievable, so the failure cause remains undiagnosed.
- The inspected tracked file tree did not show a private signing-key file. This does not prove that local key material exists or that GitHub Actions secrets, repository history, or account security settings are clean; those require separate checks.

Therefore the updater implementation exists, but **automatic updating is not yet verified as operational for users**. Before enabling public distribution, the maintainer must make the Builder release endpoint publicly readable (by making Builder public or using a separate public HTTPS release host), create a signed versioned release locally with the matching offline private key, attach the exact update ZIP plus manifest and detached signature, include corresponding GPL source/notices, and complete clean-install plus upgrade/rollback tests. Keep Core private. Never put the private signing key or passphrase in GitHub, Actions secrets, Oracle, or ChatGPT.
