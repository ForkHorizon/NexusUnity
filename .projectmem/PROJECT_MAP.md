# Project Map - NexusUnity

Status: curated project map. Update this map when package boundaries or primary entry points change.

## Project purpose
Open source Unity Editor automation server for local AI tools and developer workflows.

## Stack
- Unity Package Manager package: `com.forkhorizon.nexus.unity` for Unity `6000.0+`.
- C# Editor implementation under `Editor/`, with runtime support under `Runtime/`.
- Python 3 MCP bridge under `Editor/nexus_unity_bridge.py` and `Editor/nexus_bridge/`.
- Tests: Unity EditMode tests under `Tests/Editor/` and Python bridge tests under `Editor/tests/`.
- Validation: `scripts/prepush-validate.sh`, GitHub Actions, and the .NET `tools~/NexusQualityGate` tool.

## Entry points
- `package.json` — UPM package identity, dependencies, and public version.
- `Editor/MCPServer.cs` — local JSON-RPC server lifecycle and editor state.
- `Editor/MCPServerMethods.cs` — raw JSON-RPC dispatch; partial method files implement tool groups.
- `Editor/nexus_unity_bridge.py` — Python MCP bridge process entry point.
- `Editor/nexus_bridge/routing.py` — MCP manager routing and macros.

## Structure
- `Editor/` — Unity Editor server, raw tools, integration UI, and Python bridge.
  - `MCPServer.cs` — server state/lifecycle; `MCPServer.Networking.cs` owns HTTP/WebSocket handling.
  - `MCPServerMethods.cs` — tool dispatch; `MCPServerMethods.*.cs` group the raw tool implementations.
  - `Capture/` — transport-independent Capture V2 (`ICaptureGateway`, DriverOwnedReadback, Game View source, JPEG/PNG encode).
  - `Commands/` — canonical Nexus command descriptors and handlers (`nexus.project_map`, `nexus.group_compile_errors`, `nexus.capture_game_view`).
  - `Pipeline/` — optional `UnityMCP.Editor.Pipeline` assembly with `[CliCommand]` wrappers; excluded when `com.unity.pipeline` is absent.
  - `Runtime/` — transport adapters, Pipeline capability probe, and `legacy`/`pipeline`/`auto` runtime selection.
  - `MCPServerMethods.HighValue.GameViewCapture.cs` — Legacy HTTP/MCP adapter that maps `capture_game_view_screenshot` onto `ICaptureGateway`.
  - `NexusMcpConfigGenerator.cs` — generated MCP client configuration and bridge health checks.
  - `nexus_unity_bridge.py` — MCP protocol entry point.
  - `nexus_bridge/` — bridge transport, schemas, and manager routing.
  - `tests/` — Python bridge unit tests.
- `Runtime/` — runtime assembly support used by Editor-facing features.
- `Tests/Editor/` — the single Unity EditMode/API/security/integration test suite.
- `scripts/` — package validation and optional agent-tooling smoke script.
- `tools~/NexusQualityGate/` — standalone .NET documentation and source-quality gate.
- `.github/workflows/validate.yml` — protected-branch CI validation and Unity package smoke.
- `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD`, `RELEASE.md`, `CHANGELOG.md` — public package and release documentation.

## Relationships
- `Editor/MCPServer.Networking.cs` validates a local request before `Editor/MCPServerMethods.cs` dispatches the raw JSON-RPC method on the Unity main thread.
- `Editor/MCPServerMethods.HighValue.GameViewCapture.cs` parses Legacy screenshot params, calls `Editor/Capture/CaptureGateway.cs`, then Base64-encodes `CaptureResult` into the existing JSON schema.
- `Editor/Commands/NexusLegacyCommandProjection.cs` registers canonical command handlers on the Legacy HTTP table; `Editor/Pipeline/NexusPipelineCommands.cs` calls the same handlers through `[CliCommand]` when Pipeline is present.
- `Editor/Runtime/NexusRuntimeHost.cs` selects Legacy vs Pipeline from `MCPSettings.RuntimeMode` and `Editor/Runtime/NexusRuntimeCapabilities.cs` without blocking Editor init.
- `Editor/nexus_unity_bridge.py` loads `Editor/nexus_bridge/routing.py`, which maps MCP manager tools to the raw Unity JSON-RPC API.
- `Editor/nexus_bridge/schemas.py` defines the public bridge contracts exercised by `Editor/tests/`.
- `Editor/NexusMcpConfigGenerator.cs` deploys the bridge and writes client configuration containing the current auth token.
- `scripts/prepush-validate.sh` runs bridge tests and `tools~/NexusQualityGate`; GitHub CI additionally imports the package into a clean Unity project.

## Suggested first reads
- For API or server behavior: `API_REFERENCE.MD`, `Editor/MCPServer.cs`, and the relevant `Editor/MCPServerMethods.*.cs` file.
- For MCP bridge work: `Editor/nexus_unity_bridge.py`, `Editor/nexus_bridge/routing.py`, and `Editor/nexus_bridge/schemas.py`.
- For package publication: `RELEASE.md`, `CHANGELOG.md`, and `.github/workflows/validate.yml`.
