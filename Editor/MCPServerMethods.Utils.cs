using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
namespace UnityMCP.Editor
{
    /// <summary>
    /// Provides shared editor utilities for safe project path validation, Unity object serialization, and UI Toolkit lookup.
    /// </summary>
    /// <remarks>
    /// Path helpers resolve filesystem input against <see cref="Application.dataPath"/> and restrict asset paths before
    /// calling AssetDatabase-facing code, preventing traversal outside allowed project folders.
    /// </remarks>
    public static partial class MCPServerMethods
    {
        [DllImport("libc", EntryPoint = "realpath", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr sys_realpath(string path, IntPtr resolved_path);

        [DllImport("libc", EntryPoint = "free")]
        private static extern void sys_free(IntPtr ptr);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle hFile,
            [Out] StringBuilder lpszFilePath,
            uint cchFilePath,
            uint dwFlags);

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint VOLUME_NAME_DOS = 0x0;

        private static string ResolveUnixPath(string path)
        {
            IntPtr resolved = sys_realpath(path, IntPtr.Zero);
            if (resolved == IntPtr.Zero)
            {
                return path;
            }
            try
            {
                return Marshal.PtrToStringAnsi(resolved);
            }
            finally
            {
                sys_free(resolved);
            }
        }

        private static string ResolveWindowsPath(string path)
        {
            using (SafeFileHandle hFile = CreateFile(
                path,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero))
            {
                if (hFile.IsInvalid)
                {
                    return path;
                }

                StringBuilder sb = new StringBuilder(4096);
                uint result = GetFinalPathNameByHandle(hFile, sb, (uint)sb.Capacity, VOLUME_NAME_DOS);
                if (result == 0)
                {
                    return path;
                }

                string resolved = sb.ToString();
                if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = @"\\" + resolved.Substring(8);
                }
                else if (resolved.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = resolved.Substring(4);
                }
                return resolved;
            }
        }

        private static string ResolveRealPathInternal(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ResolveWindowsPath(path);
            }
            else
            {
                return ResolveUnixPath(path);
            }
        }

        private static string ResolveRealPath(string path)
        {
            string absolutePath = Path.GetFullPath(path).Replace('\\', '/');
            string current = absolutePath;
            string remainder = "";

            while (!string.IsNullOrEmpty(current) && !File.Exists(current) && !Directory.Exists(current))
            {
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current)
                {
                    break;
                }

                string name = Path.GetFileName(current);
                if (!string.IsNullOrEmpty(name))
                {
                    remainder = string.IsNullOrEmpty(remainder) ? name : name + "/" + remainder;
                }
                current = parent.Replace('\\', '/');
            }

            string resolvedCurrent = current;
            if (File.Exists(current) || Directory.Exists(current))
            {
                resolvedCurrent = ResolveRealPathInternal(current);
            }

            string result = string.IsNullOrEmpty(remainder) ? resolvedCurrent : Path.Combine(resolvedCurrent, remainder);
            return Path.GetFullPath(result).Replace('\\', '/');
        }

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

            // Resolve real target path (resolving symbolic links and junctions)
            string resolvedPath = ResolveRealPath(cleanPath);

            // Get project root with resolved symlinks as well to allow matching when project itself is in symlinked dir
            string resolvedProjectRoot = ResolveRealPath(projectRoot);

            // Check if path is within project root
            string projectRootSlash = resolvedProjectRoot.EndsWith("/") ? resolvedProjectRoot : resolvedProjectRoot + "/";
            if (!resolvedPath.Equals(resolvedProjectRoot, System.StringComparison.OrdinalIgnoreCase) &&
                !resolvedPath.StartsWith(projectRootSlash, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Exception("Access denied: Path is outside project directory.");
            }

            return resolvedPath;
        }

        /// <summary>
        /// Validates that an asset path is safe to use with AssetDatabase.
        /// Prevents traversal outside allowed project folders (Assets, Packages, ProjectSettings).
        /// Returns the relative, safe asset path.
        /// </summary>
        internal static string ValidateAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new System.Exception("Asset path cannot be empty");

            // ValidatePath ensures it resolves securely without escaping the project root
            string fullPath = ValidatePath(path);

            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string projectRootSlash = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";

            // If the path is exactly the project root, this usually isn't a valid asset path
            if (fullPath.Equals(projectRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Exception("Access denied: Cannot use the project root as an asset path.");
            }

            // Convert absolute path back to a relative path from the project root
            string relativePath = fullPath.Substring(projectRootSlash.Length).Replace('\\', '/');

            if (!relativePath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("ProjectSettings/", System.StringComparison.OrdinalIgnoreCase) &&
                !relativePath.Equals("Assets", System.StringComparison.OrdinalIgnoreCase) &&
                !relativePath.Equals("Packages", System.StringComparison.OrdinalIgnoreCase) &&
                !relativePath.Equals("ProjectSettings", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Exception("Asset path must be within Assets, Packages, or ProjectSettings folders.");
            }

            return relativePath;
        }

        private static bool IsCSharpScriptPath(string path)
        {
            return path.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireScriptWriteConfirmation(JToken p)
        {
            if (p?["confirm"]?.Value<bool>() != true)
            {
                throw new System.ArgumentException("Writing C# scripts requires confirm: true because it triggers Unity compilation.");
            }
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
    }
}
