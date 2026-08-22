using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken SemanticFind(JToken p)
        {
            string query = p?["query"]?.ToString();
            if (string.IsNullOrEmpty(query)) throw new Exception("query parameter required");
            var matches = FindSemanticMatches(query);
            return new JObject
            {
                ["status"] = "Success",
                ["matches"] = new JArray(matches.OrderByDescending(match => (int)match["score"]).Take(20))
            };
        }

        private static List<JObject> FindSemanticMatches(string query)
        {
            var matches = new List<JObject>();
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);
            using (UnityEngine.Pool.ListPool<Component>.Get(out var components))
            {
                foreach (var go in allGameObjects)
                {
                    var match = CreateSemanticMatch(go, query, components);
                    if (match != null) matches.Add(match);
                }
            }
            return matches;
        }

        private static JObject CreateSemanticMatch(GameObject go, string query, List<Component> components)
        {
            if (!go.scene.IsValid() || !go.scene.isLoaded) return null;
            int score = go.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? 50 : 0;
            var reasons = new List<string>();
            if (score > 0) reasons.Add("Name match");

            go.GetComponents(components);
            foreach (var component in components)
            {
                if (component != null) AddComponentMatches(component, query, reasons, ref score);
            }
            return score == 0 ? null : new JObject
            {
                ["name"] = go.name,
                ["instance_id"] = go.GetRawId(),
                ["score"] = score,
                ["reasons"] = new JArray(reasons.Distinct())
            };
        }

        private static void AddComponentMatches(Component component, string query, List<string> reasons, ref int score)
        {
            string typeName = GetTypeName(component.GetType());
            if (typeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 30;
                reasons.Add($"Component type match: {typeName}");
            }
            AddFieldMatches(component.GetType(), query, typeName, reasons, ref score);
            AddMethodMatches(component.GetType(), query, typeName, reasons, ref score);
        }

        private static void AddFieldMatches(Type type, string query, string typeName, List<string> reasons, ref int score)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                score += 10;
                reasons.Add($"Field match: {typeName}.{field.Name}");
            }
        }

        private static void AddMethodMatches(Type type, string query, string typeName, List<string> reasons, ref int score)
        {
            var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                score += 15;
                reasons.Add($"Method match: {typeName}.{method.Name}()");
            }
        }
    }
}
