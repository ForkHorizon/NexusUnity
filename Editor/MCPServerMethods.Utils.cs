#pragma warning disable 0618
using System.Linq;
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

        private static EditorWindow FindWindow(string title)
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.titleContent.text == title);
        }

        private static VisualElement FindElementByName(VisualElement root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (var child in root.Children())
            {
                var found = FindElementByName(child, name);
                if (found != null) return found;
            }
            return null;
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
        // Internal obsolete calls are suppressed ONLY here.

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
#pragma warning disable CS0618
                // We use the obsolete cast ONLY here to bridge JSON (int) to EntityId.
                return (EntityId)(int)token;
#pragma warning restore CS0618
            }
            catch
            {
                return default;
            }
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
        /// Returns the raw integer value of the ID for JSON serialization.
        /// </summary>
        public static int GetRawId(this UnityEngine.Object obj)
        {
            if (obj == null) return 0;
#pragma warning disable CS0618
            return (int)obj.GetEntityId();
#pragma warning restore CS0618
        }
    }
}