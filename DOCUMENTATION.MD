# Technical Documentation - Nexus Unity

This document provides a comprehensive reference for the `Nexus Unity` library.

**Requirements**: Unity 6000.0 or newer.

## Overview

Nexus Unity is a bridge for deep Unity Editor control. It allows AI agents and external tools to interact with the Unity Editor using the Model Context Protocol (MCP).

## 🔌 Unity MCP Server

The library implements a Model Context Protocol (MCP) server that enables external tools to interact with the Unity Editor via JSON-RPC 2.0.

### Architecture
- **Server**: Runs on a background thread using `System.Net.HttpListener`.
- **Self-Healing**: Automatically restarts the server loop if a network crash occurs.
- **Persistence**: Uses `SessionState` and `[InitializeOnLoad]` to survive Unity domain reloads (compilation) autonomously.
- **Main Thread Dispatching**: Unity APIs are not thread-safe; all operations are executed on the main thread via a thread-safe queue.
- **Port**: 8081 (Default). Configurable via Project Settings or CLI.

---

## 🐍 Python Bridge (`nexus_unity_bridge.py`)

The Python bridge is located in the project root. It handles communication between external AI tools (like Gemini, Claude, or Codex) and the Unity Editor.

### 1. MCP Mode (Default)
When run without arguments, the bridge acts as a persistent JSON-RPC 2.0 translator.
```bash
python3 nexus_unity_bridge.py
```

### 2. CLI Mode
Execute any Unity tool directly from the command line:
```bash
python3 nexus_unity_bridge.py unity_refresh_asset_database
```
Parameters can be passed as `key=value` pairs:
```bash
python3 nexus_unity_bridge.py unity_read_logs count=10
```

---

## 🛠 Consolidated Tool Architecture

To maximize performance for Large Language Models (LLMs), Nexus Unity consolidates granular operations into high-level "Managers".

### Scene Management (`unity_scene_manager`)
Manages scene transitions (create, open, save, list).
- **Note**: Automatically saves the current scene to `Assets/AutoSavedScene.unity` if it has unsaved changes.

### Hierarchy Management (`unity_hierarchy_manager`)
Handles GameObject lifecycle: creating primitives, empty objects, destroying, duplicating, and reparenting.

### Component Management (`unity_component_manager`)
A powerful tool for adding/removing components and updating properties.
- **Recursive Unpacking**: `inspect` action returns nested JSON for Arrays, Lists, and Structs.
- **Fuzzy Naming**: Automatically resolves AI-friendly names (e.g., `sprite`) to internal fields (e.g., `m_Sprite`).
- **Advanced Referencing**: Supports setting object references via `instance_id`, asset path, or GUID.

### Wait Controller (`unity_wait`)
Blocks the agent until Unity reaches a specific state (compilation finished, entered play mode, asset import idle, or editor idle).

---

## 🔍 Discovery & Analysis

### Advanced Inspection
- `unity_find_references`: Find which assets or scene objects reference a specific target.
- `unity_semantic_find`: Locate objects by intent-based search.
- `unity_dump_scene_graph`: Generate a recursive tree of the active scene.

### UI Toolkit Automation
Inspect and interact with any Editor Window's VisualElement tree using `unity_ui_get_hierarchy`, `unity_ui_click`, and `unity_ui_input_text`.

---

## ⚡ Workflow Optimization

### `unity_apply_code_change`
The primary tool for C# development. It performs a "Write -> Wait -> Check" loop in a single turn, ensuring the AI agent receives compiler feedback immediately.

---

## ⚙️ Configuration

### Project Settings
Go to **Edit > Project Settings > Nexus Unity** to configure the port.

### Server Control Panel
Open **Window > Nexus Unity > Server Control Panel** to start or stop the server manually and view logs.
