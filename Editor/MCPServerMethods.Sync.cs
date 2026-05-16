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
            _methods["wait_for_asset_import_idle"] = WaitForAssetImportIdle;
            _methods["wait_for_editor_idle"] = WaitForEditorIdle;
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

        private static JToken WaitForAssetImportIdle(JToken p)
        {
            double timeoutSeconds = p?["timeout_seconds"]?.Value<double>() ?? 60;
            DateTime started = DateTime.UtcNow;

            while ((DateTime.UtcNow - started).TotalSeconds < timeoutSeconds)
            {
                if (!MCPServer.IsUpdatingCached)
                {
                    return new JObject
                    {
                        ["status"] = "Ready",
                        ["time_waited_seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 2)
                    };
                }

                System.Threading.Thread.Sleep(100);
            }

            return new JObject
            {
                ["status"] = "Timeout",
                ["time_waited_seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 2),
                ["is_updating"] = MCPServer.IsUpdatingCached
            };
        }

        private static JToken WaitForEditorIdle(JToken p)
        {
            double timeoutSeconds = p?["timeout_seconds"]?.Value<double>() ?? 120;
            DateTime started = DateTime.UtcNow;

            while ((DateTime.UtcNow - started).TotalSeconds < timeoutSeconds)
            {
                if (!MCPServer.IsCompilingCached && !MCPServer.IsUpdatingCached)
                {
                    return new JObject
                    {
                        ["status"] = "Ready",
                        ["time_waited_seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 2)
                    };
                }

                System.Threading.Thread.Sleep(100);
            }

            return new JObject
            {
                ["status"] = "Timeout",
                ["time_waited_seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 2),
                ["is_compiling"] = MCPServer.IsCompilingCached,
                ["is_updating"] = MCPServer.IsUpdatingCached
            };
        }
    }
}
