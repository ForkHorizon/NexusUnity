using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Nexus-owned Capture V2 gateway. Copies a presented Game View or framed Scene View into a Nexus RT, then uses DriverOwnedReadback.
    /// </summary>
    public sealed class CaptureGateway : ICaptureGateway
    {
        private const int MaxRepaintTicks = 10;
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static int _cachedMainThreadId;

        /// <summary>Shared gateway used by Legacy HTTP/MCP adapters.</summary>
        public static ICaptureGateway Shared { get; } = new CaptureGateway();

        [InitializeOnLoadMethod]
        private static void CacheMainThread()
        {
            _cachedMainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Captures a presented Game View, or a Scene View framed on a target, through DriverOwnedReadback.
        /// </summary>
        /// <param name="request">Semantic capture options. Must not include transport envelopes.</param>
        /// <param name="cancellationToken">Token used to fail the logical request without destroying an in-flight GPU buffer.</param>
        /// <returns>Compressed image bytes and capture metadata.</returns>
        public async Task<CaptureResult> CaptureGameView(CaptureRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Format != CaptureFormat.Png && request.Format != CaptureFormat.Jpeg)
            {
                throw new CaptureException(CaptureErrorCode.UnsupportedFormat, "Unsupported capture format.");
            }

            if (DriverOwnedReadback.IsReloading)
            {
                throw new CaptureException(CaptureErrorCode.DomainReloadInterrupted, "Capture rejected during domain reload.");
            }

            if (IsMainThread())
            {
                if (!Gate.Wait(0))
                {
                    throw new CaptureException(CaptureErrorCode.CaptureBusy, "A Game View capture is already in progress.");
                }
            }
            else
            {
                await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await RunOnMainThread(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }

        private static bool IsMainThread()
        {
            int known = _cachedMainThreadId != 0 ? _cachedMainThreadId : MCPServer.MainThreadId;
            if (known != 0 && known != -1)
            {
                return Thread.CurrentThread.ManagedThreadId == known;
            }

            return !Thread.CurrentThread.IsThreadPoolThread && !Thread.CurrentThread.IsBackground;
        }

        private static Task<CaptureResult> RunOnMainThread(
            CaptureRequest request,
            CancellationToken cancellationToken)
        {
            if (IsMainThread()) return Execute(request, cancellationToken);

            var tcs = new TaskCompletionSource<CaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            MCPServer.Enqueue(() =>
            {
                try
                {
                    Task<CaptureResult> started = Execute(request, cancellationToken);
                    started.ContinueWith(
                        async t =>
                        {
                            if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerException ?? t.Exception);
                            else if (t.IsCanceled) tcs.TrySetCanceled();
                            else
                            {
                                try { tcs.TrySetResult(await t.ConfigureAwait(false)); }
                                catch (Exception ex) { tcs.TrySetException(ex); }
                            }
                        },
                        TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        // Repaint is deferred: a docked/hidden Game View only creates its RT on a later editor tick.
        private static async Task<RenderTexture> WaitForPresentedRt(EditorWindow gameView, CancellationToken cancellationToken)
        {
            for (int tick = 0; ; tick++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (gameView == null)
                {
                    throw new CaptureException(CaptureErrorCode.GameViewUnavailable, "Game View window was closed during capture.");
                }

                gameView.Repaint();
                RenderTexture rt = GameViewCaptureSource.TryGetPresentedRt(gameView);
                if (rt != null || tick >= MaxRepaintTicks) return rt;

                var next = new TaskCompletionSource<bool>();
                EditorApplication.CallbackFunction onUpdate = null;
                onUpdate = () =>
                {
                    EditorApplication.update -= onUpdate;
                    next.TrySetResult(true);
                };
                EditorApplication.update += onUpdate;
                EditorApplication.QueuePlayerLoopUpdate();
                // No ConfigureAwait(false): resume through the Unity sync context so the caller keeps running on the main thread.
                await next.Task;
            }
        }

        private static async Task<CaptureResult> Execute(
            CaptureRequest request,
            CancellationToken cancellationToken)
        {
            var timings = new CaptureTimings();
            var swTotal = Stopwatch.StartNew();
            var swAcquire = Stopwatch.StartNew();
            PreparedFrame frame = await Prepare(request, cancellationToken);
            swAcquire.Stop();
            timings.SourceAcquireMs = swAcquire.Elapsed.TotalMilliseconds;

            byte[] encoded = await DriverOwnedReadback.ReadAndEncode(
                frame.Texture, new Vector2Int(frame.Width, frame.Height), request, timings, cancellationToken)
                .ConfigureAwait(false);

            swTotal.Stop();
            timings.MainThreadStallMs = timings.SourceAcquireMs + timings.ReadbackSubmitMs + timings.EncodeMs;
            timings.TotalInternalMs = swTotal.Elapsed.TotalMilliseconds;
            return new CaptureResult
            {
                CompressedBytes = encoded,
                Format = request.Format,
                Width = frame.Width,
                Height = frame.Height,
                Source = frame.Source,
                Timings = request.IncludeTelemetry ? timings : null
            };
        }

        private static async Task<PreparedFrame> Prepare(CaptureRequest request, CancellationToken cancellationToken)
        {
            GameObject target = ResolveTarget(request);
            if (request.View == CaptureView.Scene)
            {
                RenderTexture scene = SceneViewCaptureSource.Capture(target, request);
                return new PreparedFrame
                {
                    Texture = scene,
                    Width = scene.width,
                    Height = scene.height,
                    Source = "scene_view"
                };
            }

            return await PrepareGame(request, target, cancellationToken);
        }

        private static GameObject ResolveTarget(CaptureRequest request)
        {
            bool needsTarget = request.View == CaptureView.Scene
                || request.TargetInstanceId != 0
                || !string.IsNullOrEmpty(request.TargetPath);
            if (!needsTarget) return null;
            return CaptureTargetResolver.Resolve(request.TargetInstanceId, request.TargetPath);
        }

        private static async Task<PreparedFrame> PrepareGame(
            CaptureRequest request,
            GameObject target,
            CancellationToken cancellationToken)
        {
            EditorWindow gameView = GameViewCaptureSource.FindOpenGameView();
            if (gameView == null)
            {
                throw new CaptureException(CaptureErrorCode.GameViewUnavailable, "Game View window not found or not open.");
            }

            gameView.Focus();
            RenderTexture presented = await WaitForPresentedRt(gameView, cancellationToken);
            if (presented == null)
            {
                throw new CaptureException(CaptureErrorCode.CaptureSourceEmpty, "Game View window has no capturable area.");
            }

            Vector2Int output;
            RenderTexture submitted;
            if (target == null)
            {
                output = GameViewCaptureSource.ResolveOutputSize(presented.width, presented.height, request);
                submitted = GameViewCaptureSource.CopyToOwned(presented, output.x, output.y);
            }
            else
            {
                RectInt crop = CaptureFraming.ProjectToRenderTexture(target, presented.width, presented.height);
                output = GameViewCaptureSource.ResolveOutputSize(crop.width, crop.height, request);
                submitted = GameViewCaptureSource.CopyCropped(presented, crop, output.x, output.y);
            }

            return new PreparedFrame
            {
                Texture = submitted,
                Width = output.x,
                Height = output.y,
                Source = "game_view"
            };
        }

        private struct PreparedFrame
        {
            public RenderTexture Texture;
            public int Width;
            public int Height;
            public string Source;
        }
    }
}
