# Nexus Unity

A core library for Unity providing a built-in Model Context Protocol (MCP) server for seamless interaction with external AI tools and workflows.

## 🚀 Features

- **Full Unity Editor Control**: 59+ JSON-RPC methods for manipulating Scenes, Assets, GameObjects, and Components.
- **Self-Healing Server**: Automatically restarts after Unity domain reloads (compilation) or network errors.
- **Gemini CLI Native**: One-click integration with the Gemini CLI via the unified dashboard.
- **Non-Blocking Logic**: Automated scene saving and non-interactive workflows to prevent Editor stalls.
- **Surgical Property Editing**: Precise manipulation of fields using SerializedObjects traversal.
- **UI Toolkit Automation**: Inspect and interact with Unity Editor windows and visual elements.

## 📂 Internal Structure

- `Editor/`: Modularized implementation of the MCP Server.
  - `MCPServerWindow*.cs`: Unified tabbed dashboard for server and tool control.
  - `MCPServerMethods*.cs`: core request handlers categorized by feature.
  - `MCPCliInstaller.cs`: Cross-platform link logic for Gemini CLI.
  - `nexus_unity_bridge.py`: Standard MCP stdio translator.
- `Runtime/`: core runtime components.

## 🚦 Usage

### Quick Start
1. Go to **Window > Nexus Unity** in the Unity menu.
2. Click **Start Server** (Server will now persist through reloads).
3. Click **Link to Gemini CLI** to instantly connect your terminal to Unity.

### Integration
External tools can send JSON-RPC 2.0 requests to `http://localhost:8081/`. See `DOCUMENTATION.MD` for the full protocol specification.

## 📜 License
Refer to the main project license for details.