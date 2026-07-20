using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();
        private static ConcurrentDictionary<Type, string> _typeNameCache = new ConcurrentDictionary<Type, string>();
        private static List<SymbolInfo> _symbolIndex = new List<SymbolInfo>();
        private static bool _isIndexing = false;
        private static object _indexLock = new object();

        /// <summary>
        /// Identifies the kind of reflected C# symbol returned by the Unity editor symbol index.
        /// </summary>
        /// <remarks>
        /// Class entries describe compiled types, Method entries describe invocable members exposed for editor inspection,
        /// and Field entries describe reflected fields discovered while scanning Unity assemblies and project scripts.
        /// </remarks>
        public enum SymbolType { Class, Method, Field }

        /// <summary>
        /// Carries reflected C# symbol metadata used by Nexus Unity search and method invocation tools.
        /// </summary>
        /// <remarks>
        /// Values are populated from Unity's loaded assemblies on a background indexing task. Metadata stores serialized
        /// reflection details used by editor search results and JSON-RPC method invocation, so consumers should treat it
        /// as protocol data rather than user-authored text.
        /// </remarks>
        public struct SymbolInfo
        {
            public string Name;
            public string Namespace;
            public string DeclaringType;
            public SymbolType Type;
            public string Metadata;
        }

        private static void RegisterReflectionMethods()
        {
            _methods["symbol_index"] = SymbolIndex;
            _methods["invoke_method"] = InvokeMethod;
            
            // Trigger initial scan
            StartIndexing();
        }

        private static void AddReflectionTools(JArray tools)
        {
            tools.Add(CreateTool("symbol_index", "Index and search all compiled symbols (Classes, Methods, Fields)", new JObject
            {
                ["query"] = new JObject { ["type"] = "string", ["description"] = "Fuzzy or Regex search query" },
                ["type_filter"] = new JObject { 
                    ["type"] = "string", 
                    ["enum"] = new JArray("Class", "Method", "Field"),
                    ["description"] = "Optional: Filter by symbol type"
                }
            }));
            
            tools.Add(CreateTool("invoke_method", "Invoke a C# method on a component", new JObject
            {
                ["instance_id"] = new JObject { ["type"] = "integer" },
                ["component_name"] = new JObject { ["type"] = "string" },
                ["method_name"] = new JObject { ["type"] = "string" },
                ["arguments"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "any" } }
            }, "instance_id", "method_name"));
        }

        private static void StartIndexing()
        {
            lock (_indexLock)
            {
                if (_isIndexing) return;
                _isIndexing = true;
            }

            ThreadPool.QueueUserWorkItem(_ => {
                try
                {
                    var newIndex = BuildSymbolIndex();
                    lock (_indexLock)
                    {
                        _symbolIndex = newIndex;
                        _isIndexing = false;
                    }
                }
                catch (Exception)
                {
                    lock (_indexLock) { _isIndexing = false; }
                }
            });
        }

        private static List<SymbolInfo> BuildSymbolIndex()
        {
            var newIndex = new List<SymbolInfo>(100000);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                AddAssemblySymbols(assembly, newIndex);
            }
            return newIndex;
        }

        private static void AddAssemblySymbols(Assembly assembly, List<SymbolInfo> index)
        {
            string asmName = assembly.GetName().Name;
            if (asmName.StartsWith("System") || asmName.StartsWith("Microsoft") || asmName.StartsWith("mscorlib") || asmName.StartsWith("netstandard"))
                return;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { return; }

            foreach (var type in types)
            {
                if (type == null) continue;

                index.Add(new SymbolInfo
                {
                    Name = type.Name,
                    Namespace = type.Namespace,
                    DeclaringType = type.FullName,
                    Type = SymbolType.Class,
                    Metadata = type.BaseType?.Name
                });

                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (var method in type.GetMethods(flags))
                {
                    if (method.IsSpecialName) continue;
                    
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    index.Add(new SymbolInfo
                    {
                        Name = method.Name,
                        Namespace = type.Namespace,
                        DeclaringType = type.FullName,
                        Type = SymbolType.Method,
                        Metadata = $"({parameters})"
                    });
                }

                foreach (var field in type.GetFields(flags))
                {
                    index.Add(new SymbolInfo
                    {
                        Name = field.Name,
                        Namespace = type.Namespace,
                        DeclaringType = type.FullName,
                        Type = SymbolType.Field,
                        Metadata = field.FieldType.Name
                    });
                }
            }
        }

        private static JToken SymbolIndex(JToken p)
        {
            string query = p?["query"]?.ToString();
            string typeFilterStr = p?["type_filter"]?.ToString();
            
            SymbolType? typeFilter = null;
            if (!string.IsNullOrEmpty(typeFilterStr))
            {
                if (Enum.TryParse<SymbolType>(typeFilterStr, true, out var result))
                    typeFilter = result;
            }

            List<SymbolInfo> currentSnapshot;
            lock (_indexLock)
            {
                currentSnapshot = _symbolIndex;
            }

            if (currentSnapshot == null || currentSnapshot.Count == 0)
            {
                return new JObject { 
                    ["status"] = "Success", 
                    ["symbols"] = new JArray(),
                    ["message"] = _isIndexing ? "Index is being built..." : "Index is empty"
                };
            }

            IEnumerable<SymbolInfo> results = currentSnapshot;

            if (typeFilter.HasValue)
                results = results.Where(s => s.Type == typeFilter.Value);

            if (!string.IsNullOrEmpty(query))
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(query, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    results = results.Where(s => regex.IsMatch(s.Name) || (s.Namespace != null && regex.IsMatch(s.Namespace)));
                }
                catch (ArgumentException)
                {
                    // Fallback to simple contains if regex is invalid
                    results = results.Where(s => s.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || 
                                               (s.Namespace != null && s.Namespace.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
                }
            }

            var resultList = results.Take(500).Select(s => new JObject {
                ["name"] = s.Name,
                ["namespace"] = s.Namespace,
                ["declaring_type"] = s.DeclaringType,
                ["type"] = s.Type.ToString(),
                ["metadata"] = s.Metadata
            }).ToList();

            return new JObject { 
                ["status"] = "Success", 
                ["symbols"] = new JArray(resultList),
                ["count"] = resultList.Count,
                ["is_indexing"] = _isIndexing
            };
        }

        internal static string GetTypeName(Type type)
        {
            if (type == null) return "Unknown";
            if (_typeNameCache.TryGetValue(type, out var cachedName)) return cachedName;

            string name = type.Name;
            _typeNameCache[type] = name;
            return name;
        }

        private static JToken InvokeMethod(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["method_name"] == null) 
                throw new Exception("instance_id and method_name required");

            object target = ResolveTarget(p);
            string methodName = p["method_name"].ToString();
            var argsArray = p["arguments"] as JArray ?? new JArray();
            
            var method = ResolveMethod(target, methodName, argsArray);
            var invokeArgs = PrepareArguments(method, argsArray);

            try
            {
                object result = method.Invoke(target, invokeArgs);
                return FormatResult(method, result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Method invocation failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private static object ResolveTarget(JToken p)
        {
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (go == null) throw new Exception("GameObject not found");

            string compName = p["component_name"]?.ToString();
            if (!string.IsNullOrEmpty(compName))
            {
                var comp = go.GetComponent(compName);
                if (comp == null) throw new Exception($"Component '{compName}' not found on object");
                return comp;
            }
            return go;
        }

        private static MethodInfo ResolveMethod(object target, string methodName, JArray argsArray)
        {
            int argCount = argsArray.Count;
            var candidates = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
                .ToList();

            if (candidates.Count == 0)
                throw new Exception($"Method '{methodName}' with {argCount} parameters not found on '{target.GetType().Name}'");

            if (candidates.Count > 1)
            {
                // Try to narrow down by type matching
                var filtered = candidates.Where(m => CanMapArguments(m.GetParameters(), argsArray)).ToList();
                if (filtered.Count == 1) return filtered[0];
                if (filtered.Count > 1) candidates = filtered;

                throw new Exception($"Ambiguous method call. Found multiple '{methodName}' with {argCount} parameters.");
            }

            return candidates[0];
        }

        private static bool CanMapArguments(ParameterInfo[] parameters, JArray argsArray)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var jsonValue = argsArray[i];

                if (jsonValue.Type == JTokenType.Null)
                {
                    if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null) return false;
                    continue;
                }

                switch (jsonValue.Type)
                {
                    case JTokenType.Integer:
                        if (paramType != typeof(int) && paramType != typeof(long) && paramType != typeof(float) && paramType != typeof(double) && paramType != typeof(short) && paramType != typeof(byte)) return false;
                        break;
                    case JTokenType.Float:
                        if (paramType != typeof(float) && paramType != typeof(double)) return false;
                        break;
                    case JTokenType.Boolean:
                        if (paramType != typeof(bool)) return false;
                        break;
                    case JTokenType.String:
                        if (paramType != typeof(string) && !paramType.IsEnum) return false;
                        break;
                }
            }
            return true;
        }

        private static object[] PrepareArguments(MethodInfo method, JArray argsArray)
        {
            var parameters = method.GetParameters();
            int argCount = argsArray.Count;
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
            return invokeArgs;
        }

        private static JToken FormatResult(MethodInfo method, object result)
        {
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
                if (jo["status"] == null) jo["status"] = "Success";
                return jo;
            }

            return new JObject { ["status"] = "Success", ["result"] = resultToken };
        }

        private static Vector3 ParseVector3(JToken t, Vector3 _defaultValue = default)
        {
            if (t == null) return _defaultValue;
            if (t is JArray array)
            {
                if (array.Count != 3) throw new Exception("Vector3 array must have exactly 3 numbers");
                return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
            }
            if (t.Type != JTokenType.Object) throw new Exception("Vector3 must be an object with x/y/z or an array [x,y,z]");
            return new Vector3((float)(t["x"] ?? _defaultValue.x), (float)(t["y"] ?? _defaultValue.y), (float)(t["z"] ?? _defaultValue.z));
        }

        private static JToken SerializeGameObject(GameObject go)
        {
            if (go == null) return JValue.CreateNull();
            return new JObject { ["name"] = go.name, ["instance_id"] = go.GetRawId(),
                ["transform"] = new JObject { ["position"] = SerializeVector3(go.transform.position), ["rotation"] = SerializeVector3(go.transform.eulerAngles), ["scale"] = SerializeVector3(go.transform.localScale) },
                ["components"] = new JArray(go.GetComponents<Component>().Where(c => c != null).Select(c => SerializeComponentSnapshot(c, false))) };
        }
    }
}
