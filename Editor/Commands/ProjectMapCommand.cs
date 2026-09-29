using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor.Commands
{
    /// <summary>
    /// Canonical handler for curated project intelligence. Reads Unity editor project, scene, and graphics state.
    /// </summary>
    public sealed class ProjectMapCommand : INexusCommand
    {
        /// <summary>Canonical command id.</summary>
        public const string Id = "nexus.project_map";
        /// <summary>HTTP / Unity CLI alias used by the hybrid proof of concept.</summary>
        public const string Alias = "nexus_project_map";
        /// <summary>Shared command description for every projection.</summary>
        public const string Description =
            "Generates a curated high-level project intelligence map for AI agents.";

        /// <summary>Gets the canonical project-map descriptor.</summary>
        public NexusCommandDescriptor Descriptor { get; } = new NexusCommandDescriptor
        {
            Id = Id,
            Aliases = new[] { Alias },
            Title = "Project map",
            Description = Description,
            Profiles = new[] { "core" },
            Parameters = Array.Empty<NexusCommandParameter>()
        };

        /// <summary>
        /// Executes the project map command and returns JSON for Legacy HTTP/MCP.
        /// Completes synchronously (already-completed task); the async signature only matches <see cref="INexusCommand"/>.
        /// </summary>
        /// <param name="parameters">Unused; the command has no arguments.</param>
        /// <param name="cancellationToken">Unused; the command completes synchronously and cannot be cancelled.</param>
        /// <returns>Structured project map JSON.</returns>
        public Task<JToken> ExecuteAsync(JToken parameters, CancellationToken cancellationToken)
        {
            return Task.FromResult(JToken.FromObject(Execute()));
        }

        /// <summary>
        /// Builds the project map from the current Unity editor project.
        /// </summary>
        /// <returns>Curated project overview.</returns>
        public static NexusProjectMapResult Execute()
        {
            var sw = Stopwatch.StartNew();
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string activeScene = EditorSceneManager.GetActiveScene().path;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.FullName.StartsWith("Unity", StringComparison.Ordinal))
                .Select(a => a.GetName().Name)
                .Take(25)
                .ToList();
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            sw.Stop();
            return new NexusProjectMapResult
            {
                Success = true,
                ProjectName = Application.productName,
                ProjectPath = projectPath,
                UnityVersion = Application.unityVersion,
                ActiveScene = string.IsNullOrEmpty(activeScene) ? "Untitled / Unsaved" : activeScene,
                ColorSpace = PlayerSettings.colorSpace.ToString(),
                GraphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                RenderPipeline = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.GetType().Name
                    : "Built-in",
                BuildSceneCount = scenes.Count,
                SampleScenes = scenes,
                KeyAssemblies = assemblies,
                ExecutionDurationMs = sw.Elapsed.TotalMilliseconds
            };
        }
    }
}
