# PS5 Windows Hub — Builder

Windows build and packaging repository for **PS5 WINDOWS HUB**.

## Current build

`.github/workflows/build-windows.yml` builds:

- A self-contained .NET 8 WinForms desktop shell for Windows x64.
- The upstream AnyPS5 v0.1.5 **relinker-only** executable, pinned to commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`.
- A separate self-contained updater helper and an offline Windows release-signing GUI.
- Matching AnyPS5 source and GPL-2.0-only license notices, plus SHA-256 checksums.

The desktop shell now includes a **KytyPS5 launch tab**. The current preview still asks the user to select an engine folder, but this is only an intermediate development state. The required release experience is one PS5 Windows Hub installer containing a pinned KytyPS5 Windows runtime under `Engine/KytyPS5/`; users must not need to download or install Kyty separately. Bundled-runtime packaging and its corresponding-source delivery are not implemented or verified yet. Any distributed KytyPS5 runtime must comply with GPL-2.0-only, including notices and complete corresponding-source availability. The AnyPS5 relinker remains a separate experimental utility and is not a complete PS5 emulator.

The workflow runs relinker tests, updater security tests, offline signing-tool tests, and updater install/rollback integration tests. It does **not** yet run a dedicated Kyty process-launch validation test. The GitHub-hosted Windows PR run [45804637894](https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/actions/runs/45804637894) completed successfully on commit `3b082f52c7717693ea5abec37b154e5aaedb9a09`. This verifies the listed CI build and test suite; it does not prove that a real signed release can update an installed user's PC or that a PS5 game is playable.

## Build runner and Actions cost

The Windows workflow uses GitHub-hosted `windows-latest` runners. GitHub-hosted Actions usage is subject to the account's included minutes and billing limits.

Build artifacts are uploaded only for a manual owner-triggered workflow run and retained for one day. Use the **Actions** tab to start a manual build when a fresh artifact is needed.

## Secure updates — implementation present, public release pending

The WinForms app currently has secure update checks enabled. It contains an ECDSA P-256 public trust anchor and checks signed release metadata after startup and every six hours. The update path verifies the signature, manifest, approved release asset URL, file size, and SHA-256 before staging the update through a separate helper. The helper has install health checks, backup, rollback, and user-data preservation logic.

The offline signer is packaged separately under `ReleaseSigner/`. It uses the encrypted private key locally, verifies that its public key matches the app's embedded trust anchor, and creates `manifest.json` plus `manifest.sig.base64`. The private key and passphrase must remain on the maintainer's own device; neither belongs in GitHub or in ChatGPT.

**No official signed GitHub Release has been published yet.** CI success verifies the build and test harness, not the complete live update path. The first stable release must be signed locally and published with the exact versioned update ZIP, `manifest.json`, `manifest.sig.base64`, matching AnyPS5 source, and license. A real installed-client update test is still required.

**Distribution status:** this Builder repository is public, and the app's metadata URLs point to its GitHub Releases. No signed release is published yet, so public clients cannot receive a real automatic update until the first signed release is created and tested. Keep the Core repository private and never expose private signing material.

The ECDSA update signature authenticates update metadata; it is **not** a Windows Authenticode publisher signature. Without an Authenticode certificate, Windows may still display an unknown-publisher/SmartScreen warning.

## Engine and licensing boundaries

- **KytyPS5:** https://github.com/KytyPS5/KytyPS5 — GPL-2.0-only. The Hub launches a separately installed, unmodified upstream executable. Do not copy, link, patch, or redistribute KytyPS5 through the Hub without a separate GPL compliance review and the exact corresponding source/notices.
- **AnyPS5:** https://github.com/boykopovar/AnyPS5 — v0.1.5 at commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`, GPL-2.0-only. This is a relinker-only utility, not a complete PS5 runtime. Its matching source and license are packaged with the build.

A successful relink or successful process launch does not prove a game is playable. Compatibility must be tested per title and per engine build.

## Security

- Core remains private; this Builder repository contains no private Core checkout.
- Never commit private signing keys, passphrases, game files, firmware, or console keys.
- Third-party GitHub Actions are pinned to full commit SHAs.
- Workflow repository permissions remain read-only.
- Do not expose secrets to pull-request workflows. The current workflow is restricted to owner-created same-repository pull requests and uses read-only repository permissions.
