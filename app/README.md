# PS5 WINDOWS HUB desktop shell

This is a self-contained Windows x64 GUI shell built with .NET 8 WinForms.

## Current functions

- Select a local KytyPS5 installation directory and verify that `kyty_emulator.exe` exists.
- Select a local PS5 game directory containing `eboot.bin`, or an `eboot.bin`, ELF, or supported ZArchive (`.zar`) file.
- Launch the separately installed, unmodified KytyPS5 engine as a child process using argument-safe process creation (`--game <path>`).
- Report process start/exit state and keep the upstream engine responsible for its own graphics, game runtime, and logs.
- Open the official KytyPS5 releases page from the Hub.
- Keep the AnyPS5 relinker in a separate experimental tab. It only relinks ELF files and is not represented as a complete PS5 emulator.
- Expose a noninteractive startup health-check mode for the separate updater helper.

KytyPS5 is not bundled, modified, linked, or copied into the Hub package. The current integration preview still expects the user to select a separately installed engine folder. This is an intermediate development state, not the intended end-user experience. The required release design is a single PS5 Windows Hub installer containing a pinned KytyPS5 runtime under `Engine/KytyPS5/`, so users install only the Hub. That bundled-runtime packaging step is not implemented or verified yet. Any distributed KytyPS5 runtime must include GPL-2.0-only notices and complete corresponding-source access.

## Secure updates

The secure update client is implemented and enabled in the current candidate. It uses the ECDSA P-256 public key in `UpdateTrust.cs`, checks signed metadata after startup and every six hours, validates the manifest and approved release URL, verifies the downloaded asset's size and SHA-256, and hands installation to the separate updater helper.

The helper stages the update, waits for the app process to exit, performs a health check, retains a backup, and rolls back if validation fails. User data is stored outside the application replacement directory.

A signed public release has **not** been published yet. The release signer must be run locally with the matching encrypted private key; the private key and passphrase must never be uploaded or sent in messages. The app's current release URLs point to this Builder repository, which is private. The Builder repository is public and the app's pinned metadata URLs target its GitHub Releases. However, no signed release or manifest has been published yet, so the updater cannot complete a real update until the first signed release is created and tested. The Core repository remains private.

The ECDSA update signature is separate from Windows Authenticode signing. This build is not represented as Authenticode-signed.

## Engine and licensing boundary

KytyPS5 is GPL-2.0-only. The Hub launches the independently installed upstream executable as a separate process and does not incorporate KytyPS5 source code. Do not merge, statically link, patch, or redistribute KytyPS5 through the Hub without first reviewing GPL-2.0-only obligations and the exact upstream notices/source for the selected revision. A separate-process design is an engineering boundary, not a legal determination for every possible distribution arrangement.

The AnyPS5 relinker package is also GPL-2.0-only and is distributed with its matching source and license. A successful relink does not mean a PS5 game will run on Windows.

## Safety and privacy

- The Hub does not include game files, firmware, console keys, or decrypted system software.
- Game files remain on the user's local storage.
- The Hub does not attempt to decrypt game content or extract console keys.
