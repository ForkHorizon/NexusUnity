using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken _cachedTools;

        internal static void ClearCache()
        {
            _cachedTools = null;
            _typeCache.Clear();
            _typeNameCache.Clear();
        }

        /// <summary>Lists all available tools for the MCP server.</summary>
        private static JToken ListTools(JToken p)
        {
            // Return cached version. Note: The returned JToken is shared and must NOT be modified by the caller.
            if (_cachedTools != null) return _cachedTools;

            var tools = new JArray();
            AddServerHealthTools(tools);
            AddSceneTools(tools);
            AddHierarchyTools(tools);
            AddComponentTools(tools);
            AddAssetTools(tools);
            AddEditorControlTools(tools);
            AddDiscoveryTools(tools);
            AddReflectionTools(tools);
            AddUITools(tools);
            AddSerializationTools(tools);
            AddLinterTools(tools);
            AddHighValueTools(tools);
            AddPlayerPrefsTools(tools);
            AddScriptableObjectTools(tools);
            AddSyncTools(tools);
            AddInputTools(tools);
            AddSnapshotTools(tools);
            AddTimelineTools(tools);
            AddContextTools(tools);
            AddDeltaTools(tools);

            _cachedTools = tools;
            return tools;
        }

        private static void AddContextTools(JArray tools)
        {
            tools.Add(CreateTool("get_selected_object_full_context", "Returns a massive, context-rich JSON payload for the currently selected GameObject (including all serialized components, prefab status, and hierarchy path)", new JObject { }));
            tools.Add(CreateTool("show_unresolved_missing_references", "Scans the active scene for 'Missing Script' components or broken ObjectReferences", new JObject { }));
        }

        private static void AddTimelineTools(JArray tools)
        {
            tools.Add(CreateTool("get_editor_timeline", "Returns a list of recent Editor actions (imports, scene changes, play mode transitions)", new JObject { }));
        }

        private static void AddSnapshotTools(JArray tools)
        {
            tools.Add(CreateTool("dump_scene_graph", "Dump a recursive tree of the active scene with components and key fields", new JObject
            {
                ["root_id"] = new JObject { ["type"] = "integer", ["description"] = "Optional: instance_id of the root object to start from" },
                ["max_depth"] = new JObject { ["type"] = "integer", ["description"] = "Maximum recursion depth (default: 5)" },
                ["include_all_properties"] = new JObject { ["type"] = "boolean", ["description"] = "If true, serializes all component properties instead of just key fields" },
                ["compact"] = new JObject { ["type"] = "boolean", ["description"] = "If true, only include name, instance_id, and component types (no properties)" }
            }));

            tools.Add(CreateTool("compact_scene_snapshot", "Get a highly compressed hierarchy (name/id/component list only) for fast full-scene overview", new JObject
            {
                ["root_id"] = new JObject { ["type"] = "integer", ["description"] = "Optional: instance_id of the root object to start from" },
                ["max_depth"] = new JObject { ["type"] = "integer", ["description"] = "Maximum recursion depth (default: 5)" }
            }));

            tools.Add(CreateTool("get_scene_dependencies", "Scans the scene for cross-object references and returns a dependency map", new JObject { }));
        }

        private static void AddServerHealthTools(JArray tools)
        {
            tools.Add(CreateTool("get_server_status", "Get explicit health and state of the MCP server and Unity editor", new JObject { }));
            tools.Add(CreateTool("attach_existing_session", "Attach to an existing healthy session", new JObject { }));
            tools.Add(CreateTool("ping_main_thread", "Explicit liveness check for Unity API execution on main thread", new JObject { }));
            tools.Add(CreateTool("get_tool_usage_stats", "Return in-memory raw tool call counts, durations, and errors since domain load", new JObject { }));
            tools.Add(CreateTool("reset_tool_usage_stats", "Clear in-memory raw tool call counts for scoped diagnostics", new JObject { }));
            tools.Add(CreateTool("shutdown_server", "Safely stop the MCP server for this Unity instance", new JObject { }));
            tools.Add(CreateTool("batch_execute", "Execute multiple JSON-RPC calls in a single HTTP request", new JObject
            {
                ["requests"] = new JObject
                {
                    ["type"] = "array",
                    ["maxItems"] = MaxBatchExecuteRequests,
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["method"] = new JObject { ["type"] = "string" },
                            ["params"] = new JObject { ["type"] = "object" }
                        },
                        ["required"] = new JArray("method")
                    }
                }
            }, "requests"));
        }

        private static void AddInputTools(JArray tools)
        {
            tools.Add(CreateTool("simulate_mouse", "Simulate mouse input in Play Mode", new JObject
            {
                ["action"] = new JObject { ["type"] = "string", ["description"] = "move, press, release, click" },
                ["x"] = new JObject { ["type"] = "number" },
                ["y"] = new JObject { ["type"] = "number" },
                ["normalized"] = new JObject { ["type"] = "boolean", ["description"] = "If true, x/y are 0-1" },
                ["button"] = new JObject { ["type"] = "integer", ["description"] = "0: Left, 1: Right, 2: Middle" }
            }));
            tools.Add(CreateTool("simulate_touch", "Simulate touch input in Play Mode", new JObject
            {
                ["action"] = new JObject { ["type"] = "string", ["description"] = "press, move, release" },
                ["x"] = new JObject { ["type"] = "number" },
                ["y"] = new JObject { ["type"] = "number" },
                ["normalized"] = new JObject { ["type"] = "boolean" },
                ["id"] = new JObject { ["type"] = "integer", ["description"] = "Touch ID" }
            }));
            tools.Add(CreateTool("click_object_in_game", "Click a GameObject in the Game View", new JObject
            {
                ["instance_id"] = new JObject { ["type"] = "integer" },
                ["path"] = new JObject { ["type"] = "string" }
            }));
        }

        private static void AddSyncTools(JArray tools)
        {
            tools.Add(CreateTool("wait_for_asset_import_idle", "Wait until Unity is done importing assets", new JObject { ["timeout_seconds"] = new JObject { ["type"] = "integer" } }));
            tools.Add(CreateTool("wait_for_editor_idle", "Wait until the editor is fully idle (not compiling, not importing, no background tasks)", new JObject { ["timeout_seconds"] = new JObject { ["type"] = "integer" } }));
        }

        private static void AddScriptableObjectTools(JArray tools)
        {
            tools.Add(CreateTool("read_scriptable_object", "Read ScriptableObject properties", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["instance_id"] = new JObject { ["type"] = "integer" } }));
            tools.Add(CreateTool("update_scriptable_object", "Update ScriptableObject properties", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["instance_id"] = new JObject { ["type"] = "integer" }, ["properties"] = new JObject { ["type"] = "object" } }, "properties"));
            tools.Add(CreateTool("patch_scriptable_object", "Surgically update ScriptableObject fields (alias for update_scriptable_object)", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["instance_id"] = new JObject { ["type"] = "integer" }, ["properties"] = new JObject { ["type"] = "object" } }, "properties"));
            tools.Add(CreateTool("create_scriptable_object_asset", "Create new ScriptableObject asset", new JObject { ["type"] = new JObject { ["type"] = "string" }, ["path"] = new JObject { ["type"] = "string" } }, "type", "path"));
            tools.Add(CreateTool("duplicate_scriptable_object_asset", "Duplicate ScriptableObject asset", new JObject { ["source_path"] = new JObject { ["type"] = "string" }, ["dest_path"] = new JObject { ["type"] = "string" } }, "source_path", "dest_path"));
            tools.Add(CreateTool("list_fields_for_type", "List serializable fields for a type", new JObject { ["type"] = new JObject { ["type"] = "string" } }, "type"));
            tools.Add(CreateTool("diff_scriptable_objects", "Compare two ScriptableObject assets", new JObject
            {
                ["path_a"] = new JObject { ["type"] = "string" },
                ["path_b"] = new JObject { ["type"] = "string" },
                ["instance_id_a"] = new JObject { ["type"] = "integer" },
                ["instance_id_b"] = new JObject { ["type"] = "integer" }
            }));
            tools.Add(CreateTool("diff_scriptable_object_against_defaults", "Compare an asset against its default state", new JObject
            {
                ["path"] = new JObject { ["type"] = "string" },
                ["instance_id"] = new JObject { ["type"] = "integer" }
            }));
        }

        private static void AddPlayerPrefsTools(JArray tools)
        {
            tools.Add(CreateTool("get_player_pref", "Get PlayerPref value", new JObject { ["key"] = new JObject { ["type"] = "string" }, ["type"] = new JObject { ["type"] = "string", ["description"] = "int, float, string" }, ["default"] = new JObject { ["type"] = "any" } }, "key"));
            tools.Add(CreateTool("set_player_pref", "Set PlayerPref value", new JObject { ["key"] = new JObject { ["type"] = "string" }, ["value"] = new JObject { ["type"] = "any" }, ["type"] = new JObject { ["type"] = "string", ["description"] = "int, float, string" } }, "key", "value"));
            tools.Add(CreateTool("delete_player_pref", "Delete PlayerPref key, or delete all with explicit confirmation", new JObject { ["key"] = new JObject { ["type"] = "string", ["description"] = "Specific key, or 'all' only with confirm: true" }, ["confirm"] = new JObject { ["type"] = "boolean", ["description"] = "Required when key is 'all'; PlayerPrefs deletion is not undoable" } }, "key"));
            tools.Add(CreateTool("list_player_prefs", "List all PlayerPref keys and values", new JObject { }));
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
            tools.Add(CreateTool("inspect_object", "Universal inspector for ANY Unity object (Material, Texture, Mesh, ScriptableObject, etc.)", new JObject
            {
                ["instance_id"] = new JObject { ["type"] = "integer" },
                ["detailed"] = new JObject { ["type"] = "boolean", ["description"] = "If true, returns values with type metadata" }
            }, "instance_id"));
        }

        private static void AddLinterTools(JArray tools)
        {
            tools.Add(CreateTool("lint_project", "Run deterministic C# audit", new JObject { }));
        }

        private static void AddSceneTools(JArray tools)
        {
            tools.Add(CreateTool("create_scene", "Create new scene", new JObject
            {
                ["name"] = new JObject { ["type"] = "string" },
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Optional scene asset path, for example Assets/Scenes/Showcase.unity" },
                ["open_if_exists"] = new JObject { ["type"] = "boolean", ["description"] = "Open path instead of creating if the scene already exists" }
            }));
            tools.Add(CreateTool("open_scene", "Open scene file", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("save_scene", "Save active scene", new JObject { ["path"] = new JObject { ["type"] = "string" } }));
            tools.Add(CreateTool("list_scenes", "List all scene files", new JObject { }));
        }

        private static void AddHierarchyTools(JArray tools)
        {
            tools.Add(CreateTool("create_game_object", "Create empty GameObject", new JObject { ["name"] = new JObject { ["type"] = "string" }, ["parent_id"] = new JObject { ["type"] = "integer" } }, "name"));
            tools.Add(CreateTool("create_primitive", "Create primitive (Cube, Sphere...)", GetPrimitiveSchema(), "primitive_type"));
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
            tools.Add(CreateTool("inspect_component", "Get properties and values", new JObject
            {
                ["instance_id"] = new JObject { ["type"] = "integer" },
                ["component_name"] = new JObject { ["type"] = "string" },
                ["fields"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" }, ["description"] = "Optional: only return these specific fields" },
                ["detailed"] = new JObject { ["type"] = "boolean", ["description"] = "If true, returns values with type metadata" }
            }, "instance_id", "component_name"));
            tools.Add(CreateTool("component_values", "Surgically read specific component fields as a clean key-value object", new JObject
            {
                ["instance_id"] = new JObject { ["type"] = "integer" },
                ["component_name"] = new JObject { ["type"] = "string" },
                ["fields"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" }, ["description"] = "Array of field names to read" }
            }, "instance_id", "component_name", "fields"));
            tools.Add(CreateTool("get_component_schema", "Get serializable fields names/types", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("update_component", "Update component with detailed result", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" }, ["properties"] = new JObject { ["type"] = "object", ["description"] = "Preferred object form for property updates" }, ["json_data"] = new JObject { ["type"] = "string", ["description"] = "Legacy JSON string form" } }, "instance_id", "component_name"));
            tools.Add(CreateTool("set_transform", "Move/Rotate", GetTransformSchema(), "instance_id"));
            tools.Add(CreateTool("set_property", "Surgical field edit", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["property_name"] = new JObject { ["type"] = "string" }, ["value"] = new JObject { ["type"] = "string" } }, "instance_id", "property_name", "value"));
            tools.Add(CreateTool("set_enabled", "Enable/Disable Component", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["component_name"] = new JObject { ["type"] = "string" }, ["enabled"] = new JObject { ["type"] = "boolean" } }, "instance_id", "component_name", "enabled"));
        }

        private static void AddAssetTools(JArray tools)
        {
            tools.Add(CreateTool("list_assets", "List assets", new JObject { ["filter"] = new JObject { ["type"] = "string" } }));
            tools.Add(CreateTool("explore_asset", "List all internal sub-assets (e.g., sliced sprites) and their fileIDs within a single file.", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_material", "Create material", new JObject
            {
                ["name"] = new JObject { ["type"] = "string" },
                ["path"] = new JObject { ["type"] = "string", ["description"] = "Optional explicit asset path, for example Assets/Folder/Material.mat" },
                ["shader"] = new JObject { ["type"] = "string" },
                ["base_color"] = new JObject { ["type"] = "string", ["description"] = "Optional hex color, for example #00CCFFFF" },
                ["color"] = new JObject { ["type"] = "string", ["description"] = "Alias for base_color" },
                ["emission_color"] = new JObject { ["type"] = "string", ["description"] = "Optional emission hex color" }
            }, "name"));
            tools.Add(CreateTool("refresh_asset_database", "Refresh Assets", new JObject { }));
            tools.Add(CreateTool("import_asset", "Import file", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("instantiate_prefab", "Create from Prefab", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_prefab", "Save as Prefab", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["path"] = new JObject { ["type"] = "string" } }, "instance_id", "path"));
            tools.Add(CreateTool("apply_prefab_overrides", "Apply changes to prefab asset", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("revert_prefab_overrides", "Revert scene changes to prefab defaults", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("get_prefab_overrides", "Get list of property and component modifications on a prefab instance", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("edit_prefab_asset", "Directly modify a prefab asset on disk without instantiating it", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["component_name"] = new JObject { ["type"] = "string", ["description"] = "Optional component name to target" }, ["properties"] = new JObject { ["type"] = "object" } }, "path", "properties"));
            tools.Add(CreateTool("move_asset", "Move/Rename", new JObject { ["old_path"] = new JObject { ["type"] = "string" }, ["new_path"] = new JObject { ["type"] = "string" } }, "old_path", "new_path"));
            tools.Add(CreateTool("delete_asset", "Delete file or folder (moves to OS trash)", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["confirm"] = new JObject { ["type"] = "boolean", ["description"] = "Required: must be true to confirm asset deletion" } }, "path", "confirm"));
            tools.Add(CreateTool("copy_asset", "Duplicate file", new JObject { ["source_path"] = new JObject { ["type"] = "string" }, ["dest_path"] = new JObject { ["type"] = "string" } }, "source_path", "dest_path"));
            tools.Add(CreateTool("get_dependencies", "Get file deps", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("create_folder", "Create directory", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("read_file", "Read text content", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("write_file", "Write text content", new JObject { ["path"] = new JObject { ["type"] = "string" }, ["content"] = new JObject { ["type"] = "string" }, ["confirm"] = new JObject { ["type"] = "boolean", ["description"] = "Required when writing .cs files because Unity compilation is triggered" } }, "path", "content"));
            tools.Add(CreateTool("write_files_batch", "Write multiple files in a single pass", new JObject { ["files"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["path"] = new JObject { ["type"] = "string" }, ["content"] = new JObject { ["type"] = "string" } }, ["required"] = new JArray { "path", "content" } } }, ["confirm"] = new JObject { ["type"] = "boolean", ["description"] = "Required when any file path ends in .cs because Unity compilation is triggered" } }, "files"));
        }

        private static JObject CreateTool(string name, string desc, JObject props, params string[] required)
        {
            var tool = new JObject { ["name"] = name, ["description"] = desc };
            var schema = new JObject { ["type"] = "object", ["properties"] = props };
            if (required != null && required.Length > 0) schema["required"] = new JArray(required);
            tool["inputSchema"] = schema;
            return tool;
        }
    }
}
