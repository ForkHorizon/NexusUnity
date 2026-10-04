using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    internal static partial class EditorReadiness
    {
        internal static int[] CaptureSessionForTests()
        {
            var values = new int[SessionKeys.Length];
            for (int i = 0; i < SessionKeys.Length; i++) values[i] = Get(SessionKeys[i]);
            return values;
        }

        internal static string CaptureIdleReasonForTests() => _idleReason;

        internal static void RestoreSessionForTests(int[] values, string idleReason)
        {
            for (int i = 0; i < SessionKeys.Length && i < values.Length; i++) Set(SessionKeys[i], values[i]);
            _idleReason = string.IsNullOrEmpty(idleReason) ? ReasonAlreadyReady : idleReason;
            PublishEvaluate();
        }

        internal static void ForgetPublishedForTests()
        {
            lock (PublishLock) _published = Snapshot.Unknown;
        }

        internal static void RebuildFromSessionForTests() => PublishEvaluate();

        internal static void ResetForTests()
        {
            for (int i = 0; i < SessionKeys.Length; i++) Set(SessionKeys[i], 0);
            _idleReason = ReasonAlreadyReady;
            _isCompiling = false;
            _isUpdating = false;
            _isPlayTransition = false;
            PublishEvaluate();
        }

        internal static JObject ToJson(Snapshot snap)
        {
            return new JObject
            {
                ["ready"] = snap.Ready,
                ["reason"] = snap.Reason,
                ["reconnect"] = snap.Reconnect,
                ["epoch"] = new JObject { ["compile"] = snap.CompileEpoch, ["reload"] = snap.ReloadEpoch }
            };
        }

        internal static JObject BuildEditorIdle(Snapshot snap)
        {
            JObject payload = ToJson(snap);
            payload["is_idle"] = snap.Ready;
            payload["is_compiling"] = snap.IsCompiling;
            payload["is_updating"] = snap.IsUpdating;
            return payload;
        }

        internal static JObject BuildWaitPayload(Snapshot snap, double waitedSeconds, bool timedOut)
        {
            bool ready = snap.Ready && !timedOut;
            string status = ready ? "Ready" : (snap.Reconnect && !timedOut ? "Reloading" : "Timeout");
            JObject payload = ToJson(snap);
            payload["ready"] = ready;
            payload["status"] = status;
            payload["time_waited_seconds"] = Math.Round(waitedSeconds, 2);
            payload["is_compiling"] = snap.IsCompiling;
            payload["is_updating"] = snap.IsUpdating;
            return payload;
        }

        internal static string BusyReason(Snapshot snap)
        {
            if (snap.Ready) return "idle";
            if (snap.Reason == ReasonCompiling) return "compiling";
            if (snap.Reason == ReasonPlayTransition) return "play_mode_transition";
            if (snap.Reason == ReasonDomainReload) return "domain_reload";
            return "importing";
        }

        internal static bool ImportBlocked(Snapshot snap)
        {
            return snap.Reconnect
                || snap.Reason == ReasonDomainReload
                || snap.Reason == ReasonImporting
                || snap.Reason == ReasonPendingRefresh
                || snap.Reason == ReasonScriptWrite
                || snap.IsUpdating;
        }

        internal static bool BlocksForcedRefresh(Snapshot snap)
        {
            return ScriptRefreshQueued
                || snap.Reason == ReasonCompiling
                || snap.Reason == ReasonScriptWrite
                || snap.Reason == ReasonDomainReload
                || snap.Reason == ReasonPendingRefresh;
        }

        static void Subscribe()
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnCompilationStarted(object _)
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            NotifyCompilationStarted();
        }

        static void OnCompilationFinished(object _)
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            NotifyCompilationFinished(!EditorUtility.scriptCompilationFailed);
        }

        static void OnBeforeAssemblyReload()
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            NotifyBeforeReload();
        }

        static void OnAfterAssemblyReload()
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            ConsumeArmedReload();
            PublishFromEditor();
        }

        static void OnPlayModeChanged(PlayModeStateChange _)
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            PublishFromEditor();
        }
    }
}
