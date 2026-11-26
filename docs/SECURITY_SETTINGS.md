# Public Builder repository hardening checklist

The Builder is public to use GitHub-hosted public-repository CI. It must never contain private Core source, secrets, signing keys, proprietary game data, firmware, or console keys.

## Required repository settings
- Keep Actions permissions set to read-only by default.
- Do not expose secrets to pull-request workflows.
- Enable branch protection for `main`: require pull requests before merging, block force pushes, and block branch deletion.
- Do not require an approval from another reviewer when this is a single-owner project; that would block the owner's normal workflow.
- Require the Windows build/test status check only after the workflow runs on owner-created pull requests and the exact check name is visible in GitHub.
- Enable secret scanning and push protection if offered for this public repository.
- Enable Dependabot security updates and alerts where available.

## Workflow rules
- Keep third-party Actions pinned to full commit SHAs.
- Keep `GITHUB_TOKEN` permissions minimal.
- Do not use `pull_request_target` to check out or execute untrusted pull-request code.
- Do not add secrets to this public builder. Signing must remain separate from compilation.
- Review every dependency update before merging.

## Verification
Settings are account/repository controls and cannot be enabled by adding files. Verify them in GitHub Settings after applying the checklist.


## Verification addendum — 2026-11-26

## Observed state and required target

GitHub repository metadata inspection on 2026-11-26 confirms `PS5-Windows-Hub-Builder` is **public** and `PS5-Windows-Hub-Core` is **private**. This is the intended visibility arrangement:

- **Core:** remain private; it contains the proprietary/original project coordination and engine work.
- **Builder:** keep public for source distribution and public in-app release downloads. The updater pins metadata and assets to this repository's GitHub Releases.

The following were observed in the tracked Builder tree:
- `.github/CODEOWNERS` assigns ownership to `@loudply0-eng`.
- `.github/dependabot.yml` is present.
- The build workflow declares `permissions: contents: read` and pins its third-party Actions to full commit SHAs.
- The tracked tree inspection did not show a private signing-key file. This is not a scan of all Git history or GitHub Actions secrets.

The connected GitHub integration could not re-read branch-protection settings or secret-scanning configuration during this inspection. **Do not mark secret scanning, push protection, or Actions secret hygiene as complete without checking those settings directly in GitHub.** The current repository is public, so public-repository ruleset options may differ from the earlier private-repository limitation.

For the public Builder repository, configure a ruleset for `main`: require pull requests, require the successful Windows CI check once its exact check name is stable, block force pushes and branch deletion, and restrict who can bypass the ruleset. Keep Actions default permissions read-only, enable secret scanning and push protection where available, enable Dependabot alerts/security updates, and keep signing private keys offline. A single-owner project can require pull requests without requiring an unavailable second reviewer.
