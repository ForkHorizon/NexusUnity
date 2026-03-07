using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class ProjectAuditor
    {
        [MenuItem("Window/Nexus Unity/Run Code Audit")]
        public static void RunAuditMenu()
        {
            Debug.Log("Project Auditor is temporarily disabled for stability.");
        }

        public static string RunAudit(bool silent)
        {
            return "Project Auditor is temporarily disabled.";
        }
    }
}
