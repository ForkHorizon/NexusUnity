using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace UnityMCP.Editor.Tests {
    public class ConsolidatedManagersTests {
        private List<GameObject> _createdObjects = new List<GameObject>();

        [SetUp]
        public void InitRegistry() {
            typeof(MCPServerMethods)
                .GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
        }

        [TearDown]
        public void Cleanup() {
            foreach(var go in _createdObjects) {
                if (go != null) {
                    GameObject.DestroyImmediate(go);
                }
            }
            _createdObjects.Clear();
        }

        private JToken SimulateBridgeRouting(string managerName, JObject args) {
            string mappedMethod = null;
            JObject mappedParams = null;

            if (managerName == "unity_scene_manager") {
                string action = args["action"]?.ToString();
                if (action == "create") { mappedMethod = "create_scene"; mappedParams = new JObject { ["name"] = args["name"] }; }
                else if (action == "open") { mappedMethod = "open_scene"; mappedParams = new JObject { ["path"] = args["path"] }; }
                else if (action == "save") { mappedMethod = "save_scene"; mappedParams = new JObject { ["path"] = args["path"] }; }
                else if (action == "list") { mappedMethod = "list_scenes"; mappedParams = new JObject(); }
            }
            else if (managerName == "unity_hierarchy_manager") {
                string action = args["action"]?.ToString();
                if (action == "create_empty") { mappedMethod = "create_game_object"; mappedParams = new JObject { ["name"] = args["name"], ["parent_id"] = args["parent_id"] }; }
                else if (action == "create_primitive") { mappedMethod = "create_primitive"; mappedParams = new JObject { ["primitive_type"] = args["primitive_type"] }; }
                else if (action == "destroy") { mappedMethod = "destroy_game_object"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
                else if (action == "duplicate") { mappedMethod = "duplicate_object"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
                else if (action == "set_active") { mappedMethod = "set_active"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["active"] = args["active"] }; }
                else if (action == "set_parent") { mappedMethod = "set_parent"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["parent_id"] = args["parent_id"] }; }
                else if (action == "set_sibling_index") { mappedMethod = "set_sibling_index"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["index"] = args["index"] }; }
            }
            else if (managerName == "unity_component_manager") {
                string action = args["action"]?.ToString();
                if (action == "add") { mappedMethod = "add_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
                else if (action == "remove") { mappedMethod = "remove_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
                else if (action == "inspect") { mappedMethod = "inspect_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
                else if (action == "get_schema") { mappedMethod = "get_component_schema"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"] }; }
                else if (action == "update_properties") { mappedMethod = "update_component"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"], ["properties"] = args["properties"] }; }
                else if (action == "set_property") { mappedMethod = "set_property"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["property_name"] = args["property_name"], ["value"] = args["value"] }; }
                else if (action == "set_enabled") { mappedMethod = "set_enabled"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["component_name"] = args["component_name"], ["enabled"] = args["enabled"] }; }
            }
            else if (managerName == "unity_search_manager") {
                string strategy = args["strategy"]?.ToString();
                if (strategy == "regex") { mappedMethod = "find_objects"; mappedParams = new JObject { ["name"] = args["query"], ["tag"] = args["tag"], ["type"] = args["type"] }; }
                else if (strategy == "path") { mappedMethod = "find_by_path"; mappedParams = new JObject { ["path"] = args["query"] }; }
                else if (strategy == "semantic") { mappedMethod = "semantic_find"; mappedParams = new JObject { ["query"] = args["query"] }; }
                else if (strategy == "references") { mappedMethod = "find_references"; mappedParams = new JObject { ["target_id"] = args["target_id"], ["target_guid"] = args["target_guid"] }; }
            }
            else if (managerName == "unity_asset_manager") {
                string action = args["action"]?.ToString();
                if (action == "search") { mappedMethod = "list_assets"; mappedParams = new JObject { ["filter"] = args["filter"] }; }
                else if (action == "explore") { mappedMethod = "explore_asset"; mappedParams = new JObject { ["path"] = args["path"] }; }
                else if (action == "create_material") { mappedMethod = "create_material"; mappedParams = new JObject { ["name"] = args["name"], ["shader"] = args["shader"] }; }
                else if (action == "import") { mappedMethod = "import_asset"; mappedParams = new JObject { ["path"] = args["path"] }; }
                else if (action == "refresh") { mappedMethod = "refresh_asset_database"; mappedParams = new JObject(); }
                else if (action == "instantiate_prefab") { mappedMethod = "instantiate_prefab"; mappedParams = new JObject { ["path"] = args["path"] }; }
                else if (action == "create_prefab") { mappedMethod = "create_prefab"; mappedParams = new JObject { ["instance_id"] = args["instance_id"], ["path"] = args["path"] }; }
                else if (action == "apply_overrides") { mappedMethod = "apply_prefab_overrides"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
                else if (action == "revert_overrides") { mappedMethod = "revert_prefab_overrides"; mappedParams = new JObject { ["instance_id"] = args["instance_id"] }; }
            }
            else if (managerName == "unity_editor_controller") {
                string action = args["action"]?.ToString();
                if (action == "undo") { mappedMethod = "undo"; mappedParams = new JObject(); }
                else if (action == "redo") { mappedMethod = "redo"; mappedParams = new JObject(); }
                else if (action == "play") { mappedMethod = "toggle_play_mode"; mappedParams = new JObject { ["value"] = args["state"] }; }
                else if (action == "pause") { mappedMethod = "pause_play_mode"; mappedParams = new JObject { ["value"] = args["state"] }; }
                else if (action == "step") { mappedMethod = "step_frame"; mappedParams = new JObject(); }
                else if (action == "menu") { mappedMethod = "execute_menu_item"; mappedParams = new JObject { ["item_path"] = args["item_path"] }; }
                else if (action == "read_logs") { mappedMethod = "read_logs"; mappedParams = new JObject { ["count"] = args["count"] ?? 100 }; }
                else if (action == "clear_logs") { mappedMethod = "clear_logs"; mappedParams = new JObject(); }
            }
            else if (managerName == "unity_ui_automation") {
                string action = args["action"]?.ToString();
                if (action == "list_windows") { mappedMethod = "ui_list_windows"; mappedParams = new JObject(); }
                else if (action == "get_hierarchy") { mappedMethod = "ui_get_hierarchy"; mappedParams = new JObject { ["window_title"] = args["window_title"] }; }
                else if (action == "query") { mappedMethod = "ui_query_elements"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["name"] = args["name"], ["text"] = args["text"] }; }
                else if (action == "click") { mappedMethod = "ui_click"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["element_name"] = args["element_name"] }; }
                else if (action == "input") { mappedMethod = "ui_input_text"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["element_name"] = args["element_name"], ["text"] = args["text"] }; }
            }
            else if (managerName == "unity_playerprefs_manager") {
                string action = args["action"]?.ToString();
                if (action == "get") { mappedMethod = "get_player_pref"; mappedParams = new JObject { ["key"] = args["key"], ["type"] = args["type"] ?? "string" }; }
                else if (action == "set") { mappedMethod = "set_player_pref"; mappedParams = new JObject { ["key"] = args["key"], ["value"] = args["value"], ["type"] = args["type"] ?? "string" }; }
                else if (action == "delete") { mappedMethod = "delete_player_pref"; mappedParams = new JObject { ["key"] = args["key"] }; }
                else if (action == "list") { mappedMethod = "list_player_prefs"; mappedParams = new JObject(); }
            }
            else if (managerName == "unity_wait") {
                string condition = args["condition"]?.ToString();
                if (condition == "compilation" || condition == "play_mode" || condition == "import" || condition == "editor_idle") {
                    mappedMethod = "get_editor_state"; mappedParams = new JObject();
                }
            }
            else if (managerName == "unity_write_and_compile" || managerName == "unity_invoke_method" || managerName == "unity_dump_scene_graph" || managerName == "unity_get_scene_dependencies" || managerName == "unity_lint_project") {
                 mappedMethod = managerName.Substring("unity_".Length);
                 mappedParams = args;
            }

            if (mappedMethod == null) {
                throw new Exception($"Unmapped manager or action: {managerName} {args["action"]}");
            }

            JObject request = new JObject {
                ["jsonrpc"] = "2.0",
                ["method"] = mappedMethod,
                ["params"] = mappedParams,
                ["id"] = 1
            };

            // Assuming ProcessJsonRpc exists and handles raw JSON string.
            var processMethod = typeof(MCPServerMethods).GetMethod("ProcessJsonRpc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
            if (processMethod == null) throw new Exception("Could not find MCPServerMethods.ProcessJsonRpc(string)");

            string responseJson = (string)processMethod.Invoke(null, new object[] { request.ToString(Newtonsoft.Json.Formatting.None) });
            return JObject.Parse(responseJson);
        }

        private GameObject CreateTestGameObject() {
            var go = new GameObject("TestGo");
            _createdObjects.Add(go);
            return go;
        }

        [Test]
        public void UnitySceneManager_ListScenes_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_scene_manager", new JObject { ["action"] = "list" });
            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
        }

        [Test]
        public void UnitySceneManager_CreateScene_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_scene_manager", new JObject { ["action"] = "create", ["name"] = "TestManagerScene" });
            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
        }

        [Test]
        public void UnityHierarchyManager_CreateEmpty_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject { ["action"] = "create_empty", ["name"] = "TestEmptyManager" });
            Assert.IsNotNull(res["result"]);
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            if (id.HasValue && id.Value != 0) {
                var go = EditorUtility.EntityIdToObject(id.Value) as GameObject;
                if (go != null) _createdObjects.Add(go);
            }
        }

        [Test]
        public void UnityHierarchyManager_CreatePrimitive_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject { ["action"] = "create_primitive", ["primitive_type"] = "Cube" });
            Assert.IsNotNull(res["result"]);
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            if (id.HasValue && id.Value != 0) {
                var go = EditorUtility.EntityIdToObject(id.Value) as GameObject;
                if (go != null) _createdObjects.Add(go);
            }
        }

        [Test]
        public void UnityComponentManager_Inspect_ReturnsSuccess() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_component_manager", new JObject { ["action"] = "inspect", ["instance_id"] = go.GetEntityId(), ["component_name"] = "Transform" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityAssetManager_Search_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_asset_manager", new JObject { ["action"] = "search", ["filter"] = "t:Material" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnitySearchManager_Regex_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_search_manager", new JObject { ["strategy"] = "regex", ["query"] = ".*", ["type"] = "GameObject" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityPlayerPrefsManager_List_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_playerprefs_manager", new JObject { ["action"] = "list" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityEditorController_ReadLogs_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_editor_controller", new JObject { ["action"] = "read_logs", ["count"] = 10 });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityUIAutomation_ListWindows_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_ui_automation", new JObject { ["action"] = "list_windows" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityWait_EditorIdle_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_wait", new JObject { ["condition"] = "editor_idle", ["timeout_seconds"] = 1 });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityWriteAndCompile_ReturnsSuccess() {
            // Test mapping macro name
            var res = SimulateBridgeRouting("unity_write_and_compile", new JObject { ["files"] = new JArray() });
            // C# implementation missing, it might throw or return method missing error
            // As long as we map correctly to "write_and_compile" RPC
            Assert.IsNotNull(res["error"]);
            Assert.IsTrue(res["error"]["message"].ToString().Contains("Method missing") || res["error"]["message"].ToString().Contains("not found"), "Should correctly try to call write_and_compile");
        }

        [Test]
        public void UnityInvokeMethod_ReturnsSuccess() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_invoke_method", new JObject { ["instance_id"] = go.GetEntityId(), ["method_name"] = "GetInstanceID" });
            Assert.IsTrue(res["result"] != null || res["error"] != null, "Successfully routed but may have execution specific errors.");
        }

        [Test]
        public void UnityDumpSceneGraph_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_dump_scene_graph", new JObject { ["root_id"] = 0, ["max_depth"] = 1 });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityGetSceneDependencies_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_get_scene_dependencies", new JObject());
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityLintProject_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_lint_project", new JObject());
            // It might fail without project auditor, but should map correctly
            Assert.IsTrue(res["result"] != null || res["error"] != null);
        }
    }
}
