using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Minimal curated tool profiles. Aliases stay dispatchable without being advertised twice.
    /// </summary>
    public static class NexusToolCatalog
    {
        /// <summary>Small status and intelligence surface.</summary>
        public const string Core = "core";
        /// <summary>Game View / screenshot surface.</summary>
        public const string Visual = "visual";
        /// <summary>High-level scene context surface.</summary>
        public const string Scene = "scene";
        /// <summary>Minimal fallback when Pipeline is absent.</summary>
        public const string Compat = "compat";

        private static readonly HashSet<string> CoreTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "get_server_status", "list_tools", "initialize",
            ProjectMapCommand.Id, GroupCompileErrorsCommand.Id
        };

        private static readonly HashSet<string> VisualTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CaptureGameViewCommand.Id, "capture_game_view_screenshot"
        };

        private static readonly HashSet<string> SceneTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dump_scene_graph", "compact_scene_snapshot", "get_scene_dependencies", "generate_mermaid_diagram"
        };

        private static readonly HashSet<string> CompatTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "get_server_status", "list_tools", "initialize", "capture_game_view_screenshot"
        };

        /// <summary>
        /// True when <paramref name="toolName"/> should appear in the model-visible schema for <paramref name="profile"/>.
        /// Empty profile advertises the full catalog except duplicate canonical aliases.
        /// </summary>
        public static bool IsVisible(string toolName, string profile)
        {
            if (string.IsNullOrEmpty(profile)) return true;
            if (profile.Equals(Core, StringComparison.OrdinalIgnoreCase)) return CoreTools.Contains(toolName);
            if (profile.Equals(Visual, StringComparison.OrdinalIgnoreCase)) return VisualTools.Contains(toolName);
            if (profile.Equals(Scene, StringComparison.OrdinalIgnoreCase)) return SceneTools.Contains(toolName);
            if (profile.Equals(Compat, StringComparison.OrdinalIgnoreCase)) return CompatTools.Contains(toolName);
            return false;
        }
    }
}
