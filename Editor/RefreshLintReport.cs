using UnityEditor;
using UnityMCP.Editor;

public static class RefreshLintReport
{
    [MenuItem("Tools/Nexus/Refresh Lint Report")]
    public static void Refresh()
    {
        ProjectAuditor.RunAudit();
    }
}
