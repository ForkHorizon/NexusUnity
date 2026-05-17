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
    /// Handles the autonomous background compilation by waiting for OS focus.
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
            _methods["explore_asset"] = ExploreAsset;
            _methods["create_material"] = CreateMaterial;
            _methods["refresh_asset_database"] = RefreshAssetDatabase;
            _methods["import_asset"] = ImportAsset;
            _methods["create_prefab"] = CreatePrefab;
            _methods["apply_prefab_overrides"] = ApplyPrefabOverrides;
            _methods["revert_prefab_overrides"] = RevertPrefabOverrides;
            _methods["get_prefab_overrides"] = GetPrefabOverrides;
            _methods["edit_prefab_asset"] = EditPrefabAsset;
        }

        private static JToken ExploreAsset(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            string path = ValidateAssetPath(p["path"].ToString());
            
            string mainGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(mainGuid)) throw new Exception($"Asset not found at path: {path}");

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (allAssets == null || allAssets.Length == 0) throw new Exception($"No assets loaded at path: {path}");

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            
            JObject result = new JObject { ["guid"] = mainGuid };
            JArray subAssetsArr = new JArray();

            foreach (var asset in allAssets)
            {
                if (asset == null) continue;
                
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId))
                {
                    var assetData = new JObject
                    {
                        ["name"] = asset.name,
                        ["type"] = asset.GetType().Name,
                        ["file_id"] = fileId,
                        ["instance_id"] = asset.GetRawId()
                    };
                    
                    if (asset == mainAsset)
                    {
                        result["main_asset"] = assetData;
                    }
                    else
                    {
                        subAssetsArr.Add(assetData);
                    }
                }
            }
            
            result["sub_assets"] = subAssetsArr;
            return result;
        }

        private static JToken ListAssets(JToken p)
        {
            string filter = p?["filter"]?.ToString() ?? "";
            string[] folders = p?["folders"]?.ToObject<string[]>();
            var guids = AssetDatabase.FindAssets(filter, folders);
            return new JObject { ["assets"] = new JArray(guids.Select(AssetDatabase.GUIDToAssetPath)) };
        }

        private static JToken CreateMaterial(JToken p)
        {
            if (p == null || p["name"] == null) throw new Exception("name is required");
            string name = p["name"].ToString();
            string shader = p["shader"]?.ToString() ?? "Standard";
            Material mat = new Material(Shader.Find(shader));
            string path = ValidateAssetPath(Path.Combine("Assets", $"{name}.mat"));
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = "Success", ["path"] = path };
        }

        private static JToken RefreshAssetDatabase(JToken p)
        {
            #if UNITY_EDITOR_OSX
            // Signal AppNapBypass to bring Unity to the foreground so compilation can happen.
            AppNapBypass.ScheduleActivation();

            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
            }
            #else
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            #endif

            // Check for immediate compiler errors in the log
            var logs = MCPServer.GetLogs(20, "Error", "");
            var compilerErrors = logs.Where(l => l.Message.Contains("error CS")).Select(l => l.Message).ToList();

            return new JObject 
            { 
                ["status"] = compilerErrors.Count > 0 ? "Error" : (EditorApplication.isCompiling ? "Compiling" : "Success"), 
                ["is_compiling"] = EditorApplication.isCompiling,
                ["is_updating"] = EditorApplication.isUpdating,
                ["compiler_errors"] = new JArray(compilerErrors)
            };
        }

        private static JToken ImportAsset(JToken p)
        {
            if (p == null || p["path"] == null) throw new Exception("path is required");
            string path = ValidateAssetPath(p["path"].ToString());
            AssetDatabase.ImportAsset(path);
            return new JObject { ["status"] = "Success", ["message"] = "Imported" };
        }






        // Helper for isolated prefab asset edits

        private static JToken MoveAsset(JToken p)
        {
            if (p?["old_path"] == null || p["new_path"] == null) throw new Exception("old_path and new_path required");
            
            string oldPath = ValidateAssetPath(p["old_path"].ToString());
            string newPath = ValidateAssetPath(p["new_path"].ToString());

            if (AssetDatabase.IsValidFolder(oldPath) && AssetDatabase.IsValidFolder(newPath))
            {
                MergeDirectories(oldPath, newPath);
                return new JObject { ["status"] = "Success", ["message"] = "OK (Merged)" };
            }

            string result = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(result)) throw new Exception(result);
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
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

        private static JToken DeleteAsset(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            string path = ValidateAssetPath(p["path"].ToString());
            if (!AssetDatabase.DeleteAsset(path)) throw new Exception("Delete failed");
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        private static JToken CopyAsset(JToken p)
        {
            if (p?["source_path"] == null || p["dest_path"] == null) throw new Exception("source_path and dest_path required");
            string sourcePath = ValidateAssetPath(p["source_path"].ToString());
            string destPath = ValidateAssetPath(p["dest_path"].ToString());
            if (!AssetDatabase.CopyAsset(sourcePath, destPath)) throw new Exception("Copy failed");
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        private static JToken GetDependencies(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            string path = ValidateAssetPath(p["path"].ToString());
            bool recursive = p["recursive"]?.Value<bool>() ?? true;
            var deps = AssetDatabase.GetDependencies(path, recursive);
            return new JObject { ["dependencies"] = new JArray(deps) };
        }

        private static JToken CreateFolder(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required (e.g., 'Assets/NewFolder')");
            string path = ValidateAssetPath(p["path"].ToString());
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid)) throw new Exception("Failed to create folder");
            return new JObject { ["status"] = "Success" };
        }
    }
}// Force Wed Apr  1 21:53:54 CEST 2026
