# NexusUnity

A core library for Unity providing a built-in Model Context Protocol (MCP) server for seamless interaction with external AI tools and workflows.

## 🚀 Features

- **Full Unity Editor Control**: 42+ JSON-RPC methods for manipulating Scenes, Assets, GameObjects, and Components.
- **AI Power Tools (v1.4.0)**: Advanced search, selection control, and prefab lifecycle management.
- **Surgical Property Editing**: Precise manipulation of single fields without full JSON state transfers.
- **Editor Automation**: Remote control for Play Mode, Undo/Redo, and Menu Commands.
- **Modular Architecture**: Clean separation of concerns using partial classes for better maintainability.
- **UI Toolkit Automation**: Inspect and interact with Unity Editor windows and visual elements.

## 📂 Internal Structure

- `Editor/`: Modularized implementation of the MCP Server and processing logic.
  - `MCPServerWindow*.cs`: Partial classes managing the server lifecycle, UI, and log capturing.
  - `MCPServerMethods*.cs`: Partial classes containing the core request handling (Scene, Asset, UI manipulation).
  - `UnityMCP.Editor.asmdef`: Assembly definition with required dependencies (EditorCoroutines, Newtonsoft.Json).
- `Runtime/`: Core runtime components for the library.

## 🚦 Usage

### Starting the Server
1. Go to **Window > Unity MCP > Server** in the Unity menu.
2. Click **Start Server**.
3. The library will start listening on `http://localhost:8081/`. (Default port 8081).

### Integration
External tools can send JSON-RPC 2.0 requests to this endpoint to interact with the Unity Editor. See `DOCUMENTATION.MD` for the full protocol specification.

## 📦 Dependencies
- `Newtonsoft.Json`: Required for robust JSON-RPC parsing.

## 📜 License
Refer to the main project license for details.
