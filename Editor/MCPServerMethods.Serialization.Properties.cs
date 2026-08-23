using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken SerializeProperty(SerializedProperty prop, bool detailed = false)
        {
            JToken value = JValue.CreateNull();
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: value = prop.intValue; break;
                case SerializedPropertyType.Boolean: value = prop.boolValue; break;
                case SerializedPropertyType.Float: value = prop.floatValue; break;
                case SerializedPropertyType.String: value = prop.stringValue; break;
                case SerializedPropertyType.Color: value = "#" + ColorUtility.ToHtmlStringRGBA(prop.colorValue); break;
                case SerializedPropertyType.Enum: value = prop.enumNames.Length > 0 && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString(); break;
                case SerializedPropertyType.Vector2: value = new JObject { ["x"] = prop.vector2Value.x, ["y"] = prop.vector2Value.y }; break;
                case SerializedPropertyType.Vector3: value = new JObject { ["x"] = prop.vector3Value.x, ["y"] = prop.vector3Value.y, ["z"] = prop.vector3Value.z }; break;
                case SerializedPropertyType.Vector4: value = new JObject { ["x"] = prop.vector4Value.x, ["y"] = prop.vector4Value.y, ["z"] = prop.vector4Value.z, ["w"] = prop.vector4Value.w }; break;
                case SerializedPropertyType.Quaternion: value = new JObject { ["x"] = prop.quaternionValue.x, ["y"] = prop.quaternionValue.y, ["z"] = prop.quaternionValue.z, ["w"] = prop.quaternionValue.w }; break;
                case SerializedPropertyType.Rect: value = new JObject { ["x"] = prop.rectValue.x, ["y"] = prop.rectValue.y, ["width"] = prop.rectValue.width, ["height"] = prop.rectValue.height }; break;
                case SerializedPropertyType.Bounds: value = new JObject { ["center"] = new JObject { ["x"] = prop.boundsValue.center.x, ["y"] = prop.boundsValue.center.y, ["z"] = prop.boundsValue.center.z }, ["size"] = new JObject { ["x"] = prop.boundsValue.size.x, ["y"] = prop.boundsValue.size.y, ["z"] = prop.boundsValue.size.z } }; break;
                case SerializedPropertyType.ObjectReference:
                    var obj = prop.objectReferenceValue;
                    if (obj != null) value = new JObject { ["instance_id"] = obj.GetRawId(), ["name"] = obj.name, ["type"] = obj.GetType().Name };
                    break;
                case SerializedPropertyType.ManagedReference:
                    value = SerializeManagedReference(prop, detailed);
                    break;
                default: value = SerializeComplexProperty(prop, detailed); break;
            }

            if (detailed)
            {
                return new JObject
                {
                    ["value"] = value,
                    ["type"] = prop.type,
                    ["propertyType"] = prop.propertyType.ToString(),
                    ["displayName"] = prop.displayName,
                    ["tooltip"] = prop.tooltip
                };
            }
            return value;
        }

        private static JToken SerializeManagedReference(SerializedProperty prop, bool detailed)
        {
            if (prop.managedReferenceValue == null) return JValue.CreateNull();

            JObject result = new JObject();
            result["_type"] = prop.managedReferenceFullTypename;

            SerializedProperty child = prop.Copy();
            SerializedProperty end = prop.GetEndProperty();
            if (child.Next(true))
            {
                while (!SerializedProperty.EqualContents(child, end))
                {
                    result[child.name] = SerializeProperty(child, detailed);
                    if (!child.Next(false)) break;
                }
            }
            return result;
        }

        private static JToken SerializeComplexProperty(SerializedProperty prop, bool detailed)
        {
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                JArray arr = new JArray();
                for (int i = 0; i < prop.arraySize; i++)
                {
                    arr.Add(SerializeProperty(prop.GetArrayElementAtIndex(i), detailed));
                }
                return arr;
            }

            if (prop.propertyType == SerializedPropertyType.Generic)
            {
                JObject dict = new JObject();
                SerializedProperty childProp = prop.Copy();
                SerializedProperty endProp = childProp.GetEndProperty();
                bool enter = true;
                while (childProp.Next(enter) && !SerializedProperty.EqualContents(childProp, endProp))
                {
                    enter = false;
                    dict[childProp.name] = SerializeProperty(childProp, detailed);
                }
                return dict;
            }

            return prop.propertyType.ToString();
        }
    }
}
