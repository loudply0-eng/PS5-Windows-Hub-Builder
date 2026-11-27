#!/usr/bin/env python3
"""Create a redistributable KytyPS5 corresponding-source archive.

The caller must first check out the exact upstream release commit and initialize
all submodules recursively. This script packages that working tree, including
submodule files, while excluding Git metadata and local build/cache folders.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import zipfile


EXCLUDED_DIRECTORIES = {".git", "_Build", "build", "out", "CMakeFiles", "__pycache__"}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    source = args.source.resolve(strict=True)
    output = args.output.resolve()
    if not source.is_dir():
        raise SystemExit(f"Source directory does not exist: {source}")

    for required in ("README.md", "LICENSE", ".gitmodules", "LICENSES", "3rdparty"):
        if not (source / required).exists():
            raise SystemExit(f"Required upstream source item is missing: {required}")

    output.parent.mkdir(parents=True, exist_ok=True)
    prefix = source.name
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for current, directories, files in os.walk(source, followlinks=False):
            current_path = Path(current)
            directories[:] = sorted(
                name for name in directories
                if name not in EXCLUDED_DIRECTORIES
                and not (current_path / name).is_symlink()
            )
            for filename in sorted(files):
                if filename == ".git":
                    continue
                item = current_path / filename
                if item.is_symlink():
                    resolved = item.resolve(strict=True)
                    try:
                        relative_target = resolved.relative_to(source)
                    except ValueError as exc:
                        raise SystemExit(f"Source symlink escapes checkout: {item}") from exc
                    if not resolved.is_file():
                        continue
                    item_to_write = resolved
                else:
                    item_to_write = item
                if not item_to_write.is_file():
                    continue
                relative = item.relative_to(source).as_posix()
                archive.write(item_to_write, f"{prefix}/{relative}")

    with zipfile.ZipFile(output, "r") as archive:
        names = set(archive.namelist())
    required_entries = (
        f"{prefix}/README.md",
        f"{prefix}/LICENSE",
        f"{prefix}/.gitmodules",
    )
    for entry in required_entries:
        if entry not in names:
            raise SystemExit(f"Source archive is missing {entry}")
    for required_prefix in (f"{prefix}/LICENSES/", f"{prefix}/3rdparty/SDL3/"):
        if not any(name.startswith(required_prefix) for name in names):
            raise SystemExit(f"Source archive is missing recursively checked-out content under {required_prefix}")

    print(f"Created corresponding-source archive: {output} ({output.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
