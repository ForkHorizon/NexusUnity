using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tracks a timeline of recent Editor actions and state changes.
    /// Persists across domain reloads using SessionState.
    /// </summary>
    public static partial class MCPServer
    {
        private const string TimelineKey = "MCP_EditorTimeline";
        private const int MaxTimelineEvents = 50;

        /// <summary>
        /// Represents a persisted Unity Editor timeline event exposed through diagnostics.
        /// </summary>
        [Serializable]
        public struct EditorEvent
        {
            public string timestamp;
            public string type;
            public string details;

            /// <summary>
            /// Creates a timeline event stamped with the current UTC time.
            /// </summary>
            public EditorEvent(string type, string details)
            {
                this.timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                this.type = type;
                this.details = details;
            }
        }

        internal static void InitTimeline()
        {
            // Only initialize once per domain reload
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;

            // Record domain reload completion
            RecordEvent("domain_reload", "Domain reload finished");
        }

        /// <summary>
        /// Records a Unity Editor lifecycle event and trims the persisted timeline buffer.
        /// </summary>
        public static void RecordEvent(string type, string details)
        {
            var timeline = GetTimelineInternal();
            timeline.Insert(0, new EditorEvent(type, details));

            if (timeline.Count > MaxTimelineEvents)
            {
                timeline.RemoveRange(MaxTimelineEvents, timeline.Count - MaxTimelineEvents);
            }

            SaveTimelineInternal(timeline);
        }

        /// <summary>
        /// Returns recent Unity Editor lifecycle events persisted across domain reloads.
        /// </summary>
        public static List<EditorEvent> GetTimeline()
        {
            return GetTimelineInternal();
        }

        private static List<EditorEvent> GetTimelineInternal()
        {
            string json = SessionState.GetString(TimelineKey, "[]");
            try
            {
                return JsonConvert.DeserializeObject<List<EditorEvent>>(json) ?? new List<EditorEvent>();
            }
            catch
            {
                return new List<EditorEvent>();
            }
        }

        private static void SaveTimelineInternal(List<EditorEvent> timeline)
        {
            string json = JsonConvert.SerializeObject(timeline);
            SessionState.SetString(TimelineKey, json);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            RecordEvent("play_mode", $"State changed to {state}");
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RecordEvent("scene_opened", $"Opened scene: {scene.name} (Mode: {mode})");
        }

        private static void OnSceneSaved(Scene scene)
        {
            RecordEvent("scene_saved", $"Saved scene: {scene.name}");
        }
    }

    /// <summary>
    /// Captures asset import events for the timeline.
    /// </summary>
    public class TimelineAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length > 0)
            {
                MCPServer.RecordEvent("asset_import", $"Imported {importedAssets.Length} assets: {string.Join(", ", importedAssets.Take(3))}{(importedAssets.Length > 3 ? "..." : "")}");
            }
            if (deletedAssets.Length > 0)
            {
                MCPServer.RecordEvent("asset_delete", $"Deleted {deletedAssets.Length} assets: {string.Join(", ", deletedAssets.Take(3))}{(deletedAssets.Length > 3 ? "..." : "")}");
            }
            if (movedAssets.Length > 0)
            {
                MCPServer.RecordEvent("asset_move", $"Moved {movedAssets.Length} assets");
            }
        }
    }
}
