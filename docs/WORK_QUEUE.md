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
   - Status: build and all CI checks succeeded on current Builder PR head `b93e02a6d99e19e1171c39c7d6a4c68d9ad6cec8` in Windows CI run [45851498934](https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/actions/runs/45851498934). The build, relinker, updater security, offline signing, transaction integration and SHA-256 verification steps all passed; the earlier `CS0162` warning is absent.
   - Remaining action: a maintainer must run `workflow_dispatch` on the final reviewed branch head to upload the artifact. PR CI intentionally skips upload.
   - Acceptance: artifact is uploaded for the exact reviewed commit, archive integrity passes, all published SHA-256 entries verify.

2. **Run a clean Windows installation smoke test**
   - Status: requires a Windows machine.
   - Acceptance: install/extract to a normal user-writable directory, launch the app, inspect startup logs, confirm no unexpected elevation requirement, verify relinker and Kyty tabs explain their limitations correctly.

3. **Validate Kyty external-process launch safely**
   - Status: Core now has a portable factory that builds an Emulation-role process backend from an installed Kyty executable and the upstream-documented `--game <path>` argument. It rejects missing executables, invalid paths and game directories without a regular `eboot.bin`; path/argument tests pass in the local portable audit. This is not a real Kyty launch.
   - Remaining action: requires a Windows machine and a legitimate user-selected Kyty installation. Exercise the built process adapter against that exact installed binary and a legally obtained game target; confirm exit status, logs and graceful/force shutdown.
   - Limitations: Core does not inspect the selected Kyty version, and it does not parse the contents of `.zar` files; Kyty itself verifies the archive's root `eboot.bin`. No Kyty binary is bundled or modified.

4. **Validate the local release-signing key**
   - Status: requires the maintainer's Windows machine and locally held encrypted private key.
   - Acceptance: offline signer accepts the matching key, rejects a mismatched key, creates `manifest.json` and `manifest.sig.base64`, and the public verification tool validates the exact manifest bytes.
   - Security: never upload or commit the private key or passphrase.

5. **Publish and test the first signed update**
   - Status: blocked by task 4 and a maintainer release decision.
   - Acceptance: publish exact versioned ZIP, manifest, detached signature, corresponding GPL source and notices to a public GitHub Release; test update from 0.1.0 to 0.1.1 on a disposable Windows install; test failed-health-check rollback and user-data preservation.
   - Do not claim production auto-update works until this passes.

## P1 — hardening

6. **Complete persistent user-data preservation coverage**
   - Status: implemented and covered by automated tests. The desktop app stores its selected Kyty directory, game input, relinker input and output in versioned JSON at `%LOCALAPPDATA%\\PS5WindowsHub\\settings.json`; writes use a temporary file and atomic replacement, and corrupt/unknown-schema settings safely fall back to defaults.
   - The settings-store tests verify exact round-trip and safe recovery from corrupt/unsupported settings. The Windows updater transaction test seeds representative values at the actual settings path, performs a successful directory swap and a failed-update rollback, then reads the JSON and verifies every setting is unchanged after both operations. A separate external user-data sentinel is also checked after both paths.

7. **Reject reparse points in updater paths and clean backups without traversing links**
   - Status: implemented and validated on latest Builder PR head `b93e02a6d99e19e1171c39c7d6a4c68d9ad6cec8`, Windows CI run [45851498934](https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/actions/runs/45851498934); all build, relinker, updater security, signing, transaction integration and SHA-256 steps passed.
   - Coverage: validates every existing directory component from the volume root through staging, payload, installation and installation parent; verifies payload containment using a relative path; rejects a reparse-point application executable; and rejects payload/install paths that traverse Junction ancestors.
   - The Windows transaction test created an old backup containing a nested Junction, removed that backup tree without traversing the link, verified the external sentinel remained intact, and passed successful update, failed-health-check rollback, settings persistence and user-data preservation.
   - Limitation: path validation is not a complete defence against a hostile same-user process racing filesystem changes between validation and use (TOCTOU).

