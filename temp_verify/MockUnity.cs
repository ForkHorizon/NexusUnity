using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Debug
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) { }
    }
}

namespace UnityEditor
{
    public class EditorApplication
    {
        public static bool isCompiling = false;
        public static bool isUpdating = false;
    }
}

namespace UnityMCP.Editor
{
    public static class MCPServer
    {
        public static void Enqueue(Action a) { a(); }
    }

    public static partial class MCPServerMethods
    {
        private static void ClearCache() {}
        private static void RegisterCoreMethods() {}
        private static void RegisterSceneMethods() {}
        private static void RegisterDiscoveryMethods() {}
        private static void RegisterEditorMethods() {}
        private static void RegisterAssetMethods() {}
        private static void RegisterHierarchyMethods() {}
        private static void RegisterComponentMethods() {}
        private static void RegisterSerializationMethods() {}
        private static void RegisterUIMethods() {}
        private static void RegisterHighValueMethods() {}
        private static void RegisterPlayerPrefsMethods() {}
        private static void RegisterScriptableObjectMethods() {}
        private static void RegisterReflectionMethods() {}
        private static void RegisterSyncMethods() {}
        private static void RegisterInputMethods() {}
        private static void RegisterSnapshotMethods() {}
        private static void RegisterTimelineMethods() {}
        private static void RegisterContextMethods() {}
        private static void RegisterDeltaMethods() {}
    }
}
