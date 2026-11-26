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

GitHub API inspection on 2026-11-26 reports **both** `PS5-Windows-Hub-Core` and `PS5-Windows-Hub-Builder` as private. The intended arrangement is:

- **Core:** remain private; it contains the proprietary/original project coordination and engine work.
- **Builder:** make public only when ready for public source distribution and public in-app release downloads. The current updater pins release metadata to this repository, so a private Builder repository prevents anonymous clients from fetching updates.

The following were observed in the tracked Builder tree:
- `.github/CODEOWNERS` assigns ownership to `@loudply0-eng`.
- `.github/dependabot.yml` is present.
- The build workflow declares `permissions: contents: read` and pins its third-party Actions to full commit SHAs.
- The tracked tree inspection did not show a private signing-key file. This is not a scan of all Git history or GitHub Actions secrets.

The connected GitHub integration could not read branch-protection settings (403), and GitHub reported that repository rulesets require a Pro plan or a public repository for the current private-repository setup. Secrets and security-alert settings were not exposed by the available integration. **Do not mark branch protection, secret scanning, push protection, or Actions secret hygiene as complete until those settings are checked directly in GitHub.**

For a public Builder repository, configure a ruleset for `main`: require pull requests, require the successful Windows CI check once its exact check name is stable, block force pushes and branch deletion, and restrict who can bypass the ruleset. Keep Actions default permissions read-only, enable secret scanning and push protection where available, enable Dependabot alerts/security updates, and keep signing private keys offline. A single-owner project can use a required pull request without requiring an unavailable second reviewer; the owner must still review their own changes before merging.
