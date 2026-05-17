using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Manages settings for the MCP Server, exposed via Project Settings.
    /// </summary>
    public static class MCPSettings
    {
        private const string _PORT_KEY = "UnityMCP_Server_Port";
        private const int _DEFAULT_PORT = 8081;

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
                    GUILayout.Label(new GUIContent("Server Configuration", "Local port and server settings for Nexus Unity"), EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();

                    int newPort = EditorGUILayout.IntField(new GUIContent("Server Port", "The port the MCP server listens on."), Port);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Port = newPort;
                    }

                    GUILayout.Space(10);
                    GUILayout.Label(new GUIContent("Changes to port require server restart.", "Restart the server from the Nexus Unity control panel after changing this setting"), EditorStyles.helpBox);
                },

                keywords = new System.Collections.Generic.HashSet<string>(new[] { "MCP", "Server", "Port", "AI" })
            };

            return provider;
        }
    }
}
