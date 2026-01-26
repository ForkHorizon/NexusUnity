# NexusUnity

A core library for Unity providing a built-in Model Context Protocol (MCP) server for seamless interaction with external AI tools and workflows.

## 🚀 Features

- **Unity MCP Server**: An integrated HTTP-based server implementing the Model Context Protocol.
- **Main Thread Execution**: Safe execution of Unity API calls from background server threads.
- **Extensible Architecture**: Easily add new JSON-RPC methods to interact with any Unity subsystem.
- **Primitive Generation**: Built-in methods to create and manipulate GameObjects via external commands.
- **Dynamic Scripting**: Create and attach C# scripts to GameObjects on-the-fly.

## 📂 Internal Structure

- `Editor/`: Documentation and implementation of the MCP Server window and processing logic.
  - `MCPServerWindow.cs`: Manages the server lifecycle and editor UI.
  - `MCPServerMethods.cs`: Contains the core request handling and Unity API integration.
  - `UnityMCP.Editor.asmdef`: Assembly definition for the editor tools.
- `Runtime/`: Placeholder for runtime-side MCP integrations.
  - `UnityMCP.Runtime.asmdef`: Assembly definition for runtime components.

## 🚦 Usage

### Starting the Server
1. Go to **Tools > MCP Server** in the Unity menu.
2. Click **Start Server**.
3. The library will start listening on `http://localhost:8080/`.

### Integration
External tools can send JSON-RPC 2.0 requests to this endpoint to interact with the Unity Editor. See `DOCUMENTATION.MD` for the full protocol specification.

## 📦 Dependencies
- `Newtonsoft.Json`: Required for robust JSON-RPC parsing.

## 📜 License
Refer to the main project license for details.
