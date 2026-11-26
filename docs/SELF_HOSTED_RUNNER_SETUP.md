# PS5 Windows Hub — self-hosted Windows build runner

The Builder workflow uses a repository-scoped self-hosted Windows x64 runner. This avoids consuming GitHub-hosted Windows compute minutes; build CPU time and disk usage are provided by the runner PC. GitHub artifact storage is still subject to the account's storage allowance.

## Runner target

The workflow requires all of these labels:

- `self-hosted`
- `Windows`
- `X64`
- `ps5-windows-hub`

Register the runner from **Builder → Settings → Actions → Runners → New self-hosted runner → Windows → x64**. Use the current download and configuration commands displayed by GitHub. Runner registration tokens are temporary; generate one immediately before setup and never commit or send it in chat.

Run the runner as a Windows service so it returns after a reboot. Give the runner folder a dedicated path such as `C:\actions-runner`. Do not install it in the application or source checkout directory.

## Required PC software

- Windows 10/11 x64.
- .NET 8 SDK x64 (not only the runtime).
- A working internet connection.
- Enough free disk space for MSYS2, build outputs, and temporary archives (10 GB or more recommended).

The workflow installs its pinned MSYS2 UCRT64 toolchain automatically. GitHub Actions checks out the source and runs the build only when the repository owner `loudply0-eng` triggers the workflow or updates the owner-controlled pull request.

## Safety and billing

- The runner is registered to this repository only.
- The workflow runs with read-only repository permissions and contains no secrets.
- Do not add untrusted fork pull-request execution to this workflow. Self-hosted runners can expose the PC to code executed by workflows.
- Standard GitHub-hosted compute charges do not apply to self-hosted runner execution. Artifact storage remains subject to GitHub's storage limits.
- Build artifacts are uploaded only for an owner-triggered `workflow_dispatch` run and are retained for one day to control storage use.

## Running a build

After the runner shows **Idle** in the repository's Actions runner settings, open **Actions → Build AnyPS5 Windows relinker → Run workflow**, select the feature branch when prompted, and start the workflow. Pull-request validation runs automatically when the owner pushes commits to the PR branch.

If no runner is online, the job remains queued. Do not switch back to `windows-latest` when the included Actions compute allowance has been exhausted.
