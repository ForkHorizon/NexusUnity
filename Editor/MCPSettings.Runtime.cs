using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Runtime;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Runtime transport preference stored in EditorPrefs.
    /// </summary>
    public static partial class MCPSettings
    {
        private const string RuntimeModeKey = "UnityMCP_Runtime_Mode";
        private const NexusRuntimeMode DefaultRuntimeMode = NexusRuntimeMode.Auto;
        private static NexusRuntimeMode _cachedRequestedMode = DefaultRuntimeMode;
        private static bool _requestedModeCached;

        /// <summary>
        /// Gets or sets the requested Nexus runtime. Default is Auto, which prefers Pipeline on eligible installs.
        /// EditorPrefs is only read/written on the Unity main thread; other threads use the last published cache.
        /// </summary>
        public static NexusRuntimeMode RuntimeMode
        {
            get
            {
                if (CanUseEditorPrefs())
                {
                    _cachedRequestedMode = NexusRuntimeSelector.Parse(EditorPrefs.GetString(RuntimeModeKey, "auto"));
                    _requestedModeCached = true;
                    return _cachedRequestedMode;
                }

                return _requestedModeCached ? _cachedRequestedMode : DefaultRuntimeMode;
            }
            set
            {
                if (!CanUseEditorPrefs()) return;
                EditorPrefs.SetString(RuntimeModeKey, NexusRuntimeSelector.ToWireName(value));
                _cachedRequestedMode = value;
                _requestedModeCached = true;
                NexusRuntimeHost.PublishSnapshot();
            }
        }

        /// <summary>
        /// Restores the runtime preference to Auto.
        /// </summary>
        public static void ResetRuntimeModeDefault()
        {
            RuntimeMode = DefaultRuntimeMode;
        }

        internal static void ClearRuntimeModeForTests()
        {
            EditorPrefs.DeleteKey(RuntimeModeKey);
        }

        private static void DrawRuntimeSettings()
        {
            GUILayout.Label(new GUIContent("Runtime", "Selects Legacy HTTP/MCP or optional Unity Pipeline."), EditorStyles.boldLabel);
            RuntimeMode = (NexusRuntimeMode)EditorGUILayout.EnumPopup(
                new GUIContent("Mode", "auto prefers Pipeline on eligible installs and Legacy otherwise. legacy forces HTTP/MCP. pipeline requires a healthy Unity Pipeline server."),
                RuntimeMode);
            GUILayout.Label(
                new GUIContent(
                    "Effective runtime: " + NexusRuntimeSelector.ToWireName(NexusRuntimeHost.EffectiveMode) +
                    " (requested " + NexusRuntimeSelector.ToWireName(RuntimeMode) + "). Set Mode to Legacy to force HTTP immediately. Legacy HTTP remains fully supported; removal is not scheduled.",
                    NexusLegacyDeprecation.RemovalPrerequisite),
                EditorStyles.helpBox);
        }
    }
}
