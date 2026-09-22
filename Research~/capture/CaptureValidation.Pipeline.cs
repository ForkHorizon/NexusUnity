using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Implements end-to-end benchmark pipelines P0-P4 and registers capture validation endpoints.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterCaptureValidationMethods()
        {
            _asyncMethods["benchmark_timer_resolution"] = p => Task.FromResult<JToken>(BenchmarkTimerResolution());
            _asyncMethods["benchmark_readback"] = RunReadbackValidationAsync;
            _asyncMethods["benchmark_metal_alignment"] = p => RunOnMainThreadAsync(() => RunMetalAlignmentSweep());
            _asyncMethods["benchmark_padded_physical_width"] = p => RunOnMainThreadAsync(() => TestPaddedPhysicalWidth());
            _asyncMethods["benchmark_r1_temporal_delays"] = p => RunOnMainThreadAsync(() => TestR1TemporalContractDelays());
            _asyncMethods["benchmark_dynamic_resolution"] = p => RunOnMainThreadAsync(() => RunDynamicResolutionCycling((int)(p?["cycles"] ?? 50)));
            _asyncMethods["benchmark_encoders"] = p => RunOnMainThreadAsync(() => RunCorpusEncoderBenchmark((string)(p?["corpus_id"] ?? "corpus_text_heavy_ui"), (int)(p?["iterations"] ?? 10)));
            _asyncMethods["benchmark_downscale_matrix"] = p => RunOnMainThreadAsync(() => RunDownscaleMatrix((int)(p?["iterations"] ?? 10)));
            _asyncMethods["benchmark_managed_allocations"] = p => RunOnMainThreadAsync(() => RunManagedAllocationBreakdown());
            _asyncMethods["benchmark_source_freshness"] = p => RunOnMainThreadAsync(async () => (JToken)await RunSourceFreshnessTestAsync(
                (string)(p?["backend"] ?? "reflected"), (string)(p?["condition"] ?? "visible_focused"), (int)(p?["iterations"] ?? 30)));
            _asyncMethods["benchmark_private_rt_lifetime"] = p => RunOnMainThreadAsync(async () => (JToken)await VerifyPrivateRtLifetimeAsync());
            _asyncMethods["benchmark_legacy_window"] = p => RunOnMainThreadAsync(() => BenchmarkLegacyWindowCapture((int)(p?["iterations"] ?? 30)));
            _asyncMethods["benchmark_pipeline"] = p => RunOnMainThreadAsync(async () => await RunPipelineBenchmarkAsync(p));
            _asyncMethods["benchmark_async_rpc_stress"] = RunAsyncRpcStressTestAsync;
        }

        private static Task<JToken> RunOnMainThreadAsync(Func<JToken> action)
        {
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            MCPServer.Enqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private static Task<JToken> RunOnMainThreadAsync(Func<Task<JToken>> asyncAction)
        {
            var tcs = new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            MCPServer.Enqueue(async () =>
            {
                try
                {
                    var res = await asyncAction();
                    tcs.TrySetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private sealed class PipelineIterationResult
        {
            public double TcsMs;
            public double StallMs;
            public double ReadbackMs;
            public double EncodeMs;
            public double CopyMs;
            public double Base64Ms;
            public int PayloadBytes;
            public long AllocatedBytes;
        }

        /// <summary>
        /// Benchmarks full end-to-end capture pipelines P0 through P4, exercising Unity GPU rendering, readback, encoding, and editor main-thread execution.
        /// </summary>
        /// <param name="p">Input arguments specifying pipeline ID and iterations.</param>
        /// <returns>Asynchronous task returning pipeline timing and allocation metrics.</returns>
        public static async Task<JToken> RunPipelineBenchmarkAsync(JToken p)
        {
            string pipeline = ((string)p?["pipeline"] ?? "P1").ToUpperInvariant();
            int iterations = (int)(p?["iterations"] ?? 20);
            int width = (int)(p?["width"] ?? 1920);
            int height = (int)(p?["height"] ?? 1080);
            int quality = (int)(p?["jpeg_quality"] ?? 85);

            var result = new JObject();
            result["pipeline"] = pipeline;
            result["iterations"] = iterations;
            result["width"] = width;
            result["height"] = height;

            var tcsTimes = new List<double>();
            var stallTimes = new List<double>();
            var readbackTimes = new List<double>();
            var encodeTimes = new List<double>();
            var copyTimes = new List<double>();
            var base64Times = new List<double>();
            var gcAllocations = new List<double>();
            int payloadBytes = 0;

            RenderTexture srcRt = CreateCorpusRenderTexture("corpus_real_game_view", width, height);
            NativeArray<byte> r2Buffer = default;
            if (pipeline == "P4")
            {
                r2Buffer = new NativeArray<byte>(width * height * 4, Allocator.Persistent);
            }

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    PipelineIterationResult r;
                    if (pipeline == "P0")
                    {
                        r = ExecuteP0Iteration(srcRt, width, height);
                    }
                    else
                    {
                        r = await ExecuteV2IterationAsync(pipeline, srcRt, r2Buffer, width, height, quality);
                    }

                    tcsTimes.Add(r.TcsMs);
                    stallTimes.Add(r.StallMs);
                    readbackTimes.Add(r.ReadbackMs);
                    encodeTimes.Add(r.EncodeMs);
                    copyTimes.Add(r.CopyMs);
                    base64Times.Add(r.Base64Ms);
                    gcAllocations.Add(r.AllocatedBytes);
                    payloadBytes = r.PayloadBytes;

                    await Task.Yield();
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(srcRt);
                if (r2Buffer.IsCreated) r2Buffer.Dispose();
            }

            result["payload_bytes"] = payloadBytes;
            result["unity_tcs_ms"] = ComputeStats(tcsTimes);
            result["main_thread_stall_ms"] = ComputeStats(stallTimes);
            result["readback_ms"] = ComputeStats(readbackTimes);
            result["encode_ms"] = ComputeStats(encodeTimes);
            result["copy_ms"] = ComputeStats(copyTimes);
            result["base64_ms"] = ComputeStats(base64Times);
            result["managed_allocation_bytes"] = ComputeStats(gcAllocations);

            return result;
        }

        private static PipelineIterationResult ExecuteP0Iteration(RenderTexture srcRt, int width, int height)
        {
            var res = new PipelineIterationResult();
            long gcBefore = GC.GetAllocatedBytesForCurrentThread();
            var swTotal = Stopwatch.StartNew();

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = srcRt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var swEnc = Stopwatch.StartNew();
            byte[] png = tex.EncodeToPNG();
            swEnc.Stop();
            res.EncodeMs = swEnc.Elapsed.TotalMilliseconds;

            var swB64 = Stopwatch.StartNew();
            string b64 = Convert.ToBase64String(png);
            swB64.Stop();
            res.Base64Ms = swB64.Elapsed.TotalMilliseconds;
            res.PayloadBytes = b64.Length;

            UnityEngine.Object.DestroyImmediate(tex);
            swTotal.Stop();

            res.TcsMs = swTotal.Elapsed.TotalMilliseconds;
            res.StallMs = swTotal.Elapsed.TotalMilliseconds;
            res.ReadbackMs = 0.0;
            res.CopyMs = 0.0;
            res.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - gcBefore;
            return res;
        }

        private static async Task<PipelineIterationResult> ExecuteV2IterationAsync(
            string pipeline, RenderTexture srcRt, NativeArray<byte> r2Buffer, int width, int height, int quality)
        {
            var res = new PipelineIterationResult();
            long gcBefore = GC.GetAllocatedBytesForCurrentThread();
            var swTcs = Stopwatch.StartNew();
            var swStall = new Stopwatch();

            int targetW = (pipeline == "P3") ? 1600 : width;
            int targetH = (pipeline == "P3") ? 900 : height;

            RenderTexture pipeRt = (pipeline == "P3")
                ? RenderTexture.GetTemporary(targetW, targetH, 0, GraphicsFormat.R8G8B8A8_SRGB)
                : srcRt;

            if (pipeline == "P3") Graphics.Blit(srcRt, pipeRt);

            swStall.Start();
            var tcs = new TaskCompletionSource<AsyncGPUReadbackRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            var swRb = Stopwatch.StartNew();

            if (pipeline == "P4")
            {
                AsyncGPUReadback.RequestIntoNativeArray(ref r2Buffer, pipeRt, 0, TextureFormat.RGBA32, req => tcs.TrySetResult(req));
            }
            else
            {
                AsyncGPUReadback.Request(pipeRt, 0, TextureFormat.RGBA32, req => tcs.TrySetResult(req));
            }
            swStall.Stop();

            var reqResult = await tcs.Task;
            swRb.Stop();
            res.ReadbackMs = swRb.Elapsed.TotalMilliseconds;

            var swEnc = Stopwatch.StartNew();
            NativeArray<byte> pixels = (pipeline == "P4") ? r2Buffer : reqResult.GetData<byte>();
            NativeArray<byte> encoded = (pipeline == "P1")
                ? ImageConversion.EncodeNativeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)targetW, (uint)targetH)
                : ImageConversion.EncodeNativeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)targetW, (uint)targetH, 0, quality);
            swEnc.Stop();
            res.EncodeMs = swEnc.Elapsed.TotalMilliseconds;

            var swCopy = Stopwatch.StartNew();
            byte[] managedBytes = encoded.ToArray();
            encoded.Dispose();
            swCopy.Stop();
            res.CopyMs = swCopy.Elapsed.TotalMilliseconds;

            var swB64 = Stopwatch.StartNew();
            string b64 = Convert.ToBase64String(managedBytes);
            swB64.Stop();
            res.Base64Ms = swB64.Elapsed.TotalMilliseconds;
            res.PayloadBytes = b64.Length;

            swTcs.Stop();
            res.TcsMs = swTcs.Elapsed.TotalMilliseconds;
            res.StallMs = swStall.Elapsed.TotalMilliseconds + swEnc.Elapsed.TotalMilliseconds + swCopy.Elapsed.TotalMilliseconds;

            if (pipeline == "P3") RenderTexture.ReleaseTemporary(pipeRt);

            res.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - gcBefore;
            return res;
        }

        /// <summary>
        /// Executes an asynchronous JSON-RPC burst stress test with simulated random delays on the Unity EditorApplication update cycle and network dispatch.
        /// </summary>
        /// <param name="p">Input parameters for concurrency and count.</param>
        /// <returns>Asynchronous task returning stress test metrics.</returns>
        public static async Task<JToken> RunAsyncRpcStressTestAsync(JToken p)
        {
            int concurrency = (int)(p?["concurrency"] ?? 10);
            int totalRequests = (int)(p?["requests"] ?? 50);

            var result = new JObject();
            result["concurrency"] = concurrency;
            result["total_requests"] = totalRequests;

            var swTotal = Stopwatch.StartNew();
            int completed = 0;
            int errors = 0;
            var latencies = new List<double>();
            var random = new System.Random(42);

            using (var semaphore = new System.Threading.SemaphoreSlim(concurrency, concurrency))
            {
                var tasks = new List<Task>();
                for (int i = 0; i < totalRequests; i++)
                {
                    await semaphore.WaitAsync();
                    int delayTicks = random.Next(1, 6);

                    tasks.Add(Task.Run(async () =>
                    {
                        var swReq = Stopwatch.StartNew();
                        try
                        {
                            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            MCPServer.Enqueue(() =>
                            {
                                int ticksRemaining = delayTicks;
                                EditorApplication.CallbackFunction tickHandler = null;
                                tickHandler = () =>
                                {
                                    ticksRemaining--;
                                    if (ticksRemaining <= 0)
                                    {
                                        EditorApplication.update -= tickHandler;
                                        tcs.TrySetResult(true);
                                    }
                                };
                                EditorApplication.update += tickHandler;
                            });

                            await tcs.Task;
                            swReq.Stop();
                            lock (latencies) latencies.Add(swReq.Elapsed.TotalMilliseconds);
                            System.Threading.Interlocked.Increment(ref completed);
                        }
                        catch
                        {
                            System.Threading.Interlocked.Increment(ref errors);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            swTotal.Stop();
            result["elapsed_ms"] = swTotal.Elapsed.TotalMilliseconds;
            result["completed"] = completed;
            result["errors"] = errors;
            result["latency_ms"] = ComputeStats(latencies);
            return result;
        }
    }
}
