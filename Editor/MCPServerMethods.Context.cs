namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterContextMethods()
        {
            _methods["get_selected_object_full_context"] = GetSelectedObjectFullContext;
            _methods["show_unresolved_missing_references"] = ShowUnresolvedMissingReferences;
        }
    }
}
