using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

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
        private static string ValidatePath(string path)
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
            // Ensure projectRoot ends with slash for correct prefix checking (e.g. prevent /project_secret matching /project)
            string secureRoot = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";

            // Allow access if it's the root itself or a file inside
            if (!fullPath.StartsWith(secureRoot, System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, projectRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Exception("Access denied: Path is outside project directory.");
            }

            // Ensure we aren't matching a sibling directory with a similar prefix (e.g. /Project_Secret)
            if (fullPath.Length > projectRoot.Length && fullPath[projectRoot.Length] != '/')
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

        private static JToken SerializeVisualElement(VisualElement el)
        {
            var obj = new JObject
            {
                ["name"] = el.name,
                ["type"] = el.GetType().Name,
                ["visible"] = el.resolvedStyle.display != DisplayStyle.None
            };
            var children = new JArray();
            foreach (var child in el.Children()) children.Add(SerializeVisualElement(child));
            if (children.Count > 0) obj["children"] = children;
            return obj;
        }
    }
}
