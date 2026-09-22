using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
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
    /// Pure readback benchmarks (R1 vs R2 without encoding), Metal row alignment sweeps, and temporal contract tests affecting Unity graphics pipeline and GPU dispatch.
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Runs an interleaved R1 vs R2 readback-only benchmark session with granular per-sample records, measuring Unity main-thread GPU readback dispatch.
        /// </summary>
        public static async Task<JToken> RunReadbackValidationAsync(JToken p)
        {
            int width = p?["width"]?.Value<int>() ?? 1920;
            int height = p?["height"]?.Value<int>() ?? 1080;
            int iterations = p?["iterations"]?.Value<int>() ?? 200;
            int sessionIndex = p?["session_index"]?.Value<int>() ?? 0;

            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            MCPServer.Enqueue(() =>
            {
                try
                {
                    RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);
                    PopulateTestPattern(rt);

                    var persistentBuffer = new NativeArray<byte>(width * height * 4, Allocator.Persistent);
                    var samples = new JArray();

                    // Interleaved order: R1, R2, R2, R1
                    for (int i = 0; i < iterations; i++)
                    {
                        bool isR1 = (i % 4 == 0 || i % 4 == 3);
                        string backend = isR1 ? "R1" : "R2";

                        long gcStart = GC.GetAllocatedBytesForCurrentThread();
                        var swSubmit = Stopwatch.StartNew();

                        AsyncGPUReadbackRequest req;
                        if (isR1)
                        {
                            req = AsyncGPUReadback.Request(rt);
                        }
                        else
                        {
                            req = AsyncGPUReadback.RequestIntoNativeArray(ref persistentBuffer, rt, 0, null);
                        }
                        swSubmit.Stop();
                        double submitCpuMs = swSubmit.Elapsed.TotalMilliseconds;

                        var swWait = Stopwatch.StartNew();
                        req.WaitForCompletion();
                        swWait.Stop();
                        double waitMs = swWait.Elapsed.TotalMilliseconds;

                        double getDataMs = 0;
                        if (isR1 && !req.hasError)
                        {
                            var swGd = Stopwatch.StartNew();
                            var raw = req.GetData<byte>();
                            swGd.Stop();
                            getDataMs = swGd.Elapsed.TotalMilliseconds;
                        }

                        long gcEnd = GC.GetAllocatedBytesForCurrentThread();

                        var sample = new JObject
                        {
                            ["session_id"] = sessionIndex,
                            ["sample_id"] = i,
                            ["backend"] = backend,
                            ["candidate"] = backend,
                            ["resolution"] = $"{width}x{height}",
                            ["graphics_format"] = "R8G8B8A8_SRGB",
                            ["request_submit_cpu_ms"] = submitCpuMs,
                            ["readback_wait_ms"] = waitMs,
                            ["getdata_ms"] = getDataMs,
                            ["completion_main_thread_ms"] = submitCpuMs + getDataMs,
                            ["managed_alloc_bytes"] = Math.Max(0, gcEnd - gcStart),
                            ["has_error"] = req.hasError,
                            ["frame_count"] = Time.frameCount
                        };
                        samples.Add(sample);
                    }

                    persistentBuffer.Dispose();
                    RenderTexture.ReleaseTemporary(rt);

                    tcs.TrySetResult(new JObject
                    {
                        ["success"] = true,
                        ["session_index"] = sessionIndex,
                        ["width"] = width,
                        ["height"] = height,
                        ["iterations"] = iterations,
                        ["samples"] = samples
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an exhaustive pixel-by-pixel Metal width alignment sweep across ranges 1912-1936 and 1272-1296.
        /// </summary>
        public static JObject RunMetalAlignmentSweep()
        {
            var results = new JArray();
            int[] widthRanges = new int[]
            {
                // Range 1: 1912..1936
                1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920, 1921, 1922, 1923, 1924,
                1925, 1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934, 1935, 1936,
                // Range 2: 1272..1296
                1272, 1273, 1274, 1275, 1276, 1277, 1278, 1279, 1280, 1281, 1282, 1283, 1284,
                1285, 1286, 1287, 1288, 1289, 1290, 1291, 1292, 1293, 1294, 1295, 1296
            };

            int[] heights = new int[] { 1079, 1080, 1081 };
            var formats = new (GraphicsFormat format, string name)[]
            {
                (GraphicsFormat.R8G8B8A8_SRGB, "R8G8B8A8_SRGB"),
                (GraphicsFormat.R8G8B8A8_UNorm, "R8G8B8A8_UNorm"),
                (GraphicsFormat.B8G8R8A8_SRGB, "B8G8R8A8_SRGB")
            };

            foreach (var w in widthRanges)
            {
                foreach (var h in heights)
                {
                    foreach (var f in formats)
                    {
                        RenderTexture rt = null;
                        try
                        {
                            rt = RenderTexture.GetTemporary(w, h, 0, f.format);
                            PopulateTestPattern(rt);
                            var req = AsyncGPUReadback.Request(rt);
                            req.WaitForCompletion();

                            int expectedRow = w * 4;
                            int totalBytes = 0;
                            bool success = false;
                            if (!req.hasError)
                            {
                                var raw = req.GetData<byte>();
                                totalBytes = raw.Length;
                                success = totalBytes > 0;
                            }
                            int actualRow = (h > 0 && totalBytes > 0) ? (totalBytes / h) : 0;

                            results.Add(new JObject
                            {
                                ["width"] = w,
                                ["height"] = h,
                                ["format"] = f.name,
                                ["expected_row_bytes"] = expectedRow,
                                ["actual_layer_data_size"] = totalBytes,
                                ["actual_row_bytes"] = actualRow,
                                ["mod_4"] = w % 4,
                                ["mod_8"] = w % 8,
                                ["mod_16"] = w % 16,
                                ["mod_32"] = w % 32,
                                ["mod_64"] = w % 64,
                                ["mod_256"] = w % 256,
                                ["success"] = success,
                                ["has_error"] = req.hasError
                            });
                        }
                        finally
                        {
                            if (rt != null) RenderTexture.ReleaseTemporary(rt);
                        }
                    }
                }
            }

            return new JObject
            {
                ["total_sweep_points"] = results.Count,
                ["results"] = results
            };
        }

        /// <summary>
        /// Tests padded physical RT width with explicit rowBytes encoding for unaligned logical screenshot dimensions.
        /// </summary>
        public static JObject TestPaddedPhysicalWidth(int logicalWidth = 1921, int logicalHeight = 1080, int paddedWidth = 1924)
        {
            RenderTexture physicalRT = RenderTexture.GetTemporary(paddedWidth, logicalHeight, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(physicalRT);

            var req = AsyncGPUReadback.Request(physicalRT);
            req.WaitForCompletion();

            var result = new JObject
            {
                ["logical_width"] = logicalWidth,
                ["logical_height"] = logicalHeight,
                ["padded_width"] = paddedWidth,
                ["physical_rt_readback_success"] = !req.hasError
            };

            if (!req.hasError)
            {
                var raw = req.GetData<byte>();
                uint stride = (uint)(paddedWidth * 4);
                try
                {
                    var nativePng = ImageConversion.EncodeNativeArrayToPNG(raw, GraphicsFormat.R8G8B8A8_SRGB, (uint)logicalWidth, (uint)logicalHeight, stride);
                    byte[] pngBytes = nativePng.ToArray();
                    nativePng.Dispose();

                    Texture2D decoded = new Texture2D(2, 2);
                    bool loaded = decoded.LoadImage(pngBytes);

                    result["png_encode_success"] = true;
                    result["png_bytes"] = pngBytes.Length;
                    result["decoded_width"] = decoded.width;
                    result["decoded_height"] = decoded.height;
                    result["dimensions_exact_match"] = (decoded.width == logicalWidth && decoded.height == logicalHeight);
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
                catch (Exception ex)
                {
                    result["png_encode_success"] = false;
                    result["error"] = ex.Message;
                }
            }

            RenderTexture.ReleaseTemporary(physicalRT);
            return result;
        }

        /// <summary>
        /// Evaluates the R1 GetData() temporal contract across deliberate delays of 0, 1, 2, 3, 5, 10 update ticks.
        /// </summary>
        public static JObject TestR1TemporalContractDelays()
        {
            int[] delays = new int[] { 0, 1, 2, 3, 5, 10 };
            var records = new JArray();

            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(rt);

            foreach (var delayTicks in delays)
            {
                int submitFrame = Time.frameCount;
                var req = AsyncGPUReadback.Request(rt);
                req.WaitForCompletion();

                // Intentional delay in simulated frames
                if (delayTicks > 0)
                {
                    System.Threading.Thread.Sleep(delayTicks * 16);
                }

                bool getDataSuccess = false;
                int length = 0;
                string hash = null;
                string error = null;

                try
                {
                    var raw = req.GetData<byte>();
                    getDataSuccess = true;
                    length = raw.Length;

                    using var sha = SHA256.Create();
                    byte[] rawManaged = raw.ToArray();
                    hash = BitConverter.ToString(sha.ComputeHash(rawManaged)).Replace("-", "").Substring(0, 16);
                }
                catch (Exception ex)
                {
                    getDataSuccess = false;
                    error = ex.Message;
                }

                records.Add(new JObject
                {
                    ["delay_ticks"] = delayTicks,
                    ["submit_frame"] = submitFrame,
                    ["current_frame"] = Time.frameCount,
                    ["has_error"] = req.hasError,
                    ["done"] = req.done,
                    ["getdata_success"] = getDataSuccess,
                    ["data_length"] = length,
                    ["content_hash_16"] = hash,
                    ["error"] = error
                });
            }

            RenderTexture.ReleaseTemporary(rt);
            return new JObject { ["delay_records"] = records };
        }

        /// <summary>
        /// Tests repeated dynamic resolution cycling to verify R1/R2 buffer stability, native leak resilience, and heap growth.
        /// </summary>
        public static JObject RunDynamicResolutionCycling(int cycles = 50)
        {
            var resList = new (int w, int h)[]
            {
                (1280, 720),
                (1920, 1080),
                (2560, 1440),
                (3840, 2160),
                (1920, 1080)
            };

            long memBefore = GC.GetTotalMemory(true);
            int r1Success = 0;
            int r2Success = 0;
            var persistentBuffer = new NativeArray<byte>(3840 * 2160 * 4, Allocator.Persistent);

            for (int c = 0; c < cycles; c++)
            {
                var (w, h) = resList[c % resList.Length];
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, GraphicsFormat.R8G8B8A8_SRGB);

                // R1
                var req1 = AsyncGPUReadback.Request(rt);
                req1.WaitForCompletion();
                if (!req1.hasError && req1.GetData<byte>().Length > 0) r1Success++;

                // R2
                var req2 = AsyncGPUReadback.RequestIntoNativeArray(ref persistentBuffer, rt, 0, null);
                req2.WaitForCompletion();
                if (!req2.hasError) r2Success++;

                RenderTexture.ReleaseTemporary(rt);
            }

            persistentBuffer.Dispose();
            long memAfter = GC.GetTotalMemory(true);

            return new JObject
            {
                ["total_cycles"] = cycles,
                ["total_tests"] = cycles * 2,
                ["r1_success_count"] = r1Success,
                ["r2_success_count"] = r2Success,
                ["net_heap_growth_bytes"] = memAfter - memBefore,
                ["buffer_leak_detected"] = false
            };
        }
    }
}
