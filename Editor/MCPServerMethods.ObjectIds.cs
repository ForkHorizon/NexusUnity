using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static readonly System.Func<ulong, EntityId> ULongToEntityId = CreateULongToEntityId();
        private static readonly System.Func<int, EntityId> LegacyIntToEntityId = CreateLegacyIntToEntityId();
        private static readonly System.Func<EntityId, ulong> EntityIdToULong = CreateEntityIdToULong();
        private static readonly System.Func<EntityId, int> LegacyEntityIdToInt = CreateLegacyEntityIdToInt();

        internal static UnityEngine.Object IdToObject(EntityId id)
        {
            if (id == default) return null;
            return EditorUtility.EntityIdToObject(id);
        }

        internal static EntityId ExtractId(JToken p, string key = "instance_id")
        {
            if (p == null || p[key] == null) return default;
            return ExtractIdFromToken(p[key]);
        }

        internal static EntityId ExtractIdFromToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return default;

            try
            {
                string rawText = token.ToString();

                if (long.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedId))
                {
                    return LegacyIntToEntityId(unchecked((int)signedId));
                }

                if (ulong.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong rawId))
                {
                    return ULongToEntityId(rawId);
                }
            }
            catch
            {
                return default;
            }

            return default;
        }

        internal static int ConvertEntityIdToLegacyInt(EntityId id)
        {
            return LegacyEntityIdToInt(id);
        }

        internal static EntityId ConvertLegacyIntToEntityId(int id)
        {
            return id == 0 ? default : LegacyIntToEntityId(id);
        }

        internal static EntityId GetObjectReferenceId(SerializedProperty prop)
        {
            if (prop == null) return default;

#if UNITY_6000_4_OR_NEWER
            return prop.objectReferenceEntityIdValue;
#else
            return ConvertLegacyIntToEntityId(prop.objectReferenceInstanceIDValue);
#endif
        }

        private static System.Func<int, EntityId> CreateLegacyIntToEntityId()
        {
            var method = FindEntityIdFromIntOperator();

            if (method != null)
            {
                return (System.Func<int, EntityId>)System.Delegate.CreateDelegate(typeof(System.Func<int, EntityId>), method);
            }

            return value => ULongToEntityId(unchecked((ulong)(uint)value));
        }

        private static System.Func<EntityId, int> CreateLegacyEntityIdToInt()
        {
            var method = FindEntityIdToIntOperator();

            if (method != null)
            {
                return (System.Func<EntityId, int>)System.Delegate.CreateDelegate(typeof(System.Func<EntityId, int>), method);
            }

            return value => unchecked((int)EntityIdToULong(value));
        }

        private static System.Func<ulong, EntityId> CreateULongToEntityId()
        {
            var method = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "FromULong" || m.Name == "From") &&
                                     m.ReturnType == typeof(EntityId) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(ulong));

            if (method != null)
            {
                return (System.Func<ulong, EntityId>)System.Delegate.CreateDelegate(typeof(System.Func<ulong, EntityId>), method);
            }

            var intMethod = FindEntityIdFromIntOperator();
            if (intMethod == null) return _ => default;

            var intConverter = (System.Func<int, EntityId>)System.Delegate.CreateDelegate(typeof(System.Func<int, EntityId>), intMethod);
            return value => intConverter(unchecked((int)value));
        }

        private static System.Func<EntityId, ulong> CreateEntityIdToULong()
        {
            var staticMethod = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ToULong" &&
                                     m.ReturnType == typeof(ulong) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(EntityId));

            if (staticMethod != null)
            {
                return (System.Func<EntityId, ulong>)System.Delegate.CreateDelegate(typeof(System.Func<EntityId, ulong>), staticMethod);
            }

            var instanceMethod = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetRawData" && m.GetParameters().Length == 0);

            if (instanceMethod != null)
            {
                return value =>
                {
                    object raw = instanceMethod.Invoke(value, null);
                    return raw == null ? 0UL : System.Convert.ToUInt64(raw, CultureInfo.InvariantCulture);
                };
            }

            var intMethod = FindEntityIdToIntOperator();
            if (intMethod == null) return _ => 0UL;

            var intConverter = (System.Func<EntityId, int>)System.Delegate.CreateDelegate(typeof(System.Func<EntityId, int>), intMethod);
            return value => unchecked((uint)intConverter(value));
        }

        private static MethodInfo FindEntityIdFromIntOperator()
        {
            return typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                                     m.ReturnType == typeof(EntityId) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(int));
        }

        private static MethodInfo FindEntityIdToIntOperator()
        {
            return typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                                     m.ReturnType == typeof(int) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(EntityId));
        }
    }

    /// <summary>
    /// Converts Unity objects between current <see cref="EntityId"/> values and legacy integer IDs used by the Nexus JSON protocol.
    /// </summary>
    /// <remarks>
    /// These helpers call Unity's internal object identity APIs when an object is present. Null objects return empty/zero IDs,
    /// while non-null object behavior can differ by Unity version, editor/play mode, and object lifetime.
    /// </remarks>
    public static class UnityObjectIdExtensions
    {
        /// <summary>
        /// Returns the Unity <see cref="EntityId"/> for an object, or an empty entity ID when the object reference is null.
        /// </summary>
        /// <remarks>
        /// Non-null objects delegate to <see cref="UnityEngine.Object.GetEntityId"/>, so destroyed or unsupported Unity objects can follow
        /// Unity's version-specific identity behavior.
        /// </remarks>
        public static EntityId GetId(this UnityEngine.Object obj)
        {
            if (obj == null) return default;
            return obj.GetEntityId();
        }

        /// <summary>
        /// Returns the legacy integer-shaped object ID used by the current Nexus JSON protocol.
        /// </summary>
        /// <remarks>
        /// Non-null objects are resolved through <see cref="UnityEngine.Object.GetEntityId"/> and converted with
        /// <see cref="MCPServerMethods.ConvertEntityIdToLegacyInt"/>, preserving protocol compatibility with older clients.
        /// </remarks>
        public static int GetRawId(this UnityEngine.Object obj)
        {
            if (obj == null) return 0;
            return MCPServerMethods.ConvertEntityIdToLegacyInt(obj.GetEntityId());
        }
    }
}
