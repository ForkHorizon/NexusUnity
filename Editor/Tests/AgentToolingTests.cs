using System.IO;
using System.Linq;
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
}
