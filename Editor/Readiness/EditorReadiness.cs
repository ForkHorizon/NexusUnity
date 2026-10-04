using System;
using System.Threading;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Single Unity Editor readiness predicate shared by status and waits.
    /// Persists compile and reload epochs in SessionState because domain reload clears statics.
    /// Publishes a snapshot the HTTP listener can read without calling Unity APIs.
    /// </summary>
    internal static partial class EditorReadiness
    {
        internal const string ReasonReady = "ready";
        internal const string ReasonAlreadyReady = "already_ready";
        internal const string ReasonCompiling = "compiling";
        internal const string ReasonDomainReload = "domain_reload";
        internal const string ReasonPendingRefresh = "pending_refresh";
        internal const string ReasonScriptWrite = "script_write";
        internal const string ReasonPlayTransition = "play_transition";
        internal const string ReasonImporting = "importing";
        internal const int QuietTicksToSettle = 2;

        const string CompileEpochKey = "Nexus.Readiness.CompileEpoch";
        const string ReloadEpochKey = "Nexus.Readiness.ReloadEpoch";
        const string CompileInFlightKey = "Nexus.Readiness.CompileInFlight";
        const string ReloadArmedKey = "Nexus.Readiness.ReloadArmed";
        const string ScriptWriteKey = "Nexus.Readiness.ScriptWrite";
        const string PendingRefreshKey = "Nexus.Readiness.PendingRefresh";
        const string RefreshInvokedKey = "Nexus.Readiness.RefreshInvoked";
        const string SeenCompileKey = "Nexus.Readiness.SeenCompile";
        const string ReloadExpectedKey = "Nexus.Readiness.ReloadExpected";
        const string BaselineCompileKey = "Nexus.Readiness.BaselineCompile";
        const string BaselineReloadKey = "Nexus.Readiness.BaselineReload";
        const string QuietTicksKey = "Nexus.Readiness.QuietTicks";

        static readonly string[] SessionKeys =
        {
            CompileEpochKey, ReloadEpochKey, CompileInFlightKey, ReloadArmedKey, ScriptWriteKey,
            PendingRefreshKey, RefreshInvokedKey, SeenCompileKey, ReloadExpectedKey, BaselineCompileKey, BaselineReloadKey, QuietTicksKey
        };

        static readonly object PublishLock = new object();
        static Snapshot _published = Snapshot.Unknown;
        static string _idleReason = ReasonAlreadyReady;
        static bool _isCompiling;
        static bool _isUpdating;
        static bool _isPlayTransition;
        static int _testDepth;

        internal readonly struct Snapshot
        {
            public readonly bool Ready;
            public readonly int CompileEpoch;
            public readonly int ReloadEpoch;
            public readonly string Reason;
            public readonly bool Reconnect;
            public readonly bool IsCompiling;
            public readonly bool IsUpdating;
            public readonly bool IsPlayTransition;
            public readonly bool ScriptWritePending;
            public readonly bool PendingRefresh;

            internal Snapshot(
                bool ready, int compileEpoch, int reloadEpoch, string reason, bool reconnect,
                bool isCompiling, bool isUpdating, bool isPlayTransition, bool scriptWritePending, bool pendingRefresh)
            {
                Ready = ready;
                CompileEpoch = compileEpoch;
                ReloadEpoch = reloadEpoch;
                Reason = reason;
                Reconnect = reconnect;
                IsCompiling = isCompiling;
                IsUpdating = isUpdating;
                IsPlayTransition = isPlayTransition;
                ScriptWritePending = scriptWritePending;
                PendingRefresh = pendingRefresh;
            }

            public static Snapshot Unknown => new Snapshot(false, 0, 0, ReasonPendingRefresh, true, false, false, false, false, true);
        }

        internal static Snapshot Published
        {
            get { lock (PublishLock) return _published; }
        }

        internal static bool ScriptRefreshQueued => Get(ScriptWriteKey) == 1 && Get(RefreshInvokedKey) == 0;

        static EditorReadiness()
        {
            ConsumeArmedReload();
            Subscribe();
        }

        internal static void BeginTestOverride() => Interlocked.Increment(ref _testDepth);

        internal static void EndTestOverride() => Interlocked.Decrement(ref _testDepth);

        internal static void NotifyEditorTick(bool isCompiling, bool isUpdating, bool isPlayTransition)
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            ApplyTick(isCompiling, isUpdating, isPlayTransition);
        }

        internal static void DriveTickForTests(bool isCompiling, bool isUpdating, bool isPlayTransition) =>
            ApplyTick(isCompiling, isUpdating, isPlayTransition);

        internal static void PublishFromEditor()
        {
            if (Volatile.Read(ref _testDepth) > 0) return;
            bool transition = EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;
            ApplyTick(EditorApplication.isCompiling, EditorApplication.isUpdating, transition);
        }

        internal static void ArmScriptWrite()
        {
            Set(ScriptWriteKey, 1);
            Set(PendingRefreshKey, 1);
            Set(RefreshInvokedKey, 0);
            Set(SeenCompileKey, 0);
            Set(ReloadExpectedKey, 0);
            Set(BaselineCompileKey, Get(CompileEpochKey));
            Set(BaselineReloadKey, Get(ReloadEpochKey));
            Set(QuietTicksKey, 0);
            PublishEvaluate();
        }

        internal static void ArmPendingRefreshForTests()
        {
            Set(PendingRefreshKey, 1);
            Set(RefreshInvokedKey, 0);
            Set(QuietTicksKey, 0);
            PublishEvaluate();
        }

        internal static void NotifyRefreshInvoked()
        {
            Set(RefreshInvokedKey, 1);
            Set(QuietTicksKey, 0);
            PublishEvaluate();
        }

        internal static void NotifyCompilationStarted()
        {
            Set(CompileInFlightKey, Get(CompileInFlightKey) + 1);
            Set(SeenCompileKey, 1);
            Set(QuietTicksKey, 0);
            _isCompiling = true;
            PublishEvaluate();
        }

        internal static void NotifyCompilationFinished(bool reloadExpected)
        {
            Set(CompileEpochKey, Get(CompileEpochKey) + 1);
            Set(CompileInFlightKey, Math.Max(0, Get(CompileInFlightKey) - 1));
            Set(QuietTicksKey, 0);
            if (reloadExpected) Set(ReloadExpectedKey, 1);
            _isCompiling = Get(CompileInFlightKey) > 0;
            PublishEvaluate();
        }

        internal static void NotifyBeforeReload()
        {
            Set(ReloadArmedKey, 1);
            Set(QuietTicksKey, 0);
            PublishEvaluate();
        }

        internal static void ConsumeArmedReload()
        {
            if (Get(ReloadArmedKey) != 1) return;
            Set(ReloadArmedKey, 0);
            Set(ReloadEpochKey, Get(ReloadEpochKey) + 1);
            Set(CompileInFlightKey, 0);
            _isCompiling = false;
        }

        internal static void NotifyDomainLoadedForTests()
        {
            ConsumeArmedReload();
            PublishEvaluate();
        }

        static void ApplyTick(bool isCompiling, bool isUpdating, bool isPlayTransition)
        {
            _isCompiling = isCompiling;
            _isUpdating = isUpdating;
            _isPlayTransition = isPlayTransition;
            UpdateQuiet(isCompiling, isUpdating);
            PublishEvaluate();
        }

        static void UpdateQuiet(bool isCompiling, bool isUpdating)
        {
            bool busy = Get(CompileInFlightKey) > 0 || Get(ReloadArmedKey) == 1 || isCompiling || isUpdating;
            if (busy || Get(RefreshInvokedKey) != 1)
            {
                if (busy) Set(QuietTicksKey, 0);
                return;
            }

            Set(QuietTicksKey, Get(QuietTicksKey) + 1);
        }

        static void PublishEvaluate()
        {
            SettleLatches();
            Snapshot snapshot = BuildSnapshot();
            lock (PublishLock) _published = snapshot;
        }

        static void SettleLatches()
        {
            bool scriptWrite = Get(ScriptWriteKey) == 1;
            bool pending = Get(PendingRefreshKey) == 1;
            if (!scriptWrite && !pending) return;

            bool reloadArmed = Get(ReloadArmedKey) == 1;
            bool quiet = Get(QuietTicksKey) >= QuietTicksToSettle
                && Get(CompileInFlightKey) == 0
                && !_isCompiling && !_isUpdating && !reloadArmed
                && Get(RefreshInvokedKey) == 1;
            bool seenCompile = Get(SeenCompileKey) == 1;
            // A successful compile reloads the domain. Two quiet ticks in the gap before
            // beforeAssemblyReload must not clear the latch; only a failed compile settles here.
            bool reloadSatisfies = scriptWrite && seenCompile && Get(ReloadEpochKey) > Get(BaselineReloadKey);
            bool failedCompileSettled = scriptWrite && quiet && seenCompile
                && Get(ReloadExpectedKey) == 0
                && Get(CompileEpochKey) > Get(BaselineCompileKey);
            bool noOp = scriptWrite && quiet && !seenCompile;
            if (reloadSatisfies || failedCompileSettled)
            {
                _idleReason = ReasonReady;
                ClearLatches();
                return;
            }

            if (noOp || (!scriptWrite && pending && quiet))
            {
                _idleReason = ReasonAlreadyReady;
                ClearLatches();
            }
        }

        static void ClearLatches()
        {
            Set(ScriptWriteKey, 0);
            Set(PendingRefreshKey, 0);
            Set(RefreshInvokedKey, 0);
            Set(SeenCompileKey, 0);
            Set(ReloadExpectedKey, 0);
            Set(QuietTicksKey, 0);
        }

        static Snapshot BuildSnapshot()
        {
            bool scriptWrite = Get(ScriptWriteKey) == 1;
            bool pending = Get(PendingRefreshKey) == 1;
            bool refreshInvoked = Get(RefreshInvokedKey) == 1;
            bool reloadArmed = Get(ReloadArmedKey) == 1;
            bool compiling = _isCompiling || Get(CompileInFlightKey) > 0;
            string blocking = BlockingReason(reloadArmed, _isPlayTransition, compiling, scriptWrite, pending, refreshInvoked, _isUpdating);
            bool ready = blocking == null;
            return new Snapshot(
                ready,
                Get(CompileEpochKey),
                Get(ReloadEpochKey),
                ready ? _idleReason : blocking,
                reloadArmed,
                _isCompiling,
                _isUpdating,
                _isPlayTransition,
                scriptWrite,
                pending);
        }

        static string BlockingReason(bool reloadArmed, bool playTransition, bool compiling, bool scriptWrite, bool pending, bool refreshInvoked, bool updating)
        {
            if (reloadArmed) return ReasonDomainReload;
            if (playTransition) return ReasonPlayTransition;
            if (compiling) return ReasonCompiling;
            if (scriptWrite) return ReasonScriptWrite;
            if (pending && !refreshInvoked) return ReasonPendingRefresh;
            if (updating) return ReasonImporting;
            if (pending) return ReasonPendingRefresh;
            return null;
        }

        static int Get(string key) => SessionState.GetInt(key, 0);

        static void Set(string key, int value) => SessionState.SetInt(key, value);
    }
}
