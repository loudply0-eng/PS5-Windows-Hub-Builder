# PS5 WINDOWS HUB — Secure Update Design

Status: partial implementation. This document does not claim that automatic updates are complete or enabled.

## Trust model

- Publish versioned releases from the public Builder repository.
- Embed only an ECDSA P-256 public verification key in the application.
- Keep the matching private signing key offline on the maintainer's device. Never store it in GitHub, GitHub Actions, Oracle, or this repository.
- Sign the exact UTF-8 bytes of the release manifest using ECDSA P-256 with SHA-256. Distribute a detached signature beside the manifest.
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

The WinForms preview still has a disabled update button. The repository now contains a partial signed-manifest verifier (P-256 key enforcement, exact-byte signature verification, strict JSON property checks, version downgrade checks), a downloader that validates every HTTPS redirect against an explicit GitHub asset-host allow-list and checks exact length and SHA-256, and a bounded ZIP extractor that rejects traversal, rooted paths, duplicate/case-colliding paths, file/directory collisions, links, special files, excessive entry counts, and excessive expanded sizes. A Windows CI security-test harness covers the verifier and archive extraction. These changes are committed but the newest CI run has not yet completed.

Still not implemented: signed manifest/release publication, offline key provisioning procedure, Authenticode publisher verification, separate updater helper, transactional installation, health checks, rollback, persistent user-data migration, and end-to-end Windows install/update tests. The current downloader only stages a verified ZIP; it does not install or execute it. Do not enable the button or advertise automatic updates until all remaining components and required tests pass.