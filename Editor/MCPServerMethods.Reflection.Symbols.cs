using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
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
                AddTypeSymbols(type, index);
            }
        }

        private static void AddTypeSymbols(Type type, List<SymbolInfo> index)
        {
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

        private static JToken SymbolIndex(JToken p)
        {
            string query = p?["query"]?.ToString();
            string typeFilterStr = p?["type_filter"]?.ToString();
            SymbolType? typeFilter = ParseSymbolTypeFilter(typeFilterStr);

            List<SymbolInfo> currentSnapshot;
            lock (_indexLock) { currentSnapshot = _symbolIndex; }

            if (currentSnapshot == null || currentSnapshot.Count == 0)
            {
                return new JObject {
                    ["status"] = "Success",
                    ["symbols"] = new JArray(),
                    ["message"] = _isIndexing ? "Index is being built..." : "Index is empty"
                };
            }

            IEnumerable<SymbolInfo> results = currentSnapshot;
            if (typeFilter.HasValue) results = results.Where(s => s.Type == typeFilter.Value);
            if (!string.IsNullOrEmpty(query)) results = FilterSymbolsByQuery(results, query);

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

        private static SymbolType? ParseSymbolTypeFilter(string filter)
        {
            if (!string.IsNullOrEmpty(filter) && Enum.TryParse<SymbolType>(filter, true, out var result))
            {
                return result;
            }
            return null;
        }

        private static IEnumerable<SymbolInfo> FilterSymbolsByQuery(IEnumerable<SymbolInfo> source, string query)
        {
            try
            {
                var regex = new Regex(query, RegexOptions.IgnoreCase);
                return source.Where(s => regex.IsMatch(s.Name) || (s.Namespace != null && regex.IsMatch(s.Namespace)));
            }
            catch (ArgumentException)
            {
                return source.Where(s => s.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       (s.Namespace != null && s.Namespace.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            }
        }
    }
}
