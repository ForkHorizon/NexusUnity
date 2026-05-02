using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods providing internal utilities.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static readonly System.Func<int, EntityId> LegacyIntToEntityId = CreateLegacyIntToEntityId();
        private static readonly System.Func<EntityId, int> LegacyEntityIdToInt = CreateLegacyEntityIdToInt();

        /// <summary>
        /// Validates that the path is within the project directory to prevent path traversal.
        /// Returns the absolute path if valid.
        /// </summary>
        internal static string ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new System.Exception("Path cannot be empty");

            // Normalize path separators
            string cleanPath = path.Replace('\\', '/');

            // Get project root (parent of Assets folder)
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

            // If path is relative, combine with project root
            if (!System.IO.Path.IsPathRooted(cleanPath))
            {
                cleanPath = System.IO.Path.Combine(projectRoot, cleanPath).Replace('\\', '/');
            }

            // Resolve full path (handles .. and symlinks)
            string fullPath = System.IO.Path.GetFullPath(cleanPath).Replace('\\', '/');

            // Check if path is within project root
            string projectRootSlash = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";
            if (!fullPath.Equals(projectRoot, System.StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(projectRootSlash, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Exception("Access denied: Path is outside project directory.");
            }

            return fullPath;
        }

        internal static JObject SerializeVector3(Vector3 v)
        {
            return new JObject { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };
        }

        internal static string SerializeColor(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        private static EditorWindow FindWindow(string title)
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.titleContent.text == title);
        }

        private static VisualElement FindElementByName(VisualElement root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            return root.Q(name);
        }

        private static JToken SerializeVisualElement(VisualElement el, bool deep = false)
        {
            var obj = new JObject
            {
                ["name"] = el.name,
                ["type"] = el.GetType().Name,
                ["visible"] = el.resolvedStyle.display != DisplayStyle.None
            };

            if (el is TextElement te && !string.IsNullOrEmpty(te.text))
                obj["text"] = te.text;

            var classes = el.GetClasses().ToList();
            if (classes.Count > 0)
                obj["classes"] = new JArray(classes);

            if (deep)
            {
                var rect = el.layout;
                obj["layout"] = new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };
                
                // Add useful computed styles
                var style = new JObject();
                style["display"] = el.resolvedStyle.display.ToString();
                style["visibility"] = el.resolvedStyle.visibility.ToString();
                style["opacity"] = el.resolvedStyle.opacity;
                style["color"] = el.resolvedStyle.color.ToString();
                style["backgroundColor"] = el.resolvedStyle.backgroundColor.ToString();
                obj["computed_style"] = style;
            }

            var children = new JArray();
            foreach (var child in el.Children()) children.Add(SerializeVisualElement(child, deep));
            if (children.Count > 0) obj["children"] = children;
            return obj;
        }

        // --- Version-Agnostic ID Wrappers (Updated for Unity 6) ---
        // We use GetId() and IdToObject() throughout the codebase to stay future-proof.
        // JSON still carries the legacy integer-shaped instance_id, so we bridge through
        // EntityId.ToULong/FromULong instead of the removed int conversion operators.

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
                    return EntityId.FromULong(rawId);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        internal static int ConvertEntityIdToLegacyInt(EntityId id)
        {
            return LegacyEntityIdToInt(id);
        }

        private static System.Func<int, EntityId> CreateLegacyIntToEntityId()
        {
            var method = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                                     m.ReturnType == typeof(EntityId) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(int));

            if (method != null)
            {
                return (System.Func<int, EntityId>)System.Delegate.CreateDelegate(typeof(System.Func<int, EntityId>), method);
            }

            return value => EntityId.FromULong(unchecked((ulong)(uint)value));
        }

        private static System.Func<EntityId, int> CreateLegacyEntityIdToInt()
        {
            var method = typeof(EntityId).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") &&
                                     m.ReturnType == typeof(int) &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(EntityId));

            if (method != null)
            {
                return (System.Func<EntityId, int>)System.Delegate.CreateDelegate(typeof(System.Func<EntityId, int>), method);
            }

            return value => unchecked((int)EntityId.ToULong(value));
        }
    }

    /// <summary>
    /// Extension methods to handle version-specific Unity Object ID changes.
    /// </summary>
    public static class UnityObjectIdExtensions
    {
        public static EntityId GetId(this UnityEngine.Object obj)
        {
            if (obj == null) return default;
            return obj.GetEntityId();
        }

        /// <summary>
        /// Returns the legacy integer-shaped ID used by the current JSON protocol.
        /// </summary>
        public static int GetRawId(this UnityEngine.Object obj)
        {
            if (obj == null) return 0;
            return MCPServerMethods.ConvertEntityIdToLegacyInt(obj.GetEntityId());
        }
    }
}
