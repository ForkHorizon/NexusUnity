# Nexus Unity

A core library for Unity providing a built-in Model Context Protocol (MCP) server for seamless interaction with external AI tools and workflows.

**Requirements**: Unity 6000.0 or newer.

## 🚀 Features

- **Autonomous Background Workflow**: Native macOS App Nap bypass and focus-stealing synchronization for 100% zero-touch script compilation.
- **Hybrid Deep Auditor**: Combined static code analysis (via Project Auditor) and Nexus-native scene health scanning.
- **Intuitive Component Updates**: Supports native JSON objects and fuzzy property naming (e.g., auto-maps `sprite` to `m_Sprite`).
- **Standardized Return Payloads**: Consistent `JObject` returns across all 61+ tools for predictable AI error handling.
- **Native Wait Tools**: Built-in Python bridge tools to wait for compilation or play mode transitions.
- **Intelligent Asset Merging**: High-level `move_asset` tool that automatically merges directories.
- **Surgical Property Editing**: Deep recursive serialization of Arrays, Lists, and Generic structs.
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