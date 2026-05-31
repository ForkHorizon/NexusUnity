using System;
using System.IO;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static partial class NexusMcpConfigGenerator
    {
        private static readonly Regex BridgeVersionRegex = new Regex("\"version\"\\s*:\\s*\"([^\"]+)\"");

        private static void ApplyBridgeVersions(NexusMcpClientInfo info, string sourceBridgePath, string sourceBridgeVersion, string deployedBridgeVersion)
        {
            info.SourceBridgePath = sourceBridgePath;
            info.SourceBridgeVersion = sourceBridgeVersion;
            info.DeployedBridgeVersion = deployedBridgeVersion;
        }

        private static void ApplyDeploymentDriftStatus(NexusMcpClientInfo info)
        {
            if (info.Status == NexusMcpClientStatus.Error || info.Status == NexusMcpClientStatus.NotFound)
            {
                return;
            }

            if (info.Format == NexusMcpConfigFormat.ManualOnly)
            {
                ApplyManualBridgeDetail(info);
                return;
            }

            if (info.Format == NexusMcpConfigFormat.CliManaged)
            {
                ApplyCliBridgeDetail(info);
                return;
            }

            if (info.Status != NexusMcpClientStatus.Configured)
            {
                AppendDeploymentDetail(info);
                return;
            }

            ApplyConfiguredBridgeHealth(info);
        }

        private static void ApplyConfiguredBridgeHealth(NexusMcpClientInfo info)
        {
            if (!PathsEqual(info.ConfiguredBridgePath, info.BridgePath))
            {
                info.Status = NexusMcpClientStatus.Outdated;
                info.StatusDetail = "nexus-unity points to " + DisplayPath(info.ConfiguredBridgePath) +
                                    ". Current project bridge is " + DisplayPath(info.BridgePath) +
                                    ". Run Auto Setup, then restart this client.";
                return;
            }

            if (!File.Exists(info.BridgePath))
            {
                info.Status = NexusMcpClientStatus.Outdated;
                info.StatusDetail = "Config points to this project, but the deployed bridge file is missing. Run Auto Setup, then restart this client.";
                return;
            }

            if (HasVersionDrift(info))
            {
                info.Status = NexusMcpClientStatus.Outdated;
                info.StatusDetail = "Deployed bridge version " + info.DeployedBridgeVersion +
                                    " differs from package bridge version " + info.SourceBridgeVersion +
                                    ". Run Auto Setup, then restart this client.";
                return;
            }

            info.StatusDetail = "Configured for this project. " + BridgeVersionDetail(info);
        }

        private static void ApplyManualBridgeDetail(NexusMcpClientInfo info)
        {
            info.StatusDetail = "Manual config is always available. " + BridgeVersionDetail(info);
        }

        private static void ApplyCliBridgeDetail(NexusMcpClientInfo info)
        {
            if (!File.Exists(info.BridgePath) || HasVersionDrift(info))
            {
                info.Status = NexusMcpClientStatus.Outdated;
                info.StatusDetail = "The project-root bridge used by generated CLI config is missing or stale. Run Auto Setup, then restart this client.";
                return;
            }

            info.StatusDetail = info.StatusDetail + " " + BridgeVersionDetail(info);
        }

        private static void AppendDeploymentDetail(NexusMcpClientInfo info)
        {
            if (string.IsNullOrEmpty(info.StatusDetail)) info.StatusDetail = BridgeVersionDetail(info);
            else info.StatusDetail = info.StatusDetail + " " + BridgeVersionDetail(info);
        }

        private static bool HasVersionDrift(NexusMcpClientInfo info)
        {
            return !string.IsNullOrEmpty(info.SourceBridgeVersion) &&
                   !string.IsNullOrEmpty(info.DeployedBridgeVersion) &&
                   !string.Equals(info.SourceBridgeVersion, info.DeployedBridgeVersion, StringComparison.Ordinal);
        }

        private static string BridgeVersionDetail(NexusMcpClientInfo info)
        {
            if (!File.Exists(info.BridgePath)) return "Bridge has not been deployed to the project root.";
            if (!string.IsNullOrEmpty(info.SourceBridgeVersion) && !string.IsNullOrEmpty(info.DeployedBridgeVersion))
            {
                return "Bridge " + info.DeployedBridgeVersion + " is deployed from package " + info.SourceBridgeVersion + ".";
            }
            if (!string.IsNullOrEmpty(info.DeployedBridgeVersion)) return "Bridge " + info.DeployedBridgeVersion + " is deployed.";
            return "Bridge is deployed, but its version could not be read.";
        }

        internal static string GetBridgeDeploymentSummary(string bridgePath, string sourceBridgePath)
        {
            string deployedVersion = ReadBridgeVersion(bridgePath);
            string sourceVersion = ReadBridgeVersion(sourceBridgePath);
            string normalizedBridgePath = NormalizePath(bridgePath);

            if (!File.Exists(normalizedBridgePath))
            {
                return "Bridge path: " + normalizedBridgePath + " (not deployed)";
            }

            if (!string.IsNullOrEmpty(sourceVersion) && !string.IsNullOrEmpty(deployedVersion) &&
                !string.Equals(sourceVersion, deployedVersion, StringComparison.Ordinal))
            {
                return "Bridge path: " + normalizedBridgePath + " (outdated " + deployedVersion + ", package " + sourceVersion + ")";
            }

            if (!string.IsNullOrEmpty(deployedVersion))
            {
                return "Bridge path: " + normalizedBridgePath + " (version " + deployedVersion + ")";
            }

            return "Bridge path: " + normalizedBridgePath + " (version unknown)";
        }

        internal static string ReadBridgeVersion(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                string text = File.ReadAllText(path);
                var match = BridgeVersionRegex.Match(text);
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = NormalizeComparablePath(left);
            string normalizedRight = NormalizeComparablePath(right);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeComparablePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            try
            {
                return Path.GetFullPath(path).Replace("\\", "/").TrimEnd('/');
            }
            catch
            {
                return NormalizePath(path).TrimEnd('/');
            }
        }

        private static string DisplayPath(string path)
        {
            return string.IsNullOrEmpty(path) ? "(missing)" : NormalizePath(path);
        }
    }
}
