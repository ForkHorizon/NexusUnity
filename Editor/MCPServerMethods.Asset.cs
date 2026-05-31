using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registers JSON-RPC methods that manipulate Unity assets and project files through <see cref="AssetDatabase"/>.
    /// </summary>
    /// <remarks>
    /// These methods move, copy, delete, import, and refresh assets; create folders, materials, and prefabs; and inspect prefab
    /// overrides/dependencies. They can write to the project filesystem, dirty assets, trigger AssetDatabase imports, and affect prefab state.
    /// </remarks>
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
            var shaderAsset = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shaderAsset == null) throw new Exception($"Shader '{shader}' not found and no supported fallback shader is available");

            Material mat = new Material(shaderAsset);
            ApplyOptionalMaterialColor(mat, p["base_color"] ?? p["color"], false);
            ApplyOptionalMaterialColor(mat, p["emission_color"] ?? (p["emission"]?.Type == JTokenType.Boolean ? null : p["emission"]), true);

            string path = p["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine("Assets", $"{name}.mat");
            }
            else if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                path += ".mat";
            }
            path = ValidateAssetPath(path);
            string fullPath = ValidatePath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            mat.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return new JObject { ["status"] = "Success", ["path"] = path };
        }

        private static void ApplyOptionalMaterialColor(Material mat, JToken token, bool emission)
        {
            if (token == null) return;

            Color color = ParseColorToken(token);
            string property = emission ? "_EmissionColor" : (mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
            if (!mat.HasProperty(property)) return;

            mat.SetColor(property, color);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        private static Color ParseColorToken(JToken token)
        {
            if (token.Type == JTokenType.String)
            {
                string value = token.ToString();
                if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
                if (ColorUtility.TryParseHtmlString(value, out Color parsed)) return parsed;
                throw new Exception($"Invalid color string: {token}");
            }

            if (token.Type == JTokenType.Object)
            {
                float r = token["r"]?.Value<float>() ?? 1f;
                float g = token["g"]?.Value<float>() ?? 1f;
                float b = token["b"]?.Value<float>() ?? 1f;
                float a = token["a"]?.Value<float>() ?? 1f;
                return new Color(r, g, b, a);
            }

            throw new Exception("Color must be a hex string or object with r/g/b/a fields");
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
