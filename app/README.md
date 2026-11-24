# PS5 WINDOWS HUB desktop shell

This is the first Windows GUI shell, built with .NET 8 WinForms.

## Current functions
- Select a local ELF executable.
- Choose a Windows output path.
- Invoke the bundled AnyPS5 relinker with argument-safe process launching (--windows --registry).
- Capture standard output and error in the application log.

The app expects relinker.exe beside PS5-Windows-Hub.exe; the Builder workflow packages both together.

## Not implemented yet
- Signed automatic updates. The update control is intentionally disabled until signed-manifest verification, key provisioning, staging, and rollback are implemented.
- Full AnyPS5 system PRX libraries and complete game runtime.
- Game launching and per-title compatibility.

A successful relink operation is not proof that a game will run. Users must only process software they are authorized to use.

AnyPS5 is GPL-2.0-only. The build artifact includes the corresponding upstream source archive and license.
