using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterSyncMethods()
        {
            _methods["is_asset_import_idle"] = IsAssetImportIdle;
            _methods["is_editor_idle"] = IsEditorIdle;
        }

        private static JToken IsAssetImportIdle(JToken p)
        {
            bool isUpdating = EditorApplication.isUpdating;
            return new JObject 
            { 
                ["is_idle"] = !isUpdating,
                ["is_updating"] = isUpdating
            };
        }

        private static JToken IsEditorIdle(JToken p)
        {
            bool isCompiling = EditorApplication.isCompiling;
            bool isUpdating = EditorApplication.isUpdating;

            return new JObject
            {
                ["is_idle"] = !isCompiling && !isUpdating,
                ["is_compiling"] = isCompiling,
                ["is_updating"] = isUpdating
            };
        }
    }
}
