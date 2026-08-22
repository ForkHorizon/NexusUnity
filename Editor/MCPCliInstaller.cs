using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

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
        private static string ResolveExecutablePath(string name)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string fromWhere = GetPathFromWhere(name);
                return string.IsNullOrEmpty(fromWhere) ? name : fromWhere;
            }

            if (name == "codex")
            {
                string nvmCodex = ResolveNvmCodex();
                if (!string.IsNullOrEmpty(nvmCodex)) return nvmCodex;
            }

            string pathFromWhich = GetPathFromWhich(name);
            if (!string.IsNullOrEmpty(pathFromWhich) && pathFromWhich != name && File.Exists(pathFromWhich))
            {
                if (name != "codex" || !pathFromWhich.Contains("homebrew"))
                {
                    return pathFromWhich;
                }
            }

            return ResolveSystemExecutable(name);
        }

        private static string ResolveNvmCodex()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string nvmBase = Path.Combine(home, ".nvm/versions/node");
            if (!Directory.Exists(nvmBase)) return null;

            foreach (var versionDir in Directory.GetDirectories(nvmBase))
            {
                string potential = Path.Combine(versionDir, "bin/codex");
                if (File.Exists(potential)) return potential;
            }
            return null;
        }

        private static string ResolveSystemExecutable(string name)
        {
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

        private static string ResolvePythonPath()
        {
            string python3 = ResolveExecutablePath("python3");
            if (!string.IsNullOrEmpty(python3) && python3 != "python3") return python3;

            string python = ResolveExecutablePath("python");
            if (!string.IsNullOrEmpty(python) && python != "python") return python;

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
            return RunInstallerProcess(psi, cliPath, showSuccessDialog, cliName, out _, true);
        }

        private static bool RunInstallerProcess(ProcessStartInfo psi, string cliPath, bool showSuccessDialog, string cliName, out string error, bool logFailure)
        {
            error = string.Empty;
            try
            {
                using (Process p = Process.Start(psi))
                {
                    error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                    {
                        return true;
                    }

                    string msg = "CLI command failed at " + cliPath + ".\n\nExit Code: " + p.ExitCode + "\nError: " + error;
                    if (logFailure) NexusEditorLog.Warning(NexusLogCategory.Integrations, "[MCP] " + msg);

                    if (showSuccessDialog)
                    {
                        EditorUtility.DisplayDialog("MCP Error", "Failed to link to " + cliName + " CLI.\n\n" + error, "OK");
                    }
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                NexusEditorLog.Error(NexusLogCategory.Integrations, "[MCP] Process start failed: " + e.Message);
            }
            return false;
        }
    }
}
