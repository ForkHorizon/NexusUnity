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
    /// Tracks recent Unity Editor lifecycle events for diagnostics and persists them across domain reloads with <see cref="SessionState"/>.
    /// </summary>
    /// <remarks>
    /// Initialization registers editor callbacks for play mode and scene events, and asset postprocessing records import,
    /// delete, and move activity. Timeline writes update Unity editor session state and are trimmed to a bounded buffer.
    /// </remarks>
    public static partial class MCPServer
    {
        private const string TimelineKey = "MCP_EditorTimeline";
        private const int MaxTimelineEvents = 50;

        /// <summary>
        /// Represents a Unity Editor lifecycle event persisted in the Nexus diagnostic timeline.
        /// </summary>
        /// <remarks>
        /// The timestamp is generated in UTC with millisecond precision when the event is constructed, then serialized into
        /// SessionState for diagnostic JSON-RPC responses across domain reloads in the same editor session.
        /// </remarks>
        [Serializable]
        public struct EditorEvent
        {
            public string timestamp;
            public string type;
            public string details;

            /// <summary>
            /// Creates a timeline event stamped with the current UTC time for editor diagnostic persistence.
            /// </summary>
            /// <param name="type">Short event category, such as play mode, scene open, scene save, or asset import.</param>
            /// <param name="details">Human-readable event details written to the serialized timeline buffer.</param>
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
        /// Inserts a Unity Editor lifecycle event into the persisted SessionState timeline and trims the buffer to the latest entries.
        /// </summary>
        /// <remarks>
        /// This method reads and writes serialized JSON in Unity editor session state. It allocates a short list for trimming
        /// and should stay on editor lifecycle paths rather than high-frequency runtime loops.
        /// </remarks>
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
        /// Returns recent Unity Editor lifecycle events loaded from the SessionState-backed Nexus diagnostic timeline.
        /// </summary>
        /// <remarks>
        /// The returned list describes editor events such as play mode changes, scene open/save events, and asset pipeline changes
        /// retained for the current editor session across domain reloads.
        /// </remarks>
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
    /// Receives Unity asset pipeline postprocess callbacks and records asset imports, deletes, and moves into the Nexus editor timeline.
    /// </summary>
    /// <remarks>
    /// Unity invokes this <see cref="AssetPostprocessor"/> automatically during asset database refreshes, so it can write
    /// diagnostic timeline events while the editor is importing or moving project assets.
    /// </remarks>
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
