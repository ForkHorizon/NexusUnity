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
                    var eventType = stream.GetEventType(i);
                    SceneChangeEvent evt = null;

                    switch (eventType)
                    {
                        case ObjectChangeKind.CreateGameObjectHierarchy:
                            stream.GetCreateGameObjectHierarchyEvent(i, out var createEvt);
#if UNITY_6000_4_OR_NEWER
                            var go = EditorUtility.EntityIdToObject(createEvt.entityId) as GameObject;
                            int createdId = ConvertEntityIdToLegacyInt(createEvt.entityId);
#else
                            var go = IdToObject(ConvertLegacyIntToEntityId(createEvt.instanceId)) as GameObject;
                            int createdId = createEvt.instanceId;
#endif
                            evt = new SceneChangeEvent
                            {
                                id = ++_deltaCounter,
                                type = "CreateGameObject",
                                instanceId = createdId,
                                name = go != null ? go.name : "Unknown",
                                timestamp = timestamp
                            };
                            break;

                        case ObjectChangeKind.DestroyGameObjectHierarchy:
                            stream.GetDestroyGameObjectHierarchyEvent(i, out var destroyEvt);
#if UNITY_6000_4_OR_NEWER
                            int destroyedId = ConvertEntityIdToLegacyInt(destroyEvt.entityId);
#else
                            int destroyedId = destroyEvt.instanceId;
#endif
                            evt = new SceneChangeEvent
                            {
                                id = ++_deltaCounter,
                                type = "DestroyGameObject",
                                instanceId = destroyedId,
                                name = "DestroyedObject", // Name not available in event args
                                timestamp = timestamp
                            };
                            break;

                        case ObjectChangeKind.ChangeGameObjectParent:
                            stream.GetChangeGameObjectParentEvent(i, out var parentEvt);
#if UNITY_6000_4_OR_NEWER
                            var movedGo = EditorUtility.EntityIdToObject(parentEvt.entityId) as GameObject;
                            int movedId = ConvertEntityIdToLegacyInt(parentEvt.entityId);
                            int newParentId = ConvertEntityIdToLegacyInt(parentEvt.newParentEntityId);
#else
                            var movedGo = IdToObject(ConvertLegacyIntToEntityId(parentEvt.instanceId)) as GameObject;
                            int movedId = parentEvt.instanceId;
                            int newParentId = parentEvt.newParentInstanceId;
#endif
                            evt = new SceneChangeEvent
                            {
                                id = ++_deltaCounter,
                                type = "ReparentGameObject",
                                instanceId = movedId,
                                parentId = newParentId,
                                name = movedGo != null ? movedGo.name : "Unknown",
                                timestamp = timestamp
                            };
                            break;

                        case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var changePropEvt);
#if UNITY_6000_4_OR_NEWER
                            var changedObj = EditorUtility.EntityIdToObject(changePropEvt.entityId);
                            int changedId = ConvertEntityIdToLegacyInt(changePropEvt.entityId);
#else
                            var changedObj = IdToObject(ConvertLegacyIntToEntityId(changePropEvt.instanceId));
                            int changedId = changePropEvt.instanceId;
#endif
                            evt = new SceneChangeEvent
                            {
                                id = ++_deltaCounter,
                                type = "PropertiesChanged",
                                instanceId = changedId,
                                name = changedObj != null ? changedObj.name : "Unknown",
                                timestamp = timestamp
                            };
                            break;

                        case ObjectChangeKind.ChangeChildrenOrder:
                            stream.GetChangeChildrenOrderEvent(i, out var orderEvt);
#if UNITY_6000_4_OR_NEWER
                            var parentObj = EditorUtility.EntityIdToObject(orderEvt.entityId);
                            int orderId = ConvertEntityIdToLegacyInt(orderEvt.entityId);
#else
                            var parentObj = IdToObject(ConvertLegacyIntToEntityId(orderEvt.instanceId));
                            int orderId = orderEvt.instanceId;
#endif
                            evt = new SceneChangeEvent
                            {
                                id = ++_deltaCounter,
                                type = "ChildrenOrderChanged",
                                instanceId = orderId,
                                name = parentObj != null ? parentObj.name : "Unknown",
                                timestamp = timestamp
                            };
                            break;
                    }

                    if (evt != null)
                    {
                        _deltaBuffer.Add(evt);
                    }
                }

                // Prune buffer
                if (_deltaBuffer.Count > MAX_DELTA_BUFFER)
                {
                    _deltaBuffer.RemoveRange(0, _deltaBuffer.Count - MAX_DELTA_BUFFER);
                }
            }
        }
    }
}
