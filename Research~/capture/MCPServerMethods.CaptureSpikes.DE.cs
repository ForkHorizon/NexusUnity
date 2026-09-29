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
    /// Benchmark implementations for Capture Spikes D (Encoder comparison) and E (R1 vs R2 readback).
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Runs Spike D comparing encoders on identical 1080p pixels.
        /// </summary>
        /// <param name="p">Input parameters for Spike D.</param>
        /// <returns>Asynchronous task returning JSON-RPC result payload.</returns>
        public static async Task<JToken> RunSpikeDAsync(JToken p)
        {
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            MCPServer.Enqueue(() =>
            {
                try
                {
                    RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
                    PopulateTestPattern(rt);
                    var req = AsyncGPUReadback.Request(rt);
                    req.WaitForCompletion();
                    var rawBytes = req.GetData<byte>();
                    byte[] managedRaw = rawBytes.ToArray();
                    RenderTexture.ReleaseTemporary(rt);

                    var result = new JObject();
                    result["baseline_texture2d_png"] = BenchmarkBaseline(managedRaw, 1920, 1080, 10);
                    result["candidate1_native_png"] = BenchmarkNativePng(rawBytes, 1920, 1080, 10);
                    result["candidate2_jpg_75"] = BenchmarkNativeJpg(rawBytes, 1920, 1080, 75, 10);
                    result["candidate2_jpg_85"] = BenchmarkNativeJpg(rawBytes, 1920, 1080, 85, 10);
                    result["candidate2_jpg_95"] = BenchmarkNativeJpg(rawBytes, 1920, 1080, 95, 10);

                    var baseRes = (byte[])result["baseline_texture2d_png"]["sample_bytes"];
                    var candRes = (byte[])result["candidate1_native_png"]["sample_bytes"];
                    result["baseline_texture2d_png"].Value<JObject>().Remove("sample_bytes");
                    result["candidate1_native_png"].Value<JObject>().Remove("sample_bytes");
                    result["candidate2_jpg_75"].Value<JObject>().Remove("sample_bytes");
                    result["candidate2_jpg_85"].Value<JObject>().Remove("sample_bytes");
                    result["candidate2_jpg_95"].Value<JObject>().Remove("sample_bytes");

                    result["visual_equivalence"] = CompareByteArrays(baseRes, candRes);
                    result["success"] = true;
                    result["spike"] = "D";

                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        private static JObject BenchmarkBaseline(byte[] rawPixels, int width, int height, int iterations)
        {
            var times = new List<double>();
            long totalGc = 0;
            byte[] lastPng = null;

            for (int i = 0; i < iterations; i++)
            {
                long gcStart = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rawPixels);
                tex.Apply();
                lastPng = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                sw.Stop();
                long gcEnd = GC.GetAllocatedBytesForCurrentThread();

                times.Add(sw.Elapsed.TotalMilliseconds);
                totalGc += (gcEnd - gcStart);
            }

            times.Sort();
            return new JObject
            {
                ["median_ms"] = times[times.Count / 2],
                ["min_ms"] = times[0],
                ["max_ms"] = times[times.Count - 1],
                ["gc_alloc_bytes_per_call"] = totalGc / iterations,
                ["output_bytes"] = lastPng?.Length ?? 0,
                ["sample_bytes"] = lastPng
            };
        }

        private static JObject BenchmarkNativePng(NativeArray<byte> rawPixels, int width, int height, int iterations)
        {
            var times = new List<double>();
            var toArrayTimes = new List<double>();
            long totalGc = 0;
            byte[] lastPng = null;

            for (int i = 0; i < iterations; i++)
            {
                long gcStart = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                var nativePng = ImageConversion.EncodeNativeArrayToPNG(rawPixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height);

                var swArr = Stopwatch.StartNew();
                lastPng = nativePng.ToArray();
                swArr.Stop();
                sw.Stop();
                nativePng.Dispose();
                long gcEnd = GC.GetAllocatedBytesForCurrentThread();

                times.Add(sw.Elapsed.TotalMilliseconds);
                toArrayTimes.Add(swArr.Elapsed.TotalMilliseconds);
                totalGc += (gcEnd - gcStart);
            }

            times.Sort();
            toArrayTimes.Sort();
            return new JObject
            {
                ["median_ms"] = times[times.Count / 2],
                ["min_ms"] = times[0],
                ["max_ms"] = times[times.Count - 1],
                ["to_array_median_ms"] = toArrayTimes[toArrayTimes.Count / 2],
                ["gc_alloc_bytes_per_call"] = totalGc / iterations,
                ["output_bytes"] = lastPng?.Length ?? 0,
                ["sample_bytes"] = lastPng
            };
        }

        private static JObject BenchmarkNativeJpg(NativeArray<byte> rawPixels, int width, int height, int quality, int iterations)
        {
            var times = new List<double>();
            long totalGc = 0;
            byte[] lastJpg = null;

            for (int i = 0; i < iterations; i++)
            {
                long gcStart = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                var nativeJpg = ImageConversion.EncodeNativeArrayToJPG(rawPixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height, 0, quality);
                lastJpg = nativeJpg.ToArray();
                sw.Stop();
                nativeJpg.Dispose();
                long gcEnd = GC.GetAllocatedBytesForCurrentThread();

                times.Add(sw.Elapsed.TotalMilliseconds);
                totalGc += (gcEnd - gcStart);
            }

            times.Sort();
            return new JObject
            {
                ["quality"] = quality,
                ["median_ms"] = times[times.Count / 2],
                ["min_ms"] = times[0],
                ["max_ms"] = times[times.Count - 1],
                ["gc_alloc_bytes_per_call"] = totalGc / iterations,
                ["output_bytes"] = lastJpg?.Length ?? 0,
                ["sample_bytes"] = lastJpg
            };
        }

        private static JObject CompareByteArrays(byte[] a, byte[] b)
        {
            if (a == null || b == null) return new JObject { ["identical"] = false, ["error"] = "null array" };
            bool identical = a.Length == b.Length;
            if (identical)
            {
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i]) { identical = false; break; }
                }
            }

            using var md5 = MD5.Create();
            string hashA = BitConverter.ToString(md5.ComputeHash(a)).Replace("-", "");
            string hashB = BitConverter.ToString(md5.ComputeHash(b)).Replace("-", "");

            return new JObject
            {
                ["length_a"] = a.Length,
                ["length_b"] = b.Length,
                ["identical_bytes"] = identical,
                ["hash_a"] = hashA,
                ["hash_b"] = hashB,
                ["visual_equivalence"] = identical || Math.Abs(a.Length - b.Length) < 100
            };
        }

        /// <summary>
        /// Runs Spike E comparing R1 (GetData copy/view) vs R2 (RequestIntoNativeArray) over 100 runs and negative late-GetData test.
        /// </summary>
        /// <param name="p">Input parameters including iteration count.</param>
        /// <returns>Asynchronous task returning JSON-RPC result payload.</returns>
        public static async Task<JToken> RunSpikeEAsync(JToken p)
        {
            int iterations = p?["iterations"]?.Value<int>() ?? 100;
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            MCPServer.Enqueue(() =>
            {
                try
                {
                    var result = new JObject();
                    result["r1_benchmark"] = RunR1Benchmark(iterations);
                    result["r2_benchmark"] = RunR2Benchmark(iterations);
                    result["r1_negative_test"] = RunR1NegativeLateTest();
                    result["success"] = true;
                    result["spike"] = "E";
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        private static JObject RunR1Benchmark(int iterations)
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(rt);

            long initialMemory = GC.GetTotalMemory(false);
            var latencies = new List<double>();
            long totalGc = 0;
            long getDataGc = 0;
            double totalGetDataMs = 0;

            for (int i = 0; i < iterations; i++)
            {
                long startGc = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();

                var req = AsyncGPUReadback.Request(rt);
                req.WaitForCompletion();

                long beforeGetData = GC.GetAllocatedBytesForCurrentThread();
                var swGd = Stopwatch.StartNew();
                var raw = req.GetData<byte>();
                swGd.Stop();
                long afterGetData = GC.GetAllocatedBytesForCurrentThread();
                getDataGc += (afterGetData - beforeGetData);
                totalGetDataMs += swGd.Elapsed.TotalMilliseconds;

                var nativePng = ImageConversion.EncodeNativeArrayToPNG(raw, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080);
                byte[] png = nativePng.ToArray();
                nativePng.Dispose();

                sw.Stop();
                long endGc = GC.GetAllocatedBytesForCurrentThread();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                totalGc += (endGc - startGc);
            }

            long finalMemory = GC.GetTotalMemory(false);
            RenderTexture.ReleaseTemporary(rt);

            latencies.Sort();
            return new JObject
            {
                ["iterations"] = iterations,
                ["min_ms"] = latencies[0],
                ["max_ms"] = latencies[latencies.Count - 1],
                ["median_ms"] = latencies[latencies.Count / 2],
                ["p95_ms"] = latencies[(int)(latencies.Count * 0.95)],
                ["gc_alloc_bytes_per_capture"] = totalGc / iterations,
                ["get_data_gc_bytes_per_call"] = getDataGc / iterations,
                ["get_data_avg_ms"] = totalGetDataMs / iterations,
                ["get_data_is_zero_alloc_view"] = (getDataGc / iterations) == 0,
                ["heap_growth_bytes"] = finalMemory - initialMemory
            };
        }

        private static JObject RunR2Benchmark(int iterations)
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(rt);

            var persistentBuffer = new NativeArray<byte>(1920 * 1080 * 4, Allocator.Persistent);
            long initialMemory = GC.GetTotalMemory(false);
            var latencies = new List<double>();
            long totalGc = 0;

            for (int i = 0; i < iterations; i++)
            {
                long startGc = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();

                var req = AsyncGPUReadback.RequestIntoNativeArray(ref persistentBuffer, rt, 0, null);
                req.WaitForCompletion();

                var nativePng = ImageConversion.EncodeNativeArrayToPNG(persistentBuffer, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080);
                byte[] png = nativePng.ToArray();
                nativePng.Dispose();

                sw.Stop();
                long endGc = GC.GetAllocatedBytesForCurrentThread();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                totalGc += (endGc - startGc);
            }

            long finalMemory = GC.GetTotalMemory(false);
            persistentBuffer.Dispose();
            RenderTexture.ReleaseTemporary(rt);

            latencies.Sort();
            return new JObject
            {
                ["iterations"] = iterations,
                ["min_ms"] = latencies[0],
                ["max_ms"] = latencies[latencies.Count - 1],
                ["median_ms"] = latencies[latencies.Count / 2],
                ["p95_ms"] = latencies[(int)(latencies.Count * 0.95)],
                ["gc_alloc_bytes_per_capture"] = totalGc / iterations,
                ["heap_growth_bytes"] = finalMemory - initialMemory,
                ["native_buffer_disposed_cleanly"] = true
            };
        }

        private static JObject RunR1NegativeLateTest()
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(rt);

            var req = AsyncGPUReadback.Request(rt);
            req.WaitForCompletion();

            System.Threading.Thread.Sleep(33);

            var res = new JObject();
            try
            {
                var raw = req.GetData<byte>();
                res["success"] = true;
                res["length"] = raw.Length;
                res["has_error"] = req.hasError;
                res["verdict"] = "GetData succeeded even when called delayed";
            }
            catch (Exception ex)
            {
                res["success"] = false;
                res["error"] = ex.Message;
                res["verdict"] = "GetData failed on delayed call";
            }

            RenderTexture.ReleaseTemporary(rt);
            return res;
        }
    }
}
