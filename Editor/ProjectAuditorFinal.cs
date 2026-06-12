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
    /// Runs Unity Project Auditor through reflection and adds Nexus Unity scene/style diagnostics for local editor health checks.
    /// </summary>
    /// <remarks>
    /// Audit execution dynamically locates ProjectAuditor assemblies, filters issues for the package or user project,
    /// scans active scene objects for missing scripts/materials, writes Nexus audit logs, and returns a JSON report.
    /// </remarks>
    public static class ProjectAuditorWrapper
    {
        private const string ProjectAuditorRulesPackageName = "com.unity.project-auditor-rules";

        private static readonly List<Component> _componentCache = new List<Component>();
        private static readonly List<Renderer> _rendererCache = new List<Renderer>();
        private static readonly List<Material> _materialCache = new List<Material>();
        private static readonly Stack<string> _pathStackCache = new Stack<string>();

        /// <summary>
        /// Runs the full audit from a Unity menu context and writes the JSON report to the Nexus audit log channel.
        /// </summary>
        public static void RunAuditMenu()
        {
            NexusEditorLog.Log(NexusLogCategory.Audit, "[Nexus] Starting Full Project Audit...", true);
            string report = RunAudit(false);
            NexusEditorLog.Log(NexusLogCategory.Audit, report);
        }

        /// <summary>
        /// Builds a JSON audit report from reflected Unity Project Auditor data, Nexus style checks, and active-scene health checks.
        /// </summary>
        /// <remarks>
        /// The method may load auditor types via reflection, inspect filesystem paths to decide package/user-project scope,
        /// read C# files for style limits, log audit progress, and catch auditor exceptions into the returned JSON payload.
        /// </remarks>
        public static string RunAudit(bool silent)
        {
            var result = new JObject();
            
            try
            {
                RunUnityProjectAuditor(result, silent);
                RunNexusStyleAudit(result);
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

        private static void RunUnityProjectAuditor(JObject result, bool silent)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Contains("ProjectAuditor"));
            if (assembly == null || !ShouldRunUnityProjectAuditor(silent))
            {
                return;
            }

            var auditorType = assembly.GetType("Unity.ProjectAuditor.Editor.ProjectAuditor");
            var paramsType = assembly.GetType("Unity.ProjectAuditor.Editor.AnalysisParams");
            if (auditorType == null || paramsType == null)
            {
                return;
            }

            var auditMethod = auditorType.GetMethods().FirstOrDefault(m => m.Name == "Audit" && m.GetParameters().Length == 2);
            if (auditMethod == null)
            {
                return;
            }

            var auditor = Activator.CreateInstance(auditorType);
            var analysisParams = Activator.CreateInstance(paramsType, new object[] { true });
            var report = auditMethod.Invoke(auditor, new object[] { analysisParams, null });
            if (report == null)
            {
                return;
            }

            bool isSandbox = Directory.Exists("Assets/NexusUnity");
            string targetPath = isSandbox ? "Assets/NexusUnity" : "Assets";
            NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Audit] START - isSandbox: {isSandbox}, targetPath: {targetPath}");

            var getAllIssuesMethod = report.GetType().GetMethod("GetAllIssues");
            var allIssues = (System.Collections.IEnumerable)getAllIssuesMethod.Invoke(report, null);
            var codeIssues = CollectProjectAuditorIssues(allIssues, isSandbox, targetPath);
            result["code_issues"] = codeIssues;
            result["num_total_issues"] = codeIssues.Count;
            NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Audit] END - Total Filtered: {codeIssues.Count}", true);
        }

        private static bool ShouldRunUnityProjectAuditor(bool silent)
        {
            bool hasRulesPackage = IsPackageResolved(ProjectAuditorRulesPackageName);
            if (!hasRulesPackage && !silent)
            {
                NexusEditorLog.Log(NexusLogCategory.Audit, "[Nexus Audit] Skipping Unity Project Auditor because no Project Auditor rules package is resolved.");
            }

            return hasRulesPackage;
        }

        private static bool IsPackageResolved(string packageName)
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static JArray CollectProjectAuditorIssues(System.Collections.IEnumerable allIssues, bool isSandbox, string targetPath)
        {
            var codeIssues = new JArray();
            if (allIssues == null)
            {
                return codeIssues;
            }

            string[] sandboxIgnorePatterns = { "Newtonsoft.Json", "allocation", "usage", "System.Reflection", "System.Linq", "System.String.Concat", "ref type", "Closure", "UnityEngine.Object.name", "Debug.Log", "Implicit", "GetEntityId" };

            foreach (var issue in allIssues)
            {
                AddProjectAuditorIssue(codeIssues, issue, isSandbox, targetPath, sandboxIgnorePatterns);
            }

            return codeIssues;
        }

        private static void AddProjectAuditorIssue(JArray codeIssues, object issue, bool isSandbox, string targetPath, string[] sandboxIgnorePatterns)
        {
            var t = issue.GetType();
            string category = t.GetProperty("Category")?.GetValue(issue)?.ToString() ?? "Unknown";
            string description = t.GetProperty("Description")?.GetValue(issue)?.ToString() ?? "No description";
            var location = t.GetProperty("Location")?.GetValue(issue);
            string filePath = GetAuditorIssuePath(location);

            if (ShouldSkipAuditorIssue(category, description, filePath, isSandbox, targetPath, sandboxIgnorePatterns))
            {
                return;
            }

            var i = new JObject { ["category"] = category, ["description"] = description, ["file"] = filePath };
            if (location != null)
            {
                var locType = location.GetType();
                i["line"] = locType.GetProperty("Line")?.GetValue(location)?.ToString();
            }

            codeIssues.Add(i);
        }

        private static string GetAuditorIssuePath(object location)
        {
            return location?.GetType().GetProperty("Path")?.GetValue(location)?.ToString() ?? "";
        }

        private static bool ShouldSkipAuditorIssue(string category, string description, string filePath, bool isSandbox, string targetPath, string[] sandboxIgnorePatterns)
        {
            if (category.Contains("Code"))
            {
                if (string.IsNullOrEmpty(filePath) || !filePath.StartsWith(targetPath))
                {
                    return true;
                }

                return isSandbox && sandboxIgnorePatterns.Any(pattern => description.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return isSandbox && (string.IsNullOrEmpty(filePath) || (!filePath.Contains("com.forkhorizon.nexus.unity") && !filePath.Contains("Assets/NexusUnity")));
        }

        private static void RunNexusStyleAudit(JObject result)
        {
            string customTargetPath = Directory.Exists("Assets/NexusUnity") ? "Assets/NexusUnity" : "Assets";
            var codeIssuesList = result["code_issues"] as JArray ?? new JArray();
            NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Style Audit] Scanning path: {customTargetPath}, current issues: {codeIssuesList.Count}");

            string[] files = Directory.GetFiles(customTargetPath, "*.cs", SearchOption.AllDirectories);
            int styleIssuesAdded = 0;
            foreach (var file in files)
            {
                string relativePath = file.Replace(Directory.GetCurrentDirectory() + "/", "").Replace("\\", "/");
                if (relativePath.Contains("Assets/")) relativePath = relativePath.Substring(relativePath.IndexOf("Assets/"));

                string[] lines = File.ReadAllLines(file);
                if (lines.Length <= 300)
                {
                    continue;
                }

                codeIssuesList.Add(new JObject { ["category"] = "Style", ["description"] = $"File exceeds 300 lines limit (Current: {lines.Length} lines).", ["file"] = relativePath, ["line"] = "1" });
                styleIssuesAdded++;
            }

            result["code_issues"] = codeIssuesList;
            result["num_total_issues"] = codeIssuesList.Count;
            NexusEditorLog.Log(NexusLogCategory.Audit, $"[Nexus Style Audit] Added {styleIssuesAdded} style issues. Total: {codeIssuesList.Count}", true);
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
