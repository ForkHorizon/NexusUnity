# Nexus Unity

A core library for Unity providing a built-in Model Context Protocol (MCP) server for seamless interaction with external AI tools and workflows.

**Requirements**: Unity 6000.0 or newer.

## 🚀 Features

- **Autonomous Background Workflow**: Native macOS App Nap bypass and focus-stealing synchronization for 100% zero-touch script compilation.
- **Explicit Server Health Monitoring**: Real-time tracking of main-thread responsiveness and engine state (`compiling`, `importing`, `playing`).
- **Persistent Session Management**: Unique session IDs and generation tracking that survive domain reloads for deterministic AI synchronization.
- **Hybrid Deep Auditor**: Combined static code analysis (via Project Auditor) and Nexus-native scene health scanning.
- **Unity 6 Legacy ID Compatibility**: JSON-RPC `instance_id` values now round-trip correctly across create/read/update calls while the wire protocol stays stable.
- **Intuitive Component Updates**: Supports native JSON objects and fuzzy property naming (e.g., auto-maps `sprite` to `m_Sprite`).
- **ScriptableObject Diff & Balancing**: Advanced tools to compare assets and apply surgical patches for data balancing.
- **Prefab Editing Helpers**: Open prefab stages in isolation mode, edit prefab assets on disk, and introspect overrides.
- **Strong Serialized Object Inspection**: Deep recursive serialization of Arrays, Lists, and `[SerializeReference]` types with optional detailed type metadata.
- **Runtime Gameplay Input Tools**: Robust, cross-frame simulation of Mouse and Touch events, including GameView spatial object targeting.
- **Enhanced Log Consumption**: Cursor-based log retrieval with multi-severity filtering, fully bridged to capture Play Mode runtime events.
- **PlayerPrefs Tools**: Native support for `get_player_pref`, `set_player_pref`, `delete_player_pref`, and `list_player_prefs`.
- **Asset Pipeline Sync**: Deterministic tools to wait for asset imports and editor idle states.
- **Native Wait Tools**: Built-in Python bridge tools to wait for compilation or play mode transitions.
- **Intelligent Asset Merging**: High-level `move_asset` tool that automatically merges directories.
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
1. Go to **Window > Nexus Unity > Server Control Panel** in the Unity menu.
2. Click **START SERVER** (Server will now persist through reloads).
3. Under "CLI Integrations", click **Link to Gemini CLI** or **Link to Codex CLI** to instantly connect your terminal to Unity.

### Integration
External tools can send JSON-RPC 2.0 requests to `http://localhost:8081/`. See `DOCUMENTATION.MD` for the full protocol specification.

## 📜 License
Refer to the main project license for details.
