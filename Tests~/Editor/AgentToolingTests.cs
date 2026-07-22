using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class AgentToolingTests
    {
        private string _resultPath;

        [SetUp]
        public void SetUp()
        {
            MCPServerMethods.Init();
            _resultPath = Path.Combine(Application.persistentDataPath, "AgentToolingTestResults.xml");
            if (File.Exists(_resultPath)) File.Delete(_resultPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_resultPath) && File.Exists(_resultPath)) File.Delete(_resultPath);

            foreach (MCPTestWindow window in Resources.FindObjectsOfTypeAll<MCPTestWindow>())
            {
                window.Close();
            }
        }

        [Test]
        public void GetTestResultsReturnsNotFoundForMissingFile()
        {
            JObject result = RpcResult("get_test_results", new JObject { ["result_path"] = _resultPath });

            Assert.AreEqual("NotFound", result["status"]?.ToString());
            Assert.AreEqual(_resultPath, result["result_path"]?.ToString());
        }

        [Test]
        public void GetTestResultsParsesPassingXml()
        {
            WriteTestResults("<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" inconclusive=\"0\" skipped=\"0\" duration=\"0.1\"><test-suite><test-case name=\"Passes\" fullname=\"Agent.Passes\" result=\"Passed\" /></test-suite></test-run>");

            JObject result = RpcResult("get_test_results", new JObject { ["result_path"] = _resultPath });

            Assert.AreEqual("Success", result["status"]?.ToString());
            Assert.AreEqual("Passed", result["result"]?.ToString());
            Assert.AreEqual(1, result["total"]?.Value<int>());
            Assert.AreEqual(0, result["failed_tests"]?.Count());
        }

        [Test]
        public void GetTestResultsParsesFailingXml()
        {
            WriteTestResults("<test-run result=\"Failed\" total=\"2\" passed=\"1\" failed=\"1\" inconclusive=\"0\" skipped=\"0\" duration=\"0.2\"><test-suite><test-case name=\"Passes\" fullname=\"Agent.Passes\" result=\"Passed\" /><test-case name=\"Fails\" fullname=\"Agent.Fails\" result=\"Failed\"><failure><message>Expected true</message></failure></test-case></test-suite></test-run>");

            JObject result = RpcResult("get_test_results", new JObject { ["result_path"] = _resultPath });
            JToken failure = result["failed_tests"]?.First;

            Assert.AreEqual("Success", result["status"]?.ToString());
            Assert.AreEqual(2, result["total"]?.Value<int>());
            Assert.AreEqual(1, result["failed"]?.Value<int>());
            Assert.AreEqual("Agent.Fails", failure?["fullname"]?.ToString());
            Assert.AreEqual("Expected true", failure?["message"]?.ToString());
        }

        [Test]
        public void GetTestResultsUsesOnlyScopedMessagesForNonPassingXml()
        {
            WriteTestResults("<test-run result=\"Failed\" total=\"2\" passed=\"0\" failed=\"0\" inconclusive=\"1\" skipped=\"0\" duration=\"0.2\"><test-suite><test-case name=\"Reason\" fullname=\"Agent.Reason\" result=\"Inconclusive\"><reason><message>No assertions</message></reason><metadata><message>Wrong nested message</message></metadata></test-case><test-case name=\"Error\" fullname=\"Agent.Error\" result=\"Error\"><metadata><message>Wrong nested message</message></metadata></test-case></test-suite></test-run>");

            JObject result = RpcResult("get_test_results", new JObject { ["result_path"] = _resultPath });
            JArray failures = (JArray)result["failed_tests"];

            Assert.AreEqual("No assertions", failures[0]?["message"]?.ToString());
            Assert.AreEqual("Error", failures[1]?["message"]?.ToString());
        }

        [Test]
        public void ToolUsageStatsTrackCountsAndErrorsWithoutPayloads()
        {
            RpcResult("get_editor_state");
            JObject missingResponse = Rpc("agent_tooling_missing_method", new JObject { ["secret_payload"] = "do-not-store" });
            Assert.IsNotNull(missingResponse["error"]);

            JObject stats = RpcResult("get_tool_usage_stats");
            JArray tools = (JArray)stats["tools"];
            JObject editorState = tools.Children<JObject>().FirstOrDefault(t => t["method"]?.ToString() == "get_editor_state");
            JObject missing = tools.Children<JObject>().FirstOrDefault(t => t["method"]?.ToString() == "agent_tooling_missing_method");

            Assert.IsNotNull(editorState);
            Assert.GreaterOrEqual(editorState["count"].Value<int>(), 1);
            Assert.IsNotNull(missing);
            Assert.GreaterOrEqual(missing["error_count"].Value<int>(), 1);
            CollectionAssert.DoesNotContain(missing.Properties().Select(p => p.Name).ToArray(), "secret_payload");
            CollectionAssert.DoesNotContain(missing.Properties().Select(p => p.Name).ToArray(), "payload");
        }

        [Test]
        public void ResetToolUsageStatsClearsPreviousCalls()
        {
            RpcResult("get_editor_state");
            JObject reset = RpcResult("reset_tool_usage_stats");
            JObject stats = RpcResult("get_tool_usage_stats");
            JArray tools = (JArray)stats["tools"];

            Assert.AreEqual("Success", reset["status"]?.ToString());
            Assert.IsFalse(tools.Children<JObject>().Any(t => t["method"]?.ToString() == "get_editor_state"));
        }

        [Test]
        public void FastPathMethodsCanRunOffMainThread()
        {
            NexusConsoleLogMode originalLogMode = MCPSettings.ConsoleLogMode;
            string[] methods =
            {
                "get_server_status",
                "attach_existing_session",
                "wait_for_asset_import_idle",
                "wait_for_editor_idle"
            };

            try
            {
                MCPSettings.ConsoleLogMode = NexusConsoleLogMode.All;

                foreach (string method in methods)
                {
                    JObject response = Task.Run(() => Rpc(method, new JObject { ["timeout_seconds"] = 0 })).GetAwaiter().GetResult();

                    Assert.IsNull(response["error"], $"{method}: {response.ToString(Formatting.None)}");
                }
            }
            finally
            {
                MCPSettings.ConsoleLogMode = originalLogMode;
            }
        }

        [Test]
        public void BatchExecuteRejectsOversizedBatches()
        {
            var requests = new JArray();
            for (int i = 0; i < 51; i++)
            {
                requests.Add(new JObject { ["method"] = "get_server_status", ["params"] = new JObject() });
            }

            JObject response = Rpc("batch_execute", new JObject { ["requests"] = requests });

            StringAssert.Contains("at most 50 requests", response["error"]?["message"]?.ToString());
        }

        [Test]
        public void BatchExecuteRejectsRecursiveRequests()
        {
            JObject result = RpcResult("batch_execute", new JObject
            {
                ["requests"] = new JArray
                {
                    new JObject
                    {
                        ["method"] = "batch_execute",
                        ["params"] = new JObject { ["requests"] = new JArray() }
                    }
                }
            });

            Assert.AreEqual("Error", result["results"]?[0]?["status"]?.ToString());
            StringAssert.Contains("Recursive batch_execute", result["results"]?[0]?["message"]?.ToString());
        }

        [Test]
        public void UiWindowRectMethodsRoundTrip()
        {
            MCPTestWindow window = MCPTestWindow.ShowWindow();
            window.position = new Rect(80, 80, 420, 260);

            JObject setResult = RpcResult("ui_set_window_rect", new JObject
            {
                ["window_title"] = MCPTestWindow.WindowTitle,
                ["x"] = 90,
                ["y"] = 95,
                ["width"] = 390,
                ["height"] = 240
            });
            JObject getResult = RpcResult("ui_get_window_rect", new JObject { ["window_title"] = MCPTestWindow.WindowTitle });

            Assert.AreEqual("Success", setResult["status"]?.ToString());
            Assert.AreEqual("Success", getResult["status"]?.ToString());
            Assert.GreaterOrEqual(getResult["rect"]?["width"]?.Value<float>() ?? 0, window.minSize.x);
            Assert.GreaterOrEqual(getResult["rect"]?["height"]?.Value<float>() ?? 0, window.minSize.y);
        }

        [Test]
        public void UiCaptureWindowSnapshotReturnsRectHierarchyAndBestEffortImage()
        {
            MCPTestWindow window = MCPTestWindow.ShowWindow();
            window.position = new Rect(80, 80, 420, 260);

            JObject result = RpcResult("ui_capture_window_snapshot", new JObject
            {
                ["window_title"] = MCPTestWindow.WindowTitle,
                ["include_image"] = true,
                ["include_hierarchy"] = true
            });

            string status = result["status"]?.ToString();
            Assert.IsTrue(status == "Success" || status == "PartialSuccess", result.ToString(Formatting.None));
            Assert.IsNotNull(result["rect"]);
            Assert.IsNotNull(result["ui_hierarchy"]);

#if UNITY_EDITOR_OSX
            if (status == "Success")
                Assert.IsFalse(string.IsNullOrEmpty(result["image_base64"]?.ToString()));
            else
                Assert.IsFalse(string.IsNullOrEmpty(result["message"]?.ToString()));
#else
            Assert.AreEqual("PartialSuccess", status);
            Assert.IsNull(result["image_base64"]);
#endif
        }

        [Test]
        public void CreateScriptableObjectAssetRejectsAbstractTypes()
        {
            var response = Rpc("create_scriptable_object_asset", new JObject
            {
                ["type"] = typeof(AbstractScriptableObjectProbe).FullName,
                ["path"] = "Assets/AbstractProbe.asset"
            });

            Assert.IsNotNull(response["error"]);
            string message = response["error"]["message"]?.ToString();
            Assert.IsTrue(message.Contains("is abstract and cannot be instantiated"), message);
        }

        [Test]
        public void ListFieldsForTypeRejectsAbstractTypes()
        {
            var response = Rpc("list_fields_for_type", new JObject
            {
                ["type"] = typeof(AbstractScriptableObjectProbe).FullName
            });

            Assert.IsNotNull(response["error"]);
            string message = response["error"]["message"]?.ToString();
            Assert.IsTrue(message.Contains("is abstract and cannot be instantiated"), message);
        }

        [Test]
        public void FindObjectsHandlesInvalidRegexAndPerformsLiteralSearch()
        {
            var go = new GameObject("TestObject (1)");
            try
            {
                JObject result = RpcResult("find_objects", new JObject { ["name"] = "TestObject (1)" });
                JArray objects = (JArray)result["objects"];
                Assert.IsNotNull(objects);
                Assert.IsTrue(objects.Any(o => o["name"]?.ToString() == "TestObject (1)"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FindObjectsMatchesValidRegexPattern()
        {
            var go = new GameObject("RegexObject123");
            try
            {
                JObject result = RpcResult("find_objects", new JObject { ["name"] = "^RegexObject\\d+$" });
                JArray objects = (JArray)result["objects"];
                Assert.IsNotNull(objects);
                Assert.IsTrue(objects.Any(o => o["name"]?.ToString() == "RegexObject123"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private void WriteTestResults(string xml)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));
            File.WriteAllText(_resultPath, xml);
        }

        private static JObject RpcResult(string method, JObject parameters = null)
        {
            JObject response = Rpc(method, parameters);
            Assert.IsNull(response["error"], response.ToString(Formatting.None));
            return (JObject)response["result"];
        }

        private static JObject Rpc(string method, JObject parameters = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters ?? new JObject(),
                ["id"] = 1
            };

            return JObject.Parse(MCPServerMethods.ProcessJsonRpc(request.ToString(Formatting.None)));
        }
    }

    public abstract class AbstractScriptableObjectProbe : ScriptableObject
    {
    }
}
