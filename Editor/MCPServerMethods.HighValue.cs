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

        private static JToken CaptureGameViewScreenshot(JToken p)
        {
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => w.GetType().Name == "GameView");

            if (gameView == null) throw new Exception("Game View window not found or not open.");

            gameView.Focus();
            gameView.Repaint();

            Rect pos = gameView.position;
            // On macOS, the top bar is offset. screencapture -R uses Screen coordinates.
            // We need to account for Unity's window header if needed, but usually gameView.position is the whole window.
            int x = (int)pos.x;
            int y = (int)pos.y;
            int w = (int)pos.width;
            int h = (int)pos.height;

            string tempPath = Path.Combine(Path.GetTempPath(), $"unity_gameview_{DateTime.Now.Ticks}.png");
            Debug.Log($"[MCP_SCREENSHOT] Capturing GameView at {x},{y},{w},{h} to {tempPath}");

            #if UNITY_EDITOR_OSX
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-x -R{x},{y},{w},{h} \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0) Debug.LogError($"[MCP_SCREENSHOT] screencapture failed with exit code {process.ExitCode}. Error: {error}");
            }
            #else
            ScreenCapture.CaptureScreenshot(tempPath);
            int retries = 0;
            while (!File.Exists(tempPath) && retries < 20)
            {
                System.Threading.Thread.Sleep(100);
                retries++;
            }
            #endif

            if (!File.Exists(tempPath)) throw new Exception("Failed to capture Game View screenshot.");

            byte[] bytes = File.ReadAllBytes(tempPath);
            File.Delete(tempPath);

            return new JObject 
            { 
                ["status"] = "Success", 
                ["image_base64"] = Convert.ToBase64String(bytes),
                ["format"] = "png"
            };
        }

        private static JToken CaptureInspectorScreenshot(JToken p)
        {
            #if !UNITY_EDITOR_OSX
            throw new Exception("Inspector screenshot is currently only supported on macOS.");
            #else
            if (p?["instance_id"] != null)
            {
                var target = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
                if (target != null) Selection.activeObject = target;
            }

            var inspector = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => w.titleContent.text == "Inspector");
            
            if (inspector == null) throw new Exception("Inspector window not found or not open.");

            inspector.Focus();
            inspector.Repaint();

            // Always capture the UI layout as it is higher fidelity for AI
            var layout = SerializeVisualElement(inspector.rootVisualElement, true);

            Rect pos = inspector.position;
            int x = (int)pos.x;
            int y = (int)pos.y;
            int w = (int)pos.width;
            int h = (int)pos.height;

            string tempPath = Path.Combine(Path.GetTempPath(), $"unity_inspector_{DateTime.Now.Ticks}.png");
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-x -R{x},{y},{w},{h} \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
            }

            if (!File.Exists(tempPath))
            {
                return new JObject 
                { 
                    ["status"] = "PartialSuccess", 
                    ["message"] = "Screenshot failed (permissions?), but UI layout was captured.",
                    ["ui_layout"] = layout
                };
            }

            byte[] bytes = File.ReadAllBytes(tempPath);
            File.Delete(tempPath);

            return new JObject 
            { 
                ["status"] = "Success", 
                ["image_base64"] = Convert.ToBase64String(bytes),
                ["format"] = "png",
                ["ui_layout"] = layout
            };
            #endif
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
            string nodeId = "node_" + id.ToString();
            sb.AppendLine($"  {nodeId}[\"{safeName}\"]");

            using (UnityEngine.Pool.ListPool<Component>.Get(out var comps))
            {
                go.GetComponents(comps);
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    string compName = comp.GetType().Name;
                    if (compName == "Transform" || compName == "RectTransform") continue;
                    string compId = "comp_" + comp.GetRawId().ToString();
                    sb.AppendLine($"  {nodeId} --- {compId}([\"{compName}\"])");
                }
            }

            foreach (Transform child in go.transform)
            {
                string childId = "node_" + child.gameObject.GetRawId().ToString();
                sb.AppendLine($"  {nodeId} --> {childId}");
                BuildMermaidRecursive(child.gameObject, sb, processed);
            }
        }

        private static JToken SemanticFind(JToken p)
        {
            string query = p?["query"]?.ToString();
            if (string.IsNullOrEmpty(query)) throw new Exception("query parameter required");

            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.NotEditable);

            var matches = new List<JObject>();
            string lowerQuery = query.ToLower();

            using (UnityEngine.Pool.ListPool<Component>.Get(out var comps))
            {
                foreach (var go in allGOs)
                {
                    if (go.scene == null || !go.scene.isLoaded) continue;

                    int score = 0;
                    List<string> reasons = new List<string>();

                    if (go.name.ToLower().Contains(lowerQuery))
                    {
                        score += 50;
                        reasons.Add("Name match");
                    }

                    go.GetComponents(comps);
                    foreach (var comp in comps)
                    {
                        if (comp == null) continue;
                        var type = comp.GetType();
                        string typeName = type.Name;

                        if (typeName.ToLower().Contains(lowerQuery))
                        {
                            score += 30;
                            reasons.Add($"Component type match: {typeName}");
                        }

                        // Scan fields
                        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        foreach (var f in fields)
                        {
                            if (f.Name.ToLower().Contains(lowerQuery))
                            {
                                score += 10;
                                reasons.Add($"Field match: {typeName}.{f.Name}");
                            }
                        }

                        // Scan methods (Behavioral scanning)
                        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                        foreach (var m in methods)
                        {
                            if (m.Name.ToLower().Contains(lowerQuery))
                            {
                                score += 15;
                                reasons.Add($"Method match: {typeName}.{m.Name}()");
                            }
                        }
                    }

                    if (score > 0)
                    {
                        matches.Add(new JObject
                        {
                            ["name"] = go.name,
                            ["instance_id"] = go.GetRawId(),
                            ["score"] = score,
                            ["reasons"] = new JArray(reasons.Distinct())
                        });
                    }
                }
            }

            return new JObject 
            { 
                ["status"] = "Success", 
                ["matches"] = new JArray(matches.OrderByDescending(m => (int)m["score"]).Take(20)) 
            };
        }
    }
}