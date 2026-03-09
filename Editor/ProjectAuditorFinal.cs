using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static class ProjectAuditorWrapper
    {
        [MenuItem("Window/Nexus Unity/Run Full Project Audit")]
        public static void RunAuditMenu()
        {
            Debug.Log("[Nexus] Starting Full Project Audit...");
            string report = RunAudit(false);
            Debug.Log(report);
        }

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
                                result["num_total_issues"] = (int)report.GetType().GetProperty("NumTotalIssues").GetValue(report);
                                
                                var getAllIssuesMethod = report.GetType().GetMethod("GetAllIssues");
                                var allIssues = (System.Collections.IEnumerable)getAllIssuesMethod.Invoke(report, null);
                                
                                var codeIssues = new JArray();
                                if (allIssues != null)
                                {
                                    foreach (var issue in allIssues)
                                    {
                                        var i = new JObject();
                                        var t = issue.GetType();
                                        i["category"] = t.GetProperty("Category")?.GetValue(issue)?.ToString() ?? "Unknown";
                                        i["description"] = t.GetProperty("Description")?.GetValue(issue)?.ToString() ?? "No description";
                                        
                                        var location = t.GetProperty("Location")?.GetValue(issue);
                                        if (location != null)
                                        {
                                            var locType = location.GetType();
                                            i["file"] = locType.GetProperty("Path")?.GetValue(location)?.ToString();
                                            i["line"] = locType.GetProperty("Line")?.GetValue(location)?.ToString();
                                        }
                                        codeIssues.Add(i);
                                    }
                                }
                                result["code_issues"] = codeIssues;
                            }
                        }
                    }
                }
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
                if (go.scene == null || !go.scene.isLoaded) continue;

                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        issues.Add(new JObject {
                            ["type"] = "MissingScript",
                            ["object"] = go.name,
                            ["path"] = GetGameObjectPath(go),
                            ["description"] = "GameObject has a missing script reference."
                        });
                        continue;
                    }

                    if (comp is Renderer renderer)
                    {
                        foreach (var mat in renderer.sharedMaterials)
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
            string path = obj.name;
            var current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }
    }
}