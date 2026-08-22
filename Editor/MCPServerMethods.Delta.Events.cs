using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static SceneChangeEvent CreateSceneChangeEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            switch (stream.GetEventType(index))
            {
                case ObjectChangeKind.CreateGameObjectHierarchy:
                    return CreateGameObjectEvent(ref stream, index, timestamp);
                case ObjectChangeKind.DestroyGameObjectHierarchy:
                    return CreateDestroyEvent(ref stream, index, timestamp);
                case ObjectChangeKind.ChangeGameObjectParent:
                    return CreateParentEvent(ref stream, index, timestamp);
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    return CreatePropertyChangeEvent(ref stream, index, timestamp);
                case ObjectChangeKind.ChangeChildrenOrder:
                    return CreateChildrenOrderEvent(ref stream, index, timestamp);
                default:
                    return null;
            }
        }

        private static SceneChangeEvent CreateGameObjectEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            stream.GetCreateGameObjectHierarchyEvent(index, out var change);
#if UNITY_6000_4_OR_NEWER
            var gameObject = EditorUtility.EntityIdToObject(change.entityId) as GameObject;
            int instanceId = ConvertEntityIdToLegacyInt(change.entityId);
#else
            var gameObject = IdToObject(ConvertLegacyIntToEntityId(change.instanceId)) as GameObject;
            int instanceId = change.instanceId;
#endif
            return new SceneChangeEvent
            {
                id = ++_deltaCounter,
                type = "CreateGameObject",
                instanceId = instanceId,
                name = gameObject != null ? gameObject.name : "Unknown",
                timestamp = timestamp
            };
        }

        private static SceneChangeEvent CreateDestroyEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            stream.GetDestroyGameObjectHierarchyEvent(index, out var change);
#if UNITY_6000_4_OR_NEWER
            int instanceId = ConvertEntityIdToLegacyInt(change.entityId);
#else
            int instanceId = change.instanceId;
#endif
            return new SceneChangeEvent
            {
                id = ++_deltaCounter,
                type = "DestroyGameObject",
                instanceId = instanceId,
                name = "DestroyedObject",
                timestamp = timestamp
            };
        }

        private static SceneChangeEvent CreateParentEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            stream.GetChangeGameObjectParentEvent(index, out var change);
#if UNITY_6000_4_OR_NEWER
            var gameObject = EditorUtility.EntityIdToObject(change.entityId) as GameObject;
            int instanceId = ConvertEntityIdToLegacyInt(change.entityId);
            int parentId = ConvertEntityIdToLegacyInt(change.newParentEntityId);
#else
            var gameObject = IdToObject(ConvertLegacyIntToEntityId(change.instanceId)) as GameObject;
            int instanceId = change.instanceId;
            int parentId = change.newParentInstanceId;
#endif
            return new SceneChangeEvent
            {
                id = ++_deltaCounter,
                type = "ReparentGameObject",
                instanceId = instanceId,
                parentId = parentId,
                name = gameObject != null ? gameObject.name : "Unknown",
                timestamp = timestamp
            };
        }

        private static SceneChangeEvent CreatePropertyChangeEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var change);
#if UNITY_6000_4_OR_NEWER
            var changedObject = EditorUtility.EntityIdToObject(change.entityId);
            int instanceId = ConvertEntityIdToLegacyInt(change.entityId);
#else
            var changedObject = IdToObject(ConvertLegacyIntToEntityId(change.instanceId));
            int instanceId = change.instanceId;
#endif
            return new SceneChangeEvent
            {
                id = ++_deltaCounter,
                type = "PropertiesChanged",
                instanceId = instanceId,
                name = changedObject != null ? changedObject.name : "Unknown",
                timestamp = timestamp
            };
        }

        private static SceneChangeEvent CreateChildrenOrderEvent(ref ObjectChangeEventStream stream, int index, string timestamp)
        {
            stream.GetChangeChildrenOrderEvent(index, out var change);
#if UNITY_6000_4_OR_NEWER
            var parentObject = EditorUtility.EntityIdToObject(change.entityId);
            int instanceId = ConvertEntityIdToLegacyInt(change.entityId);
#else
            var parentObject = IdToObject(ConvertLegacyIntToEntityId(change.instanceId));
            int instanceId = change.instanceId;
#endif
            return new SceneChangeEvent
            {
                id = ++_deltaCounter,
                type = "ChildrenOrderChanged",
                instanceId = instanceId,
                name = parentObject != null ? parentObject.name : "Unknown",
                timestamp = timestamp
            };
        }

        private static void TrimDeltaBuffer()
        {
            if (_deltaBuffer.Count > MAX_DELTA_BUFFER)
                _deltaBuffer.RemoveRange(0, _deltaBuffer.Count - MAX_DELTA_BUFFER);
        }
    }
}
