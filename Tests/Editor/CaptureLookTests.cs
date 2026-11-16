// EditMode tests for canonical look capture: framing, cropping, delivery, and arguments.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Capture;
using UnityMCP.Editor.Commands;

namespace UnityMCP.Editor.Tests
{
    public class CaptureLookTests
    {
        [Test]
        public void FullFrameCropUsesTheOverlayFlip()
        {
            CaptureFraming.CropScale(new RectInt(0, 0, 1920, 1080), 1920, 1080, out Vector2 scale, out Vector2 offset);
            Assert.AreEqual(1f, scale.x, 0.0001f);
            Assert.AreEqual(-1f, scale.y, 0.0001f);
            Assert.AreEqual(0f, offset.x, 0.0001f);
            Assert.AreEqual(1f, offset.y, 0.0001f);
        }

        [Test]
        public void PartialCropKeepsTheSameFlipDirection()
        {
            CaptureFraming.CropScale(new RectInt(10, 20, 100, 50), 200, 100, out Vector2 scale, out Vector2 offset);
            Assert.AreEqual(0.5f, scale.x, 0.0001f);
            Assert.AreEqual(-0.5f, scale.y, 0.0001f);
            Assert.AreEqual(0.05f, offset.x, 0.0001f);
            Assert.AreEqual(0.7f, offset.y, 0.0001f);
        }

        [Test]
        public void CropOutsideTheFrameIsNotVisible()
        {
            RectInt outside = CaptureFraming.IntersectPixels(new Vector2(5000f, 5000f), new Vector2(5100f, 5100f), 1920, 1080);
            Assert.AreEqual(0, outside.width);
            var ex = Assert.Throws<CaptureException>(() => CaptureFraming.RequireVisible(outside));
            Assert.AreEqual(CaptureErrorCode.TargetNotVisible, ex.Code);
        }

        [Test]
        public void TinyCropIsNotVisible()
        {
            RectInt tiny = CaptureFraming.IntersectPixels(new Vector2(1f, 1f), new Vector2(3f, 8f), 100, 100);
            Assert.Less(tiny.width, CaptureFraming.MinimumCropPixels);
            Assert.Throws<CaptureException>(() => CaptureFraming.RequireVisible(tiny));
        }

        [Test]
        public void DefaultLongEdgeFitsTheTokenBudgetAndDoesNotUpscale()
        {
            var cap = new CaptureRequest { MaxLongEdge = CaptureGameViewCommand.DefaultMaxLongEdge };
            Vector2Int wide = GameViewCaptureSource.ResolveOutputSize(1920, 1080, cap);
            Vector2Int square = GameViewCaptureSource.ResolveOutputSize(2000, 2000, cap);
            Vector2Int portrait = GameViewCaptureSource.ResolveOutputSize(1080, 1920, cap);
            Vector2Int small = GameViewCaptureSource.ResolveOutputSize(320, 180, cap);
            Vector2Int strip = GameViewCaptureSource.ResolveOutputSize(2000, 200, cap);
            Vector2Int explicitZero = GameViewCaptureSource.ResolveOutputSize(1920, 1080, new CaptureRequest());
            Assert.AreEqual(new Vector2Int(768, 432), wide);
            Assert.AreEqual(new Vector2Int(768, 768), square);
            Assert.AreEqual(new Vector2Int(432, 768), portrait);
            Assert.AreEqual(new Vector2Int(320, 180), small);
            Assert.AreEqual(new Vector2Int(768, 77), strip);
            Assert.AreEqual(new Vector2Int(1920, 1080), explicitZero);
            Assert.LessOrEqual(wide.x * wide.y / 750.0, 800);
            Assert.LessOrEqual(square.x * square.y / 750.0, 800);

            var opted = new CaptureRequest { MaxLongEdge = 1024 };
            Assert.AreEqual(new Vector2Int(1024, 576), GameViewCaptureSource.ResolveOutputSize(1920, 1080, opted));
            Assert.AreEqual(new Vector2Int(1024, 1024), GameViewCaptureSource.ResolveOutputSize(2000, 2000, opted));
        }

        [Test]
        public void OmittedMaxEdgeIs768AndExplicitZeroStaysUnconstrained()
        {
            Assert.IsTrue(CaptureLookRequest.TryParse(new JObject(), 1, out CaptureLookCall omitted));
            Assert.AreEqual(768, omitted.Request.MaxLongEdge);
            Assert.AreEqual(CaptureView.Game, omitted.Request.View);
            Assert.IsFalse(omitted.WriteFile);

            Assert.IsTrue(CaptureLookRequest.TryParse(new JObject { ["max_dimension"] = 0 }, 1, out CaptureLookCall unconstrained));
            Assert.AreEqual(0, unconstrained.Request.MaxLongEdge);
            Assert.IsTrue(CaptureLookRequest.TryParse(
                new JObject { ["max_dimension"] = 0, ["max_long_edge"] = 1024 }, 1, out CaptureLookCall explicitZeroWins));
            Assert.AreEqual(0, explicitZeroWins.Request.MaxLongEdge);

            Assert.IsTrue(CaptureLookRequest.TryParse(new JObject { ["max_dimension"] = 1024 }, 1, out CaptureLookCall opted));
            Assert.AreEqual(1024, opted.Request.MaxLongEdge);

            CaptureRequest legacy = MCPServerMethods.ParseGameViewCaptureRequest(new JObject());
            Assert.AreEqual(CaptureFormat.Png, legacy.Format);
            Assert.AreEqual(0, legacy.MaxLongEdge);
        }

