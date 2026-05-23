using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class MCPCliInstaller
    {
        internal static bool DeployBridgeForUi(out string destinationPath)
        {
            return DeployBridgeScript(out destinationPath);
        }

        internal static string ResolveExecutablePathForUi(string name)
        {
            return ResolveExecutablePath(name);
        }

        internal static string GetProjectRootForUi()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        internal static string GetDefaultBridgePathForUi()
        {
            return Path.Combine(GetProjectRootForUi(), "nexus_unity_bridge.py");
        }
    }
}
