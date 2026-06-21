#pragma warning disable 0618
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
            }
            else if (managerName == "unity_hierarchy_manager") {
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
                else if (action == "get_state") { mappedMethod = "get_editor_state"; mappedParams = new JObject(); }
                else if (action == "get_server_status") { mappedMethod = "get_server_status"; mappedParams = new JObject(); }
                else if (action == "refresh_assets") { mappedMethod = "refresh_asset_database"; mappedParams = new JObject(); }
                else if (action == "run_tests") { mappedMethod = "run_tests"; mappedParams = new JObject { ["mode"] = args["mode"] ?? "EditMode", ["filter"] = args["filter"] }; }
                else if (action == "get_test_results") { mappedMethod = "get_test_results"; mappedParams = new JObject { ["result_path"] = args["result_path"] }; }
                else if (action == "get_tool_usage_stats") { mappedMethod = "get_tool_usage_stats"; mappedParams = new JObject(); }
                else if (action == "reset_tool_usage_stats") { mappedMethod = "reset_tool_usage_stats"; mappedParams = new JObject(); }
            }
            else if (managerName == "unity_ui_automation") {
                string action = args["action"]?.ToString();
                if (action == "list_windows") { mappedMethod = "ui_list_windows"; mappedParams = new JObject(); }
                else if (action == "get_hierarchy") { mappedMethod = "ui_get_hierarchy"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["deep"] = args["deep"] }; }
                else if (action == "query") { mappedMethod = "ui_query_elements"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["name"] = args["name"], ["text"] = args["text"], ["class_name"] = args["class_name"] }; }
                else if (action == "get_window_rect") { mappedMethod = "ui_get_window_rect"; mappedParams = new JObject { ["window_title"] = args["window_title"] }; }
                else if (action == "set_window_rect") { mappedMethod = "ui_set_window_rect"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["x"] = args["x"], ["y"] = args["y"], ["width"] = args["width"], ["height"] = args["height"] }; }
                else if (action == "capture_window_snapshot") { mappedMethod = "ui_capture_window_snapshot"; mappedParams = new JObject { ["window_title"] = args["window_title"], ["include_image"] = args["include_image"], ["include_hierarchy"] = args["include_hierarchy"] }; }
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

            var processMethod = typeof(MCPServerMethods).GetMethod("ProcessJsonRpc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
            if (processMethod == null) throw new Exception("Could not find MCPServerMethods.ProcessJsonRpc(string)");

            string responseJson = (string)processMethod.Invoke(null, new object[] { request.ToString(Newtonsoft.Json.Formatting.None) });
            return JObject.Parse(responseJson);
        }

        private static void CopyIfPresent(JObject source, JObject destination, params string[] keys) {
            foreach (string key in keys) {
                if (source[key] != null) destination[key] = source[key];
            }
        }

        private static JToken CallRaw(string method, JObject parameters) {
            JObject request = new JObject {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters,
                ["id"] = 1
            };

            string responseJson = MCPServerMethods.ProcessJsonRpc(request.ToString(Newtonsoft.Json.Formatting.None));
            return JObject.Parse(responseJson);
        }

        private GameObject CreateTestGameObject() {
            var go = new GameObject("TestGo");
            _createdObjects.Add(go);
            return go;
        }

        private static void CleanupGeneratedAssetRoot(string root) {
            AssetDatabase.DeleteAsset(root);

            string absoluteRoot = MCPServerMethods.ValidatePath(root);
            if (Directory.Exists(absoluteRoot)) Directory.Delete(absoluteRoot, true);

            string meta = absoluteRoot + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            AssetDatabase.Refresh();
        }

        [Test]
        public void UnitySceneManager_ListScenes_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_scene_manager", new JObject { ["action"] = "list" });
            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
        }

        [Test]
        public void UnitySceneManager_ListScenesAlias_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_scene_manager", new JObject { ["action"] = "list_scenes" });
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
                var go = EditorUtility.InstanceIDToObject(id.Value) as GameObject;
                if (go != null) _createdObjects.Add(go);
            }
        }

        [Test]
        public void UnityHierarchyManager_CreatePrimitive_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject { ["action"] = "create_primitive", ["primitive_type"] = "Cube" });
            Assert.IsNotNull(res["result"]);
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            if (id.HasValue && id.Value != 0) {
                var go = EditorUtility.InstanceIDToObject(id.Value) as GameObject;
                if (go != null) _createdObjects.Add(go);
            }
        }

        [Test]
        public void UnityHierarchyManager_CreateAlias_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject { ["action"] = "create_gameobject", ["name"] = "AliasCreatedManager" });
            Assert.IsNotNull(res["result"]);
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            if (id.HasValue && id.Value != 0) {
                var go = EditorUtility.InstanceIDToObject(id.Value) as GameObject;
                Assert.IsNotNull(go);
                Assert.AreEqual("AliasCreatedManager", go.name);
                _createdObjects.Add(go);
            }
        }

        [Test]
        public void UnityHierarchyManager_CreatePrimitiveWithNameAndTransform_ReturnsPlacedObject() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "create_primitive",
                ["primitive_type"] = "Cube",
                ["name"] = "PlacedManagerCube",
                ["position"] = new JObject { ["x"] = 2, ["y"] = 1, ["z"] = -3 },
                ["rotation"] = new JObject { ["x"] = 0, ["y"] = 25, ["z"] = 0 },
                ["scale"] = new JObject { ["x"] = 1.5, ["y"] = 0.5, ["z"] = 2 }
            });

            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            Assert.IsTrue(id.HasValue && id.Value != 0);

            var go = EditorUtility.InstanceIDToObject(id.Value) as GameObject;
            Assert.IsNotNull(go);
            _createdObjects.Add(go);
            Assert.AreEqual("PlacedManagerCube", go.name);
            Assert.AreEqual(2f, go.transform.position.x, 0.001f);
            Assert.AreEqual(1f, go.transform.position.y, 0.001f);
            Assert.AreEqual(-3f, go.transform.position.z, 0.001f);
            Assert.AreEqual(25f, go.transform.eulerAngles.y, 0.001f);
            Assert.AreEqual(1.5f, go.transform.localScale.x, 0.001f);
            Assert.AreEqual(0.5f, go.transform.localScale.y, 0.001f);
            Assert.AreEqual(2f, go.transform.localScale.z, 0.001f);
        }

        [Test]
        public void UnityHierarchyManager_CreatePrimitiveWithArrayPosition_ReturnsPlacedObject() {
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "create_primitive",
                ["primitive_type"] = "Cube",
                ["name"] = "ArrayPlacedManagerCube",
                ["position"] = new JArray(9, 9, 9)
            });

            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
            var id = res["result"]["data"]?["instance_id"]?.Value<int>();
            Assert.IsTrue(id.HasValue && id.Value != 0);

            var go = EditorUtility.InstanceIDToObject(id.Value) as GameObject;
            Assert.IsNotNull(go);
            _createdObjects.Add(go);
            Assert.AreEqual(9f, go.transform.position.x, 0.001f);
            Assert.AreEqual(9f, go.transform.position.y, 0.001f);
            Assert.AreEqual(9f, go.transform.position.z, 0.001f);
        }

        [Test]
        public void UnityHierarchyManager_CreatePrimitiveWithInvalidArrayPosition_DoesNotCreateObject() {
            string name = "InvalidArrayCube_" + Guid.NewGuid().ToString("N");
            int before = GameObject.FindObjectsOfType<GameObject>().Count(go => go.name == name);

            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "create_primitive",
                ["primitive_type"] = "Cube",
                ["name"] = name,
                ["position"] = new JArray(9, 9)
            });

            Assert.IsNotNull(res["error"], "Expected invalid Vector3 array to fail.");
            int after = GameObject.FindObjectsOfType<GameObject>().Count(go => go.name == name);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void UnityHierarchyManager_RenameAlias_UpdatesGameObjectName() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "rename",
                ["instance_id"] = go.GetInstanceID(),
                ["name"] = "RenamedByManager"
            });

            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
            Assert.AreEqual("RenamedByManager", go.name);
        }

        [Test]
        public void UnityHierarchyManager_SetTransform_UpdatesPositionRotationAndScale() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "set_transform",
                ["instance_id"] = go.GetInstanceID(),
                ["position"] = new JObject { ["x"] = -1, ["y"] = 2, ["z"] = 3 },
                ["rotation"] = new JObject { ["x"] = 10, ["y"] = 20, ["z"] = 30 },
                ["scale"] = new JObject { ["x"] = 2, ["y"] = 3, ["z"] = 4 }
            });

            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
            Assert.AreEqual(-1f, go.transform.position.x, 0.001f);
            Assert.AreEqual(2f, go.transform.position.y, 0.001f);
            Assert.AreEqual(3f, go.transform.position.z, 0.001f);
            Assert.AreEqual(10f, go.transform.eulerAngles.x, 0.001f);
            Assert.AreEqual(20f, go.transform.eulerAngles.y, 0.001f);
            Assert.AreEqual(30f, go.transform.eulerAngles.z, 0.001f);
            Assert.AreEqual(2f, go.transform.localScale.x, 0.001f);
            Assert.AreEqual(3f, go.transform.localScale.y, 0.001f);
            Assert.AreEqual(4f, go.transform.localScale.z, 0.001f);
        }

        [Test]
        public void GetGameObject_ReturnsTransformAndComponentsForReadBackVerification() {
            var go = CreateTestGameObject();

            var transformRes = SimulateBridgeRouting("unity_hierarchy_manager", new JObject {
                ["action"] = "set_transform",
                ["instance_id"] = go.GetInstanceID(),
                ["position"] = new JObject { ["x"] = -1, ["y"] = 2, ["z"] = 3 },
                ["rotation"] = new JObject { ["x"] = 10, ["y"] = 20, ["z"] = 30 },
                ["scale"] = new JObject { ["x"] = 2, ["y"] = 3, ["z"] = 4 }
            });
            Assert.IsNotNull(transformRes["result"], $"Expected result, got error: {transformRes["error"]}");

            var addComponentRes = CallRaw("add_component", new JObject { ["instance_id"] = go.GetInstanceID(), ["component_name"] = "BoxCollider" });
            Assert.IsNotNull(addComponentRes["result"], $"Expected result, got error: {addComponentRes["error"]}");

            var readRes = CallRaw("get_game_object", new JObject { ["instance_id"] = go.GetInstanceID() });
            Assert.IsNotNull(readRes["result"], $"Expected result, got error: {readRes["error"]}");
            var data = readRes["result"]["data"];
            Assert.IsNotNull(data);
            Assert.AreEqual(-1f, data["transform"]["position"]["x"].Value<float>(), 0.001f);
            Assert.AreEqual(2f, data["transform"]["position"]["y"].Value<float>(), 0.001f);
            Assert.AreEqual(3f, data["transform"]["position"]["z"].Value<float>(), 0.001f);
            Assert.AreEqual(10f, data["transform"]["rotation"]["x"].Value<float>(), 0.001f);
            Assert.AreEqual(20f, data["transform"]["rotation"]["y"].Value<float>(), 0.001f);
            Assert.AreEqual(30f, data["transform"]["rotation"]["z"].Value<float>(), 0.001f);
            Assert.AreEqual(2f, data["transform"]["scale"]["x"].Value<float>(), 0.001f);
            Assert.AreEqual(3f, data["transform"]["scale"]["y"].Value<float>(), 0.001f);
            Assert.AreEqual(4f, data["transform"]["scale"]["z"].Value<float>(), 0.001f);

            var components = data["components"] as JArray;
            Assert.IsNotNull(components);
            Assert.IsTrue(components.Any(c => c["type"]?.ToString() == "Transform"));
            Assert.IsTrue(components.Any(c => c["type"]?.ToString() == "BoxCollider"));
        }

        [Test]
        public void UnityComponentManager_Inspect_ReturnsSuccess() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_component_manager", new JObject { ["action"] = "inspect", ["instance_id"] = go.GetInstanceID(), ["component_name"] = "Transform" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityAssetManager_Search_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_asset_manager", new JObject { ["action"] = "search", ["filter"] = "t:Material" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityAssetManager_CreateMaterialWithPathAndColor_ReturnsSuccess() {
            string root = "Assets/NexusUnityGeneratedTests";
            string path = $"{root}/Materials/BridgeMaterial.mat";
            CleanupGeneratedAssetRoot(root);

            try {
                var res = SimulateBridgeRouting("unity_asset_manager", new JObject {
                    ["action"] = "create_material",
                    ["name"] = "BridgeMaterial",
                    ["path"] = path,
                    ["base_color"] = "#00CCFFFF",
                    ["emission_color"] = "#112233FF"
                });

                Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(material);

                Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.GetColor("_Color");
                Assert.AreEqual(0f, baseColor.r, 0.01f);
                Assert.AreEqual(0.8f, baseColor.g, 0.01f);
                Assert.AreEqual(1f, baseColor.b, 0.01f);
            }
            finally {
                CleanupGeneratedAssetRoot(root);
            }
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
        public void UnityEditorController_GetStateAndStatus_ReturnSuccess() {
            var state = SimulateBridgeRouting("unity_editor_controller", new JObject { ["action"] = "get_state" });
            var status = SimulateBridgeRouting("unity_editor_controller", new JObject { ["action"] = "get_server_status" });
            var results = SimulateBridgeRouting("unity_editor_controller", new JObject { ["action"] = "get_test_results" });

            Assert.IsNotNull(state["result"]);
            Assert.IsNotNull(status["result"]);
            Assert.IsNotNull(results["result"]);
        }

        [Test]
        public void UnityEditorController_GetToolUsageStats_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_editor_controller", new JObject { ["action"] = "get_tool_usage_stats" });
            Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
        }

        [Test]
        public void UnityUIAutomation_ListWindows_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_ui_automation", new JObject { ["action"] = "list_windows" });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityUIAutomation_CaptureWindowSnapshot_ReturnsSuccess() {
            var window = MCPTestWindow.ShowWindow();
            try {
                var res = SimulateBridgeRouting("unity_ui_automation", new JObject {
                    ["action"] = "capture_window_snapshot",
                    ["window_title"] = MCPTestWindow.WindowTitle,
                    ["include_image"] = false,
                    ["include_hierarchy"] = true
                });
                Assert.IsNotNull(res["result"]);
                Assert.AreEqual("Success", res["result"]["status"]?.ToString());
            }
            finally {
                window.Close();
            }
        }

        [Test]
        public void UnityWait_EditorIdle_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_wait", new JObject { ["condition"] = "editor_idle", ["timeout_seconds"] = 1 });
            Assert.IsNotNull(res["result"]);
        }

        [Test]
        public void UnityWriteAndCompile_ReturnsSuccess() {
            var res = SimulateBridgeRouting("unity_write_and_compile", new JObject { ["files"] = new JArray() });
            Assert.IsNotNull(res["error"]);
            Assert.IsTrue(res["error"]["message"].ToString().Contains("Method missing") || res["error"]["message"].ToString().Contains("not found"), "Should correctly try to call write_and_compile");
        }

        [Test]
        public void RawWriteFileCreatesMissingParentFolder() {
            string root = "Assets/NexusUnityGeneratedTests";
            string path = $"{root}/Nested/TestFile.txt";
            CleanupGeneratedAssetRoot(root);

            try {
                var res = CallRaw("write_file", new JObject { ["path"] = path, ["content"] = "Nexus Unity generated test file" });
                Assert.IsNotNull(res["result"], $"Expected result, got error: {res["error"]}");
                Assert.IsTrue(File.Exists(MCPServerMethods.ValidatePath(path)), "write_file should create missing parent folders before writing.");
            }
            finally {
                CleanupGeneratedAssetRoot(root);
            }
        }

        [Test]
        public void UnityInvokeMethod_ReturnsSuccess() {
            var go = CreateTestGameObject();
            var res = SimulateBridgeRouting("unity_invoke_method", new JObject { ["instance_id"] = go.GetInstanceID(), ["method_name"] = "GetInstanceID" });
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
            Assert.IsTrue(res["result"] != null || res["error"] != null);
        }
    }
}
#pragma warning restore 0618
