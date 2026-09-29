using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Capture;

namespace UnityMCP.Editor.Tests
{
    public class CaptureGatewayTests
    {
        [SetUp]
        public void SetUp()
        {
            MCPServerMethods.Init();
        }

        [Test]
        public void CaptureRequestDefaultsToPngWithoutTransportFields()
        {
            var request = new CaptureRequest();
            Assert.AreEqual(CaptureFormat.Png, request.Format);
            Assert.AreEqual(85, request.JpegQuality);
            Assert.AreEqual(0, request.MaxLongEdge);
            Assert.IsNull(request.GetType().GetProperty("ImageBase64"));
            Assert.IsNull(request.GetType().GetProperty("JsonRpcId"));
        }

        [Test]
        public void ParseGameViewCaptureRequestMapsOptionalSemanticOptions()
        {
            CaptureRequest request = MCPServerMethods.ParseGameViewCaptureRequest(new JObject
            {
                ["format"] = "jpeg",
                ["quality"] = 70,
                ["width"] = 1600,
                ["height"] = 900,
                ["max_long_edge"] = 1600,
                ["include_telemetry"] = true
            });

            Assert.AreEqual(CaptureFormat.Jpeg, request.Format);
            Assert.AreEqual(70, request.JpegQuality);
            Assert.AreEqual(1600, request.RequestedWidth);
            Assert.AreEqual(900, request.RequestedHeight);
            Assert.AreEqual(1600, request.MaxLongEdge);
            Assert.IsTrue(request.IncludeTelemetry);
        }

        [Test]
        public void ResolveOutputSizeHonorsMaxLongEdge()
        {
            var request = new CaptureRequest { MaxLongEdge = 1600 };
            Vector2Int size = GameViewCaptureSource.ResolveOutputSize(1920, 1080, request);
            Assert.AreEqual(1600, size.x);
            Assert.AreEqual(900, size.y);
        }

        [Test]
        public void JpegQualityIsClampedToInclusiveRange()
        {
            Assert.AreEqual(1, CaptureEncoder.ClampJpegQuality(0));
            Assert.AreEqual(100, CaptureEncoder.ClampJpegQuality(250));
            Assert.AreEqual(85, CaptureEncoder.ClampJpegQuality(85));
        }

        [Test]
        public void CaptureResultSchemaUsesBytesNotBase64()
        {
            var result = new CaptureResult
            {
                CompressedBytes = new byte[] { 1, 2, 3 },
                Format = CaptureFormat.Png,
                Width = 8,
                Height = 8,
                Source = "game_view"
            };

            Assert.AreEqual(3, result.CompressedBytes.Length);
            Assert.IsNull(result.GetType().GetProperty("Base64"));
            Assert.AreEqual("png", CaptureEncoder.ToWireFormat(result.Format));
            Assert.AreEqual("jpg", CaptureEncoder.ToWireFormat(CaptureFormat.Jpeg));
        }

        [Test]
        public void LegacyScreenshotResultKeepsPngSchema()
        {
            byte[] png = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3 };
            JObject result = MCPServerMethods.CreateScreenshotResult(
                true, "Game View screenshot captured.", png, new Vector2Int(64, 32), 1.5);

            Assert.AreEqual("Success", result["status"]?.ToString());
            Assert.IsTrue(result["success"]?.Value<bool>() ?? false);
            Assert.AreEqual("png", result["format"]?.ToString());
            Assert.AreEqual("png", result["data"]?["format"]?.ToString());
            Assert.AreEqual(64, result["data"]?["width"]?.Value<int>());
            Assert.AreEqual(32, result["data"]?["height"]?.Value<int>());
            Assert.IsFalse(string.IsNullOrEmpty(result["image_base64"]?.ToString()));
            Assert.IsFalse(string.IsNullOrEmpty(result["data"]?["image_base64"]?.ToString()));
        }

        [UnityTest]
        public IEnumerator MissingGameViewFailsWithDomainError()
        {
            CloseGameViews();
            yield return null;
            var task = CaptureGateway.Shared.CaptureGameView(new CaptureRequest(), CancellationToken.None);
            while (!task.IsCompleted)
                yield return null;
            Assert.IsTrue(task.IsFaulted);
            Exception ex = task.Exception != null ? task.Exception.GetBaseException() : null;
            Assert.IsInstanceOf<CaptureException>(ex);
            Assert.AreEqual(CaptureErrorCode.GameViewUnavailable, ((CaptureException)ex).Code);
        }

        [Test]
        public void CaptureSourcesStayTransportIndependent()
        {
            string directory = LocateCaptureDirectory();
            Assert.IsNotNull(directory);
            string[] banned =
            {
                "System.Net",
                "HttpListener",
                "Python",
                "Unity.Pipeline",
                "RequestIntoNativeArray",
                "Convert.ToBase64String",
                "JsonRpc"
            };

            foreach (string path in Directory.GetFiles(directory, "*.cs"))
            {
                string text = File.ReadAllText(path);
                foreach (string token in banned)
                {
                    Assert.IsFalse(text.Contains(token), $"{Path.GetFileName(path)} contains {token}");
                }
            }

            string readback = File.ReadAllText(Path.Combine(directory, "DriverOwnedReadback.cs"));
            Assert.IsTrue(readback.Contains("AsyncGPUReadback.Request("));
            Assert.IsTrue(readback.Contains("GetData<byte>()"));
            Assert.IsFalse(readback.Contains("WaitForCompletion"));
            Assert.IsFalse(readback.Contains("RequestIntoNativeArray"));
        }

        [Test]
        public void DomainReloadHookRejectsNewCaptureWithoutGpuWait()
        {
            DriverOwnedReadback.ResetReloadingForTests();
            Assert.AreEqual(0, DriverOwnedReadback.PendingCount);
            DriverOwnedReadback.NotifyDomainReloadForTests();
            try
            {
                var ex = Assert.Throws<CaptureException>(() =>
                    CaptureGateway.Shared.CaptureGameView(new CaptureRequest(), CancellationToken.None)
                        .GetAwaiter().GetResult());
                Assert.AreEqual(CaptureErrorCode.DomainReloadInterrupted, ex.Code);
            }
            finally
            {
                DriverOwnedReadback.ResetReloadingForTests();
            }
        }

        [Test]
        public void PipelineCaptureCommandDoesNotBlockOnGetResult()
        {
            string path = Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Pipeline", "NexusPipelineCommands.cs");
            if (!File.Exists(path))
            {
                path = Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity",
                    "Editor", "Pipeline", "NexusPipelineCommands.cs");
            }

            if (!File.Exists(path)) Assert.Ignore("Pipeline command wrapper source should exist in the package.");
            string text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("GetAwaiter().GetResult()"));
            Assert.IsFalse(text.Contains(".Result"));
            Assert.IsTrue(text.Contains("Task<NexusCaptureResult>"));
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

        private static string LocateCaptureDirectory()
        {
            string[] guids = AssetDatabase.FindAssets("CaptureGateway t:MonoScript");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (assetPath.Contains("/Editor/Capture/"))
                {
                    return Path.GetDirectoryName(assetPath);
                }
            }

            string[] fallbacks =
            {
                Path.Combine(Application.dataPath, "NexusUnity", "Editor", "Capture"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.forkhorizon.nexus.unity", "Editor", "Capture")
            };
            return fallbacks.FirstOrDefault(Directory.Exists);
        }
    }
}
