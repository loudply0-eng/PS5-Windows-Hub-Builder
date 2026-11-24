#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 8 ]]; then
  echo "Usage: $0 <version> <target-commit> <dist-dir> <manifest.json> <manifest.sig.base64> <public-key.pem> <release-notes.txt> <repository>" >&2
  exit 2
fi

VERSION="$1"
TARGET_COMMIT="$2"
DIST_DIR="$(cd "$3" && pwd)"
MANIFEST="$(cd "$(dirname "$4")" && pwd)/$(basename "$4")"
SIGNATURE="$(cd "$(dirname "$5")" && pwd)/$(basename "$5")"
PUBLIC_KEY="$(cd "$(dirname "$6")" && pwd)/$(basename "$6")"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NOTES="$(cd "$(dirname "$7")" && pwd)/$(basename "$7")"
REPOSITORY="$8"
TAG="v$VERSION"
ASSET="$DIST_DIR/PS5-Windows-Hub-$VERSION.zip"
SOURCE="$DIST_DIR/AnyPS5-v0.1.5-source.zip"
LICENSE="$DIST_DIR/LICENSE-AnyPS5.txt"
BUILDER_COMMIT_FILE="$DIST_DIR/BUILDER-COMMIT.txt"

if [[ ! "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid stable release version: $VERSION" >&2
  exit 1
fi
if [[ ! "$TARGET_COMMIT" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "Target must be a full 40-character commit SHA." >&2
  exit 1
fi
for file in "$ASSET" "$SOURCE" "$LICENSE" "$BUILDER_COMMIT_FILE" "$MANIFEST" "$SIGNATURE" "$PUBLIC_KEY" "$NOTES"; do
  [[ -f "$file" ]] || { echo "Required release file is missing: $file" >&2; exit 1; }
done
command -v gh >/dev/null 2>&1 || { echo "GitHub CLI is required." >&2; exit 1; }
command -v sha256sum >/dev/null 2>&1 || { echo "sha256sum is required." >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Python 3 is required." >&2; exit 1; }
command -v openssl >/dev/null 2>&1 || { echo "OpenSSL is required." >&2; exit 1; }
(
  cd "$DIST_DIR"
  sha256sum --check SHA256SUMS.txt
)
BUILT_COMMIT="$(tr -d '\r\n' < "$BUILDER_COMMIT_FILE")"
if [[ "${BUILT_COMMIT,,}" != "${TARGET_COMMIT,,}" ]]; then
  echo "The build artifact commit does not match the requested release target." >&2
  exit 1
fi
gh auth status --hostname github.com >/dev/null
python3 "$SCRIPT_DIR/verify-manifest.py" "$MANIFEST" "$ASSET" "$PUBLIC_KEY" "$SIGNATURE"
if gh api "repos/$REPOSITORY/git/ref/tags/$TAG" >/dev/null 2>&1; then
  echo "Git tag $TAG already exists. Refusing to publish against an unverified tag." >&2
  exit 1
fi
gh api "repos/$REPOSITORY/commits/$TARGET_COMMIT" >/dev/null
if gh release view "$TAG" --repo "$REPOSITORY" >/dev/null 2>&1; then
  echo "Release $TAG already exists. Refusing to overwrite it." >&2
  exit 1
fi

gh release create "$TAG" \
  "$ASSET" \
  "$SOURCE" \
  "$LICENSE" \
  "$MANIFEST" \
  "$SIGNATURE" \
  --repo "$REPOSITORY" \
  --target "$TARGET_COMMIT" \
  --title "PS5 WINDOWS HUB $VERSION" \
  --notes-file "$NOTES"

echo "Published $TAG with the signed manifest, update package, matching source, and license."
echo "The private signing key was not read or uploaded by this script."
