using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers scene delta tracking methods and listens to Unity editor object-change events for incremental hierarchy updates.
    /// </summary>
    /// <remarks>
    /// Registration subscribes to <see cref="ObjectChangeEvents.changesPublished"/> and stores GameObject creation, destruction,
    /// reparenting, and property changes in a lock-protected buffer. Results include Nexus session generation data so clients can
    /// detect stale cursors or buffer overruns between editor changes.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        private static long _deltaCounter = 0;
        private static readonly List<SceneChangeEvent> _deltaBuffer = new List<SceneChangeEvent>();
        private const int MAX_DELTA_BUFFER = 2000;

        [Serializable]
        private class SceneChangeEvent
        {
            public long id;
            public string type;
            public int instanceId;
            public string name;
            public string propertyPath;
            public int parentId;
            public string timestamp;
        }

        private static void RegisterDeltaMethods()
        {
            _methods["scene_delta"] = SceneDelta;

            // Register for changes
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void AddDeltaTools(JArray tools)
        {
            tools.Add(CreateTool("scene_delta", "Returns a list of scene changes (created, destroyed, reparented, property changes) since a specific generation", new JObject
            {
                ["since_generation"] = new JObject { ["type"] = "integer", ["description"] = "Optional: Return changes since this generation ID" }
            }));
        }

        private static JToken SceneDelta(JToken p)
        {
            long sinceGeneration = p?["since_generation"]?.Value<long>() ?? 0;
            List<SceneChangeEvent> changes;
            long currentGeneration;
            bool overrun = false;

            lock (_deltaBuffer)
            {
                currentGeneration = _deltaCounter;
                if (_deltaBuffer.Count > 0 && sinceGeneration != 0 && _deltaBuffer[0].id > sinceGeneration + 1)
                {
                    overrun = true;
                }
                changes = _deltaBuffer.Where(c => c.id > sinceGeneration).ToList();
            }

            return new JObject
            {
                ["changes"] = JArray.FromObject(changes),
                ["current_generation"] = currentGeneration,
                ["session_generation"] = MCPServer.SessionGeneration,
                ["buffer_overrun"] = overrun
            };
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            string timestamp = DateTime.UtcNow.ToString("o");
            lock (_deltaBuffer)
            {
                for (int i = 0; i < stream.length; i++)
                {
                    SceneChangeEvent evt = CreateSceneChangeEvent(ref stream, i, timestamp);
                    if (evt != null)
                        _deltaBuffer.Add(evt);
                }
                TrimDeltaBuffer();
            }
        }
    }
}
