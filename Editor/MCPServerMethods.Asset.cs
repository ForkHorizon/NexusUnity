using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Partial implementation of MCPServerMethods handling Asset manipulation.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static JToken ListAssets(JToken p)
        {
            string filter = p?["filter"]?.ToString() ?? "";
            string[] folders = p?["folders"]?.ToObject<string[]>();
            var guids = AssetDatabase.FindAssets(filter, folders);
            return new JArray(guids.Select(AssetDatabase.GUIDToAssetPath));
        }

        private static JToken CreateMaterial(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();
            string shader = p["shader"]?.ToString() ?? "Standard";
            Material mat = new Material(Shader.Find(shader));
            string path = Path.Combine("Assets", $"{name}.mat");
            AssetDatabase.CreateAsset(mat, path);
            return $"Created material at {path}";
        }

        private static JToken RefreshAssetDatabase(JToken p)
        {
            AssetDatabase.Refresh();
            return "Refreshed";
        }

        private static JToken ImportAsset(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            AssetDatabase.ImportAsset(p["path"].ToString());
            return "Imported";
        }

        private static JToken CreatePrefab(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["path"] == null) throw new Exception("instance_id and path required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            PrefabUtility.SaveAsPrefabAsset(go, p["path"].ToString());
            return $"Prefab created at {p["path"]}";
        }

        private static JToken ApplyPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            return "Overrides applied";
        }

        private static JToken RevertPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);
            return "Overrides reverted";
        }
    }
}
