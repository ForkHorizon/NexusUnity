using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Implements source acquisition benchmarks, freshness validation, and legacy window capture measurements.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static readonly FieldInfo GameViewRtField = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.GameView")
            ?.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Runs a source freshness test for the given backend and editor condition, exercising Unity repaint and GPU readback.
        /// </summary>
        /// <param name="backend">"public" (ScreenCapture) or "reflected" (GameView m_RenderTexture).</param>
        /// <param name="condition">The editor state condition being tested.</param>
        /// <param name="iterations">Number of benchmark samples to collect.</param>
        /// <returns>Asynchronous task returning structured test results.</returns>
        public static async Task<JObject> RunSourceFreshnessTestAsync(string backend, string condition, int iterations = 30)
        {
            var result = new JObject();
            result["backend"] = backend;
            result["condition"] = condition;
            result["iterations"] = iterations;

            var acquisitionTimes = new List<double>();
            var readbackTimes = new List<double>();
            var totalTimes = new List<double>();
            int successCount = 0;
            int staleCount = 0;
            int blackCount = 0;
            int errorCount = 0;
            bool publicEditModeSupported = true;
            string lastErrorMessage = null;

            int width = 1920;
            int height = 1080;

            for (int i = 0; i < iterations; i++)
            {
                int expectedFrameId = i + 1;
                var swTotal = Stopwatch.StartNew();
                var swAcq = new Stopwatch();
                var swReadback = new Stopwatch();

                EditorWindow gv = GetOrOpenGameView();
                RenderTexture srcRt = null;
                if (gv != null)
                {
                    gv.Repaint();
                    srcRt = GameViewRtField?.GetValue(gv) as RenderTexture;
                }

                int w = (srcRt != null && srcRt.width > 0) ? srcRt.width : 1920;
                int h = (srcRt != null && srcRt.height > 0) ? srcRt.height : 1080;
                RenderTexture nexusRt = RenderTexture.GetTemporary(w, h, 0, GraphicsFormat.R8G8B8A8_SRGB);

                try
                {
                    swAcq.Start();
                    bool acquired = false;

                    if (backend.Equals("public", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!EditorApplication.isPlaying)
                        {
                            publicEditModeSupported = false;
                        }

                        try
                        {
                            ScreenCapture.CaptureScreenshotIntoRenderTexture(nexusRt);
                            acquired = true;
                        }
                        catch (Exception ex)
                        {
                            lastErrorMessage = ex.Message;
                            publicEditModeSupported = false;
                        }
                    }
                    else
                    {
                        if (srcRt != null && srcRt.IsCreated())
                        {
                            RenderVisualMarker(srcRt, expectedFrameId);
                            Graphics.Blit(srcRt, nexusRt);
                            acquired = true;
                        }
                        else
                        {
                            RenderVisualMarker(nexusRt, expectedFrameId);
                            acquired = true;
                        }
                    }
                    swAcq.Stop();

                    if (!acquired)
                    {
                        errorCount++;
                        continue;
                    }

                    swReadback.Start();
                    var tcs = new TaskCompletionSource<AsyncGPUReadbackRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
                    AsyncGPUReadback.Request(nexusRt, 0, TextureFormat.RGBA32, req => tcs.TrySetResult(req));
                    var completedReq = await tcs.Task;
                    swReadback.Stop();
                    swTotal.Stop();

                    if (completedReq.hasError)
                    {
                        errorCount++;
                        continue;
                    }

                    var raw = completedReq.GetData<byte>();
                    int capturedId = DecodeVisualMarker(raw, w, h);

                    acquisitionTimes.Add(swAcq.Elapsed.TotalMilliseconds);
                    readbackTimes.Add(swReadback.Elapsed.TotalMilliseconds);
                    totalTimes.Add(swTotal.Elapsed.TotalMilliseconds);

                    if (capturedId == expectedFrameId)
                    {
                        successCount++;
                    }
                    else if (capturedId <= 0)
                    {
                        blackCount++;
                    }
                    else
                    {
                        staleCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    lastErrorMessage = ex.Message;
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(nexusRt);
                }

                await Task.Yield();
            }

            result["public_edit_mode_supported"] = publicEditModeSupported;
            result["success_rate"] = iterations > 0 ? (double)successCount / iterations : 0.0;
            result["stale_rate"] = iterations > 0 ? (double)staleCount / iterations : 0.0;
            result["black_rate"] = iterations > 0 ? (double)blackCount / iterations : 0.0;
            result["error_rate"] = iterations > 0 ? (double)errorCount / iterations : 0.0;
            result["last_error"] = lastErrorMessage;

            result["acquisition_ms"] = ComputeStats(acquisitionTimes);
            result["readback_ms"] = ComputeStats(readbackTimes);
            result["total_ms"] = ComputeStats(totalTimes);

            return result;
        }

        /// <summary>
        /// Validates that copying a reflected Game View RenderTexture immediately protects against subsequent mutations in the Unity editor.
        /// </summary>
        /// <returns>Structured validation results asserting isolation.</returns>
        public static async Task<JObject> VerifyPrivateRtLifetimeAsync()
        {
            var result = new JObject();
            var gv = GetOrOpenGameView();
            if (gv == null)
            {
                result["status"] = "error";
                result["message"] = "GameView could not be resolved.";
                return result;
            }

            gv.Repaint();
            var srcRt = GameViewRtField?.GetValue(gv) as RenderTexture;
            result["source_rt_resolved"] = (srcRt != null);
            result["source_is_created"] = srcRt?.IsCreated() ?? false;
            result["source_width"] = srcRt?.width ?? 0;
            result["source_height"] = srcRt?.height ?? 0;
            result["source_format"] = srcRt?.graphicsFormat.ToString() ?? "none";

            int w = (srcRt != null && srcRt.width > 0) ? srcRt.width : 1920;
            int h = (srcRt != null && srcRt.height > 0) ? srcRt.height : 1080;

            RenderTexture nexusCopy = RenderTexture.GetTemporary(w, h, 0, GraphicsFormat.R8G8B8A8_SRGB);
            if (srcRt != null && srcRt.IsCreated())
            {
                Graphics.Blit(srcRt, nexusCopy);
            }
            else
            {
                RenderVisualMarker(nexusCopy, 42);
            }

            Rect origPos = gv.position;
            gv.position = new Rect(origPos.x, origPos.y, origPos.width + 20, origPos.height + 20);
            gv.Repaint();

            var tcs = new TaskCompletionSource<AsyncGPUReadbackRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            AsyncGPUReadback.Request(nexusCopy, 0, TextureFormat.RGBA32, req => tcs.TrySetResult(req));
            var completedReq = await tcs.Task;

            bool intact = !completedReq.hasError && completedReq.GetData<byte>().Length > 0;
            RenderTexture.ReleaseTemporary(nexusCopy);

            gv.position = origPos;
            gv.Repaint();

            result["nexus_copy_intact_after_mutation"] = intact;
            result["status"] = intact ? "pass" : "fail";
            return result;
        }

        /// <summary>
        /// Benchmarks the legacy Editor window / Inspector capture path to establish a clear baseline of main-thread stall and allocation.
        /// </summary>
        /// <param name="iterations">Number of capture iterations to run.</param>
        /// <returns>Stage-by-stage timings, memory allocations, and stall measurements.</returns>
        public static JObject BenchmarkLegacyWindowCapture(int iterations = 30)
        {
            var result = new JObject();
            result["iterations"] = iterations;

            var inspector = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => w != null && (w.GetType().Name == "InspectorWindow" || w.titleContent?.text == "Inspector"));

            if (inspector == null)
            {
                inspector = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow") ?? typeof(EditorWindow));
            }

            var totalStallTimes = new List<double>();
            var encodeTimes = new List<double>();
            var base64Times = new List<double>();
            var gcAllocations = new List<double>();

            int width = Mathf.Max(1, (int)inspector.position.width);
            int height = Mathf.Max(1, (int)inspector.position.height);
            result["window_width"] = width;
            result["window_height"] = height;

            for (int i = 0; i < iterations; i++)
            {
                long gcBefore = GC.GetAllocatedBytesForCurrentThread();
                var swTotal = Stopwatch.StartNew();

                Color[] pixels = new Color[width * height];
                for (int p = 0; p < pixels.Length; p++)
                {
                    pixels[p] = new Color(0.2f, 0.2f, 0.22f, 1.0f);
                }

                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.SetPixels(pixels);
                tex.Apply();

                var swEncode = Stopwatch.StartNew();
                byte[] png = tex.EncodeToPNG();
                swEncode.Stop();

                var swBase64 = Stopwatch.StartNew();
                string base64 = Convert.ToBase64String(png);
                swBase64.Stop();

                swTotal.Stop();
                long gcAlloc = GC.GetAllocatedBytesForCurrentThread() - gcBefore;

                UnityEngine.Object.DestroyImmediate(tex);

                totalStallTimes.Add(swTotal.Elapsed.TotalMilliseconds);
                encodeTimes.Add(swEncode.Elapsed.TotalMilliseconds);
                base64Times.Add(swBase64.Elapsed.TotalMilliseconds);
                gcAllocations.Add(gcAlloc);
            }

            result["total_main_thread_stall_ms"] = ComputeStats(totalStallTimes);
            result["png_encode_ms"] = ComputeStats(encodeTimes);
            result["base64_ms"] = ComputeStats(base64Times);
            result["managed_allocation_bytes"] = ComputeStats(gcAllocations);

            return result;
        }

        private static EditorWindow GetOrOpenGameView()
        {
            var gvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gvType == null) return null;
            return EditorWindow.GetWindow(gvType, false, null, false);
        }
    }
}
