# Nexus Unity

Nexus Unity is a Unity Editor automation library that provides a built-in JSON-RPC/MCP server for AI tools and local workflows.

**Requirements**: Unity 6000.0 or newer.

## Role In NexusSoma

Nexus Unity is the low-level Unity control layer. In the current NexusSoma architecture, Big AI clients should normally connect to **Soma**, not directly to Nexus Unity.

```text
Big AI client
  -> Soma MCP gateway
  -> Nexus Unity HTTP JSON-RPC server
  -> Unity Editor
```

Nexus remains powerful and fully available, but Soma hides the large raw `unity_*` tool catalog behind compact `soma_*` tools. This reduces token cost and prevents exploratory Unity tool spam.

Direct Nexus MCP/CLI linking is still supported for:

- low-level Nexus library development
- direct diagnostics
- bridge testing
- cases where the user explicitly wants raw Unity tools

For normal Big AI work, use Soma first.

## Features

- **Autonomous Background Workflow**: Native macOS App Nap bypass and focus synchronization for script compilation workflows.
- **Robust Port Management**: Process owner detection and dynamic port support.
- **Explicit Server Health Monitoring**: Tracks compiling/importing/play mode/main-thread responsiveness.
- **Persistent Session Management**: Session IDs and generation counters survive domain reloads.
- **Hybrid Deep Auditor**: Static code analysis plus Nexus-native scene health scanning.
- **Unity 6 Compatibility**: Uses modern Unity 6000 APIs for instance/object resolution.
- **Intuitive Component Updates**: Native JSON payloads and fuzzy property naming.
- **ScriptableObject Diff & Balancing**: Compare and patch data assets.
- **Prefab Editing Helpers**: Edit prefab assets and inspect overrides.
- **Strong Serialized Object Inspection**: Deep arrays/lists/managed reference serialization.
- **Runtime Gameplay Input Tools**: Mouse/touch simulation for Game View workflows.
- **Enhanced Log Consumption**: Cursor-based log retrieval and Play Mode runtime capture.
- **PlayerPrefs Tools**: Native get/set/delete/list operations.
- **Asset Pipeline Sync**: Wait for imports, compilation, and editor idle states.
- **UI Toolkit Automation**: Inspect/click/type in Unity Editor windows.
- **High-Value Composition Tools**: Batch execute, compact scene snapshot, scene delta, symbol index, component values, and apply-code macro.

## Internal Structure

| Path | Purpose |
|---|---|
| `Editor/MCPServer.cs` | Core server lifecycle and dispatch |
| `Editor/MCPServerMethods*.cs` | Tool implementations split by feature |
| `Editor/MCPServerWindow*.cs` | Unity dashboard UI |
| `Editor/MCPSettings.cs` | Project settings |
| `Editor/nexus_unity_bridge.py` | Python stdio bridge for direct Nexus workflows |
| `Editor/nexus_unity_bridge_mojo` | High-performance bridge binary |
| `Editor/Tests/` | Editor tests |
| `Runtime/` | Runtime package files |

## Starting The Server

1. Open a Unity project containing Nexus Unity.
2. Go to `Window > Nexus Unity`.
3. Click `START SERVER`.
4. Confirm the server is listening, usually on:

   ```text
   http://localhost:8081/
   ```

5. For Soma workflows, return to Soma and refresh the selected project status.

## Recommended Soma Workflow

1. Open Soma.
2. Select the Unity project root.
3. Start or refresh the Soma MCP gateway.
4. Install or copy client config that points Big AI to Soma.
5. Start the Nexus Unity server in Unity.
6. Run Soma live verification.
7. Let Big AI use `soma_scene`, `soma_inspect`, `soma_apply`, and `soma_delta` through Soma.

Big AI should not call raw `unity_*` tools in this workflow.

## Direct Nexus Workflow

Direct JSON-RPC remains available:

```text
http://localhost:8081/
```

Direct bridge execution:

```bash
python3 nexus_unity_bridge.py
```

Direct command execution:

```bash
python3 nexus_unity_bridge.py unity_get_server_status
python3 nexus_unity_bridge.py unity_read_logs count=20
```

Use direct Nexus only when you intentionally need raw Unity tools.

## Documentation

| Document | Purpose |
|---|---|
| `DOCUMENTATION.MD` | Technical guide and architecture |
| `API_REFERENCE.MD` | Raw `unity_*` tool reference |
| `CHANGELOG.md` | Version history |
| Root `NEXUS_UNITY_DOCS.md` | AI-facing project context copy |

## Safety Notes

- Unity APIs must run on the Unity main thread; Nexus dispatches tool work accordingly.
- File operations are project-root constrained.
- Use `apply_code_change`/Soma `soma_apply` for compile-safe write loops.
- Prefer compact snapshots and filtered component values over full dumps.
- In NexusSoma workflows, prefer Soma's compact packet and narrow tool calls before raw Nexus commands.

## License

Refer to the main project license for details.
