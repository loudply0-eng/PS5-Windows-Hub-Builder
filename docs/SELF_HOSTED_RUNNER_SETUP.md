# Archived: self-hosted Windows runner setup

> **This guide is obsolete.** The active workflow now uses GitHub-hosted `windows-latest` runners. You do not need to register a self-hosted runner, install a runner service, or configure the `ps5-windows-hub` label.

## Current build process

1. Open **PS5-Windows-Hub-Builder → Actions**.
2. Select **Build PS5 Windows Hub CI (AnyPS5 relinker preview)**.
3. Choose **Run workflow** and select the desired branch.
4. Download the generated artifact from the completed workflow run.

GitHub-hosted Actions usage is subject to the account's included minutes and billing limits. Check the GitHub Actions usage page before repeatedly starting builds.

## Security

- The active workflow uses read-only repository permissions.
- Do not add signing private keys, passphrases, game files, firmware, or console keys to the repository or workflow.
- Do not execute untrusted pull-request code with secrets.
- Keep the Core repository private.
