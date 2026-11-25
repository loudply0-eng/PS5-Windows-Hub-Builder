# PS5 WINDOWS HUB desktop shell

This is a self-contained Windows x64 GUI shell built with .NET 8 WinForms.

## Current functions

- Select a local ELF executable.
- Choose a Windows output path.
- Invoke the bundled AnyPS5 relinker using argument-safe process launching (`--windows --registry`).
- Capture relinker output and errors in the application log.
- Expose a noninteractive startup health-check mode for the separate updater helper.

The app expects `relinker.exe` and `PS5WindowsHub.Updater.exe` beside `PS5-Windows-Hub.exe`.

## Secure updates

The secure update client is implemented and enabled in the current candidate. It uses the ECDSA P-256 public key in `UpdateTrust.cs`, checks signed metadata after startup and every six hours, validates the manifest and approved GitHub release URL, verifies the downloaded asset's size and SHA-256, and hands installation to the separate updater helper.

The helper stages the update, waits for the app process to exit, performs a health check, retains a backup, and rolls back if validation fails. User data is stored outside the application replacement directory.

A signed public release has **not** been published yet. The release signer must be run locally with the matching encrypted private key; the private key and passphrase must never be uploaded or sent in messages. The app's current release URLs point to this Builder repository, which is private. Public installs and automatic updates will not work anonymously until the assets are moved to a public release endpoint and the app's pinned URLs are updated (or the Builder repository is intentionally made public).

The ECDSA update signature is separate from Windows Authenticode signing. This build is not represented as Authenticode-signed.

## Engine limitations

This package contains the AnyPS5 relinker-only target. It does not include a complete PS5 runtime or all system PRX libraries. A successful relink operation is not proof that a game will run. Users must only process software they are authorized to use.

AnyPS5 is GPL-2.0-only. The Builder workflow packages the matching upstream source archive and license.
