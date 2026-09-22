using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor.Capture
{
    /// <summary>
    /// Production readback: <c>AsyncGPUReadback.Request</c>, Editor update poll, then <c>GetData&lt;byte&gt;()</c>.
    /// Never waits synchronously for GPU completion.
    /// </summary>
    internal static class DriverOwnedReadback
    {
        private const int PollTimeoutMs = 8000;
        private static readonly List<Pending> PendingReads = new List<Pending>();
        private static bool _reloading;

        private sealed class Pending
        {
            public AsyncGPUReadbackRequest Request;
            public RenderTexture SubmittedRt;
            public int Width;
            public int Height;
            public CaptureFormat Format;
            public int JpegQuality;
            public CaptureTimings Timings;
            public TaskCompletionSource<byte[]> Tcs;
            public CancellationToken CancellationToken;
            public Stopwatch DoneWatch;
            public long Deadline;
            public bool LogicalCompleted;
            public EditorApplication.CallbackFunction Poll;
            public int EditorTicks;
        }

        [InitializeOnLoadMethod]
        private static void HookDomainReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= FailPendingForReload;
            AssemblyReloadEvents.beforeAssemblyReload += FailPendingForReload;
        }

        internal static bool IsReloading => _reloading;

        internal static int PendingCount
        {
            get { lock (PendingReads) return PendingReads.Count; }
        }

        internal static void NotifyDomainReloadForTests()
        {
            FailPendingForReload();
        }

        internal static void ResetReloadingForTests()
        {
            _reloading = false;
        }

        /// <summary>
        /// True when a GPU readback is in the pending list. <paramref name="done"/> is the current request.done flag.
        /// </summary>
        internal static bool TryDescribeInFlight(out bool submitted, out bool done)
        {
            lock (PendingReads)
            {
                if (PendingReads.Count == 0)
                {
                    submitted = false;
                    done = true;
                    return false;
                }

                Pending pending = PendingReads[PendingReads.Count - 1];
                submitted = true;
                done = pending.Request.done;
                return true;
            }
        }

        internal static Task<byte[]> ReadAndEncode(
            RenderTexture submittedRt,
            int width,
            int height,
            CaptureFormat format,
            int jpegQuality,
            CaptureTimings timings,
            CancellationToken cancellationToken)
        {
            if (_reloading)
            {
                if (submittedRt != null) RenderTexture.ReleaseTemporary(submittedRt);
                throw new CaptureException(CaptureErrorCode.DomainReloadInterrupted, "Capture rejected during domain reload.");
            }

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending pending;
            try
            {
                pending = CreatePending(
                    submittedRt, width, height, format, jpegQuality, timings, tcs, cancellationToken);
            }
            catch
            {
                if (submittedRt != null) RenderTexture.ReleaseTemporary(submittedRt);
                throw;
            }

            StartPoll(pending);
            return tcs.Task;
        }

        private static Pending CreatePending(
            RenderTexture submittedRt,
            int width,
            int height,
            CaptureFormat format,
            int jpegQuality,
            CaptureTimings timings,
            TaskCompletionSource<byte[]> tcs,
            CancellationToken cancellationToken)
        {
            var swSubmit = Stopwatch.StartNew();
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(submittedRt);
            swSubmit.Stop();
            timings.ReadbackSubmitMs = swSubmit.Elapsed.TotalMilliseconds;
            timings.EditorTicksSubmitToDone = 0;
            return new Pending
            {
                Request = request,
                SubmittedRt = submittedRt,
                Width = width,
                Height = height,
                Format = format,
                JpegQuality = jpegQuality,
                Timings = timings,
                Tcs = tcs,
                CancellationToken = cancellationToken,
                DoneWatch = Stopwatch.StartNew(),
                Deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * PollTimeoutMs / 1000
            };
        }

        private static void StartPoll(Pending pending)
        {
            lock (PendingReads) PendingReads.Add(pending);
            pending.Poll = () => PollTick(pending);
            EditorApplication.update += pending.Poll;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void PollTick(Pending pending)
        {
            pending.EditorTicks++;
            pending.Timings.EditorTicksSubmitToDone = pending.EditorTicks;
            if (!pending.Request.done)
            {
                HandleIncompletePoll(pending);
                return;
            }

            EditorApplication.update -= pending.Poll;
            RemovePending(pending);
            pending.Timings.SubmitToDoneMs = pending.DoneWatch.Elapsed.TotalMilliseconds;
            if (!pending.LogicalCompleted) Finish(pending);
            ReleaseSubmitted(pending);
        }

        private static void HandleIncompletePoll(Pending pending)
        {
            if (!pending.LogicalCompleted && Stopwatch.GetTimestamp() > pending.Deadline)
            {
                pending.LogicalCompleted = true;
                pending.Tcs.TrySetException(new CaptureException(
                    CaptureErrorCode.ReadbackFailed, "GPU readback timed out."));
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Finish(Pending pending)
        {
            pending.LogicalCompleted = true;
            if (pending.CancellationToken.IsCancellationRequested)
            {
                pending.Tcs.TrySetCanceled(pending.CancellationToken);
                return;
            }

            if (pending.Request.hasError)
            {
                pending.Tcs.TrySetException(new CaptureException(
                    CaptureErrorCode.ReadbackFailed, "GPU readback failed."));
                return;
            }

            var swEncode = Stopwatch.StartNew();
            byte[] encoded = CaptureEncoder.Encode(
                pending.Request.GetData<byte>(), pending.Width, pending.Height,
                pending.Format, pending.JpegQuality);
            swEncode.Stop();
            pending.Timings.EncodeMs = swEncode.Elapsed.TotalMilliseconds;
            pending.Tcs.TrySetResult(encoded);
        }

        private static void ReleaseSubmitted(Pending pending)
        {
            if (pending.SubmittedRt == null) return;
            RenderTexture.ReleaseTemporary(pending.SubmittedRt);
            pending.SubmittedRt = null;
        }

        private static void RemovePending(Pending pending)
        {
            lock (PendingReads) PendingReads.Remove(pending);
        }

        private static void FailPendingForReload()
        {
            _reloading = true;
            Pending[] snapshot;
            lock (PendingReads)
            {
                snapshot = PendingReads.ToArray();
                PendingReads.Clear();
            }

            foreach (Pending pending in snapshot)
            {
                if (pending.Poll != null) EditorApplication.update -= pending.Poll;
                pending.LogicalCompleted = true;
                pending.SubmittedRt = null;
                pending.Tcs.TrySetException(new CaptureException(
                    CaptureErrorCode.DomainReloadInterrupted,
                    "Capture cancelled because a domain reload began while GPU readback was pending."));
            }
        }
    }
}
