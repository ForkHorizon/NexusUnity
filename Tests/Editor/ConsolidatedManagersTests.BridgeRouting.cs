// C# mirror of the Python bridge manager routing used by the manager tests.
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Tests {
    /// <summary>C# mirror of the Python bridge manager routing, used to drive manager tests through ProcessJsonRpc.</summary>
    public partial class ConsolidatedManagersTests {
        private static (string Method, JObject Params) MapManager(string managerName, JObject args) {
            switch (managerName) {
                case "unity_scene_manager": return MapSceneManager(args);
                case "unity_hierarchy_manager": return MapHierarchyManager(args);
                case "unity_component_manager": return MapComponentManager(args);
                case "unity_search_manager": return MapSearchManager(args);
                case "unity_asset_manager": return MapAssetManager(args);
                case "unity_editor_controller": return MapEditorController(args);
                case "unity_ui_automation": return MapUiAutomation(args);
                case "unity_playerprefs_manager": return MapPlayerPrefsManager(args);
                case "unity_wait": return MapWait(args);
                case "unity_write_and_compile":
                case "unity_invoke_method":
                case "unity_dump_scene_graph":
                case "unity_get_scene_dependencies":
                case "unity_lint_project":
                    return (managerName.Substring("unity_".Length), args);
                default:
                    return (null, null);
            }
        }

        private static (string Method, JObject Params) MapSceneManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "create_scene") action = "create";
            else if (action == "open_scene") action = "open";
            else if (action == "save_scene") action = "save";
            else if (action == "list_scenes") action = "list";

            if (action == "create") {
                mappedMethod = "create_scene";
                mappedParams = new JObject();
                CopyIfPresent(args, mappedParams, "name", "path", "open_if_exists");
            }
            else if (action == "open") { mappedMethod = "open_scene"; mappedParams = new JObject { ["path"] = args["path"] }; }
            else if (action == "save") { mappedMethod = "save_scene"; mappedParams = new JObject { ["path"] = args["path"] }; }
            else if (action == "list") { mappedMethod = "list_scenes"; mappedParams = new JObject(); }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapHierarchyManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "create" || action == "create_gameobject" || action == "create_game_object") action = "create_empty";
            else if (action == "rename") action = "set_name";

            if (action == "create_empty") { mappedMethod = "create_game_object"; mappedParams = new JObject { ["name"] = args["name"], ["parent_id"] = args["parent_id"] }; }
            else if (action == "create_primitive") {
                mappedMethod = "create_primitive";
                mappedParams = new JObject();
                CopyIfPresent(args, mappedParams, "primitive_type", "name", "parent_id", "position", "rotation", "scale", "material_path");
            }
            else if (action == "create_hierarchy") { mappedMethod = "create_hierarchy"; mappedParams = new JObject { ["tree"] = args["tree"], ["parent_id"] = args["parent_id"] }; }
            else if (action == "set_name") { mappedMethod = "set_property"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["property_name"] = "m_Name", ["value"] = args["name"] ?? args["new_name"] }; }
            else if (action == "set_transform") {
                mappedMethod = "set_transform";
                mappedParams = new JObject { ["instance_id"] = args["instance_id"] };
                CopyIfPresent(args, mappedParams, "position", "rotation", "scale", "eulerAngles", "localScale");
            }
            else if (action == "destroy") { mappedMethod = "destroy_game_object"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
            else if (action == "duplicate") { mappedMethod = "duplicate_object"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
            else if (action == "set_active") { mappedMethod = "set_active"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["active"] = args["active"] }; }
            else if (action == "set_parent") { mappedMethod = "set_parent"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["parent_id"] = args["parent_id"] }; }
            else if (action == "set_sibling_index") { mappedMethod = "set_sibling_index"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["index"] = args["index"] }; }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapComponentManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "add") { mappedMethod = "add_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
            else if (action == "remove") { mappedMethod = "remove_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
            else if (action == "inspect") { mappedMethod = "inspect_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
            else if (action == "get_schema") { mappedMethod = "get_component_schema"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
            else if (action == "update_properties") { mappedMethod = "update_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"], ["properties"] = args["properties"] }; }
            else if (action == "set_property") { mappedMethod = "set_property"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["property_name"] = args["property_name"], ["value"] = args["value"] }; }
            else if (action == "set_enabled") { mappedMethod = "set_enabled"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"], ["enabled"] = args["enabled"] }; }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapSearchManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string strategy = args["strategy"]?.ToString();
            if (strategy == "regex") { mappedMethod = "find_objects"; mappedParams = new JObject { ["name"] = args["query"], ["tag"] = args["tag"], ["type"] = args["type"] }; }
            else if (strategy == "path") { mappedMethod = "find_by_path"; mappedParams = new JObject { ["path"] = args["query"] }; }
            else if (strategy == "semantic") { mappedMethod = "semantic_find"; mappedParams = new JObject { ["query"] = args["query"] }; }
            else if (strategy == "references") { mappedMethod = "find_references"; mappedParams = new JObject { ["target_id"] = args["target_id"], ["target_guid"] = args["target_guid"] }; }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapAssetManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "search") { mappedMethod = "list_assets"; mappedParams = new JObject { ["filter"] = args["filter"] }; }
            else if (action == "explore") { mappedMethod = "explore_asset"; mappedParams = new JObject { ["path"] = args["path"] }; }
            else if (action == "create_material") {
                mappedMethod = "create_material";
                mappedParams = new JObject();
                CopyIfPresent(args, mappedParams, "name", "shader", "path", "base_color", "color", "emission_color", "emission");
            }
            else if (action == "import") { mappedMethod = "import_asset"; mappedParams = new JObject { ["path"] = args["path"] }; }
            else if (action == "refresh") { mappedMethod = "refresh_asset_database"; mappedParams = new JObject(); }
            else if (action == "instantiate_prefab") { mappedMethod = "instantiate_prefab"; mappedParams = new JObject { ["path"] = args["path"] }; }
            else if (action == "create_prefab") { mappedMethod = "create_prefab"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["path"] = args["path"] }; }
            else if (action == "apply_overrides") { mappedMethod = "apply_prefab_overrides"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
            else if (action == "revert_overrides") { mappedMethod = "revert_prefab_overrides"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapEditorController(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "undo") { mappedMethod = "undo"; mappedParams = new JObject(); }
            else if (action == "redo") { mappedMethod = "redo"; mappedParams = new JObject(); }
            else if (action == "play") { mappedMethod = "toggle_play_mode"; mappedParams = new JObject { ["value"] = args["state"] }; }
            else if (action == "pause") { mappedMethod = "pause_play_mode"; mappedParams = new JObject { ["value"] = args["state"] }; }
            else if (action == "step") { mappedMethod = "step_frame"; mappedParams = new JObject(); }
            else if (action == "menu") { mappedMethod = "execute_menu_item"; mappedParams = new JObject { ["item_path"] = args["item_path"] }; }
            else if (action == "read_logs") { mappedMethod = "read_logs"; mappedParams = new JObject { ["count"] = args["count"] ?? 100 }; }
            else if (action == "clear_logs") { mappedMethod = "clear_logs"; mappedParams = new JObject(); }
            else if (action == "get_state") { mappedMethod = "get_editor_state"; mappedParams = new JObject(); }
            else if (action == "get_server_status") { mappedMethod = "get_server_status"; mappedParams = new JObject(); }
            else if (action == "refresh_assets") { mappedMethod = "refresh_asset_database"; mappedParams = new JObject(); }
            else if (action == "run_tests") { mappedMethod = "run_tests"; mappedParams = new JObject { ["mode"] = args["mode"] ?? "EditMode", ["filter"] = args["filter"] }; }
            else if (action == "get_test_results") { mappedMethod = "get_test_results"; mappedParams = new JObject { ["result_path"] = args["result_path"] }; }
            else if (action == "get_tool_usage_stats") { mappedMethod = "get_tool_usage_stats"; mappedParams = new JObject(); }
            else if (action == "reset_tool_usage_stats") { mappedMethod = "reset_tool_usage_stats"; mappedParams = new JObject(); }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapUiAutomation(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "list_windows") { mappedMethod = "ui_list_windows"; mappedParams = new JObject(); }
            else if (action == "get_hierarchy") { mappedMethod = "ui_get_hierarchy"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["deep"] = args["deep"], ["max_depth"] = args["max_depth"], ["max_elements"] = args["max_elements"] ?? args["max_results"] }; }
            else if (action == "query") { mappedMethod = "ui_query_elements"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["name"] = args["name"], ["text"] = args["text"], ["class_name"] = args["class_name"], ["max_depth"] = args["max_depth"], ["max_results"] = args["max_results"] ?? args["max_elements"], ["max_elements"] = args["max_elements"] }; }
            else if (action == "get_window_rect") { mappedMethod = "ui_get_window_rect"; mappedParams = new JObject { ["window_title"] = args["window_title"] }; }
            else if (action == "set_window_rect") { mappedMethod = "ui_set_window_rect"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["x"] = args["x"], ["y"] = args["y"], ["width"] = args["width"], ["height"] = args["height"] }; }
            else if (action == "capture_window_snapshot") { mappedMethod = "ui_capture_window_snapshot"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["include_image"] = args["include_image"], ["include_hierarchy"] = args["include_hierarchy"], ["max_depth"] = args["max_depth"], ["max_elements"] = args["max_elements"] ?? args["max_results"] }; }
            else if (action == "click") { mappedMethod = "ui_click"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["element_name"] = args["element_name"] }; }
            else if (action == "input") { mappedMethod = "ui_input_text"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["element_name"] = args["element_name"], ["text"] = args["text"] }; }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapPlayerPrefsManager(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string action = args["action"]?.ToString();
            if (action == "get") { mappedMethod = "get_player_pref"; mappedParams = new JObject { ["key"] = args["key"], ["type"] = args["type"] ?? "string" }; }
            else if (action == "set") { mappedMethod = "set_player_pref"; mappedParams = new JObject { ["key"] = args["key"], ["value"] = args["value"], ["type"] = args["type"] ?? "string" }; }
            else if (action == "delete") {
                mappedMethod = "delete_player_pref";
                mappedParams = new JObject { ["key"] = args["key"] };
                CopyIfPresent(args, mappedParams, "confirm");
            }
            else if (action == "list") { mappedMethod = "list_player_prefs"; mappedParams = new JObject(); }
            return (mappedMethod, mappedParams);
        }

        private static (string Method, JObject Params) MapWait(JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;
            string condition = args["condition"]?.ToString();
            if (condition == "compilation" || condition == "play_mode" || condition == "import" || condition == "editor_idle") {
                mappedMethod = "get_editor_state"; mappedParams = new JObject();
            }
            return (mappedMethod, mappedParams);
        }
    }
}
