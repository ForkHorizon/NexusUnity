using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        private static bool DeployBridgeScript(out string destinationPath)
        {
            destinationPath = null;
            string sourcePath = FindBridgeScript();

            if (string.IsNullOrEmpty(sourcePath))
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Could not find 'nexus_unity_bridge.py' in the project.");
                EditorUtility.DisplayDialog("MCP Error", "Could not find 'nexus_unity_bridge.py'.\n\nEnsure the library is correctly imported.", "OK");
                return false;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            destinationPath = Path.Combine(projectRoot, "nexus_unity_bridge.py");

            try
            {
                File.Copy(sourcePath, destinationPath, true);
                DeployBridgeModule(projectRoot, sourcePath);
                NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Bridge script deployed to stable location: " + destinationPath, true);
                DeployDocumentationPointer(projectRoot, sourcePath);
                return true;
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Failed to deploy bridge or docs: " + e.Message);
                EditorUtility.DisplayDialog("MCP Error", "Failed to deploy integration files to project root.\n\n" + e.Message, "OK");
                return false;
            }
        }

        private static void DeployBridgeModule(string projectRoot, string sourcePath)
        {
            string sourceDir = Path.GetDirectoryName(sourcePath);
            string sourceModuleDir = Path.Combine(sourceDir, "nexus_bridge");
            if (!Directory.Exists(sourceModuleDir))
            {
                return;
            }

            string destinationModuleDir = Path.Combine(projectRoot, "nexus_bridge");
            CopyDirectory(sourceModuleDir, destinationModuleDir);
            NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Bridge module deployed to stable location: " + destinationModuleDir);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destinationDir, fileName), true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName == "__pycache__")
                {
                    continue;
                }

                CopyDirectory(dir, Path.Combine(destinationDir, dirName));
            }
        }

        private static string FindLibraryRoot(string sourcePath)
        {
            string dir = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "package.json")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static void DeployDocumentationPointer(string projectRoot, string sourcePath)
        {
            string libraryRoot = FindLibraryRoot(sourcePath);
            if (string.IsNullOrEmpty(libraryRoot))
            {
                return;
            }

            string docSource = Path.Combine(libraryRoot, "DOCUMENTATION.MD");
            if (!File.Exists(docSource))
            {
                return;
            }

            string relativeDocPath = GetRelativePath(projectRoot, docSource).Replace("\\", "/");
            string pointerPath = Path.Combine(projectRoot, "NEXUS_UNITY_DOCUMENTATION.md");
            string pointerContent = "# Nexus Unity Documentation\n\n" +
                                   "The canonical Nexus Unity documentation is maintained in the package root:\n\n" +
                                   "- [DOCUMENTATION.MD](" + relativeDocPath + ")\n\n" +
                                   "Keep all edits in the package copy so changes stay with version control.\n";

            File.WriteAllText(pointerPath, pointerContent);
        }

        private static string GetRelativePath(string fromPath, string toPath)
        {
            Uri fromUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(fromPath)));
            Uri toUri = new Uri(Path.GetFullPath(toPath));
            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path;
            }
            return path + Path.DirectorySeparatorChar;
        }

        private static string FindBridgeScript()
        {
            string[] guids = AssetDatabase.FindAssets("nexus_unity_bridge");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("nexus_unity_bridge.py"))
                {
                    return Path.GetFullPath(path);
                }
            }

            string manualSearch = Path.Combine(Application.dataPath, "nexus_unity_bridge.py");
            if (File.Exists(manualSearch))
            {
                return manualSearch;
            }

            foreach (string path in Directory.GetFiles(Application.dataPath, "*.py", SearchOption.AllDirectories))
            {
                if (path.EndsWith("nexus_unity_bridge.py")) return Path.GetFullPath(path);
            }
            return null;
        }
    }
}
