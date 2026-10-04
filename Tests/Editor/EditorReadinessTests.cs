using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class EditorReadinessTests
    {
        int[] _savedSession;
        string _savedReason;

        [SetUp]
        public void SetUp()
        {
            EditorReadiness.BeginTestOverride();
            _savedSession = EditorReadiness.CaptureSessionForTests();
            _savedReason = EditorReadiness.CaptureIdleReasonForTests();
            EditorReadiness.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            EditorReadiness.RestoreSessionForTests(_savedSession, _savedReason);
            EditorReadiness.EndTestOverride();
            EditorReadiness.PublishFromEditor();
        }

        [Test]
        public void AlreadyReady_WhenNothingIsPending()
        {
            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            Assert.IsTrue(snap.Ready);
            Assert.AreEqual(EditorReadiness.ReasonAlreadyReady, snap.Reason);
            Assert.IsFalse(snap.Reconnect);
            Assert.AreEqual("idle", EditorReadiness.BusyReason(snap));
            Assert.AreEqual(true, EditorReadiness.BuildEditorIdle(snap)["is_idle"].Value<bool>());
        }

        [Test]
        public void CompileEpoch_IncrementsWhenCompilationFinishes()
        {
            EditorReadiness.NotifyCompilationStarted();
            Assert.IsFalse(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonCompiling, EditorReadiness.Published.Reason);
            Assert.AreEqual(0, EditorReadiness.Published.CompileEpoch);

            EditorReadiness.NotifyCompilationFinished(false);
            Assert.AreEqual(1, EditorReadiness.Published.CompileEpoch);
            Assert.IsTrue(EditorReadiness.Published.Ready);
            Assert.AreEqual("idle", EditorReadiness.BusyReason(EditorReadiness.Published));
        }

        [Test]
        public void ReloadEpoch_IncrementsOnceWhenArmedReloadIsConsumed()
        {
            EditorReadiness.NotifyBeforeReload();
            Assert.IsFalse(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonDomainReload, EditorReadiness.Published.Reason);
            Assert.IsTrue(EditorReadiness.Published.Reconnect);
            Assert.AreEqual(0, EditorReadiness.Published.ReloadEpoch);

            EditorReadiness.NotifyDomainLoadedForTests();
            Assert.AreEqual(1, EditorReadiness.Published.ReloadEpoch);
            Assert.IsFalse(EditorReadiness.Published.Reconnect);

            EditorReadiness.NotifyDomainLoadedForTests();
            Assert.AreEqual(1, EditorReadiness.Published.ReloadEpoch);
        }

        [Test]
        public void Epochs_PersistAcrossSimulatedDomainReset()
        {
            EditorReadiness.NotifyCompilationFinished(false);
            EditorReadiness.NotifyBeforeReload();
            EditorReadiness.NotifyDomainLoadedForTests();
            int compile = EditorReadiness.Published.CompileEpoch;
            int reload = EditorReadiness.Published.ReloadEpoch;

            EditorReadiness.ForgetPublishedForTests();
            Assert.AreEqual(0, EditorReadiness.Published.CompileEpoch);
            Assert.AreEqual(0, EditorReadiness.Published.ReloadEpoch);

            EditorReadiness.RebuildFromSessionForTests();
            Assert.AreEqual(compile, EditorReadiness.Published.CompileEpoch);
            Assert.AreEqual(reload, EditorReadiness.Published.ReloadEpoch);
        }

        [Test]
        public void ScriptWrite_IsNotReadyBeforeCompileStarts()
        {
            EditorReadiness.ArmScriptWrite();
            for (int i = 0; i < 10; i++) EditorReadiness.DriveTickForTests(false, false, false);

            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            Assert.IsFalse(snap.Ready);
            Assert.AreEqual(EditorReadiness.ReasonScriptWrite, snap.Reason);
            JObject wait = EditorReadiness.BuildWaitPayload(snap, 0.099, false);
            Assert.AreEqual("Timeout", wait["status"].ToString());
            Assert.IsFalse(wait["ready"].Value<bool>());
            Assert.AreEqual("importing", EditorReadiness.BusyReason(snap));
            Assert.AreEqual(false, EditorReadiness.BuildEditorIdle(snap)["is_idle"].Value<bool>());
        }

        [Test]
        public void PendingRefresh_IsNotReadyUntilRefreshSettles()
        {
            EditorReadiness.ArmPendingRefreshForTests();
            Assert.AreEqual(EditorReadiness.ReasonPendingRefresh, EditorReadiness.Published.Reason);

            EditorReadiness.NotifyRefreshInvoked();
            EditorReadiness.DriveTickForTests(false, true, false);
            Assert.AreEqual(EditorReadiness.ReasonImporting, EditorReadiness.Published.Reason);
            Assert.IsFalse(EditorReadiness.Published.Ready, "Importing is not ready.");

            EditorReadiness.DriveTickForTests(false, false, false);
            Assert.IsFalse(EditorReadiness.Published.Ready, "One quiet tick does not settle a pending refresh.");
            EditorReadiness.DriveTickForTests(false, false, false);
            Assert.IsTrue(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonAlreadyReady, EditorReadiness.Published.Reason);
        }

        [Test]
        public void PlayTransition_IsNotReady()
        {
            EditorReadiness.DriveTickForTests(false, false, true);
            Assert.IsFalse(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonPlayTransition, EditorReadiness.Published.Reason);
            Assert.AreEqual("play_mode_transition", EditorReadiness.BusyReason(EditorReadiness.Published));
        }

        [Test]
        public void ScriptWrite_NoOpRefresh_BecomesAlreadyReadyWithoutEpochAdvance()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.NotifyRefreshInvoked();
            EditorReadiness.DriveTickForTests(false, false, false);
            EditorReadiness.DriveTickForTests(false, false, false);

            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            Assert.IsTrue(snap.Ready);
            Assert.AreEqual(EditorReadiness.ReasonAlreadyReady, snap.Reason);
            Assert.AreEqual(0, snap.CompileEpoch);
            Assert.AreEqual(0, snap.ReloadEpoch);
            Assert.AreEqual("Ready", EditorReadiness.BuildWaitPayload(snap, 0.04, false)["status"].ToString());
        }

        [Test]
        public void ScriptWrite_FailedCompile_SettlesWithoutReload()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.NotifyRefreshInvoked();
            EditorReadiness.NotifyCompilationStarted();
            EditorReadiness.NotifyCompilationFinished(false);
            Assert.IsFalse(EditorReadiness.Published.Ready, "A failed compile must not report ready on the finished callback.");

            EditorReadiness.DriveTickForTests(false, false, false);
            Assert.IsFalse(EditorReadiness.Published.Ready);
            EditorReadiness.DriveTickForTests(false, false, false);

            Assert.IsTrue(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonReady, EditorReadiness.Published.Reason);
            Assert.AreEqual(1, EditorReadiness.Published.CompileEpoch);
            Assert.AreEqual(0, EditorReadiness.Published.ReloadEpoch);
        }

        [Test]
        public void ScriptWrite_SuccessfulCompile_StaysBlockedUntilReload()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.NotifyRefreshInvoked();
            EditorReadiness.NotifyCompilationStarted();
            EditorReadiness.NotifyCompilationFinished(true);
            for (int i = 0; i < 10; i++) EditorReadiness.DriveTickForTests(false, false, false);

            Assert.IsFalse(EditorReadiness.Published.Ready);
            Assert.AreEqual(EditorReadiness.ReasonScriptWrite, EditorReadiness.Published.Reason);
            Assert.AreEqual(1, EditorReadiness.Published.CompileEpoch);
            Assert.AreEqual(0, EditorReadiness.Published.ReloadEpoch);

            EditorReadiness.NotifyBeforeReload();
            EditorReadiness.NotifyDomainLoadedForTests();
            Assert.IsTrue(EditorReadiness.Published.Ready);
            Assert.AreEqual(1, EditorReadiness.Published.ReloadEpoch);
            Assert.AreEqual(EditorReadiness.ReasonReady, EditorReadiness.Published.Reason);
        }

        [Test]
        public void ScriptWrite_ReloadWithoutCompile_DoesNotReportReady()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.NotifyBeforeReload();
            EditorReadiness.NotifyDomainLoadedForTests();
            EditorReadiness.DriveTickForTests(false, false, false);

            Assert.IsFalse(EditorReadiness.Published.Ready);
            Assert.AreEqual(1, EditorReadiness.Published.ReloadEpoch);
            Assert.IsTrue(EditorReadiness.ScriptRefreshQueued);
        }

        [Test]
        public void ScriptWrite_ReportsReadyOnlyAfterCompileAndReload()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.NotifyRefreshInvoked();
            EditorReadiness.NotifyCompilationStarted();
            EditorReadiness.NotifyCompilationFinished(true);
            EditorReadiness.NotifyBeforeReload();
            Assert.AreEqual("Reloading", EditorReadiness.BuildWaitPayload(EditorReadiness.Published, 0.2, false)["status"].ToString());

            EditorReadiness.NotifyDomainLoadedForTests();
            EditorReadiness.DriveTickForTests(false, false, false);

            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            Assert.IsTrue(snap.Ready);
            Assert.AreEqual(EditorReadiness.ReasonReady, snap.Reason);
            Assert.AreEqual(1, snap.CompileEpoch);
            Assert.AreEqual(1, snap.ReloadEpoch);
            Assert.AreEqual(true, EditorReadiness.BuildEditorIdle(snap)["ready"].Value<bool>());
            Assert.AreEqual("idle", EditorReadiness.BusyReason(snap));
        }

        [Test]
        public void StatusAndWait_ShareTheSameReadyFlag()
        {
            EditorReadiness.ArmScriptWrite();
            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            JObject idle = EditorReadiness.BuildEditorIdle(snap);
            JObject wait = EditorReadiness.BuildWaitPayload(snap, 1, false);
            Assert.AreEqual(idle["ready"].Value<bool>(), snap.Ready);
            Assert.AreEqual(wait["ready"].Value<bool>(), snap.Ready);
            Assert.AreEqual(idle["reason"].ToString(), snap.Reason);
            Assert.AreEqual(wait["reason"].ToString(), snap.Reason);
            Assert.AreEqual(idle["epoch"]["compile"].Value<int>(), wait["epoch"]["compile"].Value<int>());
            Assert.AreEqual(idle["epoch"]["reload"].Value<int>(), wait["epoch"]["reload"].Value<int>());
        }
    }
}
