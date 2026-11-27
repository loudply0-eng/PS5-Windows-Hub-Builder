# PS5 Windows Hub — Builder

Windows build and packaging repository for **PS5 WINDOWS HUB**.

## Current build

`.github/workflows/build-windows.yml` builds and packages:

- A self-contained .NET 8 WinForms desktop shell for Windows x64.
- The official, unmodified KytyPS5 Windows x64 runtime from release `KytyPS5-2026-11-26-61db2f8`, pinned to source commit `61db2f8874d4a94fd2908a87cefa973ddc48ef6e` and verified against the upstream Windows archive SHA-256.
- The complete recursively checked-out KytyPS5 corresponding-source tree, including pinned submodules, plus its GPL-2.0-only license and notices.
- The upstream AnyPS5 v0.1.5 **relinker-only** executable, pinned to commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`.
- A separate self-contained updater helper and an offline Windows release-signing GUI.
- A versioned x64 Inno Setup installer, compiled with a pinned and Authenticode-verified Inno Setup compiler; CI installs it over a pre-existing test directory and verifies startup health, bundled-engine presence, settings preservation and safe uninstall.
- Matching AnyPS5 source and GPL-2.0-only license notices, plus SHA-256 checksums.

The installed Hub automatically detects the bundled KytyPS5 runtime at `Engine/KytyPS5/`. A user may still select a different engine directory manually. The AnyPS5 relinker remains a separate experimental utility; ELF relinking does not make a PS5 game playable on Windows.

The workflow runs relinker tests, updater security tests (including bundled-engine path resolution and Kyty game-input validation), offline signing-tool tests, and updater install/rollback integration tests. CI verifies the Hub installer and packaged engine files, but does not boot a real PS5 game. KytyPS5 is an actively developed emulator/translation layer with limited and changing game compatibility; a successful installer or process launch does not prove that a specific game is playable.

## Build runner and Actions cost

The Windows workflow uses GitHub-hosted `windows-latest` runners. GitHub-hosted Actions usage is subject to the account's included minutes and billing limits.

Build artifacts are uploaded for owner-triggered pushes to `feat/persistent-app-settings-20261126` and manual owner-triggered workflow runs, retained for seven days, and named as CI previews. They are not signed public Releases; signing and an installed-client update test remain separate release gates.

## Secure updates — implementation present, public release pending

The WinForms app contains an ECDSA P-256 public trust anchor and checks signed release metadata after startup and every six hours. The update path verifies the signature, manifest, approved release asset URL, file size, and SHA-256 before staging the update through a separate helper. The helper has install health checks, backup, rollback, and user-data preservation logic.

The offline signer is packaged separately under `ReleaseSigner/`. It uses the encrypted private key locally, verifies that its public key matches the app's embedded trust anchor, and creates `manifest.json` plus `manifest.sig.base64`. The private key and passphrase must remain on the maintainer's own device; neither belongs in GitHub or in ChatGPT.

**No official signed GitHub Release has been published yet.** CI now produces a tested Setup.exe and an update ZIP containing the Hub, pinned KytyPS5 runtime and relinker. The first live in-app update still requires signing the exact update ZIP and manifest offline with the matching private key, publishing `manifest.json` and `manifest.sig.base64`, and testing an installed-client upgrade. A real installed-client update test is still required.

**Distribution status:** this Builder repository is public, and the app's metadata URLs point to its GitHub Releases. No signed release is published yet, so installed clients cannot receive an automatic update until the first signed release is created and tested. Keep the Core repository private and never expose private signing material.

The ECDSA update signature authenticates update metadata; it is **not** a Windows Authenticode publisher signature. Without an Authenticode certificate, Windows may still display an unknown-publisher/SmartScreen warning.

## Engine and licensing boundaries

- **KytyPS5:** https://github.com/KytyPS5/KytyPS5 — GPL-2.0-only. This build redistributes the unmodified Windows x64 binary from upstream release `KytyPS5-2026-11-26-61db2f8`. The installer includes the matching recursively checked-out source tree at commit `61db2f8874d4a94fd2908a87cefa973ddc48ef6e`, its pinned submodules, `LICENSE`, and `LICENSES` notices. The emulator is early-stage; compatibility varies by title and release.
- **AnyPS5:** https://github.com/boykopovar/AnyPS5 — v0.1.5 at commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`, GPL-2.0-only. This is a relinker-only utility, not a complete PS5 runtime. Its matching source and license are packaged with the build.

The Hub does not distribute game files, PS5 firmware, console keys, or decryption keys. Use only game content and system software you are legally entitled to use.

## Security

- Core remains private; this Builder repository contains no private Core checkout.
- Never commit private signing keys, passphrases, game files, firmware, or console keys.
- Third-party GitHub Actions are pinned to full commit SHAs.
- Workflow repository permissions remain read-only.
- Do not expose secrets to pull-request workflows. The current workflow is restricted to owner-created same-repository pull requests and uses read-only repository permissions.
