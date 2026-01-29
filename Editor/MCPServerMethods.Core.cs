using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling Core system and log features.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static JToken Initialize(JToken p) => new JObject { ["protocolVersion"] = "2024-11-05", ["serverInfo"] = new JObject { ["name"] = "Unity MCP Server", ["version"] = "1.6.1" } };

        private static JToken CreatePrimitive(JToken p)
        {
            if (!Enum.TryParse(typeof(PrimitiveType), p["primitive_type"].ToString(), true, out var type)) throw new Exception("Invalid primitive");
            var go = GameObject.CreatePrimitive((PrimitiveType)type);
            Undo.RegisterCreatedObjectUndo(go, "Create Primitive");
            Selection.activeGameObject = go;
            return $"Created {go.name}";
        }

        private static JToken AttachScript(JToken p)
        {
            string name = SanitizeScriptName(p["script_name"].ToString());
            string content = p["script_content"]?.ToString().Replace("\\n", "\n") ?? GetDefaultScript(name);
            File.WriteAllText(Path.Combine("Assets", $"{name}.cs"), content);
            if (Selection.activeGameObject != null)
            {
                SessionState.SetString("MCP_PendingAttach_Script", name);
                SessionState.SetInt("MCP_PendingAttach_GO", Selection.activeGameObject.GetInstanceID());
            }
            AssetDatabase.Refresh();
            return "Script created and compilation triggered";
        }

        private static JToken ReadLogs(JToken p) => JArray.FromObject(MCPServerWindow.GetLogs((int)(p?["count"] ?? 50), p?["filter_type"]?.ToString(), p?["search_text"]?.ToString()));
        private static JToken ClearLogs(JToken p) { MCPServerWindow.ClearLogs(); return "Logs cleared"; }
        private static JToken TestCoroutine(JToken p) { MCPServerWindow.StartCoroutine(WaitAndLog()); return "Started"; }

        private static JToken ListTools(JToken p)
        {
            return new JArray(
                new JObject { ["name"] = "unity_create_primitive", ["description"] = "Create Cube, Sphere, etc.", ["inputSchema"] = GetPrimitiveSchema() },
                new JObject { ["name"] = "unity_find_objects", ["description"] = "Search objects by name/tag", ["inputSchema"] = GetSearchSchema() },
                new JObject { ["name"] = "unity_set_transform", ["description"] = "Move objects", ["inputSchema"] = GetTransformSchema() },
                new JObject { ["name"] = "unity_read_logs", ["description"] = "Get Unity console", ["inputSchema"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["count"] = new JObject { ["type"] = "integer" } } } }
            );
        }

        private static JObject GetPrimitiveSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject { ["primitive_type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad") } }, ["required"] = new JArray("primitive_type") };
        private static JObject GetSearchSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject { ["name"] = new JObject { ["type"] = "string" }, ["tag"] = new JObject { ["type"] = "string" }, ["type"] = new JObject { ["type"] = "string" } } };
        private static JObject GetTransformSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject { ["instance_id"] = new JObject { ["type"] = "integer" }, ["position"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["x"] = new JObject { ["type"] = "number" }, ["y"] = new JObject { ["type"] = "number" }, ["z"] = new JObject { ["type"] = "number" } } } }, ["required"] = new JArray("instance_id") };

        private static System.Collections.IEnumerator WaitAndLog() { yield return new Unity.EditorCoroutines.Editor.EditorWaitForSeconds(2); Debug.Log("[MCP] Coroutine complete"); }

        private static string SanitizeScriptName(string n) => System.Text.RegularExpressions.Regex.Replace(n, @"[^a-zA-Z0-9_]", "_");
        private static string GetDefaultScript(string n) => $"using UnityEngine;\npublic class {n} : MonoBehaviour {{ void Start() {{ Debug.Log(\"Hello from {n}\"); }} }}";
    }
}
