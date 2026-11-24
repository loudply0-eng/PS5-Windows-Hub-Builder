# Offline release signing

These scripts are intended to run locally on the maintainer's phone (for example, in Termux) or another offline device. They never upload keys.

## 1. Generate the key once

Install OpenSSL on the phone, then run:

```sh
bash tools/release-signing/generate-key.sh
```

The default key directory is:

```
$HOME/.local/share/ps5-windows-hub-signing
```

The private key is P-256, encrypted with a passphrase, and stays on the phone. Choose a strong passphrase when OpenSSL prompts. Do not copy it to GitHub, Oracle, GitHub Actions, this repository, or a public/shared folder. Keep a second backup only in encrypted offline storage. Do not regenerate the key after publishing unless a deliberate key-rotation process is implemented.

The generated `public-key-spki-base64.txt` is the public SubjectPublicKeyInfo value. It is safe to share and will be embedded in the Windows application after it is reviewed.

## 2. Sign exact manifest bytes

After a release manifest has been finalized, sign it without editing or reformatting it afterward:

```sh
bash tools/release-signing/sign-manifest.sh \
  "$HOME/.local/share/ps5-windows-hub-signing/update-signing-private.pem" \
  manifest.json \
  manifest.sig.base64
```

The output is a base64-encoded ECDSA P-256/SHA-256 DER signature. The verifier checks the signature against the exact downloaded manifest bytes.

## 3. Never publish private material

Publish only:
- `manifest.json`
- `manifest.sig.base64`
- the exact ZIP asset referenced by the signed manifest

Never publish:
- `update-signing-private.pem`
- an unencrypted backup of the private key
- any private-key material in commits, issues, logs, workflow artifacts, or Oracle server storage

The updater button remains disabled until the public key is embedded, the release metadata is fetched and verified in-app, and the full update/rollback tests pass.
