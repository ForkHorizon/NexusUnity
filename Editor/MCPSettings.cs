using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Manages settings for the MCP Server, exposed via Project Settings.
    /// </summary>
    public static class MCPSettings
    {
        private const string _PORT_KEY = "UnityMCP_Server_Port";
        private const string _CONSOLE_LOG_MODE_KEY = "UnityMCP_Console_Log_Mode";
        private const string _CONSOLE_LOG_CATEGORIES_KEY = "UnityMCP_Console_Log_Categories";
        private const int _DEFAULT_PORT = 8081;
        private const NexusConsoleLogMode _DEFAULT_CONSOLE_LOG_MODE = NexusConsoleLogMode.Important;
        private const NexusLogCategory _DEFAULT_LOG_CATEGORIES = NexusLogCategory.All;
        private static int _settingsMainThreadId = -1;
        private static NexusConsoleLogMode _consoleLogModeCache = _DEFAULT_CONSOLE_LOG_MODE;
        private static NexusLogCategory _enabledLogCategoriesCache = _DEFAULT_LOG_CATEGORIES;
        private static bool _consoleLoggingPrefsLoaded;

        internal static readonly NexusLogCategory[] LogCategoryOptions =
        {
            NexusLogCategory.Server,
            NexusLogCategory.Integrations,
            NexusLogCategory.Api,
            NexusLogCategory.UiAutomation,
            NexusLogCategory.Diagnostics,
            NexusLogCategory.Audit,
            NexusLogCategory.Runtime
        };

        /// <summary>
        /// The local server port for the MCP Server.
        /// </summary>
        public static int Port
        {
            get {
                int p = EditorPrefs.GetInt(_PORT_KEY, _DEFAULT_PORT);
                return p <= 0 ? _DEFAULT_PORT : p;
            }
            set => EditorPrefs.SetInt(_PORT_KEY, value);
        }

        public static NexusConsoleLogMode ConsoleLogMode
        {
            get
            {
                EnsureConsoleLoggingPrefsLoaded();
                return _consoleLogModeCache;
            }
            set
            {
                _consoleLogModeCache = value;
                _consoleLoggingPrefsLoaded = true;
                if (CanUseEditorPrefs())
                {
                    EditorPrefs.SetInt(_CONSOLE_LOG_MODE_KEY, (int)value);
                }
            }
        }

        public static NexusLogCategory EnabledLogCategories
        {
            get
            {
                EnsureConsoleLoggingPrefsLoaded();
                return _enabledLogCategoriesCache;
            }
            set
            {
                _enabledLogCategoriesCache = value & NexusLogCategory.All;
                _consoleLoggingPrefsLoaded = true;
                if (CanUseEditorPrefs())
                {
                    EditorPrefs.SetInt(_CONSOLE_LOG_CATEGORIES_KEY, (int)_enabledLogCategoriesCache);
                }
            }
        }

        [InitializeOnLoadMethod]
        internal static void LoadConsoleLoggingPrefsFromEditor()
        {
            if (_settingsMainThreadId == -1)
            {
                _settingsMainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            if (!CanUseEditorPrefs()) return;

            int rawMode = EditorPrefs.GetInt(_CONSOLE_LOG_MODE_KEY, (int)_DEFAULT_CONSOLE_LOG_MODE);
            _consoleLogModeCache = System.Enum.IsDefined(typeof(NexusConsoleLogMode), rawMode)
                ? (NexusConsoleLogMode)rawMode
                : _DEFAULT_CONSOLE_LOG_MODE;

            int rawCategories = EditorPrefs.GetInt(_CONSOLE_LOG_CATEGORIES_KEY, (int)_DEFAULT_LOG_CATEGORIES);
            _enabledLogCategoriesCache = (NexusLogCategory)rawCategories & NexusLogCategory.All;
            _consoleLoggingPrefsLoaded = true;
        }

        private static void EnsureConsoleLoggingPrefsLoaded()
        {
            if (_consoleLoggingPrefsLoaded) return;
            LoadConsoleLoggingPrefsFromEditor();
        }

        private static bool CanUseEditorPrefs()
        {
            return _settingsMainThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _settingsMainThreadId;
        }

        public static void ResetConsoleLoggingDefaults()
        {
            ConsoleLogMode = _DEFAULT_CONSOLE_LOG_MODE;
            EnabledLogCategories = _DEFAULT_LOG_CATEGORIES;
        }

        internal static void ClearConsoleLoggingPrefsForTests()
        {
            EditorPrefs.DeleteKey(_CONSOLE_LOG_MODE_KEY);
            EditorPrefs.DeleteKey(_CONSOLE_LOG_CATEGORIES_KEY);
            _consoleLogModeCache = _DEFAULT_CONSOLE_LOG_MODE;
            _enabledLogCategoriesCache = _DEFAULT_LOG_CATEGORIES;
            _consoleLoggingPrefsLoaded = false;
        }

        internal static string GetLogCategoryLabel(NexusLogCategory category)
        {
            return ObjectNames.NicifyVariableName(category.ToString());
        }

        internal static string GetConsoleLoggingSummary()
        {
            switch (ConsoleLogMode)
            {
                case NexusConsoleLogMode.All:
                    return "Console logging: All Nexus Unity messages.";
                case NexusConsoleLogMode.Custom:
                    return "Console logging: Custom categories (" + EnabledLogCategories + ").";
                default:
                    return "Console logging: Important Nexus Unity messages, warnings, and errors.";
            }
        }

        internal static void SetLogCategoryEnabled(NexusLogCategory category, bool enabled)
        {
            var categories = EnabledLogCategories;
            if (enabled) categories |= category;
            else categories &= ~category;
            EnabledLogCategories = categories;
        }

        /// <summary>
        /// Shows the main Nexus Unity window.
        /// </summary>
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<MCPServerWindow>("Nexus Unity");
        }

        /// <summary>
        /// Creates the SettingsProvider for the MCP Server.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/Nexus Unity", SettingsScope.Project)
            {
                label = "Nexus Unity",
                guiHandler = (searchContext) =>
                {
                    EditorGUILayout.Space();
                    DrawServerSettings();
                    GUILayout.Space(12);
                    DrawConsoleLoggingSettings();
                    GUILayout.Space(10);
                    GUILayout.Label(new GUIContent("Changes to port require server restart.", "Restart the server from the Nexus Unity control panel after changing this setting"), EditorStyles.helpBox);
                },

                keywords = new HashSet<string>(new[] { "MCP", "Server", "Port", "AI", "Logs", "Console", "Logging" })
            };

            return provider;
        }

        private static void DrawServerSettings()
        {
            GUILayout.Label(new GUIContent("Server", "Local port and server settings for Nexus Unity"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int newPort = EditorGUILayout.IntField(new GUIContent("Server Port", "The port the MCP server listens on."), Port);
            if (EditorGUI.EndChangeCheck())
            {
                Port = newPort;
            }
        }

        private static void DrawConsoleLoggingSettings()
        {
            GUILayout.Label(new GUIContent("Console Logging", "Controls which Nexus Unity service messages are written to the Unity Console."), EditorStyles.boldLabel);

            ConsoleLogMode = (NexusConsoleLogMode)EditorGUILayout.EnumPopup(
                new GUIContent("Mode", "Important keeps the Console quieter. All shows every Nexus Unity diagnostic. Custom filters info logs by category."),
                ConsoleLogMode);

            if (ConsoleLogMode == NexusConsoleLogMode.Custom)
            {
                EditorGUI.indentLevel++;
                foreach (var category in LogCategoryOptions)
                {
                    bool isEnabled = (EnabledLogCategories & category) != 0;
                    SetLogCategoryEnabled(category, EditorGUILayout.ToggleLeft(GetLogCategoryLabel(category), isEnabled));
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.Label(new GUIContent(GetConsoleLoggingSummary(), "Warnings and errors are always shown."), EditorStyles.helpBox);
            if (GUILayout.Button("Reset Console Logging Defaults"))
            {
                ResetConsoleLoggingDefaults();
            }
        }
    }
}
