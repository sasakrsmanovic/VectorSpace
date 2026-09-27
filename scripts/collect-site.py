#!/usr/bin/env python3
"""Collect Uno's static root and report the actual project or release version."""
import argparse
import json
import os
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path


def project_version(project_file: Path) -> str:
    """Read this repository's explicit Version; never silently report an old release."""
    for element in ET.parse(project_file).getroot().iter():
        if element.tag.rsplit("}", 1)[-1] == "Version":
            value = (element.text or "").strip()
            if value and "$(" not in value:
                return value
    raise ValueError(f"No literal Version found in {project_file}; supply --version or VERSION")


def collect_site(publish: Path, output: Path, version: str, commit: str) -> None:
    version = version.strip()
    if not version:
        raise ValueError("Published version must not be empty")
    candidates = sorted(publish.rglob("index.html"), key=lambda p: (len(p.parts), str(p)))
    if not candidates:
        raise ValueError(f"No index.html found below {publish}")
    source = candidates[0].parent
    # Do not recursively copy a directory into itself, or overwrite its parent.
    if source.resolve() == output.resolve() or source.resolve() in output.resolve().parents or output.resolve() in source.resolve().parents:
        raise ValueError("Publish source and output must not contain each other")
    output.mkdir(parents=True, exist_ok=True)
    shutil.copytree(source, output, dirs_exist_ok=True)
    (output / ".nojekyll").touch()
    (output / "build-info.json").write_text(json.dumps({
        "application": "VectorSpace", "host": "Uno WebAssembly",
        "version": version, "commit": commit
    }), encoding="utf-8")
    print(f"Collected {source} into {output} (version {version}, commit {commit})")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--version", help="Override the project version (otherwise VERSION takes precedence)")
    args = parser.parse_args()
    try:
        version = args.version if args.version is not None else os.environ.get("VERSION")
        if version is None:
            version = project_version(Path(__file__).resolve().parent.parent / "Directory.Build.props")
        collect_site(args.publish, args.output, version, os.environ.get("GITHUB_SHA", "local"))
    except (OSError, ValueError, ET.ParseError) as error:
        parser.exit(1, f"collect-site: {error}\n")


if __name__ == "__main__":
    main()
