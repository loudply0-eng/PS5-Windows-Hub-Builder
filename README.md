# PS5 Windows Hub — Builder

Public, owner-triggered Windows CI for PS5 Windows Hub.

## Current workflow

`.github/workflows/build-windows.yml` builds the upstream AnyPS5 **rel linker-only** target for Windows from the fixed upstream release v0.1.5.

- Trigger: manual `workflow_dispatch`
- Runner: GitHub-hosted Windows runner
- Toolchain: MSYS2 UCRT64 / MinGW-w64
- Upstream source: public AnyPS5 tag v0.1.5, pinned to commit `62bd7dd61e2b8a7a59262c4c145edff549bae5b9`
- Tests: CTest
- Output: relinker executable, corresponding upstream source archive, license and SHA-256 checksums
- Artifact retention: 14 days

Only the GitHub account `loudply0-eng` can start this workflow in this repository. It has read-only repository permissions and uses no secrets or private-repository token.

## Important limitation

This is an initial toolchain and relinker test build. It is **not** the complete AnyPS5 Windows runtime and does not mean PS5 games are broadly playable. The full Windows system-library build and per-game compatibility testing are separate milestones.

## Upstream and license

- https://github.com/boykopovar/AnyPS5
- Upstream license: GPL-2.0-only

Every distributed build derived from AnyPS5 must meet the GPL-2.0-only source and notice requirements. The workflow includes the matching upstream source archive and license with the build artifact.

## Security

- No private Core checkout.
- No secrets, signing keys, game files, firmware or console keys.
- Third-party actions are pinned to full commit SHAs.
- Do not add pull-request workflows that execute untrusted code with secrets.
