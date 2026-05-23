using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class ConsoleLoggingSettingsTests
    {
        private NexusConsoleLogMode _originalMode;
        private NexusLogCategory _originalCategories;

        [SetUp]
        public void SaveSettings()
        {
            _originalMode = MCPSettings.ConsoleLogMode;
            _originalCategories = MCPSettings.EnabledLogCategories;
        }

        [TearDown]
        public void RestoreSettings()
        {
            MCPSettings.ConsoleLogMode = _originalMode;
            MCPSettings.EnabledLogCategories = _originalCategories;
        }

        [Test]
        public void ConsoleLoggingDefaultsToImportant()
        {
            MCPSettings.ClearConsoleLoggingPrefsForTests();

            Assert.AreEqual(NexusConsoleLogMode.Important, MCPSettings.ConsoleLogMode);
            Assert.AreEqual(NexusLogCategory.All, MCPSettings.EnabledLogCategories);
        }

        [Test]
        public void ConsoleLoggingSettingsPersist()
        {
            MCPSettings.ConsoleLogMode = NexusConsoleLogMode.Custom;
            MCPSettings.EnabledLogCategories = NexusLogCategory.Api | NexusLogCategory.Audit;

            Assert.AreEqual(NexusConsoleLogMode.Custom, MCPSettings.ConsoleLogMode);
            Assert.AreEqual(NexusLogCategory.Api | NexusLogCategory.Audit, MCPSettings.EnabledLogCategories);
        }

        [Test]
        public void ImportantModeKeepsConsoleQuietExceptImportantWarningsAndErrors()
        {
            MCPSettings.ConsoleLogMode = NexusConsoleLogMode.Important;
            MCPSettings.EnabledLogCategories = NexusLogCategory.None;

            Assert.IsFalse(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Api, false, LogType.Log));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Server, true, LogType.Log));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Api, false, LogType.Warning));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Api, false, LogType.Error));
        }

        [Test]
        public void AllModeShowsVerboseInfoLogs()
        {
            MCPSettings.ConsoleLogMode = NexusConsoleLogMode.All;
            MCPSettings.EnabledLogCategories = NexusLogCategory.None;

            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Api, false, LogType.Log));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.UiAutomation, false, LogType.Log));
        }

        [Test]
        public void CustomModeFiltersInfoLogsByCategory()
        {
            MCPSettings.ConsoleLogMode = NexusConsoleLogMode.Custom;
            MCPSettings.EnabledLogCategories = NexusLogCategory.Api | NexusLogCategory.UiAutomation;

            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Api, false, LogType.Log));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.UiAutomation, false, LogType.Log));
            Assert.IsFalse(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Server, false, LogType.Log));
            Assert.IsTrue(NexusEditorLog.ShouldWriteToConsole(NexusLogCategory.Server, false, LogType.Warning));
        }
    }
}
