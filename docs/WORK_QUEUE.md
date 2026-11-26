# PS5 Windows Hub — execution queue

Last reviewed: 2026-11-26

This is the working backlog for the Builder preview and the linked ABRJ Core prototype. A task is marked complete only when its acceptance evidence exists. A green CI run does not mean a real PS5 game is playable or that production auto-updates are live.

## Completed in the current work batch

- [x] Move the Windows CI workflow to GitHub-hosted `windows-latest`.
- [x] Build the WinForms application and the AnyPS5 relinker-only component on Windows CI.
- [x] Run relinker, updater security, Kyty path/input/argument-construction, offline signing-tool, updater-helper, install/rollback integration tests.
- [x] Verify the latest PR #7 head before documentation follow-ups: run [45804637894](https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/actions/runs/45804637894) succeeded on `3b082f52c7717693ea5abec37b154e5aaedb9a09`; all executed build/test steps passed.
- [x] Correct documentation that incorrectly described the public Builder repository as private.
- [x] Clarify that Kyty CI tests validate paths, inputs and argument construction; they do not launch a real Kyty binary.
- [x] Archive the obsolete self-hosted-runner setup guide.
- [x] Record the difference between the AnyPS5 audit HEAD and the pinned `v0.1.5` release used by the preview workflow.
- [x] Keep PRs in draft; no PR was merged and no release was published.

## P0 — release and user-install readiness

1. **Produce a fresh preview artifact for the latest commit**
   - Status: waiting for a maintainer-triggered `workflow_dispatch` run.
   - Acceptance: artifact is uploaded for the exact reviewed commit, archive integrity passes, all published SHA-256 entries verify.
   - Note: pull-request CI intentionally skips artifact upload; the successful PR run above is build/test evidence, not a downloadable artifact.

2. **Run a clean Windows installation smoke test**
   - Status: requires a Windows machine.
   - Acceptance: install/extract to a normal user-writable directory, launch the app, inspect startup logs, confirm no unexpected elevation requirement, verify relinker and Kyty tabs explain their limitations correctly.

3. **Validate Kyty external-process launch safely**
   - Status: requires a Windows machine and a legitimate user-selected Kyty installation. CI currently tests path/input validation and argument construction, not a real emulator process launch.
   - Acceptance: missing path, invalid executable, unsupported input, and normal launch paths produce clear results; no bundled or modified Kyty binaries.

4. **Validate the local release-signing key**
   - Status: requires the maintainer's Windows machine and locally held encrypted private key.
   - Acceptance: offline signer accepts the matching key, rejects a mismatched key, creates `manifest.json` and `manifest.sig.base64`, and the public verification tool validates the exact manifest bytes.
   - Security: never upload or commit the private key or passphrase.

5. **Publish and test the first signed update**
   - Status: blocked by task 4 and a maintainer release decision.
   - Acceptance: publish exact versioned ZIP, manifest, detached signature, corresponding GPL source and notices to a public GitHub Release; test update from 0.1.0 to 0.1.1 on a disposable Windows install; test failed-health-check rollback and user-data preservation.
   - Do not claim production auto-update works until this passes.

## P1 — hardening

6. **Add persistent user-data preservation coverage**
   - Acceptance: integration test proves settings and user data outside the application install directory survive successful update and rollback.

7. **Add clean-install and upgrade end-to-end automation**
   - Acceptance: Windows CI installs the packaged build in a temporary directory, starts it, validates health, applies a signed test update, and verifies rollback without touching a real user install.

8. **Decide on Authenticode signing**
   - Status: not implemented.
   - Acceptance: document publisher identity/certificate requirements and sign release executables if a legitimate code-signing certificate is obtained. ECDSA update metadata signatures are not Authenticode and do not remove SmartScreen warnings.

9. **Review GitHub repository security settings**
   - Acceptance: directly verify main-branch ruleset, required checks, least-privilege Actions permissions, Dependabot alerts/security updates, secret scanning and push protection where available. Do not infer settings from workflow files.

10. **Resolve ABRJ Core CI startup failure**
    - Status: blocked on actionable GitHub runner diagnostics. Latest run [45804983367](https://github.com/loudply0-eng/PS5-Windows-Hub-Core/actions/runs/45804983367) failed before exposing any job steps; job-step retrieval returned an empty list and log retrieval was unavailable. The cause is not established; do not label it a source-code failure.
    - Acceptance: obtain runner/job initialization diagnostics, fix the actual cause, then pass configure, build and CTest on Windows.

## P2 — real engine development (not a preview promise)

11. **Turn the ABRJ coordinator prototype into a tested backend contract**
    - Acceptance: define capability discovery, preflight, process lifecycle, structured errors, logs, cancellation and deterministic backend selection; tests cover fallback before game execution.

12. **Audit and pin a real native backend integration**
    - Acceptance: select an exact upstream revision, record license/source obligations, build reproducibly on Windows, and prove a documented supported sample works. AnyPS5 relinker-only output must not be presented as a full emulator.

13. **Implement and test Kyty integration boundaries**
    - Acceptance: documented external process contract, version compatibility checks, exit-code mapping, logs and safe shutdown. No state migration between emulators is promised.

14. **Define a game-compatibility evidence matrix**
    - Acceptance: each tested title records source provenance, required user-provided files, hardware/driver, backend, exact build, boot result and known issues. Never label an untested title playable.

## Operating rules

- Keep `PS5-Windows-Hub-Core` private.
- Do not merge draft PRs without explicit owner approval.
- Do not put private signing keys, passphrases, firmware, console keys or user game dumps in GitHub Actions or the repositories.
- Do not claim full PS5 emulation or game compatibility from relinker tests.
- When a task needs the owner's Windows device, signing key, paid certificate or an explicit release decision, mark it blocked and give one precise action rather than pretending it is complete.
