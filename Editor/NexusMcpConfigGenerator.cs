using System;
using System.Collections.Generic;
using System.IO;
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
            string pythonPath = MCPCliInstaller.ResolveExecutablePathForUi("python3");
            return BuildAll(MCPCliInstaller.GetDefaultBridgePathForUi(), pythonPath,
                MCPCliInstaller.GetProjectRootForUi(), GetHomeDirectory(), MCPCliInstaller.GetSourceBridgePathForUi());
        }

        internal static List<NexusMcpClientInfo> BuildAll(string bridgePath, string pythonPath, string projectRoot, string homeDir, string sourceBridgePath = null)
        {
            string normalizedBridgePath = NormalizePath(bridgePath);
            string normalizedSourceBridgePath = NormalizePath(sourceBridgePath);
            string sourceBridgeVersion = ReadBridgeVersion(normalizedSourceBridgePath);
            string deployedBridgeVersion = ReadBridgeVersion(normalizedBridgePath);

            var clients = new List<NexusMcpClientInfo>
            {
                CreateCodex(normalizedBridgePath, pythonPath, homeDir),
                CreateClaude(normalizedBridgePath, pythonPath),
                CreateCliClient(NexusMcpClientKind.Gemini, "Gemini", "gemini",
                    "Run Auto Setup, then restart or reopen Gemini CLI sessions.", normalizedBridgePath, pythonPath),
                CreateCliClient(NexusMcpClientKind.Antigravity, "Antigravity", "ag",
                    "Run Auto Setup, then restart Antigravity sessions that use MCP.", normalizedBridgePath, pythonPath),
                CreateJsonClient(NexusMcpClientKind.Cursor, "Cursor", Path.Combine(projectRoot, ".cursor", "mcp.json"),
                    "Paste into .cursor/mcp.json or use Auto Setup for this Unity project.", normalizedBridgePath, pythonPath, "mcpServers"),
                CreateJsonClient(NexusMcpClientKind.VsCodeClineRoo, "VS Code / Cline / Roo", Path.Combine(projectRoot, ".vscode", "mcp.json"),
                    "Paste into .vscode/mcp.json, then restart the MCP client extension.", normalizedBridgePath, pythonPath, "servers"),
                CreateJsonClient(NexusMcpClientKind.Windsurf, "Windsurf", Path.Combine(homeDir, ".codeium", "windsurf", "mcp_config.json"),
                    "Paste into the Windsurf MCP config, then restart Windsurf.", normalizedBridgePath, pythonPath, "mcpServers"),
                CreateManual(normalizedBridgePath, pythonPath)
            };

            foreach (var client in clients)
            {
                ApplyBridgeVersions(client, normalizedSourceBridgePath, sourceBridgeVersion, deployedBridgeVersion);
            }

            foreach (var client in clients)
            {
                ApplyDeploymentDriftStatus(client);
            }

            return clients;
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
                   "args = [ \"" + EscapeToml(NormalizePath(bridgePath)) + "\" ]\n";
        }

        internal static string BackupFileIfExists(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            if (File.Exists(backup)) backup = backup + "-" + DateTime.Now.Ticks;
            File.Copy(path, backup, false);
            return backup;
        }

        private static NexusMcpClientInfo CreateCodex(string bridgePath, string pythonPath, string homeDir)
        {
            string path = Path.Combine(homeDir, ".codex", "config.toml");
            var info = BaseInfo(NexusMcpClientKind.Codex, "Codex", bridgePath, pythonPath);
            info.Format = NexusMcpConfigFormat.CodexToml;
            info.ConfigPath = path;
            info.ConfigText = BuildCodexToml(bridgePath, pythonPath);
            info.Instruction = "Paste into ~/.codex/config.toml, then restart Codex CLI sessions.";
            info.SupportsAutoSetup = true;
            ApplyTomlStatus(info, "codex");
            return info;
        }

        private static NexusMcpClientInfo CreateClaude(string bridgePath, string pythonPath)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
            var info = CreateJsonClient(NexusMcpClientKind.ClaudeDesktop, "Claude Desktop", path,
                "Paste into claude_desktop_config.json, then restart Claude Desktop.", bridgePath, pythonPath, "mcpServers");
            info.SupportsAutoSetup = true;
            return info;
        }

        private static NexusMcpClientInfo CreateJsonClient(NexusMcpClientKind kind, string name, string path,
            string instruction, string bridgePath, string pythonPath, string rootKey)
        {
            var info = BaseInfo(kind, name, bridgePath, pythonPath);
            info.Format = rootKey == "servers" ? NexusMcpConfigFormat.JsonServers : NexusMcpConfigFormat.JsonMcpServers;
            info.ConfigPath = path;
            info.RootKey = rootKey;
            info.Instruction = instruction;
            info.ConfigText = BuildJsonConfig(rootKey, bridgePath, pythonPath);
            info.SupportsAutoSetup = true;
            ApplyJsonStatus(info);
            return info;
        }

        private static NexusMcpClientInfo CreateCliClient(NexusMcpClientKind kind, string name, string command,
            string instruction, string bridgePath, string pythonPath)
        {
            var info = BaseInfo(kind, name, bridgePath, pythonPath);
            info.Format = NexusMcpConfigFormat.CliManaged;
            info.Instruction = instruction;
            info.ConfigPath = "Managed by " + command + " CLI";
            info.ConfigText = BuildJsonConfig("mcpServers", bridgePath, pythonPath);
            bool found = IsExecutableResolved(command) || (kind == NexusMcpClientKind.Antigravity && IsExecutableResolved("antigravity"));
            info.SupportsAutoSetup = found;
            info.Status = found ? NexusMcpClientStatus.Detected : NexusMcpClientStatus.NotFound;
            info.StatusDetail = found ? command + " CLI detected." : command + " CLI was not found on PATH.";
            if (!found) info.AutoSetupDisabledReason = info.StatusDetail;
            return info;
        }

        private static NexusMcpClientInfo CreateManual(string bridgePath, string pythonPath)
        {
            var info = BaseInfo(NexusMcpClientKind.GenericJson, "Generic MCP JSON", bridgePath, pythonPath);
            info.Format = NexusMcpConfigFormat.ManualOnly;
            info.Instruction = "Paste this JSON into any MCP client that accepts mcpServers.";
            info.ConfigPath = "Manual setup";
            info.ConfigText = BuildJsonConfig("mcpServers", bridgePath, pythonPath);
            info.Status = NexusMcpClientStatus.Detected;
            info.StatusDetail = "Manual config is always available.";
            info.SupportsAutoSetup = false;
            info.AutoSetupDisabledReason = "Generic MCP JSON is manual copy/paste only.";
            return info;
        }

        private static NexusMcpClientInfo BaseInfo(NexusMcpClientKind kind, string name, string bridgePath, string pythonPath)
        {
            return new NexusMcpClientInfo
            {
                Kind = kind,
                ElementKey = kind.ToString(),
                DisplayName = name,
                BridgePath = NormalizePath(bridgePath),
                PythonPath = NormalizePath(pythonPath)
            };
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
            return new JObject
            {
                ["command"] = NormalizePath(pythonPath),
                ["args"] = new JArray { NormalizePath(bridgePath) }
            };
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
    }
}
