using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Runs Unity Project Auditor and Nexus-specific scene health checks for local diagnostics.
    /// </summary>
    public static class ProjectAuditorWrapper
    {
        private static readonly List<Component> _componentCache = new List<Component>();
        private static readonly List<Renderer> _rendererCache = new List<Renderer>();
        private static readonly List<Material> _materialCache = new List<Material>();
        private static readonly Stack<string> _pathStackCache = new Stack<string>();

        /// <summary>
        /// Runs the full audit from the Unity menu and writes the report to the Nexus log channel.
        /// </summary>
        public static void RunAuditMenu()
        {
            NexusEditorLog.Log(NexusLogCategory.Audit, "[Nexus] Starting Full Project Audit...", true);
            string report = RunAudit(false);
            NexusEditorLog.Log(NexusLogCategory.Audit, report);
        }

        /// <summary>
        /// Builds a JSON audit report from Unity Project Auditor and active-scene health checks.
        /// </summary>
        public static string RunAudit(bool silent)
        {
            var result = new JObject();
            
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Contains("ProjectAuditor"));
                if (assembly != null)
                {
                    var auditorType = assembly.GetType("Unity.ProjectAuditor.Editor.ProjectAuditor");
                    var paramsType = assembly.GetType("Unity.ProjectAuditor.Editor.AnalysisParams");
                    
                    if (auditorType != null && paramsType != null)
                    {
                        var auditor = Activator.CreateInstance(auditorType);
                        var analysisParams = Activator.CreateInstance(paramsType, new object[] { true });
                        
                        var auditMethod = auditorType.GetMethods().FirstOrDefault(m => m.Name == "Audit" && m.GetParameters().Length == 2);
                        if (auditMethod != null)
                        {
                            var report = auditMethod.Invoke(auditor, new object[] { analysisParams, null });
                            if (report != null)
                            {
                                // Determine if we are in the Nexus sandbox or a user project
                                bool isSandbox = System.IO.Directory.Exists("Assets/NexusUnity");
                                string targetPath = isSandbox ? "Assets/NexusUnity" : "Assets";
                                NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Audit] START - isSandbox: {isSandbox}, targetPath: {targetPath}");
                                
                                var getAllIssuesMethod = report.GetType().GetMethod("GetAllIssues");
                                var allIssues = (System.Collections.IEnumerable)getAllIssuesMethod.Invoke(report, null);
                                
                                var codeIssues = new JArray();
                                if (allIssues != null)
                                {
                                    // Sandbox-specific noise reduction filters
                                    string[] sandboxIgnorePatterns = {
                                        "Newtonsoft.Json", "allocation", "usage", "System.Reflection", 
                                        "System.Linq", "System.String.Concat", "ref type", "Closure",
                                        "UnityEngine.Object.name", "Debug.Log", "Implicit", "GetEntityId"
                                    };

                                    foreach (var issue in allIssues)
                                    {
                                        var t = issue.GetType();
                                        string category = t.GetProperty("Category")?.GetValue(issue)?.ToString() ?? "Unknown";
                                        string description = t.GetProperty("Description")?.GetValue(issue)?.ToString() ?? "No description";
                                        
                                        var location = t.GetProperty("Location")?.GetValue(issue);
                                        string filePath = "";
                                        
                                        if (location != null)
                                        {
                                            var locType = location.GetType();
                                            filePath = locType.GetProperty("Path")?.GetValue(location)?.ToString() ?? "";
                                        }

                                        // 1. Path Filtering
                                        if (category.Contains("Code"))
                                        {
                                            if (string.IsNullOrEmpty(filePath) || !filePath.StartsWith(targetPath))
                                            {
                                                continue;
                                            }
                                            
                                            // 2. Sandbox Noise Reduction (Only active when developing Nexus)
                                            if (isSandbox)
                                            {
                                                bool shouldIgnore = false;
                                                foreach (var pattern in sandboxIgnorePatterns)
                                                {
                                                    if (description.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0) { shouldIgnore = true; break; }
                                                }
                                                if (shouldIgnore) continue;
                                            }
                                        }
                                        else
                                        {
                                            // Ignore general project noise (outdated packages, etc.) in the sandbox
                                            if (isSandbox) 
                                            {
                                                if (string.IsNullOrEmpty(filePath) || (!filePath.Contains("com.forkhorizon.nexus.unity") && !filePath.Contains("Assets/NexusUnity")))
                                                {
                                                    continue;
                                                }
                                            }
                                        }

                                        var i = new JObject();
                                        i["category"] = category;
                                        i["description"] = description;
                                        i["file"] = filePath;
                                        
                                        if (location != null)
                                        {
                                            var locType = location.GetType();
                                            i["line"] = locType.GetProperty("Line")?.GetValue(location)?.ToString();
                                        }
                                        
                                        codeIssues.Add(i);
                                    }
                                }
                                result["code_issues"] = codeIssues;
                                result["num_total_issues"] = codeIssues.Count;
                                NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Audit] END - Total Filtered: {codeIssues.Count}", true);
                            }
                        }
                    }
                }

                // --- Custom Nexus Style Audit ---
                string customTargetPath = System.IO.Directory.Exists("Assets/NexusUnity") ? "Assets/NexusUnity" : "Assets";
                var codeIssuesList = result["code_issues"] as JArray ?? new JArray();
                NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Style Audit] Scanning path: {customTargetPath}, current issues: {codeIssuesList.Count}");
                
                string[] files = System.IO.Directory.GetFiles(customTargetPath, "*.cs", System.IO.SearchOption.AllDirectories);
                int styleIssuesAdded = 0;
                foreach (var file in files)
                {
                    string relativePath = file.Replace(System.IO.Directory.GetCurrentDirectory() + "/", "").Replace("\\", "/");
                    if (relativePath.Contains("Assets/")) relativePath = relativePath.Substring(relativePath.IndexOf("Assets/"));

                    string[] lines = System.IO.File.ReadAllLines(file);
                    if (lines.Length > 300)
                    {
                        codeIssuesList.Add(new JObject {
                            ["category"] = "Style",
                            ["description"] = $"File exceeds 300 lines limit (Current: {lines.Length} lines).",
                            ["file"] = relativePath,
                            ["line"] = "1"
                        });
                        styleIssuesAdded++;
                    }
                }
                result["code_issues"] = codeIssuesList;
                result["num_total_issues"] = codeIssuesList.Count;
                NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Style Audit] Added {styleIssuesAdded} style issues. Total: {codeIssuesList.Count}", true);
            }
            catch (Exception e)
            {
                result["code_audit_error"] = e.Message;
            }

            var sceneIssues = new JArray();
            ScanSceneHealth(sceneIssues);
            result["scene_issues"] = sceneIssues;
            result["status"] = "Success";

            return result.ToString();
        }

        private static void ScanSceneHealth(JArray issues)
        {
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => (go.hideFlags & (HideFlags.HideInInspector | HideFlags.HideAndDontSave)) == 0);

            foreach (var go in allGOs)
            {
                // In Unity 6, go.scene is a struct. Check for validity and if it's loaded to only scan active scene objects.
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;

                int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                for (int i = 0; i < missingScripts; i++)
                {
                    issues.Add(new JObject {
                        ["type"] = "MissingScript",
                        ["object"] = go.name,
                        ["path"] = GetGameObjectPath(go),
                        ["description"] = "GameObject has a missing script reference."
                    });
                }

                go.GetComponents<Renderer>(_rendererCache);
                foreach (var renderer in _rendererCache)
                {
                    renderer.GetSharedMaterials(_materialCache);
                    foreach (var mat in _materialCache)
                    {
                        if (mat == null)
                        {
                            issues.Add(new JObject {
                                ["type"] = "MissingMaterial",
                                ["object"] = go.name,
                                ["path"] = GetGameObjectPath(go),
                                ["description"] = "Renderer has a null material entry."
                            });
                        }
                        else if (mat.shader != null && mat.shader.name == "Hidden/InternalErrorShader")
                        {
                            issues.Add(new JObject {
                                ["type"] = "PinkMaterial",
                                ["object"] = go.name,
                                ["path"] = GetGameObjectPath(go),
                                ["description"] = "Material is using the error shader (Pink)."
                            });
                        }
                    }
                }

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var status = PrefabUtility.GetPrefabInstanceStatus(go);
                    if (status == PrefabInstanceStatus.MissingAsset)
                    {
                        issues.Add(new JObject {
                            ["type"] = "BrokenPrefab",
                            ["object"] = go.name,
                            ["path"] = GetGameObjectPath(go),
                            ["description"] = "Prefab instance is missing its source asset."
                        });
                    }
                }
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            _pathStackCache.Clear();
            _pathStackCache.Push(obj.name);
            var current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                _pathStackCache.Push(current.name);
            }
            return string.Join("/", _pathStackCache);
        }
    }
}
