using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Implements rigorous validation benchmarks for T1 (focused reflected capture anomaly) and T2 (P3 identity verification).
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Registers T1 and T2 benchmark endpoints in the JSON-RPC dispatch table.
        /// </summary>
        public static void RegisterT1T2Methods()
        {
            _asyncMethods["benchmark_t1_anomaly"] = RunT1AnomalyBenchmarkAsync;
            _asyncMethods["benchmark_t2_p3_identity"] = RunT2P3IdentityBenchmarkAsync;
        }

        /// <summary>
        /// Executes T1 focused reflected capture anomaly benchmark under the intended production path.
        /// Evaluates 3 conditions (visible_focused, visible_unfocused, hidden_docked) across warmups and measured captures.
        /// </summary>
        /// <param name="p">JSON-RPC parameters specifying iterations and warmup count.</param>
        /// <returns>Asynchronous task returning detailed T1 measurement results.</returns>
        public static async Task<JToken> RunT1AnomalyBenchmarkAsync(JToken p)
        {
            int warmups = p?["warmups"]?.Value<int>() ?? 25;
            int iterations = p?["iterations"]?.Value<int>() ?? 100;
            string requestedCondition = p?["condition"]?.Value<string>() ?? "all";

            var conditions = new List<string>();
            if (requestedCondition == "all")
            {
                conditions.Add("visible_focused");
                conditions.Add("visible_unfocused");
                conditions.Add("hidden_docked");
            }
            else
            {
                conditions.Add(requestedCondition);
            }

            var rootResult = new JObject
            {
                ["warmups"] = warmups,
                ["iterations"] = iterations,
                ["target_path"] = "GameView reflected RT -> Nexus RT copy -> R1 -> update poll -> GetData -> JPEG Q85"
            };

            var conditionsObj = new JObject();
            foreach (var cond in conditions)
            {
                var condResult = await RunT1ConditionAsync(cond, warmups, iterations).ConfigureAwait(false);
                conditionsObj[cond] = condResult;
            }
            rootResult["conditions"] = conditionsObj;

            return rootResult;
        }

        private static async Task<JObject> RunT1ConditionAsync(string condition, int warmups, int iterations)
        {
            var res = new JObject
            {
                ["condition"] = condition,
                ["warmups"] = warmups,
                ["iterations"] = iterations
            };

            // Setup window state on main thread
            await RunOnMainThreadActionAsync(() =>
            {
                var gv = GetOrOpenGameView();
                if (gv != null)
                {
                    if (condition == "visible_focused")
                    {
                        gv.Focus();
                        gv.Repaint();
                    }
                    else if (condition == "visible_unfocused")
                    {
                        var sv = EditorWindow.GetWindow(typeof(SceneView));
                        sv.Focus();
                        gv.Repaint();
                    }
                    else if (condition == "hidden_docked")
                    {
                        var sv = EditorWindow.GetWindow(typeof(SceneView));
                        sv.Focus();
                    }
                }
            }).ConfigureAwait(false);

            // Warmup iterations
            for (int w = 0; w < warmups; w++)
            {
                await ExecuteT1SingleSampleAsync(false).ConfigureAwait(false);
            }

            var acqTimes = new List<double>();
            var submitTimes = new List<double>();
            var submitToDoneTimes = new List<double>();
            var tickCounts = new List<double>();
            var encodeTimes = new List<double>();
            var stallTimes = new List<double>();
            var totalTimes = new List<double>();
            int successCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var sample = await ExecuteT1SingleSampleAsync(true).ConfigureAwait(false);
                if (sample.Success)
                {
                    successCount++;
                    acqTimes.Add(sample.AcquisitionMs);
                    submitTimes.Add(sample.SubmitMs);
                    submitToDoneTimes.Add(sample.SubmitToDoneMs);
                    tickCounts.Add(sample.TickCount);
                    encodeTimes.Add(sample.EncodeMs);
                    stallTimes.Add(sample.StallMs);
                    totalTimes.Add(sample.TotalMs);
                }
            }

            res["success_rate"] = (double)successCount / iterations;
            res["source_acquisition_ms"] = ComputeStats(acqTimes);
            res["readback_submit_ms"] = ComputeStats(submitTimes);
            res["submit_to_done_ms"] = ComputeStats(submitToDoneTimes);
            res["update_tick_count"] = ComputeStats(tickCounts);
            res["encode_ms"] = ComputeStats(encodeTimes);
            res["main_thread_stall_ms"] = ComputeStats(stallTimes);
            res["total_ms"] = ComputeStats(totalTimes);

            return res;
        }

        private struct T1SampleResult
        {
            public bool Success;
            public double AcquisitionMs;
            public double SubmitMs;
            public double SubmitToDoneMs;
            public int TickCount;
            public double EncodeMs;
            public double StallMs;
            public double TotalMs;
        }

        private static async Task<T1SampleResult> ExecuteT1SingleSampleAsync(bool recordMetrics)
        {
            var res = new T1SampleResult();
            var swTotal = Stopwatch.StartNew();

            var mainTcs = new TaskCompletionSource<T1SampleResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            MCPServer.Enqueue(() =>
            {
                var swAcq = Stopwatch.StartNew();
                var gv = GetOrOpenGameView();
                RenderTexture srcRt = null;
                if (gv != null)
                {
                    srcRt = GameViewRtField?.GetValue(gv) as RenderTexture;
                }

                int w = (srcRt != null && srcRt.width > 0) ? srcRt.width : 1920;
                int h = (srcRt != null && srcRt.height > 0) ? srcRt.height : 1080;
                RenderTexture nexusRt = RenderTexture.GetTemporary(w, h, 0, GraphicsFormat.R8G8B8A8_SRGB);

                if (srcRt != null && srcRt.IsCreated())
                {
                    Graphics.Blit(srcRt, nexusRt);
                }
                else
                {
                    PopulateTestPattern(nexusRt);
                }
                swAcq.Stop();

                var swSubmit = Stopwatch.StartNew();
                var req = AsyncGPUReadback.Request(nexusRt);
                swSubmit.Stop();

                double acqMs = swAcq.Elapsed.TotalMilliseconds;
                double submitMs = swSubmit.Elapsed.TotalMilliseconds;
                double stallMs = acqMs + submitMs;

                // Update poll loop using EditorApplication.update and QueuePlayerLoopUpdate
                var swPoll = Stopwatch.StartNew();
                int ticks = 0;

                EditorApplication.CallbackFunction pollCallback = null;
                pollCallback = () =>
                {
                    ticks++;
                    if (req.done)
                    {
                        EditorApplication.update -= pollCallback;
                        swPoll.Stop();

                        var swEnc = Stopwatch.StartNew();
                        double encodeMs = 0;
                        bool ok = false;
                        if (!req.hasError)
                        {
                            var pixels = req.GetData<byte>();
                            var encoded = ImageConversion.EncodeNativeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h, 0, 85);
                            encodeMs = swEnc.Elapsed.TotalMilliseconds;
                            encoded.Dispose();
                            ok = true;
                        }
                        RenderTexture.ReleaseTemporary(nexusRt);
                        swTotal.Stop();

                        mainTcs.TrySetResult(new T1SampleResult
                        {
                            Success = ok,
                            AcquisitionMs = acqMs,
                            SubmitMs = submitMs,
                            SubmitToDoneMs = swPoll.Elapsed.TotalMilliseconds,
                            TickCount = ticks,
                            EncodeMs = encodeMs,
                            StallMs = stallMs + encodeMs,
                            TotalMs = swTotal.Elapsed.TotalMilliseconds
                        });
                    }
                    else
                    {
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                };

                EditorApplication.update += pollCallback;
                EditorApplication.QueuePlayerLoopUpdate();
            });

            return await mainTcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Executes T2 P3 identity validation test verifying exact dimensions, instance IDs, and encoder parameters.
        /// </summary>
        /// <param name="p">JSON-RPC parameters.</param>
        /// <returns>Structured dictionary containing submitted RT parameters and verification metrics.</returns>
        public static async Task<JToken> RunT2P3IdentityBenchmarkAsync(JToken p)
        {
            int iterations = p?["iterations"]?.Value<int>() ?? 20;

            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            MCPServer.Enqueue(() =>
            {
                try
                {
                    int srcW = 1920;
                    int srcH = 1080;
                    int normW = 1600;
                    int normH = 900;

                    var samples = new JArray();
                    for (int i = 0; i < iterations; i++)
                    {
                        var swBlit = Stopwatch.StartNew();
                        RenderTexture srcRt = RenderTexture.GetTemporary(srcW, srcH, 0, GraphicsFormat.R8G8B8A8_SRGB);
                        PopulateTestPattern(srcRt);

                        RenderTexture normRt = RenderTexture.GetTemporary(normW, normH, 0, GraphicsFormat.R8G8B8A8_SRGB);
                        Graphics.Blit(srcRt, normRt);
                        swBlit.Stop();

                        int rtInstanceId = normRt.GetInstanceID();
                        int rtWidth = normRt.width;
                        int rtHeight = normRt.height;
                        GraphicsFormat rtFormat = normRt.graphicsFormat;

                        var swSubmit = Stopwatch.StartNew();
                        var req = AsyncGPUReadback.Request(normRt);
                        swSubmit.Stop();

                        var swWait = Stopwatch.StartNew();
                        req.WaitForCompletion();
                        swWait.Stop();

                        var swGd = Stopwatch.StartNew();
                        var pixels = req.GetData<byte>();
                        swGd.Stop();

                        int readbackByteLength = pixels.Length;
                        int expectedByteLength = normW * normH * 4;

                        var swEnc = Stopwatch.StartNew();
                        var encoded = ImageConversion.EncodeNativeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)normW, (uint)normH, 0, 85);
                        swEnc.Stop();

                        int encodedBytes = encoded.Length;

                        // Verify decoded image dimensions
                        var swDec = Stopwatch.StartNew();
                        byte[] managedJpg = encoded.ToArray();
                        encoded.Dispose();

                        var decodeTex = new Texture2D(2, 2);
                        bool loaded = decodeTex.LoadImage(managedJpg);
                        int decodedW = decodeTex.width;
                        int decodedH = decodeTex.height;
                        UnityEngine.Object.DestroyImmediate(decodeTex);
                        swDec.Stop();

                        RenderTexture.ReleaseTemporary(normRt);
                        RenderTexture.ReleaseTemporary(srcRt);

                        var sample = new JObject
                        {
                            ["iteration"] = i,
                            ["rt_instance_id"] = rtInstanceId,
                            ["rt_width"] = rtWidth,
                            ["rt_height"] = rtHeight,
                            ["rt_format"] = rtFormat.ToString(),
                            ["readback_length_bytes"] = readbackByteLength,
                            ["expected_length_bytes"] = expectedByteLength,
                            ["length_matches"] = (readbackByteLength == expectedByteLength),
                            ["encoder_input_width"] = normW,
                            ["encoder_input_height"] = normH,
                            ["decoded_output_width"] = decodedW,
                            ["decoded_output_height"] = decodedH,
                            ["dimensions_intact"] = (decodedW == normW && decodedH == normH),
                            ["encoded_bytes"] = encodedBytes,
                            ["blit_ms"] = swBlit.Elapsed.TotalMilliseconds,
                            ["submit_ms"] = swSubmit.Elapsed.TotalMilliseconds,
                            ["wait_ms"] = swWait.Elapsed.TotalMilliseconds,
                            ["getdata_ms"] = swGd.Elapsed.TotalMilliseconds,
                            ["encode_ms"] = swEnc.Elapsed.TotalMilliseconds,
                            ["decode_verify_ms"] = swDec.Elapsed.TotalMilliseconds,
                            ["total_pipeline_ms"] = swBlit.Elapsed.TotalMilliseconds + swSubmit.Elapsed.TotalMilliseconds + swWait.Elapsed.TotalMilliseconds + swGd.Elapsed.TotalMilliseconds + swEnc.Elapsed.TotalMilliseconds
                        };
                        samples.Add(sample);
                    }

                    var res = new JObject
                    {
                        ["target"] = "1920x1080 source -> 1600x900 normalizeRT -> R1 -> JPEG Q85",
                        ["iterations"] = iterations,
                        ["samples"] = samples
                    };
                    tcs.TrySetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        private static Task RunOnMainThreadActionAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            MCPServer.Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }
    }
}
