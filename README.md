# PS5 Windows Hub — Builder

Public Windows CI and packaging repository for **PS5 WINDOWS HUB**.

## Current workflow

`.github/workflows/build-windows.yml` builds the upstream AnyPS5 **relinker-only** target for Windows and publishes the self-contained .NET 8 WinForms shell.

- Triggers: manual `workflow_dispatch` and owner-authored pull requests targeting `main`
- Runner: GitHub-hosted Windows runner
- Toolchain: MSYS2 UCRT64 / MinGW-w64
- Upstream source: AnyPS5 v0.1.5 pinned to commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`
- Tests: upstream relinker CTest, updater manifest/archive security harness, and updater directory-swap/rollback integration tests
- Package: self-contained Windows x64 app, relinker, separate updater helper, versioned update-payload ZIP, matching upstream source and license, SHA-256 checksums
- Artifact retention: 14 days; artifact upload and packaging are limited to manual workflow dispatch

Only the GitHub account `loudply0-eng` can run the job. The workflow has read-only repository permissions and uses no secrets or private-repository token.

## Important limitation

The updater is **not enabled yet**. The manifest verifier, verified downloader, bounded ZIP extractor, external updater helper, rollback path, and offline signing scripts are partial foundations. There is no production signing key or embedded public key, no in-app manifest/signature retrieval flow, and no official signed release. The update button remains disabled until the complete flow and all tests pass.

AnyPS5 is a **relinker**, not a complete PS5 emulator or complete game runtime. A successful relink does not mean a PS5 game will run; required system PRX libraries and per-game compatibility are separate work.

## Upstream and license

- https://github.com/boykopovar/AnyPS5
- Upstream license: GPL-2.0-only

Every distributed build derived from AnyPS5 must meet the GPL-2.0-only source and notice requirements. Matching upstream source and license files are packaged with the build.

## Security

- Core repository remains private; no private Core checkout is used here.
- No private signing keys, game files, firmware or console keys belong in this repository.
- Third-party actions are pinned to full commit SHAs.
- Actions permissions remain read-only.
- Do not add pull-request workflows that execute untrusted code with secrets.
