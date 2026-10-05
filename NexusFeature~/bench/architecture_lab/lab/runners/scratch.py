"""Prepare the scratch Unity project. Never touches the harness or port 7800."""

from __future__ import annotations

import json
import os
import shutil
import signal
import subprocess
from pathlib import Path

from lab.errors import SafetyError
from lab.runners.safety import assert_safe_to_stop

LAB_ROOT = Path(__file__).resolve().parents[2]
PACKAGE_ROOT = Path(__file__).resolve().parents[5]
DEFAULT_SOURCE = Path("/tmp/nexus-p08-harness")
DEFAULT_PROJECT = Path("/tmp/nexus-arch-lab")
DEFAULT_PORT = 7811
OLD_PACKAGE = "file:/tmp/nexus-p08-wt"
EDITOR = Path("/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity")
PID_FILE = Path("/tmp/nexus-arch-lab.pid")
LOG_FILE = Path("/tmp/nexus-arch-lab-editor.log")


def prepare_project(source: Path = DEFAULT_SOURCE, destination: Path = DEFAULT_PROJECT, port: int = DEFAULT_PORT) -> dict[str, str]:
    if port == 7800 or port == 0:
        raise SafetyError(f"refusing lab port {port}")
    if "UnityTestForNexus" in str(destination):
        raise SafetyError("refusing to prepare the harness project")
    if not source.is_dir():
        raise SafetyError(f"scratch source missing: {source}")
    if destination.exists():
        raise SafetyError(f"{destination} already exists; refusing to overwrite it")
    shutil.copytree(source, destination, symlinks=True, ignore=shutil.ignore_patterns("Logs"))
    manifest_path = destination / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["dependencies"]["com.forkhorizon.nexus.unity"] = f"file:{PACKAGE_ROOT}"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    lock = destination / "Packages" / "packages-lock.json"
    if lock.exists():
        lock.write_text(lock.read_text(encoding="utf-8").replace(OLD_PACKAGE, f"file:{PACKAGE_ROOT}"), encoding="utf-8")
    config = destination / "ProjectSettings" / "Packages" / "com.unity.pipeline" / "EditorPipelineConfig.json"
    config.parent.mkdir(parents=True, exist_ok=True)
    config.write_text(json.dumps({"m_Port": port}, indent=2) + "\n", encoding="utf-8")
    _install_fixture(destination)
    neutralize_foreign_hooks(destination)
    return {"project": str(destination), "port": str(port), "package": str(PACKAGE_ROOT)}


def neutralize_foreign_hooks(project: Path) -> list[str]:
    """Park editor scripts that open a different scene on every domain reload.

    The copied discovery harness starts YardOpen, which replaces the active
    scene with DiscFixture. That would discard a lab write before the oracle.
    """
    if "UnityTestForNexus" in str(project):
        raise SafetyError("refusing to edit the harness")
    editor = project / "Assets" / "Editor"
    removed = []
    for name in ("YardOpen.cs",):
        path = editor / name
        if not path.is_file():
            continue
        parked = path.with_name(name + ".off")
        if parked.exists():
            parked.unlink()
        path.rename(parked)
        meta = path.with_suffix(path.suffix + ".meta")
        if meta.is_file():
            meta_parked = parked.with_suffix(parked.suffix + ".meta")
            if meta_parked.exists():
                meta_parked.unlink()
            meta.rename(meta_parked)
        removed.append(name)
    return removed


def _install_fixture(project: Path) -> None:
    source = LAB_ROOT / "fixtures" / "unity"
    destination = project / "Assets" / "LabFixture"
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(source, destination, ignore=shutil.ignore_patterns("LabFixtureBuilder.cs", "LabFixtureReset.cs"))
    editor = project / "Assets" / "Editor"
    editor.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source / "LabFixtureBuilder.cs", editor / "LabFixtureBuilder.cs")
    shutil.copyfile(source / "LabFixtureReset.cs", editor / "LabFixtureReset.cs")


def start_editor(project: Path = DEFAULT_PROJECT, *, execute_method: str | None = None, quit_after: bool = False) -> int:
    if not EDITOR.is_file():
        raise SafetyError(f"Unity editor missing: {EDITOR}")
    if "UnityTestForNexus" in str(project):
        raise SafetyError("refusing to launch the harness")
    command = [str(EDITOR), "-batchmode", "-projectPath", str(project), "-logFile", str(LOG_FILE)]
    if execute_method:
        command.extend(["-executeMethod", execute_method])
    if quit_after:
        command.append("-quit")
    proc = subprocess.Popen(command, start_new_session=True)
    PID_FILE.write_text(str(proc.pid), encoding="utf-8")
    return proc.pid


def recorded_pid() -> int | None:
    if not PID_FILE.exists():
        return None
    text = PID_FILE.read_text(encoding="utf-8").strip()
    return int(text) if text.isdigit() else None


def process_command(pid: int) -> str:
    proc = subprocess.run(["ps", "-p", str(pid), "-o", "command="], capture_output=True, text=True, check=False)
    return (proc.stdout or "").strip()


def stop_editor(project_marker: str = "nexus-arch-lab") -> None:
    pid = recorded_pid()
    if pid is None:
        raise SafetyError("no recorded lab editor pid")
    command = process_command(pid)
    assert_safe_to_stop(pid, project_marker, {pid: {"command": command}})
    os.kill(pid, signal.SIGTERM)
