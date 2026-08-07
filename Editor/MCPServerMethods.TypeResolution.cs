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
            Type resultType = null;
            
            // Priority 1: Check Assembly-CSharp
            var mainAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (mainAsm != null)
            {
                resultType = GetTypeFromAssembly(mainAsm, name);
            }

            // Priority 2: Check UnityEngine CoreModule (Component, Camera, BoxCollider, etc.)
            if (resultType == null)
            {
                var coreAsm = typeof(Component).Assembly;
                if (IsAllowedAssembly(coreAsm))
                {
                    resultType = GetTypeFromAssembly(coreAsm, name);
                }
            }

            // Priority 3: Check UnityEngine.UI module if loaded
            if (resultType == null)
            {
                var uiAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "UnityEngine.UI");
                if (uiAsm != null)
                {
                    resultType = GetTypeFromAssembly(uiAsm, name);
                }
            }

            // Priority 4: Check Assembly-CSharp-firstpass
            if (resultType == null)
            {
                var firstPassAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp-firstpass");
                if (firstPassAsm != null)
                {
                    resultType = GetTypeFromAssembly(firstPassAsm, name);
                }
            }

            // Priority 5: Check all other allowed assemblies
            if (resultType == null)
            {
                foreach (var a in assemblies)
                {
                    resultType = GetTypeFromAssembly(a, name);
                    if (resultType != null) break;
                }
            }

            if (resultType != null && IsAllowedType(resultType))
            {
                _typeCache[name] = resultType;
                return resultType;
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
