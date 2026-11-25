# PS5 Windows Hub — Builder

Windows build and packaging repository for **PS5 WINDOWS HUB**.

## Current build

`.github/workflows/build-windows.yml` builds:

- A self-contained .NET 8 WinForms desktop shell for Windows x64.
- The upstream AnyPS5 v0.1.5 **relinker-only** executable, pinned to commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`.
- A separate self-contained updater helper and an offline Windows release-signing GUI.
- Matching upstream source and GPL-2.0-only license notices, plus SHA-256 checksums.

The workflow runs relinker tests, updater security tests, offline signing-tool tests, and updater install/rollback integration tests. The latest checked run (#159) completed successfully.

## Build runner and Actions cost

The Windows workflow now targets the repository-scoped self-hosted Windows x64 runner labelled `ps5-windows-hub`. This avoids GitHub-hosted compute-minute charges. The runner PC supplies the CPU and build environment; GitHub artifact storage is still subject to the account's allowance.

Build artifacts are uploaded only for a manual owner-triggered workflow run and retained for one day. See [the self-hosted runner setup guide](docs/SELF_HOSTED_RUNNER_SETUP.md) before expecting a queued build to start.

## Secure updates — implementation present, public release pending

The WinForms app currently has secure update checks enabled. It contains an ECDSA P-256 public trust anchor and checks signed release metadata after startup and every six hours. The update path verifies the signature, manifest, approved release asset URL, file size, and SHA-256 before staging the update through a separate helper. The helper has install health checks, backup, rollback, and user-data preservation logic.

The offline signer is packaged separately under `ReleaseSigner/`. It uses the existing encrypted private key locally, verifies that its public key matches the app's embedded trust anchor, and creates `manifest.json` plus `manifest.sig.base64`. The private key and passphrase must remain on the maintainer's own device; neither belongs in GitHub or in ChatGPT.

**No official signed GitHub Release has been published yet.** CI success verifies the build and test harness, not the complete live update path. The first stable release must be signed locally and published with the exact versioned update ZIP, `manifest.json`, `manifest.sig.base64`, matching upstream source, and license. A real installed-client update test is still required.

**Distribution constraint:** this Builder repository is currently private. Anonymous installations cannot download its private release assets or metadata. Before public distribution or in-app updates for other users, either this repository must be deliberately made public or the release metadata/assets must be hosted at a separate public endpoint and the app's pinned URLs updated. Do not expose the private Core repository or any private signing material.

The ECDSA signature authenticates update metadata; it is **not** a Windows Authenticode publisher signature. Without an Authenticode certificate, Windows may still display an unknown-publisher/SmartScreen warning.

## Engine limitation

AnyPS5 is a **relinker**, not a complete PS5 emulator or complete game runtime. A successful ELF relink does not mean a PS5 game will run; required system PRX libraries and per-game compatibility are separate work.

## Upstream and license

- Upstream: https://github.com/boykopovar/AnyPS5
- Version: AnyPS5 v0.1.5, pinned commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`
- License: GPL-2.0-only

Every distributed build derived from AnyPS5 must meet the GPL-2.0-only source and notice requirements. The matching upstream source archive and license are packaged with the build.

## Security

- Core remains private; this Builder repository contains no private Core checkout.
- Never commit private signing keys, passphrases, game files, firmware, or console keys.
- Third-party GitHub Actions are pinned to full commit SHAs.
- Workflow repository permissions remain read-only.
- Do not run untrusted pull-request code with secrets.
