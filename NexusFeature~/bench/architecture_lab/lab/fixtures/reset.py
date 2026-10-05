"""Fixture fingerprint and file reset.

A fingerprint mismatch voids the run before a model is started.
"""

from __future__ import annotations

import hashlib
import shutil
from pathlib import Path
from typing import Any

from lab.errors import RunVoid


def fingerprint(root: Path) -> str:
    """Hash regular files under a fixture directory. Order is by relative path."""
    digest = hashlib.sha256()
    files = [path for path in root.rglob("*") if path.is_file() and path.name != ".DS_Store"]
    for path in sorted(files, key=lambda item: item.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def reset_tree(pristine: Path, destination: Path) -> str:
    """Replace destination with a copy of pristine and return the new fingerprint."""
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(pristine, destination)
    return fingerprint(destination)


def require_fingerprint(root: Path, expected: str) -> None:
    actual = fingerprint(root)
    if actual != expected:
        raise RunVoid(f"fixture fingerprint {actual} != {expected}")


def reload_lab_scene(backend, project: Path, pristine: Path, destination: Path) -> dict[str, Any]:
    """Leave the lab scene, restore its files, then open the pristine scene.

    Copying files while the editor still has the scene loaded does not change
    the in-memory objects. Creating a different scene first gives the file
    restore a chance to stick. The scratch scene lives outside the hashed tree.
    """
    # Keep this folder. Deleting it while the AssetDatabase still has it imported
    # makes the next create_scene fail with "Parent directory must exist".
    scratch = project / "Assets" / "LabReset"
    scratch.mkdir(parents=True, exist_ok=True)
    switched = backend(
        "create_scene",
        {"path": "Assets/LabReset/Scratch.unity", "additive": False, "template": "empty"},
    )
    digest = reset_tree(pristine, destination)
    opened = backend(
        "open_scene",
        {"path": "Assets/LabFixture/Scenes/Lab.unity", "additive": False},
    )
    for path in scratch.glob("Scratch.unity*"):
        path.unlink(missing_ok=True)
    return {"fingerprint": digest, "switched": switched, "opened": opened}
