using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System;
using System.Text;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Deploys Nexus Unity bridge files from the package into the Unity project root and registers them with external MCP clients.
    /// </summary>
    /// <remarks>
    /// Installer methods copy Python bridge files and documentation pointers, create project-root directories, run local CLI processes,
    /// update client configuration files, write Nexus editor logs, and display Unity editor dialogs when setup fails.
    /// </remarks>
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
            return Path.GetDirectoryName(sourcePath);
        }

        private static void DeployDocumentationPointer(string projectRoot, string sourcePath)
        {
            string libraryRoot = FindLibraryRoot(sourcePath);
            string[] docFiles = { "API_REFERENCE.MD", "DOCUMENTATION.MD" };

            foreach (string file in docFiles)
            {
                string src = Path.Combine(libraryRoot, file);
                if (File.Exists(src))
                {
                    try
                    {
                        string dst = Path.Combine(projectRoot, file);
                        File.Copy(src, dst, true);
                        NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Copied documentation to root: " + dst);
                    }
                    catch (Exception e)
                    {
                        NexusEditorLog.Warning(NexusLogCategory.Integrations, "[MCP] Failed to copy " + file + " to root: " + e.Message);
                    }
                }
            }

            string docPointerPath = Path.Combine(projectRoot, "NEXUS_UNITY_DOCS.md");
            string docContent = "# Nexus Unity - AI Context\n\n" +
                               "This project uses **Nexus Unity** for AI Editor automation.\n\n" +
                               "## 📚 Documentation Access\n" +
                               "- **Full Tool Reference**: [API_REFERENCE.MD](API_REFERENCE.MD)\n" +
                               "- **Technical Guide**: [DOCUMENTATION.MD](DOCUMENTATION.MD)\n\n" +
                               "## 🤖 AI Instructions\n" +
                               "Before performing any Unity tasks, ALWAYS read `API_REFERENCE.MD` to understand the available tools, their parameters, and the surgical editing patterns required for this project.";

            File.WriteAllText(docPointerPath, docContent);
            NexusEditorLog.Log(NexusLogCategory.Integrations, "[MCP] Documentation pointer deployed: " + docPointerPath);
        }



        private static string FindBridgeScript()
        {
            string[] guids = AssetDatabase.FindAssets("MCPCliInstaller t:Script");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("NexusUnity")) continue;
                
                string dir = Path.GetDirectoryName(path);
                string potentialBridge = Path.Combine(dir, "nexus_unity_bridge.py");
                if (File.Exists(Path.GetFullPath(potentialBridge))) return Path.GetFullPath(potentialBridge);
            }

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.EndsWith("nexus_unity_bridge.py")) return Path.GetFullPath(path);
            }
            return null;
        }

        private static string ResolveExecutablePath(string name)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                // 'which' does not exist on Windows; use 'where' to resolve against PATH (and PATHEXT, so
                // .cmd/.exe shims like gemini.cmd are found). Falls back to the bare name when unresolved.
                string fromWhere = GetPathFromWhere(name);
                return string.IsNullOrEmpty(fromWhere) ? name : fromWhere;
            }

            // 1. Explicit check for NVM/Node paths (High priority for Codex)
            if (name == "codex")
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string nvmBase = Path.Combine(home, ".nvm/versions/node");
                if (Directory.Exists(nvmBase))
                {
                    foreach (var versionDir in Directory.GetDirectories(nvmBase))
                    {
                        string potential = Path.Combine(versionDir, "bin/codex");
                        if (File.Exists(potential)) return potential;
                    }
                }
            }

            // 2. Try 'which' (respects shell PATH)
            string pathFromWhich = GetPathFromWhich(name);
            if (!string.IsNullOrEmpty(pathFromWhich) && pathFromWhich != name && File.Exists(pathFromWhich))
            {
                // Only return if it's not the older homebrew version of codex (if we can tell)
                if (name == "codex" && pathFromWhich.Contains("homebrew")) {
                    // Continue to fallback if we prefer NVM
                } else {
                    return pathFromWhich;
                }
            }

            // 3. Common system paths
            string[] searchPaths = { 
                "/usr/local/bin/" + name, 
                "/opt/homebrew/bin/" + name, 
                "/usr/bin/" + name, 
                "/bin/" + name 
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path)) return path;
            }

            return name;
        }

        private static string GetPathFromWhich(string name)
        {
            ProcessStartInfo psi = new ProcessStartInfo 
            { 
                FileName = "/usr/bin/which", 
                Arguments = name, 
                RedirectStandardOutput = true, 
                UseShellExecute = false, 
                CreateNoWindow = true 
            };

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                string pathEnv = "";
                try { pathEnv = Environment.GetEnvironmentVariable("PATH"); } catch(Exception) {}
                // Unity launched from Finder/Hub (not a terminal) inherits a stripped PATH
                // (typically just /usr/bin:/bin:/usr/sbin:/sbin) with no /usr/local/bin or /opt/homebrew/bin.
                // 'which' would then resolve python3 to the old Xcode-bundled interpreter at /usr/bin/python3
                // instead of a modern one. Prepend common interpreter locations so they're checked first,
                // matching the priority order ResolveExecutablePath's own fallback search list already uses.
                psi.EnvironmentVariables["PATH"] = "/usr/local/bin:/opt/homebrew/bin:" + pathEnv;
            }

            try
            {
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(output)) return output;
                }
            }
            catch (Exception) { }
            return name;
        }

        private static string GetPathFromWhere(string name)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        // 'where' can list multiple matches, one per line. Take the first that exists on disk.
                        foreach (string line in output.Split('\n'))
                        {
                            string candidate = line.Trim();
                            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// Resolves the Python interpreter to embed in generated MCP configs, preferring a concrete path.
        /// Tries <c>python3</c>, then <c>python</c>, then the Windows <c>py</c> launcher (which defaults to Python 3).
        /// </summary>
        /// <remarks>
        /// Generated configs previously hardcoded <c>python3</c>, which is frequently absent from PATH on Windows
        /// (where the interpreter is usually <c>python</c> or the <c>py</c> launcher), so the bridge failed to launch.
        /// </remarks>
        private static string ResolvePythonPath()
        {
            string python3 = ResolveExecutablePath("python3");
            if (!string.IsNullOrEmpty(python3) && python3 != "python3") return python3;

            string python = ResolveExecutablePath("python");
            if (!string.IsNullOrEmpty(python) && python != "python") return python;

            // Windows commonly ships the 'py' launcher instead of a 'python3' alias; 'py <script>' runs Python 3.
            if (Application.platform == RuntimePlatform.WindowsEditor) return "py";

            return "python3";
        }

        private static ProcessStartInfo CreateProcessStartInfo(string executable, params string[] arguments)
        {
            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            string extension = Path.GetExtension(executable);
            bool useCmdShim = isWindows && (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase));
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = useCmdShim ? "cmd.exe" : executable,
                Arguments = useCmdShim ? BuildWindowsBatchArguments(executable, arguments) : BuildProcessArguments(arguments),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!isWindows)
            {
                string pathEnv = "";
                try { pathEnv = Environment.GetEnvironmentVariable("PATH"); } catch(Exception) {}
                
                // Add common locations but keep existing PATH to support NVM, etc.
                psi.EnvironmentVariables["PATH"] = pathEnv + ":/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin";
            }
            return psi;
        }

        private static string BuildProcessArguments(string[] arguments)
        {
            return string.Join(" ", Array.ConvertAll(arguments, QuoteWindowsArgument));
        }

        private static string BuildWindowsBatchArguments(string executable, string[] arguments)
        {
            string command = QuoteCmdArgument(executable);
            foreach (string argument in arguments)
            {
                command += " " + QuoteCmdArgument(argument);
            }
            return "/d /v:off /s /c " + QuoteWindowsArgument(command);
        }

        private static string QuoteCmdArgument(string argument)
        {
            return QuoteWindowsArgument(EscapeCmdMetacharacters(argument));
        }

        private static string EscapeCmdMetacharacters(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>")
                .Replace("%", "^%");
        }

        private static string QuoteWindowsArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument)) return "\"\"";
            if (argument.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0) return argument;

            StringBuilder result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }
                result.Append('\\', backslashes);
                result.Append(c);
                backslashes = 0;
            }
            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static bool RunInstallerProcess(ProcessStartInfo psi, string cliPath, bool showSuccessDialog, string cliName)
        {
            try
            {
                using (Process p = Process.Start(psi))
                {
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                    {
                        return true;
                    }
                    else
                    {
                        string msg = "CLI command failed at " + cliPath + ".\n\nExit Code: " + p.ExitCode + "\nError: " + error;
                        NexusEditorLog.Warning(NexusLogCategory.Integrations, "[MCP] " + msg);
                        
                        if (showSuccessDialog) // If this was supposed to be the final step
                        {
                            EditorUtility.DisplayDialog("MCP Error", "Failed to link to " + cliName + " CLI.\n\n" + error, "OK");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Process start failed: " + e.Message);
            }
            return false;
        }
    }
}
