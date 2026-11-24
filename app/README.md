# PS5 WINDOWS HUB desktop shell

This is the Windows GUI shell, built with .NET 8 WinForms and published as a self-contained Windows x64 application.

## Current functions
- Select a local ELF executable.
- Choose a Windows output path.
- Invoke the bundled AnyPS5 relinker with argument-safe process launching (`--windows --registry`).
- Capture standard output and error in the application log.
- Expose a noninteractive startup health-check mode for the separate updater helper.

The app expects `relinker.exe` and the standalone `PS5WindowsHub.Updater.exe` beside `PS5-Windows-Hub.exe`.

## Secure updates

The update control is intentionally disabled. Partial foundations exist for signed-manifest verification, HTTPS asset download, bounded ZIP extraction, and a separate transactional updater with backup and rollback. The app does not yet retrieve or verify a production release manifest, has no embedded production public key, and does not run an end-to-end update flow. Do not enable or advertise automatic updates until all required tests pass.

## Engine limitations

This package contains the AnyPS5 relinker-only target. It does not include a complete PS5 runtime or all system PRX libraries. A successful relink operation is not proof that a game will run. Users must only process software they are authorized to use.

AnyPS5 is GPL-2.0-only. The Builder workflow packages the matching upstream source archive and license.
