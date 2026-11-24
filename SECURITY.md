# Security Policy

## Supported versions
Security fixes are applied to the current default branch and the latest published release, if any.

## Reporting a vulnerability
Please do not publish exploit details in public issues. Use GitHub's private vulnerability reporting or contact the repository owner through GitHub's private channels. Include affected version, impact, and reproducible steps. Do not include private game data, console keys, firmware, credentials, or personal information.

## Project security rules
- Never commit tokens, passwords, signing keys, private certificates, or local environment files.
- Keep release-signing private keys offline or in a dedicated signing service; never place them in GitHub Actions build jobs.
- Verify update signatures and SHA-256 hashes before installation. A hash without a trusted signature is not sufficient.
- Treat all game files and user-selected paths as untrusted input.
- Do not bundle Sony firmware, console keys, proprietary system libraries, or copyrighted game data.
- Review third-party actions and pin them to full commit SHAs.
