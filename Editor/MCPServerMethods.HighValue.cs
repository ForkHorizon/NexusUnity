using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static void RegisterHighValueMethods()
        {
            _methods["capture_inspector_screenshot"] = CaptureInspectorScreenshot;
            _methods["capture_game_view_screenshot"] = CaptureGameViewScreenshot;
            _methods["generate_mermaid_diagram"] = GenerateMermaidDiagram;
            _methods["semantic_find"] = SemanticFind;
        }

        private static JToken GenerateMermaidDiagram(JToken p)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("graph TD");

            HashSet<int> processed = new HashSet<int>();

            // Use ListPool to avoid GC allocation from array creation
            using (UnityEngine.Pool.ListPool<GameObject>.Get(out var roots))
            {
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(roots);
                foreach (var root in roots)
                {
                    BuildMermaidRecursive(root, sb, processed);
                }
            }

            return new JObject 
            { 
                ["status"] = "Success", 
                ["mermaid"] = sb.ToString() 
            };
        }

        private static void BuildMermaidRecursive(GameObject go, StringBuilder sb, HashSet<int> processed)
        {
            int id = go.GetRawId();
            if (processed.Contains(id)) return;
            processed.Add(id);

            string safeName = go.name.Replace("[", "(").Replace("]", ")").Replace("\"", "'");

            sb.Append("  node_");
            sb.Append(id);
            sb.Append("[\"");
            sb.Append(safeName);
            sb.AppendLine("\"]");

            using (UnityEngine.Pool.ListPool<Component>.Get(out var comps))
            {
                go.GetComponents(comps);
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    string compName = GetTypeName(comp.GetType());
                    if (compName == "Transform" || compName == "RectTransform") continue;

                    sb.Append("  node_");
                    sb.Append(id);
                    sb.Append(" --- comp_");
                    sb.Append(comp.GetRawId());
                    sb.Append("([\"");
                    sb.Append(compName);
                    sb.AppendLine("\"])");
                }
            }

            foreach (Transform child in go.transform)
            {
                sb.Append("  node_");
                sb.Append(id);
                sb.Append(" --> node_");
                sb.Append(child.gameObject.GetRawId());
                sb.AppendLine();
                BuildMermaidRecursive(child.gameObject, sb, processed);
            }
        }

    }
}
