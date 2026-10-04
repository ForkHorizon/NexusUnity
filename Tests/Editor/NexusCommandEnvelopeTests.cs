using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Capture;
using UnityMCP.Editor.Commands;
using UnityMCP.Editor.Runtime;

namespace UnityMCP.Editor.Tests
{
    public class NexusCommandEnvelopeTests
    {
        [SetUp]
        public void InitRegistry()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void SuccessEnvelope_ProjectMap_EchoesRequestIdAndGen()
        {
            JObject parsed = Rpc("nexus.project_map", new JObject { ["request_id"] = "req-1" }, 7);
            JObject result = Result(parsed);
            Assert.AreEqual(7, parsed["id"].Value<int>());
            Assert.IsTrue(result["ok"].Value<bool>());
            Assert.AreEqual(JTokenType.Null, result["error"].Type);
            Assert.AreEqual("req-1", result["meta"]["request_id"].Value<string>());
            Assert.AreEqual(MCPServer.SessionGeneration, result["meta"]["gen"].Value<int>());
            Assert.AreEqual(NexusRuntimeSelector.ToWireName(NexusRuntimeHost.EffectiveMode), result["meta"]["mode"].Value<string>());
            Assert.That(result["meta"]["mode"].Value<string>(), Is.EqualTo("legacy").Or.EqualTo("pipeline"));
            Assert.GreaterOrEqual(result["meta"]["ms"].Value<long>(), 0);
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]["project_name"]?.ToString()));
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]["project_path"]?.ToString()));
            Assert.AreEqual(Application.unityVersion, result["data"]["unity_version"]?.ToString());
            Assert.IsFalse(((JObject)result["data"]).ContainsKey("request_id"));
            Assert.IsFalse(result.ContainsKey("project_name"));
            AssertNoPending(result);
            AssertNoStack(result);
        }

        [Test]
        public void SuccessEnvelope_AliasMatchesCanonicalId()
        {
            JObject canonical = Result(Rpc("nexus.project_map", new JObject { ["request_id"] = "req-alias" }, 3));
            JObject alias = Result(Rpc("nexus_project_map", new JObject { ["request_id"] = "req-alias" }, 4));
            Assert.AreEqual(canonical["ok"].Value<bool>(), alias["ok"].Value<bool>());
            Assert.AreEqual(canonical["data"]["project_path"]?.ToString(), alias["data"]["project_path"]?.ToString());
            Assert.AreEqual("req-alias", alias["meta"]["request_id"]?.ToString());
        }

        [Test]
        public void SuccessEnvelope_CompileErrorsAreSuccessWhenNotCompiling()
        {
            if (EditorApplication.isCompiling)
                Assert.Inconclusive("The editor is compiling, so this read is partial rather than success.");

            JObject result = Result(Rpc("nexus.group_compile_errors", new JObject { ["max_logs"] = 20 }, 1));
            Assert.IsTrue(result["ok"].Value<bool>());
            Assert.AreEqual(JTokenType.Null, result["error"].Type);
            Assert.IsFalse(result["data"]["is_compiling"].Value<bool>());
            Assert.AreEqual(result["data"]["group_count"].Value<int>(), result["data"]["groups"].Count());
            Assert.GreaterOrEqual(result["data"]["total_errors"].Value<int>(), 0);
        }

        [Test]
        public void SuccessEnvelope_UnsavedSceneIsNotUnavailable()
        {
            JObject result = Result(Rpc("nexus.project_map", new JObject(), 1));
            Assert.IsTrue(result["ok"].Value<bool>());
            Assert.AreEqual(JTokenType.Null, result["error"].Type);
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]["active_scene"]?.ToString()));
            Assert.AreNotEqual(NexusCommandCodes.Unavailable, ErrorCode(result));
        }

        [Test]
        public void InvalidArgument_UnsupportedFormat_BothNames()
        {
            foreach (string name in new[] { "nexus.capture_game_view", "nexus_capture_game_view" })
            {
                JObject parsed = RpcAsyncCompleted(name, new JObject { ["format"] = "bmp" }, 9);
                Assert.IsNull(parsed["error"], parsed.ToString(Formatting.None));
                JObject result = Result(parsed);
                Assert.IsFalse(result["ok"].Value<bool>());
                Assert.AreEqual(NexusCommandCodes.InvalidArgument, result["error"]["code"].Value<string>());
                Assert.IsFalse(result["error"]["retryable"].Value<bool>());
                Assert.IsFalse(string.IsNullOrEmpty(result["error"]["hint"]?.ToString()));
                Assert.AreEqual(JTokenType.Null, result["data"].Type);
                Assert.AreEqual("Unsupported capture format.", result["error"]["message"].Value<string>());
                AssertNoPending(result);
                AssertNoStack(result);
            }
        }

        [Test]
        public void InvalidArgument_BadMaxLogs_DoesNotCallLogs()
        {
            JObject text = Result(Rpc("nexus.group_compile_errors", new JObject { ["max_logs"] = "abc" }, 1));
            JObject negative = Result(Rpc("nexus.group_compile_errors", new JObject { ["max_logs"] = -1 }, 1));
            foreach (JObject result in new[] { text, negative })
            {
                Assert.IsFalse(result["ok"].Value<bool>());
                Assert.AreEqual(NexusCommandCodes.InvalidArgument, result["error"]["code"].Value<string>());
                Assert.IsFalse(result["error"]["retryable"].Value<bool>());
                StringAssert.Contains("max_logs", result["error"]["message"].Value<string>());
                Assert.AreEqual(JTokenType.Null, result["data"].Type);
            }

            if (!EditorApplication.isCompiling)
                Assert.IsTrue(Result(Rpc("nexus.group_compile_errors", new JObject(), 1))["ok"].Value<bool>());
        }

        [Test]
        public void InvalidArgument_NonStringRequestId_SkipsHandler()
        {
            JObject result = Result(Rpc("nexus.project_map", new JObject { ["request_id"] = 12 }, 5));
            Assert.IsFalse(result["ok"].Value<bool>());
            Assert.AreEqual(NexusCommandCodes.InvalidArgument, result["error"]["code"].Value<string>());
            Assert.AreEqual(JTokenType.Null, result["meta"]["request_id"].Type);
            Assert.AreEqual(JTokenType.Null, result["data"].Type);
            Assert.IsFalse(result["error"]["retryable"].Value<bool>());
        }

        [UnityTest]
        public IEnumerator ClampedQualityAndNegativeWidth_AreNotInvalidArgument()
        {
            JObject[] cases =
            {
                new JObject { ["quality"] = 0 },
                new JObject { ["quality"] = 250 },
                new JObject { ["width"] = -4 }
            };
            foreach (JObject parameters in cases)
            {
                Task<string> task = StartAsync("nexus.capture_game_view", parameters, 1);
                for (int frame = 0; frame < 900 && !task.IsCompleted; frame++)
                    yield return null;
                Assert.IsTrue(task.IsCompleted, "The capture envelope did not finish.");
                if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().ToString());
                JObject parsed = JObject.Parse(task.Result);
                Assert.IsNull(parsed["error"], parsed.ToString(Formatting.None));
                JObject result = Result(parsed);
                Assert.AreNotEqual(NexusCommandCodes.InvalidArgument, ErrorCode(result));
                if (!result["ok"].Value<bool>())
                    Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            }
        }

        [UnityTest]
        public IEnumerator Unavailable_MissingGameView_IsResultNotJsonRpcError()
        {
            CloseGameViews();
            yield return null;
            try
            {
                Task<string> canonical = StartAsync("nexus.capture_game_view", new JObject(), 2);
                for (int frame = 0; frame < 900 && !canonical.IsCompleted; frame++)
                    yield return null;
                Assert.IsTrue(canonical.IsCompleted, "The capture envelope did not finish.");
                JObject parsed = JObject.Parse(canonical.Result);
                Assert.IsNull(parsed["error"], parsed.ToString(Formatting.None));
                JObject result = Result(parsed);
                Assert.IsFalse(result["ok"].Value<bool>());
                Assert.AreEqual(NexusCommandCodes.Unavailable, result["error"]["code"].Value<string>());
                Assert.IsTrue(result["error"]["retryable"].Value<bool>());
                Assert.AreEqual("Game View window not found or not open.", result["error"]["message"].Value<string>());
                AssertNoStack(parsed);
                AssertNoStack(result);

                Task<string> legacy = StartAsync("capture_game_view_screenshot", new JObject(), 3);
                for (int frame = 0; frame < 900 && !legacy.IsCompleted; frame++)
                    yield return null;
                Assert.IsTrue(legacy.IsCompleted, "The legacy screenshot call did not finish.");
                JObject legacyParsed = JObject.Parse(legacy.Result);
                Assert.IsNull(legacyParsed["result"]);
                Assert.AreEqual(-32000, legacyParsed["error"]["code"].Value<int>());
                Assert.IsFalse(string.IsNullOrEmpty(legacyParsed["error"]["data"]?["stackTrace"]?.ToString()));
                Assert.IsFalse(legacyParsed["error"].ToString().Contains("retryable"));
            }
            finally
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
        }

        [Test]
        public void Unavailable_EmptySource_SameCodeDifferentMessage()
        {
            JObject result = Project(new CaptureException(
                CaptureErrorCode.CaptureSourceEmpty, "Game View window has no capturable area."));
            Assert.AreEqual(NexusCommandCodes.Unavailable, result["error"]["code"].Value<string>());
            Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            Assert.AreEqual("Game View window has no capturable area.", result["error"]["message"].Value<string>());
        }

        [Test]
        public void Busy_CaptureInProgress_IsNotPending()
        {
            JObject result = Project(new CaptureException(
                CaptureErrorCode.CaptureBusy, "A Game View capture is already in progress."));
            Assert.AreEqual(NexusCommandCodes.Busy, result["error"]["code"].Value<string>());
            Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            AssertNoPending(result);
            Assert.IsFalse(result.ToString(Formatting.None).Contains("\"job\""));
            Assert.IsFalse(result.ToString(Formatting.None).Contains("\"next\""));
        }

        [Test]
        public void Timeout_Readback_IsRetryableAndHasNoUnknownOutcome()
        {
            JObject result = Project(new CaptureException(CaptureErrorCode.ReadbackFailed, "GPU readback timed out."));
            Assert.AreEqual(NexusCommandCodes.Timeout, result["error"]["code"].Value<string>());
            Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            Assert.IsFalse(((JObject)result["error"]).ContainsKey("outcome"));
        }

        [Test]
        public void CaptureFailed_ReadbackError_IsNotTimeoutOrInternal()
        {
            JObject result = Project(new CaptureException(CaptureErrorCode.ReadbackFailed, "GPU readback failed."));
            Assert.AreEqual(NexusCommandCodes.CaptureFailed, result["error"]["code"].Value<string>());
            Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            Assert.AreNotEqual(NexusCommandCodes.Timeout, result["error"]["code"].Value<string>());
            Assert.AreNotEqual(NexusCommandCodes.Internal, result["error"]["code"].Value<string>());
        }

        [Test]
        public void Partial_CompilingErrorList_DoesNotTrustDtoSuccess()
        {
            var data = new NexusCompileErrorsResult
            {
                Success = true,
                IsCompiling = true,
                TotalErrors = 1,
                GroupCount = 1,
                Groups = new List<NexusCompileErrorGroup>
                {
                    new NexusCompileErrorGroup { File = "A.cs", Count = 1, Errors = new List<string> { "error CS1001" } }
                }
            };
            JObject result = NexusCommandEnvelope.ProjectCompile(data, "partial-1", 4).ToJson();
            Assert.IsFalse(result["ok"].Value<bool>());
            Assert.AreEqual(NexusCommandCodes.Partial, result["error"]["code"].Value<string>());
            Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            StringAssert.Contains("compilation", result["error"]["hint"].Value<string>());
            Assert.IsTrue(result["data"]["is_compiling"].Value<bool>());
            Assert.AreEqual(1, result["data"]["groups"].Count());
            Assert.IsTrue(result["data"]["success"].Value<bool>());
            Assert.AreEqual("partial-1", result["meta"]["request_id"].Value<string>());
        }

        [Test]
        public void HttpCompileResult_MatchesProjector()
        {
            JObject http = Result(Rpc("nexus.group_compile_errors", new JObject { ["max_logs"] = 20, ["request_id"] = "cmp" }, 1));
            JObject alias = Result(Rpc("nexus_group_compile_errors", new JObject { ["max_logs"] = 20, ["request_id"] = "cmp" }, 1));
            JObject projected = GroupCompileErrorsCommand.ExecuteEnveloped(20, "cmp").ToJson();
            AssertSamePayload(projected, http);
            AssertSamePayload(http, alias);
        }

        [Test]
        public void EditorNotReady_DomainReload_IsRetryable()
        {
            JObject rejected = Project(new CaptureException(
                CaptureErrorCode.DomainReloadInterrupted, "Capture rejected during domain reload."));
            JObject cancelled = Project(new CaptureException(
                CaptureErrorCode.DomainReloadInterrupted,
                "Capture cancelled because a domain reload began while GPU readback was pending."));
            foreach (JObject result in new[] { rejected, cancelled })
            {
                Assert.AreEqual(NexusCommandCodes.EditorNotReady, result["error"]["code"].Value<string>());
                Assert.IsTrue(result["error"]["retryable"].Value<bool>());
            }

            DriverOwnedReadback.NotifyDomainReloadForTests();
            try
            {
                Task<string> task = StartAsync("nexus.capture_game_view", new JObject { ["request_id"] = "reload-1" }, 8);
                Assert.IsTrue(task.IsCompleted, "A reload rejection must finish without waiting on the editor loop.");
                JObject live = Result(JObject.Parse(task.Result));
                Assert.IsFalse(live["ok"].Value<bool>());
                Assert.AreEqual(NexusCommandCodes.EditorNotReady, live["error"]["code"].Value<string>());
                Assert.IsTrue(live["error"]["retryable"].Value<bool>());
                Assert.AreEqual("reload-1", live["meta"]["request_id"].Value<string>());
                AssertNoStack(live);
            }
            finally
            {
                DriverOwnedReadback.ResetReloadingForTests();
            }
        }

        [Test]
        public void Internal_UnexpectedException_RetryableFalse_NoStack()
        {
            string previous = Environment.GetEnvironmentVariable("NEXUS_DEBUG");
            Environment.SetEnvironmentVariable("NEXUS_DEBUG", null);
            try
            {
                JObject result = NexusCommandEnvelope.FromException(Boom(), "in-1", 2).ToJson();
                Assert.AreEqual(NexusCommandCodes.Internal, result["error"]["code"].Value<string>());
                Assert.IsFalse(result["error"]["retryable"].Value<bool>());
                Assert.IsFalse(string.IsNullOrEmpty(result["error"]["hint"]?.ToString()));
                Assert.AreEqual("encoder fault", result["error"]["message"].Value<string>());
                string json = result.ToString(Formatting.None);
                Assert.IsFalse(json.Contains("stack"));
                Assert.IsFalse(json.Contains("at UnityMCP."));
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEXUS_DEBUG", previous);
            }
        }

        [Test]
        public void DebugEnv_AddsStackOnlyWhenSet()
        {
            string previous = Environment.GetEnvironmentVariable("NEXUS_DEBUG");
            Exception exception = Boom();
            try
            {
                Environment.SetEnvironmentVariable("NEXUS_DEBUG", null);
                string quiet = NexusCommandEnvelope.FromException(exception, null, 1).ToJson().ToString(Formatting.None);
                Assert.IsFalse(quiet.Contains("stack_trace"));
                Environment.SetEnvironmentVariable("NEXUS_DEBUG", "1");
                JObject loud = NexusCommandEnvelope.FromException(exception, null, 1).ToJson();
                Assert.IsFalse(string.IsNullOrEmpty(loud["error"]["stack_trace"]?.ToString()));
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEXUS_DEBUG", previous);
            }
        }

        [Test]
        public void UnknownMutationOutcomeForcesRetryableFalse()
        {
            JObject result = NexusCommandEnvelope.Failure(
                NexusCommandCodes.Timeout,
                "The mutation timed out.",
                true,
                "call again",
                null,
                "mut-1",
                9,
                NexusCommandCodes.UnknownOutcome).ToJson();
            Assert.IsFalse(result["ok"].Value<bool>());
            Assert.AreEqual(NexusCommandCodes.Timeout, result["error"]["code"].Value<string>());
            Assert.IsFalse(result["error"]["retryable"].Value<bool>());
            Assert.AreEqual(NexusCommandCodes.UnknownOutcome, result["error"]["outcome"].Value<string>());
            Assert.AreEqual("mut-1", result["meta"]["request_id"].Value<string>());
            StringAssert.Contains("Do not retry", result["error"]["hint"].Value<string>());
            Assert.IsFalse(result["error"]["hint"].Value<string>().Contains("call again"));
        }

        [Test]
        public void RequestIdOmitted_IsNull_NotJsonRpcId()
        {
            JObject parsed = Rpc("nexus.project_map", new JObject(), "abc");
            Assert.AreEqual("abc", parsed["id"].Value<string>());
            Assert.AreEqual(JTokenType.Null, Result(parsed)["meta"]["request_id"].Type);
        }

        [Test]
        public void MetaShape_Types()
        {
            JObject success = Result(Rpc("nexus.project_map", new JObject { ["request_id"] = "shape" }, 1));
            Assert.AreEqual(JTokenType.Boolean, success["ok"].Type);
            Assert.AreEqual(JTokenType.Null, success["error"].Type);
            Assert.AreEqual(JTokenType.Integer, success["meta"]["ms"].Type);
            Assert.AreEqual(JTokenType.Integer, success["meta"]["gen"].Type);
            Assert.AreEqual(JTokenType.String, success["meta"]["mode"].Type);
            Assert.AreEqual(JTokenType.String, success["meta"]["request_id"].Type);

            JObject error = Result(Rpc("nexus.group_compile_errors", new JObject { ["max_logs"] = "nope" }, 1))["error"] as JObject;
            CollectionAssert.AreEquivalent(
                new[] { "code", "message", "retryable", "hint" },
                error.Properties().Select(property => property.Name).ToArray());
            Assert.AreEqual(JTokenType.Boolean, error["retryable"].Type);
            Assert.AreEqual(JTokenType.String, error["code"].Type);
        }

        [Test]
        public void LegacyScreenshot_Success_HasNoEnvelope()
        {
            byte[] png = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1 };
            JObject result = MCPServerMethods.CreateScreenshotResult(
                true, "Game View screenshot captured.", png, new Vector2Int(8, 4), 1.25);
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
                false, "capture failed", null, Vector2Int.zero, 1);
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
            Assert.AreEqual(120, tools["result"].Count());
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
            StringAssert.Contains("format ?? \"jpg\"", command);
            JObject legacy = MCPServerMethods.CreateScreenshotResult(true, "ok", new byte[] { 1 }, new Vector2Int(1, 1), 1);
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

        private static JObject Project(CaptureException exception)
        {
            return NexusCommandEnvelope.FromException(exception, "cap", 1).ToJson();
        }

        private static void AssertSamePayload(JObject expected, JObject actual)
        {
            Assert.AreEqual(expected["ok"]?.ToString(), actual["ok"]?.ToString());
            Assert.AreEqual(expected["error"]?.ToString(Formatting.None), actual["error"]?.ToString(Formatting.None));
            JObject left = expected["data"] as JObject;
            JObject right = actual["data"] as JObject;
            if (left != null) left = (JObject)left.DeepClone();
            if (right != null) right = (JObject)right.DeepClone();
            left?.Remove("execution_duration_ms");
            right?.Remove("execution_duration_ms");
            Assert.AreEqual(left?.ToString(Formatting.None), right?.ToString(Formatting.None));
            Assert.AreEqual(expected["meta"]["request_id"]?.ToString(), actual["meta"]["request_id"]?.ToString());
        }

        private static void AssertNoPending(JObject result)
        {
            string json = result.ToString(Formatting.None);
            Assert.IsFalse(json.Contains("\"pending\""));
            Assert.IsFalse(json.Contains("\"state\""));
            Assert.AreNotEqual("pending", ErrorCode(result));
        }

        /// <summary>Reads error.code, treating a JSON null error as no code.</summary>
        private static string ErrorCode(JObject result)
        {
            JToken error = result?["error"];
            if (error == null || error.Type != JTokenType.Object)
                return null;
            return error["code"]?.ToString();
        }

        private static void AssertNoStack(JObject token)
        {
            string json = token.ToString(Formatting.None);
            Assert.IsFalse(json.Contains("stackTrace"));
            Assert.IsFalse(json.Contains("stack_trace"));
        }

        private static Exception Boom()
        {
            try
            {
                throw new InvalidOperationException("encoder fault");
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static Task<string> StartAsync(string method, JObject parameters, JToken id)
        {
            return MCPServerMethods.ProcessJsonRpcAsync(Request(method, parameters, id));
        }

        private static JObject RpcAsyncCompleted(string method, JObject parameters, JToken id)
        {
            Task<string> task = StartAsync(method, parameters, id);
            Assert.IsTrue(task.IsCompleted, "This failure must be returned without waiting on the editor loop.");
            return JObject.Parse(task.Result);
        }

        private static JObject Rpc(string method, JToken parameters, JToken id)
        {
            return JObject.Parse(MCPServerMethods.ProcessJsonRpc(Request(method, parameters, id)));
        }

        private static string Request(string method, JToken parameters, JToken id)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters ?? new JObject(),
                ["id"] = id ?? 1
            }.ToString(Formatting.None);
        }

        private static JObject Result(JObject parsed)
        {
            JObject result = parsed["result"] as JObject;
            Assert.IsNotNull(result, parsed.ToString(Formatting.None));
            return result;
        }

        private static void CloseGameViews()
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(candidate => candidate != null && candidate.GetType().Name == "GameView")
                .ToArray())
            {
                window.Close();
            }
        }

        private static string LocateCommand(string fileName)
        {
            string[] roots =
            {
                Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Commands", fileName),
                Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity", "Editor", "Commands", fileName)
            };
            return roots.First(File.Exists);
        }
    }
}
