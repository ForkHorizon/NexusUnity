#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
PACKAGE_ROOT=$(git -C "$SCRIPT_DIR/.." rev-parse --show-toplevel)
MODE="${1:---quick}"
NEXUS_UNITY_URL="${NEXUS_UNITY_URL:-http://127.0.0.1:8081/}"
NEXUS_UNITY_HOOK_LIVE="${NEXUS_UNITY_HOOK_LIVE:-auto}"
NEXUS_QUALITY_ROOT="${NEXUS_QUALITY_ROOT:-$PACKAGE_ROOT}"
VALIDATION_STARTED=$SECONDS

cd "$PACKAGE_ROOT"

log() {
  printf '\n==> [%ss] %s\n' "$((SECONDS - VALIDATION_STARTED))" "$1"
}

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

run_static_validation() {
  local started=$SECONDS
  log "Validating package metadata"
  python3 -m json.tool package.json > /tmp/nexus-package-json.$$.json
  python3 - <<'PY'
import json

with open("package.json", encoding="utf-8") as handle:
    package = json.load(handle)

assert package["name"] == "com.forkhorizon.nexus.unity"
assert package["version"]
assert package["license"] == "GPL-3.0-only"
assert package["repository"]["url"] == "https://github.com/ForkHorizon/NexusUnity.git"
PY

  log "Compiling Python bridge"
  python3 - <<'PY'
from pathlib import Path

paths = [Path("Editor/nexus_unity_bridge.py")]
paths.extend(sorted(Path("Editor/nexus_bridge").glob("*.py")))
paths.append(Path("scripts/agent-tooling-smoke.py"))

for path in paths:
    source = path.read_text(encoding="utf-8")
    compile(source, str(path), "exec")
PY

  log "Checking for generated or local-only files"
  python3 - <<'PY'
import os
import sys

forbidden = []
for root, dirs, files in os.walk("."):
    parts = set(root.split(os.sep))
    if ".git" in parts:
        continue

    for name in files:
        path = os.path.join(root, name)
        normalized = path.replace(os.sep, "/")
        if (
            name == ".DS_Store"
            or name.endswith((".pyc", ".pyo"))
            or "__pycache__" in normalized
            or "/Library/" in normalized
            or "/Temp/" in normalized
            or "/graphify-out/" in normalized
            or "/.soma/" in normalized
        ):
            forbidden.append(path)

if forbidden:
    print("Forbidden generated/local files found:")
    print("\n".join(forbidden))
    sys.exit(1)
PY

  log "Checking Unity .meta pairing"
  python3 - <<'PY'
import pathlib
import sys

root = pathlib.Path(".")
missing_meta = []
orphan_meta = []
ignore_roots = {".git", ".github", "bin", "obj"}

for path in root.rglob("*"):
    if any(part in ignore_roots for part in path.parts):
        continue
    if path.suffix == ".meta":
        target = pathlib.Path(str(path)[:-5])
        if not target.exists():
            orphan_meta.append(str(path))
    elif path.is_file() and path.name != ".gitignore":
        if not pathlib.Path(str(path) + ".meta").exists():
            missing_meta.append(str(path))

if missing_meta or orphan_meta:
    if missing_meta:
        print("Missing .meta files:")
        print("\n".join(missing_meta))
    if orphan_meta:
        print("Orphan .meta files:")
        print("\n".join(orphan_meta))
    sys.exit(1)
PY

  log "Running Nexus quality gate tests"
  dotnet run --project "$PACKAGE_ROOT/tools~/NexusQualityGate.Tests/NexusQualityGate.Tests.csproj"

  log "Running deterministic Nexus quality gate"
  dotnet run --project "$PACKAGE_ROOT/tools~/NexusQualityGate/NexusQualityGate.csproj" -- \
    --root "$NEXUS_QUALITY_ROOT" \
    --ai off \
    --format github

  printf 'Static validation completed in %ss.\n' "$((SECONDS - started))"
}

run_ai_quality_validation() {
  local started=$SECONDS
  log "Running required Ollama documentation quality gate"
  dotnet run --project "$PACKAGE_ROOT/tools~/NexusQualityGate/NexusQualityGate.csproj" -- \
    --root "$NEXUS_QUALITY_ROOT" \
    --ai required \
    --format github
  printf 'AI quality validation completed in %ss.\n' "$((SECONDS - started))"
}

run_quick_live_smoke() {
  case "$NEXUS_UNITY_HOOK_LIVE" in
    auto|required|off)
      ;;
    *)
      fail "Invalid NEXUS_UNITY_HOOK_LIVE=$NEXUS_UNITY_HOOK_LIVE. Use auto, required, or off."
      ;;
  esac

  if [[ "$NEXUS_UNITY_HOOK_LIVE" == "off" ]]; then
    log "Skipping quick live server smoke"
    printf 'NOTICE: NEXUS_UNITY_HOOK_LIVE=off; static validation only.\n'
    return
  fi

  local started=$SECONDS
  log "Running quick live server smoke"
  python3 - "$NEXUS_UNITY_URL" "$NEXUS_UNITY_HOOK_LIVE" <<'PY'
import json
import sys
import time
import urllib.error
import urllib.request

url = sys.argv[1]
mode = sys.argv[2]

TRANSIENT_ERRORS = (
    OSError,
    TimeoutError,
    urllib.error.URLError,
    json.JSONDecodeError,
)


class TransientFailure(Exception):
    pass


class ContractFailure(Exception):
    pass


