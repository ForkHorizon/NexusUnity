using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    internal static partial class NexusMcpConfigGenerator
    {
        internal const string ServerName = "nexus-unity";
        private static readonly Regex TomlArgsBridgeRegex = new Regex("^\\s*args\\s*=\\s*\\[\\s*\"([^\"]+)\"");

        internal static List<NexusMcpClientInfo> BuildAllForCurrentProject()
        {
            string pythonPath = MCPCliInstaller.ResolvePythonPathForUi();
            return BuildAll(MCPCliInstaller.GetDefaultBridgePathForUi(), pythonPath,
                MCPCliInstaller.GetProjectRootForUi(), GetHomeDirectory(), MCPCliInstaller.GetSourceBridgePathForUi());
        }

        internal static NexusMcpSetupResult WriteConfig(NexusMcpClientInfo info)
        {
            if (info == null)
            {
                return new NexusMcpSetupResult { Success = false, Message = "Missing integration info." };
            }

            try
            {
                if (info.Format == NexusMcpConfigFormat.CodexToml)
                {
                    return WriteCodexConfig(info.ConfigPath, info.BridgePath, info.PythonPath);
                }

                if (info.Format == NexusMcpConfigFormat.JsonMcpServers || info.Format == NexusMcpConfigFormat.JsonServers)
                {
                    return WriteJsonConfig(info.ConfigPath, info.RootKey, info.BridgePath, info.PythonPath);
                }
            }
            catch (Exception e)
            {
                return new NexusMcpSetupResult { Success = false, ConfigPath = info.ConfigPath, Message = e.Message };
            }

            return new NexusMcpSetupResult
            {
                Success = false,
                ConfigPath = info.ConfigPath,
                Message = string.IsNullOrEmpty(info.AutoSetupDisabledReason) ? "Auto setup is not available." : info.AutoSetupDisabledReason
            };
        }

        internal static string BuildJsonConfig(string rootKey, string bridgePath, string pythonPath)
        {
            var root = new JObject();
            root[rootKey] = new JObject { [ServerName] = BuildServerEntry(bridgePath, pythonPath) };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        internal static string BuildCodexToml(string bridgePath, string pythonPath)
        {
            return "[mcp_servers.nexus-unity]\n" +
                   "command = \"" + EscapeToml(NormalizePath(pythonPath)) + "\"\n" +
                   "args = [ \"" + EscapeToml(NormalizePath(bridgePath)) + "\" ]\n" +
                   "env = { " + MCPServer.AuthTokenEnvironmentVariable + " = \"" + EscapeToml(MCPServer.AuthToken) + "\" }\n";
        }

        internal static string BackupFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            if (File.Exists(backup)) backup = backup + "-" + DateTime.Now.Ticks;
            File.Copy(path, backup, false);
            return backup;
        }

        private static void ApplyTomlStatus(NexusMcpClientInfo info, string executable)
        {
            if (File.Exists(info.ConfigPath))
            {
                var lines = new List<string>(File.ReadAllLines(info.ConfigPath));
                if (HasTomlSection(lines))
                {
                    info.ConfiguredBridgePath = NormalizePath(ReadTomlBridgePath(lines));
                    if (string.IsNullOrEmpty(info.ConfiguredBridgePath))
                    {
                        info.Status = NexusMcpClientStatus.Error;
                        info.StatusDetail = "nexus-unity is present, but its args do not include a bridge path.";
                        return;
                    }

                    info.Status = NexusMcpClientStatus.Configured;
                    info.StatusDetail = "nexus-unity is present in the config.";
                    if (!TomlHasCurrentAuthToken(lines))
                    {
                        info.Status = NexusMcpClientStatus.Outdated;
                        info.StatusDetail = "nexus-unity is missing the current auth token. Run Auto Setup, then restart this client.";
                    }
                    return;
                }
            }

            bool found = IsExecutableResolved(executable);
            info.Status = found ? NexusMcpClientStatus.Detected : NexusMcpClientStatus.NotFound;
            info.StatusDetail = found ? executable + " CLI detected." : executable + " CLI was not found on PATH.";
        }

        private static bool HasTomlSection(List<string> lines)
        {
            return lines.Exists(line => line.Trim() == "[mcp_servers.nexus-unity]");
        }

        private static string ReadTomlBridgePath(List<string> lines)
        {
            int start = lines.FindIndex(line => line.Trim() == "[mcp_servers.nexus-unity]");
            if (start < 0) return null;

            for (int i = start + 1; i < lines.Count; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("[")) break;

                var match = TomlArgsBridgeRegex.Match(lines[i]);
                if (match.Success) return match.Groups[1].Value;
            }

            return null;
        }

        private static void ApplyJsonStatus(NexusMcpClientInfo info)
        {
            if (!File.Exists(info.ConfigPath))
            {
                info.Status = NexusMcpClientStatus.NotFound;
                info.StatusDetail = "Config file does not exist yet.";
                return;
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText(info.ConfigPath));
                var servers = json[info.RootKey] as JObject;
                var server = servers?[ServerName] as JObject;
                if (server != null)
                {
                    info.ConfiguredBridgePath = NormalizePath(ReadJsonBridgePath(server));
                    if (string.IsNullOrEmpty(info.ConfiguredBridgePath))
                    {
                        info.Status = NexusMcpClientStatus.Error;
                        info.StatusDetail = "nexus-unity is present, but its args do not include a bridge path.";
                        return;
                    }

                    info.Status = NexusMcpClientStatus.Configured;
                    info.StatusDetail = "nexus-unity is present in the config.";
                    if (!JsonHasCurrentAuthToken(server))
                    {
                        info.Status = NexusMcpClientStatus.Outdated;
                        info.StatusDetail = "nexus-unity is missing the current auth token. Run Auto Setup, then restart this client.";
                    }
                }
                else
                {
                    info.Status = NexusMcpClientStatus.Detected;
                    info.StatusDetail = "Config exists. Auto Setup will add nexus-unity.";
                }
            }
            catch (Exception e)
            {
                info.Status = NexusMcpClientStatus.Error;
                info.StatusDetail = "Config exists but could not be parsed: " + e.Message;
            }
        }

        private static string ReadJsonBridgePath(JObject server)
        {
            var args = server["args"] as JArray;
            if (args != null && args.Count > 0) return (string)args[0];
            return null;
        }

        private static NexusMcpSetupResult WriteJsonConfig(string path, string rootKey, string bridgePath, string pythonPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string backup = BackupFileIfExists(path);
            JObject config = new JObject();
            if (File.Exists(path))
            {
                try { config = JObject.Parse(File.ReadAllText(path)); }
                catch { config = new JObject(); }
            }

            if (!(config[rootKey] is JObject servers))
            {
                servers = new JObject();
                config[rootKey] = servers;
            }

            servers[ServerName] = BuildServerEntry(bridgePath, pythonPath);
            File.WriteAllText(path, config.ToString(Newtonsoft.Json.Formatting.Indented));
            return new NexusMcpSetupResult { Success = true, ConfigPath = path, BackupPath = backup, Message = "Config updated." };
        }

        private static NexusMcpSetupResult WriteCodexConfig(string path, string bridgePath, string pythonPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string backup = BackupFileIfExists(path);
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            RemoveTomlSection(lines);
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1])) lines.Add("");
            lines.AddRange(BuildCodexToml(bridgePath, pythonPath).TrimEnd().Split('\n'));
            File.WriteAllLines(path, lines);
            return new NexusMcpSetupResult { Success = true, ConfigPath = path, BackupPath = backup, Message = "Config updated." };
        }

        private static void RemoveTomlSection(List<string> lines)
        {
            int start = lines.FindIndex(line => line.Trim() == "[mcp_servers.nexus-unity]");
            if (start < 0) return;
            int end = start + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[")) end++;
            lines.RemoveRange(start, end - start);
        }

        private static JObject BuildServerEntry(string bridgePath, string pythonPath)
        {
            return new JObject { ["command"] = NormalizePath(pythonPath), ["args"] = new JArray { NormalizePath(bridgePath) }, ["env"] = new JObject { [MCPServer.AuthTokenEnvironmentVariable] = MCPServer.AuthToken } };
        }

        private static bool JsonHasCurrentAuthToken(JObject server) => string.Equals((string)server["env"]?[MCPServer.AuthTokenEnvironmentVariable], MCPServer.AuthToken, StringComparison.Ordinal);

        private static bool TomlHasCurrentAuthToken(List<string> lines)
        {
            string needle = MCPServer.AuthTokenEnvironmentVariable + " = \"" + MCPServer.AuthToken + "\"";
            int start = lines.FindIndex(line => line.Trim() == "[mcp_servers.nexus-unity]");
            if (start < 0) return false;
            for (int i = start + 1; i < lines.Count && !lines[i].TrimStart().StartsWith("["); i++)
            {
                if (lines[i].Contains(needle)) return true;
            }
            return false;
        }

        private static bool IsExecutableResolved(string name)
        {
            string path = MCPCliInstaller.ResolveExecutablePathForUi(name);
            return !string.IsNullOrEmpty(path) && path != name && File.Exists(path);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }

        private static string EscapeToml(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string GetHomeDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static string GetClaudeDesktopConfigPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(GetHomeDirectory(), "Library", "Application Support", "Claude", "claude_desktop_config.json");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
        }

        private static string GetClineConfigPath(string homeDir)
        {
            return Path.Combine(homeDir, ".cline", "data", "settings", "cline_mcp_settings.json");
        }
    }
}
