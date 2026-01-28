using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Manages settings for the MCP Server, exposed via Project Settings.
    /// </summary>
    public static class MCPSettings
    {
        private const string PORT_KEY = "UnityMCP_Server_Port";
        private const int DEFAULT_PORT = 8081;

        /// <summary>
        /// The local server port for the MCP Server.
        /// </summary>
        public static int Port
        {
            get => EditorPrefs.GetInt(PORT_KEY, DEFAULT_PORT);
            set => EditorPrefs.SetInt(PORT_KEY, value);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/Unity MCP Server", SettingsScope.Project)
            {
                label = "Unity MCP Server",
                guiHandler = (searchContext) =>
                {
                    EditorGUILayout.Space();
                    GUILayout.Label("Server Configuration", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();

                    int newPort = EditorGUILayout.IntField(new GUIContent("Server Port", "The port the MCP server listens on."), Port);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Port = newPort;
                    }

                    GUILayout.Space(10);
                    GUILayout.Label("Changes to port require server restart.", EditorStyles.helpBox);
                },

                keywords = new System.Collections.Generic.HashSet<string>(new[] { "MCP", "Server", "Port", "AI" })
            };

            return provider;
        }
    }
}
