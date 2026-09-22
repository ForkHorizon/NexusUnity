using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityMCP.Editor.Commands
{
    /// <summary>Structured result for <see cref="ProjectMapCommand"/>.</summary>
    [Serializable]
    public sealed class NexusProjectMapResult
    {
        /// <summary>Gets or sets whether the command succeeded.</summary>
        [JsonProperty("success")] public bool Success { get; set; }
        /// <summary>Gets or sets the Unity product name.</summary>
        [JsonProperty("project_name")] public string ProjectName { get; set; }
        /// <summary>Gets or sets the project root path.</summary>
        [JsonProperty("project_path")] public string ProjectPath { get; set; }
        /// <summary>Gets or sets the running Unity editor version.</summary>
        [JsonProperty("unity_version")] public string UnityVersion { get; set; }
        /// <summary>Gets or sets the active scene path.</summary>
        [JsonProperty("active_scene")] public string ActiveScene { get; set; }
        /// <summary>Gets or sets the player color space.</summary>
        [JsonProperty("color_space")] public string ColorSpace { get; set; }
        /// <summary>Gets or sets the active graphics device.</summary>
        [JsonProperty("graphics_device")] public string GraphicsDevice { get; set; }
        /// <summary>Gets or sets the current render pipeline name.</summary>
        [JsonProperty("render_pipeline")] public string RenderPipeline { get; set; }
        /// <summary>Gets or sets the number of build scenes.</summary>
        [JsonProperty("build_scene_count")] public int BuildSceneCount { get; set; }
        /// <summary>Gets or sets listed build scene paths.</summary>
        [JsonProperty("sample_scenes")] public List<string> SampleScenes { get; set; }
        /// <summary>Gets or sets a short list of loaded Unity assemblies.</summary>
        [JsonProperty("key_assemblies")] public List<string> KeyAssemblies { get; set; }
        /// <summary>Gets or sets handler execution time in milliseconds.</summary>
        [JsonProperty("execution_duration_ms")] public double ExecutionDurationMs { get; set; }
    }

    /// <summary>Structured result for <see cref="GroupCompileErrorsCommand"/>.</summary>
    [Serializable]
    public sealed class NexusCompileErrorsResult
    {
        /// <summary>Gets or sets whether the command succeeded.</summary>
        [JsonProperty("success")] public bool Success { get; set; }
        /// <summary>Gets or sets the number of CSxxxx errors found.</summary>
        [JsonProperty("total_errors")] public int TotalErrors { get; set; }
        /// <summary>Gets or sets the number of file groups.</summary>
        [JsonProperty("group_count")] public int GroupCount { get; set; }
        /// <summary>Gets or sets whether the editor is currently compiling.</summary>
        [JsonProperty("is_compiling")] public bool IsCompiling { get; set; }
        /// <summary>Gets or sets errors grouped by source file.</summary>
        [JsonProperty("groups")] public List<NexusCompileErrorGroup> Groups { get; set; }
        /// <summary>Gets or sets handler execution time in milliseconds.</summary>
        [JsonProperty("execution_duration_ms")] public double ExecutionDurationMs { get; set; }
    }

    /// <summary>One compiler-error group keyed by source file.</summary>
    [Serializable]
    public sealed class NexusCompileErrorGroup
    {
        /// <summary>Gets or sets the source file path or <c>Unknown</c>.</summary>
        [JsonProperty("file")] public string File { get; set; }
        /// <summary>Gets or sets how many errors were grouped under this file.</summary>
        [JsonProperty("count")] public int Count { get; set; }
        /// <summary>Gets or sets the grouped error messages.</summary>
        [JsonProperty("errors")] public List<string> Errors { get; set; }
    }

    /// <summary>Structured result for <see cref="CaptureGameViewCommand"/>.</summary>
    [Serializable]
    public sealed class NexusCaptureResult
    {
        /// <summary>Gets or sets whether capture succeeded.</summary>
        [JsonProperty("success")] public bool Success { get; set; }
        /// <summary>Gets or sets encoded width in pixels.</summary>
        [JsonProperty("width")] public int Width { get; set; }
        /// <summary>Gets or sets encoded height in pixels.</summary>
        [JsonProperty("height")] public int Height { get; set; }
        /// <summary>Gets or sets <c>jpg</c> or <c>png</c>.</summary>
        [JsonProperty("encoding")] public string Encoding { get; set; }
        /// <summary>Gets or sets compressed payload size in bytes.</summary>
        [JsonProperty("bytes")] public int Bytes { get; set; }
        /// <summary>Gets or sets Base64 image bytes for transport consumers.</summary>
        [JsonProperty("base64")] public string Base64 { get; set; }
        /// <summary>Gets or sets the capture source label.</summary>
        [JsonProperty("source")] public string Source { get; set; }
        /// <summary>Gets or sets source acquisition milliseconds.</summary>
        [JsonProperty("acquisition_ms")] public double AcquisitionMs { get; set; }
        /// <summary>Gets or sets readback submit milliseconds.</summary>
        [JsonProperty("submit_ms")] public double SubmitMs { get; set; }
        /// <summary>Gets or sets submit-to-done milliseconds.</summary>
        [JsonProperty("wait_ms")] public double WaitMs { get; set; }
        /// <summary>Gets or sets get-data milliseconds when measured separately.</summary>
        [JsonProperty("getdata_ms")] public double GetDataMs { get; set; }
        /// <summary>Gets or sets encode milliseconds.</summary>
        [JsonProperty("encode_ms")] public double EncodeMs { get; set; }
        /// <summary>Gets or sets Base64 encoding milliseconds.</summary>
        [JsonProperty("base64_ms")] public double Base64Ms { get; set; }
        /// <summary>Gets or sets observed main-thread stall milliseconds.</summary>
        [JsonProperty("main_thread_stall_ms")] public double MainThreadStallMs { get; set; }
        /// <summary>Gets or sets total handler milliseconds including Base64.</summary>
        [JsonProperty("total_ms")] public double TotalMs { get; set; }
        /// <summary>Gets or sets Editor update ticks from readback submit until done.</summary>
        [JsonProperty("editor_ticks_submit_to_done")] public int EditorTicksSubmitToDone { get; set; }
    }
}
