using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        internal static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_typeCache.TryGetValue(name, out var cachedType)) return cachedType;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            // Priority 1: Check Assembly-CSharp
            var mainAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (mainAsm != null)
            {
                var t = mainAsm.GetType(name) ?? mainAsm.GetTypes().FirstOrDefault(x => x.Name == name);
                if (t != null) return _typeCache[name] = t;
            }

            // Priority 2: Check all other assemblies
            foreach (var a in assemblies)
            {
                try 
                {
                    var t = a.GetType(name) ?? a.GetTypes().FirstOrDefault(x => x.Name == name);
                    if (t != null) return _typeCache[name] = t;
                }
                catch { }
            }

            _typeCache[name] = null;
            return null;
        }

        private static JToken InvokeMethod(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["method_name"] == null) 
                throw new Exception("instance_id and method_name required");

            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("GameObject not found");

            object target = go;
            string compName = p["component_name"]?.ToString();
            if (!string.IsNullOrEmpty(compName))
            {
                var comp = go.GetComponent(compName);
                if (comp == null) throw new Exception($"Component '{compName}' not found on object");
                target = comp;
            }

            string methodName = p["method_name"].ToString();
            var argsArray = p["arguments"] as JArray;
            int argCount = argsArray != null ? argsArray.Count : 0;

            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
                .ToList();

            if (methods.Count == 0)
                throw new Exception($"Method '{methodName}' with {argCount} parameters not found on '{target.GetType().Name}'");

            if (methods.Count > 1)
                throw new Exception($"Ambiguous method call. Found multiple '{methodName}' with {argCount} parameters.");

            var method = methods[0];
            var parameters = method.GetParameters();
            object[] invokeArgs = new object[argCount];

            for (int i = 0; i < argCount; i++)
            {
                var expectedType = parameters[i].ParameterType;
                var jsonValue = argsArray[i];

                try
                {
                    if (jsonValue.Type == JTokenType.Null)
                    {
                        invokeArgs[i] = null;
                    }
                    else if (expectedType == typeof(int)) invokeArgs[i] = (int)jsonValue;
                    else if (expectedType == typeof(float)) invokeArgs[i] = (float)jsonValue;
                    else if (expectedType == typeof(double)) invokeArgs[i] = (double)jsonValue;
                    else if (expectedType == typeof(bool)) invokeArgs[i] = (bool)jsonValue;
                    else if (expectedType == typeof(string)) invokeArgs[i] = (string)jsonValue;
                    else
                    {
                        invokeArgs[i] = jsonValue.ToObject(expectedType);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to convert argument {i} to type '{expectedType.Name}': {ex.Message}");
                }
            }

            object result = null;
            try
            {
                result = method.Invoke(target, invokeArgs);
            }
            catch (Exception ex)
            {
                throw new Exception($"Method invocation failed: {ex.InnerException?.Message ?? ex.Message}");
            }

            if (method.ReturnType == typeof(void))
                return new JObject { ["status"] = "Success" };

            if (result == null) return new JObject { ["status"] = "Success", ["result"] = JValue.CreateNull() };

            JToken resultToken;
            if (result is int || result is float || result is bool || result is string || result is double)
                resultToken = JToken.FromObject(result);
            else if (result is Vector3 v3) resultToken = new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
            else if (result is Vector2 v2) resultToken = new JObject { ["x"] = v2.x, ["y"] = v2.y };
            else
            {
                try { resultToken = JToken.FromObject(result); }
                catch { resultToken = result.ToString(); }
            }

            if (resultToken is JObject jo)
            {
                jo["status"] = "Success";
                return jo;
            }

            return new JObject { ["status"] = "Success", ["result"] = resultToken };
        }

        private static Vector3 ParseVector3(JToken t, Vector3 _defaultValue = default)
        {
            if (t == null) return _defaultValue;
            return new Vector3((float)(t["x"] ?? _defaultValue.x), (float)(t["y"] ?? _defaultValue.y), (float)(t["z"] ?? _defaultValue.z));
        }

        private static JToken SerializeGameObject(GameObject go)
        {
            if (go == null) return JValue.CreateNull();
            return new JObject { ["name"] = go.name, ["instance_id"] = go.GetRawId() };
        }
    }
}