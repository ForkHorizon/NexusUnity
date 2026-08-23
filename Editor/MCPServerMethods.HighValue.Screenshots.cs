using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    public static partial class MCPServerMethods
    {
        private static JToken CaptureGameViewScreenshot(JToken p)
        {
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().Name == "GameView");
            if (gameView == null) throw new Exception("Game View window not found or not open.");

            gameView.Focus();
            gameView.Repaint();
            Rect position = gameView.position;
            string tempPath = Path.Combine(Path.GetTempPath(), $"unity_gameview_{DateTime.Now.Ticks}.png");
            NexusEditorLog.Log(NexusLogCategory.UiAutomation, $"[MCP_SCREENSHOT] Capturing GameView at {position} to {tempPath}");
            CaptureGameViewImage(tempPath, position);
            return ReadGameViewImage(tempPath);
        }

        private static void CaptureGameViewImage(string tempPath, Rect position)
        {
#if UNITY_EDITOR_OSX
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-x -R{(int)position.x},{(int)position.y},{(int)position.width},{(int)position.height} \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                string error = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                    NexusEditorLog.Error(NexusLogCategory.UiAutomation, $"[MCP_SCREENSHOT] screencapture failed with exit code {process.ExitCode}. Error: {error}");
            }
#else
            ScreenCapture.CaptureScreenshot(tempPath);
            for (int retries = 0; !File.Exists(tempPath) && retries < 20; retries++)
                System.Threading.Thread.Sleep(100);
#endif
        }

        private static JObject ReadGameViewImage(string tempPath)
        {
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
            return CaptureInspectorScreenshotOnMac(p);
#endif
        }

#if UNITY_EDITOR_OSX
        private static JObject CaptureInspectorScreenshotOnMac(JToken p)
        {
            SelectInspectorTarget(p);
            var inspector = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.titleContent.text == "Inspector");
            if (inspector == null) throw new Exception("Inspector window not found or not open.");

            inspector.Focus();
            inspector.Repaint();
            var layout = SerializeVisualElement(inspector.rootVisualElement, true);
            string tempPath = Path.Combine(Path.GetTempPath(), $"unity_inspector_{DateTime.Now.Ticks}.png");
            CaptureInspectorImage(tempPath, inspector.position);
            if (!File.Exists(tempPath))
                return new JObject { ["status"] = "PartialSuccess", ["message"] = "Screenshot failed (permissions?), but UI layout was captured.", ["ui_layout"] = layout };

            byte[] bytes = File.ReadAllBytes(tempPath);
            File.Delete(tempPath);
            return new JObject { ["status"] = "Success", ["image_base64"] = Convert.ToBase64String(bytes), ["format"] = "png", ["ui_layout"] = layout };
        }

        private static void SelectInspectorTarget(JToken p)
        {
            if (p?["instance_id"] == null) return;
            var target = MCPServerMethods.IdToObject(MCPServerMethods.ExtractId(p));
            if (target != null) Selection.activeObject = target;
        }

        private static void CaptureInspectorImage(string tempPath, Rect position)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-x -R{(int)position.x},{(int)position.y},{(int)position.width},{(int)position.height} \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
                process.WaitForExit();
        }
#endif
    }
}
