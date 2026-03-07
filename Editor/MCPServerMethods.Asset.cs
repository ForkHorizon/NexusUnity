#pragma warning disable 0618 // Suppress obsolete InstanceIDToObject/GetInstanceID warnings for stability in 2021.3+
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
        private static void RegisterAssetMethods()
        {
            _methods["move_asset"] = MoveAsset;
            _methods["delete_asset"] = DeleteAsset;
            _methods["copy_asset"] = CopyAsset;
            _methods["get_dependencies"] = GetDependencies;
            _methods["create_folder"] = CreateFolder;
            _methods["list_assets"] = ListAssets;
            _methods["create_material"] = CreateMaterial;
            _methods["refresh_asset_database"] = RefreshAssetDatabase;
            _methods["import_asset"] = ImportAsset;
            _methods["create_prefab"] = CreatePrefab;
            _methods["apply_prefab_overrides"] = ApplyPrefabOverrides;
            _methods["revert_prefab_overrides"] = RevertPrefabOverrides;
        }

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
            AssetDatabase.SaveAssets();
            return $"Created material at {path}";
        }

        private static JToken RefreshAssetDatabase(JToken p)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            return new JObject 
            { 
                ["status"] = "Refreshed", 
                ["is_compiling"] = EditorApplication.isCompiling,
                ["is_updating"] = EditorApplication.isUpdating
            };
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
            AssetDatabase.SaveAssets();
            return $"Prefab created at {p["path"]}";
        }

        private static JToken ApplyPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            return "Overrides applied";
        }

        private static JToken RevertPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = EditorUtility.InstanceIDToObject((int)p["instance_id"]) as GameObject;
            PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);
            return "Overrides reverted";
        }

        /// <summary>Moves or renames an asset, merging directories if necessary.</summary>
        private static JToken MoveAsset(JToken p)
        {
            if (p?["old_path"] == null || p["new_path"] == null) throw new Exception("old_path and new_path required");
            
            string oldPath = p["old_path"].ToString();
            string newPath = p["new_path"].ToString();

            if (AssetDatabase.IsValidFolder(oldPath) && AssetDatabase.IsValidFolder(newPath))
            {
                MergeDirectories(oldPath, newPath);
                return "OK (Merged)";
            }

            string result = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(result)) throw new Exception(result);
            return "OK";
        }

        private static void MergeDirectories(string sourceDir, string targetDir)
        {
            var files = Directory.GetFiles(sourceDir);
            foreach (var file in files)
            {
                if (file.EndsWith(".meta")) continue;
                
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                
                string result = AssetDatabase.MoveAsset(file.Replace("\\", "/"), destFile);
                if (!string.IsNullOrEmpty(result)) throw new Exception($"Failed to move {fileName}: {result}");
            }

            var dirs = Directory.GetDirectories(sourceDir);
            foreach (var dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(targetDir, dirName).Replace("\\", "/");
                
                if (!AssetDatabase.IsValidFolder(destDir))
                {
                    AssetDatabase.CreateFolder(targetDir, dirName);
                }
                
                MergeDirectories(dir.Replace("\\", "/"), destDir);
            }

            AssetDatabase.DeleteAsset(sourceDir);
        }

        /// <summary>Deletes an asset file.</summary>
        private static JToken DeleteAsset(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            if (!AssetDatabase.DeleteAsset(p["path"].ToString())) throw new Exception("Delete failed");
            return "OK";
        }

        /// <summary>Duplicates an asset file.</summary>
        private static JToken CopyAsset(JToken p)
        {
            if (p?["source_path"] == null || p["dest_path"] == null) throw new Exception("source_path and dest_path required");
            if (!AssetDatabase.CopyAsset(p["source_path"].ToString(), p["dest_path"].ToString())) throw new Exception("Copy failed");
            return "OK";
        }

        /// <summary>Returns all assets required by a target asset.</summary>
        private static JToken GetDependencies(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            bool recursive = p["recursive"]?.Value<bool>() ?? true;
            var deps = AssetDatabase.GetDependencies(p["path"].ToString(), recursive);
            return new JArray(deps);
        }

        /// <summary>Creates a new folder in the project.</summary>
        private static JToken CreateFolder(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required (e.g., 'Assets/NewFolder')");
            string path = p["path"].ToString();
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid)) throw new Exception("Failed to create folder");
            return "OK";
        }
    }
}