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
            string path = p["path"].ToString();
            
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
            string path = Path.Combine("Assets", $"{name}.mat");
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
            AssetDatabase.ImportAsset(p["path"].ToString());
            return new JObject { ["status"] = "Success", ["message"] = "Imported" };
        }

        private static JToken CreatePrefab(JToken p)
        {
            if (p == null || p["instance_id"] == null || p["path"] == null) throw new Exception("instance_id and path required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            PrefabUtility.SaveAsPrefabAsset(go, p["path"].ToString());
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = "Success", ["path"] = p["path"].ToString() };
        }

        private static JToken GetPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            JArray propertyMods = new JArray();
            var mods = PrefabUtility.GetPropertyModifications(go);
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    propertyMods.Add(new JObject {
                        ["target_type"] = mod.target?.GetType().Name,
                        ["property_path"] = mod.propertyPath,
                        ["value"] = mod.value
                    });
                }
            }

            JArray addedComps = new JArray();
            var added = PrefabUtility.GetAddedComponents(go);
            if (added != null)
            {
                foreach (var comp in added) addedComps.Add(comp.instanceComponent.GetType().Name);
            }

            JArray removedComps = new JArray();
            var removed = PrefabUtility.GetRemovedComponents(go);
            if (removed != null)
            {
                foreach (var comp in removed) removedComps.Add(comp.assetComponent.GetType().Name);
            }

            return new JObject {
                ["status"] = "Success",
                ["has_overrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(go, true),
                ["property_modifications"] = propertyMods,
                ["added_components"] = addedComps,
                ["removed_components"] = removedComps
            };
        }

        private static JToken ApplyPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            var overrides = GetPrefabOverrides(p);
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            
            var result = overrides as JObject;
            result["status"] = "Success";
            result["message"] = "Overrides applied to prefab asset";
            return result;
        }

        private static JToken RevertPrefabOverrides(JToken p)
        {
            if (p == null || p["instance_id"] == null) throw new Exception("instance_id required");
            var go = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p)) as GameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) throw new Exception("GameObject is not a prefab instance");

            var overrides = GetPrefabOverrides(p);
            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
            
            var result = overrides as JObject;
            result["status"] = "Success";
            result["message"] = "Overrides reverted to prefab asset defaults";
            return result;
        }

        private static JToken EditPrefabAsset(JToken p)
        {
            if (p == null || p["path"] == null || p["properties"] == null) 
                throw new Exception("path and properties are required");

            string path = p["path"].ToString();
            JObject data = p["properties"] as JObject;
            string compName = p["component_name"]?.ToString();

            var go = PrefabUtility.LoadPrefabContents(path);
            if (go == null) throw new Exception($"Could not load prefab at {path}");

            try
            {
                UnityEngine.Object target = go;
                if (!string.IsNullOrEmpty(compName))
                {
                    target = go.GetComponent(compName);
                    if (target == null) throw new Exception($"Component {compName} not found on prefab");
                }

                SerializedObject so = new SerializedObject(target);
                JArray errors = new JArray();
                int updatedCount = 0;

                foreach (var propPair in data)
                {
                    SerializedProperty prop = so.FindProperty(propPair.Key);
                    if (prop == null) 
                    {
                        string cleanName = propPair.Key.StartsWith("m_") ? propPair.Key : "m_" + char.ToUpper(propPair.Key[0]) + propPair.Key.Substring(1);
                        prop = so.FindProperty(cleanName);
                    }

                    if (prop != null)
                    {
                        try { ApplyValueToSerializedPropertyAsset(prop, propPair.Value); updatedCount++; }
                        catch (Exception e) { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = e.Message }); }
                    }
                    else { errors.Add(new JObject { ["field"] = propPair.Key, ["error"] = "Property not found" }); }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                
                return new JObject 
                { 
                    ["status"] = errors.Count == 0 ? "Success" : (updatedCount > 0 ? "Partial" : "Failed"),
                    ["updated_count"] = updatedCount,
                    ["errors"] = errors,
                    ["message"] = "Prefab asset updated directly"
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }

        // Helper for isolated prefab asset edits
        private static void ApplyValueToSerializedPropertyAsset(SerializedProperty prop, JToken val)
        {
            if (val.Type == JTokenType.Boolean) prop.boolValue = val.Value<bool>();
            else if (val.Type == JTokenType.Float) prop.floatValue = val.Value<float>();
            else if (val.Type == JTokenType.Integer) prop.intValue = val.Value<int>();
            else if (val.Type == JTokenType.String) prop.stringValue = val.Value<string>();
            else if (val.Type == JTokenType.Object && val["x"] != null)
            {
                prop.vector3Value = new Vector3(val["x"].Value<float>(), val["y"].Value<float>(), val["z"].Value<float>());
            }
            else throw new Exception("Value type not supported for surgical asset edit yet");
        }

        private static JToken MoveAsset(JToken p)
        {
            if (p?["old_path"] == null || p["new_path"] == null) throw new Exception("old_path and new_path required");
            
            string oldPath = p["old_path"].ToString();
            string newPath = p["new_path"].ToString();

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
            if (!AssetDatabase.DeleteAsset(p["path"].ToString())) throw new Exception("Delete failed");
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        private static JToken CopyAsset(JToken p)
        {
            if (p?["source_path"] == null || p["dest_path"] == null) throw new Exception("source_path and dest_path required");
            if (!AssetDatabase.CopyAsset(p["source_path"].ToString(), p["dest_path"].ToString())) throw new Exception("Copy failed");
            return new JObject { ["status"] = "Success", ["message"] = "OK" };
        }

        private static JToken GetDependencies(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required");
            bool recursive = p["recursive"]?.Value<bool>() ?? true;
            var deps = AssetDatabase.GetDependencies(p["path"].ToString(), recursive);
            return new JObject { ["dependencies"] = new JArray(deps) };
        }

        private static JToken CreateFolder(JToken p)
        {
            if (p?["path"] == null) throw new Exception("path required (e.g., 'Assets/NewFolder')");
            string path = p["path"].ToString();
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid)) throw new Exception("Failed to create folder");
            return new JObject { ["status"] = "Success" };
        }
    }
}// Force Wed Apr  1 21:53:54 CEST 2026