        [Test]
        public void MalformedLookArgumentsAreNotRetryable()
        {
            AssertRejected(new JObject { ["view"] = "both" }, "view");
            AssertRejected(new JObject { ["delivery"] = "both" }, "delivery");
            AssertRejected(new JObject { ["view"] = "scene" }, "target");
            Assert.IsTrue(CaptureLookRequest.TryParse(new JObject { ["instance_id"] = -3 }, 1, out CaptureLookCall sceneObject));
            Assert.AreEqual(-3, sceneObject.Request.TargetInstanceId);
            AssertRejected(new JObject { ["target"] = "" }, "target");
            AssertRejected(new JObject { ["target"] = "  " }, "target");
            AssertRejected(new JObject { ["target"] = "Cube", ["instance_id"] = 4 }, "both");
            AssertRejected(new JObject { ["max_dimension"] = "768" }, "integer");
        }

        [Test]
        public void FileDeliveryWritesOneEncoding()
        {
            string path = CaptureFileDelivery.Write(new byte[] { 0xff, 0xd8, 1 }, CaptureFormat.Jpeg);
            try
            {
                Assert.IsTrue(path.EndsWith(".jpg", StringComparison.Ordinal));
                Assert.IsFalse(File.Exists(Path.ChangeExtension(path, ".png")));
                Assert.AreEqual(3, new FileInfo(path).Length);
                string png = CaptureFileDelivery.Write(new byte[] { 0x89, 0x50 }, CaptureFormat.Png);
                try
                {
                    Assert.IsTrue(png.EndsWith(".png", StringComparison.Ordinal));
                    Assert.AreNotEqual(path, png);
                }
                finally
                {
                    if (File.Exists(png)) File.Delete(png);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void DisabledAndUnknownTargetsFailBeforeCapture()
        {
            string name = "nexus-look-" + Guid.NewGuid().ToString("N");
            var disabled = new GameObject(name);
            disabled.SetActive(false);
            var first = new GameObject(name + "-dup");
            var second = new GameObject(name + "-dup");
            try
            {
                var disabledError = Assert.Throws<CaptureException>(() => CaptureTargetResolver.Resolve(disabled.GetInstanceID(), null));
                Assert.AreEqual(CaptureErrorCode.TargetDisabled, disabledError.Code);
                var missing = Assert.Throws<CaptureException>(() => CaptureTargetResolver.Resolve(0, name + "-missing"));
                Assert.AreEqual(CaptureErrorCode.TargetNotFound, missing.Code);
                var ambiguous = Assert.Throws<CaptureException>(() => CaptureTargetResolver.Resolve(0, name + "-dup"));
                Assert.AreEqual(CaptureErrorCode.TargetNotFound, ambiguous.Code);
                StringAssert.Contains("more than one", ambiguous.Message);

                JObject notFound = NexusCommandEnvelope.FromException(missing, "look", 1).ToJson();
                Assert.IsFalse(notFound["ok"].Value<bool>());
                Assert.AreEqual("invalid_argument", notFound["error"]["code"].Value<string>());
                Assert.IsFalse(notFound["error"]["retryable"].Value<bool>());

                JObject unavailable = NexusCommandEnvelope.FromException(
                    new CaptureException(CaptureErrorCode.SceneViewUnavailable, "No active Scene View is open."),
                    "look",
                    1).ToJson();
                Assert.AreEqual("unavailable", unavailable["error"]["code"].Value<string>());
                Assert.IsTrue(unavailable["error"]["retryable"].Value<bool>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(disabled);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SceneCaptureFramesATargetAndRestoresTheView()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "nexus-look-cube-" + Guid.NewGuid().ToString("N");
            cube.transform.position = new Vector3(40f, 2f, -15f);
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : Vector3.zero;
            float size = view != null ? view.size : 0f;
            RenderTexture texture = null;
            try
            {
                if (view == null)
                {
                    Task<JToken> task = new CaptureGameViewCommand().ExecuteAsync(new JObject
                    {
                        ["view"] = "scene",
                        ["instance_id"] = cube.GetInstanceID(),
                        ["request_id"] = "look-scene"
                    }, CancellationToken.None);
                    Assert.IsTrue(task.IsCompleted, "A missing Scene View must fail before GPU readback.");
                    JObject result = (JObject)task.Result;
                    Assert.IsFalse(result["ok"].Value<bool>());
                    Assert.AreEqual("unavailable", result["error"]["code"].Value<string>());
                    Assert.IsTrue(result["error"]["retryable"].Value<bool>());
                    return;
                }

                var request = new CaptureRequest
                {
                    Format = CaptureFormat.Jpeg,
                    MaxLongEdge = CaptureGameViewCommand.DefaultMaxLongEdge,
                    View = CaptureView.Scene,
                    TargetInstanceId = cube.GetInstanceID()
                };
                texture = SceneViewCaptureSource.Capture(cube, request);
                Assert.Greater(Mathf.Max(texture.width, texture.height), 1);
                Assert.LessOrEqual(Mathf.Max(texture.width, texture.height), CaptureGameViewCommand.DefaultMaxLongEdge);
                Assert.AreEqual(pivot.x, view.pivot.x, 0.001f);
                Assert.AreEqual(pivot.y, view.pivot.y, 0.001f);
                Assert.AreEqual(pivot.z, view.pivot.z, 0.001f);
                Assert.AreEqual(size, view.size, 0.001f);
            }
            finally
            {
                if (texture != null) RenderTexture.ReleaseTemporary(texture);
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        private static void AssertRejected(JObject parameters, string expected)
        {
            bool ok = CaptureLookRequest.TryParse(parameters, 1, out CaptureLookCall call);
            Assert.IsFalse(ok);
            Assert.AreEqual("invalid_argument", call.Rejected.Error.Code);
            Assert.IsFalse(call.Rejected.Error.Retryable);
            StringAssert.Contains(expected, call.Rejected.Error.Message);
        }
    }
}
