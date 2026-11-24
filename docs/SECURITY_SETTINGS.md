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
