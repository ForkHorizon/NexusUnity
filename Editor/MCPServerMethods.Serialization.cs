using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityMCP.Runtime;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation handling serialization fixes like ForceDefault enforcement.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static void RegisterSerializationMethods()
        {
            _methods["enforce_forced_defaults"] = EnforceForcedDefaults;
        }

        private static JToken EnforceForcedDefaults(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            if (go == null) throw new Exception("Object not found");

            int count = 0;
            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                count += EnforceOnComponent(comp);
            }

            return $"Enforced {count} default values on {go.name}";
        }

        private static int EnforceOnComponent(MonoBehaviour comp)
        {
            int enforcedCount = 0;
            var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            SerializedObject so = new SerializedObject(comp);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<ForceDefaultAttribute>();
                if (attr == null) continue;

                var prop = so.FindProperty(field.Name);
                if (prop == null) continue;

                Undo.RecordObject(comp, "Enforce Default");
                
                // If the attribute has a specific value, use it. 
                // Otherwise, try to get the current value from a fresh instance of the script?
                // Actually, the attribute usually carries the "Correct" value.
                
                if (attr.DefaultValue != null)
                {
                    ApplyValueToProperty(prop, attr.DefaultValue);
                    enforcedCount++;
                }
            }

            so.ApplyModifiedProperties();
            return enforcedCount;
        }

        private static void ApplyValueToProperty(SerializedProperty prop, object value)
        {
            if (value is bool b) prop.boolValue = b;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is string s) prop.stringValue = s;
            else if (value is Vector3 v) prop.vector3Value = v;
            // Add more types as needed
        }
    }
}
