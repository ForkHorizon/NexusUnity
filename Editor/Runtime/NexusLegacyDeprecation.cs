// Legacy HTTP sunset ledger reported by get_server_status.
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor.Runtime
{
    /// <summary>
    /// Legacy HTTP/MCP sunset policy. This is not removal: HTTP stays fully supported until published gates pass.
    /// </summary>
    public static class NexusLegacyDeprecation
    {
        /// <summary>Human-readable removal prerequisites.</summary>
        public const string RemovalPrerequisite =
            "Non-experimental com.unity.pipeline and one Nexus stable release after Pipeline-primary auto.";

        /// <summary>Unity CLI gate detail. The Editor never runs the unity executable, so the host CLI is not probed.</summary>
        public const string UnityCliNote = "not_probed_in_editor (1.0.x GA released upstream)";

        /// <summary>Gets whether Legacy HTTP is fully deprecated. False while maturity gates fail.</summary>
        public static bool IsFullyDeprecated => false;

        /// <summary>Gets whether Legacy HTTP remains a supported compatibility backend.</summary>
        public static bool StillSupported => true;

        /// <summary>
        /// True when a package version is missing or carries a SemVer pre-release suffix (exp, pre, preview, alpha, beta).
        /// </summary>
        public static bool IsPreviewVersion(string version)
        {
            return string.IsNullOrEmpty(version) || version.IndexOf('-') >= 0;
        }

        /// <summary>
        /// Builds the additive Legacy sunset object for <c>get_server_status</c>.
        /// </summary>
        public static JObject ToStatusJson(RuntimeCapabilitySnapshot snapshot)
        {
            bool pipelineGa = snapshot != null &&
                snapshot.PipelinePackagePresent &&
                !snapshot.PipelineExperimental &&
                !IsPreviewVersion(snapshot.PipelinePackageVersion);
            return new JObject
            {
                ["deprecated"] = IsFullyDeprecated,
                ["still_supported"] = StillSupported,
                ["recommended_primary"] = "pipeline",
                ["sunset_status"] = "announced",
                ["removal"] = "not_scheduled",
                ["requires"] = RemovalPrerequisite,
                ["unity_cli"] = new JObject
                {
                    ["detected"] = false,
                    ["version"] = (string)null,
                    ["prerelease"] = null,
                    ["stable_1_0_or_newer"] = null,
                    ["note"] = UnityCliNote
                },
                ["gates"] = new JObject
                {
                    ["unity_cli_stable"] = Gate(true, UnityCliNote),
                    ["pipeline_non_experimental"] = Gate(pipelineGa,
                        string.IsNullOrEmpty(snapshot?.PipelinePackageVersion)
                            ? "com.unity.pipeline is not installed."
                            : "com.unity.pipeline " + snapshot.PipelinePackageVersion +
                              (pipelineGa ? " (non-experimental)." : " (pre-release).")),
                    ["command_api_registered"] = Gate(snapshot != null && snapshot.NexusCommandsRegistered,
                        "Canonical Nexus commands must remain registered for Pipeline projection."),
                    ["unity_version_supported"] = Gate(snapshot != null && snapshot.UnityVersionSupported,
                        "Nexus currently targets Unity 6000.x."),
                    ["capture_nexus_owned"] = Gate(true,
                        "ICaptureGateway remains the capture implementation."),
                    ["stable_release_cycle_after_m4"] = Gate(false,
                        "M4 has not shipped in a stable Nexus Unity release yet.")
                }
            };
        }

        private static JObject Gate(bool passed, string detail)
        {
            return new JObject { ["passed"] = passed, ["detail"] = detail };
        }
    }
}
