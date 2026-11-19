// EditMode tests for HTTP, CLI, and legacy projections of the canonical envelope.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    /// <summary>Legacy screenshot bodies, list_tools, and HTTP/CLI/Pipeline projections of the canonical envelope.</summary>
    public partial class NexusCommandEnvelopeTests
    {
        [Test]
        public void LegacyScreenshot_Success_HasNoEnvelope()
        {
            byte[] png = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1 };
            JObject result = MCPServerMethods.CreateScreenshotResult(
                true, "Game View screenshot captured.", new MCPServerMethods.ScreenshotPayload(png, new Vector2Int(8, 4)), 1.25);
            Assert.AreEqual("Success", result["status"]?.ToString());
            Assert.AreEqual("png", result["format"]?.ToString());
            Assert.IsFalse(string.IsNullOrEmpty(result["image_base64"]?.ToString()));
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]?["image_base64"]?.ToString()));
            Assert.IsFalse(result.ContainsKey("ok"));
            Assert.IsFalse(result.ContainsKey("meta"));
            Assert.IsFalse(result.ContainsKey("error"));
        }

        [Test]
        public void LegacyScreenshot_Failure_StaysFailedBody()
        {
            JObject result = MCPServerMethods.CreateScreenshotResult(
                false, "capture failed", new MCPServerMethods.ScreenshotPayload(null, Vector2Int.zero), 1);
            Assert.AreEqual("Failed", result["status"]?.ToString());
            Assert.IsFalse(result["success"].Value<bool>());
            Assert.AreEqual(string.Empty, result["data"]["image_base64"]?.ToString());
            Assert.IsFalse(result.ContainsKey("ok"));
            Assert.IsFalse(result.ContainsKey("meta"));
            Assert.IsFalse(result.ContainsKey("error"));
        }

        [Test]
        public void LegacyRawMethod_HasNoEnvelope()
        {
            JObject status = Rpc("get_server_status", new JObject(), 1);
            JObject result = status["result"] as JObject;
            Assert.IsNotNull(result);
            Assert.IsNull(status["error"]);
            Assert.IsFalse(result.ContainsKey("ok"));
            Assert.IsFalse(result.ContainsKey("meta"));
            Assert.IsFalse(result.ContainsKey("error"));

            string raw = MCPServerMethods.CreateErrorResponse(4, -32602, "bad argument");
            JObject error = JObject.Parse(raw);
            Assert.AreEqual(-32602, error["error"]["code"].Value<int>());
            Assert.IsNull(error["result"]);
            Assert.IsFalse(error["error"].ToString().Contains("retryable"));

            JObject tools = Rpc("list_tools", new JObject(), 1);
            Assert.AreEqual(121, tools["result"].Count());
            Assert.IsNull(tools["result"].FirstOrDefault(token => token["name"]?.ToString() == "nexus_project_map"));
        }

        [Test]
        public void ListTools_AdvertisesOptionalRequestIdOnCanonicalCommands()
        {
            JArray tools = (JArray)Rpc("list_tools", new JObject(), 1)["result"];
            foreach (string name in new[]
            {
                ProjectMapCommand.Id,
                GroupCompileErrorsCommand.Id,
                CaptureGameViewCommand.Id
            })
            {
                JObject tool = tools.First(token => token["name"]?.ToString() == name) as JObject;
                Assert.IsNotNull(tool, name);
                JObject props = (JObject)tool["inputSchema"]["properties"];
                Assert.AreEqual("string", props["request_id"]?["type"]?.ToString(), name);
                JToken required = tool["inputSchema"]["required"];
                if (required != null)
                    Assert.IsFalse(required.Any(token => token.ToString() == "request_id"), name);
            }
        }

        [Test]
        public void SyncProcessJsonRpc_Capture_StaysTransportError()
        {
            JObject parsed = Rpc("nexus.capture_game_view", new JObject(), 1);
            Assert.IsNull(parsed["result"]);
            Assert.AreEqual(-32000, parsed["error"]["code"].Value<int>());
            StringAssert.Contains("ProcessJsonRpcAsync", parsed["error"]["message"].Value<string>());
            Assert.IsFalse(parsed["error"].ToString().Contains("retryable"));
        }

        [Test]
        public void CanonicalCaptureData_IsNotLegacyScreenshot()
        {
            var data = new NexusCaptureResult
            {
                Success = true,
                Width = 8,
                Height = 8,
                Encoding = "jpg",
                Bytes = 3,
                Base64 = "abc",
                Source = "game_view"
            };
            JObject result = NexusCommandEnvelope.Success(data, null, 1).ToJson();
            Assert.AreEqual("jpg", result["data"]["encoding"]?.ToString());
            Assert.AreEqual("abc", result["data"]["base64"]?.ToString());
            Assert.AreEqual("game_view", result["data"]["source"]?.ToString());
            Assert.IsFalse(((JObject)result["data"]).ContainsKey("image_base64"));
            Assert.IsFalse(((JObject)result["data"]).ContainsKey("status"));
            Assert.IsFalse(((JObject)result["data"]).ContainsKey("message"));
            Assert.IsFalse(((JObject)result["data"]).ContainsKey("duration_ms"));
            string command = File.ReadAllText(LocateCommand("CaptureGameViewCommand.cs"));
            Assert.AreEqual("jpg", CaptureGameViewCommand.DefaultFormat);
            Assert.AreEqual("jpg", CaptureGameViewCommand.FormatOrDefault(null));
            StringAssert.Contains("FormatOrDefault(", command);
            JObject legacy = MCPServerMethods.CreateScreenshotResult(true, "ok", new MCPServerMethods.ScreenshotPayload(new byte[] { 1 }, new Vector2Int(1, 1)), 1);
            Assert.AreEqual("png", legacy["format"]?.ToString());
        }

        [Test]
        public void CanonicalProjectMap_IsNotBareDto()
        {
            JObject result = Result(Rpc("nexus.project_map", new JObject(), 1));
            Assert.IsTrue(result.ContainsKey("ok"));
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]["project_name"]?.ToString()));
            Assert.IsFalse(result.ContainsKey("project_name"));
            Assert.IsFalse(result.ContainsKey("execution_duration_ms"));
            Assert.IsTrue(result["data"]["success"].Value<bool>());
        }

        [Test]
        public void PipelineProjection_ReturnsEnvelopeType()
        {
            string path = Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Pipeline", "NexusPipelineCommands.cs");
            if (!File.Exists(path))
            {
                path = Path.Combine(
                    Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity",
                    "Editor", "Pipeline", "NexusPipelineCommands.cs");
            }

            string text = File.ReadAllText(path);
            StringAssert.Contains("NexusCommandEnvelope ProjectMap(", text);
            StringAssert.Contains("NexusCommandEnvelope GroupCompileErrors(", text);
            StringAssert.Contains("Task<NexusCommandEnvelope> CaptureGameView(", text);
            Assert.IsFalse(text.Contains("Task<NexusCaptureResult>"));
            Assert.IsFalse(text.Contains("GetAwaiter().GetResult()"));
            Assert.IsFalse(text.Contains(".Result"));

            Type pipeline = Type.GetType("UnityMCP.Editor.Pipeline.NexusPipelineCommands, UnityMCP.Editor.Pipeline");
            if (pipeline == null)
            {
                Assert.Ignore("Pipeline assembly is not loaded in this editor.");
                return;
            }

            MethodInfo method = pipeline.GetMethod("ProjectMap");
            Assert.IsNotNull(method);
            object envelope = method.Invoke(null, new object[] { "pipe-1" });
            JObject cli = JObject.FromObject(envelope);
            JObject http = Result(Rpc("nexus_project_map", new JObject { ["request_id"] = "pipe-1" }, 1));
            cli["meta"]["ms"] = 0;
            http["meta"]["ms"] = 0;
            cli["data"]["execution_duration_ms"] = 0;
            http["data"]["execution_duration_ms"] = 0;
            Assert.AreEqual(http.ToString(Formatting.None), cli.ToString(Formatting.None));
        }
    }
}
