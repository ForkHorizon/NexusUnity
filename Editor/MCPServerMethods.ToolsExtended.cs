using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void AddEditorControlTools(JArray tools)
        {
            tools.Add(CreateTool("undo", "Unity Undo", new JObject { }));
            tools.Add(CreateTool("redo", "Unity Redo", new JObject { }));
            tools.Add(CreateTool("toggle_play_mode", "Start/Stop", new JObject { ["value"] = new JObject { ["type"] = "boolean" } }));
            tools.Add(CreateTool("pause_play_mode", "Pause/Unpause", new JObject { ["value"] = new JObject { ["type"] = "boolean" } }));
            tools.Add(CreateTool("step_frame", "Advance frame", new JObject { }));
            tools.Add(CreateTool("execute_menu_item", "Execute Menu", new JObject { ["item_path"] = new JObject { ["type"] = "string" } }, "item_path"));
            tools.Add(CreateTool("focus_scene_view", "Frame selection", new JObject { }));
            tools.Add(CreateTool("open_prefab_stage", "Open prefab asset in isolation mode", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("close_prefab_stage", "Exit prefab isolation mode", new JObject { }));
            tools.Add(CreateTool("read_logs", "Get Console logs with optional noise reduction", new JObject
            {
                ["count"] = new JObject { ["type"] = "integer", ["description"] = "Number of logs to retrieve" },
                ["structured"] = new JObject { ["type"] = "boolean", ["description"] = "If true, collapses consecutive identical messages" },
                ["filter_type"] = new JObject { ["type"] = "string", ["description"] = "Filter by type (Log, Warning, Error)" },
                ["search_text"] = new JObject { ["type"] = "string", ["description"] = "Search in message or stacktrace" }
            }));
            tools.Add(CreateTool("read_logs_since_cursor", "Read only new logs since last poll with optional noise reduction", new JObject
            {
                ["cursor"] = new JObject { ["type"] = "integer", ["description"] = "Last seen log ID" },
                ["structured"] = new JObject { ["type"] = "boolean", ["description"] = "If true, collapses consecutive identical messages" },
                ["severities"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" }, ["description"] = "e.g. ['Error', 'Exception']" },
                ["search_text"] = new JObject { ["type"] = "string", ["description"] = "Filter by content" }
            }));
            tools.Add(CreateTool("clear_logs", "Clear Console", new JObject { }));
            tools.Add(CreateTool("attach_script", "Create & Link C#", new JObject { ["script_name"] = new JObject { ["type"] = "string" }, ["script_content"] = new JObject { ["type"] = "string" }, ["confirm"] = new JObject { ["type"] = "boolean", ["description"] = "Required because this writes a .cs file and triggers Unity compilation" } }, "script_name", "confirm"));
            tools.Add(CreateTool("wait_for_ready", "Wait until server is responsive", new JObject { }));
            tools.Add(CreateTool("run_tests", "Run NUnit tests in the editor", new JObject
            {
                ["filter"] = new JObject { ["type"] = "string", ["description"] = "Optional: name of test or class" },
                ["mode"] = new JObject { ["type"] = "string", ["description"] = "EditMode or PlayMode" }
            }));
            tools.Add(CreateTool("get_test_results", "Read the latest Unity TestResults XML summary", new JObject
            {
                ["result_path"] = new JObject { ["type"] = "string", ["description"] = "Optional TestResults XML path inside the project or Unity persistent data path" }
            }));
        }

        private static void AddDiscoveryTools(JArray tools)
        {
            tools.Add(CreateTool("get_game_object", "Get object data", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("get_active_game_object", "Get current selection", new JObject { }));
            tools.Add(CreateTool("get_root_game_objects", "Get top-level objects", new JObject { }));
            tools.Add(CreateTool("get_object_path", "Get hierarchy breadcrumb", new JObject { ["instance_id"] = new JObject { ["type"] = "integer" } }, "instance_id"));
            tools.Add(CreateTool("find_objects", "Deep search", GetSearchSchema()));
            tools.Add(CreateTool("find_by_path", "Search by exact path", new JObject { ["path"] = new JObject { ["type"] = "string" } }, "path"));
            tools.Add(CreateTool("find_references", "Find scene and asset references to a target object or GUID", new JObject
            {
                ["target_id"] = new JObject { ["type"] = "integer", ["description"] = "Optional scene instance/entity id" },
                ["target_guid"] = new JObject { ["type"] = "string", ["description"] = "Optional asset GUID" }
            }));
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
            tools.Add(CreateTool("ui_get_window_rect", "Get an EditorWindow position and size for layout QA", new JObject { ["window_title"] = new JObject { ["type"] = "string" } }, "window_title"));
            tools.Add(CreateTool("ui_set_window_rect", "Set an EditorWindow position and size for layout QA", new JObject
            {
                ["window_title"] = new JObject { ["type"] = "string" },
                ["x"] = new JObject { ["type"] = "number" },
                ["y"] = new JObject { ["type"] = "number" },
                ["width"] = new JObject { ["type"] = "number" },
                ["height"] = new JObject { ["type"] = "number" }
            }, "window_title"));
            tools.Add(CreateTool("ui_capture_window_snapshot", "Capture an EditorWindow rect, UI hierarchy, and optional PNG image", new JObject
            {
                ["window_title"] = new JObject { ["type"] = "string" },
                ["include_image"] = new JObject { ["type"] = "boolean" },
                ["include_hierarchy"] = new JObject { ["type"] = "boolean" }
            }, "window_title"));
            tools.Add(CreateTool("ui_query_elements", "Find UI Toolkit elements by text, name, or USS class", new JObject
            {
                ["window_title"] = new JObject { ["type"] = "string" },
                ["name"] = new JObject { ["type"] = "string" },
                ["text"] = new JObject { ["type"] = "string" },
                ["class_name"] = new JObject { ["type"] = "string" }
            }, "window_title"));
            tools.Add(CreateTool("ui_click", "Simulate UI Click", new JObject { ["window_title"] = new JObject { ["type"] = "string" }, ["element_name"] = new JObject { ["type"] = "string" } }, "window_title", "element_name"));
            tools.Add(CreateTool("ui_input_text", "Type into UI field", new JObject { ["window_title"] = new JObject { ["type"] = "string" }, ["element_name"] = new JObject { ["type"] = "string" }, ["text"] = new JObject { ["type"] = "string" } }, "window_title", "element_name", "text"));
        }

        private static JObject GetPrimitiveSchema()
        {
            var schema = new JObject();
            var types = new JArray("Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad");
            schema["primitive_type"] = new JObject { ["type"] = "string", ["enum"] = types };
            schema["name"] = new JObject { ["type"] = "string" };
            schema["parent_id"] = new JObject { ["type"] = "integer" };
            schema["position"] = GetVector3Schema();
            schema["rotation"] = GetVector3Schema();
            schema["scale"] = GetVector3Schema();
            schema["material_path"] = new JObject { ["type"] = "string" };
            return schema;
        }

        private static JObject GetSearchSchema() => new JObject { ["name"] = new JObject { ["type"] = "string" }, ["tag"] = new JObject { ["type"] = "string" }, ["type"] = new JObject { ["type"] = "string" } };

        private static JObject GetTransformSchema() => new JObject
        {
            ["instance_id"] = new JObject { ["type"] = "integer" },
            ["position"] = GetVector3Schema(),
            ["rotation"] = GetVector3Schema(),
            ["scale"] = GetVector3Schema(),
            ["eulerAngles"] = GetVector3Schema(),
            ["localScale"] = GetVector3Schema()
        };

        private static JObject GetVector3Schema() => new JObject
        {
            ["oneOf"] = new JArray(
                new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["x"] = new JObject { ["type"] = "number" },
                        ["y"] = new JObject { ["type"] = "number" },
                        ["z"] = new JObject { ["type"] = "number" }
                    }
                },
                new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "number" },
                    ["minItems"] = 3,
                    ["maxItems"] = 3
                })
        };

        private static string SanitizeScriptName(string n) => System.Text.RegularExpressions.Regex.Replace(n, @"[^a-zA-Z0-9_]", "_");
        private static string GetDefaultScript(string n) => $"using UnityEngine;\npublic class {n} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {n}\"); }} }}";

        private static List<LogEntry> CollapseLogs(List<LogEntry> logs)
        {
            if (logs == null || logs.Count == 0) return logs;

            var collapsed = new List<LogEntry>();
            foreach (var log in logs)
            {
                int lastIdx = collapsed.Count - 1;
                if (lastIdx >= 0)
                {
                    var last = collapsed[lastIdx];
                    if (last.Message == log.Message && last.Type == log.Type)
                    {
                        last.Count += log.Count;
                        if (string.Compare(last.Timestamp, log.Timestamp) < 0)
                            last.Timestamp = log.Timestamp;
                        continue;
                    }
                }
                collapsed.Add(new LogEntry(log));
            }
            return collapsed;
        }
    }
}
