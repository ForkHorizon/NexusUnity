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
    /// Realistic image corpus encoder benchmarks, PSNR calculations, downscaling matrix, and allocation profiling.
    /// </summary>
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Benchmarks all candidate encoders across the realistic image corpus with PSNR and MSE quality metrics.
        /// </summary>
        public static JObject RunCorpusEncoderBenchmark(string corpusId = "corpus_text_heavy_ui", int iterations = 10)
        {
            RenderTexture rt = CreateCorpusRenderTexture(corpusId, 1920, 1080);
            var req = AsyncGPUReadback.Request(rt);
            req.WaitForCompletion();
            var rawBytes = req.GetData<byte>();
            byte[] managedRaw = rawBytes.ToArray();

            var result = new JObject
            {
                ["corpus_id"] = corpusId,
                ["width"] = 1920,
                ["height"] = 1080,
                ["iterations"] = iterations
            };

            // Baseline PNG
            result["baseline_png"] = BenchmarkEncoderCandidate("baseline_png", () =>
            {
                Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(managedRaw);
                tex.Apply();
                byte[] b = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                return b;
            }, managedRaw, 1920, 1080, iterations);

            // Native PNG
            result["native_png"] = BenchmarkEncoderCandidate("native_png", () =>
            {
                var n = ImageConversion.EncodeNativeArrayToPNG(rawBytes, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080);
                byte[] b = n.ToArray();
                n.Dispose();
                return b;
            }, managedRaw, 1920, 1080, iterations);

            // Native JPG across qualities 60, 70, 75, 80, 85, 90, 95
            int[] qualities = new int[] { 60, 70, 75, 80, 85, 90, 95 };
            foreach (var q in qualities)
            {
                result[$"native_jpg_q{q}"] = BenchmarkEncoderCandidate($"native_jpg_q{q}", () =>
                {
                    var n = ImageConversion.EncodeNativeArrayToJPG(rawBytes, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080, 0, q);
                    byte[] b = n.ToArray();
                    n.Dispose();
                    return b;
                }, managedRaw, 1920, 1080, iterations);
            }

            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static JObject BenchmarkEncoderCandidate(string candidateName, Func<byte[]> encodeAction, byte[] referenceRaw, int width, int height, int iterations)
        {
            var times = new List<double>();
            long totalGc = 0;
            byte[] lastOutput = null;

            for (int i = 0; i < iterations; i++)
            {
                long gc0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                lastOutput = encodeAction();
                sw.Stop();
                long gc1 = GC.GetAllocatedBytesForCurrentThread();

                times.Add(sw.Elapsed.TotalMilliseconds);
                totalGc += Math.Max(0, gc1 - gc0);
            }

            times.Sort();
            double median = times[times.Count / 2];
            double min = times[0];
            double max = times[times.Count - 1];
            double p95 = times[(int)(times.Count * 0.95)];

            double sum = 0;
            foreach (var t in times) sum += t;
            double mean = sum / times.Count;

            double sumSq = 0;
            foreach (var t in times) sumSq += (t - mean) * (t - mean);
            double stddev = Math.Sqrt(sumSq / times.Count);

            // Compute PSNR and MSE against reference pixels
            var quality = ComputePsnr(lastOutput, referenceRaw, width, height);

            return new JObject
            {
                ["candidate"] = candidateName,
                ["median_ms"] = median,
                ["min_ms"] = min,
                ["p95_ms"] = p95,
                ["max_ms"] = max,
                ["mean_ms"] = mean,
                ["stddev_ms"] = stddev,
                ["gc_alloc_bytes"] = totalGc / iterations,
                ["output_bytes"] = lastOutput?.Length ?? 0,
                ["psnr_db"] = quality.psnr,
                ["mse"] = quality.mse
            };
        }

        private static (double psnr, double mse) ComputePsnr(byte[] encodedBytes, byte[] referenceRaw, int width, int height)
        {
            if (encodedBytes == null || referenceRaw == null) return (0, 0);

            Texture2D dec = new Texture2D(2, 2);
            if (!dec.LoadImage(encodedBytes))
            {
                UnityEngine.Object.DestroyImmediate(dec);
                return (0, 0);
            }

            Color32[] decPixels = dec.GetPixels32();
            UnityEngine.Object.DestroyImmediate(dec);

            if (decPixels.Length != width * height) return (0, 0);

            double sumSquareErr = 0;
            int totalComponents = width * height * 3;

            for (int i = 0; i < decPixels.Length; i++)
            {
                int rawOffset = i * 4;
                if (rawOffset + 2 >= referenceRaw.Length) break;

                int dr = decPixels[i].r - referenceRaw[rawOffset];
                int dg = decPixels[i].g - referenceRaw[rawOffset + 1];
                int db = decPixels[i].b - referenceRaw[rawOffset + 2];

                sumSquareErr += (dr * dr) + (dg * dg) + (db * db);
            }

            double mse = sumSquareErr / totalComponents;
            if (mse <= 0.00001) return (99.0, 0.0); // Essentially lossless

            double psnr = 10.0 * Math.Log10((255.0 * 255.0) / mse);
            return (psnr, mse);
        }

        /// <summary>
        /// Evaluates end-to-end normalization and downscaling latency across target long-edge resolutions, altering Unity graphics state and RenderTexture allocation.
        /// </summary>
        public static JObject RunDownscaleMatrix(int iterations = 10)
        {
            int[] targets = new int[] { 2560, 2048, 1920, 1600, 1280 };
            var list = new JArray();

            RenderTexture src4K = RenderTexture.GetTemporary(3840, 2160, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(src4K);

            foreach (var maxLongEdge in targets)
            {
                int targetW = maxLongEdge;
                int targetH = (int)Math.Round((double)maxLongEdge * 9.0 / 16.0);
                if (targetW % 2 != 0) targetW--;
                if (targetH % 2 != 0) targetH--;

                RenderTexture normRT = RenderTexture.GetTemporary(targetW, targetH, 0, GraphicsFormat.R8G8B8A8_SRGB);

                var blitTimes = new List<double>();
                var readbackTimes = new List<double>();
                var encodeTimes = new List<double>();
                int outputBytes = 0;

                for (int i = 0; i < iterations; i++)
                {
                    var swBlit = Stopwatch.StartNew();
                    Graphics.Blit(src4K, normRT);
                    swBlit.Stop();
                    blitTimes.Add(swBlit.Elapsed.TotalMilliseconds);

                    var swRb = Stopwatch.StartNew();
                    var req = AsyncGPUReadback.Request(normRT);
                    req.WaitForCompletion();
                    swRb.Stop();
                    readbackTimes.Add(swRb.Elapsed.TotalMilliseconds);

                    var raw = req.GetData<byte>();
                    var swEnc = Stopwatch.StartNew();
                    var nativeJpg = ImageConversion.EncodeNativeArrayToJPG(raw, GraphicsFormat.R8G8B8A8_SRGB, (uint)targetW, (uint)targetH, 0, 85);
                    byte[] b = nativeJpg.ToArray();
                    nativeJpg.Dispose();
                    swEnc.Stop();
                    encodeTimes.Add(swEnc.Elapsed.TotalMilliseconds);
                    outputBytes = b.Length;
                }

                blitTimes.Sort();
                readbackTimes.Sort();
                encodeTimes.Sort();

                list.Add(new JObject
                {
                    ["target_long_edge"] = maxLongEdge,
                    ["dimensions"] = $"{targetW}x{targetH}",
                    ["blit_submit_cpu_ms"] = blitTimes[blitTimes.Count / 2],
                    ["readback_wait_ms"] = readbackTimes[readbackTimes.Count / 2],
                    ["encode_ms"] = encodeTimes[encodeTimes.Count / 2],
                    ["total_pipeline_ms"] = blitTimes[blitTimes.Count / 2] + readbackTimes[readbackTimes.Count / 2] + encodeTimes[encodeTimes.Count / 2],
                    ["output_bytes"] = outputBytes
                });

                RenderTexture.ReleaseTemporary(normRT);
            }

            RenderTexture.ReleaseTemporary(src4K);
            return new JObject { ["downscale_targets"] = list };
        }

        /// <summary>
        /// Profiles managed allocations stage-by-stage across Legacy V1 and Capture V2 pipelines.
        /// </summary>
        public static JObject RunManagedAllocationBreakdown()
        {
            RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, GraphicsFormat.R8G8B8A8_SRGB);
            PopulateTestPattern(rt);

            // 1. Legacy V1 stage allocations
            long gcV1Raw0 = GC.GetAllocatedBytesForCurrentThread();
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            long gcV1Raw1 = GC.GetAllocatedBytesForCurrentThread();

            long gcV1Png0 = GC.GetAllocatedBytesForCurrentThread();
            byte[] v1Png = tex.EncodeToPNG();
            long gcV1Png1 = GC.GetAllocatedBytesForCurrentThread();
            UnityEngine.Object.DestroyImmediate(tex);

            // 2. V2 Readback allocations
            long gcV2Rb0 = GC.GetAllocatedBytesForCurrentThread();
            var req = AsyncGPUReadback.Request(rt);
            req.WaitForCompletion();
            var raw = req.GetData<byte>();
            long gcV2Rb1 = GC.GetAllocatedBytesForCurrentThread();

            // 3. V2 Native Encode allocations
            long gcV2Png0 = GC.GetAllocatedBytesForCurrentThread();
            var nPng = ImageConversion.EncodeNativeArrayToPNG(raw, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080);
            byte[] v2Png = nPng.ToArray();
            nPng.Dispose();
            long gcV2Png1 = GC.GetAllocatedBytesForCurrentThread();

            long gcV2Jpg0 = GC.GetAllocatedBytesForCurrentThread();
            var nJpg = ImageConversion.EncodeNativeArrayToJPG(raw, GraphicsFormat.R8G8B8A8_SRGB, 1920, 1080, 0, 85);
            byte[] v2Jpg = nJpg.ToArray();
            nJpg.Dispose();
            long gcV2Jpg1 = GC.GetAllocatedBytesForCurrentThread();

            // 4. Compatibility boundary allocations
            long gcB64_0 = GC.GetAllocatedBytesForCurrentThread();
            string b64 = Convert.ToBase64String(v2Jpg);
            long gcB64_1 = GC.GetAllocatedBytesForCurrentThread();

            long gcJson0 = GC.GetAllocatedBytesForCurrentThread();
            var jo = new JObject { ["image"] = b64 };
            string json = jo.ToString(Newtonsoft.Json.Formatting.None);
            long gcJson1 = GC.GetAllocatedBytesForCurrentThread();

            RenderTexture.ReleaseTemporary(rt);

            return new JObject
            {
                ["v1_texture2d_readpixels_alloc_bytes"] = Math.Max(0, gcV1Raw1 - gcV1Raw0),
                ["v1_encode_to_png_alloc_bytes"] = Math.Max(0, gcV1Png1 - gcV1Png0),
                ["v2_async_readback_and_getdata_alloc_bytes"] = Math.Max(0, gcV2Rb1 - gcV2Rb0),
                ["v2_native_png_and_toarray_alloc_bytes"] = Math.Max(0, gcV2Png1 - gcV2Png0),
                ["v2_native_jpg_and_toarray_alloc_bytes"] = Math.Max(0, gcV2Jpg1 - gcV2Jpg0),
                ["base64_string_alloc_bytes"] = Math.Max(0, gcB64_1 - gcB64_0),
                ["json_serialization_alloc_bytes"] = Math.Max(0, gcJson1 - gcJson0)
            };
        }
    }
}
