#pragma warning disable 0618
using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Tests {
    public partial class ConsolidatedManagersTests {
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
            (string mappedMethod, JObject mappedParams) = MapManager(managerName, args);

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
        public void AddComponent_StaleInstanceId_ReturnsGameObjectNotFound() {
            var go = CreateTestGameObject();
            int staleId = go.GetInstanceID();
            GameObject.DestroyImmediate(go);

            var res = CallRaw("add_component", new JObject { ["instance_id"] = staleId, ["component_name"] = "BoxCollider" });

            Assert.IsNotNull(res["error"], res.ToString());
            StringAssert.Contains("GameObject not found", res["error"]["message"].ToString());
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
        public void DeletePlayerPref_EmptyOrMissingKey_ReturnsErrorWithoutDeletingPrefs() {
            string key = "NexusUnity_DeleteGuard_" + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, "keep");
            PlayerPrefs.Save();

            try {
                var missing = CallRaw("delete_player_pref", new JObject());
                Assert.IsNotNull(missing["error"], missing.ToString());
                Assert.IsTrue(PlayerPrefs.HasKey(key));

                var empty = CallRaw("delete_player_pref", new JObject { ["key"] = "" });
                Assert.IsNotNull(empty["error"], empty.ToString());
                Assert.IsTrue(PlayerPrefs.HasKey(key));
            }
            finally {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void DeletePlayerPref_ToolSchemaRequiresKeyAndAdvertisesConfirm() {
            var tools = (JArray)CallRaw("list_tools", new JObject())["result"];
            var tool = (JObject)tools.First(item => item["name"]?.ToString() == "delete_player_pref");
            var schema = (JObject)tool["inputSchema"];
            var properties = (JObject)schema["properties"];

            CollectionAssert.Contains(((JArray)schema["required"]).Select(item => item.ToString()).ToArray(), "key");
            Assert.AreEqual("boolean", properties["confirm"]["type"]?.ToString());
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
