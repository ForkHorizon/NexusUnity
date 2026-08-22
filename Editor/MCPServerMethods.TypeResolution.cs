using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static readonly HashSet<string> DisallowedAssemblyPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "Microsoft",
            "Mono",
            "Newtonsoft",
            "nunit",
            "UnityEditorInternal",
            "UnityEngine.Internal",
            "UnityEditor.TestRunner",
            "Unity.PerformanceTesting",
            "mscorlib"
        };

        private static readonly HashSet<string> DisallowedNamespacePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "Microsoft",
            "Mono",
            "Newtonsoft",
            "UnityEditorInternal",
            "UnityEngine.Internal"
        };

        internal static bool IsAllowedAssembly(Assembly asm)
        {
            if (asm == null) return false;
            string asmName = asm.GetName().Name;
            if (string.IsNullOrEmpty(asmName)) return false;

            foreach (var prefix in DisallowedAssemblyPrefixes)
            {
                if (asmName.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsAllowedNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns)) return true;

            foreach (var prefix in DisallowedNamespacePrefixes)
            {
                if (ns.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    ns.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsAllowedType(Type type)
        {
            if (type == null) return false;
            if (!IsAllowedAssembly(type.Assembly)) return false;
            if (!IsAllowedNamespace(type.Namespace)) return false;
            if (type.IsPointer || type.IsByRef) return false;
            return true;
        }

        internal static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_typeCache.TryGetValue(name, out var cachedType)) return cachedType;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(IsAllowedAssembly).ToList();
            Type resultType = FindTypeInPriorityAssemblies(assemblies, name);

            if (resultType != null && IsAllowedType(resultType))
            {
                _typeCache[name] = resultType;
                return resultType;
            }

            return null;
        }

        private static Type FindTypeInPriorityAssemblies(List<Assembly> assemblies, string name)
        {
            Type result = FindTypeInNamedAssembly(assemblies, "Assembly-CSharp", name);
            if (result != null) return result;

            var coreAssembly = typeof(Component).Assembly;
            if (IsAllowedAssembly(coreAssembly))
            {
                result = GetTypeFromAssembly(coreAssembly, name);
                if (result != null) return result;
            }

            result = FindTypeInNamedAssembly(assemblies, "UnityEngine.UI", name);
            if (result != null) return result;
            result = FindTypeInNamedAssembly(assemblies, "Assembly-CSharp-firstpass", name);
            return result ?? FindTypeInAssemblies(assemblies, name);
        }

        private static Type FindTypeInNamedAssembly(List<Assembly> assemblies, string assemblyName, string name)
        {
            var assembly = assemblies.FirstOrDefault(item => item.GetName().Name == assemblyName);
            return assembly == null ? null : GetTypeFromAssembly(assembly, name);
        }

        private static Type FindTypeInAssemblies(List<Assembly> assemblies, string name)
        {
            foreach (var assembly in assemblies)
            {
                var result = GetTypeFromAssembly(assembly, name);
                if (result != null) return result;
            }
            return null;
        }

        private static Type GetTypeFromAssembly(Assembly asm, string name)
        {
            try
            {
                var t = asm.GetType(name);
                if (t != null && IsAllowedType(t)) return t;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types != null ? ex.Types.Where(x => x != null).ToArray() : Array.Empty<Type>();
                }

                return types.FirstOrDefault(x => x != null && IsAllowedType(x) && (x.Name == name || x.FullName == name));
            }
            catch
            {
                return null;
            }
        }

        internal static Type FindComponentType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Type type = FindType(name);
            if (type == null) return null;

            if (!typeof(Component).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.IsInterface) return null;

            return type;
        }

        internal static Type FindScriptableObjectType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Type type = FindType(name);
            if (type == null) return null;

            if (!typeof(ScriptableObject).IsAssignableFrom(type)) return null;
            if (type.IsAbstract || type.IsInterface) return null;

            return type;
        }
    }
}
