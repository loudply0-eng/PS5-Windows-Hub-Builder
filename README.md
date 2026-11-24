# PS5 Windows Hub — Builder

Public build-workflow repository for PS5 Windows Hub.

## Purpose

This repository will contain reproducible Windows build workflows and release automation. It must not contain private source, credentials, signing keys, game files, firmware, or proprietary assets.

## Source and licensing

The upstream engine is https://github.com/boykopovar/AnyPS5 and is licensed GPL-2.0-only. If a distributed build includes or is derived from that engine, publish and provide the corresponding source and notices as required by GPL-2.0. A public builder repository does not make private GPL-covered source exempt from those obligations.

## Security model

- Workflows should run only on trusted manual dispatches or reviewed changes.
- Never execute untrusted pull-request code with secrets.
- Use a narrowly scoped, read-only token only if a private source checkout is genuinely required.
- Prefer public, license-compliant source inputs for reproducible public builds.
- Pin third-party actions to reviewed commit SHAs.
- Verify downloaded artifacts with SHA-256 and publish checksums.
- Do not store signing credentials in workflow files or repository contents.

## Current status

Scaffolding only. No build workflow or private-repository access token has been configured yet.