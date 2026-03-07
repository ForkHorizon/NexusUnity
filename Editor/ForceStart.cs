using UnityEditor;
using UnityMCP.Editor;

[InitializeOnLoad]
public static class ForceStartServer
{
    static ForceStartServer()
    {
        EditorApplication.update += OneTimeUpdate;
    }

    private static void OneTimeUpdate()
    {
        EditorApplication.update -= OneTimeUpdate;
        MCPServer.Start();
    }
}
