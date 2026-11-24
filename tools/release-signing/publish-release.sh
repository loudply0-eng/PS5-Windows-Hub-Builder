#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 7 ]]; then
  echo "Usage: $0 <version> <target-commit> <dist-dir> <manifest.json> <manifest.sig.base64> <release-notes.txt> <repository>" >&2
  exit 2
fi

VERSION="$1"
TARGET_COMMIT="$2"
DIST_DIR="$(cd "$3" && pwd)"
MANIFEST="$(cd "$(dirname "$4")" && pwd)/$(basename "$4")"
SIGNATURE="$(cd "$(dirname "$5")" && pwd)/$(basename "$5")"
NOTES="$(cd "$(dirname "$6")" && pwd)/$(basename "$6")"
REPOSITORY="$7"
TAG="v$VERSION"
ASSET="$DIST_DIR/PS5-Windows-Hub-$VERSION.zip"
SOURCE="$DIST_DIR/AnyPS5-v0.1.5-source.zip"
LICENSE="$DIST_DIR/LICENSE-AnyPS5.txt"

if [[ ! "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid stable release version: $VERSION" >&2
  exit 1
fi
for file in "$ASSET" "$SOURCE" "$LICENSE" "$MANIFEST" "$SIGNATURE" "$NOTES"; do
  [[ -f "$file" ]] || { echo "Required release file is missing: $file" >&2; exit 1; }
done
command -v gh >/dev/null 2>&1 || { echo "GitHub CLI is required." >&2; exit 1; }
command -v sha256sum >/dev/null 2>&1 || { echo "sha256sum is required." >&2; exit 1; }
(
  cd "$DIST_DIR"
  sha256sum --check SHA256SUMS.txt
)
gh auth status --hostname github.com >/dev/null
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