def rpc(method, params=None, timeout=2, retries=0):
    payload = json.dumps({
        "jsonrpc": "2.0",
        "method": method,
        "params": params or {},
        "id": 1,
    }).encode("utf-8")

    last_error = None
    for attempt in range(retries + 1):
        try:
            request = urllib.request.Request(
                url,
                data=payload,
                headers={"Content-Type": "application/json"},
            )
            with urllib.request.urlopen(request, timeout=timeout) as response:
                data = json.loads(response.read().decode("utf-8"))
            if "error" in data:
                raise ContractFailure(f"{method} returned error: {data['error']}")
            return data.get("result")
        except ContractFailure:
            raise
        except TRANSIENT_ERRORS as exc:
            last_error = exc
            if attempt < retries:
                time.sleep(min(0.25 * (2 ** attempt), 2.0))

    raise TransientFailure(str(last_error))


def live_or_skip(operation):
    try:
        return operation()
    except TransientFailure as exc:
        if mode == "auto":
            print(f"NOTICE: Nexus Unity server is not reachable or timed out at {url}; skipping live smoke ({exc}).")
            sys.exit(0)
        raise SystemExit(f"Nexus Unity live smoke failed in required mode: {exc}")


def stage(label, operation):
    started = time.monotonic()
    result = live_or_skip(operation)
    print(f"{label}: {time.monotonic() - started:.2f}s")
    return result

total_started = time.monotonic()
server = stage("get_server_status", lambda: rpc("get_server_status", timeout=2, retries=4))

if not server:
    raise SystemExit("get_server_status returned an empty result")

tools = stage("list_tools", lambda: rpc("list_tools", timeout=3, retries=2))
if not isinstance(tools, list) or len(tools) < 100:
    raise SystemExit(f"list_tools returned an unexpected catalog size: {len(tools) if isinstance(tools, list) else type(tools).__name__}")

by_name = {tool.get("name"): tool for tool in tools if isinstance(tool, dict)}
required = {
    "get_server_status",
    "update_component",
    "create_material",
    "invoke_method",
    "click_object_in_game",
}
missing = sorted(required - set(by_name))
if missing:
    raise SystemExit(f"Missing expected public raw tools: {', '.join(missing)}")

update_schema = by_name["update_component"]["inputSchema"]
update_props = update_schema["properties"]
for key in ("properties", "json_data"):
    if key not in update_props:
        raise SystemExit(f"update_component schema is missing {key}")
if set(update_schema.get("required", [])) != {"instance_id", "component_name"}:
    raise SystemExit("update_component required fields should be instance_id and component_name")

invoke_args = by_name["invoke_method"]["inputSchema"]["properties"].get("arguments")
if not isinstance(invoke_args, dict) or invoke_args.get("type") != "array":
    raise SystemExit("invoke_method.arguments must be documented as an array schema")

if "path" not in by_name["create_material"]["inputSchema"]["properties"]:
    raise SystemExit("create_material schema must expose optional path")

stage("get_editor_state", lambda: rpc("get_editor_state", timeout=3, retries=2))
print(f"Quick live smoke passed in {time.monotonic() - total_started:.2f}s: {len(tools)} raw tools, server state={server.get('state', 'unknown')}.")
PY
  printf 'Quick live server smoke completed in %ss.\n' "$((SECONDS - started))"
}

resolve_unity_project_root() {
  if [[ -n "${NEXUS_UNITY_PROJECT_ROOT:-}" ]]; then
    printf '%s\n' "$NEXUS_UNITY_PROJECT_ROOT"
    return
  fi

  local candidate
  candidate=$(cd "$PACKAGE_ROOT/../.." && pwd)
  if [[ -f "$candidate/ProjectSettings/ProjectVersion.txt" ]]; then
    printf '%s\n' "$candidate"
    return
  fi

  fail "Unable to find Unity project root. Set NEXUS_UNITY_PROJECT_ROOT before running local integration validation."
}

check_nexus_server_ready() {
  local unity_project_root="$1"
  python3 - "$unity_project_root" <<'PY'
import json
import sys
import time
import urllib.error
import urllib.request

project_root = sys.argv[1]
payload = json.dumps({
    "jsonrpc": "2.0",
    "method": "get_server_status",
    "params": {},
    "id": 1,
}).encode("utf-8")
deadline = time.time() + 10

while time.time() < deadline:
    request = urllib.request.Request(
        "http://127.0.0.1:8081/",
        data=payload,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=3) as response:
            data = json.loads(response.read().decode("utf-8"))
        if "result" in data:
            sys.exit(0)
    except (OSError, TimeoutError, urllib.error.URLError, json.JSONDecodeError):
        time.sleep(1)

print(
    "Nexus Unity server is unreachable at http://127.0.0.1:8081/.\n"
    f"Open {project_root} in Unity and start the server from "
    "Window > Nexus Unity > Server > Start Server.",
    file=sys.stderr,
)
sys.exit(1)
PY
}

run_local_integration_validation() {
  local unity_project_root
  unity_project_root=$(resolve_unity_project_root)

  [[ -f "$unity_project_root/AI/run_all_tests.py" ]] || fail "Missing $unity_project_root/AI/run_all_tests.py"

  log "Checking live Nexus Unity server"
  check_nexus_server_ready "$unity_project_root"

  log "Running Python integration tests"
  (cd "$unity_project_root" && python3 AI/run_all_tests.py)
}

case "$MODE" in
  --static-only)
    run_static_validation
    ;;
  --quality-ai)
    run_ai_quality_validation
    ;;
  --quick|--local)
    run_static_validation
    run_quick_live_smoke
    ;;
  --integration|--full)
    run_static_validation
    run_quick_live_smoke
    run_local_integration_validation
    ;;
  *)
    fail "Unknown mode: $MODE. Use --quick, --static-only, or --integration."
    ;;
esac

log "Nexus Unity validation passed in $((SECONDS - VALIDATION_STARTED))s"
