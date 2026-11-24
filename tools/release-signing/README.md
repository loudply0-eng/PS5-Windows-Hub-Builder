# Offline release signing

These scripts are intended to run locally on the maintainer's phone (for example, in Termux) or another offline device. They never upload keys.

## 1. Generate the key once

On Termux, install the local tools with `pkg install openssl python`, then run:

```sh
bash tools/release-signing/generate-key.sh
```

The default key directory is:

```
$HOME/.local/share/ps5-windows-hub-signing
```

The private key is P-256, encrypted with a passphrase, and stays on the phone. Choose a strong passphrase when OpenSSL prompts. Do not copy it to GitHub, Oracle, GitHub Actions, this repository, or a public/shared folder. Keep a second backup only in encrypted offline storage. Do not regenerate the key after publishing unless a deliberate key-rotation process is implemented.

The generated `public-key-spki-base64.txt` is the public SubjectPublicKeyInfo value. It is safe to share and will be embedded in the Windows application after it is reviewed.

## 2. Embed only the public key

After cloning the Builder repository on the phone, copy or point the script at the generated public-key file:

```sh
python tools/release-signing/embed-public-key.py \
  --public-key-base64 "$HOME/.local/share/ps5-windows-hub-signing/public-key-spki-base64.txt"
```

The script validates that the key is ECDSA P-256 and updates only the empty public-key placeholder in `app/PS5WindowsHub/UpdateTrust.cs`. Review that one source change, commit it to the Builder branch, and rebuild. The private key is never opened by this script.


## 3. Create and sign exact manifest bytes

After the versioned ZIP has been built and copied to the phone, create the manifest locally. The asset URL is the planned GitHub Release URL and must match the exact release tag and ZIP filename; the ZIP does not need to be uploaded yet:

```sh
python tools/release-signing/create-manifest.py \
  --version 0.1.1 \
  --minimum-supported-version 0.1.0 \
  --asset PS5-Windows-Hub-0.1.1.zip \
  --asset-url https://github.com/loudply0-eng/PS5-Windows-Hub-Builder/releases/download/v0.1.1/PS5-Windows-Hub-0.1.1.zip \
  --release-notes release-notes.txt \
  --output manifest.json
```

Then sign the exact file without editing or reformatting it afterward:

```sh
bash tools/release-signing/sign-manifest.sh \
  "$HOME/.local/share/ps5-windows-hub-signing/update-signing-private.pem" \
  manifest.json \
  manifest.sig.base64
```

The output is a base64-encoded ECDSA P-256/SHA-256 DER signature. The verifier checks the signature against the exact downloaded manifest bytes.

## 4. Publish only public release files

After creating and signing the manifest, a maintainer with GitHub CLI access can publish the versioned ZIP, manifest, signature, and matching upstream source and license together:

```sh
bash tools/release-signing/publish-release.sh \
  0.1.1 \
  "$(tr -d '\\r\\n' < ./PS5-Windows-Hub-Preview/BUILDER-COMMIT.txt)" \
  ./PS5-Windows-Hub-Preview \
  ./manifest.json \
  ./manifest.sig.base64 \
  "$HOME/.local/share/ps5-windows-hub-signing/update-signing-public.pem" \
  ./release-notes.txt \
  loudply0-eng/PS5-Windows-Hub-Builder
```

The script verifies the packaged SHA-256 list, checks that the target commit matches the commit recorded in the artifact, verifies the detached signature and P-256 curve, confirms the same public key is embedded in that commit's Windows app source, refuses to reuse an existing tag or release, and uploads only public assets. It never reads the private key.

## 5. Never publish private material

Publish only:
- `manifest.json`
- `manifest.sig.base64`
- the exact ZIP asset referenced by the signed manifest

Never publish:
- `update-signing-private.pem`
- an unencrypted backup of the private key
- any private-key material in commits, issues, logs, workflow artifacts, or Oracle server storage

The updater button remains disabled until the public key is embedded, the release metadata is fetched and verified in-app, and the full update/rollback tests pass.
