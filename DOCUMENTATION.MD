# Technical Documentation - NexusUnity

This document provides technical details for the `NexusUnity` library.

## 🔌 Unity MCP Server

The primary component of this library is the Model Context Protocol (MCP) server, which enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Main Thread Dispatching**: Since Unity APIs are not thread-safe, all Editor operations are queued and executed on the main thread via `EditorApplication.update`.
- **Persistence**: Uses `SessionState` to maintain server state and pending operations across assembly reloads.
- **Port**: 8080 (Default).

### JSON-RPC 2.0 Methods

#### `initialize`
- **Result**: Returns server metadata and capabilities.
```json
{ "protocolVersion": "2024-11-05", "serverInfo": { "name": "Unity MCP Server", "version": "0.0.1" } }
```

#### `create_primitive`
- **Params**: `primitive_type` (Enum string: `Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`).
- **Result**: Confirms creation and focuses the new object in the hierarchy.

#### `attach_script`
- **Params**: 
  - `script_name`: Class name for the new component.
  - `script_content` (Optional): Source code for the script.
- **Result**: Creates the `.cs` file in `Assets/` and schedules an automatic attachment to the `Selection.activeGameObject` after compilation is finished.

### Best Practices for Extension
If you wish to add new methods to the library:
1. Locate `MCPServerMethods.cs`.
2. Add the method name to the switch-case in `ExecuteMethod`.
3. Implement the method as a `static JToken` function.
4. Ensure any Unity API calls are within the `Enqueue` block if they are part of a larger asynchronous workflow.
