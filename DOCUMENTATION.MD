# Technical Documentation - Nexus Unity

This document provides a comprehensive reference for the `Nexus Unity` library.

## 🔌 Unity MCP Server

The library implements a Model Context Protocol (MCP) server that enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Self-Healing**: Automatically restarts the server loop if a network crash occurs.
- **Persistence**: Uses `SessionState` and `[InitializeOnLoad]` to survive Unity domain reloads (compilation) autonomously.
- **Main Thread Dispatching**: Unity APIs are not thread-safe; all operations are executed on the main thread via a thread-safe queue.
- **Port**: 8081 (Default). Configurable via Project Settings or CLI.

---

## 🛠 JSON-RPC 2.0 API Reference

### Core & System

#### `initialize`
Returns server capabilities and metadata.
- **Result**: `{ "protocolVersion": "2024-11-05", "serverInfo": { "name": "Unity MCP Server", "version": "1.8.7" } }`

#### `list_tools`
Dynamically returns JSON schemas for all available tools. Used by the Python bridge for LLM discovery.

#### `read_logs`
Retrieves captured console logs.
- **Params**:
  - `count` (int, optional): Max logs to return.
- **Result**: Array of `LogEntry` objects.

---

### Scene & GameObject Lifecycle

#### `open_scene` / `create_scene`
Manages scene transitions.
- **Note**: Automatically saves the current scene to `Assets/AutoSavedScene.unity` if it has unsaved changes, preventing blocking dialogs.

#### `save_scene`
Saves the currently active scene.
- **Params**: `path` (string, optional): Specific save location. If empty, saves to current path (or default for new scenes).

#### `create_primitive`
Creates a standard Unity primitive.
- **Result**: Returns the full serialized GameObject (Name and InstanceID).

---

### Transforms & Components

#### `inspect_component`
Returns serialized state of any component using `SerializedObject`.
- **Note**: Safe for internal engine handles (won't crash on Transforms/Cameras).

#### `update_component`
Updates component values using `EditorJsonUtility.FromJsonOverwrite`.

#### `set_property`
Surgical update of a single property (bool, int, float, string, Vector3).

---

### UI Toolkit Automation

#### `ui_get_hierarchy`
Generates a JSON tree of all `VisualElement` objects in a window.

#### `ui_click` / `ui_input_text`
Simulates interaction with Editor Window elements.

---

## ⚙️ Configuration & Tooling

### Project Settings
Go to **Edit > Project Settings > Nexus Unity** to configure the port.

### Integration
1. Open **Window > Nexus Unity**.
2. Click **Link to Gemini CLI**.
3. Your local Gemini CLI is now connected to this Unity instance with all 59+ tools.