namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        /// <summary>
        /// Identifies the kind of reflected C# symbol returned by the Unity editor symbol index.
        /// </summary>
        /// <remarks>
        /// Class entries describe compiled types, Method entries describe invocable members exposed for editor inspection,
        /// and Field entries describe reflected fields discovered while scanning Unity assemblies and project scripts.
        /// </remarks>
        public enum SymbolType { Class, Method, Field }

        /// <summary>
        /// Carries reflected C# symbol metadata used by Nexus Unity search and method invocation tools.
        /// </summary>
        /// <remarks>
        /// Values are populated from Unity's loaded assemblies on a background indexing task. Metadata stores serialized
        /// reflection details used by editor search results and JSON-RPC method invocation.
        /// </remarks>
        public struct SymbolInfo
        {
            public string Name;
            public string Namespace;
            public string DeclaringType;
            public SymbolType Type;
            public string Metadata;
        }
    }
}
