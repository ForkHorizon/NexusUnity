using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling Core system and tool listing.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterCoreMethods()
        {
            _methods["initialize"] = Initialize;
            _methods["read_logs"] = ReadLogs;
            _methods["clear_logs"] = ClearLogs;
            _methods["test_coroutine"] = TestCoroutine;
            _methods["list_tools"] = ListTools;
            _methods["wait_for_ready"] = WaitForReady;
            _methods["create_primitive"] = CreatePrimitive;
            _methods["attach_script"] = AttachScript;
        }

        private static JToken Initialize(JToken p) => new JObject { ["protocolVersion"] = "2024-11-05", ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = MCPServer.Version } };

        private static JToken WaitForReady(JToken p)
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        private static JToken CreatePrimitive(JToken p)
        {
            if (!Enum.TryParse(typeof(PrimitiveType), p["primitive_type"].ToString(), true, out var type)) throw new Exception("Invalid primitive");
            var go = GameObject.CreatePrimitive((PrimitiveType)type);
            Undo.RegisterCreatedObjectUndo(go, "Create Primitive");
            Selection.activeGameObject = go;
            return new JObject { ["status"] = "Success", ["data"] = SerializeGameObject(go) };
        }

        private static JToken AttachScript(JToken p)
        {
            string name = SanitizeScriptName(p["script_name"].ToString());
            string content = p["script_content"]?.ToString().Replace("\n", "\n") ?? GetDefaultScript(name);
            File.WriteAllText(Path.Combine("Assets", $"{name}.cs"), content);
            if (Selection.activeGameObject != null)
            {
                SessionState.SetString("MCP_PendingAttach_Script", name);
                SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetRawId());
            }
            AssetDatabase.Refresh();
            return new JObject { ["status"] = "Success", ["message"] = "Script created and compilation triggered" };
        }

        private static JToken ReadLogs(JToken p) => new JObject { ["logs"] = JArray.FromObject(MCPServer.GetLogs((int)(p?["count"] ?? 50), p?["filter_type"]?.ToString(), p?["search_text"]?.ToString())) };
        private static JToken ClearLogs(JToken p) { MCPServer.ClearLogs(); return new JObject { ["status"] = "Success", ["message"] = "Logs cleared" }; }
        private static JToken TestCoroutine(JToken p) { EditorApplication.delayCall += () => Debug.Log("[MCP] Delay call complete"); return new JObject { ["status"] = "Success", ["message"] = "Started" }; }

        // Cache the tool definitions since they are static and do not change during the session.
        // This avoids creating thousands of JObjects every time 'list_tools' is called.
        private static JToken _cachedTools;

        /// <summary>Lists all available tools for the MCP server.</summary>
        private static JToken ListTools(JToken p)
        {
            // Return cached version. Note: The returned JToken is shared and must NOT be modified by the caller.
            if (_cachedTools != null) return _cachedTools;

            var tools = new JArray();
            AddSceneTools(tools);
            AddHierarchyTools(tools);
            AddComponentTools(tools);
            AddAssetTools(tools);
            AddEditorControlTools(tools);
            AddDiscoveryTools(tools);
            AddUITools(tools);
            AddSerializationTools(tools);
            AddLinterTools(tools);
            AddHighValueTools(tools);

            _cachedTools = tools;
            return tools;
        }

        private static void AddHighValueTools(JArray tools)
        {
            tools.Add(CreateTool("capture_inspector_screenshot", "Capture PNG of Inspector (macOS only)", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }));
            tools.Add(CreateTool("capture_game_view_screenshot", "Capture PNG of Game View", new JObject { }));
            tools.Add(CreateTool("generate_mermaid_diagram", "Generate Mermaid diagram of scene", new JObject { }));
            tools.Add(CreateTool("semantic_find", "Find objects by semantic meaning", new JObject { ["query"] = new JObject { ["type"] = "string" } }, "query"));
        }

        private static void AddSerializationTools(JArray tools)
        {
            tools.Add(CreateTool("enforce_forced_defaults", "Enforce [ForceDefault] attributes", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
        }

        private static void AddLinterTools(JArray tools)
        {
            tools.Add(CreateTool("lint_project", "Run deterministic C# audit", new JObject { }));
        }

        private static void AddSceneTools(JArray tools)
        {
            tools.Add(CreateTool("create_scene", "Create new scene", new JObject { ["name"] = new JObject { ["type"] = "string" } }));
            tools.Add(CreateTool("open_scene", "Open scene file", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("save_scene", "Save active scene", new JObject { ["path"] = new JObject { ["type"] = "string" } }));
            tools.Add(CreateTool("list_scenes", "List all scene files", new JObject { }));
        }

        private static void AddHierarchyTools(JArray tools)
        {
            tools.Add(CreateTool("create_game_object", "Create empty GameObject", new JObject { ["name"] = new JObject { ["type"] = "string" }, ["parent_id"] = new JObject { ["type"] = "integer" } }, "name"));
            tools.Add(CreateTool("create_primitive", "Create primitive (Cube, Sphere...)", GetPrimitiveSchema()));
            tools.Add(CreateTool("destroy_game_object", "Delete GameObject", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("duplicate_object", "Copy GameObject", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("set_active", "Enable/Disable GameObject", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["active"] = new JObject { ["type"] = "boolean" } }, "instance_id", "active"));
            tools.Add(CreateTool("set_parent", "Parent an object", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["parent_id"] = new JObject { ["type"] = "integer" } }, "instance_id", "parent_id"));
            tools.Add(CreateTool("set_sibling_index", "Reorder in hierarchy", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["index"] = new JObject { ["type"] = "string", ["description"] = "int or 'first' or 'last'" } }, "instance_id", "index"));
            tools.Add(CreateTool("create_hierarchy", "Batch create full hierarchy", new JObject { ["tree"] = new JObject { ["type"] = "object" }, ["parent_id"] = new JObject { ["type"] = "integer" } }, "tree"));
        }

        private static void AddComponentTools(JArray tools)
        {
            tools.Add(CreateTool("add_component", "Add component", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("remove_component", "Remove component", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("inspect_component", "Get properties and values", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("get_component_schema", "Get serializable fields names/types", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("update_component", "Update component with detailed result", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" }, ["json_data"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name", "json_data"));
            tools.Add(CreateTool("set_transform", "Move/Rotate", GetTransformSchema()));
            tools.Add(CreateTool("set_property", "Surgical field edit", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["property_name"] = new JObject { ["type"] = "string" }, ["value"] = new JObject { ["type"] = "string" } }, "instance_id", "property_name", "value"));
            tools.Add(CreateTool("set_enabled", "Enable/Disable Component", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" }, ["enabled"] = new JObject { ["type"] = "boolean" } }, "instance_id", "component_name", "enabled"));
        }

        private static void AddAssetTools(JArray tools)
        {
            tools.Add(CreateTool("list_assets", "List assets", new JObject { ["filter"] = new JObject { ["type"] = "string" } }));
            tools.Add(CreateTool("explore_asset", "List all internal sub-assets (e.g., sliced sprites) and their fileIDs within a single file.", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_material", "Create material", new JObject { ["name"] = new JObject { ["type"] = "string" }, ["shader"] = new JObject { ["type"] = "string" } }, "name"));
            tools.Add(CreateTool("refresh_asset_database", "Refresh Assets", new JObject { }));
            tools.Add(CreateTool("import_asset", "Import file", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("instantiate_prefab", "Create from Prefab", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_prefab", "Save as Prefab", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["path"] = new JObject { ["type"] = "string" } }, "instance_id", "path"));
            tools.Add(CreateTool("apply_prefab_overrides", "Apply changes", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("revert_prefab_overrides", "Revert changes", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("move_asset", "Move/Rename", new JObject { ["old_path"] = new JObject { ["type"] = "string" }, ["new_path"] = new JObject { ["type"] = "string" } }, "old_path", "new_path"));
            tools.Add(CreateTool("delete_asset", "Delete file", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("copy_asset", "Duplicate file", new JObject { ["source_path"] = new JObject { ["type"] = "string" }, ["dest_path"] = new JObject { ["type"] = "string" } }, "source_path", "dest_path"));
            tools.Add(CreateTool("get_dependencies", "Get file deps", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_folder", "Create directory", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("read_file", "Read text content", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("write_file", "Write text content", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["content"] = new JObject { ["type"] = "string" } }, "path", "content"));
            tools.Add(CreateTool("write_files_batch", "Write multiple files in a single pass", new JObject { ["files"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["path"] = new JObject { ["type"] = "string" }, ["content"] = new JObject { ["type"] = "string" } }, ["required"] = new JArray { "path", "content" } } } }, "files"));
        }

        private static void AddEditorControlTools(JArray tools)
        {
            tools.Add(CreateTool("undo", "Unity Undo", new JObject { }));
            tools.Add(CreateTool("redo", "Unity Redo", new JObject { }));
            tools.Add(CreateTool("toggle_play_mode", "Start/Stop", new JObject { ["value"] = new JObject { ["type"] = "boolean" } }));
            tools.Add(CreateTool("pause_play_mode", "Pause/Unpause", new JObject { ["value"] = new JObject { ["type"] = "boolean" } }));
            tools.Add(CreateTool("step_frame", "Advance frame", new JObject { }));
            tools.Add(CreateTool("execute_menu_item", "Execute Menu", new JObject { ["item_path"] = new JObject { ["type"] = "string" } }, "item_path"));
            tools.Add(CreateTool("focus_scene_view", "Frame selection", new JObject { }));
            tools.Add(CreateTool("read_logs", "Get Console", new JObject { ["count"] = new JObject { ["type"] = "integer" } }));
            tools.Add(CreateTool("clear_logs", "Clear Console", new JObject { }));
            tools.Add(CreateTool("attach_script", "Create & Link C#", new JObject { ["script_name"] = new JObject { ["type"] = "string" }, ["script_content"] = new JObject { ["type"] = "string" } }, "script_name"));
            tools.Add(CreateTool("wait_for_ready", "Wait until server is responsive", new JObject { }));
        }

        private static void AddDiscoveryTools(JArray tools)
        {
            tools.Add(CreateTool("get_game_object", "Get object data", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("get_active_game_object", "Get current selection", new JObject { }));
            tools.Add(CreateTool("get_root_game_objects", "Get top-level objects", new JObject { }));
            tools.Add(CreateTool("get_object_path", "Get hierarchy breadcrumb", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("find_objects", "Deep search", GetSearchSchema()));
            tools.Add(CreateTool("find_by_path", "Search by exact path", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("get_tags_and_layers", "Get Tags/Layers list", new JObject { }));
            tools.Add(CreateTool("ping_object", "Ping in Editor", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("get_children", "Get direct children", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("get_editor_state", "Get Play/Paused/Compiling", new JObject { }));
            tools.Add(CreateTool("get_project_info", "Get Project metadata", new JObject { }));
            tools.Add(CreateTool("set_selection", "Select objects", new JObject { ["instance_ids"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "integer" } } }, "instance_ids"));
        }

        private static void AddUITools(JArray tools)
        {
            tools.Add(CreateTool("ui_list_windows", "List Editor Windows", new JObject { }));
            tools.Add(CreateTool("ui_get_hierarchy", "Inspect Window UI", new JObject { ["window_title"] = new JObject { ["type"] = "string" } }, "window_title"));
            tools.Add(CreateTool("ui_click", "Simulate UI Click", new JObject { ["window_title"] = new JObject { ["type"] = "string" }, ["element_name"] = new JObject { ["type"] = "string" } }, "window_title", "element_name"));
            tools.Add(CreateTool("ui_input_text", "Type into UI field", new JObject { ["window_title"] = new JObject { ["type"] = "string" }, ["element_name"] = new JObject { ["type"] = "string" }, ["text"] = new JObject { ["type"] = "string" } }, "window_title", "element_name", "text"));
        }

        private static JObject CreateTool(string name, string desc, JObject props, params string[] required)
        {
            var tool = new JObject { ["name"] = name, ["description"] = desc };
            var schema = new JObject { ["type"] = "object", ["properties"] = props };
            if (required != null && required.Length > 0) schema["required"] = new JArray(required);
            tool["inputSchema"] = schema;
            return tool;
        }

        private static JObject GetPrimitiveSchema()
        {
            var schema = new JObject();
            var types = new JArray("Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad");
            schema["primitive_type"] = new JObject { ["type"] = "string", ["enum"] = types };
            return schema;
        }
        private static JObject GetSearchSchema() => new JObject { ["name"] = new JObject { ["type"] = "string" }, ["tag"] = new JObject { ["type"] = "string" }, ["type"] = new JObject { ["type"] = "string" } };
        private static JObject GetTransformSchema() => new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["position"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" }, ["z"] = new JObject { ["type"] = "number" } } } };

        private static string SanitizeScriptName(string n) => System.Text.RegularExpressions.Regex.Replace(n, @"[^a-zA-Z0-9_]", "_");
        private static string GetDefaultScript(string n) => $"using UnityEngine;\npublic class {n} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {n}\"); }} }}";
    }
}