8. **Add clean-install and upgrade end-to-end automation**
   - Acceptance: Windows CI installs the packaged build in a temporary directory, starts it, validates health, applies a signed test update, and verifies rollback without touching a real user install.

9. **Decide on Authenticode signing**
   - Status: not implemented.
   - Acceptance: document publisher identity/certificate requirements and sign release executables if a legitimate code-signing certificate is obtained. ECDSA update metadata signatures are not Authenticode and do not remove SmartScreen warnings.

10. **Review GitHub repository security settings**
   - Acceptance: directly verify main-branch ruleset, required checks, least-privilege Actions permissions, Dependabot alerts/security updates, secret scanning and push protection where available. Do not infer settings from workflow files.

11. **Resolve ABRJ Core Actions runner allocation failure**
    - Status: blocked outside the source build. Latest runs [45853402977](https://github.com/loudply0-eng/PS5-Windows-Hub-Core/actions/runs/45853402977) (PR) and [45853391243](https://github.com/loudply0-eng/PS5-Windows-Hub-Core/actions/runs/45853391243) (push) both failed before workflow steps. The PR run's `ubuntu-latest` job `140025369230` and `windows-latest` job `140025369889` each had `runner_id: 0`, blank runner names, `steps: []` and no downloadable logs. The same pattern persists on both platforms; it is not a compiler/CTest failure and the precise GitHub-side cause remains unknown.
    - Local evidence: the portable Core source/test subset was reconstructed and built using GNU C++ 14.2.0 and CMake 3.31.6. Release build/CTest and Debug build with `-Wall -Wextra -Wpedantic` both passed (1/1 CTest executable, 26 named test cases). This does not compile or verify the Windows-only process adapter.
    - Acceptance: restore Actions runner allocation for the private Core repository, then pass Windows configure/build/CTest and portable Linux configure/build/CTest on the current code.

## P2 — real engine development (not a preview promise)

12. **Turn the ABRJ coordinator prototype into a tested backend contract**
    - Status: partial implementation. Native-first selection, preflight, isolated workspaces, process lifecycle snapshots, structured failure records, no-fallback-after-guest-start, NUL path rejection, process cancellation/graceful-close seams and an Emulation-role process backend exist. The portable suite has 26 named test cases and passes the local Release/strict-Debug Linux audit; Windows-only integration remains unverified because Actions cannot allocate runners.
    - Remaining acceptance: Windows build/tests, stdout/stderr capture, environment policy, process Job Object containment, crash diagnostics, and a tested real backend runtime.

13. **Audit and pin a real native backend integration**
    - Acceptance: select an exact upstream revision, record license/source obligations, build reproducibly on Windows, and prove a documented supported sample works. AnyPS5 relinker-only output must not be presented as a full emulator.

14. **Implement and test Kyty integration boundaries**
    - Status: partial launcher boundary implemented. Core validates the Kyty executable and game target, requires `eboot.bin` in directory targets, produces the documented `--game <path>` UTF-8 argument, and constructs an Emulation-role process backend. The factory is covered through the native-first coordinator with fake-process tests.
    - Remaining acceptance: identify/validate the installed Kyty version, compile the Win32 adapter, launch a legitimate installed Kyty binary on Windows, capture its actual logs/exit behavior, and verify graceful shutdown. Core does not parse `.zar` internals; Kyty checks the archive's root `eboot.bin`. No state migration between emulators is promised.

15. **Define a game-compatibility evidence matrix**
    - Acceptance: each tested title records source provenance, required user-provided files, hardware/driver, backend, exact build, boot result and known issues. Never label an untested title playable.

## Operating rules

- Keep `PS5-Windows-Hub-Core` private.
- Do not merge draft PRs without explicit owner approval.
- Do not put private signing keys, passphrases, firmware, console keys or user game dumps in GitHub Actions or the repositories.
- Do not claim full PS5 emulation or game compatibility from relinker tests.
- When a task needs the owner's Windows device, signing key, paid certificate or an explicit release decision, mark it blocked and give one precise action rather than pretending it is complete.
