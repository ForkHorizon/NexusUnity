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
            EditorReadiness.Snapshot snap = EditorReadiness.Published;
            bool blocked = EditorReadiness.ImportBlocked(snap);
            return new JObject
            {
                ["is_idle"] = !blocked,
                ["is_updating"] = blocked
            };
        }

        private static JToken IsEditorIdle(JToken p)
        {
            return EditorReadiness.BuildEditorIdle(EditorReadiness.Published);
        }

        private static JToken WaitForAssetImportIdle(JToken p)
        {
            double timeoutSeconds = p?["timeout_seconds"]?.Value<double>() ?? 60;
            DateTime started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalSeconds < timeoutSeconds)
            {
                EditorReadiness.Snapshot snap = EditorReadiness.Published;
                if (snap.Reconnect)
                    return EditorReadiness.BuildWaitPayload(snap, (DateTime.UtcNow - started).TotalSeconds, false);
                if (!EditorReadiness.ImportBlocked(snap))
                {
                    JObject payload = EditorReadiness.ToJson(snap);
                    payload["status"] = "Ready";
                    payload["time_waited_seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 2);
                    return payload;
                }
                System.Threading.Thread.Sleep(50);
            }

            return EditorReadiness.BuildWaitPayload(EditorReadiness.Published, (DateTime.UtcNow - started).TotalSeconds, true);
        }

        private static JToken WaitForEditorIdle(JToken p)
        {
            double timeoutSeconds = p?["timeout_seconds"]?.Value<double>() ?? 120;
            DateTime started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalSeconds < timeoutSeconds)
            {
                EditorReadiness.Snapshot snap = EditorReadiness.Published;
                // A single HTTP call cannot survive domain reload. Return so the bridge can re-poll.
                if (snap.Ready || snap.Reconnect)
                    return EditorReadiness.BuildWaitPayload(snap, (DateTime.UtcNow - started).TotalSeconds, false);
                System.Threading.Thread.Sleep(50);
            }

            return EditorReadiness.BuildWaitPayload(EditorReadiness.Published, (DateTime.UtcNow - started).TotalSeconds, true);
        }
    }
}
