using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UnityMCP.Editor
{
    internal static partial class NexusMcpConfigGenerator
    {
        internal readonly struct JsonClientDescriptor
        {
            internal readonly NexusMcpClientKind Kind;
            internal readonly string Name;
            internal readonly string Path;
            internal readonly string Instruction;
            internal readonly string RootKey;

            internal JsonClientDescriptor(NexusMcpClientKind kind, string name, string path, string instruction, string rootKey)
            {
                Kind = kind;
                Name = name;
                Path = path;
                Instruction = instruction;
                RootKey = rootKey;
            }
        }

        internal static List<NexusMcpClientInfo> BuildAll(string bridgePath, string pythonPath, string projectRoot, string homeDir, string sourceBridgePath = null)
        {
            string normBridge = NormalizePath(bridgePath);
            string normSourceBridge = NormalizePath(sourceBridgePath);
            string sourceVer = ReadBridgeVersion(normSourceBridge);
            string deployedVer = ReadBridgeVersion(normBridge);

            var clients = CreateCoreClients(normBridge, pythonPath, projectRoot, homeDir);
            AppendJsonClients(clients, projectRoot, homeDir, normBridge, pythonPath);
            clients.Add(CreateManual(normBridge, pythonPath));

            foreach (var client in clients)
            {
                ApplyBridgeVersions(client, normSourceBridge, sourceVer, deployedVer);
                ApplyDeploymentDriftStatus(client);
            }

            return clients;
        }

        internal readonly struct CliClientDescriptor
        {
            internal readonly NexusMcpClientKind Kind;
            internal readonly string Name;
            internal readonly string Command;
            internal readonly string Instruction;

            internal CliClientDescriptor(NexusMcpClientKind kind, string name, string command, string instruction)
            {
                Kind = kind;
                Name = name;
                Command = command;
                Instruction = instruction;
            }
        }

        private static List<NexusMcpClientInfo> CreateCoreClients(string bridgePath, string pythonPath, string projectRoot, string homeDir)
        {
            var geminiDesc = new CliClientDescriptor(NexusMcpClientKind.Gemini, "Gemini", "gemini",
                "Run Auto Setup, then restart or reopen Gemini CLI sessions.");
            var gemini = CreateCliClient(in geminiDesc, bridgePath, pythonPath);
            gemini.CustomAutoSetup = MCPCliInstaller.LinkToGemini;

            var claudeCode = CreateClaudeCode(projectRoot, bridgePath, pythonPath);
            claudeCode.CustomAutoSetup = MCPCliInstaller.LinkToClaudeCode;

            return new List<NexusMcpClientInfo>
            {
                CreateCodex(bridgePath, pythonPath, homeDir),
                CreateClaude(bridgePath, pythonPath),
                claudeCode,
                gemini
            };
        }

        private static void AppendJsonClients(List<NexusMcpClientInfo> clients, string projectRoot, string homeDir, string bridgePath, string pythonPath)
        {
            var descriptors = new[]
            {
                new JsonClientDescriptor(NexusMcpClientKind.Antigravity, "Antigravity", Path.Combine(projectRoot, ".agents", "mcp_config.json"),
                    "Paste into .agents/mcp_config.json, then restart or reload Antigravity sessions.", "mcpServers"),
                new JsonClientDescriptor(NexusMcpClientKind.Cursor, "Cursor", Path.Combine(projectRoot, ".cursor", "mcp.json"),
                    "Paste into .cursor/mcp.json or use Auto Setup for this Unity project.", "mcpServers"),
                new JsonClientDescriptor(NexusMcpClientKind.VsCode, "VS Code", Path.Combine(projectRoot, ".vscode", "mcp.json"),
                    "Paste into .vscode/mcp.json, then restart VS Code's built-in MCP support.", "servers"),
                new JsonClientDescriptor(NexusMcpClientKind.RooCode, "Roo Code", Path.Combine(projectRoot, ".roo", "mcp.json"),
                    "Paste into .roo/mcp.json, then restart Roo Code.", "mcpServers"),
                new JsonClientDescriptor(NexusMcpClientKind.Cline, "Cline", GetClineConfigPath(homeDir),
                    "Paste into Cline's global MCP settings, then restart Cline.", "mcpServers"),
                new JsonClientDescriptor(NexusMcpClientKind.Windsurf, "Windsurf", Path.Combine(homeDir, ".codeium", "windsurf", "mcp_config.json"),
                    "Paste into the Windsurf MCP config, then restart Windsurf.", "mcpServers"),
            };

            foreach (var desc in descriptors)
            {
                clients.Add(CreateJsonClient(desc, bridgePath, pythonPath));
            }
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
            string path = GetClaudeDesktopConfigPath();
            var desc = new JsonClientDescriptor(NexusMcpClientKind.ClaudeDesktop, "Claude Desktop", path,
                "Paste into claude_desktop_config.json, then restart Claude Desktop.", "mcpServers");
            var info = CreateJsonClient(desc, bridgePath, pythonPath);
            bool supportedPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (supportedPlatform)
            {
                info.SupportsAutoSetup = true;
            }
            else
            {
                info.SupportsAutoSetup = false;
                info.AutoSetupDisabledReason = "Claude Desktop Auto Setup is only supported on macOS and Windows. Use Copy Config and paste it into Claude Desktop's settings manually.";
            }
            return info;
        }

        private static NexusMcpClientInfo CreateClaudeCode(string projectRoot, string bridgePath, string pythonPath)
        {
            string path = Path.Combine(projectRoot, ".mcp.json");
            var desc = new JsonClientDescriptor(NexusMcpClientKind.ClaudeCode, "Claude Code", path,
                "Writes .mcp.json in the project root (uses the claude CLI when available). Run /mcp or restart Claude Code afterwards.", "mcpServers");
            return CreateJsonClient(desc, bridgePath, pythonPath);
        }

        private static NexusMcpClientInfo CreateJsonClient(in JsonClientDescriptor desc, string bridgePath, string pythonPath)
        {
            var info = BaseInfo(desc.Kind, desc.Name, bridgePath, pythonPath);
            info.Format = desc.RootKey == "servers" ? NexusMcpConfigFormat.JsonServers : NexusMcpConfigFormat.JsonMcpServers;
            info.ConfigPath = desc.Path;
            info.RootKey = desc.RootKey;
            info.Instruction = desc.Instruction;
            info.ConfigText = BuildJsonConfig(desc.RootKey, bridgePath, pythonPath);
            info.SupportsAutoSetup = true;
            ApplyJsonStatus(info);
            return info;
        }

        private static NexusMcpClientInfo CreateCliClient(in CliClientDescriptor desc, string bridgePath, string pythonPath)
        {
            var info = BaseInfo(desc.Kind, desc.Name, bridgePath, pythonPath);
            info.Format = NexusMcpConfigFormat.CliManaged;
            info.Instruction = desc.Instruction;
            info.ConfigPath = "Managed by " + desc.Command + " CLI";
            info.ConfigText = BuildJsonConfig("mcpServers", bridgePath, pythonPath);
            bool found = IsExecutableResolved(desc.Command);
            info.SupportsAutoSetup = found;
            info.Status = found ? NexusMcpClientStatus.Detected : NexusMcpClientStatus.NotFound;
            info.StatusDetail = found ? desc.Command + " CLI detected." : desc.Command + " CLI was not found on PATH.";
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
    }
}
